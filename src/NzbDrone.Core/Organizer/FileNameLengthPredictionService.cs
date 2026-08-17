using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Organizer
{
    public interface IPredictFileNameLength
    {
        FileNameLengthPrediction Predict(NamingConfig nameSpec);
    }

    /// <summary>
    /// Every part measured separately, because they are fixed in different places and capped in different
    /// ways. The three folder and file names are each a single component, and it is a component that a file
    /// system caps; the whole path is capped by whatever is doing the reading.
    /// </summary>
    public class FileNameLengthPrediction
    {
        public string SeriesTitle { get; set; }
        public string EpisodeTitle { get; set; }
        public string Path { get; set; }
        public int FileNameLength { get; set; }
        public int SeasonFolderLength { get; set; }
        public int SeriesFolderLength { get; set; }

        /// <summary>
        /// Series folder, season folder and file name together - what the library holds under its root
        /// folder, and the part a naming format can actually change.
        /// </summary>
        public int RelativePathLength { get; set; }

        /// <summary>
        /// The above with the root folder in front of it, which is what anything opening the file has to
        /// carry.
        /// </summary>
        public int FullPathLength { get; set; }

        public int NameLimit { get; set; }
        public int PathLimit { get; set; }
        public bool NameTooLong { get; set; }
        public bool PathTooLong { get; set; }
    }

    /// <summary>
    /// How long the names a format produces can get, answered before the format is saved rather than after
    /// a rename has been carried out on tens of thousands of files.
    ///
    /// The samples beside each format box are built from an invented series with a short title, so a format
    /// can look comfortable there and still produce something nothing outside Sonarr can open. This keeps
    /// the invented names - a settings page naming real series would be picking on whichever one happened
    /// to be worst - but stretches them to the longest series title, longest episode title and longest root
    /// folder the library actually holds. Nothing in the library can come out longer than the answer.
    /// </summary>
    public class FileNameLengthPredictionService : IPredictFileNameLength
    {
        private const string TitleFiller = " The Title Goes On";

        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IConfigService _configService;
        private readonly ICached<LibraryExtremes> _cache;
        private readonly Logger _logger;

        public FileNameLengthPredictionService(ISeriesService seriesService,
                                               IEpisodeService episodeService,
                                               IBuildFileNames fileNameBuilder,
                                               IConfigService configService,
                                               ICacheManager cacheManager,
                                               Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _fileNameBuilder = fileNameBuilder;
            _configService = configService;
            _cache = cacheManager.GetCache<LibraryExtremes>(GetType());
            _logger = logger;
        }

        public FileNameLengthPrediction Predict(NamingConfig nameSpec)
        {
            // Measured whether or not a limit is set. The lengths are the answer to "how long does this
            // format get", which is worth knowing before there is a limit to compare it to; a limit only
            // decides what gets marked as past it.
            var nameLimit = _configService.FileNameLengthLimit;
            var pathLimit = _configService.FilePathLengthLimit;

            try
            {
                var extremes = Extremes();
                var series = WorstCaseSeries(extremes);
                var episodes = new List<Episode> { WorstCaseEpisode(extremes) };

                // Composed here rather than through BuildFilePath, which takes the folders from the saved
                // config: the whole point is to answer for the format being typed, and that is all three
                // formats, not just the one for the file name.
                var fileName = _fileNameBuilder.BuildFileName(episodes, series, WorstCaseFile(), ".mkv", nameSpec);
                var seriesFolder = BuildFolder(_fileNameBuilder.GetSeriesFolder(series, nameSpec), series.Title);
                var seasonFolder = BuildFolder(_fileNameBuilder.GetSeasonFolder(series, 1, nameSpec), string.Empty);

                var relativePath = seasonFolder.IsNullOrWhiteSpace()
                    ? System.IO.Path.Combine(seriesFolder, fileName)
                    : System.IO.Path.Combine(seriesFolder, seasonFolder, fileName);

                var fullPath = System.IO.Path.Combine(extremes.RootFolder, relativePath);

                var fileNameLength = fileName.GetByteCount();
                var seasonFolderLength = seasonFolder.GetByteCount();
                var seriesFolderLength = seriesFolder.GetByteCount();
                var fullPathLength = fullPath.GetByteCount();

                // The name limit is a limit on one component, so every component is measured against it and
                // any one of them being past is enough to be worth saying.
                var longestComponent = Math.Max(fileNameLength, Math.Max(seasonFolderLength, seriesFolderLength));

                return new FileNameLengthPrediction
                {
                    SeriesTitle = series.Title,
                    EpisodeTitle = episodes.First().Title,
                    Path = fullPath,
                    FileNameLength = fileNameLength,
                    SeasonFolderLength = seasonFolderLength,
                    SeriesFolderLength = seriesFolderLength,
                    RelativePathLength = relativePath.GetByteCount(),
                    FullPathLength = fullPathLength,
                    NameLimit = nameLimit,
                    PathLimit = pathLimit,
                    NameTooLong = nameLimit > 0 && longestComponent > nameLimit,
                    PathTooLong = pathLimit > 0 && fullPathLength > pathLimit
                };
            }
            catch (Exception ex)
            {
                // A format that cannot be built is already reported by the validation beside it, and a
                // prediction is not worth failing the settings page over.
                _logger.Debug(ex, "Unable to predict the length of a file name");

                return null;
            }
        }

        /// <summary>
        /// An empty format is not an error here the way it is for a file name: it means the folder is left
        /// as it is, so there is nothing for a format to make longer.
        /// </summary>
        private static string BuildFolder(string folder, string fallback)
        {
            return folder.IsNullOrWhiteSpace() ? fallback : FileNameBuilder.CleanFileName(folder);
        }

        /// <summary>
        /// The invented title stretched to a given length. Latin letters only, so a character is a byte and
        /// the result comes out at exactly the length asked for; a title of repeated Thai would be no more
        /// readable for being accurate about which script it padded with.
        /// </summary>
        private static string StretchTitle(string title, int lengthInBytes)
        {
            if (lengthInBytes <= title.GetByteCount())
            {
                return title;
            }

            var stretched = new StringBuilder(title);

            while (stretched.Length < lengthInBytes)
            {
                stretched.Append(TitleFiller);
            }

            return stretched.ToString().Substring(0, lengthInBytes);
        }

        /// <summary>
        /// The longest series title, episode title and root folder the library holds. Held for a few
        /// minutes because the answer only moves when series are added, and the settings page asks again on
        /// every keystroke.
        /// </summary>
        private LibraryExtremes Extremes()
        {
            return _cache.Get("extremes", FindExtremes, TimeSpan.FromMinutes(5));
        }

        private LibraryExtremes FindExtremes()
        {
            var series = _seriesService.GetAllSeries();

            // The root folder is the one part of a path no naming format can shorten, so the longest one in
            // use is what the worst case has to sit under.
            var rootFolder = series.Select(s => s.Path.GetParentPath())
                                   .Where(p => p.IsNotNullOrWhiteSpace())
                                   .OrderByDescending(p => p.GetByteCount())
                                   .FirstOrDefault() ?? string.Empty;

            return new LibraryExtremes
            {
                SeriesTitleLength = series.Select(s => s.Title?.GetByteCount() ?? 0).DefaultIfEmpty(0).Max(),
                EpisodeTitleLength = _episodeService.AllEpisodesWithFiles()
                                                    .Select(e => e.Title?.GetByteCount() ?? 0)
                                                    .DefaultIfEmpty(0)
                                                    .Max(),
                RootFolder = rootFolder
            };
        }

        private static Series WorstCaseSeries(LibraryExtremes extremes)
        {
            var title = StretchTitle("The Series Title's!", extremes.SeriesTitleLength);

            return new Series
            {
                SeriesType = SeriesTypes.Standard,
                Title = title,
                Year = 2010,
                ImdbId = "tt12345",
                TvdbId = 12345,
                TvMazeId = 54321,
                TmdbId = 11223,
                SeasonFolder = true,
                Path = System.IO.Path.Combine(extremes.RootFolder, title)
            };
        }

        private static Episode WorstCaseEpisode(LibraryExtremes extremes)
        {
            return new Episode
            {
                SeasonNumber = 1,
                EpisodeNumber = 1,
                AbsoluteEpisodeNumber = 1,
                AirDate = "2013-10-30",
                Title = StretchTitle("Episode Title", extremes.EpisodeTitleLength)
            };
        }

        /// <summary>
        /// The same invented file the samples beside each format box are built from, so the quality,
        /// release group and media info tokens come out the length they do there.
        /// </summary>
        private static EpisodeFile WorstCaseFile()
        {
            return new EpisodeFile
            {
                Quality = new QualityModel(Quality.WEBDL1080p, new Revision(2)),
                RelativePath = "The.Series.Title's!.S01E01.1080p.WEBDL.x264-EVOLVE.mkv",
                SceneName = "The.Series.Title's!.S01E01.1080p.WEBDL.x264-EVOLVE",
                ReleaseGroup = "RlsGrp",
                MediaInfo = new MediaInfoModel
                {
                    VideoFormat = "AVC",
                    VideoBitDepth = 10,
                    VideoColourPrimaries = "bt2020",
                    VideoTransferCharacteristics = "HLG",
                    AudioFormat = "DTS",
                    AudioChannels = 6,
                    AudioChannelPositions = "5.1",
                    AudioLanguages = new List<string> { "ger" },
                    Subtitles = new List<string> { "eng", "ger" }
                }
            };
        }

        internal class LibraryExtremes
        {
            public int SeriesTitleLength { get; set; }
            public int EpisodeTitleLength { get; set; }
            public string RootFolder { get; set; }
        }
    }
}

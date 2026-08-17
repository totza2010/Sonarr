using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IFileNameLengthService
    {
        List<OverlongFile> GetOverlongFiles();
        List<OverlongFile> Scan();
    }

    public class OverlongFile
    {
        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public string RelativePath { get; set; }
        public int NameLength { get; set; }
        public int PathLength { get; set; }

        /// <summary>
        /// Which ceiling this file went past. Both are worth knowing apart: a name is capped per
        /// component by the file system, a path by whatever is doing the reading.
        /// </summary>
        public bool NameTooLong { get; set; }
        public bool PathTooLong { get; set; }
    }

    /// <summary>
    /// Finds files whose names are longer than whatever is reading the library can cope with. Sonarr is
    /// more tolerant than most of what comes after it - a player, an SMB share, a cloud mount - so a name
    /// it handles happily can still be one nothing else can open, and nothing says so until somebody
    /// tries.
    ///
    /// Measured in bytes rather than characters, which is what the limit is on the file systems that
    /// have one: a Thai title costs three bytes a character, so 255 bytes is about 85 characters.
    /// </summary>
    public class FileNameLengthService : IFileNameLengthService,
                                        IExecute<CheckFileNameLengthsCommand>,
                                        IHandle<SeriesRefreshCompleteEvent>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly ISeriesService _seriesService;
        private readonly IConfigService _configService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICached<List<OverlongFile>> _cache;
        private readonly Logger _logger;

        public FileNameLengthService(IMediaFileService mediaFileService,
                                     ISeriesService seriesService,
                                     IConfigService configService,
                                     IEventAggregator eventAggregator,
                                     ICacheManager cacheManager,
                                     Logger logger)
        {
            _mediaFileService = mediaFileService;
            _seriesService = seriesService;
            _configService = configService;
            _eventAggregator = eventAggregator;
            _cache = cacheManager.GetCache<List<OverlongFile>>(GetType());
            _logger = logger;
        }

        /// <summary>
        /// What the last scan found. Held in memory rather than stored: it is worked out from rows that
        /// are already in the database, so it costs nothing to find again, and there is nothing to tidy
        /// up when a series is deleted.
        /// </summary>
        public List<OverlongFile> GetOverlongFiles()
        {
            return _cache.Find("overlong") ?? new List<OverlongFile>();
        }

        public List<OverlongFile> Scan()
        {
            var limit = _configService.FileNameLengthLimit;
            var pathLimit = _configService.FilePathLengthLimit;

            if (limit <= 0 && pathLimit <= 0)
            {
                _cache.Set("overlong", new List<OverlongFile>());

                return new List<OverlongFile>();
            }

            var series = _seriesService.GetAllSeries().ToDictionary(s => s.Id);
            var files = _mediaFileService.GetAllFiles();
            var overlong = new List<OverlongFile>();

            _logger.ProgressInfo("Checking the length of {0} file names and paths", files.Count);

            // Walked a series at a time so there is something to say while it runs, the way a library scan
            // names what it is on. Grouped rather than taken in the order the rows came back, since that
            // order is the database's and would jump between series.
            var bySeries = files.Where(f => f.RelativePath.IsNotNullOrWhiteSpace() && series.ContainsKey(f.SeriesId))
                                .GroupBy(f => f.SeriesId)
                                .ToList();

            var checkedSoFar = 0;

            foreach (var group in bySeries)
            {
                var seriesForFiles = series[group.Key];

                checkedSoFar += group.Count();

                // Debug rather than info: the corner of the screen wants every series, the event list wants
                // only the two lines that bracket the run.
                _logger.ProgressDebug("Checking {0} ({1} of {2} files)", seriesForFiles.Path, checkedSoFar, files.Count);

                foreach (var file in group)
                {
                    // Both ceilings, because they are different limits in different places: the file system
                    // caps a single name, while whatever reads the library caps the path that reaches it.
                    var nameLength = Path.GetFileName(file.RelativePath).GetByteCount();
                    var fullPath = Path.Combine(seriesForFiles.Path, file.RelativePath);
                    var pathLength = fullPath.GetByteCount();

                    var nameTooLong = limit > 0 && nameLength > limit;
                    var pathTooLong = pathLimit > 0 && pathLength > pathLimit;

                    if (!nameTooLong && !pathTooLong)
                    {
                        continue;
                    }

                    // Named one by one at debug level: knowing that twelve files are too long is the summary,
                    // and knowing which twelve is what anyone actually has to act on.
                    _logger.Debug("name {0}, path {1}: {2}",
                                  nameTooLong ? $"{nameLength} over by {nameLength - limit}" : $"{nameLength} ok",
                                  pathTooLong ? $"{pathLength} over by {pathLength - pathLimit}" : $"{pathLength} ok",
                                  fullPath);

                    overlong.Add(new OverlongFile
                    {
                        SeriesId = file.SeriesId,
                        SeriesTitle = seriesForFiles.Title,
                        RelativePath = file.RelativePath,
                        NameLength = nameLength,
                        PathLength = pathLength,
                        NameTooLong = nameTooLong,
                        PathTooLong = pathTooLong
                    });
                }
            }

            _logger.ProgressInfo("{0} of {1} files are over a length limit ({2} by name, {3} by path)",
                                 overlong.Count,
                                 files.Count,
                                 overlong.Count(f => f.NameTooLong),
                                 overlong.Count(f => f.PathTooLong));

            _cache.Set("overlong", overlong);

            return overlong;
        }

        public void Execute(CheckFileNameLengthsCommand message)
        {
            Scan();

            _eventAggregator.PublishEvent(new FileNameLengthsScannedEvent());
        }

        // A refresh is the moment the library has just been looked over anyway, and this changes only
        // when files are imported or renamed - slowly enough that riding along is fresh enough, and far
        // cheaper than answering every import event one file at a time.
        public void Handle(SeriesRefreshCompleteEvent message)
        {
            Scan();

            _eventAggregator.PublishEvent(new FileNameLengthsScannedEvent());
        }
    }
}

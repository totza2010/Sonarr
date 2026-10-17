using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    /// <summary>
    /// Reads the part or version back out of the file name. The marker is written into the name by the
    /// {Multiple} token, and the name is the only place it survives: a disk scan re-imports whatever it
    /// finds without any memory of what the database used to say, so without this every rescan would
    /// collapse a split episode back down to a single file.
    /// </summary>
    public class AggregateMultipleFile : IAggregateLocalEpisode
    {
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IDiskProvider _diskProvider;
        private readonly IMediaFileService _mediaFileService;
        private readonly IEpisodeFileLinkService _episodeFileLinkService;

        public AggregateMultipleFile(IBuildFileNames fileNameBuilder,
                                     IDiskProvider diskProvider,
                                     IMediaFileService mediaFileService,
                                     IEpisodeFileLinkService episodeFileLinkService)
        {
            _fileNameBuilder = fileNameBuilder;
            _diskProvider = diskProvider;
            _mediaFileService = mediaFileService;
            _episodeFileLinkService = episodeFileLinkService;
        }

        // After AggregateQuality, which is what decides whether a "v2" in the name was a repack marker.
        public int Order => 2;

        public LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            // A choice made by hand during a manual import wins over whatever the name happens to say.
            if (localEpisode.IsAdditionalFile)
            {
                return localEpisode;
            }

            // So does a whole episode, when whoever chose it said the name is not to be read.
            if (!localEpisode.MultipleFromName)
            {
                return localEpisode;
            }

            // Reading these out of names is only safe once the user has opted in by putting {Multiple} in
            // their naming format. Without that the token never wrote anything, so anything matching the
            // pattern came from somewhere else and is not ours to interpret.
            if (!_fileNameBuilder.SupportsMultipleFiles(localEpisode.Series, localEpisode.Episodes))
            {
                return localEpisode;
            }

            var marker = MultipleFileMarkerReader.Read(Path.GetFileNameWithoutExtension(localEpisode.Path),
                                                       localEpisode.Series,
                                                       localEpisode.Episodes);

            if (marker == null)
            {
                return localEpisode;
            }

            var number = marker.Number;
            var isVersion = marker.Type == EpisodeFileMultipleType.Version;

            // "v2" is already how scene and anime releases mark a repack, and Sonarr reads it into the
            // quality revision. Claiming it here as well would turn an upgrade into a second file kept
            // alongside the first, so the revision wins whenever it accounts for the same marker.
            if (isVersion && localEpisode.Quality?.Revision?.Version == number)
            {
                return localEpisode;
            }

            var multipleType = marker.Type;

            // A third version is only a version if there were a first and a second. Without them the
            // marker is more likely something that happens to look like one, and calling it a version
            // would make an ordinary file an extra copy of its episode that is never upgraded.
            if (!HasEarlierMembers(localEpisode, multipleType, number))
            {
                return localEpisode;
            }

            localEpisode.MultipleType = multipleType;
            localEpisode.MultipleNumber = number;

            return localEpisode;
        }

        /// <summary>
        /// Whether every lower number of the same kind exists for this episode, looking in the two places
        /// the rest of a set can be: the library, and the folder this file is being imported from.
        /// </summary>
        private bool HasEarlierMembers(LocalEpisode localEpisode, EpisodeFileMultipleType multipleType, int number)
        {
            if (number <= 1)
            {
                return true;
            }

            var held = new HashSet<int>();
            var episodes = localEpisode.Episodes ?? new List<Episode>();

            // Already in the library: the file each episode points at, and any it owns through a link.
            var fileIds = episodes.Select(e => e.EpisodeFileId).Where(id => id > 0).ToList();
            fileIds.AddRange(_episodeFileLinkService.GetLinkedFileIds(episodes.Select(e => e.Id).ToList()) ?? new List<int>());

            if (fileIds.Any())
            {
                foreach (var file in _mediaFileService.GetFiles(fileIds.Distinct()) ?? new List<EpisodeFile>())
                {
                    if (file.MultipleType == multipleType)
                    {
                        held.Add(file.MultipleNumber);
                        continue;
                    }

                    // A file the database has lost the marker for still carries it in its name, which is
                    // where {Multiple} wrote it. The name is evidence enough to count it here.
                    if (file.MultipleType == EpisodeFileMultipleType.None)
                    {
                        var named = MultipleFileMarkerReader.Read(Path.GetFileNameWithoutExtension(file.RelativePath ?? string.Empty),
                                                                  localEpisode.Series,
                                                                  episodes);

                        if (named?.Type == multipleType)
                        {
                            held.Add(named.Number);
                        }
                    }
                }
            }

            // Arriving with it: the other files in the same folder that name the same episode.
            var folder = Path.GetDirectoryName(localEpisode.Path);
            var episodeNumbers = episodes.Select(e => (e.SeasonNumber, e.EpisodeNumber)).ToHashSet();

            if (folder.IsNotNullOrWhiteSpace() && episodeNumbers.Any() && _diskProvider.FolderExists(folder))
            {
                foreach (var sibling in _diskProvider.GetFiles(folder, false))
                {
                    if (sibling.PathEquals(localEpisode.Path) ||
                        !MediaFileExtensions.Extensions.Contains(Path.GetExtension(sibling)))
                    {
                        continue;
                    }

                    var parsed = Parser.Parser.ParsePath(sibling);

                    if (parsed == null ||
                        !parsed.EpisodeNumbers.Any() ||
                        !parsed.EpisodeNumbers.All(n => episodeNumbers.Contains((parsed.SeasonNumber, n))))
                    {
                        continue;
                    }

                    var siblingMarker = MultipleFileMarkerReader.Read(Path.GetFileNameWithoutExtension(sibling),
                                                                      localEpisode.Series,
                                                                      episodes);

                    if (siblingMarker?.Type == multipleType)
                    {
                        held.Add(siblingMarker.Number);
                    }
                }
            }

            return Enumerable.Range(1, number - 1).All(held.Contains);
        }
    }
}

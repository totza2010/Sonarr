using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        // Only the pt1 and v1 forms the {Multiple} token itself writes, never the words "part" or
        // "version". Plenty of episodes are genuinely titled "Part 2", and treating those as a part would
        // turn an ordinary file into an additional one and stop it ever being upgraded.
        private static readonly Regex MultipleRegex = new Regex(@"(?<![a-z0-9])(?<kind>pt|v)(?<number>\d{1,2})(?![a-z0-9])",
                                                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

            // Reading these out of names is only safe once the user has opted in by putting {Multiple} in
            // their naming format. Without that the token never wrote anything, so anything matching the
            // pattern came from somewhere else and is not ours to interpret.
            if (!_fileNameBuilder.SupportsMultipleFiles(localEpisode.Series, localEpisode.Episodes))
            {
                return localEpisode;
            }

            var match = FindMarker(Path.GetFileNameWithoutExtension(localEpisode.Path) ?? string.Empty, localEpisode);

            if (match == null)
            {
                return localEpisode;
            }

            var number = int.Parse(match.Groups["number"].Value);
            var isVersion = match.Groups["kind"].Value.Equals("v", StringComparison.InvariantCultureIgnoreCase);

            // "v2" is already how scene and anime releases mark a repack, and Sonarr reads it into the
            // quality revision. Claiming it here as well would turn an upgrade into a second file kept
            // alongside the first, so the revision wins whenever it accounts for the same marker.
            if (isVersion && localEpisode.Quality?.Revision?.Version == number)
            {
                return localEpisode;
            }

            var multipleType = isVersion ? EpisodeFileMultipleType.Version : EpisodeFileMultipleType.Part;

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

                    var marker = FindMarker(Path.GetFileNameWithoutExtension(sibling), localEpisode);

                    if (marker != null && IsKind(marker, multipleType))
                    {
                        held.Add(int.Parse(marker.Groups["number"].Value));
                    }
                }
            }

            return Enumerable.Range(1, number - 1).All(held.Contains);
        }

        private static bool IsKind(Match match, EpisodeFileMultipleType multipleType)
        {
            var isVersion = match.Groups["kind"].Value.Equals("v", StringComparison.InvariantCultureIgnoreCase);

            return isVersion == (multipleType == EpisodeFileMultipleType.Version);
        }

        /// <summary>
        /// The first marker the titles do not account for. A title can carry one of its own - Kamen Rider
        /// V3 is a series whose episodes are called things like "His Name is V3!" - and a name holds its
        /// titles, so a "V3" there says nothing about versions. Counted rather than simply skipped, so a
        /// real marker on such an episode is still found: the name then holds one more than its titles.
        ///
        /// Titles are measured both as written and as the naming format cleans them, and the larger count
        /// wins. Missing a genuine marker costs a row set by hand; inventing one turns an ordinary file
        /// into an extra copy that is never upgraded.
        /// </summary>
        private static Match FindMarker(string fileName, LocalEpisode localEpisode)
        {
            var matches = MultipleRegex.Matches(fileName).Cast<Match>().ToList();

            if (matches.Empty())
            {
                return null;
            }

            var titles = new List<string> { localEpisode.Series?.Title };
            titles.AddRange((localEpisode.Episodes ?? new List<Episode>()).Select(e => e.Title));

            foreach (var group in matches.GroupBy(Token))
            {
                var inTitles = titles.Where(t => !string.IsNullOrWhiteSpace(t))
                                     .Sum(t => Math.Max(CountToken(t, group.Key), CountToken(FileNameBuilder.CleanTitle(t), group.Key)));

                if (group.Count() > inTitles)
                {
                    return group.Last();
                }
            }

            return null;
        }

        private static string Token(Match match)
        {
            return match.Groups["kind"].Value.ToLowerInvariant() + int.Parse(match.Groups["number"].Value);
        }

        private static int CountToken(string text, string token)
        {
            return MultipleRegex.Matches(text).Count(m => Token(m) == token);
        }
    }
}

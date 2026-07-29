using System;
using System.IO;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;

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

        private readonly INamingConfigService _namingConfigService;

        public AggregateMultipleFile(INamingConfigService namingConfigService)
        {
            _namingConfigService = namingConfigService;
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
            if (!UsesMultipleToken())
            {
                return localEpisode;
            }

            var match = MultipleRegex.Match(Path.GetFileNameWithoutExtension(localEpisode.Path) ?? string.Empty);

            if (!match.Success)
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

            localEpisode.MultipleType = isVersion ? EpisodeFileMultipleType.Version : EpisodeFileMultipleType.Part;
            localEpisode.MultipleNumber = number;

            return localEpisode;
        }

        private bool UsesMultipleToken()
        {
            var config = _namingConfigService.GetConfig();

            return ContainsMultipleToken(config.StandardEpisodeFormat) ||
                   ContainsMultipleToken(config.DailyEpisodeFormat) ||
                   ContainsMultipleToken(config.AnimeEpisodeFormat);
        }

        private bool ContainsMultipleToken(string format)
        {
            return format.IsNotNullOrWhiteSpace() && format.Contains("{Multiple", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

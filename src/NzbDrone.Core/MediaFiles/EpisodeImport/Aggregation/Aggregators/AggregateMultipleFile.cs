using System.IO;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    /// <summary>
    /// Reads the part back out of the file name. The part number is written into the name by the {Part}
    /// token, and the name is the only place it survives: a disk scan re-imports whatever it finds without
    /// any memory of what the database used to say, so without this every rescan would collapse a split
    /// episode back down to a single file.
    /// </summary>
    public class AggregatePartNumber : IAggregateLocalEpisode
    {
        // Only the pt1 form the {Part} token itself writes, never the word "part". Plenty of episodes are
        // genuinely titled "Part 2", and treating those as a part would turn an ordinary file into an
        // additional one and stop it ever being upgraded.
        private static readonly Regex PartRegex = new Regex(@"(?<![a-z0-9])pt(?<part>\d{1,2})(?![a-z0-9])",
                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly INamingConfigService _namingConfigService;

        public AggregatePartNumber(INamingConfigService namingConfigService)
        {
            _namingConfigService = namingConfigService;
        }

        public int Order => 1;

        public LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            // A part chosen by hand during a manual import wins over whatever the name happens to say.
            if (localEpisode.PartNumber > 0)
            {
                return localEpisode;
            }

            // Reading parts out of names is only safe once the user has opted in by putting {Part} in their
            // naming format. Without that the token never wrote anything, so anything matching the pattern
            // came from somewhere else and is not ours to interpret.
            if (!UsesPartToken())
            {
                return localEpisode;
            }

            var match = PartRegex.Match(Path.GetFileNameWithoutExtension(localEpisode.Path) ?? string.Empty);

            if (match.Success)
            {
                localEpisode.PartNumber = int.Parse(match.Groups["part"].Value);
            }

            return localEpisode;
        }

        private bool UsesPartToken()
        {
            var config = _namingConfigService.GetConfig();

            return ContainsPartToken(config.StandardEpisodeFormat) ||
                   ContainsPartToken(config.DailyEpisodeFormat) ||
                   ContainsPartToken(config.AnimeEpisodeFormat);
        }

        private bool ContainsPartToken(string format)
        {
            return format.IsNotNullOrWhiteSpace() && format.Contains("{Part", System.StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

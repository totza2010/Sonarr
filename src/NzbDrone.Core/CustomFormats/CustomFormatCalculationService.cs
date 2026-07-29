using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.CustomFormats
{
    public interface ICustomFormatCalculationService
    {
        List<CustomFormat> ParseCustomFormat(RemoteEpisode remoteEpisode, long size);
        List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile, Series series);
        List<CustomFormat> ParseCustomFormatFromName(EpisodeFile episodeFile, Series series);
        List<CustomFormat> ParseScoredCustomFormat(EpisodeFile episodeFile, Series series);
        List<CustomFormat> ParseScoredCustomFormat(EpisodeFile episodeFile);
        List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile);
        List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Series series);
        List<CustomFormat> ParseCustomFormat(EpisodeHistory history, Series series);
        List<CustomFormat> ParseCustomFormat(LocalEpisode localEpisode);
    }

    public class CustomFormatCalculationService : ICustomFormatCalculationService
    {
        private readonly ICustomFormatService _formatService;
        private readonly Logger _logger;

        public CustomFormatCalculationService(ICustomFormatService formatService, Logger logger)
        {
            _formatService = formatService;
            _logger = logger;
        }

        public List<CustomFormat> ParseCustomFormat(RemoteEpisode remoteEpisode, long size)
        {
            var input = new CustomFormatInput
            {
                EpisodeInfo = remoteEpisode.ParsedEpisodeInfo,
                Series = remoteEpisode.Series,
                Size = size,
                Languages = remoteEpisode.Languages,
                IndexerFlags = remoteEpisode.Release?.IndexerFlags ?? 0,
                ReleaseType = remoteEpisode.ParsedEpisodeInfo.ReleaseType
            };

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile, Series series)
        {
            return Merge(episodeFile, series, _formatService.All());
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeFile episodeFile)
        {
            return Merge(episodeFile, episodeFile.Series.Value, _formatService.All());
        }

        public List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Series series)
        {
            var parsed = Parser.Parser.ParseTitle(blocklist.SourceTitle);

            var episodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = series.Title,
                ReleaseTitle = parsed?.ReleaseTitle ?? blocklist.SourceTitle,
                Quality = blocklist.Quality,
                Languages = blocklist.Languages,
                ReleaseGroup = parsed?.ReleaseGroup
            };

            var input = new CustomFormatInput
            {
                EpisodeInfo = episodeInfo,
                Series = series,
                Size = blocklist.Size ?? 0,
                Languages = blocklist.Languages,
                IndexerFlags = blocklist.IndexerFlags,
                ReleaseType = blocklist.ReleaseType
            };

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(EpisodeHistory history, Series series)
        {
            var parsed = Parser.Parser.ParseTitle(history.SourceTitle);

            long.TryParse(history.Data.GetValueOrDefault("size"), out var size);
            Enum.TryParse(history.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags indexerFlags);
            Enum.TryParse(history.Data.GetValueOrDefault("releaseType"), out ReleaseType releaseType);

            var episodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = series.Title,
                ReleaseTitle = parsed?.ReleaseTitle ?? history.SourceTitle,
                Quality = history.Quality,
                Languages = history.Languages,
                ReleaseGroup = parsed?.ReleaseGroup,
            };

            var input = new CustomFormatInput
            {
                EpisodeInfo = episodeInfo,
                Series = series,
                Size = size,
                Languages = history.Languages,
                IndexerFlags = indexerFlags,
                ReleaseType = releaseType
            };

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(LocalEpisode localEpisode)
        {
            var episodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = localEpisode.Series.Title,
                ReleaseTitle = localEpisode.SceneName.IsNotNullOrWhiteSpace() ? localEpisode.SceneName : Path.GetFileName(localEpisode.Path),
                Quality = localEpisode.Quality,
                Languages = localEpisode.Languages,
                ReleaseGroup = localEpisode.ReleaseGroup
            };

            var input = new CustomFormatInput
            {
                EpisodeInfo = episodeInfo,
                Series = localEpisode.Series,
                Size = localEpisode.Size,
                Languages = localEpisode.Languages,
                IndexerFlags = localEpisode.IndexerFlags,
                ReleaseType = localEpisode.ReleaseType,
                Filename = Path.GetFileName(localEpisode.Path)
            };

            return ParseCustomFormat(input);
        }

        private List<CustomFormat> ParseCustomFormat(CustomFormatInput input)
        {
            return ParseCustomFormat(input, _formatService.All());
        }

        private static List<CustomFormat> ParseCustomFormat(CustomFormatInput input, List<CustomFormat> allCustomFormats)
        {
            var matches = new List<CustomFormat>();

            foreach (var customFormat in allCustomFormats)
            {
                var specificationMatches = customFormat.Specifications
                    .GroupBy(t => t.GetType())
                    .Select(g => new SpecificationMatchesGroup
                    {
                        Matches = g.ToDictionary(t => t, t => t.IsSatisfiedBy(input))
                    })
                    .ToList();

                if (specificationMatches.All(x => x.DidMatch))
                {
                    matches.Add(customFormat);
                }
            }

            return matches.OrderBy(x => x.Name).ToList();
        }

        /// <summary>
        /// What the file earns on its own, before anything added by hand is folded in. Exposed so the
        /// cleanup can tell the two apart; everything else wants the merged list below.
        /// </summary>
        public List<CustomFormat> ParseCustomFormatFromName(EpisodeFile episodeFile, Series series)
        {
            return MatchFormats(episodeFile, series, _formatService.All());
        }

        private List<CustomFormat> MatchFormats(EpisodeFile episodeFile, Series series, List<CustomFormat> allCustomFormats)
        {
            var releaseTitle = string.Empty;

            if (episodeFile.SceneName.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using scene name for release title: {0}", episodeFile.SceneName);
                releaseTitle = episodeFile.SceneName;
            }
            else if (episodeFile.OriginalFilePath.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using original file path for release title: {0}", Path.GetFileName(episodeFile.OriginalFilePath));
                releaseTitle = Path.GetFileName(episodeFile.OriginalFilePath);
            }
            else if (episodeFile.RelativePath.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using relative path for release title: {0}", Path.GetFileName(episodeFile.RelativePath));
                releaseTitle = Path.GetFileName(episodeFile.RelativePath);
            }

            var episodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = series.Title,
                ReleaseTitle = releaseTitle,
                Quality = episodeFile.Quality,
                Languages = episodeFile.Languages,
                ReleaseGroup = episodeFile.ReleaseGroup,
            };

            var input = new CustomFormatInput
            {
                EpisodeInfo = episodeInfo,
                Series = series,
                Size = episodeFile.Size,
                Languages = episodeFile.Languages,
                IndexerFlags = episodeFile.IndexerFlags,
                ReleaseType = episodeFile.ReleaseType,
                Filename = Path.GetFileName(episodeFile.RelativePath),
            };

            return ParseCustomFormat(input, allCustomFormats);
        }

        /// <summary>
        /// What counts towards the file's score: only what the file itself is, never what was added by
        /// hand. A hand-added format is a naming instruction, not a property of the release, so scoring
        /// it would let a rename inflate the file's own score and block upgrades. Once the new name
        /// carries the format the file earns it here on its own, and the hand-added copy is dropped.
        /// </summary>
        public List<CustomFormat> ParseScoredCustomFormat(EpisodeFile episodeFile, Series series)
        {
            return Earned(episodeFile, series, _formatService.All()).ToList();
        }

        public List<CustomFormat> ParseScoredCustomFormat(EpisodeFile episodeFile)
        {
            return ParseScoredCustomFormat(episodeFile, episodeFile.Series.Value);
        }

        /// <summary>
        /// What the name matches, less anything ruled out by hand.
        /// </summary>
        private IEnumerable<CustomFormat> Earned(EpisodeFile episodeFile, Series series, List<CustomFormat> allCustomFormats)
        {
            return MatchFormats(episodeFile, series, allCustomFormats)
                .Where(f => episodeFile.ExcludedCustomFormats?.Contains(f.Id) != true);
        }

        /// <summary>
        /// What the file matches on its own plus what it was given by hand. Ids that no longer name a
        /// format are ignored rather than reported, since deleting a format is a deliberate act.
        /// </summary>
        private List<CustomFormat> Merge(EpisodeFile episodeFile, Series series, List<CustomFormat> allCustomFormats)
        {
            var matched = Earned(episodeFile, series, allCustomFormats).ToList();

            var manual = allCustomFormats.Where(f => episodeFile.ManualCustomFormats?.Contains(f.Id) == true &&
                                                     matched.All(m => m.Id != f.Id));

            return matched.Concat(manual).ToList();
        }
    }
}

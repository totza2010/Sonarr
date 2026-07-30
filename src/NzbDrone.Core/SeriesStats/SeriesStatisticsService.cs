using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Organizer;

namespace NzbDrone.Core.SeriesStats
{
    public interface ISeriesStatisticsService
    {
        List<SeriesStatistics> SeriesStatistics();
        SeriesStatistics SeriesStatistics(int seriesId);
    }

    public class SeriesStatisticsService : ISeriesStatisticsService
    {
        private readonly ISeriesStatisticsRepository _seriesStatisticsRepository;
        private readonly INamingConfigService _namingConfigService;

        public SeriesStatisticsService(ISeriesStatisticsRepository seriesStatisticsRepository,
                                      INamingConfigService namingConfigService)
        {
            _seriesStatisticsRepository = seriesStatisticsRepository;
            _namingConfigService = namingConfigService;
        }

        public List<SeriesStatistics> SeriesStatistics()
        {
            var namingConfig = _namingConfigService.GetConfig();
            var seasonStatistics = _seriesStatisticsRepository.SeriesStatistics(ShowLanguageFlags(namingConfig));

            return seasonStatistics.GroupBy(s => s.SeriesId).Select(s => MapSeriesStatistics(s.ToList(), namingConfig)).ToList();
        }

        public SeriesStatistics SeriesStatistics(int seriesId)
        {
            var namingConfig = _namingConfigService.GetConfig();
            var stats = _seriesStatisticsRepository.SeriesStatistics(seriesId, ShowLanguageFlags(namingConfig));

            if (stats == null || stats.Count == 0)
            {
                return new SeriesStatistics();
            }

            return MapSeriesStatistics(stats, namingConfig);
        }

        private SeriesStatistics MapSeriesStatistics(List<SeasonStatistics> seasonStatistics, NamingConfig namingConfig)
        {
            var languages = MergeLanguageGroups(seasonStatistics);

            // The names carry two unlabelled groups; the format that wrote them says which is which.
            var subtitlesFirst = FileNameLanguages.SubtitlesComeFirst(namingConfig?.StandardEpisodeFormat,
                                                                     namingConfig?.DailyEpisodeFormat,
                                                                     namingConfig?.AnimeEpisodeFormat);

            var seriesStatistics = new SeriesStatistics
            {
                SeasonStatistics = seasonStatistics,
                SeriesId = seasonStatistics.First().SeriesId,
                EpisodeFileCount = seasonStatistics.Sum(s => s.EpisodeFileCount),
                EpisodeCount = seasonStatistics.Sum(s => s.EpisodeCount),
                TotalEpisodeCount = seasonStatistics.Sum(s => s.TotalEpisodeCount),
                SizeOnDisk = seasonStatistics.Sum(s => s.SizeOnDisk),
                ReleaseGroups = seasonStatistics.SelectMany(s => s.ReleaseGroups).Distinct().ToList(),
                AudioLanguages = At(languages, subtitlesFirst ? 1 : 0),
                SubtitleLanguages = At(languages, subtitlesFirst ? 0 : 1)
            };

            var nextAiring = seasonStatistics.Where(s => s.NextAiring != null).MinBy(s => s.NextAiring);
            var previousAiring = seasonStatistics.Where(s => s.PreviousAiring != null).MaxBy(s => s.PreviousAiring);
            var lastAired = seasonStatistics.Where(s => s.SeasonNumber > 0 && s.LastAired != null).MaxBy(s => s.LastAired);

            seriesStatistics.NextAiring = nextAiring?.NextAiring;
            seriesStatistics.PreviousAiring = previousAiring?.PreviousAiring;
            seriesStatistics.LastAired = lastAired?.LastAired;

            return seriesStatistics;
        }

        private static bool ShowLanguageFlags(NamingConfig namingConfig)
        {
            return namingConfig?.ShowLanguageFlags == true;
        }

        private static List<string> At(List<List<string>> groups, int index)
        {
            return index < groups.Count ? groups[index] : new List<string>();
        }

        // Position by position across the seasons, so a series dubbed from season two on shows both.
        private static List<List<string>> MergeLanguageGroups(List<SeasonStatistics> seasonStatistics)
        {
            var merged = new List<List<string>>();

            foreach (var groups in seasonStatistics.Select(s => s.LanguageGroups))
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    while (merged.Count <= i)
                    {
                        merged.Add(new List<string>());
                    }

                    merged[i].AddRange(groups[i].Where(c => !merged[i].Contains(c)));
                }
            }

            return merged;
        }
    }
}

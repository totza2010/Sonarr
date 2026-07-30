using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.SeriesStats;

namespace Sonarr.Api.V3.Series
{
    public class SeriesStatisticsResource
    {
        public int SeasonCount { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public long SizeOnDisk { get; set; }
        public List<string> ReleaseGroups { get; set; }

        // What the file names say this series' audio and subtitle languages are. Absent unless the
        // language flags are switched on, so nothing that has never heard of them sees anything new.
        public List<string> AudioLanguages { get; set; }
        public List<string> SubtitleLanguages { get; set; }

        public decimal PercentOfEpisodes
        {
            get
            {
                if (EpisodeCount == 0)
                {
                    return 0;
                }

                return (decimal)EpisodeFileCount / (decimal)EpisodeCount * 100;
            }
        }
    }

    public static class SeriesStatisticsResourceMapper
    {
        public static SeriesStatisticsResource ToResource(this SeriesStatistics model, List<SeasonResource> seasons)
        {
            if (model == null)
            {
                return null;
            }

            return new SeriesStatisticsResource
            {
                SeasonCount = seasons == null ? 0 : seasons.Where(s => s.SeasonNumber > 0).Count(),
                EpisodeFileCount = model.EpisodeFileCount,
                EpisodeCount = model.EpisodeCount,
                TotalEpisodeCount = model.TotalEpisodeCount,
                SizeOnDisk = model.SizeOnDisk,
                ReleaseGroups = model.ReleaseGroups,
                AudioLanguages = model.AudioLanguages?.Any() == true ? model.AudioLanguages : null,
                SubtitleLanguages = model.SubtitleLanguages?.Any() == true ? model.SubtitleLanguages : null
            };
        }
    }
}

using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.SeriesStats
{
    public class SeriesStatistics : ResultSet
    {
        public int SeriesId { get; set; }
        public DateTime? NextAiring { get; set; }
        public DateTime? PreviousAiring { get; set; }
        public DateTime? LastAired { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public long SizeOnDisk { get; set; }
        public List<string> ReleaseGroups { get; set; }

        // What this series' file names say its audio and subtitle languages are. Empty unless the
        // language flags are being shown.
        public List<string> AudioLanguages { get; set; } = new List<string>();
        public List<string> SubtitleLanguages { get; set; } = new List<string>();
        public List<SeasonStatistics> SeasonStatistics { get; set; }
    }
}

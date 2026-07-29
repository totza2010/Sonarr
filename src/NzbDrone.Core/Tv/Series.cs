using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Tv
{
    public class Series : ModelBase
    {
        public Series()
        {
            Images = new List<MediaCover.MediaCover>();
            Genres = new List<string>();
            Actors = new List<Actor>();
            Seasons = new List<Season>();
            Tags = new HashSet<int>();
            OriginalLanguage = Language.English;
            NamingLanguage = Language.Unknown;
            MalIds = new HashSet<int>();
            AniListIds = new HashSet<int>();
        }

        public int TvdbId { get; set; }
        public int TvRageId { get; set; }
        public int TvMazeId { get; set; }
        public string ImdbId { get; set; }
        public int TmdbId { get; set; }
        public HashSet<int> MalIds { get; set; }
        public HashSet<int> AniListIds { get; set; }
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string SortTitle { get; set; }
        public SeriesStatusType Status { get; set; }
        public string Overview { get; set; }
        public string AirTime { get; set; }
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }
        public int QualityProfileId { get; set; }
        public bool SeasonFolder { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public int Runtime { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public SeriesTypes SeriesType { get; set; }
        public string Network { get; set; }
        public bool UseSceneNumbering { get; set; }
        public string TitleSlug { get; set; }
        public string Path { get; set; }
        public int Year { get; set; }
        public Ratings Ratings { get; set; }
        public List<string> Genres { get; set; }
        public List<Actor> Actors { get; set; }
        public string Certification { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public DateTime? FirstAired { get; set; }
        public DateTime? LastAired { get; set; }
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }
        public Language OriginalLanguage { get; set; }

        // Overrides OriginalLanguage for ORIGINAL in a naming format, and only there. The metadata
        // refresh keeps owning OriginalLanguage, so custom formats and auto tagging carry on reading
        // what the metadata says while file names can say what the files actually are.
        public Language NamingLanguage { get; set; }

        public List<Season> Seasons { get; set; }
        public HashSet<int> Tags { get; set; }
        public AddSeriesOptions AddOptions { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", TvdbId, Title.NullSafe());
        }

        public void ApplyChanges(Series otherSeries)
        {
            TvdbId = otherSeries.TvdbId;

            Seasons = otherSeries.Seasons;
            Path = otherSeries.Path;
            QualityProfileId = otherSeries.QualityProfileId;

            SeasonFolder = otherSeries.SeasonFolder;
            Monitored = otherSeries.Monitored;
            MonitorNewItems = otherSeries.MonitorNewItems;

            SeriesType = otherSeries.SeriesType;

            // Set by hand and never by the metadata refresh, so it has to be carried across here or
            // an edit is thrown away without a word. A client that has never heard of the field sends
            // nothing at all, and taking that as "clear it" would both wipe the setting and fail the
            // save, since a language column cannot hold null.
            if (otherSeries.NamingLanguage != null)
            {
                NamingLanguage = otherSeries.NamingLanguage;
            }

            RootFolderPath = otherSeries.RootFolderPath;
            Tags = otherSeries.Tags;
            AddOptions = otherSeries.AddOptions;
        }
    }
}

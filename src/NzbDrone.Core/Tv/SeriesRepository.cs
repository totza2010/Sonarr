using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Tv
{
    public interface ISeriesRepository : IBasicRepository<Series>
    {
        bool SeriesPathExists(string path);
        Series FindByTitle(string cleanTitle);
        Series FindByTitle(string cleanTitle, int year);
        List<Series> FindByTitleInexact(string cleanTitle);
        Series FindByTvdbId(int tvdbId);
        List<Series> FindAllByTvdbId(int tvdbId);
        Series FindByTvdbIdAndEdition(int tvdbId, string editionName);
        Series FindByTvRageId(int tvRageId);
        Series FindByImdbId(string imdbId);
        Series FindByPath(string path);
        List<int> AllSeriesTvdbIds();
        Dictionary<int, List<string>> AllSeriesEditions();
        Dictionary<int, string> AllSeriesPaths();
        Dictionary<int, List<int>> AllSeriesTags();
    }

    public class SeriesRepository : BasicRepository<Series>, ISeriesRepository
    {
        public SeriesRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public bool SeriesPathExists(string path)
        {
            return Query(c => c.Path == path).Any();
        }

        public Series FindByTitle(string cleanTitle)
        {
            cleanTitle = cleanTitle.ToLowerInvariant();

            var series = Query(s => s.CleanTitle == cleanTitle)
                                        .ToList();

            return ReturnSingleSeriesOrThrow(series);
        }

        public Series FindByTitle(string cleanTitle, int year)
        {
            cleanTitle = cleanTitle.ToLowerInvariant();

            var series = Query(s => s.CleanTitle == cleanTitle && s.Year == year).ToList();

            return ReturnSingleSeriesOrThrow(series);
        }

        public List<Series> FindByTitleInexact(string cleanTitle)
        {
            var builder = Builder().Where($"instr(@cleanTitle, \"Series\".\"CleanTitle\")", new { cleanTitle = cleanTitle });

            if (_database.DatabaseType == DatabaseType.PostgreSQL)
            {
                builder = Builder().Where($"(strpos(@cleanTitle, \"Series\".\"CleanTitle\") > 0)", new { cleanTitle = cleanTitle });
            }

            return Query(builder).ToList();
        }

        public Series FindByTvdbId(int tvdbId)
        {
            var series = Query(s => s.TvdbId == tvdbId).ToList();

            if (series.Count <= 1)
            {
                return series.FirstOrDefault();
            }

            // The series has editions. Callers that only know the TVDB ID (metadata refresh, API lookups,
            // release parsing) get the main edition, editions are resolved from the file or by the user.
            return GetMainEdition(series);
        }

        public List<Series> FindAllByTvdbId(int tvdbId)
        {
            return Query(s => s.TvdbId == tvdbId).ToList();
        }

        public Series FindByTvdbIdAndEdition(int tvdbId, string editionName)
        {
            var normalized = SeriesEditions.NormalizeEditionName(editionName);

            return Query(s => s.TvdbId == tvdbId && s.EditionName == normalized).FirstOrDefault();
        }

        public Series FindByTvRageId(int tvRageId)
        {
            // Every edition carries the ids of the series it is an edition of, so this can match more
            // than one series without them being different series.
            return ReturnMainEditionOrSingle(Query(s => s.TvRageId == tvRageId).ToList());
        }

        public Series FindByImdbId(string imdbId)
        {
            return ReturnMainEditionOrSingle(Query(s => s.ImdbId == imdbId).ToList());
        }

        public Series FindByPath(string path)
        {
            return Query(s => s.Path == path)
                        .FirstOrDefault();
        }

        public List<int> AllSeriesTvdbIds()
        {
            using (var conn = _database.OpenConnection())
            {
                return conn.Query<int>("SELECT \"TvdbId\" FROM \"Series\"").ToList();
            }
        }

        public Dictionary<int, List<string>> AllSeriesEditions()
        {
            using (var conn = _database.OpenConnection())
            {
                var strSql = "SELECT \"TvdbId\", \"EditionName\" FROM \"Series\"";

                return conn.Query<SeriesEditionKey>(strSql)
                    .GroupBy(x => x.TvdbId)
                    .ToDictionary(g => g.Key, g => g.Select(x => SeriesEditions.NormalizeEditionName(x.EditionName)).ToList());
            }
        }

        public Dictionary<int, string> AllSeriesPaths()
        {
            using (var conn = _database.OpenConnection())
            {
                var strSql = "SELECT \"Id\" AS Key, \"Path\" AS Value FROM \"Series\"";
                return conn.Query<KeyValuePair<int, string>>(strSql).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public Dictionary<int, List<int>> AllSeriesTags()
        {
            using (var conn = _database.OpenConnection())
            {
                var strSql = "SELECT \"Id\" AS Key, \"Tags\" AS Value FROM \"Series\" WHERE \"Tags\" IS NOT NULL";
                return conn.Query<KeyValuePair<int, List<int>>>(strSql).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        private static Series GetMainEdition(List<Series> series)
        {
            return series.FirstOrDefault(s => SeriesEditions.IsMainEdition(s.EditionName)) ?? series.First();
        }

        // Lookups by an id the metadata source owns can hit every edition of a series, since they all
        // carry the same ids. Anything else matching more than once is a real ambiguity.
        private static Series ReturnMainEditionOrSingle(List<Series> series)
        {
            if (series.Count <= 1)
            {
                return series.FirstOrDefault();
            }

            if (series.Select(s => s.TvdbId).Distinct().Count() == 1)
            {
                return GetMainEdition(series);
            }

            return series.SingleOrDefault();
        }

        private Series ReturnSingleSeriesOrThrow(List<Series> series)
        {
            if (series.Count == 0)
            {
                return null;
            }

            if (series.Count == 1)
            {
                return series.First();
            }

            // Editions of one series share their title, so matching by title finds all of them. That is
            // not the ambiguity this throws for, the main edition is the one release parsing wants.
            if (series.Select(s => s.TvdbId).Distinct().Count() == 1)
            {
                return GetMainEdition(series);
            }

            // Separate series that happen to share a title stay ambiguous, the user has to pick one.
            throw new MultipleSeriesFoundException(series, "Expected one series, but found {0}. Matching series: {1}", series.Count, string.Join(", ", series));
        }

        public class SeriesEditionKey
        {
            public int TvdbId { get; set; }
            public string EditionName { get; set; }
        }
    }
}

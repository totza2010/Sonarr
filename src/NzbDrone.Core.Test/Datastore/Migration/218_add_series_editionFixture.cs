using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class add_series_editionFixture : MigrationTest<add_series_edition>
    {
        [Test]
        public void should_default_existing_series_to_the_main_edition()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("Series").Row(new
                {
                    TvdbId = 1,
                    TvRageId = 1,
                    TvMazeId = 1,
                    TmdbId = 1,
                    OriginalLanguage = 1,
                    Title = "Title1",
                    CleanTitle = "title1",
                    TitleSlug = "title1",
                    Status = 1,
                    Images = "[]",
                    Path = "c:\\title1",
                    Monitored = true,
                    SeasonFolder = true,
                    Runtime = 0,
                    SeriesType = 0,
                    UseSceneNumbering = false,
                    Seasons = "[]",
                    Tags = "[]",
                    QualityProfileId = 1,
                    MonitorNewItems = 0
                });
            });

            var series = db.Query<Series218>("SELECT \"TvdbId\", \"EditionName\" FROM \"Series\"");

            series.Should().HaveCount(1);
            series.First().EditionName.Should().BeEmpty();
        }

        [Test]
        public void should_drop_the_unique_index_on_tvdb_id_alone()
        {
            var db = WithMigrationTestDb();

            var index = db.QueryScalar<string>(
                "SELECT \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'index' AND \"name\" = 'IX_Series_TvdbId'");

            index.Should().BeNull();
        }

        [Test]
        public void should_drop_the_unique_constraint_declared_on_the_column()
        {
            var db = WithMigrationTestDb();

            var table = db.QueryScalar<string>(
                "SELECT \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = 'Series'");

            table.Should().NotContain("\"TvdbId\" INTEGER NOT NULL UNIQUE");
        }

        [Test]
        public void should_make_tvdb_id_unique_per_edition_instead()
        {
            var db = WithMigrationTestDb();

            var index = db.QueryScalar<string>(
                "SELECT \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'index' AND \"tbl_name\" = 'Series' AND \"sql\" LIKE '%EditionName%'");

            index.Should().NotBeNull();
            index.Should().Contain("UNIQUE");
            index.Should().Contain("TvdbId");
        }
    }

    public class Series218
    {
        public int TvdbId { get; set; }
        public string EditionName { get; set; }
    }
}

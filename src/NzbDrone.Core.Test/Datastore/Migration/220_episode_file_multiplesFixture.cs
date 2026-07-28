using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class episode_file_multiplesFixture : MigrationTest<episode_file_multiples>
    {
        private object Row(int partNumber, string versionName)
        {
            return new
            {
                SeriesId = 1,
                SeasonNumber = 1,
                RelativePath = $"Season 01/S01E05.pt{partNumber}.mkv",
                Size = 125.Megabytes(),
                DateAdded = DateTime.UtcNow.AddDays(-5),
                ReleaseGroup = "Sonarr",
                Quality = new QualityModel(Quality.HDTV720p).ToJson(),
                Languages = "[1]",
                PartNumber = partNumber,
                VersionName = versionName
            };
        }

        [Test]
        public void should_keep_a_parts_number_and_call_it_a_part()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("EpisodeFiles").Row(Row(2, string.Empty));
            });

            var items = db.Query<EpisodeFile220>("SELECT \"Id\", \"MultipleType\", \"MultipleNumber\" FROM \"EpisodeFiles\"");

            items.Should().HaveCount(1);
            items.First().MultipleType.Should().Be((int)EpisodeFileMultipleType.Part);
            items.First().MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_turn_a_version_name_into_version_one()
        {
            // The name was free text and there is nowhere for it to go, so every named version becomes the
            // first one. Losing the label is the point of the change; losing the file would not be.
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("EpisodeFiles").Row(Row(0, "Alternate Ending"));
            });

            var items = db.Query<EpisodeFile220>("SELECT \"Id\", \"MultipleType\", \"MultipleNumber\" FROM \"EpisodeFiles\"");

            items.Should().HaveCount(1);
            items.First().MultipleType.Should().Be((int)EpisodeFileMultipleType.Version);
            items.First().MultipleNumber.Should().Be(1);
        }

        [Test]
        public void should_leave_an_ordinary_file_alone()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("EpisodeFiles").Row(Row(0, string.Empty));
            });

            var items = db.Query<EpisodeFile220>("SELECT \"Id\", \"MultipleType\", \"MultipleNumber\" FROM \"EpisodeFiles\"");

            items.Should().HaveCount(1);
            items.First().MultipleType.Should().Be((int)EpisodeFileMultipleType.None);
            items.First().MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_prefer_the_part_when_a_file_somehow_has_both()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("EpisodeFiles").Row(Row(3, "Alternate Ending"));
            });

            var items = db.Query<EpisodeFile220>("SELECT \"Id\", \"MultipleType\", \"MultipleNumber\" FROM \"EpisodeFiles\"");

            items.Should().HaveCount(1);
            items.First().MultipleType.Should().Be((int)EpisodeFileMultipleType.Part);
            items.First().MultipleNumber.Should().Be(3);
        }

        public class EpisodeFile220
        {
            public int Id { get; set; }
            public int MultipleType { get; set; }
            public int MultipleNumber { get; set; }
        }
    }
}

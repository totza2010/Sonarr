using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.SeriesRepositoryTests
{
    [TestFixture]

    public class SeriesRepositoryFixture : DbTest<SeriesRepository, Series>
    {
        [Test]
        public void should_lazyload_quality_profile()
        {
            var profile = new QualityProfile
                {
                    Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p, Quality.DVD, Quality.HDTV720p),

                    Cutoff = Quality.Bluray1080p.Id,
                    Name = "TestProfile"
                };

            Mocker.Resolve<QualityProfileRepository>().Insert(profile);

            var series = Builder<Series>.CreateNew().BuildNew();
            series.QualityProfileId = profile.Id;

            Subject.Insert(series);

            StoredModel.QualityProfile.Should().NotBeNull();
        }

        private void GivenSeries()
        {
            var series = Builder<Series>.CreateListOfSize(2)
                .All()
                .With(a => a.Id = 0)
                .TheFirst(1)
                .With(x => x.CleanTitle = "crown")
                .TheNext(1)
                .With(x => x.CleanTitle = "crownextralong")
                .BuildList();

            Subject.InsertMany(series);
        }

        [TestCase("crow")]
        [TestCase("rownc")]
        public void should_find_no_inexact_matches(string cleanTitle)
        {
            GivenSeries();

            var found = Subject.FindByTitleInexact(cleanTitle);
            found.Should().BeEmpty();
        }

        [TestCase("crowna")]
        [TestCase("acrown")]
        [TestCase("acrowna")]
        public void should_find_one_inexact_match(string cleanTitle)
        {
            GivenSeries();

            var found = Subject.FindByTitleInexact(cleanTitle);
            found.Should().HaveCount(1);
            found.First().CleanTitle.Should().Be("crown");
        }

        [TestCase("crownextralong")]
        [TestCase("crownextralonga")]
        [TestCase("acrownextralong")]
        [TestCase("acrownextralonga")]
        public void should_find_two_inexact_matches(string cleanTitle)
        {
            GivenSeries();

            var found = Subject.FindByTitleInexact(cleanTitle);
            found.Should().HaveCount(2);
            found.Select(x => x.CleanTitle).Should().BeEquivalentTo(new[] { "crown", "crownextralong" });
        }

        private void GivenSeriesWithEditions()
        {
            var series = Builder<Series>.CreateListOfSize(2)
                .All()
                .With(a => a.Id = 0)
                .With(a => a.TvdbId = 100)
                .TheFirst(1)
                .With(x => x.TitleSlug = "spider-noir")
                .With(x => x.EditionName = SeriesEditions.MainEdition)
                .TheNext(1)
                .With(x => x.TitleSlug = "spider-noir-black-white")
                .With(x => x.EditionName = "Black & White")
                .BuildList();

            Subject.InsertMany(series);
        }

        [Test]
        public void should_store_multiple_editions_of_the_same_tvdb_id()
        {
            GivenSeriesWithEditions();

            Subject.FindAllByTvdbId(100).Should().HaveCount(2);
        }

        [Test]
        public void should_return_the_main_edition_when_finding_by_tvdb_id_alone()
        {
            GivenSeriesWithEditions();

            var found = Subject.FindByTvdbId(100);

            found.Should().NotBeNull();
            found.EditionName.Should().BeEmpty();
        }

        [Test]
        public void should_find_a_specific_edition()
        {
            GivenSeriesWithEditions();

            var found = Subject.FindByTvdbIdAndEdition(100, "Black & White");

            found.Should().NotBeNull();
            found.TitleSlug.Should().Be("spider-noir-black-white");
        }

        [Test]
        public void should_not_find_an_edition_that_was_not_added()
        {
            GivenSeriesWithEditions();

            Subject.FindByTvdbIdAndEdition(100, "Remastered").Should().BeNull();
        }

        [Test]
        public void should_list_the_editions_of_each_tvdb_id()
        {
            GivenSeriesWithEditions();

            var editions = Subject.AllSeriesEditions();

            editions.Should().ContainKey(100);
            editions[100].Should().BeEquivalentTo(new[] { SeriesEditions.MainEdition, "Black & White" });
        }
    }
}

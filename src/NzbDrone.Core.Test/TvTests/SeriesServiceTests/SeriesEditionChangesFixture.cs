using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.SeriesServiceTests
{
    [TestFixture]
    public class SeriesEditionChangesFixture : CoreTest<SeriesService>
    {
        private Series _main;
        private Series _remastered;
        private Series _uncut;

        [SetUp]
        public void Setup()
        {
            _main = GivenEdition(1, "", "show");
            _remastered = GivenEdition(2, "Remastered", "show-remastered");
            _uncut = GivenEdition(3, "Uncut", "show-uncut");

            GivenLibrary(_main, _remastered, _uncut);

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(It.IsAny<Series>()))
                  .Returns(new AutoTaggingChanges());

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Update(It.IsAny<Series>()))
                  .Returns<Series>(s => s);
        }

        private static Series GivenEdition(int id, string editionName, string titleSlug)
        {
            return new Series
            {
                Id = id,
                TvdbId = 100,
                Title = "Show",
                EditionName = editionName,
                TitleSlug = titleSlug,
                Seasons = new List<Season>(),
                Tags = new HashSet<int>()
            };
        }

        private void GivenLibrary(params Series[] editions)
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindAllByTvdbId(100))
                  .Returns(editions.ToList());

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(It.IsAny<int>()))
                  .Returns<int>(id => editions.Single(e => e.Id == id));

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns<IEnumerable<int>>(ids => editions.Where(e => ids.Contains(e.Id)).ToList());
        }

        // What a save from the edit screen hands over: the stored series with the changes applied.
        private Series Renamed(Series stored, string editionName)
        {
            var edited = GivenEdition(stored.Id, editionName, stored.TitleSlug);

            return Subject.UpdateSeries(edited);
        }

        [Test]
        public void should_rename_an_edition_and_its_slug_with_it()
        {
            var updated = Renamed(_remastered, "Director's Cut");

            updated.EditionName.Should().Be("Director's Cut");
            updated.TitleSlug.Should().Be("show-directors-cut");
        }

        [Test]
        public void should_store_the_name_the_folder_will_carry()
        {
            var updated = Renamed(_remastered, "Cut: Final");

            updated.EditionName.Should().Be(FileNameBuilder.CleanFileName("Cut: Final"));
        }

        [Test]
        public void should_build_the_slug_from_the_edition_itself_when_the_main_edition_is_gone()
        {
            GivenLibrary(_remastered, _uncut);

            Renamed(_remastered, "Colour").TitleSlug.Should().Be("show-colour");
        }

        [Test]
        public void should_allow_changing_only_the_case_of_the_name()
        {
            Renamed(_remastered, "REMASTERED").EditionName.Should().Be("REMASTERED");
        }

        [Test]
        public void should_refuse_a_name_another_edition_already_has()
        {
            Assert.Throws<ValidationException>(() => Renamed(_remastered, "uncut"));

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.Update(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void should_refuse_to_name_the_main_edition()
        {
            Assert.Throws<ValidationException>(() => Renamed(_main, "Original"));
        }

        [Test]
        public void should_refuse_to_take_an_editions_name_away()
        {
            Assert.Throws<ValidationException>(() => Renamed(_remastered, " "));
        }

        [Test]
        public void should_not_look_at_editions_when_the_name_is_unchanged()
        {
            // Every save that is not a rename - monitoring, a profile, a refresh - takes this path, and the
            // slug it already has stays exactly as it is.
            var edited = GivenEdition(_remastered.Id, "Remastered", "a-slug-left-alone");

            Subject.UpdateSeries(edited).TitleSlug.Should().Be("a-slug-left-alone");

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.FindAllByTvdbId(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_refuse_to_delete_the_main_edition_while_others_remain()
        {
            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteSeries(new List<int> { _main.Id }, false, false));

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.DeleteMany(It.IsAny<List<int>>()), Times.Never());
        }

        [Test]
        public void should_delete_the_main_edition_together_with_all_the_others()
        {
            Subject.DeleteSeries(new List<int> { _main.Id, _remastered.Id, _uncut.Id }, false, false);

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.DeleteMany(It.IsAny<List<int>>()), Times.Once());
        }

        [Test]
        public void should_delete_an_edition_that_is_not_the_main_one()
        {
            Subject.DeleteSeries(new List<int> { _uncut.Id }, false, false);

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.DeleteMany(It.IsAny<List<int>>()), Times.Once());
        }

        [Test]
        public void should_delete_a_series_that_has_no_editions()
        {
            GivenLibrary(_main);

            Subject.DeleteSeries(new List<int> { _main.Id }, false, false);

            Mocker.GetMock<ISeriesRepository>().Verify(v => v.DeleteMany(It.IsAny<List<int>>()), Times.Once());
        }
    }
}

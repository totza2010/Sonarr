using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class AddSeriesFixture : CoreTest<AddSeriesService>
    {
        private Series _fakeSeries;

        [SetUp]
        public void Setup()
        {
            _fakeSeries = Builder<Series>
                .CreateNew()
                .With(s => s.Path = null)
                .Build();
        }

        private void GivenValidSeries(int tvdbId)
        {
            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(tvdbId))
                  .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));
        }

        private void GivenValidPath()
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), null))
                  .Returns<Series, NamingConfig>((c, n) => c.Title);

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult());
        }

        [Test]
        public void should_be_able_to_add_a_series_without_passing_in_title()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                RootFolderPath = @"C:\Test\TV"
            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Title.Should().Be(_fakeSeries.Title);
        }

        [Test]
        public void should_have_proper_path()
        {
            var newSeries = new Series
                            {
                                TvdbId = 1,
                                RootFolderPath = @"C:\Test\TV"
                            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Path.Should().Be(Path.Combine(newSeries.RootFolderPath, _fakeSeries.Title));
        }

        [Test]
        public void should_throw_if_series_validation_fails()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1"
            };

            GivenValidSeries(newSeries.TvdbId);

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));
        }

        [Test]
        public void should_throw_if_series_cannot_be_found()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1"
            };

            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(newSeries.TvdbId))
                  .Throws(new SeriesNotFoundException(newSeries.TvdbId));

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));

            ExceptionVerification.ExpectedErrors(1);
        }

        // What the library import screen sends: several series at once, each with the folder it was found
        // in. Every lookup gets a series of its own, since the answer is then changed to match the request.
        private List<Series> ImportSeries(Dictionary<int, List<string>> existingEditions, params Series[] newSeries)
        {
            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(It.IsAny<int>()))
                  .Returns<int>(tvdbId => new Tuple<Series, List<Episode>>(
                      new Series { TvdbId = tvdbId, Title = "Series", TitleSlug = "series", Seasons = new List<Season>() },
                      new List<Episode>()));

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesEditions())
                  .Returns(existingEditions);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AddSeries(It.IsAny<List<Series>>()))
                  .Returns<List<Series>>(added => added);

            GivenValidPath();

            return Subject.AddSeries(newSeries.ToList(), true);
        }

        private static Dictionary<int, List<string>> Library(params string[] editionsOfSeriesOne)
        {
            return editionsOfSeriesOne.Any()
                ? new Dictionary<int, List<string>> { { 1, editionsOfSeriesOne.ToList() } }
                : new Dictionary<int, List<string>>();
        }

        [Test]
        public void should_import_an_edition_of_a_series_that_is_already_in_the_library()
        {
            var folder = @"C:\Test\TV\Series (2020)".AsOsAgnostic();

            var added = ImportSeries(Library(""), new Series { TvdbId = 1, Path = folder, EditionName = "Remastered" });

            var edition = added.Should().ContainSingle().Subject;

            edition.EditionName.Should().Be("Remastered");
            edition.TitleSlug.Should().Be("series-remastered");
        }

        [Test]
        public void should_keep_the_folder_an_imported_edition_was_found_in()
        {
            // The folder is already there with the files in it. Building the name an edition would
            // normally be given would point the series at a folder that does not exist.
            var folder = @"C:\Test\TV\Series (2020)".AsOsAgnostic();

            var added = ImportSeries(Library(""), new Series { TvdbId = 1, Path = folder, EditionName = "Remastered" });

            added.Single().Path.Should().Be(folder);

            Mocker.GetMock<IBuildFileNames>()
                  .Verify(v => v.GetSeriesFolder(It.IsAny<Series>(), It.IsAny<NamingConfig>()), Times.Never());
        }

        [Test]
        public void should_not_import_an_edition_the_library_already_has()
        {
            var added = ImportSeries(Library("", "Remastered"),
                                     new Series { TvdbId = 1, Path = @"C:\Test\TV\Other".AsOsAgnostic(), EditionName = "remastered" });

            added.Should().BeEmpty();
        }

        [Test]
        public void should_not_import_a_second_copy_of_the_main_edition()
        {
            // Unchanged from before editions: a folder for a series that is already there, with nothing
            // said about an edition, is a duplicate.
            var added = ImportSeries(Library(""), new Series { TvdbId = 1, Path = @"C:\Test\TV\Other".AsOsAgnostic() });

            added.Should().BeEmpty();
        }

        [Test]
        public void should_import_two_editions_of_one_series_together()
        {
            var added = ImportSeries(Library(""),
                                     new Series { TvdbId = 1, Path = @"C:\Test\TV\A".AsOsAgnostic(), EditionName = "Remastered" },
                                     new Series { TvdbId = 1, Path = @"C:\Test\TV\B".AsOsAgnostic(), EditionName = "Uncut" });

            added.Select(s => s.EditionName).Should().BeEquivalentTo("Remastered", "Uncut");
        }

        [Test]
        public void should_import_the_same_edition_only_once_from_two_folders()
        {
            var added = ImportSeries(Library(""),
                                     new Series { TvdbId = 1, Path = @"C:\Test\TV\A".AsOsAgnostic(), EditionName = "Remastered" },
                                     new Series { TvdbId = 1, Path = @"C:\Test\TV\B".AsOsAgnostic(), EditionName = "Remastered" });

            added.Should().ContainSingle().Which.Path.Should().Be(@"C:\Test\TV\A".AsOsAgnostic());
        }
}
}

using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.OrganizerTests.FileNameBuilderTests
{
    [TestFixture]
    public class SeriesEditionFolderFixture : CoreTest<FileNameBuilder>
    {
        private Series _series;
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>
                      .CreateNew()
                      .With(s => s.Title = "Series Title")
                      .With(s => s.Year = 2020)
                      .With(s => s.EditionName = SeriesEditions.MainEdition)
                      .Build();

            _namingConfig = NamingConfig.Default;
            _namingConfig.SeriesFolderFormat = "{Series Title} ({Series Year})";

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);
        }

        [Test]
        public void should_not_change_the_folder_of_the_main_edition()
        {
            Subject.GetSeriesFolder(_series).Should().Be("Series Title (2020)");
        }

        [Test]
        public void should_add_the_plex_edition_token_for_an_edition()
        {
            _series.EditionName = "Black & White";

            Subject.GetSeriesFolder(_series)
                   .Should().Be("Series Title (2020) {edition-Black & White}");
        }

        [Test]
        public void should_not_put_path_characters_from_the_edition_name_into_the_folder()
        {
            _series.EditionName = "4:3 / Open\\Matte";

            var folder = Subject.GetSeriesFolder(_series);

            folder.Should().StartWith("Series Title (2020) {edition-");
            folder.Should().EndWith("}");
            folder.Should().NotContainAny(":", "/", "\\");
        }

        [Test]
        public void should_give_each_edition_a_folder_of_its_own()
        {
            var main = Subject.GetSeriesFolder(_series);

            _series.EditionName = "Remastered";
            var edition = Subject.GetSeriesFolder(_series);

            edition.Should().NotBe(main);
        }
    }
}

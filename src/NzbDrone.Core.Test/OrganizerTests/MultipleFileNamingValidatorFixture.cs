using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class MultipleFileNamingValidatorFixture : CoreTest<MultipleFileNamingValidator>
    {
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _namingConfig = new NamingConfig
            {
                RenameEpisodes = true,
                StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00} - {Episode Title} {Multiple}",
                DailyEpisodeFormat = "{Series Title} - {Air-Date} - {Episode Title} {Multiple}",
                AnimeEpisodeFormat = "{Series Title} - {absolute:000} - {Episode Title} {Multiple}"
            };

            GivenSeriesWithMultipleFiles();
        }

        private void GivenSeriesWithMultipleFiles(params SeriesTypes[] types)
        {
            if (types.Length == 0)
            {
                Mocker.GetMock<IMediaFileService>()
                      .Setup(v => v.SeriesIdsWithMultipleFiles())
                      .Returns(new List<int>());

                return;
            }

            var series = types.Select((t, i) => Builder<Series>.CreateNew()
                                                               .With(s => s.Id = i + 1)
                                                               .With(s => s.SeriesType = t)
                                                               .Build())
                              .ToList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.SeriesIdsWithMultipleFiles())
                  .Returns(series.Select(s => s.Id).ToList());

            Mocker.GetMock<ISeriesService>()
                  .Setup(v => v.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns(series);
        }

        [Test]
        public void should_require_nothing_of_a_library_that_has_no_parts()
        {
            // Which is every library that has never used the feature, so nobody is asked to change a
            // format they were happy with.
            _namingConfig.RenameEpisodes = false;
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_accept_a_format_that_can_tell_parts_apart()
        {
            GivenSeriesWithMultipleFiles(SeriesTypes.Standard);

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_allow_renaming_to_be_turned_off()
        {
            // Nothing is renamed then, so the parts on disk keep the names they have. What it costs is
            // the feature itself, which the import specification and the UI take away - not the save.
            GivenSeriesWithMultipleFiles(SeriesTypes.Standard);
            _namingConfig.RenameEpisodes = false;
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_reject_a_format_that_lost_the_token()
        {
            GivenSeriesWithMultipleFiles(SeriesTypes.Standard);
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00} - {Episode Title}";

            Subject.Validate(_namingConfig).Select(f => f.PropertyName).Should().BeEquivalentTo(new[] { "StandardEpisodeFormat" });
        }

        [Test]
        public void should_only_ask_of_the_formats_that_are_reached()
        {
            // A standard series having parts says nothing about the anime format, so dropping the token
            // there is nobody's business.
            GivenSeriesWithMultipleFiles(SeriesTypes.Standard);
            _namingConfig.AnimeEpisodeFormat = "{Series Title} - {absolute:000}";
            _namingConfig.DailyEpisodeFormat = "{Series Title} - {Air-Date}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_name_each_format_that_needs_the_token()
        {
            GivenSeriesWithMultipleFiles(SeriesTypes.Anime, SeriesTypes.Daily);
            _namingConfig.AnimeEpisodeFormat = "{Series Title} - {absolute:000}";
            _namingConfig.DailyEpisodeFormat = "{Series Title} - {Air-Date}";

            Subject.Validate(_namingConfig)
                   .Select(f => f.PropertyName)
                   .Should()
                   .BeEquivalentTo(new[] { "AnimeEpisodeFormat", "DailyEpisodeFormat" });
        }
    }
}

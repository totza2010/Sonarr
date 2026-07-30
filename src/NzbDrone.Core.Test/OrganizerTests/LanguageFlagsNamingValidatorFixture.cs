using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class LanguageFlagsNamingValidatorFixture : CoreTest<LanguageFlagsNamingValidator>
    {
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _namingConfig = new NamingConfig
            {
                RenameEpisodes = true,
                ShowLanguageFlags = true,
                StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00} - {Episode Title} {MediaInfo AudioLanguages}",
                DailyEpisodeFormat = "{Series Title} - {Air-Date} - {Episode Title} {MediaInfo AudioLanguages}",
                AnimeEpisodeFormat = "{Series Title} - {absolute:000} - {Episode Title} {MediaInfo AudioLanguages}"
            };

            GivenLibraryOf(SeriesTypes.Standard);
        }

        private void GivenLibraryOf(params SeriesTypes[] types)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(v => v.AllSeriesTypes())
                  .Returns(types.ToList());
        }

        [Test]
        public void should_require_nothing_while_the_flags_are_not_shown()
        {
            _namingConfig.ShowLanguageFlags = false;
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_accept_a_format_carrying_the_token()
        {
            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_accept_the_subtitle_token_on_its_own()
        {
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00} {MediaInfo SubtitleLanguages}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        // A token can carry a separator in front of its name and a filter behind it, and a format
        // written that way still writes the languages.
        [TestCase("{Series Title}{.MediaInfo AudioLanguages}")]
        [TestCase("{Series Title} {MediaInfo AudioLanguages:TH+EN+ORIGINAL}")]
        [TestCase("{Series Title}{.MediaInfo SubtitleLanguages:TH+EN+ORIGINAL}")]
        [TestCase("{Series Title} {MediaInfo AudioLanguagesAll}")]
        public void should_accept_a_token_carrying_a_separator_or_a_filter(string format)
        {
            _namingConfig.StandardEpisodeFormat = format;

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_reject_a_format_with_no_language_token()
        {
            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";

            Subject.Validate(_namingConfig)
                   .Should().ContainSingle()
                   .Which.PropertyName.Should().Be("standardEpisodeFormat");
        }

        [Test]
        public void should_reject_showing_the_flags_while_renaming_is_off()
        {
            _namingConfig.RenameEpisodes = false;

            Subject.Validate(_namingConfig)
                   .Should().ContainSingle()
                   .Which.PropertyName.Should().Be("showLanguageFlags");
        }

        [Test]
        public void should_say_nothing_about_a_format_the_library_never_reaches()
        {
            _namingConfig.AnimeEpisodeFormat = "{Series Title} - {absolute:000}";

            Subject.Validate(_namingConfig).Should().BeEmpty();
        }

        [Test]
        public void should_name_every_format_the_library_reaches()
        {
            GivenLibraryOf(SeriesTypes.Standard, SeriesTypes.Daily, SeriesTypes.Anime);

            _namingConfig.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";
            _namingConfig.AnimeEpisodeFormat = "{Series Title} - {absolute:000}";

            Subject.Validate(_namingConfig)
                   .Select(f => f.PropertyName)
                   .Should().BeEquivalentTo(new List<string> { "standardEpisodeFormat", "animeEpisodeFormat" });
        }
    }
}

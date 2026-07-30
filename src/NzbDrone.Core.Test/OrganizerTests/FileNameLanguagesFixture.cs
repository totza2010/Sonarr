using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class FileNameLanguagesFixture : CoreTest
    {
        [Test]
        public void should_read_the_groups_in_the_order_they_appear()
        {
            var groups = FileNameLanguages.Read("Series Title - S01E01 - Episode Title [EN+TH] [TH] WEBDL-1080p");

            groups.Should().HaveCount(2);
            groups[0].Should().BeEquivalentTo("EN", "TH");
            groups[1].Should().BeEquivalentTo("TH");
        }

        [Test]
        public void should_read_a_single_group_for_a_format_carrying_one_token()
        {
            FileNameLanguages.Read("Series Title - S01E01 - Episode Title [JA] WEBDL-1080p")
                             .Should().HaveCount(1);
        }

        [TestCase("Series Title - S01E01 [HD] WEBDL-1080p")]
        [TestCase("Series Title - S01E01 [AAC 2.0] [x264]")]
        [TestCase("Series Title - S01E01 [SubsPlease]")]
        [TestCase("")]
        public void should_not_mistake_other_brackets_for_languages(string fileName)
        {
            FileNameLanguages.Read(fileName).Should().BeEmpty();
        }

        [Test]
        public void should_take_the_case_the_token_writes_and_any_other()
        {
            FileNameLanguages.Read("Series Title [en+th]")[0].Should().BeEquivalentTo("EN", "TH");
        }

        // Nothing in a name says which group is which, so the format that wrote it has to.
        [TestCase("{Series Title}{.MediaInfo AudioLanguages}{.MediaInfo SubtitleLanguages}", false)]
        [TestCase("{Series Title}{.MediaInfo SubtitleLanguages}{.MediaInfo AudioLanguages}", true)]
        [TestCase("{Series Title} {MediaInfo AudioLanguages:TH+EN+ORIGINAL}", false)]
        [TestCase("{Series Title} {MediaInfo SubtitleLanguages:TH+EN}", true)]
        [TestCase("{Series Title} {Quality Full}", false)]
        public void should_take_the_order_from_the_format(string format, bool expected)
        {
            FileNameLanguages.SubtitlesComeFirst(format).Should().Be(expected);
        }

        [Test]
        public void should_fall_through_to_a_format_that_names_both()
        {
            // The standard format says nothing, so the question is settled by one that does.
            FileNameLanguages.SubtitlesComeFirst(
                "{Series Title} {Quality Full}",
                "{Series Title}{.MediaInfo SubtitleLanguages}{.MediaInfo AudioLanguages}")
                             .Should().BeTrue();
        }

        [Test]
        public void should_union_position_by_position()
        {
            var groups = FileNameLanguages.Union(new[]
            {
                "Series Title - S01E01 [EN] [EN]",
                "Series Title - S01E02 [EN+TH] [TH]"
            });

            groups.Should().HaveCount(2);
            groups[0].Should().BeEquivalentTo("EN", "TH");
            groups[1].Should().BeEquivalentTo("EN", "TH");
        }

        [Test]
        public void should_keep_a_longer_name_from_being_cut_short_by_a_shorter_one()
        {
            // One file with subtitles and one without still has a right-hand group.
            var groups = FileNameLanguages.Union(new[]
            {
                "Series Title - S01E01 [EN]",
                "Series Title - S01E02 [EN] [TH]"
            });

            groups.Should().HaveCount(2);
        }
    }
}

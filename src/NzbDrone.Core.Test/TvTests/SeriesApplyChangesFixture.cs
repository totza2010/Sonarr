using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesApplyChangesFixture : CoreTest
    {
        [Test]
        public void should_apply_a_naming_language_that_was_sent()
        {
            var series = new Series { NamingLanguage = Language.Unknown };

            series.ApplyChanges(new Series { NamingLanguage = Language.Thai });

            series.NamingLanguage.Should().Be(Language.Thai);
        }

        [Test]
        public void should_keep_the_naming_language_when_none_was_sent()
        {
            // A client that predates the field sends nothing, which deserialises to null. Treating that
            // as a value would wipe the setting, and the column cannot hold null so the save would fail.
            var series = new Series { NamingLanguage = Language.Thai };

            series.ApplyChanges(new Series { NamingLanguage = null });

            series.NamingLanguage.Should().Be(Language.Thai);
        }
    }
}

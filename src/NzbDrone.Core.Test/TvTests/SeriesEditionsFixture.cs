using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesEditionsFixture
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void should_treat_a_missing_edition_name_as_the_main_edition(string editionName)
        {
            SeriesEditions.IsMainEdition(editionName).Should().BeTrue();
            SeriesEditions.NormalizeEditionName(editionName).Should().Be(SeriesEditions.MainEdition);
        }

        [Test]
        public void should_trim_the_edition_name()
        {
            SeriesEditions.NormalizeEditionName("  Black & White  ").Should().Be("Black & White");
        }

        [Test]
        public void should_not_change_the_slug_of_the_main_edition()
        {
            SeriesEditions.ApplyEditionToSlug("spider-noir", SeriesEditions.MainEdition).Should().Be("spider-noir");
        }

        [TestCase("Black & White", "spider-noir-black-white")]
        [TestCase("Director's Cut", "spider-noir-directors-cut")]
        [TestCase("4:3", "spider-noir-43")]
        public void should_append_the_edition_to_the_slug(string editionName, string expected)
        {
            SeriesEditions.ApplyEditionToSlug("spider-noir", editionName).Should().Be(expected);
        }

        [Test]
        public void should_be_stable_across_metadata_refreshes()
        {
            // RefreshSeriesService reapplies this to the slug coming back from the metadata source,
            // so it has to produce the same result every time or the unique slug index breaks.
            var first = SeriesEditions.ApplyEditionToSlug("spider-noir", "Black & White");
            var second = SeriesEditions.ApplyEditionToSlug("spider-noir", "Black & White");

            second.Should().Be(first);
        }

        [Test]
        public void should_leave_the_slug_alone_when_the_edition_has_no_usable_characters()
        {
            SeriesEditions.ApplyEditionToSlug("spider-noir", "!!!").Should().Be("spider-noir");
        }
    }
}

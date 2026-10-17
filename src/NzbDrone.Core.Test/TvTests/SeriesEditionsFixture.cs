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

        [TestCase("Dragon Ball Z (1989) {tvdb-81472} {edition-Remastered}", "Remastered")]
        [TestCase("Attack on Titan (2013) {tvdb-267440} {edition-AI Upscale}", "AI Upscale")]
        [TestCase("Series {EDITION-Dubbed}", "Dubbed")]
        [TestCase("Series {edition- Bluray }", "Bluray")]
        public void should_read_the_edition_a_folder_name_carries(string folderName, string expected)
        {
            SeriesEditions.ReadEditionFromFolder(folderName).Should().Be(expected);
        }

        [TestCase("Dragon Ball Z (1989) {tvdb-81472}")]
        [TestCase("Special Edition Show (2020)")]
        [TestCase("Series {edition-}")]
        [TestCase("")]
        [TestCase(null)]
        public void should_read_no_edition_from_a_folder_that_names_none(string folderName)
        {
            // Every folder of a library without editions, which has to come back exactly as it always did.
            SeriesEditions.ReadEditionFromFolder(folderName).Should().BeNull();
        }

        [Test]
        public void should_read_the_last_edition_token_in_a_folder_name()
        {
            // GetSeriesFolder appends its token after whatever the folder format produced.
            SeriesEditions.ReadEditionFromFolder("Series {edition-Old} {edition-New}").Should().Be("New");
        }

        [Test]
        public void should_read_back_the_edition_the_folder_was_built_with()
        {
            // What is written and what is read have to agree, or a folder that drops out of the library
            // comes back as a different edition than it left as.
            var folder = $"Series (2020) {{edition-{SeriesEditions.NormalizeEditionName(" Director's Cut ")}}}";

            SeriesEditions.ReadEditionFromFolder(folder).Should().Be("Director's Cut");
        }

        [Test]
        public void should_take_the_edition_back_off_a_slug()
        {
            SeriesEditions.RemoveEditionFromSlug("spider-noir-black-and-white", "Black and White").Should().Be("spider-noir");
        }

        [Test]
        public void should_leave_a_slug_alone_when_it_does_not_end_with_the_edition()
        {
            SeriesEditions.RemoveEditionFromSlug("spider-noir", "Black and White").Should().Be("spider-noir");
        }

        [Test]
        public void should_leave_the_main_editions_slug_alone()
        {
            SeriesEditions.RemoveEditionFromSlug("spider-noir", "").Should().Be("spider-noir");
        }
    }
}

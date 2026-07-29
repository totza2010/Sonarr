using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    [TestFixture]
    public class AggregateMultipleFileFixture : CoreTest<AggregateMultipleFile>
    {
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Quality = new QualityModel(Quality.HDTV720p)
            };

            GivenNamingFormat("{Series Title} - S{season:00}E{episode:00} - {Multiple} {Quality Full}");
        }

        private void GivenNamingFormat(string standardFormat)
        {
            Mocker.GetMock<INamingConfigService>()
                  .Setup(s => s.GetConfig())
                  .Returns(new NamingConfig { StandardEpisodeFormat = standardFormat });
        }

        private LocalEpisode AggregateFor(string fileName)
        {
            _localEpisode.Path = $@"C:\Test\TV\Series\Season 01\{fileName}".AsOsAgnostic();

            return Subject.Aggregate(_localEpisode, null);
        }

        [Test]
        public void should_read_a_part_out_of_the_name()
        {
            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Part);
            result.MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_read_a_version_out_of_the_name()
        {
            var result = AggregateFor("Series Title - S01E01 - v2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Version);
            result.MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_ignore_a_name_with_no_marker()
        {
            var result = AggregateFor("Series Title - S01E01 - WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_do_nothing_when_the_naming_format_has_no_multiple_token()
        {
            // Nothing wrote the marker, so anything that looks like one came from somewhere else.
            GivenNamingFormat("{Series Title} - S{season:00}E{episode:00} - {Quality Full}");

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_leave_a_choice_made_during_a_manual_import_alone()
        {
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 3;

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleNumber.Should().Be(3);
        }

        [Test]
        public void should_not_claim_a_v2_that_is_a_repack()
        {
            // Scene and anime releases mark a repack with v2 and Sonarr reads it into the quality
            // revision. Taking it as a version would keep the repack beside the file it replaces.
            _localEpisode.Quality = new QualityModel(Quality.HDTV720p) { Revision = new Revision(2) };

            var result = AggregateFor("Series.Title.S01E01.1080p.v2.WEB.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_still_read_a_version_that_the_revision_does_not_account_for()
        {
            _localEpisode.Quality = new QualityModel(Quality.HDTV720p) { Revision = new Revision(2) };

            var result = AggregateFor("Series Title - S01E01 - v3 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Version);
            result.MultipleNumber.Should().Be(3);
        }

        [TestCase("Series Title - S01E01 - 01v2 WEBDL-1080p.mkv")]
        [TestCase("Series Title - S01E01 - abcpt2 WEBDL-1080p.mkv")]
        [TestCase("Series Title - S01E01 - pt2x WEBDL-1080p.mkv")]
        public void should_only_match_a_marker_standing_on_its_own(string fileName)
        {
            // Anime numbers episodes like "01v2", which is a release version and not ours to read.
            var result = AggregateFor(fileName);

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }
    }
}

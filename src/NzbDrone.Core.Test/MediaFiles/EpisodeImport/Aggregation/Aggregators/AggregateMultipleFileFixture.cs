using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
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
                Series = new Series(),
                Episodes = new List<Episode>(),
                Quality = new QualityModel(Quality.HDTV720p)
            };

            GivenNamingSupportsMultipleFiles(true);
        }

        private void GivenNamingSupportsMultipleFiles(bool supported)
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(v => v.SupportsMultipleFiles(It.IsAny<Series>(), It.IsAny<List<Episode>>()))
                  .Returns(supported);
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
        public void should_do_nothing_when_the_naming_cannot_keep_multiple_files()
        {
            // Nothing wrote the marker, so anything that looks like one came from somewhere else. This
            // covers renaming being off as well, since then no format is used at all.
            GivenNamingSupportsMultipleFiles(false);

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

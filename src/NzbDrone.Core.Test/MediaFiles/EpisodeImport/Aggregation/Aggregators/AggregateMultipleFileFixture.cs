using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
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
        private List<string> _siblings;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Series = new Series(),
                Episodes = new List<Episode> { new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1 } },
                Quality = new QualityModel(Quality.HDTV720p)
            };

            _siblings = new List<string>();

            GivenNamingSupportsMultipleFiles(true);

            Mocker.GetMock<IDiskProvider>().Setup(v => v.FolderExists(It.IsAny<string>())).Returns(true);
            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.GetFiles(It.IsAny<string>(), false))
                  .Returns(() => _siblings);
        }

        /// <summary>
        /// Other files in the same folder as the one being read - where the rest of a set arrives with it.
        /// </summary>
        private void GivenSiblings(params string[] fileNames)
        {
            foreach (var fileName in fileNames)
            {
                _siblings.Add($@"C:\Test\TV\Series\Season 01\{fileName}".AsOsAgnostic());
            }
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
            _siblings.Add(_localEpisode.Path);

            return Subject.Aggregate(_localEpisode, null);
        }

        [Test]
        public void should_read_a_part_out_of_the_name()
        {
            GivenSiblings("Series Title - S01E01 - pt1 WEBDL-1080p.mkv");

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Part);
            result.MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_read_a_version_out_of_the_name()
        {
            GivenSiblings("Series Title - S01E01 - v1 WEBDL-1080p.mkv");

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
            GivenSiblings("Series Title - S01E01 - v1 WEBDL-1080p.mkv", "Series Title - S01E01 - v2 WEBDL-1080p.mkv");

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

        private void GivenTitles(string seriesTitle, string episodeTitle, int seasonNumber = 2, int episodeNumber = 1)
        {
            _localEpisode.Series = new Series { Title = seriesTitle };
            _localEpisode.Episodes = new List<Episode>
            {
                new Episode { Id = 1, Title = episodeTitle, SeasonNumber = seasonNumber, EpisodeNumber = episodeNumber }
            };
        }

        [Test]
        public void should_not_read_a_version_out_of_the_episode_title()
        {
            // Kamen Rider V3's episodes carry the name in their titles. That is the title, not a version.
            GivenTitles("Kamen Rider", "Rider Number 3: His Name is V3!");

            var result = AggregateFor("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_not_read_a_version_out_of_the_series_title()
        {
            GivenTitles("Kamen Rider V3", "Episode Title", 1, 1);

            var result = AggregateFor("Kamen.Rider.V3.S01E01.Episode.Title.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }

        [Test]
        public void should_still_find_a_marker_beside_a_title_that_has_one()
        {
            // The name holds one more than its titles, and that one is the marker.
            GivenTitles("Kamen Rider", "Rider Number 3: His Name is V3!");
            GivenSiblings("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V1.BLURAY-1080P.mkv",
                          "Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V2.BLURAY-1080P.mkv");

            var result = AggregateFor("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V3.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Version);
            result.MultipleNumber.Should().Be(3);
        }

        [Test]
        public void should_find_a_different_marker_than_the_one_in_the_title()
        {
            GivenTitles("Kamen Rider", "Rider Number 3: His Name is V3!");
            GivenSiblings("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V1.BLURAY-1080P.mkv");

            var result = AggregateFor("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V2.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Version);
            result.MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_count_a_title_the_way_the_naming_format_cleans_it()
        {
            // "V3's" is cleaned to "V3s", which is no marker - but the title as written holds one, so the
            // single V3 in the name is still the title's and not a version.
            GivenTitles("Kamen Rider", "V3's 26 Secrets!?", 2, 4);

            var result = AggregateFor("Kamen.Rider.1971.S02E04.V3s.26.Secrets!.V3.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }

        [Test]
        public void should_not_read_a_third_version_without_the_first_two()
        {
            // A version 3 with nothing before it is not the third of anything.
            var result = AggregateFor("Series Title - S01E01 - v3 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
            result.MultipleNumber.Should().Be(0);
        }

        [Test]
        public void should_not_read_a_third_part_with_a_gap_before_it()
        {
            GivenSiblings("Series Title - S01E01 - pt1 WEBDL-1080p.mkv");

            var result = AggregateFor("Series Title - S01E01 - pt3 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }

        [Test]
        public void should_read_a_first_part_on_its_own()
        {
            // The first of a set needs nothing before it.
            var result = AggregateFor("Series Title - S01E01 - pt1 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Part);
            result.MultipleNumber.Should().Be(1);
        }

        [Test]
        public void should_count_the_rest_of_a_set_already_in_the_library()
        {
            // Part 1 imported last week, part 2 arriving today in a different folder.
            _localEpisode.Episodes[0].EpisodeFileId = 10;

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFiles(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<EpisodeFile> { new EpisodeFile { Id = 10, MultipleType = EpisodeFileMultipleType.Part, MultipleNumber = 1 } });

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Part);
            result.MultipleNumber.Should().Be(2);
        }

        [Test]
        public void should_count_an_earlier_member_owned_through_a_link()
        {
            Mocker.GetMock<IEpisodeFileLinkService>()
                  .Setup(v => v.GetLinkedFileIds(It.IsAny<List<int>>()))
                  .Returns(new List<int> { 11 });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFiles(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<EpisodeFile> { new EpisodeFile { Id = 11, MultipleType = EpisodeFileMultipleType.Version, MultipleNumber = 1 } });

            GivenSiblings("Series Title - S01E01 - v2 WEBDL-1080p.mkv");

            var result = AggregateFor("Series Title - S01E01 - v3 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.Version);
            result.MultipleNumber.Should().Be(3);
        }

        [Test]
        public void should_only_count_files_of_the_same_episode()
        {
            // The first part of a different episode says nothing about this one.
            GivenSiblings("Series Title - S01E02 - pt1 WEBDL-1080p.mkv");

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }

        [Test]
        public void should_not_count_a_version_towards_a_part()
        {
            GivenSiblings("Series Title - S01E01 - v1 WEBDL-1080p.mkv");

            var result = AggregateFor("Series Title - S01E01 - pt2 WEBDL-1080p.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }

        [Test]
        public void should_not_count_a_title_marker_on_a_sibling_as_its_version()
        {
            // The sibling holds V3 only in its title, so it is not version 1 of anything.
            GivenTitles("Kamen Rider", "Rider Number 3: His Name is V3!");
            GivenSiblings("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.BLURAY-1080P.mkv");

            var result = AggregateFor("Kamen.Rider.1971.S02E01.Rider.Number.3.His.Name.is.V3!.V2.BLURAY-1080P.mkv");

            result.MultipleType.Should().Be(EpisodeFileMultipleType.None);
        }
}
}

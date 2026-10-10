using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MultipleFileMarkerReaderFixture : CoreTest
    {
        private static MultipleFileMarker Read(string fileName, string seriesTitle = "Series Title", string episodeTitle = "Episode Title")
        {
            return MultipleFileMarkerReader.Read(fileName,
                                                 new Series { Title = seriesTitle },
                                                 new List<Episode> { new Episode { Title = episodeTitle } });
        }

        [TestCase("Series.Title.S01E01.Episode.Title.PT2.WEBDL-1080P", 2)]
        [TestCase("Series Title - S01E01 - pt2 WEBDL-1080p", 2)]
        [TestCase("S01E02 - Episode 2 - [WEBDL-1080p][VIU]-part4-AudioTHSubEN", 4)]
        [TestCase("Series Title - S01E01 - Part 3 - WEBDL-1080p", 3)]
        [TestCase("Series.Title.S01E01.Part.3.WEBDL-1080p", 3)]
        [TestCase("Series_Title_S01E01_part_3", 3)]
        public void should_read_the_forms_people_name_their_own_parts_with(string fileName, int number)
        {
            var marker = Read(fileName);

            marker.Type.Should().Be(EpisodeFileMultipleType.Part);
            marker.Number.Should().Be(number);
        }

        [TestCase("Series.Title.S01E01.Episode.Title.V2.WEBDL-1080P", 2)]
        [TestCase("Series Title - S01E01 - Version 2 - WEBDL-1080p", 2)]
        [TestCase("Series.Title.S01E01.version.3.WEBDL-1080p", 3)]
        public void should_read_versions(string fileName, int number)
        {
            var marker = Read(fileName);

            marker.Type.Should().Be(EpisodeFileMultipleType.Version);
            marker.Number.Should().Be(number);
        }

        [Test]
        public void should_not_read_a_part_the_episode_is_titled_with()
        {
            // The reason words like "part" used to be left alone: plenty of episodes are called this.
            Read("Series.Title.S01E05.The.Battle.Part.2.WEBDL-1080p", episodeTitle: "The Battle (Part 2)").Should().BeNull();
        }

        [Test]
        public void should_not_read_a_part_the_series_is_titled_with()
        {
            Read("Attack.on.Titan.Final.Season.Part.2.S04E17.WEBDL-1080p", "Attack on Titan Final Season Part 2").Should().BeNull();
        }

        [Test]
        public void should_read_a_marker_beside_a_title_that_holds_the_same_words()
        {
            var marker = Read("Series.Title.S01E05.The.Battle.Part.2.PT2.WEBDL-1080p", episodeTitle: "The Battle (Part 2)");

            marker.Type.Should().Be(EpisodeFileMultipleType.Part);
            marker.Number.Should().Be(2);
        }

        [TestCase("Series Title - S01E01 - 01v2 WEBDL-1080p")]
        [TestCase("Series Title - S01E01 - counterpart2 WEBDL-1080p")]
        [TestCase("Series Title - S01E01 - partial2 WEBDL-1080p")]
        [TestCase("Series Title - S01E01 - v 2 WEBDL-1080p")]
        [TestCase("Series Title - S01E01 - WEBDL-1080p")]
        public void should_not_read_anything_that_is_not_a_marker(string fileName)
        {
            Read(fileName).Should().BeNull();
        }

        [Test]
        public void should_read_the_marker_on_a_multi_episode_file_whose_titles_hold_the_same_letters()
        {
            // S00E04-E05 of Kamen Rider: both titles carry V3, the name carries them and a version.
            var marker = MultipleFileMarkerReader.Read("Kamen.Rider.1971.S00E04-E05.Kamen.Rider.V3.and.Kamen.Rider.V3.vs.Destron.V2.BLURAY-1080P.X264.AC3-[mxn]",
                                                       new Series { Title = "Kamen Rider" },
                                                       new List<Episode>
                                                       {
                                                           new Episode { Title = "Kamen Rider V3" },
                                                           new Episode { Title = "Kamen Rider V3 vs. Destron" }
                                                       });

            marker.Type.Should().Be(EpisodeFileMultipleType.Version);
            marker.Number.Should().Be(2);
        }
}
}

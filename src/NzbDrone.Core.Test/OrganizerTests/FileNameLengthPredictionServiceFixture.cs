using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class FileNameLengthPredictionServiceFixture : CoreTest<FileNameLengthPredictionService>
    {
        private NamingConfig _nameSpec;

        [SetUp]
        public void Setup()
        {
            _nameSpec = NamingConfig.Default;

            Mocker.GetMock<IConfigService>().SetupGet(s => s.FileNameLengthLimit).Returns(255);

            GivenSeries(new Series { Id = 1, Title = "Series", Path = @"C:\TV\Series".AsOsAgnostic() });
            GivenEpisodeTitles("Episode");
            GivenBuiltName("file.mkv");
        }

        private void GivenSeries(params Series[] series)
        {
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetAllSeries()).Returns(series.ToList());
        }

        private void GivenEpisodeTitles(params string[] titles)
        {
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.AllEpisodesWithFiles())
                  .Returns(titles.Select(t => new Episode { Title = t }).ToList());
        }

        private void GivenBuiltName(string fileName)
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.BuildFileName(It.IsAny<List<Episode>>(),
                                              It.IsAny<Series>(),
                                              It.IsAny<EpisodeFile>(),
                                              It.IsAny<string>(),
                                              It.IsAny<NamingConfig>(),
                                              null))
                  .Returns(fileName);
        }

        /// <summary>
        /// The series the format is actually applied to, which is the invented one rather than anything in
        /// the library and so is not on the result.
        /// </summary>
        private Series CapturedSeries()
        {
            Series captured = null;

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.BuildFileName(It.IsAny<List<Episode>>(),
                                              It.IsAny<Series>(),
                                              It.IsAny<EpisodeFile>(),
                                              It.IsAny<string>(),
                                              It.IsAny<NamingConfig>(),
                                              null))
                  .Callback((List<Episode> e, Series s, EpisodeFile f, string x, NamingConfig n, List<CustomFormat> c) => captured = s)
                  .Returns("file.mkv");

            Subject.Predict(_nameSpec);

            return captured;
        }

        [Test]
        public void should_keep_the_invented_title_rather_than_naming_a_real_series()
        {
            // A settings page that named a real series would be picking on whichever one happened to be
            // worst, and the answer is about the format rather than about that series.
            GivenSeries(new Series { Id = 1, Title = "Attack on Titan", Path = @"C:\TV\Attack on Titan".AsOsAgnostic() });

            Subject.Predict(_nameSpec).SeriesTitle.Should().NotContain("Attack on Titan");
        }

        [Test]
        public void should_stretch_the_invented_title_to_the_longest_title_held()
        {
            // The invented title is short, so a format measured against it looks comfortable no matter what
            // the library holds. Stretching it to the longest title is what makes the number a ceiling.
            GivenSeries(new Series { Id = 1, Title = new string('a', 120), Path = @"C:\TV\Series".AsOsAgnostic() });

            CapturedSeries().Title.GetByteCount().Should().Be(120);
        }

        [Test]
        public void should_stretch_the_episode_title_too()
        {
            GivenEpisodeTitles("Short", new string('a', 90));

            Subject.Predict(_nameSpec).EpisodeTitle.GetByteCount().Should().Be(90);
        }

        [Test]
        public void should_measure_titles_in_bytes_rather_than_characters()
        {
            // Thai costs three bytes a character, so a 40 character title is a 120 byte one - and it is
            // bytes that a file system counts.
            GivenSeries(new Series { Id = 1, Title = new string('ก', 40), Path = @"C:\TV\Series".AsOsAgnostic() });

            CapturedSeries().Title.GetByteCount().Should().Be(120);
        }

        [Test]
        public void should_leave_the_invented_title_alone_when_nothing_held_is_longer()
        {
            GivenSeries(new Series { Id = 1, Title = "TV", Path = @"C:\TV\TV".AsOsAgnostic() });

            CapturedSeries().Title.Should().Be("The Series Title's!");
        }

        [Test]
        public void should_sit_the_worst_case_under_the_longest_root_folder()
        {
            // A root folder is the one part of a path no naming format can shorten, so the longest one in
            // use is what the worst case has to sit under.
            GivenSeries(
                new Series { Id = 1, Title = "Series", Path = @"C:\TV\Series".AsOsAgnostic() },
                new Series { Id = 2, Title = "Other", Path = @"C:\A much longer root folder\Other".AsOsAgnostic() });

            Subject.Predict(_nameSpec).Path.Should().Contain("A much longer root folder");
        }

        [Test]
        public void should_measure_every_part_of_the_path_separately()
        {
            // Each part is fixed somewhere different - the file name and the folders by their own formats,
            // the root folder not at all - so each is worth its own number rather than one total.
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), It.IsAny<NamingConfig>()))
                  .Returns("Series Folder");

            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeasonFolder(It.IsAny<Series>(), It.IsAny<int>(), It.IsAny<NamingConfig>()))
                  .Returns("Season 01");

            var prediction = Subject.Predict(_nameSpec);

            prediction.FileNameLength.Should().Be(8);
            prediction.SeasonFolderLength.Should().Be(9);
            prediction.SeriesFolderLength.Should().Be(13);

            // The three names and the two separators between them.
            prediction.RelativePathLength.Should().Be(8 + 9 + 13 + 2);

            // And the root folder it all sits in, which no format can shorten.
            prediction.FullPathLength.Should().BeGreaterThan(prediction.RelativePathLength);
        }

        [Test]
        public void should_mark_a_name_past_the_limit()
        {
            GivenBuiltName(new string('a', 300) + ".mkv");

            var prediction = Subject.Predict(_nameSpec);

            prediction.FileNameLength.Should().Be(304);
            prediction.NameTooLong.Should().BeTrue();
        }

        [Test]
        public void should_mark_a_path_past_the_limit()
        {
            // The name is well inside its own limit; it is what sits above it that pushes the path past.
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FilePathLengthLimit).Returns(15);

            var prediction = Subject.Predict(_nameSpec);

            prediction.PathTooLong.Should().BeTrue();
            prediction.NameTooLong.Should().BeFalse();
        }

        [Test]
        public void should_still_measure_when_no_limit_is_set()
        {
            // The lengths answer "how long does this format get", which is worth knowing before there is a
            // limit to compare it to. A limit only decides what gets marked as past it.
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FileNameLengthLimit).Returns(0);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FilePathLengthLimit).Returns(0);

            GivenBuiltName(new string('a', 300) + ".mkv");

            var prediction = Subject.Predict(_nameSpec);

            prediction.FileNameLength.Should().Be(304);
            prediction.NameTooLong.Should().BeFalse();
            prediction.PathTooLong.Should().BeFalse();
        }

        [Test]
        public void should_answer_for_the_format_being_typed_rather_than_the_saved_one()
        {
            // The point is to answer for what is in the boxes now, and that is all three formats, not just
            // the one for the file name.
            Subject.Predict(_nameSpec);

            Mocker.GetMock<IBuildFileNames>()
                  .Verify(s => s.GetSeriesFolder(It.IsAny<Series>(), _nameSpec), Times.Once());

            Mocker.GetMock<IBuildFileNames>()
                  .Verify(s => s.GetSeasonFolder(It.IsAny<Series>(), It.IsAny<int>(), _nameSpec), Times.Once());
        }

        [Test]
        public void should_say_nothing_rather_than_fail_on_a_format_that_cannot_be_built()
        {
            // An empty format is already reported by the validation beside the field; the prediction has
            // nothing to add and must not take the settings page down with it.
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.BuildFileName(It.IsAny<List<Episode>>(),
                                              It.IsAny<Series>(),
                                              It.IsAny<EpisodeFile>(),
                                              It.IsAny<string>(),
                                              It.IsAny<NamingConfig>(),
                                              null))
                  .Throws(new NamingFormatException("Standard episode format cannot be empty"));

            Subject.Predict(_nameSpec).Should().BeNull();
        }

        [Test]
        public void should_cope_with_an_empty_library()
        {
            GivenSeries();
            GivenEpisodeTitles();

            Subject.Predict(_nameSpec).Should().NotBeNull();
        }
    }
}

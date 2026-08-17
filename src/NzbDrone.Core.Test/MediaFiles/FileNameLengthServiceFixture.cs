using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class FileNameLengthServiceFixture : CoreTest<FileNameLengthService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FileNameLengthLimit).Returns(255);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series> { new Series { Id = 1, Title = "Series", Path = @"C:\TV\Series".AsOsAgnostic() } });
        }

        private void GivenFiles(params string[] relativePaths)
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetAllFiles())
                  .Returns(relativePaths.Select(p => new EpisodeFile { SeriesId = 1, RelativePath = p }).ToList());
        }

        private static string NameOfBytes(int bytes)
        {
            return new string('a', bytes - 4) + ".mkv";
        }

        [Test]
        public void should_pass_over_a_name_within_the_limit()
        {
            GivenFiles($"Season 01/{NameOfBytes(255)}");

            Subject.Scan().Should().BeEmpty();
        }

        [Test]
        public void should_find_a_name_over_the_limit()
        {
            GivenFiles($"Season 01/{NameOfBytes(256)}");

            var overlong = Subject.Scan().Should().ContainSingle().Subject;

            overlong.NameLength.Should().Be(256);
            overlong.SeriesTitle.Should().Be("Series");
        }

        [Test]
        public void should_measure_the_name_rather_than_the_path()
        {
            // A season folder pushes the path past the limit long before the name reaches it, and it is
            // the name that a file system refuses.
            GivenFiles($"A very long season folder name that goes on and on/{NameOfBytes(200)}");

            Subject.Scan().Should().BeEmpty();
        }

        [Test]
        public void should_measure_in_bytes_rather_than_characters()
        {
            // Thai costs three bytes a character, so 100 characters is 300 bytes and over a limit that
            // 100 Latin characters would sit comfortably inside.
            GivenFiles("Season 01/" + new string('ก', 100) + ".mkv");

            Subject.Scan().Should().ContainSingle().Which.NameLength.Should().BeGreaterThan(255);
        }

        [Test]
        public void should_find_a_path_over_the_path_limit()
        {
            // The ceiling the person asked about first: a name well inside its own limit still reaches a
            // client that counts the whole path from its own drive letter.
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FilePathLengthLimit).Returns(40);

            GivenFiles($"Season 01/{NameOfBytes(60)}");

            var overlong = Subject.Scan().Should().ContainSingle().Subject;

            overlong.PathTooLong.Should().BeTrue();
            overlong.NameTooLong.Should().BeFalse();
            overlong.PathLength.Should().BeGreaterThan(overlong.NameLength);
        }

        [Test]
        public void should_leave_the_path_alone_when_only_the_name_limit_is_set()
        {
            // Path checking is off by default, since no one number is right for every way of reaching a
            // library, and turning it on by accident would warn about every file.
            GivenFiles($"Season 01/{NameOfBytes(100)}");

            Subject.Scan().Should().BeEmpty();
        }

        [Test]
        public void should_find_nothing_when_the_limit_is_off()
        {
            // Every install that has not asked for this takes the same path, and it must cost nothing.
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FileNameLengthLimit).Returns(0);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.FilePathLengthLimit).Returns(0);

            Subject.Scan().Should().BeEmpty();

            Mocker.GetMock<IMediaFileService>().Verify(s => s.GetAllFiles(), Times.Never());
        }

        [Test]
        public void should_check_every_series_rather_than_the_first()
        {
            // The files are walked a series at a time so the scan can say what it is on; that grouping must
            // not leave the later groups unchecked.
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series>
                  {
                      new Series { Id = 1, Title = "First", Path = @"C:\TV\First".AsOsAgnostic() },
                      new Series { Id = 2, Title = "Second", Path = @"C:\TV\Second".AsOsAgnostic() }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetAllFiles())
                  .Returns(new List<EpisodeFile>
                  {
                      new EpisodeFile { SeriesId = 1, RelativePath = $"Season 01/{NameOfBytes(300)}" },
                      new EpisodeFile { SeriesId = 2, RelativePath = $"Season 01/{NameOfBytes(300)}" }
                  });

            Subject.Scan().Select(f => f.SeriesTitle).Should().BeEquivalentTo("First", "Second");
        }

        [Test]
        public void should_skip_a_file_whose_series_is_gone()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetAllFiles())
                  .Returns(new List<EpisodeFile> { new EpisodeFile { SeriesId = 99, RelativePath = $"Season 01/{NameOfBytes(300)}" } });

            Subject.Scan().Should().BeEmpty();
        }

        [Test]
        public void should_report_what_the_last_scan_found()
        {
            GivenFiles($"Season 01/{NameOfBytes(300)}");

            Subject.GetOverlongFiles().Should().BeEmpty();

            Subject.Scan();

            Subject.GetOverlongFiles().Should().ContainSingle();
        }

        [Test]
        public void should_say_when_it_has_scanned_so_the_health_check_can_report()
        {
            GivenFiles($"Season 01/{NameOfBytes(100)}");

            Subject.Execute(new CheckFileNameLengthsCommand());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<FileNameLengthsScannedEvent>()), Times.Once());
        }
    }
}

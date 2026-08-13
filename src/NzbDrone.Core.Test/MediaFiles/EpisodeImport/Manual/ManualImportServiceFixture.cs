using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Manual
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        [SetUp]
        public void Setup()
        {
            // Nothing on disk: every download resolves to an output path that is neither a folder nor a
            // file, which is the shortest way through the method and leaves the walk over the ids as the
            // only thing under test.
            Mocker.GetMock<IDiskProvider>().Setup(s => s.FolderExists(It.IsAny<string>())).Returns(false);
            Mocker.GetMock<IDiskProvider>().Setup(s => s.FileExists(It.IsAny<string>())).Returns(false);
        }

        private void GivenTrackedDownload(string downloadId)
        {
            Mocker.GetMock<ITrackedDownloadService>()
                  .Setup(s => s.Find(downloadId))
                  .Returns(new TrackedDownload
                  {
                      ImportItem = new DownloadClientItem
                      {
                          OutputPath = new OsPath($@"C:\Downloads\{downloadId}".AsOsAgnostic())
                      }
                  });
        }

        [Test]
        public void should_look_up_every_download_it_is_given()
        {
            GivenTrackedDownload("a");
            GivenTrackedDownload("b");

            Subject.GetMediaFiles(new List<string> { "a", "b" }, true);

            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find("a"), Times.Once());
            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find("b"), Times.Once());
        }

        [Test]
        public void should_look_up_a_repeated_download_once()
        {
            GivenTrackedDownload("a");

            Subject.GetMediaFiles(new List<string> { "a", "a" }, true);

            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find("a"), Times.Once());
        }

        [Test]
        public void should_skip_empty_ids_rather_than_treat_them_as_a_folder()
        {
            // An empty id falls through to the path branch, which with no path would walk the library.
            GivenTrackedDownload("a");

            Subject.GetMediaFiles(new List<string> { "", null, "a" }, true);

            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_return_nothing_for_a_download_that_is_no_longer_tracked()
        {
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("gone")).Returns((TrackedDownload)null);

            Subject.GetMediaFiles(new List<string> { "gone" }, true).Should().BeEmpty();
        }

        [Test]
        public void should_carry_on_when_one_of_several_downloads_is_no_longer_tracked()
        {
            // One stale row in the queue must not cost the user the rest of the selection.
            GivenTrackedDownload("a");
            Mocker.GetMock<ITrackedDownloadService>().Setup(s => s.Find("gone")).Returns((TrackedDownload)null);

            Subject.GetMediaFiles(new List<string> { "gone", "a" }, true);

            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find("a"), Times.Once());
        }

        [Test]
        public void should_return_nothing_when_given_no_ids()
        {
            Subject.GetMediaFiles(new List<string>(), true).Should().BeEmpty();

            Mocker.GetMock<ITrackedDownloadService>().Verify(s => s.Find(It.IsAny<string>()), Times.Never());
        }
    }
}

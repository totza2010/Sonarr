using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
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

        private string GivenSingleFile(params Series[] editions)
        {
            var path = @"C:\Downloads\Series.S01E01.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(s => s.FileExists(path)).Returns(true);
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns(editions.First());
            Mocker.GetMock<ISeriesService>().Setup(s => s.FindAllByTvdbId(It.IsAny<int>())).Returns(editions.ToList());

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<bool>()))
                  .Returns((List<string> files, Series s, DownloadClientItem d, ParsedEpisodeInfo p, bool sc) =>
                      new List<ImportDecision> { new ImportDecision(new LocalEpisode { Path = files.First(), Series = s, Episodes = new List<Episode>() }) });

            return path;
        }

        private static Series GivenEdition(int id, string editionName, string path)
        {
            return new Series { Id = id, TvdbId = 5, EditionName = editionName, Path = path.AsOsAgnostic() };
        }

        [Test]
        public void should_not_ask_to_confirm_an_edition_when_the_series_has_only_one()
        {
            var path = GivenSingleFile(GivenEdition(1, null, @"C:\TV\Series"));

            Subject.GetMediaFiles(path, null, null, false)
                   .Should().OnlyContain(i => !i.EditionUnconfirmed);
        }

        [Test]
        public void should_ask_to_confirm_an_edition_when_the_name_landed_on_a_series_that_has_several()
        {
            // Title matching always lands on the main edition and never says so, which is the whole
            // reason the screen has to hold the import.
            var path = GivenSingleFile(
                GivenEdition(1, null, @"C:\TV\Series"),
                GivenEdition(2, "Extended", @"C:\TV\Series (Extended)"));

            Subject.GetMediaFiles(path, null, null, false)
                   .Should().OnlyContain(i => i.EditionUnconfirmed);
        }

        [Test]
        public void should_not_ask_to_confirm_an_edition_when_the_file_already_sits_in_one()
        {
            // Re-importing a library folder. The path has answered the question already.
            var path = @"C:\TV\Series (Extended)\Season 01\Series.S01E01.mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(s => s.FileExists(path)).Returns(true);
            Mocker.GetMock<IParsingService>().Setup(s => s.GetSeries(It.IsAny<string>())).Returns(GivenEdition(1, null, @"C:\TV\Series"));
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindAllByTvdbId(It.IsAny<int>()))
                  .Returns(new List<Series>
                  {
                      GivenEdition(1, null, @"C:\TV\Series"),
                      GivenEdition(2, "Extended", @"C:\TV\Series (Extended)")
                  });

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<bool>()))
                  .Returns((List<string> files, Series s, DownloadClientItem d, ParsedEpisodeInfo p, bool sc) =>
                      new List<ImportDecision> { new ImportDecision(new LocalEpisode { Path = files.First(), Series = s, Episodes = new List<Episode>() }) });

            Subject.GetMediaFiles(path, null, null, false)
                   .Should().OnlyContain(i => !i.EditionUnconfirmed);
        }
    }
}

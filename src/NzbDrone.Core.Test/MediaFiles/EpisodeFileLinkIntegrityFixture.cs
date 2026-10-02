using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class EpisodeFileLinkIntegrityFixture : CoreTest<EpisodeFileLinkIntegrity>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series> { new Series { Id = 1, Title = "Series" } });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisode(It.IsAny<int>()))
                  .Returns((int id) => new Episode { Id = id, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = id });
        }

        private void GivenLinks(params int[] episodeFileIds)
        {
            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Setup(s => s.All())
                  .Returns(episodeFileIds.Select((f, i) => new EpisodeFileLink { EpisodeId = i + 1, EpisodeFileId = f }).ToList());
        }

        private void GivenFiles(params int[] ids)
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetAllFiles())
                  .Returns(ids.Select(id => new EpisodeFile { Id = id, SeriesId = 1 }).ToList());
        }

        [Test]
        public void should_find_a_link_to_a_file_that_is_gone()
        {
            GivenLinks(10, 11);
            GivenFiles(10);

            var broken = Subject.BrokenLinks().Should().ContainSingle().Subject;

            broken.EpisodeFileId.Should().Be(11);
            broken.SeriesTitle.Should().Be("Series");
        }

        [Test]
        public void should_say_nothing_when_every_link_resolves()
        {
            GivenLinks(10, 11);
            GivenFiles(10, 11);

            Subject.BrokenLinks().Should().BeEmpty();
        }

        [Test]
        public void should_not_read_the_files_when_nothing_is_linked()
        {
            // Almost every library has no links at all, and that case must cost nothing.
            GivenLinks();

            Subject.BrokenLinks().Should().BeEmpty();

            Mocker.GetMock<IMediaFileService>().Verify(s => s.GetAllFiles(), Times.Never());
        }

        [Test]
        public void should_not_read_the_series_when_every_link_resolves()
        {
            GivenLinks(10);
            GivenFiles(10);

            Subject.BrokenLinks();

            Mocker.GetMock<ISeriesService>().Verify(s => s.GetAllSeries(), Times.Never());
        }

        [Test]
        public void should_report_a_link_whose_episode_is_gone()
        {
            // The episode can be gone as well, which leaves nothing to name - and the link is still there
            // to be cleared, so it still counts.
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisode(It.IsAny<int>())).Returns((Episode)null);

            GivenLinks(11);
            GivenFiles(10);

            Subject.BrokenLinks().Should().ContainSingle().Which.SeriesTitle.Should().BeNull();
        }

        [Test]
        public void should_name_a_broken_link_by_series_and_episode()
        {
            GivenLinks(11);
            GivenFiles(10);

            Subject.BrokenLinks().Single().ToString().Should().Be("Series - S01E01");
        }

        [Test]
        public void should_clear_the_broken_links_when_asked()
        {
            GivenLinks(10, 11);
            GivenFiles(10);

            Subject.Execute(new CleanEpisodeFileLinksCommand());

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.DeleteByEpisodeFileIds(It.Is<List<int>>(ids => ids.Count == 1 && ids.Contains(11))),
                          Times.Once());
        }

        [Test]
        public void should_leave_the_links_that_still_resolve()
        {
            // The repair drops only what points at nothing; an extra part still on disk is not a fault.
            GivenLinks(10);
            GivenFiles(10);

            Subject.Execute(new CleanEpisodeFileLinksCommand());

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.DeleteByEpisodeFileIds(It.IsAny<List<int>>()), Times.Never());
        }

        [Test]
        public void should_say_when_it_has_cleaned_so_the_health_check_can_report()
        {
            GivenLinks(11);
            GivenFiles(10);

            Subject.Execute(new CleanEpisodeFileLinksCommand());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<EpisodeFileLinksCleanedEvent>()), Times.Once());
        }
}
}

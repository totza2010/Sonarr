using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class EpisodeFileLinkServiceFixture : CoreTest<EpisodeFileLinkService>
    {
        private void GivenLinks(params (int EpisodeId, int EpisodeFileId)[] links)
        {
            var rows = links.Select(l => new EpisodeFileLink { EpisodeId = l.EpisodeId, EpisodeFileId = l.EpisodeFileId }).ToList();

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Setup(s => s.GetByEpisodeIds(It.IsAny<List<int>>()))
                  .Returns<List<int>>(ids => rows.Where(r => ids.Contains(r.EpisodeId)).ToList());

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Setup(s => s.GetByEpisodeFileIds(It.IsAny<List<int>>()))
                  .Returns<List<int>>(ids => rows.Where(r => ids.Contains(r.EpisodeFileId)).ToList());
        }

        [Test]
        public void should_link_a_file_to_an_episode()
        {
            GivenLinks();

            Subject.Link(1, 5);

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.Insert(It.Is<EpisodeFileLink>(l => l.EpisodeId == 1 && l.EpisodeFileId == 5)), Times.Once());
        }

        [Test]
        public void should_not_link_the_same_file_twice()
        {
            GivenLinks((1, 5));

            Subject.Link(1, 5);

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.Insert(It.IsAny<EpisodeFileLink>()), Times.Never());
        }

        [Test]
        public void should_return_no_file_ids_for_no_episodes()
        {
            // Guards the repository from a query with an empty list, which every caller can produce.
            Subject.GetLinkedFileIds(new List<int>()).Should().BeEmpty();

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.GetByEpisodeIds(It.IsAny<List<int>>()), Times.Never());
        }

        [Test]
        public void should_return_each_linked_file_once()
        {
            GivenLinks((1, 5), (2, 5), (1, 6));

            Subject.GetLinkedFileIds(new List<int> { 1, 2 }).Should().BeEquivalentTo(new[] { 5, 6 });
        }

        [Test]
        public void should_map_files_back_to_their_episodes()
        {
            GivenLinks((1, 5), (2, 5), (1, 6));

            var result = Subject.GetEpisodeIdsByFileIds(new List<int> { 5, 6 });

            result[5].Should().BeEquivalentTo(new[] { 1, 2 });
            result[6].Should().BeEquivalentTo(new[] { 1 });
        }

        [Test]
        public void should_remove_links_pointing_at_files_that_are_gone()
        {
            GivenLinks((1, 5), (1, 6));

            Subject.RemoveLinksToMissingFiles(new List<int> { 1 }, new List<int> { 5 });

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.DeleteByEpisodeFileIds(It.Is<List<int>>(ids => ids.Count == 1 && ids.Contains(6))), Times.Once());
        }

        [Test]
        public void should_not_touch_links_when_every_file_still_exists()
        {
            GivenLinks((1, 5));

            Subject.RemoveLinksToMissingFiles(new List<int> { 1 }, new List<int> { 5 });

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.DeleteByEpisodeFileIds(It.IsAny<List<int>>()), Times.Never());
        }

        [Test]
        public void should_drop_the_link_when_the_file_is_deleted()
        {
            GivenLinks((1, 5));

            Subject.Handle(new EpisodeFileDeletedEvent(new EpisodeFile { Id = 5 }, DeleteMediaFileReason.Manual));

            Mocker.GetMock<IEpisodeFileLinkRepository>()
                  .Verify(v => v.DeleteByEpisodeFileIds(It.Is<List<int>>(ids => ids.Contains(5))), Times.Once());
        }
    }
}

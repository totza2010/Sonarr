using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.HistoryTests
{
    public class HistoryServiceFixture : CoreTest<HistoryService>
    {
        private QualityProfile _profile;
        private QualityProfile _profileCustom;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile
                {
                    Cutoff = Quality.WEBDL720p.Id,
                    Items = QualityFixture.GetDefaultQualities(),
                };

            _profileCustom = new QualityProfile
                {
                    Cutoff = Quality.WEBDL720p.Id,
                    Items = QualityFixture.GetDefaultQualities(Quality.DVD),
                };
        }

        [Test]
        public void should_use_file_name_for_source_title_if_scene_name_is_null()
        {
            var series = Builder<Series>.CreateNew().Build();
            var episodes = Builder<Episode>.CreateListOfSize(1).Build().ToList();
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                                                  .With(f => f.SceneName = null)
                                                  .Build();

            var localEpisode = new LocalEpisode
                               {
                                   Series = series,
                                   Episodes = episodes,
                                   Path = @"C:\Test\Unsorted\Series.s01e01.mkv"
                               };

            var downloadClientItem = new DownloadClientItem
                                     {
                                         DownloadClientInfo = new DownloadClientItemClientInfo
                                         {
                                             Protocol = DownloadProtocol.Usenet,
                                             Id = 1,
                                             Name = "sab"
                                         },
                                         DownloadId = "abcd"
                                     };

            Subject.Handle(new EpisodeImportedEvent(localEpisode, episodeFile, new List<DeletedEpisodeFile>(), true, downloadClientItem));

            Mocker.GetMock<IHistoryRepository>()
                .Verify(v => v.Insert(It.Is<EpisodeHistory>(h => h.SourceTitle == Path.GetFileNameWithoutExtension(localEpisode.Path))));
        }

        private EpisodeFileDeletedEvent GivenDeleted(DeleteMediaFileReason reason, List<int> linkedEpisodeIds = null)
        {
            var episodeFile = new EpisodeFile
            {
                Id = 5,
                SeriesId = 1,
                Path = @"C:\TV\Seriesile.mkv",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>()
            };

            // The relation an ordinary file's episodes arrive through, and the one an extra part or version
            // is always absent from.
            episodeFile.Episodes = linkedEpisodeIds == null
                ? new List<Episode> { new Episode { Id = 9, SeriesId = 1 } }
                : new List<Episode>();

            return new EpisodeFileDeletedEvent(episodeFile, reason, linkedEpisodeIds);
        }

        [Test]
        public void should_record_an_additional_file_being_replaced()
        {
            // Replacing an ordinary file is skipped because the import that replaced it is recorded in its
            // place. An extra version has no such trace, so skipping it leaves its removal invisible - and
            // that is exactly the hole that made a broken link impossible to explain after the fact.
            Subject.Handle(GivenDeleted(DeleteMediaFileReason.ManualOverride, new List<int> { 9 }));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(v => v.Insert(It.Is<EpisodeHistory>(h => h.EpisodeId == 9 &&
                                                                   h.EventType == EpisodeHistoryEventType.EpisodeFileDeleted)),
                          Times.Once());
        }

        [Test]
        public void should_record_an_additional_file_removed_by_the_cleanup_routine()
        {
            Subject.Handle(GivenDeleted(DeleteMediaFileReason.NoLinkedEpisodes, new List<int> { 9 }));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(v => v.Insert(It.IsAny<EpisodeHistory>()), Times.Once());
        }

        [Test]
        public void should_still_skip_an_ordinary_file_being_replaced()
        {
            // Unchanged from upstream: the import that replaced it is the record.
            Subject.Handle(GivenDeleted(DeleteMediaFileReason.ManualOverride));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(v => v.Insert(It.IsAny<EpisodeHistory>()), Times.Never());
        }

        [Test]
        public void should_still_skip_an_ordinary_file_removed_by_the_cleanup_routine()
        {
            Subject.Handle(GivenDeleted(DeleteMediaFileReason.NoLinkedEpisodes));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(v => v.Insert(It.IsAny<EpisodeHistory>()), Times.Never());
        }

        [Test]
        public void should_record_an_additional_file_once_per_episode_that_owned_it()
        {
            Subject.Handle(GivenDeleted(DeleteMediaFileReason.Manual, new List<int> { 9, 10 }));

            Mocker.GetMock<IHistoryRepository>()
                  .Verify(v => v.Insert(It.IsAny<EpisodeHistory>()), Times.Exactly(2));
        }
}
}

using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedEpisodeFilesFixture : DbTest<CleanupOrphanedEpisodeFiles, EpisodeFile>
    {
        [Test]
        public void should_delete_orphaned_episode_files()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .BuildNew();

            Db.Insert(episodeFile);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_unorphaned_episode_files()
        {
            var episodeFiles = Builder<EpisodeFile>.CreateListOfSize(2)
                .All()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .BuildListOfNew();

            Db.InsertMany(episodeFiles);

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.EpisodeFileId = episodeFiles.First().Id)
                .BuildNew();

            Db.Insert(episode);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
            Db.All<Episode>().Should().Contain(e => e.EpisodeFileId == AllStoredModels.First().Id);
        }

        private EpisodeFile GivenFile()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .BuildNew();

            Db.Insert(episodeFile);

            return episodeFile;
        }

        [Test]
        public void should_not_delete_an_extra_part_or_version()
        {
            // No episode points at an extra file - the episode points at its first file, and owns the
            // others through a link. This ran once a day and took every one of them with it.
            var primary = GivenFile();
            var extra = GivenFile();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.EpisodeFileId = primary.Id)
                .BuildNew();

            Db.Insert(episode);
            Db.Insert(new EpisodeFileLink { EpisodeId = episode.Id, EpisodeFileId = extra.Id });

            Subject.Clean();

            AllStoredModels.Select(f => f.Id).Should().BeEquivalentTo(new[] { primary.Id, extra.Id });
        }

        [Test]
        public void should_delete_an_extra_file_whose_episode_is_gone()
        {
            // The link is what keeps it, and a link naming an episode that no longer exists keeps nothing.
            var extra = GivenFile();

            Db.Insert(new EpisodeFileLink { EpisodeId = 999, EpisodeFileId = extra.Id });

            Subject.Clean();

            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_still_delete_an_orphan_beside_a_linked_file()
        {
            var primary = GivenFile();
            var extra = GivenFile();
            var orphan = GivenFile();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.EpisodeFileId = primary.Id)
                .BuildNew();

            Db.Insert(episode);
            Db.Insert(new EpisodeFileLink { EpisodeId = episode.Id, EpisodeFileId = extra.Id });

            Subject.Clean();

            AllStoredModels.Select(f => f.Id).Should().NotContain(orphan.Id);
            AllStoredModels.Should().HaveCount(2);
        }
}
}

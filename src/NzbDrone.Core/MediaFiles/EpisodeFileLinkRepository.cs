using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IEpisodeFileLinkRepository : IBasicRepository<EpisodeFileLink>
    {
        List<EpisodeFileLink> GetByEpisodeIds(List<int> episodeIds);
        List<EpisodeFileLink> GetByEpisodeFileIds(List<int> episodeFileIds);
        void DeleteByEpisodeFileIds(List<int> episodeFileIds);
        void DeleteByEpisodeIds(List<int> episodeIds);
    }

    public class EpisodeFileLinkRepository : BasicRepository<EpisodeFileLink>, IEpisodeFileLinkRepository
    {
        public EpisodeFileLinkRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<EpisodeFileLink> GetByEpisodeIds(List<int> episodeIds)
        {
            return Query(l => episodeIds.Contains(l.EpisodeId));
        }

        public List<EpisodeFileLink> GetByEpisodeFileIds(List<int> episodeFileIds)
        {
            return Query(l => episodeFileIds.Contains(l.EpisodeFileId));
        }

        public void DeleteByEpisodeFileIds(List<int> episodeFileIds)
        {
            Delete(l => episodeFileIds.Contains(l.EpisodeFileId));
        }

        public void DeleteByEpisodeIds(List<int> episodeIds)
        {
            Delete(l => episodeIds.Contains(l.EpisodeId));
        }
    }
}

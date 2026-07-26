using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IEpisodeFileLinkService
    {
        void Link(int episodeId, int episodeFileId);
        List<int> GetLinkedFileIds(List<int> episodeIds);
        bool IsLinked(int episodeFileId);
    }

    /// <summary>
    /// Tracks the extra files an episode owns. Two files belong to the same episode without replacing
    /// each other when they are different parts of it, or different versions of it; anything else is
    /// still an upgrade and still replaces what is there.
    /// </summary>
    public class EpisodeFileLinkService : IEpisodeFileLinkService,
                                          IHandle<EpisodeFileDeletedEvent>,
                                          IHandle<SeriesDeletedEvent>
    {
        private readonly IEpisodeFileLinkRepository _repository;
        private readonly IEpisodeService _episodeService;
        private readonly Logger _logger;

        public EpisodeFileLinkService(IEpisodeFileLinkRepository repository,
                                      IEpisodeService episodeService,
                                      Logger logger)
        {
            _repository = repository;
            _episodeService = episodeService;
            _logger = logger;
        }

        public void Link(int episodeId, int episodeFileId)
        {
            var existing = _repository.GetByEpisodeIds(new List<int> { episodeId });

            if (existing.Any(l => l.EpisodeFileId == episodeFileId))
            {
                return;
            }

            _logger.Debug("Linking file {0} to episode {1} as an additional file", episodeFileId, episodeId);

            _repository.Insert(new EpisodeFileLink
            {
                EpisodeId = episodeId,
                EpisodeFileId = episodeFileId
            });
        }

        public List<int> GetLinkedFileIds(List<int> episodeIds)
        {
            if (episodeIds.Empty())
            {
                return new List<int>();
            }

            return _repository.GetByEpisodeIds(episodeIds).Select(l => l.EpisodeFileId).Distinct().ToList();
        }

        public bool IsLinked(int episodeFileId)
        {
            return _repository.GetByEpisodeFileIds(new List<int> { episodeFileId }).Any();
        }

        public void Handle(EpisodeFileDeletedEvent message)
        {
            _repository.DeleteByEpisodeFileIds(new List<int> { message.EpisodeFile.Id });
        }

        public void Handle(SeriesDeletedEvent message)
        {
            var episodeIds = message.Series
                .SelectMany(s => _episodeService.GetEpisodeBySeries(s.Id))
                .Select(e => e.Id)
                .ToList();

            if (episodeIds.Any())
            {
                _repository.DeleteByEpisodeIds(episodeIds);
            }
        }
    }
}

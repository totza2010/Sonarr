using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface ICheckEpisodeFileLinks
    {
        List<BrokenEpisodeFileLink> BrokenLinks();
    }

    public class BrokenEpisodeFileLink
    {
        public int EpisodeId { get; set; }
        public int EpisodeFileId { get; set; }
        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }

        /// <summary>
        /// What to put in front of someone: the series and the episode inside it, which together are
        /// enough to open the right page. The file id would be exact and mean nothing to anybody.
        /// </summary>
        public override string ToString()
        {
            return SeriesTitle.IsNullOrWhiteSpace()
                ? $"episode {EpisodeId}"
                : $"{SeriesTitle} - S{SeasonNumber:00}E{EpisodeNumber:00}";
        }
    }

    /// <summary>
    /// Finds links pointing at files that are no longer there.
    ///
    /// An extra part or version belongs to an episode through the link table alone, so a link left behind
    /// makes an episode claim a file nothing can open. It cost a series page once: the browser asked for
    /// the missing file, got a 404, and the page reported that it could not load any files at all.
    ///
    /// Reads both tables whole rather than joining them, which is affordable because only extra parts and
    /// versions are linked at all - a library of a hundred thousand files has a link table of dozens.
    /// </summary>
    public class EpisodeFileLinkIntegrity : ICheckEpisodeFileLinks, IExecute<CleanEpisodeFileLinksCommand>
    {
        private readonly IEpisodeFileLinkRepository _linkRepository;
        private readonly IMediaFileService _mediaFileService;
        private readonly IEpisodeService _episodeService;
        private readonly ISeriesService _seriesService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public EpisodeFileLinkIntegrity(IEpisodeFileLinkRepository linkRepository,
                                        IMediaFileService mediaFileService,
                                        IEpisodeService episodeService,
                                        ISeriesService seriesService,
                                        IEventAggregator eventAggregator,
                                        Logger logger)
        {
            _linkRepository = linkRepository;
            _mediaFileService = mediaFileService;
            _episodeService = episodeService;
            _seriesService = seriesService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<BrokenEpisodeFileLink> BrokenLinks()
        {
            var links = _linkRepository.All().ToList();

            if (links.Empty())
            {
                return new List<BrokenEpisodeFileLink>();
            }

            var fileIds = _mediaFileService.GetAllFiles().Select(f => f.Id).ToHashSet();
            var broken = links.Where(l => !fileIds.Contains(l.EpisodeFileId)).ToList();

            if (broken.Empty())
            {
                return new List<BrokenEpisodeFileLink>();
            }

            // Named by series rather than by episode, because the repair is per series: a rescan is what
            // clears these, and a rescan takes a series at a time.
            var series = _seriesService.GetAllSeries().ToDictionary(s => s.Id);

            return broken.Select(l =>
            {
                var episode = _episodeService.GetEpisode(l.EpisodeId);
                var seriesId = episode?.SeriesId ?? 0;

                return new BrokenEpisodeFileLink
                {
                    EpisodeId = l.EpisodeId,
                    EpisodeFileId = l.EpisodeFileId,
                    SeriesId = seriesId,
                    SeriesTitle = series.GetValueOrDefault(seriesId)?.Title,
                    SeasonNumber = episode?.SeasonNumber ?? 0,
                    EpisodeNumber = episode?.EpisodeNumber ?? 0
                };
            }).ToList();
        }

        /// <summary>
        /// Clears them. Nothing is lost by it - a link to a file that is not there says only that an
        /// episode once had an extra part or version, and keeping it does nothing but break the page that
        /// tries to show it.
        /// </summary>
        public void Execute(CleanEpisodeFileLinksCommand message)
        {
            var broken = BrokenLinks();

            if (broken.Any())
            {
                foreach (var link in broken)
                {
                    _logger.Debug("Removing link from {0} to file {1}, which no longer exists", link, link.EpisodeFileId);
                }

                _linkRepository.DeleteByEpisodeFileIds(broken.Select(b => b.EpisodeFileId).Distinct().ToList());
            }

            _logger.ProgressInfo("Removed {0} link(s) pointing at files that no longer exist", broken.Count);

            _eventAggregator.PublishEvent(new EpisodeFileLinksCleanedEvent());
        }
    }
}

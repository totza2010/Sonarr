using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Sonarr.Api.V3.EpisodeFiles;
using Sonarr.Api.V3.Series;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Episodes
{
    public abstract class EpisodeControllerWithSignalR : RestControllerWithSignalR<EpisodeResource, Episode>,
                                                         IHandle<EpisodeGrabbedEvent>,
                                                         IHandle<EpisodeImportedEvent>,
                                                         IHandle<EpisodeFileDeletedEvent>
    {
        protected readonly IEpisodeService _episodeService;
        protected readonly ISeriesService _seriesService;
        protected readonly IUpgradableSpecification _upgradableSpecification;
        protected readonly ICustomFormatCalculationService _formatCalculator;
        protected readonly IEpisodeFileLinkService _episodeFileLinkService;

        protected EpisodeControllerWithSignalR(IEpisodeService episodeService,
                                           ISeriesService seriesService,
                                           IUpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatCalculator,
                                           IEpisodeFileLinkService episodeFileLinkService,
                                           IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _episodeService = episodeService;
            _seriesService = seriesService;
            _upgradableSpecification = upgradableSpecification;
            _formatCalculator = formatCalculator;
            _episodeFileLinkService = episodeFileLinkService;
        }

        protected EpisodeControllerWithSignalR(IEpisodeService episodeService,
                                           ISeriesService seriesService,
                                           IUpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatCalculator,
                                           IEpisodeFileLinkService episodeFileLinkService,
                                           IBroadcastSignalRMessage signalRBroadcaster,
                                           string resource)
            : base(signalRBroadcaster)
        {
            _episodeService = episodeService;
            _seriesService = seriesService;
            _upgradableSpecification = upgradableSpecification;
            _formatCalculator = formatCalculator;
            _episodeFileLinkService = episodeFileLinkService;
        }

        protected override EpisodeResource GetResourceById(int id)
        {
            var episode = _episodeService.GetEpisode(id);
            var resource = MapToResource(episode, true, true, true);
            return resource;
        }

        protected EpisodeResource MapToResource(Episode episode, bool includeSeries, bool includeEpisodeFile, bool includeImages)
        {
            var resource = episode.ToResource();

            if (includeSeries || includeEpisodeFile || includeImages)
            {
                var series = episode.Series ?? _seriesService.GetSeries(episode.SeriesId);

                if (includeSeries)
                {
                    resource.Series = series.ToResource();
                }

                if (includeEpisodeFile && episode.EpisodeFileId != 0)
                {
                    resource.EpisodeFile = episode.EpisodeFile.Value.ToResource(series, _upgradableSpecification, _formatCalculator);
                }

                if (includeImages)
                {
                    resource.Images = episode.Images;
                }
            }

            AddAdditionalFiles(new List<EpisodeResource> { resource });

            return resource;
        }

        /// <summary>
        /// Fills in the extra parts and versions each episode owns. They are only reachable through the link
        /// table, so they are looked up for the whole batch at once rather than per episode.
        /// </summary>
        private void AddAdditionalFiles(List<EpisodeResource> resources)
        {
            var episodeIds = resources.Select(r => r.Id).ToList();
            var links = _episodeFileLinkService.GetLinkedFileIds(episodeIds);

            if (links.Empty())
            {
                return;
            }

            var fileIdsByEpisode = new Dictionary<int, List<int>>();

            foreach (var link in _episodeFileLinkService.GetEpisodeIdsByFileIds(links))
            {
                foreach (var episodeId in link.Value)
                {
                    if (!fileIdsByEpisode.TryGetValue(episodeId, out var fileIds))
                    {
                        fileIds = new List<int>();
                        fileIdsByEpisode[episodeId] = fileIds;
                    }

                    fileIds.Add(link.Key);
                }
            }

            foreach (var resource in resources)
            {
                var fileIds = fileIdsByEpisode.GetValueOrDefault(resource.Id)
                    ?.Where(id => id != resource.EpisodeFileId)
                    .OrderBy(id => id)
                    .ToList();

                if (fileIds == null || fileIds.Empty())
                {
                    continue;
                }

                resource.AdditionalEpisodeFileIds = fileIds;
            }
        }

        protected List<EpisodeResource> MapToResource(List<Episode> episodes, bool includeSeries, bool includeEpisodeFile, bool includeImages)
        {
            var result = episodes.ToResource();

            if (includeSeries || includeEpisodeFile || includeImages)
            {
                var seriesDict = new Dictionary<int, NzbDrone.Core.Tv.Series>();
                for (var i = 0; i < episodes.Count; i++)
                {
                    var episode = episodes[i];
                    var resource = result[i];

                    var series = episode.Series ?? seriesDict.GetValueOrDefault(episodes[i].SeriesId) ?? _seriesService.GetSeries(episodes[i].SeriesId);
                    seriesDict[series.Id] = series;

                    if (includeSeries)
                    {
                        resource.Series = series.ToResource();
                    }

                    if (includeEpisodeFile && episode.EpisodeFileId != 0)
                    {
                        resource.EpisodeFile = episode.EpisodeFile.Value.ToResource(series, _upgradableSpecification, _formatCalculator);
                    }

                    if (includeImages)
                    {
                        resource.Images = episode.Images;
                    }
                }
            }

            AddAdditionalFiles(result);

            return result;
        }

        [NonAction]
        public void Handle(EpisodeGrabbedEvent message)
        {
            foreach (var episode in message.Episode.Episodes)
            {
                var resource = episode.ToResource();
                resource.Grabbed = true;

                BroadcastResourceChange(ModelAction.Updated, resource);
            }
        }

        [NonAction]
        public void Handle(EpisodeImportedEvent message)
        {
            foreach (var episode in message.EpisodeInfo.Episodes)
            {
                BroadcastResourceChange(ModelAction.Updated, episode.Id);
            }
        }

        [NonAction]
        public void Handle(EpisodeFileDeletedEvent message)
        {
            foreach (var episode in message.EpisodeFile.Episodes.Value)
            {
                BroadcastResourceChange(ModelAction.Updated, episode.Id);
            }
        }
    }
}

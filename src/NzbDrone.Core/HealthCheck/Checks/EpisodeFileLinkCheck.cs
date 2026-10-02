using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.HealthCheck.Checks
{
    /// <summary>
    /// Says so when an episode still claims an extra part or version that is no longer there.
    ///
    /// The symptom without this is a series page that refuses to load any files at all, with nothing in
    /// the log to say why, because the missing file is only ever asked for by the browser. A rescan of the
    /// series clears it, so the check exists to say which series to rescan.
    /// </summary>
    [CheckOn(typeof(SeriesRefreshCompleteEvent))]
    [CheckOn(typeof(EpisodeFileDeletedEvent))]
    [CheckOn(typeof(EpisodeFileLinksCleanedEvent))]
    public class EpisodeFileLinkCheck : HealthCheckBase
    {
        private const int EpisodesToName = 3;

        private readonly ICheckEpisodeFileLinks _linkIntegrity;

        public EpisodeFileLinkCheck(ICheckEpisodeFileLinks linkIntegrity, ILocalizationService localizationService)
            : base(localizationService)
        {
            _linkIntegrity = linkIntegrity;
        }

        public override HealthCheck Check()
        {
            var broken = _linkIntegrity.BrokenLinks();

            if (broken.Empty())
            {
                return new HealthCheck(GetType());
            }

            // The episode, not just the series: a series can have hundreds, and "which one" is the first
            // thing anyone asks. Ordered so the same library always reads the same way.
            var episodes = broken.Select(b => b.ToString())
                                 .Distinct()
                                 .OrderBy(t => t)
                                 .ToList();

            // Naming every one would run to a paragraph on a library that has drifted; the first few are
            // enough to recognise the shape of the problem, and the rest is a count.
            var named = episodes.Take(EpisodesToName).Join(", ");
            var episodeText = episodes.Count > EpisodesToName
                ? $"{named} and {episodes.Count - EpisodesToName} more"
                : named;

            var message = _localizationService.GetLocalizedString("EpisodeFileLinkHealthCheckMessage",
                new Dictionary<string, object>
                {
                    { "files", broken.Count },
                    { "episodes", episodeText }
                });

            return new HealthCheck(GetType(), HealthCheckResult.Warning, message, "#missing-episode-files");
        }
    }
}

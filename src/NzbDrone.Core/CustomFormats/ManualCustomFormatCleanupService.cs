using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.CustomFormats
{
    /// <summary>
    /// Drops a custom format that was added to a file by hand once the file's own name matches it.
    /// The point of writing formats into the name is that the name becomes the record; keeping the
    /// hand-added copy after that would leave two places saying the same thing, and only one of them
    /// would survive the file being imported somewhere else.
    /// </summary>
    public class ManualCustomFormatCleanupService : IHandle<EpisodeFileRenamedEvent>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly Logger _logger;

        public ManualCustomFormatCleanupService(IMediaFileService mediaFileService,
                                                ICustomFormatCalculationService formatCalculator,
                                                Logger logger)
        {
            _mediaFileService = mediaFileService;
            _formatCalculator = formatCalculator;
            _logger = logger;
        }

        public void Handle(EpisodeFileRenamedEvent message)
        {
            var episodeFile = message.EpisodeFile;

            var hasAdded = episodeFile.ManualCustomFormats?.Any() == true;
            var hasExcluded = episodeFile.ExcludedCustomFormats?.Any() == true;

            if (!hasAdded && !hasExcluded)
            {
                return;
            }

            // Deliberately not the merged list: only what the new name matches on its own counts, or
            // every hand-added format would look like it had been earned and remove itself.
            var matched = _formatCalculator.ParseCustomFormatFromName(episodeFile, message.Series)
                                           .Select(f => f.Id)
                                           .ToList();

            // Added ones the name now earns, and excluded ones the name no longer offers: both have
            // been settled by the name itself and have nothing left to say.
            var added = hasAdded
                ? episodeFile.ManualCustomFormats.Where(id => !matched.Contains(id)).ToList()
                : episodeFile.ManualCustomFormats;

            var excluded = hasExcluded
                ? episodeFile.ExcludedCustomFormats.Where(id => matched.Contains(id)).ToList()
                : episodeFile.ExcludedCustomFormats;

            if (added.Count == episodeFile.ManualCustomFormats?.Count &&
                excluded.Count == episodeFile.ExcludedCustomFormats?.Count)
            {
                return;
            }

            _logger.Debug("{0} was renamed and its name now settles some of its formats, dropping those", episodeFile);

            episodeFile.ManualCustomFormats = added;
            episodeFile.ExcludedCustomFormats = excluded;
            _mediaFileService.Update(episodeFile);
        }
    }
}

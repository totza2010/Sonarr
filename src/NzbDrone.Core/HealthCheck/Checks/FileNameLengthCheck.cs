using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;

namespace NzbDrone.Core.HealthCheck.Checks
{
    /// <summary>
    /// Sonarr copes with names that other things reading the same library do not, so a file it manages
    /// happily can be one a player or a share cannot open, and nothing says so. Reports what the scan
    /// found rather than looking itself, which is why it listens for the scan rather than for imports.
    /// </summary>
    [CheckOn(typeof(FileNameLengthsScannedEvent))]
    public class FileNameLengthCheck : HealthCheckBase
    {
        private const int SeriesToName = 3;

        private readonly IFileNameLengthService _fileNameLengthService;

        public FileNameLengthCheck(IFileNameLengthService fileNameLengthService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _fileNameLengthService = fileNameLengthService;
        }

        public override HealthCheck Check()
        {
            var overlong = _fileNameLengthService.GetOverlongFiles();

            if (overlong.Empty())
            {
                return new HealthCheck(GetType());
            }

            var series = overlong.Select(f => f.SeriesTitle).Distinct().OrderBy(t => t).ToList();

            // Naming every series would run to a paragraph on a library that has drifted; the first few
            // are enough to recognise the shape of the problem, and the rest is a count.
            var named = series.Take(SeriesToName).Join(", ");
            var seriesText = series.Count > SeriesToName
                ? $"{named} and {series.Count - SeriesToName} more"
                : named;

            // Which ceiling was passed changes what to do about it - a name is shortened by the naming
            // format, a path by moving the library or shortening the folders above it.
            var byName = overlong.Count(f => f.NameTooLong);
            var byPath = overlong.Count(f => f.PathTooLong);

            var messageKey = byName > 0 && byPath > 0
                ? "FileNameAndPathLengthHealthCheckMessage"
                : byPath > 0 ? "FilePathLengthHealthCheckMessage" : "FileNameLengthHealthCheckMessage";

            var message = _localizationService.GetLocalizedString(messageKey, new Dictionary<string, object>
            {
                { "files", overlong.Count },
                { "names", byName },
                { "paths", byPath },
                { "series", seriesText }
            });

            // The wiki fragment is spelled out rather than left to be built from the message, which would
            // otherwise put the titles of a library into a URL and move the link every time a file changed.
            return new HealthCheck(GetType(), HealthCheckResult.Warning, message, "#file-name-length");
        }
    }
}

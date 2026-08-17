using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    /// <summary>
    /// Raised when the scan finishes so the health check can report the new answer. The check reads what
    /// was found rather than working it out itself, which keeps it from walking the library every time
    /// any event at all is raised.
    /// </summary>
    public class FileNameLengthsScannedEvent : IEvent
    {
    }
}

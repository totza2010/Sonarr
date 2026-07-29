using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class EpisodeFileAddedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }

        // The file joins the episode instead of taking the place of what it already has: another part
        // of it, or another version. Said explicitly by whoever imported it rather than inferred from
        // the file, so an ordinary import cannot be mistaken for one by accident.
        public bool IsAdditionalFile { get; private set; }

        public EpisodeFileAddedEvent(EpisodeFile episodeFile, bool isAdditionalFile = false)
        {
            EpisodeFile = episodeFile;
            IsAdditionalFile = isAdditionalFile;
        }
    }
}

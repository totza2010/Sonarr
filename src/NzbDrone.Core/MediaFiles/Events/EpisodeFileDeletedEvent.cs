using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class EpisodeFileDeletedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }
        public DeleteMediaFileReason Reason { get; private set; }

        /// <summary>
        /// The episodes that owned this file as an extra part or version. Carried on the event because it
        /// is read from a table this deletion is about to empty, and because handlers run in no particular
        /// order - a handler that looked it up for itself might find it already gone.
        ///
        /// Empty for an ordinary file, whose episodes are on <see cref="EpisodeFile"/> itself.
        /// </summary>
        public List<int> LinkedEpisodeIds { get; private set; }

        public EpisodeFileDeletedEvent(EpisodeFile episodeFile, DeleteMediaFileReason reason, List<int> linkedEpisodeIds = null)
        {
            EpisodeFile = episodeFile;
            Reason = reason;
            LinkedEpisodeIds = linkedEpisodeIds ?? new List<int>();
        }
    }
}

using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class CleanEpisodeFileLinksCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}

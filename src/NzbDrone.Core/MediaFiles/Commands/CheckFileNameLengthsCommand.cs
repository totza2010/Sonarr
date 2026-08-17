using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class CheckFileNameLengthsCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}

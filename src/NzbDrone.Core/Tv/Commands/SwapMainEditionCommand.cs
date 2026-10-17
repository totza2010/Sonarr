using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.Commands
{
    /// <summary>
    /// Makes the given edition the main one, and the main one an edition under the name the given one
    /// had. Each keeps its own files, episodes and settings; what changes hands is the name, the slug
    /// and the folder.
    /// </summary>
    public class SwapMainEditionCommand : Command
    {
        public int SeriesId { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
    }
}

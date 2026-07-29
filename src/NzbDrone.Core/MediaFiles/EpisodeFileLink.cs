using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles
{
    /// <summary>
    /// An extra file an episode owns beyond the one Episode.EpisodeFileId points at: a further part of
    /// a split episode, or another version of it. The primary file is deliberately not listed here, so
    /// everything that reads Episode.EpisodeFileId keeps working untouched.
    /// </summary>
    public class EpisodeFileLink : ModelBase
    {
        public int EpisodeId { get; set; }
        public int EpisodeFileId { get; set; }
    }
}

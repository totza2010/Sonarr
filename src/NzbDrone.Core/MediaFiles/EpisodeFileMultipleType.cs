namespace NzbDrone.Core.MediaFiles
{
    /// <summary>
    /// Why an episode owns more than one file. A part is a piece of the episode, a version is the whole of
    /// it told differently. They behave identically, so they share one field and differ only in the marker
    /// written into the file name.
    /// </summary>
    public enum EpisodeFileMultipleType
    {
        None = 0,
        Part = 1,
        Version = 2
    }
}

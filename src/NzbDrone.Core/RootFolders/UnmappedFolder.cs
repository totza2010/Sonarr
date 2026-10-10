namespace NzbDrone.Core.RootFolders
{
    public class UnmappedFolder
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string RelativePath { get; set; }

        /// <summary>
        /// The edition the folder's own name says it holds, read from the {edition-...} token the series
        /// folder format writes. Null when the name carries none, which is every folder of a library that
        /// has no editions.
        /// </summary>
        public string EditionName { get; set; }
    }
}

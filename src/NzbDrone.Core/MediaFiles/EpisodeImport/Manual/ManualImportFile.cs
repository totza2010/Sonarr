using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Manual
{
    public class ManualImportFile : IEquatable<ManualImportFile>
    {
        public string Path { get; set; }
        public string FolderName { get; set; }
        public int SeriesId { get; set; }
        public List<int> EpisodeIds { get; set; }
        public int? EpisodeFileId { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }

        // Chosen before the file is imported, so the very first name it gets is already right.
        public List<Language> NamingAudioLanguages { get; set; }
        public List<Language> NamingSubtitleLanguages { get; set; }
        public List<int> ManualCustomFormats { get; set; }
        public List<int> ExcludedCustomFormats { get; set; }
        public string ReleaseGroup { get; set; }
        public int IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public string DownloadId { get; set; }

        public bool Equals(ManualImportFile other)
        {
            if (other == null)
            {
                return false;
            }

            return Path.PathEquals(other.Path);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Path.PathEquals(((ManualImportFile)obj).Path);
        }

        public override int GetHashCode()
        {
            return Path != null ? Path.GetHashCode() : 0;
        }
    }
}

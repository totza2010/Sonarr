using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public class EpisodeFile : ModelBase
    {
        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public string RelativePath { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }
        public string OriginalFilePath { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseHash { get; set; }
        public QualityModel Quality { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public MediaInfoModel MediaInfo { get; set; }
        public LazyLoaded<List<Episode>> Episodes { get; set; }
        public LazyLoaded<Series> Series { get; set; }
        public List<Language> Languages { get; set; }

        // What the naming tokens should say this file's audio and subtitles are. Empty means take
        // MediaInfo's word for it. Kept apart from Languages, which decides upgrades and profiles, so
        // correcting a file name cannot quietly change what Sonarr grabs next.
        // The columns are not nullable, so these default rather than leaving every insert to fail.
        public List<Language> NamingAudioLanguages { get; set; } = new List<Language>();
        public List<Language> NamingSubtitleLanguages { get; set; } = new List<Language>();

        // Custom formats this file was given by hand, added to the ones its name matches, and ones it
        // matches but was told to ignore. Both empty means the name speaks for itself.
        public List<int> ManualCustomFormats { get; set; } = new List<int>();
        public List<int> ExcludedCustomFormats { get; set; } = new List<int>();
        public ReleaseType ReleaseType { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, RelativePath);
        }

        public string GetSceneOrFileName()
        {
            if (SceneName.IsNotNullOrWhiteSpace())
            {
                return SceneName;
            }

            if (RelativePath.IsNotNullOrWhiteSpace())
            {
                return System.IO.Path.GetFileNameWithoutExtension(RelativePath);
            }

            if (Path.IsNotNullOrWhiteSpace())
            {
                return System.IO.Path.GetFileNameWithoutExtension(Path);
            }

            return string.Empty;
        }
    }
}

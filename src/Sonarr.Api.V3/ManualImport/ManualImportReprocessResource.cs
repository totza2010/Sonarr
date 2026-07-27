using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using Sonarr.Api.V3.CustomFormats;
using Sonarr.Api.V3.Episodes;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.ManualImport
{
    public class ManualImportReprocessResource : RestResource
    {
        public string Path { get; set; }
        public int SeriesId { get; set; }
        public int? SeasonNumber { get; set; }
        public List<EpisodeResource> Episodes { get; set; }
        public List<int> EpisodeIds { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }
        public string ReleaseGroup { get; set; }
        public string DownloadId { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public int IndexerFlags { get; set; }
        public ReleaseType ReleaseType { get; set; }

        // Round-tripped untouched: reprocessing recomputes rejections, and which part of an episode a
        // file is has no bearing on those. Carried so a reprocess does not discard what the user set.
        public int PartNumber { get; set; }
        public IEnumerable<ImportRejectionResource> Rejections { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using Sonarr.Api.V3.CustomFormats;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.EpisodeFiles
{
    public class EpisodeFileResource : RestResource
    {
        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public string RelativePath { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public List<Language> Languages { get; set; }

        // Null unless the file was told what to report, so a file that just uses MediaInfo serialises
        // exactly as it did before these existed.
        public List<Language> NamingAudioLanguages { get; set; }
        public List<Language> NamingSubtitleLanguages { get; set; }
        public List<int> ManualCustomFormats { get; set; }
        public List<int> ExcludedCustomFormats { get; set; }
        public QualityModel Quality { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public int? IndexerFlags { get; set; }
        public ReleaseType? ReleaseType { get; set; }
        public MediaInfoResource MediaInfo { get; set; }

        public bool QualityCutoffNotMet { get; set; }
    }

    public static class EpisodeFileResourceMapper
    {
        public static EpisodeFileResource ToResource(this EpisodeFile model, NzbDrone.Core.Tv.Series series, IUpgradableSpecification upgradableSpecification, ICustomFormatCalculationService formatCalculationService)
        {
            if (model == null)
            {
                return null;
            }

            model.Series = series;
            var customFormats = formatCalculationService?.ParseCustomFormat(model, model.Series);

            // Shown and scored are not the same list: a hand-added format is shown so it can be managed,
            // but only what the file matches on its own counts, or renaming a file would raise its score.
            var scoredFormats = formatCalculationService?.ParseScoredCustomFormat(model, model.Series);
            var customFormatScore = series?.QualityProfile?.Value?.CalculateCustomFormatScore(scoredFormats) ?? 0;

            return new EpisodeFileResource
            {
                Id = model.Id,

                SeriesId = model.SeriesId,
                SeasonNumber = model.SeasonNumber,
                RelativePath = model.RelativePath,
                Path = Path.Combine(series.Path, model.RelativePath),
                Size = model.Size,
                DateAdded = model.DateAdded,
                SceneName = model.SceneName,
                ReleaseGroup = model.ReleaseGroup,
                Languages = model.Languages,
                NamingAudioLanguages = model.NamingAudioLanguages?.Any() == true ? model.NamingAudioLanguages : null,
                NamingSubtitleLanguages = model.NamingSubtitleLanguages?.Any() == true ? model.NamingSubtitleLanguages : null,
                ManualCustomFormats = model.ManualCustomFormats?.Any() == true ? model.ManualCustomFormats : null,
                ExcludedCustomFormats = model.ExcludedCustomFormats?.Any() == true ? model.ExcludedCustomFormats : null,
                Quality = model.Quality,
                MediaInfo = model.MediaInfo.ToResource(model.SceneName),
                QualityCutoffNotMet = upgradableSpecification.QualityCutoffNotMet(series.QualityProfile.Value, model.Quality),
                CustomFormats = customFormats.ToResource(false),
                CustomFormatScore = customFormatScore,
                IndexerFlags = (int)model.IndexerFlags,
                ReleaseType = model.ReleaseType,
            };
        }
    }
}

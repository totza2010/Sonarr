using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V3.Config
{
    /// <summary>
    /// How long the format being typed comes out for the longest titles and root folder the library holds.
    /// Added rather than replacing anything, so a consumer that has never asked for this sees the same
    /// response it always has.
    /// </summary>
    public class NamingLengthPredictionResource
    {
        public string SeriesTitle { get; set; }
        public string EpisodeTitle { get; set; }
        public string Path { get; set; }
        public int FileNameLength { get; set; }
        public int SeasonFolderLength { get; set; }
        public int SeriesFolderLength { get; set; }
        public int RelativePathLength { get; set; }
        public int FullPathLength { get; set; }
        public int NameLimit { get; set; }
        public int PathLimit { get; set; }
        public bool NameTooLong { get; set; }
        public bool PathTooLong { get; set; }
    }

    public class NamingExampleResource
    {
        public NamingLengthPredictionResource LengthPrediction { get; set; }
        public string SingleEpisodeExample { get; set; }
        public string MultiEpisodeExample { get; set; }
        public string DailyEpisodeExample { get; set; }
        public string AnimeEpisodeExample { get; set; }
        public string AnimeMultiEpisodeExample { get; set; }
        public string SeriesFolderExample { get; set; }
        public string SeasonFolderExample { get; set; }
        public string SpecialsFolderExample { get; set; }
    }

    public static class NamingConfigResourceMapper
    {
        public static NamingLengthPredictionResource ToResource(this FileNameLengthPrediction model)
        {
            if (model == null)
            {
                return null;
            }

            return new NamingLengthPredictionResource
            {
                SeriesTitle = model.SeriesTitle,
                EpisodeTitle = model.EpisodeTitle,
                Path = model.Path,
                FileNameLength = model.FileNameLength,
                SeasonFolderLength = model.SeasonFolderLength,
                SeriesFolderLength = model.SeriesFolderLength,
                RelativePathLength = model.RelativePathLength,
                FullPathLength = model.FullPathLength,
                NameLimit = model.NameLimit,
                PathLimit = model.PathLimit,
                NameTooLong = model.NameTooLong,
                PathTooLong = model.PathTooLong
            };
        }

        public static NamingConfigResource ToResource(this NamingConfig model)
        {
            return new NamingConfigResource
            {
                Id = model.Id,

                RenameEpisodes = model.RenameEpisodes,
                ReplaceIllegalCharacters = model.ReplaceIllegalCharacters,
                ColonReplacementFormat = (int)model.ColonReplacementFormat,
                CustomColonReplacementFormat = model.CustomColonReplacementFormat,
                MultiEpisodeStyle = (int)model.MultiEpisodeStyle,
                StandardEpisodeFormat = model.StandardEpisodeFormat,
                DailyEpisodeFormat = model.DailyEpisodeFormat,
                AnimeEpisodeFormat = model.AnimeEpisodeFormat,
                SeriesFolderFormat = model.SeriesFolderFormat,
                SeasonFolderFormat = model.SeasonFolderFormat,
                SpecialsFolderFormat = model.SpecialsFolderFormat,
                ShowLanguageFlags = model.ShowLanguageFlags
            };
        }

        public static NamingConfig ToModel(this NamingConfigResource resource)
        {
            return new NamingConfig
            {
                Id = resource.Id,

                RenameEpisodes = resource.RenameEpisodes,
                ReplaceIllegalCharacters = resource.ReplaceIllegalCharacters,
                MultiEpisodeStyle = (MultiEpisodeStyle)resource.MultiEpisodeStyle,
                ColonReplacementFormat = (ColonReplacementFormat)resource.ColonReplacementFormat,
                CustomColonReplacementFormat = resource.CustomColonReplacementFormat ?? "",
                StandardEpisodeFormat = resource.StandardEpisodeFormat,
                DailyEpisodeFormat = resource.DailyEpisodeFormat,
                AnimeEpisodeFormat = resource.AnimeEpisodeFormat,
                SeriesFolderFormat = resource.SeriesFolderFormat,
                SeasonFolderFormat = resource.SeasonFolderFormat,
                SpecialsFolderFormat = resource.SpecialsFolderFormat,
                ShowLanguageFlags = resource.ShowLanguageFlags
            };
        }
    }
}

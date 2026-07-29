using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Organizer
{
    public interface IValidateMultipleFileNaming
    {
        List<ValidationFailure> Validate(NamingConfig nameSpec);
    }

    /// <summary>
    /// Keeps a naming config from being saved in a state that would destroy episodes already split
    /// across several files. There is only one such state: renaming on with a format that has no
    /// {Multiple} in it. Renaming then computes the same name for every part of an episode, and the
    /// next rename - after an import, or from Organize - drops them all onto one path.
    ///
    /// Turning renaming off is not that state and is not blocked. Nothing is renamed at all then, so
    /// the files that exist keep the names they have; the feature simply stops being available, which
    /// the import specification and the UI both take care of.
    ///
    /// Nothing is required of a library that has no such files, which is every library that has never
    /// used the feature.
    /// </summary>
    public class MultipleFileNamingValidator : IValidateMultipleFileNaming
    {
        private const string FormatMessage = "Must contain {Multiple} while episodes of this type are split across several files";

        private readonly IMediaFileService _mediaFileService;
        private readonly ISeriesService _seriesService;

        public MultipleFileNamingValidator(IMediaFileService mediaFileService, ISeriesService seriesService)
        {
            _mediaFileService = mediaFileService;
            _seriesService = seriesService;
        }

        public List<ValidationFailure> Validate(NamingConfig nameSpec)
        {
            var failures = new List<ValidationFailure>();

            // No format is used while renaming is off, so no format can be wrong.
            if (!nameSpec.RenameEpisodes)
            {
                return failures;
            }

            var seriesIds = _mediaFileService.SeriesIdsWithMultipleFiles();

            if (seriesIds.Empty())
            {
                return failures;
            }

            // Only the formats that are actually reached. Asking for the token in the anime format
            // because a standard series has parts would be a rule about nothing.
            var types = _seriesService.GetSeries(seriesIds).Select(s => s.SeriesType).Distinct().ToList();

            foreach (var type in types)
            {
                var property = type switch
                {
                    SeriesTypes.Daily => "DailyEpisodeFormat",
                    SeriesTypes.Anime => "AnimeEpisodeFormat",
                    _ => "StandardEpisodeFormat"
                };

                var format = type switch
                {
                    SeriesTypes.Daily => nameSpec.DailyEpisodeFormat,
                    SeriesTypes.Anime => nameSpec.AnimeEpisodeFormat,
                    _ => nameSpec.StandardEpisodeFormat
                };

                if (!ContainsMultipleToken(format))
                {
                    failures.Add(new ValidationFailure(property, FormatMessage));
                }
            }

            return failures;
        }

        private static bool ContainsMultipleToken(string format)
        {
            return format.IsNotNullOrWhiteSpace() &&
                   format.Contains("{Multiple", System.StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

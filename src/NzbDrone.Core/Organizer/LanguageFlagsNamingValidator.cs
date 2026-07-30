using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Organizer
{
    public interface IValidateLanguageFlagsNaming
    {
        List<ValidationFailure> Validate(NamingConfig nameSpec);
    }

    /// <summary>
    /// The language flags are read out of file names, so they can only be shown where a format puts
    /// them there. This says so at the point of saving rather than leaving a switch that turns on and
    /// shows nothing.
    ///
    /// It reads both ways round, because it is one rule and either side can break it: turning the
    /// switch on while a format has no language token, and taking the token out of a format while the
    /// switch is on, are the same mistake arriving from two directions.
    ///
    /// Renaming has to be on as well. With it off the names are whatever they already were and no
    /// format is ever applied, so nothing would put the languages in them.
    ///
    /// Nothing is asked of a library with the switch off, which is every library that has not turned it
    /// on.
    /// </summary>
    public class LanguageFlagsNamingValidator : IValidateLanguageFlagsNaming
    {
        private const string FormatMessage = "Must contain {MediaInfo AudioLanguages} or {MediaInfo SubtitleLanguages} while language flags are shown";
        private const string RenameMessage = "Language flags are read out of file names, so renaming has to be on to show them";

        private readonly ISeriesService _seriesService;

        public LanguageFlagsNamingValidator(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        public List<ValidationFailure> Validate(NamingConfig nameSpec)
        {
            var failures = new List<ValidationFailure>();

            if (!nameSpec.ShowLanguageFlags)
            {
                return failures;
            }

            if (!nameSpec.RenameEpisodes)
            {
                failures.Add(new ValidationFailure("showLanguageFlags", RenameMessage));

                return failures;
            }

            // Only the formats that are actually reached. Asking for the token in the anime format of a
            // library with no anime in it would be a rule about nothing.
            var types = _seriesService.GetAllSeries().Select(s => s.SeriesType).Distinct().ToList();

            foreach (var type in types)
            {
                var property = type switch
                {
                    SeriesTypes.Daily => "dailyEpisodeFormat",
                    SeriesTypes.Anime => "animeEpisodeFormat",
                    _ => "standardEpisodeFormat"
                };

                var format = type switch
                {
                    SeriesTypes.Daily => nameSpec.DailyEpisodeFormat,
                    SeriesTypes.Anime => nameSpec.AnimeEpisodeFormat,
                    _ => nameSpec.StandardEpisodeFormat
                };

                if (!ContainsLanguageToken(format))
                {
                    failures.Add(new ValidationFailure(property, FormatMessage));
                }
            }

            return failures;
        }

        private static bool ContainsLanguageToken(string format)
        {
            return NamingTokens.Contains(format, "MediaInfo AudioLanguages") ||
                   NamingTokens.Contains(format, "MediaInfo SubtitleLanguages");
        }
    }
}

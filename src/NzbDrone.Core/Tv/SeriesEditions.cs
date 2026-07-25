using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Tv
{
    /// <summary>
    /// Implemented by anything that can identify a specific edition of a series, so validation can
    /// check the TVDB ID and the edition together without knowing the concrete type.
    /// </summary>
    public interface ISeriesEditionIdentity
    {
        string EditionName { get; }
    }

    /// <summary>
    /// A series edition is a second (third, ...) copy of the same TVDB series holding a different
    /// release of the same episodes (black and white vs colour, dubbed vs original, remastered, ...).
    /// The main edition is identified by an empty edition name, so existing series are unaffected.
    /// </summary>
    public static class SeriesEditions
    {
        public const string MainEdition = "";

        public static string NormalizeEditionName(string editionName)
        {
            return editionName.IsNullOrWhiteSpace() ? MainEdition : editionName.Trim();
        }

        public static bool IsMainEdition(string editionName)
        {
            return editionName.IsNullOrWhiteSpace();
        }

        /// <summary>
        /// Series.TitleSlug is unique, so editions of the same series need a slug of their own.
        /// The suffix is derived from the edition name, which keeps it stable across metadata refreshes.
        /// </summary>
        public static string ApplyEditionToSlug(string titleSlug, string editionName)
        {
            if (IsMainEdition(editionName) || titleSlug.IsNullOrWhiteSpace())
            {
                return titleSlug;
            }

            var suffix = NormalizeEditionName(editionName).ToUrlSlug();

            if (suffix.IsNullOrWhiteSpace())
            {
                return titleSlug;
            }

            return $"{titleSlug}-{suffix}";
        }
    }
}

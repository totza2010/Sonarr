using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        // The token GetSeriesFolder appends, which is also the one Plex reads. The last one wins, since it
        // is appended after everything the folder format itself produced.
        private static readonly Regex EditionFolderRegex = new Regex(@"\{edition-(?<edition>[^{}]+)\}",
                                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// The edition a folder name carries, or null when it carries none. The name is written by
        /// GetSeriesFolder, so a folder that has dropped out of the library still says which edition it
        /// was - without this it is indistinguishable from a stray second copy of the main one.
        /// </summary>
        public static string ReadEditionFromFolder(string folderName)
        {
            if (folderName.IsNullOrWhiteSpace())
            {
                return null;
            }

            var match = EditionFolderRegex.Match(folderName);
            var edition = match.Success ? match.Groups["edition"].Value.Trim() : null;

            return edition.IsNullOrWhiteSpace() ? null : edition;
        }

        /// <summary>
        /// Whether a file is headed for an edition nobody has chosen. Editions of a series share a title,
        /// so matching a name always lands on the main one and never says that it guessed; only the path
        /// can settle it on its own, by the file already sitting inside an edition's folder.
        /// </summary>
        public static bool EditionIsAmbiguous(IReadOnlyCollection<Series> editions, string path)
        {
            if (editions == null || editions.Count <= 1 || path.IsNullOrWhiteSpace())
            {
                return false;
            }

            return FindEditionForPath(editions, path) == null;
        }

        /// <summary>
        /// The edition a path already belongs to, by sitting inside that edition's folder. The only way
        /// an edition is settled without somebody saying so.
        /// </summary>
        public static Series FindEditionForPath(IEnumerable<Series> editions, string path)
        {
            if (editions == null || path.IsNullOrWhiteSpace())
            {
                return null;
            }

            return editions.FirstOrDefault(s => s.Path.IsParentPath(path) || s.Path.PathEquals(path));
        }

        public static string NormalizeEditionName(string editionName)
        {
            return editionName.IsNullOrWhiteSpace() ? MainEdition : editionName.Trim();
        }

        public static bool IsMainEdition(string editionName)
        {
            return editionName.IsNullOrWhiteSpace();
        }

        /// <summary>
        /// Two editions of a series cannot differ by case alone: they would be one folder on Windows
        /// and two labels in Plex.
        /// </summary>
        public static bool SameEdition(string editionName, string otherEditionName)
        {
            return NormalizeEditionName(editionName)
                .Equals(NormalizeEditionName(otherEditionName), StringComparison.InvariantCultureIgnoreCase);
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

        /// <summary>
        /// The slug an edition's own was built from, for when the main edition is not there to ask.
        /// </summary>
        public static string RemoveEditionFromSlug(string titleSlug, string editionName)
        {
            if (IsMainEdition(editionName) || titleSlug.IsNullOrWhiteSpace())
            {
                return titleSlug;
            }

            var suffix = $"-{NormalizeEditionName(editionName).ToUrlSlug()}";

            return titleSlug.EndsWith(suffix, StringComparison.Ordinal)
                ? titleSlug.Substring(0, titleSlug.Length - suffix.Length)
                : titleSlug;
        }
    }
}

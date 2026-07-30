using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Organizer
{
    /// <summary>
    /// Reads the language codes back out of a file name.
    ///
    /// {MediaInfo AudioLanguages} and {MediaInfo SubtitleLanguages} both write the same thing - a
    /// bracketed list of two-letter codes, like [EN+TH] - so nothing in the name says which of the two
    /// a group came from. It does not have to: the order is the order they were placed in the format,
    /// the same order for every file in the library. So the first group in the name is the first group
    /// on screen and the second is the second, and a format carrying only one token gives one group.
    ///
    /// The name is the source rather than MediaInfo because the name is what the media server and the
    /// person browsing a folder both see, and because MediaInfo lives in a JSON column that carries the
    /// whole of ffprobe's output with it - far too much to read for a poster.
    /// </summary>
    public static class FileNameLanguages
    {
        // Two-letter codes joined by '+', in brackets, which is exactly what GetLanguagesToken writes.
        private static readonly Regex GroupRegex = new Regex(@"\[(?<codes>[A-Za-z]{2}(?:\+[A-Za-z]{2})*)\]",
                                                            RegexOptions.Compiled);

        /// <summary>
        /// The groups one file name carries, in the order they appear in it.
        /// </summary>
        public static List<List<string>> Read(string fileName)
        {
            var groups = new List<List<string>>();

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return groups;
            }

            foreach (Match match in GroupRegex.Matches(fileName))
            {
                var codes = match.Groups["codes"].Value
                                 .Split('+')
                                 .Select(c => c.ToUpperInvariant())
                                 .ToList();

                // [HD] and [UP] are shaped like a language group and are not one. Every code has to be
                // a language Sonarr knows before the group counts, which is also what keeps a release
                // group or a quality tag from turning into a flag.
                if (codes.All(IsLanguageCode))
                {
                    groups.Add(codes);
                }
            }

            return groups;
        }

        /// <summary>
        /// The groups a set of file names carries between them, position by position. Two files of the
        /// same episode can differ - one dubbed, one not - and a poster stands for all of them.
        /// </summary>
        public static List<List<string>> Union(IEnumerable<string> fileNames)
        {
            var positions = new List<List<string>>();

            foreach (var fileName in fileNames)
            {
                var groups = Read(fileName);

                for (var i = 0; i < groups.Count; i++)
                {
                    while (positions.Count <= i)
                    {
                        positions.Add(new List<string>());
                    }

                    foreach (var code in groups[i].Where(c => !positions[i].Contains(c)))
                    {
                        positions[i].Add(code);
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// Whether the subtitle group comes before the audio group in the names. The groups themselves
        /// say nothing about which is which - the format that wrote them does, and it wrote every name
        /// in the library the same way.
        ///
        /// A format carrying only one of the two tokens is not ambiguous either: what it writes is
        /// whichever token it has.
        /// </summary>
        public static bool SubtitlesComeFirst(params string[] formats)
        {
            var sawSubtitles = false;

            foreach (var format in formats.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                var audio = NamingTokens.IndexOf(format, "MediaInfo AudioLanguages");
                var subtitles = NamingTokens.IndexOf(format, "MediaInfo SubtitleLanguages");

                if (audio >= 0 && subtitles >= 0)
                {
                    return subtitles < audio;
                }

                if (audio >= 0)
                {
                    return false;
                }

                sawSubtitles |= subtitles >= 0;
            }

            return sawSubtitles;
        }

        private static bool IsLanguageCode(string code)
        {
            return IsoLanguages.Find(code.ToLowerInvariant()) != null;
        }
    }
}

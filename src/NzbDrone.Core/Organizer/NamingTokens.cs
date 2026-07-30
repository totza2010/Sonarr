using System;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Organizer
{
    /// <summary>
    /// Finding a token in a naming format, for the checks that have to know whether a format writes a
    /// particular thing.
    ///
    /// Neither end of a token can be matched on directly. It may carry a separator in front of its
    /// name - {.MediaInfo AudioLanguages} - and a filter behind it - {MediaInfo AudioLanguages:EN} -
    /// so neither "{MediaInfo AudioLanguages" nor "MediaInfo AudioLanguages}" is reliable. Searching
    /// for the bare name is reliable and too generous: a format with the word Multiple typed into it
    /// as literal text would satisfy it and then not write what was promised.
    ///
    /// What is always true is that the name appears inside a pair of braces, which is what this looks
    /// for.
    /// </summary>
    public static class NamingTokens
    {
        public static bool Contains(string format, string name)
        {
            return IndexOf(format, name) >= 0;
        }

        /// <summary>
        /// Where the token opens, or -1. Positions from this are comparable with each other, which is
        /// what says which of two tokens a format writes first.
        /// </summary>
        public static int IndexOf(string format, string name)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return -1;
            }

            var match = Regex.Match(format,
                                    @"\{[^}]*" + Regex.Escape(name),
                                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                                    TimeSpan.FromSeconds(1));

            return match.Success ? match.Index : -1;
        }
    }
}

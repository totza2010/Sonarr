using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public class MultipleFileMarker
    {
        public EpisodeFileMultipleType Type { get; set; }
        public int Number { get; set; }
    }

    /// <summary>
    /// Reads a part or version marker out of a file name. Shared by everything that has to agree on what
    /// a name says: the import that reads it, the check that decides whether a later part may sit beside
    /// an earlier one, and the step that records a marker an existing file's name already carries.
    ///
    /// The pt1 and v1 that {Multiple} writes are read, and so are the forms people name their own files
    /// with - part1, part 1, part.1, part-1, version 2. Words like "part" used to be left alone because
    /// plenty of episodes are titled "Part 2"; that is now handled by measuring the name against its
    /// titles, so a "Part 2" the title accounts for is never taken for a marker.
    /// </summary>
    public static class MultipleFileMarkerReader
    {
        // The word forms may have a separator before the number; a bare v may not, since "v.2" and "v 2"
        // are rarely anything but text. Nothing may run into the marker on either side, which keeps
        // anime's "01v2" release numbering and words like "counterpart2" out.
        private static readonly Regex MarkerRegex = new Regex(@"(?<![a-z0-9])(?:(?<kind>part|pt|version)[\s._-]?(?<number>\d{1,2})|(?<kind>v)(?<number>\d{1,2}))(?![a-z0-9])",
                                                              RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// The first marker in the name that its titles do not account for, or null. A title can carry one
        /// of its own - Kamen Rider V3 is a series whose episodes are called things like "His Name is
        /// V3!" - and a name holds its titles, so a token counts only when the name holds more of it than
        /// the series and episode titles do. A genuine marker beside a title that has one is still found,
        /// since the name then holds one extra.
        ///
        /// Titles are measured both as written and as the naming format cleans them, and the larger count
        /// wins. Missing a genuine marker costs a row set by hand; inventing one turns an ordinary file
        /// into an extra copy that is never upgraded.
        /// </summary>
        public static MultipleFileMarker Read(string fileName, Series series, IEnumerable<Episode> episodes)
        {
            if (fileName.IsNullOrWhiteSpace())
            {
                return null;
            }

            var matches = MarkerRegex.Matches(fileName).Cast<Match>().ToList();

            if (matches.Empty())
            {
                return null;
            }

            var titles = new List<string> { series?.Title };
            titles.AddRange((episodes ?? Enumerable.Empty<Episode>()).Select(e => e.Title));
            titles = titles.Where(t => t.IsNotNullOrWhiteSpace()).ToList();

            foreach (var group in matches.GroupBy(Token))
            {
                var inTitles = titles.Sum(t => Math.Max(Count(t, group.Key), Count(FileNameBuilder.CleanTitle(t), group.Key)));

                if (group.Count() > inTitles)
                {
                    return ToMarker(group.Last());
                }
            }

            return null;
        }

        private static MultipleFileMarker ToMarker(Match match)
        {
            return new MultipleFileMarker
            {
                Type = IsVersion(match) ? EpisodeFileMultipleType.Version : EpisodeFileMultipleType.Part,
                Number = int.Parse(match.Groups["number"].Value)
            };
        }

        private static bool IsVersion(Match match)
        {
            return match.Groups["kind"].Value.StartsWith("v", StringComparison.InvariantCultureIgnoreCase);
        }

        // "part2", "pt 2" and "Part.2" are one marker, so they are counted as one.
        private static string Token(Match match)
        {
            return (IsVersion(match) ? "v" : "p") + int.Parse(match.Groups["number"].Value);
        }

        private static int Count(string text, string token)
        {
            return MarkerRegex.Matches(text).Count(m => Token(m) == token);
        }
    }
}

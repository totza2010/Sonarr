using System;
using System.Linq;
using System.Runtime.InteropServices;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Update
{
    /// <summary>
    /// Turns a GitHub release into the update package the rest of Sonarr already understands. Upstream has
    /// a service that picks the right file for whoever asked; a fork has only its releases, so the choice
    /// is made here instead - which it can be, because the running install knows its own platform.
    /// </summary>
    public static class UpdatePackageMapper
    {
        public static UpdatePackage Map(GitHubReleaseResource release, string branch)
        {
            if (release == null || release.Draft)
            {
                return null;
            }

            var version = ParseVersion(release.TagName);

            if (version == null)
            {
                return null;
            }

            // The asset names carry the branch they were built from, so a release of another branch has
            // nothing this install can use rather than something that would run but not be what it asked
            // for.
            var fileName = $"Sonarr.{branch}.{version}.{RuntimeIdentifier()}.{PackageExtension()}";
            var asset = release.Assets?.FirstOrDefault(a => a.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                return null;
            }

            return new UpdatePackage
            {
                Version = version,
                ReleaseDate = release.PublishedAt,
                FileName = asset.Name,
                Url = asset.BrowserDownloadUrl,
                Hash = StripDigestAlgorithm(asset.Digest),
                Branch = branch,
                Changes = ParseChanges(release.Body)
            };
        }

        public static Version ParseVersion(string tagName)
        {
            return Version.TryParse(tagName?.TrimStart('v', 'V'), out var version) ? version : null;
        }

        /// <summary>
        /// The download is checked against this, so a digest that is not the algorithm the check uses is
        /// worse than none: it would fail every install rather than let one through unchecked.
        /// </summary>
        private static string StripDigestAlgorithm(string digest)
        {
            if (digest.IsNullOrWhiteSpace())
            {
                return null;
            }

            var parts = digest.Split(':', 2);

            if (parts.Length != 2)
            {
                return digest;
            }

            return parts[0].Equals("sha256", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
        }

        /// <summary>
        /// The release notes are written by the build, which groups the commits under two headings. Anything
        /// else in the body is left alone - a body nobody wrote to this shape simply has no changes to show,
        /// which the modal already handles.
        /// </summary>
        public static UpdateChanges ParseChanges(string body)
        {
            if (body.IsNullOrWhiteSpace())
            {
                return null;
            }

            var changes = new UpdateChanges();
            var fixedSection = false;
            var sawHeading = false;

            foreach (var raw in body.Split('\n'))
            {
                var line = raw.Trim();

                if (line.StartsWith("#"))
                {
                    sawHeading = true;
                    fixedSection = line.Contains("fix", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!sawHeading || (!line.StartsWith("* ") && !line.StartsWith("- ")))
                {
                    continue;
                }

                var entry = line.Substring(2).Trim();

                if (entry.IsNullOrWhiteSpace())
                {
                    continue;
                }

                (fixedSection ? changes.Fixed : changes.New).Add(entry);
            }

            return changes.New.Any() || changes.Fixed.Any() ? changes : null;
        }

        /// <summary>
        /// The same identifiers the build names its artefacts with.
        /// </summary>
        public static string RuntimeIdentifier()
        {
            var os = OsInfo.Os switch
            {
                Os.Windows => "win",
                Os.Osx => "osx",
                Os.LinuxMusl => "linux-musl",
                Os.Bsd => "freebsd",
                _ => "linux"
            };

            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm => "arm",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64"
            };

            return $"{os}-{arch}";
        }

        private static string PackageExtension()
        {
            return OsInfo.IsWindows ? "zip" : "tar.gz";
        }
    }
}

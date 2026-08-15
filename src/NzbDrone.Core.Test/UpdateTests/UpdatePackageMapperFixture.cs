using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    [TestFixture]
    public class UpdatePackageMapperFixture : CoreTest
    {
        private static GitHubReleaseResource GivenRelease(params string[] assetNames)
        {
            var assets = new List<GitHubReleaseAssetResource>();

            foreach (var name in assetNames)
            {
                assets.Add(new GitHubReleaseAssetResource
                {
                    Name = name,
                    BrowserDownloadUrl = $"https://github.com/totza2010/Sonarr/releases/download/v4.0.19.827/{name}",
                    Digest = "sha256:2cb65bd41326baee5c34d2f35ec4deade5b8272317bee31a02ba52a32fc4576d"
                });
            }

            return new GitHubReleaseResource
            {
                TagName = "v4.0.19.827",
                PublishedAt = new DateTime(2026, 8, 13, 18, 9, 18, DateTimeKind.Utc),
                Assets = assets
            };
        }

        private static string ThisRuntimeAsset()
        {
            var extension = OsInfo.IsWindows ? "zip" : "tar.gz";

            return $"Sonarr.main.4.0.19.827.{UpdatePackageMapper.RuntimeIdentifier()}.{extension}";
        }

        [Test]
        public void should_pick_the_asset_built_for_this_platform()
        {
            // The one thing upstream's service did that a plain list of releases does not.
            var release = GivenRelease("Sonarr.main.4.0.19.827.some-other-runtime.tar.gz", ThisRuntimeAsset());

            var package = UpdatePackageMapper.Map(release, "main");

            package.Should().NotBeNull();
            package.FileName.Should().Be(ThisRuntimeAsset());
            package.Version.Should().Be(new Version("4.0.19.827"));
            package.Url.Should().EndWith(ThisRuntimeAsset());
        }

        [Test]
        public void should_ignore_a_release_with_nothing_built_for_this_platform()
        {
            var package = UpdatePackageMapper.Map(GivenRelease("Sonarr.main.4.0.19.827.some-other-runtime.tar.gz"), "main");

            package.Should().BeNull();
        }

        [Test]
        public void should_ignore_a_release_of_another_branch()
        {
            // Asset names carry the branch, so asking for one and being handed another is not a match.
            UpdatePackageMapper.Map(GivenRelease(ThisRuntimeAsset()), "develop").Should().BeNull();
        }

        [Test]
        public void should_ignore_a_draft()
        {
            var release = GivenRelease(ThisRuntimeAsset());
            release.Draft = true;

            UpdatePackageMapper.Map(release, "main").Should().BeNull();
        }

        [Test]
        public void should_take_the_hash_from_the_digest_without_its_algorithm()
        {
            // What the download is checked against, and the check expects the bare hash.
            UpdatePackageMapper.Map(GivenRelease(ThisRuntimeAsset()), "main")
                               .Hash.Should().Be("2cb65bd41326baee5c34d2f35ec4deade5b8272317bee31a02ba52a32fc4576d");
        }

        [Test]
        public void should_leave_the_hash_empty_when_the_digest_is_not_sha256()
        {
            // Better than passing something the check cannot use, which would fail every install.
            var release = GivenRelease(ThisRuntimeAsset());
            release.Assets[0].Digest = "sha512:abc";

            UpdatePackageMapper.Map(release, "main").Hash.Should().BeNull();
        }

        [Test]
        public void should_read_the_version_from_the_tag()
        {
            UpdatePackageMapper.ParseVersion("v4.0.19.827").Should().Be(new Version("4.0.19.827"));
            UpdatePackageMapper.ParseVersion("4.0.19.827").Should().Be(new Version("4.0.19.827"));
            UpdatePackageMapper.ParseVersion("nightly").Should().BeNull();
        }

        [Test]
        public void should_group_the_notes_the_way_the_modal_shows_them()
        {
            var changes = UpdatePackageMapper.ParseChanges("## New\n* Something added\n\n## Fixed\n* Something repaired\n");

            changes.New.Should().BeEquivalentTo(new[] { "Something added" });
            changes.Fixed.Should().BeEquivalentTo(new[] { "Something repaired" });
        }

        [Test]
        public void should_have_no_changes_for_notes_written_some_other_way()
        {
            // A release published before the build wrote them this way, or by hand. The modal copes with
            // nothing to show; it does not cope with a heading landing in the list.
            UpdatePackageMapper.ParseChanges("**Full Changelog**: https://github.com/x/y/compare/a...b").Should().BeNull();
            UpdatePackageMapper.ParseChanges("").Should().BeNull();
            UpdatePackageMapper.ParseChanges(null).Should().BeNull();
        }
    }
}

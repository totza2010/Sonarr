using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [Test]
        public void no_update_when_version_higher()
        {
            UseRealHttp();
            Subject.GetLatestUpdate("main", new Version(10, 0)).Should().BeNull();
        }

        [Test]
        public void finds_update_when_version_lower()
        {
            UseRealHttp();
            Subject.GetLatestUpdate("main", new Version(3, 0)).Should().NotBeNull();
        }

        [Test]
        public void should_have_nothing_for_a_branch_that_was_never_released()
        {
            // Assets are named after the branch they were built from, so there is nothing to offer rather
            // than something from a branch nobody asked for.
            UseRealHttp();
            Subject.GetLatestUpdate("invalid_branch", new Version(3, 0)).Should().BeNull();
        }

        [Test]
        public void should_get_recent_updates()
        {
            const string branch = "main";
            UseRealHttp();
            var recent = Subject.GetRecentUpdates(branch, new Version(4, 0), null);

            recent.Should().NotBeEmpty();
            recent.Should().OnlyContain(c => c.FileName.Contains($"Sonarr.{c.Branch}.4."));
            recent.Should().OnlyContain(c => c.Url.IsNotNullOrWhiteSpace());
            recent.Should().OnlyContain(c => c.ReleaseDate.Year >= 2014);

            // Nothing is asserted about the notes here: whether a release has any depends on when it was
            // cut, and reading them is covered against fixed input in UpdatePackageMapperFixture.
        }

        [Test]
        public void should_be_newest_first()
        {
            UseRealHttp();
            var recent = Subject.GetRecentUpdates("main", new Version(4, 0), null);

            recent.Should().BeInDescendingOrder(c => c.Version);
        }

        [Test]
        public void should_say_there_is_nothing_rather_than_throw_when_github_cannot_be_reached()
        {
            // Rate limits and a install with no internet both land here, and neither is something the
            // person can act on - the update check is meant to be quiet when it cannot look.
            Subject.GetLatestUpdate("main", new Version(3, 0)).Should().BeNull();
            Subject.GetRecentUpdates("main", new Version(3, 0), null).Should().BeEmpty();
        }
    }
}

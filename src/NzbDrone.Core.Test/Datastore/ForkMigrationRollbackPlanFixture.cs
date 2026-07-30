using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using FluentMigrator;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    /// <summary>
    /// tools/rollback-plan.json says what each migration this fork added did and what has to happen
    /// before official Sonarr can run on a database that has seen it. It is only worth anything if it
    /// covers every one of them, and a plan that quietly falls a migration behind is worse than none:
    /// the rollback would report all clear and leave the database in a state that throws later.
    ///
    /// So adding a migration in the fork's number range means adding it to the plan, and this says so
    /// at build time rather than at somebody's rollback.
    /// </summary>
    [TestFixture]
    public class ForkMigrationRollbackPlanFixture : CoreTest
    {
        // Everything from here up belongs to this fork - see the reserved range in the migrations.
        private const int ForkMigrationFloor = 9000;

        private static List<int> ForkMigrationVersions()
        {
            return typeof(Core.Datastore.Migration.Framework.NzbDroneMigrationBase).Assembly
                       .GetTypes()
                       .Select(t => t.GetCustomAttribute<MigrationAttribute>())
                       .Where(a => a != null && a.Version >= ForkMigrationFloor)
                       .Select(a => (int)a.Version)
                       .OrderBy(v => v)
                       .ToList();
        }

        private static Dictionary<string, JsonElement> Plan()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "rollback-plan.json");

            File.Exists(path).Should().BeTrue(because: $"the plan is copied next to the tests, expected at {path}");

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            return document.RootElement
                           .GetProperty("migrations")
                           .EnumerateObject()
                           .ToDictionary(p => p.Name, p => p.Value.Clone());
        }

        [Test]
        public void every_fork_migration_should_be_in_the_rollback_plan()
        {
            var plan = Plan();
            var missing = ForkMigrationVersions()
                          .Where(v => !plan.ContainsKey(v.ToString()))
                          .ToList();

            missing.Should().BeEmpty(
                because: "a migration the plan has never heard of is one a rollback would leave behind. Add it to tools/rollback-plan.json.");
        }

        [Test]
        public void the_rollback_plan_should_not_name_migrations_that_do_not_exist()
        {
            var versions = ForkMigrationVersions();
            var stale = Plan().Keys
                              .Where(k => !versions.Contains(int.Parse(k)))
                              .ToList();

            stale.Should().BeEmpty(because: "the plan would be undoing something nothing does");
        }

        [Test]
        public void every_entry_should_say_what_it_did_and_whether_it_matters()
        {
            foreach (var (version, entry) in Plan())
            {
                entry.TryGetProperty("name", out _).Should().BeTrue(because: $"{version} needs a name");
                entry.TryGetProperty("adds", out _).Should().BeTrue(because: $"{version} needs to record what it added");

                // Either it can be left in place, or there is something to do about it. Silence on
                // both counts is the case that gets missed.
                var inert = entry.TryGetProperty("inert", out var value) && value.GetBoolean();
                var acts = entry.TryGetProperty("undo", out _) ||
                           entry.TryGetProperty("blocks", out _) ||
                           entry.TryGetProperty("warns", out _);

                (inert || acts).Should().BeTrue(
                    because: $"{version} says neither that it is inert nor what to do about it");
            }
        }
    }
}

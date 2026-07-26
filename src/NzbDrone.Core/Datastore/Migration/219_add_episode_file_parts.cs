using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(219)]
    public class add_episode_file_parts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // An episode can be split across files (part 1, part 2) or exist in more than one version
            // (two endings). Both are the same shape: several files belong to one episode. Zero and
            // empty mean neither, so every existing file keeps its current meaning.
            Alter.Table("EpisodeFiles")
                .AddColumn("PartNumber").AsInt32().NotNullable().WithDefaultValue(0)
                .AddColumn("VersionName").AsString().NotNullable().WithDefaultValue("");

            // Episodes.EpisodeFileId still points at the primary file, so nothing that reads it has to
            // change. The extra files an episode owns are listed here instead, which is also what keeps
            // MediaFileTableCleanupService from deleting them for having no episode.
            Create.TableForModel("EpisodeFileLinks")
                .WithColumn("EpisodeId").AsInt32().NotNullable()
                .WithColumn("EpisodeFileId").AsInt32().NotNullable();

            Create.Index().OnTable("EpisodeFileLinks").OnColumn("EpisodeId").Ascending()
                                                      .OnColumn("EpisodeFileId").Ascending()
                                                      .WithOptions().Unique();

            Create.Index().OnTable("EpisodeFileLinks").OnColumn("EpisodeFileId");
        }
    }
}

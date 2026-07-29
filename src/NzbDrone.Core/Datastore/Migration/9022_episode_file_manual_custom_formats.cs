using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(9022)]
    public class episode_file_manual_custom_formats : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Custom formats added to a file by hand, on top of whatever its name matches. Nothing can
            // work out that a file came from a particular network or is a particular edition, so this
            // is the only way to say so. Ids rather than names, so renaming a format keeps working.
            Alter.Table("EpisodeFiles")
                .AddColumn("ManualCustomFormats").AsString().NotNullable().WithDefaultValue("[]");
        }
    }
}

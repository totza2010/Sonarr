using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(221)]
    public class episode_file_excluded_custom_formats : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // The other half of ManualCustomFormats: formats the file's name matches but that should
            // not count for it. Kept apart from the added ones so the two can be undone separately,
            // and so a format that is neither added nor excluded needs no record at all.
            Alter.Table("EpisodeFiles")
                .AddColumn("ExcludedCustomFormats").AsString().NotNullable().WithDefaultValue("[]");
        }
    }
}

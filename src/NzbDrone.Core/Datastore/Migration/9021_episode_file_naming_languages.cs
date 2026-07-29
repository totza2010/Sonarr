using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(9021)]
    public class episode_file_naming_languages : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // What the language tokens should say about this file. MediaInfo reports what the streams
            // declare, which is sometimes wrong and sometimes missing, and it is rebuilt from the file
            // on every scan so there is nowhere in it to record a correction. Empty means say whatever
            // MediaInfo says, which is every file that existed before this column.
            Alter.Table("EpisodeFiles")
                .AddColumn("NamingAudioLanguages").AsString().NotNullable().WithDefaultValue("[]")
                .AddColumn("NamingSubtitleLanguages").AsString().NotNullable().WithDefaultValue("[]");
        }
    }
}

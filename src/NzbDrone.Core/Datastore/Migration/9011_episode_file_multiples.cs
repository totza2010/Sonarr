using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(9011)]
    public class episode_file_multiples : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Parts and versions were two separate fields doing the same job: saying that this file is one
            // of several the episode owns. They collapse into a kind and a number, so a file is either
            // part 2 or version 2 and everything that handles one handles the other.
            Alter.Table("EpisodeFiles").AddColumn("MultipleType").AsInt32().NotNullable().WithDefaultValue(0);

            Rename.Column("PartNumber").OnTable("EpisodeFiles").To("MultipleNumber");

            // 1 is Part, 2 is Version. A file that had a part keeps its number; a file that only had a
            // version name becomes version 1, since the name it carried is not a number and is going away.
            Execute.Sql("UPDATE \"EpisodeFiles\" SET \"MultipleType\" = 1 WHERE \"MultipleNumber\" > 0");
            Execute.Sql("UPDATE \"EpisodeFiles\" SET \"MultipleType\" = 2, \"MultipleNumber\" = 1 WHERE \"MultipleType\" = 0 AND \"VersionName\" <> ''");

            Delete.Column("VersionName").FromTable("EpisodeFiles");
        }
    }
}

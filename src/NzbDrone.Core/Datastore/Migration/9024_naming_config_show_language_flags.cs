using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(9024)]
    public class naming_config_show_language_flags : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Whether the language groups written into file names are shown alongside a series. It sits
            // with the naming config rather than with the UI settings because it is a rule about the
            // format: the flags are read out of the names, so they can only be shown once a format puts
            // them there, and the validator on this page is what says so.
            //
            // Off by default, which is every library that existed before this column.
            Alter.Table("NamingConfig")
                .AddColumn("ShowLanguageFlags").AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }
}

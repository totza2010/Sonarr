using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Datastore.Migration
{
    // 9000 and up is reserved for this fork. Taking upstream's next number would either collide with
    // theirs outright or, worse, make a database that ran ours skip theirs without a word, since
    // FluentMigrator tracks progress by number alone.
    [Migration(9020)]
    public class series_naming_language : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // What ORIGINAL means for this series when building a file name. OriginalLanguage itself is
            // owned by the metadata refresh and is what custom formats and auto tagging read, so this
            // sits beside it: Unknown leaves every one of those untouched and naming falls back to it.
            // Not nullable, because the language picker reads an id off the value it is given.
            Alter.Table("Series").AddColumn("NamingLanguage").AsInt32().NotNullable().WithDefaultValue((int)Language.Unknown);
        }
    }
}

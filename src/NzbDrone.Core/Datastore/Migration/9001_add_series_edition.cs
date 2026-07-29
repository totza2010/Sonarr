using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // 9000 and up is reserved for this fork. Taking upstream's next number would either collide with
    // theirs outright or, worse, make a database that ran ours skip theirs without a word, since
    // FluentMigrator tracks progress by number alone.
    [Migration(9001)]
    public class add_series_edition : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // TvdbId was declared unique when the Series table was created, so dropping the index is not
            // enough, the column constraint has to go as well. SQLite rebuilds the table without it,
            // Postgres has to drop the constraint it generated for the column.
            // This runs before the new column is added, so the rebuild cannot affect its default.
            IfDatabase("sqlite")
                .Alter.Table("Series").AlterColumn("TvdbId").AsInt32().NotNullable();

            IfDatabase("postgres").Execute.Sql(@"
DO $$
DECLARE
    unique_constraint text;
BEGIN
    FOR unique_constraint IN
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        WHERE rel.relname = 'Series'
          AND con.contype = 'u'
          AND con.conkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = rel.oid AND attname = 'TvdbId')]
    LOOP
        EXECUTE format('ALTER TABLE ""Series"" DROP CONSTRAINT %I', unique_constraint);
    END LOOP;
END $$;");

            Execute.Sql("DROP INDEX IF EXISTS \"IX_Series_TvdbId\"");

            // Empty edition name is the main edition, so existing series keep their current identity.
            Alter.Table("Series")
                .AddColumn("EditionName").AsString().NotNullable().WithDefaultValue("");

            // A TVDB ID is no longer unique on its own, but it still has to be unique per edition.
            Create.Index().OnTable("Series").OnColumn("TvdbId").Ascending()
                                            .OnColumn("EditionName").Ascending()
                                            .WithOptions().Unique();
        }
    }
}

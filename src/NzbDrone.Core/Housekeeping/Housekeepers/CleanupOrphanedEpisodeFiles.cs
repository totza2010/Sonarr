using Dapper;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedEpisodeFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedEpisodeFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            // An extra part or version is owned through EpisodeFileLinks rather than through
            // Episodes.EpisodeFileId, so by the first test alone every one of them is an orphan. Left at
            // that, this removed them all once a day - quietly, since it is plain SQL and raises no event -
            // and left the files on disk, the links pointing at nothing and no trace of what had happened.
            //
            // A link only spares a file while the episode it names still exists; one left behind by an
            // episode that has gone is no reason to keep anything.
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""EpisodeFiles""
                                     WHERE ""Id"" IN (
                                     SELECT ""EpisodeFiles"".""Id"" FROM ""EpisodeFiles""
                                     LEFT OUTER JOIN ""Episodes""
                                     ON ""EpisodeFiles"".""Id"" = ""Episodes"".""EpisodeFileId""
                                     WHERE ""Episodes"".""Id"" IS NULL)
                                     AND ""Id"" NOT IN (
                                     SELECT ""EpisodeFileLinks"".""EpisodeFileId"" FROM ""EpisodeFileLinks""
                                     INNER JOIN ""Episodes""
                                     ON ""EpisodeFileLinks"".""EpisodeId"" = ""Episodes"".""Id"")");
        }
    }
}

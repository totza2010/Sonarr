# Going back to official Sonarr

A database this fork has touched can go back to official Sonarr. Whether it needs any preparation
depends on whether the features were used, not on how long it ran here.

```
python tools/rollback-to-official.py /path/to/sonarr.db
```

Stop Sonarr first. That command only reports; nothing is written until `--apply`, which copies the
database before it changes anything.

## Nothing was used: just switch

Most of what this fork adds is invisible to official Sonarr.

| What is left behind | Why it does not matter |
| --- | --- |
| `VersionInfo` rows numbered 9001, 9010, 9011, 9020-9023 | They name migrations official Sonarr has never heard of, so it skips them and runs its own 218 onwards as usual. This is what the reserved number range was for. |
| Extra columns on `Series` and `EpisodeFiles` | Every one is `NOT NULL` with a default. Sonarr's inserts name the columns they know, so the rest fill themselves. |
| The `EpisodeFileLinks` table | Nothing reads it. |

This also means coming *back* here later is just starting the other build again: the columns are
still there, the migrations are still recorded, and nothing has to be redone.

## Editions were used: remove them first

Migration 9001 dropped the index that made `TvdbId` unique, because an edition shares its TVDB id
with the series it is an edition of. Official Sonarr does not expect that:

```csharp
return Query(s => s.TvdbId == tvdbId).SingleOrDefault();
```

`SingleOrDefault` throws the moment two rows come back, and that lookup runs during a metadata
refresh, a release parse and an import. It would not fail on startup; it would fail later, at
whatever it was doing.

**Delete each edition from Sonarr's own interface.** A series has rows in a dozen tables, and only
Sonarr removes it properly. Deleting the series does not have to delete its files - the choice is on
that dialog.

The script lists them, and refuses to change anything while any are left. `--apply` then restores the
unique index, which fails loudly if an edition was missed.

## Parts were used: their rows go

The second and later file of an episode are reached through `EpisodeFileLinks`. They are not what
`Episodes.EpisodeFileId` points at, and official Sonarr deletes any file row no episode claims:

```csharp
if (episodes.None(e => e.EpisodeFileId == episodeFile.Id))
{
    _mediaFileService.Delete(episodeFile, DeleteMediaFileReason.NoLinkedEpisodes);
}
```

It would then find the file still sitting on disk and import it again, on top of the part that was
already there. `--apply` removes those rows deliberately instead, which at least happens at a moment
of your choosing.

**The files themselves stay on disk.** Move them somewhere else first if they should not be imported
again as ordinary files. The script prints their paths.

## What `--apply` does

1. Copies the database to `sonarr.db.before-rollback-<timestamp>`
2. Removes file rows marked as a part or version that no episode points at
3. Empties `EpisodeFileLinks`
4. Drops `IX_Series_TvdbId_EditionName` and restores `IX_Series_TvdbId`

All of it in one transaction. If the index will not build, nothing is kept and the copy is still
there.

It does not drop the added columns. Dropping a column in SQLite rebuilds the table, which on a large
library is slow and risky for no gain - they are inert, and keeping them is what makes coming back
here cost nothing.

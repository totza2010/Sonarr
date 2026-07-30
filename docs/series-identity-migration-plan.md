# Moving a series' identity, and moving its files

Four things a library eventually needs and Sonarr cannot do: change which TVDB series a row points at,
rename an edition, swap which edition is the main one, and hand a series' files to a season of a
different series.

None of them is urgent. They come up a few times a year, and the workaround for all four - delete and
re-add - works. What it costs is the history, and that is why they are written down: the cost is
avoidable, and the reason it is avoidable is the same in every case.

Nothing here is built. This is the analysis, so that whoever picks it up does not have to redo it.

## Why all four are possible at all

Everything worth keeping keys on `Series.Id`, the internal row id: `EpisodeFiles`, `Episodes`,
`History`, `Blocklist`, `DownloadHistory`, tags, the date it was added, the custom formats a file was
given by hand. Nothing outside the `Series` row itself keys on `TvdbId` or `EditionName`.

So changing a series' identity is, underneath, an update to one row. What makes it work is arranging
the three things that are *derived* from that identity:

| | derived from | why it bites |
| --- | --- | --- |
| `IX_Series_TvdbId_EditionName` | TvdbId + EditionName | unique, so two rows cannot pass through the same value |
| `TitleSlug` | the metadata, plus the edition (`ApplyEditionToSlug`) | unique as well, since migration 019 |
| the folder on disk | title, year, `{edition-...}` | it is what Plex and Silo read |

And one ordering rule that governs the file-moving cases: **deleting a series deletes everything
hanging off it.** `MediaFileService`, `HistoryService`, `BlocklistService` and `DownloadHistoryService`
all handle `SeriesDeletedEvent` and clear their own rows. Anything being kept has to be moved before
the delete, and the delete is the last step or there is nothing left to move.

## 1. Pointing a series at a different TVDB id

TheTVDB sometimes deletes an entry and recreates it under a new id, usually just after a new series
was added and its files imported. Sonarr marks the series `Deleted` and the removed-series health
check names it:

> Series Berlin and the Lady with an Ermine (tvdbid 477676), Spider-Noir - Color (tvdbid 478550) were
> removed from TheTVDB

Most of this already works.

- `Series.ApplyChanges` copies `TvdbId` from the payload, so `PUT /api/v3/series/{id}` with a new id
  already saves. No migration and no schema change.
- `RefreshEpisodeService` matches existing episodes on **season and episode number**, not on the
  episode's own TVDB id. So after the id changes, a refresh re-attaches the episode rows positionally:
  `Episode.Id` survives, `EpisodeFileId` survives, and the files and history stay where they are.

What is missing is everything that makes it safe, which is why it wants to be a page rather than a
field:

- the new id has to resolve on TheTVDB, and must not already be held by another series - the unique
  index would otherwise throw a raw database error out of the save
- **the slug has to be checked at the same time.** It is unique, and it is written from the metadata
  during the refresh, not computed locally. If the new entry's slug is already held by another series,
  the refresh fails *after* the id has been committed, leaving the row with a new id and an old slug.
  The preview step is already fetching the new entry's metadata to build the episode mapping, so it
  knows the new slug then - check it there, before anything is written.
- episodes in the old entry that the new one does not have are deleted by the refresh, and their files
  become rows nothing points at. The preview has to show how many, and offer somewhere to send them
  (which is case 4).
- `Status` has to be cleared off `Deleted` and a refresh forced
- `.nfo` metadata carries the tvdb id and has to be rewritten
- an `ImportListExclusion` on the *new* id would fight the refresh; one on the old id is harmless

The slug changing costs nothing else. Nothing in the backend looks a series up by slug - it is a route
key the frontend resolves from series it has already loaded - so an old bookmark stops working and
that is all.

Downstream apps key requests on the tvdb id and will see the old one vanish. That is true of
delete-and-re-add too; the difference is that this way Sonarr's own history survives.

## 2. Renaming an edition

The smallest of the four. One row, and the only collision is another edition of the same series
already using that name, which `SeriesEditions.SameEdition` and the unique index both already catch.

Two things have to happen in the same save:

- `TitleSlug` is derived from the edition name and is unique, so it has to be regenerated then rather
  than left to the next refresh
- the folder carries `{edition-...}`. Nothing currently recomputes `Path` when `EditionName` changes,
  so the folder would keep the old tag and Plex would keep showing the old edition. This needs what a
  title change gets: propose the new path, move the files.

## 3. Swapping which edition is the main one

The main edition is the one third-party software sees, since the list endpoints filter the others out
unless asked. Swapping is how you say "the colourised one is the real one now".

Two rows have to exchange `EditionName`, and the unique index means neither can be updated first -
whichever moves collides with the value the other still holds. It needs a transaction and an
intermediate value: A to a temporary name, B to A's old name, A to B's old name. `TitleSlug` is unique
too and has to make the same three-step trip.

The folders have the identical problem and need the identical answer: a temporary folder name, and two
full moves rather than one rename.

The risk that has to be designed for is an interrupted swap, which leaves the temporary value in the
database, on disk, or both. It has to be resumable, or at minimum loud enough that nobody discovers it
weeks later.

This is the only one of the four that is a dedicated command rather than an edit.

## 4. Handing files to a season of another series

TheTVDB deleted *Berlin and the Lady with an Ermine* because it became season 3 of another show. No
change of id helps: the destination is a series that already exists and has an id of its own. What
moves is the files.

| | from | to |
| --- | --- | --- |
| `EpisodeFile.SeriesId` | A | B |
| `EpisodeFile.SeasonNumber` | 1 | 3 |
| `EpisodeFile.RelativePath`, and the file itself | A's folder | `B/Season 03/`, renamed to B's format |
| `Episode.EpisodeFileId` | A's episodes | B's season 3 episodes |
| `EpisodeHistory.SeriesId` and `.EpisodeId` | A | B - both columns exist, so this is remappable |

Then delete A without deleting files, last.

What the preview has to cover:

- **B has to be refreshed first.** Season 3 must exist before anything can point at it.
- the default mapping is a season and an offset - A season 1 to B season 3, offset 0 - with a
  per-episode table for the exceptions, since orderings differ and specials rarely line up
- a destination episode that already has a file. Stock Sonarr would make you choose which one wins;
  this fork does not have to, because an episode is allowed more than one file - offer to keep both as
  versions.
- episodes of A with nowhere to go: say plainly that their files stay where they are, or refuse

## They are one page

Cases 1 and 4 are the same screen with a different destination:

```
choose destination  ->  fetch its metadata  ->  episode mapping table  ->  preview  ->  confirm
                          |                                                  |
   1: a new TVDB id (same row)                       what moves where, what has nowhere
   4: an existing series and season                  to go, what it collides with
```

Underneath they are different operations - case 1 keeps the row and changes what it points at, case 4
keeps the destination row and moves files into it - but the preview and the mapping table are the same
work, and they are the bulk of it.

They also meet in the middle: the episodes case 1 strands are exactly what case 4 exists to move, so
stranding them is case 4 offered as a step rather than a dead end.

A fifth case will turn up eventually - a series split in two - and it is the same screen again, with a
destination that does not exist yet.

**Build case 4 first, not case 1.** Case 1 is the special case where the destination happens to be the
row you started from. Doing it first and fitting case 4 around it afterwards gives two pieces of code
that do one thing.

## Where this fits

The editions this refers to are [series-editions-v5-plan.md](series-editions-v5-plan.md). The
`{edition-...}` folder these operations rename or swap is read by the media servers described in
[media-server-naming.md](media-server-naming.md), so a folder move here is a re-scan there.

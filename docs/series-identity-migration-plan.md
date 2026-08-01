# Moving a series' identity, and moving its files

Five things a library eventually needs and Sonarr cannot do: change which TVDB series a row points at,
rename an edition, swap which edition is the main one, hand a series' files to a season of a different
series, and number a series the way the world names its releases rather than the way it first aired.

The first four come up a few times a year, and the workaround for them - delete and re-add - works.
What it costs is the history, and that is why they are written down: the cost is avoidable, and the
reason it is avoidable is the same in every case. The fifth is not rare at all and has no good
workaround, but it belongs here because it is the same screen.

Nothing here is built. This is the analysis, so that whoever picks it up does not have to redo it.

## Why the first four are possible at all

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

## 5. Numbering a series the way its releases are named

*La Casa de Papel* is the case everyone runs into, and it is worth going through in full, because it
shows both what this would fix and where nothing can.

TheTVDB carries the order the Spanish network aired. Netflix bought the show and re-cut it, and every
release is named the Netflix way. Sonarr's own wiki describes the result as season 5 being imported
over season 3. That phrasing is the diagnosis: a release for a season Sonarr does not have would be
*rejected*, not imported somewhere else. Landing on the wrong season means something is translating
the numbers, and something is - scene numbering.

### What Sonarr already has

`Episodes` carries `SceneSeasonNumber`, `SceneEpisodeNumber`, `SceneAbsoluteEpisodeNumber` and
`UnverifiedSceneNumbering`, and both directions already read them - `ParsingService` when a release is
matched, `EpisodeRepository.FindEpisodesBySceneNumbering` when one is searched for. Give those columns
the right values and downloading and importing both come out right with no other change.

The problem is that only XEM may write them, and the user has no say at any point:

| | owned by | can the user change it |
| --- | --- | --- |
| which order TheTVDB serves | Skyhook | no |
| `Series.UseSceneNumbering` | `XemService`, which sets it to `mappings.Any()` | no - `ApplyChanges` does not even copy it, so the API cannot either |
| the per-episode scene numbers | `XemService`, which clears and rewrites them on every refresh | no |

XEM is not abandoned - mappings are still added daily. Its maintainers simply do not want to change
this one. That is their call to make, and it is exactly why the mapping has to be something a person
can override locally rather than something requested from someone else.

### What the numbers say

Aired order, from Skyhook, against the Netflix order:

| aired | episodes | runtime | Netflix | episodes | runtime |
| --- | --- | --- | --- | --- | --- |
| Season 1 | 15 | ~1,025 min, avg 68 | Parte 1 + Parte 2 | 13 + 9 = **22** | ~1,020 min, avg 47 |
| Season 2 | 16 | ~775 min | Parte 3 + Parte 4 | 8 + 8 = 16 | ~775 min |
| Season 3 | 10 | ~541 min | Parte 5 | 10 | ~541 min |

**Everything from Parte 3 on is the same episodes.** Those were made for Netflix; the aired order just
groups Parte 3 and 4 together as its season 2 and calls Parte 5 season 3. Nothing was re-cut, and the
mapping is a season number and an offset:

```
aired S02E01..E08  ->  S03E01..E08
aired S02E09..E16  ->  S04E01..E08
aired S03E01..E10  ->  S05E01..E10
```

That is exactly what the scene numbering columns hold. Twenty-six of the forty-eight episodes are
fixed by a mapping a person can type in.

**Season 1 is a different problem, and not one a mapping can solve.** Fifteen episodes became
twenty-two and the total runtime is unchanged, so nothing was added - it was re-divided. The question
is whether the new cuts kept the old ones, and the running times answer it. Laying both orders out as
cumulative minutes:

```
aired    80 145 210 275 345 420 485 555 625 690 755 820 890 955   (1,025 total)
Netflix  45  85 135 185 225 270 320 365 410 465 510 555 610 655 ...  (1,020 total)
```

Of the fourteen internal boundaries, **one coincides**. The first Netflix episode runs 45 minutes and
the aired episode it would belong to runs 80; the gap between the two orders opens to around ninety
minutes before closing again at the end. A Netflix episode there does not correspond to an aired
episode - it finishes partway through one and carries on into the next.

So no mapping of that half can be *correct*. What people do instead - and it works - is arrange the
twenty-two files under the fifteen episodes so the order is preserved and the counts come out:

```
E05 x2   E06 x3   E07 x2   E11 x2   E12 x2   E13 x2   the other nine x1   =  22
```

Watched in order that gives the whole story with nothing missing or repeated. What it gives up is the
labelling: the title and synopsis on an episode describe eighty minutes of content while the file
filed under it holds forty-five of them, and the rest sits under the next episode's name. That is a
trade worth making with open eyes, not a mapping to be computed.

**This fork already holds that arrangement.** Several files on one episode is what the multiple-file
feature is, `{Multiple}` writes the `pt1`/`pt2`/`pt3` the arrangement needs, and manual import assigns
them. Elsewhere this is done by renaming every file by hand and keeping Sonarr away from the series
entirely. Nothing further is needed here, and nothing further would help: which file belongs under
which episode is a judgement, and there is no rule to derive it from.

The only thing that would make that half *correct* is the Netflix episode list itself, and Sonarr
cannot reach it. Asking Skyhook for this series returns 42 episodes - 1 special, 15, 16, 10 - with no
season type and no alternate list anywhere in the payload. TheTVDB's v4 API does serve it, so getting
it would mean a second metadata integration with a key each user has to obtain, and then a switch from
42 episode rows to 48, which is a destructive re-identification of every episode in the series - case
1 and case 4 over again.

### What to build, and what to say about it

The mapping is worth building for the twenty-six episodes it does fix here - Parte 3 to 5 are a
season number and an offset, and without a mapping every one of them is filed by hand as it arrives.
It also fixes outright the many series whose mismatch really is only a renumbering.

What it must not do is pretend to fix a re-cut. Where the counts differ the preview has to be able to
say "these releases have nowhere to go" and leave them to manual import, rather than finding somewhere
for them.

- a flag saying this series' mapping is hand-made, which `XemService` has to respect instead of
  clearing
- `UseSceneNumbering` settable once that flag is on
- the mapping table, which is the one below

**This fork has to be more careful here than stock Sonarr.** With one file per episode, a bad mapping
overwrites something and is noticed. Here an episode may legitimately hold several files, so a bad
mapping can quietly file the new one alongside the old as another version. The preview is not a
courtesy; it is what keeps a wrong mapping from being silent.

## They are one page

Cases 1, 4 and 5 are the same screen with a different destination:

```
choose destination  ->  fetch its metadata  ->  episode mapping table  ->  preview  ->  confirm
                          |                                                  |
   1: a new TVDB id (same row)                       what moves where, what has nowhere
   4: an existing series and season                  to go, what it collides with
   5: different numbers, same row
```

Underneath they are different operations - case 1 keeps the row and changes what it points at, case 4
keeps the destination row and moves files into it, case 5 changes no identity at all and only writes
the numbers the outside world uses - but the preview and the mapping table are the same work, and they
are the bulk of it.

They also meet in the middle: the episodes case 1 strands are exactly what case 4 exists to move, so
stranding them is case 4 offered as a step rather than a dead end.

One more will turn up eventually - a series split in two - and it is the same screen again, with a
destination that does not exist yet.

**Build case 4 first, not case 1.** Case 1 is the special case where the destination happens to be the
row you started from. Doing it first and fitting case 4 around it afterwards gives two pieces of code
that do one thing.

**Case 5 is the one with the most people waiting for it**, and it is the cheapest of the three that
share the table: no files move, no identity changes, nothing can collide with a unique index. If the
mapping table gets built for anything, build it there and the other two inherit it.

## Where this fits

The editions this refers to are [series-editions-v5-plan.md](series-editions-v5-plan.md). The
`{edition-...}` folder these operations rename or swap is read by the media servers described in
[media-server-naming.md](media-server-naming.md), so a folder move here is a re-scan there.

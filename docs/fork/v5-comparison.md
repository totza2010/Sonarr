# v5 against v4 and this fork

What v5 does that v4 does not, what that means for each feature of this fork, and where the two
collide. Checked against `upstream/develop` at `2b39049ee6` (16 October 2026, build 5.0.0.2668),
`upstream/main` at 4.0.20, and this fork's `main`.

v5 has 1,060 commits v4 does not; 144 of them are `New:` (Trusted Networks, hostname validation and
qBittorrent API keys reached v4 as well and are not counted). To repeat the count:

```bash
git log --no-merges --right-only --cherry-pick --format='%h %ad %s' --date=short upstream/main...upstream/develop | grep " New: "
```

## What v5 changes under everything

| | v4 (4.0.20) | v5 |
| --- | --- | --- |
| Runtime | .NET 6 | .NET 10 |
| UI build | webpack, redux | Vite, react-query, `.tsx` throughout |
| API the UI uses | `/api/v3` | **`/api/v5`** (`frontend/src/Utilities/Fetch/fetchJson.ts`); v3 is still served |
| Authentication | Basic, Forms, External | Basic obsolete; Forms is the default; OIDC added; lockout after failed logins; optional bypass for localhost. **External is kept** - production uses it |
| Postgres | 12+ | 15+ (production is SQLite) |
| Migrations | up to 217 | adds 207, 208 and 218-236. 207 and 208 are lower than v4's 217 and are applied out of order on upgrade, as for every v4 install |

## What v5 adds, by area

Only what matters for deciding the port; the full list is the command above.

**Library and metadata**

- Metadata language per series and per import list, with translated titles (`Series.Language`,
  `OriginalTitle`, table `SeriesTranslations`; migration 208)
- Episode order per series: aired, DVD, Netflix and the like (`Series.SeasonType`, `SeasonTypes`;
  migration 208, UI in `b4e9ab1791`)
- Original country (227); MAL and AniList ids (217, also in v4); TMDb id stored (206, also in v4)
- A series' path changes only once its files have moved (`PendingPath`, 234, `234326c2ad`)
- Statistics page; average size per episode on the series list; series progress in the details header
- Series filters by episode file quality, episode release type, monitored episodes

**Import**

- Multi-season releases are not imported automatically (`f91ebd4c07`), and episodes resolve across
  multi-season packs (`b0bde26f81`)
- Several queue items imported in one manual import screen (`ce8a5d8a6b`)
- **Manual import refuses any two rows on the same episode** (`494f446b05`)
- Custom format score worked out from the name the file will be given, before importing (`33fb0a4e88`)
- Media info keeps every audio and subtitle stream rather than a list of languages (`f3b39ba4f7`,
  migration 225)
- Fails executables and other dangerous files by default; qBittorrent-skipped extensions fail the torrent
- Parsing: "Part #" in an episode title of a non-mini series (`7b0db46c25`), ambiguous episode numbering
  (`b84a621e99`), Chinese season packs, 3-digit seasons, Temporada, UK dates, H.266

**Search, release profiles, download clients**

- Size limits move from Quality Definitions into Quality Profiles (207)
- Release profiles: several indexers each, excluded tags, air-date filtering, season packs before every
  episode has aired, minimum upgrade format score
- Fallback to another download client on failure; RQBit and Tribler 8 clients; full Transmission labels;
  per-indexer season pack seed ratio

**UI**

- Calendar date picker and custom filters; Wanted custom filters; time zone for displayed times
- Test Parsing and Manual Import moved to the page header menu; names on edit dialogs; accessibility

## The fork's features against v4 and v5

| Feature | v4 | v5 | What the port has to do |
| --- | --- | --- | --- |
| Series editions | - | - | Port in full. Key "same series" on one call, since v5 adds `OriginalTitle` and TMDb is coming |
| Edition rename and main-edition swap | - | - | Port, **adapting to `PendingPath`**: v5 no longer writes a moved series' path before the move |
| Parts and versions per episode | - | - | Port in full, **and change v5's manual import duplicate check** (`494f446b05`), which refuses exactly what this feature imports. Keep it for unmarked rows - it is the same rule as `findUnmarkedSharedFiles` |
| Daily housekeeper sparing extra files | - | - | Port; v5's `CleanupOrphanedEpisodeFiles` is unchanged and would delete every extra part |
| Map in Order | - | - | Port the pure module and its tests as they are; rebuild the screen |
| Naming languages, `ORIGINAL`, language flags | - | - | Port, **rewriting the MediaInfo side** for streams instead of language lists. `ORIGINAL` can use v5's series language data |
| Custom formats chosen by hand | - | - | Port, after reading `33fb0a4e88`: v5 now scores from the name the file will get, which is close to what the hand-added formats do |
| File name length warning and prediction | - | - | Port in full |
| Not importing multi-season releases | - | **Yes** | Nothing to port; this was wanted here and never built |
| Episode order per series | - | **Yes** | Nothing to port. Combined with editions: main edition in aired order, `{edition-Netflix}` in Netflix order |
| Several queue items in one import screen | - | **Yes** | **Drop ours** |
| Confirm Move or Hardlink/Copy on folder import | - | - | Port; v5's footer was rewritten |
| Replaces label, blocked import for clashing files | - | partly | v5 has the block, as an error on press; keep ours on the row and in the bar |
| Thai calendar date fixes (5 sites) | - | - | Still wrong in v5. Port, and offer upstream |
| Updates from this fork's releases | - | - | Port; v5's update code moved with .NET 10 |
| UI tests (`yarn test`) | - | - | v5 builds with Vite, so `vitest` costs nothing there and replaces the tsc + `node:test` runner |

## Where v5 and the fork collide

Each of these breaks something if the port carries the fork's code over unchanged.

1. **Duplicate episodes in manual import.** v5 stops the import if two rows share an episode. Every
   part and version import does that. The rule to keep is the fork's: refuse only when one of the rows
   says nothing about being a part or version.
2. **`PendingPath`.** A series' `Path` now changes after the move succeeds. The edition swap writes
   paths itself after moving folders, and the rename relies on `PUT ...?moveFiles=true`; both need to
   go through the new flow or deliberately around it.
3. **Media info streams.** `FileNameBuilder`'s language tokens and the fork's naming languages read
   `MediaInfo.AudioLanguages` and `Subtitles`, which v5 replaces with `AudioStreams` and
   `SubtitleStreams`.
4. **Custom format score from the future name.** v5 scores a file by the name it will be renamed to.
   The fork keeps hand-added formats out of the score on purpose; check the two rules agree.
5. **"Part #" in episode titles.** v5 parses it differently for non-mini series. The fork's
   `MultipleFileMarkerReader` already ignores a part token that only comes from the title; re-run its
   tests against v5's parser.
6. **Statistics and counts.** Editions are separate series rows, so the new statistics page counts
   each edition as a series. Decide whether that is right.
7. **API.** The fork's fields live on v3 resources. The v5 UI needs them on v5 resources; scripts and
   third-party clients still use v3, so both.

## What production needs to know

- External authentication is still supported, so Saltbox's setup carries over.
- The database is SQLite with the fork's migrations 9001-9024 applied. v5's own migrations do not touch
  the fork's columns (only additive columns on `Series`, and 225 rewrites `EpisodeFiles.MediaInfo`), so
  a database can move from the v4 fork to a v5 fork as long as the fork's migrations keep their
  numbers.
- Every v5 migration is one way. Rehearse on a copy of the production database before switching the
  image, and keep the copy.

## Open decisions

- When to start: the backend now, on a branch from `upstream/develop`, with the UI after v5 settles;
  or all of it once v5 is stable.
- Whether the row of bulk-action icons (a UI setting here) is rebuilt, or v5's footer is taken as it is.
- Whether the Thai date fixes go upstream first, so the port has five sites fewer to carry.

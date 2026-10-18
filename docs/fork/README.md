# What this fork adds to Sonarr v4

This folder is the map for moving the fork onto Sonarr v5. Each page covers one feature: what it does
for the person using it, where it lives in the code, which tests pin it down, and what v5 already
has in the same place. The code is the record; these pages say where to look and why it is shaped
the way it is.

Written against `main` at the 4.0.20 merge (October 2026). Paths are relative to the repository
root. When a page and the code disagree, the code wins and the page is out of date.

## The features

| Page | In one line |
| --- | --- |
| [Series editions](editions.md) | Several copies of one TVDB series, each a different release of the same episodes, in its own folder |
| [Multiple files per episode](multiple-files.md) | Parts and versions of an episode kept side by side instead of replacing each other |
| [Rename helper](rename-helper.md) | Naming languages, the `ORIGINAL` language filter, language flags, custom formats chosen by hand |
| [File name length](file-name-length.md) | Warns before a name or path is too long for what reads the library |
| [v5 against v4 and this fork](v5-comparison.md) | What v5 adds, where it collides with the fork, what production needs |
| [Smaller changes](smaller-changes.md) | Thai calendar fixes, Map in Order, several downloads in one import screen, updates from this fork, CI and Docker |

## Rules every feature follows

These are why the code looks the way it does, and they have to survive the port.

- **Nothing changes for an install or a client that does not use the feature.** Every new API field
  is optional, every write path treats a missing field as "leave it alone", and editions and extra
  files stay out of the API unless a client asks for them (`includeEditions`, `includeMultiples`).
  A third-party client written against stock Sonarr sees stock Sonarr.
- **Migrations live in their own number range**, 9000 and up, so upstream can keep numbering from
  where it is without colliding. See the table below.
- **Edits to upstream files stay in one labelled block** with a comment saying what it is for, never
  interleaved line by line. The files that collide on every upstream merge are `FileNameBuilder.cs`,
  `ManualImportService.cs`, `EpisodeFileResource.cs` and `EpisodeFileController.cs`.
- **Upstream is merged, never rebased onto.** `main` is published and other branches start from it.

## Migrations

| Number | File | Adds |
| --- | --- | --- |
| 9001 | `src/NzbDrone.Core/Datastore/Migration/9001_add_series_edition.cs` | `Series.EditionName` (empty = main edition); unique index on `(TvdbId, EditionName)` in place of the one on `TvdbId` |
| 9010 | `9010_add_episode_file_parts.cs` | `EpisodeFiles.PartNumber`, `VersionName`; table `EpisodeFileLinks (EpisodeId, EpisodeFileId)` |
| 9011 | `9011_episode_file_multiples.cs` | `EpisodeFiles.MultipleType`; `PartNumber` renamed `MultipleNumber`; `VersionName` folded in and dropped |
| 9020 | `9020_series_naming_language.cs` | `Series.NamingLanguage` |
| 9021 | `9021_episode_file_naming_languages.cs` | `EpisodeFiles.NamingAudioLanguages`, `NamingSubtitleLanguages` (JSON lists) |
| 9022 | `9022_episode_file_manual_custom_formats.cs` | `EpisodeFiles.ManualCustomFormats` |
| 9023 | `9023_episode_file_excluded_custom_formats.cs` | `EpisodeFiles.ExcludedCustomFormats` |
| 9024 | `9024_naming_config_show_language_flags.cs` | `NamingConfig.ShowLanguageFlags` |

v5 numbers its own migrations in the 230s, so the range is still clear. What each one would take to
undo is in `tools/rollback-plan.json`, checked by `ForkMigrationRollbackPlanFixture`, and the way
back to official Sonarr is [rollback-to-official.md](../rollback-to-official.md).

## What v5 already has in the same place

Checked against `upstream/v5-develop` at `4831dfe91c` (10 October 2026).

| Ours | In v5 | What it means for the port |
| --- | --- | --- |
| Series editions | Nothing | Port in full. v5's `Series` has a `TmdbId` beside `TvdbId`; see the TMDb note in [editions](editions.md) |
| Multiple files per episode | Nothing | Port in full |
| Naming languages, `ORIGINAL`, flags | Nothing | Port in full |
| Custom formats chosen by hand | Nothing | Port in full |
| File name length warning | Only the existing trim to the file system limit | Port in full |
| Several downloads in one import screen | **Yes**, `ce8a5d8a6b` "Manually import multiple items at the same time from Activity: Queue" (Dec 2025) | **Drop ours**, keep only the edition handling it carried |
| Thai calendar date fixes | **Not fixed.** `Parser.cs` and `STJUtcConverter.cs` still format dates in the machine's culture | Port, and worth offering upstream |
| Not importing multi-season releases automatically | **Yes**, `f91ebd4c07` (Feb 2026) | Never built here; v5 gives it for free |
| Episode order (aired, DVD, Netflix) per series | **Yes**, `b4e9ab1791` | New; combines with editions, see [editions](editions.md) |

To repeat the check after upstream moves:

```bash
git fetch upstream
git grep -l -E "EditionName|EpisodeFileLink|MultipleType|NamingLanguage|ManualCustomFormats|FileNameLength" upstream/v5-develop -- src frontend/src
git log --oneline upstream/v5-develop --no-merges --since=<last check> | grep -iE "edition|part|version|multiple|language|custom format|length|import"
```

## The order to port in

1. **Backend first, feature by feature, migrations with it.** The tests listed on each page are the
   guard: anything upstream renamed underneath us shows up as a compile error or a failing test.
2. **Editions before multiple files before the rename helper.** The rename helper touches both of the
   others (episode rows, import rows, naming), so it goes last. The same order has merged with the
   fewest conflicts every time it has been tried.
3. **The UI last, rebuilt rather than carried over.** v5 moved from webpack to Vite, from redux to
   react-query, and from `.js` to `.tsx`. Of the 131 frontend files this fork changes, 100 no longer
   exist under the same path in v5. Each page lists the UI pieces by what they do, so they can be
   rebuilt on v5's own components.

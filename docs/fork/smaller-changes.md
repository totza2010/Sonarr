# Smaller changes

Changes that are not features of their own but have to come along, or be consciously dropped, when
the fork moves.

## Dates written in the machine's calendar

On a machine whose culture uses another calendar — Thai uses the Buddhist one — formatting a date
without naming a culture writes a different year: 2026 leaves as 2569. Sonarr formats dates that way
in several places, so every date the API sends is 543 years out and comes back wrong, and a daily
episode's air date matches no episode.

Each site now names `CultureInfo.InvariantCulture`:

| Where | What it broke |
| --- | --- |
| `src/NzbDrone.Common/Serializer/System.Text.Json/STJUtcConverter.cs` | Every date in every API response and request, Sonarr's own UI included |
| `src/NzbDrone.Core/Parser/Parser.cs` | Air date parsed from a daily release name |
| `src/NzbDrone.Core/Tv/RefreshEpisodeService.cs` | Air date given to an episode from the series' first aired date |
| `src/NzbDrone.Core/Extras/Metadata/Consumers/Xbmc/XbmcMetadata.cs` | `premiered` and `enddate` in Kodi `.nfo` files |
| `src/NzbDrone.Core/Indexers/HDBits/HDBitsRequestGenerator.cs` | Daily episode searches on HDBits |

Tests: `JsonFixture`, `DailyEpisodeParserFixture`, `GetEpisodesFixture`.

**Against v5:** none of these is fixed in v5 (`Parser.cs` and `STJUtcConverter.cs` still format with
the current culture). Port all of them. They are self-contained and would be worth offering upstream
as a pull request.

## Several downloads in one manual import screen

Select several items in Activity → Queue and import them together, so a batch of releases that all
need the same correction is one screen instead of one each. Each file keeps the download it came
from.

- `src/NzbDrone.Core/MediaFiles/EpisodeImport/Manual/ManualImportService.cs`,
  `src/Sonarr.Api.V3/ManualImport/ManualImportController.cs` (several download ids)
- UI: `Activity/Queue/Queue.tsx`, `InteractiveImport/InteractiveImportModal.tsx`
- Test: `ManualImportServiceFixture`

**Against v5:** v5 has its own (`ce8a5d8a6b`, "Manually import multiple items at the same time from
Activity: Queue", December 2025). **Drop ours and use v5's.** Check only that an edition chosen per
row survives v5's version.

## Map in Order

A manual import helper for a folder whose names cannot be trusted - a whole run of an anime in one
folder, numbered its own way. The selected files are given episodes by their order alone, across every
season, with specials left out of the order. Each row says what it is (an episode, two episodes, a
part or a version of the file above, a special chosen by hand), can skip episodes that have no file or
be pinned to an episode, and every row below follows. Rows can be dragged, or moved after a numbered
row. Applying writes series, season, episodes and markers onto the rows without reprocessing them
(over a slow mount that takes most of an hour for a thousand files); Import is unchanged. The work in
progress is kept in the browser per set of files.

Two helpers sit beside it. The number a file gives itself (after `EP`, `E`, `Episode` or `ตอนที่`) is
shown next to its place in the order, and the rows where the difference between the two changes are
flagged with a button to step through them - those are where a double episode, a gap or a special
usually is. And **Check Selected Files** in manual import reads the selected rows again fifty at a
time with a running count and a way to stop (`checkInteractiveImportItems` in
`Store/Actions/interactiveImportActions.js`), for when the rejections are wanted before importing.

- The mapping itself: `frontend/src/InteractiveImport/MapInOrder/mapInOrder.ts` - the order, the
  natural sort, the number read from a name and the shift finder, all pure functions importing only
  types. Tested in `mapInOrder.test.ts` with `yarn test`; see "UI tests" below.
- The screen: `MapInOrder/MapInOrderModalContent.tsx`; the action in `InteractiveImportModalContent.tsx`.
- Backend: `ManualImportFile.MultipleFromName` and `ManualImportReprocessResource.MultipleFromName`
  (optional; missing means true). False stops `AggregateMultipleFile` reading a part or version from
  the name, so a file the order calls a whole episode stays one. Tests in `AggregateMultipleFileFixture`
  and `ManualImportServiceFixture`.

**Against v5:** nothing like it. The backend flag ports with the multiple-file work; the screen is
rebuilt on v5's manual import.

## Manual import bulk actions in one click

The bulk edits (series, edition, season, episode, release group, quality, languages, naming
languages, custom formats, indexer flags, release type, part or version) as a row of icon buttons
along the footer instead of a `Select…` menu, shown only while something is selected. Behind a UI
setting, `InteractiveImportInlineActions` (`UiConfigResource`, `Settings/UI/UISettings.js`).

**Against v5:** the modal was rewritten; rebuild on v5's footer if still wanted.

## Updates from this fork's releases

`src/NzbDrone.Core/Update/UpdatePackageProvider.cs` reads releases from
`github.com/totza2010/Sonarr` instead of Sonarr's update service, through `GitHubReleaseResource` and
`UpdatePackageMapper`, so the update modal shows this fork's changes. A refusal from GitHub (no
network, rate limit) is answered with "no updates" on purpose. The tests that call GitHub become
inconclusive rather than failing when GitHub refuses the build machine.

Tests: `UpdatePackageMapperFixture`, `UpdatePackageProviderFixture`.

## Build, image, release

- `.github/workflows/build.yml`, `deploy.yml`, `release.yml`: build on this fork, publish the image to
  `ghcr.io`, create the GitHub release the update check reads. Upstream's notify jobs removed.
- `Dockerfile`, `docker-entrypoint.sh`, `.dockerignore`: Alpine image in the shape of linuxserver and
  hotio, no updater shipped, `PUID`/`PGID`, `TZ`. Details in [docker.md](../docker.md).
- The test that needed `badssl.com` to answer no longer gates a release.

## UI tests

Sonarr has no tests for its UI. `yarn test` (`frontend/build/runTests.js`) compiles every
`frontend/src/**/*.test.ts` with the project's own TypeScript (`frontend/tsconfig.test.json`, into
`_temp/uitests`) and runs it with Node's built-in test runner, so no package is added and CI runs it
in the frontend job. A file under test may import only types from the rest of the app, because the
compiled output does not know the app's import paths; keep logic worth testing in such files.

## Developer tools

- `dev.ps1` — starts the development server against its own data folder: builds whatever is missing
  (`Sonarr.Windows.dll` is in another project from the one `dotnet watch` builds), builds the UI if
  absent, stops a server already running. `-Ui`, `-Build`, `-NoWatch`, `-Stop`.
- `tools/compare-openapi.py` — compares the API spec against upstream's, to check that every change
  is an addition.
- `tools/rollback-to-official.py`, `tools/rollback-plan.json` — see
  [rollback-to-official.md](../rollback-to-official.md).

## Writing that is not code

- [media-server-naming.md](../media-server-naming.md) — what Plex, Jellyfin and Emby actually read
  from folder and file names (editions, parts, versions).

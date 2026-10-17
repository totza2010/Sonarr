# Series editions

A second (third, ...) copy of the same TVDB series holding a different release of the same episodes:
black and white against colour, dubbed against original, an AI upscale, another broadcast order. Each
edition has its own folder, its own files and its own episode rows, and Plex reads it as an edition
through `{edition-Name}` in the folder name.

The background, and what moving a series between identities would take, is in
[series-editions-v5-plan.md](../series-editions-v5-plan.md) (written in July, before most of the
work below; its migration numbers and "v5 line" column are out of date) and
[series-identity-migration-plan.md](../series-identity-migration-plan.md).

## The rules it rests on

- **The main edition has an empty edition name.** Existing series keep their identity untouched, and
  everything that only knows an id or a title lands on the main edition: release parsing, metadata
  refresh, `?tvdbId=` lookups. Only the main edition is automated.
- **Editions are the version axis, not the quality axis.** `{edition-Black & White}` is right,
  `{edition-4K}` is wrong; resolution belongs to the quality profile and to a separate instance.
- **An edition is an internal idea.** TVDB never hears about editions; they share its metadata and
  differ only in name, path and slug.
- **Two editions of one series cannot share a name**, case-insensitively, and the name is stored
  already cleaned for a folder so the UI, the folder and Plex cannot drift apart.

## Data

- `Series.EditionName`, migration 9001. Unique index `(TvdbId, EditionName)`.
- `TitleSlug` stays unique by appending the edition (`SeriesEditions.ApplyEditionToSlug`); a metadata
  refresh rewrites the slug the same way (`RefreshSeriesService`).
- Episode rows are per edition. A grab, a queue item and a history entry each point at one edition's
  episodes, which is what ties a download to an edition.

## Backend

| Where | What |
| --- | --- |
| `src/NzbDrone.Core/Tv/SeriesEditions.cs` | All the rules in one place: `NormalizeEditionName`, `IsMainEdition`, `SameEdition`, `ApplyEditionToSlug`, `ReadEditionFromFolder` (the `{edition-X}` token), `FindEditionForPath`, `EditionIsAmbiguous`. `ISeriesEditionIdentity` lets validators read the edition off any resource |
| `src/NzbDrone.Core/Tv/SeriesRepository.cs`, `SeriesService.cs` | `FindByTvdbId` and the other id and title lookups return the main edition; `FindAllByTvdbId`, `FindByTvdbIdAndEdition`, `AllSeriesEditions()` (TvdbId → names); inexact title matches break ties towards the main edition |
| `src/NzbDrone.Core/Tv/AddSeriesService.cs` | Adding an edition: refuses a name the series already has, stores the cleaned name, keeps a given folder as-is when importing an existing one |
| `src/NzbDrone.Core/Validation/Paths/SeriesExistsValidator.cs` | A TVDB id may be added again as long as the edition is new |
| `src/NzbDrone.Core/Organizer/FileNameBuilder.cs` | `GetSeriesFolder` adds `{edition-Name}`; the edition block is labelled |
| `src/NzbDrone.Core/Download/TrackedDownloads/TrackedDownloadService.cs` | A release title only ever matches the main edition, so a download grabbed for another edition is swapped back to the edition the grab recorded, episodes and all |
| `src/NzbDrone.Core/MediaFiles/EpisodeImport/Specifications/EditionSpecification.cs` | Refuses to import automatically when the edition is a guess: the file's episodes are not the ones a grab recorded and its path does not say which edition |
| `src/NzbDrone.Core/MediaFiles/EpisodeImport/Manual/ManualImportService.cs` | Manual import picks the edition from the path (`FindEditionForPath`) and flags `EditionUnconfirmed` when it could not |
| `src/NzbDrone.Core/RootFolders/RootFolderService.cs`, `UnmappedFolder.cs` | Library Import reads `{edition-X}` off each unmapped folder (`UnmappedFolder.EditionName`) |

## Renaming an edition, and changing which one is main

- **Rename** (`SeriesService.ApplyEditionRename`, reached from any `PUT /series/{id}`): the name is
  stored cleaned, the slug follows at once (built from the main edition's slug, or from the edition's
  own when there is no main), and a name another edition has is refused as a validation error on
  `EditionName`. The main edition cannot be given a name, nor an edition have its name taken away -
  both are a swap. The Edit Series dialog has the field for non-main editions and rewrites
  `{edition-Old}` in the path as the name is typed (`frontend/src/Series/replaceEditionInPath.ts`,
  tested with `yarn test`), so the usual "move files?" question follows on save. The series page
  follows the slug when it changes (`SeriesDetailsPageConnector`).
- **Swap** (`SwapMainEditionCommand`, `SeriesEditionSwapService`): the chosen edition becomes main and
  the main one takes its name. Each row keeps its files, episodes and settings. The folders trade
  places through `<main path>.edition-swap`, and are put back if a move fails; the database is only
  written once the folders are where they belong, through a temporary name, slug and path so no
  unique index is ever crossed. A left-over `.edition-swap` folder stops the next swap. With no main
  edition at all, the edition is simply promoted and its folder left where it is. "Make Main Edition"
  on the series page, beside the list of editions (`Series/Details/SeriesEditionLinks.tsx`).

## API (`Sonarr.Api.V3`)

- `SeriesResource.EditionName` — empty for the main edition.
- `GET /series` returns main editions only; `?includeEditions=true` returns every edition. `?tvdbId=`
  always returns the main edition only, with or without the flag.
- `PUT /series/{id}?moveFiles=true` with a changed `path` moves an edition's folder through upstream's
  own `MoveSeriesCommand`.
- `ManualImportResource.EditionUnconfirmed`.
- Duplicate editions reach the database's unique index as a 500 on `PUT`; there is a validator on
  `POST` only.

## UI (what to rebuild on v5)

- Add Series: an "Add Edition" action on a search result that is already in the library, with name
  suggestions (`Series/seriesEditionSuggestions.ts`) — `AddSeries/AddNewSeries/SelectSeriesEdition*`.
  The lookup result for a series already in the library is that series, so its id and path must be
  cleared before adding (`Utilities/Series/getNewSeries.ts`).
- Library Import: a folder whose series is already in the library is no longer locked. Each row can
  skip, become a new edition (name taken from `{edition-X}` in the folder when it names one the
  library does not have) or replace an existing edition's folder after a confirmation —
  `AddSeries/ImportSeries/Import/getImportSeriesEdition.js`, `ImportSeriesEdition*`,
  `Store/Actions/importSeriesActions.js` (new series and editions `POST /series/import`;
  replacements `PUT` with the new path and then a refresh).
- An edition badge wherever a series is named: index, posters, overview, search, queue, history,
  blocklist, wanted, episode details, manual import — `Series/SeriesEditionBadge.tsx`,
  `Series/seriesEditionTitle.ts`, `Series/SeriesTitleLink.tsx`.
- Manual import: an edition picker per row and in bulk (`InteractiveImport/Edition/`), and the
  unconfirmed state drawn like an empty required cell.

## Tests

`SeriesEditionsFixture`, `SeriesEditionChangesFixture`, `SeriesEditionSwapServiceFixture`, `SeriesRepositoryFixture`, `SeriesApplyChangesFixture`, `AddSeriesFixture`,
`SeriesEditionFolderFixture`, `EditionSpecificationFixture`, `TrackedDownloadServiceFixture`,
`ManualImportServiceFixture`, `RootFolderServiceFixture`, `9001_add_series_editionFixture`.

## Known gaps

- Only the main edition is automated; other editions are filled by manual import.
- The unique key ignores the root folder, so one instance cannot hold the same edition twice.
- A main edition cannot be deleted while other editions remain (`SeriesService.DeleteSeries`, HTTP 409;
  the delete dialog says so and disables the button). Deleting all of them together is allowed.

## Against v5

v5 has nothing for editions. Two v5 additions change the design space:

- **Episode order per series** (`b4e9ab1791`: aired, DVD, Netflix). An edition with a different order
  is one of the main reasons to have editions — the main edition in aired order, `{edition-Netflix}`
  in Netflix order. On v4 every edition is forced into the one order.
- **TMDb.** v5's `Series` already stores a `TmdbId`, and the release notes promise TMDb as the
  metadata source for "all or some" series. Every edition rule here keys on `TvdbId`: the unique
  index, `FindAllByTvdbId`, `AllSeriesEditions()`, `?tvdbId=`, the `{tvdb-…}` folder token. A series
  sourced from TMDb may have no TVDB id, or one whose seasons no longer describe it. Before porting,
  find out how v5 identifies such a series and key "editions of the same series" on that. Keep the
  question behind one call — "which series are editions of this one" — rather than comparing
  `TvdbId` at each site, so the answer can change in one place.

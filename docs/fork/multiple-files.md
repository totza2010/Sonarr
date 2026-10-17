# Multiple files per episode

Stock Sonarr keeps one file per episode, and a second file for the same episode replaces the first.
This keeps several, as either:

- **Parts** — pieces of one episode (`pt1`, `pt2`, `pt3`), e.g. a broadcast split across two files.
- **Versions** — the whole episode more than once, told differently (`v1`, `v2`), e.g. a director's
  cut beside the broadcast cut.

The two behave identically and share one field; they differ only in the marker written into the
file name. A file can also span several episodes (`S01E04E05.pt2`) and still be a part or version.

## The rules it rests on

- **The episode still points at one file.** `Episode.EpisodeFileId` is the primary file, exactly as
  in stock Sonarr. Every other part or version is owned through the link table. Anything that only
  knows one file per episode keeps working, and sees the first part.
- **A file is only kept beside another when it says which one it is.** A file with no marker
  replaces whatever the episode has, parts included, as a normal upgrade would. The same marker
  replaces the file with that marker. A different marker sits beside it.
- **The naming format has to be able to tell them apart.** Without `{Multiple}` in the episode
  format, renaming would give every part the same name and the next rename would drop them onto one
  path. Saving such a format is refused (`MultipleFileNamingValidator`), and importing a part is
  refused while the format cannot write it (`MultipleNotSupportedByNaming`).
- **Lower numbers come first.** `pt3` or `v3` read from a name only counts when `1` and `2` of the
  same kind exist for the same episode, in the library or beside it in the folder. This is what keeps
  `V3` in an episode title, or `Part 3` of a story arc, from being read as a version or part.

## Data

- `EpisodeFiles.MultipleType` (`None`, `Part`, `Version`) and `MultipleNumber`, migrations
  9010–9011 (`src/NzbDrone.Core/MediaFiles/EpisodeFileMultipleType.cs`).
- Table `EpisodeFileLinks (EpisodeId, EpisodeFileId)`: one row per extra file per episode it covers.
  The primary file is **not** in it.

## Backend

| Where | What |
| --- | --- |
| `src/NzbDrone.Core/MediaFiles/EpisodeFileLinkService.cs`, `EpisodeFileLinkRepository.cs`, `EpisodeFileLink.cs` | `Link`, `GetLinkedFileIds`, `GetEpisodeIdsByFileIds`, `GetLinkedEpisodeIds`; drops links when a file or a series is deleted |
| `src/NzbDrone.Core/MediaFiles/MultipleFileMarker.cs` | `MultipleFileMarkerReader.Read(fileName, series, episodes)`: finds `part N`, `ptN`, `vN` and the like, and ignores a token the series or episode titles already contain as often |
| `src/NzbDrone.Core/MediaFiles/EpisodeImport/Aggregation/Aggregators/AggregateMultipleFile.cs` | Reads the marker during import, after quality (which decides whether `v2` was a repack), only when the format has `{Multiple}`; applies the lower-numbers-first rule. A marker chosen by hand wins |
| `src/NzbDrone.Core/MediaFiles/EpisodeImport/Specifications/MultipleFileSpecification.cs` | Refuses a part whose earlier parts the episode does not have, or whose existing file is not marked — unless that file's name carries an earlier marker of the same kind |
| `src/NzbDrone.Core/MediaFiles/EpisodeImport/ImportApprovedEpisodes.cs` | Adds the extra file, writes its links, and records the marker an existing file carries only in its name (`RecordNamedMarkers`). Remembers which file each episode came to point at during one run, so parts imported together do not each take the episode (labelled block) |
| `src/NzbDrone.Core/MediaFiles/UpgradeMediaFileService.cs` | Which existing files a new one replaces: same marker replaces, different marker stays, no marker replaces all |
| `src/NzbDrone.Core/Tv/EpisodeService.cs` (`Handle(EpisodeFileAddedEvent)`) | Leaves the episode's pointer alone when an additional file arrives for an episode that already has one |
| `src/NzbDrone.Core/MediaFiles/MediaFileService.cs` | Logs every add and delete with the reason; reads a file's links before deleting so history can still name its episodes (`EpisodeFileDeletedEvent.LinkedEpisodeIds`) |
| `src/NzbDrone.Core/MediaFiles/MediaFileTableCleanupService.cs` | A disk scan does not treat a linked file as unassigned |
| `src/NzbDrone.Core/Housekeeping/Housekeepers/CleanupOrphanedEpisodeFiles.cs` | The daily orphan cleanup spares linked files. Upstream's SQL removed every file no episode pointed at — every extra part, once a day |
| `src/NzbDrone.Core/MediaFiles/RenameEpisodeFileService.cs`, `EpisodeFileMovingService.cs` | Renaming and moving find an extra file's episodes through its links |
| `src/NzbDrone.Core/Organizer/FileNameBuilder.cs`, `NamingTokens.cs` | The `{Multiple}` token (`pt1`/`v1`, with any separator), `SupportsMultipleFiles` |
| `src/NzbDrone.Core/MediaFiles/EpisodeFileLinkIntegrity.cs`, `HealthCheck/Checks/EpisodeFileLinkCheck.cs`, `Commands/CleanEpisodeFileLinksCommand.cs` | Health check for links to files that no longer exist, naming each one (`Series - S01E01`), with a command to clear them |
| `src/NzbDrone.Core/History/HistoryService.cs` | History for extra files resolves episodes from their links |

## API (`Sonarr.Api.V3`)

- `EpisodeFileResource.MultipleType`, `MultipleNumber` (nullable on write: missing means unchanged).
- `GET /episodefile?seriesId=N` returns primary files only; `&includeMultiples=true` adds the extras.
- `EpisodeResource.AdditionalEpisodeFileIds` — null unless the episode has extras, and dropped from
  the JSON when null, so an install without extras serves the same bytes as stock.
- `ManualImportResource.MultipleType`, `MultipleNumber`. Reprocessing a row returns the marker read
  from the file name when none was chosen by hand (`ManualImportController.ReprocessItems`).
- Calendar, missing and cutoff controllers carry `IEpisodeFileLinkService` so episodes report their
  extra files.

## UI (what to rebuild on v5)

- Episode details list every file of an episode (`Episode/Summary/EpisodeSummaryFileRow.tsx`), the
  series page shows the marker, and one missing file no longer blanks the whole page
  (`FETCH_EPISODE_FILE` has a handler of its own in `Store/Actions/episodeFileActions.js`).
- Manual import: a Part or Version column per row; "Mark as Multiple" numbers a selection in the
  order it is listed and also clears markers; an existing file's marker is saved straight away
  (`InteractiveImport/Multiple/`, `InteractiveImportModalContent.tsx`).
- Manual import status bar: counts of files that share an episode with nothing said about them,
  files marked and waiting for import, and files marked in the library; each count filters the table
  to those episodes, keeping one episode's files together (`InteractiveImport/Multiple/getMultipleStates.ts`).
- Health page: "Clear Missing Links" beside the link health check (`System/Status/Health/Health.tsx`).

## Tests

`EpisodeFileLinkServiceFixture`, `MultipleFileMarkerReaderFixture`, `AggregateMultipleFileFixture`,
`MultipleFileSpecificationFixture`, `ImportApprovedEpisodesFixture`, `UpgradeMediaFileServiceFixture`,
`UpgradeSpecificationFixture`, `RenameEpisodeFileServiceFixture`, `MediaFileRepositoryFixture`,
`CleanupOrphanedEpisodeFilesFixture` (database test), `EpisodeFileLinkIntegrityFixture`,
`EpisodeFileLinkCheckFixture`, `HistoryServiceFixture`, `MultipleFileNamingValidatorFixture`,
`9011_episode_file_multiplesFixture`.

## Things that went wrong, and what guards them now

Each of these lost parts on a real library. They are the cases to re-run first after porting.

| What happened | Guard |
| --- | --- |
| Upstream's daily housekeeper deleted every file no episode pointed at | `CleanupOrphanedEpisodeFilesFixture` |
| Parts found together by a scan each took the episode; all but the last were left belonging to nothing | `ImportApprovedEpisodesFixture.should_keep_the_first_part_as_the_file_of_an_episode_when_parts_are_imported_together` |
| A marker set on several rows at once was lost on Import | the Import payload carries `multipleType`/`multipleNumber` |
| `V3` in an episode title read as a version | lower-numbers-first rule, `AggregateMultipleFileFixture` |
| A part with no record of its earlier part refused on rescan | `MultipleFileSpecificationFixture` (name of the existing file) |
| A part read from the name dropped when the row was given its episode | `ManualImportController.ReprocessItems` (no unit test; controller glue) |

Production evidence: `Kamen Rider`, `Cherry Magic`, `Dr. Slump` were the libraries that showed these.

## Against v5

Nothing in v5 knows about more than one file per episode. Two v5 changes sit right on top of this
code and need reading before the port:

- `ImportApprovedEpisodes.cs` lost about 70 lines in v5. Find where they went before re-adding the
  additional-file block.
- `ManualImportService.cs` and the manual import modal were reworked for importing several queue
  items at once; the multiple-file pieces have to be re-applied on that version.

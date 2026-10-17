# File name length

Sonarr copes with longer names than most of what reads the library afterwards — a Plex client, an
SMB share, a cloud mount, Windows' 260-character path limit. A name Sonarr writes happily can be one
nothing else can open, and nobody finds out until playback fails. This says so ahead of time, in
three places.

## Settings

Media Management → File Name Length Limit (bytes, default 255) and File Path Length Limit
(characters, default 0 = off). Zero turns either check off. They are separate because they are
different ceilings in different units: a name is capped per component by the file system in bytes, a
path by whatever is reading it.

- `src/NzbDrone.Core/Configuration/ConfigService.cs`, `IConfigService.cs` — `FileNameLengthLimit`,
  `FilePathLengthLimit`
- `src/Sonarr.Api.V3/Config/MediaManagementConfigResource.cs` — the same two fields

## 1. The library check

A scheduled task (daily) and a command measure every file in the library against both limits.

- `src/NzbDrone.Core/MediaFiles/FileNameLengthService.cs` — `IFileNameLengthService`, `OverlongFile`
  (`NameTooLong`, `PathTooLong` kept apart); reports progress per series in the corner while it runs
  and lists the series over a limit in Events when it finishes
- `src/NzbDrone.Core/MediaFiles/Commands/CheckFileNameLengthsCommand.cs`,
  `Events/FileNameLengthsScannedEvent.cs`, `src/NzbDrone.Core/Jobs/TaskManager.cs` (the schedule)
- `src/NzbDrone.Core/Tv/EpisodeRepository.cs`, `EpisodeService.cs` — `AllEpisodesWithFiles()`

## 2. The health check

`src/NzbDrone.Core/HealthCheck/Checks/FileNameLengthCheck.cs` turns the last scan's result into a
health warning, so it is visible without opening Events.

## 3. The prediction on the naming page

Before a format is saved, the naming settings show how long it can get: file name, season folder,
series folder, the whole relative path and the full path, each against its limit. It uses Sonarr's
own invented sample series and episode, stretched to the longest series title, episode title and root
folder actually in the library, so the number is the worst case for this library rather than for the
sample.

- `src/NzbDrone.Core/Organizer/FileNameLengthPredictionService.cs` — `IPredictFileNameLength.Predict(NamingConfig)`
  returns one `FileNameLengthPrediction`; `StretchTitle` builds the synthetic names
- `src/Sonarr.Api.V3/Config/NamingExampleResource.cs` — `LengthPrediction`
- UI: `Settings/MediaManagement/Naming/NamingLengths.tsx` as a sticky table to the right of the form
  (`Naming.tsx`; the form column needs `min-width: 0` or long examples push the table off screen)

## Tests

`FileNameLengthServiceFixture`, `FileNameLengthCheckFixture`, `FileNameLengthPredictionServiceFixture`.

## Against v5

v5 has only what v4 already had: `FileNameBuilder` trims a name to the file system limit
(`LongPathSupport.MaxFileNameLength`). Nothing warns, nothing predicts. Port in full; the backend
pieces are self-contained.

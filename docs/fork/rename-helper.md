# Rename helper

The part of the fork that lets a person correct what a file's name says about it, and see what the
names in the library say. Four pieces, one purpose: the file name is the record a media server and
the next tool read, so it should be right, and fixable without renaming by hand.

What is still missing is in [rename-helper-plan.md](../rename-helper-plan.md). Its first section,
setting naming languages and custom formats on many files at once, has since been built (both are in
the manual import bulk actions).

## 1. Naming languages

MediaInfo is often wrong or empty about languages — an untagged dub, a track named in words rather
than a code. A file can carry its own list of audio and subtitle languages that the name is built
from instead of MediaInfo, and a series can carry the language that counts as its original.

- Data: `EpisodeFiles.NamingAudioLanguages`, `NamingSubtitleLanguages` (9021); `Series.NamingLanguage`
  (9020). An empty list means "use MediaInfo", so untouched files name exactly as before.
- `src/NzbDrone.Core/Organizer/FileNameBuilder.cs` — the `{MediaInfo AudioLanguages}`,
  `{MediaInfo SubtitleLanguages}`, their `All` forms and `{MediaInfo Full}` read the file's list
  before MediaInfo's (`GetNamingLanguages`).
- Languages written in words are resolved to codes on every platform (`EpisodeFile/MediaInfo.tsx`
  shows them in words; the Linux and Windows culture lookups disagree, see the locale notes in the
  repository's test-failure memory).
- API: `EpisodeFileResource.NamingAudioLanguages`, `NamingSubtitleLanguages`;
  `SeriesResource.NamingLanguage`; `ManualImportResource` carries both plus the detected lists
  (`DetectedAudioLanguages`, `DetectedSubtitleLanguages`) the picker starts from. A series update from
  a client that predates the field leaves it alone.
- UI: a picker per file and in bulk in manual import (`InteractiveImport/NamingLanguages/`), the
  series edit modal, `EpisodeFile/NamingLanguages.tsx`.

## 2. The `ORIGINAL` language filter

A language filter can name `ORIGINAL` — `{MediaInfo AudioLanguages:TH+EN+ORIGINAL}` — meaning "the
series' original language, whatever it is". It resolves to `Series.NamingLanguage` when set,
otherwise the series' original language from metadata (`ResolveLanguageFilter`,
`OriginalLanguageFilterRegex`). A filter without the word is untouched.

## 3. Language flags

With `NamingConfig.ShowLanguageFlags` (9024) on, the series list and series page show flags for the
languages the library's file names carry.

- The languages are read back out of file names, not MediaInfo, so the flags show what Plex will see
  (`src/NzbDrone.Core/Organizer/FileNameLanguages.cs`: the bracketed groups in the order the format
  writes them).
- Season statistics carry the file names only when flags are on (`SeriesStatisticsRepository`,
  `SeasonStatistics.FileNamesString`, `LanguageGroups`); `SeriesStatisticsResource` exposes
  `AudioLanguages`, `SubtitleLanguages`.
- `LanguageFlagsNamingValidator` refuses to save the switch on with no language token in the format,
  or the token removed while the switch is on.
- UI: `Components/LanguageFlags/`.

## 4. Custom formats chosen by hand

A file can be given a custom format its name does not earn yet (`ManualCustomFormats`, 9022) or have
one taken away (`ExcludedCustomFormats`, 9023), so `{Custom Formats}` writes what the person says.

- `src/NzbDrone.Core/CustomFormats/CustomFormatCalculationService.cs` separates what a file earns on
  its own (`ParseCustomFormatFromName`, and `ParseScoredCustomFormat` for scoring) from the list the
  name is built from. **A format added by hand never counts towards the score**: it is a naming
  instruction, and scoring it would let a rename block real upgrades. `UpgradeDiskSpecification` uses
  the scored list.
- `ManualCustomFormatCleanupService` drops a hand-added format once the renamed file earns it on its
  own, so the name stays the only record.
- API: `EpisodeFileResource` and `ManualImportResource` `ManualCustomFormats`, `ExcludedCustomFormats`.
- UI: `InteractiveImport/CustomFormat/`, `Episode/EpisodeFormats.tsx` (a forced format is shown as
  such until the name carries it).

## Tests

`FileNameBuilderFixture` (organizer tests; 40 of them fail on a machine using NLS for three-letter
codes, which is locale and not code), `FileNameLanguagesFixture`, `LanguageFlagsNamingValidatorFixture`,
`CustomFormatCalculationServiceFixture`, `ManualCustomFormatCleanupServiceFixture`,
`SeriesApplyChangesFixture`.

## Against v5

Nothing in v5 for any of the four. v5's `FileNameBuilder.cs` moved by about 70 lines in the areas
this touches; the token handlers are the place to start. Language handling is the area most likely to
behave differently between Windows and Linux — verify on CI, not on a Windows machine.

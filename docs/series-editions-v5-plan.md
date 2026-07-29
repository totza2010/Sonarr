# Series editions: porting the 4.0 work to v5

Editions were built on the 4.0 line first, because that is what runs in production here. v5 has the
same feature but stops at an earlier point. This is what is missing and why, so the rest can be
picked up in one go once v5 becomes the main line.

## What an edition is

A second (third, ...) copy of the same TVDB series holding a different release of the same episodes:
black and white vs colour, dubbed vs original, an AI upscale, a different broadcast order. It is an
internal concept — TVDB is never asked how many editions a series has, and merging or deleting an
entry upstream cannot orphan one.

Two rules the whole design rests on:

- **The main edition has an empty edition name.** Existing series keep their identity, and every
  lookup that only knows an id or a title resolves to it. It is the edition automation uses.
- **Editions are the version axis only.** Resolution and quality belong to the quality profile and,
  when they need their own folder and Plex library, to a separate instance. `{edition-4K}` is wrong;
  `{edition-Black & White}` is right.

## Where each line stands

| | 4.0 (`feature/series-editions-v4`) | v5 (`feature/series-editions`) |
| --- | --- | --- |
| Schema, composite unique key | ✅ migration 218 | ✅ migration 233 |
| Edition folder and slug | ✅ | ✅ |
| Add edition UI, edition picker | ✅ | ✅ |
| Edition shown in lists | ✅ | ✅ |
| `FindByTvdbId` → main edition | ✅ | ✅ |
| **`FindByTitle` → main edition** | ✅ | ❌ |
| **`FindByTitleInexact` tie-break** | ✅ | ❌ |
| **`FindByTvRageId` / `FindByImdbId`** | ✅ | ❌ |
| **Edition name suggestions** | ✅ | ❌ |
| **Case-insensitive edition names** | ✅ | ❌ |
| **Edition name stored already cleaned** | ✅ | ❌ |
| **Manual import resolves edition by path** | ✅ | ❌ |

Everything in bold is the port.

## The port, in the order it should be done

### 1. Lookups that can now match more than one series

This is the part that matters. Release parsing matches by title before it falls back to an id, and
every edition shares the title, the TVDB id, the TVRage id and the IMDb id — metadata refresh copies
them onto every edition. So every lookup that assumed one match can now see several.

`SeriesRepository`:

- `FindByTitle` and `FindByTitle(cleanTitle, year)` go through `ReturnSingleSeriesOrThrow`, which
  throws. Nothing on the RSS or import path catches it, so **every release for a series with
  editions is rejected**, including releases meant for the main edition. Resolve to the main edition
  when the matches share a TVDB id, and keep throwing when they do not — separate series that happen
  to share a title are a real ambiguity, and the queue and manual import rely on the exception to ask
  the user which one they meant.
- `FindByTvRageId` and `FindByImdbId` use `SingleOrDefault`, which throws on the second edition.
  Same rule, via `ReturnMainEditionOrSingle`.
- `SeriesService.FindByTitleInexact` orders by position then title length. Editions tie on both, so
  the winner is whatever order the database returned — a release can be imported into the wrong
  edition with no error at all. Add a tie-break on the main edition.

`FindByPath` needs nothing: every edition has its own path.

Tests to bring across, they are the ones that pin the behaviour:

- editions of one series → main edition, no throw
- editions with no main edition → still returns something
- **separate series sharing a title → still throws** (this one guards the queue and manual import)

### 2. Edition names cannot differ by case

`Black & White` and `black & white` are one folder on Windows and two labels in Plex. Add
`SeriesEditions.SameEdition` and use it in `SeriesExistsValidator` and the bulk add path instead of
`==` / `Contains`.

### 3. Store the edition name already cleaned

The folder token runs through `CleanFileName`, so `4:3` becomes `4-3` on disk while the UI still
shows `4:3`. Clean the name in `AddSeriesService.SetPropertiesAndValidate` so what is stored is what
the folder carries and what Plex reads. `GetSeriesFolder` keeps cleaning as well — it is a no-op for
series added through the UI and still protects series edited straight through the API.

### 4. Manual import resolves the edition from the path

Title matching resolves to the main edition, which is right for a fresh download but wrong when
re-importing a library folder that already sits under an edition. Prefer the edition whose path is a
parent of the file. Applies to both the folder and the file path in `ManualImportService`.

### 5. Edition name suggestions

`seriesEditionSuggestions.ts` plus an `AUTO_COMPLETE` input, with `shouldRenderSuggestions` forced
true so the list opens on focus rather than after the first character. Pass it at the call site, not
in `AutoCompleteInput`: the branch field in Settings → General uses the same component and should
keep waiting for input.

The suggestions are deliberately **not** translated. They end up in the folder name, so translating
them would rename folders when the UI language changes.

## Differences to expect between the lines

The v5 port is not a cherry-pick — v4 and v5 have diverged.

| | 4.0 | v5 |
| --- | --- | --- |
| API project | `Sonarr.Api.V3` | `Sonarr.Api.V5` (V3 still there) |
| Frontend | Redux containers, `.js` | react-query hooks, `.tsx` |
| `AllSeriesTvdbIds()` | `List<int>` | `Dictionary<int, int>` |
| `IfDatabase` | `"sqlite"` / `"postgres"` | `ProcessorIdConstants.*` |
| Add payload | explicit fields | spread of the lookup result |

That last row is a trap in both lines, reached differently. Looking up a series that is already in
the library returns **that series**, not a fresh lookup result, so its id and path ride along into
the add. v5 spreads the whole object; v4 mutates the cached lookup item in `getNewSeries`. Either
way the id and path have to be cleared when adding an edition, or the path validator rejects it as a
duplicate of the main edition.

## Known gaps, not yet addressed on either line

- Only the main edition can be automated. Everything that resolves a series from a release ends at
  the main edition, by design — two editions of one series would otherwise compete for the same
  releases. Other editions are manual import only.
- Because of that, one instance cannot automate both an HD and a 4K line. Separate instances still
  handle the quality axis; editions handle the version axis within each.
- The unique key is `(TvdbId, EditionName)` and ignores the root folder, so one instance cannot hold
  the same edition in two root folders. Consolidating instances would need
  `(TvdbId, EditionName, RootFolderPath)`.
- Editions share their episode records, so the calendar and wanted lists would show every episode
  once per edition. In practice this does not surface: secondary editions are unmonitored, and the
  calendar defaults to `includeUnmonitored = false` while wanted defaults to `monitored = true`.

## Running the 4.0 line

`global.json` pins .NET SDK 6.0.4xx, and `Sonarr.Console.csproj` declares `<TargetFrameworks>`
(plural) with a single entry, which is enough to make `dotnet run` demand a framework.

Build, then run the executable directly — it is faster than `dotnet run` and avoids its argument
handling entirely:

```
dotnet build src/Sonarr.sln -c Debug
.\_output\net6.0\Sonarr.Console.exe -nobrowser -data=C:\sonarr-v4-data
```

`-c Debug` is not optional: `ConfigFileProvider.UiFolder` resolves to `../UI` only in debug builds,
which is where webpack writes. A release build looks for `_output/net6.0/UI` and serves nothing.

To rebuild on every change:

```
$env:SolutionDir="<repo>\src\"
dotnet watch --project src/NzbDrone.Console/Sonarr.Console.csproj --no-hot-reload run -c Debug -f net6.0 -- -nobrowser "-data=C:\sonarr-v4-data"
```

Each part earns its place:

- `$env:SolutionDir` — `Directory.Build.props` pulls in `$(SolutionDir)stylecop.json`. Building a
  project rather than the solution leaves it empty, StyleCop falls back to its defaults, and the
  build fails with hundreds of SA1200 errors in files nobody touched.
- `-c Debug` after `run`, not before — SDK 6's `dotnet watch` does not take `-c` itself.
- `-f net6.0` — because of the `<TargetFrameworks>` element above.
- `--no-hot-reload` — hot reload reports `No managed code changes to apply` and leaves the old code
  running, which is worse than not reloading. This forces a full restart. `--non-interactive` does
  not exist on SDK 6.
- quotes around `-data=` — `dotnet run` mangles the value otherwise and the data directory ends up
  as `C:`.

Sonarr locks its output assemblies, so it has to be stopped before a build.

The frontend has no unit tests; `yarn lint` and `yarn build` are the checks. `yarn stylelint` cannot
run here — an ESM-only dependency of this branch's stylelint fails to load under the installed Node,
before any CSS is read.

## The .NET tests do not run on this line

`dotnet test` aborts with `Test host process crashed : Stack overflow`, inside
`NUnit.Framework.Internal.Builders.NamespaceTreeBuilder.GetNamespaceSuite` during discovery. A stack
overflow cannot be caught, so the host dies and the transport error that follows is only a symptom.

It happens with the edition tests removed as well, so it is not caused by them. Discovery scans the
whole assembly regardless of `--filter`, which is why filtering down to an unrelated test does not
avoid it either.

This line pins NUnit 3.13.3 with NUnit3TestAdapter 3.17.0. The edition code here is therefore
verified by the build and by hand only, and the tests that ship with it have never been executed.
Bumping the adapter is the cheapest thing to try if that becomes a problem.

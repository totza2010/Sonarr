# What the media servers actually read

This fork writes three things into names that a media server has to understand: a part marker, a
version marker, and an edition folder. Each was chosen against what Plex, Jellyfin, Emby and Silo do,
and none of that is obvious from the outside — so it is written down here, with the reasoning, before
somebody changes one of them to something that reads better and quietly breaks a library.

Checked 2026-07-30, against each project's own documentation and, for Silo, its source.

| | `pt<n>` | `v<n>` | `{edition-Name}` folder |
| --- | --- | --- | --- |
| **Plex** | yes | yes, as duplicate versions | **yes, since the TV Show Editions release** (Plex Pass to add or edit) |
| **Jellyfin** | yes | yes, as duplicate versions | no, movies only |
| **Emby** | yes, but see below | yes, as duplicate versions | no, movies only |
| **Silo** | yes, `split_episode` | yes | yes, at the scanner |

## The part marker: why `pt`

An episode split across files only plays as one episode if the server recognises the marker, so this
one is not free to choose.

| | `cd` | `disc` | `disk` | `dvd` | `part` | `pt` |
| --- | --- | --- | --- | --- | --- | --- |
| Plex | yes | yes | yes | yes | yes | yes |
| Jellyfin | yes | yes | yes | yes | yes | yes |
| Emby | yes | yes | yes | yes | yes | yes |
| Silo | yes | yes | **no** | **no** | yes | yes |

Silo's is the narrow one:

```go
presentationPartRe = `(?i)(?:^|[.\-_\s])(cd|disc|part|pt)(?:\s*|[._-]?)(\d{1,2})(?:$|[.\-_\s])`
```

`pt` and `part` and `cd` and `disc` are the four all of them take. `pt` is the shortest and reads as
what it is. **Changing this to `dvd` or `disk` loses Silo.**

Splitting an episode is a legacy shape everywhere. Plex says outright that not every feature works on
split files and suggests merging them where that is an option, and Emby 4.8 went as far as reading
multi-part episodes as separate versions rather than parts. Where a single file is possible, it is
still the better answer; the marker is for where it is not.

## The version marker: why `v` needs nobody's permission

`pt` has to be recognised. `v` has to be *ignored*.

Two files that resolve to the same episode are gathered as versions of it by every one of these
servers, whatever the files are called — the viewer picks which to play. The only thing actually
required is that the two files not have the same name, since they sit in the same folder.

So `v1` and `v2` do their whole job by being different from each other, and the only real constraint
is the one that is easy to miss: **the marker must not collide with a part marker**. `v` appears in
none of the four part lists, which is the point of it.

This is why the version marker did not need a survey and the part marker did. Anything unique and
not in that table would work; `v` is short and numbers itself.

## The edition folder

Editions of a series are separate folders, each carrying `{edition-Name}`:

```
/TV/Dao Kiao Duen (2013) {tvdb-274360}
/TV/Dao Kiao Duen (2013) {tvdb-274360} {edition-Uncut}
```

That shape came from Plex, where it long meant movies only. Plex has since released the same thing
for TV shows: the edition is set at the show level, each gets its own listing with its own watch
state, and adding or editing edition information wants a Plex Pass. Their own examples are dubbed
versus original-language anime, and a show in two aspect ratios — which is what this is for.

Silo reads `{edition-...}` from a folder with full confidence and does not care what kind of library
it is; its scanner puts `EditionKey` into the grouping key while the owner may be an episode, so an
edition of a series is a shape it can already hold.

Jellyfin and Emby have no editions for TV. They see two folders and list two shows — which is the
same outcome the folders were chosen for.

Note the two heuristic layers in Silo that guess an edition from words like `Theatrical` or `IMAX` in
a name. They run through movie-shaped code that expects `Title (Year)` folders, and the `{edition-}`
tag matches before they are ever reached. They are not something to rely on.

## What this means for a naming format

- `{Multiple}` writes `pt1` or `v1`. Both want a separator in front — `- {Multiple}` or
  `.{Multiple}` — since every parser above looks for one.
- Renaming has to be on and `{Multiple}` has to be in the format before parts can be kept at all;
  the naming settings refuse to save a format that has lost it while episodes are relying on it.
- Editions need nothing in the episode format. The folder carries them.

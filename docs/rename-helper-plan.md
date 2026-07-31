# What the rename helper still needs

The rename helper is the part of this fork that lets a person correct what a file's name says about it:
per-file naming languages, per-file custom formats, and a per-series naming language. What is written
here is the work it is missing, not the work it does.

Nothing here is built.

## Setting the naming languages of many files at once

A season pack arrives as one folder of ten files, all of them the same Thai dub of an English series.
Every one of them needs the same correction - audio Thai and English, subtitles Thai - and today that
is ten trips through the same modal.

The manual import screen already has the machinery for exactly this. The row-level editors are
columns, and the bulk editors are a `Select...` menu at the bottom driven by a `SelectType` union in
[InteractiveImportModalContent.tsx](../frontend/src/InteractiveImport/Interactive/InteractiveImportModalContent.tsx):

```
'select' | 'series' | 'season' | 'episode' | 'releaseGroup' | 'quality' | 'language'
        | 'indexerFlags' | 'releaseType'
```

Both of the fork's own fields are columns and neither is in that union. Series, season, quality and
language can all be set for every selected row in one go; naming languages and manual custom formats
cannot, and they are the two most likely to be identical across a pack.

What it takes:

- add `namingLanguages` and `customFormats` to `SelectType` and to the menu
- the modals already exist. `SelectNamingLanguagesModalContent` and `SelectCustomFormatModalContent`
  are what the row-level editors open, and the bulk versions of the stock fields reuse their modals
  the same way.
- the apply handler writes the chosen value onto every selected row, which is what the stock bulk
  actions already do

One thing does have to be decided rather than copied. The row-level modal seeds itself from what
MediaInfo detected for *that file*, because correcting a detected list is the common case. A bulk edit
has no single detected list to start from. The choices are to start empty, or to start from what the
selected files agree on. Starting from the agreement is friendlier - a pack is usually uniform, so the
box arrives already filled in and the person only removes what is wrong - but it is a guess when the
files disagree, and the modal has to make that visible rather than quietly picking one file's answer.

Both fields want the same treatment at the same time. They are the same shape, they are set from the
same screen, and building one and not the other leaves the menu looking arbitrary.

## Requiring the token that does not drop English

`{MediaInfo AudioLanguages}` returns nothing when the only language left after filtering is English.
The rule is older than the filters, and the commit that added filters put it *after* filtering, so a
filter meaning "whichever of these are present" can reduce a two-language file to `EN` and have it
thrown away:

```
[EN, JA]  ->  filter TH+EN+ORIGINAL  ->  [EN]  ->  skipEnglishOnly  ->  ""
```

`{MediaInfo AudioLanguagesAll}` is the same token without that rule - it exists for no other reason.

This matters beyond the name itself. The language flags read the groups out of the file name by
position, so a silently dropped audio group makes the subtitle group first, and the flags label
subtitles as audio. The naming validator already refuses to show the flags unless a format writes the
languages; it should hold out for the `All` variant, since the ordinary one is a format that promises
to write them and then sometimes does not.

The subtitle side needs nothing: `{MediaInfo SubtitleLanguages}` and `{MediaInfo SubtitleLanguagesAll}`
call the same function with the same arguments, so the `All` there is a name for nothing.

## Keeping a language group from being read as a release group

`Parser`'s release-group pattern has an alternative for bracketed groups at the end of a name:

```
[-._ ]\[(?<releasegroup>[a-z0-9]+)\]$
```

A name ending in `[EN]` matches it, and nothing rejects two letters. `[TH+EN]` does not match, because
`+` is not in the character class - so the hazard is only ever a group with a single language in it.

The dash form of the same pattern already carries a negative lookbehind for `-ES`, `-EN` and `-CAT`,
so upstream has met this problem; the bracket form was simply never given the same guard.

A name ends with a language group more easily than it looks. `{Release Group}` on its own writes
`Sonarr` when a file has no group, but `DefaultValue` only supplies that default when the token has
neither a prefix nor a suffix - so `{.[Release Group]}`, which is how nearly everyone writes it,
returns empty instead and lets whatever came before it fall to the end of the name.

Two ways out, and the second is the one to document rather than build:

- guard the bracket alternative the way the dash alternative is guarded. It is upstream's pattern,
  and changing how release groups are parsed is a wide blast radius for a narrow problem.
- **put the language tokens where something non-empty always follows them.** `{.QUALITY.TITLE}` is
  always present and carries no brackets, so a format that places the languages before it can never
  end with `[EN]`. This is a documentation fix, not a code one.

## Lengths

Truncation exists - `{Series CleanTitleYear:45}`, positive to keep the head, negative to keep the tail,
cut at `N-3` with an ellipsis appended so the result is exactly `N`.

The trap is that the two ends of a file name do not measure the same way:

| token | limit | unit |
| --- | --- | --- |
| `{Episode Title}`, `{Episode CleanTitle}` | a built-in maximum, and `:N`, whichever is smaller | **bytes** |
| every `{Series ...}` token | `:N` only | **characters** |

A filename on ext4 may be 255 bytes. Thai is three bytes per character, so `{Series CleanTitleYear:60}`
is 60 bytes of Latin and 180 of Thai, while `{Episode CleanTitle:60}` is 60 bytes of either. The series
side is the one that needs a number chosen for the script the titles are actually in.

Against a real name, everything that is not a title costs 70-80 bytes once both language groups, a
part marker and a release group are counted, leaving about 175 for the two titles together.

## Where this fits

The editions and multiple-file features these fields sit beside are
[series-editions-v5-plan.md](series-editions-v5-plan.md) and [media-server-naming.md](media-server-naming.md).
Moving a series' identity, which shares a mapping table with none of this, is
[series-identity-migration-plan.md](series-identity-migration-plan.md).

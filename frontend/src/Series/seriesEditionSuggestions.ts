// Suggestions for naming an edition. These end up in the folder name and are what Plex shows as the
// edition label, so they are deliberately not translated: the folder has to stay the same whatever
// language the UI is in. The list is only a starting point, any name can be typed instead.
//
// An edition is a different version of the same episodes. Resolution and quality are not editions,
// they belong to the quality profile and, when they need separate folders, to a separate instance.
const seriesEditionSuggestions = [
  // Cut
  "Director's Cut",
  'Extended',
  'Uncut',
  'Uncensored',
  'Unrated',
  'Theatrical',
  'Original Broadcast',

  // Restoration
  'Remastered',
  'Bluray Remaster',
  'Restored',
  'AI Upscale',

  // Picture
  'Black & White',
  'Colorized',
  'Open Matte',
  'IMAX',
  '4:3',
  '16:9',

  // Audio and language
  'Dubbed',
  'Subbed',
  'Original Audio',
  'Commentary',

  // Put together from more than one source
  'Hybrid',

  // Episode order
  'Broadcast Order',
  'Production Order',
  'DVD Order',
  'Chronological Order',

  // Where this cut came from
  'Netflix',
  'Disney+',
  'Prime Video',
  'Max',
  'Hulu',
  'Apple TV+',
];

export default seriesEditionSuggestions;

import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

// The token name inside its braces, rather than the opening brace and the name. A token may carry a
// separator in front of it - {.Multiple} is how most people write it - so matching on '{multiple'
// missed those formats and switched the whole feature off for them without a word.
const MULTIPLE_TOKEN = /\{[^}]*multiple/i;

// Parts are told apart by their file names, so keeping more than one file for an episode needs
// renaming turned on and {Multiple} in the format that will be used. The server decides this per
// series and refuses an import that cannot work; this is the same question asked loosely enough to
// answer without one, so the buttons that would only lead to that refusal can be put away.
function createMultipleFilesEnabledSelector() {
  return createSelector(
    (state: AppState) => state.settings.naming,
    (naming) => {
      const { isPopulated, item } = naming;

      if (!isPopulated || !item.renameEpisodes) {
        return false;
      }

      return [
        item.standardEpisodeFormat,
        item.dailyEpisodeFormat,
        item.animeEpisodeFormat,
      ].some((format) => MULTIPLE_TOKEN.test(format ?? ''));
    }
  );
}

export default createMultipleFilesEnabledSelector;

import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

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
      ].some((format) => format?.toLowerCase().includes('{multiple'));
    }
  );
}

export default createMultipleFilesEnabledSelector;

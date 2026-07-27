import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import { EpisodeFile } from 'EpisodeFile/EpisodeFile';

// The extra parts and versions an episode owns, in part order. Episodes without any resolve to an empty
// array, so callers can render them the same way whether the feature is in use or not.
function createAdditionalEpisodeFilesSelector() {
  return createSelector(
    (
      _: AppState,
      { additionalEpisodeFileIds }: { additionalEpisodeFileIds?: number[] }
    ) => additionalEpisodeFileIds,
    (state: AppState) => state.episodeFiles,
    (additionalEpisodeFileIds, episodeFiles) => {
      if (!additionalEpisodeFileIds?.length) {
        return [];
      }

      return additionalEpisodeFileIds
        .map((id) => episodeFiles.items.find((file) => file.id === id))
        .filter((file): file is EpisodeFile => !!file)
        .sort((a, b) => (a.partNumber ?? 0) - (b.partNumber ?? 0));
    }
  );
}

export default createAdditionalEpisodeFilesSelector;

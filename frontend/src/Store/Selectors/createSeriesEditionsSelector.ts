import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import createAllSeriesSelector from './createAllSeriesSelector';

// Every copy of a series in the library, the main edition first and the editions after it.
function createSeriesEditionsSelector() {
  return createSelector(
    (_: AppState, { tvdbId }: { tvdbId: number }) => tvdbId,
    createAllSeriesSelector(),
    (tvdbId, series) => {
      return series
        .filter((s) => s.tvdbId === tvdbId)
        .sort((a, b) => a.editionName.localeCompare(b.editionName));
    }
  );
}

export default createSeriesEditionsSelector;

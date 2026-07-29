import { some } from 'lodash';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import createAllSeriesSelector from './createAllSeriesSelector';

// Editions of a series share its TVDB ID, the main edition is the one without an edition name.
function createExistingSeriesSelector() {
  return createSelector(
    (_: AppState, { tvdbId }: { tvdbId: number }) => tvdbId,
    createAllSeriesSelector(),
    (tvdbId, series) => {
      return some(series, (s) => s.tvdbId === tvdbId && !s.editionName);
    }
  );
}

export default createExistingSeriesSelector;

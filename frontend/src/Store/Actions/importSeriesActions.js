import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import getImportSeriesEdition, { isSameEdition } from 'AddSeries/ImportSeries/Import/getImportSeriesEdition';
import * as commandNames from 'Commands/commandNames';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getNewSeries from 'Utilities/Series/getNewSeries';
import * as seriesTypes from 'Utilities/Series/seriesTypes';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import { removeItem, set, updateItem } from './baseActions';
import { executeCommand } from './commandActions';
import createHandleActions from './Creators/createHandleActions';
import { fetchRootFolders } from './rootFolderActions';

//
// Variables

export const section = 'importSeries';
let concurrentLookups = 0;
let abortCurrentLookup = null;
const queue = [];

//
// State

export const defaultState = {
  isLookingUpSeries: false,
  isImporting: false,
  isImported: false,
  importError: null,
  items: []
};

//
// Actions Types

export const QUEUE_LOOKUP_SERIES = 'importSeries/queueLookupSeries';
export const START_LOOKUP_SERIES = 'importSeries/startLookupSeries';
export const CANCEL_LOOKUP_SERIES = 'importSeries/cancelLookupSeries';
export const LOOKUP_UNSEARCHED_SERIES = 'importSeries/lookupUnsearchedSeries';
export const CLEAR_IMPORT_SERIES = 'importSeries/clearImportSeries';
export const SET_IMPORT_SERIES_VALUE = 'importSeries/setImportSeriesValue';
export const IMPORT_SERIES = 'importSeries/importSeries';

//
// Action Creators

export const queueLookupSeries = createThunk(QUEUE_LOOKUP_SERIES);
export const startLookupSeries = createThunk(START_LOOKUP_SERIES);
export const importSeries = createThunk(IMPORT_SERIES);
export const lookupUnsearchedSeries = createThunk(LOOKUP_UNSEARCHED_SERIES);
export const clearImportSeries = createAction(CLEAR_IMPORT_SERIES);
export const cancelLookupSeries = createAction(CANCEL_LOOKUP_SERIES);

export const setImportSeriesValue = createAction(SET_IMPORT_SERIES_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Action Handlers

export const actionHandlers = handleThunks({

  [QUEUE_LOOKUP_SERIES]: function(getState, payload, dispatch) {
    const {
      name,
      path,
      relativePath,
      folderEditionName,
      term,
      topOfQueue = false
    } = payload;

    const state = getState().importSeries;
    const item = _.find(state.items, { id: name }) || {
      id: name,
      term,
      path,
      relativePath,
      folderEditionName,
      isFetching: false,
      isPopulated: false,
      error: null
    };

    dispatch(updateItem({
      section,
      ...item,
      term,
      isQueued: true,
      items: []
    }));

    const itemIndex = queue.indexOf(item.id);

    if (itemIndex >= 0) {
      queue.splice(itemIndex, 1);
    }

    if (topOfQueue) {
      queue.unshift(item.id);
    } else {
      queue.push(item.id);
    }

    if (term && term.length > 2) {
      dispatch(startLookupSeries({ start: true }));
    }
  },

  [START_LOOKUP_SERIES]: function(getState, payload, dispatch) {
    if (concurrentLookups >= 1) {
      return;
    }

    const state = getState().importSeries;

    const {
      isLookingUpSeries,
      items
    } = state;

    const queueId = queue[0];

    if (payload.start && !isLookingUpSeries) {
      dispatch(set({ section, isLookingUpSeries: true }));
    } else if (!isLookingUpSeries) {
      return;
    } else if (!queueId) {
      dispatch(set({ section, isLookingUpSeries: false }));
      return;
    }

    concurrentLookups++;
    queue.splice(0, 1);

    const queued = items.find((i) => i.id === queueId);

    dispatch(updateItem({
      section,
      id: queued.id,
      isFetching: true
    }));

    const { request, abortRequest } = createAjaxRequest({
      url: '/series/lookup',
      data: {
        term: queued.term
      }
    });

    abortCurrentLookup = abortRequest;

    request.done((data) => {
      const selectedSeries = queued.selectedSeries || data[0];

      const itemProps = {
        section,
        id: queued.id,
        isFetching: false,
        isPopulated: true,
        error: null,
        items: data,
        isQueued: false,
        selectedSeries,
        updateOnly: true
      };

      if (selectedSeries && selectedSeries.seriesType !== seriesTypes.STANDARD) {
        itemProps.seriesType = selectedSeries.seriesType;
      }

      dispatch(updateItem(itemProps));
    });

    request.fail((xhr) => {
      dispatch(updateItem({
        section,
        id: queued.id,
        isFetching: false,
        isPopulated: false,
        error: xhr,
        isQueued: false,
        updateOnly: true
      }));
    });

    request.always(() => {
      concurrentLookups--;

      dispatch(startLookupSeries());
    });
  },

  [LOOKUP_UNSEARCHED_SERIES]: function(getState, payload, dispatch) {
    const state = getState().importSeries;

    if (state.isLookingUpSeries) {
      return;
    }

    state.items.forEach((item) => {
      const id = item.id;

      if (
        !item.isPopulated &&
        !queue.includes(id)
      ) {
        queue.push(item.id);
      }
    });

    if (queue.length) {
      dispatch(startLookupSeries({ start: true }));
    }
  },

  [IMPORT_SERIES]: function(getState, payload, dispatch) {
    dispatch(set({ section, isImporting: true }));

    const ids = payload.ids;
    const items = getState().importSeries.items;
    const allSeries = getState().series.items;
    const addedIds = [];

    // Folders pointed at an edition that is already in the library. Nothing is added for these: the
    // series keeps its id, and with it its history and settings, and only where it lives changes.
    const replacements = [];

    const allNewSeries = ids.reduce((acc, id) => {
      const item = items.find((i) => i.id === id);

      // A row that has already been imported can still be ticked: the list is rebuilt after an import
      // and the tick outlives the row it was on. There is nothing left to import for it.
      if (!item) {
        return acc;
      }

      const selectedSeries = item.selectedSeries;
      const edition = getImportSeriesEdition(item, allSeries);

      if (!selectedSeries || !edition.isImportable) {
        return acc;
      }

      if (edition.replaceSeries) {
        // One folder per edition; a second row asking for the same one is left where it is.
        if (!replacements.some((r) => r.series.id === edition.replaceSeries.id)) {
          replacements.push({ id, series: edition.replaceSeries, path: item.path });
        }

        return acc;
      }

      // Make sure the same edition of the same series hasn't been added yet.
      if (!acc.some((a) => a.tvdbId === selectedSeries.tvdbId && isSameEdition(a.editionName, edition.editionName))) {
        const newSeries = getNewSeries(_.cloneDeep(selectedSeries), {
          ...item,
          editionName: edition.editionName
        });

        newSeries.path = item.path;

        addedIds.push(id);
        acc.push(newSeries);
      }

      return acc;
    }, []);

    const requests = [];

    if (allNewSeries.length) {
      requests.push(createAjaxRequest({
        url: '/series/import',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(allNewSeries)
      }).request);
    }

    // The same request the path field of the edit screen makes, without moving anything: the files are
    // already where the series is being told to look.
    replacements.forEach(({ series, path }) => {
      requests.push(createAjaxRequest({
        url: `/series/${series.id}`,
        method: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify({ ...series, path, rootFolderPath: undefined })
      }).request.then(() => [{ ...series, path }]));
    });

    // Nothing left to send - every ticked row was one of the above. Said, rather than left spinning.
    if (!requests.length) {
      dispatch(set({ section, isImporting: false }));
      return;
    }

    const promise = Promise.all(requests).then((results) => _.flatten(results));

    promise.then((data) => {
      dispatch(batchActions([
        set({
          section,
          isImporting: false,
          isImported: true,
          importError: null
        }),

        ...data.map((series) => updateItem({ section: 'series', ...series })),

        ...addedIds.map((id) => removeItem({ section, id })),

        ...replacements.map(({ id }) => removeItem({ section, id }))
      ]));

      // What the series holds is whatever is in the folder it now points at, which nothing knows
      // until it is looked at.
      if (replacements.length) {
        dispatch(executeCommand({
          name: commandNames.REFRESH_SERIES,
          seriesIds: replacements.map((r) => r.series.id)
        }));
      }

      dispatch(fetchRootFolders());
    }, (xhr) => {
      dispatch(batchActions([
        set({
          section,
          isImporting: false,
          isImported: true,
          importError: xhr
        }),

        ...addedIds.map((id) => updateItem({
          section,
          id
        }))
      ]));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CANCEL_LOOKUP_SERIES]: function(state) {
    queue.splice(0, queue.length);

    const items = state.items.map((item) => {
      if (item.isQueued) {
        return {
          ...item,
          isQueued: false
        };
      }

      return item;
    });

    return Object.assign({}, state, {
      isLookingUpSeries: false,
      items
    });
  },

  [CLEAR_IMPORT_SERIES]: function(state) {
    if (abortCurrentLookup) {
      abortCurrentLookup();

      abortCurrentLookup = null;
    }

    queue.splice(0, queue.length);

    return Object.assign({}, state, defaultState);
  },

  [SET_IMPORT_SERIES_VALUE]: function(state, { payload }) {
    const newState = getSectionState(state, section);
    const items = newState.items;
    const index = items.findIndex((item) => item.id === payload.id);

    newState.items = [...items];

    if (index >= 0) {
      const item = items[index];

      newState.items.splice(index, 1, { ...item, ...payload });
    } else {
      newState.items.push({ ...payload });
    }

    return updateSectionState(state, section, newState);
  }

}, defaultState, section);

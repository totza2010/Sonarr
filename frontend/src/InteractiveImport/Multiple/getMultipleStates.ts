import InteractiveImport from 'InteractiveImport/InteractiveImport';

// Where a row stands as a part or version:
//   saved     - the file is in the library and its marker is stored with it
//   pending   - the marker is only on the row; it is stored when the row is imported
//   undecided - another row is for the same episodes and this one says nothing about itself, so one
//               of them replaces the other unless a part or version is set
export type MultipleState = 'saved' | 'pending' | 'undecided';

export interface MultipleStates {
  byId: Record<number, MultipleState>;
  // The episodes a row is for, as one value: rows that share it are files of the same episode.
  groupById: Record<number, string>;
  saved: number;
  pending: number;
  undecided: number;
  total: number;
}

function getState(isMarked: boolean, isInLibrary: boolean): MultipleState {
  if (!isMarked) {
    return 'undecided';
  }

  return isInLibrary ? 'saved' : 'pending';
}

function getMultipleStates(items: InteractiveImport[]): MultipleStates {
  const sharing = new Map<string, number>();

  const keys = items.map((item) => {
    if (!item.episodes?.length) {
      return null;
    }

    const key = item.episodes
      .map((episode) => episode.id)
      .sort((a, b) => a - b)
      .join(',');

    sharing.set(key, (sharing.get(key) ?? 0) + 1);

    return key;
  });

  const result: MultipleStates = {
    byId: {},
    groupById: {},
    saved: 0,
    pending: 0,
    undecided: 0,
    total: 0,
  };

  items.forEach((item, index) => {
    const key = keys[index];
    const isMarked = !!item.multipleType && item.multipleType !== 'none';
    const isShared = !!key && (sharing.get(key) ?? 0) > 1;

    if (!isMarked && !isShared) {
      return;
    }

    const state: MultipleState = getState(isMarked, !!item.episodeFileId);

    result.byId[item.id] = state;
    result.groupById[item.id] = key ?? `file-${item.id}`;
    result[state]++;
    result.total++;
  });

  return result;
}

export default getMultipleStates;

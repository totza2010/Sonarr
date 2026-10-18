import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import type Episode from 'Episode/Episode';
import mapInOrder, {
  DEFAULT_SETTING,
  MapResult,
  MapSetting,
} from './mapInOrder';
import {
  buildLineRows,
  closeGap,
  makePartOf,
  OrderState,
  placeAt,
  removeFiles,
  skipEpisode,
} from './mapLines';

// Two seasons of three, ids 11-13 and 21-23, and one special, id 1.
function episode(seasonNumber: number, episodeNumber: number) {
  return {
    id: seasonNumber * 10 + episodeNumber,
    seasonNumber,
    episodeNumber,
  } as Episode;
}

const regular = [
  episode(1, 1),
  episode(1, 2),
  episode(1, 3),
  episode(2, 1),
  episode(2, 2),
  episode(2, 3),
];

const specials = [episode(0, 1)];

function state(settings: Partial<MapSetting>[]): OrderState {
  return {
    order: settings.map((_, i) => i + 1),
    settings: Object.fromEntries(
      settings.map((s, i) => [i + 1, { ...DEFAULT_SETTING, ...s }])
    ),
  };
}

function results(s: OrderState, start = 0) {
  return Object.fromEntries(
    mapInOrder(
      s.order.map((id) => ({ id, setting: s.settings[id] ?? DEFAULT_SETTING })),
      regular,
      start,
      specials
    ).map((r) => [r.id, r])
  ) as Record<number, MapResult>;
}

// Where each file ends up, as S1E1 and the like, in file id order.
function placed(s: OrderState) {
  const r = results(s);

  return s.order
    .slice()
    .sort((a, b) => a - b)
    .map((id) =>
      r[id].episodes
        .map((e) => `S${e.seasonNumber}E${e.episodeNumber}`)
        .join('+')
    );
}

describe('buildLineRows', () => {
  test('gives every episode of the order a line, with its file beside it', () => {
    const s = state([{}, {}, {}]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(rows, [
      { type: 'slot', episodeIndex: 0, fileIds: [1], isGap: false },
      { type: 'slot', episodeIndex: 1, fileIds: [2], isGap: false },
      { type: 'slot', episodeIndex: 2, fileIds: [3], isGap: false },
    ]);
  });

  test('stacks the parts of an episode on its line', () => {
    const s = state([{}, { kind: 'part' }, {}]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(rows[0], {
      type: 'slot',
      episodeIndex: 0,
      fileIds: [1, 2],
      isGap: false,
    });
  });

  test('shows an episode with no file as a gap', () => {
    const s = state([{}, { skipBefore: 1 }]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(rows[1], {
      type: 'slot',
      episodeIndex: 1,
      fileIds: [],
      isGap: true,
    });
  });

  test('leaves the episodes a file runs across as lines of their own, not gaps', () => {
    const s = state([{ kind: 'double', episodeCount: 3 }, {}]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(
      rows.map((r) => r.type === 'slot' && [r.fileIds, r.isGap]),
      [
        [[1], false],
        [[], false],
        [[], false],
        [[2], false],
      ]
    );
  });

  test('puts specials and files that need a decision under headings at the end', () => {
    const s = state([
      { kind: 'special', specialEpisodeId: 1 },
      {},
      { kind: 'part' },
      { kind: 'special' },
    ]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(rows.slice(1), [
      { type: 'section', section: 'specials' },
      { type: 'file', fileId: 1 },
      { type: 'section', section: 'problems' },
      { type: 'file', fileId: 4 },
    ]);
  });

  test('starts from the first episode of the order', () => {
    const s = state([{}]);
    const rows = buildLineRows(s.order, results(s, 2), regular, 2);

    assert.deepEqual(rows, [
      { type: 'slot', episodeIndex: 2, fileIds: [1], isGap: false },
    ]);
  });
});

describe('buildLineRows with skipped files', () => {
  test('lists a skipped file under its own heading', () => {
    const s = state([{}, { kind: 'skip' }, {}]);
    const rows = buildLineRows(s.order, results(s), regular, 0);

    assert.deepEqual(rows.slice(2), [
      { type: 'section', section: 'skipped' },
      { type: 'file', fileId: 2 },
    ]);
  });
});

describe('skipEpisode', () => {
  test('moves the file on an episode to the next one, and the rest follow', () => {
    const before = state([{}, {}, {}]);
    const s = skipEpisode(before, 12, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S1E3', 'S2E1']);
  });

  test('moves a pin on by one episode', () => {
    const before = state([{}, { pinEpisodeId: 21 }]);
    const s = skipEpisode(before, 21, results(before), regular);

    assert.equal(s.settings[2].pinEpisodeId, 22);
  });

  test('does nothing for an episode no file starts on', () => {
    const before = state([{ kind: 'double' }]);

    assert.deepEqual(skipEpisode(before, 12, results(before), regular), before);
  });
});

describe('removeFiles', () => {
  test('hands the place of a removed file to the first of its parts', () => {
    const s = removeFiles(
      state([{}, { pinEpisodeId: 21 }, { kind: 'part' }, {}]),
      [2]
    );

    assert.deepEqual(s.order, [1, 3, 4]);
    assert.equal(s.settings[3].kind, 'episode');
    assert.equal(s.settings[3].pinEpisodeId, 21);
  });
});

describe('makePartOf', () => {
  test('makes a file another part of the file it is dropped on', () => {
    const s = makePartOf(state([{}, {}, {}, {}]), 4, 2);

    assert.deepEqual(s.order, [1, 2, 4, 3]);
    assert.deepEqual(placed(s), ['S1E1', 'S1E2', 'S1E3', 'S1E2']);
    assert.equal(results(s)[4].multipleNumber, 2);
  });

  test('puts it at the end of a set that already has parts', () => {
    const s = makePartOf(state([{}, { kind: 'part' }, {}]), 3, 1);

    assert.deepEqual(s.order, [1, 2, 3]);
    assert.equal(results(s)[3].multipleNumber, 3);
  });

  test('joins a set of versions as a version', () => {
    const s = makePartOf(state([{}, { kind: 'version' }, {}]), 3, 2);

    assert.equal(s.settings[3].kind, 'version');
  });

  test('swaps the order of two parts when the first is dropped on the second', () => {
    const s = makePartOf(state([{}, { kind: 'part' }]), 1, 2);

    assert.deepEqual(s.order, [2, 1]);
    assert.equal(s.settings[2].kind, 'episode');
    assert.deepEqual(placed(s), ['S1E1', 'S1E1']);
    assert.equal(results(s)[2].multipleNumber, 1);
  });
});

describe('placeAt', () => {
  test('pins a file to the episode it is dropped on, and the files after it follow', () => {
    const before = state([{}, {}, {}]);
    const s = placeAt(before, [2], 21, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S2E1', 'S2E2']);
  });

  test('moves a file to its place in the order', () => {
    const before = state([{}, {}, {}]);
    const s = placeAt(before, [3], 11, results(before), regular);

    assert.deepEqual(s.order, [3, 1, 2]);
    assert.deepEqual(placed(s), ['S1E2', 'S1E3', 'S1E1']);
  });

  test('places several files together from that episode on', () => {
    const before = state([{}, {}, {}, {}]);
    const s = placeAt(before, [3, 4], 21, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S1E2', 'S2E1', 'S2E2']);
  });

  test('turns a part dropped on an episode back into a file of its own', () => {
    const before = state([{}, { kind: 'part' }]);
    const s = placeAt(before, [2], 12, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S1E2']);
  });
});

describe('closeGap', () => {
  test('closes a gap left by a skip', () => {
    const before = state([{}, { skipBefore: 2 }]);
    const s = closeGap(before, 12, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S1E3']);
  });

  test('closes a gap left by a pin', () => {
    const before = state([{}, { pinEpisodeId: 13 }]);
    const s = closeGap(before, 12, results(before), regular);

    assert.deepEqual(placed(s), ['S1E1', 'S1E2']);
  });

  test('leaves a pin alone when the gap was not its doing', () => {
    const before = state([{}, { skipBefore: 1 }, { pinEpisodeId: 22 }]);
    const s = closeGap(before, 12, results(before), regular);

    assert.equal(s.settings[3].pinEpisodeId, 22);
  });
});

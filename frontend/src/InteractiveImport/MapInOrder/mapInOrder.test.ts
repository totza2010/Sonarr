import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import type Episode from 'Episode/Episode';
import mapInOrder, {
  DEFAULT_SETTING,
  findShifts,
  MapSetting,
  naturalCompare,
  readNameNumber,
} from './mapInOrder';

// Two seasons of three, ids 11-13 and 21-23, and two specials, ids 1-2. Only the fields the mapping
// reads are filled in.
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

const specials = [episode(0, 1), episode(0, 2)];

function map(settings: Partial<MapSetting>[], start = 0) {
  return mapInOrder(
    settings.map((setting, index) => ({
      id: index + 1,
      setting: { ...DEFAULT_SETTING, ...setting },
    })),
    regular,
    start,
    specials
  );
}

function codes(result: { episodes: Episode[] }) {
  return result.episodes
    .map((e) => `S${e.seasonNumber}E${e.episodeNumber}`)
    .join('+');
}

describe('mapInOrder', () => {
  test('gives each file the next episode, carrying on into the next season', () => {
    const results = map([{}, {}, {}, {}]);

    assert.deepEqual(results.map(codes), ['S1E1', 'S1E2', 'S1E3', 'S2E1']);
    assert.ok(results.every((r) => r.multipleType === 'none' && !r.error));
  });

  test('starts where it is told to', () => {
    assert.deepEqual(map([{}, {}], 2).map(codes), ['S1E3', 'S2E1']);
  });

  test('numbers a set of parts from the file it follows', () => {
    const results = map([{}, { kind: 'part' }, { kind: 'part' }, {}]);

    assert.deepEqual(results.map(codes), ['S1E1', 'S1E1', 'S1E1', 'S1E2']);
    assert.deepEqual(
      results.map((r) => [r.multipleType, r.multipleNumber]),
      [
        ['part', 1],
        ['part', 2],
        ['part', 3],
        ['none', 0],
      ]
    );
  });

  test('numbers versions the same way', () => {
    const results = map([{}, { kind: 'version' }]);

    assert.deepEqual(
      results.map((r) => [r.multipleType, r.multipleNumber]),
      [
        ['version', 1],
        ['version', 2],
      ]
    );
  });

  test('leaves a file that has no company as a whole episode', () => {
    const results = map([{}, {}]);

    assert.deepEqual(
      results.map((r) => r.multipleNumber),
      [0, 0]
    );
  });

  test('refuses parts and versions mixed in one set', () => {
    const results = map([{}, { kind: 'part' }, { kind: 'version' }]);

    assert.equal(results[2].error, 'mixedChain');
  });

  test('refuses a part with nothing above it', () => {
    assert.equal(map([{ kind: 'part' }])[0].error, 'noPrevious');
  });

  test('passes over episodes that have no file, and says which', () => {
    const results = map([{}, { skipBefore: 2 }, {}]);

    assert.deepEqual(results.map(codes), ['S1E1', 'S2E1', 'S2E2']);
    assert.deepEqual(
      results[1].skipped.map((e) => e.id),
      [12, 13]
    );
  });

  test('gives a file that runs across two episodes both of them', () => {
    const results = map([{ kind: 'double' }, {}]);

    assert.deepEqual(results.map(codes), ['S1E1+S1E2', 'S1E3']);
  });

  test('gives a part of a double file both episodes as well', () => {
    const results = map([{ kind: 'double' }, { kind: 'part' }]);

    assert.equal(codes(results[1]), 'S1E1+S1E2');
    assert.equal(results[1].multipleNumber, 2);
  });

  test('places a special without moving the files after it', () => {
    const results = map([{}, { kind: 'special', specialEpisodeId: 2 }, {}]);

    assert.deepEqual(results.map(codes), ['S1E1', 'S0E2', 'S1E2']);
  });

  test('asks for a special when none was chosen', () => {
    assert.equal(map([{ kind: 'special' }])[0].error, 'noSpecial');
  });

  test('carries on counting from a pinned episode', () => {
    const results = map([{}, { pinEpisodeId: 22 }, {}]);

    assert.deepEqual(results.map(codes), ['S1E1', 'S2E2', 'S2E3']);
  });

  test('flags a file placed on an episode another file already took', () => {
    const results = map([{}, {}, { pinEpisodeId: 11 }]);

    assert.equal(results[2].duplicateOf, 1);
  });

  test('does not count a part as a second file for its episode', () => {
    const results = map([{}, { kind: 'part' }]);

    assert.equal(results[1].duplicateOf, undefined);
  });

  test('refuses a pin to an episode the series does not have', () => {
    assert.equal(map([{ pinEpisodeId: -1 }])[0].error, 'pinNotFound');
  });

  test('refuses files past the last episode', () => {
    const results = map([{}, {}], 5);

    assert.equal(results[0].error, undefined);
    assert.equal(results[1].error, 'pastEnd');
  });
});

describe('naturalCompare', () => {
  test('reads numbers as numbers', () => {
    assert.ok(naturalCompare('EP.99', 'EP.100') < 0);
  });

  test('orders Thai names by their number', () => {
    const names = [
      'อวตาร ปี 1 EP.07 ตอนที่ 7.mkv',
      'อวตาร ปี 1 EP.05 คัมภีร์น้ำ.mkv',
      'อวตาร ปี 1 EP.06 ตอนที่ 6.mkv',
    ];

    assert.deepEqual([...names].sort(naturalCompare), [
      'อวตาร ปี 1 EP.05 คัมภีร์น้ำ.mkv',
      'อวตาร ปี 1 EP.06 ตอนที่ 6.mkv',
      'อวตาร ปี 1 EP.07 ตอนที่ 7.mkv',
    ]);
  });

  test('ignores case', () => {
    assert.ok(naturalCompare('ep.2', 'EP.10') < 0);
  });

  test('puts a shorter name that the longer one begins with first', () => {
    assert.ok(naturalCompare('Show', 'Show 2') < 0);
  });
});

describe('readNameNumber', () => {
  const cases: [string, number | undefined][] = [
    ['เดอะซีรีย์ปี 4/EP.145 คดีตามหา.mp4', 145],
    ['อวตาร ปี 1 EP.20-21 ศึก.mkv', 20],
    ['โคนัน ตอนที่ 88.mp4', 88],
    ['Show Episode 12.mkv', 12],
    ['Show.S01E05.WEBDL-1080p.x265.mkv', undefined],
    ['Show - Special.mkv', undefined],
  ];

  cases.forEach(([name, expected]) => {
    test(`reads ${expected ?? 'nothing'} from ${name}`, () => {
      assert.equal(readNameNumber(name), expected);
    });
  });
});

describe('findShifts', () => {
  function shifts(names: string[], settings: Partial<MapSetting>[]) {
    const results = map(settings);

    return findShifts(
      names.map((name, index) => ({ id: index + 1, name })),
      results.reduce<Record<number, (typeof results)[number]>>((acc, r) => {
        acc[r.id] = r;
        return acc;
      }, {}),
      regular
    );
  }

  test('finds nothing while the names keep a steady distance from the order', () => {
    const found = shifts(['EP.11', 'EP.12', 'EP.13'], [{}, {}, {}]);

    assert.deepEqual(found.shiftRows, []);
  });

  test('points at the file after one that is missing', () => {
    const found = shifts(['EP.1', 'EP.2', 'EP.4'], [{}, {}, {}]);

    assert.deepEqual(found.shiftRows, [2]);
    assert.equal(found.byId[3].shift, 1);
  });

  test('leaves the second part of an episode out of the count', () => {
    const found = shifts(['EP.1', 'EP.1', 'EP.2'], [{}, { kind: 'part' }, {}]);

    assert.deepEqual(found.shiftRows, []);
  });

  test('leaves a special out of the count', () => {
    const found = shifts(
      ['EP.1', 'EP.50', 'EP.2'],
      [{}, { kind: 'special', specialEpisodeId: 1 }, {}]
    );

    assert.deepEqual(found.shiftRows, []);
  });

  test('skips files that carry no number', () => {
    const found = shifts(['EP.1', 'Bonus', 'EP.3'], [{}, {}, {}]);

    assert.deepEqual(found.shiftRows, []);
    assert.equal(found.byId[2].number, undefined);
  });
});

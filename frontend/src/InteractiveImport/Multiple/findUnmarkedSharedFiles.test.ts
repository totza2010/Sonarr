import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import type Episode from 'Episode/Episode';
import type MultipleType from 'InteractiveImport/MultipleType';
import findUnmarkedSharedFiles from './findUnmarkedSharedFiles';

function file(
  id: number,
  episodeIds: number[],
  multipleType: MultipleType = 'none',
  episodeFileId?: number
) {
  return {
    id,
    episodes: episodeIds.map((e) => ({ id: e } as Episode)),
    multipleType,
    episodeFileId,
  };
}

function find(files: ReturnType<typeof file>[], selected?: number[]) {
  return findUnmarkedSharedFiles(
    files,
    selected ?? files.map((f) => f.id)
  ).sort((a, b) => a - b);
}

describe('findUnmarkedSharedFiles', () => {
  test('finds two new files for one episode with nothing said', () => {
    assert.deepEqual(
      find([file(1, [10]), file(2, [10]), file(3, [11])]),
      [1, 2]
    );
  });

  test('leaves a set of parts alone', () => {
    assert.deepEqual(find([file(1, [10], 'part'), file(2, [10], 'part')]), []);
  });

  test('finds the one file of a set that says nothing', () => {
    assert.deepEqual(find([file(1, [10], 'part'), file(2, [10])]), [2]);
  });

  test('finds files sharing one episode of a double', () => {
    assert.deepEqual(find([file(1, [10, 11]), file(2, [11])]), [1, 2]);
  });

  test('leaves an upgrade of a file already in the library alone', () => {
    assert.deepEqual(find([file(1, [10], 'none', 500), file(2, [10])]), []);
  });

  test('counts only the files that are ticked', () => {
    assert.deepEqual(find([file(1, [10]), file(2, [10])], [1]), []);
  });

  test('leaves a file with no episode yet alone', () => {
    assert.deepEqual(find([file(1, []), file(2, [])]), []);
  });
});

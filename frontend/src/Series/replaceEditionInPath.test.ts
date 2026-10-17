import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import replaceEditionInPath from './replaceEditionInPath';

describe('replaceEditionInPath', () => {
  test('renames the edition in the folder', () => {
    assert.equal(
      replaceEditionInPath(
        '/tv/Show (2020) {tvdb-1} {edition-Remastered}',
        'Remastered',
        "Director's Cut"
      ),
      "/tv/Show (2020) {tvdb-1} {edition-Director's Cut}"
    );
  });

  test('matches the old name whatever its case', () => {
    assert.equal(
      replaceEditionInPath(
        '/tv/Show {edition-REMASTERED}',
        'remastered',
        'Uncut'
      ),
      '/tv/Show {edition-Uncut}'
    );
  });

  test('leaves a folder with no edition token alone', () => {
    assert.equal(
      replaceEditionInPath('/tv/Show Remastered', 'Remastered', 'Uncut'),
      '/tv/Show Remastered'
    );
  });

  test('leaves a token naming another edition alone', () => {
    assert.equal(
      replaceEditionInPath('/tv/Show {edition-Colour}', 'Remastered', 'Uncut'),
      '/tv/Show {edition-Colour}'
    );
  });

  test('leaves the path alone while the new name is empty', () => {
    assert.equal(
      replaceEditionInPath('/tv/Show {edition-Remastered}', 'Remastered', '  '),
      '/tv/Show {edition-Remastered}'
    );
  });

  test('renames only the last token, the one the folder was built with', () => {
    assert.equal(
      replaceEditionInPath(
        '/tv/{edition-Remastered}/Show {edition-Remastered}',
        'Remastered',
        'Uncut'
      ),
      '/tv/{edition-Remastered}/Show {edition-Uncut}'
    );
  });

  test('works on Windows paths', () => {
    assert.equal(
      replaceEditionInPath(
        'C:TVShow {edition-Remastered}',
        'Remastered',
        'Uncut'
      ),
      'C:TVShow {edition-Uncut}'
    );
  });
});

// Types only, so the file runs on its own under the tests without the app's import paths.
import type Episode from 'Episode/Episode';
import type MultipleType from 'InteractiveImport/MultipleType';

// What a file is, as far as the order goes:
//   episode - takes the next episode
//   double  - takes the next two, a file that runs across both
//   part    - is another piece of the file above it, so takes nothing new
//   version - is the file above it told differently, so takes nothing new
//   special - is a special picked by hand; specials are not in the order, so it takes nothing either
export type MapKind = 'episode' | 'double' | 'part' | 'version' | 'special';

export interface MapSetting {
  kind: MapKind;
  // Episodes with no file, passed over before this one is placed.
  skipBefore: number;
  // Put this file at this episode, and carry on counting from there.
  pinEpisodeId?: number;
  specialEpisodeId?: number;
}

export type MapError =
  | 'noPrevious'
  | 'pastEnd'
  | 'noSpecial'
  | 'mixedChain'
  | 'pinNotFound';

export interface MapResult {
  id: number;
  episodes: Episode[];
  multipleType: MultipleType;
  multipleNumber: number;
  // The episodes passed over just before this file, so the screen can say which ones have no file.
  skipped: Episode[];
  error?: MapError;
  // Another file already took one of these episodes, and neither is a part or version of the other.
  duplicateOf?: number;
}

export const DEFAULT_SETTING: MapSetting = { kind: 'episode', skipBefore: 0 };

/**
 * Hands the files out to the episodes in the order given. Every file is placed from the one before
 * it, so changing one row moves everything after it - which is the point: mark a file as a part, or
 * say an episode is missing, and the rest of a long list follows.
 *
 * `regular` is every episode outside season 0, in season and episode order; `start` is where in it
 * the first file goes. Nothing here reads a file name.
 */
export default function mapInOrder(
  rows: { id: number; setting: MapSetting }[],
  regular: Episode[],
  start: number,
  specials: Episode[]
): MapResult[] {
  const results: MapResult[] = [];
  const takenBy = new Map<number, number>();

  let cursor = Math.max(0, start);
  let head = undefined as MapResult | undefined;
  let chainLength = 0;
  let chainType = undefined as MultipleType | undefined;

  rows.forEach(({ id, setting }) => {
    const { kind } = setting;

    if (kind === 'part' || kind === 'version') {
      const result: MapResult = {
        id,
        episodes: [],
        multipleType: kind,
        multipleNumber: 0,
        skipped: [],
      };

      if (!head || !head.episodes.length) {
        result.error = 'noPrevious';
      } else if (chainType && chainType !== kind) {
        result.error = 'mixedChain';
      } else {
        // The file the set started from becomes number one of it only now that it has company: on
        // its own it is the whole episode.
        if (chainLength === 1) {
          head.multipleType = kind;
          head.multipleNumber = 1;
          chainType = kind;
        }

        chainLength++;
        result.episodes = head.episodes;
        result.multipleNumber = chainLength;
      }

      results.push(result);

      return;
    }

    const result: MapResult = {
      id,
      episodes: [],
      multipleType: 'none',
      multipleNumber: 0,
      skipped: [],
    };

    if (kind === 'special') {
      const special = specials.find((e) => e.id === setting.specialEpisodeId);

      if (special) {
        result.episodes = [special];
      } else {
        result.error = 'noSpecial';
      }
    } else {
      if (setting.pinEpisodeId != null) {
        const pinned = regular.findIndex((e) => e.id === setting.pinEpisodeId);

        if (pinned < 0) {
          result.error = 'pinNotFound';
        } else {
          cursor = pinned;
        }
      } else if (setting.skipBefore > 0) {
        result.skipped = regular.slice(cursor, cursor + setting.skipBefore);
        cursor += setting.skipBefore;
      }

      const count = kind === 'double' ? 2 : 1;
      const episodes = regular.slice(cursor, cursor + count);

      if (!result.error) {
        if (episodes.length < count) {
          result.error = 'pastEnd';
        } else {
          result.episodes = episodes;
        }
      }

      cursor += count;
    }

    result.episodes.forEach((episode) => {
      const earlier = takenBy.get(episode.id);

      if (earlier != null && result.duplicateOf == null) {
        result.duplicateOf = earlier;
      }

      takenBy.set(episode.id, id);
    });

    head = result;
    chainLength = 1;
    chainType = undefined;

    results.push(result);
  });

  return results;
}

const CHUNKS = /(\d+)/;

/**
 * Names compared the way a person reads them, so EP.99 comes before EP.100. Done by hand rather
 * than with the browser's collation: under a Thai locale that put EP.07 ahead of EP.05.
 */
export function naturalCompare(a: string, b: string) {
  const left = a.toLowerCase().split(CHUNKS);
  const right = b.toLowerCase().split(CHUNKS);

  for (let i = 0; i < Math.min(left.length, right.length); i++) {
    if (left[i] !== right[i]) {
      // split() with a capture group leaves the numbers at the odd positions.
      if (i % 2 === 1) {
        const difference = Number(left[i]) - Number(right[i]);

        return difference || left[i].length - right[i].length;
      }

      return left[i] < right[i] ? -1 : 1;
    }
  }

  return left.length - right.length;
}

// After EP, E, Episode or ตอนที่, which is where a run's own count is written. The E has to stand on
// its own, so the E of S01E05 is not taken for one.
const NAME_NUMBER =
  /(?:^|[^a-z0-9])(?:ep(?:isode)?|e)[ ._-]*(\d{1,4})|ตอนที่\s*(\d{1,4})/i;

/** The number a file gives itself. Read only to point at places worth a look, never to place it. */
export function readNameNumber(name: string) {
  const match = NAME_NUMBER.exec(name);
  const number = match ? match[1] ?? match[2] : undefined;

  return number == null ? undefined : Number(number);
}

export interface NameNumber {
  number?: number;
  shift?: number;
}

/**
 * Where a file's own number stops moving in step with the order. A run numbered its own way is
 * usually off by a steady amount; where that amount changes is where a double episode, a missing
 * file or a special sits, and where the mapping most likely needs a hand. Only files that move the
 * count are compared: not a second part or version, not a special.
 */
export function findShifts(
  rows: { id: number; name: string }[],
  results: Record<number, MapResult>,
  regular: Episode[]
) {
  const positions = regular.reduce<Record<number, number>>((acc, e, i) => {
    acc[e.id] = i + 1;
    return acc;
  }, {});

  const byId: Record<number, NameNumber> = {};
  const shiftRows: number[] = [];
  let previous = undefined as number | undefined;

  rows.forEach(({ id, name }, index) => {
    const result = results[id];
    const number = readNameNumber(name);
    const first = result?.episodes[0];

    byId[id] = { number };

    if (
      number == null ||
      !first ||
      first.seasonNumber === 0 ||
      (result?.multipleNumber ?? 0) > 1
    ) {
      return;
    }

    const offset = number - positions[first.id];

    if (previous != null && offset !== previous) {
      byId[id].shift = offset - previous;
      shiftRows.push(index);
    }

    previous = offset;
  });

  return { byId, shiftRows };
}

// Types only, so the file runs on its own under the tests without the app's import paths.
import type Episode from 'Episode/Episode';
import type MultipleType from 'InteractiveImport/MultipleType';

// What a file is, as far as the order goes:
//   episode - takes the next episode
//   double  - takes the next few (episodeCount, two unless said), a file that runs across them
//   part    - is another piece of the file above it, so takes nothing new
//   version - is the file above it told differently, so takes nothing new
//   special - is a special picked by hand; specials are not in the order, so it takes nothing either
//   skip    - is not imported at all: a sample, a trailer, a file of another show. It takes nothing,
//             and the files after it carry on as though it were not there
export type MapKind =
  | 'episode'
  | 'double'
  | 'part'
  | 'version'
  | 'special'
  | 'skip';

export interface MapSetting {
  kind: MapKind;
  // Episodes with no file, passed over before this one is placed.
  skipBefore: number;
  // Put this file at this episode, and carry on counting from there.
  pinEpisodeId?: number;
  specialEpisodeId?: number;
  // How many episodes a 'double' file runs across. The name stayed when it stopped meaning two.
  episodeCount?: number;
}

export function getEpisodeCount(setting: MapSetting) {
  return setting.kind === 'double' ? Math.max(2, setting.episodeCount ?? 2) : 1;
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

    // Left out entirely: it is no file's head, and no part can join it.
    if (kind === 'skip') {
      results.push({
        id,
        episodes: [],
        multipleType: 'none',
        multipleNumber: 0,
        skipped: [],
      });

      return;
    }

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

      const count = getEpisodeCount(setting);
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

/**
 * Ticks or unticks every row from the one last clicked to this one, the way Shift+click works in a
 * file list. With nothing clicked before, or the last one gone from the list, only this row changes.
 */
export function selectRange(
  order: number[],
  selected: Set<number>,
  anchorId: number | undefined,
  id: number,
  value: boolean
) {
  const next = new Set(selected);
  const from = anchorId == null ? -1 : order.indexOf(anchorId);
  const to = order.indexOf(id);
  const ids =
    from < 0 || to < 0
      ? [id]
      : order.slice(Math.min(from, to), Math.max(from, to) + 1);

  ids.forEach((rowId) => {
    if (value) {
      next.add(rowId);
    } else {
      next.delete(rowId);
    }
  });

  return next;
}

export interface MappedRow {
  id: number;
  name: string;
  episodes: Episode[];
  multipleType?: MultipleType;
  multipleNumber?: number;
}

export interface MapState {
  order: number[];
  settings: Record<number, MapSetting>;
  startEpisodeId: number;
}

function episodeKey(episode: Pick<Episode, 'seasonNumber' | 'episodeNumber'>) {
  return `${episode.seasonNumber}x${episode.episodeNumber}`;
}

/**
 * The settings that would give the rows the episodes they already have - so opening Map in Order
 * again after applying it, or after setting episodes some other way, starts from what is on the rows
 * rather than from the top. The order follows the episodes; a special keeps its place by name among
 * them; files with no episode yet go last. Episodes are matched by season and number, not id, since
 * the rows may carry another edition's episodes.
 *
 * Nothing on the rows says which episodes have no file, so a gap comes back as a skip, and a file
 * placed on an episode before the one the order has reached comes back as a pin.
 */
export function mapFromRows(
  rows: MappedRow[],
  regular: Episode[],
  specials: Episode[]
): MapState | null {
  const positions = new Map(regular.map((e, i) => [episodeKey(e), i]));
  const specialsByKey = new Map(specials.map((e) => [episodeKey(e), e]));
  const byName = (a: MappedRow, b: MappedRow) => naturalCompare(a.name, b.name);

  const firstPosition = (row: MappedRow) =>
    row.episodes.length
      ? positions.get(episodeKey(row.episodes[0]))
      : undefined;

  const mapped = rows
    .filter((r) => firstPosition(r) != null)
    .sort(
      (a, b) =>
        (firstPosition(a) as number) - (firstPosition(b) as number) ||
        (a.multipleNumber ?? 0) - (b.multipleNumber ?? 0) ||
        byName(a, b)
    );

  if (!mapped.length) {
    return null;
  }

  const specialRows = rows
    .filter(
      (r) =>
        firstPosition(r) == null &&
        r.episodes.length &&
        specialsByKey.has(episodeKey(r.episodes[0]))
    )
    .sort(byName);

  const ordered = [...mapped];

  specialRows.forEach((special) => {
    const before = ordered.findIndex((r) => byName(r, special) > 0);

    ordered.splice(before < 0 ? ordered.length : before, 0, special);
  });

  const rest = rows.filter((r) => !ordered.includes(r)).sort(byName);

  const start = firstPosition(mapped[0]) as number;
  const settings: Record<number, MapSetting> = {};

  let cursor = start;
  let headKey = '';

  [...ordered, ...rest].forEach((row) => {
    const key = row.episodes.map(episodeKey).join('+');
    const position = firstPosition(row);
    const isFollower =
      (row.multipleNumber ?? 0) > 1 &&
      key === headKey &&
      (row.multipleType === 'part' || row.multipleType === 'version');

    if (isFollower) {
      settings[row.id] = {
        ...DEFAULT_SETTING,
        kind: row.multipleType as MapKind,
      };

      return;
    }

    headKey = key;

    if (position == null) {
      const special = row.episodes.length
        ? specialsByKey.get(episodeKey(row.episodes[0]))
        : undefined;

      settings[row.id] = special
        ? { ...DEFAULT_SETTING, kind: 'special', specialEpisodeId: special.id }
        : { ...DEFAULT_SETTING };

      return;
    }

    const setting: MapSetting = {
      ...DEFAULT_SETTING,
      kind: row.episodes.length > 1 ? 'double' : 'episode',
    };

    if (row.episodes.length > 2) {
      setting.episodeCount = row.episodes.length;
    }

    if (position > cursor) {
      setting.skipBefore = position - cursor;
    } else if (position < cursor) {
      setting.pinEpisodeId = regular[position].id;
    }

    settings[row.id] = setting;
    cursor = position + getEpisodeCount(setting);
  });

  return {
    order: [...ordered, ...rest].map((r) => r.id),
    settings,
    startEpisodeId: regular[start].id,
  };
}

// Types only from the rest of the app, so the file runs on its own under the tests.
import type Episode from 'Episode/Episode';
import { DEFAULT_SETTING, MapResult, MapSetting } from './mapInOrder';

// What the lines view draws, one entry per line of the list:
//   slot    - one episode of the order, with the files whose first episode it is beside it. A slot
//             with no file of its own is either covered by a file above that runs across several
//             episodes, or a gap: an episode with no file.
//   section - a heading: the specials, or the files that still need a decision.
//   file    - a file under one of those headings.
export type LineRow =
  | { type: 'slot'; episodeIndex: number; fileIds: number[]; isGap: boolean }
  | { type: 'section'; section: 'specials' | 'skipped' | 'problems' }
  | { type: 'file'; fileId: number };

export interface OrderState {
  order: number[];
  settings: Record<number, MapSetting>;
}

function isFollower(setting?: MapSetting) {
  return setting?.kind === 'part' || setting?.kind === 'version';
}

function positionsOf(regular: Episode[]) {
  return new Map(regular.map((e, i) => [e.id, i]));
}

/**
 * The lines of the view, from the start of the order to the last episode a file reaches. Every file
 * lands in exactly one place: the slot of its first episode, the specials, or the files that need a
 * decision. Parts and versions sit in the same slot as the file they belong to, in order.
 */
export function buildLineRows(
  order: number[],
  results: Record<number, MapResult>,
  regular: Episode[],
  start: number
): LineRow[] {
  const positions = positionsOf(regular);
  const bySlot = new Map<number, number[]>();
  const covered = new Set<number>();
  const specials: number[] = [];
  const skippedFiles: number[] = [];
  const problems: number[] = [];
  let last = start;

  order.forEach((id) => {
    const result = results[id];
    const first = result?.episodes[0];

    if (result && !result.error && !first) {
      skippedFiles.push(id);
      return;
    }

    if (!result || result.error || !first) {
      problems.push(id);
      return;
    }

    if (first.seasonNumber === 0) {
      specials.push(id);
      return;
    }

    const index = positions.get(first.id) as number;

    // Pinned back before the start, where no line of the view reaches.
    if (index < start) {
      problems.push(id);
      return;
    }

    bySlot.set(index, [...(bySlot.get(index) ?? []), id]);

    result.episodes.forEach((e) => {
      const position = positions.get(e.id);

      if (position != null) {
        covered.add(position);
        last = Math.max(last, position);
      }
    });
  });

  const rows: LineRow[] = [];

  for (let i = start; i <= last && i < regular.length; i++) {
    rows.push({
      type: 'slot',
      episodeIndex: i,
      fileIds: bySlot.get(i) ?? [],
      isGap: !covered.has(i),
    });
  }

  if (specials.length) {
    rows.push({ type: 'section', section: 'specials' });
    specials.forEach((fileId) => rows.push({ type: 'file', fileId }));
  }

  if (skippedFiles.length) {
    rows.push({ type: 'section', section: 'skipped' });
    skippedFiles.forEach((fileId) => rows.push({ type: 'file', fileId }));
  }

  if (problems.length) {
    rows.push({ type: 'section', section: 'problems' });
    problems.forEach((fileId) => rows.push({ type: 'file', fileId }));
  }

  return rows;
}

/**
 * Takes files out of the order. A file that had parts or versions after it hands its place to the
 * first of them, so the set stays where it was rather than attaching itself to the file above.
 */
export function removeFiles(state: OrderState, ids: number[]): OrderState {
  const settings = { ...state.settings };

  state.order.forEach((id, index) => {
    if (!ids.includes(id) || isFollower(settings[id])) {
      return;
    }

    const heir = state.order
      .slice(index + 1)
      .find((other) => !ids.includes(other));

    if (heir != null && isFollower(settings[heir])) {
      settings[heir] = { ...(settings[id] ?? DEFAULT_SETTING) };
    }
  });

  return { order: state.order.filter((id) => !ids.includes(id)), settings };
}

/**
 * Makes a file another part (or version) of the set the target file is in, at the end of that set.
 * Dropping a file onto a file is how a set is built.
 */
export function makePartOf(
  state: OrderState,
  id: number,
  targetId: number
): OrderState {
  if (id === targetId) {
    return state;
  }

  const removed = removeFiles(state, [id]);
  const order = [...removed.order];
  let head = order.indexOf(targetId);

  while (head > 0 && isFollower(removed.settings[order[head]])) {
    head--;
  }

  let end = head + 1;

  while (end < order.length && isFollower(removed.settings[order[end]])) {
    end++;
  }

  // A set is all parts or all versions; the new file joins as whatever the set already is.
  const kind = end > head + 1 ? removed.settings[order[head + 1]].kind : 'part';

  order.splice(end, 0, id);

  return {
    order,
    settings: { ...removed.settings, [id]: { ...DEFAULT_SETTING, kind } },
  };
}

/**
 * Puts files at an episode: the first is pinned there and the rest follow it. Dropped further on than
 * where it is now, a file keeps its place in the order and the files after it follow it on; dropped
 * further back, it moves in front of the file that had that episode, and that file and the rest carry
 * on after it. Dropping a file onto an episode is how a pin is made.
 */
export function placeAt(
  state: OrderState,
  ids: number[],
  episodeId: number,
  results: Record<number, MapResult>,
  regular: Episode[]
): OrderState {
  const positions = positionsOf(regular);
  const target = positions.get(episodeId);

  if (target == null || !ids.length) {
    return state;
  }

  const firstOf = (id: number) => {
    const first = results[id]?.episodes[0];

    return first ? positions.get(first.id) : undefined;
  };

  const current = firstOf(ids[0]);
  const keepsPlace =
    current != null &&
    current <= target &&
    ids.every(
      (id, i) =>
        i === 0 || state.order.indexOf(id) === state.order.indexOf(ids[0]) + i
    );

  const moved = keepsPlace ? state : removeFiles(state, ids);
  const settings = { ...moved.settings };
  const order = [...moved.order];

  if (!keepsPlace) {
    const before = order.findIndex((id) => {
      const position = firstOf(id);

      return (
        !isFollower(settings[id]) && position != null && position >= target
      );
    });

    order.splice(before < 0 ? order.length : before, 0, ...ids);
  }

  const first = settings[ids[0]] ?? DEFAULT_SETTING;

  settings[ids[0]] = {
    ...first,
    kind: first.kind === 'double' ? 'double' : 'episode',
    pinEpisodeId: episodeId,
    skipBefore: 0,
  };

  return { order, settings };
}

/**
 * Closes the gap at an episode with no file: the file that skipped it skips one fewer, or, when the
 * gap was left by a pin further on, that pin goes and the file falls back into the order.
 */
export function closeGap(
  state: OrderState,
  episodeId: number,
  results: Record<number, MapResult>,
  regular: Episode[]
): OrderState {
  const positions = positionsOf(regular);
  const gap = positions.get(episodeId);
  const settings = { ...state.settings };

  const skipper = state.order.find((id) =>
    results[id]?.skipped.some((e) => e.id === episodeId)
  );

  if (skipper != null) {
    const setting = settings[skipper] ?? DEFAULT_SETTING;

    settings[skipper] = {
      ...setting,
      skipBefore: Math.max(0, setting.skipBefore - 1),
    };

    return { order: state.order, settings };
  }

  if (gap == null) {
    return state;
  }

  // The first file placed after the gap; if a pin put it there, the pin made the gap.
  const next = state.order
    .filter((id) => !isFollower(settings[id]))
    .map((id) => ({ id, first: results[id]?.episodes[0] }))
    .filter(({ first }) => first && (positions.get(first.id) ?? -1) > gap)
    .sort(
      (a, b) =>
        (positions.get(a.first!.id) as number) -
        (positions.get(b.first!.id) as number)
    )[0];

  if (next && settings[next.id]?.pinEpisodeId != null) {
    const rest = { ...settings[next.id] };

    delete rest.pinEpisodeId;
    settings[next.id] = rest;
  }

  return { order: state.order, settings };
}

/**
 * Leaves an episode without a file: the file that had it moves on to the next episode, and the files
 * after it follow. A pinned file keeps its pin, one episode further on.
 */
export function skipEpisode(
  state: OrderState,
  episodeId: number,
  results: Record<number, MapResult>,
  regular: Episode[]
): OrderState {
  const positions = positionsOf(regular);
  const position = positions.get(episodeId);
  const settings = { ...state.settings };

  const owner = state.order.find(
    (id) =>
      !isFollower(settings[id]) && results[id]?.episodes[0]?.id === episodeId
  );

  if (owner == null || position == null) {
    return state;
  }

  const setting = settings[owner] ?? DEFAULT_SETTING;

  if (setting.pinEpisodeId == null) {
    settings[owner] = { ...setting, skipBefore: setting.skipBefore + 1 };
  } else {
    const next = regular[position + 1];

    if (!next) {
      return state;
    }

    settings[owner] = { ...setting, pinEpisodeId: next.id };
  }

  return { order: state.order, settings };
}

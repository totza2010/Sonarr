// Types only, so the file runs on its own under the tests.
import type InteractiveImport from 'InteractiveImport/InteractiveImport';

/**
 * The selected files that would knock each other out. Two new files for the same episode both get
 * imported, and the second replaces the first, unless they say they are parts or versions of it -
 * so a file with nothing said, sharing an episode with another selected new file, is a mistake
 * waiting to happen. Files already in the library are left out: a new file for an episode that has
 * one is an upgrade, which is what importing is for.
 */
export default function findUnmarkedSharedFiles(
  items: Pick<
    InteractiveImport,
    'id' | 'episodes' | 'episodeFileId' | 'multipleType'
  >[],
  selectedIds: number[]
): number[] {
  const byEpisode = new Map<number, typeof items>();

  items.forEach((item) => {
    if (!selectedIds.includes(item.id) || item.episodeFileId) {
      return;
    }

    (item.episodes ?? []).forEach((episode) => {
      byEpisode.set(episode.id, [...(byEpisode.get(episode.id) ?? []), item]);
    });
  });

  const found = new Set<number>();

  byEpisode.forEach((files) => {
    if (files.length < 2) {
      return;
    }

    files
      .filter((f) => !f.multipleType || f.multipleType === 'none')
      .forEach((f) => found.add(f.id));
  });

  return [...found];
}

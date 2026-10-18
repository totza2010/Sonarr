import classNames from 'classnames';
import React, {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import {
  ListChildComponentProps,
  VariableSizeList as List,
} from 'react-window';
import CheckInput from 'Components/Form/CheckInput';
import SelectInput from 'Components/Form/SelectInput';
import Icon from 'Components/Icon';
import Episode from 'Episode/Episode';
import { icons } from 'Helpers/Props';
import getMultipleMarker from 'InteractiveImport/Multiple/getMultipleMarker';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import {
  DEFAULT_SETTING,
  getEpisodeCount,
  MapResult,
  MapSetting,
  NameNumber,
} from './mapInOrder';
import { LineRow } from './mapLines';
import styles from './MapInOrderLines.css';

const CHIP = 34;

// Wrapped, a name gets up to three lines; past that the tooltip still has all of it. The height is
// fixed rather than measured so the lines to episodes further down can still be worked out up front.
const CHIP_WRAPPED = 62;

function chipHeight(wrap: boolean) {
  return wrap ? CHIP_WRAPPED : CHIP;
}
const PAD = 6;
const SECTION = 32;

function preventDefault(event: React.SyntheticEvent) {
  event.preventDefault();
}

const EPISODE: Partial<MapSetting> = { kind: 'episode' };
const PART: Partial<MapSetting> = { kind: 'part' };
const VERSION: Partial<MapSetting> = { kind: 'version' };
const SKIP: Partial<MapSetting> = { kind: 'skip' };
const UNPIN: Partial<MapSetting> = { pinEpisodeId: undefined };
const NO_SKIP: Partial<MapSetting> = { skipBefore: 0 };

const SECTION_KEYS = {
  specials: 'MapInOrderSpecials',
  skipped: 'MapInOrderSkippedFiles',
  problems: 'MapInOrderNeedsDecision',
};

const ERROR_KEYS: Record<string, string> = {
  noPrevious: 'MapInOrderErrorNoPrevious',
  pastEnd: 'MapInOrderErrorPastEnd',
  noSpecial: 'MapInOrderErrorNoSpecial',
  mixedChain: 'MapInOrderErrorMixedChain',
  pinNotFound: 'MapInOrderErrorPinNotFound',
};

function episodeCode(episode: Episode) {
  return `S${`${episode.seasonNumber}`.padStart(
    2,
    '0'
  )}E${`${episode.episodeNumber}`.padStart(2, '0')}`;
}

function formatShift(shift: number) {
  return shift > 0 ? `+${shift}` : `${shift}`;
}

export interface LinesData {
  rows: LineRow[];
  regular: Episode[];
  specials: Episode[];
  results: Record<number, MapResult>;
  settings: Record<number, MapSetting>;
  names: Record<number, string>;
  nameNumbers: Record<number, NameNumber>;
  selected: Set<number>;
  replaced: Set<number>;
  heights: number[];
  offsets: number[];
  rowByEpisodeIndex: Map<number, number>;
  onOpenMenu(id: number, x: number, y: number): void;
  onSettingChange(id: number, change: Partial<MapSetting>): void;
  onSelectedChange(id: number, value: boolean, shiftKey: boolean): void;
  onDragStart(id: number): void;
  onDropOnEpisode(episodeId: number): void;
  onDropOnFile(id: number): void;
  onCloseGap(episodeId: number): void;
  onSkipEpisode(episodeId: number): void;
  wrap: boolean;
}

interface ChipProps {
  id: number;
  data: LinesData;
}

// A long name loses its middle, not its end: the start says which episode it is and the end often
// says which part or cut. The extension says nothing here and is left off; the tooltip has it all.
const TAIL = 18;

function MiddleName({ name, wrap }: { name: string; wrap: boolean }) {
  const shown = name.replace(/\.[a-z0-9]{2,4}$/i, '');

  if (wrap) {
    return <span className={styles.wrappedName}>{shown}</span>;
  }

  if (shown.length <= TAIL * 2) {
    return <span className={styles.name}>{shown}</span>;
  }

  return (
    <span className={styles.middleName}>
      <span className={styles.head}>{shown.slice(0, -TAIL)}</span>
      <span className={styles.tail}>{shown.slice(-TAIL)}</span>
    </span>
  );
}

function FileChip({ id, data }: ChipProps) {
  const {
    results,
    settings,
    names,
    nameNumbers,
    selected,
    onOpenMenu,
    onSelectedChange,
    onDragStart,
    onDropOnFile,
  } = data;

  const result = results[id];
  const setting = settings[id] ?? DEFAULT_SETTING;
  const shift = nameNumbers[id]?.shift;
  const marker = result
    ? getMultipleMarker(result.multipleType, result.multipleNumber)
    : null;
  const isBad = !!result?.error || result?.duplicateOf != null;
  const isMulti =
    !!marker || (result?.episodes.length ?? 0) > 1 || setting.kind === 'double';

  // The settings for a file are on its right-click menu, so nothing has to be opened first and
  // nothing is drawn inline to push the rows apart.
  const handleContextMenu = useCallback(
    (event: React.MouseEvent) => {
      event.preventDefault();
      onOpenMenu(id, event.clientX, event.clientY);
    },
    [id, onOpenMenu]
  );

  const handleSelected = useCallback(
    ({ value, shiftKey = false }: CheckInputChanged) => {
      onSelectedChange(id, value, shiftKey);
    },
    [id, onSelectedChange]
  );

  const stop = useCallback((event: React.MouseEvent) => {
    event.stopPropagation();
  }, []);

  const handleDragStart = useCallback(
    (event: React.DragEvent) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', `${id}`);
      onDragStart(id);
    },
    [id, onDragStart]
  );

  const handleDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.currentTarget.classList.add(styles.over);
  }, []);

  const handleDragLeave = useCallback((event: React.DragEvent) => {
    event.currentTarget.classList.remove(styles.over);
  }, []);

  const handleDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      event.stopPropagation();
      event.currentTarget.classList.remove(styles.over);
      onDropOnFile(id);
    },
    [id, onDropOnFile]
  );

  return (
    <div
      className={classNames(
        styles.chip,
        data.wrap && styles.wrapped,
        isMulti && styles.multi,
        isBad && styles.bad
      )}
      draggable={true}
      title={names[id]}
      onContextMenu={handleContextMenu}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className={styles.check} onClick={stop}>
        <CheckInput
          className={styles.checkInput}
          containerClassName={styles.checkContainer}
          name={`select-${id}`}
          value={selected.has(id)}
          onChange={handleSelected}
        />
      </div>

      <MiddleName name={names[id]} wrap={data.wrap} />

      {shift ? (
        <span
          className={styles.shift}
          title={translate('MapInOrderShift', { shift: formatShift(shift) })}
        >
          {formatShift(shift)}
        </span>
      ) : null}

      {marker ? <span className={styles.tag}>{marker}</span> : null}

      {setting.kind === 'double' ? (
        <span className={styles.tag}>
          {translate('MapInOrderEpisodeCountTag', {
            count: getEpisodeCount(setting),
          })}
        </span>
      ) : null}

      {setting.pinEpisodeId == null ? null : (
        <Icon name={icons.PIN} title={translate('MapInOrderPinned')} />
      )}
    </div>
  );
}

function MenuButton({
  label,
  change,
  isActive = false,
  keepOpen = false,
  onPress,
  onDone,
}: {
  label: string;
  change: Partial<MapSetting>;
  isActive?: boolean;
  keepOpen?: boolean;
  onPress(change: Partial<MapSetting>): void;
  onDone(): void;
}) {
  const handleClick = useCallback(() => {
    onPress(change);

    if (!keepOpen) {
      onDone();
    }
  }, [change, keepOpen, onPress, onDone]);

  return (
    <button
      type="button"
      className={classNames(styles.menuButton, isActive && styles.active)}
      onClick={handleClick}
    >
      {label}
    </button>
  );
}

interface ContextMenuProps {
  id: number;
  x: number;
  y: number;
  data: LinesData;
  onClose(): void;
}

function FileMenu({ id, x, y, data, onClose }: ContextMenuProps) {
  const { settings, specials, onSettingChange } = data;
  const ref = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ left: x, top: y });

  // Kept inside the window: a menu opened near the bottom or the right edge opens up or to the left.
  useLayoutEffect(() => {
    const bounds = ref.current?.getBoundingClientRect();

    if (bounds) {
      setPosition({
        left: Math.max(4, Math.min(x, window.innerWidth - bounds.width - 4)),
        top: Math.max(4, Math.min(y, window.innerHeight - bounds.height - 4)),
      });
    }
  }, [x, y]);

  useEffect(() => {
    const handleDown = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) {
        onClose();
      }
    };

    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleDown);
    document.addEventListener('keydown', handleKey);

    return () => {
      document.removeEventListener('mousedown', handleDown);
      document.removeEventListener('keydown', handleKey);
    };
  }, [onClose]);
  const setting = settings[id] ?? DEFAULT_SETTING;

  const set = useCallback(
    (change: Partial<MapSetting>) => onSettingChange(id, change),
    [id, onSettingChange]
  );

  const specialOptions = useMemo(
    () => [
      { key: '', value: translate('MapInOrderKindSpecial') },
      ...specials.map((e) => ({
        key: `${e.id}`,
        value: `${episodeCode(e)} ${e.title}`,
      })),
    ],
    [specials]
  );

  const handleSpecial = useCallback(
    ({ value }: InputChanged<string>) => {
      if (value) {
        set({ kind: 'special', specialEpisodeId: Number(value) });
        onClose();
      }
    },
    [set, onClose]
  );

  const count = getEpisodeCount(setting);

  return (
    <div
      ref={ref}
      className={styles.menu}
      style={position}
      onContextMenu={preventDefault}
    >
      <MenuButton
        label={translate('MapInOrderKindEpisode')}
        change={EPISODE}
        isActive={setting.kind === 'episode'}
        onPress={set}
        onDone={onClose}
      />
      <MenuButton
        label={translate('MapInOrderKindDouble')}
        change={{ kind: 'double', episodeCount: Math.max(2, count) }}
        isActive={setting.kind === 'double'}
        onPress={set}
        onDone={onClose}
      />

      {setting.kind === 'double' ? (
        <span className={classNames(styles.counter, styles.menuIndent)}>
          <MenuButton
            label="−"
            change={{ episodeCount: Math.max(2, count - 1) }}
            keepOpen={true}
            onPress={set}
            onDone={onClose}
          />
          <span>{count}</span>
          <MenuButton
            label="+"
            change={{ episodeCount: count + 1 }}
            keepOpen={true}
            onPress={set}
            onDone={onClose}
          />
        </span>
      ) : null}

      <MenuButton
        label={translate('MapInOrderKindPart')}
        change={PART}
        isActive={setting.kind === 'part'}
        onPress={set}
        onDone={onClose}
      />
      <MenuButton
        label={translate('MapInOrderKindVersion')}
        change={VERSION}
        isActive={setting.kind === 'version'}
        onPress={set}
        onDone={onClose}
      />

      <MenuButton
        label={translate('MapInOrderKindSkip')}
        change={SKIP}
        isActive={setting.kind === 'skip'}
        onPress={set}
        onDone={onClose}
      />

      <SelectInput
        className={styles.special}
        name={`special-${id}`}
        value={
          setting.kind === 'special' ? `${setting.specialEpisodeId ?? ''}` : ''
        }
        values={specialOptions}
        onChange={handleSpecial}
      />

      {setting.pinEpisodeId == null ? null : (
        <MenuButton
          label={translate('MapInOrderUnpin')}
          change={UNPIN}
          onPress={set}
          onDone={onClose}
        />
      )}

      {setting.skipBefore ? (
        <MenuButton
          label={translate('MapInOrderNoSkip')}
          change={NO_SKIP}
          onPress={set}
          onDone={onClose}
        />
      ) : null}
    </div>
  );
}

function EpisodeBox({
  episode,
  data,
  isMissing,
  canSkip = false,
}: {
  episode: Episode;
  data: LinesData;
  isMissing?: boolean;
  canSkip?: boolean;
}) {
  const { replaced, onDropOnEpisode, onSkipEpisode } = data;

  const handleSkip = useCallback(() => {
    onSkipEpisode(episode.id);
  }, [episode.id, onSkipEpisode]);

  const handleDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.currentTarget.classList.add(styles.over);
  }, []);

  const handleDragLeave = useCallback((event: React.DragEvent) => {
    event.currentTarget.classList.remove(styles.over);
  }, []);

  const handleDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      event.currentTarget.classList.remove(styles.over);
      onDropOnEpisode(episode.id);
    },
    [episode.id, onDropOnEpisode]
  );

  return (
    <div
      className={classNames(styles.episode, isMissing && styles.missing)}
      title={episode.title}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <span className={styles.code}>{episodeCode(episode)}</span>
      <span className={styles.name}>{episode.title}</span>

      {replaced.has(episode.id) ? (
        <span
          className={styles.replaces}
          title={translate('MapInOrderReplaceHint')}
        />
      ) : null}

      {canSkip ? (
        <button
          type="button"
          className={styles.skipEpisode}
          title={translate('MapInOrderSkipEpisode')}
          onClick={handleSkip}
        >
          {translate('MapInOrderSkipEpisodeShort')}
        </button>
      ) : null}
    </div>
  );
}

// The lines from each file on a row to its episodes. Episodes further down the list are reached by
// the row offsets worked out up front, so nothing has to be measured on screen.
function Lines({ index, data }: { index: number; data: LinesData }) {
  const { rows, results, regular, heights, offsets, rowByEpisodeIndex } = data;
  const row = rows[index];
  const positions = useMemo(
    () => new Map(regular.map((e, i) => [e.id, i])),
    [regular]
  );

  let ids: number[] = [];

  if (row.type === 'slot') {
    ids = row.fileIds;
  } else if (row.type === 'file') {
    ids = [row.fileId];
  }

  const paths: { d: string; className: string }[] = [];
  let y = PAD / 2;

  ids.forEach((id) => {
    const result = results[id];
    const from = y + chipHeight(data.wrap) / 2;
    const isMulti =
      (result?.multipleType ?? 'none') !== 'none' ||
      (result?.episodes.length ?? 0) > 1;

    (result?.episodes ?? []).forEach((episode) => {
      const position = positions.get(episode.id);
      const target =
        row.type === 'file' || position == null
          ? index
          : rowByEpisodeIndex.get(position) ?? index;
      const to = offsets[target] - offsets[index] + heights[target] / 2;

      let className = isMulti ? styles.lineMulti : styles.line;

      if (result.duplicateOf != null) {
        className = styles.lineBad;
      }

      paths.push({
        d: `M0 ${from} C 50 ${from}, 50 ${to}, 100 ${to}`,
        className,
      });
    });

    y += chipHeight(data.wrap);
  });

  if (row.type === 'slot' && row.isGap) {
    const mid = heights[index] / 2;

    paths.push({ d: `M0 ${mid} L 100 ${mid}`, className: styles.lineGap });
  }

  return (
    <svg
      className={styles.lines}
      viewBox={`0 0 100 ${heights[index]}`}
      preserveAspectRatio="none"
      height={heights[index]}
    >
      {paths.map((p, i) => (
        <path
          key={i}
          d={p.d}
          className={p.className}
          vectorEffect="non-scaling-stroke"
        />
      ))}
    </svg>
  );
}

function LineRowView({
  index,
  style,
  data,
}: ListChildComponentProps<LinesData>) {
  const { rows, regular, results, onCloseGap } = data;
  const row = rows[index];

  const handleCloseGap = useCallback(() => {
    if (row.type === 'slot') {
      onCloseGap(regular[row.episodeIndex].id);
    }
  }, [row, regular, onCloseGap]);

  if (row.type === 'section') {
    return (
      <div className={styles.section} style={style}>
        {translate(SECTION_KEYS[row.section])}
      </div>
    );
  }

  const ids = row.type === 'slot' ? row.fileIds : [row.fileId];

  let right: React.ReactNode = null;

  if (row.type === 'slot') {
    right = (
      <EpisodeBox
        episode={regular[row.episodeIndex]}
        data={data}
        isMissing={row.isGap}
        canSkip={row.fileIds.length > 0}
      />
    );
  } else {
    const result = results[row.fileId];
    const episode = result?.episodes[0];

    right = episode ? (
      <EpisodeBox episode={episode} data={data} />
    ) : (
      <span className={styles.error}>
        {result?.error
          ? translate(ERROR_KEYS[result.error])
          : translate('MapInOrderNotImported')}
      </span>
    );
  }

  return (
    <div className={styles.row} style={style}>
      <div className={styles.files}>
        {ids.map((id) => (
          <React.Fragment key={id}>
            <FileChip id={id} data={data} />
          </React.Fragment>
        ))}

        {row.type === 'slot' && row.isGap ? (
          <button type="button" className={styles.gap} onClick={handleCloseGap}>
            {translate('MapInOrderNoFile')}
          </button>
        ) : null}
      </div>

      <div className={styles.middle}>
        <Lines index={index} data={data} />
      </div>

      <div className={styles.episodes}>{right}</div>
    </div>
  );
}

function rowHeight(row: LineRow, wrap: boolean) {
  if (row.type === 'section') {
    return SECTION;
  }

  const ids = row.type === 'slot' ? row.fileIds : [row.fileId];
  return PAD + Math.max(1, ids.length) * chipHeight(wrap);
}

export function measureRows(rows: LineRow[], wrap: boolean) {
  const heights = rows.map((row) => rowHeight(row, wrap));
  const offsets: number[] = [];
  let total = 0;

  heights.forEach((h) => {
    offsets.push(total);
    total += h;
  });

  const rowByEpisodeIndex = new Map<number, number>();

  rows.forEach((row, i) => {
    if (row.type === 'slot') {
      rowByEpisodeIndex.set(row.episodeIndex, i);
    }
  });

  return { heights, offsets, rowByEpisodeIndex };
}

interface MapInOrderLinesProps {
  data: LinesData;
  height: number;
  listRef: React.RefObject<List<LinesData>>;
  outerRef: React.RefObject<HTMLDivElement>;
}

function MapInOrderLines({
  data,
  height,
  listRef,
  outerRef,
}: MapInOrderLinesProps) {
  const getSize = useCallback(
    (index: number) => data.heights[index],
    [data.heights]
  );

  const [menu, setMenu] = useState<{ id: number; x: number; y: number } | null>(
    null
  );

  const handleOpenMenu = useCallback((id: number, x: number, y: number) => {
    setMenu({ id, x, y });
  }, []);

  const handleCloseMenu = useCallback(() => setMenu(null), []);

  const itemData = useMemo(
    () => ({ ...data, onOpenMenu: handleOpenMenu }),
    [data, handleOpenMenu]
  );

  // Row heights change with every set built, and the list keeps the old ones until told.
  useEffect(() => {
    listRef.current?.resetAfterIndex(0);
  }, [data.heights, listRef]);

  return (
    <>
      <List<LinesData>
        ref={listRef}
        outerRef={outerRef}
        height={height}
        width="100%"
        itemCount={data.rows.length}
        itemSize={getSize}
        itemData={itemData}
        overscanCount={8}
      >
        {LineRowView}
      </List>

      {menu ? (
        <FileMenu
          id={menu.id}
          x={menu.x}
          y={menu.y}
          data={itemData}
          onClose={handleCloseMenu}
        />
      ) : null}
    </>
  );
}

export default MapInOrderLines;

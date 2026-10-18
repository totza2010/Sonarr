import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useSelector } from 'react-redux';
import {
  FixedSizeList as List,
  ListChildComponentProps,
  VariableSizeList,
} from 'react-window';
import CheckInput from 'Components/Form/CheckInput';
import NumberInput from 'Components/Form/NumberInput';
import SelectInput from 'Components/Form/SelectInput';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Episode from 'Episode/Episode';
import { icons, kinds } from 'Helpers/Props';
import InteractiveImport from 'InteractiveImport/InteractiveImport';
import getMultipleMarker from 'InteractiveImport/Multiple/getMultipleMarker';
import MultipleType from 'InteractiveImport/MultipleType';
import getSeriesEditions from 'Series/getSeriesEditions';
import Series from 'Series/Series';
import seriesEditionTitle from 'Series/seriesEditionTitle';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import {
  CheckInputChanged,
  FileInputChanged,
  InputChanged,
} from 'typings/inputs';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import mapInOrder, {
  DEFAULT_SETTING,
  findShifts,
  mapFromRows,
  MapKind,
  MapResult,
  MapSetting,
  NameNumber,
  naturalCompare,
  selectRange,
} from './mapInOrder';
import MapInOrderLines, { LinesData, measureRows } from './MapInOrderLines';
import {
  buildLineRows,
  closeGap,
  makePartOf,
  OrderState,
  placeAt,
  skipEpisode,
} from './mapLines';
import styles from './MapInOrderModalContent.css';

const ROW_HEIGHT = 44;

// The lines view opens its own menu; the screen has nothing to add.
function noop() {
  // Nothing to do.
}
const DRAFT_PREFIX = 'sonarr.mapInOrder.';

export interface MappedFile {
  id: number;
  episodes: Episode[];
  multipleType: MultipleType;
  multipleNumber: number;
}

interface Draft {
  seriesId: number;
  startEpisodeId?: number;
  order: string[];
  settings: Record<string, MapSetting>;
}

const KIND_OPTIONS = [
  { key: 'episode', value: () => translate('MapInOrderKindEpisode') },
  { key: 'double', value: () => translate('MapInOrderKindDouble') },
  { key: 'part', value: () => translate('MapInOrderKindPart') },
  { key: 'version', value: () => translate('MapInOrderKindVersion') },
  { key: 'special', value: () => translate('MapInOrderKindSpecial') },
  { key: 'skip', value: () => translate('MapInOrderKindSkip') },
];

const ERROR_KEYS = {
  noPrevious: 'MapInOrderErrorNoPrevious',
  pastEnd: 'MapInOrderErrorPastEnd',
  noSpecial: 'MapInOrderErrorNoSpecial',
  mixedChain: 'MapInOrderErrorMixedChain',
  pinNotFound: 'MapInOrderErrorPinNotFound',
};

function episodeCode(episode: Episode) {
  const season = `${episode.seasonNumber}`.padStart(2, '0');
  const number = `${episode.episodeNumber}`.padStart(2, '0');

  return `S${season}E${number}`;
}

function fileName(item: InteractiveImport) {
  const path = item.relativePath || item.path || '';

  return path.split(/[\\/]/).pop() || path;
}

function formatShift(shift: number) {
  return shift > 0 ? `+${shift}` : `${shift}`;
}

function byName(a: InteractiveImport, b: InteractiveImport) {
  return naturalCompare(a.relativePath || a.path, b.relativePath || b.path);
}

// The set of files a draft belongs to. A draft for one folder is never offered for another, nor for
// the same folder once files have been added or taken away.
function draftKey(series: Series, items: InteractiveImport[]) {
  const paths = items
    .map((i) => i.path)
    .sort()
    .join('|');

  let hash = 0;

  for (let i = 0; i < paths.length; i++) {
    hash = (hash * 31 + paths.charCodeAt(i)) % 2147483647;
  }

  return `${DRAFT_PREFIX}${series.tvdbId}.${items.length}.${hash}`;
}

const WRAP_KEY = `${DRAFT_PREFIX}wrapNames`;

// Remembered across sessions: it is a matter of taste, not of the files.
function readWrap() {
  try {
    return window.localStorage.getItem(WRAP_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeWrap(value: boolean) {
  try {
    window.localStorage.setItem(WRAP_KEY, `${value}`);
  } catch {
    // Not remembered, and nothing else.
  }
}

function readDraft(key: string): Draft | null {
  try {
    const stored = window.localStorage.getItem(key);

    return stored ? (JSON.parse(stored) as Draft) : null;
  } catch {
    return null;
  }
}

function writeDraft(key: string, draft: Draft | null) {
  try {
    if (draft) {
      window.localStorage.setItem(key, JSON.stringify(draft));
    } else {
      window.localStorage.removeItem(key);
    }
  } catch {
    // A browser that will not store it loses the draft, and nothing else.
  }
}

function getItemKey(index: number, data: RowData) {
  return data.order[index];
}

interface RowData {
  order: number[];
  nameNumbers: Record<number, NameNumber>;
  itemsById: Record<number, InteractiveImport>;
  settings: Record<number, MapSetting>;
  results: Record<number, MapResult>;
  rowNumberById: Record<number, number>;
  selected: Set<number>;
  dropIndex: number | null;
  specialOptions: { key: string; value: string }[];
  onSettingChange(id: number, change: Partial<MapSetting>): void;
  onSelectedChange(id: number, value: boolean, shiftKey: boolean): void;
  onDragStart(id: number): void;
  onDragOver(index: number): void;
  onDrop(): void;
  pinCodes: Record<number, string>;
  onPinChange(id: number, code: string): void;
}

function MapInOrderRow({
  index,
  style,
  data,
}: ListChildComponentProps<RowData>) {
  const {
    order,
    nameNumbers,
    itemsById,
    settings,
    results,
    rowNumberById,
    selected,
    dropIndex,
    specialOptions,
    onSettingChange,
    onSelectedChange,
    onDragStart,
    onDragOver,
    onDrop,
    pinCodes,
    onPinChange,
  } = data;

  const id = order[index];
  const item = itemsById[id];
  const setting = settings[id] ?? DEFAULT_SETTING;
  const result = results[id];
  const takesEpisode = setting.kind === 'episode' || setting.kind === 'double';

  const handleEpisodeCountChange = useCallback(
    (change: InputChanged<number | null> | FileInputChanged) => {
      const value = change.value as number | null;

      onSettingChange(id, { episodeCount: Math.max(2, value ?? 2) });
    },
    [id, onSettingChange]
  );

  const handleKindChange = useCallback(
    ({ value }: InputChanged<string>) => {
      onSettingChange(id, { kind: value as MapKind });
    },
    [id, onSettingChange]
  );

  const handleSkipChange = useCallback(
    (change: InputChanged<number | null> | FileInputChanged) => {
      const value = change.value as number | null;
      onSettingChange(id, { skipBefore: Math.max(0, value ?? 0) });
    },
    [id, onSettingChange]
  );

  const handlePinChange = useCallback(
    (change: InputChanged<string> | FileInputChanged) => {
      onPinChange(id, change.value as string);
    },
    [id, onPinChange]
  );

  const handleSpecialChange = useCallback(
    ({ value }: InputChanged<string | number>) => {
      onSettingChange(id, { specialEpisodeId: Number(value) });
    },
    [id, onSettingChange]
  );

  const handleSelectedChange = useCallback(
    ({ value, shiftKey = false }: CheckInputChanged) => {
      onSelectedChange(id, value, shiftKey);
    },
    [id, onSelectedChange]
  );

  const handleDragStart = useCallback(
    (event: React.DragEvent) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', `${id}`);
      onDragStart(id);
    },
    [id, onDragStart]
  );

  const handleDragOver = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();

      const bounds = event.currentTarget.getBoundingClientRect();
      const below = event.clientY > bounds.top + bounds.height / 2;

      onDragOver(below ? index + 1 : index);
    },
    [index, onDragOver]
  );

  const handleDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      onDrop();
    },
    [onDrop]
  );

  const marker = result
    ? getMultipleMarker(result.multipleType, result.multipleNumber)
    : null;

  return (
    <div
      className={styles.row}
      style={style}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      {dropIndex === index ? <div className={styles.dropBefore} /> : null}
      {dropIndex === index + 1 && index === order.length - 1 ? (
        <div className={styles.dropAfter} />
      ) : null}

      <div className={styles.select}>
        <CheckInput
          className={styles.checkInput}
          containerClassName={styles.checkContainer}
          name={`select-${id}`}
          value={selected.has(id)}
          onChange={handleSelectedChange}
        />
      </div>

      <div
        className={styles.handle}
        draggable={true}
        title={translate('MapInOrderDrag')}
        onDragStart={handleDragStart}
      >
        <Icon name={icons.REORDER} />
      </div>

      <div className={styles.number}>{index + 1}</div>

      <div className={styles.file} title={item?.relativePath}>
        {item ? fileName(item) : ''}
      </div>

      <div className={styles.nameNumber}>
        {nameNumbers[id]?.number ?? ''}

        {nameNumbers[id]?.shift ? (
          <Label
            kind={kinds.WARNING}
            title={translate('MapInOrderShift', {
              shift: formatShift(nameNumbers[id].shift as number),
            })}
          >
            {formatShift(nameNumbers[id].shift as number)}
          </Label>
        ) : null}
      </div>

      <div className={styles.kind}>
        <SelectInput
          className={styles.input}
          name={`kind-${id}`}
          value={setting.kind}
          values={KIND_OPTIONS}
          onChange={handleKindChange}
        />

        {setting.kind === 'double' ? (
          <NumberInput
            className={styles.countInput}
            name={`count-${id}`}
            value={setting.episodeCount ?? 2}
            min={2}
            onChange={handleEpisodeCountChange}
          />
        ) : null}
      </div>

      <div className={styles.skip}>
        {takesEpisode ? (
          <NumberInput
            className={styles.input}
            name={`skip-${id}`}
            value={setting.skipBefore || null}
            min={0}
            readOnly={setting.pinEpisodeId != null}
            onChange={handleSkipChange}
          />
        ) : null}
      </div>

      <div className={styles.pin}>
        {takesEpisode ? (
          <TextInput
            className={styles.input}
            name={`pin-${id}`}
            value={pinCodes[id] ?? ''}
            placeholder="SxxExx"
            hasError={result?.error === 'pinNotFound'}
            onChange={handlePinChange}
          />
        ) : null}

        {setting.kind === 'special' ? (
          <SelectInput
            className={styles.input}
            name={`special-${id}`}
            value={setting.specialEpisodeId ?? ''}
            values={specialOptions}
            hasError={result?.error === 'noSpecial'}
            onChange={handleSpecialChange}
          />
        ) : null}
      </div>

      <div className={styles.result}>
        {result?.skipped.length ? (
          <Label
            kind={kinds.WARNING}
            title={result.skipped.map(episodeCode).join(', ')}
          >
            {translate('MapInOrderSkipped', { count: result.skipped.length })}
          </Label>
        ) : null}

        {result?.error ? (
          <span className={styles.error}>
            {translate(ERROR_KEYS[result.error])}
          </span>
        ) : (
          <span
            className={styles.episodes}
            title={result?.episodes.map((e) => e.title).join(' / ')}
          >
            {result?.episodes.map(episodeCode).join('+')}{' '}
            {result?.episodes[0]?.title}
          </span>
        )}

        {marker ? <Label kind={kinds.INFO}>{marker}</Label> : null}

        {result?.duplicateOf == null ? null : (
          <Label kind={kinds.DANGER}>
            {translate('MapInOrderDuplicate', {
              row: rowNumberById[result.duplicateOf],
            })}
          </Label>
        )}

        {result?.episodes.some((e) => e.hasFile) &&
        result.multipleType === 'none' ? (
          <Label
            kind={kinds.DEFAULT}
            title={translate('MapInOrderReplaceHint')}
          >
            {translate('MapInOrderReplace')}
          </Label>
        ) : null}
      </div>
    </div>
  );
}

interface MapInOrderModalContentProps {
  items: InteractiveImport[];
  series: Series;
  onApply(series: Series, files: MappedFile[], skippedIds: number[]): void;
  onModalClose(): void;
}

function MapInOrderModalContent(props: MapInOrderModalContentProps) {
  const { items, series: initialSeries, onApply, onModalClose } = props;

  const allSeries: Series[] = useSelector(createAllSeriesSelector());
  const editions = useMemo(
    () => getSeriesEditions(allSeries, initialSeries),
    [allSeries, initialSeries]
  );

  const key = useMemo(
    () => draftKey(initialSeries, items),
    [initialSeries, items]
  );
  const draft = useMemo(() => readDraft(key), [key]);

  const itemsById = useMemo(() => {
    return items.reduce<Record<number, InteractiveImport>>((acc, item) => {
      acc[item.id] = item;
      return acc;
    }, {});
  }, [items]);

  // A draft stores paths rather than row ids: ids are made up afresh every time the folder is read.
  const [order, setOrder] = useState<number[]>(() => {
    const sorted = [...items].sort(byName);

    if (!draft) {
      return sorted.map((i) => i.id);
    }

    const byPath = new Map(items.map((i) => [i.path, i.id]));
    const restored = draft.order
      .map((path) => byPath.get(path))
      .filter((id): id is number => id != null);

    return restored.length === items.length
      ? restored
      : sorted.map((i) => i.id);
  });

  const [settings, setSettings] = useState<Record<number, MapSetting>>(() => {
    if (!draft) {
      return {};
    }

    return items.reduce<Record<number, MapSetting>>((acc, item) => {
      const stored = draft.settings[item.path];

      if (stored) {
        acc[item.id] = stored;
      }

      return acc;
    }, {});
  });

  const [isRestored, setIsRestored] = useState(!!draft);
  const [seriesId, setSeriesId] = useState(draft?.seriesId ?? initialSeries.id);
  const [startEpisodeId, setStartEpisodeId] = useState<number | undefined>(
    draft?.startEpisodeId
  );
  const [episodes, setEpisodes] = useState<Episode[]>([]);
  const [isFetching, setIsFetching] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [dragIds, setDragIds] = useState<number[]>([]);
  const [dropIndex, setDropIndex] = useState<number | null>(null);
  const [moveAfter, setMoveAfter] = useState<number | null>(null);
  const [pinCodes, setPinCodes] = useState<Record<number, string>>({});

  // The lines view is the default; the table stays for when a column of settings is easier to scan.
  const [view, setView] = useState<'lines' | 'table'>('lines');
  const [wrap, setWrap] = useState(readWrap);
  const linesRef = useRef<VariableSizeList<LinesData>>(null);

  const outerRef = useRef<HTMLDivElement>(null);
  const isSeeded = useRef(false);
  const listRef = useRef<List<RowData>>(null);
  const scrollTimer = useRef<number | null>(null);

  const series = allSeries.find((s) => s.id === seriesId) ?? initialSeries;

  useEffect(() => {
    setIsFetching(true);
    setFetchError(false);

    const { request, abortRequest } = createAjaxRequest({
      url: '/episode',
      data: { seriesId },
    });

    request
      .done((data: Episode[]) => {
        // With no work in progress, start from what the rows already say - after an Apply, or
        // episodes set some other way - rather than from the top. Only once: later the person's own
        // changes are what is on screen, and they are kept as a draft.
        if (!draft && !isSeeded.current) {
          isSeeded.current = true;

          const byNumber = (a: Episode, b: Episode) =>
            a.seasonNumber - b.seasonNumber ||
            a.episodeNumber - b.episodeNumber;

          const state = mapFromRows(
            items.map((item) => ({
              id: item.id,
              name: fileName(item),
              episodes: item.episodes ?? [],
              multipleType: item.multipleType,
              multipleNumber: item.multipleNumber,
            })),
            data.filter((e) => e.seasonNumber > 0).sort(byNumber),
            data.filter((e) => e.seasonNumber === 0).sort(byNumber)
          );

          if (state) {
            setOrder(state.order);
            setSettings(state.settings);
            setStartEpisodeId(state.startEpisodeId);
          }
        }

        setEpisodes(data);
        setIsFetching(false);
      })
      .fail((xhr: { aborted?: boolean }) => {
        if (!xhr.aborted) {
          setFetchError(true);
          setIsFetching(false);
        }
      });

    return abortRequest;
    // Read once per series; the rows and the draft are what the screen opened with.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seriesId]);

  // Editions keep episode rows of their own, so ids carried over from another edition mean nothing
  // here. Pins are kept as codes on screen and turned back into this edition's ids.
  const regular = useMemo(() => {
    return episodes
      .filter((e) => e.seasonNumber > 0)
      .sort(
        (a, b) =>
          a.seasonNumber - b.seasonNumber || a.episodeNumber - b.episodeNumber
      );
  }, [episodes]);

  const specials = useMemo(() => {
    return episodes
      .filter((e) => e.seasonNumber === 0)
      .sort((a, b) => a.episodeNumber - b.episodeNumber);
  }, [episodes]);

  const episodeByCode = useMemo(() => {
    return regular.reduce<Record<string, Episode>>((acc, e) => {
      acc[episodeCode(e)] = e;
      return acc;
    }, {});
  }, [regular]);

  // Codes for pins restored from a draft, once the episodes they name are known.
  useEffect(() => {
    if (!episodes.length) {
      return;
    }

    setPinCodes((current) => {
      const next = { ...current };

      Object.entries(settings).forEach(([id, setting]) => {
        if (setting.pinEpisodeId != null && next[Number(id)] == null) {
          const episode = episodes.find((e) => e.id === setting.pinEpisodeId);

          if (episode) {
            next[Number(id)] = episodeCode(episode);
          }
        }
      });

      return next;
    });
    // Only when the episodes arrive; later pins are typed and land in pinCodes directly.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [episodes]);

  const startIndex = useMemo(() => {
    if (startEpisodeId == null) {
      // The first episode the first file already landed on, if the name gave one; otherwise the top.
      const first = itemsById[order[0]]?.episodes?.[0];
      const fromName = first
        ? regular.findIndex(
            (e) =>
              e.seasonNumber === first.seasonNumber &&
              e.episodeNumber === first.episodeNumber
          )
        : -1;

      return Math.max(0, fromName);
    }

    return Math.max(
      0,
      regular.findIndex((e) => e.id === startEpisodeId)
    );
  }, [startEpisodeId, regular, itemsById, order]);

  const results = useMemo(() => {
    const mapped = mapInOrder(
      order.map((id) => ({ id, setting: settings[id] ?? DEFAULT_SETTING })),
      regular,
      startIndex,
      specials
    );

    return mapped.reduce<Record<number, MapResult>>((acc, r) => {
      acc[r.id] = r;
      return acc;
    }, {});
  }, [order, settings, regular, startIndex, specials]);

  // Where the file's own number stops moving in step with the order. A run numbered its own way is
  // usually off by a steady amount; where that amount changes is where a double episode, a missing
  // file or a special sits, and where the mapping most likely needs a hand.
  // Where the numbers in the names stop following the order - see findShifts.
  const nameNumbers = useMemo(() => {
    return findShifts(
      order.map((id) => ({
        id,
        name: itemsById[id] ? fileName(itemsById[id]) : '',
      })),
      results,
      regular
    );
  }, [order, itemsById, results, regular]);

  const rowNumberById = useMemo(() => {
    return order.reduce<Record<number, number>>((acc, id, index) => {
      acc[id] = index + 1;
      return acc;
    }, {});
  }, [order]);

  const summary = useMemo(() => {
    const all = order.map((id) => results[id]).filter(Boolean);
    const placed = all.flatMap((r) => r.episodes);
    const replaced = new Set(
      all
        .filter((r) => r.multipleType === 'none')
        .flatMap((r) => r.episodes)
        .filter((e) => e.hasFile)
        .map((e) => e.id)
    );

    return {
      first: placed.find((e) => e.seasonNumber > 0),
      last: [...placed].reverse().find((e) => e.seasonNumber > 0),
      problems: all.filter((r) => r.error).length,
      duplicates: all.filter((r) => r.duplicateOf != null).length,
      replaced: replaced.size,
    };
  }, [order, results]);

  // Kept as the person goes, so closing the window by accident costs nothing.
  useEffect(() => {
    if (isFetching) {
      return;
    }

    const timer = window.setTimeout(() => {
      writeDraft(key, {
        seriesId,
        startEpisodeId,
        order: order.map((id) => itemsById[id].path),
        settings: Object.entries(settings).reduce<Record<string, MapSetting>>(
          (acc, [id, setting]) => {
            acc[itemsById[Number(id)].path] = setting;
            return acc;
          },
          {}
        ),
      });
    }, 300);

    return () => window.clearTimeout(timer);
  }, [key, isFetching, seriesId, startEpisodeId, order, settings, itemsById]);

  const applyState = useCallback(
    (next: OrderState) => {
      setOrder(next.order);
      setSettings(next.settings);
      setPinCodes(
        Object.entries(next.settings).reduce<Record<number, string>>(
          (acc, [id, setting]) => {
            const episode = regular.find((e) => e.id === setting.pinEpisodeId);

            if (episode) {
              acc[Number(id)] = episodeCode(episode);
            }

            return acc;
          },
          {}
        )
      );
    },
    [regular]
  );

  const onSettingChange = useCallback(
    (id: number, change: Partial<MapSetting>) => {
      setSettings((current) => ({
        ...current,
        [id]: { ...(current[id] ?? DEFAULT_SETTING), ...change },
      }));
    },
    []
  );

  const onPinChange = useCallback(
    (id: number, code: string) => {
      const normalised = code.trim().toUpperCase();
      const match = /^S(\d+)E(\d+)$/.exec(normalised);
      const episode = match
        ? episodeByCode[
            `S${match[1].padStart(2, '0')}E${match[2].padStart(2, '0')}`
          ]
        : undefined;

      setPinCodes((current) => ({ ...current, [id]: code }));

      // An unfinished code is not a pin yet; one that names nothing is shown as an error.
      onSettingChange(id, {
        pinEpisodeId: normalised ? episode?.id ?? -1 : undefined,
      });
    },
    [episodeByCode, onSettingChange]
  );

  // The row last ticked or unticked, which Shift+click counts a range from.
  const anchorId = useRef<number | undefined>(undefined);

  const onSelectedChange = useCallback(
    (id: number, value: boolean, shiftKey: boolean) => {
      const anchor = shiftKey ? anchorId.current : undefined;

      setSelected((current) => selectRange(order, current, anchor, id, value));
      anchorId.current = id;
    },
    [order]
  );

  const onSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setSelected(value ? new Set(order) : new Set());
      anchorId.current = undefined;
    },
    [order]
  );

  const moveTo = useCallback((ids: number[], index: number) => {
    setOrder((current) => {
      const moving = current.filter((id) => ids.includes(id));
      const before = current.slice(0, index).filter((id) => !ids.includes(id));
      const after = current.slice(index).filter((id) => !ids.includes(id));

      return [...before, ...moving, ...after];
    });
  }, []);

  // Dragging a ticked row takes every ticked row with it, in the order they are in now.
  const onDragStart = useCallback(
    (id: number) => {
      setDragIds(selected.has(id) ? [...selected] : [id]);
    },
    [selected]
  );

  const stopScrolling = useCallback(() => {
    if (scrollTimer.current != null) {
      window.clearInterval(scrollTimer.current);
      scrollTimer.current = null;
    }
  }, []);

  const onDragOver = useCallback((index: number) => {
    setDropIndex(index);
  }, []);

  const onDrop = useCallback(() => {
    stopScrolling();

    if (dragIds.length && dropIndex != null) {
      moveTo(dragIds, dropIndex);
    }

    setDragIds([]);
    setDropIndex(null);
  }, [dragIds, dropIndex, moveTo, stopScrolling]);

  // A list this long cannot be dragged across by hand, so holding a row near either edge scrolls it.
  const onListDragOver = useCallback(
    (event: React.DragEvent) => {
      const outer = outerRef.current;

      if (!outer) {
        return;
      }

      const bounds = outer.getBoundingClientRect();
      const edge = ROW_HEIGHT * 1.5;
      let step = 0;

      if (event.clientY < bounds.top + edge) {
        step = -ROW_HEIGHT;
      } else if (event.clientY > bounds.bottom - edge) {
        step = ROW_HEIGHT;
      }

      stopScrolling();

      if (step) {
        scrollTimer.current = window.setInterval(() => {
          outer.scrollTop += step;
        }, 50);
      }
    },
    [stopScrolling]
  );

  useEffect(() => stopScrolling, [stopScrolling]);

  const onMoveAfterChange = useCallback(
    (change: InputChanged<number | null> | FileInputChanged) => {
      setMoveAfter(change.value as number | null);
    },
    []
  );

  const onMovePress = useCallback(() => {
    if (moveAfter == null || !selected.size) {
      return;
    }

    moveTo([...selected], Math.min(Math.max(moveAfter, 0), order.length));
  }, [moveAfter, selected, order.length, moveTo]);

  const onStartChange = useCallback(({ value }: InputChanged<string>) => {
    setStartEpisodeId(Number(value));
  }, []);

  const onSeriesChange = useCallback(({ value }: InputChanged<string>) => {
    setSeriesId(Number(value));
    setStartEpisodeId(undefined);
  }, []);

  const onStartOverPress = useCallback(() => {
    writeDraft(key, null);
    setOrder([...items].sort(byName).map((i) => i.id));
    setSettings({});
    setPinCodes({});
    setSelected(new Set());
    setStartEpisodeId(undefined);
    setIsRestored(false);
  }, [key, items]);

  const lineRows = useMemo(
    () => buildLineRows(order, results, regular, startIndex),
    [order, results, regular, startIndex]
  );

  const lineSizes = useMemo(
    () => measureRows(lineRows, wrap),
    [lineRows, wrap]
  );

  const names = useMemo(() => {
    return items.reduce<Record<number, string>>((acc, item) => {
      acc[item.id] = fileName(item);
      return acc;
    }, {});
  }, [items]);

  // Episodes a whole-episode file is put on that already have one: importing replaces it.
  const replaced = useMemo(() => {
    return new Set(
      order
        .map((id) => results[id])
        .filter((r) => r && r.multipleType === 'none')
        .flatMap((r) => r.episodes)
        .filter((e) => e.hasFile)
        .map((e) => e.id)
    );
  }, [order, results]);

  const onDropOnEpisode = useCallback(
    (episodeId: number) => {
      stopScrolling();

      if (dragIds.length) {
        applyState(
          placeAt({ order, settings }, dragIds, episodeId, results, regular)
        );
      }

      setDragIds([]);
    },
    [dragIds, order, settings, results, regular, applyState, stopScrolling]
  );

  // A file dropped on a file joins its set; several dropped together all join it, in their order.
  const onDropOnFile = useCallback(
    (targetId: number) => {
      stopScrolling();

      const moving = dragIds.filter((id) => id !== targetId);

      if (moving.length) {
        let next: OrderState = { order, settings };
        let last = targetId;

        moving.forEach((id) => {
          next = makePartOf(next, id, last);
          last = id;
        });

        applyState(next);
      }

      setDragIds([]);
    },
    [dragIds, order, settings, applyState, stopScrolling]
  );

  const onSkipEpisode = useCallback(
    (episodeId: number) => {
      applyState(skipEpisode({ order, settings }, episodeId, results, regular));
    },
    [order, settings, results, regular, applyState]
  );

  const onCloseGap = useCallback(
    (episodeId: number) => {
      applyState(closeGap({ order, settings }, episodeId, results, regular));
    },
    [order, settings, results, regular, applyState]
  );

  const linesData: LinesData = {
    rows: lineRows,
    regular,
    specials,
    results,
    settings,
    names,
    nameNumbers: nameNumbers.byId,
    selected,
    replaced,
    heights: lineSizes.heights,
    offsets: lineSizes.offsets,
    rowByEpisodeIndex: lineSizes.rowByEpisodeIndex,
    onOpenMenu: noop,
    onSettingChange,
    onSelectedChange,
    onDragStart,
    onDropOnEpisode,
    onDropOnFile,
    onCloseGap,
    onSkipEpisode,
    wrap,
  };

  const [shiftCursor, setShiftCursor] = useState(-1);

  const onNextShiftPress = useCallback(() => {
    const { shiftRows } = nameNumbers;

    if (!shiftRows.length) {
      return;
    }

    const next = (shiftCursor + 1) % shiftRows.length;

    setShiftCursor(next);

    if (view === 'lines') {
      const id = order[shiftRows[next]];
      const row = lineRows.findIndex(
        (r) =>
          (r.type === 'slot' && r.fileIds.includes(id)) ||
          (r.type === 'file' && r.fileId === id)
      );

      if (row >= 0) {
        linesRef.current?.scrollToItem(row, 'center');
      }
    } else {
      listRef.current?.scrollToItem(shiftRows[next], 'center');
    }
  }, [nameNumbers, shiftCursor, view, order, lineRows]);

  const onWrapChange = useCallback(({ value }: CheckInputChanged) => {
    setWrap(value);
    writeWrap(value);
  }, []);

  const onLinesViewPress = useCallback(() => setView('lines'), []);
  const onTableViewPress = useCallback(() => setView('table'), []);

  const canApply =
    !isFetching && !fetchError && summary.problems === 0 && order.length > 0;

  const onApplyPress = useCallback(() => {
    if (!canApply) {
      return;
    }

    writeDraft(key, null);

    const isSkipped = (id: number) =>
      (settings[id] ?? DEFAULT_SETTING).kind === 'skip';

    onApply(
      series,
      order
        .filter((id) => !isSkipped(id))
        .map((id) => {
          const {
            episodes: mapped,
            multipleType,
            multipleNumber,
          } = results[id];

          return { id, episodes: mapped, multipleType, multipleNumber };
        }),
      order.filter(isSkipped)
    );
  }, [canApply, key, series, order, results, settings, onApply]);

  const startOptions = useMemo(
    () =>
      regular.map((e) => ({
        key: `${e.id}`,
        value: `${episodeCode(e)} - ${e.title}`,
      })),
    [regular]
  );

  const specialOptions = useMemo(
    () => [
      { key: '', value: '' },
      ...specials.map((e) => ({
        key: `${e.id}`,
        value: `${episodeCode(e)} - ${e.title}`,
      })),
    ],
    [specials]
  );

  const editionOptions = useMemo(
    () =>
      editions.map((s) => ({
        key: `${s.id}`,
        value: seriesEditionTitle(s.title, s.editionName),
      })),
    [editions]
  );

  const rowData: RowData = {
    order,
    nameNumbers: nameNumbers.byId,
    itemsById,
    settings,
    results,
    rowNumberById,
    selected,
    dropIndex,
    specialOptions,
    onSettingChange,
    onSelectedChange,
    onDragStart,
    onDragOver,
    onDrop,
    pinCodes,
    onPinChange,
  };

  const listHeight = Math.max(240, window.innerHeight - 380);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('MapInOrder')} -{' '}
        {seriesEditionTitle(series.title, series.editionName)}
      </ModalHeader>

      <ModalBody>
        {/* One row of controls and one of counts; the explanation is on the icon, read once. */}
        <div className={styles.toolbar}>
          <span className={styles.segmented}>
            <Button
              className={styles.segment}
              kind={view === 'lines' ? kinds.PRIMARY : kinds.DEFAULT}
              onPress={onLinesViewPress}
            >
              {translate('MapInOrderViewLines')}
            </Button>
            <Button
              className={styles.segment}
              kind={view === 'table' ? kinds.PRIMARY : kinds.DEFAULT}
              onPress={onTableViewPress}
            >
              {translate('MapInOrderViewTable')}
            </Button>
          </span>

          {editions.length > 1 ? (
            <SelectInput
              className={styles.toolbarSelect}
              name="seriesId"
              value={seriesId}
              values={editionOptions}
              onChange={onSeriesChange}
            />
          ) : null}

          <label className={styles.control}>
            <span className={styles.controlLabel}>
              {translate('MapInOrderStart')}
            </span>
            <SelectInput
              className={styles.toolbarSelect}
              name="startEpisodeId"
              value={regular[startIndex]?.id ?? ''}
              values={startOptions}
              isDisabled={!regular.length}
              onChange={onStartChange}
            />
          </label>

          {view === 'lines' ? (
            <CheckInput
              containerClassName={styles.wrapToggle}
              name="wrap"
              value={wrap}
              helpText={translate('MapInOrderWrapNames')}
              onChange={onWrapChange}
            />
          ) : null}

          <span className={styles.help} title={translate('MapInOrderHelpText')}>
            <Icon name={icons.INFO} />
          </span>
        </div>

        {isFetching ? <LoadingIndicator /> : null}

        {fetchError ? (
          <div className={styles.error}>{translate('EpisodesLoadError')}</div>
        ) : null}

        {!isFetching && !fetchError ? (
          <>
            <div className={styles.summary}>
              <span>
                {translate('MapInOrderSummary', {
                  files: order.length,
                  first: summary.first ? episodeCode(summary.first) : '-',
                  last: summary.last ? episodeCode(summary.last) : '-',
                })}
              </span>

              {summary.problems ? (
                <Label kind={kinds.DANGER}>
                  {translate('MapInOrderProblems', {
                    count: summary.problems,
                  })}
                </Label>
              ) : null}

              {summary.duplicates ? (
                <Label kind={kinds.WARNING}>
                  {translate('MapInOrderDuplicates', {
                    count: summary.duplicates,
                  })}
                </Label>
              ) : null}

              {nameNumbers.shiftRows.length ? (
                <button
                  type="button"
                  className={styles.shiftButton}
                  title={translate('MapInOrderNextShift')}
                  onClick={onNextShiftPress}
                >
                  {translate('MapInOrderShifts', {
                    count: nameNumbers.shiftRows.length,
                  })}
                  <Icon name={icons.CARET_DOWN} size={10} />
                </button>
              ) : null}

              {summary.replaced ? (
                <Label kind={kinds.DEFAULT}>
                  {translate('MapInOrderReplaces', {
                    count: summary.replaced,
                  })}
                </Label>
              ) : null}

              <span className={styles.summaryEnd}>
                {view === 'table' && selected.size ? (
                  <span className={styles.control}>
                    <span className={styles.controlLabel}>
                      {translate('MapInOrderMoveAfter', {
                        count: selected.size,
                      })}
                    </span>
                    <NumberInput
                      className={styles.moveInput}
                      name="moveAfter"
                      value={moveAfter}
                      min={0}
                      max={order.length}
                      onChange={onMoveAfterChange}
                    />
                    <Button onPress={onMovePress}>
                      {translate('MapInOrderMove')}
                    </Button>
                  </span>
                ) : null}

                {isRestored ? (
                  <span className={styles.restored}>
                    {translate('MapInOrderRestored')}
                    <Link onPress={onStartOverPress}>
                      {translate('MapInOrderStartOver')}
                    </Link>
                  </span>
                ) : null}
              </span>
            </div>

            {view === 'lines' ? (
              <>
                <div className={styles.linesHeader}>
                  <span>{translate('MapInOrderFilesInOrder')}</span>
                  <span />
                  <span>{translate('Episodes')}</span>
                </div>

                <div onDragOver={onListDragOver} onDragLeave={stopScrolling}>
                  <MapInOrderLines
                    data={linesData}
                    height={listHeight}
                    listRef={linesRef}
                    outerRef={outerRef}
                  />
                </div>
              </>
            ) : null}

            {view === 'table' ? (
              <>
                <div className={styles.header}>
                  <div className={styles.select}>
                    <CheckInput
                      name="selectAll"
                      // Neither true nor false draws the box as partly ticked.
                      value={
                        selected.size > 0 && selected.size < order.length
                          ? 'some'
                          : order.length > 0 && selected.size === order.length
                      }
                      onChange={onSelectAllChange}
                    />
                  </div>
                  <div />
                  <div>#</div>
                  <div>{translate('MapInOrderFile')}</div>
                  <div>{translate('MapInOrderNameNumber')}</div>
                  <div>{translate('MapInOrderKind')}</div>
                  <div>{translate('MapInOrderSkip')}</div>
                  <div>{translate('MapInOrderPin')}</div>
                  <div>{translate('MapInOrderResult')}</div>
                </div>

                <div onDragOver={onListDragOver} onDragLeave={stopScrolling}>
                  <List<RowData>
                    ref={listRef}
                    outerRef={outerRef}
                    height={listHeight}
                    width="100%"
                    itemCount={order.length}
                    itemSize={ROW_HEIGHT}
                    itemData={rowData}
                    itemKey={getItemKey}
                  >
                    {MapInOrderRow}
                  </List>
                </div>
              </>
            ) : null}
          </>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button
          kind={kinds.SUCCESS}
          isDisabled={!canApply}
          onPress={onApplyPress}
        >
          {translate('MapInOrderApply')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default MapInOrderModalContent;

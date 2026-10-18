import { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { cloneDeep, without } from 'lodash';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { batchActions } from 'redux-batched-actions';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import InteractiveImportAppState from 'App/State/InteractiveImportAppState';
import * as commandNames from 'Commands/commandNames';
import SelectInput from 'Components/Form/SelectInput';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import SelectedMenuItem from 'Components/Menu/SelectedMenuItem';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { EpisodeFile } from 'EpisodeFile/EpisodeFile';
import usePrevious from 'Helpers/Hooks/usePrevious';
import useSelectState from 'Helpers/Hooks/useSelectState';
import { align, icons, kinds, scrollDirections } from 'Helpers/Props';
import SelectCustomFormatModal from 'InteractiveImport/CustomFormat/SelectCustomFormatModal';
import SelectEditionModal from 'InteractiveImport/Edition/SelectEditionModal';
import SelectEpisodeModal from 'InteractiveImport/Episode/SelectEpisodeModal';
import { SelectedEpisode } from 'InteractiveImport/Episode/SelectEpisodeModalContent';
import ImportMode from 'InteractiveImport/ImportMode';
import SelectIndexerFlagsModal from 'InteractiveImport/IndexerFlags/SelectIndexerFlagsModal';
import InteractiveImport, {
  InteractiveImportCommandOptions,
} from 'InteractiveImport/InteractiveImport';
import SelectLanguageModal from 'InteractiveImport/Language/SelectLanguageModal';
import MapInOrderModal from 'InteractiveImport/MapInOrder/MapInOrderModal';
import { MappedFile } from 'InteractiveImport/MapInOrder/MapInOrderModalContent';
import findUnmarkedSharedFiles from 'InteractiveImport/Multiple/findUnmarkedSharedFiles';
import getMultipleStates, {
  MultipleState,
} from 'InteractiveImport/Multiple/getMultipleStates';
import SelectMultipleModal from 'InteractiveImport/Multiple/SelectMultipleModal';
import MultipleType from 'InteractiveImport/MultipleType';
import SelectNamingLanguagesModal from 'InteractiveImport/NamingLanguages/SelectNamingLanguagesModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import SelectReleaseGroupModal from 'InteractiveImport/ReleaseGroup/SelectReleaseGroupModal';
import SelectReleaseTypeModal from 'InteractiveImport/ReleaseType/SelectReleaseTypeModal';
import SelectSeasonModal from 'InteractiveImport/Season/SelectSeasonModal';
import SelectSeriesModal from 'InteractiveImport/Series/SelectSeriesModal';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import getSeriesEditions from 'Series/getSeriesEditions';
import Series from 'Series/Series';
import { executeCommand } from 'Store/Actions/commandActions';
import {
  deleteEpisodeFiles,
  updateEpisodeFiles,
} from 'Store/Actions/episodeFileActions';
import {
  cancelCheckInteractiveImportItems,
  checkInteractiveImportItems,
  clearInteractiveImport,
  fetchInteractiveImportItems,
  reprocessInteractiveImportItems,
  setInteractiveImportMode,
  setInteractiveImportSort,
  updateInteractiveImportItem,
  updateInteractiveImportItems,
} from 'Store/Actions/interactiveImportActions';
import { fetchNamingSettings } from 'Store/Actions/settingsActions';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createMultipleFilesEnabledSelector from 'Store/Selectors/createMultipleFilesEnabledSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import { SortCallback } from 'typings/callbacks';
import { SelectStateInputProps } from 'typings/props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import hasDifferentItems from 'Utilities/Object/hasDifferentItems';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import InteractiveImportRow from './InteractiveImportRow';
import styles from './InteractiveImportModalContent.css';

type SelectType =
  | 'select'
  | 'series'
  | 'edition'
  | 'season'
  | 'episode'
  | 'releaseGroup'
  | 'quality'
  | 'language'
  | 'namingLanguages'
  | 'customFormats'
  | 'indexerFlags'
  | 'releaseType'
  // Reached from its own button rather than the Select... list, so it is deliberately absent from
  // bulkSelectOptions below.
  | 'multiple'
  | 'mapInOrder'
  // Not a modal: it starts reading the selected rows again, a batch at a time.
  | 'check';

type FilterExistingFiles = 'all' | 'new';

interface BulkAction {
  key: SelectType;
  label: string;
  icon: IconDefinition;
  isDisabled?: boolean;
}

interface BulkActionButtonProps {
  action: BulkAction;
  onPress(key: SelectType): void;
}

// Its own component so each button carries its own handler without one being made during render.
function BulkActionButton({ action, onPress }: BulkActionButtonProps) {
  const handlePress = useCallback(() => {
    onPress(action.key);
  }, [action.key, onPress]);

  return (
    <Button
      className={styles.bulkAction}
      title={action.label}
      onPress={handlePress}
    >
      <Icon name={action.icon} />
    </Button>
  );
}

const MULTIPLE_FILTERS: {
  state: MultipleState;
  label: string;
  kind: 'danger' | 'warning' | 'success';
}[] = [
  { state: 'undecided', label: 'MultipleFilterUndecided', kind: kinds.DANGER },
  { state: 'pending', label: 'MultipleFilterPending', kind: kinds.WARNING },
  { state: 'saved', label: 'MultipleFilterSaved', kind: kinds.SUCCESS },
];

interface MultipleFilterButtonProps {
  state: MultipleState | null;
  label: string;
  kind?: 'danger' | 'warning' | 'success';
  count?: number;
  isActive?: boolean;
  onPress(state: MultipleState | null): void;
}

// A count that is also the way to the rows it counts. Nothing to press when it counts nothing.
function MultipleFilterButton({
  state,
  label,
  kind,
  count,
  isActive = false,
  onPress,
}: MultipleFilterButtonProps) {
  const handlePress = useCallback(() => {
    onPress(state);
  }, [state, onPress]);

  return (
    <Link
      className={isActive ? styles.multipleFilterActive : styles.multipleFilter}
      isDisabled={count === 0}
      onPress={handlePress}
    >
      {label}

      {count == null ? null : (
        <Label
          className={styles.multipleFilterCount}
          kind={count ? kind : kinds.DISABLED}
        >
          {count}
        </Label>
      )}
    </Link>
  );
}

// TODO: This feels janky to do, but not sure of a better way currently
type OnSelectedChangeCallback = React.ComponentProps<
  typeof InteractiveImportRow
>['onSelectedChange'];

const COLUMNS = [
  {
    name: 'relativePath',
    label: () => translate('RelativePath'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'series',
    label: () => translate('Series'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'season',
    label: () => translate('Season'),
    isVisible: true,
  },
  {
    name: 'episodes',
    label: () => translate('Episodes'),
    isVisible: true,
  },
  {
    name: 'releaseGroup',
    label: () => translate('ReleaseGroup'),
    isVisible: true,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'namingLanguages',
    label: () => translate('NamingLanguages'),
    isVisible: true,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'releaseType',
    label: () => translate('ReleaseType'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'multiple',
    label: () => translate('MultipleTypeLabel'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: React.createElement(Icon, {
      name: icons.INTERACTIVE,
      title: () => translate('CustomFormatScore'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'indexerFlags',
    label: React.createElement(Icon, {
      name: icons.FLAG,
      title: () => translate('IndexerFlags'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'rejections',
    label: React.createElement(Icon, {
      name: icons.DANGER,
      kind: kinds.DANGER,
      title: () => translate('Rejections'),
    }),
    isSortable: true,
    isVisible: true,
  },
];

const importModeOptions = [
  {
    key: 'chooseImportMode',
    value: () => translate('ChooseImportMode'),
    disabled: true,
  },
  {
    key: 'move',
    value: () => translate('MoveFiles'),
  },
  {
    key: 'copy',
    value: () => translate('HardlinkCopyFiles'),
  },
];

function isSameEpisodeFile(
  file: InteractiveImport,
  originalFile?: InteractiveImport
) {
  const { series, seasonNumber, episodes } = file;

  if (!originalFile) {
    return false;
  }

  if (!originalFile.series || series?.id !== originalFile.series.id) {
    return false;
  }

  if (seasonNumber !== originalFile.seasonNumber) {
    return false;
  }

  return !hasDifferentItems(originalFile.episodes, episodes);
}

const episodeFilesInfoSelector = createSelector(
  (state: AppState) => state.episodeFiles.isDeleting,
  (state: AppState) => state.episodeFiles.deleteError,
  (isDeleting, deleteError) => {
    return {
      isDeleting,
      deleteError,
    };
  }
);

const importModeSelector = createSelector(
  (state: AppState) => state.interactiveImport.importMode,
  (importMode) => {
    return importMode;
  }
);

interface InteractiveImportModalContentProps {
  downloadId?: string;
  downloadIds?: string[];
  seriesId?: number;
  seasonNumber?: number;
  showSeries?: boolean;
  allowSeriesChange?: boolean;
  showDelete?: boolean;
  showImportMode?: boolean;
  showFilterExistingFiles?: boolean;
  title?: string;
  folder?: string;
  sortKey?: string;
  sortDirection?: string;
  initialSortKey?: string;
  initialSortDirection?: string;
  modalTitle: string;
  onModalClose(): void;
}

function InteractiveImportModalContent(
  props: InteractiveImportModalContentProps
) {
  const {
    downloadId,
    downloadIds,
    seriesId,
    seasonNumber,
    allowSeriesChange = true,
    showSeries = true,
    showFilterExistingFiles = false,
    showDelete = false,
    showImportMode = true,
    title,
    folder,
    initialSortKey,
    initialSortDirection,
    modalTitle,
    onModalClose,
  } = props;

  const {
    isFetching,
    isPopulated,
    error,
    items,
    originalItems,
    sortKey,
    sortDirection,
    checkProgress,
  }: InteractiveImportAppState = useSelector(
    createClientSideCollectionSelector('interactiveImport')
  );

  const { isDeleting, deleteError } = useSelector(episodeFilesInfoSelector);
  const importMode = useSelector(importModeSelector);
  const isMultipleEnabled = useSelector(createMultipleFilesEnabledSelector());

  const [invalidRowsSelected, setInvalidRowsSelected] = useState<number[]>([]);
  const [
    withoutEpisodeFileIdRowsSelected,
    setWithoutEpisodeFileIdRowsSelected,
  ] = useState<number[]>([]);
  const [selectModalOpen, setSelectModalOpen] = useState<SelectType | null>(
    null
  );
  const [isConfirmImportModeOpen, setIsConfirmImportModeOpen] = useState(false);
  const [isConfirmDeleteModalOpen, setIsConfirmDeleteModalOpen] =
    useState(false);
  const [filterExistingFiles, setFilterExistingFiles] = useState(false);
  const [multipleFilter, setMultipleFilter] = useState<MultipleState | null>(
    null
  );
  const [interactiveImportErrorMessage, setInteractiveImportErrorMessage] =
    useState<string | null>(null);
  const [selectState, setSelectState] = useSelectState();
  const { allSelected, allUnselected, selectedState } = selectState;
  const previousIsDeleting = usePrevious(isDeleting);
  const dispatch = useDispatch();
  const allSeries: Series[] = useSelector(createAllSeriesSelector());
  const { interactiveImportInlineActions } = useSelector(
    createUISettingsSelector()
  );

  const columns: Column[] = useMemo(() => {
    const result: Column[] = cloneDeep(COLUMNS);

    if (!showSeries) {
      const seriesColumn = result.find((c) => c.name === 'series');

      if (seriesColumn) {
        seriesColumn.isVisible = false;
      }
    }

    const showIndexerFlags = items.some((item) => item.indexerFlags);

    if (!showIndexerFlags) {
      const indexerFlagsColumn = result.find((c) => c.name === 'indexerFlags');

      if (indexerFlagsColumn) {
        indexerFlagsColumn.isVisible = false;
      }
    }

    return result;
  }, [showSeries, items]);

  const selectedIds: number[] = useMemo(() => {
    return getSelectedIds(selectedState);
  }, [selectedState]);

  // In a season of a few hundred files the handful that are parts or versions are otherwise found by
  // scrolling, and nothing says which of them are stored and which are only read off the name.
  const multipleStates = useMemo(() => getMultipleStates(items), [items]);

  // Falls back to everything once nothing is left to show, so the last file of a kind being dealt
  // with does not leave an empty table with the way back hidden.
  const activeMultipleFilter =
    multipleFilter && multipleStates[multipleFilter] > 0
      ? multipleFilter
      : null;

  // The files of an episode are shown together, whatever the table is sorted by: a file that says
  // nothing about itself is only a question beside the one it shares the episode with, so that one
  // comes along even when it has its own marker.
  const visibleItems = useMemo(() => {
    if (!activeMultipleFilter) {
      return items;
    }

    const { byId, groupById } = multipleStates;
    const groups = new Set(
      items
        .filter((item) => byId[item.id] === activeMultipleFilter)
        .map((item) => groupById[item.id])
    );

    return items
      .filter((item) => groups.has(groupById[item.id]))
      .sort((a, b) =>
        groupById[a.id].localeCompare(groupById[b.id], undefined, {
          numeric: true,
        })
      );
  }, [items, activeMultipleFilter, multipleStates]);

  // Rows whose episodes were worked out from the file name can land on the same episode more than
  // once, which is what a split episode looks like before anyone says so. Grouped here so the action
  // can be offered only when there is something to act on.
  const multipleGroups = useMemo(() => {
    const groups = new Map<string, number[]>();

    items.forEach((item) => {
      if (!selectedIds.includes(item.id) || !item.episodes?.length) {
        return;
      }

      const key = item.episodes
        .map((episode) => episode.id)
        .sort((a, b) => a - b)
        .join(',');

      groups.set(key, [...(groups.get(key) ?? []), item.id]);
    });

    return [...groups.values()].filter((ids) => ids.length > 1);
  }, [items, selectedIds]);

  // Selected rows already carrying a part or version. Taking a marker off needs no group - a file that
  // was wrongly read as a version is usually the only file of its episode - so these are offered the
  // action on their own.
  const markedSelectedIds = useMemo(() => {
    return items
      .filter(
        (item) =>
          selectedIds.includes(item.id) &&
          !!item.multipleType &&
          item.multipleType !== 'none'
      )
      .map((item) => item.id);
  }, [items, selectedIds]);

  // Editions belong to one series, so the bulk action only means anything when everything selected is
  // the same series. Offering it across a mixed selection would ask one question of several answers.
  const commonSeries = useMemo(() => {
    const selected = items.filter((item) => selectedIds.includes(item.id));
    const tvdbIds = new Set(selected.map((item) => item.series?.tvdbId));

    return tvdbIds.size === 1 ? selected[0]?.series : undefined;
  }, [items, selectedIds]);

  // The one series the selection is for, counting only the rows that have one. Map in Order hands
  // every file a series along with its episode, so rows the name told nothing about can come along.
  const mapSeries = useMemo(() => {
    const selected = items.filter(
      (item) => selectedIds.includes(item.id) && item.series
    );
    const ids = new Set(selected.map((item) => item.series?.tvdbId));

    return ids.size === 1 ? selected[0].series : undefined;
  }, [items, selectedIds]);

  // Importing these would have one replace the other. Refused up front rather than found out
  // afterwards, with the red count in the bar above as the way to them.
  const unmarkedSharedIds = useMemo(
    () => findUnmarkedSharedFiles(items, selectedIds),
    [items, selectedIds]
  );

  const mapItems = useMemo(
    () => items.filter((item) => selectedIds.includes(item.id)),
    [items, selectedIds]
  );

  // Shown only while they can be used, the way Mark as Multiple already behaves. A greyed row of
  // twelve costs the width whether or not any of them can do anything, and this bar has to share its
  // line with the import mode and the Import button.
  const bulkActions = useMemo<BulkAction[]>(() => {
    if (!selectedIds.length) {
      return [];
    }

    const { seasonSelectDisabled, episodeSelectDisabled } = items.reduce(
      (acc, item) => {
        if (!selectedIds.includes(item.id)) {
          return acc;
        }

        const lastSelectedSeason = acc.lastSelectedSeason;

        acc.seasonSelectDisabled ||= !item.series;
        acc.episodeSelectDisabled ||=
          item.seasonNumber === undefined ||
          (lastSelectedSeason >= 0 && item.seasonNumber !== lastSelectedSeason);
        acc.lastSelectedSeason = item.seasonNumber ?? -1;

        return acc;
      },
      {
        seasonSelectDisabled: false,
        episodeSelectDisabled: false,
        lastSelectedSeason: -1,
      }
    );

    const actions: BulkAction[] = [
      {
        key: 'edition',
        label: translate('SelectEdition'),
        icon: icons.CLONE,

        // Nothing to choose between unless the selection is all one series and that series has more
        // than the one edition. Choosing the edition is choosing the series, so where the series is
        // fixed - Manage Episodes, opened from the series itself - it is not offered either.
        isDisabled:
          !allowSeriesChange ||
          getSeriesEditions(allSeries, commonSeries).length <= 1,
      },
      {
        key: 'season',
        label: translate('SelectSeason'),
        icon: icons.CALENDAR,
        isDisabled: seasonSelectDisabled,
      },
      {
        key: 'episode',
        label: translate('SelectEpisodes'),
        icon: icons.EPISODE_FILE,
        isDisabled: episodeSelectDisabled,
      },
      {
        key: 'quality',
        label: translate('SelectQuality'),
        icon: icons.MEDIA_INFO,
      },
      {
        key: 'releaseGroup',
        label: translate('SelectReleaseGroup'),
        icon: icons.GROUP,
      },
      {
        key: 'language',
        label: translate('SelectLanguage'),
        icon: icons.LANGUAGE,
      },
      {
        key: 'namingLanguages',
        label: translate('SelectNamingLanguages'),
        icon: icons.SUBTITLE,
      },
      {
        key: 'customFormats',
        label: translate('SelectCustomFormats'),
        icon: icons.INTERACTIVE,
      },
      {
        key: 'indexerFlags',
        label: translate('SelectIndexerFlags'),
        icon: icons.FLAG,
      },
      {
        key: 'releaseType',
        label: translate('SelectReleaseType'),
        icon: icons.TAGS,
      },
      {
        key: 'check',
        label: translate('CheckSelectedFiles'),
        icon: icons.REFRESH,
      },
      {
        key: 'mapInOrder',
        label: translate('MapInOrder'),
        icon: icons.SORT,
        isDisabled: !mapSeries || selectedIds.length < 2,
      },
    ];

    if (allowSeriesChange) {
      actions.unshift({
        key: 'series',
        label: translate('SelectSeries'),
        icon: icons.ORGANIZE,
      });
    }

    return actions.filter((a) => !a.isDisabled);
  }, [
    allowSeriesChange,
    allSeries,
    commonSeries,
    mapSeries,
    items,
    selectedIds,
  ]);

  // The menu wants the same actions as a list, with a heading of its own that the buttons do not need.
  const bulkSelectOptions = useMemo(() => {
    return [
      {
        key: 'select',
        value: translate('SelectDropdown'),
        disabled: true,
      },
      ...bulkActions.map((action) => ({
        key: action.key,
        value: action.label,
      })),
    ];
  }, [bulkActions]);

  const onBulkActionPress = useCallback(
    (key: SelectType) => {
      if (key === 'check') {
        dispatch(checkInteractiveImportItems({ ids: selectedIds }));
        return;
      }

      setSelectModalOpen(key);
    },
    [selectedIds, dispatch]
  );

  const onCancelCheckPress = useCallback(() => {
    dispatch(cancelCheckInteractiveImportItems());
  }, [dispatch]);

  const onSelectModalSelect = useCallback<
    ({ value }: { value: SelectType }) => void
  >(
    ({ value }) => {
      onBulkActionPress(value);
    },
    [onBulkActionPress]
  );
  useEffect(
    () => {
      if (initialSortKey) {
        const sortProps: { sortKey: string; sortDirection?: string } = {
          sortKey: initialSortKey,
        };

        if (initialSortDirection) {
          sortProps.sortDirection = initialSortDirection;
        }

        dispatch(setInteractiveImportSort(sortProps));
      }

      // Needed to know whether parts can be named apart, which decides whether marking them is
      // offered at all. Nothing else in this view has a reason to have loaded it.
      dispatch(fetchNamingSettings());

      dispatch(
        fetchInteractiveImportItems({
          downloadId,
          downloadIds: downloadIds?.join(','),
          seriesId,
          seasonNumber,
          folder,
          filterExistingFiles,
        })
      );

      // returned function will be called on component unmount
      return () => {
        dispatch(clearInteractiveImport());
      };
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  useEffect(() => {
    if (!isDeleting && previousIsDeleting && !deleteError) {
      onModalClose();
    }
  }, [previousIsDeleting, isDeleting, deleteError, onModalClose]);

  const onSelectAllChange = useCallback(
    ({ value }: SelectStateInputProps) => {
      setSelectState({
        type: value ? 'selectAll' : 'unselectAll',
        items: visibleItems,
      });
    },
    [visibleItems, setSelectState]
  );

  const onSelectedChange = useCallback<OnSelectedChangeCallback>(
    ({ id, value, hasEpisodeFileId, shiftKey = false }) => {
      setSelectState({
        type: 'toggleSelected',
        items: visibleItems,
        id,
        isSelected: value,
        shiftKey,
      });

      // From the latest list rather than the one this callback was made with: many rows report in
      // the same render, and each would otherwise undo the one before.
      setWithoutEpisodeFileIdRowsSelected((current) => {
        if (hasEpisodeFileId || !value) {
          return current.includes(id) ? without(current, id) : current;
        }

        return current.includes(id) ? current : [...current, id];
      });
    },
    [visibleItems, setSelectState, setWithoutEpisodeFileIdRowsSelected]
  );

  // The selection is dropped with the rows: the bar along the bottom acts on everything ticked, and a
  // tick on a row that can no longer be seen would be acted on without anyone knowing it was there.
  const onMultipleFilterPress = useCallback(
    (state: MultipleState | null) => {
      setSelectState({ type: 'unselectAll', items });
      setWithoutEpisodeFileIdRowsSelected([]);
      setInvalidRowsSelected([]);
      setMultipleFilter(state === activeMultipleFilter ? null : state);
    },
    [
      items,
      activeMultipleFilter,
      setSelectState,
      setWithoutEpisodeFileIdRowsSelected,
      setInvalidRowsSelected,
      setMultipleFilter,
    ]
  );

  // Rows report here from an effect, so a change to many rows at once - Map in Order, or any bulk
  // edit - has them all report in the same render. Each works from the latest list: working from the
  // one the callback was made with, every report undid the one before it, rows that had become valid
  // stayed counted as invalid, and Import stayed disabled with nothing to show why.
  const onValidRowChange = useCallback(
    (id: number, isValid: boolean) => {
      setInvalidRowsSelected((current) => {
        if (isValid) {
          return current.includes(id) ? without(current, id) : current;
        }

        return current.includes(id) ? current : [...current, id];
      });
    },
    [setInvalidRowsSelected]
  );

  const onDeleteSelectedPress = useCallback(() => {
    setIsConfirmDeleteModalOpen(true);
  }, [setIsConfirmDeleteModalOpen]);

  const onConfirmDelete = useCallback(() => {
    setIsConfirmDeleteModalOpen(false);

    const episodeFileIds = items.reduce((acc: number[], item) => {
      if (selectedIds.indexOf(item.id) > -1 && item.episodeFileId) {
        acc.push(item.episodeFileId);
      }

      return acc;
    }, []);

    dispatch(deleteEpisodeFiles({ episodeFileIds }));
  }, [items, selectedIds, setIsConfirmDeleteModalOpen, dispatch]);

  const onConfirmDeleteModalClose = useCallback(() => {
    setIsConfirmDeleteModalOpen(false);
  }, [setIsConfirmDeleteModalOpen]);

  const onImportSelectedPress = useCallback(() => {
    const finalImportMode =
      downloadId || downloadIds?.length || !showImportMode
        ? 'auto'
        : importMode;

    const existingFiles: Partial<EpisodeFile>[] = [];
    const files: InteractiveImportCommandOptions[] = [];

    if (finalImportMode === 'chooseImportMode') {
      setInteractiveImportErrorMessage(
        translate('InteractiveImportNoImportMode')
      );

      return;
    }

    items.forEach((item) => {
      const isSelected = selectedIds.indexOf(item.id) > -1;

      if (isSelected) {
        const {
          series,
          seasonNumber,
          episodes,
          releaseGroup,
          quality,
          languages,
          namingAudioLanguages,
          namingSubtitleLanguages,
          manualCustomFormats,
          excludedCustomFormats,
          indexerFlags,
          episodeFileId,
          releaseType,
          multipleType,
          multipleNumber,
          multipleFromName,
        } = item;

        if (!series) {
          setInteractiveImportErrorMessage(
            translate('InteractiveImportNoSeries')
          );
          return;
        }

        if (isNaN(seasonNumber)) {
          setInteractiveImportErrorMessage(
            translate('InteractiveImportNoSeason')
          );
          return;
        }

        if (!episodes || !episodes.length) {
          setInteractiveImportErrorMessage(
            translate('InteractiveImportNoEpisode')
          );
          return;
        }

        if (!quality) {
          setInteractiveImportErrorMessage(
            translate('InteractiveImportNoQuality')
          );
          return;
        }

        if (!languages) {
          setInteractiveImportErrorMessage(
            translate('InteractiveImportNoLanguage')
          );
          return;
        }

        setInteractiveImportErrorMessage(null);

        if (episodeFileId) {
          const originalItem = originalItems.find((i) => i.id === item.id);

          if (isSameEpisodeFile(item, originalItem)) {
            existingFiles.push({
              id: episodeFileId,
              releaseGroup,
              quality,
              languages,
              namingAudioLanguages,
              namingSubtitleLanguages,
              manualCustomFormats,
              excludedCustomFormats,
              indexerFlags,
              releaseType,

              // Carried with the rest. Without them a marker set on a row in this screen was shown,
              // then dropped on Import, because this edit was the only thing sent for the file.
              multipleType,
              multipleNumber,
            });

            return;
          }
        }

        files.push({
          path: item.path,
          folderName: item.folderName,
          seriesId: series.id,
          episodeIds: episodes.map((e) => e.id),
          releaseGroup,
          quality,
          languages,
          namingAudioLanguages,
          namingSubtitleLanguages,
          manualCustomFormats,
          excludedCustomFormats,
          indexerFlags,
          releaseType,
          multipleType,
          multipleNumber,
          multipleFromName,

          // The row's own download, not the screen's: opened from the queue for several at once there
          // is no single one, and without it the import is treated as though nobody downloaded the
          // file - no history against the release, and the download client never told it finished.
          downloadId: item.downloadId ?? downloadId,
          episodeFileId,
        });
      }
    });

    let shouldClose = false;

    if (existingFiles.length) {
      dispatch(
        updateEpisodeFiles({
          files: existingFiles,
        })
      );

      shouldClose = true;
    }

    if (files.length) {
      dispatch(
        executeCommand({
          name: commandNames.INTERACTIVE_IMPORT,
          files,
          importMode: finalImportMode,
        })
      );

      shouldClose = true;
    }

    if (shouldClose) {
      onModalClose();
    }
  }, [
    downloadId,
    downloadIds,
    showImportMode,
    importMode,
    items,
    originalItems,
    selectedIds,
    onModalClose,
    dispatch,
  ]);

  // Picking a folder from Wanted has nothing to say whether a torrent is still seeding the files, and
  // the mode is remembered from last time. Moving them out from under a torrent breaks it, so the
  // mode is said back before anything is imported. Imports from the queue choose for themselves and
  // are not asked.
  const isImportModeChosenByHand =
    showImportMode && !downloadId && !downloadIds?.length;

  const onImportPress = useCallback(() => {
    if (
      isImportModeChosenByHand &&
      (importMode === 'move' || importMode === 'copy')
    ) {
      setIsConfirmImportModeOpen(true);
      return;
    }

    onImportSelectedPress();
  }, [isImportModeChosenByHand, importMode, onImportSelectedPress]);

  const onConfirmImportMode = useCallback(() => {
    setIsConfirmImportModeOpen(false);
    onImportSelectedPress();
  }, [onImportSelectedPress]);

  const onConfirmImportModeClose = useCallback(() => {
    setIsConfirmImportModeOpen(false);
  }, []);

  const onSortPress = useCallback<SortCallback>(
    (sortKey, sortDirection) => {
      dispatch(setInteractiveImportSort({ sortKey, sortDirection }));
    },
    [dispatch]
  );

  const onFilterExistingFilesChange = useCallback<
    (value: FilterExistingFiles) => void
  >(
    (value) => {
      const filter = value !== 'all';

      setFilterExistingFiles(filter);

      dispatch(
        fetchInteractiveImportItems({
          downloadId,
          downloadIds: downloadIds?.join(','),
          seriesId,
          folder,
          filterExistingFiles: filter,
        })
      );
    },
    [
      downloadId,
      downloadIds,
      seriesId,
      folder,
      setFilterExistingFiles,
      dispatch,
    ]
  );

  const onImportModeChange = useCallback<
    ({ value }: { value: ImportMode }) => void
  >(
    ({ value }) => {
      dispatch(setInteractiveImportMode({ importMode: value }));
    },
    [dispatch]
  );

  const onMarkAsMultiplePress = useCallback(() => {
    setSelectModalOpen('multiple');
  }, [setSelectModalOpen]);

  // The numbers come from the order the files sit in the table, so only the type is asked for. A file
  // already carrying a marker keeps nothing of it: the whole group is renumbered from one.
  const onMarkAsMultipleSelect = useCallback(
    (multipleType: MultipleType) => {
      const existingFiles: Partial<EpisodeFile>[] = [];

      // Numbering only means something within a group, but clearing does not: 'none' reaches every
      // selected row that has a marker, grouped or not.
      const targets =
        multipleType === 'none'
          ? [...new Set([...multipleGroups.flat(), ...markedSelectedIds])].map(
              (id) => [id]
            )
          : multipleGroups;

      targets.forEach((ids) => {
        ids.forEach((id, index) => {
          const multipleNumber = multipleType === 'none' ? 0 : index + 1;

          dispatch(
            updateInteractiveImportItem({
              id,
              multipleType,
              multipleNumber,
            })
          );

          const episodeFileId = items.find(
            (item) => item.id === id
          )?.episodeFileId;

          if (episodeFileId) {
            existingFiles.push({
              id: episodeFileId,
              multipleType,
              multipleNumber,
            });
          }
        });
      });

      // A file already in the library is not waiting to be imported, so the marker is saved against
      // the file here, the way marking a single row does. Left to the Import button it was lost: a row
      // whose episodes have not changed is saved as an edit rather than imported again, and the edit
      // did not carry the marker.
      if (existingFiles.length) {
        dispatch(updateEpisodeFiles({ files: existingFiles }));
      }

      setSelectModalOpen(null);
    },
    [multipleGroups, markedSelectedIds, items, dispatch, setSelectModalOpen]
  );

  const onSelectModalClose = useCallback(() => {
    setSelectModalOpen(null);
  }, [setSelectModalOpen]);

  const onEditionSelect = useCallback(
    (edition: Series) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          series: edition,
          editionUnconfirmed: false,
          seasonNumber: undefined,
          episodes: [],
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch, setSelectModalOpen]
  );

  // Same follow-on the row does: naming a series with editions leaves the second question open, so it
  // is asked while the person is still choosing rather than left to be found in the table.
  const onSeriesSelect = useCallback(
    (series: Series) => {
      const hasEditions = getSeriesEditions(allSeries, series).length > 1;

      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          series,
          editionUnconfirmed: hasEditions,
          seasonNumber: undefined,
          episodes: [],
        })
      );

      if (hasEditions) {
        setSelectModalOpen('edition');
        return;
      }

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, allSeries, setSelectModalOpen, dispatch]
  );

  const onSeasonSelect = useCallback(
    (seasonNumber: number) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          seasonNumber,
          episodes: [],
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, setSelectModalOpen, dispatch]
  );

  // Written onto the rows in one go and not reprocessed: a thousand files read again over a slow mount
  // takes most of an hour, and the import checks every file itself anyway. What reprocessing would
  // have filled in for a row the name told nothing about - the series' language - is filled in here.
  const onMapInOrderApply = useCallback(
    (series: Series, files: MappedFile[]) => {
      dispatch(
        batchActions(
          files.map(({ id, episodes, multipleType, multipleNumber }) => {
            const item = items.find((i) => i.id === id);
            const languages = item?.languages ?? [];
            const knowsLanguage = languages.some((l) => l.id > 0);

            return updateInteractiveImportItem({
              id,
              series,
              editionUnconfirmed: false,
              seasonNumber: episodes[0]?.seasonNumber,
              episodes,
              multipleType,
              multipleNumber,
              multipleFromName: false,
              ...(knowsLanguage || !series.originalLanguage
                ? {}
                : { languages: [series.originalLanguage] }),
            });
          })
        )
      );

      setSelectModalOpen(null);
    },
    [items, dispatch]
  );

  const onEpisodesSelect = useCallback(
    (selectedEpisodes: SelectedEpisode[]) => {
      selectedEpisodes.forEach((selectedEpisode) => {
        const { id, episodes, multipleType, multipleNumber } = selectedEpisode;

        dispatch(
          updateInteractiveImportItem({
            id,
            episodes,
            // Only carried by the parts flow, which says 'none' for the episodes it decided are not
            // split. The ordinary path sends neither and leaves whatever each row holds alone.
            ...(multipleType
              ? { multipleType, multipleNumber: multipleNumber ?? 0 }
              : {}),
          })
        );
      });

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, setSelectModalOpen, dispatch]
  );

  const onReleaseGroupSelect = useCallback(
    (releaseGroup: string) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          releaseGroup,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch]
  );

  const onLanguagesSelect = useCallback(
    (newLanguages: Language[]) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          languages: newLanguages,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch]
  );

  // The rows waiting to be imported carry this until Import is pressed, but rows that are already in
  // the library are not waiting for anything - pressing Import would add a second row for the same
  // file - so those are written straight to the file, exactly as the row-level editor does.
  const selectedEpisodeFileIds = useMemo(
    () =>
      items
        .filter((item) => selectedIds.includes(item.id) && item.episodeFileId)
        .map((item) => item.episodeFileId as number),
    [items, selectedIds]
  );

  // Neither of these changes what a release is, so unlike the fields above there is nothing for the
  // server to reprocess - and a reprocess would only throw the choice away.
  const onNamingLanguagesSelect = useCallback(
    (audio: Language[], subtitles: Language[]) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          namingAudioLanguages: audio,
          namingSubtitleLanguages: subtitles,
        })
      );

      if (selectedEpisodeFileIds.length) {
        dispatch(
          updateEpisodeFiles({
            files: selectedEpisodeFileIds.map((id) => ({
              id,
              namingAudioLanguages: audio,
              namingSubtitleLanguages: subtitles,
            })),
          })
        );
      }

      setSelectModalOpen(null);
    },
    [selectedIds, selectedEpisodeFileIds, dispatch]
  );

  const onCustomFormatsSelect = useCallback(
    (added: number[], excluded: number[]) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          manualCustomFormats: added,
          excludedCustomFormats: excluded,
        })
      );

      if (selectedEpisodeFileIds.length) {
        dispatch(
          updateEpisodeFiles({
            files: selectedEpisodeFileIds.map((id) => ({
              id,
              manualCustomFormats: added,
              excludedCustomFormats: excluded,
            })),
          })
        );
      }

      setSelectModalOpen(null);
    },
    [selectedIds, selectedEpisodeFileIds, dispatch]
  );

  const onQualitySelect = useCallback(
    (quality: QualityModel) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          quality,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch]
  );

  const onIndexerFlagsSelect = useCallback(
    (indexerFlags: number) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          indexerFlags,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch]
  );

  const onReleaseTypeSelect = useCallback(
    (releaseType: string) => {
      dispatch(
        updateInteractiveImportItems({
          ids: selectedIds,
          releaseType,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: selectedIds }));

      setSelectModalOpen(null);
    },
    [selectedIds, dispatch]
  );

  const orderedSelectedIds = items.reduce((acc: number[], file) => {
    if (selectedIds.includes(file.id)) {
      acc.push(file.id);
    }

    return acc;
  }, []);

  const selectedItem = selectedIds.length
    ? items.find((file) => file.id === selectedIds[0])
    : null;

  const errorMessage = getErrorMessage(
    error,
    translate('InteractiveImportLoadError')
  );

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {modalTitle} - {title || folder}
      </ModalHeader>

      <ModalBody scrollDirection={scrollDirections.BOTH}>
        {showFilterExistingFiles || multipleStates.total ? (
          <div className={styles.filterContainer}>
            {multipleStates.total ? (
              <div className={styles.multipleSummary}>
                {MULTIPLE_FILTERS.map(({ state, label, kind }) => {
                  return (
                    <MultipleFilterButton
                      key={state}
                      state={state}
                      label={translate(label)}
                      kind={kind}
                      count={multipleStates[state]}
                      isActive={activeMultipleFilter === state}
                      onPress={onMultipleFilterPress}
                    />
                  );
                })}

                {/* Beside the count that leads to them, rather than in the footer, which has no room. */}
                {unmarkedSharedIds.length ? (
                  <span className={styles.importBlocked}>
                    {translate('InteractiveImportBlockedUnmarked')}
                  </span>
                ) : null}

                {activeMultipleFilter ? (
                  <MultipleFilterButton
                    state={null}
                    label={translate('MultipleShowAll')}
                    onPress={onMultipleFilterPress}
                  />
                ) : null}
              </div>
            ) : null}

            {showFilterExistingFiles ? (
              <Menu alignMenu={align.RIGHT}>
                <MenuButton>
                  <Icon name={icons.FILTER} size={22} />

                  <div className={styles.filterText}>
                    {filterExistingFiles
                      ? translate('UnmappedFilesOnly')
                      : translate('AllFiles')}
                  </div>
                </MenuButton>

                <MenuContent>
                  <SelectedMenuItem
                    name="all"
                    isSelected={!filterExistingFiles}
                    onPress={onFilterExistingFilesChange}
                  >
                    {translate('AllFiles')}
                  </SelectedMenuItem>

                  <SelectedMenuItem
                    name="new"
                    isSelected={filterExistingFiles}
                    onPress={onFilterExistingFilesChange}
                  >
                    {translate('UnmappedFilesOnly')}
                  </SelectedMenuItem>
                </MenuContent>
              </Menu>
            ) : null}
          </div>
        ) : null}

        {isFetching ? <LoadingIndicator /> : null}

        {error ? <div>{errorMessage}</div> : null}

        {isPopulated && !!items.length && !isFetching && !isFetching ? (
          <Table
            columns={columns}
            horizontalScroll={true}
            selectAll={true}
            allSelected={allSelected}
            allUnselected={allUnselected}
            sortKey={sortKey}
            sortDirection={sortDirection}
            onSortPress={onSortPress}
            onSelectAllChange={onSelectAllChange}
          >
            <TableBody>
              {visibleItems.map((item) => {
                return (
                  <InteractiveImportRow
                    key={item.id}
                    isSelected={selectedState[item.id]}
                    {...item}
                    multipleState={multipleStates.byId[item.id]}
                    allowSeriesChange={allowSeriesChange}
                    columns={columns}
                    modalTitle={modalTitle}
                    onSelectedChange={onSelectedChange}
                    onValidRowChange={onValidRowChange}
                  />
                );
              })}
            </TableBody>
          </Table>
        ) : null}

        {isPopulated && !items.length && !isFetching
          ? translate('InteractiveImportNoFilesFound')
          : null}
      </ModalBody>

      <ModalFooter className={styles.footer}>
        <div className={styles.leftButtons}>
          {showDelete ? (
            <SpinnerButton
              className={styles.deleteButton}
              kind={kinds.DANGER}
              isSpinning={isDeleting}
              isDisabled={
                !selectedIds.length || !!withoutEpisodeFileIdRowsSelected.length
              }
              onPress={onDeleteSelectedPress}
            >
              {translate('Delete')}
            </SpinnerButton>
          ) : null}

          {!downloadId && !downloadIds?.length && showImportMode ? (
            <SelectInput
              className={styles.importMode}
              name="importMode"
              value={importMode}
              values={importModeOptions}
              onChange={onImportModeChange}
            />
          ) : null}

          {/* A row rather than a list behind a menu: picking one used to take opening the menu and then
              choosing, and there is room along the bottom for the choices themselves. The bar stays put
              while the table scrolls, so they are reachable from anywhere in a long list. */}
          {interactiveImportInlineActions ? (
            bulkActions.map((action) => {
              return (
                <BulkActionButton
                  key={action.key}
                  action={action}
                  onPress={onBulkActionPress}
                />
              );
            })
          ) : (
            <SelectInput
              className={styles.bulkSelect}
              name="select"
              value="select"
              values={bulkSelectOptions}
              isDisabled={!selectedIds.length}
              onChange={onSelectModalSelect}
            />
          )}

          {/* Sits with the rest even though it acts on whole groups rather than on each selected row,
              since a lone worded button beside a row of icons reads as something left over. It appears
              when the naming format can tell the files apart and the selection either falls into groups
              or holds files that already carry a marker to take off. */}
          {isMultipleEnabled &&
          (multipleGroups.length || markedSelectedIds.length) ? (
            <Button
              className={styles.bulkAction}
              title={translate('MarkAsMultiple')}
              onPress={onMarkAsMultiplePress}
            >
              <Icon name={icons.UNGROUP} />
            </Button>
          ) : null}
        </div>

        <div className={styles.rightButtons}>
          {checkProgress ? (
            <span className={styles.checkProgress}>
              {translate('CheckingFiles', checkProgress)}{' '}
              <Link onPress={onCancelCheckPress}>{translate('Cancel')}</Link>
            </span>
          ) : null}

          <Button onPress={onModalClose}>Cancel</Button>

          {interactiveImportErrorMessage && (
            <span className={styles.errorMessage}>
              {interactiveImportErrorMessage}
            </span>
          )}

          <Button
            kind={kinds.SUCCESS}
            isDisabled={
              !selectedIds.length ||
              !!invalidRowsSelected.length ||
              !!unmarkedSharedIds.length
            }
            title={
              unmarkedSharedIds.length
                ? translate('InteractiveImportUnmarkedShared', {
                    count: unmarkedSharedIds.length,
                  })
                : undefined
            }
            onPress={onImportPress}
          >
            {translate('Import')}
          </Button>
        </div>
      </ModalFooter>

      <MapInOrderModal
        isOpen={selectModalOpen === 'mapInOrder'}
        items={mapItems}
        series={mapSeries}
        onApply={onMapInOrderApply}
        onModalClose={onSelectModalClose}
      />

      <SelectEditionModal
        isOpen={selectModalOpen === 'edition'}
        series={commonSeries}
        modalTitle={modalTitle}
        onEditionSelect={onEditionSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectSeriesModal
        isOpen={selectModalOpen === 'series'}
        modalTitle={modalTitle}
        onSeriesSelect={onSeriesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectSeasonModal
        isOpen={selectModalOpen === 'season'}
        seriesId={selectedItem?.series?.id}
        modalTitle={modalTitle}
        onSeasonSelect={onSeasonSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectEpisodeModal
        isOpen={selectModalOpen === 'episode'}
        selectedIds={orderedSelectedIds}
        seriesId={selectedItem?.series?.id}
        seasonNumber={selectedItem?.seasonNumber}
        isAnime={selectedItem?.series?.seriesType === 'anime'}
        modalTitle={modalTitle}
        onEpisodesSelect={onEpisodesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectReleaseGroupModal
        isOpen={selectModalOpen === 'releaseGroup'}
        releaseGroup=""
        modalTitle={modalTitle}
        onReleaseGroupSelect={onReleaseGroupSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectLanguageModal
        isOpen={selectModalOpen === 'language'}
        languageIds={[0]}
        modalTitle={modalTitle}
        onLanguagesSelect={onLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectNamingLanguagesModal
        isOpen={selectModalOpen === 'namingLanguages'}
        audioLanguages={[]}
        subtitleLanguages={[]}
        detectedAudioLanguages={[]}
        detectedSubtitleLanguages={[]}
        modalTitle={modalTitle}
        onNamingLanguagesSelect={onNamingLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectCustomFormatModal
        isOpen={selectModalOpen === 'customFormats'}
        selectedIds={[]}
        matchedIds={[]}
        modalTitle={modalTitle}
        onCustomFormatsSelect={onCustomFormatsSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectQualityModal
        isOpen={selectModalOpen === 'quality'}
        qualityId={0}
        proper={false}
        real={false}
        modalTitle={modalTitle}
        onQualitySelect={onQualitySelect}
        onModalClose={onSelectModalClose}
      />

      <SelectIndexerFlagsModal
        isOpen={selectModalOpen === 'indexerFlags'}
        indexerFlags={0}
        modalTitle={modalTitle}
        onIndexerFlagsSelect={onIndexerFlagsSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectReleaseTypeModal
        isOpen={selectModalOpen === 'releaseType'}
        releaseType="unknown"
        modalTitle={modalTitle}
        onReleaseTypeSelect={onReleaseTypeSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectMultipleModal
        isOpen={selectModalOpen === 'multiple'}
        // With nothing to group, the only thing the action can do is take markers off.
        multipleType={multipleGroups.length ? 'part' : 'none'}
        multipleNumber={0}
        autoNumber={true}
        modalTitle={modalTitle}
        onMultipleSelect={onMarkAsMultipleSelect}
        onModalClose={onSelectModalClose}
      />

      <ConfirmModal
        isOpen={isConfirmImportModeOpen}
        kind={importMode === 'move' ? kinds.DANGER : kinds.PRIMARY}
        title={
          importMode === 'move'
            ? translate('ConfirmImportMoveTitle')
            : translate('ConfirmImportCopyTitle')
        }
        message={
          importMode === 'move'
            ? translate('ConfirmImportMoveMessage', {
                count: selectedIds.length,
              })
            : translate('ConfirmImportCopyMessage', {
                count: selectedIds.length,
              })
        }
        confirmLabel={
          importMode === 'move'
            ? translate('MoveFiles')
            : translate('HardlinkCopyFiles')
        }
        cancelLabel={translate('ConfirmImportChangeMode')}
        onConfirm={onConfirmImportMode}
        onCancel={onConfirmImportModeClose}
      />

      <ConfirmModal
        isOpen={isConfirmDeleteModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteSelectedEpisodeFiles')}
        message={translate('DeleteSelectedEpisodeFilesHelpText')}
        confirmLabel={translate('Delete')}
        onConfirm={onConfirmDelete}
        onCancel={onConfirmDeleteModalClose}
      />
    </ModalContent>
  );
}

export default InteractiveImportModalContent;

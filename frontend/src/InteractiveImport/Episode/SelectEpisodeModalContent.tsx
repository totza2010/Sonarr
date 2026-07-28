import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import EpisodesAppState from 'App/State/EpisodesAppState';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Scroller from 'Components/Scroller/Scroller';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Episode from 'Episode/Episode';
import useSelectState from 'Helpers/Hooks/useSelectState';
import { kinds, scrollDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import MultipleType from 'InteractiveImport/MultipleType';
import {
  clearEpisodes,
  fetchEpisodes,
  setEpisodesSort,
} from 'Store/Actions/episodeSelectionActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import SelectEpisodeRow from './SelectEpisodeRow';
import styles from './SelectEpisodeModalContent.css';

const columns = [
  {
    name: 'episodeNumber',
    label: '#',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'title',
    label: () => translate('Title'),
    isVisible: true,
  },
  {
    name: 'airDate',
    label: () => translate('AirDate'),
    isVisible: true,
  },
];

function episodesSelector() {
  return createSelector(
    createClientSideCollectionSelector('episodeSelection'),
    (episodes: EpisodesAppState) => {
      return episodes;
    }
  );
}

export interface SelectedEpisode {
  id: number;
  episodes: Episode[];
  multipleType?: MultipleType;
  multipleNumber?: number;
}

interface SelectEpisodeModalContentProps {
  selectedIds: number[] | string[];
  seriesId?: number;
  seasonNumber?: number;
  selectedDetails?: string;
  isAnime: boolean;
  modalTitle: string;
  onEpisodesSelect(selectedEpisodes: SelectedEpisode[]): unknown;
  onModalClose(): unknown;
}

function SelectEpisodeModalContent(props: SelectEpisodeModalContentProps) {
  const {
    selectedIds,
    seriesId,
    seasonNumber,
    selectedDetails,
    isAnime,
    modalTitle,
    onEpisodesSelect,
    onModalClose,
  } = props;

  const [filter, setFilter] = useState('');
  const [selectState, setSelectState] = useSelectState();
  const [splitSelectState, setSplitSelectState] = useSelectState();

  // The second step works from a snapshot taken when it is entered rather than from the live
  // selection, so nothing it shows can drift out from under it while the user is deciding.
  const [splitStep, setSplitStep] = useState<{
    episodes: Episode[];
    count: number;
  } | null>(null);

  const { allSelected, allUnselected, selectedState } = selectState;
  const { isFetching, isPopulated, items, error, sortKey, sortDirection } =
    useSelector(episodesSelector());
  const dispatch = useDispatch();

  const filterEpisodeNumber = parseInt(filter);
  const errorMessage = getErrorMessage(error, translate('EpisodesLoadError'));
  const selectedCount = selectedIds.length;
  const selectedEpisodesCount = getSelectedIds(selectedState).length;
  const selectionIsValid =
    selectedEpisodesCount > 0 && selectedEpisodesCount % selectedCount === 0;

  const chosenEpisodes = useMemo(() => {
    const episodeIds: number[] = getSelectedIds(selectedState);

    return items
      .filter((item) => episodeIds.includes(item.id))
      .sort(
        (a, b) =>
          a.seasonNumber - b.seasonNumber || a.episodeNumber - b.episodeNumber
      );
  }, [items, selectedState]);

  // Splitting episodes across the files is the opposite of the rule above, which shares several
  // episodes out between the files, so it gets its own button rather than changing that one. It
  // needs more files than episodes: 12 over 6 is two parts each, 7 over 6 is one episode in two.
  // Counted off the same list the action works from, so the button cannot offer something the
  // action then finds nothing to do with.
  const partSelectionIsValid =
    chosenEpisodes.length > 0 && selectedCount > chosenEpisodes.length;

  // How many episodes have an extra file when the files do not divide evenly. Nothing can work that
  // out on the user's behalf, so it becomes a second step where they say which ones.
  const splitCount = chosenEpisodes.length
    ? selectedCount % chosenEpisodes.length
    : 0;

  const splitSelectedCount = getSelectedIds(
    splitSelectState.selectedState
  ).length;

  const onFilterChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setFilter(value.toLowerCase());
    },
    [setFilter]
  );

  const onSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setSelectState({ type: value ? 'selectAll' : 'unselectAll', items });
    },
    [items, setSelectState]
  );

  const onSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      setSelectState({
        type: 'toggleSelected',
        items,
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [items, setSelectState]
  );

  const onSortPress = useCallback(
    (newSortKey: string, newSortDirection: SortDirection) => {
      dispatch(
        setEpisodesSort({
          sortKey: newSortKey,
          sortDirection: newSortDirection,
        })
      );
    },
    [dispatch]
  );

  const onEpisodesSelectWrapper = useCallback(() => {
    const episodeIds: number[] = getSelectedIds(selectedState);

    const selectedEpisodes = items.reduce((acc: Episode[], item) => {
      if (episodeIds.indexOf(item.id) > -1) {
        acc.push(item);
      }

      return acc;
    }, []);

    const episodesPerFile = selectedEpisodes.length / selectedIds.length;
    const sortedEpisodes = selectedEpisodes.sort((a, b) => {
      return a.seasonNumber - b.seasonNumber;
    });

    const mappedEpisodes = selectedIds.map((id, index): SelectedEpisode => {
      const startingIndex = index * episodesPerFile;
      const episodes = sortedEpisodes.slice(
        startingIndex,
        startingIndex + episodesPerFile
      );

      return {
        id: id as number,
        episodes,
      };
    });

    onEpisodesSelect(mappedEpisodes);
  }, [selectedIds, items, selectedState, onEpisodesSelect]);

  // Hands the files out to the episodes in order, giving each one as many as it is owed. Both
  // orders are the ones already on screen: files top to bottom in the table behind this modal,
  // episodes in numerical order, so the first block of files becomes the first episode.
  const assignParts = useCallback(
    (episodes: Episode[], splitEpisodeIds: number[]) => {
      const base = Math.floor(selectedCount / episodes.length);
      const mappedEpisodes: SelectedEpisode[] = [];
      let fileIndex = 0;

      episodes.forEach((episode) => {
        const share = splitEpisodeIds.includes(episode.id) ? base + 1 : base;

        for (let part = 1; part <= share; part++) {
          mappedEpisodes.push({
            id: selectedIds[fileIndex] as number,
            episodes: [episode],

            // An episode with one file to itself is not split, so it is left as the whole episode.
            // Saying so rather than saying nothing also clears a part left over from an earlier go.
            multipleType: share > 1 ? 'part' : 'none',
            multipleNumber: share > 1 ? part : 0,
          });

          fileIndex++;
        }
      });

      onEpisodesSelect(mappedEpisodes);
    },
    [selectedIds, selectedCount, onEpisodesSelect]
  );

  const onEpisodePartsSelectWrapper = useCallback(() => {
    if (!chosenEpisodes.length) {
      return;
    }

    if (splitCount === 0) {
      assignParts(chosenEpisodes, []);
      return;
    }

    setSplitStep({ episodes: chosenEpisodes, count: splitCount });
  }, [chosenEpisodes, splitCount, assignParts, setSplitStep]);

  const onSplitEpisodesConfirm = useCallback(() => {
    if (!splitStep) {
      return;
    }

    assignParts(
      splitStep.episodes,
      getSelectedIds(splitSelectState.selectedState)
    );
  }, [splitStep, assignParts, splitSelectState]);

  const onSplitSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      setSplitSelectState({
        type: 'toggleSelected',
        items: splitStep?.episodes ?? [],
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [splitStep, setSplitSelectState]
  );

  const onBackFromSplitsPress = useCallback(() => {
    setSplitStep(null);
    setSplitSelectState({ type: 'reset' });
  }, [setSplitStep, setSplitSelectState]);

  useEffect(
    () => {
      dispatch(fetchEpisodes({ seriesId, seasonNumber }));

      return () => {
        dispatch(clearEpisodes());
      };
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  let details = selectedDetails;

  if (!details) {
    details =
      selectedCount > 1
        ? translate('CountSelectedFiles', { selectedCount })
        : translate('CountSelectedFile', { selectedCount });
  }

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectEpisodesModalTitle', { modalTitle })}
      </ModalHeader>

      <ModalBody
        className={styles.modalBody}
        scrollDirection={scrollDirections.NONE}
      >
        {splitStep ? null : (
          <TextInput
            className={styles.filterInput}
            placeholder={translate('FilterEpisodesPlaceholder')}
            name="filter"
            value={filter}
            autoFocus={true}
            onChange={onFilterChange}
          />
        )}

        <Scroller className={styles.scroller} autoFocus={false}>
          {isFetching ? <LoadingIndicator /> : null}

          {error ? <div>{errorMessage}</div> : null}

          {splitStep ? (
            <Table columns={columns}>
              <TableBody>
                {splitStep.episodes.map((item) => {
                  return (
                    <SelectEpisodeRow
                      key={item.id}
                      id={item.id}
                      episodeNumber={item.episodeNumber}
                      absoluteEpisodeNumber={item.absoluteEpisodeNumber}
                      title={item.title}
                      airDate={item.airDate}
                      isAnime={isAnime}
                      isSelected={splitSelectState.selectedState[item.id]}
                      onSelectedChange={onSplitSelectedChange}
                    />
                  );
                })}
              </TableBody>
            </Table>
          ) : null}

          {!splitStep && isPopulated && !!items.length ? (
            <Table
              columns={columns}
              selectAll={true}
              allSelected={allSelected}
              allUnselected={allUnselected}
              sortKey={sortKey}
              sortDirection={sortDirection}
              onSortPress={onSortPress}
              onSelectAllChange={onSelectAllChange}
            >
              <TableBody>
                {items.map((item) => {
                  return item.title.toLowerCase().includes(filter) ||
                    item.episodeNumber === filterEpisodeNumber ? (
                    <SelectEpisodeRow
                      key={item.id}
                      id={item.id}
                      episodeNumber={item.episodeNumber}
                      absoluteEpisodeNumber={item.absoluteEpisodeNumber}
                      title={item.title}
                      airDate={item.airDate}
                      isAnime={isAnime}
                      isSelected={selectedState[item.id]}
                      onSelectedChange={onSelectedChange}
                    />
                  ) : null;
                })}
              </TableBody>
            </Table>
          ) : null}

          {isPopulated && !items.length
            ? translate('NoEpisodesFoundForSelectedSeason')
            : null}
        </Scroller>
      </ModalBody>

      <ModalFooter className={styles.footer}>
        <div className={styles.details}>
          {splitStep
            ? translate('SelectSplitEpisodesDetails', {
                splitCount: splitStep.count,
              })
            : details}
        </div>

        <div className={styles.buttons}>
          {splitStep ? (
            <>
              <Button onPress={onBackFromSplitsPress}>
                {translate('Back')}
              </Button>

              <Button
                kind={kinds.SUCCESS}
                isDisabled={splitSelectedCount !== splitStep.count}
                onPress={onSplitEpisodesConfirm}
              >
                {translate('SelectEpisodeParts')}
              </Button>
            </>
          ) : (
            <>
              <Button onPress={onModalClose}>{translate('Cancel')}</Button>

              <Button
                kind={kinds.SUCCESS}
                isDisabled={!selectionIsValid}
                onPress={onEpisodesSelectWrapper}
              >
                {translate('SelectEpisodes')}
              </Button>

              {partSelectionIsValid ? (
                <Button
                  kind={kinds.SUCCESS}
                  title={translate('SelectEpisodePartsHelpText')}
                  onPress={onEpisodePartsSelectWrapper}
                >
                  {translate('SelectEpisodeParts')}
                </Button>
              ) : null}
            </>
          )}
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectEpisodeModalContent;

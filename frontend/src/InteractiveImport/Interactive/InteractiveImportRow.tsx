import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRowCellButton from 'Components/Table/Cells/TableRowCellButton';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Episode from 'Episode/Episode';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import getReleaseTypeName from 'Episode/getReleaseTypeName';
import IndexerFlags from 'Episode/IndexerFlags';
import NamingLanguages from 'EpisodeFile/NamingLanguages';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import SelectCustomFormatModal from 'InteractiveImport/CustomFormat/SelectCustomFormatModal';
import SelectEpisodeModal from 'InteractiveImport/Episode/SelectEpisodeModal';
import { SelectedEpisode } from 'InteractiveImport/Episode/SelectEpisodeModalContent';
import SelectIndexerFlagsModal from 'InteractiveImport/IndexerFlags/SelectIndexerFlagsModal';
import SelectLanguageModal from 'InteractiveImport/Language/SelectLanguageModal';
import SelectNamingLanguagesModal from 'InteractiveImport/NamingLanguages/SelectNamingLanguagesModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import SelectReleaseGroupModal from 'InteractiveImport/ReleaseGroup/SelectReleaseGroupModal';
import ReleaseType from 'InteractiveImport/ReleaseType';
import SelectReleaseTypeModal from 'InteractiveImport/ReleaseType/SelectReleaseTypeModal';
import SelectSeasonModal from 'InteractiveImport/Season/SelectSeasonModal';
import SelectSeriesModal from 'InteractiveImport/Series/SelectSeriesModal';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import Series from 'Series/Series';
import { updateEpisodeFiles } from 'Store/Actions/episodeFileActions';
import {
  reprocessInteractiveImportItems,
  updateInteractiveImportItem,
} from 'Store/Actions/interactiveImportActions';
import CustomFormat from 'typings/CustomFormat';
import { SelectStateInputProps } from 'typings/props';
import Rejection from 'typings/Rejection';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import InteractiveImportRowCellPlaceholder from './InteractiveImportRowCellPlaceholder';
import styles from './InteractiveImportRow.css';

type SelectType =
  | 'series'
  | 'season'
  | 'episode'
  | 'releaseGroup'
  | 'quality'
  | 'language'
  | 'indexerFlags'
  | 'releaseType'
  | 'namingLanguages'
  | 'customFormats';

type SelectedChangeProps = SelectStateInputProps & {
  hasEpisodeFileId: boolean;
};

interface InteractiveImportRowProps {
  id: number;
  allowSeriesChange: boolean;
  relativePath: string;
  series?: Series;
  seasonNumber?: number;
  episodes?: Episode[];
  releaseGroup?: string;
  quality?: QualityModel;
  languages?: Language[];
  namingAudioLanguages?: Language[];
  namingSubtitleLanguages?: Language[];
  detectedAudioLanguages?: Language[];
  detectedSubtitleLanguages?: Language[];
  size: number;
  releaseType: ReleaseType;
  customFormats?: CustomFormat[];
  manualCustomFormats?: number[];
  excludedCustomFormats?: number[];
  customFormatScore?: number;
  indexerFlags: number;
  rejections: Rejection[];
  columns: Column[];
  episodeFileId?: number;
  isReprocessing?: boolean;
  isSelected?: boolean;
  modalTitle: string;
  onSelectedChange(result: SelectedChangeProps): void;
  onValidRowChange(id: number, isValid: boolean): void;
}

function InteractiveImportRow(props: InteractiveImportRowProps) {
  const {
    id,
    allowSeriesChange,
    relativePath,
    series,
    seasonNumber,
    episodes = [],
    quality,
    languages,
    namingAudioLanguages,
    namingSubtitleLanguages,
    detectedAudioLanguages,
    detectedSubtitleLanguages,
    releaseGroup,
    size,
    releaseType,
    customFormats = [],
    manualCustomFormats,
    excludedCustomFormats,
    customFormatScore,
    indexerFlags,
    rejections,
    isReprocessing,
    isSelected,
    modalTitle,
    episodeFileId,
    columns,
    onSelectedChange,
    onValidRowChange,
  } = props;

  const dispatch = useDispatch();

  // The list the backend returns is what the name matched; anything added by hand only exists on the
  // row until the file is imported, so its names are looked up here to show it right away.
  const allCustomFormats = useSelector(
    (state: AppState) => state.settings.customFormats.items
  );

  const shownCustomFormats = useMemo(() => {
    const kept = customFormats.filter(
      (format) => !excludedCustomFormats?.includes(format.id)
    );

    const manual = allCustomFormats.filter(
      (format) =>
        manualCustomFormats?.includes(format.id) &&
        !kept.some((f) => f.id === format.id)
    );

    return [...kept, ...manual];
  }, [
    customFormats,
    manualCustomFormats,
    excludedCustomFormats,
    allCustomFormats,
  ]);

  // What the name matched on its own, which is what the picker measures additions and removals
  // against. The list from the backend already has the file's own choices folded in, so they come
  // back out here rather than being asked for again.
  const matchedCustomFormatIds = useMemo(() => {
    return customFormats
      .filter((format) => !manualCustomFormats?.includes(format.id))
      .map((format) => format.id)
      .concat(excludedCustomFormats ?? []);
  }, [customFormats, manualCustomFormats, excludedCustomFormats]);

  const isSeriesColumnVisible = useMemo(
    () => columns.find((c) => c.name === 'series')?.isVisible ?? false,
    [columns]
  );
  const isIndexerFlagsColumnVisible = useMemo(
    () => columns.find((c) => c.name === 'indexerFlags')?.isVisible ?? false,
    [columns]
  );

  const [selectModalOpen, setSelectModalOpen] = useState<SelectType | null>(
    null
  );

  useEffect(
    () => {
      if (
        allowSeriesChange &&
        series &&
        seasonNumber != null &&
        episodes.length &&
        quality &&
        languages &&
        size > 0
      ) {
        onSelectedChange({
          id,
          hasEpisodeFileId: !!episodeFileId,
          value: true,
          shiftKey: false,
        });
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  useEffect(() => {
    const isValid = !!(
      series &&
      seasonNumber != null &&
      episodes.length &&
      quality &&
      languages
    );

    if (isSelected && !isValid) {
      onValidRowChange(id, false);
    } else {
      onValidRowChange(id, true);
    }
  }, [
    id,
    series,
    seasonNumber,
    episodes,
    quality,
    languages,
    isSelected,
    onValidRowChange,
  ]);

  const onSelectedChangeWrapper = useCallback(
    (result: SelectedChangeProps) => {
      onSelectedChange({
        ...result,
        hasEpisodeFileId: !!episodeFileId,
      });
    },
    [episodeFileId, onSelectedChange]
  );

  const selectRowAfterChange = useCallback(() => {
    if (!isSelected) {
      onSelectedChange({
        id,
        hasEpisodeFileId: !!episodeFileId,
        value: true,
        shiftKey: false,
      });
    }
  }, [id, episodeFileId, isSelected, onSelectedChange]);

  const onSelectModalClose = useCallback(() => {
    setSelectModalOpen(null);
  }, [setSelectModalOpen]);

  const onSelectSeriesPress = useCallback(() => {
    setSelectModalOpen('series');
  }, [setSelectModalOpen]);

  const onSeriesSelect = useCallback(
    (series: Series) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          series,
          seasonNumber: undefined,
          episodes: [],
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectSeasonPress = useCallback(() => {
    setSelectModalOpen('season');
  }, [setSelectModalOpen]);

  const onSeasonSelect = useCallback(
    (seasonNumber: number) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          seasonNumber,
          episodes: [],
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectEpisodePress = useCallback(() => {
    setSelectModalOpen('episode');
  }, [setSelectModalOpen]);

  const onEpisodesSelect = useCallback(
    (selectedEpisodes: SelectedEpisode[]) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          episodes: selectedEpisodes[0].episodes,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectReleaseGroupPress = useCallback(() => {
    setSelectModalOpen('releaseGroup');
  }, [setSelectModalOpen]);

  const onReleaseGroupSelect = useCallback(
    (releaseGroup: string) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          releaseGroup,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectQualityPress = useCallback(() => {
    setSelectModalOpen('quality');
  }, [setSelectModalOpen]);

  const onQualitySelect = useCallback(
    (quality: QualityModel) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          quality,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectLanguagePress = useCallback(() => {
    setSelectModalOpen('language');
  }, [setSelectModalOpen]);

  const onLanguagesSelect = useCallback(
    (languages: Language[]) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          languages,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  // What the tokens will actually say: the override where there is one, what MediaInfo read where
  // there is not. Showing only overrides would leave the column blank on every file nobody has
  // touched, which is most of them; showing them the same way would hide which is which.
  const namingParts = [
    { chosen: namingAudioLanguages, detected: detectedAudioLanguages },
    { chosen: namingSubtitleLanguages, detected: detectedSubtitleLanguages },
  ]
    .map(({ chosen, detected }) => ({
      languages: chosen?.length ? chosen : detected ?? [],
      detectedNames: (detected ?? []).map((l) => l.name),
    }))
    .filter(({ languages }) => languages.length);

  const hasNamingOverride = !!(
    namingAudioLanguages?.length || namingSubtitleLanguages?.length
  );

  const onSelectNamingLanguagesPress = useCallback(() => {
    setSelectModalOpen('namingLanguages');
  }, [setSelectModalOpen]);

  const onNamingLanguagesSelect = useCallback(
    (audio: Language[], subtitles: Language[]) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          namingAudioLanguages: audio,
          namingSubtitleLanguages: subtitles,
        })
      );

      // A file already in the library is not waiting to be imported, so pressing Import would add a
      // second row for the same file rather than change this one. Save it against the file instead.
      if (episodeFileId) {
        dispatch(
          updateEpisodeFiles({
            files: [
              {
                id: episodeFileId,
                namingAudioLanguages: audio,
                namingSubtitleLanguages: subtitles,
              },
            ],
          })
        );
      }

      setSelectModalOpen(null);
    },
    [id, episodeFileId, dispatch, setSelectModalOpen]
  );

  const onSelectCustomFormatsPress = useCallback(() => {
    setSelectModalOpen('customFormats');
  }, [setSelectModalOpen]);

  const onCustomFormatsSelect = useCallback(
    (added: number[], excluded: number[]) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          manualCustomFormats: added,
          excludedCustomFormats: excluded,
        })
      );

      // Only files already in the library have somewhere to keep this; a file waiting to be imported
      // has no row in the database yet, so there is nothing to write it to.
      if (episodeFileId) {
        dispatch(
          updateEpisodeFiles({
            files: [
              {
                id: episodeFileId,
                manualCustomFormats: added,
                excludedCustomFormats: excluded,
              },
            ],
          })
        );
      }

      setSelectModalOpen(null);
    },
    [id, episodeFileId, dispatch, setSelectModalOpen]
  );

  const onSelectReleaseTypePress = useCallback(() => {
    setSelectModalOpen('releaseType');
  }, [setSelectModalOpen]);

  const onReleaseTypeSelect = useCallback(
    (releaseType: ReleaseType) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          releaseType,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const onSelectIndexerFlagsPress = useCallback(() => {
    setSelectModalOpen('indexerFlags');
  }, [setSelectModalOpen]);

  const onIndexerFlagsSelect = useCallback(
    (indexerFlags: number) => {
      dispatch(
        updateInteractiveImportItem({
          id,
          indexerFlags,
        })
      );

      dispatch(reprocessInteractiveImportItems({ ids: [id] }));

      setSelectModalOpen(null);
      selectRowAfterChange();
    },
    [id, dispatch, setSelectModalOpen, selectRowAfterChange]
  );

  const seriesTitle = series ? series.title : '';
  const isAnime = series?.seriesType === 'anime';

  const episodeInfo = episodes.map((episode) => {
    return (
      <div key={episode.id}>
        {episode.episodeNumber}

        {isAnime && episode.absoluteEpisodeNumber != null
          ? ` (${episode.absoluteEpisodeNumber})`
          : ''}

        {` - ${episode.title}`}
      </div>
    );
  });

  const requiresSeasonNumber = isNaN(Number(seasonNumber));
  const showSeriesPlaceholder = isSelected && !series;
  const showSeasonNumberPlaceholder =
    isSelected && !!series && requiresSeasonNumber && !isReprocessing;
  const showEpisodeNumbersPlaceholder =
    isSelected && Number.isInteger(seasonNumber) && !episodes.length;
  const showReleaseGroupPlaceholder = isSelected && !releaseGroup;
  const showQualityPlaceholder = isSelected && !quality;
  const showLanguagePlaceholder = isSelected && !languages;
  const showIndexerFlagsPlaceholder = isSelected && !indexerFlags;

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={onSelectedChangeWrapper}
      />

      <TableRowCell className={styles.relativePath} title={relativePath}>
        {relativePath}
      </TableRowCell>

      {isSeriesColumnVisible ? (
        <TableRowCellButton
          isDisabled={!allowSeriesChange}
          title={
            allowSeriesChange ? translate('ClickToChangeSeries') : undefined
          }
          onPress={onSelectSeriesPress}
        >
          {showSeriesPlaceholder ? (
            <InteractiveImportRowCellPlaceholder />
          ) : (
            seriesTitle
          )}
        </TableRowCellButton>
      ) : null}

      <TableRowCellButton
        isDisabled={!series}
        title={series ? translate('ClickToChangeSeason') : undefined}
        onPress={onSelectSeasonPress}
      >
        {showSeasonNumberPlaceholder ? (
          <InteractiveImportRowCellPlaceholder />
        ) : (
          seasonNumber
        )}

        {isReprocessing && seasonNumber == null ? (
          <LoadingIndicator className={styles.reprocessing} size={20} />
        ) : null}
      </TableRowCellButton>

      <TableRowCellButton
        isDisabled={!series || requiresSeasonNumber}
        title={
          series && !requiresSeasonNumber
            ? translate('ClickToChangeEpisode')
            : undefined
        }
        onPress={onSelectEpisodePress}
      >
        {showEpisodeNumbersPlaceholder ? (
          <InteractiveImportRowCellPlaceholder />
        ) : (
          episodeInfo
        )}
      </TableRowCellButton>

      <TableRowCellButton
        title={translate('ClickToChangeReleaseGroup')}
        onPress={onSelectReleaseGroupPress}
      >
        {showReleaseGroupPlaceholder ? (
          <InteractiveImportRowCellPlaceholder isOptional={true} />
        ) : (
          releaseGroup
        )}
      </TableRowCellButton>

      <TableRowCellButton
        className={styles.quality}
        title={translate('ClickToChangeQuality')}
        onPress={onSelectQualityPress}
      >
        {showQualityPlaceholder && <InteractiveImportRowCellPlaceholder />}

        {!showQualityPlaceholder && !!quality && (
          <EpisodeQuality className={styles.label} quality={quality} />
        )}
      </TableRowCellButton>

      <TableRowCellButton
        className={styles.languages}
        title={translate('ClickToChangeLanguage')}
        onPress={onSelectLanguagePress}
      >
        {showLanguagePlaceholder && <InteractiveImportRowCellPlaceholder />}

        {!showLanguagePlaceholder && !!languages && (
          <EpisodeLanguages className={styles.label} languages={languages} />
        )}
      </TableRowCellButton>

      <TableRowCellButton
        className={styles.languages}
        title={
          hasNamingOverride
            ? translate('ClickToChangeNamingLanguages')
            : translate('ClickToChangeNamingLanguagesDetected')
        }
        onPress={onSelectNamingLanguagesPress}
      >
        {namingParts.length ? (
          namingParts.map(({ languages, detectedNames }, index) => (
            <span key={index}>
              {index > 0 ? ' / ' : null}

              {/* Both branches go through the same component: with no override every language is
                  simply "kept", which renders plainly and truncates like everything else. */}
              <NamingLanguages
                languages={languages}
                detectedNames={detectedNames}
              />
            </span>
          ))
        ) : (
          <InteractiveImportRowCellPlaceholder isOptional={true} />
        )}
      </TableRowCellButton>

      <TableRowCell>{formatBytes(size)}</TableRowCell>

      <TableRowCellButton
        title={translate('ClickToChangeReleaseType')}
        onPress={onSelectReleaseTypePress}
      >
        {getReleaseTypeName(releaseType)}
      </TableRowCellButton>

      <TableRowCellButton
        title={translate('ClickToChangeCustomFormats')}
        onPress={onSelectCustomFormatsPress}
      >
        {shownCustomFormats.length ? (
          <Popover
            anchor={formatCustomFormatScore(
              customFormatScore,
              shownCustomFormats.length
            )}
            title={translate('CustomFormats')}
            body={
              <div className={styles.customFormatTooltip}>
                <EpisodeFormats
                  formats={shownCustomFormats}
                  manualIds={manualCustomFormats}
                  excludedIds={excludedCustomFormats}
                />
              </div>
            }
            position={tooltipPositions.LEFT}
          />
        ) : (
          <InteractiveImportRowCellPlaceholder isOptional={true} />
        )}
      </TableRowCellButton>

      {isIndexerFlagsColumnVisible ? (
        <TableRowCellButton
          title={translate('ClickToChangeIndexerFlags')}
          onPress={onSelectIndexerFlagsPress}
        >
          {showIndexerFlagsPlaceholder ? (
            <InteractiveImportRowCellPlaceholder isOptional={true} />
          ) : (
            <>
              {indexerFlags ? (
                <Popover
                  anchor={<Icon name={icons.FLAG} />}
                  title={translate('IndexerFlags')}
                  body={<IndexerFlags indexerFlags={indexerFlags} />}
                  position={tooltipPositions.LEFT}
                />
              ) : null}
            </>
          )}
        </TableRowCellButton>
      ) : null}

      <TableRowCell>
        {rejections.length ? (
          <Popover
            anchor={<Icon name={icons.DANGER} kind={kinds.DANGER} />}
            title={translate('ReleaseRejected')}
            body={
              <ul>
                {rejections.map((rejection, index) => {
                  return <li key={index}>{rejection.reason}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
            canFlip={false}
          />
        ) : null}
      </TableRowCell>

      <SelectSeriesModal
        isOpen={selectModalOpen === 'series'}
        modalTitle={modalTitle}
        onSeriesSelect={onSeriesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectSeasonModal
        isOpen={selectModalOpen === 'season'}
        seriesId={series?.id}
        modalTitle={modalTitle}
        onSeasonSelect={onSeasonSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectEpisodeModal
        isOpen={selectModalOpen === 'episode'}
        selectedIds={[id]}
        seriesId={series?.id}
        isAnime={isAnime}
        seasonNumber={seasonNumber}
        selectedDetails={relativePath}
        modalTitle={modalTitle}
        onEpisodesSelect={onEpisodesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectReleaseGroupModal
        isOpen={selectModalOpen === 'releaseGroup'}
        releaseGroup={releaseGroup ?? ''}
        modalTitle={modalTitle}
        onReleaseGroupSelect={onReleaseGroupSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectQualityModal
        isOpen={selectModalOpen === 'quality'}
        qualityId={quality ? quality.quality.id : 0}
        proper={quality ? quality.revision.version > 1 : false}
        real={quality ? quality.revision.real > 0 : false}
        modalTitle={modalTitle}
        onQualitySelect={onQualitySelect}
        onModalClose={onSelectModalClose}
      />

      <SelectLanguageModal
        isOpen={selectModalOpen === 'language'}
        languageIds={languages ? languages.map((l) => l.id) : []}
        modalTitle={modalTitle}
        onLanguagesSelect={onLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectReleaseTypeModal
        isOpen={selectModalOpen === 'releaseType'}
        releaseType={releaseType ?? 'unknown'}
        modalTitle={modalTitle}
        onReleaseTypeSelect={onReleaseTypeSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectCustomFormatModal
        isOpen={selectModalOpen === 'customFormats'}
        selectedIds={shownCustomFormats.map((f) => f.id)}
        matchedIds={matchedCustomFormatIds}
        modalTitle={modalTitle}
        onCustomFormatsSelect={onCustomFormatsSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectNamingLanguagesModal
        isOpen={selectModalOpen === 'namingLanguages'}
        audioLanguages={namingAudioLanguages ?? []}
        subtitleLanguages={namingSubtitleLanguages ?? []}
        detectedAudioLanguages={detectedAudioLanguages ?? []}
        detectedSubtitleLanguages={detectedSubtitleLanguages ?? []}
        modalTitle={modalTitle}
        onNamingLanguagesSelect={onNamingLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectIndexerFlagsModal
        isOpen={selectModalOpen === 'indexerFlags'}
        indexerFlags={indexerFlags ?? 0}
        modalTitle={modalTitle}
        onIndexerFlagsSelect={onIndexerFlagsSelect}
        onModalClose={onSelectModalClose}
      />
    </TableRow>
  );
}

export default InteractiveImportRow;

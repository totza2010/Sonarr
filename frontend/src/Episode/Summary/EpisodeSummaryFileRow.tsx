import React, { useCallback, useEffect } from 'react';
import { useDispatch } from 'react-redux';
import Column from 'Components/Table/Column';
import { EpisodeEntities } from 'Episode/useEpisode';
import useEpisodeFile from 'EpisodeFile/useEpisodeFile';
import {
  deleteEpisodeFile,
  fetchEpisodeFile,
} from 'Store/Actions/episodeFileActions';
import EpisodeFileRow from './EpisodeFileRow';

interface EpisodeSummaryFileRowProps {
  episodeFileId: number;
  episodeEntity: EpisodeEntities;
  columns: Column[];
}

// One row per file the episode owns. An episode can hold several files once it has extra parts or
// versions, and each of them has to load itself, so this cannot be inlined into EpisodeSummary.
function EpisodeSummaryFileRow(props: EpisodeSummaryFileRowProps) {
  const { episodeFileId, episodeEntity, columns } = props;

  const dispatch = useDispatch();

  const {
    path,
    mediaInfo,
    size,
    languages,
    quality,
    qualityCutoffNotMet,
    customFormats,
    customFormatScore,
    multipleType,
    multipleNumber,
  } = useEpisodeFile(episodeFileId) || {};

  const handleDeleteEpisodeFile = useCallback(() => {
    dispatch(
      deleteEpisodeFile({
        id: episodeFileId,
        episodeEntity,
      })
    );
  }, [episodeFileId, episodeEntity, dispatch]);

  useEffect(() => {
    if (episodeFileId && !path) {
      dispatch(fetchEpisodeFile({ id: episodeFileId }));
    }
  }, [episodeFileId, path, dispatch]);

  if (!path) {
    return null;
  }

  return (
    <EpisodeFileRow
      path={path}
      size={size!}
      languages={languages!}
      quality={quality!}
      qualityCutoffNotMet={qualityCutoffNotMet!}
      customFormats={customFormats!}
      customFormatScore={customFormatScore!}
      mediaInfo={mediaInfo!}
      multipleType={multipleType}
      multipleNumber={multipleNumber}
      columns={columns}
      onDeleteEpisodeFile={handleDeleteEpisodeFile}
    />
  );
}

export default EpisodeSummaryFileRow;

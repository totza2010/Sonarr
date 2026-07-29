import React from 'react';
import getLanguageName from 'Utilities/String/getLanguageName';
import translate from 'Utilities/String/translate';
import NamingLanguages from './NamingLanguages';
import useEpisodeFile from './useEpisodeFile';

function toLanguageNames(languages: string | undefined) {
  if (!languages) {
    return [];
  }

  return [...new Set(languages.split('/'))].map((l) => {
    const simpleLanguage = l.split('_')[0];

    if (simpleLanguage === 'und') {
      return translate('Unknown');
    }

    return getLanguageName(simpleLanguage);
  });
}

function formatLanguages(languages: string | undefined) {
  if (!languages) {
    return null;
  }

  const splitLanguages = toLanguageNames(languages);

  if (splitLanguages.length > 3) {
    return (
      <span title={splitLanguages.join(', ')}>
        {splitLanguages.slice(0, 2).join(', ')}, {splitLanguages.length - 2}{' '}
        more
      </span>
    );
  }

  return <span>{splitLanguages.join(', ')}</span>;
}

export type MediaInfoType =
  | 'audio'
  | 'audioLanguages'
  | 'subtitles'
  | 'video'
  | 'videoDynamicRangeType';

interface MediaInfoProps {
  episodeFileId?: number;
  type: MediaInfoType;
}

function MediaInfo({ episodeFileId, type }: MediaInfoProps) {
  const episodeFile = useEpisodeFile(episodeFileId);

  // Checked before MediaInfo, both because it overrides it and because a file can carry these
  // without MediaInfo ever having read anything worth reporting.
  if (type === 'audioLanguages' && episodeFile?.namingAudioLanguages?.length) {
    return (
      <NamingLanguages
        languages={episodeFile.namingAudioLanguages}
        detectedNames={toLanguageNames(episodeFile.mediaInfo?.audioLanguages)}
      />
    );
  }

  if (type === 'subtitles' && episodeFile?.namingSubtitleLanguages?.length) {
    return (
      <NamingLanguages
        languages={episodeFile.namingSubtitleLanguages}
        detectedNames={toLanguageNames(episodeFile.mediaInfo?.subtitles)}
      />
    );
  }

  if (!episodeFile?.mediaInfo) {
    return null;
  }

  const {
    audioChannels,
    audioCodec,
    audioLanguages,
    subtitles,
    videoCodec,
    videoDynamicRangeType,
  } = episodeFile.mediaInfo;

  if (type === 'audio') {
    return (
      <span>
        {audioCodec ? audioCodec : ''}

        {audioCodec && audioChannels ? ' - ' : ''}

        {audioChannels ? audioChannels.toFixed(1) : ''}
      </span>
    );
  }

  if (type === 'audioLanguages') {
    return formatLanguages(audioLanguages);
  }

  if (type === 'subtitles') {
    return formatLanguages(subtitles);
  }

  if (type === 'video') {
    return <span>{videoCodec}</span>;
  }

  if (type === 'videoDynamicRangeType') {
    return <span>{videoDynamicRangeType}</span>;
  }

  return null;
}

export default MediaInfo;

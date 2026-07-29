import ModelBase from 'App/ModelBase';
import Series from 'Series/Series';

interface Episode extends ModelBase {
  seriesId: number;
  tvdbId: number;
  episodeFileId: number;
  seasonNumber: number;
  episodeNumber: number;
  airDate: string;
  airDateUtc?: string;
  lastSearchTime?: string;
  runtime: number;
  absoluteEpisodeNumber?: number;
  sceneSeasonNumber?: number;
  sceneEpisodeNumber?: number;
  sceneAbsoluteEpisodeNumber?: number;
  overview: string;
  title: string;
  episodeFile?: object;
  // The extra parts and versions of this episode. episodeFileId stays the primary file.
  additionalEpisodeFileIds?: number[];
  hasFile: boolean;
  monitored: boolean;
  grabbed?: boolean;
  unverifiedSceneNumbering: boolean;
  endTime?: string;
  grabDate?: string;
  seriesTitle?: string;
  queued?: boolean;
  series?: Series;
  finaleType?: string;
}

export default Episode;

import ModelBase from 'App/ModelBase';
import MultipleType from 'InteractiveImport/MultipleType';
import ReleaseType from 'InteractiveImport/ReleaseType';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';
import MediaInfo from 'typings/MediaInfo';

export interface EpisodeFile extends ModelBase {
  seriesId: number;
  seasonNumber: number;
  relativePath: string;
  path: string;
  size: number;
  dateAdded: string;
  sceneName: string;
  releaseGroup: string;
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  indexerFlags: number;
  releaseType: ReleaseType;
  // Which of the episode's files this one is. Both are absent when the file is the whole episode.
  multipleType?: MultipleType;
  multipleNumber?: number;
  mediaInfo: MediaInfo;
  qualityCutoffNotMet: boolean;
}

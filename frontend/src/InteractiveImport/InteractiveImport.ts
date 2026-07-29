import ModelBase from 'App/ModelBase';
import Episode from 'Episode/Episode';
import MultipleType from 'InteractiveImport/MultipleType';
import ReleaseType from 'InteractiveImport/ReleaseType';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import Series from 'Series/Series';
import CustomFormat from 'typings/CustomFormat';
import Rejection from 'typings/Rejection';

export interface InteractiveImportCommandOptions {
  path: string;
  folderName: string;
  seriesId: number;
  episodeIds: number[];
  releaseGroup?: string;
  quality: QualityModel;
  languages: Language[];
  namingAudioLanguages?: Language[];
  namingSubtitleLanguages?: Language[];
  manualCustomFormats?: number[];
  excludedCustomFormats?: number[];
  indexerFlags: number;
  releaseType: ReleaseType;
  multipleType?: MultipleType;
  multipleNumber?: number;
  downloadId?: string;
  episodeFileId?: number;
}

interface InteractiveImport extends ModelBase {
  path: string;
  relativePath: string;
  folderName: string;
  name: string;
  size: number;
  releaseGroup: string;
  quality: QualityModel;
  languages: Language[];
  // What the naming tokens should say about this file, when MediaInfo got it wrong or said nothing.
  namingAudioLanguages?: Language[];
  namingSubtitleLanguages?: Language[];
  // What MediaInfo read, so the picker can open on it rather than on nothing.
  detectedAudioLanguages?: Language[];
  detectedSubtitleLanguages?: Language[];
  manualCustomFormats?: number[];
  excludedCustomFormats?: number[];
  series?: Series;
  seasonNumber: number;
  episodes: Episode[];
  qualityWeight: number;
  customFormats: CustomFormat[];
  indexerFlags: number;
  releaseType: ReleaseType;
  // Which of the episode's files this one is. 'none' means the file is the whole episode, and importing
  // it replaces whatever the episode already has; anything else is kept alongside the other files.
  multipleType: MultipleType;
  multipleNumber: number;
  rejections: Rejection[];
  episodeFileId?: number;
}

export default InteractiveImport;

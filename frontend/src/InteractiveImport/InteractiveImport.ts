import ModelBase from 'App/ModelBase';
import Episode from 'Episode/Episode';
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
  rejections: Rejection[];
  episodeFileId?: number;
}

export default InteractiveImport;

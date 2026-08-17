export interface NamingLengthPrediction {
  seriesTitle: string;
  episodeTitle: string;
  path: string;
  fileNameLength: number;
  seasonFolderLength: number;
  seriesFolderLength: number;
  relativePathLength: number;
  fullPathLength: number;
  nameLimit: number;
  pathLimit: number;
  nameTooLong: boolean;
  pathTooLong: boolean;
}

export default interface NamingExample {
  lengthPrediction: NamingLengthPrediction | null;
  singleEpisodeExample: string;
  multiEpisodeExample: string;
  dailyEpisodeExample: string;
  animeEpisodeExample: string;
  animeMultiEpisodeExample: string;
  seriesFolderExample: string;
  seasonFolderExample: string;
  specialsFolderExample: string;
}

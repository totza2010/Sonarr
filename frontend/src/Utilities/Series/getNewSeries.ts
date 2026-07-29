import Series, {
  MonitorNewItems,
  SeriesMonitor,
  SeriesType,
} from 'Series/Series';

interface NewSeriesPayload {
  editionName?: string;
  rootFolderPath: string;
  monitor: SeriesMonitor;
  monitorNewItems: MonitorNewItems;
  qualityProfileId: number;
  seriesType: SeriesType;
  seasonFolder: boolean;
  tags: number[];
  searchForMissingEpisodes?: boolean;
  searchForCutoffUnmetEpisodes?: boolean;
}

function getNewSeries(series: Series, payload: NewSeriesPayload) {
  const {
    editionName = '',
    rootFolderPath,
    monitor,
    monitorNewItems,
    qualityProfileId,
    seriesType,
    seasonFolder,
    tags,
    searchForMissingEpisodes = false,
    searchForCutoffUnmetEpisodes = false,
  } = payload;

  const addOptions = {
    monitor,
    searchForMissingEpisodes,
    searchForCutoffUnmetEpisodes,
  };

  // Looking up a series that is already in the library returns that series, so the id and path of
  // the existing edition have to be dropped or the new edition would collide with it.
  if (editionName) {
    series.id = 0;
    series.path = '';
  }

  series.editionName = editionName;
  series.addOptions = addOptions;
  series.monitored = true;
  series.monitorNewItems = monitorNewItems;
  series.qualityProfileId = qualityProfileId;
  series.rootFolderPath = rootFolderPath;
  series.seriesType = seriesType;
  series.seasonFolder = seasonFolder;
  series.tags = tags;

  return series;
}

export default getNewSeries;

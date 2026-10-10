// What a folder is to be imported as when the series it matched is already in the library.
//
// Until editions existed the answer was always "nothing": one series per TVDB id, so a second folder
// for the same show could only be a duplicate and its row was locked. It can now also be another
// edition of that show, or the folder an edition that is already there should be pointed at.

export const SKIP = 'skip';
export const NEW_EDITION = 'new';
const REPLACE_PREFIX = 'replace:';

export function replaceAction(seriesId) {
  return `${REPLACE_PREFIX}${seriesId}`;
}

// Mirrors SeriesEditions.SameEdition: two editions cannot differ by case or padding alone.
export function isSameEdition(a, b) {
  return (a || '').trim().toLowerCase() === (b || '').trim().toLowerCase();
}

function getImportSeriesEdition(item, allSeries) {
  const selectedSeries = item && item.selectedSeries;

  const editions = selectedSeries ?
    allSeries.filter((s) => s.tvdbId === selectedSeries.tvdbId) :
    [];

  if (!editions.length) {
    return {
      isExistingSeries: false,
      isImportable: !!selectedSeries,
      editions,
      action: null,
      editionName: '',
      replaceSeries: null
    };
  }

  // The folder's own name is the only thing that can answer on its own: {edition-...} is written by
  // the series folder format, so a folder carrying one that is not in the library yet is that edition.
  // One that names an edition already here is left for a person to decide - the only thing it could be
  // imported as is a replacement, and that is never assumed.
  const folderEditionName = item.folderEditionName || '';
  const folderNamesNewEdition = !!folderEditionName &&
    !editions.some((e) => isSameEdition(e.editionName, folderEditionName));

  const action = item.editionAction || (folderNamesNewEdition ? NEW_EDITION : SKIP);
  const editionName = (item.editionName == null ? folderEditionName : item.editionName).trim();

  if (action === NEW_EDITION) {
    return {
      isExistingSeries: true,
      isImportable: !!editionName && !editions.some((e) => isSameEdition(e.editionName, editionName)),
      editions,
      action,
      editionName,
      replaceSeries: null
    };
  }

  if (action.startsWith(REPLACE_PREFIX)) {
    const replaceSeries = editions.find((e) => e.id === parseInt(action.substring(REPLACE_PREFIX.length))) || null;

    return {
      isExistingSeries: true,
      isImportable: !!replaceSeries,
      editions,
      action,
      editionName: '',
      replaceSeries
    };
  }

  return {
    isExistingSeries: true,
    isImportable: false,
    editions,
    action: SKIP,
    editionName: '',
    replaceSeries: null
  };
}

export default getImportSeriesEdition;

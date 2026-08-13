import Series from './Series';

// Every edition of a series carries the same TVDB id; nothing else groups them. A series with no id
// has no editions to speak of, so it comes back on its own.
export default function getSeriesEditions(
  allSeries: Series[],
  series?: Series
): Series[] {
  if (!series?.tvdbId) {
    return series ? [series] : [];
  }

  return allSeries.filter((s) => s.tvdbId === series.tvdbId);
}

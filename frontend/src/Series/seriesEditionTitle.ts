// Editions of a series share the title coming from the metadata source, so the edition name is
// what tells them apart in the UI.
export default function seriesEditionTitle(
  title: string,
  editionName?: string
) {
  return editionName ? `${title} (${editionName})` : title;
}

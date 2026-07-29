import MultipleType from 'InteractiveImport/MultipleType';

// The same marker the {Multiple} naming token writes, so what the UI shows and what ends up in the file
// name are never out of step.
export default function getMultipleMarker(
  multipleType: MultipleType | undefined,
  multipleNumber: number | undefined
) {
  if (!multipleNumber || !multipleType || multipleType === 'none') {
    return '';
  }

  return multipleType === 'version'
    ? `v${multipleNumber}`
    : `pt${multipleNumber}`;
}

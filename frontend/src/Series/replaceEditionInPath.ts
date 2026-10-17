// Plex reads an edition from {edition-Name} in the folder, so renaming an edition means renaming the
// token too. Only a token naming the old edition is touched: a folder somebody named by hand, without
// one, is left for them to change, and nothing else in the path is rewritten.
const EDITION_TOKEN = /\{edition-([^{}]+)\}(?!.*\{edition-)/i;

export default function replaceEditionInPath(
  path: string,
  oldEditionName: string,
  newEditionName: string
) {
  const newName = newEditionName.trim();
  const match = EDITION_TOKEN.exec(path);

  if (
    !newName ||
    !match ||
    match[1].trim().toLowerCase() !== oldEditionName.trim().toLowerCase()
  ) {
    return path;
  }

  return `${path.slice(0, match.index)}{edition-${newName}}${path.slice(
    match.index + match[0].length
  )}`;
}

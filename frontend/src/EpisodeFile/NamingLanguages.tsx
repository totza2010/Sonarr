import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import Language from 'Language/Language';
import translate from 'Utilities/String/translate';
import styles from './NamingLanguages.css';

interface NamingLanguagesProps {
  languages: Language[];
  detectedNames: string[];
}

interface Entry {
  name: string;
  state: 'kept' | 'added' | 'removed';
}

function EntryText({ entry }: { entry: Entry }) {
  if (entry.state === 'added') {
    return <Label kind={kinds.INFO}>{entry.name}</Label>;
  }

  if (entry.state === 'removed') {
    return <span className={styles.removed}>{entry.name}</span>;
  }

  return <span>{entry.name}</span>;
}

// The same cut-off the media info cells use, so a file with a dozen tracks does not push the rest of
// the row off screen. Everything is still readable from the title.
const MAX_SHOWN = 3;

// Three states, because what was taken away is as much a decision as what was put in: a language the
// file declared and that survived reads plainly, one that was added stands out, and one that was
// dropped is struck through so the choice is visible rather than just absent.
function NamingLanguages({ languages, detectedNames }: NamingLanguagesProps) {
  const chosenNames = languages.map((language) => language.name);

  const entries: Entry[] = [
    ...languages.map(
      (language): Entry => ({
        name: language.name,
        state: detectedNames.includes(language.name) ? 'kept' : 'added',
      })
    ),
    ...detectedNames
      .filter((name) => !chosenNames.includes(name))
      .map((name): Entry => ({ name, state: 'removed' as const })),
  ];

  if (!entries.length) {
    return null;
  }

  const isTruncated = entries.length > MAX_SHOWN;
  const shown = isTruncated ? entries.slice(0, MAX_SHOWN - 1) : entries;

  const title = entries
    .map((entry) =>
      entry.state === 'removed'
        ? `${entry.name} (${translate('NamingLanguageRemoved')})`
        : entry.name
    )
    .join(', ');

  return (
    <span title={isTruncated ? title : undefined}>
      {shown.map((entry, index) => (
        <span key={`${entry.state}-${entry.name}`}>
          {index === 0 ? null : ', '}

          <EntryText entry={entry} />
        </span>
      ))}

      {isTruncated ? `, ${entries.length - shown.length} more` : null}
    </span>
  );
}

export default NamingLanguages;

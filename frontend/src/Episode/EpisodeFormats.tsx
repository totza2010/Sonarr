import React from 'react';
import { useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import CustomFormat from 'typings/CustomFormat';
import styles from './EpisodeFormats.css';

interface EpisodeFormatsProps {
  formats: CustomFormat[];
  // Formats added to the file by hand rather than matched from its name, and ones its name matches
  // that were taken away. Left out by callers that have no such thing, which renders as it always did.
  manualIds?: number[];
  excludedIds?: number[];
}

function EpisodeFormats({
  formats,
  manualIds,
  excludedIds,
}: EpisodeFormatsProps) {
  const allFormats = useSelector(
    (state: AppState) => state.settings.customFormats.items
  );

  // Excluded formats are not in the list handed over, since the file no longer counts as them.
  // Showing them struck through says the removal was a decision rather than an absence.
  const excluded = excludedIds?.length
    ? allFormats.filter((format) => excludedIds.includes(format.id))
    : [];

  return (
    <div>
      {formats.map(({ id, name }) => (
        <Label
          key={id}
          kind={manualIds?.includes(id) ? kinds.PURPLE : kinds.INFO}
        >
          {name}
        </Label>
      ))}

      {excluded.map(({ id, name }) => (
        <span key={id} className={styles.excluded}>
          {name}
        </span>
      ))}
    </div>
  );
}

export default EpisodeFormats;

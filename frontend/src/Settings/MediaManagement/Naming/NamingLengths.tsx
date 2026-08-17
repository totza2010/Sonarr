import classNames from 'classnames';
import React from 'react';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import { NamingLengthPrediction } from 'typings/Settings/NamingExample';
import translate from 'Utilities/String/translate';
import styles from './NamingLengths.css';

interface NamingLengthRow {
  label: string;
  length: number;
  limit: number;
}

interface NamingLengthsProps {
  prediction: NamingLengthPrediction | null;
}

function NamingLengths({ prediction }: NamingLengthsProps) {
  if (!prediction) {
    return null;
  }

  // A row for every part, because they are capped in different places and fixed in different ways: the
  // three names are each one component, which is what a file system caps, and the whole path is what
  // anything opening the file has to carry.
  const rows: NamingLengthRow[] = [
    {
      label: translate('NamingLengthSeriesFolder'),
      length: prediction.seriesFolderLength,
      limit: prediction.nameLimit,
    },
    {
      label: translate('NamingLengthSeasonFolder'),
      length: prediction.seasonFolderLength,
      limit: prediction.nameLimit,
    },
    {
      label: translate('NamingLengthFileName'),
      length: prediction.fileNameLength,
      limit: prediction.nameLimit,
    },
    {
      // No limit of its own: it is the part a naming format can change, and it is here to show how the
      // three above add up.
      label: translate('NamingLengthRelativePath'),
      length: prediction.relativePathLength,
      limit: 0,
    },
    {
      label: translate('NamingLengthFullPath'),
      length: prediction.fullPathLength,
      limit: prediction.pathLimit,
    },
  ];

  return (
    <div className={styles.namingLengths}>
      <div className={styles.legend}>{translate('NamingLengths')}</div>

      <div className={styles.helpText}>
        {translate('NamingLengthsHelpText')}
      </div>

      {/* The invented titles, stretched: seeing them is how anyone can tell the numbers below are a
          ceiling rather than a measurement of one particular file. The whole path is on the tooltip. */}
      <div className={styles.series} title={prediction.path}>
        {`${prediction.seriesTitle} - ${prediction.episodeTitle}`}
      </div>

      <table className={styles.table}>
        <tbody>
          {rows.map((row) => {
            // A season folder is empty when the series does not use one, and a row of zero says nothing.
            if (row.length === 0) {
              return null;
            }

            const isOver = row.limit > 0 && row.length > row.limit;

            return (
              <tr key={row.label}>
                <td className={styles.label}>{row.label}</td>

                <td
                  className={classNames(styles.length, isOver && styles.over)}
                >
                  {row.length}
                </td>

                <td className={styles.limit}>
                  {row.limit > 0 ? `/ ${row.limit}` : null}
                </td>

                <td className={styles.flag}>
                  {isOver ? (
                    <Icon
                      name={icons.WARNING}
                      kind={kinds.WARNING}
                      title={translate('NamingLengthOverLimit')}
                    />
                  ) : null}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export default NamingLengths;

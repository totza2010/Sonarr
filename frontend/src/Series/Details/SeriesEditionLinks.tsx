import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { kinds } from 'Helpers/Props';
import getSeriesEditions from 'Series/getSeriesEditions';
import Series from 'Series/Series';
import { executeCommand } from 'Store/Actions/commandActions';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import translate from 'Utilities/String/translate';
import styles from './SeriesEditionLinks.css';

interface SeriesEditionLinksProps {
  seriesId: number;
  tvdbId: number;
}

// Every edition of the series, with the one on screen marked, so the others are a click away rather
// than a search. Nothing is shown for a series that has only the one.
function SeriesEditionLinks({ seriesId, tvdbId }: SeriesEditionLinksProps) {
  const dispatch = useDispatch();
  const allSeries: Series[] = useSelector(createAllSeriesSelector());
  const isSwapping: boolean = useSelector(
    createCommandExecutingSelector(commandNames.SWAP_MAIN_EDITION)
  );
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const editions = useMemo(() => {
    const series = allSeries.find((s) => s.id === seriesId);

    // The main edition first, the rest by name.
    return getSeriesEditions(allSeries, series).sort((a, b) => {
      if (!a.editionName || !b.editionName) {
        return a.editionName ? 1 : -1;
      }

      return a.editionName.localeCompare(b.editionName);
    });
  }, [allSeries, seriesId]);

  const current = editions.find((e) => e.id === seriesId);
  const main = editions.find((e) => !e.editionName);

  const handleSwapPress = useCallback(() => {
    setIsConfirmOpen(true);
  }, []);

  const handleConfirmClose = useCallback(() => {
    setIsConfirmOpen(false);
  }, []);

  const handleSwapConfirm = useCallback(() => {
    setIsConfirmOpen(false);

    dispatch(
      executeCommand({ name: commandNames.SWAP_MAIN_EDITION, seriesId })
    );
  }, [seriesId, dispatch]);

  if (!tvdbId || editions.length < 2) {
    return null;
  }

  return (
    <div className={styles.editions}>
      <span className={styles.label}>
        {translate('SeriesEditionsCount', { count: editions.length })}
      </span>

      {editions.map((edition) => {
        const name = edition.editionName || translate('MainEdition');

        if (edition.id === seriesId) {
          return (
            <span
              key={edition.id}
              className={classNames(styles.edition, styles.current)}
              title={translate('SeriesEditionCurrent')}
            >
              {name}
            </span>
          );
        }

        return (
          <Link
            key={edition.id}
            className={styles.edition}
            to={`/series/${edition.titleSlug}`}
          >
            {name}
          </Link>
        );
      })}

      {current?.editionName ? (
        <SpinnerButton
          className={styles.swap}
          isSpinning={isSwapping}
          onPress={handleSwapPress}
        >
          {translate('MakeMainEdition')}
        </SpinnerButton>
      ) : null}

      <ConfirmModal
        isOpen={isConfirmOpen}
        kind={kinds.WARNING}
        title={translate('MakeMainEdition')}
        message={translate('MakeMainEditionMessage', {
          edition: current?.editionName ?? '',
          mainPath: main?.path ?? '-',
          editionPath: current?.path ?? '-',
        })}
        confirmLabel={translate('MakeMainEdition')}
        onConfirm={handleSwapConfirm}
        onCancel={handleConfirmClose}
      />
    </div>
  );
}

export default SeriesEditionLinks;

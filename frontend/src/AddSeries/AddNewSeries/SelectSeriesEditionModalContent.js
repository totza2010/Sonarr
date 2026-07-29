import PropTypes from 'prop-types';
import React from 'react';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import SeriesIndexProgressBar from 'Series/Index/ProgressBar/SeriesIndexProgressBar';
import SeriesEditionBadge from 'Series/SeriesEditionBadge';
import SeriesPoster from 'Series/SeriesPoster';
import translate from 'Utilities/String/translate';
import styles from './SelectSeriesEditionModalContent.css';

// The series index gives the poster an explicit size and hands the same width to the progress bar,
// which is what keeps the two the same width there. The ratio is its own.
const POSTER_WIDTH = 150;
const POSTER_HEIGHT = Math.ceil((250 / 170) * POSTER_WIDTH);
const posterStyle = { width: `${POSTER_WIDTH}px`, height: `${POSTER_HEIGHT}px` };

function SelectSeriesEditionModalContent(props) {
  const {
    title,
    editions,
    onAddEditionPress,
    onModalClose
  } = props;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {`${title} - ${translate('SelectEdition')}`}
      </ModalHeader>

      <ModalBody>
        {/* Cards rather than a list of paths: an edition is picked out by its artwork and its name
            everywhere else, and the path is the least of what tells them apart. */}
        <div className={styles.editions}>
          {
            editions.map((series) => {
              const {
                episodeCount = 0,
                episodeFileCount = 0,
                totalEpisodeCount = 0
              } = series.statistics || {};

              return (
                <Link
                  key={series.id}
                  className={styles.edition}
                  to={`/series/${series.titleSlug}`}
                  onPress={onModalClose}
                >
                  <div className={styles.posterContainer}>
                    <SeriesPoster
                      style={posterStyle}
                      images={series.images}
                      size={250}
                      lazy={false}
                      overflow={true}
                    />

                    <SeriesEditionBadge
                      className={styles.badge}
                      editionName={series.editionName}
                    />
                  </div>

                  <SeriesIndexProgressBar
                    seriesId={series.id}
                    monitored={series.monitored}
                    status={series.status}
                    episodeCount={episodeCount}
                    episodeFileCount={episodeFileCount}
                    totalEpisodeCount={totalEpisodeCount}
                    width={POSTER_WIDTH}
                    detailedProgressBar={false}
                    isStandalone={false}
                  />

                  <div className={styles.editionName}>
                    {series.editionName || translate('MainEdition')}
                  </div>

                  <div className={styles.path} title={series.path}>
                    {series.path}
                  </div>
                </Link>
              );
            })
          }
        </div>
      </ModalBody>

      <ModalFooter>
        <Button
          kind={kinds.SUCCESS}
          onPress={onAddEditionPress}
        >
          {translate('AddEdition')}
        </Button>

        <Button onPress={onModalClose}>
          {translate('Cancel')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

SelectSeriesEditionModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  onAddEditionPress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default SelectSeriesEditionModalContent;

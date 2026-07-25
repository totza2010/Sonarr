import PropTypes from 'prop-types';
import React, { Component } from 'react';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import MetadataAttribution from 'Components/MetadataAttribution';
import { icons, kinds, sizes } from 'Helpers/Props';
import SeriesGenres from 'Series/SeriesGenres';
import SeriesPoster from 'Series/SeriesPoster';
import translate from 'Utilities/String/translate';
import AddNewSeriesModal from './AddNewSeriesModal';
import SelectSeriesEditionModal from './SelectSeriesEditionModal';
import styles from './AddNewSeriesSearchResult.css';

class AddNewSeriesSearchResult extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isNewAddSeriesModalOpen: false,
      isSelectEditionModalOpen: false,
      isAddingEdition: false
    };
  }

  //
  // Listeners

  // An existing series can have editions, so go through a picker instead of straight to the series.
  onPress = () => {
    if (this.props.isExistingSeries) {
      this.setState({ isSelectEditionModalOpen: true });
      return;
    }

    this.setState({ isNewAddSeriesModalOpen: true, isAddingEdition: false });
  };

  onAddEditionPress = () => {
    this.setState({
      isSelectEditionModalOpen: false,
      isNewAddSeriesModalOpen: true,
      isAddingEdition: true
    });
  };

  onSelectEditionModalClose = () => {
    this.setState({ isSelectEditionModalOpen: false });
  };

  onAddSeriesModalClose = () => {
    this.setState({ isNewAddSeriesModalOpen: false, isAddingEdition: false });
  };

  onTVDBLinkPress = (event) => {
    event.stopPropagation();
  };

  //
  // Render

  render() {
    const {
      tvdbId,
      title,
      year,
      network,
      originalLanguage,
      genres,
      status,
      overview,
      statistics,
      ratings,
      folder,
      seriesType,
      images,
      isExistingSeries,
      editions,
      isSmallScreen
    } = this.props;

    const seasonCount = statistics.seasonCount;

    const {
      isNewAddSeriesModalOpen,
      isSelectEditionModalOpen,
      isAddingEdition
    } = this.state;

    const linkProps = { onPress: this.onPress };
    let seasons = translate('OneSeason');

    if (seasonCount > 1) {
      seasons = translate('CountSeasons', { count: seasonCount });
    }

    return (
      <div className={styles.searchResult}>
        <Link
          className={styles.underlay}
          {...linkProps}
        />

        <div className={styles.overlay}>
          {
            isSmallScreen ?
              null :
              <SeriesPoster
                className={styles.poster}
                images={images}
                size={250}
                overflow={true}
                lazy={false}
              />
          }

          <div className={styles.content}>
            <div className={styles.titleRow}>
              <div className={styles.titleContainer}>
                <div className={styles.title}>
                  {title}

                  {
                    !title.contains(year) && year ?
                      <span className={styles.year}>
                        ({year})
                      </span> :
                      null
                  }
                </div>
              </div>

              <div className={styles.icons}>
                {
                  isExistingSeries ?
                    <Icon
                      className={styles.alreadyExistsIcon}
                      name={icons.CHECK_CIRCLE}
                      size={36}
                      title={translate('AlreadyInYourLibrary')}
                    /> :
                    null
                }

                <Link
                  className={styles.tvdbLink}
                  to={`https://www.thetvdb.com/?tab=series&id=${tvdbId}`}
                  onPress={this.onTVDBLinkPress}
                >
                  <Icon
                    className={styles.tvdbLinkIcon}
                    name={icons.EXTERNAL_LINK}
                    size={28}
                  />
                </Link>
              </div>
            </div>

            <div>
              <Label size={sizes.LARGE}>
                <HeartRating
                  rating={ratings.value}
                  votes={ratings.votes}
                  iconSize={13}
                />
              </Label>

              {
                originalLanguage?.name ?
                  <Label size={sizes.LARGE}>
                    <Icon
                      name={icons.LANGUAGE}
                      size={13}
                    />

                    <span className={styles.originalLanguageName}>
                      {originalLanguage.name}
                    </span>
                  </Label> :
                  null
              }

              {
                network ?
                  <Label size={sizes.LARGE}>
                    <Icon
                      name={icons.NETWORK}
                      size={13}
                    />

                    <span className={styles.network}>
                      {network}
                    </span>
                  </Label> :
                  null
              }

              {
                genres.length > 0 ?
                  <Label size={sizes.LARGE}>
                    <Icon
                      name={icons.GENRE}
                      size={13}
                    />
                    <SeriesGenres className={styles.genres} genres={genres} />
                  </Label> :
                  null
              }

              {
                seasonCount ?
                  <Label size={sizes.LARGE}>
                    {seasons}
                  </Label> :
                  null
              }

              {
                status === 'ended' ?
                  <Label
                    kind={kinds.DANGER}
                    size={sizes.LARGE}
                  >
                    {translate('Ended')}
                  </Label> :
                  null
              }

              {
                status === 'upcoming' ?
                  <Label
                    kind={kinds.INFO}
                    size={sizes.LARGE}
                  >
                    {translate('Upcoming')}
                  </Label> :
                  null
              }
            </div>

            <div className={styles.overview}>
              {overview}
            </div>

            <MetadataAttribution />
          </div>
        </div>

        <SelectSeriesEditionModal
          isOpen={isSelectEditionModalOpen}
          title={title}
          editions={editions}
          onAddEditionPress={this.onAddEditionPress}
          onModalClose={this.onSelectEditionModalClose}
        />

        <AddNewSeriesModal
          isOpen={isNewAddSeriesModalOpen && (!isExistingSeries || isAddingEdition)}
          tvdbId={tvdbId}
          title={title}
          year={year}
          overview={overview}
          folder={folder}
          initialSeriesType={seriesType}
          images={images}
          isAddingEdition={isAddingEdition}
          onModalClose={this.onAddSeriesModalClose}
        />
      </div>
    );
  }
}

AddNewSeriesSearchResult.propTypes = {
  tvdbId: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  titleSlug: PropTypes.string.isRequired,
  year: PropTypes.number.isRequired,
  network: PropTypes.string,
  originalLanguage: PropTypes.object,
  genres: PropTypes.arrayOf(PropTypes.string),
  status: PropTypes.string.isRequired,
  overview: PropTypes.string,
  statistics: PropTypes.object.isRequired,
  ratings: PropTypes.object.isRequired,
  folder: PropTypes.string.isRequired,
  seriesType: PropTypes.string.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  isExistingSeries: PropTypes.bool.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSmallScreen: PropTypes.bool.isRequired
};

AddNewSeriesSearchResult.defaultProps = {
  genres: [],
  editions: []
};

export default AddNewSeriesSearchResult;

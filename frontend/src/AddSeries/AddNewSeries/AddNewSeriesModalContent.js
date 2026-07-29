import PropTypes from 'prop-types';
import React, { Component } from 'react';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import SeriesTypePopoverContent from 'AddSeries/SeriesTypePopoverContent';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import seriesEditionSuggestions from 'Series/seriesEditionSuggestions';
import SeriesPoster from 'Series/SeriesPoster';
import * as seriesTypes from 'Utilities/Series/seriesTypes';
import translate from 'Utilities/String/translate';
import styles from './AddNewSeriesModalContent.css';

// The edition suggestions are a menu to pick from, so they show on focus instead of waiting for
// the first character to be typed.
function alwaysRenderSuggestions() {
  return true;
}

class AddNewSeriesModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      seriesType: props.initialSeriesType === seriesTypes.STANDARD ?
        props.seriesType.value :
        props.initialSeriesType,
      editionName: ''
    };
  }

  componentDidUpdate(prevProps) {
    if (this.props.seriesType.value !== prevProps.seriesType.value) {
      this.setState({ seriesType: this.props.seriesType.value });
    }
  }

  //
  // Listeners

  onQualityProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'qualityProfileId', value: parseInt(value) });
  };

  onEditionNameChange = ({ value }) => {
    this.setState({ editionName: value });
  };

  onAddSeriesPress = () => {
    const {
      seriesType,
      editionName
    } = this.state;

    this.props.onAddSeriesPress(
      seriesType,
      editionName.trim()
    );
  };

  //
  // Render

  render() {
    const {
      title,
      year,
      overview,
      images,
      isAdding,
      rootFolderPath,
      monitor,
      qualityProfileId,
      seriesType,
      seasonFolder,
      searchForMissingEpisodes,
      searchForCutoffUnmetEpisodes,
      folder,
      tags,
      isAddingEdition,
      isSmallScreen,
      isWindows,
      onModalClose,
      onInputChange,
      ...otherProps
    } = this.props;

    const { editionName } = this.state;
    const trimmedEditionName = editionName.trim();

    // Mirrors the folder GetSeriesFolder builds for an edition, so the preview matches what is created.
    const editionFolder = trimmedEditionName ?
      `${folder} {edition-${trimmedEditionName}}` :
      folder;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {title}

          {
            !title.contains(year) && !!year &&
              <span className={styles.year}>({year})</span>
          }
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            {
              isSmallScreen ?
                null :
                <div className={styles.poster}>
                  <SeriesPoster
                    className={styles.poster}
                    images={images}
                    size={250}
                  />
                </div>
            }

            <div className={styles.info}>
              {
                overview ?
                  <div className={styles.overview}>
                    {overview}
                  </div> :
                  null
              }

              <Form {...otherProps}>
                {
                  isAddingEdition ?
                    <FormGroup>
                      <FormLabel>{translate('Edition')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.AUTO_COMPLETE}
                        name="editionName"
                        value={editionName}
                        values={seriesEditionSuggestions}
                        shouldRenderSuggestions={alwaysRenderSuggestions}
                        helpText={translate('SeriesEditionHelpText')}
                        onChange={this.onEditionNameChange}
                      />
                    </FormGroup> :
                    null
                }

                <FormGroup>
                  <FormLabel>{translate('RootFolder')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.ROOT_FOLDER_SELECT}
                    name="rootFolderPath"
                    valueOptions={{
                      seriesFolder: editionFolder,
                      isWindows
                    }}
                    selectedValueOptions={{
                      seriesFolder: editionFolder,
                      isWindows
                    }}
                    helpText={translate('AddNewSeriesRootFolderHelpText', { folder: editionFolder })}
                    onChange={onInputChange}
                    {...rootFolderPath}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>
                    {translate('Monitor')}

                    <Popover
                      anchor={
                        <Icon
                          className={styles.labelIcon}
                          name={icons.INFO}
                        />
                      }
                      title={translate('MonitoringOptions')}
                      body={<SeriesMonitoringOptionsPopoverContent />}
                      position={tooltipPositions.RIGHT}
                    />
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.MONITOR_EPISODES_SELECT}
                    name="monitor"
                    onChange={onInputChange}
                    {...monitor}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>{translate('QualityProfile')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.QUALITY_PROFILE_SELECT}
                    name="qualityProfileId"
                    onChange={this.onQualityProfileIdChange}
                    {...qualityProfileId}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>
                    {translate('SeriesType')}

                    <Popover
                      anchor={
                        <Icon
                          className={styles.labelIcon}
                          name={icons.INFO}
                        />
                      }
                      title={translate('SeriesTypes')}
                      body={<SeriesTypePopoverContent />}
                      position={tooltipPositions.RIGHT}
                    />
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.SERIES_TYPE_SELECT}
                    name="seriesType"
                    onChange={onInputChange}
                    {...seriesType}
                    value={this.state.seriesType}
                    helpText={translate('SeriesTypesHelpText')}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>{translate('SeasonFolder')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="seasonFolder"
                    onChange={onInputChange}
                    {...seasonFolder}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>{translate('Tags')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TAG}
                    name="tags"
                    onChange={onInputChange}
                    {...tags}
                  />
                </FormGroup>
              </Form>
            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <div>
            <label className={styles.searchLabelContainer}>
              <span className={styles.searchLabel}>
                {translate('AddNewSeriesSearchForMissingEpisodes')}
              </span>

              <CheckInput
                containerClassName={styles.searchInputContainer}
                className={styles.searchInput}
                name="searchForMissingEpisodes"
                onChange={onInputChange}
                {...searchForMissingEpisodes}
              />
            </label>

            <label className={styles.searchLabelContainer}>
              <span className={styles.searchLabel}>
                {translate('AddNewSeriesSearchForCutoffUnmetEpisodes')}
              </span>

              <CheckInput
                containerClassName={styles.searchInputContainer}
                className={styles.searchInput}
                name="searchForCutoffUnmetEpisodes"
                onChange={onInputChange}
                {...searchForCutoffUnmetEpisodes}
              />
            </label>
          </div>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            isDisabled={isAddingEdition && !editionName.trim()}
            onPress={this.onAddSeriesPress}
          >
            {translate('AddSeriesWithTitle', { title })}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewSeriesModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  year: PropTypes.number.isRequired,
  overview: PropTypes.string,
  initialSeriesType: PropTypes.string.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  seriesType: PropTypes.object.isRequired,
  seasonFolder: PropTypes.object.isRequired,
  searchForMissingEpisodes: PropTypes.object.isRequired,
  searchForCutoffUnmetEpisodes: PropTypes.object.isRequired,
  folder: PropTypes.string.isRequired,
  isAddingEdition: PropTypes.bool,
  tags: PropTypes.object.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  isWindows: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onAddSeriesPress: PropTypes.func.isRequired
};

export default AddNewSeriesModalContent;

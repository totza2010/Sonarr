import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addSeries, setAddSeriesDefault } from 'Store/Actions/addSeriesActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewSeriesModalContent from './AddNewSeriesModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.addSeries,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (addSeriesState, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        defaults
      } = addSeriesState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(defaults, {}, addError);

      return {
        isAdding,
        addError,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setAddSeriesDefault,
  addSeries
};

class AddNewSeriesModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    // An edition leaves the search result marked as already added, so the modal has to be closed
    // from here rather than when the result stops being addable.
    if (prevProps.isAdding && !this.props.isAdding && !this.props.addError) {
      this.props.onModalClose();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setAddSeriesDefault({ [name]: value });
  };

  onAddSeriesPress = (seriesType, editionName) => {
    const {
      tvdbId,
      rootFolderPath,
      monitor,
      qualityProfileId,
      seasonFolder,
      searchForMissingEpisodes,
      searchForCutoffUnmetEpisodes,
      tags
    } = this.props;

    this.props.addSeries({
      tvdbId,
      editionName,
      rootFolderPath: rootFolderPath.value,
      monitor: monitor.value,
      qualityProfileId: qualityProfileId.value,
      seriesType,
      seasonFolder: seasonFolder.value,
      searchForMissingEpisodes: searchForMissingEpisodes.value,
      searchForCutoffUnmetEpisodes: searchForCutoffUnmetEpisodes.value,
      tags: tags.value
    });
  };

  //
  // Render

  render() {
    return (
      <AddNewSeriesModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddSeriesPress={this.onAddSeriesPress}
      />
    );
  }
}

AddNewSeriesModalContentConnector.propTypes = {
  tvdbId: PropTypes.number.isRequired,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  seriesType: PropTypes.object.isRequired,
  seasonFolder: PropTypes.object.isRequired,
  searchForMissingEpisodes: PropTypes.object.isRequired,
  searchForCutoffUnmetEpisodes: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAddSeriesDefault: PropTypes.func.isRequired,
  addSeries: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewSeriesModalContentConnector);

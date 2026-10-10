import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setImportSeriesValue } from 'Store/Actions/importSeriesActions';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import getImportSeriesEdition, { SKIP } from './getImportSeriesEdition';
import ImportSeriesRow from './ImportSeriesRow';

function createImportSeriesItemSelector() {
  return createSelector(
    (state, { id }) => id,
    (state) => state.importSeries.items,
    (id, items) => {
      return _.find(items, { id }) || {};
    }
  );
}

function createMapStateToProps() {
  return createSelector(
    createImportSeriesItemSelector(),
    createAllSeriesSelector(),
    (item, series) => {
      const edition = getImportSeriesEdition(item, series);

      return {
        ...item,
        isExistingSeries: edition.isExistingSeries,
        isImportable: edition.isImportable,
        editions: edition.editions,
        editionAction: edition.action,
        editionName: edition.editionName
      };
    }
  );
}

const mapDispatchToProps = {
  setImportSeriesValue
};

class ImportSeriesRowConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setImportSeriesValue({
      id: this.props.id,
      [name]: value
    });
  };

  // Saying what a folder is to be imported as is the same act as choosing to import it, so the row is
  // ticked with it - and unticked when it goes back to being skipped.
  onEditionChange = ({ action, editionName }) => {
    const {
      id,
      onSelectedChange
    } = this.props;

    this.props.setImportSeriesValue({
      id,
      editionAction: action,
      editionName
    });

    onSelectedChange({ id, value: action !== SKIP });
  };

  //
  // Render

  render() {
    // Don't show the row until we have the information we require for it.

    const {
      items,
      monitor,
      seriesType,
      seasonFolder
    } = this.props;

    if (!items || !monitor || !seriesType || !seasonFolder == null) {
      return null;
    }

    return (
      <ImportSeriesRow
        {...this.props}
        onInputChange={this.onInputChange}
        onEditionChange={this.onEditionChange}
        onSeriesSelect={this.onSeriesSelect}
      />
    );
  }
}

ImportSeriesRowConnector.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  id: PropTypes.string.isRequired,
  monitor: PropTypes.string,
  seriesType: PropTypes.string,
  seasonFolder: PropTypes.bool,
  items: PropTypes.arrayOf(PropTypes.object),
  onSelectedChange: PropTypes.func.isRequired,
  setImportSeriesValue: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportSeriesRowConnector);

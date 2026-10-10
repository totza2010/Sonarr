import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { kinds } from 'Helpers/Props';
import createRouteMatchShape from 'Helpers/Props/Shapes/createRouteMatchShape';
import { setAddSeriesDefault } from 'Store/Actions/addSeriesActions';
import { clearImportSeries, importSeries, setImportSeriesValue } from 'Store/Actions/importSeriesActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import translate from 'Utilities/String/translate';
import getImportSeriesEdition from './getImportSeriesEdition';
import ImportSeries from './ImportSeries';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => match,
    (state) => state.rootFolders,
    (state) => state.addSeries,
    (state) => state.importSeries,
    (state) => state.settings.qualityProfiles,
    (state) => state.series.items,
    (
      match,
      rootFolders,
      addSeries,
      importSeriesState,
      qualityProfiles,
      allSeries
    ) => {
      const {
        isFetching: rootFoldersFetching,
        isPopulated: rootFoldersPopulated,
        error: rootFoldersError,
        items
      } = rootFolders;

      const rootFolderId = parseInt(match.params.rootFolderId);

      const result = {
        rootFolderId,
        rootFoldersFetching,
        rootFoldersPopulated,
        rootFoldersError,
        qualityProfiles: qualityProfiles.items,
        defaultQualityProfileId: addSeries.defaults.qualityProfileId,
        allSeries
      };

      if (items.length) {
        const rootFolder = _.find(items, { id: rootFolderId });

        return {
          ...result,
          ...rootFolder,
          items: importSeriesState.items
        };
      }

      return result;
    }
  );
}

const mapDispatchToProps = {
  dispatchSetImportSeriesValue: setImportSeriesValue,
  dispatchImportSeries: importSeries,
  dispatchClearImportSeries: clearImportSeries,
  dispatchFetchRootFolders: fetchRootFolders,
  dispatchSetAddSeriesDefault: setAddSeriesDefault
};

class ImportSeriesConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      pendingIds: null
    };
  }

  componentDidMount() {
    const {
      rootFolderId,
      qualityProfiles,
      defaultQualityProfileId,
      dispatchFetchRootFolders,
      dispatchSetAddSeriesDefault
    } = this.props;

    dispatchFetchRootFolders({ id: rootFolderId, timeout: false });

    let setDefaults = false;
    const setDefaultPayload = {};

    if (
      !defaultQualityProfileId ||
      !qualityProfiles.some((p) => p.id === defaultQualityProfileId)
    ) {
      setDefaults = true;
      setDefaultPayload.qualityProfileId = qualityProfiles[0].id;
    }

    if (setDefaults) {
      dispatchSetAddSeriesDefault(setDefaultPayload);
    }
  }

  componentWillUnmount() {
    this.props.dispatchClearImportSeries();
  }

  //
  // Listeners

  onInputChange = (ids, name, value) => {
    this.props.dispatchSetAddSeriesDefault({ [name]: value });

    ids.forEach((id) => {
      this.props.dispatchSetImportSeriesValue({
        id,
        [name]: value
      });
    });
  };

  // Pointing a series that is already in the library at another folder changes what it holds, so it
  // is said back before it happens. Adding a series or an edition never needed asking and still does not.
  getReplacements = (ids) => {
    const {
      items = [],
      allSeries
    } = this.props;

    return ids.reduce((acc, id) => {
      const item = items.find((i) => i.id === id);
      const edition = item ? getImportSeriesEdition(item, allSeries) : null;

      if (edition && edition.isImportable && edition.replaceSeries) {
        acc.push({ series: edition.replaceSeries, path: item.path });
      }

      return acc;
    }, []);
  };

  onImportPress = (ids) => {
    if (this.getReplacements(ids).length) {
      this.setState({ pendingIds: ids });
      return;
    }

    this.props.dispatchImportSeries({ ids });
  };

  onConfirmReplace = () => {
    const ids = this.state.pendingIds;

    this.setState({ pendingIds: null });
    this.props.dispatchImportSeries({ ids });
  };

  onCancelReplace = () => {
    this.setState({ pendingIds: null });
  };

  //
  // Render

  render() {
    const replacements = this.state.pendingIds ? this.getReplacements(this.state.pendingIds) : [];

    return (
      <>
        <ImportSeries
          {...this.props}
          onInputChange={this.onInputChange}
          onImportPress={this.onImportPress}
        />

        <ConfirmModal
          isOpen={!!replacements.length}
          kind={kinds.WARNING}
          title={translate('ImportSeriesReplaceConfirmTitle')}
          message={
            <div>
              <div>{translate('ImportSeriesReplaceConfirmMessage')}</div>

              <ul>
                {
                  replacements.map(({ series, path }) => {
                    return (
                      <li key={series.id}>
                        {series.title}{series.editionName ? ` [${series.editionName}]` : ''}: {series.path} &rarr; {path}
                      </li>
                    );
                  })
                }
              </ul>
            </div>
          }
          confirmLabel={translate('Replace')}
          onConfirm={this.onConfirmReplace}
          onCancel={this.onCancelReplace}
        />
      </>
    );
  }
}

const routeMatchShape = createRouteMatchShape({
  rootFolderId: PropTypes.string.isRequired
});

ImportSeriesConnector.propTypes = {
  match: routeMatchShape.isRequired,
  rootFolderId: PropTypes.number.isRequired,
  rootFoldersFetching: PropTypes.bool.isRequired,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  qualityProfiles: PropTypes.arrayOf(PropTypes.object).isRequired,
  defaultQualityProfileId: PropTypes.number.isRequired,
  items: PropTypes.arrayOf(PropTypes.object),
  allSeries: PropTypes.arrayOf(PropTypes.object).isRequired,
  dispatchSetImportSeriesValue: PropTypes.func.isRequired,
  dispatchImportSeries: PropTypes.func.isRequired,
  dispatchClearImportSeries: PropTypes.func.isRequired,
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchSetAddSeriesDefault: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportSeriesConnector);

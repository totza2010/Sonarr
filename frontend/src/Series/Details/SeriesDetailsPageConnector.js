import { push } from 'connected-react-router';
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import NotFound from 'Components/NotFound';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import translate from 'Utilities/String/translate';
import SeriesDetailsConnector from './SeriesDetailsConnector';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => match,
    createAllSeriesSelector(),
    (match, allSeries) => {
      const titleSlug = match.params.titleSlug;
      const seriesIndex = _.findIndex(allSeries, { titleSlug });

      if (seriesIndex > -1) {
        return {
          titleSlug,
          seriesId: allSeries[seriesIndex].id
        };
      }

      // Renaming an edition changes its slug under the page that shows it. The series is still here,
      // so the page follows it rather than giving up.
      return {
        allSeries
      };
    }
  );
}

const mapDispatchToProps = {
  push
};

class SeriesDetailsPageConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    if (!this.props.titleSlug) {
      const moved = prevProps.seriesId ?
        (this.props.allSeries || []).find((s) => s.id === prevProps.seriesId) :
        null;

      this.props.push(moved ?
        `${window.Sonarr.urlBase}/series/${moved.titleSlug}` :
        `${window.Sonarr.urlBase}/`);

      return;
    }
  }

  //
  // Render

  render() {
    const {
      titleSlug
    } = this.props;

    if (!titleSlug) {
      return (
        <NotFound
          message={translate('SeriesCannotBeFound')}
        />
      );
    }

    return (
      <SeriesDetailsConnector
        titleSlug={titleSlug}
      />
    );
  }
}

SeriesDetailsPageConnector.propTypes = {
  titleSlug: PropTypes.string,
  seriesId: PropTypes.number,
  allSeries: PropTypes.arrayOf(PropTypes.object),
  match: PropTypes.shape({ params: PropTypes.shape({ titleSlug: PropTypes.string.isRequired }).isRequired }).isRequired,
  push: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(SeriesDetailsPageConnector);

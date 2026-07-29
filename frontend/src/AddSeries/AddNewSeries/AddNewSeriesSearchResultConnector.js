import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createExistingSeriesSelector from 'Store/Selectors/createExistingSeriesSelector';
import createSeriesEditionsSelector from 'Store/Selectors/createSeriesEditionsSelector';
import AddNewSeriesSearchResult from './AddNewSeriesSearchResult';

function createMapStateToProps() {
  return createSelector(
    createExistingSeriesSelector(),
    createSeriesEditionsSelector(),
    createDimensionsSelector(),
    (isExistingSeries, editions, dimensions) => {
      return {
        isExistingSeries,
        editions,
        isSmallScreen: dimensions.isSmallScreen
      };
    }
  );
}

export default connect(createMapStateToProps)(AddNewSeriesSearchResult);

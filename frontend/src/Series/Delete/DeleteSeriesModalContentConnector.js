import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import getSeriesEditions from 'Series/getSeriesEditions';
import { deleteSeries, setDeleteOption } from 'Store/Actions/seriesActions';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import createSeriesSelector from 'Store/Selectors/createSeriesSelector';
import DeleteSeriesModalContent from './DeleteSeriesModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.series.deleteOptions,
    createSeriesSelector(),
    createAllSeriesSelector(),
    (deleteOptions, series, allSeries) => {
      // The main edition cannot go while other editions stay; the server refuses it, so the dialog
      // says so up front rather than failing after the button is pressed.
      const otherEditionCount = series && !series.editionName ?
        getSeriesEditions(allSeries, series).length - 1 :
        0;

      return {
        ...series,
        otherEditionCount,
        deleteOptions
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onDeleteOptionChange(option) {
      dispatch(
        setDeleteOption({
          [option.name]: option.value
        })
      );
    },

    onDeletePress(deleteFiles, addImportListExclusion) {
      dispatch(
        deleteSeries({
          id: props.seriesId,
          deleteFiles,
          addImportListExclusion
        })
      );

      props.onModalClose(true);
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(DeleteSeriesModalContent);

import PropTypes from 'prop-types';
import React from 'react';
import FormInputGroup from 'Components/Form/FormInputGroup';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import { inputTypes } from 'Helpers/Props';
import ImportSeriesEdition from './ImportSeriesEdition';
import ImportSeriesSelectSeriesConnector from './SelectSeries/ImportSeriesSelectSeriesConnector';
import styles from './ImportSeriesRow.css';

function ImportSeriesRow(props) {
  const {
    id,
    relativePath,
    monitor,
    qualityProfileId,
    seasonFolder,
    seriesType,
    selectedSeries,
    isExistingSeries,
    isImportable,
    editions,
    editionAction,
    editionName,
    folderEditionName,
    isSelected,
    onSelectedChange,
    onInputChange,
    onEditionChange
  } = props;

  return (
    <>
      <VirtualTableSelectCell
        inputClassName={styles.selectInput}
        id={id}
        isSelected={isSelected}
        isDisabled={!selectedSeries || !isImportable}
        onSelectedChange={onSelectedChange}
      />

      <VirtualTableRowCell className={styles.folder}>
        {relativePath}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.monitor}>
        <FormInputGroup
          type={inputTypes.MONITOR_EPISODES_SELECT}
          name="monitor"
          value={monitor}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.qualityProfile}>
        <FormInputGroup
          type={inputTypes.QUALITY_PROFILE_SELECT}
          name="qualityProfileId"
          value={qualityProfileId}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.seriesType}>
        <FormInputGroup
          type={inputTypes.SERIES_TYPE_SELECT}
          name="seriesType"
          value={seriesType}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.seasonFolder}>
        <FormInputGroup
          type={inputTypes.CHECK}
          name="seasonFolder"
          value={seasonFolder}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.series}>
        <ImportSeriesSelectSeriesConnector
          id={id}
          isExistingSeries={isExistingSeries}
          onInputChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.edition}>
        {
          isExistingSeries ?
            <ImportSeriesEdition
              action={editionAction}
              editionName={editionName}
              editions={editions}
              folderEditionName={folderEditionName}
              onEditionChange={onEditionChange}
            /> :
            null
        }
      </VirtualTableRowCell>
    </>
  );
}

ImportSeriesRow.propTypes = {
  id: PropTypes.string.isRequired,
  relativePath: PropTypes.string.isRequired,
  monitor: PropTypes.string.isRequired,
  qualityProfileId: PropTypes.number.isRequired,
  seriesType: PropTypes.string.isRequired,
  seasonFolder: PropTypes.bool.isRequired,
  selectedSeries: PropTypes.object,
  isExistingSeries: PropTypes.bool.isRequired,
  isImportable: PropTypes.bool.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  editionAction: PropTypes.string,
  editionName: PropTypes.string.isRequired,
  folderEditionName: PropTypes.string,
  onEditionChange: PropTypes.func.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired
};

ImportSeriesRow.defaultsProps = {
  items: []
};

export default ImportSeriesRow;

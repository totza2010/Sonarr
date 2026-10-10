import PropTypes from 'prop-types';
import React, { useCallback, useMemo, useState } from 'react';
import FormInputGroup from 'Components/Form/FormInputGroup';
import Icon from 'Components/Icon';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { NEW_EDITION, replaceAction, SKIP } from './getImportSeriesEdition';
import ImportSeriesEditionModal from './ImportSeriesEditionModal';
import styles from './ImportSeriesEdition.css';

// Asking for a name is its own entry rather than a second input in the cell, since the rows of this
// table are a fixed height and have no room for one.
const ASK_FOR_NAME = 'askForName';

function ImportSeriesEdition(props) {
  const {
    action,
    editionName,
    editions,
    folderEditionName,
    onEditionChange
  } = props;

  const [isModalOpen, setIsModalOpen] = useState(false);

  const values = useMemo(() => {
    const options = [
      { key: SKIP, value: translate('ImportSeriesEditionSkip') }
    ];

    if (action === NEW_EDITION && editionName) {
      options.push({
        key: NEW_EDITION,
        value: translate('ImportSeriesEditionNew', { edition: editionName })
      });
    }

    options.push({ key: ASK_FOR_NAME, value: translate('ImportSeriesEditionNewPrompt') });

    editions.forEach((edition) => {
      options.push({
        key: replaceAction(edition.id),
        value: translate('ImportSeriesEditionReplace', {
          edition: edition.editionName || translate('MainEdition')
        })
      });
    });

    return options;
  }, [action, editionName, editions]);

  const handleChange = useCallback(({ value }) => {
    if (value === ASK_FOR_NAME) {
      setIsModalOpen(true);
      return;
    }

    onEditionChange({ action: value, editionName: value === NEW_EDITION ? editionName : '' });
  }, [editionName, onEditionChange]);

  const handleNameSelect = useCallback((name) => {
    setIsModalOpen(false);
    onEditionChange({ action: NEW_EDITION, editionName: name });
  }, [onEditionChange]);

  const handleModalClose = useCallback(() => {
    setIsModalOpen(false);
  }, []);

  // The folder keeps the name it has. Without the token an edition imported from it works in Sonarr
  // but is not one as far as Plex can tell, and that is worth saying before it is imported.
  const isMissingFolderToken = action === NEW_EDITION &&
    !!editionName &&
    (folderEditionName || '').trim().toLowerCase() !== editionName.toLowerCase();

  return (
    <div className={styles.container}>
      <div className={styles.select}>
        <FormInputGroup
          type={inputTypes.SELECT}
          name="editionAction"
          value={action}
          values={values}
          onChange={handleChange}
        />
      </div>

      {
        isMissingFolderToken ?
          <Icon
            className={styles.warning}
            name={icons.WARNING}
            kind={kinds.WARNING}
            title={translate('ImportSeriesEditionFolderWarning', { edition: editionName })}
          /> :
          null
      }

      <ImportSeriesEditionModal
        isOpen={isModalOpen}
        editionName={editionName || folderEditionName || ''}
        editions={editions}
        onEditionSelect={handleNameSelect}
        onModalClose={handleModalClose}
      />
    </div>
  );
}

ImportSeriesEdition.propTypes = {
  action: PropTypes.string.isRequired,
  editionName: PropTypes.string.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  folderEditionName: PropTypes.string,
  onEditionChange: PropTypes.func.isRequired
};

export default ImportSeriesEdition;

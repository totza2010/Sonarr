import PropTypes from 'prop-types';
import React, { useCallback, useEffect, useState } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import seriesEditionSuggestions from 'Series/seriesEditionSuggestions';
import translate from 'Utilities/String/translate';
import { isSameEdition } from './getImportSeriesEdition';

// The suggestions are a menu to pick from, so they show on focus instead of waiting for the first
// character, the same as when an edition is added from the search page.
function alwaysRenderSuggestions() {
  return true;
}

function ImportSeriesEditionModal(props) {
  const {
    isOpen,
    editionName: initialEditionName,
    editions,
    onEditionSelect,
    onModalClose
  } = props;

  const [editionName, setEditionName] = useState(initialEditionName);

  useEffect(() => {
    if (isOpen) {
      setEditionName(initialEditionName);
    }
  }, [isOpen, initialEditionName]);

  const handleChange = useCallback(({ value }) => {
    setEditionName(value);
  }, []);

  const trimmed = (editionName || '').trim();
  const isTaken = editions.some((e) => isSameEdition(e.editionName, trimmed));

  const handleSelectPress = useCallback(() => {
    onEditionSelect(trimmed);
  }, [trimmed, onEditionSelect]);

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.MEDIUM}
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{translate('AddEdition')}</ModalHeader>

        <ModalBody>
          <Form>
            <FormGroup>
              <FormLabel>{translate('Edition')}</FormLabel>

              <FormInputGroup
                type={inputTypes.AUTO_COMPLETE}
                name="editionName"
                value={editionName}
                values={seriesEditionSuggestions}
                shouldRenderSuggestions={alwaysRenderSuggestions}
                helpText={translate('SeriesEditionHelpText')}
                errors={isTaken && trimmed ? [{ message: translate('ImportSeriesEditionTaken') }] : []}
                onChange={handleChange}
              />
            </FormGroup>
          </Form>
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <Button
            kind={kinds.SUCCESS}
            isDisabled={!trimmed || isTaken}
            onPress={handleSelectPress}
          >
            {translate('AddEdition')}
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

ImportSeriesEditionModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  editionName: PropTypes.string.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  onEditionSelect: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default ImportSeriesEditionModal;

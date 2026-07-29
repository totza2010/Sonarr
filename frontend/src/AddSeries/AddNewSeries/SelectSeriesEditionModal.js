import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import SelectSeriesEditionModalContent from './SelectSeriesEditionModalContent';

function SelectSeriesEditionModal(props) {
  const {
    isOpen,
    onModalClose,
    ...otherProps
  } = props;

  return (
    <Modal
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <SelectSeriesEditionModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

SelectSeriesEditionModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default SelectSeriesEditionModal;

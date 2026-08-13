import React from 'react';
import Modal from 'Components/Modal/Modal';
import Series from 'Series/Series';
import SelectEditionModalContent from './SelectEditionModalContent';

interface SelectEditionModalProps {
  isOpen: boolean;
  series?: Series;
  modalTitle: string;
  onEditionSelect(series: Series): void;
  onModalClose(): void;
}

function SelectEditionModal({
  isOpen,
  series,
  modalTitle,
  onEditionSelect,
  onModalClose,
}: SelectEditionModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectEditionModalContent
        series={series}
        modalTitle={modalTitle}
        onEditionSelect={onEditionSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectEditionModal;

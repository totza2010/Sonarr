import React from 'react';
import Modal from 'Components/Modal/Modal';
import MultipleType from 'InteractiveImport/MultipleType';
import SelectMultipleModalContent from './SelectMultipleModalContent';

interface SelectMultipleModalProps {
  isOpen: boolean;
  multipleType: MultipleType;
  multipleNumber: number;
  modalTitle: string;
  onMultipleSelect(multipleType: MultipleType, multipleNumber: number): void;
  onModalClose(): void;
}

function SelectMultipleModal(props: SelectMultipleModalProps) {
  const {
    isOpen,
    multipleType,
    multipleNumber,
    modalTitle,
    onMultipleSelect,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectMultipleModalContent
        multipleType={multipleType}
        multipleNumber={multipleNumber}
        modalTitle={modalTitle}
        onMultipleSelect={onMultipleSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectMultipleModal;

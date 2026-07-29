import React from 'react';
import Modal from 'Components/Modal/Modal';
import SelectCustomFormatModalContent from './SelectCustomFormatModalContent';

interface SelectCustomFormatModalProps {
  isOpen: boolean;
  selectedIds: number[];
  matchedIds: number[];
  modalTitle: string;
  onCustomFormatsSelect(added: number[], excluded: number[]): void;
  onModalClose(): void;
}

function SelectCustomFormatModal(props: SelectCustomFormatModalProps) {
  const {
    isOpen,
    selectedIds,
    matchedIds,
    modalTitle,
    onCustomFormatsSelect,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectCustomFormatModalContent
        selectedIds={selectedIds}
        matchedIds={matchedIds}
        modalTitle={modalTitle}
        onCustomFormatsSelect={onCustomFormatsSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectCustomFormatModal;

import React from 'react';
import Modal from 'Components/Modal/Modal';
import Language from 'Language/Language';
import SelectNamingLanguagesModalContent from './SelectNamingLanguagesModalContent';

interface SelectNamingLanguagesModalProps {
  isOpen: boolean;
  audioLanguages: Language[];
  subtitleLanguages: Language[];
  detectedAudioLanguages: Language[];
  detectedSubtitleLanguages: Language[];
  modalTitle: string;
  onNamingLanguagesSelect(audio: Language[], subtitles: Language[]): void;
  onModalClose(): void;
}

function SelectNamingLanguagesModal(props: SelectNamingLanguagesModalProps) {
  const {
    isOpen,
    audioLanguages,
    subtitleLanguages,
    detectedAudioLanguages,
    detectedSubtitleLanguages,
    modalTitle,
    onNamingLanguagesSelect,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectNamingLanguagesModalContent
        audioLanguages={audioLanguages}
        subtitleLanguages={subtitleLanguages}
        detectedAudioLanguages={detectedAudioLanguages}
        detectedSubtitleLanguages={detectedSubtitleLanguages}
        modalTitle={modalTitle}
        onNamingLanguagesSelect={onNamingLanguagesSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectNamingLanguagesModal;

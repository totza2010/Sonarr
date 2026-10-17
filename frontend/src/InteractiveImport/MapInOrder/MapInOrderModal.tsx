import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import InteractiveImport from 'InteractiveImport/InteractiveImport';
import Series from 'Series/Series';
import MapInOrderModalContent, { MappedFile } from './MapInOrderModalContent';

interface MapInOrderModalProps {
  isOpen: boolean;
  items: InteractiveImport[];
  series?: Series;
  onApply(series: Series, files: MappedFile[]): void;
  onModalClose(): void;
}

function MapInOrderModal(props: MapInOrderModalProps) {
  const { isOpen, items, series, onApply, onModalClose } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      onModalClose={onModalClose}
    >
      {isOpen && series ? (
        <MapInOrderModalContent
          items={items}
          series={series}
          onApply={onApply}
          onModalClose={onModalClose}
        />
      ) : null}
    </Modal>
  );
}

export default MapInOrderModal;

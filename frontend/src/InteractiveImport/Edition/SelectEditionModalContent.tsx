import React, { useCallback, useMemo } from 'react';
import { useSelector } from 'react-redux';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import getSeriesEditions from 'Series/getSeriesEditions';
import Series from 'Series/Series';
import SeriesEditionBadge from 'Series/SeriesEditionBadge';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import translate from 'Utilities/String/translate';
import styles from './SelectEditionModalContent.css';

interface EditionRowProps {
  edition: Series;
  onPress(edition: Series): void;
}

function EditionRow({ edition, onPress }: EditionRowProps) {
  const handlePress = useCallback(() => {
    onPress(edition);
  }, [edition, onPress]);

  return (
    <Button className={styles.edition} onPress={handlePress}>
      <span>{edition.title}</span>

      <SeriesEditionBadge
        className={styles.badge}
        editionName={edition.editionName}
      />
    </Button>
  );
}

interface SelectEditionModalContentProps {
  // Any edition of the series will do: they all carry the same TVDB id, which is what the list is
  // gathered by.
  series?: Series;
  modalTitle: string;
  onEditionSelect(series: Series): void;
  onModalClose(): void;
}

function SelectEditionModalContent({
  series,
  modalTitle,
  onEditionSelect,
  onModalClose,
}: SelectEditionModalContentProps) {
  const allSeries: Series[] = useSelector(createAllSeriesSelector());

  const editions = useMemo(() => {
    if (!series?.tvdbId) {
      return [];
    }

    // The main edition carries no name, so an empty string sorts it to the top - the one most files
    // belong to is where the eye lands, and the order never changes between openings.
    return [...getSeriesEditions(allSeries, series)].sort((a, b) =>
      (a.editionName ?? '').localeCompare(b.editionName ?? '')
    );
  }, [allSeries, series]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {modalTitle} - {translate('SelectEdition')}
      </ModalHeader>

      <ModalBody>
        <div className={styles.editions}>
          {editions.map((edition) => {
            return (
              <EditionRow
                key={edition.id}
                edition={edition}
                onPress={onEditionSelect}
              />
            );
          })}
        </div>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectEditionModalContent;

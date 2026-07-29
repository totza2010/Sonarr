import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './SelectSeriesEditionModalContent.css';

function SelectSeriesEditionModalContent(props) {
  const {
    title,
    editions,
    onAddEditionPress,
    onModalClose
  } = props;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {`${title} - ${translate('SelectEdition')}`}
      </ModalHeader>

      <ModalBody>
        <div className={styles.editions}>
          {
            editions.map((series) => {
              return (
                <Link
                  key={series.id}
                  className={styles.edition}
                  to={`/series/${series.titleSlug}`}
                  onPress={onModalClose}
                >
                  <div className={styles.editionName}>
                    {series.editionName || translate('MainEdition')}
                  </div>

                  <div className={styles.path}>
                    {series.path}
                  </div>

                  <Icon name={icons.ARROW_RIGHT} />
                </Link>
              );
            })
          }
        </div>
      </ModalBody>

      <ModalFooter>
        <Button
          kind={kinds.SUCCESS}
          onPress={onAddEditionPress}
        >
          {translate('AddEdition')}
        </Button>

        <Button onPress={onModalClose}>
          {translate('Cancel')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

SelectSeriesEditionModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  editions: PropTypes.arrayOf(PropTypes.object).isRequired,
  onAddEditionPress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default SelectSeriesEditionModalContent;

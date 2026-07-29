import React, { useCallback, useState } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import MultipleType from 'InteractiveImport/MultipleType';
import translate from 'Utilities/String/translate';

const typeOptions = [
  {
    key: 'none',
    get value() {
      return translate('MultipleTypeNone');
    },
  },
  {
    key: 'part',
    get value() {
      return translate('MultipleTypePart');
    },
  },
  {
    key: 'version',
    get value() {
      return translate('MultipleTypeVersion');
    },
  },
];

interface SelectMultipleModalContentProps {
  multipleType: MultipleType;
  multipleNumber: number;
  modalTitle: string;
  onMultipleSelect(multipleType: MultipleType, multipleNumber: number): void;
  onModalClose(): void;
}

function SelectMultipleModalContent(props: SelectMultipleModalContentProps) {
  const { modalTitle, onMultipleSelect, onModalClose } = props;

  const [multipleType, setMultipleType] = useState(props.multipleType);
  const [multipleNumber, setMultipleNumber] = useState(props.multipleNumber);

  const handleTypeChange = useCallback(
    ({ value }: { value: string }) => {
      setMultipleType(value as MultipleType);
    },
    [setMultipleType]
  );

  const handleNumberChange = useCallback(
    ({ value }: { value: number | null }) => {
      setMultipleNumber(Number(value) || 0);
    },
    [setMultipleNumber]
  );

  const handleSelect = useCallback(() => {
    // Neither half means anything without the other, so picking one and leaving the other blank clears
    // the file rather than storing a part with no number or a number belonging to nothing.
    if (multipleType === 'none' || multipleNumber <= 0) {
      onMultipleSelect('none', 0);
      return;
    }

    onMultipleSelect(multipleType, multipleNumber);
  }, [multipleType, multipleNumber, onMultipleSelect]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {modalTitle} - {translate('SelectMultiple')}
      </ModalHeader>

      <ModalBody>
        <Form>
          <FormGroup>
            <FormLabel>{translate('MultipleTypeLabel')}</FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="multipleType"
              value={multipleType}
              values={typeOptions}
              helpText={translate('MultipleTypeHelpText')}
              onChange={handleTypeChange}
            />
          </FormGroup>

          {multipleType === 'none' ? null : (
            <FormGroup>
              <FormLabel>{translate('MultipleNumber')}</FormLabel>

              <FormInputGroup
                type={inputTypes.NUMBER}
                name="multipleNumber"
                value={multipleNumber || null}
                min={1}
                helpText={translate('MultipleNumberHelpText')}
                onChange={handleNumberChange}
              />
            </FormGroup>
          )}
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.SUCCESS} onPress={handleSelect}>
          {translate('SelectMultiple')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectMultipleModalContent;

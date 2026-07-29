import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { fetchCustomFormats } from 'Store/Actions/settingsActions';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

interface SelectCustomFormatModalContentProps {
  // The formats this file ends up with, and which of them its name matched on its own. The picker
  // works on the first and reports back in terms of the difference from the second.
  selectedIds: number[];
  matchedIds: number[];
  modalTitle: string;
  onCustomFormatsSelect(added: number[], excluded: number[]): void;
  onModalClose(): void;
}

function SelectCustomFormatModalContent(
  props: SelectCustomFormatModalContentProps
) {
  const {
    selectedIds,
    matchedIds,
    modalTitle,
    onCustomFormatsSelect,
    onModalClose,
  } = props;

  const dispatch = useDispatch();

  const { isFetching, isPopulated, error, items } = useSelector(
    (state: AppState) => state.settings.customFormats
  );

  const [formatIds, setFormatIds] = useState(selectedIds);

  // Fetched every time rather than only when the store looks empty: the list is small, and trusting
  // a populated flag set by some other part of the app is how this ended up showing nothing.
  useEffect(() => {
    dispatch(fetchCustomFormats());
  }, [dispatch]);

  const values = useMemo(
    () =>
      [...items]
        .sort((a, b) => a.name.localeCompare(b.name))
        .map((format, index) => ({
          key: format.id,
          value: format.name,
          order: index,
        })),
    [items]
  );

  const onChange = useCallback(
    ({ value }: InputChanged<number | number[]>) => {
      setFormatIds(Array.isArray(value) ? value : [value]);
    },
    [setFormatIds]
  );

  const onSelectPress = useCallback(() => {
    // Anything kept that the name did not match was added; anything the name matched but that is no
    // longer in the list was taken away. Storing the difference rather than the whole list means a
    // format the name starts matching later needs no record at all.
    onCustomFormatsSelect(
      formatIds.filter((id) => !matchedIds.includes(id)),
      matchedIds.filter((id) => !formatIds.includes(id))
    );
  }, [formatIds, matchedIds, onCustomFormatsSelect]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectCustomFormatsModalTitle', { modalTitle })}
      </ModalHeader>

      <ModalBody>
        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>
            {translate('CustomFormatsLoadError')}
          </Alert>
        ) : null}

        {isPopulated && !error ? (
          <Form>
            <Alert kind={kinds.INFO}>
              {translate('ManualCustomFormatsHelpText')}
            </Alert>

            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('CustomFormats')}</FormLabel>

              <FormInputGroup
                type={inputTypes.TAG_SELECT}
                name="manualCustomFormats"
                value={formatIds}
                values={values}
                minQueryLength={0}
                onChange={onChange}
              />
            </FormGroup>
          </Form>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.SUCCESS} onPress={onSelectPress}>
          {translate('SelectCustomFormats')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectCustomFormatModalContent;

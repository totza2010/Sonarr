import React, { useCallback, useMemo, useState } from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { LanguageSettingsAppState } from 'App/State/SettingsAppState';
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
import Language from 'Language/Language';
import createLanguagesSelector from 'Store/Selectors/createLanguagesSelector';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

interface SelectNamingLanguagesModalContentProps {
  audioLanguages: Language[];
  subtitleLanguages: Language[];
  detectedAudioLanguages: Language[];
  detectedSubtitleLanguages: Language[];
  modalTitle: string;
  onNamingLanguagesSelect(audio: Language[], subtitles: Language[]): void;
  onModalClose(): void;
}

function createSelectableLanguagesSelector() {
  return createSelector(createLanguagesSelector(), (languages) => {
    const { isFetching, isPopulated, error, items } =
      languages as LanguageSettingsAppState;

    // Any and Original answer "what will you accept", not "what is in this file".
    const filterItems = ['Any', 'Original', 'Unknown'];

    return {
      isFetching,
      isPopulated,
      error,
      items: items.filter((lang: Language) => !filterItems.includes(lang.name)),
    };
  });
}

function SelectNamingLanguagesModalContent(
  props: SelectNamingLanguagesModalContentProps
) {
  const {
    audioLanguages,
    subtitleLanguages,
    detectedAudioLanguages,
    detectedSubtitleLanguages,
    modalTitle,
    onNamingLanguagesSelect,
    onModalClose,
  } = props;

  const { isFetching, isPopulated, error, items } = useSelector(
    createSelectableLanguagesSelector()
  );

  // Start from what was already decided, or from what MediaInfo read when nothing was. Correcting a
  // detected list is the common case, and building one from an empty box is not.
  const [audioIds, setAudioIds] = useState(
    (audioLanguages.length ? audioLanguages : detectedAudioLanguages).map(
      (l) => l.id
    )
  );

  const [subtitleIds, setSubtitleIds] = useState(
    (subtitleLanguages.length
      ? subtitleLanguages
      : detectedSubtitleLanguages
    ).map((l) => l.id)
  );

  const values = useMemo(
    () =>
      items.map((language, index) => ({
        key: language.id,
        value: language.name,
        order: index,
      })),
    [items]
  );

  const onAudioChange = useCallback(
    ({ value }: InputChanged<number | number[]>) => {
      setAudioIds(Array.isArray(value) ? value : [value]);
    },
    [setAudioIds]
  );

  const onSubtitleChange = useCallback(
    ({ value }: InputChanged<number | number[]>) => {
      setSubtitleIds(Array.isArray(value) ? value : [value]);
    },
    [setSubtitleIds]
  );

  const onSelectPress = useCallback(() => {
    onNamingLanguagesSelect(
      items.filter((lang) => audioIds.includes(lang.id)),
      items.filter((lang) => subtitleIds.includes(lang.id))
    );
  }, [items, audioIds, subtitleIds, onNamingLanguagesSelect]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectNamingLanguagesModalTitle', { modalTitle })}
      </ModalHeader>

      <ModalBody>
        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>{translate('LanguagesLoadError')}</Alert>
        ) : null}

        {isPopulated && !error ? (
          <Form>
            <Alert kind={kinds.INFO}>
              {translate('NamingLanguagesHelpText')}
            </Alert>

            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('AudioLanguages')}</FormLabel>

              <FormInputGroup
                type={inputTypes.TAG_SELECT}
                name="namingAudioLanguages"
                value={audioIds}
                values={values}
                onChange={onAudioChange}
              />
            </FormGroup>

            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('SubtitleLanguages')}</FormLabel>

              <FormInputGroup
                type={inputTypes.TAG_SELECT}
                name="namingSubtitleLanguages"
                value={subtitleIds}
                values={values}
                onChange={onSubtitleChange}
              />
            </FormGroup>
          </Form>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.SUCCESS} onPress={onSelectPress}>
          {translate('SelectLanguages')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectNamingLanguagesModalContent;

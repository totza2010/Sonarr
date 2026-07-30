import classNames from 'classnames';
import React from 'react';
import Icon, { IconProps } from 'Components/Icon';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import flags from './flags';
import languageCountries from './languageCountries';
import styles from './LanguageFlags.css';

interface LanguageFlagsProps {
  // Already sorted out by the format that wrote the names: the names themselves carry two unlabelled
  // groups, and the order of the two tokens in the format is what says which is which.
  audioLanguages?: string[];
  subtitleLanguages?: string[];
  // Set where the flags follow something else on a line rather than sitting on their own, which is
  // every view except the poster. Kept here so no caller needs a stylesheet just for a margin.
  spaced?: boolean;
  className?: string;
}

interface GroupProps {
  codes?: string[];
  icon: IconProps['name'];
  title: string;
  className?: string;
}

function Group({ codes, icon, title, className }: GroupProps) {
  if (!codes?.length) {
    return null;
  }

  return (
    <div className={classNames(styles.group, className)} title={title}>
      <Icon className={styles.icon} name={icon} size={11} />

      <div className={styles.codes}>
        {codes.map((code) => {
          const flag = flags[languageCountries[code]];

          return (
            <span key={code} className={styles.chip}>
              {flag ? (
                <svg className={styles.flag} viewBox="0 0 6 4">
                  {flag}
                </svg>
              ) : null}
              {code}
            </span>
          );
        })}
      </div>
    </div>
  );
}

function LanguageFlags({
  audioLanguages,
  subtitleLanguages,
  spaced,
  className,
}: LanguageFlagsProps) {
  if (!audioLanguages?.length && !subtitleLanguages?.length) {
    return null;
  }

  return (
    <div
      className={classNames(styles.flags, spaced && styles.spaced, className)}
    >
      <Group
        codes={audioLanguages}
        icon={icons.AUDIO}
        title={translate('AudioLanguages')}
      />

      <Group
        codes={subtitleLanguages}
        icon={icons.SUBTITLE}
        title={translate('SubtitleLanguages')}
        className={styles.subtitles}
      />
    </div>
  );
}

export default LanguageFlags;

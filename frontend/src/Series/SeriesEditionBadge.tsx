import classNames from 'classnames';
import React from 'react';
import styles from './SeriesEditionBadge.css';

interface SeriesEditionBadgeProps {
  editionName?: string;
  className?: string;
}

// Editions of a series share the title the metadata gives them, so on a poster the edition name is
// the only thing that says which one is being looked at. Nothing is shown for the main edition,
// which is what every series has until somebody says otherwise.
function SeriesEditionBadge({
  editionName,
  className,
}: SeriesEditionBadgeProps) {
  if (!editionName) {
    return null;
  }

  return (
    <div className={classNames(styles.badge, className)} title={editionName}>
      {editionName}
    </div>
  );
}

export default SeriesEditionBadge;

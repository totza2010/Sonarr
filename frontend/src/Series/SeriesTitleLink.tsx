import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';
import SeriesEditionBadge from 'Series/SeriesEditionBadge';
import styles from './SeriesTitleLink.css';

export interface SeriesTitleLinkProps extends LinkProps {
  titleSlug: string;
  title: string;
  editionName?: string;
}

// Editions share the title their metadata gives them, so a bare title cannot say which one a row is
// about. Carried by the link itself rather than by each of the pages using it, since a page added
// later would otherwise be one more place to remember.
export default function SeriesTitleLink({
  titleSlug,
  title,
  editionName,
  ...linkProps
}: SeriesTitleLinkProps) {
  const link = `/series/${titleSlug}`;

  return (
    <Link to={link} {...linkProps}>
      {title}

      <SeriesEditionBadge
        className={styles.edition}
        editionName={editionName}
      />
    </Link>
  );
}

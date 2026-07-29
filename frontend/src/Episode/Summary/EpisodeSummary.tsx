import React, { useMemo } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Episode from 'Episode/Episode';
import useEpisode, { EpisodeEntities } from 'Episode/useEpisode';
import { icons, kinds, sizes } from 'Helpers/Props';
import Series from 'Series/Series';
import useSeries from 'Series/useSeries';
import QualityProfileNameConnector from 'Settings/Profiles/Quality/QualityProfileNameConnector';
import translate from 'Utilities/String/translate';
import EpisodeAiring from './EpisodeAiring';
import EpisodeSummaryFileRow from './EpisodeSummaryFileRow';
import styles from './EpisodeSummary.css';

const COLUMNS: Column[] = [
  {
    name: 'path',
    label: () => translate('Path'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'multiple',
    label: () => translate('MultipleTypeLabel'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: () => translate('Formats'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormatScore',
    label: React.createElement(Icon, {
      name: icons.SCORE,
      title: () => translate('CustomFormatScore'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isSortable: false,
    isVisible: true,
  },
];

interface EpisodeSummaryProps {
  seriesId: number;
  episodeId: number;
  episodeEntity: EpisodeEntities;
  episodeFileId?: number;
}

function EpisodeSummary(props: EpisodeSummaryProps) {
  const { seriesId, episodeId, episodeEntity, episodeFileId } = props;

  const { qualityProfileId, network } = useSeries(seriesId) as Series;

  const { airDateUtc, overview, additionalEpisodeFileIds } = useEpisode(
    episodeId,
    episodeEntity
  ) as Episode;

  // The primary file first, then whatever extra parts or versions the episode holds.
  const episodeFileIds = useMemo(() => {
    const ids = episodeFileId ? [episodeFileId] : [];

    return ids.concat(
      (additionalEpisodeFileIds ?? []).filter((id) => id !== episodeFileId)
    );
  }, [episodeFileId, additionalEpisodeFileIds]);

  const hasOverview = !!overview;

  return (
    <div>
      <div>
        <span className={styles.infoTitle}>{translate('Airs')}</span>

        <EpisodeAiring airDateUtc={airDateUtc} network={network} />
      </div>

      <div>
        <span className={styles.infoTitle}>{translate('QualityProfile')}</span>

        <Label kind={kinds.PRIMARY} size={sizes.MEDIUM}>
          <QualityProfileNameConnector qualityProfileId={qualityProfileId} />
        </Label>
      </div>

      <div className={styles.overview}>
        {hasOverview ? overview : translate('NoEpisodeOverview')}
      </div>

      {episodeFileIds.length > 0 ? (
        <Table columns={COLUMNS}>
          <TableBody>
            {episodeFileIds.map((id) => (
              <EpisodeSummaryFileRow
                key={id}
                episodeFileId={id}
                episodeEntity={episodeEntity}
                columns={COLUMNS}
              />
            ))}
          </TableBody>
        </Table>
      ) : null}
    </div>
  );
}

export default EpisodeSummary;

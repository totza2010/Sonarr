using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    /// <summary>
    /// Holds back a file whose edition nobody has chosen. Editions of a series share their title, so
    /// matching a release name lands on the main edition every time and says nothing about having
    /// guessed - the file lands in the wrong series quietly, and the mistake only surfaces later when
    /// somebody notices the episode in the wrong place.
    ///
    /// Refusing here leaves the download in the queue for its owner to sort out by hand, which is the
    /// same treatment a file that would be a downgrade already gets.
    /// </summary>
    public class EditionSpecification : IImportDecisionEngineSpecification
    {
        private readonly ISeriesService _seriesService;
        private readonly Logger _logger;

        public EditionSpecification(ISeriesService seriesService, Logger logger)
        {
            _seriesService = seriesService;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.Series == null)
            {
                return ImportSpecDecision.Accept();
            }

            var editions = _seriesService.FindAllByTvdbId(localEpisode.Series.TvdbId);

            if (!SeriesEditions.EditionIsAmbiguous(editions, localEpisode.Path))
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Debug("{0} has {1} editions and the file name cannot say which, refusing to guess",
                          localEpisode.Series.Title,
                          editions.Count);

            return ImportSpecDecision.Reject(ImportRejectionReason.AmbiguousEdition,
                                             "{0} has more than one edition and a file name cannot say which. Import it by hand and choose.",
                                             localEpisode.Series.Title);
        }
    }
}

using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    /// <summary>
    /// Stops a later part or version being imported next to a file that is not marked as one. Such a file
    /// is an ordinary whole-episode file, and treating it as the first would quietly reinterpret every
    /// file already in the library. The existing file has to be marked first, which is a deliberate act.
    /// </summary>
    public class MultipleFileSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MultipleFileSpecification(Logger logger)
        {
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            // The first of a kind replaces whatever whole-episode file is there and needs nothing to sit
            // beside, so only a second or later one has to find the episode already marked up.
            if (!localEpisode.IsAdditionalFile || localEpisode.MultipleNumber <= 1)
            {
                return ImportSpecDecision.Accept();
            }

            var unmarked = localEpisode.Episodes
                                       .Where(e => e.EpisodeFileId > 0)
                                       .Select(e => e.EpisodeFile?.Value)
                                       .Where(f => f != null && f.MultipleType == EpisodeFileMultipleType.None)
                                       .ToList();

            if (unmarked.Empty())
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Debug("Episode already has a file that is not marked as a part or version, refusing to import {0} {1}",
                          localEpisode.MultipleType,
                          localEpisode.MultipleNumber);

            return ImportSpecDecision.Reject(ImportRejectionReason.ExistingFileIsNotAMultiple,
                                             "Existing episode file is not marked as a part or version. Mark it first, then import this one.");
        }
    }
}

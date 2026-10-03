using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    /// <summary>
    /// Stops a later part or version being imported next to a file that is not marked as one. Such a file
    /// is an ordinary whole-episode file, and treating it as the first would quietly reinterpret every
    /// file already in the library. The existing file has to be marked first, which is a deliberate act -
    /// unless its own name already carries an earlier marker of the same kind. That name was written by
    /// {Multiple} or by whoever split the episode, so it says the file was a part all along and only the
    /// database lost track; the import records the marker on it rather than asking for it again.
    /// </summary>
    public class MultipleFileSpecification : IImportDecisionEngineSpecification
    {
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly Logger _logger;

        public MultipleFileSpecification(IBuildFileNames fileNameBuilder, Logger logger)
        {
            _fileNameBuilder = fileNameBuilder;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.MultipleType == EpisodeFileMultipleType.None)
            {
                return ImportSpecDecision.Accept();
            }

            // Without a name that can tell the files apart they all land on the same path, and the import
            // fails partway through with a file already at the destination. Refusing here says why, before
            // anything has been moved.
            if (!_fileNameBuilder.SupportsMultipleFiles(localEpisode.Series, localEpisode.Episodes))
            {
                _logger.Debug("Naming format cannot tell parts apart, refusing to import {0} {1}",
                              localEpisode.MultipleType,
                              localEpisode.MultipleNumber);

                return ImportSpecDecision.Reject(ImportRejectionReason.MultipleNotSupportedByNaming,
                                                 "Parts and versions need Rename Episodes turned on and {Multiple} in the episode format, otherwise every part gets the same file name.");
            }

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
                                       .Where(f => !NamedAsEarlierMember(f, localEpisode))
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

        private static bool NamedAsEarlierMember(EpisodeFile file, LocalEpisode localEpisode)
        {
            var marker = MultipleFileMarkerReader.Read(Path.GetFileNameWithoutExtension(file.RelativePath ?? string.Empty),
                                                       localEpisode.Series,
                                                       localEpisode.Episodes);

            return marker != null &&
                   marker.Type == localEpisode.MultipleType &&
                   marker.Number < localEpisode.MultipleNumber;
        }
    }
}

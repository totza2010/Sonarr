using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles
{
    public interface IUpgradeMediaFiles
    {
        EpisodeFileMoveResult UpgradeEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, bool copyOnly = false);
    }

    public class UpgradeMediaFileService : IUpgradeMediaFiles
    {
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMoveEpisodeFiles _episodeFileMover;
        private readonly IEpisodeFileLinkService _episodeFileLinkService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public UpgradeMediaFileService(IRecycleBinProvider recycleBinProvider,
                                       IMediaFileService mediaFileService,
                                       IMoveEpisodeFiles episodeFileMover,
                                       IEpisodeFileLinkService episodeFileLinkService,
                                       IDiskProvider diskProvider,
                                       Logger logger)
        {
            _recycleBinProvider = recycleBinProvider;
            _mediaFileService = mediaFileService;
            _episodeFileMover = episodeFileMover;
            _episodeFileLinkService = episodeFileLinkService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        // Two files sit alongside each other when they are different parts of one episode, or different
        // versions of it. The same part, or the same version, means this one takes the other's place.
        private static bool IsSeparateFileFor(EpisodeFile existing, LocalEpisode localEpisode)
        {
            if (localEpisode.IsAdditionalFile)
            {
                return existing.MultipleType != localEpisode.MultipleType ||
                       existing.MultipleNumber != localEpisode.MultipleNumber;
            }

            // An ordinary import replaces whatever is there, including files that carry a part or a
            // version: nothing was said about which one it is, so it is not an addition.
            return false;
        }

        public EpisodeFileMoveResult UpgradeEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, bool copyOnly = false)
        {
            var moveFileResult = new EpisodeFileMoveResult();
            var episodeIds = localEpisode.Episodes.Select(e => e.Id).ToList();

            var currentFiles = localEpisode.Episodes
                                           .Where(e => e.EpisodeFileId > 0)
                                           .Select(e => e.EpisodeFile.Value)
                                           .Where(e => e != null)
                                           .ToList();

            // Files the episode owns beyond the one it points at. They are needed either way: so a third
            // part does not delete the second, and so a plain release replaces every part it supersedes
            // instead of only the first and leaving the rest behind from the older release.
            var linkedIds = _episodeFileLinkService.GetLinkedFileIds(episodeIds);

            if (linkedIds?.Any() == true)
            {
                var linkedFiles = _mediaFileService.Get(linkedIds);

                if (linkedFiles != null)
                {
                    currentFiles.AddRange(linkedFiles.Where(f => f != null));
                }
            }

            // A file that holds a different part of the episode, or a different version of it, is not
            // what this file replaces — both belong to the episode and both stay.
            var existingFiles = currentFiles
                                .Where(e => !IsSeparateFileFor(e, localEpisode))
                                .GroupBy(e => e.Id)
                                .ToList();

            var rootFolder = _diskProvider.GetParentFolder(localEpisode.Series.Path);

            // If there are existing episode files and the root folder is missing, throw, so the old file isn't left behind during the import process.
            if (existingFiles.Any() && !_diskProvider.FolderExists(rootFolder))
            {
                throw new RootFolderNotFoundException($"Root folder '{rootFolder}' was not found.");
            }

            foreach (var existingFile in existingFiles)
            {
                var file = existingFile.First();
                var episodeFilePath = Path.Combine(localEpisode.Series.Path, file.RelativePath);
                var subfolder = rootFolder.GetRelativePath(_diskProvider.GetParentFolder(episodeFilePath));
                string recycleBinPath = null;

                if (_diskProvider.FileExists(episodeFilePath))
                {
                    _logger.Debug("Removing existing episode file: {0}", file);
                    recycleBinPath = _recycleBinProvider.DeleteFile(episodeFilePath, subfolder);
                }

                moveFileResult.OldFiles.Add(new DeletedEpisodeFile(file, recycleBinPath));
                _mediaFileService.Delete(file, DeleteMediaFileReason.Upgrade);
            }

            localEpisode.OldFiles = moveFileResult.OldFiles;

            if (copyOnly)
            {
                moveFileResult.EpisodeFile = _episodeFileMover.CopyEpisodeFile(episodeFile, localEpisode);
            }
            else
            {
                moveFileResult.EpisodeFile = _episodeFileMover.MoveEpisodeFile(episodeFile, localEpisode);
            }

            return moveFileResult;
        }
    }
}

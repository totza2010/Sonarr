using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMediaFileService
    {
        EpisodeFile Add(EpisodeFile episodeFile, bool isAdditionalFile);
        void Update(EpisodeFile episodeFile);
        void Update(List<EpisodeFile> episodeFiles);
        void Delete(EpisodeFile episodeFile, DeleteMediaFileReason reason);
        List<EpisodeFile> GetAllFiles();
        List<EpisodeFile> GetFilesBySeries(int seriesId);
        List<EpisodeFile> GetFilesBySeason(int seriesId, int seasonNumber);
        List<EpisodeFile> GetFiles(IEnumerable<int> ids);
        List<EpisodeFile> GetFilesWithoutMediaInfo();
        List<string> FilterExistingFiles(List<string> files, Series series);
        EpisodeFile Get(int id);
        List<EpisodeFile> Get(IEnumerable<int> ids);
        List<EpisodeFile> GetFilesWithRelativePath(int seriesId, string relativePath);
        List<int> SeriesIdsWithMultipleFiles();
    }

    public class MediaFileService : IMediaFileService, IHandleAsync<SeriesDeletedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IMediaFileRepository _mediaFileRepository;
        private readonly IEpisodeFileLinkRepository _episodeFileLinkRepository;
        private readonly Logger _logger;

        public MediaFileService(IMediaFileRepository mediaFileRepository,
                                IEpisodeFileLinkRepository episodeFileLinkRepository,
                                IEventAggregator eventAggregator,
                                Logger logger)
        {
            _mediaFileRepository = mediaFileRepository;
            _episodeFileLinkRepository = episodeFileLinkRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public EpisodeFile Add(EpisodeFile episodeFile, bool isAdditionalFile)
        {
            var addedFile = _mediaFileRepository.Insert(episodeFile);

            // The id it was given, said out loud. Everything afterwards - links, renames, deletions - is
            // recorded against that number, and without this line there is nothing to tie the number back
            // to a file anyone can recognise.
            _logger.Debug("Added episode file {0}{1} for series {2}: {3}",
                          addedFile.Id,
                          isAdditionalFile ? " (additional)" : string.Empty,
                          addedFile.SeriesId,
                          addedFile.RelativePath);

            _eventAggregator.PublishEvent(new EpisodeFileAddedEvent(addedFile, isAdditionalFile));

            return addedFile;
        }

        public void Update(EpisodeFile episodeFile)
        {
            _mediaFileRepository.Update(episodeFile);
        }

        public void Update(List<EpisodeFile> episodeFiles)
        {
            _mediaFileRepository.UpdateMany(episodeFiles);
        }

        public void Delete(EpisodeFile episodeFile, DeleteMediaFileReason reason)
        {
            // Little hack so we have the episodes and series attached for the event consumers
            episodeFile.Episodes.LazyLoad();
            episodeFile.Path = Path.Combine(episodeFile.Series.Value.Path, episodeFile.RelativePath);

            // Read before the row goes, since the links are deleted along with it and a handler asking
            // afterwards would find nothing. An extra part or version is owned this way and no other, so
            // without this its removal is invisible to everything downstream.
            var linkedEpisodeIds = _episodeFileLinkRepository.GetByEpisodeFileIds(new List<int> { episodeFile.Id })
                                                             .Select(l => l.EpisodeId)
                                                             .Distinct()
                                                             .ToList();

            _logger.Debug("Deleting episode file {0}{1} ({2}): {3}",
                          episodeFile.Id,
                          linkedEpisodeIds.Any() ? " (additional)" : string.Empty,
                          reason,
                          episodeFile.Path);

            _mediaFileRepository.Delete(episodeFile);
            _eventAggregator.PublishEvent(new EpisodeFileDeletedEvent(episodeFile, reason, linkedEpisodeIds));
        }

        public List<EpisodeFile> GetAllFiles()
        {
            return _mediaFileRepository.All().ToList();
        }

        public List<EpisodeFile> GetFilesBySeries(int seriesId)
        {
            return _mediaFileRepository.GetFilesBySeries(seriesId);
        }

        public List<EpisodeFile> GetFilesBySeason(int seriesId, int seasonNumber)
        {
            return _mediaFileRepository.GetFilesBySeason(seriesId, seasonNumber);
        }

        public List<EpisodeFile> GetFiles(IEnumerable<int> ids)
        {
            return _mediaFileRepository.Get(ids).ToList();
        }

        public List<EpisodeFile> GetFilesWithoutMediaInfo()
        {
            return _mediaFileRepository.GetFilesWithoutMediaInfo();
        }

        public List<string> FilterExistingFiles(List<string> files, Series series)
        {
            var seriesFiles = GetFilesBySeries(series.Id);

            return FilterExistingFiles(files, seriesFiles, series);
        }

        public EpisodeFile Get(int id)
        {
            return _mediaFileRepository.Get(id);
        }

        public List<EpisodeFile> Get(IEnumerable<int> ids)
        {
            return _mediaFileRepository.Get(ids).ToList();
        }

        public List<EpisodeFile> GetFilesWithRelativePath(int seriesId, string relativePath)
        {
            return _mediaFileRepository.GetFilesWithRelativePath(seriesId, relativePath);
        }

        public List<int> SeriesIdsWithMultipleFiles()
        {
            return _mediaFileRepository.SeriesIdsWithMultipleFiles();
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            _mediaFileRepository.DeleteForSeries(message.Series.Select(s => s.Id).ToList());
        }

        public static List<string> FilterExistingFiles(List<string> files, List<EpisodeFile> seriesFiles, Series series)
        {
            var seriesFilePaths = seriesFiles.Select(f => Path.Combine(series.Path, f.RelativePath)).ToList();

            if (!seriesFilePaths.Any())
            {
                return files;
            }

            return files.Except(seriesFilePaths, PathEqualityComparer.Instance).ToList();
        }
    }
}

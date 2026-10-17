using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    /// <summary>
    /// Swaps which edition is the main one. The main edition is the one automation works on and the one
    /// clients that know nothing of editions see, so "the colourised one is the real one now" means
    /// moving that role, not the files.
    ///
    /// Two things are unique and have to change hands: the name (with its slug) in the database, and
    /// the folder on disk - the main edition's folder carries no {edition-...}, and Plex reads that.
    /// Neither can be swapped in one step, so both go through a temporary value. The folders move
    /// first and are put back if any move fails; the database is only touched once they are where they
    /// belong, so it never points at a folder that holds the other edition's files.
    /// </summary>
    public class SeriesEditionSwapService : IExecute<SwapMainEditionCommand>
    {
        private const string TemporarySuffix = ".edition-swap";

        private readonly ISeriesRepository _seriesRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public SeriesEditionSwapService(ISeriesRepository seriesRepository,
                                        IDiskProvider diskProvider,
                                        IDiskTransferService diskTransferService,
                                        IEventAggregator eventAggregator,
                                        Logger logger)
        {
            _seriesRepository = seriesRepository;
            _diskProvider = diskProvider;
            _diskTransferService = diskTransferService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Execute(SwapMainEditionCommand message)
        {
            var promoted = _seriesRepository.Get(message.SeriesId);

            if (SeriesEditions.IsMainEdition(promoted.EditionName))
            {
                throw new InvalidOperationException($"{promoted.Title} is already the main edition");
            }

            var main = _seriesRepository.FindAllByTvdbId(promoted.TvdbId)
                                        .FirstOrDefault(s => s.Id != promoted.Id && SeriesEditions.IsMainEdition(s.EditionName));

            if (main == null)
            {
                Promote(promoted);
                return;
            }

            Swap(main, promoted);
        }

        // Nothing is main, which happens when it was deleted before that was refused. The edition only
        // takes the role; its folder stays where it is, since there is no main folder to trade with.
        private void Promote(Series promoted)
        {
            var oldName = promoted.EditionName;

            promoted.TitleSlug = SeriesEditions.RemoveEditionFromSlug(promoted.TitleSlug, oldName);
            promoted.EditionName = SeriesEditions.MainEdition;

            _seriesRepository.Update(promoted);

            _logger.ProgressInfo("{0} ({1}) is now the main edition. Its folder was left at '{2}'", promoted.Title, oldName, promoted.Path);

            _eventAggregator.PublishEvent(new SeriesUpdatedEvent(promoted));
        }

        private void Swap(Series main, Series promoted)
        {
            var editionName = promoted.EditionName;
            var mainPath = main.Path;
            var editionPath = promoted.Path;
            var mainSlug = main.TitleSlug;
            var editionSlug = promoted.TitleSlug;

            _logger.ProgressInfo("Making {0} ({1}) the main edition", promoted.Title, editionName);

            SwapFolders(mainPath, editionPath);

            // The unique index on (TvdbId, EditionName), and those on the slug and the path, mean neither
            // row can take the other's values while the other still holds them.
            var temporaryName = $"{editionName}{TemporarySuffix}";

            promoted.EditionName = temporaryName;
            promoted.TitleSlug = $"{editionSlug}{TemporarySuffix}";
            promoted.Path = $"{editionPath}{TemporarySuffix}";
            _seriesRepository.Update(promoted);

            main.EditionName = editionName;
            main.TitleSlug = editionSlug;
            main.Path = editionPath;
            _seriesRepository.Update(main);

            promoted.EditionName = SeriesEditions.MainEdition;
            promoted.TitleSlug = mainSlug;
            promoted.Path = mainPath;
            _seriesRepository.Update(promoted);

            _logger.ProgressInfo("{0} ({1}) is now the main edition; the former main edition is now {1}", promoted.Title, editionName);

            _eventAggregator.PublishEvent(new SeriesMovedEvent(main, mainPath, editionPath));
            _eventAggregator.PublishEvent(new SeriesMovedEvent(promoted, editionPath, mainPath));
            _eventAggregator.PublishEvent(new SeriesUpdatedEvent(main));
            _eventAggregator.PublishEvent(new SeriesUpdatedEvent(promoted));
        }

        // The main folder goes aside, the edition's folder takes its place, and the main one takes the
        // edition's. A folder that does not exist yet has nothing to move, and its step is skipped.
        private void SwapFolders(string mainPath, string editionPath)
        {
            var temporaryPath = $"{mainPath}{TemporarySuffix}";

            if (_diskProvider.FolderExists(temporaryPath))
            {
                throw new InvalidOperationException($"'{temporaryPath}' is left over from an earlier swap that did not finish. Sort it out by hand before swapping again.");
            }

            var moves = new List<(string From, string To)>();

            if (_diskProvider.FolderExists(mainPath))
            {
                moves.Add((mainPath, temporaryPath));
            }

            if (_diskProvider.FolderExists(editionPath))
            {
                moves.Add((editionPath, mainPath));
            }

            if (moves.Any(m => m.From == mainPath))
            {
                moves.Add((temporaryPath, editionPath));
            }

            var done = new Stack<(string From, string To)>();

            try
            {
                foreach (var move in moves)
                {
                    _logger.Debug("Moving '{0}' to '{1}'", move.From, move.To);
                    _diskProvider.CreateFolder(new DirectoryInfo(move.To).Parent.FullName);
                    _diskTransferService.TransferFolder(move.From, move.To, TransferMode.Move);
                    done.Push(move);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to swap '{0}' and '{1}', putting back what was moved", mainPath, editionPath);

                PutBack(done);

                throw;
            }
        }

        private void PutBack(Stack<(string From, string To)> done)
        {
            while (done.Any())
            {
                var move = done.Pop();

                try
                {
                    _diskTransferService.TransferFolder(move.To, move.From, TransferMode.Move);
                }
                catch (Exception ex)
                {
                    // Nothing in the database has changed, but a folder is in the wrong place, and only a
                    // person can decide what to do with it.
                    _logger.Error(ex, "Unable to move '{0}' back to '{1}'. Move it by hand; the library still expects it at '{1}'", move.To, move.From);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesEditionSwapServiceFixture : CoreTest<SeriesEditionSwapService>
    {
        private Series _main;
        private Series _colour;
        private List<(string From, string To)> _moves;
        private List<(string Name, string Slug, string Path)> _saves;
        private HashSet<string> _folders;

        [SetUp]
        public void Setup()
        {
            _main = new Series { Id = 1, TvdbId = 100, Title = "Show", EditionName = "", TitleSlug = "show", Path = @"C:\TV\Show".AsOsAgnostic() };
            _colour = new Series { Id = 2, TvdbId = 100, Title = "Show", EditionName = "Colour", TitleSlug = "show-colour", Path = @"C:\TV\Show {edition-Colour}".AsOsAgnostic() };

            _moves = new List<(string, string)>();
            _saves = new List<(string, string, string)>();
            _folders = new HashSet<string> { _main.Path, _colour.Path };

            GivenLibrary(_main, _colour);

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Update(It.IsAny<Series>()))
                  .Callback<Series>(s => _saves.Add((s.EditionName, s.TitleSlug, s.Path)))
                  .Returns<Series>(s => s);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns<string>(path => _folders.Contains(path));

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(s => s.TransferFolder(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move))
                  .Callback<string, string, TransferMode>((from, to, mode) => _moves.Add((from, to)))
                  .Returns(TransferMode.Move);
        }

        private void GivenLibrary(params Series[] editions)
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Get(It.IsAny<int>()))
                  .Returns<int>(id => editions.Single(e => e.Id == id));

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindAllByTvdbId(100))
                  .Returns(editions.ToList());
        }

        private void Swap()
        {
            Subject.Execute(new SwapMainEditionCommand { SeriesId = _colour.Id });
        }

        [Test]
        public void should_make_the_edition_the_main_one_and_the_main_one_the_edition()
        {
            var mainPath = _main.Path;
            var colourPath = _colour.Path;

            Swap();

            _colour.EditionName.Should().Be("");
            _colour.TitleSlug.Should().Be("show");
            _colour.Path.Should().Be(mainPath);

            _main.EditionName.Should().Be("Colour");
            _main.TitleSlug.Should().Be("show-colour");
            _main.Path.Should().Be(colourPath);
        }

        [Test]
        public void should_swap_the_folders_through_a_temporary_one()
        {
            var mainPath = _main.Path;
            var colourPath = _colour.Path;
            var temporary = mainPath + ".edition-swap";

            Swap();

            _moves.Should().Equal((mainPath, temporary), (colourPath, mainPath), (temporary, colourPath));
        }

        [Test]
        public void should_never_save_two_rows_with_the_same_name_slug_or_path()
        {
            var rows = new Dictionary<int, (string Name, string Slug, string Path)>
            {
                { 1, (_main.EditionName, _main.TitleSlug, _main.Path) },
                { 2, (_colour.EditionName, _colour.TitleSlug, _colour.Path) }
            };

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.Update(It.IsAny<Series>()))
                  .Callback<Series>(s =>
                  {
                      rows[s.Id] = (s.EditionName, s.TitleSlug, s.Path);
                      rows[1].Name.Should().NotBe(rows[2].Name);
                      rows[1].Slug.Should().NotBe(rows[2].Slug);
                      rows[1].Path.Should().NotBe(rows[2].Path);
                  })
                  .Returns<Series>(s => s);

            Swap();
        }

        [Test]
        public void should_put_the_folders_back_and_leave_the_database_alone_when_a_move_fails()
        {
            var mainPath = _main.Path;
            var colourPath = _colour.Path;
            var temporary = mainPath + ".edition-swap";

            Mocker.GetMock<IDiskTransferService>()
                  .Setup(s => s.TransferFolder(colourPath, mainPath, TransferMode.Move))
                  .Throws(new IOException("in use"));

            Assert.Throws<IOException>(Swap);

            _moves.Should().Equal((mainPath, temporary), (temporary, mainPath));
            _saves.Should().BeEmpty();

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_move_only_the_folder_that_exists()
        {
            var mainPath = _main.Path;
            var colourPath = _colour.Path;

            _folders.Remove(mainPath);

            Swap();

            _moves.Should().Equal((colourPath, mainPath));
        }

        [Test]
        public void should_refuse_to_start_while_an_earlier_swap_is_left_over()
        {
            _folders.Add(_main.Path + ".edition-swap");

            Assert.Throws<InvalidOperationException>(Swap);

            _moves.Should().BeEmpty();
            _saves.Should().BeEmpty();
        }

        [Test]
        public void should_refuse_to_swap_the_main_edition_with_itself()
        {
            Assert.Throws<InvalidOperationException>(() => Subject.Execute(new SwapMainEditionCommand { SeriesId = _main.Id }));

            _saves.Should().BeEmpty();
        }

        [Test]
        public void should_promote_an_edition_when_there_is_no_main_one_and_leave_its_folder()
        {
            GivenLibrary(_colour);

            var colourPath = _colour.Path;

            Swap();

            _colour.EditionName.Should().Be("");
            _colour.TitleSlug.Should().Be("show");
            _colour.Path.Should().Be(colourPath);
            _moves.Should().BeEmpty();
        }

        [Test]
        public void should_tell_the_rest_of_the_app_both_series_changed()
        {
            Swap();

            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<SeriesUpdatedEvent>()), Times.Exactly(2));

            Mocker.GetMock<NzbDrone.Core.Messaging.Events.IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<SeriesMovedEvent>()), Times.Exactly(2));
        }
    }
}

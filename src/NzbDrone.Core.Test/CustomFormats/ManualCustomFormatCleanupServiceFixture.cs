using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class ManualCustomFormatCleanupServiceFixture : CoreTest<ManualCustomFormatCleanupService>
    {
        private Series _series;
        private EpisodeFile _episodeFile;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew().Build();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                                               .With(f => f.ManualCustomFormats = new List<int> { 1, 2 })
                                               .With(f => f.ExcludedCustomFormats = new List<int>())
                                               .Build();
        }

        private void GivenNameMatches(params int[] formatIds)
        {
            var formats = new List<CustomFormat>();

            foreach (var id in formatIds)
            {
                formats.Add(new CustomFormat { Id = id, Name = $"Format {id}" });
            }

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormatFromName(_episodeFile, _series))
                  .Returns(formats);
        }

        private void WhenRenamed()
        {
            Subject.Handle(new EpisodeFileRenamedEvent(_series, _episodeFile, "old.mkv"));
        }

        [Test]
        public void should_drop_a_format_the_new_name_matches_on_its_own()
        {
            // The name is now the record, so keeping the hand-added copy would say it twice.
            GivenNameMatches(1);

            WhenRenamed();

            _episodeFile.ManualCustomFormats.Should().BeEquivalentTo(new[] { 2 });

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Update(_episodeFile), Times.Once());
        }

        [Test]
        public void should_keep_formats_the_name_does_not_match()
        {
            GivenNameMatches(3);

            WhenRenamed();

            _episodeFile.ManualCustomFormats.Should().BeEquivalentTo(new[] { 1, 2 });

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Never());
        }

        [Test]
        public void should_drop_an_exclusion_the_new_name_no_longer_offers()
        {
            // Nothing left to exclude once the name stopped matching it, and the record would
            // otherwise sit there suppressing a format the file might legitimately earn again.
            _episodeFile.ManualCustomFormats = new List<int>();
            _episodeFile.ExcludedCustomFormats = new List<int> { 5 };

            GivenNameMatches(3);

            WhenRenamed();

            _episodeFile.ExcludedCustomFormats.Should().BeEmpty();

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Update(_episodeFile), Times.Once());
        }

        [Test]
        public void should_keep_an_exclusion_the_name_still_matches()
        {
            _episodeFile.ManualCustomFormats = new List<int>();
            _episodeFile.ExcludedCustomFormats = new List<int> { 5 };

            GivenNameMatches(5);

            WhenRenamed();

            _episodeFile.ExcludedCustomFormats.Should().BeEquivalentTo(new[] { 5 });

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_for_a_file_with_none_of_its_own()
        {
            _episodeFile.ManualCustomFormats = new List<int>();
            _episodeFile.ExcludedCustomFormats = new List<int>();

            WhenRenamed();

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Verify(v => v.ParseCustomFormatFromName(It.IsAny<EpisodeFile>(), It.IsAny<Series>()), Times.Never());
        }
    }
}

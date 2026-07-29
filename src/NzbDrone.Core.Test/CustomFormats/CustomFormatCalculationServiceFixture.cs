using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class CustomFormatCalculationServiceFixture : CoreTest<CustomFormatCalculationService>
    {
        private Series _series;
        private EpisodeFile _episodeFile;

        private CustomFormat _inName;
        private CustomFormat _notInName;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew().Build();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                                               .With(f => f.RelativePath = "Season 01/Series.S01E01.IQ.WEBDL-1080p.mkv")
                                               .With(f => f.SceneName = null)
                                               .With(f => f.OriginalFilePath = null)
                                               .With(f => f.Quality = new QualityModel(Quality.WEBDL1080p))
                                               .With(f => f.ManualCustomFormats = new List<int>())
                                               .With(f => f.ExcludedCustomFormats = new List<int>())
                                               .Build();

            _inName = GivenFormat(1, "IQ", "IQ");
            _notInName = GivenFormat(2, "NF", "NF");

            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat> { _inName, _notInName });
        }

        private static CustomFormat GivenFormat(int id, string name, string pattern)
        {
            return new CustomFormat(name, new ReleaseTitleSpecification { Value = pattern })
            {
                Id = id
            };
        }

        [Test]
        public void should_score_what_the_name_matches()
        {
            Subject.ParseScoredCustomFormat(_episodeFile, _series).Should().BeEquivalentTo(new[] { _inName });
        }

        [Test]
        public void should_not_score_a_format_added_by_hand()
        {
            // It is a naming instruction until the rename happens; scoring it here would let the file
            // out-score itself and refuse the upgrade that would have replaced it.
            _episodeFile.ManualCustomFormats = new List<int> { _notInName.Id };

            Subject.ParseCustomFormat(_episodeFile, _series).Should().HaveCount(2);
            Subject.ParseScoredCustomFormat(_episodeFile, _series).Should().BeEquivalentTo(new[] { _inName });
        }

        [Test]
        public void should_score_a_hand_added_format_once_the_name_carries_it()
        {
            // Same format, but now it is in the filename — the file earns it on its own.
            _episodeFile.ManualCustomFormats = new List<int> { _notInName.Id };
            _episodeFile.RelativePath = "Season 01/Series.S01E01.NF.IQ.WEBDL-1080p.mkv";

            Subject.ParseScoredCustomFormat(_episodeFile, _series).Should().BeEquivalentTo(new[] { _inName, _notInName });
        }

        [Test]
        public void should_not_score_a_format_ruled_out_by_hand()
        {
            _episodeFile.ExcludedCustomFormats = new List<int> { _inName.Id };

            Subject.ParseScoredCustomFormat(_episodeFile, _series).Should().BeEmpty();
        }

        [Test]
        public void should_not_double_count_a_format_that_is_both_matched_and_added()
        {
            _episodeFile.ManualCustomFormats = new List<int> { _inName.Id };

            Subject.ParseCustomFormat(_episodeFile, _series).Should().BeEquivalentTo(new[] { _inName });
            Subject.ParseScoredCustomFormat(_episodeFile, _series).Should().BeEquivalentTo(new[] { _inName });
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class MultipleFileSpecificationFixture : CoreTest<MultipleFileSpecification>
    {
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Series = Builder<Series>.CreateNew().Build(),
                Episodes = new List<Episode>()
            };

            // The naming format can tell parts apart unless a test says otherwise.
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(v => v.SupportsMultipleFiles(It.IsAny<Series>(), It.IsAny<List<Episode>>()))
                  .Returns(true);
        }

        private void GivenNamingCannotTellPartsApart()
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(v => v.SupportsMultipleFiles(It.IsAny<Series>(), It.IsAny<List<Episode>>()))
                  .Returns(false);
        }

        private void GivenExistingFile(EpisodeFileMultipleType multipleType, int multipleNumber, string relativePath = "Season 01/Series.Title.S01E01.WEBDL-1080P.mkv")
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    MultipleType = multipleType,
                                                                                    MultipleNumber = multipleNumber,
                                                                                    RelativePath = relativePath
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        [Test]
        public void should_accept_an_ordinary_import()
        {
            GivenExistingFile(EpisodeFileMultipleType.None, 0);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_the_first_part_over_an_unmarked_file()
        {
            // Part 1 replaces the whole-episode file rather than joining it, so there is nothing to mark.
            GivenExistingFile(EpisodeFileMultipleType.None, 0);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 1;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_later_part_when_the_existing_file_is_unmarked()
        {
            GivenExistingFile(EpisodeFileMultipleType.None, 0);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 2;

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.ExistingFileIsNotAMultiple);
        }

        [Test]
        public void should_reject_a_later_version_when_the_existing_file_is_unmarked()
        {
            GivenExistingFile(EpisodeFileMultipleType.None, 0);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.MultipleNumber = 2;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_a_later_part_when_the_existing_file_is_marked()
        {
            GivenExistingFile(EpisodeFileMultipleType.Part, 1);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 2;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_part_when_the_naming_format_cannot_tell_them_apart()
        {
            // Every part would compute the same path, so the second one fails partway through the import
            // on a file already at the destination. Saying so here costs nothing and explains itself.
            GivenNamingCannotTellPartsApart();
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 1;

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.MultipleNotSupportedByNaming);
        }

        [Test]
        public void should_accept_an_ordinary_import_whatever_the_naming_format_says()
        {
            // A file that is not a part is not affected by any of this.
            GivenNamingCannotTellPartsApart();
            GivenExistingFile(EpisodeFileMultipleType.None, 0);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_a_later_part_when_the_episode_has_no_file_at_all()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 0)
                                                     .Build()
                                                     .ToList();

            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 2;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        private void GivenImporting(EpisodeFileMultipleType multipleType, int multipleNumber)
        {
            _localEpisode.MultipleType = multipleType;
            _localEpisode.MultipleNumber = multipleNumber;
        }

        [Test]
        public void should_accept_a_later_version_when_the_existing_file_is_named_as_an_earlier_one()
        {
            // Its name says version 1; only the database lost track of it.
            GivenExistingFile(EpisodeFileMultipleType.None, 0, "Season 19/Kamen.Rider.1971.S19E30.Rider.War.Prologue.V1.BLURAY-1080P.mkv");
            GivenImporting(EpisodeFileMultipleType.Version, 2);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_still_reject_when_the_existing_file_is_named_as_another_kind()
        {
            GivenExistingFile(EpisodeFileMultipleType.None, 0, "Season 01/Series.Title.S01E01.PT1.WEBDL-1080P.mkv");
            GivenImporting(EpisodeFileMultipleType.Version, 2);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_still_reject_when_the_existing_file_is_named_as_the_same_or_a_later_member()
        {
            GivenExistingFile(EpisodeFileMultipleType.None, 0, "Season 01/Series.Title.S01E01.V2.WEBDL-1080P.mkv");
            GivenImporting(EpisodeFileMultipleType.Version, 2);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }
}
}

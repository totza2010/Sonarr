using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class EditionSpecificationFixture : CoreTest<EditionSpecification>
    {
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Path = @"C:\Downloads\Series.S01E01.mkv".AsOsAgnostic(),
                Series = GivenEdition(1, null),
                Episodes = new List<Episode>()
            };
        }

        private static Series GivenEdition(int id, string editionName)
        {
            return new Series
            {
                Id = id,
                TvdbId = 5,
                Title = "Series",
                EditionName = editionName,
                Path = (editionName == null ? @"C:\TV\Series" : $@"C:\TV\Series ({editionName})").AsOsAgnostic()
            };
        }

        private static GrabbedReleaseInfo GivenGrab(params int[] episodeIds)
        {
            return new GrabbedReleaseInfo(episodeIds
                .Select(id => new EpisodeHistory { EpisodeId = id, SourceTitle = "TV Series S01", Date = DateTime.UtcNow })
                .ToList());
        }

        private void GivenEditions(params Series[] editions)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindAllByTvdbId(It.IsAny<int>()))
                  .Returns(new List<Series>(editions));
        }

        [Test]
        public void should_accept_a_series_that_has_one_edition()
        {
            // Every series in an ordinary library takes this path, so it has to stay untouched.
            GivenEditions(GivenEdition(1, null));

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_download_whose_series_has_several_editions()
        {
            GivenEditions(GivenEdition(1, null), GivenEdition(2, "Extended"));

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.AmbiguousEdition);
        }

        [Test]
        public void should_accept_a_file_that_already_sits_in_an_edition_folder()
        {
            // Rescanning the library. The path has settled which edition, so there is nothing to ask.
            GivenEditions(GivenEdition(1, null), GivenEdition(2, "Extended"));
            _localEpisode.Path = @"C:\TV\Series (Extended)\Season 01\Series.S01E01.mkv".AsOsAgnostic();

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_a_file_that_sits_in_the_main_edition_folder()
        {
            GivenEditions(GivenEdition(1, null), GivenEdition(2, "Extended"));
            _localEpisode.Path = @"C:\TV\Series\Season 01\Series.S01E01.mkv".AsOsAgnostic();

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_a_download_grabbed_for_this_edition_s_episodes()
        {
            // Searching from an edition's page and pressing download has already answered the question.
            GivenEditions(GivenEdition(1, null), GivenEdition(2, "Extended"));

            _localEpisode.Series = GivenEdition(2, "Extended");
            _localEpisode.Episodes = new List<Episode> { new Episode { Id = 40, SeriesId = 2 } };
            _localEpisode.Release = GivenGrab(40);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_download_whose_grab_named_another_edition_s_episodes()
        {
            // Episode rows belong to one edition, so a grab naming different ones did not settle this.
            GivenEditions(GivenEdition(1, null), GivenEdition(2, "Extended"));

            _localEpisode.Episodes = new List<Episode> { new Episode { Id = 40, SeriesId = 1 } };
            _localEpisode.Release = GivenGrab(99);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_the_series_is_not_known_yet()
        {
            // Another specification refuses that file; this one has nothing to say about it.
            _localEpisode.Series = null;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();

            Mocker.GetMock<ISeriesService>().Verify(s => s.FindAllByTvdbId(It.IsAny<int>()), Times.Never());
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class EpisodeFileLinkCheckFixture : CoreTest<EpisodeFileLinkCheck>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                  .Returns((string _, Dictionary<string, object> args) => $"{args["files"]} missing: {args["episodes"]}");
        }

        private void GivenBroken(params string[] seriesTitles)
        {
            Mocker.GetMock<ICheckEpisodeFileLinks>()
                  .Setup(s => s.BrokenLinks())
                  .Returns(seriesTitles.Select((t, i) => new BrokenEpisodeFileLink
                  {
                      SeriesTitle = t,
                      EpisodeFileId = i + 1,
                      SeasonNumber = 1,
                      EpisodeNumber = i + 1
                  }).ToList());
        }

        [Test]
        public void should_be_ok_when_every_link_resolves()
        {
            GivenBroken();

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_warn_rather_than_error()
        {
            // The library is intact; it is one episode's extra file that is not, and a rescan fixes it.
            GivenBroken("Series");

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_name_the_episode_not_just_the_series()
        {
            // A series can have hundreds of episodes, and "which one" is the first thing anyone asks.
            GivenBroken("Kamen Rider", "Nautilus");

            var message = Subject.Check().Message;

            message.Should().Contain("Kamen Rider - S01E01");
            message.Should().Contain("Nautilus - S01E02");
        }

        [Test]
        public void should_count_the_rest_rather_than_name_them_all()
        {
            GivenBroken("A", "B", "C", "D", "E");

            Subject.Check().Message.Should().Contain("2 more");
        }

        [Test]
        public void should_count_files_not_series()
        {
            // Two broken links in one series is two files to recover, and each is its own episode.
            GivenBroken("Series", "Series");

            var message = Subject.Check().Message;

            message.Should().StartWith("2 ");
            message.Should().Contain("Series - S01E01");
            message.Should().Contain("Series - S01E02");
        }

        [Test]
        public void should_cope_with_a_link_whose_series_is_gone()
        {
            // The episode a link names can itself be gone, which leaves nothing to name - and is still
            // worth reporting, because the link is still there.
            GivenBroken(new string[] { null });

            Subject.Check().ShouldBeWarning();
            Subject.Check().Message.Should().Contain("episode");
        }
    }
}

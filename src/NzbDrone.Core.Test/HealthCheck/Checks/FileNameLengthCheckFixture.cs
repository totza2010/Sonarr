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
    public class FileNameLengthCheckFixture : CoreTest<FileNameLengthCheck>
    {
        [SetUp]
        public void Setup()
        {
            // The real string is filled in from the arguments, which is what the message assertions read.
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                  .Returns((string _, Dictionary<string, object> args) =>
                      $"{args["files"]} file name(s) are too long: {args["series"]}");
        }

        private void GivenOverlong(params string[] seriesTitles)
        {
            Mocker.GetMock<IFileNameLengthService>()
                  .Setup(s => s.GetOverlongFiles())
                  .Returns(seriesTitles.Select(t => new OverlongFile { SeriesTitle = t, NameLength = 300 }).ToList());
        }

        [Test]
        public void should_be_ok_when_nothing_is_too_long()
        {
            GivenOverlong();

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_warn_rather_than_error()
        {
            // Sonarr goes on working; it is everything else reading the library that cannot.
            GivenOverlong("Series");

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_name_the_series_involved()
        {
            GivenOverlong("Attack on Titan", "Nautilus");

            var message = Subject.Check().Message;

            message.Should().Contain("Attack on Titan");
            message.Should().Contain("Nautilus");
        }

        [Test]
        public void should_count_the_rest_rather_than_name_them_all()
        {
            // A library that has drifted would otherwise turn the message into a paragraph.
            GivenOverlong("A", "B", "C", "D", "E");

            var message = Subject.Check().Message;

            message.Should().Contain("2 more");
            message.Should().NotContain("E");
        }

        [Test]
        public void should_count_files_not_series()
        {
            // Two files of one series is two files with one name to fix, and the count says which.
            GivenOverlong("Series", "Series");

            Subject.Check().Message.Should().Contain("2");
        }

        [Test]
        public void should_keep_the_library_out_of_the_wiki_link()
        {
            // Left to itself the link is built from the message, which carries series titles and a count -
            // so it would leak what is in the library and move every time a file was renamed.
            GivenOverlong("Attack on Titan");

            Subject.Check().WikiUrl.ToString().Should().EndWith("#file-name-length");
        }

        [Test]
        public void should_not_look_for_itself()
        {
            // The scan walks the library; doing that inside the check would repeat it on every event the
            // check is attached to.
            GivenOverlong();

            Subject.Check();

            Mocker.GetMock<IFileNameLengthService>().Verify(s => s.Scan(), Times.Never());
        }
    }
}

using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.DataAugmentation.Xem;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common.Categories;

namespace NzbDrone.Core.Test.Providers
{
    [TestFixture]
    [IntegrationTest]
    public class XemProxyFixture : CoreTest<XemProxy>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();
        }

        // These ask thexem.info itself. When it is down - it has answered 521 from behind Cloudflare for
        // a day at a time, while pages Cloudflare still holds keep answering - nothing here says anything
        // about the code, and a failed test stops the release. So a server error on the real call makes
        // the test inconclusive rather than failed; anything else still fails as before.
        private T Xem<T>(Func<T> call)
        {
            try
            {
                return call();
            }
            catch (HttpException ex) when ((int)ex.Response.StatusCode >= 500)
            {
                Assert.Inconclusive($"thexem.info answered {(int)ex.Response.StatusCode}");
                throw;
            }
        }

        [Test]
        public void get_series_ids()
        {
            var ids = Xem(() => Subject.GetXemSeriesIds());

            ids.Should().NotBeEmpty();
            ids.Should().Contain(i => i == 73141);
        }

        [TestCase(12345, Description = "invalid id")]
        [TestCase(279042, Description = "no single connection")]
        public void should_return_empty_when_known_error(int id)
        {
            Xem(() => Subject.GetSceneTvdbMappings(id)).Should().BeEmpty();
        }

        [TestCase(82807)]
        [TestCase(73141, Description = "American Dad!")]
        public void should_get_mapping(int seriesId)
        {
            var result = Xem(() => Subject.GetSceneTvdbMappings(seriesId));

            result.Should().NotBeEmpty();
            result.Should().OnlyContain(c => c.Scene != null);
            result.Should().OnlyContain(c => c.Tvdb != null);
        }

        [TestCase(78916)]
        public void should_filter_out_episodes_without_scene_mapping(int seriesId)
        {
            var result = Xem(() => Subject.GetSceneTvdbMappings(seriesId));

            result.Should().NotContain(c => c.Tvdb == null);
        }
    }
}

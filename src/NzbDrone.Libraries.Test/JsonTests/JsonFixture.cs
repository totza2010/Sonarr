using System;
using System.Globalization;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Test.Common;

namespace NzbDrone.Libraries.Test.JsonTests
{
    [TestFixture]
    public class JsonFixture : TestBase
    {
        public class TypeWithNumbers
        {
            public int Int32 { get; set; }
            public long Int64 { get; set; }
            public int? nullableIntIsNull { get; set; }
            public int? nullableWithValue { get; set; }
        }

        public class TypeWithDate
        {
            public DateTime Date { get; set; }
        }

        [Test]
        public void should_be_able_to_deserialize_numbers()
        {
            var quality = new TypeWithNumbers { Int32 = int.MaxValue, Int64 = long.MaxValue, nullableWithValue = 12 };
            var result = Json.Deserialize<TypeWithNumbers>(quality.ToJson());

            result.Should().BeEquivalentTo(quality, o => o.IncludingAllRuntimeProperties());
        }

        [TestCase("en-US")]
        [TestCase("th-TH")]
        [TestCase("ar-SA")]
        public void should_write_dates_in_the_same_calendar_whatever_the_machine_keeps(string culture)
        {
            // th-TH counts years from a different place, so without saying which calendar is meant a
            // 2026 date leaves as 2569 - to the UI and to every other client reading this API.
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);

                var json = STJson.ToJson(new TypeWithDate { Date = new DateTime(2026, 7, 29, 10, 44, 36, DateTimeKind.Utc) });

                json.Should().Contain("2026-07-29T10:44:36Z");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestCase("en-US")]
        [TestCase("th-TH")]
        [TestCase("ar-SA")]
        public void should_read_dates_in_the_same_calendar_whatever_the_machine_keeps(string culture)
        {
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);

                var result = STJson.Deserialize<TypeWithDate>("{\"date\": \"2026-07-29T10:44:36Z\"}");

                result.Date.Year.Should().Be(2026);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}

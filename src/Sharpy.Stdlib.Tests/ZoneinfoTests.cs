using Xunit;

using SharpyDateTime = Sharpy.DateTime;

namespace Sharpy.Stdlib.Tests
{
    public class ZoneinfoTests
    {
        // Winter (no DST) and summer (DST) reference datetimes for America/New_York.
        private static SharpyDateTime WinterDt() => new SharpyDateTime(2026, 1, 15, 12, 0);
        private static SharpyDateTime SummerDt() => new SharpyDateTime(2026, 7, 15, 12, 0);

        // --- ZoneInfo creation ---

        [Fact]
        public void Constructor_Utc_KeyIsUtc()
        {
            var zone = new ZoneInfo("UTC");
            Assert.Equal("UTC", zone.Key);
        }

        [Fact]
        public void Constructor_AmericaNewYork_Succeeds()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.NotNull(zone);
            Assert.NotEmpty(zone.Key);
        }

        [Fact]
        public void Constructor_EuropeLondon_Succeeds()
        {
            var zone = new ZoneInfo("Europe/London");
            Assert.NotNull(zone);
            Assert.NotEmpty(zone.Key);
        }

        // --- Invalid zone names ---

        [Fact]
        public void Constructor_InvalidZone_ThrowsZoneInfoNotFoundError()
        {
            Assert.Throws<ZoneInfoNotFoundError>(() => new ZoneInfo("Invalid/Zone"));
        }

        [Fact]
        public void Constructor_EmptyKey_ThrowsZoneInfoNotFoundError()
        {
            Assert.Throws<ZoneInfoNotFoundError>(() => new ZoneInfo(""));
        }

        [Fact]
        public void ZoneInfoNotFoundError_IsKeyError()
        {
            var ex = Assert.Throws<ZoneInfoNotFoundError>(() => new ZoneInfo("Not/AZone"));
            Assert.IsAssignableFrom<KeyError>(ex);
        }

        // --- UTC offset ---

        [Fact]
        public void Utcoffset_Utc_IsZero()
        {
            var zone = new ZoneInfo("UTC");
            Assert.Equal(0.0, zone.Utcoffset().TotalSeconds);
        }

        [Fact]
        public void Utcoffset_AmericaNewYork_BaseOffsetIsMinusFiveHours()
        {
            var zone = new ZoneInfo("America/New_York");
            // Utcoffset(null) returns BaseUtcOffset which for New York is always -5h.
            Assert.Equal(-5 * 3600.0, zone.Utcoffset().TotalSeconds);
        }

        // --- DST-aware offsets ---

        [Fact]
        public void Utcoffset_AmericaNewYork_Winter_IsMinusFiveHours()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(-5 * 3600.0, zone.Utcoffset(WinterDt()).TotalSeconds);
        }

        [Fact]
        public void Utcoffset_AmericaNewYork_Summer_IsMinusFourHours()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(-4 * 3600.0, zone.Utcoffset(SummerDt()).TotalSeconds);
        }

        // --- DST method ---

        [Fact]
        public void Dst_NullDt_IsZero()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(0.0, zone.Dst().TotalSeconds);
        }

        [Fact]
        public void Dst_Winter_IsZero()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(0.0, zone.Dst(WinterDt()).TotalSeconds);
        }

        [Fact]
        public void Dst_Summer_IsOneHour()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(3600.0, zone.Dst(SummerDt()).TotalSeconds);
        }

        // --- Tzname ---

        [Fact]
        public void Tzname_NullDt_ReturnsKey()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.Equal(zone.Key, zone.Tzname());
        }

        [Fact]
        public void Tzname_WithDt_ReturnsNonEmptyName()
        {
            var zone = new ZoneInfo("America/New_York");
            Assert.False(string.IsNullOrEmpty(zone.Tzname(WinterDt())));
            Assert.False(string.IsNullOrEmpty(zone.Tzname(SummerDt())));
        }

        // --- Datetime integration ---

        [Fact]
        public void DateTime_WithZoneInfoTzinfo_Constructs()
        {
            var dt = new SharpyDateTime(2026, 6, 15, 12, 0, tzinfo: new ZoneInfo("America/New_York"));
            Assert.NotNull(dt.Tzinfo);
        }

        [Fact]
        public void DateTime_WithZoneInfoTzinfo_TzinfoIsZoneInfo()
        {
            var zone = new ZoneInfo("America/New_York");
            var dt = new SharpyDateTime(2026, 6, 15, 12, 0, tzinfo: zone);
            Assert.IsType<ZoneInfo>(dt.Tzinfo);
        }

        // --- Astimezone ---

        [Fact]
        public void Astimezone_SummerNewYorkToUtc_ShiftsHourForward()
        {
            var zone = new ZoneInfo("America/New_York");
            var dt = new SharpyDateTime(2026, 7, 15, 12, 0, tzinfo: zone);
            var utc = dt.Astimezone(Timezone.Utc);
            // Summer offset is -4h, so 12:00 EDT == 16:00 UTC.
            Assert.Equal(16, utc.Hour);
        }

        [Fact]
        public void Astimezone_WinterNewYorkToUtc_ShiftsHourForward()
        {
            var zone = new ZoneInfo("America/New_York");
            var dt = new SharpyDateTime(2026, 1, 15, 12, 0, tzinfo: zone);
            var utc = dt.Astimezone(Timezone.Utc);
            // Winter offset is -5h, so 12:00 EST == 17:00 UTC.
            Assert.Equal(17, utc.Hour);
        }

        // --- Backward compatibility with fixed-offset Timezone ---

        [Fact]
        public void DateTime_WithFixedTimezoneUtc_StillWorks()
        {
            var dt = new SharpyDateTime(2026, 1, 1, tzinfo: Timezone.Utc);
            Assert.NotNull(dt.Tzinfo);
        }

        [Fact]
        public void TimezoneUtc_Utcoffset_IsZero()
        {
            Assert.Equal(0.0, Timezone.Utc.Utcoffset().TotalSeconds);
        }

        // --- AvailableTimezones ---

        [Fact]
        public void AvailableTimezones_IsNonEmpty()
        {
            var zones = Zoneinfo.AvailableTimezones();
            Assert.True(zones.Count > 0);
        }

        [Fact]
        public void AvailableTimezones_ContainsUtc()
        {
            var zones = Zoneinfo.AvailableTimezones();
            Assert.Contains("UTC", zones);
        }

        // --- Equality ---

        [Fact]
        public void Equals_SameKey_IsTrue()
        {
            Assert.True(new ZoneInfo("UTC").Equals(new ZoneInfo("UTC")));
        }

        [Fact]
        public void Equals_DifferentKeys_IsFalse()
        {
            Assert.False(new ZoneInfo("America/New_York").Equals(new ZoneInfo("Europe/London")));
        }

        [Fact]
        public void Equals_Null_IsFalse()
        {
            Assert.False(new ZoneInfo("UTC").Equals(null));
        }

        [Fact]
        public void GetHashCode_SameKey_IsEqual()
        {
            Assert.Equal(new ZoneInfo("UTC").GetHashCode(), new ZoneInfo("UTC").GetHashCode());
        }

        // --- ToString ---

        [Fact]
        public void ToString_Utc_ReturnsUtc()
        {
            Assert.Equal("UTC", new ZoneInfo("UTC").ToString());
        }
    }
}

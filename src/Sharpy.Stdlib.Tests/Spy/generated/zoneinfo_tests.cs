// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using datetime = global::Sharpy.Datetime;
using zoneinfo = global::Sharpy.Zoneinfo;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Zoneinfo.ZoneinfoTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Zoneinfo
    {
        [global::Sharpy.SharpyModule("zoneinfo.zoneinfo_tests")]
        public static partial class ZoneinfoTests
        {
            internal static global::Sharpy.DateTime _WinterDt()
            {
#line (8, 5) - (8, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                return new global::Sharpy.DateTime(2026, 1, 15, 12, 0);
            }

            internal static global::Sharpy.DateTime _SummerDt()
            {
#line (11, 5) - (11, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                return new global::Sharpy.DateTime(2026, 7, 15, 12, 0);
            }
        }
    }

    public static partial class Zoneinfo
    {
        public partial class ZoneinfoTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConstructorUtcKeyIsUtc()
            {
#line (17, 5) - (17, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("UTC");
#line (18, 5) - (18, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal("UTC", zone.Key);
            }

            [Xunit.FactAttribute]
            public void TestConstructorAmericaNewYorkSucceeds()
            {
#line (22, 5) - (22, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (23, 5) - (23, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotNull(zone);
#line (24, 5) - (24, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(zone.Key) > 0);
            }

            [Xunit.FactAttribute]
            public void TestConstructorEuropeLondonSucceeds()
            {
#line (28, 5) - (28, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("Europe/London");
#line (29, 5) - (29, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotNull(zone);
#line (30, 5) - (30, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(zone.Key) > 0);
            }

            [Xunit.FactAttribute]
            public void TestConstructorInvalidZoneThrowsZoneInfoNotFoundError()
            {
#line (36, 5) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.ZoneInfoNotFoundError>((global::System.Action)(() =>
                {
#line (37, 9) - (37, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                    new global::Sharpy.ZoneInfo("Invalid/Zone");
                }));
            }

            [Xunit.FactAttribute]
            public void TestConstructorInvalidZoneCaptureExposesException()
            {
#line (42, 5) - (44, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.ZoneInfoNotFoundError>((global::System.Action)(() =>
                {
#line (43, 9) - (43, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                    new global::Sharpy.ZoneInfo("Invalid/Zone");
                }));
#line (44, 5) - (44, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotNull(exc);
#line (45, 5) - (45, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Contains("Invalid/Zone", global::Sharpy.Builtins.Str(exc));
            }

            [Xunit.FactAttribute]
            public void TestConstructorEmptyKeyThrowsZoneInfoNotFoundError()
            {
#line (49, 5) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.ZoneInfoNotFoundError>((global::System.Action)(() =>
                {
#line (50, 9) - (50, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                    new global::Sharpy.ZoneInfo("");
                }));
            }

            [Xunit.FactAttribute]
            public void TestZoneInfoNotFoundErrorIsKeyError()
            {
#line (54, 5) - (54, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                bool caught = false;
#line (55, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                try
                {
#line (56, 9) - (56, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                    new global::Sharpy.ZoneInfo("Not/AZone");
                }
                catch (KeyError)
                {
#line (58, 9) - (58, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                    caught = true;
                }

#line (59, 5) - (59, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(caught);
            }

            [Xunit.FactAttribute]
            public void TestUtcoffsetUtcIsZero()
            {
#line (65, 5) - (65, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("UTC");
#line (66, 5) - (66, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(0.0d, zone.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestUtcoffsetAmericaNewYorkBaseOffsetIsMinusFiveHours()
            {
#line (70, 5) - (70, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (72, 5) - (72, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(-5.0d * 3600.0d, zone.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestUtcoffsetAmericaNewYorkWinterIsMinusFiveHours()
            {
#line (78, 5) - (78, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (79, 5) - (79, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(-5.0d * 3600.0d, zone.Utcoffset(_WinterDt()).TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestUtcoffsetAmericaNewYorkSummerIsMinusFourHours()
            {
#line (83, 5) - (83, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (84, 5) - (84, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(-4.0d * 3600.0d, zone.Utcoffset(_SummerDt()).TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestDstNullDtIsZero()
            {
#line (90, 5) - (90, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (91, 5) - (91, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(0.0d, zone.Dst().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestDstWinterIsZero()
            {
#line (95, 5) - (95, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (96, 5) - (96, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(0.0d, zone.Dst(_WinterDt()).TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestDstSummerIsOneHour()
            {
#line (100, 5) - (100, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (101, 5) - (101, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(3600.0d, zone.Dst(_SummerDt()).TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTznameNullDtReturnsKey()
            {
#line (107, 5) - (107, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (108, 5) - (108, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(zone.Key, zone.Tzname());
            }

            [Xunit.FactAttribute]
            public void TestTznameWithDtReturnsNonEmptyName()
            {
#line (112, 5) - (112, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (113, 5) - (113, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(zone.Tzname(_WinterDt()).Length > 0);
#line (114, 5) - (114, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(zone.Tzname(_SummerDt()).Length > 0);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithZoneInfoTzinfoConstructs()
            {
#line (120, 5) - (120, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var dt = new global::Sharpy.DateTime(2026, 6, 15, 12, 0, tzinfo: new global::Sharpy.ZoneInfo("America/New_York"));
#line (121, 5) - (121, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotNull(dt.Tzinfo);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithZoneInfoTzinfoIsZoneInfo()
            {
#line (125, 5) - (125, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (126, 5) - (126, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var dt = new global::Sharpy.DateTime(2026, 6, 15, 12, 0, tzinfo: zone);
#line (127, 5) - (127, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(dt.Tzinfo is global::Sharpy.ZoneInfo);
            }

            [Xunit.FactAttribute]
            public void TestAstimezoneSummerNewYorkToUtcShiftsHourForward()
            {
#line (133, 5) - (133, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (134, 5) - (134, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var dt = new global::Sharpy.DateTime(2026, 7, 15, 12, 0, tzinfo: zone);
#line (135, 5) - (135, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var utc = dt.Astimezone(global::Sharpy.Timezone.Utc);
#line (137, 5) - (137, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(16, utc.Hour);
            }

            [Xunit.FactAttribute]
            public void TestAstimezoneWinterNewYorkToUtcShiftsHourForward()
            {
#line (141, 5) - (141, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var zone = new global::Sharpy.ZoneInfo("America/New_York");
#line (142, 5) - (142, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var dt = new global::Sharpy.DateTime(2026, 1, 15, 12, 0, tzinfo: zone);
#line (143, 5) - (143, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var utc = dt.Astimezone(global::Sharpy.Timezone.Utc);
#line (145, 5) - (145, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(17, utc.Hour);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithFixedTimezoneUtcStillWorks()
            {
#line (151, 5) - (151, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                var dt = new global::Sharpy.DateTime(2026, 1, 1, tzinfo: global::Sharpy.Timezone.Utc);
#line (152, 5) - (152, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotNull(dt.Tzinfo);
            }

            [Xunit.FactAttribute]
            public void TestTimezoneUtcUtcoffsetIsZero()
            {
#line (156, 5) - (156, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(0.0d, global::Sharpy.Timezone.Utc.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestAvailableTimezonesIsNonEmpty()
            {
#line (162, 5) - (162, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Sharpy.Set<string> zones = zoneinfo.AvailableTimezones();
#line (163, 5) - (163, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(zones) > 0);
            }

            [Xunit.FactAttribute]
            public void TestAvailableTimezonesContainsUtc()
            {
#line (167, 5) - (167, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Sharpy.Set<string> zones = zoneinfo.AvailableTimezones();
#line (168, 5) - (168, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Contains("UTC", zones);
            }

            [Xunit.FactAttribute]
            public void TestEqualsSameKeyIsTrue()
            {
#line (174, 5) - (174, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.ZoneInfo("UTC"), new global::Sharpy.ZoneInfo("UTC"));
            }

            [Xunit.FactAttribute]
            public void TestEqualsDifferentKeysIsFalse()
            {
#line (178, 5) - (178, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.NotEqual(new global::Sharpy.ZoneInfo("Europe/London"), new global::Sharpy.ZoneInfo("America/New_York"));
            }

            [Xunit.FactAttribute]
            public void TestEqualsNoneIsFalse()
            {
#line (185, 5) - (185, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                bool isNotNone = new global::Sharpy.ZoneInfo("UTC")is not null;
#line (186, 5) - (186, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.True(isNotNone);
            }

            [Xunit.FactAttribute]
            public void TestGetHashCodeSameKeyIsEqual()
            {
#line (190, 5) - (190, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Hash(new global::Sharpy.ZoneInfo("UTC")), global::Sharpy.Builtins.Hash(new global::Sharpy.ZoneInfo("UTC")));
            }

            [Xunit.FactAttribute]
            public void TestToStringUtcReturnsUtc()
            {
#line (196, 5) - (196, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/zoneinfo/zoneinfo_tests.spy"
                Xunit.Assert.Equal("UTC", global::Sharpy.Builtins.Str(new global::Sharpy.ZoneInfo("UTC")));
            }
        }
    }
}

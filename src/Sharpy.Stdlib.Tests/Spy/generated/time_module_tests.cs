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
using time = global::Sharpy.TimeModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Time.TimeModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Time
    {
        [global::Sharpy.SharpyModule("time.time_module_tests")]
        public static partial class TimeModuleTests
        {
        }
    }

    public static partial class Time
    {
        public partial class TimeModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestTimeReturnsReasonableEpoch()
            {
#line (42, 5) - (42, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(time.Time() > 1700000000.0d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTimeNsReturnsReasonableEpoch()
            {
#line (48, 5) - (48, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(time.TimeNs() > 1700000000000000000L);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPerfCounterIsMonotonic()
            {
#line (54, 5) - (54, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                double first = time.PerfCounter();
#line (55, 5) - (55, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                double second = time.PerfCounter();
#line (56, 5) - (56, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(second >= first);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPerfCounterNsIsMonotonic()
            {
#line (60, 5) - (60, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                long first = time.PerfCounterNs();
#line (61, 5) - (61, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                long second = time.PerfCounterNs();
#line (62, 5) - (62, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(second >= first);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMonotonicIsMonotonic()
            {
#line (66, 5) - (66, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                double first = time.Monotonic();
#line (67, 5) - (67, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                double second = time.Monotonic();
#line (68, 5) - (68, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(second >= first);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMonotonicNsIsMonotonic()
            {
#line (72, 5) - (72, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                long first = time.MonotonicNs();
#line (73, 5) - (73, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                long second = time.MonotonicNs();
#line (74, 5) - (74, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(second >= first);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStrftimeReturnsDateFormat()
            {
#line (81, 5) - (81, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                string result = time.Strftime("%Y-%m-%d");
#line (82, 5) - (82, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(10, result.Length);
#line (83, 5) - (83, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal("-", global::Sharpy.StringHelpers.GetItem(result, 4));
#line (84, 5) - (84, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal("-", global::Sharpy.StringHelpers.GetItem(result, 7));
#line (85, 5) - (85, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit(global::Sharpy.Slice.GetSlice(result, 0, 4, null)));
#line (86, 5) - (86, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit(global::Sharpy.Slice.GetSlice(result, 5, 7, null)));
#line (87, 5) - (87, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit(global::Sharpy.Slice.GetSlice(result, 8, 10, null)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSleepDoesNotThrow()
            {
#line (94, 5) - (94, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                time.Sleep(0.001d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSleepZeroDoesNotThrow()
            {
#line (98, 5) - (98, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                time.Sleep(0.0d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSleepNegativeValueThrowsValueError()
            {
#line (102, 5) - (104, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                ValueError exc = null!;
#line hidden
                bool __raised_0 = false;
                try
                {
#line (103, 9) - (103, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                    time.Sleep(-1.0d);
#line hidden
                }
                catch (ValueError __caught_1)
                {
                    __raised_0 = true;
                    exc = __caught_1;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (104, 5) - (104, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Contains("sleep length must be non-negative", global::Sharpy.Builtins.Str(exc));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGmtimeReturnsValidStructTime()
            {
#line (110, 5) - (110, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime();
#line (111, 5) - (111, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(t.TmYear >= 2024);
#line (112, 5) - (112, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMon is var __cmp_2 ? __cmp_2 : __cmp_2) && __cmp_2 <= 12);
#line (113, 5) - (113, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMday is var __cmp_3 ? __cmp_3 : __cmp_3) && __cmp_3 <= 31);
#line (114, 5) - (114, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmHour is var __cmp_4 ? __cmp_4 : __cmp_4) && __cmp_4 <= 23);
#line (115, 5) - (115, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmMin is var __cmp_5 ? __cmp_5 : __cmp_5) && __cmp_5 <= 59);
#line (116, 5) - (116, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmSec is var __cmp_6 ? __cmp_6 : __cmp_6) && __cmp_6 <= 61);
#line (117, 5) - (117, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmWday is var __cmp_7 ? __cmp_7 : __cmp_7) && __cmp_7 <= 6);
#line (118, 5) - (118, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmYday is var __cmp_8 ? __cmp_8 : __cmp_8) && __cmp_8 <= 366);
#line (119, 5) - (119, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmIsdst);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLocaltimeReturnsValidStructTime()
            {
#line (123, 5) - (123, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Localtime();
#line (124, 5) - (124, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(t.TmYear >= 2024);
#line (125, 5) - (125, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMon is var __cmp_9 ? __cmp_9 : __cmp_9) && __cmp_9 <= 12);
#line (126, 5) - (126, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMday is var __cmp_10 ? __cmp_10 : __cmp_10) && __cmp_10 <= 31);
#line (127, 5) - (127, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmHour is var __cmp_11 ? __cmp_11 : __cmp_11) && __cmp_11 <= 23);
#line (128, 5) - (128, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmMin is var __cmp_12 ? __cmp_12 : __cmp_12) && __cmp_12 <= 59);
#line (129, 5) - (129, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmSec is var __cmp_13 ? __cmp_13 : __cmp_13) && __cmp_13 <= 61);
#line (130, 5) - (130, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmWday is var __cmp_14 ? __cmp_14 : __cmp_14) && __cmp_14 <= 6);
#line (131, 5) - (131, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmYday is var __cmp_15 ? __cmp_15 : __cmp_15) && __cmp_15 <= 366);
#line (132, 5) - (132, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(-1 <= (t.TmIsdst is var __cmp_16 ? __cmp_16 : __cmp_16) && __cmp_16 <= 1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStructTimeStrMatchesPythonFormat()
            {
#line (138, 5) - (138, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = new global::Sharpy.StructTime(2024, 1, 15, 10, 30, 0, 0, 15, 0);
#line (139, 5) - (139, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                string rep = global::Sharpy.Builtins.Str(t);
#line (140, 5) - (140, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Contains("tm_year=2024", rep);
#line (141, 5) - (141, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Contains("tm_mon=1", rep);
#line (142, 5) - (142, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.StartsWith("time.struct_time(", rep);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStructTimeWdayMondayIsZero()
            {
#line (149, 5) - (149, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime(1704067200.0d);
#line (150, 5) - (150, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmWday);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStructTimeWdaySundayIsSix()
            {
#line (155, 5) - (155, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime(1704585600.0d);
#line (156, 5) - (156, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(6, t.TmWday);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGmtimeUnixEpochReturns1970()
            {
#line (162, 5) - (162, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime(0.0d);
#line (163, 5) - (163, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1970, t.TmYear);
#line (164, 5) - (164, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1, t.TmMon);
#line (165, 5) - (165, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1, t.TmMday);
#line (166, 5) - (166, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmHour);
#line (167, 5) - (167, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmMin);
#line (168, 5) - (168, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmSec);
#line (169, 5) - (169, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(3, t.TmWday);
#line (170, 5) - (170, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1, t.TmYday);
#line (171, 5) - (171, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmIsdst);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGmtimeOneDayAfterEpoch()
            {
#line (175, 5) - (175, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime(86400.0d);
#line (176, 5) - (176, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1970, t.TmYear);
#line (177, 5) - (177, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1, t.TmMon);
#line (178, 5) - (178, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(2, t.TmMday);
#line (179, 5) - (179, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmHour);
#line (180, 5) - (180, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmMin);
#line (181, 5) - (181, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(0, t.TmSec);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLocaltimeUnixEpochReturnsValidStructTime()
            {
#line (186, 5) - (186, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Localtime(0.0d);
#line (187, 5) - (187, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1969 <= (t.TmYear is var __cmp_17 ? __cmp_17 : __cmp_17) && __cmp_17 <= 1970);
#line (188, 5) - (188, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMon is var __cmp_18 ? __cmp_18 : __cmp_18) && __cmp_18 <= 12);
#line (189, 5) - (189, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(1 <= (t.TmMday is var __cmp_19 ? __cmp_19 : __cmp_19) && __cmp_19 <= 31);
#line (190, 5) - (190, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.True(0 <= (t.TmHour is var __cmp_20 ? __cmp_20 : __cmp_20) && __cmp_20 <= 23);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGmtimeLargeTimestamp()
            {
#line (195, 5) - (195, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                var t = time.Gmtime(1000000000.0d);
#line (196, 5) - (196, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(2001, t.TmYear);
#line (197, 5) - (197, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(9, t.TmMon);
#line (198, 5) - (198, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(9, t.TmMday);
#line (199, 5) - (199, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(1, t.TmHour);
#line (200, 5) - (200, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(46, t.TmMin);
#line (201, 5) - (201, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/time/time_module_tests.spy"
                Xunit.Assert.Equal(40, t.TmSec);
#line hidden
            }
        }
    }
}
#line default

// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using itertools = global::Sharpy.Itertools;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsFilterTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_filter_tests")]
        public static partial class ItertoolsFilterTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsFilterTestsTests
        {
            [Xunit.FactAttribute]
            public void TestCompressIntDataFiltersCorrectly()
            {
#line (10, 5) - (10, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4
                };
#line (11, 5) - (11, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
#line hidden
                {
                    true,
                    false,
                    true,
                    false
                };
#line (12, 5) - (12, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Compress(data, sel));
#line (13, 5) - (13, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCompressEmptyDataAndSelectorsReturnsEmpty()
            {
#line (17, 5) - (17, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                };
#line (18, 5) - (18, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
#line hidden
                {
                };
#line (19, 5) - (19, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Compress(data, sel));
#line (20, 5) - (20, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCompressExtraSelectorsIgnoresExtra()
            {
#line (24, 5) - (24, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (25, 5) - (25, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
#line hidden
                {
                    true,
                    true,
                    true,
                    true
                };
#line (26, 5) - (26, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Compress(data, sel));
#line (27, 5) - (27, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCompressAllSelectorsFalseReturnsEmpty()
            {
#line (31, 5) - (31, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (32, 5) - (32, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<bool> sel = new Sharpy.List<bool>()
#line hidden
                {
                    false,
                    false,
                    false
                };
#line (33, 5) - (33, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Compress(data, sel));
#line (34, 5) - (34, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDropwhileEmptyIterableReturnsEmpty()
            {
#line (40, 5) - (40, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (41, 5) - (41, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Dropwhile((int x) => x < 5, empty));
#line (42, 5) - (42, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDropwhilePredicateAlwaysTrueReturnsEmpty()
            {
#line (46, 5) - (46, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (47, 5) - (47, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Dropwhile((int x) => true, data));
#line (48, 5) - (48, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTakewhileEmptyIterableReturnsEmpty()
            {
#line (54, 5) - (54, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (55, 5) - (55, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => x < 5, empty));
#line (56, 5) - (56, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTakewhilePredicateAlwaysFalseReturnsEmpty()
            {
#line (60, 5) - (60, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (61, 5) - (61, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => false, data));
#line (62, 5) - (62, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTakewhileStopsAtFirstFalse()
            {
#line (66, 5) - (66, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    4,
                    6,
                    4,
                    1
                };
#line (67, 5) - (67, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Takewhile((int x) => x < 5, data));
#line (68, 5) - (68, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 4 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFilterfalseEmptyIterableReturnsEmpty()
            {
#line (74, 5) - (74, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (75, 5) - (75, 82) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Filterfalse((int x) => x < 5, empty));
#line (76, 5) - (76, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFilterfalseKeepsElementsWhenPredicateFalse()
            {
#line (80, 5) - (80, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    4,
                    6,
                    4,
                    1
                };
#line (81, 5) - (81, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Filterfalse((int x) => x < 5, data));
#line (82, 5) - (82, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 6 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFilterfalseAllPredicateFalseReturnsAll()
            {
#line (86, 5) - (86, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (87, 5) - (87, 83) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Filterfalse((int x) => x > 100, data));
#line (88, 5) - (88, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsliceStopZeroReturnsEmpty()
            {
#line (94, 5) - (94, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (95, 5) - (95, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Islice(data, 0));
#line (96, 5) - (96, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsliceStopBeyondSourceReturnsAllElements()
            {
#line (100, 5) - (100, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (101, 5) - (101, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Islice(data, 100));
#line (102, 5) - (102, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsliceStartAndStopYieldsCorrectSubsequence()
            {
#line (106, 5) - (106, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    0,
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9
                };
#line (107, 5) - (107, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Islice(data, 2, 5));
#line (108, 5) - (108, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 3, 4 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsliceWithStep2SkipsEveryOther()
            {
#line (112, 5) - (112, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    0,
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9
                };
#line (113, 5) - (113, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Islice(data, 0, 10, 2));
#line (114, 5) - (114, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_filter_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 2, 4, 6, 8 }, result);
#line hidden
            }
        }
    }
}
#line default

// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using bisect = global::Sharpy.BisectModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Bisect.BisectAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Bisect
    {
        [global::Sharpy.SharpyModule("bisect.bisect_additional_tests")]
        public static partial class BisectAdditionalTests
        {
        }
    }

    public static partial class Bisect
    {
        public partial class BisectAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestBisectLeftSingleElementValueLessThanReturnsZero()
            {
#line (7, 5) - (7, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (8, 5) - (8, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightSingleElementValueLessThanReturnsZero()
            {
#line (12, 5) - (12, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (13, 5) - (13, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftSingleElementValueGreaterThanReturnsOne()
            {
#line (19, 5) - (19, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (20, 5) - (20, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(a, 7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightSingleElementValueGreaterThanReturnsOne()
            {
#line (24, 5) - (24, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (25, 5) - (25, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(a, 7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftAllElementsEqualReturnsZero()
            {
#line (31, 5) - (31, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    3,
                    3,
                    3,
                    3
                };
#line (32, 5) - (32, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightAllElementsEqualReturnsLength()
            {
#line (36, 5) - (36, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    3,
                    3,
                    3,
                    3
                };
#line (37, 5) - (37, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(5, bisect.BisectRight(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightValueSmallerThanAllReturnsZero()
            {
#line (43, 5) - (43, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20,
                    30
                };
#line (44, 5) - (44, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(a, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightValueLargerThanAllReturnsLength()
            {
#line (48, 5) - (48, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20,
                    30
                };
#line (49, 5) - (49, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(a, 35));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftWithHiBoundsExcludesElementsBeyondHi()
            {
#line (55, 5) - (55, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (56, 5) - (56, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectLeft(a, 4, hi: 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightWithLoBoundsExcludesElementsBeforeLo()
            {
#line (62, 5) - (62, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (63, 5) - (63, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(a, 3, lo: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftCombinedLoHiSearchesSubrange()
            {
#line (69, 5) - (69, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (70, 5) - (70, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(a, 3, lo: 1, hi: 4));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightCombinedLoHiSearchesSubrange()
            {
#line (74, 5) - (74, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (75, 5) - (75, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(a, 3, lo: 1, hi: 4));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortLeftIntoEmptyListInsertsSingleElement()
            {
#line (81, 5) - (81, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (82, 5) - (82, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortLeft(a, 5);
#line (83, 5) - (83, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortRightMultipleInsertionsMaintainsSortedOrder()
            {
#line (89, 5) - (89, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (90, 5) - (90, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortRight(a, 3);
#line (91, 5) - (91, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortRight(a, 1);
#line (92, 5) - (92, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortRight(a, 5);
#line (93, 5) - (93, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortRight(a, 2);
#line (94, 5) - (94, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortRightWithLoBoundsInsertsAfterLo()
            {
#line (100, 5) - (100, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    5
                };
#line (101, 5) - (101, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                bisect.InsortRight(a, 4, lo: 2);
#line (102, 5) - (102, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 4, 5 }, a);
#line hidden
            }
        }
    }
}
#line default

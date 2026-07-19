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
using static Sharpy.Stdlib.Tests.Spy.Bisect.BisectTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Bisect
    {
        [global::Sharpy.SharpyModule("bisect.bisect_tests")]
        public static partial class BisectTests
        {
        }
    }

    public static partial class Bisect
    {
        public partial class BisectTestsTests
        {
            [Xunit.FactAttribute]
            public void TestBisectLeftFindsLeftmostInsertionPoint()
            {
#line (7, 5) - (7, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (8, 5) - (8, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectLeft(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightFindsRightmostInsertionPoint()
            {
#line (12, 5) - (12, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (13, 5) - (13, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftWithDuplicatesReturnsLeftmost()
            {
#line (17, 5) - (17, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    1,
                    1
                };
#line (18, 5) - (18, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightWithDuplicatesReturnsRightmost()
            {
#line (22, 5) - (22, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    1,
                    1
                };
#line (23, 5) - (23, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectRight(a, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftEmptyListReturnsZero()
            {
#line (27, 5) - (27, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (28, 5) - (28, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightEmptyListReturnsZero()
            {
#line (32, 5) - (32, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (33, 5) - (33, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectRight(a, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftValueSmallerThanAllReturnsZero()
            {
#line (37, 5) - (37, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20,
                    30
                };
#line (38, 5) - (38, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftValueLargerThanAllReturnsLength()
            {
#line (42, 5) - (42, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20,
                    30
                };
#line (43, 5) - (43, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectLeft(a, 35));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectIsAliasForBisectRight()
            {
#line (47, 5) - (47, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (48, 5) - (48, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(bisect.BisectRight(a, 3), bisect.Bisect(a, 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftWithLoBounds()
            {
#line (52, 5) - (52, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (53, 5) - (53, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(3, bisect.BisectLeft(a, 3, lo: 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightWithHiBounds()
            {
#line (57, 5) - (57, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (58, 5) - (58, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(2, bisect.BisectRight(a, 3, hi: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortRightInsertsInSortedOrder()
            {
#line (62, 5) - (62, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5
                };
#line (63, 5) - (63, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.InsortRight(a, 4);
#line (64, 5) - (64, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 4, 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortLeftInsertsAtLeftPosition()
            {
#line (68, 5) - (68, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    3,
                    5
                };
#line (69, 5) - (69, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.InsortLeft(a, 3);
#line (70, 5) - (70, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 3, 3, 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortIsAliasForInsortRight()
            {
#line (74, 5) - (74, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a1 = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5
                };
#line (75, 5) - (75, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a2 = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5
                };
#line (76, 5) - (76, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.Insort(a1, 4);
#line (77, 5) - (77, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.InsortRight(a2, 4);
#line (78, 5) - (78, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(a2, a1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortIntoEmptyList()
            {
#line (82, 5) - (82, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (83, 5) - (83, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.Insort(a, 5);
#line (84, 5) - (84, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInsortLeftWithLoBounds()
            {
#line (88, 5) - (88, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    5
                };
#line (89, 5) - (89, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                bisect.InsortLeft(a, 4, lo: 2);
#line (90, 5) - (90, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5 }, a);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftSingleElementValueEqual()
            {
#line (94, 5) - (94, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (95, 5) - (95, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(0, bisect.BisectLeft(a, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectRightSingleElementValueEqual()
            {
#line (99, 5) - (99, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    5
                };
#line (100, 5) - (100, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectRight(a, 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBisectLeftWithStrings()
            {
#line (104, 5) - (104, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
#line hidden
                {
                    "apple",
                    "banana",
                    "cherry"
                };
#line (105, 5) - (105, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/bisect/bisect_tests.spy"
                Xunit.Assert.Equal(1, bisect.BisectLeft(a, "banana"));
#line hidden
            }
        }
    }
}
#line default

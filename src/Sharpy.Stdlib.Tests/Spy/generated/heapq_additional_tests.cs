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
using heapq = global::Sharpy.Heapq;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Heapq.HeapqAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Heapq
    {
        [global::Sharpy.SharpyModule("heapq.heapq_additional_tests")]
        public static partial class HeapqAdditionalTests
        {
        }
    }

    public static partial class Heapq
    {
        public partial class HeapqAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestHeapreplaceSingleElementReturnsOldValueAndReplaces()
            {
#line (7, 5) - (7, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                    42
                };
#line (8, 5) - (8, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int result = heapq.Heapreplace(h, 7);
#line (9, 5) - (9, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(42, result);
#line (10, 5) - (10, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(7, h[0]);
#line (11, 5) - (11, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(h));
            }

            [Xunit.FactAttribute]
            public void TestHeappushpopEmptyHeapReturnsItemDirectly()
            {
#line (15, 5) - (15, 23) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                };
#line (16, 5) - (16, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int result = heapq.Heappushpop(h, 99);
#line (17, 5) - (17, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(99, result);
#line (18, 5) - (18, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(h));
            }

            [Xunit.FactAttribute]
            public void TestHeappushpopValueEqualToSmallestReturnsItem()
            {
#line (22, 5) - (22, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (23, 5) - (23, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int result = heapq.Heappushpop(h, 1);
#line (24, 5) - (24, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(1, result);
#line (25, 5) - (25, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(h));
            }

            [Xunit.FactAttribute]
            public void TestHeapifySingleElementRemainsUnchanged()
            {
#line (29, 5) - (29, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                    42
                };
#line (30, 5) - (30, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                heapq.Heapify(h);
#line (31, 5) - (31, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, h);
            }

            [Xunit.FactAttribute]
            public void TestHeapifyAlreadyValidHeapPreservesHeapProperty()
            {
#line (35, 5) - (35, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (36, 5) - (36, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                heapq.Heapify(h);
#line (37, 5) - (37, 22) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(1, h[0]);
#line (39, 5) - (39, 16) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int i = 0;
#line (40, 5) - (49, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                while (i < global::Sharpy.Builtins.Len(h))
                {
#line (41, 9) - (41, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    int left = 2 * i + 1;
#line (42, 9) - (42, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    int right = 2 * i + 2;
#line (43, 9) - (45, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    if (left < global::Sharpy.Builtins.Len(h))
                    {
#line (44, 13) - (44, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                        Xunit.Assert.True(h[i] <= h[left]);
                    }

#line (45, 9) - (47, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    if (right < global::Sharpy.Builtins.Len(h))
                    {
#line (46, 13) - (46, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                        Xunit.Assert.True(h[i] <= h[right]);
                    }

#line (47, 9) - (47, 18) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestNsmallestZeroNReturnsEmpty()
            {
#line (51, 5) - (51, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (52, 5) - (52, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nsmallest(0, data);
#line (53, 5) - (53, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestNlargestNGreaterThanListReturnsAllDescending()
            {
#line (57, 5) - (57, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    3,
                    1,
                    2
                };
#line (58, 5) - (58, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nlargest(10, data);
#line (59, 5) - (59, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 2, 1 }, result);
            }

            [Xunit.FactAttribute]
            public void TestNlargestNEqualsListLengthReturnsAllDescending()
            {
#line (63, 5) - (63, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    5,
                    3,
                    8
                };
#line (64, 5) - (64, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nlargest(3, data);
#line (65, 5) - (65, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 8, 5, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestNsmallestNEqualsListLengthReturnsAllAscending()
            {
#line (69, 5) - (69, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    5,
                    3,
                    8
                };
#line (70, 5) - (70, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nsmallest(3, data);
#line (71, 5) - (71, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 5, 8 }, result);
            }

            [Xunit.FactAttribute]
            public void TestNlargestWithDuplicatesPreservesAllInstances()
            {
#line (75, 5) - (75, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    5,
                    5,
                    3,
                    3,
                    1
                };
#line (76, 5) - (76, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nlargest(3, data);
#line (77, 5) - (77, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 5, 5, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestNsmallestWithDuplicatesPreservesAllInstances()
            {
#line (81, 5) - (81, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
                {
                    5,
                    5,
                    3,
                    3,
                    1
                };
#line (82, 5) - (82, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> result = heapq.Nsmallest(3, data);
#line (83, 5) - (83, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 3 }, result);
            }

            [Xunit.FactAttribute]
            public void TestHeappopAfterDrainingThrowsIndexError()
            {
#line (87, 5) - (87, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
                {
                    1
                };
#line (88, 5) - (88, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                heapq.Heappop(h);
#line (89, 5) - (91, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (90, 9) - (90, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    heapq.Heappop(h);
                }));
            }
        }
    }
}

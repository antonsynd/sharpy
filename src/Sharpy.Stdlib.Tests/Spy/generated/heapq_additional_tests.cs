// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using heapq = global::Sharpy.Heapq.HeapqModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Heapq.HeapqAdditionalTests
{
    [global::Sharpy.SharpyModule("heapq.heapq_additional_tests")]
    public static partial class HeapqAdditionalTestsModule
    {
    }

    public partial class HeapqAdditionalTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestHeapreplaceSingleElementReturnsOldValueAndReplaces()
        {
#line (7, 5) - (7, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
                42
            };
#line (8, 5) - (8, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            int result = heapq.Heapreplace(h, 7);
#line (9, 5) - (9, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(42, result);
#line (10, 5) - (10, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(7, h.GetItemUnchecked(0));
#line (11, 5) - (11, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(h));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHeappushpopEmptyHeapReturnsItemDirectly()
        {
#line (15, 5) - (15, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
            };
#line (16, 5) - (16, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            int result = heapq.Heappushpop(h, 99);
#line (17, 5) - (17, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(99, result);
#line (18, 5) - (18, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(h));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHeappushpopValueEqualToSmallestReturnsItem()
        {
#line (22, 5) - (22, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (23, 5) - (23, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            int result = heapq.Heappushpop(h, 1);
#line (24, 5) - (24, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(1, result);
#line (25, 5) - (25, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(h));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHeapifySingleElementRemainsUnchanged()
        {
#line (29, 5) - (29, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
                42
            };
#line (30, 5) - (30, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            heapq.Heapify(h);
#line (31, 5) - (31, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, h);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHeapifyAlreadyValidHeapPreservesHeapProperty()
        {
#line (35, 5) - (35, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (36, 5) - (36, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            heapq.Heapify(h);
#line (37, 5) - (37, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(1, h.GetItemUnchecked(0));
#line (39, 5) - (39, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            int i = 0;
#line (40, 5) - (47, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            while (i < global::Sharpy.Builtins.Len(h))
#line hidden
            {
#line (41, 9) - (41, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int left = 2 * i + 1;
#line (42, 9) - (42, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                int right = 2 * i + 2;
#line (43, 9) - (44, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                if (left < global::Sharpy.Builtins.Len(h))
#line hidden
                {
#line (44, 13) - (44, 36) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    Xunit.Assert.True(h[i] <= h[left]);
#line hidden
                }

#line (45, 9) - (46, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                if (right < global::Sharpy.Builtins.Len(h))
#line hidden
                {
#line (46, 13) - (46, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                    Xunit.Assert.True(h[i] <= h[right]);
#line hidden
                }

#line (47, 9) - (47, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                i = i + 1;
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestNsmallestZeroNReturnsEmpty()
        {
#line (51, 5) - (51, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (52, 5) - (52, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nsmallest(0, data);
#line (53, 5) - (53, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNlargestNGreaterThanListReturnsAllDescending()
        {
#line (57, 5) - (57, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                3,
                1,
                2
            };
#line (58, 5) - (58, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nlargest(10, data);
#line (59, 5) - (59, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 2, 1 }, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNlargestNEqualsListLengthReturnsAllDescending()
        {
#line (63, 5) - (63, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                5,
                3,
                8
            };
#line (64, 5) - (64, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nlargest(3, data);
#line (65, 5) - (65, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 8, 5, 3 }, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNsmallestNEqualsListLengthReturnsAllAscending()
        {
#line (69, 5) - (69, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                5,
                3,
                8
            };
#line (70, 5) - (70, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nsmallest(3, data);
#line (71, 5) - (71, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 5, 8 }, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNlargestWithDuplicatesPreservesAllInstances()
        {
#line (75, 5) - (75, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                5,
                5,
                3,
                3,
                1
            };
#line (76, 5) - (76, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nlargest(3, data);
#line (77, 5) - (77, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 5, 5, 3 }, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNsmallestWithDuplicatesPreservesAllInstances()
        {
#line (81, 5) - (81, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
            {
                5,
                5,
                3,
                3,
                1
            };
#line (82, 5) - (82, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> result = heapq.Nsmallest(3, data);
#line (83, 5) - (83, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3, 3 }, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHeappopAfterDrainingThrowsIndexError()
        {
#line (87, 5) - (87, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
            {
                1
            };
#line (88, 5) - (88, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            heapq.Heappop(h);
#line (89, 5) - (90, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (90, 9) - (90, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_additional_tests.spy"
                heapq.Heappop(h);
#line hidden
            }
            catch (IndexError)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
        }
    }
}
#line default

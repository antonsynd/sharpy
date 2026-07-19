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
using static Sharpy.Stdlib.Tests.Spy.Heapq.HeapqTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Heapq
    {
        [global::Sharpy.SharpyModule("heapq.heapq_tests")]
        public static partial class HeapqTests
        {
        }
    }

    public static partial class Heapq
    {
        public partial class HeapqTestsTests
        {
            [Xunit.FactAttribute]
            public void TestHeappushMaintainsMinHeap()
            {
#line (7, 5) - (7, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (8, 5) - (8, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 3);
#line (9, 5) - (9, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 1);
#line (10, 5) - (10, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 2);
#line (11, 5) - (11, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, h.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeappopReturnsSmallest()
            {
#line (15, 5) - (15, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (16, 5) - (16, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 3);
#line (17, 5) - (17, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 1);
#line (18, 5) - (18, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 2);
#line (19, 5) - (19, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, heapq.Heappop(h));
#line (20, 5) - (20, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(2, heapq.Heappop(h));
#line (21, 5) - (21, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(3, heapq.Heappop(h));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeappopEmptyHeapThrowsIndexError()
            {
#line (25, 5) - (25, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (26, 5) - (29, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
#line hidden
                {
#line (27, 9) - (27, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    heapq.Heappop(h);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestHeapifyCreatesValidMinHeap()
            {
#line (31, 5) - (31, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    5,
                    3,
                    1,
                    4,
                    2
                };
#line (32, 5) - (32, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heapify(h);
#line (34, 5) - (34, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, h.GetItemUnchecked(0));
#line (36, 5) - (36, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                int i = 0;
#line (37, 5) - (46, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                while (i < global::Sharpy.Builtins.Len(h))
#line hidden
                {
#line (38, 9) - (38, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    int left = 2 * i + 1;
#line (39, 9) - (39, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    int right = 2 * i + 2;
#line (40, 9) - (42, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    if (left < global::Sharpy.Builtins.Len(h))
#line hidden
                    {
#line (41, 13) - (41, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                        Xunit.Assert.True(h[i] <= h[left]);
#line hidden
                    }

#line (42, 9) - (44, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    if (right < global::Sharpy.Builtins.Len(h))
#line hidden
                    {
#line (43, 13) - (43, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                        Xunit.Assert.True(h[i] <= h[right]);
#line hidden
                    }

#line (44, 9) - (44, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    i = i + 1;
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestHeapifyMatchesPythonOutput()
            {
#line (48, 5) - (48, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    5,
                    3,
                    1,
                    4,
                    2
                };
#line (49, 5) - (49, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heapify(h);
#line (51, 5) - (51, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 5, 4, 3 }, h);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeapreplacePopsAndPushes()
            {
#line (55, 5) - (55, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (56, 5) - (56, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                int result = heapq.Heapreplace(h, 0);
#line (58, 5) - (58, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, result);
#line (59, 5) - (59, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, h.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeapreplaceEmptyHeapThrowsIndexError()
            {
#line (63, 5) - (63, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (64, 5) - (67, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
#line hidden
                {
#line (65, 9) - (65, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    heapq.Heapreplace(h, 1);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestHeappushpopPushThenPop()
            {
#line (69, 5) - (69, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (71, 5) - (71, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                int result = heapq.Heappushpop(h, 0);
#line (72, 5) - (72, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, result);
#line (73, 5) - (73, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(h));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeappushpopItemLargerThanSmallest()
            {
#line (77, 5) - (77, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (79, 5) - (79, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                int result = heapq.Heappushpop(h, 4);
#line (80, 5) - (80, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNlargestReturnsNLargestDescending()
            {
#line (84, 5) - (84, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    1,
                    4,
                    1,
                    5,
                    9,
                    2,
                    6
                };
#line (85, 5) - (85, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Nlargest(3, data);
#line (86, 5) - (86, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 9, 6, 5 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNsmallestReturnsNSmallestAscending()
            {
#line (90, 5) - (90, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    1,
                    4,
                    1,
                    5,
                    9,
                    2,
                    6
                };
#line (91, 5) - (91, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Nsmallest(3, data);
#line (92, 5) - (92, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 1, 2 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNlargestZeroNReturnsEmpty()
            {
#line (96, 5) - (96, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (97, 5) - (97, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Nlargest(0, data);
#line (98, 5) - (98, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNsmallestNLargerThanListReturnsAll()
            {
#line (102, 5) - (102, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    1,
                    2
                };
#line (103, 5) - (103, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Nsmallest(10, data);
#line (104, 5) - (104, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeappushSingleElement()
            {
#line (108, 5) - (108, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (109, 5) - (109, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heappush(h, 42);
#line (110, 5) - (110, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, h);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeappopSingleElement()
            {
#line (114, 5) - (114, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    42
                };
#line (115, 5) - (115, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(42, heapq.Heappop(h));
#line (116, 5) - (116, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(h));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeapifyEmptyListNoOp()
            {
#line (120, 5) - (120, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (121, 5) - (121, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heapify(h);
#line (122, 5) - (122, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(h));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHeapifyAlreadySorted()
            {
#line (126, 5) - (126, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (127, 5) - (127, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                heapq.Heapify(h);
#line (128, 5) - (128, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(1, h.GetItemUnchecked(0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPushPopSequenceProducesSortedOutput()
            {
#line (132, 5) - (132, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> h = new Sharpy.List<int>()
#line hidden
                {
                };
#line (133, 5) - (133, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> inputs = new Sharpy.List<int>()
#line hidden
                {
                    5,
                    3,
                    8,
                    1,
                    9,
                    2,
                    7,
                    4,
                    6
                };
#line (134, 5) - (136, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                foreach (var __loopVar_0 in inputs)
#line hidden
                {
                    var item = __loopVar_0;
#line (135, 9) - (135, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    heapq.Heappush(h, item);
#line hidden
                }

#line (136, 5) - (136, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> sortedResult = new Sharpy.List<int>()
#line hidden
                {
                };
#line (137, 5) - (139, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                while (global::Sharpy.Builtins.Len(h) > 0)
#line hidden
                {
#line (138, 9) - (138, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    sortedResult.Append(heapq.Heappop(h));
#line hidden
                }

#line (139, 5) - (139, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, sortedResult);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeTwoSortedLists()
            {
#line (143, 5) - (143, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5
                };
#line (144, 5) - (144, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    2,
                    4,
                    6
                };
#line (145, 5) - (145, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b);
#line (146, 5) - (146, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5, 6 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeThreeSortedLists()
            {
#line (150, 5) - (150, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    4
                };
#line (151, 5) - (151, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    2,
                    5
                };
#line (152, 5) - (152, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> c = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    6
                };
#line (153, 5) - (153, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b, c);
#line (154, 5) - (154, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5, 6 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeEmptyIterables()
            {
#line (158, 5) - (158, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (159, 5) - (159, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (160, 5) - (160, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> c = new Sharpy.List<int>()
#line hidden
                {
                };
#line (161, 5) - (161, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(heapq.Merge(a, b), c);
#line (162, 5) - (162, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeAllEmpty()
            {
#line (166, 5) - (166, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (167, 5) - (167, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                };
#line (168, 5) - (168, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b);
#line (169, 5) - (169, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeSingleIterable()
            {
#line (173, 5) - (173, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (174, 5) - (174, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                };
#line (175, 5) - (175, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b);
#line (176, 5) - (176, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeNoIterables()
            {
#line (180, 5) - (180, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                };
#line (181, 5) - (181, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                };
#line (182, 5) - (182, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b);
#line (183, 5) - (183, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeEarlyBreakTakesFirstThree()
            {
#line (187, 5) - (187, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5,
                    7,
                    9
                };
#line (188, 5) - (188, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    2,
                    4,
                    6,
                    8,
                    10
                };
#line (189, 5) - (189, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(a, b);
#line (190, 5) - (190, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> firstThree = global::Sharpy.Slice.GetSlice(result, null, 3, null);
#line (191, 5) - (191, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, firstThree);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithReverseYieldsDescendingOrder()
            {
#line (196, 5) - (196, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    6,
                    4,
                    2
                };
#line (197, 5) - (197, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    5,
                    3,
                    1
                };
#line (199, 5) - (199, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> aAsc = new Sharpy.List<int>()
#line hidden
                {
                    2,
                    4,
                    6
                };
#line (200, 5) - (200, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> bAsc = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    3,
                    5
                };
#line (201, 5) - (201, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> merged = heapq.Merge(aAsc, bAsc);
#line (202, 5) - (202, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                merged.Reverse();
#line (203, 5) - (203, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 6, 5, 4, 3, 2, 1 }, merged);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithKeyFunctionSortsByKey()
            {
#line (208, 5) - (208, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
#line hidden
                {
                    "fig",
                    "apple",
                    "banana"
                };
#line (209, 5) - (209, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
#line hidden
                {
                    "hi",
                    "cherry",
                    "elephant"
                };
#line (211, 5) - (211, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> combined = new Sharpy.List<string>()
#line hidden
                {
                };
#line (212, 5) - (214, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                foreach (var __loopVar_1 in a)
#line hidden
                {
                    var s = __loopVar_1;
#line (213, 9) - (213, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    combined.Append(s);
#line hidden
                }

#line (214, 5) - (216, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                foreach (var __loopVar_2 in b)
#line hidden
                {
                    var s = __loopVar_2;
#line (215, 9) - (215, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    combined.Append(s);
#line hidden
                }

#line (216, 5) - (216, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                combined.Sort(key: s => s.Length);
#line (217, 5) - (217, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hi", "fig", "apple", "banana", "cherry", "elephant" }, combined);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithKeyAndReverseSortsByKeyDescending()
            {
#line (222, 5) - (222, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
#line hidden
                {
                    "banana",
                    "apple",
                    "fig"
                };
#line (223, 5) - (223, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
#line hidden
                {
                    "elephant",
                    "cherry",
                    "hi"
                };
#line (224, 5) - (224, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> combined = new Sharpy.List<string>()
#line hidden
                {
                };
#line (225, 5) - (227, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                foreach (var __loopVar_3 in a)
#line hidden
                {
                    var s = __loopVar_3;
#line (226, 9) - (226, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    combined.Append(s);
#line hidden
                }

#line (227, 5) - (229, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                foreach (var __loopVar_4 in b)
#line hidden
                {
                    var s = __loopVar_4;
#line (228, 9) - (228, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                    combined.Append(s);
#line hidden
                }

#line (229, 5) - (229, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                combined.Sort(key: s => s.Length, reverse: true);
#line (230, 5) - (230, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "elephant", "banana", "cherry", "apple", "fig", "hi" }, combined);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithReverseThreeLists()
            {
#line (235, 5) - (235, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    9,
                    6,
                    3
                };
#line (236, 5) - (236, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                    8,
                    5,
                    2
                };
#line (237, 5) - (237, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> c = new Sharpy.List<int>()
#line hidden
                {
                    7,
                    4,
                    1
                };
#line (239, 5) - (239, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> aAsc = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    6,
                    9
                };
#line (240, 5) - (240, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> bAsc = new Sharpy.List<int>()
#line hidden
                {
                    2,
                    5,
                    8
                };
#line (241, 5) - (241, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> cAsc = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    4,
                    7
                };
#line (242, 5) - (242, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> merged = heapq.Merge(heapq.Merge(aAsc, bAsc), cAsc);
#line (243, 5) - (243, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                merged.Reverse();
#line (244, 5) - (244, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 9, 8, 7, 6, 5, 4, 3, 2, 1 }, merged);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithKeyEmptyLists()
            {
#line (248, 5) - (248, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> a = new Sharpy.List<string>()
#line hidden
                {
                };
#line (249, 5) - (249, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> b = new Sharpy.List<string>()
#line hidden
                {
                };
#line (250, 5) - (250, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<string> result = heapq.Merge(a, b);
#line (251, 5) - (251, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeWithReverseSingleList()
            {
#line (255, 5) - (255, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    2,
                    1
                };
#line (256, 5) - (256, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
#line hidden
                {
                };
#line (258, 5) - (258, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> result = heapq.Merge(b, b);
#line (259, 5) - (259, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> aSorted = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (260, 5) - (260, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Sharpy.List<int> merged = heapq.Merge(aSorted, b);
#line (261, 5) - (261, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                merged.Reverse();
#line (262, 5) - (262, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/heapq/heapq_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 2, 1 }, merged);
#line hidden
            }
        }
    }
}
#line default

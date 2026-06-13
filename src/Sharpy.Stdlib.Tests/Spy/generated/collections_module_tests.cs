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
using collections = global::Sharpy.Collections;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Collections.CollectionsModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.collections_module_tests")]
        public static partial class CollectionsModuleTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class CollectionsModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDequeAppendAndPopWorksLikeStack()
            {
#line (9, 5) - (9, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (10, 5) - (10, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(1);
#line (11, 5) - (11, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(2);
#line (12, 5) - (12, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(3);
#line (13, 5) - (13, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, d.Pop());
#line (14, 5) - (14, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, d.Pop());
#line (15, 5) - (15, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, d.Pop());
            }

            [Xunit.FactAttribute]
            public void TestDequeAppendleftAndPopleftWorksLikeQueue()
            {
#line (19, 5) - (19, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (20, 5) - (20, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Appendleft(1);
#line (21, 5) - (21, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Appendleft(2);
#line (22, 5) - (22, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Appendleft(3);
#line (23, 5) - (23, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
#line (24, 5) - (24, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (25, 5) - (25, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequeAppendAndPopleftWorksAsFifo()
            {
#line (29, 5) - (29, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (30, 5) - (30, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(1);
#line (31, 5) - (31, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(2);
#line (32, 5) - (32, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(3);
#line (33, 5) - (33, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
#line (34, 5) - (34, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (35, 5) - (35, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequePopEmptyThrowsIndexError()
            {
#line (39, 5) - (39, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (40, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (41, 9) - (41, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    d.Pop();
                }));
            }

            [Xunit.FactAttribute]
            public void TestDequePopleftEmptyThrowsIndexError()
            {
#line (45, 5) - (45, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (46, 5) - (49, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (47, 9) - (47, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    d.Popleft();
                }));
            }

            [Xunit.FactAttribute]
            public void TestDequeCountReflectsSize()
            {
#line (51, 5) - (51, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (52, 5) - (52, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int n = d.Count;
#line (53, 5) - (53, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, n);
#line (54, 5) - (54, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(1);
#line (55, 5) - (55, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Append(2);
#line (56, 5) - (56, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                n = d.Count;
#line (57, 5) - (57, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, n);
#line (58, 5) - (58, 12) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Pop();
#line (59, 5) - (59, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                n = d.Count;
#line (60, 5) - (60, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, n);
            }

            [Xunit.FactAttribute]
            public void TestDequeConstructorWithIterableInitializesFromSequence()
            {
#line (64, 5) - (64, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2, 3 });
#line (65, 5) - (65, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int n = d.Count;
#line (66, 5) - (66, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, n);
#line (67, 5) - (67, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequeClearRemovesAllElements()
            {
#line (71, 5) - (71, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2, 3 });
#line (72, 5) - (72, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Clear();
#line (73, 5) - (73, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int n = d.Count;
#line (74, 5) - (74, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, n);
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendAddsFromRight()
            {
#line (78, 5) - (78, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1 });
#line (79, 5) - (79, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Extend(new Sharpy.List<int>() { 2, 3, 4 });
#line (80, 5) - (80, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int n = d.Count;
#line (81, 5) - (81, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(4, n);
#line (82, 5) - (82, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(4, d.Pop());
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendleftAddsFromLeft()
            {
#line (86, 5) - (86, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 4 });
#line (87, 5) - (87, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                d.Extendleft(new Sharpy.List<int>() { 1, 2, 3 });
#line (88, 5) - (88, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int n = d.Count;
#line (89, 5) - (89, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(4, n);
#line (91, 5) - (91, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequeEnumerationIteratesInOrder()
            {
#line (95, 5) - (95, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 10, 20, 30 });
#line (96, 5) - (96, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (97, 5) - (99, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                foreach (var __loopVar_0 in d)
                {
                    var item = __loopVar_0;
#line (98, 9) - (98, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    items.Append(item);
                }

#line (99, 5) - (99, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 20, 30 }, items);
            }

            [Xunit.FactAttribute]
            public void TestCounterCountsOccurrences()
            {
#line (105, 5) - (105, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "a", "c", "a", "b" });
#line (106, 5) - (106, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, c["a"]);
#line (107, 5) - (107, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, c["b"]);
#line (108, 5) - (108, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, c["c"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterMissingKeyReturnsZero()
            {
#line (112, 5) - (112, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (113, 5) - (113, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, c["nonexistent"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterMostCommonReturnsOrderedByCount()
            {
#line (117, 5) - (117, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "a", "c", "a", "b" });
#line (118, 5) - (118, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> mostCommon = c.MostCommon(2);
#line (119, 5) - (119, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(mostCommon));
#line (120, 5) - (120, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal("a", mostCommon[0].Item1);
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, mostCommon[0].Item2);
#line (122, 5) - (122, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal("b", mostCommon[1].Item1);
#line (123, 5) - (123, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, mostCommon[1].Item2);
            }

            [Xunit.FactAttribute]
            public void TestCounterMostCommonNoLimitReturnsAll()
            {
#line (127, 5) - (127, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "x", "y", "x" });
#line (128, 5) - (128, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> allCommon = c.MostCommon();
#line (129, 5) - (129, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(allCommon));
            }

            [Xunit.FactAttribute]
            public void TestCounterElementsRepeatsEachElement()
            {
#line (133, 5) - (133, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "a" });
#line (134, 5) - (134, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<string> elements = new global::Sharpy.List<string>(c.Elements());
#line (135, 5) - (135, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains("a", elements);
#line (136, 5) - (136, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains("b", elements);
#line (138, 5) - (138, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int aCount = 0;
#line (139, 5) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                foreach (var __loopVar_1 in elements)
                {
                    var e = __loopVar_1;
#line (140, 9) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    if (e == "a")
                    {
#line (141, 13) - (141, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                        aCount = aCount + 1;
                    }
                }

#line (142, 5) - (142, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, aCount);
#line (143, 5) - (143, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int bCount = 0;
#line (144, 5) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                foreach (var __loopVar_2 in elements)
                {
                    var e = __loopVar_2;
#line (145, 9) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    if (e == "b")
                    {
#line (146, 13) - (146, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                        bCount = bCount + 1;
                    }
                }

#line (147, 5) - (147, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, bCount);
            }

            [Xunit.FactAttribute]
            public void TestCounterUpdateAddsCounts()
            {
#line (151, 5) - (151, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (152, 5) - (152, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c.Update(new Sharpy.List<string>() { "a", "b" });
#line (153, 5) - (153, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, c["a"]);
#line (154, 5) - (154, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, c["b"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterIndexerSetOverridesCount()
            {
#line (158, 5) - (158, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (159, 5) - (159, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c["x"] = 10;
#line (160, 5) - (160, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(10, c["x"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterEmptyConstructorStartsEmpty()
            {
#line (164, 5) - (164, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<int> c = new global::Sharpy.Counter<int>();
#line (165, 5) - (165, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, c[42]);
            }

            [Xunit.FactAttribute]
            public void TestCounterSubtractIterableSubtractsCounts()
            {
#line (169, 5) - (169, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (170, 5) - (170, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c.Subtract(new Sharpy.List<string>() { "a" });
#line (171, 5) - (171, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, c["a"]);
#line (172, 5) - (172, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, c["b"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterSubtractCounterSubtractsCounts()
            {
#line (176, 5) - (176, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (177, 5) - (177, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "b" });
#line (178, 5) - (178, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c1.Subtract(c2);
#line (179, 5) - (179, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, c1["a"]);
#line (180, 5) - (180, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(-1, c1["b"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterSubtractMissingKeysGoesNegative()
            {
#line (184, 5) - (184, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (185, 5) - (185, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c.Subtract(new Sharpy.List<string>() { "a" });
#line (186, 5) - (186, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(-1, c["a"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterCopyReturnsIndependentCopy()
            {
#line (190, 5) - (190, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> original = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (191, 5) - (191, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> copy = original.Copy();
#line (192, 5) - (192, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                copy["a"] = 0;
#line (193, 5) - (193, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, original["a"]);
#line (194, 5) - (194, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, copy["a"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterTotalReturnsSumOfCounts()
            {
#line (198, 5) - (198, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (199, 5) - (199, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(3, c.Total());
            }

            [Xunit.FactAttribute]
            public void TestCounterTotalEmptyCounterReturnsZero()
            {
#line (203, 5) - (203, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (204, 5) - (204, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, c.Total());
            }

            [Xunit.FactAttribute]
            public void TestCounterClearRemovesAllElements()
            {
#line (208, 5) - (208, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (209, 5) - (209, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                c.Clear();
#line (210, 5) - (210, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, c["a"]);
#line (211, 5) - (211, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, c.Total());
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorAddCombinesCounts()
            {
#line (215, 5) - (215, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (216, 5) - (216, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "b", "c", "c" });
#line (217, 5) - (217, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> result = c1 + c2;
#line (218, 5) - (218, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, result["a"]);
#line (219, 5) - (219, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, result["b"]);
#line (220, 5) - (220, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, result["c"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorSubtractDropsZeroAndNegative()
            {
#line (224, 5) - (224, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (225, 5) - (225, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (226, 5) - (226, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> result = c1 - c2;
#line (227, 5) - (227, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, result["a"]);
#line (228, 5) - (228, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(result.ContainsKey("b"));
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorOrTakesMaxCounts()
            {
#line (232, 5) - (232, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (233, 5) - (233, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "b", "c", "c" });
#line (234, 5) - (234, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> result = c1 | c2;
#line (235, 5) - (235, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, result["a"]);
#line (236, 5) - (236, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, result["b"]);
#line (237, 5) - (237, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, result["c"]);
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorAndTakesMinCounts()
            {
#line (241, 5) - (241, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (242, 5) - (242, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "c" });
#line (243, 5) - (243, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.Counter<string> result = c1 & c2;
#line (244, 5) - (244, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, result["a"]);
#line (245, 5) - (245, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, result["b"]);
#line (246, 5) - (246, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(result.ContainsKey("c"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictMissingKeyReturnsDefault()
            {
#line (252, 5) - (252, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (253, 5) - (253, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, dd["new_key"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictMissingKeyCreatesEntry()
            {
#line (257, 5) - (257, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (258, 5) - (258, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int _val = dd["key"];
#line (259, 5) - (259, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.True(dd.ContainsKey("key"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictSetAndGetWorksNormally()
            {
#line (263, 5) - (263, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (264, 5) - (264, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["x"] = 42;
#line (265, 5) - (265, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(42, dd["x"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictNullFactoryThrowsTypeError()
            {
#line (269, 5) - (272, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (270, 9) - (270, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    global::Sharpy.DefaultDict<string, int> _dd = new global::Sharpy.DefaultDict<string, int>(null);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictGetWithDefaultDoesNotCreateEntry()
            {
#line (274, 5) - (274, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (275, 5) - (275, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(99, dd.Get("absent", 99));
#line (276, 5) - (276, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("absent"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictKeysReturnsAllKeys()
            {
#line (280, 5) - (280, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (281, 5) - (281, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (282, 5) - (282, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (283, 5) - (283, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<string> keys = new global::Sharpy.List<string>(dd.Keys);
#line (284, 5) - (284, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(keys));
#line (285, 5) - (285, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains("a", keys);
#line (286, 5) - (286, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains("b", keys);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictValuesReturnsAllValues()
            {
#line (290, 5) - (290, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (291, 5) - (291, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (292, 5) - (292, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (293, 5) - (293, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<int> vals = new global::Sharpy.List<int>(dd.Values);
#line (294, 5) - (294, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(vals));
#line (295, 5) - (295, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains(1, vals);
#line (296, 5) - (296, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains(2, vals);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictCopyPreservesFactoryAndItems()
            {
#line (300, 5) - (300, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (301, 5) - (301, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (302, 5) - (302, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (303, 5) - (303, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> copy = dd.Copy();
#line (304, 5) - (304, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, copy["a"]);
#line (305, 5) - (305, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, copy["b"]);
#line (306, 5) - (306, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, copy["c"]);
#line (307, 5) - (307, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("c"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictClearRemovesAllItems()
            {
#line (311, 5) - (311, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (312, 5) - (312, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (313, 5) - (313, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (314, 5) - (314, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd.Clear();
#line (315, 5) - (315, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, dd.Count);
#line (316, 5) - (316, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("a"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopExistingKeyReturnsAndRemoves()
            {
#line (320, 5) - (320, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (321, 5) - (321, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (322, 5) - (322, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                int value = dd.Pop("a");
#line (323, 5) - (323, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, value);
#line (324, 5) - (324, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("a"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopMissingKeyThrowsKeyError()
            {
#line (328, 5) - (328, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (329, 5) - (332, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (330, 9) - (330, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    dd.Pop("missing");
                }));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopWithDefaultReturnDefault()
            {
#line (334, 5) - (334, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (335, 5) - (335, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(99, dd.Pop("missing", 99));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictItemsReturnsKeyValueTuples()
            {
#line (339, 5) - (339, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (340, 5) - (340, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["x"] = 10;
#line (341, 5) - (341, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["y"] = 20;
#line (342, 5) - (342, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> items = dd.Items();
#line (343, 5) - (343, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (344, 5) - (344, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains(("x", 10), items);
#line (345, 5) - (345, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Contains(("y", 20), items);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictUpdateMergesFromDictionary()
            {
#line (349, 5) - (349, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (350, 5) - (350, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (351, 5) - (351, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.Dict<string, int> other = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        99
                    },
                    {
                        "b",
                        2
                    }
                };
#line (352, 5) - (352, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd.Update(other);
#line (353, 5) - (353, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(99, dd["a"]);
#line (354, 5) - (354, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, dd["b"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictSetDefaultExistingKeyReturnsExisting()
            {
#line (358, 5) - (358, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (359, 5) - (359, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 42;
#line (360, 5) - (360, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(42, dd.SetDefault("a", 99));
#line (361, 5) - (361, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(42, dd["a"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictSetDefaultMissingKeyInsertsAndReturns()
            {
#line (365, 5) - (365, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (366, 5) - (366, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(5, dd.SetDefault("a", 5));
#line (367, 5) - (367, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(5, dd["a"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictCountReflectsSize()
            {
#line (371, 5) - (371, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (372, 5) - (372, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(0, dd.Count);
#line (373, 5) - (373, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (374, 5) - (374, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (375, 5) - (375, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, dd.Count);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictUpdateFromTuplesMergesEntries()
            {
#line (379, 5) - (379, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (380, 5) - (380, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (381, 5) - (381, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd.Update(new Sharpy.List<global::System.ValueTuple<string, int>>() { ("a", 9), ("b", 2) });
#line (382, 5) - (382, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(9, dd["a"]);
#line (383, 5) - (383, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(2, dd["b"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopItemFifoReturnsFirstPair()
            {
#line (387, 5) - (387, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (388, 5) - (388, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (389, 5) - (389, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (390, 5) - (390, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::System.ValueTuple<string, int> pair = dd.PopItem();
#line (391, 5) - (391, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(("a", 1), pair);
#line (392, 5) - (392, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, dd.Count);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopItemLifoReturnsLastPair()
            {
#line (396, 5) - (396, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (397, 5) - (397, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (398, 5) - (398, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (399, 5) - (399, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::System.ValueTuple<string, int> pair = dd.PopItem(last: true);
#line (400, 5) - (400, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(("b", 2), pair);
#line (401, 5) - (401, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, dd.Count);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopItemEmptyThrowsKeyError()
            {
#line (405, 5) - (405, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (406, 5) - (409, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (407, 9) - (407, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    dd.PopItem();
                }));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictRemoveExistingKeyRemovesEntry()
            {
#line (411, 5) - (411, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (412, 5) - (412, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (413, 5) - (413, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (414, 5) - (414, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd.Remove("a");
#line (415, 5) - (415, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("a"));
#line (416, 5) - (416, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Equal(1, dd.Count);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictRemoveMissingKeyThrowsKeyError()
            {
#line (420, 5) - (420, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (421, 5) - (424, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (422, 9) - (422, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                    dd.Remove("missing");
                }));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictToDictionaryReturnsCopy()
            {
#line (426, 5) - (426, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (427, 5) - (427, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["a"] = 1;
#line (428, 5) - (428, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                dd["b"] = 2;
#line (429, 5) - (429, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Sharpy.Dict<string, int> plain = dd.ToDictionary();
#line (430, 5) - (430, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.True(dd.ContainsKey("a"));
#line (431, 5) - (431, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.True(dd.ContainsKey("b"));
#line (433, 5) - (433, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_module_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("c"));
            }
        }
    }
}

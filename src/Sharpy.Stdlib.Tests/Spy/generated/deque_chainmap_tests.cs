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
using static Sharpy.Stdlib.Tests.Spy.Collections.DequeChainmapTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.deque_chainmap_tests")]
        public static partial class DequeChainmapTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class DequeChainmapTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDequeConstructFromEmptyIsEmpty()
            {
#line (9, 5) - (9, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (10, 5) - (10, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (11, 5) - (11, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, n);
#line (12, 5) - (15, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (13, 9) - (13, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                    d.Pop();
                }));
            }

            [Xunit.FactAttribute]
            public void TestDequeAppendAfterPopCountReturnsToZero()
            {
#line (17, 5) - (17, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (18, 5) - (18, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append(42);
#line (19, 5) - (19, 12) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Pop();
#line (20, 5) - (20, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (21, 5) - (21, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, n);
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendOnEmptyAddsAllToRight()
            {
#line (27, 5) - (27, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (28, 5) - (28, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extend(new Sharpy.List<int>() { 1, 2, 3 });
#line (29, 5) - (29, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (30, 5) - (30, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, n);
#line (31, 5) - (31, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
#line (32, 5) - (32, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (33, 5) - (33, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendleftOnEmptyReversesOrder()
            {
#line (37, 5) - (37, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (38, 5) - (38, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extendleft(new Sharpy.List<int>() { 1, 2, 3 });
#line (39, 5) - (39, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (40, 5) - (40, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, n);
#line (41, 5) - (41, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
#line (42, 5) - (42, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (43, 5) - (43, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendEmptyIterableNoChange()
            {
#line (47, 5) - (47, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2 });
#line (48, 5) - (48, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (49, 5) - (49, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extend(empty);
#line (50, 5) - (50, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (51, 5) - (51, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendleftEmptyIterableNoChange()
            {
#line (55, 5) - (55, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2 });
#line (56, 5) - (56, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (57, 5) - (57, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extendleft(empty);
#line (58, 5) - (58, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (59, 5) - (59, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
            }

            [Xunit.FactAttribute]
            public void TestDequeClearAndRebuildWorksCorrectly()
            {
#line (65, 5) - (65, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<string> d = new global::Sharpy.Deque<string>(new Sharpy.List<string>() { "a", "b", "c" });
#line (66, 5) - (66, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Clear();
#line (67, 5) - (67, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append("x");
#line (68, 5) - (68, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append("y");
#line (69, 5) - (69, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (70, 5) - (70, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
#line (71, 5) - (71, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal("x", d.Popleft());
#line (72, 5) - (72, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal("y", d.Popleft());
            }

            [Xunit.FactAttribute]
            public void TestChainmapContainsSearchesAllMaps()
            {
#line (78, 5) - (78, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (79, 5) - (79, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        2
                    }
                };
#line (80, 5) - (80, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (81, 5) - (81, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Contains("a"));
#line (82, 5) - (82, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Contains("b"));
#line (83, 5) - (83, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.False(cm.Contains("c"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapPopKeyOnlyInSecondMapThrowsKeyError()
            {
#line (89, 5) - (89, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "x",
                        0
                    }
                };
#line (90, 5) - (90, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        2
                    }
                };
#line (91, 5) - (91, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (92, 5) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (93, 9) - (93, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                    cm.Pop("b");
                }));
            }

            [Xunit.FactAttribute]
            public void TestChainmapGetMissingKeyNoDefaultReturnsDefaultT()
            {
#line (99, 5) - (99, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (100, 5) - (100, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, cm.Get("missing"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapGetExistingKeyInSecondMap()
            {
#line (104, 5) - (104, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "y",
                        0
                    }
                };
#line (105, 5) - (105, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "x",
                        42
                    }
                };
#line (106, 5) - (106, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (107, 5) - (107, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(42, cm.Get("x"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapWriteGoesToFirstMapMakesItVisible()
            {
#line (113, 5) - (113, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (114, 5) - (114, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                cm["key"] = 99;
#line (115, 5) - (115, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(99, cm["key"]);
#line (116, 5) - (116, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Maps[0].ContainsKey("key"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapNewChildWritesToChildMapNotParent()
            {
#line (122, 5) - (122, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> parent = new Sharpy.Dict<string, int>()
                {
                    {
                        "shared",
                        1
                    }
                };
#line (123, 5) - (123, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(parent);
#line (124, 5) - (124, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> child = cm.NewChild();
#line (125, 5) - (125, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                child["new"] = 99;
#line (127, 5) - (127, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.False(parent.ContainsKey("new"));
#line (129, 5) - (129, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(child.ContainsKey("new"));
#line (130, 5) - (130, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, child["shared"]);
            }

            [Xunit.FactAttribute]
            public void TestChainmapMapsContainsCorrectCount()
            {
#line (136, 5) - (136, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        0
                    }
                };
#line (137, 5) - (137, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        0
                    }
                };
#line (138, 5) - (138, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (139, 5) - (139, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(cm.Maps));
            }

            [Xunit.FactAttribute]
            public void TestChainmapMapsMutatingFirstMapReflectedInLookup()
            {
#line (143, 5) - (143, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "x",
                        0
                    }
                };
#line (144, 5) - (144, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1);
#line (146, 5) - (146, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d1["added_later"] = 55;
#line (147, 5) - (147, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(55, cm["added_later"]);
            }
        }
    }
}

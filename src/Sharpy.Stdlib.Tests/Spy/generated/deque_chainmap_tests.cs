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
#line (9, 5) - (9, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (10, 5) - (10, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (11, 5) - (11, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, n);
#line (12, 5) - (15, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (13, 9) - (13, 16) 20 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                    d.Pop();
#line hidden
                }
                catch (IndexError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestDequeAppendAfterPopCountReturnsToZero()
            {
#line (17, 5) - (17, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (18, 5) - (18, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append(42);
#line (19, 5) - (19, 12) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Pop();
#line (20, 5) - (20, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (21, 5) - (21, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, n);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendOnEmptyAddsAllToRight()
            {
#line (27, 5) - (27, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (28, 5) - (28, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extend(new Sharpy.List<int>() { 1, 2, 3 });
#line (29, 5) - (29, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (30, 5) - (30, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, n);
#line (31, 5) - (31, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
#line (32, 5) - (32, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (33, 5) - (33, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendleftOnEmptyReversesOrder()
            {
#line (37, 5) - (37, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>();
#line (38, 5) - (38, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extendleft(new Sharpy.List<int>() { 1, 2, 3 });
#line (39, 5) - (39, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (40, 5) - (40, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, n);
#line (41, 5) - (41, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(3, d.Popleft());
#line (42, 5) - (42, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, d.Popleft());
#line (43, 5) - (43, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, d.Popleft());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendEmptyIterableNoChange()
            {
#line (47, 5) - (47, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2 });
#line (48, 5) - (48, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (49, 5) - (49, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extend(empty);
#line (50, 5) - (50, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (51, 5) - (51, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDequeExtendleftEmptyIterableNoChange()
            {
#line (55, 5) - (55, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<int> d = new global::Sharpy.Deque<int>(new Sharpy.List<int>() { 1, 2 });
#line (56, 5) - (56, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (57, 5) - (57, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Extendleft(empty);
#line (58, 5) - (58, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (59, 5) - (59, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDequeClearAndRebuildWorksCorrectly()
            {
#line (65, 5) - (65, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.Deque<string> d = new global::Sharpy.Deque<string>(new Sharpy.List<string>() { "a", "b", "c" });
#line (66, 5) - (66, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Clear();
#line (67, 5) - (67, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append("x");
#line (68, 5) - (68, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d.Append("y");
#line (69, 5) - (69, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                int n = d.Count;
#line (70, 5) - (70, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, n);
#line (71, 5) - (71, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal("x", d.Popleft());
#line (72, 5) - (72, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal("y", d.Popleft());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapContainsSearchesAllMaps()
            {
#line (78, 5) - (78, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        1
                    }
                };
#line (79, 5) - (79, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "b",
                        2
                    }
                };
#line (80, 5) - (80, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (81, 5) - (81, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Contains("a"));
#line (82, 5) - (82, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Contains("b"));
#line (83, 5) - (83, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.False(cm.Contains("c"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapPopKeyOnlyInSecondMapThrowsKeyError()
            {
#line (89, 5) - (89, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        0
                    }
                };
#line (90, 5) - (90, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "b",
                        2
                    }
                };
#line (91, 5) - (91, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (92, 5) - (97, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (93, 9) - (93, 20) 20 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                    cm.Pop("b");
#line hidden
                }
                catch (KeyError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected KeyError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestChainmapGetMissingKeyNoDefaultReturnsDefaultT()
            {
#line (99, 5) - (99, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (100, 5) - (100, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(0, cm.Get("missing"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapGetExistingKeyInSecondMap()
            {
#line (104, 5) - (104, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "y",
                        0
                    }
                };
#line (105, 5) - (105, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        42
                    }
                };
#line (106, 5) - (106, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (107, 5) - (107, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(42, cm.Get("x"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapWriteGoesToFirstMapMakesItVisible()
            {
#line (113, 5) - (113, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (114, 5) - (114, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                cm["key"] = 99;
#line (115, 5) - (115, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(99, cm["key"]);
#line (116, 5) - (116, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(cm.Maps[0].ContainsKey("key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapNewChildWritesToChildMapNotParent()
            {
#line (122, 5) - (122, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> parent = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "shared",
                        1
                    }
                };
#line (123, 5) - (123, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(parent);
#line (124, 5) - (124, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> child = cm.NewChild();
#line (125, 5) - (125, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                child["new"] = 99;
#line (127, 5) - (127, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.False(parent.ContainsKey("new"));
#line (129, 5) - (129, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.True(child.ContainsKey("new"));
#line (130, 5) - (130, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(1, child["shared"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapMapsContainsCorrectCount()
            {
#line (136, 5) - (136, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        0
                    }
                };
#line (137, 5) - (137, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "b",
                        0
                    }
                };
#line (138, 5) - (138, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (139, 5) - (139, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(cm.Maps));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestChainmapMapsMutatingFirstMapReflectedInLookup()
            {
#line (143, 5) - (143, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        0
                    }
                };
#line (144, 5) - (144, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1);
#line (146, 5) - (146, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                d1["added_later"] = 55;
#line (147, 5) - (147, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/deque_chainmap_tests.spy"
                Xunit.Assert.Equal(55, cm["added_later"]);
#line hidden
            }
        }
    }
}
#line default

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
using static Sharpy.Stdlib.Tests.Spy.Collections.CollectionsAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.collections_additional_tests")]
        public static partial class CollectionsAdditionalTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class CollectionsAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestOrderedDictMaintainsInsertionOrder()
            {
#line (9, 5) - (9, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (10, 5) - (10, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (11, 5) - (11, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (12, 5) - (12, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (13, 5) - (13, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "c", "a", "b" }, new global::Sharpy.List<string>(od.Keys()));
#line (14, 5) - (14, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 1, 2 }, new global::Sharpy.List<int>(od.Values()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictUpdateExistingKeyPreservesOrder()
            {
#line (18, 5) - (18, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (19, 5) - (19, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (20, 5) - (20, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (21, 5) - (21, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (22, 5) - (22, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 99;
#line (23, 5) - (23, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, new global::Sharpy.List<string>(od.Keys()));
#line (24, 5) - (24, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(99, od["b"]);
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndLast()
            {
#line (28, 5) - (28, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (29, 5) - (29, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (30, 5) - (30, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (31, 5) - (31, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (32, 5) - (32, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od.MoveToEnd("a", last: true);
#line (33, 5) - (33, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "b", "c", "a" }, new global::Sharpy.List<string>(od.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndFirst()
            {
#line (37, 5) - (37, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (38, 5) - (38, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (39, 5) - (39, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (40, 5) - (40, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (41, 5) - (41, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od.MoveToEnd("c", last: false);
#line (42, 5) - (42, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "c", "a", "b" }, new global::Sharpy.List<string>(od.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndMissingKeyThrowsKeyError()
            {
#line (46, 5) - (46, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (47, 5) - (47, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (48, 5) - (51, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (49, 9) - (49, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                    od.MoveToEnd("z");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopitemLast()
            {
#line (53, 5) - (53, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (54, 5) - (54, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (55, 5) - (55, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (56, 5) - (56, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (57, 5) - (57, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::System.ValueTuple<string, int> result = od.Popitem(last: true);
#line (58, 5) - (58, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal("c", result.Item1);
#line (59, 5) - (59, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(3, result.Item2);
#line (60, 5) - (60, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, od.Count);
#line (61, 5) - (61, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, new global::Sharpy.List<string>(od.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopitemFirst()
            {
#line (65, 5) - (65, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (66, 5) - (66, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (67, 5) - (67, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (68, 5) - (68, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["c"] = 3;
#line (69, 5) - (69, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::System.ValueTuple<string, int> result = od.Popitem(last: false);
#line (70, 5) - (70, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal("a", result.Item1);
#line (71, 5) - (71, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, result.Item2);
#line (72, 5) - (72, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, od.Count);
#line (73, 5) - (73, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "b", "c" }, new global::Sharpy.List<string>(od.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopitemEmptyThrowsKeyError()
            {
#line (77, 5) - (77, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (78, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (79, 9) - (79, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                    od.Popitem();
                }));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopReturnsValueAndRemoves()
            {
#line (83, 5) - (83, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (84, 5) - (84, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (85, 5) - (85, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (86, 5) - (86, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                int val = od.Pop("a");
#line (87, 5) - (87, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, val);
#line (88, 5) - (88, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, od.Count);
#line (89, 5) - (89, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(od.ContainsKey("a"));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopMissingKeyThrowsKeyError()
            {
#line (93, 5) - (93, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (94, 5) - (97, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (95, 9) - (95, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                    od.Pop("z");
                }));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictPopWithDefaultReturnDefault()
            {
#line (99, 5) - (99, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (100, 5) - (100, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(42, od.Pop("z", 42));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictContainsKey()
            {
#line (104, 5) - (104, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (105, 5) - (105, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (106, 5) - (106, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(od.ContainsKey("a"));
#line (107, 5) - (107, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(od.ContainsKey("b"));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictClear()
            {
#line (111, 5) - (111, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (112, 5) - (112, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (113, 5) - (113, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (114, 5) - (114, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od.Clear();
#line (115, 5) - (115, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(0, od.Count);
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictCopy()
            {
#line (119, 5) - (119, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (120, 5) - (120, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (121, 5) - (121, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["b"] = 2;
#line (122, 5) - (122, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> copy = od.Copy();
#line (123, 5) - (123, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, new global::Sharpy.List<string>(copy.Keys()));
#line (124, 5) - (124, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, copy["a"]);
#line (126, 5) - (126, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                copy["c"] = 3;
#line (127, 5) - (127, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(od.ContainsKey("c"));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictGetExistingKey()
            {
#line (131, 5) - (131, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (132, 5) - (132, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["a"] = 1;
#line (133, 5) - (133, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, od.Get("a"));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictGetMissingKeyReturnsDefault()
            {
#line (137, 5) - (137, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (138, 5) - (138, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(42, od.Get("a", 42));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictItems()
            {
#line (142, 5) - (142, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (143, 5) - (143, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["x"] = 10;
#line (144, 5) - (144, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                od["y"] = 20;
#line (145, 5) - (145, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> items = new global::Sharpy.List<global::System.ValueTuple<string, int>>(od.Items());
#line (146, 5) - (146, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<global::System.ValueTuple<string, int>>() { ("x", 10), ("y", 20) }, items);
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictConstructFromPairs()
            {
#line (150, 5) - (150, 111) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>(new Sharpy.List<global::System.ValueTuple<string, int>>() { ("a", 1), ("b", 2), ("c", 3) });
#line (151, 5) - (151, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, new global::Sharpy.List<string>(od.Keys()));
#line (152, 5) - (152, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, new global::Sharpy.List<int>(od.Values()));
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictGetMissingKeyThrowsKeyError()
            {
#line (156, 5) - (156, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (157, 5) - (162, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (158, 9) - (158, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                    int _val = od["z"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestChainmapLookupSearchesThroughChain()
            {
#line (164, 5) - (164, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (165, 5) - (165, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        2
                    },
                    {
                        "a",
                        99
                    }
                };
#line (166, 5) - (166, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (168, 5) - (168, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, cm["a"]);
#line (169, 5) - (169, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, cm["b"]);
            }

            [Xunit.FactAttribute]
            public void TestChainmapWritesGoToFirstMap()
            {
#line (173, 5) - (173, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "x",
                        0
                    }
                };
#line (174, 5) - (174, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (175, 5) - (175, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (176, 5) - (176, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                cm["x"] = 10;
#line (177, 5) - (177, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(d1.ContainsKey("x"));
#line (178, 5) - (178, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(d2.ContainsKey("x"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapContainsKeySearchesAllMaps()
            {
#line (182, 5) - (182, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (183, 5) - (183, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        2
                    }
                };
#line (184, 5) - (184, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (185, 5) - (185, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(cm.ContainsKey("a"));
#line (186, 5) - (186, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(cm.ContainsKey("b"));
#line (187, 5) - (187, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(cm.ContainsKey("c"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapMissingKeyThrowsKeyError()
            {
#line (191, 5) - (191, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (192, 5) - (195, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Throws<KeyError>((global::System.Action)(() =>
                {
#line (193, 9) - (193, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                    int _val = cm["z"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestChainmapNewChild()
            {
#line (197, 5) - (197, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (198, 5) - (198, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1);
#line (199, 5) - (199, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> child = new Sharpy.Dict<string, int>()
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
#line (200, 5) - (200, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> childCm = cm.NewChild(child);
#line (201, 5) - (201, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(99, childCm["a"]);
#line (202, 5) - (202, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, childCm["b"]);
#line (203, 5) - (203, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(childCm.Maps));
            }

            [Xunit.FactAttribute]
            public void TestChainmapNewChildNullCreatesEmptyDict()
            {
#line (207, 5) - (207, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (208, 5) - (208, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1);
#line (209, 5) - (209, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> childCm = cm.NewChild();
#line (210, 5) - (210, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(childCm.Maps));
#line (211, 5) - (211, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, childCm["a"]);
            }

            [Xunit.FactAttribute]
            public void TestChainmapParents()
            {
#line (215, 5) - (215, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (216, 5) - (216, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        2
                    }
                };
#line (217, 5) - (217, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d3 = new Sharpy.Dict<string, int>()
                {
                    {
                        "c",
                        3
                    }
                };
#line (218, 5) - (218, 85) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2, d3);
#line (219, 5) - (219, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> parents = cm.Parents;
#line (220, 5) - (220, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(parents.Maps));
#line (221, 5) - (221, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(parents.ContainsKey("b"));
#line (222, 5) - (222, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(parents.ContainsKey("c"));
#line (223, 5) - (223, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(parents.ContainsKey("a"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapParentsSingleMapReturnsEmpty()
            {
#line (227, 5) - (227, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (228, 5) - (228, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1);
#line (229, 5) - (229, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> parents = cm.Parents;
#line (230, 5) - (230, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(parents.Maps));
#line (231, 5) - (231, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.False(parents.ContainsKey("a"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapGetWithDefault()
            {
#line (235, 5) - (235, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (236, 5) - (236, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(42, cm.Get("x", 42));
            }

            [Xunit.FactAttribute]
            public void TestChainmapKeysReturnsUniqueKeys()
            {
#line (240, 5) - (240, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (241, 5) - (241, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "b",
                        20
                    },
                    {
                        "c",
                        30
                    }
                };
#line (242, 5) - (242, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (243, 5) - (243, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.List<string> keys = new global::Sharpy.List<string>(cm.Keys());
#line (244, 5) - (244, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line (245, 5) - (245, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Contains("a", keys);
#line (246, 5) - (246, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Contains("b", keys);
#line (247, 5) - (247, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Contains("c", keys);
            }

            [Xunit.FactAttribute]
            public void TestChainmapCountReturnsUniqueKeyCount()
            {
#line (251, 5) - (251, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (252, 5) - (252, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        10
                    },
                    {
                        "b",
                        20
                    }
                };
#line (253, 5) - (253, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (254, 5) - (254, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(2, cm.Count);
            }

            [Xunit.FactAttribute]
            public void TestChainmapPopRemovesFromFirstMap()
            {
#line (258, 5) - (258, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (259, 5) - (259, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        99
                    }
                };
#line (260, 5) - (260, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (261, 5) - (261, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                int val = cm.Pop("a");
#line (262, 5) - (262, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, val);
#line (264, 5) - (264, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(99, cm["a"]);
            }

            [Xunit.FactAttribute]
            public void TestChainmapClearClearsFirstMap()
            {
#line (268, 5) - (268, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (269, 5) - (269, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
                {
                    {
                        "c",
                        3
                    }
                };
#line (270, 5) - (270, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>(d1, d2);
#line (271, 5) - (271, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                cm.Clear();
#line (272, 5) - (272, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d1));
#line (273, 5) - (273, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(d2));
#line (274, 5) - (274, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.True(cm.ContainsKey("c"));
            }

            [Xunit.FactAttribute]
            public void TestChainmapDefaultConstructorHasOneEmptyMap()
            {
#line (278, 5) - (278, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                global::Sharpy.ChainMap<string, int> cm = new global::Sharpy.ChainMap<string, int>();
#line (279, 5) - (279, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(cm.Maps));
#line (280, 5) - (280, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/collections_additional_tests.spy"
                Xunit.Assert.Equal(0, cm.Count);
            }
        }
    }
}

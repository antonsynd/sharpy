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
using static Sharpy.Stdlib.Tests.Spy.Collections.OrderedDictTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.ordered_dict_tests")]
        public static partial class OrderedDictTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class OrderedDictTestsTests
        {
            [Xunit.FactAttribute]
            public void TestOrderedDictContainsExistingKeyReturnsTrue()
            {
#line (9, 5) - (9, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (10, 5) - (10, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (11, 5) - (11, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.True(od.Contains("a"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictContainsMissingKeyReturnsFalse()
            {
#line (15, 5) - (15, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (16, 5) - (16, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.False(od.Contains("z"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictValuesPreservesInsertionOrder()
            {
#line (22, 5) - (22, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (23, 5) - (23, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["c"] = 3;
#line (24, 5) - (24, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (25, 5) - (25, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (26, 5) - (26, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 1, 2 }, new global::Sharpy.List<int>(od.Values()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictValuesAfterUpdateOrderByFirstInsertion()
            {
#line (30, 5) - (30, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (31, 5) - (31, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (32, 5) - (32, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (33, 5) - (33, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 99;
#line (34, 5) - (34, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 99, 2 }, new global::Sharpy.List<int>(od.Values()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndDefaultIsLast()
            {
#line (40, 5) - (40, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (41, 5) - (41, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (42, 5) - (42, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (43, 5) - (43, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["c"] = 3;
#line (44, 5) - (44, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od.MoveToEnd("a");
#line (45, 5) - (45, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "b", "c", "a" }, new global::Sharpy.List<string>(od.Keys()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndAlreadyLastNoChange()
            {
#line (49, 5) - (49, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (50, 5) - (50, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (51, 5) - (51, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (52, 5) - (52, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od.MoveToEnd("b", last: true);
#line (53, 5) - (53, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, new global::Sharpy.List<string>(od.Keys()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictMoveToEndAlreadyFirstNoChange()
            {
#line (57, 5) - (57, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (58, 5) - (58, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (59, 5) - (59, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (60, 5) - (60, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od.MoveToEnd("a", last: false);
#line (61, 5) - (61, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, new global::Sharpy.List<string>(od.Keys()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictConstructFromTuplesPreservesOrder()
            {
#line (67, 5) - (67, 114) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>(new Sharpy.List<global::System.ValueTuple<string, int>>() { ("x", 10), ("y", 20), ("z", 30) });
#line (68, 5) - (68, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "x", "y", "z" }, new global::Sharpy.List<string>(od.Keys()));
#line (69, 5) - (69, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 20, 30 }, new global::Sharpy.List<int>(od.Values()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictAfterRemovalRemainingItemsCorrectlyIndexed()
            {
#line (75, 5) - (75, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (76, 5) - (76, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (77, 5) - (77, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (78, 5) - (78, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["c"] = 3;
#line (79, 5) - (79, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od.Pop("b");
#line (80, 5) - (80, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "c" }, new global::Sharpy.List<string>(od.Keys()));
#line (81, 5) - (81, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(1, od["a"]);
#line (82, 5) - (82, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(3, od["c"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictAfterRemovalReinsertGoesToEnd()
            {
#line (86, 5) - (86, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (87, 5) - (87, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (88, 5) - (88, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["b"] = 2;
#line (89, 5) - (89, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["c"] = 3;
#line (90, 5) - (90, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od.Pop("a");
#line (91, 5) - (91, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 99;
#line (92, 5) - (92, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "b", "c", "a" }, new global::Sharpy.List<string>(od.Keys()));
#line (93, 5) - (93, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(99, od["a"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictItemsPreservesInsertionOrder()
            {
#line (99, 5) - (99, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (100, 5) - (100, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["z"] = 26;
#line (101, 5) - (101, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["a"] = 1;
#line (102, 5) - (102, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                od["m"] = 13;
#line (103, 5) - (103, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> items = new global::Sharpy.List<global::System.ValueTuple<string, int>>(od.Items());
#line (104, 5) - (104, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<global::System.ValueTuple<string, int>>() { ("z", 26), ("a", 1), ("m", 13) }, items);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictEmptyCountIsZero()
            {
#line (110, 5) - (110, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (111, 5) - (111, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(0, od.Count);
#line (112, 5) - (112, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Sharpy.List<string> emptyKeys = new global::Sharpy.List<string>(od.Keys());
#line (113, 5) - (113, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(emptyKeys));
#line (114, 5) - (114, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Sharpy.List<int> emptyVals = new global::Sharpy.List<int>(od.Values());
#line (115, 5) - (115, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(emptyVals));
#line (116, 5) - (116, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> emptyItems = new global::Sharpy.List<global::System.ValueTuple<string, int>>(od.Items());
#line (117, 5) - (117, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(emptyItems));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOrderedDictEmptyContainsReturnsFalse()
            {
#line (121, 5) - (121, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                global::Sharpy.OrderedDict<string, int> od = new global::Sharpy.OrderedDict<string, int>();
#line (122, 5) - (122, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.False(od.ContainsKey("anything"));
#line (123, 5) - (123, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/ordered_dict_tests.spy"
                Xunit.Assert.False(od.Contains("anything"));
#line hidden
            }
        }
    }
}
#line default

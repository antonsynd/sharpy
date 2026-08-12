// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using collections = global::Sharpy.Collections;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Collections.CounterCompleteTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.counter_complete_tests")]
        public static partial class CounterCompleteTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class CounterCompleteTestsTests
        {
            [Xunit.FactAttribute]
            public void TestCounterContainsExistingKeyReturnsTrue()
            {
#line (7, 5) - (7, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "a" });
#line (8, 5) - (8, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.True(c.Contains("a"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterContainsMissingKeyReturnsFalse()
            {
#line (12, 5) - (12, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (13, 5) - (13, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(c.Contains("z"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterContainsKeyAfterClearReturnsFalse()
            {
#line (17, 5) - (17, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (18, 5) - (18, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c.Clear();
#line (19, 5) - (19, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(c.ContainsKey("a"));
#line (20, 5) - (20, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(c.ContainsKey("b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterKeysContainsAllAddedElements()
            {
#line (26, 5) - (26, 82) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b", "c", "a" });
#line (27, 5) - (27, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> keys = new global::Sharpy.List<string>(c.Keys());
#line (28, 5) - (28, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Contains("a", keys);
#line (29, 5) - (29, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Contains("b", keys);
#line (30, 5) - (30, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Contains("c", keys);
#line (31, 5) - (31, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterKeysEmptyReturnsEmpty()
            {
#line (35, 5) - (35, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (36, 5) - (36, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> keys = new global::Sharpy.List<string>(c.Keys());
#line (37, 5) - (37, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(keys));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterMostCommonZeroReturnsEmptyList()
            {
#line (43, 5) - (43, 87) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b", "b", "c" });
#line (44, 5) - (44, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> result = c.MostCommon(0);
#line (45, 5) - (45, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterMostCommonNGreaterThanCountReturnsAll()
            {
#line (49, 5) - (49, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (50, 5) - (50, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> result = c.MostCommon(100);
#line (51, 5) - (51, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterMostCommonTieBreak()
            {
#line (55, 5) - (55, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "b", "a", "c" });
#line (56, 5) - (56, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, int>> result = c.MostCommon();
#line (57, 5) - (57, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(result));
#line (59, 5) - (59, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result.GetItemUnchecked(0).Item2);
#line (60, 5) - (60, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result.GetItemUnchecked(1).Item2);
#line (61, 5) - (61, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result.GetItemUnchecked(2).Item2);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterElementsZeroCountNotYielded()
            {
#line (67, 5) - (67, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (68, 5) - (68, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["a"] = 2;
#line (69, 5) - (69, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["b"] = 0;
#line (70, 5) - (70, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> elements = new global::Sharpy.List<string>(c.Elements());
#line (71, 5) - (71, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Contains("a", elements);
#line (72, 5) - (72, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.DoesNotContain("b", elements);
#line (73, 5) - (73, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(elements));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterElementsNegativeCountNotYielded()
            {
#line (77, 5) - (77, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (78, 5) - (78, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["a"] = 2;
#line (79, 5) - (79, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["b"] = -1;
#line (80, 5) - (80, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> elements = new global::Sharpy.List<string>(c.Elements());
#line (81, 5) - (81, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Contains("a", elements);
#line (82, 5) - (82, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.DoesNotContain("b", elements);
#line (83, 5) - (83, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(elements));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterElementsAllNegativeReturnsEmpty()
            {
#line (87, 5) - (87, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (88, 5) - (88, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c.Subtract(new Sharpy.List<string>() { "a", "a", "b", "b" });
#line (89, 5) - (89, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> elements = new global::Sharpy.List<string>(c.Elements());
#line (90, 5) - (90, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(elements));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterUpdateTwiceAccumulatesCounts()
            {
#line (96, 5) - (96, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (97, 5) - (97, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c.Update(new Sharpy.List<string>() { "a", "b" });
#line (98, 5) - (98, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c.Update(new Sharpy.List<string>() { "b", "c" });
#line (99, 5) - (99, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(2, c["a"]);
#line (100, 5) - (100, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(2, c["b"]);
#line (101, 5) - (101, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, c["c"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterUpdateEmptyIterableNoChange()
            {
#line (105, 5) - (105, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (106, 5) - (106, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
#line hidden
                {
                };
#line (107, 5) - (107, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c.Update(empty);
#line (108, 5) - (108, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, c["a"]);
#line (109, 5) - (109, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, c["b"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterIndexerSetNewKeyCreatesEntry()
            {
#line (115, 5) - (115, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (116, 5) - (116, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["new"] = 5;
#line (117, 5) - (117, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(5, c["new"]);
#line (118, 5) - (118, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.True(c.Contains("new"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterIndexerSetOverridesExistingCount()
            {
#line (122, 5) - (122, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "a" });
#line (123, 5) - (123, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["a"] = 1;
#line (124, 5) - (124, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, c["a"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterIndexerSetNegativeCountIsNegative()
            {
#line (128, 5) - (128, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c = new global::Sharpy.Counter<string>();
#line (129, 5) - (129, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                c["x"] = -3;
#line (130, 5) - (130, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(-3, c["x"]);
#line (131, 5) - (131, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(-3, c.Total());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterCopyModifyingCopyDoesNotAffectOriginal()
            {
#line (137, 5) - (137, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> original = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "b" });
#line (138, 5) - (138, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> copy = original.Copy();
#line (139, 5) - (139, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                copy["a"] = 99;
#line (140, 5) - (140, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                copy["new"] = 5;
#line (141, 5) - (141, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, original["a"]);
#line (142, 5) - (142, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(original.Contains("new"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorSubtractAllGoZeroOrNegativeEmptyResult()
            {
#line (148, 5) - (148, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (149, 5) - (149, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "a" });
#line (150, 5) - (150, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> result = c1 - c2;
#line (151, 5) - (151, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(result.ContainsKey("a"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorAddIncludesKeysFromBoth()
            {
#line (155, 5) - (155, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a" });
#line (156, 5) - (156, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "b" });
#line (157, 5) - (157, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> result = c1 + c2;
#line (158, 5) - (158, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result["a"]);
#line (159, 5) - (159, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result["b"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCounterOperatorAndOnlyIncludesSharedKeys()
            {
#line (163, 5) - (163, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c1 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "a", "a", "b" });
#line (164, 5) - (164, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> c2 = new global::Sharpy.Counter<string>(new Sharpy.List<string>() { "b", "c" });
#line (165, 5) - (165, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                global::Sharpy.Counter<string> result = c1 & c2;
#line (166, 5) - (166, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(result.ContainsKey("a"));
#line (167, 5) - (167, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.Equal(1, result["b"]);
#line (168, 5) - (168, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/collections/counter_complete_tests.spy"
                Xunit.Assert.False(result.ContainsKey("c"));
#line hidden
            }
        }
    }
}
#line default

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
using functools = global::Sharpy.Functools;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Functools.FunctoolsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Functools
    {
        [global::Sharpy.SharpyModule("functools.functools_tests")]
        public static partial class FunctoolsTests
        {
        }
    }

    public static partial class Functools
    {
        public partial class FunctoolsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestReduceSumWithoutInitial()
            {
#line (9, 5) - (9, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, new Sharpy.List<int>() { 1, 2, 3, 4, 5 });
#line (10, 5) - (10, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(15, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceSumWithInitial()
            {
#line (14, 5) - (14, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, new Sharpy.List<int>() { 1, 2, 3, 4, 5 }, 10);
#line (15, 5) - (15, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(25, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceSingleElementWithoutInitial()
            {
#line (19, 5) - (19, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, new Sharpy.List<int>() { 42 });
#line (20, 5) - (20, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(42, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceEmptyIterableWithoutInitialThrowsTypeError()
            {
#line (24, 5) - (24, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (25, 5) - (28, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (26, 9) - (26, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                    functools.Reduce((x, y) => x + y, items);
                }));
            }

            [Xunit.FactAttribute]
            public void TestReduceEmptyIterableWithInitialReturnsInitial()
            {
#line (30, 5) - (30, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (31, 5) - (31, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, items, 42);
#line (32, 5) - (32, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(42, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceStringConcatenation()
            {
#line (36, 5) - (36, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                string result = functools.Reduce((x, y) => x + y, new Sharpy.List<string>() { "a", "b", "c" });
#line (37, 5) - (37, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal("abc", result);
            }

            [Xunit.FactAttribute]
            public void TestReduceProduct()
            {
#line (41, 5) - (41, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x * y, new Sharpy.List<int>() { 1, 2, 3, 4 });
#line (42, 5) - (42, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(24, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceWithInitialSingleElement()
            {
#line (46, 5) - (46, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, new Sharpy.List<int>() { 5 }, 10);
#line (47, 5) - (47, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(15, result);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyReturnsComparer()
            {
#line (54, 5) - (54, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                var comparer = functools.CmpToKey((int a, int b) => a - b);
#line (55, 5) - (55, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.NotNull(comparer);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyAscendingSort()
            {
#line (60, 5) - (60, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
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
#line (61, 5) - (61, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort();
#line (62, 5) - (62, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 1, 2, 3, 4, 5, 6, 9 }, items);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyDescendingSort()
            {
#line (67, 5) - (67, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
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
#line (68, 5) - (68, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort(reverse: true);
#line (69, 5) - (69, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 9, 6, 5, 4, 3, 2, 1, 1 }, items);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyStringLengthSort()
            {
#line (74, 5) - (74, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<string> items = new Sharpy.List<string>()
                {
                    "hello",
                    "hi",
                    "hey"
                };
#line (75, 5) - (75, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort(key: s => s.Length);
#line (76, 5) - (76, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hi", "hey", "hello" }, items);
            }

            [Xunit.FactAttribute]
            public void TestReduceWithRange()
            {
#line (81, 5) - (81, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (82, 5) - (82, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, items);
#line (83, 5) - (83, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(15, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceLargeListWithoutInitial()
            {
#line (88, 5) - (88, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>(global::Sharpy.Builtins.Range(1, 1001).Select(i => i));
#line (89, 5) - (89, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, items);
#line (90, 5) - (90, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(500500, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceLeftFoldSubtractionWithoutInitial()
            {
#line (95, 5) - (95, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x - y, new Sharpy.List<int>() { 10, 1, 2, 3 });
#line (96, 5) - (96, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(4, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceLeftFoldSubtractionWithInitial()
            {
#line (101, 5) - (101, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x - y, new Sharpy.List<int>() { 1, 2, 3 }, 100);
#line (102, 5) - (102, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(94, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceSingleElementDoesNotInvokeFunc()
            {
#line (107, 5) - (107, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, new Sharpy.List<int>() { 99 });
#line (108, 5) - (108, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(99, result);
            }

            [Xunit.FactAttribute]
            public void TestReduceEmptyIterableWithInitialDoesNotInvokeFunc()
            {
#line (112, 5) - (112, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (113, 5) - (113, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                int result = functools.Reduce((x, y) => x + y, items, 7);
#line (114, 5) - (114, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(7, result);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyEmptyListSortStaysEmpty()
            {
#line (118, 5) - (118, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (119, 5) - (119, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort();
#line (120, 5) - (120, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeySingleElementSort()
            {
#line (124, 5) - (124, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    42
                };
#line (125, 5) - (125, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort();
#line (126, 5) - (126, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42 }, items);
            }

            [Xunit.FactAttribute]
            public void TestCmpToKeyAllEqualPreservesAllElements()
            {
#line (131, 5) - (131, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    3,
                    1,
                    2,
                    1,
                    3
                };
#line (132, 5) - (132, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                items.Sort();
#line (133, 5) - (133, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.Builtins.Len(items));
#line (134, 5) - (134, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Contains(1, items);
#line (135, 5) - (135, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Contains(2, items);
#line (136, 5) - (136, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/functools_tests.spy"
                Xunit.Assert.Contains(3, items);
            }
        }
    }
}

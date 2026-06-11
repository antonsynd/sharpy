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
using pprint = global::Sharpy.PprintModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Pprint.PprintTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Pprint
    {
        [global::Sharpy.SharpyModule("pprint.pprint_tests")]
        public static partial class PprintTests
        {
        }
    }

    public static partial class Pprint
    {
        public partial class PprintTestsTests
        {
            [Xunit.FactAttribute]
            public void TestPformatIntegerReturnsDigits()
            {
#line (9, 5) - (9, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (10, 5) - (10, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("42", pp.Pformat(42));
            }

            [Xunit.FactAttribute]
            public void TestPformatStringReturnsPythonRepr()
            {
#line (14, 5) - (14, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (15, 5) - (15, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("'hello'", pp.Pformat("hello"));
            }

            [Xunit.FactAttribute]
            public void TestPformatNoneReturnsNone()
            {
#line (19, 5) - (19, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (20, 5) - (20, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("None", pp.Pformat(null));
            }

            [Xunit.FactAttribute]
            public void TestPformatTrueReturnsTrue()
            {
#line (24, 5) - (24, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (25, 5) - (25, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("True", pp.Pformat(true));
            }

            [Xunit.FactAttribute]
            public void TestPformatFalseReturnsFalse()
            {
#line (29, 5) - (29, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (30, 5) - (30, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("False", pp.Pformat(false));
            }

            [Xunit.FactAttribute]
            public void TestPformatDoubleIncludesDecimalPoint()
            {
#line (34, 5) - (34, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (35, 5) - (35, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                string result = pp.Pformat(3.14d);
#line (36, 5) - (36, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains(".", result);
#line (37, 5) - (37, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("3.14", result);
            }

            [Xunit.FactAttribute]
            public void TestPformatWholeDoubleAppendsDotZero()
            {
#line (41, 5) - (41, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (42, 5) - (42, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("5.0", pp.Pformat(5.0d));
            }

            [Xunit.FactAttribute]
            public void TestPformatSimpleDictFormatsEntries()
            {
#line (48, 5) - (48, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (49, 5) - (49, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
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
#line (50, 5) - (50, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{'a': 1, 'b': 2}", pp.Pformat(d));
            }

            [Xunit.FactAttribute]
            public void TestPformatDictSortsKeysByDefault()
            {
#line (54, 5) - (54, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (55, 5) - (55, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (56, 5) - (56, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["b"] = 2;
#line (57, 5) - (57, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["a"] = 1;
#line (58, 5) - (58, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["c"] = 3;
#line (59, 5) - (59, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{'a': 1, 'b': 2, 'c': 3}", pp.Pformat(d));
            }

            [Xunit.FactAttribute]
            public void TestPformatDictSortDictsFalsePreservesInsertionOrder()
            {
#line (63, 5) - (63, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter(sortDicts: false);
#line (64, 5) - (64, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (65, 5) - (65, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["b"] = 2;
#line (66, 5) - (66, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["a"] = 1;
#line (67, 5) - (67, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["c"] = 3;
#line (68, 5) - (68, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{'b': 2, 'a': 1, 'c': 3}", pp.Pformat(d));
            }

            [Xunit.FactAttribute]
            public void TestPformatEmptyDictReturnsBraces()
            {
#line (72, 5) - (72, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (73, 5) - (73, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (74, 5) - (74, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{}", pp.Pformat(d));
            }

            [Xunit.FactAttribute]
            public void TestPformatShortListSingleLine()
            {
#line (80, 5) - (80, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (81, 5) - (81, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (82, 5) - (82, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("[1, 2, 3]", pp.Pformat(lst));
            }

            [Xunit.FactAttribute]
            public void TestPformatEmptyListReturnsBrackets()
            {
#line (86, 5) - (86, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (87, 5) - (87, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                };
#line (88, 5) - (88, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("[]", pp.Pformat(lst));
            }

            [Xunit.FactAttribute]
            public void TestPformatLongListWrapsAcrossLines()
            {
#line (92, 5) - (92, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter(width: 20);
#line (93, 5) - (93, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    100,
                    200,
                    300,
                    400,
                    500,
                    600
                };
#line (94, 5) - (94, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                string result = pp.Pformat(lst);
#line (95, 5) - (95, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains("\n", result);
            }

            [Xunit.FactAttribute]
            public void TestPformatCompactListPacksMultiplePerLine()
            {
#line (99, 5) - (99, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var compact = new global::Sharpy.PrettyPrinter(width: 20, compact: true);
#line (100, 5) - (100, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    100,
                    200,
                    300,
                    400,
                    500,
                    600
                };
#line (101, 5) - (101, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                string result = compact.Pformat(lst);
#line (102, 5) - (102, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (104, 5) - (104, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains(", ", result);
            }

            [Xunit.FactAttribute]
            public void TestPformatSetFormatsWithBraces()
            {
#line (110, 5) - (110, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (111, 5) - (111, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Set<int> s = new Sharpy.Set<int>()
                {
                    1,
                    2,
                    3
                };
#line (112, 5) - (112, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{1, 2, 3}", pp.Pformat(s));
            }

            [Xunit.FactAttribute]
            public void TestPformatEmptySetReturnsSetCall()
            {
#line (116, 5) - (116, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (117, 5) - (117, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Set<int> s = new global::Sharpy.Set<int>();
#line (118, 5) - (118, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("set()", pp.Pformat(s));
            }

            [Xunit.FactAttribute]
            public void TestPformatTupleFormatsWithParens()
            {
#line (124, 5) - (124, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (125, 5) - (125, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("(1, 2, 3)", pp.Pformat((1, 2, 3)));
            }

            [Xunit.FactAttribute]
            public void TestPformatSingleElementTupleHasTrailingComma()
            {
#line (129, 5) - (129, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (130, 5) - (130, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("(1,)", pp.Pformat(System.ValueTuple.Create(1)));
            }

            [Xunit.FactAttribute]
            public void TestPformatDepthOneTruncatesInnerCollections()
            {
#line (136, 5) - (136, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter(depth: 1);
#line (137, 5) - (137, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> inner = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (138, 5) - (138, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<object> outer = new Sharpy.List<object>()
                {
                    inner
                };
#line (139, 5) - (139, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("[...]", pp.Pformat(outer));
            }

            [Xunit.FactAttribute]
            public void TestIsrecursiveSelfReferencingListReturnsTrue()
            {
#line (145, 5) - (145, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<object> lst = new Sharpy.List<object>()
                {
                };
#line (146, 5) - (146, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                lst.Append(lst);
#line (147, 5) - (147, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (148, 5) - (148, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.True(pp.Isrecursive(lst));
            }

            [Xunit.FactAttribute]
            public void TestIsreadableSelfReferencingListReturnsFalse()
            {
#line (152, 5) - (152, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<object> lst = new Sharpy.List<object>()
                {
                };
#line (153, 5) - (153, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                lst.Append(lst);
#line (154, 5) - (154, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (155, 5) - (155, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.False(pp.Isreadable(lst));
            }

            [Xunit.FactAttribute]
            public void TestPformatSelfReferencingListShowsRecursionMarker()
            {
#line (159, 5) - (159, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<object> lst = new Sharpy.List<object>()
                {
                };
#line (160, 5) - (160, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                lst.Append(lst);
#line (161, 5) - (161, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (162, 5) - (162, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains("<Recursion on list with id=", pp.Pformat(lst));
            }

            [Xunit.FactAttribute]
            public void TestIsrecursiveNonRecursiveListReturnsFalse()
            {
#line (166, 5) - (166, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (167, 5) - (167, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (168, 5) - (168, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.False(pp.Isrecursive(lst));
            }

            [Xunit.FactAttribute]
            public void TestIsreadableSimpleListReturnsTrue()
            {
#line (172, 5) - (172, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (173, 5) - (173, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter();
#line (174, 5) - (174, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.True(pp.Isreadable(lst));
            }

            [Xunit.FactAttribute]
            public void TestConstructorNegativeIndentThrows()
            {
#line (180, 5) - (183, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (181, 9) - (181, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                    new global::Sharpy.PrettyPrinter(indent: -1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestConstructorZeroWidthThrows()
            {
#line (185, 5) - (188, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (186, 9) - (186, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                    new global::Sharpy.PrettyPrinter(width: 0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestPformatCustomIndentAndWidthWrapsWithIndent()
            {
#line (190, 5) - (190, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                var pp = new global::Sharpy.PrettyPrinter(indent: 4, width: 10);
#line (191, 5) - (191, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<int> lst = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };
#line (192, 5) - (192, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                string result = pp.Pformat(lst);
#line (193, 5) - (193, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (195, 5) - (195, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Contains("\n    ", result);
            }

            [Xunit.FactAttribute]
            public void TestModulePformatIntegerWorks()
            {
#line (201, 5) - (201, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("42", pprint.Pformat(42));
            }

            [Xunit.FactAttribute]
            public void TestModulePformatDictSortsKeys()
            {
#line (205, 5) - (205, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (206, 5) - (206, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["b"] = 2;
#line (207, 5) - (207, 15) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                d["a"] = 1;
#line (208, 5) - (208, 52) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.Equal("{'a': 1, 'b': 2}", pprint.Pformat(d));
            }

            [Xunit.FactAttribute]
            public void TestModuleIsrecursiveSelfReferencingReturnsTrue()
            {
#line (212, 5) - (212, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Sharpy.List<object> lst = new Sharpy.List<object>()
                {
                };
#line (213, 5) - (213, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                lst.Append(lst);
#line (214, 5) - (214, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.True(pprint.Isrecursive(lst));
            }

            [Xunit.FactAttribute]
            public void TestModuleIsreadableSimpleReturnsTrue()
            {
#line (218, 5) - (218, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/pprint/pprint_tests.spy"
                Xunit.Assert.True(pprint.Isreadable(42));
            }
        }
    }
}

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
using toml = global::Sharpy.Toml;
using math = global::Sharpy.MathModule;
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Toml.TomlModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Toml
    {
        [global::Sharpy.SharpyModule("toml.toml_module_tests")]
        public static partial class TomlModuleTests
        {
            public static bool EqLong(object value, long expected)
            {
#line (339, 5) - (339, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                return @operator.Eq(value, expected);
            }
        }
    }

    public static partial class Toml
    {
        public partial class TomlModuleTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestLoadsStringReturnsDict()
            {
#line (23, 5) - (23, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("key = \"hello\"");
#line (24, 5) - (24, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["key"], "hello"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntegerReturnsLong()
            {
#line (28, 5) - (28, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("count = 42");
#line (29, 5) - (29, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeIntegerReturnsLong()
            {
#line (33, 5) - (33, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = -10");
#line (34, 5) - (34, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], -10));
            }

            [Xunit.FactAttribute]
            public void TestLoadsFloatReturnsDouble()
            {
#line (38, 5) - (38, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("pi = 3.14");
#line (39, 5) - (39, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["pi"], 3.14d));
            }

            [Xunit.FactAttribute]
            public void TestLoadsBooleanReturnsBool()
            {
#line (43, 5) - (43, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("flag = true");
#line (44, 5) - (44, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["flag"], true));
            }

            [Xunit.FactAttribute]
            public void TestLoadsFalseBooleanReturnsBool()
            {
#line (48, 5) - (48, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("flag = false");
#line (49, 5) - (49, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["flag"], false));
            }

            [Xunit.FactAttribute]
            public void TestLoadsArrayReturnsList()
            {
#line (55, 5) - (55, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("arr = [1, 2, 3]");
#line (56, 5) - (65, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["arr"])
                {
                    case global::Sharpy.IList l:
#line (58, 13) - (58, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(l));
#line (59, 13) - (59, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[0], 1));
#line (60, 13) - (60, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[1], 2));
#line (61, 13) - (61, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[2], 3));
                        break;
                    default:
#line (63, 13) - (63, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsTableReturnsDict()
            {
#line (67, 5) - (67, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[server]\nhost = \"localhost\"\nport = 8080");
#line (68, 5) - (75, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["server"])
                {
                    case global::Sharpy.IDict server:
#line (70, 13) - (70, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (71, 13) - (71, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(server["port"], 8080));
                        break;
                    default:
#line (73, 13) - (73, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedTablesReturnsNestedDicts()
            {
#line (77, 5) - (77, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[a]\n[a.b]\n[a.b.c]\nval = 1");
#line (78, 5) - (92, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["a"])
                {
                    case global::Sharpy.IDict a:
#line (80, 13) - (89, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (a["b"])
                        {
                            case global::Sharpy.IDict b:
#line (82, 21) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                switch (b["c"])
                                {
                                    case global::Sharpy.IDict c:
#line (84, 29) - (84, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                        Xunit.Assert.True(EqLong(c["val"], 1));
                                        break;
                                    default:
#line (86, 29) - (86, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                        Xunit.Assert.True(false);
                                        break;
                                }

                                break;
                            default:
#line (88, 21) - (88, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (90, 13) - (90, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsDottedKeys()
            {
#line (94, 5) - (94, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("a.b.c = 42");
#line (95, 5) - (105, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["a"])
                {
                    case global::Sharpy.IDict a:
#line (97, 13) - (102, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (a["b"])
                        {
                            case global::Sharpy.IDict b:
#line (99, 21) - (99, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(EqLong(b["c"], 42));
                                break;
                            default:
#line (101, 21) - (101, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (103, 13) - (103, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsInlineTable()
            {
#line (107, 5) - (107, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("point = {x = 1, y = 2}");
#line (108, 5) - (115, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["point"])
                {
                    case global::Sharpy.IDict point:
#line (110, 13) - (110, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(point["x"], 1));
#line (111, 13) - (111, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(point["y"], 2));
                        break;
                    default:
#line (113, 13) - (113, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsArrayOfTables()
            {
#line (117, 5) - (117, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[[products]]\nname = \"Hammer\"\n\n[[products]]\nname = \"Nail\"");
#line (118, 5) - (136, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["products"])
                {
                    case global::Sharpy.IList products:
#line (120, 13) - (120, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(products));
#line (121, 13) - (126, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (products[0])
                        {
                            case global::Sharpy.IDict p0:
#line (123, 21) - (123, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(p0["name"], "Hammer"));
                                break;
                            default:
#line (125, 21) - (125, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

#line (126, 13) - (131, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (products[1])
                        {
                            case global::Sharpy.IDict p1:
#line (128, 21) - (128, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(p1["name"], "Nail"));
                                break;
                            default:
#line (130, 21) - (130, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (132, 13) - (132, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMultilineBasicString()
            {
#line (138, 5) - (138, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("s = \"\"\"\nhello\nworld\"\"\"");
#line (139, 5) - (146, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["s"])
                {
                    case string s:
#line (141, 13) - (141, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Contains("hello", s);
#line (142, 13) - (142, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Contains("world", s);
                        break;
                    default:
#line (144, 13) - (144, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsLiteralString()
            {
#line (148, 5) - (148, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("path = 'C:\\Users\\foo'");
#line (149, 5) - (149, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["path"], "C:\\Users\\foo"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsHexInteger()
            {
#line (155, 5) - (155, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0xff");
#line (156, 5) - (156, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 255));
            }

            [Xunit.FactAttribute]
            public void TestLoadsOctalInteger()
            {
#line (160, 5) - (160, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0o77");
#line (161, 5) - (161, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 63));
            }

            [Xunit.FactAttribute]
            public void TestLoadsBinaryInteger()
            {
#line (165, 5) - (165, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0b1010");
#line (166, 5) - (166, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 10));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnderscoreInteger()
            {
#line (170, 5) - (170, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 1_000_000");
#line (171, 5) - (171, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 1000000));
            }

            [Xunit.FactAttribute]
            public void TestLoadsPositiveInfinity()
            {
#line (177, 5) - (177, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = inf");
#line (178, 5) - (178, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeInfinity()
            {
#line (182, 5) - (182, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = -inf");
#line (183, 5) - (183, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], -math.Inf));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNan()
            {
#line (187, 5) - (187, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = nan");
#line (189, 5) - (189, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], math.Nan));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyDocumentReturnsEmptyDict()
            {
#line (195, 5) - (195, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("");
#line (196, 5) - (196, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestLoadsCommentOnlyReturnsEmptyDict()
            {
#line (200, 5) - (200, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("# just a comment");
#line (201, 5) - (201, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestDumpsSimpleDictReturnsToml()
            {
#line (207, 5) - (207, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (208, 5) - (208, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (209, 5) - (209, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (210, 5) - (210, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (211, 5) - (211, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("name = \"test\"", result);
#line (212, 5) - (212, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("count = 42", result);
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictReturnsToml()
            {
#line (216, 5) - (216, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (217, 5) - (217, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["key"] = "val";
#line (218, 5) - (218, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (219, 5) - (219, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["section"] = inner;
#line (220, 5) - (220, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (221, 5) - (221, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("[section]", result);
#line (222, 5) - (222, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("key = \"val\"", result);
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithArrayReturnsToml()
            {
#line (226, 5) - (226, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
                {
                };
#line (227, 5) - (227, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                items.Append("a");
#line (228, 5) - (228, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                items.Append("b");
#line (229, 5) - (229, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (230, 5) - (230, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["tags"] = items;
#line (231, 5) - (231, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (232, 5) - (232, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("tags", result);
#line (233, 5) - (233, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("\"a\"", result);
#line (234, 5) - (234, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("\"b\"", result);
            }

            [Xunit.FactAttribute]
            public void TestDumpsSortKeysSortsAlphabetically()
            {
#line (238, 5) - (238, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (239, 5) - (239, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["zebra"] = 1L;
#line (240, 5) - (240, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["alpha"] = 2L;
#line (241, 5) - (241, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d, sortKeys: true);
#line (242, 5) - (242, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(result.Index("alpha") < result.Index("zebra"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonDictThrowsTypeError()
            {
#line (246, 5) - (249, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (247, 9) - (247, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Dumps("not a dict");
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullThrowsTypeError()
            {
#line (251, 5) - (256, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (252, 9) - (252, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Dumps(null);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRoundtripSimpleDict()
            {
#line (258, 5) - (258, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (259, 5) - (259, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (260, 5) - (260, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (261, 5) - (261, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["active"] = true;
#line (262, 5) - (262, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["ratio"] = 3.14d;
#line (263, 5) - (263, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads(toml.Dumps(d));
#line (264, 5) - (264, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (265, 5) - (265, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
#line (266, 5) - (266, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["active"], true));
#line (267, 5) - (267, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["ratio"], 3.14d));
            }

            [Xunit.FactAttribute]
            public void TestRoundtripNestedDict()
            {
#line (271, 5) - (271, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (272, 5) - (272, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["host"] = "localhost";
#line (273, 5) - (273, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["port"] = 8080L;
#line (274, 5) - (274, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (275, 5) - (275, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["server"] = inner;
#line (276, 5) - (276, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads(toml.Dumps(d));
#line (277, 5) - (286, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["server"])
                {
                    case global::Sharpy.IDict server:
#line (279, 13) - (279, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (280, 13) - (280, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(server["port"], 8080));
                        break;
                    default:
#line (282, 13) - (282, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlThrowsTomlDecodeError()
            {
#line (288, 5) - (291, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
                {
#line (289, 9) - (289, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("invalid = [");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlErrorIsValueError()
            {
#line (293, 5) - (295, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
                {
#line (294, 9) - (294, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("invalid = [");
                }));
#line (295, 5) - (295, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<ValueError>(ex);
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlErrorMessageContainsLineColumn()
            {
#line (299, 5) - (301, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
                {
#line (300, 9) - (300, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("valid = 1\ninvalid = [");
                }));
#line (301, 5) - (301, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("line", global::Sharpy.Builtins.Str(ex));
#line (302, 5) - (302, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("column", global::Sharpy.Builtins.Str(ex));
#line (303, 5) - (303, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(ex.Msg) > 0);
#line (304, 5) - (304, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("invalid", ex.Doc);
            }

            [Xunit.FactAttribute]
            public void TestLoadFileRoundtrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (310, 5) - (310, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var path = tmpPath + "/config.toml";
#line (311, 5) - (313, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (312, 9) - (312, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    fp.Write("name = \"test\"\ncount = 42");
                }

#line (313, 5) - (313, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.LoadFile(path);
#line (314, 5) - (314, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (315, 5) - (315, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
            }

            [Xunit.FactAttribute]
            public void TestDumpFileRoundtrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (319, 5) - (319, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var path = tmpPath + "/out.toml";
#line (320, 5) - (320, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (321, 5) - (321, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (322, 5) - (322, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (323, 5) - (323, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                toml.DumpFile(d, path);
#line (324, 5) - (324, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.LoadFile(path);
#line (325, 5) - (325, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (326, 5) - (326, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
            }

            [Xunit.FactAttribute]
            public void TestLoadFileNonexistentThrowsFileNotFoundError()
            {
#line (330, 5) - (336, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
                {
#line (331, 9) - (331, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.LoadFile("/nonexistent/path/file.toml");
                }));
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}

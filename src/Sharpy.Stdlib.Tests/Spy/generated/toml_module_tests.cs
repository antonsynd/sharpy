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
using datetime = global::Sharpy.Datetime;
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
#line (377, 5) - (377, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                return @operator.Eq(value, expected);
#line hidden
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
#line (22, 5) - (22, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("key = \"hello\"");
#line (23, 5) - (23, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["key"], "hello"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntegerReturnsLong()
            {
#line (27, 5) - (27, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("count = 42");
#line (28, 5) - (28, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeIntegerReturnsLong()
            {
#line (32, 5) - (32, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = -10");
#line (33, 5) - (33, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], -10));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFloatReturnsDouble()
            {
#line (37, 5) - (37, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("pi = 3.14");
#line (38, 5) - (38, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["pi"], 3.14d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsBooleanReturnsBool()
            {
#line (42, 5) - (42, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("flag = true");
#line (43, 5) - (43, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["flag"], true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFalseBooleanReturnsBool()
            {
#line (47, 5) - (47, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("flag = false");
#line (48, 5) - (48, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["flag"], false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsArrayReturnsList()
            {
#line (54, 5) - (54, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("arr = [1, 2, 3]");
#line (55, 5) - (64, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["arr"])
#line hidden
                {
                    case global::Sharpy.IList l:
#line (57, 13) - (57, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(l));
#line (58, 13) - (58, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[0], 1));
#line (59, 13) - (59, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[1], 2));
#line (60, 13) - (60, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(l[2], 3));
#line hidden
                        break;
                    default:
#line (62, 13) - (62, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsTableReturnsDict()
            {
#line (66, 5) - (66, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[server]\nhost = \"localhost\"\nport = 8080");
#line (67, 5) - (74, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["server"])
#line hidden
                {
                    case global::Sharpy.IDict server:
#line (69, 13) - (69, 61) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (70, 13) - (70, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(server["port"], 8080));
#line hidden
                        break;
                    default:
#line (72, 13) - (72, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedTablesReturnsNestedDicts()
            {
#line (76, 5) - (76, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[a]\n[a.b]\n[a.b.c]\nval = 1");
#line (77, 5) - (91, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["a"])
#line hidden
                {
                    case global::Sharpy.IDict a:
#line (79, 13) - (88, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (a["b"])
#line hidden
                        {
                            case global::Sharpy.IDict b:
#line (81, 21) - (86, 1) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                switch (b["c"])
#line hidden
                                {
                                    case global::Sharpy.IDict c:
#line (83, 29) - (83, 57) 40 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                        Xunit.Assert.True(EqLong(c["val"], 1));
#line hidden
                                        break;
                                    default:
#line (85, 29) - (85, 42) 40 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                        Xunit.Assert.True(false);
#line hidden
                                        break;
                                }

                                break;
                            default:
#line (87, 21) - (87, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (89, 13) - (89, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsDottedKeys()
            {
#line (93, 5) - (93, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("a.b.c = 42");
#line (94, 5) - (104, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["a"])
#line hidden
                {
                    case global::Sharpy.IDict a:
#line (96, 13) - (101, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (a["b"])
#line hidden
                        {
                            case global::Sharpy.IDict b:
#line (98, 21) - (98, 48) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(EqLong(b["c"], 42));
#line hidden
                                break;
                            default:
#line (100, 21) - (100, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (102, 13) - (102, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsInlineTable()
            {
#line (106, 5) - (106, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("point = {x = 1, y = 2}");
#line (107, 5) - (114, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["point"])
#line hidden
                {
                    case global::Sharpy.IDict point:
#line (109, 13) - (109, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(point["x"], 1));
#line (110, 13) - (110, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(point["y"], 2));
#line hidden
                        break;
                    default:
#line (112, 13) - (112, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsArrayOfTables()
            {
#line (116, 5) - (116, 92) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("[[products]]\nname = \"Hammer\"\n\n[[products]]\nname = \"Nail\"");
#line (117, 5) - (135, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["products"])
#line hidden
                {
                    case global::Sharpy.IList products:
#line (119, 13) - (119, 39) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(products));
#line (120, 13) - (125, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (products[0])
#line hidden
                        {
                            case global::Sharpy.IDict p0:
#line (122, 21) - (122, 62) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(p0["name"], "Hammer"));
#line hidden
                                break;
                            default:
#line (124, 21) - (124, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

#line (125, 13) - (130, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        switch (products[1])
#line hidden
                        {
                            case global::Sharpy.IDict p1:
#line (127, 21) - (127, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(p1["name"], "Nail"));
#line hidden
                                break;
                            default:
#line (129, 21) - (129, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (131, 13) - (131, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMultilineBasicString()
            {
#line (137, 5) - (137, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("s = \"\"\"\nhello\nworld\"\"\"");
#line (138, 5) - (145, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["s"])
#line hidden
                {
                    case string s:
#line (140, 13) - (140, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Contains("hello", s);
#line (141, 13) - (141, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.Contains("world", s);
#line hidden
                        break;
                    default:
#line (143, 13) - (143, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsLiteralString()
            {
#line (147, 5) - (147, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("path = 'C:\\Users\\foo'");
#line (148, 5) - (148, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["path"], "C:\\Users\\foo"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsHexInteger()
            {
#line (154, 5) - (154, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0xff");
#line (155, 5) - (155, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 255));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsOctalInteger()
            {
#line (159, 5) - (159, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0o77");
#line (160, 5) - (160, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 63));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsBinaryInteger()
            {
#line (164, 5) - (164, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 0b1010");
#line (165, 5) - (165, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 10));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnderscoreInteger()
            {
#line (169, 5) - (169, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = 1_000_000");
#line (170, 5) - (170, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["val"], 1000000));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsPositiveInfinity()
            {
#line (176, 5) - (176, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = inf");
#line (177, 5) - (177, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], math.Inf));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeInfinity()
            {
#line (181, 5) - (181, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = -inf");
#line (182, 5) - (182, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], -math.Inf));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNan()
            {
#line (186, 5) - (186, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("val = nan");
#line (188, 5) - (188, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["val"], math.Nan));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyDocumentReturnsEmptyDict()
            {
#line (194, 5) - (194, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("");
#line (195, 5) - (195, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsCommentOnlyReturnsEmptyDict()
            {
#line (199, 5) - (199, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("# just a comment");
#line (200, 5) - (200, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsOffsetDatetimeReturnsDatetime()
            {
#line (206, 5) - (206, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("dt = 2024-01-15T10:30:00Z");
#line (207, 5) - (207, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var value = result["dt"];
#line (208, 5) - (208, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.DateTime>(value);
#line (209, 5) - (216, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                if (value is global::Sharpy.DateTime)
#line hidden
                {
#line (210, 9) - (210, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(2024, ((global::Sharpy.DateTime)value!).Year);
#line (211, 9) - (211, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(1, ((global::Sharpy.DateTime)value!).Month);
#line (212, 9) - (212, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(15, ((global::Sharpy.DateTime)value!).Day);
#line (213, 9) - (213, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(10, ((global::Sharpy.DateTime)value!).Hour);
#line (214, 9) - (214, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(30, ((global::Sharpy.DateTime)value!).Minute);
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsLocalDatetimeReturnsDatetime()
            {
#line (218, 5) - (218, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("dt = 2024-01-15T10:30:00");
#line (219, 5) - (219, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var value = result["dt"];
#line (220, 5) - (220, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.DateTime>(value);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsLocalDateReturnsDate()
            {
#line (224, 5) - (224, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("d = 2024-01-15");
#line (225, 5) - (225, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var value = result["d"];
#line (226, 5) - (226, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.Date>(value);
#line (227, 5) - (232, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                if (value is global::Sharpy.Date)
#line hidden
                {
#line (228, 9) - (228, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(2024, ((global::Sharpy.Date)value!).Year);
#line (229, 9) - (229, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(1, ((global::Sharpy.Date)value!).Month);
#line (230, 9) - (230, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(15, ((global::Sharpy.Date)value!).Day);
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsLocalTimeReturnsTime()
            {
#line (234, 5) - (234, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads("t = 10:30:00");
#line (235, 5) - (235, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var value = result["t"];
#line (236, 5) - (236, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.Time>(value);
#line (237, 5) - (243, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                if (value is global::Sharpy.Time)
#line hidden
                {
#line (238, 9) - (238, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(10, ((global::Sharpy.Time)value!).Hour);
#line (239, 9) - (239, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    Xunit.Assert.Equal(30, ((global::Sharpy.Time)value!).Minute);
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsSimpleDictReturnsToml()
            {
#line (245, 5) - (245, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (246, 5) - (246, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (247, 5) - (247, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (248, 5) - (248, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (249, 5) - (249, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("name = \"test\"", result);
#line (250, 5) - (250, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("count = 42", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictReturnsToml()
            {
#line (254, 5) - (254, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (255, 5) - (255, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["key"] = "val";
#line (256, 5) - (256, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (257, 5) - (257, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["section"] = inner;
#line (258, 5) - (258, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (259, 5) - (259, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("[section]", result);
#line (260, 5) - (260, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("key = \"val\"", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithArrayReturnsToml()
            {
#line (264, 5) - (264, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (265, 5) - (265, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                items.Append("a");
#line (266, 5) - (266, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                items.Append("b");
#line (267, 5) - (267, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (268, 5) - (268, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["tags"] = items;
#line (269, 5) - (269, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d);
#line (270, 5) - (270, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("tags", result);
#line (271, 5) - (271, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("\"a\"", result);
#line (272, 5) - (272, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("\"b\"", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsSortKeysSortsAlphabetically()
            {
#line (276, 5) - (276, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (277, 5) - (277, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["zebra"] = 1L;
#line (278, 5) - (278, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["alpha"] = 2L;
#line (279, 5) - (279, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                string result = toml.Dumps(d, sortKeys: true);
#line (280, 5) - (280, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Index(result, "alpha") < global::Sharpy.StringExtensions.Index(result, "zebra"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonDictThrowsTypeError()
            {
#line (284, 5) - (287, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
#line hidden
                {
#line (285, 9) - (285, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Dumps("not a dict");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullThrowsTypeError()
            {
#line (289, 5) - (294, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
#line hidden
                {
#line (290, 9) - (290, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Dumps(null);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestRoundtripSimpleDict()
            {
#line (296, 5) - (296, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (297, 5) - (297, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (298, 5) - (298, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (299, 5) - (299, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["active"] = true;
#line (300, 5) - (300, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["ratio"] = 3.14d;
#line (301, 5) - (301, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads(toml.Dumps(d));
#line (302, 5) - (302, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (303, 5) - (303, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
#line (304, 5) - (304, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["active"], true));
#line (305, 5) - (305, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["ratio"], 3.14d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripNestedDict()
            {
#line (309, 5) - (309, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (310, 5) - (310, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["host"] = "localhost";
#line (311, 5) - (311, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                inner["port"] = 8080L;
#line (312, 5) - (312, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (313, 5) - (313, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["server"] = inner;
#line (314, 5) - (314, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.Loads(toml.Dumps(d));
#line (315, 5) - (324, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                switch (result["server"])
#line hidden
                {
                    case global::Sharpy.IDict server:
#line (317, 13) - (317, 61) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (318, 13) - (318, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(EqLong(server["port"], 8080));
#line hidden
                        break;
                    default:
#line (320, 13) - (320, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlThrowsTomlDecodeError()
            {
#line (326, 5) - (329, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (327, 9) - (327, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("invalid = [");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlErrorIsValueError()
            {
#line (331, 5) - (333, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (332, 9) - (332, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("invalid = [");
#line hidden
                }));
#line (333, 5) - (333, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<ValueError>(ex);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsMalformedTomlErrorMessageContainsLineColumn()
            {
#line (337, 5) - (339, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.TOMLDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (338, 9) - (338, 45) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.Loads("valid = 1\ninvalid = [");
#line hidden
                }));
#line (339, 5) - (339, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("line", global::Sharpy.Builtins.Str(ex));
#line (340, 5) - (340, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("column", global::Sharpy.Builtins.Str(ex));
#line (341, 5) - (341, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(ex.Msg) > 0);
#line (342, 5) - (342, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Contains("invalid", ex.Doc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadFileRoundtrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (348, 5) - (348, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var path = tmpPath + "/config.toml";
#line (349, 5) - (351, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (350, 9) - (350, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    fp.Write("name = \"test\"\ncount = 42");
#line hidden
                }

#line (351, 5) - (351, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.LoadFile(path);
#line (352, 5) - (352, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (353, 5) - (353, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpFileRoundtrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (357, 5) - (357, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var path = tmpPath + "/out.toml";
#line (358, 5) - (358, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (359, 5) - (359, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["name"] = "test";
#line (360, 5) - (360, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                d["count"] = 42L;
#line (361, 5) - (361, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                toml.DumpFile(d, path);
#line (362, 5) - (362, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                var result = toml.LoadFile(path);
#line (363, 5) - (363, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (364, 5) - (364, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.True(EqLong(result["count"], 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadFileNonexistentThrowsFileNotFoundError()
            {
#line (368, 5) - (374, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                Xunit.Assert.Throws<FileNotFoundError>((global::System.Action)(() =>
#line hidden
                {
#line (369, 9) - (369, 54) 20 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_module_tests.spy"
                    toml.LoadFile("/nonexistent/path/file.toml");
#line hidden
                }));
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
#line default

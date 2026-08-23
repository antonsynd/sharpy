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
using json = global::Sharpy.Json;
using math = global::Sharpy.MathModule;
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.JSON.JsonModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class JSON
    {
        [global::Sharpy.SharpyModule("json.json_module_tests")]
        public static partial class JsonModuleTests
        {
            public class Unserializable
            {
                public int Marker = 0;
            }

            public class Stamp
            {
                public int Year = 2026;
                public int Month = 1;
                public int Day = 15;
            }

            public static object StampToString(object obj)
            {
#line (875, 5) - (877, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
#line hidden
                {
#line (876, 9) - (876, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return "2026-01-15";
#line hidden
                }

#line (877, 5) - (877, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
#line hidden
            }

            public static object StampToDict(object obj)
            {
#line (881, 5) - (887, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
#line hidden
                {
#line (882, 9) - (882, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                    {
                    };
#line (883, 9) - (883, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["year"] = ((Stamp)obj!).Year;
#line (884, 9) - (884, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["month"] = ((Stamp)obj!).Month;
#line (885, 9) - (885, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["day"] = ((Stamp)obj!).Day;
#line (886, 9) - (886, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return d;
#line hidden
                }

#line (887, 5) - (887, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
#line hidden
            }

            public static object FallbackCallback(object obj)
            {
#line (891, 5) - (891, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return "fallback";
#line hidden
            }

            public static object IdentityCallback(object obj)
            {
#line (895, 5) - (895, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
#line hidden
            }
        }
    }

    public static partial class JSON
    {
        public partial class JsonModuleTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestDumpsNullReturnsNullString()
            {
#line (16, 5) - (16, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("null", json.Dumps(null));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsTrueReturnsTrueString()
            {
#line (20, 5) - (20, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("true", json.Dumps(true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsFalseReturnsFalseString()
            {
#line (24, 5) - (24, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("false", json.Dumps(false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsIntReturnsNumberString()
            {
#line (28, 5) - (28, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("42", json.Dumps(42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNegativeIntReturnsNumberString()
            {
#line (32, 5) - (32, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("-1", json.Dumps(-1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsLongReturnsNumberString()
            {
#line (36, 5) - (36, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                long value = 9999999999L;
#line (37, 5) - (37, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("9999999999", json.Dumps(value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDoubleReturnsNumberString()
            {
#line (41, 5) - (41, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("3.14", json.Dumps(3.14d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDoubleZeroReturnsNumberWithDecimal()
            {
#line (45, 5) - (45, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("0.0", json.Dumps(0.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringReturnsQuotedString()
            {
#line (49, 5) - (49, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"hello\"", json.Dumps("hello"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyStringReturnsEmptyQuotes()
            {
#line (53, 5) - (53, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"\"", json.Dumps(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithQuotesEscapesQuotes()
            {
#line (59, 5) - (59, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"say \\\"hi\\\"\"", json.Dumps("say \"hi\""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithBackslashEscapesBackslash()
            {
#line (63, 5) - (63, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"a\\\\b\"", json.Dumps("a\\b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithNewlineEscapesNewline()
            {
#line (67, 5) - (67, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"line1\\nline2\"", json.Dumps("line1\nline2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithTabEscapesTab()
            {
#line (71, 5) - (71, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"a\\tb\"", json.Dumps("a\tb"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithUnicodeEscapesNonAscii()
            {
#line (76, 5) - (76, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"caf\\u00e9\"", json.Dumps("café"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithUnicodeEnsureAsciiFalsePreservesUnicode()
            {
#line (80, 5) - (80, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"café\"", json.Dumps("café", ensureAscii: false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyDictReturnsEmptyObject()
            {
#line (86, 5) - (86, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (87, 5) - (87, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictReturnsObject()
            {
#line (91, 5) - (91, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (92, 5) - (92, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (93, 5) - (93, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringIntUsesInterfaceDispatch()
            {
#line (97, 5) - (97, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (98, 5) - (98, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", json.Dumps(d, sortKeys: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyListReturnsEmptyArray()
            {
#line (102, 5) - (102, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (103, 5) - (103, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListReturnsArray()
            {
#line (107, 5) - (107, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (108, 5) - (108, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (109, 5) - (109, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append("two");
#line (110, 5) - (110, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(true);
#line (111, 5) - (111, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1, \"two\", true]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedStructureSerializes()
            {
#line (115, 5) - (115, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (116, 5) - (116, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (117, 5) - (117, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (118, 5) - (118, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(inner);
#line (119, 5) - (119, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (120, 5) - (120, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["items"] = items;
#line (121, 5) - (121, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"items\": [{\"x\": 1}]}", json.Dumps(outer));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithIndentPrettyPrints()
            {
#line (127, 5) - (127, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (128, 5) - (128, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (129, 5) - (129, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (130, 5) - (130, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithSortKeysSortsKeys()
            {
#line (134, 5) - (134, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (135, 5) - (135, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["c"] = 3;
#line (136, 5) - (136, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (137, 5) - (137, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (138, 5) - (138, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2, \"c\": 3}", json.Dumps(d, sortKeys: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsIndentAndSortKeysCombined()
            {
#line (142, 5) - (142, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (143, 5) - (143, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["z"] = 26;
#line (144, 5) - (144, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (145, 5) - (145, 92) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n    \"a\": 1,\n    \"z\": 26\n}", json.Dumps(d, indent: 4, sortKeys: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedIndentIndentsCorrectly()
            {
#line (149, 5) - (149, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (150, 5) - (150, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (151, 5) - (151, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (152, 5) - (152, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["inner"] = inner;
#line (153, 5) - (153, 85) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"inner\": {\n    \"x\": 1\n  }\n}", json.Dumps(outer, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsInfinityEmitsCpythonToken()
            {
#line (165, 5) - (165, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("Infinity", json.Dumps(math.Inf));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNegativeInfinityEmitsCpythonToken()
            {
#line (169, 5) - (169, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("-Infinity", json.Dumps(-math.Inf));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNanEmitsCpythonToken()
            {
#line (173, 5) - (173, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("NaN", json.Dumps(math.Nan));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonFiniteInsideAListEmitsTokens()
            {
#line (177, 5) - (177, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<double> xs = new Sharpy.List<double>()
#line hidden
                {
                    1.0d,
                    math.Inf,
                    math.Nan
                };
#line (178, 5) - (178, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1.0, Infinity, NaN]", json.Dumps(xs));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsInfinityWithAllowNanFalseThrowsValueError()
            {
#line (182, 5) - (185, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (183, 9) - (183, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Inf, allowNan: false);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestDumpsNanWithAllowNanFalseThrowsValueError()
            {
#line (187, 5) - (190, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (188, 9) - (188, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Nan, allowNan: false);
#line hidden
                }
                catch (ValueError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsAcceptsTheThreeExtendedTokens()
            {
#line (194, 5) - (194, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads("[Infinity, NaN, -Infinity]");
#line (195, 5) - (201, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IList xs:
#line (197, 13) - (197, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(xs));
#line hidden
                        break;
                    default:
#line (199, 13) - (199, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestNonFiniteRoundTripHolds()
            {
#line (203, 5) - (203, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<double> xs = new Sharpy.List<double>()
#line hidden
                {
                    1.0d,
                    math.Inf,
                    -math.Inf
                };
#line (204, 5) - (204, 83) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1.0, Infinity, -Infinity]", json.Dumps(json.Loads(json.Dumps(xs))));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonSerializableTypeThrowsTypeError()
            {
#line (210, 5) - (215, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (211, 9) - (211, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(new Unserializable());
#line hidden
                }
                catch (TypeError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected TypeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsNullReturnsNull()
            {
#line (217, 5) - (217, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("null");
#line (218, 5) - (218, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Null(r);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrueReturnsTrue()
            {
#line (222, 5) - (222, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("true");
#line (223, 5) - (223, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFalseReturnsFalse()
            {
#line (227, 5) - (227, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("false");
#line (228, 5) - (228, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntReturnsInt()
            {
#line (232, 5) - (232, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42");
#line (233, 5) - (233, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (234, 5) - (234, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeIntReturnsInt()
            {
#line (238, 5) - (238, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("-7");
#line (239, 5) - (239, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (240, 5) - (240, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), -7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsLargeIntReturnsLong()
            {
#line (244, 5) - (244, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("9999999999");
#line (245, 5) - (245, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<long>(r);
#line (246, 5) - (246, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                long value = 9999999999L;
#line (247, 5) - (247, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((long)r!), value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFloatReturnsDouble()
            {
#line (251, 5) - (251, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("3.14");
#line (252, 5) - (252, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (253, 5) - (253, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), 3.14d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsScientificReturnsDouble()
            {
#line (257, 5) - (257, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("1.5e10");
#line (258, 5) - (258, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (259, 5) - (259, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), 1.5e10d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsZeroReturnsInt()
            {
#line (263, 5) - (263, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("0");
#line (264, 5) - (264, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (265, 5) - (265, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsStringReturnsString()
            {
#line (269, 5) - (269, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"hello\"");
#line (270, 5) - (270, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "hello"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringReturnsEmptyString()
            {
#line (274, 5) - (274, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"\"");
#line (275, 5) - (275, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, ""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedQuoteParsesCorrectly()
            {
#line (281, 5) - (281, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"say \\\"hi\\\"\"");
#line (282, 5) - (282, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "say \"hi\""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedBackslashParsesCorrectly()
            {
#line (286, 5) - (286, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\\\b\"");
#line (287, 5) - (287, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\\b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedNewlineParsesCorrectly()
            {
#line (291, 5) - (291, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\nb\"");
#line (292, 5) - (292, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\nb"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnicodeEscapeParsesCorrectly()
            {
#line (296, 5) - (296, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"caf\\u00e9\"");
#line (297, 5) - (297, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "café"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsAllEscapesParsesCorrectly()
            {
#line (301, 5) - (301, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\/\""), "/"));
#line (302, 5) - (302, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\b\""), "\b"));
#line (303, 5) - (303, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\f\""), "\f"));
#line (304, 5) - (304, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\r\""), "\r"));
#line (305, 5) - (305, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\t\""), "\t"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectReturnsEmptyDict()
            {
#line (311, 5) - (311, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{}");
#line (312, 5) - (318, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (314, 13) - (314, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (316, 13) - (316, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleObjectReturnsDict()
            {
#line (320, 5) - (320, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"b\": \"two\"}");
#line (321, 5) - (328, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (323, 13) - (323, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (324, 13) - (324, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], "two"));
#line hidden
                        break;
                    default:
#line (326, 13) - (326, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedObjectReturnsNestedDict()
            {
#line (330, 5) - (330, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"outer\": {\"inner\": 42}}");
#line (331, 5) - (343, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (333, 13) - (338, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (335, 21) - (335, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (337, 21) - (337, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (339, 13) - (339, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyArrayReturnsEmptyList()
            {
#line (345, 5) - (345, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[]");
#line (346, 5) - (352, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (348, 13) - (348, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(l));
#line hidden
                        break;
                    default:
#line (350, 13) - (350, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleArrayReturnsList()
            {
#line (354, 5) - (354, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, 2, 3]");
#line (355, 5) - (364, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (357, 13) - (357, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(l));
#line (358, 13) - (358, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (359, 13) - (359, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], 2));
#line (360, 13) - (360, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], 3));
#line hidden
                        break;
                    default:
#line (362, 13) - (362, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMixedArrayReturnsList()
            {
#line (366, 5) - (366, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, \"two\", true, null]");
#line (367, 5) - (376, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (369, 13) - (369, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (370, 13) - (370, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], "two"));
#line (371, 13) - (371, 44) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], true));
#line (372, 13) - (372, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(l[3]);
#line hidden
                        break;
                    default:
#line (374, 13) - (374, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedArrayReturnsList()
            {
#line (378, 5) - (378, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[[1, 2], [3, 4]]");
#line (379, 5) - (392, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (381, 13) - (387, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
#line hidden
                        {
                            case global::Sharpy.IList inner1:
#line (383, 21) - (383, 54) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[0], 1));
#line (384, 21) - (384, 54) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[1], 2));
#line hidden
                                break;
                            default:
#line (386, 21) - (386, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (388, 13) - (388, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingWhitespaceParses()
            {
#line (394, 5) - (394, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("  42");
#line (395, 5) - (395, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithTrailingWhitespaceParses()
            {
#line (399, 5) - (399, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42  ");
#line (400, 5) - (400, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsPrettyPrintedJsonParses()
            {
#line (404, 5) - (404, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\n  \"a\": 1,\n  \"b\": [\n    2,\n    3\n  ]\n}");
#line (405, 5) - (413, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (407, 13) - (407, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line hidden
                        break;
                    default:
#line (409, 13) - (409, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringThrowsJsonDecodeError()
            {
#line (415, 5) - (418, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (416, 9) - (416, 23) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsInvalidJsonThrowsJsonDecodeError()
            {
#line (420, 5) - (423, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (421, 9) - (421, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInObjectThrowsJsonDecodeError()
            {
#line (425, 5) - (428, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (426, 9) - (426, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1,}");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInArrayThrowsJsonDecodeError()
            {
#line (430, 5) - (433, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_6 = false;
#line hidden
                try
                {
#line (431, 9) - (431, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2,]");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_6 = true;
                }

                if (!__raised_6)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsExtraDataThrowsJsonDecodeError()
            {
#line (435, 5) - (438, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_7 = false;
#line hidden
                try
                {
#line (436, 9) - (436, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("1 2");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_7 = true;
                }

                if (!__raised_7)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedStringThrowsJsonDecodeError()
            {
#line (440, 5) - (443, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_8 = false;
#line hidden
                try
                {
#line (441, 9) - (441, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("\"unclosed");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_8 = true;
                }

                if (!__raised_8)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedObjectThrowsJsonDecodeError()
            {
#line (445, 5) - (448, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_9 = false;
#line hidden
                try
                {
#line (446, 9) - (446, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_9 = true;
                }

                if (!__raised_9)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedArrayThrowsJsonDecodeError()
            {
#line (450, 5) - (453, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_10 = false;
#line hidden
                try
                {
#line (451, 9) - (451, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_10 = true;
                }

                if (!__raised_10)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorIsValueError()
            {
#line (455, 5) - (457, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                global::Sharpy.JSONDecodeError ex = null!;
#line hidden
                bool __raised_11 = false;
                try
                {
#line (456, 9) - (456, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError __caught_12)
                {
                    __raised_11 = true;
                    ex = __caught_12;
                }

                if (!__raised_11)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
#line (457, 5) - (457, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.ValueError>(ex);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorHasPositionInfo()
            {
#line (461, 5) - (463, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                global::Sharpy.JSONDecodeError ex = null!;
#line hidden
                bool __raised_13 = false;
                try
                {
#line (462, 9) - (462, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError __caught_14)
                {
                    __raised_13 = true;
                    ex = __caught_14;
                }

                if (!__raised_13)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
#line (463, 5) - (463, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("invalid", ex.Doc);
#line (464, 5) - (464, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(0, ex.Pos);
#line (465, 5) - (465, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("line 1", global::Sharpy.Builtins.Str(ex));
#line (466, 5) - (466, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("column 1", global::Sharpy.Builtins.Str(ex));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripDictPreservesData()
            {
#line (472, 5) - (472, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (473, 5) - (473, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["name"] = "test";
#line (474, 5) - (474, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["value"] = 42;
#line (475, 5) - (475, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["active"] = true;
#line (476, 5) - (476, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nothing"] = null;
#line (477, 5) - (477, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(d));
#line (478, 5) - (487, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IDict result:
#line (480, 13) - (480, 56) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (481, 13) - (481, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["value"], 42));
#line (482, 13) - (482, 56) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["active"], true));
#line (483, 13) - (483, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result["nothing"]);
#line hidden
                        break;
                    default:
#line (485, 13) - (485, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripListPreservesData()
            {
#line (489, 5) - (489, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (490, 5) - (490, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (491, 5) - (491, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append("two");
#line (492, 5) - (492, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(3.0d);
#line (493, 5) - (493, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(false);
#line (494, 5) - (494, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (495, 5) - (495, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(l));
#line (496, 5) - (506, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IList result:
#line (498, 13) - (498, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[0], 1));
#line (499, 13) - (499, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[1], "two"));
#line (500, 13) - (500, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[2], 3.0d));
#line (501, 13) - (501, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[3], false));
#line (502, 13) - (502, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result[4]);
#line hidden
                        break;
                    default:
#line (504, 13) - (504, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNestedComplexPreservesData()
            {
#line (508, 5) - (508, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (509, 5) - (509, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["id"] = 1;
#line (510, 5) - (510, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["name"] = "alpha";
#line (511, 5) - (511, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (512, 5) - (512, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["id"] = 2;
#line (513, 5) - (513, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["name"] = "beta";
#line (514, 5) - (514, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (515, 5) - (515, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item1);
#line (516, 5) - (516, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item2);
#line (517, 5) - (517, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> root = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (518, 5) - (518, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["items"] = items;
#line (519, 5) - (519, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["count"] = 2;
#line (520, 5) - (520, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(root));
#line (521, 5) - (537, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IDict result:
#line (523, 13) - (523, 52) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["count"], 2));
#line (524, 13) - (534, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (result["items"])
#line hidden
                        {
                            case global::Sharpy.IList resultItems:
#line (526, 21) - (532, 1) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                switch (resultItems[0])
#line hidden
                                {
                                    case global::Sharpy.IDict first:
#line (528, 29) - (528, 64) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (529, 29) - (529, 72) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                        break;
                                    default:
#line (531, 29) - (531, 42) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(false);
#line hidden
                                        break;
                                }

                                break;
                            default:
#line (533, 21) - (533, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (535, 13) - (535, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripStringWithEscapesPreservesData()
            {
#line (539, 5) - (539, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "line1\nline2\ttab \"quoted\" back\\slash";
#line (540, 5) - (540, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (541, 5) - (541, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripUnicodeStringPreservesData()
            {
#line (545, 5) - (545, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "café üñîçöðé";
#line (546, 5) - (546, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (547, 5) - (547, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsDeeplyNestedHandlesRecursion()
            {
#line (554, 5) - (554, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var s = global::Sharpy.StringHelpers.Repeat("[", 20) + "1" + global::Sharpy.StringHelpers.Repeat("]", 20);
#line (555, 5) - (555, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object current = json.Loads(s);
#line (556, 5) - (556, 10) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var i = 0;
#line (557, 5) - (564, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                while (i < 20)
#line hidden
                {
#line (558, 9) - (563, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (current)
#line hidden
                    {
                        case global::Sharpy.IList l:
#line (560, 17) - (560, 31) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            current = l[0];
#line hidden
                            break;
                        default:
#line (562, 17) - (562, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }

#line (563, 9) - (563, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    i = i + 1;
#line hidden
                }

#line (564, 5) - (564, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(current, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectWithDuplicateKeysLastWins()
            {
#line (568, 5) - (568, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"a\": 2}");
#line (569, 5) - (575, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (571, 13) - (571, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 2));
#line hidden
                        break;
                    default:
#line (573, 13) - (573, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectAndArrayInArray()
            {
#line (577, 5) - (577, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[{}, []]");
#line (578, 5) - (593, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (580, 13) - (585, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
#line hidden
                        {
                            case global::Sharpy.IDict _:
#line (582, 21) - (582, 26) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
#line hidden
                                break;
                            default:
#line (584, 21) - (584, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

#line (585, 13) - (590, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[1])
#line hidden
                        {
                            case global::Sharpy.IList _:
#line (587, 21) - (587, 26) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
#line hidden
                                break;
                            default:
#line (589, 21) - (589, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (591, 13) - (591, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullValueInDictSerializesAsNull()
            {
#line (595, 5) - (595, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (596, 5) - (596, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["key"] = null;
#line (597, 5) - (597, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": null}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullInListSerializesAsNull()
            {
#line (601, 5) - (601, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (602, 5) - (602, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (603, 5) - (603, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[null]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyReturnsJsonObject()
            {
#line (609, 5) - (609, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (610, 5) - (610, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (611, 5) - (611, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (612, 5) - (612, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyIntReturnsJsonObject()
            {
#line (616, 5) - (616, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        10
                    },
                    {
                        "y",
                        20
                    }
                };
#line (617, 5) - (617, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"x\": 10, \"y\": 20}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictStringKeyReturnsNestedObject()
            {
#line (621, 5) - (621, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> inner = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        1
                    }
                };
#line (622, 5) - (622, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (623, 5) - (623, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["inner"] = inner;
#line (624, 5) - (624, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"inner\": {\"x\": 1}}", json.Dumps(outer));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithSortKeysSortsKeys()
            {
#line (628, 5) - (628, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (629, 5) - (629, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["c"] = 3;
#line (630, 5) - (630, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (631, 5) - (631, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (632, 5) - (632, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2, \"c\": 3}", json.Dumps(d, sortKeys: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithIndentPrettyPrints()
            {
#line (636, 5) - (636, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (637, 5) - (637, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (638, 5) - (638, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (639, 5) - (639, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyNestedDictStringKeyDictReturnsNestedObjects()
            {
#line (643, 5) - (643, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> leaf = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "val",
                        42
                    }
                };
#line (644, 5) - (644, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, Sharpy.Dict<string, int>> inner = new Sharpy.Dict<string, Sharpy.Dict<string, int>>()
#line hidden
                {
                };
#line (645, 5) - (645, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["mid"] = leaf;
#line (646, 5) - (646, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"mid\": {\"val\": 42}}", json.Dumps(inner));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntReturnsArray()
            {
#line (652, 5) - (652, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (653, 5) - (653, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1, 2, 3]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyListOfIntReturnsEmptyArray()
            {
#line (657, 5) - (657, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                };
#line (658, 5) - (658, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfStringReturnsArray()
            {
#line (662, 5) - (662, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Set<string> s = new Sharpy.Set<string>()
#line hidden
                {
                    "hello"
                };
#line (663, 5) - (663, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"hello\"]", json.Dumps(s));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfDoubleReturnsArray()
            {
#line (667, 5) - (667, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<double> l = new Sharpy.List<double>()
#line hidden
                {
                    1.5d,
                    2.5d
                };
#line (668, 5) - (668, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1.5, 2.5]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfBoolReturnsArray()
            {
#line (672, 5) - (672, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<bool> l = new Sharpy.List<bool>()
#line hidden
                {
                    true,
                    false
                };
#line (673, 5) - (673, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[true, false]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListOfIntInDictSerializes()
            {
#line (677, 5) - (677, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20
                };
#line (678, 5) - (678, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (679, 5) - (679, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nums"] = inner;
#line (680, 5) - (680, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"nums\": [10, 20]}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntWithIndentPrettyPrints()
            {
#line (684, 5) - (684, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (685, 5) - (685, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2\n]", json.Dumps(l, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (691, 5) - (691, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (692, 5) - (692, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (693, 5) - (693, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (694, 5) - (694, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCustomSeparatorsUsesGivenStrings()
            {
#line (698, 5) - (698, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (699, 5) - (699, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (700, 5) - (700, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (701, 5) - (701, 82) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\" = 1 ; \"b\" = 2}", json.Dumps(d, separators: (" ; ", " = ")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithCompactSeparatorsOmitsWhitespace()
            {
#line (705, 5) - (705, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (706, 5) - (706, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1,2,3]", json.Dumps(l, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullSeparatorsProducesDefaultOutput()
            {
#line (710, 5) - (710, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (711, 5) - (711, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (712, 5) - (712, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (713, 5) - (713, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var explicitNull = json.Dumps(d, separators: null);
#line (714, 5) - (714, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var defaultCall = json.Dumps(d);
#line (715, 5) - (715, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(defaultCall, explicitNull);
#line (716, 5) - (716, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", explicitNull);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsSeparatorsWithIndentUsesNewlineForStructureAndKeySeparator()
            {
#line (720, 5) - (720, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (721, 5) - (721, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (722, 5) - (722, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (723, 5) - (723, 95) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2, separators: (",", ": ")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictWithCompactSeparatorsAppliesRecursively()
            {
#line (727, 5) - (727, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (728, 5) - (728, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (729, 5) - (729, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["y"] = 2;
#line (730, 5) - (730, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (731, 5) - (731, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["point"] = inner;
#line (732, 5) - (732, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["count"] = 3;
#line (733, 5) - (733, 100) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"point\":{\"x\":1,\"y\":2},\"count\":3}", json.Dumps(outer, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithCompactSeparatorsAppliesRecursively()
            {
#line (737, 5) - (737, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner1 = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (738, 5) - (738, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner2 = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    4
                };
#line (739, 5) - (739, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> outer = new Sharpy.List<object>()
#line hidden
                {
                };
#line (740, 5) - (740, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner1);
#line (741, 5) - (741, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner2);
#line (742, 5) - (742, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[[1,2],[3,4]]", json.Dumps(outer, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStrKeyDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (746, 5) - (746, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (747, 5) - (747, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToString()
            {
#line (755, 5) - (755, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (756, 5) - (756, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"2026-01-15\"", json.Dumps(stamp, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToDict()
            {
#line (760, 5) - (760, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (761, 5) - (761, 102) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"year\": 2026, \"month\": 1, \"day\": 15}", json.Dumps(stamp, @default: StampToDict!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNull()
            {
#line (766, 5) - (766, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("null", json.Dumps(null, @default: FallbackCallback!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNativelySerializableTypes()
            {
#line (770, 5) - (770, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("42", json.Dumps(42, @default: FallbackCallback!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackReturningSameObjectRaisesTypeError()
            {
#line (774, 5) - (774, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (775, 5) - (778, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_15 = false;
#line hidden
                try
                {
#line (776, 9) - (776, 53) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp, @default: IdentityCallback!);
#line hidden
                }
                catch (TypeError)
                {
                    __raised_15 = true;
                }

                if (!__raised_15)
                    throw new global::Sharpy.AssertionError("Expected TypeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestDumpsNoDefaultCallbackNonSerializableTypeRaisesTypeError()
            {
#line (780, 5) - (780, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (781, 5) - (784, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                bool __raised_16 = false;
#line hidden
                try
                {
#line (782, 9) - (782, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp);
#line hidden
                }
                catch (TypeError)
                {
                    __raised_16 = true;
                }

                if (!__raised_16)
                    throw new global::Sharpy.AssertionError("Expected TypeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInDictIsInvokedForValue()
            {
#line (786, 5) - (786, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (787, 5) - (787, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (788, 5) - (788, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (789, 5) - (789, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (790, 5) - (790, 97) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\": \"2026-01-15\", \"count\": 5}", json.Dumps(d, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInListIsInvokedForElement()
            {
#line (794, 5) - (794, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (795, 5) - (795, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (796, 5) - (796, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(stamp);
#line (797, 5) - (797, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (798, 5) - (798, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"2026-01-15\", 1]", json.Dumps(l, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultAndSeparatorsCombinedCorrectly()
            {
#line (802, 5) - (802, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (803, 5) - (803, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (804, 5) - (804, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (805, 5) - (805, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (806, 5) - (806, 117) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\":\"2026-01-15\",\"count\":5}", json.Dumps(d, separators: (",", ":"), @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpWritesJsonToFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (813, 5) - (813, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (814, 5) - (814, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (815, 5) - (815, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["key"] = "value";
#line (816, 5) - (818, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (817, 9) - (817, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp);
#line hidden
                }

#line (818, 5) - (818, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                string content = "";
#line (819, 5) - (821, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (820, 9) - (820, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    content = fp2.Read();
#line hidden
                }

#line (821, 5) - (821, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": \"value\"}", content);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadReadsJsonFromFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (825, 5) - (825, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (826, 5) - (828, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (827, 9) - (827, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    fp.Write("{\"key\": \"value\"}");
#line hidden
                }

#line (828, 5) - (836, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (829, 9) - (829, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (830, 9) - (836, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (832, 17) - (832, 55) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                            break;
                        default:
#line (834, 17) - (834, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpLoadRoundTripThroughFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (838, 5) - (838, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (839, 5) - (839, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (840, 5) - (840, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["name"] = "test";
#line (841, 5) - (841, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> values = new Sharpy.List<object>()
#line hidden
                {
                };
#line (842, 5) - (842, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(1);
#line (843, 5) - (843, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(2);
#line (844, 5) - (844, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(3);
#line (845, 5) - (845, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["values"] = values;
#line (846, 5) - (848, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (847, 9) - (847, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp, indent: 2);
#line hidden
                }

#line (848, 5) - (864, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (849, 9) - (849, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (850, 9) - (864, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (852, 17) - (852, 55) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["name"], "test"));
#line (853, 17) - (858, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            switch (d["values"])
#line hidden
                            {
                                case global::Sharpy.IList vals:
#line (855, 25) - (855, 47) 36 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(vals));
#line hidden
                                    break;
                                default:
#line (857, 25) - (857, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (859, 17) - (859, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsFloat32UsesSinglePrecisionDigits()
            {
#line (913, 5) - (913, 13) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var a = 0.1f;
#line (914, 5) - (914, 13) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var b = 1.1f;
#line (915, 5) - (915, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var c = 3.14159f;
#line (916, 5) - (916, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("0.1", json.Dumps(a));
#line (917, 5) - (917, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("1.1", json.Dumps(b));
#line (918, 5) - (918, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("3.14159", json.Dumps(c));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsFloat32AgreesWithStr()
            {
#line (924, 5) - (924, 13) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var a = 0.1f;
#line (925, 5) - (925, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var c = 3.14159f;
#line (926, 5) - (926, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Str(a), json.Dumps(a));
#line (927, 5) - (927, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Str(c), json.Dumps(c));
#line hidden
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
#line default

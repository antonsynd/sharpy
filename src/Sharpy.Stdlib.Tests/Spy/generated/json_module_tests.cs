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
#line (834, 5) - (836, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
                {
#line (835, 9) - (835, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return "2026-01-15";
                }

#line (836, 5) - (836, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
            }

            public static object StampToDict(object obj)
            {
#line (840, 5) - (846, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
                {
#line (841, 9) - (841, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                    {
                    };
#line (842, 9) - (842, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["year"] = ((Stamp)obj).Year;
#line (843, 9) - (843, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["month"] = ((Stamp)obj).Month;
#line (844, 9) - (844, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["day"] = ((Stamp)obj).Day;
#line (845, 9) - (845, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return d;
                }

#line (846, 5) - (846, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
            }

            public static object FallbackCallback(object obj)
            {
#line (850, 5) - (850, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return "fallback";
            }

            public static object IdentityCallback(object obj)
            {
#line (854, 5) - (854, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
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
#line (16, 5) - (16, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("null", json.Dumps(null));
            }

            [Xunit.FactAttribute]
            public void TestDumpsTrueReturnsTrueString()
            {
#line (20, 5) - (20, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("true", json.Dumps(true));
            }

            [Xunit.FactAttribute]
            public void TestDumpsFalseReturnsFalseString()
            {
#line (24, 5) - (24, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("false", json.Dumps(false));
            }

            [Xunit.FactAttribute]
            public void TestDumpsIntReturnsNumberString()
            {
#line (28, 5) - (28, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("42", json.Dumps(42));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNegativeIntReturnsNumberString()
            {
#line (32, 5) - (32, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("-1", json.Dumps(-1));
            }

            [Xunit.FactAttribute]
            public void TestDumpsLongReturnsNumberString()
            {
#line (36, 5) - (36, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                long value = 9999999999L;
#line (37, 5) - (37, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("9999999999", json.Dumps(value));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDoubleReturnsNumberString()
            {
#line (41, 5) - (41, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("3.14", json.Dumps(3.14d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDoubleZeroReturnsNumberWithDecimal()
            {
#line (45, 5) - (45, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("0.0", json.Dumps(0.0d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringReturnsQuotedString()
            {
#line (49, 5) - (49, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"hello\"", json.Dumps("hello"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyStringReturnsEmptyQuotes()
            {
#line (53, 5) - (53, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"\"", json.Dumps(""));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithQuotesEscapesQuotes()
            {
#line (59, 5) - (59, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"say \\\"hi\\\"\"", json.Dumps("say \"hi\""));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithBackslashEscapesBackslash()
            {
#line (63, 5) - (63, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"a\\\\b\"", json.Dumps("a\\b"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithNewlineEscapesNewline()
            {
#line (67, 5) - (67, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"line1\\nline2\"", json.Dumps("line1\nline2"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithTabEscapesTab()
            {
#line (71, 5) - (71, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"a\\tb\"", json.Dumps("a\tb"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithUnicodeEscapesNonAscii()
            {
#line (76, 5) - (76, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"caf\\u00e9\"", json.Dumps("café"));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStringWithUnicodeEnsureAsciiFalsePreservesUnicode()
            {
#line (80, 5) - (80, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"café\"", json.Dumps("café", ensureAscii: false));
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyDictReturnsEmptyObject()
            {
#line (86, 5) - (86, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (87, 5) - (87, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictReturnsObject()
            {
#line (91, 5) - (91, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (92, 5) - (92, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (93, 5) - (93, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringIntUsesInterfaceDispatch()
            {
#line (97, 5) - (97, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (98, 5) - (98, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", json.Dumps(d, sortKeys: true));
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyListReturnsEmptyArray()
            {
#line (102, 5) - (102, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (103, 5) - (103, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListReturnsArray()
            {
#line (107, 5) - (107, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (108, 5) - (108, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (109, 5) - (109, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append("two");
#line (110, 5) - (110, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(true);
#line (111, 5) - (111, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1, \"two\", true]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedStructureSerializes()
            {
#line (115, 5) - (115, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (116, 5) - (116, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (117, 5) - (117, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
                {
                };
#line (118, 5) - (118, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(inner);
#line (119, 5) - (119, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (120, 5) - (120, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["items"] = items;
#line (121, 5) - (121, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"items\": [{\"x\": 1}]}", json.Dumps(outer));
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithIndentPrettyPrints()
            {
#line (127, 5) - (127, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (128, 5) - (128, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (129, 5) - (129, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (130, 5) - (130, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2));
            }

            [Xunit.FactAttribute]
            public void TestDumpsWithSortKeysSortsKeys()
            {
#line (134, 5) - (134, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (135, 5) - (135, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["c"] = 3;
#line (136, 5) - (136, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (137, 5) - (137, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (138, 5) - (138, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2, \"c\": 3}", json.Dumps(d, sortKeys: true));
            }

            [Xunit.FactAttribute]
            public void TestDumpsIndentAndSortKeysCombined()
            {
#line (142, 5) - (142, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (143, 5) - (143, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["z"] = 26;
#line (144, 5) - (144, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (145, 5) - (145, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n    \"a\": 1,\n    \"z\": 26\n}", json.Dumps(d, indent: 4, sortKeys: true));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedIndentIndentsCorrectly()
            {
#line (149, 5) - (149, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (150, 5) - (150, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (151, 5) - (151, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (152, 5) - (152, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["inner"] = inner;
#line (153, 5) - (153, 85) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"inner\": {\n    \"x\": 1\n  }\n}", json.Dumps(outer, indent: 2));
            }

            [Xunit.FactAttribute]
            public void TestDumpsInfinityThrowsValueError()
            {
#line (159, 5) - (162, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (160, 9) - (160, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Inf);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNanThrowsValueError()
            {
#line (164, 5) - (167, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (165, 9) - (165, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Nan);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonSerializableTypeThrowsTypeError()
            {
#line (169, 5) - (174, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (170, 9) - (170, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(new Unserializable());
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNullReturnsNull()
            {
#line (176, 5) - (176, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("null");
#line (177, 5) - (177, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Null(r);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrueReturnsTrue()
            {
#line (181, 5) - (181, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("true");
#line (182, 5) - (182, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, true));
            }

            [Xunit.FactAttribute]
            public void TestLoadsFalseReturnsFalse()
            {
#line (186, 5) - (186, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("false");
#line (187, 5) - (187, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, false));
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntReturnsInt()
            {
#line (191, 5) - (191, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42");
#line (192, 5) - (192, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (193, 5) - (193, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeIntReturnsInt()
            {
#line (197, 5) - (197, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("-7");
#line (198, 5) - (198, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (199, 5) - (199, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, -7));
            }

            [Xunit.FactAttribute]
            public void TestLoadsLargeIntReturnsLong()
            {
#line (203, 5) - (203, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("9999999999");
#line (204, 5) - (204, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<long>(r);
#line (205, 5) - (205, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                long value = 9999999999L;
#line (206, 5) - (206, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, value));
            }

            [Xunit.FactAttribute]
            public void TestLoadsFloatReturnsDouble()
            {
#line (210, 5) - (210, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("3.14");
#line (211, 5) - (211, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (212, 5) - (212, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 3.14d));
            }

            [Xunit.FactAttribute]
            public void TestLoadsScientificReturnsDouble()
            {
#line (216, 5) - (216, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("1.5e10");
#line (217, 5) - (217, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (218, 5) - (218, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 1.5e10d));
            }

            [Xunit.FactAttribute]
            public void TestLoadsZeroReturnsInt()
            {
#line (222, 5) - (222, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("0");
#line (223, 5) - (223, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (224, 5) - (224, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 0));
            }

            [Xunit.FactAttribute]
            public void TestLoadsStringReturnsString()
            {
#line (228, 5) - (228, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"hello\"");
#line (229, 5) - (229, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "hello"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringReturnsEmptyString()
            {
#line (233, 5) - (233, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"\"");
#line (234, 5) - (234, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, ""));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedQuoteParsesCorrectly()
            {
#line (240, 5) - (240, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"say \\\"hi\\\"\"");
#line (241, 5) - (241, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "say \"hi\""));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedBackslashParsesCorrectly()
            {
#line (245, 5) - (245, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\\\b\"");
#line (246, 5) - (246, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\\b"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedNewlineParsesCorrectly()
            {
#line (250, 5) - (250, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\nb\"");
#line (251, 5) - (251, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\nb"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnicodeEscapeParsesCorrectly()
            {
#line (255, 5) - (255, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"caf\\u00e9\"");
#line (256, 5) - (256, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "café"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsAllEscapesParsesCorrectly()
            {
#line (260, 5) - (260, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\/\""), "/"));
#line (261, 5) - (261, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\b\""), "\b"));
#line (262, 5) - (262, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\f\""), "\f"));
#line (263, 5) - (263, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\r\""), "\r"));
#line (264, 5) - (264, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\t\""), "\t"));
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectReturnsEmptyDict()
            {
#line (270, 5) - (270, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{}");
#line (271, 5) - (277, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (273, 13) - (273, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
                        break;
                    default:
#line (275, 13) - (275, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleObjectReturnsDict()
            {
#line (279, 5) - (279, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"b\": \"two\"}");
#line (280, 5) - (287, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (282, 13) - (282, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (283, 13) - (283, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], "two"));
                        break;
                    default:
#line (285, 13) - (285, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedObjectReturnsNestedDict()
            {
#line (289, 5) - (289, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"outer\": {\"inner\": 42}}");
#line (290, 5) - (302, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (292, 13) - (297, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (d["outer"])
                        {
                            case global::Sharpy.IDict inner:
#line (294, 21) - (294, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
                                break;
                            default:
#line (296, 21) - (296, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (298, 13) - (298, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyArrayReturnsEmptyList()
            {
#line (304, 5) - (304, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[]");
#line (305, 5) - (311, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IList l:
#line (307, 13) - (307, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(l));
                        break;
                    default:
#line (309, 13) - (309, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleArrayReturnsList()
            {
#line (313, 5) - (313, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, 2, 3]");
#line (314, 5) - (323, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IList l:
#line (316, 13) - (316, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(l));
#line (317, 13) - (317, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (318, 13) - (318, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], 2));
#line (319, 13) - (319, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], 3));
                        break;
                    default:
#line (321, 13) - (321, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMixedArrayReturnsList()
            {
#line (325, 5) - (325, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, \"two\", true, null]");
#line (326, 5) - (335, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IList l:
#line (328, 13) - (328, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (329, 13) - (329, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], "two"));
#line (330, 13) - (330, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], true));
#line (331, 13) - (331, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(l[3]);
                        break;
                    default:
#line (333, 13) - (333, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedArrayReturnsList()
            {
#line (337, 5) - (337, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[[1, 2], [3, 4]]");
#line (338, 5) - (351, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IList l:
#line (340, 13) - (346, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
                        {
                            case global::Sharpy.IList inner1:
#line (342, 21) - (342, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[0], 1));
#line (343, 21) - (343, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[1], 2));
                                break;
                            default:
#line (345, 21) - (345, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (347, 13) - (347, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingWhitespaceParses()
            {
#line (353, 5) - (353, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("  42");
#line (354, 5) - (354, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithTrailingWhitespaceParses()
            {
#line (358, 5) - (358, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42  ");
#line (359, 5) - (359, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
            }

            [Xunit.FactAttribute]
            public void TestLoadsPrettyPrintedJsonParses()
            {
#line (363, 5) - (363, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\n  \"a\": 1,\n  \"b\": [\n    2,\n    3\n  ]\n}");
#line (364, 5) - (372, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (366, 13) - (366, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
                        break;
                    default:
#line (368, 13) - (368, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringThrowsJsonDecodeError()
            {
#line (374, 5) - (377, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (375, 9) - (375, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsInvalidJsonThrowsJsonDecodeError()
            {
#line (379, 5) - (382, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (380, 9) - (380, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInObjectThrowsJsonDecodeError()
            {
#line (384, 5) - (387, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (385, 9) - (385, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1,}");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInArrayThrowsJsonDecodeError()
            {
#line (389, 5) - (392, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (390, 9) - (390, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2,]");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsExtraDataThrowsJsonDecodeError()
            {
#line (394, 5) - (397, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (395, 9) - (395, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("1 2");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedStringThrowsJsonDecodeError()
            {
#line (399, 5) - (402, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (400, 9) - (400, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("\"unclosed");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedObjectThrowsJsonDecodeError()
            {
#line (404, 5) - (407, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (405, 9) - (405, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedArrayThrowsJsonDecodeError()
            {
#line (409, 5) - (412, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (410, 9) - (410, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2");
                }));
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorIsValueError()
            {
#line (414, 5) - (416, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (415, 9) - (415, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
                }));
#line (416, 5) - (416, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<ValueError>(ex);
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorHasPositionInfo()
            {
#line (420, 5) - (422, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (421, 9) - (421, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
                }));
#line (422, 5) - (422, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("invalid", ex.Doc);
#line (423, 5) - (423, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(0, ex.Pos);
#line (424, 5) - (424, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("line 1", global::Sharpy.Builtins.Str(ex));
#line (425, 5) - (425, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("column 1", global::Sharpy.Builtins.Str(ex));
            }

            [Xunit.FactAttribute]
            public void TestRoundTripDictPreservesData()
            {
#line (431, 5) - (431, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (432, 5) - (432, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["name"] = "test";
#line (433, 5) - (433, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["value"] = 42;
#line (434, 5) - (434, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["active"] = true;
#line (435, 5) - (435, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nothing"] = null;
#line (436, 5) - (436, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(d));
#line (437, 5) - (446, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
                {
                    case global::Sharpy.IDict result:
#line (439, 13) - (439, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (440, 13) - (440, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["value"], 42));
#line (441, 13) - (441, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["active"], true));
#line (442, 13) - (442, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result["nothing"]);
                        break;
                    default:
#line (444, 13) - (444, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripListPreservesData()
            {
#line (448, 5) - (448, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (449, 5) - (449, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (450, 5) - (450, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append("two");
#line (451, 5) - (451, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(3.0d);
#line (452, 5) - (452, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(false);
#line (453, 5) - (453, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (454, 5) - (454, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(l));
#line (455, 5) - (465, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
                {
                    case global::Sharpy.IList result:
#line (457, 13) - (457, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[0], 1));
#line (458, 13) - (458, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[1], "two"));
#line (459, 13) - (459, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[2], 3.0d));
#line (460, 13) - (460, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[3], false));
#line (461, 13) - (461, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result[4]);
                        break;
                    default:
#line (463, 13) - (463, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNestedComplexPreservesData()
            {
#line (467, 5) - (467, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item1 = new Sharpy.Dict<string, object>()
                {
                };
#line (468, 5) - (468, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["id"] = 1;
#line (469, 5) - (469, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["name"] = "alpha";
#line (470, 5) - (470, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item2 = new Sharpy.Dict<string, object>()
                {
                };
#line (471, 5) - (471, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["id"] = 2;
#line (472, 5) - (472, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["name"] = "beta";
#line (473, 5) - (473, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
                {
                };
#line (474, 5) - (474, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item1);
#line (475, 5) - (475, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item2);
#line (476, 5) - (476, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> root = new Sharpy.Dict<string, object>()
                {
                };
#line (477, 5) - (477, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["items"] = items;
#line (478, 5) - (478, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["count"] = 2;
#line (479, 5) - (479, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(root));
#line (480, 5) - (496, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
                {
                    case global::Sharpy.IDict result:
#line (482, 13) - (482, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["count"], 2));
#line (483, 13) - (493, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (result["items"])
                        {
                            case global::Sharpy.IList resultItems:
#line (485, 21) - (491, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                switch (resultItems[0])
                                {
                                    case global::Sharpy.IDict first:
#line (487, 29) - (487, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (488, 29) - (488, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
                                        break;
                                    default:
#line (490, 29) - (490, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(false);
                                        break;
                                }

                                break;
                            default:
#line (492, 21) - (492, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (494, 13) - (494, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripStringWithEscapesPreservesData()
            {
#line (498, 5) - (498, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "line1\nline2\ttab \"quoted\" back\\slash";
#line (499, 5) - (499, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (500, 5) - (500, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
            }

            [Xunit.FactAttribute]
            public void TestRoundTripUnicodeStringPreservesData()
            {
#line (504, 5) - (504, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "café üñîçöðé";
#line (505, 5) - (505, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (506, 5) - (506, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
            }

            [Xunit.FactAttribute]
            public void TestLoadsDeeplyNestedHandlesRecursion()
            {
#line (513, 5) - (513, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var s = global::Sharpy.StringHelpers.Repeat("[", 20) + "1" + global::Sharpy.StringHelpers.Repeat("]", 20);
#line (514, 5) - (514, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object current = json.Loads(s);
#line (515, 5) - (515, 10) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var i = 0;
#line (516, 5) - (523, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                while (i < 20)
                {
#line (517, 9) - (522, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (current)
                    {
                        case global::Sharpy.IList l:
#line (519, 17) - (519, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            current = l[0];
                            break;
                        default:
#line (521, 17) - (521, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }

#line (522, 9) - (522, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    i = i + 1;
                }

#line (523, 5) - (523, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(current, 1));
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectWithDuplicateKeysLastWins()
            {
#line (527, 5) - (527, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"a\": 2}");
#line (528, 5) - (534, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (530, 13) - (530, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 2));
                        break;
                    default:
#line (532, 13) - (532, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectAndArrayInArray()
            {
#line (536, 5) - (536, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[{}, []]");
#line (537, 5) - (552, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IList l:
#line (539, 13) - (544, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
                        {
                            case global::Sharpy.IDict _:
#line (541, 21) - (541, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
                                break;
                            default:
#line (543, 21) - (543, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

#line (544, 13) - (549, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[1])
                        {
                            case global::Sharpy.IList _:
#line (546, 21) - (546, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
                                break;
                            default:
#line (548, 21) - (548, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (550, 13) - (550, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullValueInDictSerializesAsNull()
            {
#line (554, 5) - (554, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (555, 5) - (555, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["key"] = null;
#line (556, 5) - (556, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": null}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullInListSerializesAsNull()
            {
#line (560, 5) - (560, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (561, 5) - (561, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (562, 5) - (562, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[null]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyReturnsJsonObject()
            {
#line (568, 5) - (568, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (569, 5) - (569, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (570, 5) - (570, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (571, 5) - (571, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyIntReturnsJsonObject()
            {
#line (575, 5) - (575, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
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
#line (576, 5) - (576, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"x\": 10, \"y\": 20}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictStringKeyReturnsNestedObject()
            {
#line (580, 5) - (580, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> inner = new Sharpy.Dict<string, int>()
                {
                    {
                        "x",
                        1
                    }
                };
#line (581, 5) - (581, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (582, 5) - (582, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["inner"] = inner;
#line (583, 5) - (583, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"inner\": {\"x\": 1}}", json.Dumps(outer));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithSortKeysSortsKeys()
            {
#line (587, 5) - (587, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (588, 5) - (588, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["c"] = 3;
#line (589, 5) - (589, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (590, 5) - (590, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (591, 5) - (591, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2, \"c\": 3}", json.Dumps(d, sortKeys: true));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithIndentPrettyPrints()
            {
#line (595, 5) - (595, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (596, 5) - (596, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (597, 5) - (597, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (598, 5) - (598, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyNestedDictStringKeyDictReturnsNestedObjects()
            {
#line (602, 5) - (602, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> leaf = new Sharpy.Dict<string, int>()
                {
                    {
                        "val",
                        42
                    }
                };
#line (603, 5) - (603, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, Sharpy.Dict<string, int>> inner = new Sharpy.Dict<string, Sharpy.Dict<string, int>>()
                {
                };
#line (604, 5) - (604, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["mid"] = leaf;
#line (605, 5) - (605, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"mid\": {\"val\": 42}}", json.Dumps(inner));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntReturnsArray()
            {
#line (611, 5) - (611, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (612, 5) - (612, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1, 2, 3]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyListOfIntReturnsEmptyArray()
            {
#line (616, 5) - (616, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
                {
                };
#line (617, 5) - (617, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfStringReturnsArray()
            {
#line (621, 5) - (621, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Set<string> s = new Sharpy.Set<string>()
                {
                    "hello"
                };
#line (622, 5) - (622, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"hello\"]", json.Dumps(s));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfDoubleReturnsArray()
            {
#line (626, 5) - (626, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<double> l = new Sharpy.List<double>()
                {
                    1.5d,
                    2.5d
                };
#line (627, 5) - (627, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1.5, 2.5]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfBoolReturnsArray()
            {
#line (631, 5) - (631, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<bool> l = new Sharpy.List<bool>()
                {
                    true,
                    false
                };
#line (632, 5) - (632, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[true, false]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListOfIntInDictSerializes()
            {
#line (636, 5) - (636, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner = new Sharpy.List<int>()
                {
                    10,
                    20
                };
#line (637, 5) - (637, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (638, 5) - (638, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nums"] = inner;
#line (639, 5) - (639, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"nums\": [10, 20]}", json.Dumps(d));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntWithIndentPrettyPrints()
            {
#line (643, 5) - (643, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (644, 5) - (644, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2\n]", json.Dumps(l, indent: 2));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (650, 5) - (650, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (651, 5) - (651, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (652, 5) - (652, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (653, 5) - (653, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCustomSeparatorsUsesGivenStrings()
            {
#line (657, 5) - (657, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (658, 5) - (658, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (659, 5) - (659, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (660, 5) - (660, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\" = 1 ; \"b\" = 2}", json.Dumps(d, separators: (" ; ", " = ")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithCompactSeparatorsOmitsWhitespace()
            {
#line (664, 5) - (664, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (665, 5) - (665, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1,2,3]", json.Dumps(l, separators: (",", ":")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullSeparatorsProducesDefaultOutput()
            {
#line (669, 5) - (669, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (670, 5) - (670, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (671, 5) - (671, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (672, 5) - (672, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var explicitNull = json.Dumps(d, separators: null);
#line (673, 5) - (673, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var defaultCall = json.Dumps(d);
#line (674, 5) - (674, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(defaultCall, explicitNull);
#line (675, 5) - (675, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", explicitNull);
            }

            [Xunit.FactAttribute]
            public void TestDumpsSeparatorsWithIndentUsesNewlineForStructureAndKeySeparator()
            {
#line (679, 5) - (679, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (680, 5) - (680, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (681, 5) - (681, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (682, 5) - (682, 95) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2, separators: (",", ": ")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictWithCompactSeparatorsAppliesRecursively()
            {
#line (686, 5) - (686, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (687, 5) - (687, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (688, 5) - (688, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["y"] = 2;
#line (689, 5) - (689, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (690, 5) - (690, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["point"] = inner;
#line (691, 5) - (691, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["count"] = 3;
#line (692, 5) - (692, 100) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"point\":{\"x\":1,\"y\":2},\"count\":3}", json.Dumps(outer, separators: (",", ":")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithCompactSeparatorsAppliesRecursively()
            {
#line (696, 5) - (696, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner1 = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (697, 5) - (697, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner2 = new Sharpy.List<int>()
                {
                    3,
                    4
                };
#line (698, 5) - (698, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> outer = new Sharpy.List<object>()
                {
                };
#line (699, 5) - (699, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner1);
#line (700, 5) - (700, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner2);
#line (701, 5) - (701, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[[1,2],[3,4]]", json.Dumps(outer, separators: (",", ":")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsStrKeyDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (705, 5) - (705, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (706, 5) - (706, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToString()
            {
#line (714, 5) - (714, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (715, 5) - (715, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"2026-01-15\"", json.Dumps(stamp, @default: StampToString!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToDict()
            {
#line (719, 5) - (719, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (720, 5) - (720, 102) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"year\": 2026, \"month\": 1, \"day\": 15}", json.Dumps(stamp, @default: StampToDict!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNull()
            {
#line (725, 5) - (725, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("null", json.Dumps(null, @default: FallbackCallback!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNativelySerializableTypes()
            {
#line (729, 5) - (729, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("42", json.Dumps(42, @default: FallbackCallback!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackReturningSameObjectRaisesTypeError()
            {
#line (733, 5) - (733, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (734, 5) - (737, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (735, 9) - (735, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp, @default: IdentityCallback!);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNoDefaultCallbackNonSerializableTypeRaisesTypeError()
            {
#line (739, 5) - (739, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (740, 5) - (743, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (741, 9) - (741, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInDictIsInvokedForValue()
            {
#line (745, 5) - (745, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (746, 5) - (746, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (747, 5) - (747, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (748, 5) - (748, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (749, 5) - (749, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\": \"2026-01-15\", \"count\": 5}", json.Dumps(d, @default: StampToString!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInListIsInvokedForElement()
            {
#line (753, 5) - (753, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (754, 5) - (754, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (755, 5) - (755, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(stamp);
#line (756, 5) - (756, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (757, 5) - (757, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"2026-01-15\", 1]", json.Dumps(l, @default: StampToString!));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultAndSeparatorsCombinedCorrectly()
            {
#line (761, 5) - (761, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (762, 5) - (762, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (763, 5) - (763, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (764, 5) - (764, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (765, 5) - (765, 117) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\":\"2026-01-15\",\"count\":5}", json.Dumps(d, separators: (",", ":"), @default: StampToString!));
            }

            [Xunit.FactAttribute]
            public void TestDumpWritesJsonToFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (772, 5) - (772, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (773, 5) - (773, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (774, 5) - (774, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["key"] = "value";
#line (775, 5) - (777, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (776, 9) - (776, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp);
                }

#line (777, 5) - (777, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                string content = "";
#line (778, 5) - (780, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
                {
#line (779, 9) - (779, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    content = fp2.Read();
                }

#line (780, 5) - (780, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": \"value\"}", content);
            }

            [Xunit.FactAttribute]
            public void TestLoadReadsJsonFromFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (784, 5) - (784, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (785, 5) - (787, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (786, 9) - (786, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    fp.Write("{\"key\": \"value\"}");
                }

#line (787, 5) - (795, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
                {
#line (788, 9) - (788, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (789, 9) - (795, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
                    {
                        case global::Sharpy.IDict d:
#line (791, 17) - (791, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["key"], "value"));
                            break;
                        default:
#line (793, 17) - (793, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpLoadRoundTripThroughFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (797, 5) - (797, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (798, 5) - (798, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (799, 5) - (799, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["name"] = "test";
#line (800, 5) - (800, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> values = new Sharpy.List<object>()
                {
                };
#line (801, 5) - (801, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(1);
#line (802, 5) - (802, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(2);
#line (803, 5) - (803, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(3);
#line (804, 5) - (804, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["values"] = values;
#line (805, 5) - (807, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (806, 9) - (806, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp, indent: 2);
                }

#line (807, 5) - (823, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
                {
#line (808, 9) - (808, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (809, 9) - (823, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
                    {
                        case global::Sharpy.IDict d:
#line (811, 17) - (811, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["name"], "test"));
#line (812, 17) - (817, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            switch (d["values"])
                            {
                                case global::Sharpy.IList vals:
#line (814, 25) - (814, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(vals));
                                    break;
                                default:
#line (816, 25) - (816, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.True(false);
                                    break;
                            }

                            break;
                        default:
#line (818, 17) - (818, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}

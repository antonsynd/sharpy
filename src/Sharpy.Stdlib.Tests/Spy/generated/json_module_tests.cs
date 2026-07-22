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
#line (834, 5) - (836, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
#line hidden
                {
#line (835, 9) - (835, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return "2026-01-15";
#line hidden
                }

#line (836, 5) - (836, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
#line hidden
            }

            public static object StampToDict(object obj)
            {
#line (840, 5) - (846, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                if (obj is Stamp)
#line hidden
                {
#line (841, 9) - (841, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                    {
                    };
#line (842, 9) - (842, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["year"] = ((Stamp)obj!).Year;
#line (843, 9) - (843, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["month"] = ((Stamp)obj!).Month;
#line (844, 9) - (844, 27) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    d["day"] = ((Stamp)obj!).Day;
#line (845, 9) - (845, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    return d;
#line hidden
                }

#line (846, 5) - (846, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return obj;
#line hidden
            }

            public static object FallbackCallback(object obj)
            {
#line (850, 5) - (850, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                return "fallback";
#line hidden
            }

            public static object IdentityCallback(object obj)
            {
#line (854, 5) - (854, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (80, 5) - (80, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (98, 5) - (98, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (138, 5) - (138, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (145, 5) - (145, 91) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
            public void TestDumpsInfinityThrowsValueError()
            {
#line (159, 5) - (162, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (160, 9) - (160, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Inf);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNanThrowsValueError()
            {
#line (164, 5) - (167, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
                {
#line (165, 9) - (165, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(math.Nan);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNonSerializableTypeThrowsTypeError()
            {
#line (169, 5) - (174, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
#line hidden
                {
#line (170, 9) - (170, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(new Unserializable());
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNullReturnsNull()
            {
#line (176, 5) - (176, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("null");
#line (177, 5) - (177, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Null(r);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrueReturnsTrue()
            {
#line (181, 5) - (181, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("true");
#line (182, 5) - (182, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFalseReturnsFalse()
            {
#line (186, 5) - (186, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("false");
#line (187, 5) - (187, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntReturnsInt()
            {
#line (191, 5) - (191, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42");
#line (192, 5) - (192, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (193, 5) - (193, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeIntReturnsInt()
            {
#line (197, 5) - (197, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("-7");
#line (198, 5) - (198, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (199, 5) - (199, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), -7));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsLargeIntReturnsLong()
            {
#line (203, 5) - (203, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("9999999999");
#line (204, 5) - (204, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<long>(r);
#line (205, 5) - (205, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                long value = 9999999999L;
#line (206, 5) - (206, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((long)r!), value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsFloatReturnsDouble()
            {
#line (210, 5) - (210, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("3.14");
#line (211, 5) - (211, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (212, 5) - (212, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), 3.14d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsScientificReturnsDouble()
            {
#line (216, 5) - (216, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("1.5e10");
#line (217, 5) - (217, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (218, 5) - (218, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), 1.5e10d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsZeroReturnsInt()
            {
#line (222, 5) - (222, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("0");
#line (223, 5) - (223, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (224, 5) - (224, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsStringReturnsString()
            {
#line (228, 5) - (228, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"hello\"");
#line (229, 5) - (229, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "hello"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringReturnsEmptyString()
            {
#line (233, 5) - (233, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"\"");
#line (234, 5) - (234, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, ""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedQuoteParsesCorrectly()
            {
#line (240, 5) - (240, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"say \\\"hi\\\"\"");
#line (241, 5) - (241, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "say \"hi\""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedBackslashParsesCorrectly()
            {
#line (245, 5) - (245, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\\\b\"");
#line (246, 5) - (246, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\\b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEscapedNewlineParsesCorrectly()
            {
#line (250, 5) - (250, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"a\\nb\"");
#line (251, 5) - (251, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "a\nb"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnicodeEscapeParsesCorrectly()
            {
#line (255, 5) - (255, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("\"caf\\u00e9\"");
#line (256, 5) - (256, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, "café"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsAllEscapesParsesCorrectly()
            {
#line (260, 5) - (260, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\/\""), "/"));
#line (261, 5) - (261, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\b\""), "\b"));
#line (262, 5) - (262, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\f\""), "\f"));
#line (263, 5) - (263, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\r\""), "\r"));
#line (264, 5) - (264, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(json.Loads("\"\\t\""), "\t"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectReturnsEmptyDict()
            {
#line (270, 5) - (270, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{}");
#line (271, 5) - (277, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (273, 13) - (273, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (275, 13) - (275, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleObjectReturnsDict()
            {
#line (279, 5) - (279, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"b\": \"two\"}");
#line (280, 5) - (287, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (282, 13) - (282, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (283, 13) - (283, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], "two"));
#line hidden
                        break;
                    default:
#line (285, 13) - (285, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedObjectReturnsNestedDict()
            {
#line (289, 5) - (289, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"outer\": {\"inner\": 42}}");
#line (290, 5) - (302, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (292, 13) - (297, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (294, 21) - (294, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (296, 21) - (296, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (298, 13) - (298, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyArrayReturnsEmptyList()
            {
#line (304, 5) - (304, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[]");
#line (305, 5) - (311, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (307, 13) - (307, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(l));
#line hidden
                        break;
                    default:
#line (309, 13) - (309, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsSimpleArrayReturnsList()
            {
#line (313, 5) - (313, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, 2, 3]");
#line (314, 5) - (323, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (316, 13) - (316, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(l));
#line (317, 13) - (317, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (318, 13) - (318, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], 2));
#line (319, 13) - (319, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], 3));
#line hidden
                        break;
                    default:
#line (321, 13) - (321, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsMixedArrayReturnsList()
            {
#line (325, 5) - (325, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[1, \"two\", true, null]");
#line (326, 5) - (335, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (328, 13) - (328, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[0], 1));
#line (329, 13) - (329, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[1], "two"));
#line (330, 13) - (330, 44) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(l[2], true));
#line (331, 13) - (331, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(l[3]);
#line hidden
                        break;
                    default:
#line (333, 13) - (333, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNestedArrayReturnsList()
            {
#line (337, 5) - (337, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[[1, 2], [3, 4]]");
#line (338, 5) - (351, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (340, 13) - (346, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
#line hidden
                        {
                            case global::Sharpy.IList inner1:
#line (342, 21) - (342, 54) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[0], 1));
#line (343, 21) - (343, 54) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner1[1], 2));
#line hidden
                                break;
                            default:
#line (345, 21) - (345, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (347, 13) - (347, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingWhitespaceParses()
            {
#line (353, 5) - (353, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("  42");
#line (354, 5) - (354, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithTrailingWhitespaceParses()
            {
#line (358, 5) - (358, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("42  ");
#line (359, 5) - (359, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsPrettyPrintedJsonParses()
            {
#line (363, 5) - (363, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\n  \"a\": 1,\n  \"b\": [\n    2,\n    3\n  ]\n}");
#line (364, 5) - (372, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (366, 13) - (366, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line hidden
                        break;
                    default:
#line (368, 13) - (368, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyStringThrowsJsonDecodeError()
            {
#line (374, 5) - (377, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (375, 9) - (375, 23) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsInvalidJsonThrowsJsonDecodeError()
            {
#line (379, 5) - (382, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (380, 9) - (380, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInObjectThrowsJsonDecodeError()
            {
#line (384, 5) - (387, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (385, 9) - (385, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1,}");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsTrailingCommaInArrayThrowsJsonDecodeError()
            {
#line (389, 5) - (392, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (390, 9) - (390, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2,]");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsExtraDataThrowsJsonDecodeError()
            {
#line (394, 5) - (397, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (395, 9) - (395, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("1 2");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedStringThrowsJsonDecodeError()
            {
#line (399, 5) - (402, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (400, 9) - (400, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("\"unclosed");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedObjectThrowsJsonDecodeError()
            {
#line (404, 5) - (407, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (405, 9) - (405, 32) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("{\"a\": 1");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsUnclosedArrayThrowsJsonDecodeError()
            {
#line (409, 5) - (412, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (410, 9) - (410, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("[1, 2");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorIsValueError()
            {
#line (414, 5) - (416, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (415, 9) - (415, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }));
#line (416, 5) - (416, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<ValueError>(ex);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestJsonDecodeErrorHasPositionInfo()
            {
#line (420, 5) - (422, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (421, 9) - (421, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Loads("invalid");
#line hidden
                }));
#line (422, 5) - (422, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("invalid", ex.Doc);
#line (423, 5) - (423, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(0, ex.Pos);
#line (424, 5) - (424, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("line 1", global::Sharpy.Builtins.Str(ex));
#line (425, 5) - (425, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Contains("column 1", global::Sharpy.Builtins.Str(ex));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripDictPreservesData()
            {
#line (431, 5) - (431, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (432, 5) - (432, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["name"] = "test";
#line (433, 5) - (433, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["value"] = 42;
#line (434, 5) - (434, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["active"] = true;
#line (435, 5) - (435, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nothing"] = null;
#line (436, 5) - (436, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(d));
#line (437, 5) - (446, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IDict result:
#line (439, 13) - (439, 56) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (440, 13) - (440, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["value"], 42));
#line (441, 13) - (441, 56) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["active"], true));
#line (442, 13) - (442, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result["nothing"]);
#line hidden
                        break;
                    default:
#line (444, 13) - (444, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripListPreservesData()
            {
#line (448, 5) - (448, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (449, 5) - (449, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (450, 5) - (450, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append("two");
#line (451, 5) - (451, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(3.0d);
#line (452, 5) - (452, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(false);
#line (453, 5) - (453, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (454, 5) - (454, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(l));
#line (455, 5) - (465, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IList result:
#line (457, 13) - (457, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[0], 1));
#line (458, 13) - (458, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[1], "two"));
#line (459, 13) - (459, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[2], 3.0d));
#line (460, 13) - (460, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[3], false));
#line (461, 13) - (461, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.Null(result[4]);
#line hidden
                        break;
                    default:
#line (463, 13) - (463, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNestedComplexPreservesData()
            {
#line (467, 5) - (467, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (468, 5) - (468, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["id"] = 1;
#line (469, 5) - (469, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item1["name"] = "alpha";
#line (470, 5) - (470, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> item2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (471, 5) - (471, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["id"] = 2;
#line (472, 5) - (472, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                item2["name"] = "beta";
#line (473, 5) - (473, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (474, 5) - (474, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item1);
#line (475, 5) - (475, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                items.Append(item2);
#line (476, 5) - (476, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> root = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (477, 5) - (477, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["items"] = items;
#line (478, 5) - (478, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                root["count"] = 2;
#line (479, 5) - (479, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object parsed = json.Loads(json.Dumps(root));
#line (480, 5) - (496, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IDict result:
#line (482, 13) - (482, 52) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["count"], 2));
#line (483, 13) - (493, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (result["items"])
#line hidden
                        {
                            case global::Sharpy.IList resultItems:
#line (485, 21) - (491, 1) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                switch (resultItems[0])
#line hidden
                                {
                                    case global::Sharpy.IDict first:
#line (487, 29) - (487, 64) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (488, 29) - (488, 72) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                        break;
                                    default:
#line (490, 29) - (490, 42) 40 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                        Xunit.Assert.True(false);
#line hidden
                                        break;
                                }

                                break;
                            default:
#line (492, 21) - (492, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (494, 13) - (494, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripStringWithEscapesPreservesData()
            {
#line (498, 5) - (498, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "line1\nline2\ttab \"quoted\" back\\slash";
#line (499, 5) - (499, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (500, 5) - (500, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripUnicodeStringPreservesData()
            {
#line (504, 5) - (504, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var original = "café üñîçöðé";
#line (505, 5) - (505, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads(json.Dumps(original));
#line (506, 5) - (506, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, original));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsDeeplyNestedHandlesRecursion()
            {
#line (513, 5) - (513, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var s = global::Sharpy.StringHelpers.Repeat("[", 20) + "1" + global::Sharpy.StringHelpers.Repeat("]", 20);
#line (514, 5) - (514, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object current = json.Loads(s);
#line (515, 5) - (515, 10) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var i = 0;
#line (516, 5) - (523, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                while (i < 20)
#line hidden
                {
#line (517, 9) - (522, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (current)
#line hidden
                    {
                        case global::Sharpy.IList l:
#line (519, 17) - (519, 31) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            current = l[0];
#line hidden
                            break;
                        default:
#line (521, 17) - (521, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }

#line (522, 9) - (522, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    i = i + 1;
#line hidden
                }

#line (523, 5) - (523, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(current, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectWithDuplicateKeysLastWins()
            {
#line (527, 5) - (527, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("{\"a\": 1, \"a\": 2}");
#line (528, 5) - (534, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (530, 13) - (530, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 2));
#line hidden
                        break;
                    default:
#line (532, 13) - (532, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsEmptyObjectAndArrayInArray()
            {
#line (536, 5) - (536, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                object r = json.Loads("[{}, []]");
#line (537, 5) - (552, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IList l:
#line (539, 13) - (544, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[0])
#line hidden
                        {
                            case global::Sharpy.IDict _:
#line (541, 21) - (541, 26) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
#line hidden
                                break;
                            default:
#line (543, 21) - (543, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

#line (544, 13) - (549, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        switch (l[1])
#line hidden
                        {
                            case global::Sharpy.IList _:
#line (546, 21) - (546, 26) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                ;
#line hidden
                                break;
                            default:
#line (548, 21) - (548, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (550, 13) - (550, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullValueInDictSerializesAsNull()
            {
#line (554, 5) - (554, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (555, 5) - (555, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["key"] = null;
#line (556, 5) - (556, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": null}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullInListSerializesAsNull()
            {
#line (560, 5) - (560, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (561, 5) - (561, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(null);
#line (562, 5) - (562, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[null]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyReturnsJsonObject()
            {
#line (568, 5) - (568, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (569, 5) - (569, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (570, 5) - (570, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (571, 5) - (571, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyIntReturnsJsonObject()
            {
#line (575, 5) - (575, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (576, 5) - (576, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"x\": 10, \"y\": 20}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictStringKeyReturnsNestedObject()
            {
#line (580, 5) - (580, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> inner = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "x",
                        1
                    }
                };
#line (581, 5) - (581, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (582, 5) - (582, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["inner"] = inner;
#line (583, 5) - (583, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"inner\": {\"x\": 1}}", json.Dumps(outer));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithSortKeysSortsKeys()
            {
#line (587, 5) - (587, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (588, 5) - (588, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["c"] = 3;
#line (589, 5) - (589, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (590, 5) - (590, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (591, 5) - (591, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2, \"c\": 3}", json.Dumps(d, sortKeys: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyWithIndentPrettyPrints()
            {
#line (595, 5) - (595, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (596, 5) - (596, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (597, 5) - (597, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (598, 5) - (598, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictStringKeyNestedDictStringKeyDictReturnsNestedObjects()
            {
#line (602, 5) - (602, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, int> leaf = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "val",
                        42
                    }
                };
#line (603, 5) - (603, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, Sharpy.Dict<string, int>> inner = new Sharpy.Dict<string, Sharpy.Dict<string, int>>()
#line hidden
                {
                };
#line (604, 5) - (604, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["mid"] = leaf;
#line (605, 5) - (605, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"mid\": {\"val\": 42}}", json.Dumps(inner));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntReturnsArray()
            {
#line (611, 5) - (611, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (612, 5) - (612, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1, 2, 3]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsEmptyListOfIntReturnsEmptyArray()
            {
#line (616, 5) - (616, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                };
#line (617, 5) - (617, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfStringReturnsArray()
            {
#line (621, 5) - (621, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Set<string> s = new Sharpy.Set<string>()
#line hidden
                {
                    "hello"
                };
#line (622, 5) - (622, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"hello\"]", json.Dumps(s));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfDoubleReturnsArray()
            {
#line (626, 5) - (626, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<double> l = new Sharpy.List<double>()
#line hidden
                {
                    1.5d,
                    2.5d
                };
#line (627, 5) - (627, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1.5, 2.5]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfBoolReturnsArray()
            {
#line (631, 5) - (631, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<bool> l = new Sharpy.List<bool>()
#line hidden
                {
                    true,
                    false
                };
#line (632, 5) - (632, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[true, false]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListOfIntInDictSerializes()
            {
#line (636, 5) - (636, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner = new Sharpy.List<int>()
#line hidden
                {
                    10,
                    20
                };
#line (637, 5) - (637, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (638, 5) - (638, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["nums"] = inner;
#line (639, 5) - (639, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"nums\": [10, 20]}", json.Dumps(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfIntWithIndentPrettyPrints()
            {
#line (643, 5) - (643, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (644, 5) - (644, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2\n]", json.Dumps(l, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (650, 5) - (650, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (651, 5) - (651, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (652, 5) - (652, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (653, 5) - (653, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDictWithCustomSeparatorsUsesGivenStrings()
            {
#line (657, 5) - (657, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (658, 5) - (658, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (659, 5) - (659, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (660, 5) - (660, 82) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\" = 1 ; \"b\" = 2}", json.Dumps(d, separators: (" ; ", " = ")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithCompactSeparatorsOmitsWhitespace()
            {
#line (664, 5) - (664, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> l = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2,
                    3
                };
#line (665, 5) - (665, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[1,2,3]", json.Dumps(l, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNullSeparatorsProducesDefaultOutput()
            {
#line (669, 5) - (669, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (670, 5) - (670, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (671, 5) - (671, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (672, 5) - (672, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var explicitNull = json.Dumps(d, separators: null);
#line (673, 5) - (673, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var defaultCall = json.Dumps(d);
#line (674, 5) - (674, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal(defaultCall, explicitNull);
#line (675, 5) - (675, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", explicitNull);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsSeparatorsWithIndentUsesNewlineForStructureAndKeySeparator()
            {
#line (679, 5) - (679, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (680, 5) - (680, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["a"] = 1;
#line (681, 5) - (681, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["b"] = 2;
#line (682, 5) - (682, 95) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2\n}", json.Dumps(d, indent: 2, separators: (",", ": ")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedDictWithCompactSeparatorsAppliesRecursively()
            {
#line (686, 5) - (686, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (687, 5) - (687, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["x"] = 1;
#line (688, 5) - (688, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                inner["y"] = 2;
#line (689, 5) - (689, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (690, 5) - (690, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["point"] = inner;
#line (691, 5) - (691, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer["count"] = 3;
#line (692, 5) - (692, 100) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"point\":{\"x\":1,\"y\":2},\"count\":3}", json.Dumps(outer, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithCompactSeparatorsAppliesRecursively()
            {
#line (696, 5) - (696, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner1 = new Sharpy.List<int>()
#line hidden
                {
                    1,
                    2
                };
#line (697, 5) - (697, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<int> inner2 = new Sharpy.List<int>()
#line hidden
                {
                    3,
                    4
                };
#line (698, 5) - (698, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> outer = new Sharpy.List<object>()
#line hidden
                {
                };
#line (699, 5) - (699, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner1);
#line (700, 5) - (700, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                outer.Append(inner2);
#line (701, 5) - (701, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[[1,2],[3,4]]", json.Dumps(outer, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsStrKeyDictWithCompactSeparatorsOmitsWhitespace()
            {
#line (705, 5) - (705, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (706, 5) - (706, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", json.Dumps(d, separators: (",", ":")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToString()
            {
#line (714, 5) - (714, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (715, 5) - (715, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("\"2026-01-15\"", json.Dumps(stamp, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackConvertsCustomTypeToDict()
            {
#line (719, 5) - (719, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (720, 5) - (720, 102) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"year\": 2026, \"month\": 1, \"day\": 15}", json.Dumps(stamp, @default: StampToDict!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNull()
            {
#line (725, 5) - (725, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("null", json.Dumps(null, @default: FallbackCallback!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNotInvokedForNativelySerializableTypes()
            {
#line (729, 5) - (729, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("42", json.Dumps(42, @default: FallbackCallback!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackReturningSameObjectRaisesTypeError()
            {
#line (733, 5) - (733, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (734, 5) - (737, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
#line hidden
                {
#line (735, 9) - (735, 53) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp, @default: IdentityCallback!);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNoDefaultCallbackNonSerializableTypeRaisesTypeError()
            {
#line (739, 5) - (739, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (740, 5) - (743, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
#line hidden
                {
#line (741, 9) - (741, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dumps(stamp);
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInDictIsInvokedForValue()
            {
#line (745, 5) - (745, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (746, 5) - (746, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (747, 5) - (747, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (748, 5) - (748, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (749, 5) - (749, 97) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\": \"2026-01-15\", \"count\": 5}", json.Dumps(d, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultCallbackNestedInListIsInvokedForElement()
            {
#line (753, 5) - (753, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (754, 5) - (754, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (755, 5) - (755, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(stamp);
#line (756, 5) - (756, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                l.Append(1);
#line (757, 5) - (757, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("[\"2026-01-15\", 1]", json.Dumps(l, @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsDefaultAndSeparatorsCombinedCorrectly()
            {
#line (761, 5) - (761, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var stamp = new Stamp();
#line (762, 5) - (762, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (763, 5) - (763, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["when"] = stamp;
#line (764, 5) - (764, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                d["count"] = 5;
#line (765, 5) - (765, 117) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"when\":\"2026-01-15\",\"count\":5}", json.Dumps(d, separators: (",", ":"), @default: StampToString!));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpWritesJsonToFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (772, 5) - (772, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (773, 5) - (773, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (774, 5) - (774, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["key"] = "value";
#line (775, 5) - (777, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (776, 9) - (776, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp);
#line hidden
                }

#line (777, 5) - (777, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                string content = "";
#line (778, 5) - (780, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (779, 9) - (779, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    content = fp2.Read();
#line hidden
                }

#line (780, 5) - (780, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Xunit.Assert.Equal("{\"key\": \"value\"}", content);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadReadsJsonFromFile()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (784, 5) - (784, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (785, 5) - (787, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (786, 9) - (786, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    fp.Write("{\"key\": \"value\"}");
#line hidden
                }

#line (787, 5) - (795, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (788, 9) - (788, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (789, 9) - (795, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (791, 17) - (791, 55) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                            break;
                        default:
#line (793, 17) - (793, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
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
#line (797, 5) - (797, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                var path = tmpPath + "/data.json";
#line (798, 5) - (798, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (799, 5) - (799, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["name"] = "test";
#line (800, 5) - (800, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                Sharpy.List<object> values = new Sharpy.List<object>()
#line hidden
                {
                };
#line (801, 5) - (801, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(1);
#line (802, 5) - (802, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(2);
#line (803, 5) - (803, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                values.Append(3);
#line (804, 5) - (804, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                data["values"] = values;
#line (805, 5) - (807, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (806, 9) - (806, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    json.Dump(data, fp, indent: 2);
#line hidden
                }

#line (807, 5) - (823, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (808, 9) - (808, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    object result = json.Load(fp2);
#line (809, 9) - (823, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                    switch (result)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (811, 17) - (811, 55) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(@operator.Eq(d["name"], "test"));
#line (812, 17) - (817, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            switch (d["values"])
#line hidden
                            {
                                case global::Sharpy.IList vals:
#line (814, 25) - (814, 47) 36 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(vals));
#line hidden
                                    break;
                                default:
#line (816, 25) - (816, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (818, 17) - (818, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/json/json_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
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
#line default

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
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.JSON.JsonAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class JSON
    {
        [global::Sharpy.SharpyModule("json.json_additional_tests")]
        public static partial class JsonAdditionalTests
        {
        }
    }

    public static partial class JSON
    {
        public partial class JsonAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLoadsIntMaxValueReturnsInt()
            {
#line (13, 5) - (13, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("2147483647");
#line (14, 5) - (14, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (15, 5) - (15, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, 2147483647));
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntMaxValuePlusOneReturnsLong()
            {
#line (20, 5) - (20, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("2147483648");
#line (21, 5) - (21, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<long>(r);
#line (22, 5) - (22, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                long value = 2147483648L;
#line (23, 5) - (23, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, value));
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeFloatReturnsDouble()
            {
#line (27, 5) - (27, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("-3.14");
#line (28, 5) - (28, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (29, 5) - (35, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
                {
                    case double f:
#line (31, 13) - (31, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.Equal(-3.14d, f, 1e-10d);
                        break;
                    default:
#line (33, 13) - (33, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeScientificReturnsDouble()
            {
#line (37, 5) - (37, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("-1.5e2");
#line (38, 5) - (38, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (39, 5) - (39, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(r, -150.0d));
            }

            [Xunit.FactAttribute]
            public void TestLoadsWhitespaceOnlyThrowsJsonDecodeError()
            {
#line (45, 5) - (48, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
                {
#line (46, 9) - (46, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                    json.Loads("   ");
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingAndTrailingWhitespaceParsesObject()
            {
#line (50, 5) - (50, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("  {\"key\": 1}  ");
#line (51, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
                {
                    case global::Sharpy.IDict d:
#line (53, 13) - (53, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], 1));
                        break;
                    default:
#line (55, 13) - (55, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfIntReturnsJsonArray()
            {
#line (61, 5) - (61, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Set<int> s = new Sharpy.Set<int>()
                {
                    42
                };
#line (62, 5) - (62, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[42]", json.Dumps(s));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfStringReturnsJsonArray()
            {
#line (66, 5) - (66, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<string> l = new Sharpy.List<string>()
                {
                    "hello",
                    "world"
                };
#line (67, 5) - (67, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\"hello\", \"world\"]", json.Dumps(l));
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithIndentPrettyPrints()
            {
#line (73, 5) - (73, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
                {
                };
#line (74, 5) - (74, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(1);
#line (75, 5) - (75, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(2);
#line (76, 5) - (76, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(3);
#line (77, 5) - (77, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2,\n  3\n]", json.Dumps(l, indent: 2));
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithIndentPrettyPrints()
            {
#line (81, 5) - (81, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> inner = new Sharpy.List<object>()
                {
                };
#line (82, 5) - (82, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("a");
#line (83, 5) - (83, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("b");
#line (84, 5) - (84, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (85, 5) - (85, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                outer["items"] = inner;
#line (86, 5) - (86, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                string result = json.Dumps(outer, indent: 2);
#line (87, 5) - (87, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (88, 5) - (88, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"items\"", result);
#line (89, 5) - (89, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"a\"", result);
            }

            [Xunit.FactAttribute]
            public void TestRoundTripLargeIntegerPreservesValue()
            {
#line (95, 5) - (95, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                long value = 9876543210L;
#line (96, 5) - (96, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(value));
#line (97, 5) - (97, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, value));
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNegativeIntPreservesValue()
            {
#line (101, 5) - (101, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(-999));
#line (102, 5) - (102, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, -999));
            }

            [Xunit.FactAttribute]
            public void TestRoundTripEmptyStringPreservesValue()
            {
#line (106, 5) - (106, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(""));
#line (107, 5) - (107, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, ""));
            }
        }
    }
}

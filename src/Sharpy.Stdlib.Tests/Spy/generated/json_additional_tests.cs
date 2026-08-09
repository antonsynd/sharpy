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
#line (13, 5) - (13, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("2147483647");
#line (14, 5) - (14, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<int>(r);
#line (15, 5) - (15, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(((int)r!), 2147483647));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsIntMaxValuePlusOneReturnsLong()
            {
#line (20, 5) - (20, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("2147483648");
#line (21, 5) - (21, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<long>(r);
#line (22, 5) - (22, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                long value = 2147483648L;
#line (23, 5) - (23, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(((long)r!), value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeFloatReturnsDouble()
            {
#line (27, 5) - (27, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("-3.14");
#line (31, 5) - (37, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
#line hidden
                {
                    case double f:
#line (33, 13) - (33, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.Equal(-3.14d, f, 1e-10d);
#line hidden
                        break;
                    default:
#line (35, 13) - (35, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeScientificReturnsDouble()
            {
#line (39, 5) - (39, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("-1.5e2");
#line (40, 5) - (40, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (41, 5) - (41, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), -150.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsWhitespaceOnlyThrowsJsonDecodeError()
            {
#line (47, 5) - (50, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.JSONDecodeError>((global::System.Action)(() =>
#line hidden
                {
#line (48, 9) - (48, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                    json.Loads("   ");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingAndTrailingWhitespaceParsesObject()
            {
#line (52, 5) - (52, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("  {\"key\": 1}  ");
#line (53, 5) - (61, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (55, 13) - (55, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], 1));
#line hidden
                        break;
                    default:
#line (57, 13) - (57, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfIntReturnsJsonArray()
            {
#line (63, 5) - (63, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Set<int> s = new Sharpy.Set<int>()
#line hidden
                {
                    42
                };
#line (64, 5) - (64, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[42]", json.Dumps(s));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfStringReturnsJsonArray()
            {
#line (68, 5) - (68, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<string> l = new Sharpy.List<string>()
#line hidden
                {
                    "hello",
                    "world"
                };
#line (69, 5) - (69, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\"hello\", \"world\"]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithIndentPrettyPrints()
            {
#line (75, 5) - (75, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (76, 5) - (76, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(1);
#line (77, 5) - (77, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(2);
#line (78, 5) - (78, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(3);
#line (79, 5) - (79, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2,\n  3\n]", json.Dumps(l, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithIndentPrettyPrints()
            {
#line (83, 5) - (83, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> inner = new Sharpy.List<object>()
#line hidden
                {
                };
#line (84, 5) - (84, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("a");
#line (85, 5) - (85, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("b");
#line (86, 5) - (86, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (87, 5) - (87, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                outer["items"] = inner;
#line (88, 5) - (88, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                string result = json.Dumps(outer, indent: 2);
#line (89, 5) - (89, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (90, 5) - (90, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"items\"", result);
#line (91, 5) - (91, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"a\"", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripLargeIntegerPreservesValue()
            {
#line (97, 5) - (97, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                long value = 9876543210L;
#line (98, 5) - (98, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(value));
#line (99, 5) - (99, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNegativeIntPreservesValue()
            {
#line (103, 5) - (103, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(-999));
#line (104, 5) - (104, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, -999));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripEmptyStringPreservesValue()
            {
#line (108, 5) - (108, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(""));
#line (109, 5) - (109, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, ""));
#line hidden
            }
        }
    }
}
#line default

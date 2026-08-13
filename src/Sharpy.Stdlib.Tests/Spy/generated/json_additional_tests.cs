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
#line (31, 5) - (31, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (32, 5) - (38, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
#line hidden
                {
                    case double f:
#line (34, 13) - (34, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.Equal(-3.14d, f, 1e-10d);
#line hidden
                        break;
                    default:
#line (36, 13) - (36, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsNegativeScientificReturnsDouble()
            {
#line (40, 5) - (40, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("-1.5e2");
#line (41, 5) - (41, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(r);
#line (42, 5) - (42, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(((double)r!), -150.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsWhitespaceOnlyThrowsJsonDecodeError()
            {
#line (48, 5) - (51, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (49, 9) - (49, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                    json.Loads("   ");
#line hidden
                }
                catch (global::Sharpy.JSONDecodeError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected JSONDecodeError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestLoadsWithLeadingAndTrailingWhitespaceParsesObject()
            {
#line (53, 5) - (53, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object r = json.Loads("  {\"key\": 1}  ");
#line (54, 5) - (62, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                switch (r)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (56, 13) - (56, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], 1));
#line hidden
                        break;
                    default:
#line (58, 13) - (58, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDumpsSetOfIntReturnsJsonArray()
            {
#line (64, 5) - (64, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Set<int> s = new Sharpy.Set<int>()
#line hidden
                {
                    42
                };
#line (65, 5) - (65, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[42]", json.Dumps(s));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListOfStringReturnsJsonArray()
            {
#line (69, 5) - (69, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<string> l = new Sharpy.List<string>()
#line hidden
                {
                    "hello",
                    "world"
                };
#line (70, 5) - (70, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\"hello\", \"world\"]", json.Dumps(l));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsListWithIndentPrettyPrints()
            {
#line (76, 5) - (76, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> l = new Sharpy.List<object>()
#line hidden
                {
                };
#line (77, 5) - (77, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(1);
#line (78, 5) - (78, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(2);
#line (79, 5) - (79, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                l.Append(3);
#line (80, 5) - (80, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Equal("[\n  1,\n  2,\n  3\n]", json.Dumps(l, indent: 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsNestedListWithIndentPrettyPrints()
            {
#line (84, 5) - (84, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.List<object> inner = new Sharpy.List<object>()
#line hidden
                {
                };
#line (85, 5) - (85, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("a");
#line (86, 5) - (86, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                inner.Append("b");
#line (87, 5) - (87, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (88, 5) - (88, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                outer["items"] = inner;
#line (89, 5) - (89, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                string result = json.Dumps(outer, indent: 2);
#line (90, 5) - (90, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (91, 5) - (91, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"items\"", result);
#line (92, 5) - (92, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.Contains("\"a\"", result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripLargeIntegerPreservesValue()
            {
#line (98, 5) - (98, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                long value = 9876543210L;
#line (99, 5) - (99, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(value));
#line (100, 5) - (100, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, value));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripNegativeIntPreservesValue()
            {
#line (104, 5) - (104, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(-999));
#line (105, 5) - (105, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, -999));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundTripEmptyStringPreservesValue()
            {
#line (109, 5) - (109, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                object parsed = json.Loads(json.Dumps(""));
#line (110, 5) - (110, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(parsed, ""));
#line hidden
            }
        }
    }
}
#line default

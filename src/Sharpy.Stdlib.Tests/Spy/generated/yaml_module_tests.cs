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
using yaml = global::Sharpy.Yaml;
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Yaml.YamlModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Yaml
    {
        [global::Sharpy.SharpyModule("yaml.yaml_module_tests")]
        public static partial class YamlModuleTests
        {
            internal static double _LoadFloat(string text)
            {
#line (162, 5) - (162, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (163, 5) - (169, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case double f:
#line (165, 13) - (165, 22) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        return f;
#line hidden
                    default:
#line (167, 13) - (167, 63) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        throw new global::Sharpy.ValueError("expected a float from " + text);
#line hidden
                }
            }
        }
    }

    public static partial class Yaml
    {
        public partial class YamlModuleTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestRoundTripSimpleMappingPreservesData()
            {
#line (21, 5) - (21, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (22, 5) - (22, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "test";
#line (23, 5) - (23, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["count"] = 42;
#line (24, 5) - (24, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (25, 5) - (25, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (26, 5) - (33, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IDict result:
#line (28, 13) - (28, 56) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (29, 13) - (29, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["count"], 42));
#line hidden
                        break;
                    default:
#line (31, 13) - (31, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripListPreservesData()
            {
#line (35, 5) - (35, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> data = new Sharpy.List<object>()
#line hidden
                {
                };
#line (36, 5) - (36, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(1);
#line (37, 5) - (37, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append("two");
#line (38, 5) - (38, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(true);
#line (39, 5) - (39, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(null);
#line (40, 5) - (40, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (41, 5) - (41, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (42, 5) - (53, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case global::Sharpy.IList result:
#line (44, 13) - (44, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[0], 1));
#line (45, 13) - (45, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[1], "two"));
#line (46, 13) - (46, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[2], true));
#line (47, 13) - (47, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(result[3]);
#line hidden
                        break;
                    default:
#line (49, 13) - (49, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadStringReturnsString()
            {
#line (55, 5) - (55, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: hello");
#line (56, 5) - (62, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (58, 13) - (58, 51) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "hello"));
#line hidden
                        break;
                    default:
#line (60, 13) - (60, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadQuotedStringReturnsString()
            {
#line (64, 5) - (64, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: \"hello world\"");
#line (65, 5) - (71, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (67, 13) - (67, 57) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "hello world"));
#line hidden
                        break;
                    default:
#line (69, 13) - (69, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadIntReturnsInt()
            {
#line (73, 5) - (73, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 42");
#line (74, 5) - (81, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (76, 13) - (76, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<int>(d["key"]);
#line (77, 13) - (77, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((int)d["key"]!), 42));
#line hidden
                        break;
                    default:
#line (79, 13) - (79, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNegativeIntReturnsInt()
            {
#line (83, 5) - (83, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: -7");
#line (84, 5) - (90, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (86, 13) - (86, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], -7));
#line hidden
                        break;
                    default:
#line (88, 13) - (88, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadLargeIntReturnsLong()
            {
#line (92, 5) - (92, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 9999999999");
#line (93, 5) - (101, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (95, 13) - (95, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<long>(d["key"]);
#line (96, 13) - (96, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        long value = 9999999999L;
#line (97, 13) - (97, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((long)d["key"]!), value));
#line hidden
                        break;
                    default:
#line (99, 13) - (99, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFloatReturnsDoubleExactly()
            {
#line (108, 5) - (108, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 3.14");
#line (109, 5) - (122, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (114, 13) - (119, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["key"])
#line hidden
                        {
                            case double fv:
#line (116, 21) - (116, 39) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3.14d, fv);
#line hidden
                                break;
                            default:
#line (118, 21) - (118, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (120, 13) - (120, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWholeNumberFloatStaysFloatTyped()
            {
#line (124, 5) - (124, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 2.0");
#line (125, 5) - (137, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (129, 13) - (134, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["key"])
#line hidden
                        {
                            case double fv:
#line (131, 21) - (131, 38) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(2.0d, fv);
#line hidden
                                break;
                            default:
#line (133, 21) - (133, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (135, 13) - (135, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadResolvesNonFloat32ExactValues()
            {
#line (142, 5) - (142, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(0.1d, _LoadFloat("0.1"));
#line (143, 5) - (143, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3.141592653589793d, _LoadFloat("3.141592653589793"));
#line (144, 5) - (144, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2.718281828459045d, _LoadFloat("2.718281828459045"));
#line (145, 5) - (145, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1e20d, _LoadFloat("1.0e+20"));
#line (146, 5) - (146, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(-0.30000000000000004d, _LoadFloat("-0.30000000000000004"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSafeLoadRoundTripIsIdentity()
            {
#line (151, 5) - (161, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_0 in new Sharpy.List<double>()
#line hidden
                {
                    0.1d,
                    3.141592653589793d,
                    2.718281828459045d,
                    1e20d,
                    1e-5d,
                    -0.0d,
                    1.5e20d
                }

                )
                {
                    var value = __loopVar_0;
#line (152, 9) - (152, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    string text = yaml.SafeDump(value);
#line (153, 9) - (153, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object reloaded = yaml.SafeLoad(text);
#line (154, 9) - (161, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (reloaded)
#line hidden
                    {
                        case double back:
#line (156, 17) - (156, 38) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.Equal(value, back);
#line hidden
                            break;
                        default:
#line (158, 17) - (158, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolTrueReturnsBool()
            {
#line (171, 5) - (171, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: true");
#line (172, 5) - (179, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (174, 13) - (174, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (175, 13) - (175, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (177, 13) - (177, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolFalseReturnsBool()
            {
#line (181, 5) - (181, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: false");
#line (182, 5) - (188, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (184, 13) - (184, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], false));
#line hidden
                        break;
                    default:
#line (186, 13) - (186, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNullReturnsNull()
            {
#line (190, 5) - (190, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: null");
#line (191, 5) - (197, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (193, 13) - (193, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (195, 13) - (195, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTildeReturnsNull()
            {
#line (199, 5) - (199, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: ~");
#line (200, 5) - (209, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (202, 13) - (202, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (204, 13) - (204, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoCapsStaysString()
            {
#line (211, 5) - (211, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: NO");
#line (212, 5) - (219, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (214, 13) - (214, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (215, 13) - (215, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "NO"));
#line hidden
                        break;
                    default:
#line (217, 13) - (217, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoLowerStaysString()
            {
#line (221, 5) - (221, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: no");
#line (222, 5) - (229, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (224, 13) - (224, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (225, 13) - (225, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "no"));
#line hidden
                        break;
                    default:
#line (227, 13) - (227, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoTitleStaysString()
            {
#line (231, 5) - (231, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: No");
#line (232, 5) - (239, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (234, 13) - (234, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (235, 13) - (235, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "No"));
#line hidden
                        break;
                    default:
#line (237, 13) - (237, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesTitleStaysString()
            {
#line (241, 5) - (241, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Yes");
#line (242, 5) - (249, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (244, 13) - (244, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (245, 13) - (245, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Yes"));
#line hidden
                        break;
                    default:
#line (247, 13) - (247, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesLowerStaysString()
            {
#line (251, 5) - (251, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yes");
#line (252, 5) - (259, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (254, 13) - (254, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (255, 13) - (255, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "yes"));
#line hidden
                        break;
                    default:
#line (257, 13) - (257, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnLowerStaysString()
            {
#line (261, 5) - (261, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: on");
#line (262, 5) - (269, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (264, 13) - (264, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (265, 13) - (265, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "on"));
#line hidden
                        break;
                    default:
#line (267, 13) - (267, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnTitleStaysString()
            {
#line (271, 5) - (271, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: On");
#line (272, 5) - (279, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (274, 13) - (274, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (275, 13) - (275, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "On"));
#line hidden
                        break;
                    default:
#line (277, 13) - (277, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffLowerStaysString()
            {
#line (281, 5) - (281, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: off");
#line (282, 5) - (289, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (284, 13) - (284, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (285, 13) - (285, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "off"));
#line hidden
                        break;
                    default:
#line (287, 13) - (287, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffCapsStaysString()
            {
#line (291, 5) - (291, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: OFF");
#line (292, 5) - (299, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (294, 13) - (294, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (295, 13) - (295, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "OFF"));
#line hidden
                        break;
                    default:
#line (297, 13) - (297, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYStaysString()
            {
#line (301, 5) - (301, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Y");
#line (302, 5) - (309, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (304, 13) - (304, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (305, 13) - (305, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Y"));
#line hidden
                        break;
                    default:
#line (307, 13) - (307, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNStaysString()
            {
#line (311, 5) - (311, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: N");
#line (312, 5) - (321, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (314, 13) - (314, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (315, 13) - (315, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "N"));
#line hidden
                        break;
                    default:
#line (317, 13) - (317, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInMapReturnsNestedDict()
            {
#line (323, 5) - (323, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("outer:\n  inner: 42\n");
#line (324, 5) - (334, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (326, 13) - (331, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (328, 21) - (328, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (330, 21) - (330, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (332, 13) - (332, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadListInMapReturnsNestedList()
            {
#line (336, 5) - (336, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("items:\n  - 1\n  - 2\n  - 3\n");
#line (337, 5) - (349, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (339, 13) - (346, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["items"])
#line hidden
                        {
                            case global::Sharpy.IList items:
#line (341, 21) - (341, 44) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(items));
#line (342, 21) - (342, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[0], 1));
#line (343, 21) - (343, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[2], 3));
#line hidden
                                break;
                            default:
#line (345, 21) - (345, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (347, 13) - (347, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInListReturnsListOfDicts()
            {
#line (351, 5) - (351, 88) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("- id: 1\n  name: alpha\n- id: 2\n  name: beta\n");
#line (352, 5) - (366, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (354, 13) - (354, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (355, 13) - (361, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (items[0])
#line hidden
                        {
                            case global::Sharpy.IDict first:
#line (357, 21) - (357, 56) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (358, 21) - (358, 64) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                break;
                            default:
#line (360, 21) - (360, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (362, 13) - (362, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyDocumentReturnsNull()
            {
#line (368, 5) - (368, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWhitespaceOnlyReturnsNull()
            {
#line (372, 5) - (372, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad("   \n  \n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyMappingReturnsEmptyDict()
            {
#line (376, 5) - (376, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{}");
#line (377, 5) - (383, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (379, 13) - (379, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (381, 13) - (381, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptySequenceReturnsEmptyList()
            {
#line (385, 5) - (385, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("[]");
#line (386, 5) - (392, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (388, 13) - (388, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
#line hidden
                        break;
                    default:
#line (390, 13) - (390, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnicodeStringPreservesCharacters()
            {
#line (394, 5) - (394, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: café üñî");
#line (395, 5) - (401, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (397, 13) - (397, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "café üñî"));
#line hidden
                        break;
                    default:
#line (399, 13) - (399, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFlowMappingReturnsDict()
            {
#line (403, 5) - (403, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{a: 1, b: 2}");
#line (404, 5) - (413, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (406, 13) - (406, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (407, 13) - (407, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], 2));
#line hidden
                        break;
                    default:
#line (409, 13) - (409, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpBlockStyleByDefault()
            {
#line (415, 5) - (415, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (416, 5) - (416, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (417, 5) - (417, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (418, 5) - (418, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("a: 1", text);
#line (419, 5) - (419, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.DoesNotContain("{", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFlowStyleProducesInline()
            {
#line (423, 5) - (423, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (424, 5) - (424, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (425, 5) - (425, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (426, 5) - (426, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, defaultFlowStyle: true);
#line (427, 5) - (427, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("{", text);
#line (428, 5) - (428, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("}", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpIndentUsesGivenWidth()
            {
#line (432, 5) - (432, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (433, 5) - (433, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                inner["x"] = 1;
#line (434, 5) - (434, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (435, 5) - (435, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                outer["outer"] = inner;
#line (436, 5) - (436, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(outer, indent: 4);
#line (437, 5) - (437, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("    x: 1", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysTrueSortsAlphabetically()
            {
#line (441, 5) - (441, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (442, 5) - (442, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (443, 5) - (443, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (444, 5) - (444, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (445, 5) - (445, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: true);
#line (446, 5) - (446, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (447, 5) - (447, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (448, 5) - (448, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (449, 5) - (449, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line (450, 5) - (450, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posB < posC);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysFalsePreservesInsertionOrder()
            {
#line (454, 5) - (454, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (455, 5) - (455, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (456, 5) - (456, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (457, 5) - (457, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (458, 5) - (458, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: false);
#line (459, 5) - (459, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (460, 5) - (460, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (461, 5) - (461, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (462, 5) - (462, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posC < posA);
#line (463, 5) - (463, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWidthAffectsLineWrapping()
            {
#line (467, 5) - (467, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (468, 5) - (470, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.Builtins.Range(10))
#line hidden
                {
                    var i = __loopVar_1;
#line (469, 9) - (469, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    items.Append(FormattableString.Invariant($"item-{(i)}"));
#line hidden
                }

#line (470, 5) - (470, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string narrow = yaml.SafeDump(items, width: 20);
#line (471, 5) - (471, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string wide = yaml.SafeDump(items, width: 1000);
#line (472, 5) - (472, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", narrow);
#line (473, 5) - (473, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", wide);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpNullEmitsNullToken()
            {
#line (477, 5) - (477, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(null);
#line (478, 5) - (478, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(text));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllTwoDocumentsReturnsBoth()
            {
#line (484, 5) - (484, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\nb: 2\n");
#line (485, 5) - (485, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(docs));
#line (486, 5) - (486, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstElem = docs.GetItemUnchecked(0);
#line (487, 5) - (492, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstElem)
#line hidden
                {
                    case global::Sharpy.IDict first:
#line (489, 13) - (489, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(first["a"], 1));
#line hidden
                        break;
                    default:
#line (491, 13) - (491, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (492, 5) - (492, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object secondElem = docs.GetItemUnchecked(1);
#line (493, 5) - (499, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (secondElem)
#line hidden
                {
                    case global::Sharpy.IDict second:
#line (495, 13) - (495, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(second["b"], 2));
#line hidden
                        break;
                    default:
#line (497, 13) - (497, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllThreeDocumentsReturnsAll()
            {
#line (501, 5) - (501, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("1\n---\n2\n---\n3\n");
#line (502, 5) - (502, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (503, 5) - (503, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(0), 1));
#line (504, 5) - (504, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(1), 2));
#line (505, 5) - (505, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(2), 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllSingleDocumentReturnsOne()
            {
#line (509, 5) - (509, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("key: value\n");
#line (510, 5) - (510, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(docs));
#line (511, 5) - (511, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object elem = docs.GetItemUnchecked(0);
#line (512, 5) - (518, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (elem)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (514, 13) - (514, 51) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                        break;
                    default:
#line (516, 13) - (516, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllEmptyDocumentInStreamYieldsNull()
            {
#line (520, 5) - (520, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\n---\nb: 2\n");
#line (521, 5) - (521, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (522, 5) - (522, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstObj = docs.GetItemUnchecked(0);
#line (523, 5) - (528, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (525, 13) - (525, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (527, 13) - (527, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (528, 5) - (528, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(docs.GetItemUnchecked(1));
#line (529, 5) - (529, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object thirdObj = docs.GetItemUnchecked(2);
#line (530, 5) - (536, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (thirdObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (532, 13) - (532, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (534, 13) - (534, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllMultipleDocumentsProducesSeparators()
            {
#line (538, 5) - (538, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (539, 5) - (539, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (540, 5) - (540, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (541, 5) - (541, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc2["b"] = 2;
#line (542, 5) - (542, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (543, 5) - (543, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (544, 5) - (544, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc2);
#line (545, 5) - (545, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (546, 5) - (546, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("---", text);
#line (547, 5) - (547, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> reparsed = yaml.SafeLoadAll(text);
#line (548, 5) - (548, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(reparsed));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllSingleDocumentNoLeadingSeparator()
            {
#line (552, 5) - (552, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (553, 5) - (553, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (554, 5) - (554, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (555, 5) - (555, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (556, 5) - (556, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (557, 5) - (557, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(text, "---"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFileSafeLoadFileRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (563, 5) - (563, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string path = tmpPath + "/data.yaml";
#line (564, 5) - (564, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (565, 5) - (565, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "file-test";
#line (566, 5) - (566, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["value"] = 7;
#line (567, 5) - (569, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (568, 9) - (568, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeDumpFile(data, fp);
#line hidden
                }

#line (569, 5) - (569, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string name = "";
#line (570, 5) - (570, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int value = 0;
#line (571, 5) - (587, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (572, 9) - (572, 51) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object parsed = yaml.SafeLoadFile(fp2);
#line (573, 9) - (587, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (parsed)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (575, 17) - (580, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["name"])
#line hidden
                            {
                                case string n:
#line (577, 25) - (577, 33) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    name = n;
#line hidden
                                    break;
                                default:
#line (579, 25) - (579, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

#line (580, 17) - (585, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["value"])
#line hidden
                            {
                                case int v:
#line (582, 25) - (582, 34) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    value = v;
#line hidden
                                    break;
                                default:
#line (584, 25) - (584, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (586, 17) - (586, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (587, 5) - (587, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("file-test", name);
#line (588, 5) - (588, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(7, value);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAnchorAndAliasResolvesMappingReference()
            {
#line (594, 5) - (594, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "defaults: &defaults\n  timeout: 30\n  retries: 3\nproduction: *defaults\n";
#line (595, 5) - (595, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (596, 5) - (607, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (598, 13) - (604, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["production"])
#line hidden
                        {
                            case global::Sharpy.IDict production:
#line (600, 21) - (600, 67) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["timeout"], 30));
#line (601, 21) - (601, 66) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["retries"], 3));
#line hidden
                                break;
                            default:
#line (603, 21) - (603, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (605, 13) - (605, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadSimpleAliasDuplicatesValue()
            {
#line (609, 5) - (609, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "first: &val hello\nsecond: *val\n";
#line (610, 5) - (610, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (611, 5) - (618, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (613, 13) - (613, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["first"], "hello"));
#line (614, 13) - (614, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["second"], "hello"));
#line hidden
                        break;
                    default:
#line (616, 13) - (616, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUndefinedAliasThrowsParseError()
            {
#line (620, 5) - (625, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (621, 9) - (621, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("ref: *missing\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMalformedYamlThrowsParseError()
            {
#line (627, 5) - (630, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (628, 9) - (628, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnterminatedQuoteThrowsParseError()
            {
#line (632, 5) - (635, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (633, 9) - (633, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: 'unterminated\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTabIndentationThrowsParseError()
            {
#line (637, 5) - (640, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (638, 9) - (638, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("a:\n\t- 1\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorHasLineAndColumn()
            {
#line (642, 5) - (644, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (643, 9) - (643, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
#line (644, 5) - (644, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Line >= 0);
#line (645, 5) - (645, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Column >= 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorIsYamlError()
            {
#line (649, 5) - (651, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (650, 9) - (650, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
#line (651, 5) - (651, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(exc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatExponentMatchesPyyaml()
            {
#line (674, 5) - (674, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)));
#line (675, 5) - (675, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+17", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e17d)));
#line (676, 5) - (676, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-1.0e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-1e20d)));
#line (677, 5) - (677, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+100", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e100d)));
#line (679, 5) - (679, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("2.5e-10", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(2.5e-10d)));
#line (680, 5) - (680, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.5e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.5e20d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatLayoutBoundaryMatchesPyyaml()
            {
#line (686, 5) - (686, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+16", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)));
#line (687, 5) - (687, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1000000000000000.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e15d)));
#line (689, 5) - (689, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0001", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-4d)));
#line (690, 5) - (690, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e-05", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-5d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWholeFloatKeepsItsPoint()
            {
#line (696, 5) - (696, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)));
#line (697, 5) - (697, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("5.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(5.0d)));
#line (698, 5) - (698, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(0.0d)));
#line (699, 5) - (699, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-0.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-0.0d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatRoundTripsAsAFloat()
            {
#line (706, 5) - (706, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(yaml.SafeDump(1.0d));
#line (707, 5) - (707, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(parsed);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpFloatMatchesSafeDump()
            {
#line (715, 5) - (715, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e20d)));
#line (716, 5) - (716, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1.0d)));
#line (717, 5) - (717, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e16d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSpecialFloatsKeepYamlSpellings()
            {
#line (722, 5) - (722, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".inf", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("inf"))));
#line (723, 5) - (723, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-.inf", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("-inf"))));
#line (724, 5) - (724, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".nan", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("nan"))));
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

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
#line (159, 5) - (159, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (160, 5) - (166, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case double f:
#line (162, 13) - (162, 22) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        return f;
#line hidden
                    default:
#line (164, 13) - (164, 63) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (109, 5) - (120, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (111, 13) - (111, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
#line (112, 13) - (117, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (((double)d["key"]!))
#line hidden
                        {
                            case double fv:
#line (114, 21) - (114, 39) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3.14d, fv);
#line hidden
                                break;
                            default:
#line (116, 21) - (116, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (118, 13) - (118, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWholeNumberFloatStaysFloatTyped()
            {
#line (122, 5) - (122, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 2.0");
#line (123, 5) - (134, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (125, 13) - (125, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
#line (126, 13) - (131, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (((double)d["key"]!))
#line hidden
                        {
                            case double fv:
#line (128, 21) - (128, 38) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(2.0d, fv);
#line hidden
                                break;
                            default:
#line (130, 21) - (130, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (132, 13) - (132, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadResolvesNonFloat32ExactValues()
            {
#line (139, 5) - (139, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(0.1d, _LoadFloat("0.1"));
#line (140, 5) - (140, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3.141592653589793d, _LoadFloat("3.141592653589793"));
#line (141, 5) - (141, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2.718281828459045d, _LoadFloat("2.718281828459045"));
#line (142, 5) - (142, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1e20d, _LoadFloat("1.0e+20"));
#line (143, 5) - (143, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(-0.30000000000000004d, _LoadFloat("-0.30000000000000004"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSafeLoadRoundTripIsIdentity()
            {
#line (148, 5) - (158, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (149, 9) - (149, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    string text = yaml.SafeDump(value);
#line (150, 9) - (150, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object reloaded = yaml.SafeLoad(text);
#line (151, 9) - (158, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (reloaded)
#line hidden
                    {
                        case double back:
#line (153, 17) - (153, 38) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.Equal(value, back);
#line hidden
                            break;
                        default:
#line (155, 17) - (155, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolTrueReturnsBool()
            {
#line (168, 5) - (168, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: true");
#line (169, 5) - (176, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (171, 13) - (171, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (172, 13) - (172, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (174, 13) - (174, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolFalseReturnsBool()
            {
#line (178, 5) - (178, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: false");
#line (179, 5) - (185, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (181, 13) - (181, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], false));
#line hidden
                        break;
                    default:
#line (183, 13) - (183, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNullReturnsNull()
            {
#line (187, 5) - (187, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: null");
#line (188, 5) - (194, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (190, 13) - (190, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (192, 13) - (192, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTildeReturnsNull()
            {
#line (196, 5) - (196, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: ~");
#line (197, 5) - (206, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (199, 13) - (199, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (201, 13) - (201, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoCapsStaysString()
            {
#line (208, 5) - (208, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: NO");
#line (209, 5) - (216, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (211, 13) - (211, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (212, 13) - (212, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "NO"));
#line hidden
                        break;
                    default:
#line (214, 13) - (214, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoLowerStaysString()
            {
#line (218, 5) - (218, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: no");
#line (219, 5) - (226, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (221, 13) - (221, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (222, 13) - (222, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "no"));
#line hidden
                        break;
                    default:
#line (224, 13) - (224, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoTitleStaysString()
            {
#line (228, 5) - (228, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: No");
#line (229, 5) - (236, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (231, 13) - (231, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (232, 13) - (232, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "No"));
#line hidden
                        break;
                    default:
#line (234, 13) - (234, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesTitleStaysString()
            {
#line (238, 5) - (238, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Yes");
#line (239, 5) - (246, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (241, 13) - (241, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (242, 13) - (242, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Yes"));
#line hidden
                        break;
                    default:
#line (244, 13) - (244, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesLowerStaysString()
            {
#line (248, 5) - (248, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yes");
#line (249, 5) - (256, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (251, 13) - (251, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (252, 13) - (252, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "yes"));
#line hidden
                        break;
                    default:
#line (254, 13) - (254, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnLowerStaysString()
            {
#line (258, 5) - (258, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: on");
#line (259, 5) - (266, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (261, 13) - (261, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (262, 13) - (262, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "on"));
#line hidden
                        break;
                    default:
#line (264, 13) - (264, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnTitleStaysString()
            {
#line (268, 5) - (268, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: On");
#line (269, 5) - (276, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (271, 13) - (271, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (272, 13) - (272, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "On"));
#line hidden
                        break;
                    default:
#line (274, 13) - (274, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffLowerStaysString()
            {
#line (278, 5) - (278, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: off");
#line (279, 5) - (286, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (281, 13) - (281, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (282, 13) - (282, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "off"));
#line hidden
                        break;
                    default:
#line (284, 13) - (284, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffCapsStaysString()
            {
#line (288, 5) - (288, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: OFF");
#line (289, 5) - (296, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (291, 13) - (291, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (292, 13) - (292, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "OFF"));
#line hidden
                        break;
                    default:
#line (294, 13) - (294, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYStaysString()
            {
#line (298, 5) - (298, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Y");
#line (299, 5) - (306, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (301, 13) - (301, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (302, 13) - (302, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Y"));
#line hidden
                        break;
                    default:
#line (304, 13) - (304, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNStaysString()
            {
#line (308, 5) - (308, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: N");
#line (309, 5) - (318, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (311, 13) - (311, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (312, 13) - (312, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "N"));
#line hidden
                        break;
                    default:
#line (314, 13) - (314, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInMapReturnsNestedDict()
            {
#line (320, 5) - (320, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("outer:\n  inner: 42\n");
#line (321, 5) - (331, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (323, 13) - (328, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (325, 21) - (325, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (327, 21) - (327, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (329, 13) - (329, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadListInMapReturnsNestedList()
            {
#line (333, 5) - (333, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("items:\n  - 1\n  - 2\n  - 3\n");
#line (334, 5) - (346, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (336, 13) - (343, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["items"])
#line hidden
                        {
                            case global::Sharpy.IList items:
#line (338, 21) - (338, 44) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(items));
#line (339, 21) - (339, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[0], 1));
#line (340, 21) - (340, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[2], 3));
#line hidden
                                break;
                            default:
#line (342, 21) - (342, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (344, 13) - (344, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInListReturnsListOfDicts()
            {
#line (348, 5) - (348, 88) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("- id: 1\n  name: alpha\n- id: 2\n  name: beta\n");
#line (349, 5) - (363, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (351, 13) - (351, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (352, 13) - (358, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (items[0])
#line hidden
                        {
                            case global::Sharpy.IDict first:
#line (354, 21) - (354, 56) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (355, 21) - (355, 64) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                break;
                            default:
#line (357, 21) - (357, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (359, 13) - (359, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyDocumentReturnsNull()
            {
#line (365, 5) - (365, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWhitespaceOnlyReturnsNull()
            {
#line (369, 5) - (369, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad("   \n  \n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyMappingReturnsEmptyDict()
            {
#line (373, 5) - (373, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{}");
#line (374, 5) - (380, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (376, 13) - (376, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (378, 13) - (378, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptySequenceReturnsEmptyList()
            {
#line (382, 5) - (382, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("[]");
#line (383, 5) - (389, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (385, 13) - (385, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
#line hidden
                        break;
                    default:
#line (387, 13) - (387, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnicodeStringPreservesCharacters()
            {
#line (391, 5) - (391, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: café üñî");
#line (392, 5) - (398, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (394, 13) - (394, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "café üñî"));
#line hidden
                        break;
                    default:
#line (396, 13) - (396, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFlowMappingReturnsDict()
            {
#line (400, 5) - (400, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{a: 1, b: 2}");
#line (401, 5) - (410, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (403, 13) - (403, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (404, 13) - (404, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], 2));
#line hidden
                        break;
                    default:
#line (406, 13) - (406, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpBlockStyleByDefault()
            {
#line (412, 5) - (412, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (413, 5) - (413, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (414, 5) - (414, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (415, 5) - (415, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("a: 1", text);
#line (416, 5) - (416, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.DoesNotContain("{", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFlowStyleProducesInline()
            {
#line (420, 5) - (420, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (421, 5) - (421, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (422, 5) - (422, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (423, 5) - (423, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, defaultFlowStyle: true);
#line (424, 5) - (424, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("{", text);
#line (425, 5) - (425, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("}", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpIndentUsesGivenWidth()
            {
#line (429, 5) - (429, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (430, 5) - (430, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                inner["x"] = 1;
#line (431, 5) - (431, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (432, 5) - (432, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                outer["outer"] = inner;
#line (433, 5) - (433, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(outer, indent: 4);
#line (434, 5) - (434, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("    x: 1", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysTrueSortsAlphabetically()
            {
#line (438, 5) - (438, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (439, 5) - (439, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (440, 5) - (440, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (441, 5) - (441, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (442, 5) - (442, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: true);
#line (443, 5) - (443, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (444, 5) - (444, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (445, 5) - (445, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (446, 5) - (446, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line (447, 5) - (447, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posB < posC);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysFalsePreservesInsertionOrder()
            {
#line (451, 5) - (451, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (452, 5) - (452, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (453, 5) - (453, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (454, 5) - (454, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (455, 5) - (455, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: false);
#line (456, 5) - (456, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (457, 5) - (457, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (458, 5) - (458, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (459, 5) - (459, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posC < posA);
#line (460, 5) - (460, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWidthAffectsLineWrapping()
            {
#line (464, 5) - (464, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (465, 5) - (467, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.Builtins.Range(10))
#line hidden
                {
                    var i = __loopVar_1;
#line (466, 9) - (466, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    items.Append(FormattableString.Invariant($"item-{(i)}"));
#line hidden
                }

#line (467, 5) - (467, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string narrow = yaml.SafeDump(items, width: 20);
#line (468, 5) - (468, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string wide = yaml.SafeDump(items, width: 1000);
#line (469, 5) - (469, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", narrow);
#line (470, 5) - (470, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", wide);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpNullEmitsNullToken()
            {
#line (474, 5) - (474, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(null);
#line (475, 5) - (475, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(text));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllTwoDocumentsReturnsBoth()
            {
#line (481, 5) - (481, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\nb: 2\n");
#line (482, 5) - (482, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(docs));
#line (483, 5) - (483, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstElem = docs.GetItemUnchecked(0);
#line (484, 5) - (489, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstElem)
#line hidden
                {
                    case global::Sharpy.IDict first:
#line (486, 13) - (486, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(first["a"], 1));
#line hidden
                        break;
                    default:
#line (488, 13) - (488, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (489, 5) - (489, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object secondElem = docs.GetItemUnchecked(1);
#line (490, 5) - (496, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (secondElem)
#line hidden
                {
                    case global::Sharpy.IDict second:
#line (492, 13) - (492, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(second["b"], 2));
#line hidden
                        break;
                    default:
#line (494, 13) - (494, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllThreeDocumentsReturnsAll()
            {
#line (498, 5) - (498, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("1\n---\n2\n---\n3\n");
#line (499, 5) - (499, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (500, 5) - (500, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(0), 1));
#line (501, 5) - (501, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(1), 2));
#line (502, 5) - (502, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(2), 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllSingleDocumentReturnsOne()
            {
#line (506, 5) - (506, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("key: value\n");
#line (507, 5) - (507, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(docs));
#line (508, 5) - (508, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object elem = docs.GetItemUnchecked(0);
#line (509, 5) - (515, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (elem)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (511, 13) - (511, 51) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                        break;
                    default:
#line (513, 13) - (513, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllEmptyDocumentInStreamYieldsNull()
            {
#line (517, 5) - (517, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\n---\nb: 2\n");
#line (518, 5) - (518, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (519, 5) - (519, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstObj = docs.GetItemUnchecked(0);
#line (520, 5) - (525, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (522, 13) - (522, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (524, 13) - (524, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (525, 5) - (525, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(docs.GetItemUnchecked(1));
#line (526, 5) - (526, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object thirdObj = docs.GetItemUnchecked(2);
#line (527, 5) - (533, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (thirdObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (529, 13) - (529, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (531, 13) - (531, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllMultipleDocumentsProducesSeparators()
            {
#line (535, 5) - (535, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (536, 5) - (536, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (537, 5) - (537, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (538, 5) - (538, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc2["b"] = 2;
#line (539, 5) - (539, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (540, 5) - (540, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (541, 5) - (541, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc2);
#line (542, 5) - (542, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (543, 5) - (543, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("---", text);
#line (544, 5) - (544, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> reparsed = yaml.SafeLoadAll(text);
#line (545, 5) - (545, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(reparsed));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllSingleDocumentNoLeadingSeparator()
            {
#line (549, 5) - (549, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (550, 5) - (550, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (551, 5) - (551, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (552, 5) - (552, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (553, 5) - (553, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (554, 5) - (554, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(text, "---"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFileSafeLoadFileRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (560, 5) - (560, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string path = tmpPath + "/data.yaml";
#line (561, 5) - (561, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (562, 5) - (562, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "file-test";
#line (563, 5) - (563, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["value"] = 7;
#line (564, 5) - (566, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (565, 9) - (565, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeDumpFile(data, fp);
#line hidden
                }

#line (566, 5) - (566, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string name = "";
#line (567, 5) - (567, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int value = 0;
#line (568, 5) - (584, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (569, 9) - (569, 51) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object parsed = yaml.SafeLoadFile(fp2);
#line (570, 9) - (584, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (parsed)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (572, 17) - (577, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["name"])
#line hidden
                            {
                                case string n:
#line (574, 25) - (574, 33) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    name = n;
#line hidden
                                    break;
                                default:
#line (576, 25) - (576, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

#line (577, 17) - (582, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["value"])
#line hidden
                            {
                                case int v:
#line (579, 25) - (579, 34) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    value = v;
#line hidden
                                    break;
                                default:
#line (581, 25) - (581, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (583, 17) - (583, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (584, 5) - (584, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("file-test", name);
#line (585, 5) - (585, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(7, value);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAnchorAndAliasResolvesMappingReference()
            {
#line (591, 5) - (591, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "defaults: &defaults\n  timeout: 30\n  retries: 3\nproduction: *defaults\n";
#line (592, 5) - (592, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (593, 5) - (604, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (595, 13) - (601, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["production"])
#line hidden
                        {
                            case global::Sharpy.IDict production:
#line (597, 21) - (597, 67) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["timeout"], 30));
#line (598, 21) - (598, 66) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["retries"], 3));
#line hidden
                                break;
                            default:
#line (600, 21) - (600, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (602, 13) - (602, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadSimpleAliasDuplicatesValue()
            {
#line (606, 5) - (606, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "first: &val hello\nsecond: *val\n";
#line (607, 5) - (607, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (608, 5) - (615, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (610, 13) - (610, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["first"], "hello"));
#line (611, 13) - (611, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["second"], "hello"));
#line hidden
                        break;
                    default:
#line (613, 13) - (613, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUndefinedAliasThrowsParseError()
            {
#line (617, 5) - (622, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (618, 9) - (618, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("ref: *missing\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMalformedYamlThrowsParseError()
            {
#line (624, 5) - (627, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (625, 9) - (625, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnterminatedQuoteThrowsParseError()
            {
#line (629, 5) - (632, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (630, 9) - (630, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: 'unterminated\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTabIndentationThrowsParseError()
            {
#line (634, 5) - (637, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (635, 9) - (635, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("a:\n\t- 1\n");
#line hidden
                }));
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorHasLineAndColumn()
            {
#line (639, 5) - (641, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (640, 9) - (640, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
#line (641, 5) - (641, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Line >= 0);
#line (642, 5) - (642, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Column >= 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorIsYamlError()
            {
#line (646, 5) - (648, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
#line hidden
                {
#line (647, 9) - (647, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }));
#line (648, 5) - (648, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(exc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatExponentMatchesPyyaml()
            {
#line (671, 5) - (671, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)));
#line (672, 5) - (672, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+17", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e17d)));
#line (673, 5) - (673, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-1.0e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-1e20d)));
#line (674, 5) - (674, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+100", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e100d)));
#line (676, 5) - (676, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("2.5e-10", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(2.5e-10d)));
#line (677, 5) - (677, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.5e+20", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.5e20d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatLayoutBoundaryMatchesPyyaml()
            {
#line (683, 5) - (683, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+16", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)));
#line (684, 5) - (684, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1000000000000000.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e15d)));
#line (686, 5) - (686, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0001", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-4d)));
#line (687, 5) - (687, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e-05", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-5d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWholeFloatKeepsItsPoint()
            {
#line (693, 5) - (693, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)));
#line (694, 5) - (694, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("5.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(5.0d)));
#line (695, 5) - (695, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(0.0d)));
#line (696, 5) - (696, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-0.0", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-0.0d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatRoundTripsAsAFloat()
            {
#line (703, 5) - (703, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(yaml.SafeDump(1.0d));
#line (704, 5) - (704, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(parsed);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpFloatMatchesSafeDump()
            {
#line (712, 5) - (712, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e20d)));
#line (713, 5) - (713, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1.0d)));
#line (714, 5) - (714, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e16d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSpecialFloatsKeepYamlSpellings()
            {
#line (719, 5) - (719, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".inf", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("inf"))));
#line (720, 5) - (720, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-.inf", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("-inf"))));
#line (721, 5) - (721, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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

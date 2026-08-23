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
#line (161, 5) - (161, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (162, 5) - (168, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
#line hidden
                {
                    case double f:
#line (164, 13) - (164, 22) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        return f;
#line hidden
                    default:
#line (166, 13) - (166, 63) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (113, 13) - (113, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
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
#line (125, 5) - (136, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (127, 13) - (127, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
#line (128, 13) - (133, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["key"])
#line hidden
                        {
                            case double fv:
#line (130, 21) - (130, 38) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(2.0d, fv);
#line hidden
                                break;
                            default:
#line (132, 21) - (132, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (134, 13) - (134, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadResolvesNonFloat32ExactValues()
            {
#line (141, 5) - (141, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(0.1d, _LoadFloat("0.1"));
#line (142, 5) - (142, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3.141592653589793d, _LoadFloat("3.141592653589793"));
#line (143, 5) - (143, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2.718281828459045d, _LoadFloat("2.718281828459045"));
#line (144, 5) - (144, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1e20d, _LoadFloat("1.0e+20"));
#line (145, 5) - (145, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(-0.30000000000000004d, _LoadFloat("-0.30000000000000004"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSafeLoadRoundTripIsIdentity()
            {
#line (150, 5) - (160, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (151, 9) - (151, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    string text = yaml.SafeDump(value);
#line (152, 9) - (152, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object reloaded = yaml.SafeLoad(text);
#line (153, 9) - (160, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (reloaded)
#line hidden
                    {
                        case double back:
#line (155, 17) - (155, 38) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.Equal(value, back);
#line hidden
                            break;
                        default:
#line (157, 17) - (157, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolTrueReturnsBool()
            {
#line (170, 5) - (170, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: true");
#line (171, 5) - (178, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (173, 13) - (173, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (174, 13) - (174, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (176, 13) - (176, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolFalseReturnsBool()
            {
#line (180, 5) - (180, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: false");
#line (181, 5) - (187, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (183, 13) - (183, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], false));
#line hidden
                        break;
                    default:
#line (185, 13) - (185, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNullReturnsNull()
            {
#line (189, 5) - (189, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: null");
#line (190, 5) - (196, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (192, 13) - (192, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (194, 13) - (194, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTildeReturnsNull()
            {
#line (198, 5) - (198, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: ~");
#line (199, 5) - (225, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (201, 13) - (201, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
#line hidden
                        break;
                    default:
#line (203, 13) - (203, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoCapsIsFalse()
            {
#line (227, 5) - (227, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: NO");
#line (228, 5) - (235, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (230, 13) - (230, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (231, 13) - (231, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), false));
#line hidden
                        break;
                    default:
#line (233, 13) - (233, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoLowerIsFalse()
            {
#line (237, 5) - (237, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: no");
#line (238, 5) - (245, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (240, 13) - (240, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (241, 13) - (241, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), false));
#line hidden
                        break;
                    default:
#line (243, 13) - (243, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoTitleIsFalse()
            {
#line (247, 5) - (247, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: No");
#line (248, 5) - (255, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (250, 13) - (250, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (251, 13) - (251, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), false));
#line hidden
                        break;
                    default:
#line (253, 13) - (253, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesTitleIsTrue()
            {
#line (257, 5) - (257, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Yes");
#line (258, 5) - (265, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (260, 13) - (260, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (261, 13) - (261, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (263, 13) - (263, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesLowerIsTrue()
            {
#line (267, 5) - (267, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yes");
#line (268, 5) - (275, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (270, 13) - (270, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (271, 13) - (271, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (273, 13) - (273, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnLowerIsTrue()
            {
#line (277, 5) - (277, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: on");
#line (278, 5) - (285, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (280, 13) - (280, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (281, 13) - (281, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (283, 13) - (283, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnTitleIsTrue()
            {
#line (287, 5) - (287, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: On");
#line (288, 5) - (295, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (290, 13) - (290, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (291, 13) - (291, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), true));
#line hidden
                        break;
                    default:
#line (293, 13) - (293, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffLowerIsFalse()
            {
#line (297, 5) - (297, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: off");
#line (298, 5) - (305, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (300, 13) - (300, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (301, 13) - (301, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), false));
#line hidden
                        break;
                    default:
#line (303, 13) - (303, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffCapsIsFalse()
            {
#line (307, 5) - (307, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: OFF");
#line (308, 5) - (318, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (310, 13) - (310, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (311, 13) - (311, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((bool)d["key"]!), false));
#line hidden
                        break;
                    default:
#line (313, 13) - (313, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayMixedCasingStaysString()
            {
#line (320, 5) - (320, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yEs");
#line (321, 5) - (328, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (323, 13) - (323, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (324, 13) - (324, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "yEs"));
#line hidden
                        break;
                    default:
#line (326, 13) - (326, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYStaysString()
            {
#line (330, 5) - (330, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Y");
#line (331, 5) - (338, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (333, 13) - (333, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (334, 13) - (334, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Y"));
#line hidden
                        break;
                    default:
#line (336, 13) - (336, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNStaysString()
            {
#line (340, 5) - (340, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: N");
#line (341, 5) - (350, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (343, 13) - (343, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (344, 13) - (344, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "N"));
#line hidden
                        break;
                    default:
#line (346, 13) - (346, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpQuotesTheBoolFamilySoItRoundTrips()
            {
#line (352, 5) - (352, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeDump("NO"), "'NO'\n"));
#line (353, 5) - (353, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump("NO")), "NO"));
#line (354, 5) - (354, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump("yes")), "yes"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInMapReturnsNestedDict()
            {
#line (360, 5) - (360, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("outer:\n  inner: 42\n");
#line (361, 5) - (371, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (363, 13) - (368, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (365, 21) - (365, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (367, 21) - (367, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (369, 13) - (369, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadListInMapReturnsNestedList()
            {
#line (373, 5) - (373, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("items:\n  - 1\n  - 2\n  - 3\n");
#line (374, 5) - (386, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (376, 13) - (383, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["items"])
#line hidden
                        {
                            case global::Sharpy.IList items:
#line (378, 21) - (378, 44) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(items));
#line (379, 21) - (379, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[0], 1));
#line (380, 21) - (380, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[2], 3));
#line hidden
                                break;
                            default:
#line (382, 21) - (382, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (384, 13) - (384, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInListReturnsListOfDicts()
            {
#line (388, 5) - (388, 88) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("- id: 1\n  name: alpha\n- id: 2\n  name: beta\n");
#line (389, 5) - (403, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (391, 13) - (391, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (392, 13) - (398, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (items[0])
#line hidden
                        {
                            case global::Sharpy.IDict first:
#line (394, 21) - (394, 56) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (395, 21) - (395, 64) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                break;
                            default:
#line (397, 21) - (397, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (399, 13) - (399, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyDocumentReturnsNull()
            {
#line (405, 5) - (405, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWhitespaceOnlyReturnsNull()
            {
#line (409, 5) - (409, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad("   \n  \n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyMappingReturnsEmptyDict()
            {
#line (413, 5) - (413, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{}");
#line (414, 5) - (420, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (416, 13) - (416, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (418, 13) - (418, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptySequenceReturnsEmptyList()
            {
#line (422, 5) - (422, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("[]");
#line (423, 5) - (429, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (425, 13) - (425, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
#line hidden
                        break;
                    default:
#line (427, 13) - (427, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnicodeStringPreservesCharacters()
            {
#line (431, 5) - (431, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: café üñî");
#line (432, 5) - (438, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (434, 13) - (434, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "café üñî"));
#line hidden
                        break;
                    default:
#line (436, 13) - (436, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFlowMappingReturnsDict()
            {
#line (440, 5) - (440, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{a: 1, b: 2}");
#line (441, 5) - (450, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (443, 13) - (443, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (444, 13) - (444, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], 2));
#line hidden
                        break;
                    default:
#line (446, 13) - (446, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpBlockStyleByDefault()
            {
#line (452, 5) - (452, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (453, 5) - (453, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (454, 5) - (454, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (455, 5) - (455, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("a: 1", text);
#line (456, 5) - (456, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.DoesNotContain("{", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFlowStyleProducesInline()
            {
#line (460, 5) - (460, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (461, 5) - (461, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (462, 5) - (462, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (463, 5) - (463, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, defaultFlowStyle: true);
#line (464, 5) - (464, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("{", text);
#line (465, 5) - (465, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("}", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpIndentUsesGivenWidth()
            {
#line (469, 5) - (469, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (470, 5) - (470, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                inner["x"] = 1;
#line (471, 5) - (471, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (472, 5) - (472, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                outer["outer"] = inner;
#line (473, 5) - (473, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(outer, indent: 4);
#line (474, 5) - (474, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("    x: 1", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysTrueSortsAlphabetically()
            {
#line (478, 5) - (478, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (479, 5) - (479, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (480, 5) - (480, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (481, 5) - (481, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (482, 5) - (482, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: true);
#line (483, 5) - (483, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (484, 5) - (484, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (485, 5) - (485, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (486, 5) - (486, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line (487, 5) - (487, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posB < posC);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysFalsePreservesInsertionOrder()
            {
#line (491, 5) - (491, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (492, 5) - (492, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (493, 5) - (493, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (494, 5) - (494, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (495, 5) - (495, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: false);
#line (496, 5) - (496, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (497, 5) - (497, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (498, 5) - (498, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (499, 5) - (499, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posC < posA);
#line (500, 5) - (500, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWidthAffectsLineWrapping()
            {
#line (504, 5) - (504, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (505, 5) - (507, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.Builtins.Range(10))
#line hidden
                {
                    var i = __loopVar_1;
#line (506, 9) - (506, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    items.Append(FormattableString.Invariant($"item-{(i)}"));
#line hidden
                }

#line (507, 5) - (507, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string narrow = yaml.SafeDump(items, width: 20);
#line (508, 5) - (508, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string wide = yaml.SafeDump(items, width: 1000);
#line (509, 5) - (509, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", narrow);
#line (510, 5) - (510, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", wide);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpNullEmitsNullToken()
            {
#line (519, 5) - (519, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(null);
#line (520, 5) - (520, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(text, "null\n...\n"));
#line (521, 5) - (521, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(text));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllTwoDocumentsReturnsBoth()
            {
#line (527, 5) - (527, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\nb: 2\n");
#line (528, 5) - (528, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(docs));
#line (529, 5) - (529, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstElem = docs.GetItemUnchecked(0);
#line (530, 5) - (535, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstElem)
#line hidden
                {
                    case global::Sharpy.IDict first:
#line (532, 13) - (532, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(first["a"], 1));
#line hidden
                        break;
                    default:
#line (534, 13) - (534, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (535, 5) - (535, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object secondElem = docs.GetItemUnchecked(1);
#line (536, 5) - (542, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (secondElem)
#line hidden
                {
                    case global::Sharpy.IDict second:
#line (538, 13) - (538, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(second["b"], 2));
#line hidden
                        break;
                    default:
#line (540, 13) - (540, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllThreeDocumentsReturnsAll()
            {
#line (544, 5) - (544, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("1\n---\n2\n---\n3\n");
#line (545, 5) - (545, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (546, 5) - (546, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(0), 1));
#line (547, 5) - (547, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(1), 2));
#line (548, 5) - (548, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(2), 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllSingleDocumentReturnsOne()
            {
#line (552, 5) - (552, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("key: value\n");
#line (553, 5) - (553, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(docs));
#line (554, 5) - (554, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object elem = docs.GetItemUnchecked(0);
#line (555, 5) - (561, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (elem)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (557, 13) - (557, 51) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                        break;
                    default:
#line (559, 13) - (559, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllEmptyDocumentInStreamYieldsNull()
            {
#line (563, 5) - (563, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\n---\nb: 2\n");
#line (564, 5) - (564, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (565, 5) - (565, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstObj = docs.GetItemUnchecked(0);
#line (566, 5) - (571, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (568, 13) - (568, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (570, 13) - (570, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (571, 5) - (571, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(docs.GetItemUnchecked(1));
#line (572, 5) - (572, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object thirdObj = docs.GetItemUnchecked(2);
#line (573, 5) - (579, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (thirdObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (575, 13) - (575, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (577, 13) - (577, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllMultipleDocumentsProducesSeparators()
            {
#line (581, 5) - (581, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (582, 5) - (582, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (583, 5) - (583, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (584, 5) - (584, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc2["b"] = 2;
#line (585, 5) - (585, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (586, 5) - (586, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (587, 5) - (587, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc2);
#line (588, 5) - (588, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (589, 5) - (589, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("---", text);
#line (590, 5) - (590, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> reparsed = yaml.SafeLoadAll(text);
#line (591, 5) - (591, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(reparsed));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllSingleDocumentNoLeadingSeparator()
            {
#line (595, 5) - (595, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (596, 5) - (596, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (597, 5) - (597, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (598, 5) - (598, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (599, 5) - (599, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (600, 5) - (600, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(text, "---"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFileSafeLoadFileRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (606, 5) - (606, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string path = tmpPath + "/data.yaml";
#line (607, 5) - (607, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (608, 5) - (608, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "file-test";
#line (609, 5) - (609, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["value"] = 7;
#line (610, 5) - (612, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (611, 9) - (611, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeDumpFile(data, fp);
#line hidden
                }

#line (612, 5) - (612, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string name = "";
#line (613, 5) - (613, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int value = 0;
#line (614, 5) - (630, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (615, 9) - (615, 51) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object parsed = yaml.SafeLoadFile(fp2);
#line (616, 9) - (630, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (parsed)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (618, 17) - (623, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["name"])
#line hidden
                            {
                                case string n:
#line (620, 25) - (620, 33) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    name = n;
#line hidden
                                    break;
                                default:
#line (622, 25) - (622, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

#line (623, 17) - (628, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["value"])
#line hidden
                            {
                                case int v:
#line (625, 25) - (625, 34) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    value = v;
#line hidden
                                    break;
                                default:
#line (627, 25) - (627, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (629, 17) - (629, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (630, 5) - (630, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("file-test", name);
#line (631, 5) - (631, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(7, value);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAnchorAndAliasResolvesMappingReference()
            {
#line (637, 5) - (637, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "defaults: &defaults\n  timeout: 30\n  retries: 3\nproduction: *defaults\n";
#line (638, 5) - (638, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (639, 5) - (650, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (641, 13) - (647, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["production"])
#line hidden
                        {
                            case global::Sharpy.IDict production:
#line (643, 21) - (643, 67) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["timeout"], 30));
#line (644, 21) - (644, 66) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["retries"], 3));
#line hidden
                                break;
                            default:
#line (646, 21) - (646, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (648, 13) - (648, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadSimpleAliasDuplicatesValue()
            {
#line (652, 5) - (652, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "first: &val hello\nsecond: *val\n";
#line (653, 5) - (653, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (654, 5) - (661, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (656, 13) - (656, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["first"], "hello"));
#line (657, 13) - (657, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["second"], "hello"));
#line hidden
                        break;
                    default:
#line (659, 13) - (659, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUndefinedAliasThrowsParseError()
            {
#line (663, 5) - (668, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (664, 9) - (664, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("ref: *missing\n");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMalformedYamlThrowsParseError()
            {
#line (670, 5) - (673, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (671, 9) - (671, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnterminatedQuoteThrowsParseError()
            {
#line (675, 5) - (678, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (676, 9) - (676, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: 'unterminated\n");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTabIndentationThrowsParseError()
            {
#line (680, 5) - (683, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (681, 9) - (681, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("a:\n\t- 1\n");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorHasLineAndColumn()
            {
#line (685, 5) - (687, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                global::Sharpy.YAMLParseError exc = null!;
#line hidden
                bool __raised_6 = false;
                try
                {
#line (686, 9) - (686, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError __caught_7)
                {
                    __raised_6 = true;
                    exc = __caught_7;
                }

                if (!__raised_6)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
#line (687, 5) - (687, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Line >= 0);
#line (688, 5) - (688, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Column >= 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorIsYamlError()
            {
#line (692, 5) - (694, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                global::Sharpy.YAMLParseError exc = null!;
#line hidden
                bool __raised_8 = false;
                try
                {
#line (693, 9) - (693, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
#line hidden
                }
                catch (global::Sharpy.YAMLParseError __caught_9)
                {
                    __raised_8 = true;
                    exc = __caught_9;
                }

                if (!__raised_8)
                    throw new global::Sharpy.AssertionError("Expected YAMLParseError to be raised, but no exception was raised");
#line (694, 5) - (694, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(exc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatExponentMatchesPyyaml()
            {
#line (722, 5) - (722, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)));
#line (723, 5) - (723, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+17\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e17d)));
#line (724, 5) - (724, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-1.0e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-1e20d)));
#line (725, 5) - (725, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+100\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e100d)));
#line (727, 5) - (727, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("2.5e-10\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(2.5e-10d)));
#line (728, 5) - (728, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.5e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.5e20d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatLayoutBoundaryMatchesPyyaml()
            {
#line (734, 5) - (734, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+16\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)));
#line (735, 5) - (735, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1000000000000000.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e15d)));
#line (737, 5) - (737, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0001\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-4d)));
#line (738, 5) - (738, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e-05\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-5d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWholeFloatKeepsItsPoint()
            {
#line (744, 5) - (744, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)));
#line (745, 5) - (745, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("5.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(5.0d)));
#line (746, 5) - (746, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(0.0d)));
#line (747, 5) - (747, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-0.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-0.0d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatRoundTripsAsAFloat()
            {
#line (754, 5) - (754, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(yaml.SafeDump(1.0d));
#line (755, 5) - (755, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(parsed);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpFloatMatchesSafeDump()
            {
#line (767, 5) - (767, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e20d)));
#line (768, 5) - (768, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1.0d)));
#line (769, 5) - (769, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e16d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpPlainScalarEndsWithDocumentMarker()
            {
#line (781, 5) - (781, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("hello\n...\n", yaml.SafeDump("hello"));
#line (782, 5) - (782, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0\n...\n", yaml.SafeDump(1.0d));
#line (783, 5) - (783, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("42\n...\n", yaml.SafeDump(42));
#line (784, 5) - (784, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("true\n...\n", yaml.SafeDump(true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpQuotedScalarHasNoDocumentMarker()
            {
#line (790, 5) - (790, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'true'\n", yaml.SafeDump("true"));
#line (791, 5) - (791, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'42'\n", yaml.SafeDump("42"));
#line (793, 5) - (793, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'a: b'\n", yaml.SafeDump("a: b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpCollectionHasNoDocumentMarker()
            {
#line (798, 5) - (798, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> mapping = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (799, 5) - (799, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                mapping["a"] = 1;
#line (800, 5) - (800, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("a: 1\n", yaml.SafeDump(mapping));
#line (802, 5) - (802, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (803, 5) - (803, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                items.Append(1);
#line (804, 5) - (804, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                items.Append(2);
#line (805, 5) - (805, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("- 1\n- 2\n", yaml.SafeDump(items));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpMarksTheSameDocumentsAsSafeDump()
            {
#line (818, 5) - (822, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_10 in new Sharpy.List<string>()
#line hidden
                {
                    "hello",
                    "world",
                    "abc",
                    "true",
                    "a: b",
                    "yes",
                    "0x1A",
                    "#hash",
                    ""
                }

                )
                {
                    var value = __loopVar_10;
#line (819, 9) - (819, 68) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    Xunit.Assert.Equal(yaml.SafeDump(value), yaml.RoundtripDump(value));
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpMarkedScalarStillRoundTrips()
            {
#line (825, 5) - (825, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump("hello")), "hello"));
#line (826, 5) - (826, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump(42)), 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSpecialFloatsKeepYamlSpellings()
            {
#line (831, 5) - (831, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".inf\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("inf"))));
#line (832, 5) - (832, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-.inf\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("-inf"))));
#line (833, 5) - (833, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".nan\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("nan"))));
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

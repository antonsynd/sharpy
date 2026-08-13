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
#line (199, 5) - (208, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
            public void TestSafeLoadNorwayNoCapsStaysString()
            {
#line (210, 5) - (210, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: NO");
#line (211, 5) - (218, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (213, 13) - (213, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (214, 13) - (214, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "NO"));
#line hidden
                        break;
                    default:
#line (216, 13) - (216, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoLowerStaysString()
            {
#line (220, 5) - (220, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: no");
#line (221, 5) - (228, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (223, 13) - (223, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (224, 13) - (224, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "no"));
#line hidden
                        break;
                    default:
#line (226, 13) - (226, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoTitleStaysString()
            {
#line (230, 5) - (230, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: No");
#line (231, 5) - (238, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (233, 13) - (233, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (234, 13) - (234, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "No"));
#line hidden
                        break;
                    default:
#line (236, 13) - (236, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesTitleStaysString()
            {
#line (240, 5) - (240, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Yes");
#line (241, 5) - (248, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (243, 13) - (243, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (244, 13) - (244, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Yes"));
#line hidden
                        break;
                    default:
#line (246, 13) - (246, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesLowerStaysString()
            {
#line (250, 5) - (250, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yes");
#line (251, 5) - (258, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (253, 13) - (253, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (254, 13) - (254, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "yes"));
#line hidden
                        break;
                    default:
#line (256, 13) - (256, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnLowerStaysString()
            {
#line (260, 5) - (260, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: on");
#line (261, 5) - (268, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (263, 13) - (263, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (264, 13) - (264, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "on"));
#line hidden
                        break;
                    default:
#line (266, 13) - (266, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnTitleStaysString()
            {
#line (270, 5) - (270, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: On");
#line (271, 5) - (278, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (273, 13) - (273, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (274, 13) - (274, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "On"));
#line hidden
                        break;
                    default:
#line (276, 13) - (276, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffLowerStaysString()
            {
#line (280, 5) - (280, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: off");
#line (281, 5) - (288, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (283, 13) - (283, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (284, 13) - (284, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "off"));
#line hidden
                        break;
                    default:
#line (286, 13) - (286, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffCapsStaysString()
            {
#line (290, 5) - (290, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: OFF");
#line (291, 5) - (298, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (293, 13) - (293, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (294, 13) - (294, 49) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "OFF"));
#line hidden
                        break;
                    default:
#line (296, 13) - (296, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYStaysString()
            {
#line (300, 5) - (300, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Y");
#line (301, 5) - (308, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (303, 13) - (303, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (304, 13) - (304, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "Y"));
#line hidden
                        break;
                    default:
#line (306, 13) - (306, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNStaysString()
            {
#line (310, 5) - (310, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: N");
#line (311, 5) - (320, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (313, 13) - (313, 46) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (314, 13) - (314, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(((string)d["key"]!), "N"));
#line hidden
                        break;
                    default:
#line (316, 13) - (316, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInMapReturnsNestedDict()
            {
#line (322, 5) - (322, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("outer:\n  inner: 42\n");
#line (323, 5) - (333, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (325, 13) - (330, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["outer"])
#line hidden
                        {
                            case global::Sharpy.IDict inner:
#line (327, 21) - (327, 60) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
#line hidden
                                break;
                            default:
#line (329, 21) - (329, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (331, 13) - (331, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadListInMapReturnsNestedList()
            {
#line (335, 5) - (335, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("items:\n  - 1\n  - 2\n  - 3\n");
#line (336, 5) - (348, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (338, 13) - (345, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["items"])
#line hidden
                        {
                            case global::Sharpy.IList items:
#line (340, 21) - (340, 44) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(items));
#line (341, 21) - (341, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[0], 1));
#line (342, 21) - (342, 53) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[2], 3));
#line hidden
                                break;
                            default:
#line (344, 21) - (344, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (346, 13) - (346, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInListReturnsListOfDicts()
            {
#line (350, 5) - (350, 88) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("- id: 1\n  name: alpha\n- id: 2\n  name: beta\n");
#line (351, 5) - (365, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (353, 13) - (353, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (354, 13) - (360, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (items[0])
#line hidden
                        {
                            case global::Sharpy.IDict first:
#line (356, 21) - (356, 56) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (357, 21) - (357, 64) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
#line hidden
                                break;
                            default:
#line (359, 21) - (359, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (361, 13) - (361, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyDocumentReturnsNull()
            {
#line (367, 5) - (367, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWhitespaceOnlyReturnsNull()
            {
#line (371, 5) - (371, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad("   \n  \n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyMappingReturnsEmptyDict()
            {
#line (375, 5) - (375, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{}");
#line (376, 5) - (382, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (378, 13) - (378, 32) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
                        break;
                    default:
#line (380, 13) - (380, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptySequenceReturnsEmptyList()
            {
#line (384, 5) - (384, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("[]");
#line (385, 5) - (391, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IList items:
#line (387, 13) - (387, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
#line hidden
                        break;
                    default:
#line (389, 13) - (389, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnicodeStringPreservesCharacters()
            {
#line (393, 5) - (393, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: café üñî");
#line (394, 5) - (400, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (396, 13) - (396, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "café üñî"));
#line hidden
                        break;
                    default:
#line (398, 13) - (398, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFlowMappingReturnsDict()
            {
#line (402, 5) - (402, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{a: 1, b: 2}");
#line (403, 5) - (412, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (405, 13) - (405, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (406, 13) - (406, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], 2));
#line hidden
                        break;
                    default:
#line (408, 13) - (408, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpBlockStyleByDefault()
            {
#line (414, 5) - (414, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (415, 5) - (415, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (416, 5) - (416, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (417, 5) - (417, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("a: 1", text);
#line (418, 5) - (418, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.DoesNotContain("{", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFlowStyleProducesInline()
            {
#line (422, 5) - (422, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (423, 5) - (423, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (424, 5) - (424, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (425, 5) - (425, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, defaultFlowStyle: true);
#line (426, 5) - (426, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("{", text);
#line (427, 5) - (427, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("}", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpIndentUsesGivenWidth()
            {
#line (431, 5) - (431, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (432, 5) - (432, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                inner["x"] = 1;
#line (433, 5) - (433, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (434, 5) - (434, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                outer["outer"] = inner;
#line (435, 5) - (435, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(outer, indent: 4);
#line (436, 5) - (436, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("    x: 1", text);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysTrueSortsAlphabetically()
            {
#line (440, 5) - (440, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (441, 5) - (441, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (442, 5) - (442, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (443, 5) - (443, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (444, 5) - (444, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: true);
#line (445, 5) - (445, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (446, 5) - (446, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (447, 5) - (447, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (448, 5) - (448, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line (449, 5) - (449, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posB < posC);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysFalsePreservesInsertionOrder()
            {
#line (453, 5) - (453, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (454, 5) - (454, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (455, 5) - (455, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (456, 5) - (456, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (457, 5) - (457, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: false);
#line (458, 5) - (458, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = global::Sharpy.StringExtensions.Find(text, "c:");
#line (459, 5) - (459, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = global::Sharpy.StringExtensions.Find(text, "a:");
#line (460, 5) - (460, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = global::Sharpy.StringExtensions.Find(text, "b:");
#line (461, 5) - (461, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posC < posA);
#line (462, 5) - (462, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWidthAffectsLineWrapping()
            {
#line (466, 5) - (466, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (467, 5) - (469, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.Builtins.Range(10))
#line hidden
                {
                    var i = __loopVar_1;
#line (468, 9) - (468, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    items.Append(FormattableString.Invariant($"item-{(i)}"));
#line hidden
                }

#line (469, 5) - (469, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string narrow = yaml.SafeDump(items, width: 20);
#line (470, 5) - (470, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string wide = yaml.SafeDump(items, width: 1000);
#line (471, 5) - (471, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", narrow);
#line (472, 5) - (472, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", wide);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpNullEmitsNullToken()
            {
#line (476, 5) - (476, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(null);
#line (477, 5) - (477, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(text));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllTwoDocumentsReturnsBoth()
            {
#line (483, 5) - (483, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\nb: 2\n");
#line (484, 5) - (484, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(docs));
#line (485, 5) - (485, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstElem = docs.GetItemUnchecked(0);
#line (486, 5) - (491, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstElem)
#line hidden
                {
                    case global::Sharpy.IDict first:
#line (488, 13) - (488, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(first["a"], 1));
#line hidden
                        break;
                    default:
#line (490, 13) - (490, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (491, 5) - (491, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object secondElem = docs.GetItemUnchecked(1);
#line (492, 5) - (498, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (secondElem)
#line hidden
                {
                    case global::Sharpy.IDict second:
#line (494, 13) - (494, 48) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(second["b"], 2));
#line hidden
                        break;
                    default:
#line (496, 13) - (496, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllThreeDocumentsReturnsAll()
            {
#line (500, 5) - (500, 75) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("1\n---\n2\n---\n3\n");
#line (501, 5) - (501, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (502, 5) - (502, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(0), 1));
#line (503, 5) - (503, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(1), 2));
#line (504, 5) - (504, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs.GetItemUnchecked(2), 3));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllSingleDocumentReturnsOne()
            {
#line (508, 5) - (508, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("key: value\n");
#line (509, 5) - (509, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(docs));
#line (510, 5) - (510, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object elem = docs.GetItemUnchecked(0);
#line (511, 5) - (517, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (elem)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (513, 13) - (513, 51) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "value"));
#line hidden
                        break;
                    default:
#line (515, 13) - (515, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllEmptyDocumentInStreamYieldsNull()
            {
#line (519, 5) - (519, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\n---\nb: 2\n");
#line (520, 5) - (520, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (521, 5) - (521, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstObj = docs.GetItemUnchecked(0);
#line (522, 5) - (527, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (524, 13) - (524, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (526, 13) - (526, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (527, 5) - (527, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(docs.GetItemUnchecked(1));
#line (528, 5) - (528, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object thirdObj = docs.GetItemUnchecked(2);
#line (529, 5) - (535, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (thirdObj)
#line hidden
                {
                    case global::Sharpy.IDict _:
#line (531, 13) - (531, 18) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
#line hidden
                        break;
                    default:
#line (533, 13) - (533, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllMultipleDocumentsProducesSeparators()
            {
#line (537, 5) - (537, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (538, 5) - (538, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (539, 5) - (539, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc2 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (540, 5) - (540, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc2["b"] = 2;
#line (541, 5) - (541, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (542, 5) - (542, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (543, 5) - (543, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc2);
#line (544, 5) - (544, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (545, 5) - (545, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("---", text);
#line (546, 5) - (546, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> reparsed = yaml.SafeLoadAll(text);
#line (547, 5) - (547, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(reparsed));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllSingleDocumentNoLeadingSeparator()
            {
#line (551, 5) - (551, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (552, 5) - (552, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (553, 5) - (553, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
#line hidden
                {
                };
#line (554, 5) - (554, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (555, 5) - (555, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (556, 5) - (556, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith(text, "---"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFileSafeLoadFileRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (562, 5) - (562, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string path = tmpPath + "/data.yaml";
#line (563, 5) - (563, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (564, 5) - (564, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "file-test";
#line (565, 5) - (565, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["value"] = 7;
#line (566, 5) - (568, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (567, 9) - (567, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeDumpFile(data, fp);
#line hidden
                }

#line (568, 5) - (568, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string name = "";
#line (569, 5) - (569, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int value = 0;
#line (570, 5) - (586, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (571, 9) - (571, 51) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object parsed = yaml.SafeLoadFile(fp2);
#line (572, 9) - (586, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (parsed)
#line hidden
                    {
                        case global::Sharpy.IDict d:
#line (574, 17) - (579, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["name"])
#line hidden
                            {
                                case string n:
#line (576, 25) - (576, 33) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    name = n;
#line hidden
                                    break;
                                default:
#line (578, 25) - (578, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

#line (579, 17) - (584, 1) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["value"])
#line hidden
                            {
                                case int v:
#line (581, 25) - (581, 34) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    value = v;
#line hidden
                                    break;
                                default:
#line (583, 25) - (583, 38) 36 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
#line hidden
                                    break;
                            }

                            break;
                        default:
#line (585, 17) - (585, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (586, 5) - (586, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("file-test", name);
#line (587, 5) - (587, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(7, value);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAnchorAndAliasResolvesMappingReference()
            {
#line (593, 5) - (593, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "defaults: &defaults\n  timeout: 30\n  retries: 3\nproduction: *defaults\n";
#line (594, 5) - (594, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (595, 5) - (606, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (597, 13) - (603, 1) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["production"])
#line hidden
                        {
                            case global::Sharpy.IDict production:
#line (599, 21) - (599, 67) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["timeout"], 30));
#line (600, 21) - (600, 66) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["retries"], 3));
#line hidden
                                break;
                            default:
#line (602, 21) - (602, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (604, 13) - (604, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadSimpleAliasDuplicatesValue()
            {
#line (608, 5) - (608, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "first: &val hello\nsecond: *val\n";
#line (609, 5) - (609, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (610, 5) - (617, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
#line hidden
                {
                    case global::Sharpy.IDict d:
#line (612, 13) - (612, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["first"], "hello"));
#line (613, 13) - (613, 54) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["second"], "hello"));
#line hidden
                        break;
                    default:
#line (615, 13) - (615, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUndefinedAliasThrowsParseError()
            {
#line (619, 5) - (624, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (620, 9) - (620, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (626, 5) - (629, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (627, 9) - (627, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (631, 5) - (634, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (632, 9) - (632, 47) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (636, 5) - (639, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (637, 9) - (637, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (641, 5) - (643, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                global::Sharpy.YAMLParseError exc = null!;
#line hidden
                bool __raised_6 = false;
                try
                {
#line (642, 9) - (642, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (643, 5) - (643, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Line >= 0);
#line (644, 5) - (644, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Column >= 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorIsYamlError()
            {
#line (648, 5) - (650, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                global::Sharpy.YAMLParseError exc = null!;
#line hidden
                bool __raised_8 = false;
                try
                {
#line (649, 9) - (649, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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
#line (650, 5) - (650, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(exc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatExponentMatchesPyyaml()
            {
#line (678, 5) - (678, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)));
#line (679, 5) - (679, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+17\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e17d)));
#line (680, 5) - (680, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-1.0e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-1e20d)));
#line (681, 5) - (681, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+100\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e100d)));
#line (683, 5) - (683, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("2.5e-10\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(2.5e-10d)));
#line (684, 5) - (684, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.5e+20\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.5e20d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatLayoutBoundaryMatchesPyyaml()
            {
#line (690, 5) - (690, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e+16\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)));
#line (691, 5) - (691, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1000000000000000.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e15d)));
#line (693, 5) - (693, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0001\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-4d)));
#line (694, 5) - (694, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0e-05\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e-5d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWholeFloatKeepsItsPoint()
            {
#line (700, 5) - (700, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)));
#line (701, 5) - (701, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("5.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(5.0d)));
#line (702, 5) - (702, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("0.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(0.0d)));
#line (703, 5) - (703, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-0.0\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(-0.0d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFloatRoundTripsAsAFloat()
            {
#line (710, 5) - (710, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(yaml.SafeDump(1.0d));
#line (711, 5) - (711, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<double>(parsed);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpFloatMatchesSafeDump()
            {
#line (723, 5) - (723, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e20d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e20d)));
#line (724, 5) - (724, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1.0d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1.0d)));
#line (725, 5) - (725, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.StringExtensions.Strip(yaml.SafeDump(1e16d)), global::Sharpy.StringExtensions.Strip(yaml.RoundtripDump(1e16d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpPlainScalarEndsWithDocumentMarker()
            {
#line (737, 5) - (737, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("hello\n...\n", yaml.SafeDump("hello"));
#line (738, 5) - (738, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("1.0\n...\n", yaml.SafeDump(1.0d));
#line (739, 5) - (739, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("42\n...\n", yaml.SafeDump(42));
#line (740, 5) - (740, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("true\n...\n", yaml.SafeDump(true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpQuotedScalarHasNoDocumentMarker()
            {
#line (746, 5) - (746, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'true'\n", yaml.SafeDump("true"));
#line (747, 5) - (747, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'42'\n", yaml.SafeDump("42"));
#line (749, 5) - (749, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("'a: b'\n", yaml.SafeDump("a: b"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpCollectionHasNoDocumentMarker()
            {
#line (754, 5) - (754, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> mapping = new Sharpy.Dict<string, object>()
#line hidden
                {
                };
#line (755, 5) - (755, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                mapping["a"] = 1;
#line (756, 5) - (756, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("a: 1\n", yaml.SafeDump(mapping));
#line (758, 5) - (758, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
#line hidden
                {
                };
#line (759, 5) - (759, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                items.Append(1);
#line (760, 5) - (760, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                items.Append(2);
#line (761, 5) - (761, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("- 1\n- 2\n", yaml.SafeDump(items));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpMarksTheSameDocumentsAsSafeDump()
            {
#line (774, 5) - (778, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_10 in new Sharpy.List<string>()
#line hidden
                {
                    "hello",
                    "world",
                    "abc"
                }

                )
                {
                    var value = __loopVar_10;
#line (775, 9) - (775, 68) 20 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    Xunit.Assert.Equal(yaml.SafeDump(value), yaml.RoundtripDump(value));
#line hidden
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpMarkedScalarStillRoundTrips()
            {
#line (781, 5) - (781, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump("hello")), "hello"));
#line (782, 5) - (782, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(yaml.SafeLoad(yaml.SafeDump(42)), 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSpecialFloatsKeepYamlSpellings()
            {
#line (787, 5) - (787, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(".inf\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("inf"))));
#line (788, 5) - (788, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("-.inf\n...", global::Sharpy.StringExtensions.Strip(yaml.SafeDump(global::Sharpy.Builtins.Float("-inf"))));
#line (789, 5) - (789, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
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

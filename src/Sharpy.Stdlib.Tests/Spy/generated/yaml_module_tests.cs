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
#line (21, 5) - (21, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (22, 5) - (22, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "test";
#line (23, 5) - (23, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["count"] = 42;
#line (24, 5) - (24, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (25, 5) - (25, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (26, 5) - (33, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
                {
                    case global::Sharpy.IDict result:
#line (28, 13) - (28, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["name"], "test"));
#line (29, 13) - (29, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result["count"], 42));
                        break;
                    default:
#line (31, 13) - (31, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundTripListPreservesData()
            {
#line (35, 5) - (35, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> data = new Sharpy.List<object>()
                {
                };
#line (36, 5) - (36, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(1);
#line (37, 5) - (37, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append("two");
#line (38, 5) - (38, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(true);
#line (39, 5) - (39, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data.Append(null);
#line (40, 5) - (40, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (41, 5) - (41, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object parsed = yaml.SafeLoad(text);
#line (42, 5) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (parsed)
                {
                    case global::Sharpy.IList result:
#line (44, 13) - (44, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[0], 1));
#line (45, 13) - (45, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[1], "two"));
#line (46, 13) - (46, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(result[2], true));
#line (47, 13) - (47, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(result[3]);
                        break;
                    default:
#line (49, 13) - (49, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadStringReturnsString()
            {
#line (55, 5) - (55, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: hello");
#line (56, 5) - (62, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (58, 13) - (58, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "hello"));
                        break;
                    default:
#line (60, 13) - (60, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadQuotedStringReturnsString()
            {
#line (64, 5) - (64, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: \"hello world\"");
#line (65, 5) - (71, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (67, 13) - (67, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "hello world"));
                        break;
                    default:
#line (69, 13) - (69, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadIntReturnsInt()
            {
#line (73, 5) - (73, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 42");
#line (74, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (76, 13) - (76, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<int>(d["key"]);
#line (77, 13) - (77, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], 42));
                        break;
                    default:
#line (79, 13) - (79, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNegativeIntReturnsInt()
            {
#line (83, 5) - (83, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: -7");
#line (84, 5) - (90, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (86, 13) - (86, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], -7));
                        break;
                    default:
#line (88, 13) - (88, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadLargeIntReturnsLong()
            {
#line (92, 5) - (92, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 9999999999");
#line (93, 5) - (101, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (95, 13) - (95, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<long>(d["key"]);
#line (96, 13) - (96, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        long value = 9999999999L;
#line (97, 13) - (97, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], value));
                        break;
                    default:
#line (99, 13) - (99, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFloatReturnsDouble()
            {
#line (106, 5) - (106, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 3.14");
#line (107, 5) - (118, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (109, 13) - (109, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
#line (110, 13) - (115, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["key"])
                        {
                            case double fv:
#line (112, 21) - (112, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3.14d, fv, 1e-4d);
                                break;
                            default:
#line (114, 21) - (114, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (116, 13) - (116, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWholeNumberFloatStaysFloatTyped()
            {
#line (120, 5) - (120, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: 2.0");
#line (121, 5) - (132, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (123, 13) - (123, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<double>(d["key"]);
#line (124, 13) - (129, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["key"])
                        {
                            case double fv:
#line (126, 21) - (126, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(2.0d, fv, 1e-4d);
                                break;
                            default:
#line (128, 21) - (128, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (130, 13) - (130, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolTrueReturnsBool()
            {
#line (134, 5) - (134, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: true");
#line (135, 5) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (137, 13) - (137, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<bool>(d["key"]);
#line (138, 13) - (138, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], true));
                        break;
                    default:
#line (140, 13) - (140, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadBoolFalseReturnsBool()
            {
#line (144, 5) - (144, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: false");
#line (145, 5) - (151, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (147, 13) - (147, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], false));
                        break;
                    default:
#line (149, 13) - (149, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNullReturnsNull()
            {
#line (153, 5) - (153, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: null");
#line (154, 5) - (160, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (156, 13) - (156, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
                        break;
                    default:
#line (158, 13) - (158, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTildeReturnsNull()
            {
#line (162, 5) - (162, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: ~");
#line (163, 5) - (172, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (165, 13) - (165, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Null(d["key"]);
                        break;
                    default:
#line (167, 13) - (167, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoCapsStaysString()
            {
#line (174, 5) - (174, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: NO");
#line (175, 5) - (182, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (177, 13) - (177, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (178, 13) - (178, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "NO"));
                        break;
                    default:
#line (180, 13) - (180, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoLowerStaysString()
            {
#line (184, 5) - (184, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: no");
#line (185, 5) - (192, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (187, 13) - (187, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (188, 13) - (188, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "no"));
                        break;
                    default:
#line (190, 13) - (190, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNoTitleStaysString()
            {
#line (194, 5) - (194, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: No");
#line (195, 5) - (202, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (197, 13) - (197, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (198, 13) - (198, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "No"));
                        break;
                    default:
#line (200, 13) - (200, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesTitleStaysString()
            {
#line (204, 5) - (204, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Yes");
#line (205, 5) - (212, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (207, 13) - (207, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (208, 13) - (208, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "Yes"));
                        break;
                    default:
#line (210, 13) - (210, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYesLowerStaysString()
            {
#line (214, 5) - (214, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: yes");
#line (215, 5) - (222, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (217, 13) - (217, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (218, 13) - (218, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "yes"));
                        break;
                    default:
#line (220, 13) - (220, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnLowerStaysString()
            {
#line (224, 5) - (224, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: on");
#line (225, 5) - (232, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (227, 13) - (227, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (228, 13) - (228, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "on"));
                        break;
                    default:
#line (230, 13) - (230, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOnTitleStaysString()
            {
#line (234, 5) - (234, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: On");
#line (235, 5) - (242, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (237, 13) - (237, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (238, 13) - (238, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "On"));
                        break;
                    default:
#line (240, 13) - (240, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffLowerStaysString()
            {
#line (244, 5) - (244, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: off");
#line (245, 5) - (252, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (247, 13) - (247, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (248, 13) - (248, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "off"));
                        break;
                    default:
#line (250, 13) - (250, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayOffCapsStaysString()
            {
#line (254, 5) - (254, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: OFF");
#line (255, 5) - (262, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (257, 13) - (257, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (258, 13) - (258, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "OFF"));
                        break;
                    default:
#line (260, 13) - (260, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayYStaysString()
            {
#line (264, 5) - (264, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: Y");
#line (265, 5) - (272, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (267, 13) - (267, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (268, 13) - (268, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "Y"));
                        break;
                    default:
#line (270, 13) - (270, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadNorwayNStaysString()
            {
#line (274, 5) - (274, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: N");
#line (275, 5) - (284, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (277, 13) - (277, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(d["key"]);
#line (278, 13) - (278, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "N"));
                        break;
                    default:
#line (280, 13) - (280, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInMapReturnsNestedDict()
            {
#line (286, 5) - (286, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("outer:\n  inner: 42\n");
#line (287, 5) - (297, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (289, 13) - (294, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["outer"])
                        {
                            case global::Sharpy.IDict inner:
#line (291, 21) - (291, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(inner["inner"], 42));
                                break;
                            default:
#line (293, 21) - (293, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (295, 13) - (295, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadListInMapReturnsNestedList()
            {
#line (299, 5) - (299, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("items:\n  - 1\n  - 2\n  - 3\n");
#line (300, 5) - (312, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (302, 13) - (309, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["items"])
                        {
                            case global::Sharpy.IList items:
#line (304, 21) - (304, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(items));
#line (305, 21) - (305, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[0], 1));
#line (306, 21) - (306, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(items[2], 3));
                                break;
                            default:
#line (308, 21) - (308, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (310, 13) - (310, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMapInListReturnsListOfDicts()
            {
#line (314, 5) - (314, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("- id: 1\n  name: alpha\n- id: 2\n  name: beta\n");
#line (315, 5) - (329, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IList items:
#line (317, 13) - (317, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
#line (318, 13) - (324, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (items[0])
                        {
                            case global::Sharpy.IDict first:
#line (320, 21) - (320, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["id"], 1));
#line (321, 21) - (321, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(first["name"], "alpha"));
                                break;
                            default:
#line (323, 21) - (323, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (325, 13) - (325, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyDocumentReturnsNull()
            {
#line (331, 5) - (331, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(""));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadWhitespaceOnlyReturnsNull()
            {
#line (335, 5) - (335, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad("   \n  \n"));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptyMappingReturnsEmptyDict()
            {
#line (339, 5) - (339, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{}");
#line (340, 5) - (346, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (342, 13) - (342, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
                        break;
                    default:
#line (344, 13) - (344, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadEmptySequenceReturnsEmptyList()
            {
#line (348, 5) - (348, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("[]");
#line (349, 5) - (355, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IList items:
#line (351, 13) - (351, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(items));
                        break;
                    default:
#line (353, 13) - (353, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnicodeStringPreservesCharacters()
            {
#line (357, 5) - (357, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("key: café üñî");
#line (358, 5) - (364, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (360, 13) - (360, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "café üñî"));
                        break;
                    default:
#line (362, 13) - (362, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadFlowMappingReturnsDict()
            {
#line (366, 5) - (366, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad("{a: 1, b: 2}");
#line (367, 5) - (376, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (369, 13) - (369, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
#line (370, 13) - (370, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["b"], 2));
                        break;
                    default:
#line (372, 13) - (372, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpBlockStyleByDefault()
            {
#line (378, 5) - (378, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (379, 5) - (379, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (380, 5) - (380, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data);
#line (381, 5) - (381, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("a: 1", text);
#line (382, 5) - (382, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.DoesNotContain("{", text);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFlowStyleProducesInline()
            {
#line (386, 5) - (386, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (387, 5) - (387, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (388, 5) - (388, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (389, 5) - (389, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, defaultFlowStyle: true);
#line (390, 5) - (390, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("{", text);
#line (391, 5) - (391, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("}", text);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpIndentUsesGivenWidth()
            {
#line (395, 5) - (395, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> inner = new Sharpy.Dict<string, object>()
                {
                };
#line (396, 5) - (396, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                inner["x"] = 1;
#line (397, 5) - (397, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> outer = new Sharpy.Dict<string, object>()
                {
                };
#line (398, 5) - (398, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                outer["outer"] = inner;
#line (399, 5) - (399, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(outer, indent: 4);
#line (400, 5) - (400, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("    x: 1", text);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysTrueSortsAlphabetically()
            {
#line (404, 5) - (404, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (405, 5) - (405, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (406, 5) - (406, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (407, 5) - (407, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (408, 5) - (408, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: true);
#line (409, 5) - (409, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = text.Find("a:");
#line (410, 5) - (410, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = text.Find("b:");
#line (411, 5) - (411, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = text.Find("c:");
#line (412, 5) - (412, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
#line (413, 5) - (413, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posB < posC);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpSortKeysFalsePreservesInsertionOrder()
            {
#line (417, 5) - (417, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (418, 5) - (418, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["c"] = 3;
#line (419, 5) - (419, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["a"] = 1;
#line (420, 5) - (420, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["b"] = 2;
#line (421, 5) - (421, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(data, sortKeys: false);
#line (422, 5) - (422, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posC = text.Find("c:");
#line (423, 5) - (423, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posA = text.Find("a:");
#line (424, 5) - (424, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int posB = text.Find("b:");
#line (425, 5) - (425, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posC < posA);
#line (426, 5) - (426, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(posA < posB);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpWidthAffectsLineWrapping()
            {
#line (430, 5) - (430, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object> items = new Sharpy.List<object>()
                {
                };
#line (431, 5) - (433, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                foreach (var __loopVar_0 in global::Sharpy.Builtins.Range(10))
                {
                    var i = __loopVar_0;
#line (432, 9) - (432, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    items.Append(FormattableString.Invariant($"item-{(i)}"));
                }

#line (433, 5) - (433, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string narrow = yaml.SafeDump(items, width: 20);
#line (434, 5) - (434, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string wide = yaml.SafeDump(items, width: 1000);
#line (435, 5) - (435, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", narrow);
#line (436, 5) - (436, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("item-0", wide);
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpNullEmitsNullToken()
            {
#line (440, 5) - (440, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDump(null);
#line (441, 5) - (441, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(yaml.SafeLoad(text));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllTwoDocumentsReturnsBoth()
            {
#line (447, 5) - (447, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\nb: 2\n");
#line (448, 5) - (448, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(docs));
#line (449, 5) - (449, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstElem = docs[0];
#line (450, 5) - (455, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstElem)
                {
                    case global::Sharpy.IDict first:
#line (452, 13) - (452, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(first["a"], 1));
                        break;
                    default:
#line (454, 13) - (454, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (455, 5) - (455, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object secondElem = docs[1];
#line (456, 5) - (462, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (secondElem)
                {
                    case global::Sharpy.IDict second:
#line (458, 13) - (458, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(second["b"], 2));
                        break;
                    default:
#line (460, 13) - (460, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllThreeDocumentsReturnsAll()
            {
#line (464, 5) - (464, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("1\n---\n2\n---\n3\n");
#line (465, 5) - (465, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (466, 5) - (466, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs[0], 1));
#line (467, 5) - (467, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs[1], 2));
#line (468, 5) - (468, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(@operator.Eq(docs[2], 3));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllSingleDocumentReturnsOne()
            {
#line (472, 5) - (472, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("key: value\n");
#line (473, 5) - (473, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(docs));
#line (474, 5) - (474, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object elem = docs[0];
#line (475, 5) - (481, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (elem)
                {
                    case global::Sharpy.IDict d:
#line (477, 13) - (477, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["key"], "value"));
                        break;
                    default:
#line (479, 13) - (479, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAllEmptyDocumentInStreamYieldsNull()
            {
#line (483, 5) - (483, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> docs = yaml.SafeLoadAll("a: 1\n---\n---\nb: 2\n");
#line (484, 5) - (484, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(docs));
#line (485, 5) - (485, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object firstObj = docs[0];
#line (486, 5) - (491, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (firstObj)
                {
                    case global::Sharpy.IDict _:
#line (488, 13) - (488, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
                        break;
                    default:
#line (490, 13) - (490, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (491, 5) - (491, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Null(docs[1]);
#line (492, 5) - (492, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object thirdObj = docs[2];
#line (493, 5) - (499, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (thirdObj)
                {
                    case global::Sharpy.IDict _:
#line (495, 13) - (495, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        ;
                        break;
                    default:
#line (497, 13) - (497, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllMultipleDocumentsProducesSeparators()
            {
#line (501, 5) - (501, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
                {
                };
#line (502, 5) - (502, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (503, 5) - (503, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc2 = new Sharpy.Dict<string, object>()
                {
                };
#line (504, 5) - (504, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc2["b"] = 2;
#line (505, 5) - (505, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
                {
                };
#line (506, 5) - (506, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (507, 5) - (507, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc2);
#line (508, 5) - (508, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (509, 5) - (509, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Contains("---", text);
#line (510, 5) - (510, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> reparsed = yaml.SafeLoadAll(text);
#line (511, 5) - (511, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(reparsed));
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpAllSingleDocumentNoLeadingSeparator()
            {
#line (515, 5) - (515, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> doc1 = new Sharpy.Dict<string, object>()
                {
                };
#line (516, 5) - (516, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                doc1["a"] = 1;
#line (517, 5) - (517, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.List<object?> documents = new Sharpy.List<object?>()
                {
                };
#line (518, 5) - (518, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                documents.Append(doc1);
#line (519, 5) - (519, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = yaml.SafeDumpAll(documents);
#line (520, 5) - (520, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.False(text.Startswith("---"));
            }

            [Xunit.FactAttribute]
            public void TestSafeDumpFileSafeLoadFileRoundTrip()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (526, 5) - (526, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string path = tmpPath + "/data.yaml";
#line (527, 5) - (527, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (528, 5) - (528, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["name"] = "file-test";
#line (529, 5) - (529, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                data["value"] = 7;
#line (530, 5) - (532, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (531, 9) - (531, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeDumpFile(data, fp);
                }

#line (532, 5) - (532, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string name = "";
#line (533, 5) - (533, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                int value = 0;
#line (534, 5) - (550, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
                {
#line (535, 9) - (535, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    object parsed = yaml.SafeLoadFile(fp2);
#line (536, 9) - (550, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    switch (parsed)
                    {
                        case global::Sharpy.IDict d:
#line (538, 17) - (543, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["name"])
                            {
                                case string n:
#line (540, 25) - (540, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    name = n;
                                    break;
                                default:
#line (542, 25) - (542, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
                                    break;
                            }

#line (543, 17) - (548, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            switch (d["value"])
                            {
                                case int v:
#line (545, 25) - (545, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    value = v;
                                    break;
                                default:
#line (547, 25) - (547, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                    Xunit.Assert.True(false);
                                    break;
                            }

                            break;
                        default:
#line (549, 17) - (549, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }

#line (550, 5) - (550, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal("file-test", name);
#line (551, 5) - (551, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Equal(7, value);
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadAnchorAndAliasResolvesMappingReference()
            {
#line (557, 5) - (557, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "defaults: &defaults\n  timeout: 30\n  retries: 3\nproduction: *defaults\n";
#line (558, 5) - (558, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (559, 5) - (570, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (561, 13) - (567, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        switch (d["production"])
                        {
                            case global::Sharpy.IDict production:
#line (563, 21) - (563, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["timeout"], 30));
#line (564, 21) - (564, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(@operator.Eq(production["retries"], 3));
                                break;
                            default:
#line (566, 21) - (566, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (568, 13) - (568, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadSimpleAliasDuplicatesValue()
            {
#line (572, 5) - (572, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                string text = "first: &val hello\nsecond: *val\n";
#line (573, 5) - (573, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                object result = yaml.SafeLoad(text);
#line (574, 5) - (581, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (576, 13) - (576, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["first"], "hello"));
#line (577, 13) - (577, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["second"], "hello"));
                        break;
                    default:
#line (579, 13) - (579, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUndefinedAliasThrowsParseError()
            {
#line (583, 5) - (588, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (584, 9) - (584, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("ref: *missing\n");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadMalformedYamlThrowsParseError()
            {
#line (590, 5) - (593, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (591, 9) - (591, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadUnterminatedQuoteThrowsParseError()
            {
#line (595, 5) - (598, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (596, 9) - (596, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: 'unterminated\n");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTabIndentationThrowsParseError()
            {
#line (600, 5) - (603, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (601, 9) - (601, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("a:\n\t- 1\n");
                }));
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorHasLineAndColumn()
            {
#line (605, 5) - (607, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (606, 9) - (606, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
                }));
#line (607, 5) - (607, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Line >= 0);
#line (608, 5) - (608, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc.Column >= 0);
            }

            [Xunit.FactAttribute]
            public void TestYamlParseErrorIsYamlError()
            {
#line (612, 5) - (614, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                var exc = Xunit.Assert.Throws<global::Sharpy.YAMLParseError>((global::System.Action)(() =>
                {
#line (613, 9) - (613, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                    yaml.SafeLoad("key: [1, 2");
                }));
#line (614, 5) - (614, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_module_tests.spy"
                Xunit.Assert.True(exc is global::Sharpy.YAMLError);
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}

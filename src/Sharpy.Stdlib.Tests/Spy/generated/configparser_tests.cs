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
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Configparser.ConfigparserTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Configparser
    {
        [global::Sharpy.SharpyModule("configparser.configparser_tests")]
        public static partial class ConfigparserTests
        {
        }
    }

    public static partial class Configparser
    {
        public partial class ConfigparserTestsTests
        {
            [Xunit.FactAttribute]
            public void TestReadStringBasicKeyValue()
            {
#line (10, 5) - (10, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (11, 5) - (11, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (12, 5) - (12, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringColonDelimiter()
            {
#line (16, 5) - (16, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (17, 5) - (17, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey : value");
#line (18, 5) - (18, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringNoSpaces()
            {
#line (22, 5) - (22, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (23, 5) - (23, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey=value");
#line (24, 5) - (24, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringMultilineValue()
            {
#line (28, 5) - (28, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (29, 5) - (29, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = line1\n  line2\n  line3");
#line (30, 5) - (30, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("line1\nline2\nline3", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringHashComments()
            {
#line (34, 5) - (34, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (35, 5) - (35, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\n# comment\nkey = value");
#line (36, 5) - (36, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line (37, 5) - (37, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(config.Options("section")));
            }

            [Xunit.FactAttribute]
            public void TestReadStringSemicolonComments()
            {
#line (41, 5) - (41, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (42, 5) - (42, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\n; comment\nkey = value");
#line (43, 5) - (43, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringMultipleSections()
            {
#line (47, 5) - (47, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (48, 5) - (48, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section1]\nkey1 = val1\n\n[section2]\nkey2 = val2");
#line (49, 5) - (49, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(config.Sections()));
#line (50, 5) - (50, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val1", config.Get("section1", "key1"));
#line (51, 5) - (51, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val2", config.Get("section2", "key2"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringEmptyValue()
            {
#line (55, 5) - (55, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (56, 5) - (56, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey =");
#line (57, 5) - (57, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestReadStringWhitespaceInSectionName()
            {
#line (61, 5) - (61, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (62, 5) - (62, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[ section ]\nkey = value");
#line (63, 5) - (63, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get(" section ", "key"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultFallback()
            {
#line (69, 5) - (69, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (70, 5) - (70, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nfallback = yes\n\n[section]\nkey = value");
#line (71, 5) - (71, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("yes", config.Get("section", "fallback"));
#line (72, 5) - (72, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultOverriddenBySection()
            {
#line (76, 5) - (76, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (77, 5) - (77, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey = default\n\n[section]\nkey = override");
#line (78, 5) - (78, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("override", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultsReturnsDefaultValues()
            {
#line (82, 5) - (82, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (83, 5) - (83, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey1 = val1\nkey2 = val2");
#line (84, 5) - (84, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var defaults = config.Defaults();
#line (85, 5) - (85, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(defaults.ContainsKey("key1"));
#line (86, 5) - (86, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(defaults.ContainsKey("key2"));
            }

            [Xunit.FactAttribute]
            public void TestCaseInsensitiveKeys()
            {
#line (92, 5) - (92, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (93, 5) - (93, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nMyKey = myvalue");
#line (94, 5) - (94, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("myvalue", config.Get("section", "mykey"));
#line (95, 5) - (95, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("myvalue", config.Get("section", "MYKEY"));
            }

            [Xunit.FactAttribute]
            public void TestCaseSensitiveSections()
            {
#line (99, 5) - (99, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (100, 5) - (100, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[Section]\nkey = val");
#line (101, 5) - (101, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("Section"));
#line (102, 5) - (102, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("section"));
            }

            [Xunit.FactAttribute]
            public void TestHasSectionReturnsFalseForDefault()
            {
#line (106, 5) - (106, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (107, 5) - (107, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey = val\n\n[section]\nkey2 = val2");
#line (108, 5) - (108, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("DEFAULT"));
#line (109, 5) - (109, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section"));
            }

            [Xunit.FactAttribute]
            public void TestHasOptionChecksSectionAndDefault()
            {
#line (113, 5) - (113, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (114, 5) - (114, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\ndefault_key = val\n\n[section]\nsection_key = val2");
#line (115, 5) - (115, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "section_key"));
#line (116, 5) - (116, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "default_key"));
#line (117, 5) - (117, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasOption("section", "nonexistent"));
            }

            [Xunit.FactAttribute]
            public void TestAddSectionAndSet()
            {
#line (123, 5) - (123, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (124, 5) - (124, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("new_section");
#line (125, 5) - (125, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("new_section", "key", "value");
#line (126, 5) - (126, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("new_section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestAddSectionDuplicateThrows()
            {
#line (130, 5) - (130, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (131, 5) - (131, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (132, 5) - (135, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.DuplicateSectionError>((global::System.Action)(() =>
                {
#line (133, 9) - (133, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.AddSection("section");
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddSectionDefaultThrows()
            {
#line (137, 5) - (137, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (138, 5) - (141, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (139, 9) - (139, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.AddSection("DEFAULT");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetNoSectionThrows()
            {
#line (143, 5) - (143, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (144, 5) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.NoSectionError>((global::System.Action)(() =>
                {
#line (145, 9) - (145, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("nonexistent", "key");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetNoOptionThrows()
            {
#line (149, 5) - (149, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (150, 5) - (150, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (151, 5) - (154, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.NoOptionError>((global::System.Action)(() =>
                {
#line (152, 9) - (152, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "missing");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetFallbackReturned()
            {
#line (156, 5) - (156, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (157, 5) - (157, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (158, 5) - (158, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("default", config.Get("section", "missing", fallback: "default"));
            }

            [Xunit.FactAttribute]
            public void TestSetNoSectionThrows()
            {
#line (162, 5) - (162, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (163, 5) - (166, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.NoSectionError>((global::System.Action)(() =>
                {
#line (164, 9) - (164, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Set("nonexistent", "key", "value");
                }));
            }

            [Xunit.FactAttribute]
            public void TestRemoveOption()
            {
#line (168, 5) - (168, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (169, 5) - (169, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = value1\nkey2 = value2");
#line (170, 5) - (170, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.RemoveOption("section", "key1"));
#line (171, 5) - (171, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasOption("section", "key1"));
#line (172, 5) - (172, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "key2"));
            }

            [Xunit.FactAttribute]
            public void TestRemoveSection()
            {
#line (176, 5) - (176, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (177, 5) - (177, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section1]\nkey = val\n\n[section2]\nkey = val");
#line (178, 5) - (178, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.RemoveSection("section1"));
#line (179, 5) - (179, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("section1"));
#line (180, 5) - (180, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section2"));
            }

            [Xunit.FactAttribute]
            public void TestOptionsIncludesDefaults()
            {
#line (184, 5) - (184, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (185, 5) - (185, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nd = 1\n\n[section]\ns = 2");
#line (186, 5) - (186, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var options = config.Options("section");
#line (187, 5) - (187, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("d", options);
#line (188, 5) - (188, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("s", options);
            }

            [Xunit.FactAttribute]
            public void TestItemsIncludesDefaults()
            {
#line (192, 5) - (192, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (193, 5) - (193, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\ncolor = red\n\n[section]\nsize = large");
#line (194, 5) - (194, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var items = config.Items("section");
#line (195, 5) - (195, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(items.ContainsKey("color"));
#line (196, 5) - (196, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(items.ContainsKey("size"));
#line (197, 5) - (197, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("red", items["color"]);
#line (198, 5) - (198, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("large", items["size"]);
            }

            [Xunit.FactAttribute]
            public void TestMissingSectionHeaderErrorBeforeAnySection()
            {
#line (202, 5) - (202, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (203, 5) - (208, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.MissingSectionHeaderError>((global::System.Action)(() =>
                {
#line (204, 9) - (204, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.ReadString("key = value");
                }));
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccess()
            {
#line (210, 5) - (210, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (211, 5) - (211, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (212, 5) - (212, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config["section"]["key"]);
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccessSet()
            {
#line (216, 5) - (216, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (217, 5) - (217, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (218, 5) - (218, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config["section"]["key"] = "new_value";
#line (219, 5) - (219, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("new_value", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccessNoSectionThrows()
            {
#line (223, 5) - (223, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (224, 5) - (227, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.NoSectionError>((global::System.Action)(() =>
                {
#line (225, 9) - (225, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config["nonexistent"].Keys();
                }));
            }

            [Xunit.FactAttribute]
            public void TestSectionProxyKeys()
            {
#line (229, 5) - (229, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (230, 5) - (230, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = val1\nkey2 = val2");
#line (231, 5) - (231, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var keys = config["section"].Keys();
#line (232, 5) - (232, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key1", keys);
#line (233, 5) - (233, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key2", keys);
            }

            [Xunit.FactAttribute]
            public void TestSectionProxyGetWithFallback()
            {
#line (237, 5) - (237, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (238, 5) - (238, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = val1");
#line (239, 5) - (239, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val1", config["section"].Get("key1"));
#line (240, 5) - (240, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("default_val", config["section"].Get("missing", "default_val"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentSyntax()
            {
#line (246, 5) - (246, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (247, 5) - (247, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase_dir = /srv\npath = %(base_dir)s/data");
#line (248, 5) - (248, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/srv/data", config.Get("section", "path"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationFromDefault()
            {
#line (252, 5) - (252, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (253, 5) - (253, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nroot = /\n\n[section]\npath = %(root)setc");
#line (254, 5) - (254, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/etc", config.Get("section", "path"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationRecursive()
            {
#line (258, 5) - (258, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (259, 5) - (259, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = 1\nb = %(a)s2\nc = %(b)s3");
#line (260, 5) - (260, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("123", config.Get("section", "c"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationCircularThrows()
            {
#line (264, 5) - (264, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (265, 5) - (265, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b)s\nb = %(a)s");
#line (266, 5) - (269, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationError>((global::System.Action)(() =>
                {
#line (267, 9) - (267, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationCrossSection()
            {
#line (271, 5) - (271, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (272, 5) - (272, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[paths]\nhome = /Users\n\n[section]\nmy_dir = ${paths:home}/myapp");
#line (273, 5) - (273, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/Users/myapp", config.Get("section", "my_dir"));
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationSameSection()
            {
#line (277, 5) - (277, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (278, 5) - (278, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase = /srv\npath = ${base}/data");
#line (279, 5) - (279, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/srv/data", config.Get("section", "path"));
            }

            [Xunit.FactAttribute]
            public void TestRawGetSkipsInterpolation()
            {
#line (283, 5) - (283, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (284, 5) - (284, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase = /srv\npath = %(base)s/data");
#line (285, 5) - (285, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("%(base)s/data", config.Get("section", "path", raw: true));
            }

            [Xunit.FactAttribute]
            public void TestGetIntParsesInteger()
            {
#line (291, 5) - (291, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (292, 5) - (292, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nport = 8080");
#line (293, 5) - (293, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(8080, config.GetInt("section", "port"));
            }

            [Xunit.FactAttribute]
            public void TestGetIntInvalidThrows()
            {
#line (297, 5) - (297, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (298, 5) - (298, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nval = notint");
#line (299, 5) - (302, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (300, 9) - (300, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.GetInt("section", "val");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetFloatParsesDouble()
            {
#line (304, 5) - (304, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (305, 5) - (305, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nrate = 3.14");
#line (306, 5) - (306, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(config.GetFloat("section", "rate") - 3.14d) < 0.001d);
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanAllVariants()
            {
#line (310, 5) - (310, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (311, 5) - (311, 105) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = yes\nb = no\nc = true\nd = false\ne = 1\nf = 0\ng = on\nh = off");
#line (312, 5) - (312, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "a"));
#line (313, 5) - (313, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "b"));
#line (314, 5) - (314, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "c"));
#line (315, 5) - (315, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "d"));
#line (316, 5) - (316, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "e"));
#line (317, 5) - (317, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "f"));
#line (318, 5) - (318, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "g"));
#line (319, 5) - (319, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "h"));
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanInvalidThrows()
            {
#line (323, 5) - (323, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (324, 5) - (324, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nval = maybe");
#line (325, 5) - (330, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (326, 9) - (326, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.GetBoolean("section", "val");
                }));
            }

            [Xunit.FactAttribute]
            public void TestWriteRoundTrip()
            {
#line (332, 5) - (332, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (333, 5) - (333, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (334, 5) - (334, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key1", "value1");
#line (335, 5) - (335, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key2", "value2");
#line (336, 5) - (336, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (337, 5) - (337, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer);
#line (338, 5) - (338, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config2 = new global::Sharpy.ConfigParser();
#line (339, 5) - (339, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config2.ReadString(writer.Getvalue());
#line (340, 5) - (340, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value1", config2.Get("section", "key1"));
#line (341, 5) - (341, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value2", config2.Get("section", "key2"));
            }

            [Xunit.FactAttribute]
            public void TestWriteDefaultSection()
            {
#line (345, 5) - (345, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (346, 5) - (346, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nbase = /srv\n\n[section]\nkey = val");
#line (347, 5) - (347, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (348, 5) - (348, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer);
#line (349, 5) - (349, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                string output = writer.Getvalue();
#line (350, 5) - (350, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("[DEFAULT]", output);
#line (351, 5) - (351, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("base = /srv", output);
#line (352, 5) - (352, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("[section]", output);
            }

            [Xunit.FactAttribute]
            public void TestWriteNoSpaceAroundDelimiters()
            {
#line (356, 5) - (356, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (357, 5) - (357, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (358, 5) - (358, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key", "value");
#line (359, 5) - (359, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (360, 5) - (360, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer, spaceAroundDelimiters: false);
#line (361, 5) - (361, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key=value", writer.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestReadMissingFileSilentlyIgnored()
            {
#line (365, 5) - (365, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (366, 5) - (366, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Read("/tmp/nonexistent_config_file_12345.ini");
#line (367, 5) - (367, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Sections()));
            }

            [Xunit.FactAttribute]
            public void TestEmptyIniFile()
            {
#line (373, 5) - (373, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (374, 5) - (374, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("");
#line (375, 5) - (375, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Sections()));
            }

            [Xunit.FactAttribute]
            public void TestSectionWithNoKeys()
            {
#line (379, 5) - (379, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (380, 5) - (380, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]");
#line (381, 5) - (381, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section"));
#line (382, 5) - (382, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Options("section")));
            }

            [Xunit.FactAttribute]
            public void TestValueContainsDelimiter()
            {
#line (386, 5) - (386, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (387, 5) - (387, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value = with = equals");
#line (388, 5) - (388, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value = with = equals", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestInlineCommentsDisabledByDefault()
            {
#line (392, 5) - (392, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (393, 5) - (393, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = foo # bar");
#line (394, 5) - (394, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("foo # bar", config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestAllowNoValueGetReturnsNull()
            {
#line (400, 5) - (400, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(allowNoValue: true);
#line (401, 5) - (401, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey");
#line (402, 5) - (402, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Null(config.Get("section", "key"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationMissingKeyThrowsInterpolationError()
            {
#line (408, 5) - (408, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (409, 5) - (409, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\npath = %(missing)s");
#line (410, 5) - (413, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationError>((global::System.Action)(() =>
                {
#line (411, 9) - (411, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "path");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetIntMissingSectionReturnsFallback()
            {
#line (416, 5) - (416, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (417, 5) - (417, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(42, config.GetInt("nosection", "key", fallback: 42));
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanMissingSectionReturnsFallback()
            {
#line (421, 5) - (421, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (422, 5) - (422, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("nosection", "key", fallback: true));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationCircularThrowsDepthError()
            {
#line (428, 5) - (428, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (429, 5) - (429, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b)s\nb = %(a)s");
#line (430, 5) - (433, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationDepthError>((global::System.Action)(() =>
                {
#line (431, 9) - (431, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationMissingKeyThrowsMissingOptionError()
            {
#line (435, 5) - (435, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (436, 5) - (436, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\npath = %(missing)s");
#line (437, 5) - (440, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationMissingOptionError>((global::System.Action)(() =>
                {
#line (438, 9) - (438, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "path");
                }));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationUnterminatedThrowsSyntaxError()
            {
#line (442, 5) - (442, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (443, 5) - (443, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b");
#line (444, 5) - (447, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationSyntaxError>((global::System.Action)(() =>
                {
#line (445, 9) - (445, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationBadPercentThrowsSyntaxError()
            {
#line (449, 5) - (449, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (450, 5) - (450, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %z");
#line (451, 5) - (454, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationSyntaxError>((global::System.Action)(() =>
                {
#line (452, 9) - (452, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationUnterminatedThrowsSyntaxError()
            {
#line (456, 5) - (456, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (457, 5) - (457, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = ${b");
#line (458, 5) - (461, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationSyntaxError>((global::System.Action)(() =>
                {
#line (459, 9) - (459, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationTooManyColonsThrowsSyntaxError()
            {
#line (463, 5) - (463, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (464, 5) - (464, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = ${x:y:z}");
#line (465, 5) - (468, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InterpolationSyntaxError>((global::System.Action)(() =>
                {
#line (466, 9) - (466, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
                }));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentEscapeDoesNotThrow()
            {
#line (470, 5) - (470, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (471, 5) - (471, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = 100%%");
#line (472, 5) - (472, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("100%", config.Get("section", "a"));
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationValidCrossSectionDoesNotThrow()
            {
#line (476, 5) - (476, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (477, 5) - (477, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[defaults]\nbase = /opt\n[section]\npath = ${defaults:base}/app");
#line (478, 5) - (478, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/opt/app", config.Get("section", "path"));
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentEscapeBeforeKeyProducesLiteral()
            {
#line (482, 5) - (482, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (483, 5) - (483, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = world\na = 100%%(key)s");
#line (484, 5) - (484, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("100%(key)s", config.Get("section", "a"));
            }
        }
    }
}

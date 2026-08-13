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
#line (10, 5) - (10, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (11, 5) - (11, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (12, 5) - (12, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringColonDelimiter()
            {
#line (16, 5) - (16, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (17, 5) - (17, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey : value");
#line (18, 5) - (18, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringNoSpaces()
            {
#line (22, 5) - (22, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (23, 5) - (23, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey=value");
#line (24, 5) - (24, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringMultilineValue()
            {
#line (28, 5) - (28, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (29, 5) - (29, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = line1\n  line2\n  line3");
#line (30, 5) - (30, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("line1\nline2\nline3", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringHashComments()
            {
#line (34, 5) - (34, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (35, 5) - (35, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\n# comment\nkey = value");
#line (36, 5) - (36, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line (37, 5) - (37, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(config.Options("section")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringSemicolonComments()
            {
#line (41, 5) - (41, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (42, 5) - (42, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\n; comment\nkey = value");
#line (43, 5) - (43, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringMultipleSections()
            {
#line (47, 5) - (47, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (48, 5) - (48, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section1]\nkey1 = val1\n\n[section2]\nkey2 = val2");
#line (49, 5) - (49, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(config.Sections()));
#line (50, 5) - (50, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val1", config.Get("section1", "key1"));
#line (51, 5) - (51, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val2", config.Get("section2", "key2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringEmptyValue()
            {
#line (55, 5) - (55, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (56, 5) - (56, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey =");
#line (57, 5) - (57, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadStringWhitespaceInSectionName()
            {
#line (61, 5) - (61, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (62, 5) - (62, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[ section ]\nkey = value");
#line (63, 5) - (63, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get(" section ", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDefaultFallback()
            {
#line (69, 5) - (69, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (70, 5) - (70, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nfallback = yes\n\n[section]\nkey = value");
#line (71, 5) - (71, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("yes", config.Get("section", "fallback"));
#line (72, 5) - (72, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDefaultOverriddenBySection()
            {
#line (76, 5) - (76, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (77, 5) - (77, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey = default\n\n[section]\nkey = override");
#line (78, 5) - (78, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("override", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDefaultsReturnsDefaultValues()
            {
#line (82, 5) - (82, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (83, 5) - (83, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey1 = val1\nkey2 = val2");
#line (84, 5) - (84, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var defaults = config.Defaults();
#line (85, 5) - (85, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(defaults.ContainsKey("key1"));
#line (86, 5) - (86, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(defaults.ContainsKey("key2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCaseInsensitiveKeys()
            {
#line (92, 5) - (92, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (93, 5) - (93, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nMyKey = myvalue");
#line (94, 5) - (94, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("myvalue", config.Get("section", "mykey"));
#line (95, 5) - (95, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("myvalue", config.Get("section", "MYKEY"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCaseSensitiveSections()
            {
#line (99, 5) - (99, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (100, 5) - (100, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[Section]\nkey = val");
#line (101, 5) - (101, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("Section"));
#line (102, 5) - (102, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("section"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHasSectionReturnsFalseForDefault()
            {
#line (106, 5) - (106, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (107, 5) - (107, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nkey = val\n\n[section]\nkey2 = val2");
#line (108, 5) - (108, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("DEFAULT"));
#line (109, 5) - (109, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHasOptionChecksSectionAndDefault()
            {
#line (113, 5) - (113, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (114, 5) - (114, 88) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\ndefault_key = val\n\n[section]\nsection_key = val2");
#line (115, 5) - (115, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "section_key"));
#line (116, 5) - (116, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "default_key"));
#line (117, 5) - (117, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasOption("section", "nonexistent"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAddSectionAndSet()
            {
#line (123, 5) - (123, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (124, 5) - (124, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("new_section");
#line (125, 5) - (125, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("new_section", "key", "value");
#line (126, 5) - (126, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config.Get("new_section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAddSectionDuplicateThrows()
            {
#line (130, 5) - (130, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (131, 5) - (131, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (132, 5) - (135, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (133, 9) - (133, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.AddSection("section");
#line hidden
                }
                catch (global::Sharpy.DuplicateSectionError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected DuplicateSectionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestAddSectionDefaultThrows()
            {
#line (137, 5) - (137, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (138, 5) - (141, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (139, 9) - (139, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.AddSection("DEFAULT");
#line hidden
                }
                catch (ValueError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetNoSectionThrows()
            {
#line (143, 5) - (143, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (144, 5) - (147, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (145, 9) - (145, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("nonexistent", "key");
#line hidden
                }
                catch (global::Sharpy.NoSectionError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected NoSectionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetNoOptionThrows()
            {
#line (149, 5) - (149, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (150, 5) - (150, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (151, 5) - (154, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (152, 9) - (152, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "missing");
#line hidden
                }
                catch (global::Sharpy.NoOptionError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected NoOptionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetFallbackReturned()
            {
#line (156, 5) - (156, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (157, 5) - (157, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (158, 5) - (158, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("default", config.Get("section", "missing", fallback: "default"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSetNoSectionThrows()
            {
#line (162, 5) - (162, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (163, 5) - (166, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (164, 9) - (164, 50) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Set("nonexistent", "key", "value");
#line hidden
                }
                catch (global::Sharpy.NoSectionError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected NoSectionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestRemoveOption()
            {
#line (168, 5) - (168, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (169, 5) - (169, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = value1\nkey2 = value2");
#line (170, 5) - (170, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.RemoveOption("section", "key1"));
#line (171, 5) - (171, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasOption("section", "key1"));
#line (172, 5) - (172, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasOption("section", "key2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRemoveSection()
            {
#line (176, 5) - (176, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (177, 5) - (177, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section1]\nkey = val\n\n[section2]\nkey = val");
#line (178, 5) - (178, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.RemoveSection("section1"));
#line (179, 5) - (179, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.HasSection("section1"));
#line (180, 5) - (180, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOptionsIncludesDefaults()
            {
#line (184, 5) - (184, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (185, 5) - (185, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nd = 1\n\n[section]\ns = 2");
#line (186, 5) - (186, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var options = config.Options("section");
#line (187, 5) - (187, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("d", options);
#line (188, 5) - (188, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("s", options);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestItemsIncludesDefaults()
            {
#line (192, 5) - (192, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (193, 5) - (193, 76) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\ncolor = red\n\n[section]\nsize = large");
#line (194, 5) - (194, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var items = config.Items("section");
#line (195, 5) - (195, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(items.ContainsKey("color"));
#line (196, 5) - (196, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(items.ContainsKey("size"));
#line (197, 5) - (197, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("red", items["color"]);
#line (198, 5) - (198, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("large", items["size"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMissingSectionHeaderErrorBeforeAnySection()
            {
#line (202, 5) - (202, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (203, 5) - (208, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (204, 9) - (204, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.ReadString("key = value");
#line hidden
                }
                catch (global::Sharpy.MissingSectionHeaderError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected MissingSectionHeaderError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccess()
            {
#line (210, 5) - (210, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (211, 5) - (211, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (212, 5) - (212, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value", config["section"]["key"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccessSet()
            {
#line (216, 5) - (216, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (217, 5) - (217, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value");
#line (218, 5) - (218, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config["section"]["key"] = "new_value";
#line (219, 5) - (219, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("new_value", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDictLikeAccessNoSectionThrows()
            {
#line (223, 5) - (223, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (224, 5) - (227, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_6 = false;
#line hidden
                try
                {
#line (225, 9) - (225, 37) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config["nonexistent"].Keys();
#line hidden
                }
                catch (global::Sharpy.NoSectionError)
                {
                    __raised_6 = true;
                }

                if (!__raised_6)
                    throw new global::Sharpy.AssertionError("Expected NoSectionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestSectionProxyKeys()
            {
#line (229, 5) - (229, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (230, 5) - (230, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = val1\nkey2 = val2");
#line (231, 5) - (231, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var keys = config["section"].Keys();
#line (232, 5) - (232, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key1", keys);
#line (233, 5) - (233, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key2", keys);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSectionProxyGetWithFallback()
            {
#line (237, 5) - (237, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (238, 5) - (238, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey1 = val1");
#line (239, 5) - (239, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("val1", config["section"].Get("key1"));
#line (240, 5) - (240, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("default_val", config["section"].Get("missing", "default_val"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentSyntax()
            {
#line (246, 5) - (246, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (247, 5) - (247, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase_dir = /srv\npath = %(base_dir)s/data");
#line (248, 5) - (248, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/srv/data", config.Get("section", "path"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationFromDefault()
            {
#line (252, 5) - (252, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (253, 5) - (253, 79) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nroot = /\n\n[section]\npath = %(root)setc");
#line (254, 5) - (254, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/etc", config.Get("section", "path"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationRecursive()
            {
#line (258, 5) - (258, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (259, 5) - (259, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = 1\nb = %(a)s2\nc = %(b)s3");
#line (260, 5) - (260, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("123", config.Get("section", "c"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationCircularThrows()
            {
#line (264, 5) - (264, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (265, 5) - (265, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b)s\nb = %(a)s");
#line (268, 5) - (271, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_7 = false;
#line hidden
                try
                {
#line (269, 9) - (269, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationDepthError)
                {
                    __raised_7 = true;
                }

                if (!__raised_7)
                    throw new global::Sharpy.AssertionError("Expected InterpolationDepthError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationCrossSection()
            {
#line (273, 5) - (273, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (274, 5) - (274, 92) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[paths]\nhome = /Users\n\n[section]\nmy_dir = ${paths:home}/myapp");
#line (275, 5) - (275, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/Users/myapp", config.Get("section", "my_dir"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationSameSection()
            {
#line (279, 5) - (279, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (280, 5) - (280, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase = /srv\npath = ${base}/data");
#line (281, 5) - (281, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/srv/data", config.Get("section", "path"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRawGetSkipsInterpolation()
            {
#line (285, 5) - (285, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (286, 5) - (286, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nbase = /srv\npath = %(base)s/data");
#line (287, 5) - (287, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("%(base)s/data", config.Get("section", "path", raw: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetIntParsesInteger()
            {
#line (293, 5) - (293, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (294, 5) - (294, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nport = 8080");
#line (295, 5) - (295, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(8080, config.GetInt("section", "port"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetIntInvalidThrows()
            {
#line (299, 5) - (299, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (300, 5) - (300, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nval = notint");
#line (301, 5) - (304, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_8 = false;
#line hidden
                try
                {
#line (302, 9) - (302, 41) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.GetInt("section", "val");
#line hidden
                }
                catch (ValueError)
                {
                    __raised_8 = true;
                }

                if (!__raised_8)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetFloatParsesDouble()
            {
#line (306, 5) - (306, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (307, 5) - (307, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nrate = 3.14");
#line (308, 5) - (308, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(config.GetFloat("section", "rate") - 3.14d) < 0.001d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanAllVariants()
            {
#line (312, 5) - (312, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (313, 5) - (313, 105) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = yes\nb = no\nc = true\nd = false\ne = 1\nf = 0\ng = on\nh = off");
#line (314, 5) - (314, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "a"));
#line (315, 5) - (315, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "b"));
#line (316, 5) - (316, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "c"));
#line (317, 5) - (317, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "d"));
#line (318, 5) - (318, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "e"));
#line (319, 5) - (319, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "f"));
#line (320, 5) - (320, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("section", "g"));
#line (321, 5) - (321, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.False(config.GetBoolean("section", "h"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanInvalidThrows()
            {
#line (325, 5) - (325, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (326, 5) - (326, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nval = maybe");
#line (327, 5) - (332, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_9 = false;
#line hidden
                try
                {
#line (328, 9) - (328, 45) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.GetBoolean("section", "val");
#line hidden
                }
                catch (ValueError)
                {
                    __raised_9 = true;
                }

                if (!__raised_9)
                    throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestWriteRoundTrip()
            {
#line (334, 5) - (334, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (335, 5) - (335, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (336, 5) - (336, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key1", "value1");
#line (337, 5) - (337, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key2", "value2");
#line (338, 5) - (338, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (339, 5) - (339, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer);
#line (340, 5) - (340, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config2 = new global::Sharpy.ConfigParser();
#line (341, 5) - (341, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config2.ReadString(writer.Getvalue());
#line (342, 5) - (342, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value1", config2.Get("section", "key1"));
#line (343, 5) - (343, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value2", config2.Get("section", "key2"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriteDefaultSection()
            {
#line (347, 5) - (347, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (348, 5) - (348, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[DEFAULT]\nbase = /srv\n\n[section]\nkey = val");
#line (349, 5) - (349, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (350, 5) - (350, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer);
#line (351, 5) - (351, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                string output = writer.Getvalue();
#line (352, 5) - (352, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("[DEFAULT]", output);
#line (353, 5) - (353, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("base = /srv", output);
#line (354, 5) - (354, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("[section]", output);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriteNoSpaceAroundDelimiters()
            {
#line (358, 5) - (358, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (359, 5) - (359, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.AddSection("section");
#line (360, 5) - (360, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Set("section", "key", "value");
#line (361, 5) - (361, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var writer = new global::Sharpy.StringIO();
#line (362, 5) - (362, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Write(writer, spaceAroundDelimiters: false);
#line (363, 5) - (363, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Contains("key=value", writer.Getvalue());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReadMissingFileSilentlyIgnored()
            {
#line (367, 5) - (367, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (368, 5) - (368, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.Read("/tmp/nonexistent_config_file_12345.ini");
#line (369, 5) - (369, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Sections()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEmptyIniFile()
            {
#line (375, 5) - (375, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (376, 5) - (376, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("");
#line (377, 5) - (377, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Sections()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSectionWithNoKeys()
            {
#line (381, 5) - (381, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (382, 5) - (382, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]");
#line (383, 5) - (383, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.HasSection("section"));
#line (384, 5) - (384, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(config.Options("section")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestValueContainsDelimiter()
            {
#line (388, 5) - (388, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (389, 5) - (389, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = value = with = equals");
#line (390, 5) - (390, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("value = with = equals", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInlineCommentsDisabledByDefault()
            {
#line (394, 5) - (394, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (395, 5) - (395, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = foo # bar");
#line (396, 5) - (396, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("foo # bar", config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAllowNoValueGetReturnsNull()
            {
#line (402, 5) - (402, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(allowNoValue: true);
#line (403, 5) - (403, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey");
#line (404, 5) - (404, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Null(config.Get("section", "key"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationMissingKeyThrowsInterpolationError()
            {
#line (410, 5) - (410, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (411, 5) - (411, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\npath = %(missing)s");
#line (414, 5) - (417, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_10 = false;
#line hidden
                try
                {
#line (415, 9) - (415, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "path");
#line hidden
                }
                catch (global::Sharpy.InterpolationMissingOptionError)
                {
                    __raised_10 = true;
                }

                if (!__raised_10)
                    throw new global::Sharpy.AssertionError("Expected InterpolationMissingOptionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestGetIntMissingSectionReturnsFallback()
            {
#line (420, 5) - (420, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (421, 5) - (421, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal(42, config.GetInt("nosection", "key", fallback: 42));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGetBooleanMissingSectionReturnsFallback()
            {
#line (425, 5) - (425, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser();
#line (426, 5) - (426, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.True(config.GetBoolean("nosection", "key", fallback: true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationCircularThrowsDepthError()
            {
#line (432, 5) - (432, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (433, 5) - (433, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b)s\nb = %(a)s");
#line (434, 5) - (437, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_11 = false;
#line hidden
                try
                {
#line (435, 9) - (435, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationDepthError)
                {
                    __raised_11 = true;
                }

                if (!__raised_11)
                    throw new global::Sharpy.AssertionError("Expected InterpolationDepthError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationMissingKeyThrowsMissingOptionError()
            {
#line (439, 5) - (439, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (440, 5) - (440, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\npath = %(missing)s");
#line (441, 5) - (444, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_12 = false;
#line hidden
                try
                {
#line (442, 9) - (442, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "path");
#line hidden
                }
                catch (global::Sharpy.InterpolationMissingOptionError)
                {
                    __raised_12 = true;
                }

                if (!__raised_12)
                    throw new global::Sharpy.AssertionError("Expected InterpolationMissingOptionError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationUnterminatedThrowsSyntaxError()
            {
#line (446, 5) - (446, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (447, 5) - (447, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %(b");
#line (448, 5) - (451, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_13 = false;
#line hidden
                try
                {
#line (449, 9) - (449, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationSyntaxError)
                {
                    __raised_13 = true;
                }

                if (!__raised_13)
                    throw new global::Sharpy.AssertionError("Expected InterpolationSyntaxError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationBadPercentThrowsSyntaxError()
            {
#line (453, 5) - (453, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (454, 5) - (454, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = %z");
#line (455, 5) - (458, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_14 = false;
#line hidden
                try
                {
#line (456, 9) - (456, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationSyntaxError)
                {
                    __raised_14 = true;
                }

                if (!__raised_14)
                    throw new global::Sharpy.AssertionError("Expected InterpolationSyntaxError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationUnterminatedThrowsSyntaxError()
            {
#line (460, 5) - (460, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (461, 5) - (461, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = ${b");
#line (462, 5) - (465, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_15 = false;
#line hidden
                try
                {
#line (463, 9) - (463, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationSyntaxError)
                {
                    __raised_15 = true;
                }

                if (!__raised_15)
                    throw new global::Sharpy.AssertionError("Expected InterpolationSyntaxError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationTooManyColonsThrowsSyntaxError()
            {
#line (467, 5) - (467, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (468, 5) - (468, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = ${x:y:z}");
#line (469, 5) - (472, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                bool __raised_16 = false;
#line hidden
                try
                {
#line (470, 9) - (470, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                    config.Get("section", "a");
#line hidden
                }
                catch (global::Sharpy.InterpolationSyntaxError)
                {
                    __raised_16 = true;
                }

                if (!__raised_16)
                    throw new global::Sharpy.AssertionError("Expected InterpolationSyntaxError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentEscapeDoesNotThrow()
            {
#line (474, 5) - (474, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (475, 5) - (475, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\na = 100%%");
#line (476, 5) - (476, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("100%", config.Get("section", "a"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExtendedInterpolationValidCrossSectionDoesNotThrow()
            {
#line (480, 5) - (480, 77) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.ExtendedInterpolation());
#line (481, 5) - (481, 90) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[defaults]\nbase = /opt\n[section]\npath = ${defaults:base}/app");
#line (482, 5) - (482, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("/opt/app", config.Get("section", "path"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestBasicInterpolationPercentEscapeBeforeKeyProducesLiteral()
            {
#line (486, 5) - (486, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                var config = new global::Sharpy.ConfigParser(new global::Sharpy.BasicInterpolation());
#line (487, 5) - (487, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                config.ReadString("[section]\nkey = world\na = 100%%(key)s");
#line (488, 5) - (488, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/configparser/configparser_tests.spy"
                Xunit.Assert.Equal("100%(key)s", config.Get("section", "a"));
#line hidden
            }
        }
    }
}
#line default

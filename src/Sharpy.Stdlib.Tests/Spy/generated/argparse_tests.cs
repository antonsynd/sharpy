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
using static Sharpy.Stdlib.Tests.Spy.Argparse.ArgparseTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Argparse
    {
        [global::Sharpy.SharpyModule("argparse.argparse_tests")]
        public static partial class ArgparseTests
        {
        }
    }

    public static partial class Argparse
    {
        public partial class ArgparseTestsTests
        {
            [Xunit.FactAttribute]
            public void TestParseArgsSinglePositionalParsed()
            {
#line (9, 5) - (9, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (10, 5) - (10, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (11, 5) - (11, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "test.txt" });
#line (12, 5) - (12, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test.txt", ns["filename"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMultiplePositionalsParsed()
            {
#line (16, 5) - (16, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (17, 5) - (17, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("source");
#line (18, 5) - (18, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("dest");
#line (19, 5) - (19, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt" });
#line (20, 5) - (20, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("a.txt", ns["source"]);
#line (21, 5) - (21, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("b.txt", ns["dest"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsPositionalWithTypeConverted()
            {
#line (25, 5) - (25, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (26, 5) - (26, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("count", type: "int");
#line (27, 5) - (27, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "42" });
#line (28, 5) - (28, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (29, 5) - (29, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=42", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMissingPositionalThrowsArgumentError()
            {
#line (33, 5) - (33, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (34, 5) - (34, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (35, 5) - (40, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (36, 9) - (36, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsLongOptionParsed()
            {
#line (42, 5) - (42, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (43, 5) - (43, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name");
#line (44, 5) - (44, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--name", "test" });
#line (45, 5) - (45, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test", ns["name"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsShortAndLongOptionParsed()
            {
#line (49, 5) - (49, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (50, 5) - (50, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", shortName: "-n");
#line (51, 5) - (51, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-n", "test" });
#line (52, 5) - (52, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test", ns["name"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalWithDefaultUsesDefault()
            {
#line (56, 5) - (56, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (57, 5) - (57, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "default");
#line (58, 5) - (58, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (59, 5) - (59, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("default", ns["name"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalWithTypeConverted()
            {
#line (63, 5) - (63, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (64, 5) - (64, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int");
#line (65, 5) - (65, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--count", "5" });
#line (66, 5) - (66, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (67, 5) - (67, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsRequiredOptionalMissingThrowsError()
            {
#line (71, 5) - (71, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (72, 5) - (72, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", required: true);
#line (73, 5) - (78, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (74, 9) - (74, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreTrueSetsTrue()
            {
#line (80, 5) - (80, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (81, 5) - (81, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
#line (82, 5) - (82, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-v" });
#line (83, 5) - (83, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (84, 5) - (84, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreTrueDefaultFalse()
            {
#line (88, 5) - (88, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (89, 5) - (89, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true");
#line (90, 5) - (90, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (91, 5) - (91, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (92, 5) - (92, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=False", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreFalseSetsFalse()
            {
#line (96, 5) - (96, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (97, 5) - (97, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--no-feature", action: "store_false", dest: "feature");
#line (98, 5) - (98, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--no-feature" });
#line (99, 5) - (99, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (100, 5) - (100, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("feature=False", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsCountCountsOccurrences()
            {
#line (104, 5) - (104, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (105, 5) - (105, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "count");
#line (106, 5) - (106, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-v", "-v", "-v" });
#line (107, 5) - (107, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (108, 5) - (108, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=3", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsAppendCollectsValues()
            {
#line (112, 5) - (112, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (113, 5) - (113, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--item", action: "append");
#line (114, 5) - (114, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--item", "a", "--item", "b" });
#line (115, 5) - (115, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("item"));
#line (116, 5) - (116, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["item"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsValidChoiceAccepted()
            {
#line (122, 5) - (122, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (123, 5) - (123, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Sharpy.List<string> choices = new Sharpy.List<string>()
                {
                    "debug",
                    "info",
                    "warning"
                };
#line (124, 5) - (124, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--level", choices: choices);
#line (125, 5) - (125, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--level", "info" });
#line (126, 5) - (126, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("info", ns["level"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidChoiceThrowsError()
            {
#line (130, 5) - (130, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (131, 5) - (131, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Sharpy.List<string> choices = new Sharpy.List<string>()
                {
                    "debug",
                    "info"
                };
#line (132, 5) - (132, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--level", choices: choices);
#line (133, 5) - (138, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (134, 9) - (134, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--level", "error" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsStarCollectsZeroOrMore()
            {
#line (140, 5) - (140, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (141, 5) - (141, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "*");
#line (142, 5) - (142, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt" });
#line (143, 5) - (143, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (144, 5) - (144, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsStarEmptyReturnsEmptyList()
            {
#line (148, 5) - (148, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (149, 5) - (149, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "*");
#line (150, 5) - (150, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (151, 5) - (151, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (152, 5) - (152, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsPlusRequiresAtLeastOne()
            {
#line (156, 5) - (156, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (157, 5) - (157, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "+");
#line (158, 5) - (161, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (159, 9) - (159, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsQuestionOptionalValue()
            {
#line (163, 5) - (163, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (164, 5) - (164, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output", nargs: "?", defaultValue: "stdout");
#line (165, 5) - (165, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--output" });
#line (166, 5) - (166, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("stdout", ns["output"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMixedPositionalAndOptionalParsed()
            {
#line (172, 5) - (172, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (173, 5) - (173, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (174, 5) - (174, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
#line (175, 5) - (175, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 1);
#line (176, 5) - (176, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "test.txt", "-v", "--count", "5" });
#line (177, 5) - (177, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test.txt", ns["filename"]);
#line (178, 5) - (178, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (179, 5) - (179, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (180, 5) - (180, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsUnrecognizedArgumentThrowsError()
            {
#line (184, 5) - (184, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (185, 5) - (190, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (186, 9) - (186, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--unknown" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsHelpThrowsSystemExit()
            {
#line (192, 5) - (192, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(description: "A test program");
#line (193, 5) - (196, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<SystemExit>((global::System.Action)(() =>
                {
#line (194, 9) - (194, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--help" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpContainsDescription()
            {
#line (198, 5) - (198, 107) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(description: "A test program", prog: "myapp");
#line (199, 5) - (199, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("input");
#line (200, 5) - (200, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output", shortName: "-o", help: "output file");
#line (201, 5) - (201, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string helpText = parser.FormatHelp();
#line (202, 5) - (202, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("A test program", helpText);
#line (203, 5) - (203, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("myapp", helpText);
#line (204, 5) - (204, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("input", helpText);
#line (205, 5) - (205, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("output file", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpShowsHelpOption()
            {
#line (209, 5) - (209, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (210, 5) - (210, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string helpText = parser.FormatHelp();
#line (211, 5) - (211, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("-h, --help", helpText);
#line (212, 5) - (212, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("show this help message and exit", helpText);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceContainsWorks()
            {
#line (218, 5) - (218, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (219, 5) - (219, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (220, 5) - (220, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (221, 5) - (221, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("name"));
#line (222, 5) - (222, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.False(ns.Contains("missing"));
            }

            [Xunit.FactAttribute]
            public void TestNamespaceGetReturnsTyped()
            {
#line (226, 5) - (226, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (227, 5) - (227, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 5);
#line (228, 5) - (228, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (229, 5) - (229, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (230, 5) - (230, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceGetWrongTypeThrowsTypeError()
            {
#line (234, 5) - (234, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (235, 5) - (235, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (236, 5) - (236, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (237, 5) - (237, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test", ns["name"]);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceMissingAttributeThrowsAttributeError()
            {
#line (241, 5) - (241, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (242, 5) - (242, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (243, 5) - (246, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<AttributeError>((global::System.Action)(() =>
                {
#line (244, 9) - (244, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    var _ = ns["missing"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringShowsValues()
            {
#line (248, 5) - (248, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (249, 5) - (249, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (250, 5) - (250, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true");
#line (251, 5) - (251, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose" });
#line (252, 5) - (252, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (253, 5) - (253, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("Namespace(", s);
#line (254, 5) - (254, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("name='test'", s);
#line (255, 5) - (255, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsDashInNameNormalizedToUnderscore()
            {
#line (261, 5) - (261, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (262, 5) - (262, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--my-flag", action: "store_true");
#line (263, 5) - (263, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--my-flag" });
#line (264, 5) - (264, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (265, 5) - (265, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("my_flag=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsCustomDestUsesCustomDest()
            {
#line (269, 5) - (269, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (270, 5) - (270, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output-file", dest: "output");
#line (271, 5) - (271, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--output-file", "out.txt" });
#line (272, 5) - (272, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("out.txt", ns["output"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidIntThrowsArgumentError()
            {
#line (278, 5) - (278, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (279, 5) - (279, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("count", type: "int");
#line (280, 5) - (283, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (281, 9) - (281, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "abc" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidFloatThrowsArgumentError()
            {
#line (285, 5) - (285, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (286, 5) - (286, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("value", type: "float");
#line (287, 5) - (292, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (288, 9) - (288, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "abc" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersAddParserParsesSubcommand()
            {
#line (294, 5) - (294, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (295, 5) - (295, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(dest: "command");
#line (296, 5) - (296, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser sub = subparsers.AddParser("run", help: "Run a program");
#line (297, 5) - (297, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                sub.AddArgument("filename");
#line (298, 5) - (298, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "run", "test.spy" });
#line (299, 5) - (299, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("run", ns["command"]);
#line (300, 5) - (300, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test.spy", ns["filename"]);
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersMultipleSubcommands()
            {
#line (304, 5) - (304, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (305, 5) - (305, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(dest: "cmd");
#line (306, 5) - (306, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser buildParser = subparsers.AddParser("build");
#line (307, 5) - (307, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                buildParser.AddOptionalArgument("--release", action: "store_true");
#line (308, 5) - (308, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser testParser = subparsers.AddParser("test");
#line (309, 5) - (309, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                testParser.AddOptionalArgument("--filter");
#line (310, 5) - (310, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns1 = parser.ParseArgs(new Sharpy.List<string>() { "build", "--release" });
#line (311, 5) - (311, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("build", ns1["cmd"]);
#line (312, 5) - (312, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s1 = global::Sharpy.Builtins.Str(ns1);
#line (313, 5) - (313, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("release=True", s1);
#line (314, 5) - (314, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns2 = parser.ParseArgs(new Sharpy.List<string>() { "test", "--filter", "Lexer" });
#line (315, 5) - (315, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("test", ns2["cmd"]);
#line (316, 5) - (316, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("Lexer", ns2["filter"]);
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersDuplicateThrowsError()
            {
#line (320, 5) - (320, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (321, 5) - (321, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddSubparsers();
#line (322, 5) - (327, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (323, 9) - (323, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.AddSubparsers();
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupArgumentsParsedNormally()
            {
#line (329, 5) - (329, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (330, 5) - (330, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Network options");
#line (331, 5) - (331, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--host", defaultValue: "localhost");
#line (332, 5) - (332, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--port", type: "int", defaultValue: 8080);
#line (333, 5) - (333, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--host", "example.com", "--port", "9090" });
#line (334, 5) - (334, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("example.com", ns["host"]);
#line (335, 5) - (335, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (336, 5) - (336, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("port=9090", s);
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupDefaultsWork()
            {
#line (340, 5) - (340, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (341, 5) - (341, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Options");
#line (342, 5) - (342, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--name", defaultValue: "default");
#line (343, 5) - (343, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (344, 5) - (344, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Equal("default", ns["name"]);
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupSingleOptionAccepted()
            {
#line (350, 5) - (350, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (351, 5) - (351, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup();
#line (352, 5) - (352, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (353, 5) - (353, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (354, 5) - (354, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose" });
#line (355, 5) - (355, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (356, 5) - (356, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (357, 5) - (357, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("quiet=False", s);
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupBothOptionsThrowsError()
            {
#line (361, 5) - (361, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (362, 5) - (362, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup();
#line (363, 5) - (363, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (364, 5) - (364, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (365, 5) - (368, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (366, 9) - (366, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--quiet" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupRequiredNoneProvidedThrowsError()
            {
#line (370, 5) - (370, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (371, 5) - (371, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup(required: true);
#line (372, 5) - (372, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (373, 5) - (373, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (374, 5) - (376, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (375, 9) - (375, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }
        }
    }
}

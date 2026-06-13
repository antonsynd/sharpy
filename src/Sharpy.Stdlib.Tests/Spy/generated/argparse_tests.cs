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
using @operator = global::Sharpy.Operator;
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
#line (10, 5) - (10, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (11, 5) - (11, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (12, 5) - (12, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "test.txt" });
#line (13, 5) - (13, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["filename"], "test.txt"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMultiplePositionalsParsed()
            {
#line (17, 5) - (17, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (18, 5) - (18, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("source");
#line (19, 5) - (19, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("dest");
#line (20, 5) - (20, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt" });
#line (21, 5) - (21, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["source"], "a.txt"));
#line (22, 5) - (22, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["dest"], "b.txt"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsPositionalWithTypeConverted()
            {
#line (26, 5) - (26, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (27, 5) - (27, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("count", type: "int");
#line (28, 5) - (28, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "42" });
#line (29, 5) - (29, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (30, 5) - (30, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=42", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMissingPositionalThrowsArgumentError()
            {
#line (34, 5) - (34, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (35, 5) - (35, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (36, 5) - (41, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (37, 9) - (37, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsLongOptionParsed()
            {
#line (43, 5) - (43, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (44, 5) - (44, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name");
#line (45, 5) - (45, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--name", "test" });
#line (46, 5) - (46, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["name"], "test"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsShortAndLongOptionParsed()
            {
#line (50, 5) - (50, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (51, 5) - (51, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", shortName: "-n");
#line (52, 5) - (52, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-n", "test" });
#line (53, 5) - (53, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["name"], "test"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalWithDefaultUsesDefault()
            {
#line (57, 5) - (57, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (58, 5) - (58, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "default");
#line (59, 5) - (59, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (60, 5) - (60, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["name"], "default"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalWithTypeConverted()
            {
#line (64, 5) - (64, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (65, 5) - (65, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int");
#line (66, 5) - (66, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--count", "5" });
#line (67, 5) - (67, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (68, 5) - (68, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsRequiredOptionalMissingThrowsError()
            {
#line (72, 5) - (72, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (73, 5) - (73, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", required: true);
#line (74, 5) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (75, 9) - (75, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreTrueSetsTrue()
            {
#line (81, 5) - (81, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (82, 5) - (82, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
#line (83, 5) - (83, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-v" });
#line (84, 5) - (84, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (85, 5) - (85, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreTrueDefaultFalse()
            {
#line (89, 5) - (89, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (90, 5) - (90, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true");
#line (91, 5) - (91, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (92, 5) - (92, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (93, 5) - (93, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=False", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsStoreFalseSetsFalse()
            {
#line (97, 5) - (97, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (98, 5) - (98, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--no-feature", action: "store_false", dest: "feature");
#line (99, 5) - (99, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--no-feature" });
#line (100, 5) - (100, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (101, 5) - (101, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("feature=False", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsCountCountsOccurrences()
            {
#line (105, 5) - (105, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (106, 5) - (106, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "count");
#line (107, 5) - (107, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "-v", "-v", "-v" });
#line (108, 5) - (108, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (109, 5) - (109, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=3", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsAppendCollectsValues()
            {
#line (113, 5) - (113, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (114, 5) - (114, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--item", action: "append");
#line (115, 5) - (115, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--item", "a", "--item", "b" });
#line (116, 5) - (116, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("item"));
#line (117, 5) - (117, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["item"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsValidChoiceAccepted()
            {
#line (123, 5) - (123, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (124, 5) - (124, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Sharpy.List<string> choices = new Sharpy.List<string>()
                {
                    "debug",
                    "info",
                    "warning"
                };
#line (125, 5) - (125, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--level", choices: choices);
#line (126, 5) - (126, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--level", "info" });
#line (127, 5) - (127, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["level"], "info"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidChoiceThrowsError()
            {
#line (131, 5) - (131, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (132, 5) - (132, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Sharpy.List<string> choices = new Sharpy.List<string>()
                {
                    "debug",
                    "info"
                };
#line (133, 5) - (133, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--level", choices: choices);
#line (134, 5) - (139, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (135, 9) - (135, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--level", "error" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsStarCollectsZeroOrMore()
            {
#line (141, 5) - (141, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (142, 5) - (142, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "*");
#line (143, 5) - (143, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt" });
#line (144, 5) - (144, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (145, 5) - (145, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsStarEmptyReturnsEmptyList()
            {
#line (149, 5) - (149, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (150, 5) - (150, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "*");
#line (151, 5) - (151, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (152, 5) - (152, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (153, 5) - (153, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsPlusRequiresAtLeastOne()
            {
#line (157, 5) - (157, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (158, 5) - (158, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("files", nargs: "+");
#line (159, 5) - (162, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (160, 9) - (160, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsQuestionOptionalValue()
            {
#line (164, 5) - (164, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (165, 5) - (165, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output", nargs: "?", defaultValue: "stdout");
#line (166, 5) - (166, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--output" });
#line (167, 5) - (167, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["output"], "stdout"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsMixedPositionalAndOptionalParsed()
            {
#line (173, 5) - (173, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (174, 5) - (174, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("filename");
#line (175, 5) - (175, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
#line (176, 5) - (176, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 1);
#line (177, 5) - (177, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "test.txt", "-v", "--count", "5" });
#line (178, 5) - (178, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["filename"], "test.txt"));
#line (179, 5) - (179, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (180, 5) - (180, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (181, 5) - (181, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsUnrecognizedArgumentThrowsError()
            {
#line (185, 5) - (185, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (186, 5) - (191, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (187, 9) - (187, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--unknown" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsHelpThrowsSystemExit()
            {
#line (193, 5) - (193, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(description: "A test program");
#line (194, 5) - (197, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<SystemExit>((global::System.Action)(() =>
                {
#line (195, 9) - (195, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--help" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpContainsDescription()
            {
#line (199, 5) - (199, 107) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(description: "A test program", prog: "myapp");
#line (200, 5) - (200, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("input");
#line (201, 5) - (201, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output", shortName: "-o", help: "output file");
#line (202, 5) - (202, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string helpText = parser.FormatHelp();
#line (203, 5) - (203, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("A test program", helpText);
#line (204, 5) - (204, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("myapp", helpText);
#line (205, 5) - (205, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("input", helpText);
#line (206, 5) - (206, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("output file", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpShowsHelpOption()
            {
#line (210, 5) - (210, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (211, 5) - (211, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string helpText = parser.FormatHelp();
#line (212, 5) - (212, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("-h, --help", helpText);
#line (213, 5) - (213, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("show this help message and exit", helpText);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceContainsWorks()
            {
#line (219, 5) - (219, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (220, 5) - (220, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (221, 5) - (221, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (222, 5) - (222, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(ns.Contains("name"));
#line (223, 5) - (223, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.False(ns.Contains("missing"));
            }

            [Xunit.FactAttribute]
            public void TestNamespaceGetReturnsTyped()
            {
#line (227, 5) - (227, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (228, 5) - (228, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 5);
#line (229, 5) - (229, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (230, 5) - (230, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (231, 5) - (231, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("count=5", s);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceGetWrongTypeThrowsTypeError()
            {
#line (235, 5) - (235, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (236, 5) - (236, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (237, 5) - (237, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (238, 5) - (238, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["name"], "test"));
            }

            [Xunit.FactAttribute]
            public void TestNamespaceMissingAttributeThrowsAttributeError()
            {
#line (242, 5) - (242, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (243, 5) - (243, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (244, 5) - (247, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<AttributeError>((global::System.Action)(() =>
                {
#line (245, 9) - (245, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    var _ = ns["missing"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringShowsValues()
            {
#line (249, 5) - (249, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (250, 5) - (250, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--name", defaultValue: "test");
#line (251, 5) - (251, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true");
#line (252, 5) - (252, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose" });
#line (253, 5) - (253, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (254, 5) - (254, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("Namespace(", s);
#line (255, 5) - (255, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("name='test'", s);
#line (256, 5) - (256, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsDashInNameNormalizedToUnderscore()
            {
#line (262, 5) - (262, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (263, 5) - (263, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--my-flag", action: "store_true");
#line (264, 5) - (264, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--my-flag" });
#line (265, 5) - (265, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (266, 5) - (266, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("my_flag=True", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsCustomDestUsesCustomDest()
            {
#line (270, 5) - (270, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (271, 5) - (271, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddOptionalArgument("--output-file", dest: "output");
#line (272, 5) - (272, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--output-file", "out.txt" });
#line (273, 5) - (273, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["output"], "out.txt"));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidIntThrowsArgumentError()
            {
#line (279, 5) - (279, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (280, 5) - (280, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("count", type: "int");
#line (281, 5) - (284, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (282, 9) - (282, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "abc" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsInvalidFloatThrowsArgumentError()
            {
#line (286, 5) - (286, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (287, 5) - (287, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddArgument("value", type: "float");
#line (288, 5) - (293, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (289, 9) - (289, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "abc" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersAddParserParsesSubcommand()
            {
#line (295, 5) - (295, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (296, 5) - (296, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(dest: "command");
#line (297, 5) - (297, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser sub = subparsers.AddParser("run", help: "Run a program");
#line (298, 5) - (298, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                sub.AddArgument("filename");
#line (299, 5) - (299, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "run", "test.spy" });
#line (300, 5) - (300, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["command"], "run"));
#line (301, 5) - (301, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["filename"], "test.spy"));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersMultipleSubcommands()
            {
#line (305, 5) - (305, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (306, 5) - (306, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(dest: "cmd");
#line (307, 5) - (307, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser buildParser = subparsers.AddParser("build");
#line (308, 5) - (308, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                buildParser.AddOptionalArgument("--release", action: "store_true");
#line (309, 5) - (309, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser testParser = subparsers.AddParser("test");
#line (310, 5) - (310, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                testParser.AddOptionalArgument("--filter");
#line (311, 5) - (311, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns1 = parser.ParseArgs(new Sharpy.List<string>() { "build", "--release" });
#line (312, 5) - (312, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns1["cmd"], "build"));
#line (313, 5) - (313, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s1 = global::Sharpy.Builtins.Str(ns1);
#line (314, 5) - (314, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("release=True", s1);
#line (315, 5) - (315, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns2 = parser.ParseArgs(new Sharpy.List<string>() { "test", "--filter", "Lexer" });
#line (316, 5) - (316, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns2["cmd"], "test"));
#line (317, 5) - (317, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns2["filter"], "Lexer"));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersDuplicateThrowsError()
            {
#line (321, 5) - (321, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (322, 5) - (322, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                parser.AddSubparsers();
#line (323, 5) - (328, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (324, 9) - (324, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.AddSubparsers();
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupArgumentsParsedNormally()
            {
#line (330, 5) - (330, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (331, 5) - (331, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Network options");
#line (332, 5) - (332, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--host", defaultValue: "localhost");
#line (333, 5) - (333, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--port", type: "int", defaultValue: 8080);
#line (334, 5) - (334, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--host", "example.com", "--port", "9090" });
#line (335, 5) - (335, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["host"], "example.com"));
#line (336, 5) - (336, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (337, 5) - (337, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("port=9090", s);
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupDefaultsWork()
            {
#line (341, 5) - (341, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (342, 5) - (342, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Options");
#line (343, 5) - (343, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--name", defaultValue: "default");
#line (344, 5) - (344, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (345, 5) - (345, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["name"], "default"));
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupSingleOptionAccepted()
            {
#line (351, 5) - (351, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (352, 5) - (352, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup();
#line (353, 5) - (353, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (354, 5) - (354, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (355, 5) - (355, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose" });
#line (356, 5) - (356, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (357, 5) - (357, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (358, 5) - (358, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Contains("quiet=False", s);
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupBothOptionsThrowsError()
            {
#line (362, 5) - (362, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (363, 5) - (363, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup();
#line (364, 5) - (364, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (365, 5) - (365, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (366, 5) - (369, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (367, 9) - (367, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--quiet" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestMutuallyExclusiveGroupRequiredNoneProvidedThrowsError()
            {
#line (371, 5) - (371, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (372, 5) - (372, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup group = parser.AddMutuallyExclusiveGroup(required: true);
#line (373, 5) - (373, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--verbose", action: "store_true");
#line (374, 5) - (374, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                group.AddOptionalArgument("--quiet", action: "store_true");
#line (375, 5) - (377, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (376, 9) - (376, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { });
                }));
            }
        }
    }
}

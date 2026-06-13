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
using static Sharpy.Stdlib.Tests.Spy.Argparse.ArgparseAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Argparse
    {
        [global::Sharpy.SharpyModule("argparse.argparse_additional_tests")]
        public static partial class ArgparseAdditionalTests
        {
        }
    }

    public static partial class Argparse
    {
        public partial class ArgparseAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFormatHelpNonEmptyWhenNoArgs()
            {
#line (10, 5) - (10, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "test");
#line (11, 5) - (11, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (12, 5) - (12, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(helpText.Length > 0);
#line (13, 5) - (13, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("test", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpWithPositionalShowsPositionalSection()
            {
#line (17, 5) - (17, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "tool", addHelp: false);
#line (18, 5) - (18, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("input", help: "the input file");
#line (19, 5) - (19, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (20, 5) - (20, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("positional arguments", helpText);
#line (21, 5) - (21, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("input", helpText);
#line (22, 5) - (22, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("the input file", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpWithOptionalShowsOptionsSection()
            {
#line (26, 5) - (26, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "tool", addHelp: false);
#line (27, 5) - (27, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true", help: "verbose output");
#line (28, 5) - (28, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (29, 5) - (29, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("options", helpText);
#line (30, 5) - (30, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("--verbose", helpText);
#line (31, 5) - (31, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("verbose output", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpUsageLineContainsProg()
            {
#line (35, 5) - (35, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "myprogram", addHelp: false);
#line (36, 5) - (36, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (37, 5) - (37, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("usage: myprogram", helpText);
            }

            [Xunit.FactAttribute]
            public void TestProgDefaultValueIsProgString()
            {
#line (43, 5) - (43, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (44, 5) - (44, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("prog", parser.Prog);
            }

            [Xunit.FactAttribute]
            public void TestProgCanBeSetAfterConstruction()
            {
#line (48, 5) - (48, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "old");
#line (49, 5) - (49, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.Prog = "new";
#line (50, 5) - (50, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("new", parser.Prog);
#line (51, 5) - (51, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("new", parser.FormatHelp());
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringNoneValueShowsNone()
            {
#line (57, 5) - (57, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (58, 5) - (58, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--output");
#line (59, 5) - (59, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (60, 5) - (60, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (61, 5) - (61, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("Namespace(", s);
#line (62, 5) - (62, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("output=None", s);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringIntValueShowsNumber()
            {
#line (66, 5) - (66, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (67, 5) - (67, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 42);
#line (68, 5) - (68, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (69, 5) - (69, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (70, 5) - (70, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("count=42", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsFloatPositionalParsed()
            {
#line (76, 5) - (76, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (77, 5) - (77, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("ratio", type: "float");
#line (78, 5) - (78, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "3.14" });
#line (79, 5) - (79, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (80, 5) - (80, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("ratio=3.14", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsFloatOptionalParsed()
            {
#line (84, 5) - (84, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (85, 5) - (85, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--threshold", type: "float", defaultValue: 0.5d);
#line (86, 5) - (86, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--threshold", "0.75" });
#line (87, 5) - (87, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (88, 5) - (88, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("threshold=0.75", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalMissingValueThrowsArgumentError()
            {
#line (94, 5) - (94, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (95, 5) - (95, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--name");
#line (96, 5) - (99, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (97, 9) - (97, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--name" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsShortOptionMissingValueThrowsArgumentError()
            {
#line (101, 5) - (101, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (102, 5) - (102, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--name", shortName: "-n");
#line (103, 5) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (104, 9) - (104, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "-n" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsPlusMultipleValuesCollectsAll()
            {
#line (110, 5) - (110, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (111, 5) - (111, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("files", nargs: "+");
#line (112, 5) - (112, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt", "c.txt" });
#line (113, 5) - (113, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (114, 5) - (114, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupWithPositionalParsesToNamespace()
            {
#line (120, 5) - (120, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (121, 5) - (121, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Input options");
#line (122, 5) - (122, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                group.AddArgument("source", help: "source file");
#line (123, 5) - (123, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "input.txt" });
#line (124, 5) - (124, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["source"], "input.txt"));
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupMultipleGroupsBothParsedCorrectly()
            {
#line (128, 5) - (128, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (129, 5) - (129, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup g1 = parser.AddArgumentGroup("Network");
#line (130, 5) - (130, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--host", defaultValue: "localhost");
#line (131, 5) - (131, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup g2 = parser.AddArgumentGroup("Auth");
#line (132, 5) - (132, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--token", defaultValue: "none");
#line (133, 5) - (133, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--host", "example.com", "--token", "secret" });
#line (134, 5) - (134, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["host"], "example.com"));
#line (135, 5) - (135, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["token"], "secret"));
            }

            [Xunit.FactAttribute]
            public void TestTwoMutuallyExclusiveGroupsEachEnforcedIndependently()
            {
#line (141, 5) - (141, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (142, 5) - (142, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup g1 = parser.AddMutuallyExclusiveGroup();
#line (143, 5) - (143, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--verbose", action: "store_true");
#line (144, 5) - (144, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--quiet", action: "store_true");
#line (145, 5) - (145, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup g2 = parser.AddMutuallyExclusiveGroup();
#line (146, 5) - (146, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--json", action: "store_true");
#line (147, 5) - (147, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--csv", action: "store_true");
#line (149, 5) - (149, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--json" });
#line (150, 5) - (150, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (151, 5) - (151, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (152, 5) - (152, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("json=True", s);
#line (154, 5) - (159, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (155, 9) - (155, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--quiet" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersNoDestSubcommandParsed()
            {
#line (161, 5) - (161, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (162, 5) - (162, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers();
#line (163, 5) - (163, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser sub = subparsers.AddParser("run");
#line (164, 5) - (164, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                sub.AddArgument("file");
#line (165, 5) - (165, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "run", "script.spy" });
#line (166, 5) - (166, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["file"], "script.spy"));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersWithTitleSubcommandParsed()
            {
#line (170, 5) - (170, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (171, 5) - (171, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(title: "commands", dest: "cmd");
#line (172, 5) - (172, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                subparsers.AddParser("build");
#line (173, 5) - (173, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "build" });
#line (174, 5) - (174, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(@operator.Eq(ns["cmd"], "build"));
            }
        }
    }
}

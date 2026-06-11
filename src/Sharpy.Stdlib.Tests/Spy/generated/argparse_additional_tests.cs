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
using argparse = global::Sharpy.ArgparseModule;
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
#line (9, 5) - (9, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "test");
#line (10, 5) - (10, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (11, 5) - (11, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(helpText.Length > 0);
#line (12, 5) - (12, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("test", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpWithPositionalShowsPositionalSection()
            {
#line (16, 5) - (16, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "tool", addHelp: false);
#line (17, 5) - (17, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("input", help: "the input file");
#line (18, 5) - (18, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (19, 5) - (19, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("positional arguments", helpText);
#line (20, 5) - (20, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("input", helpText);
#line (21, 5) - (21, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("the input file", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpWithOptionalShowsOptionsSection()
            {
#line (25, 5) - (25, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "tool", addHelp: false);
#line (26, 5) - (26, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--verbose", action: "store_true", help: "verbose output");
#line (27, 5) - (27, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (28, 5) - (28, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("options", helpText);
#line (29, 5) - (29, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("--verbose", helpText);
#line (30, 5) - (30, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("verbose output", helpText);
            }

            [Xunit.FactAttribute]
            public void TestFormatHelpUsageLineContainsProg()
            {
#line (34, 5) - (34, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "myprogram", addHelp: false);
#line (35, 5) - (35, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string helpText = parser.FormatHelp();
#line (36, 5) - (36, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("usage: myprogram", helpText);
            }

            [Xunit.FactAttribute]
            public void TestProgDefaultValueIsProgString()
            {
#line (42, 5) - (42, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser();
#line (43, 5) - (43, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("prog", parser.Prog);
            }

            [Xunit.FactAttribute]
            public void TestProgCanBeSetAfterConstruction()
            {
#line (47, 5) - (47, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(prog: "old");
#line (48, 5) - (48, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.Prog = "new";
#line (49, 5) - (49, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("new", parser.Prog);
#line (50, 5) - (50, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("new", parser.FormatHelp());
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringNoneValueShowsNone()
            {
#line (56, 5) - (56, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (57, 5) - (57, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--output");
#line (58, 5) - (58, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (59, 5) - (59, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (60, 5) - (60, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("Namespace(", s);
#line (61, 5) - (61, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("output=None", s);
            }

            [Xunit.FactAttribute]
            public void TestNamespaceToStringIntValueShowsNumber()
            {
#line (65, 5) - (65, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (66, 5) - (66, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--count", type: "int", defaultValue: 42);
#line (67, 5) - (67, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { });
#line (68, 5) - (68, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (69, 5) - (69, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("count=42", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsFloatPositionalParsed()
            {
#line (75, 5) - (75, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (76, 5) - (76, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("ratio", type: "float");
#line (77, 5) - (77, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "3.14" });
#line (78, 5) - (78, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (79, 5) - (79, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("ratio=3.14", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsFloatOptionalParsed()
            {
#line (83, 5) - (83, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (84, 5) - (84, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--threshold", type: "float", defaultValue: 0.5d);
#line (85, 5) - (85, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--threshold", "0.75" });
#line (86, 5) - (86, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (87, 5) - (87, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("threshold=0.75", s);
            }

            [Xunit.FactAttribute]
            public void TestParseArgsOptionalMissingValueThrowsArgumentError()
            {
#line (93, 5) - (93, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (94, 5) - (94, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--name");
#line (95, 5) - (98, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (96, 9) - (96, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--name" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsShortOptionMissingValueThrowsArgumentError()
            {
#line (100, 5) - (100, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (101, 5) - (101, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddOptionalArgument("--name", shortName: "-n");
#line (102, 5) - (107, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (103, 9) - (103, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "-n" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestParseArgsNargsPlusMultipleValuesCollectsAll()
            {
#line (109, 5) - (109, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (110, 5) - (110, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                parser.AddArgument("files", nargs: "+");
#line (111, 5) - (111, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "a.txt", "b.txt", "c.txt" });
#line (112, 5) - (112, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.True(ns.Contains("files"));
#line (113, 5) - (113, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.NotNull(ns["files"]);
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupWithPositionalParsesToNamespace()
            {
#line (119, 5) - (119, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (120, 5) - (120, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup group = parser.AddArgumentGroup("Input options");
#line (121, 5) - (121, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                group.AddArgument("source", help: "source file");
#line (122, 5) - (122, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "input.txt" });
#line (123, 5) - (123, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("input.txt", ns["source"]);
            }

            [Xunit.FactAttribute]
            public void TestAddArgumentGroupMultipleGroupsBothParsedCorrectly()
            {
#line (127, 5) - (127, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (128, 5) - (128, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup g1 = parser.AddArgumentGroup("Network");
#line (129, 5) - (129, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--host", defaultValue: "localhost");
#line (130, 5) - (130, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentGroup g2 = parser.AddArgumentGroup("Auth");
#line (131, 5) - (131, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--token", defaultValue: "none");
#line (132, 5) - (132, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--host", "example.com", "--token", "secret" });
#line (133, 5) - (133, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("example.com", ns["host"]);
#line (134, 5) - (134, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("secret", ns["token"]);
            }

            [Xunit.FactAttribute]
            public void TestTwoMutuallyExclusiveGroupsEachEnforcedIndependently()
            {
#line (140, 5) - (140, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (141, 5) - (141, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup g1 = parser.AddMutuallyExclusiveGroup();
#line (142, 5) - (142, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--verbose", action: "store_true");
#line (143, 5) - (143, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g1.AddOptionalArgument("--quiet", action: "store_true");
#line (144, 5) - (144, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.MutuallyExclusiveGroup g2 = parser.AddMutuallyExclusiveGroup();
#line (145, 5) - (145, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--json", action: "store_true");
#line (146, 5) - (146, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                g2.AddOptionalArgument("--csv", action: "store_true");
#line (148, 5) - (148, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--json" });
#line (149, 5) - (149, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                string s = global::Sharpy.Builtins.Str(ns);
#line (150, 5) - (150, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("verbose=True", s);
#line (151, 5) - (151, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Contains("json=True", s);
#line (153, 5) - (158, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Throws<ArgumentError>((global::System.Action)(() =>
                {
#line (154, 9) - (154, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                    parser.ParseArgs(new Sharpy.List<string>() { "--verbose", "--quiet" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersNoDestSubcommandParsed()
            {
#line (160, 5) - (160, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (161, 5) - (161, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers();
#line (162, 5) - (162, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser sub = subparsers.AddParser("run");
#line (163, 5) - (163, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                sub.AddArgument("file");
#line (164, 5) - (164, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "run", "script.spy" });
#line (165, 5) - (165, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("script.spy", ns["file"]);
            }

            [Xunit.FactAttribute]
            public void TestAddSubparsersWithTitleSubcommandParsed()
            {
#line (169, 5) - (169, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.ArgumentParser parser = new global::Sharpy.ArgumentParser(addHelp: false);
#line (170, 5) - (170, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.SubparsersAction subparsers = parser.AddSubparsers(title: "commands", dest: "cmd");
#line (171, 5) - (171, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                subparsers.AddParser("build");
#line (172, 5) - (172, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                global::Sharpy.Namespace ns = parser.ParseArgs(new Sharpy.List<string>() { "build" });
#line (173, 5) - (173, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/argparse/argparse_additional_tests.spy"
                Xunit.Assert.Equal("build", ns["cmd"]);
            }
        }
    }
}

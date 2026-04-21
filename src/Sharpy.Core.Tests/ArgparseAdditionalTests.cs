using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    /// <summary>
    /// Additional tests for ArgumentParser covering scenarios not in ArgparseTests.cs.
    /// </summary>
    public class ArgparseAdditionalTests
    {
        #region FormatHelp Additional

        [Fact]
        public void FormatHelp_NonEmpty_WhenNoArgs()
        {
            var parser = new ArgumentParser(prog: "test");
            string help = parser.FormatHelp();
            Assert.False(string.IsNullOrEmpty(help));
            Assert.Contains("test", help, StringComparison.Ordinal);
        }

        [Fact]
        public void FormatHelp_WithPositional_ShowsPositionalSection()
        {
            var parser = new ArgumentParser(prog: "tool", addHelp: false);
            parser.AddArgument("input", help: "the input file");
            string help = parser.FormatHelp();
            Assert.Contains("positional arguments", help, StringComparison.Ordinal);
            Assert.Contains("input", help, StringComparison.Ordinal);
            Assert.Contains("the input file", help, StringComparison.Ordinal);
        }

        [Fact]
        public void FormatHelp_WithOptional_ShowsOptionsSection()
        {
            var parser = new ArgumentParser(prog: "tool", addHelp: false);
            parser.AddOptionalArgument("--verbose", action: "store_true", help: "verbose output");
            string help = parser.FormatHelp();
            Assert.Contains("options", help, StringComparison.Ordinal);
            Assert.Contains("--verbose", help, StringComparison.Ordinal);
            Assert.Contains("verbose output", help, StringComparison.Ordinal);
        }

        [Fact]
        public void FormatHelp_UsageLineContainsProg()
        {
            var parser = new ArgumentParser(prog: "myprogram", addHelp: false);
            string help = parser.FormatHelp();
            Assert.Contains("usage: myprogram", help, StringComparison.Ordinal);
        }

        #endregion

        #region ArgumentParser.Prog

        [Fact]
        public void Prog_DefaultValue_IsProgString()
        {
            var parser = new ArgumentParser();
            Assert.Equal("prog", parser.Prog);
        }

        [Fact]
        public void Prog_CanBeSetAfterConstruction()
        {
            var parser = new ArgumentParser(prog: "old");
            parser.Prog = "new";
            Assert.Equal("new", parser.Prog);
            Assert.Contains("new", parser.FormatHelp(), StringComparison.Ordinal);
        }

        #endregion

        #region Namespace.ToString Additional

        [Fact]
        public void Namespace_ToString_NoneValue_ShowsNone()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--output");
            var ns = parser.ParseArgs(new string[0]);
            string s = ns.ToString();
            Assert.Contains("Namespace(", s, StringComparison.Ordinal);
            Assert.Contains("output=None", s, StringComparison.Ordinal);
        }

        [Fact]
        public void Namespace_ToString_IntValue_ShowsNumber()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--count", type: "int", defaultValue: 42);
            var ns = parser.ParseArgs(new string[0]);
            string s = ns.ToString();
            Assert.Contains("count=42", s, StringComparison.Ordinal);
        }

        #endregion

        #region Float Type Conversion

        [Fact]
        public void ParseArgs_FloatPositional_Parsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("ratio", type: "float");
            var ns = parser.ParseArgs(new[] { "3.14" });
            Assert.Equal(3.14, (double)ns["ratio"]!, 5);
        }

        [Fact]
        public void ParseArgs_FloatOptional_Parsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--threshold", type: "float", defaultValue: 0.5);
            var ns = parser.ParseArgs(new[] { "--threshold", "0.75" });
            Assert.Equal(0.75, (double)ns["threshold"]!, 5);
        }

        #endregion

        #region Missing Optional Value

        [Fact]
        public void ParseArgs_OptionalMissingValue_ThrowsArgumentError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "--name" }));
        }

        [Fact]
        public void ParseArgs_ShortOptionMissingValue_ThrowsArgumentError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", shortName: "-n");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "-n" }));
        }

        #endregion

        #region Nargs Plus with Multiple Values

        [Fact]
        public void ParseArgs_NargsPlus_MultipleValues_CollectsAll()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("files", nargs: "+");
            var ns = parser.ParseArgs(new[] { "a.txt", "b.txt", "c.txt" });
            var files = ns["files"] as List<string>;
            Assert.NotNull(files);
            Assert.Equal(3, ((ICollection<string>)files!).Count);
        }

        #endregion

        #region ArgumentGroup with Positional

        [Fact]
        public void AddArgumentGroup_WithPositional_ParsesToNamespace()
        {
            var parser = new ArgumentParser(addHelp: false);
            var group = parser.AddArgumentGroup("Input options");
            group.AddArgument("source", help: "source file");
            var ns = parser.ParseArgs(new[] { "input.txt" });
            Assert.Equal("input.txt", ns["source"]);
        }

        [Fact]
        public void AddArgumentGroup_MultipleGroups_BothParsedCorrectly()
        {
            var parser = new ArgumentParser(addHelp: false);
            var g1 = parser.AddArgumentGroup("Network");
            g1.AddOptionalArgument("--host", defaultValue: "localhost");
            var g2 = parser.AddArgumentGroup("Auth");
            g2.AddOptionalArgument("--token", defaultValue: "none");
            var ns = parser.ParseArgs(new[] { "--host", "example.com", "--token", "secret" });
            Assert.Equal("example.com", ns["host"]);
            Assert.Equal("secret", ns["token"]);
        }

        #endregion

        #region Multiple Mutually Exclusive Groups

        [Fact]
        public void TwoMutuallyExclusiveGroups_EachEnforcedIndependently()
        {
            var parser = new ArgumentParser(addHelp: false);
            var g1 = parser.AddMutuallyExclusiveGroup();
            g1.AddOptionalArgument("--verbose", action: "store_true");
            g1.AddOptionalArgument("--quiet", action: "store_true");
            var g2 = parser.AddMutuallyExclusiveGroup();
            g2.AddOptionalArgument("--json", action: "store_true");
            g2.AddOptionalArgument("--csv", action: "store_true");

            // One from each group is fine
            var ns = parser.ParseArgs(new[] { "--verbose", "--json" });
            Assert.Equal(true, ns["verbose"]);
            Assert.Equal(true, ns["json"]);

            // Both from same group should fail
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "--verbose", "--quiet" }));
        }

        #endregion

        #region Subparsers with Dest=empty

        [Fact]
        public void AddSubparsers_NoDest_SubcommandParsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            var subparsers = parser.AddSubparsers();
            var sub = subparsers.AddParser("run");
            sub.AddArgument("file");
            var ns = parser.ParseArgs(new[] { "run", "script.spy" });
            Assert.Equal("script.spy", ns["file"]);
        }

        [Fact]
        public void AddSubparsers_WithTitle_SubcommandParsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            var subparsers = parser.AddSubparsers(title: "commands", dest: "cmd");
            subparsers.AddParser("build");
            var ns = parser.ParseArgs(new[] { "build" });
            Assert.Equal("build", ns["cmd"]);
        }

        #endregion

        #region SetOutput and Help

        [Fact]
        public void SetOutput_RedirectsHelpOutput()
        {
            var parser = new ArgumentParser(description: "Test app", prog: "test");
            var writer = new System.IO.StringWriter();
            parser.SetOutput(writer);
            Assert.Throws<SystemExit>(() => parser.ParseArgs(new[] { "--help" }));
            string output = writer.ToString();
            Assert.Contains("Test app", output, StringComparison.Ordinal);
        }

        #endregion
    }
}

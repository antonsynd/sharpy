using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class ArgparseTests
    {
        #region Positional Arguments

        [Fact]
        public void ParseArgs_SinglePositional_Parsed()
        {
            var parser = new ArgumentParser();
            parser.AddArgument("filename");
            var ns = parser.ParseArgs(new[] { "test.txt" });
            Assert.Equal("test.txt", ns["filename"]);
        }

        [Fact]
        public void ParseArgs_MultiplePositionals_Parsed()
        {
            var parser = new ArgumentParser();
            parser.AddArgument("source");
            parser.AddArgument("dest");
            var ns = parser.ParseArgs(new[] { "a.txt", "b.txt" });
            Assert.Equal("a.txt", ns["source"]);
            Assert.Equal("b.txt", ns["dest"]);
        }

        [Fact]
        public void ParseArgs_PositionalWithType_Converted()
        {
            var parser = new ArgumentParser();
            parser.AddArgument("count", type: "int");
            var ns = parser.ParseArgs(new[] { "42" });
            Assert.Equal(42, ns["count"]);
        }

        [Fact]
        public void ParseArgs_MissingPositional_ThrowsArgumentError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("filename");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new string[0]));
        }

        #endregion

        #region Optional Arguments

        [Fact]
        public void ParseArgs_LongOption_Parsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name");
            var ns = parser.ParseArgs(new[] { "--name", "test" });
            Assert.Equal("test", ns["name"]);
        }

        [Fact]
        public void ParseArgs_ShortAndLongOption_Parsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", shortName: "-n");
            var ns = parser.ParseArgs(new[] { "-n", "test" });
            Assert.Equal("test", ns["name"]);
        }

        [Fact]
        public void ParseArgs_OptionalWithDefault_UsesDefault()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", defaultValue: "default");
            var ns = parser.ParseArgs(new string[0]);
            Assert.Equal("default", ns["name"]);
        }

        [Fact]
        public void ParseArgs_OptionalWithType_Converted()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--count", type: "int");
            var ns = parser.ParseArgs(new[] { "--count", "5" });
            Assert.Equal(5, ns["count"]);
        }

        [Fact]
        public void ParseArgs_RequiredOptional_Missing_ThrowsError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", required: true);
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new string[0]));
        }

        #endregion

        #region Actions

        [Fact]
        public void ParseArgs_StoreTrue_SetsTrue()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
            var ns = parser.ParseArgs(new[] { "-v" });
            Assert.Equal(true, ns["verbose"]);
        }

        [Fact]
        public void ParseArgs_StoreTrue_DefaultFalse()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--verbose", action: "store_true");
            var ns = parser.ParseArgs(new string[0]);
            Assert.Equal(false, ns["verbose"]);
        }

        [Fact]
        public void ParseArgs_StoreFalse_SetsFalse()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--no-feature", action: "store_false", dest: "feature");
            var ns = parser.ParseArgs(new[] { "--no-feature" });
            Assert.Equal(false, ns["feature"]);
        }

        [Fact]
        public void ParseArgs_Count_CountsOccurrences()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--verbose", shortName: "-v", action: "count");
            var ns = parser.ParseArgs(new[] { "-v", "-v", "-v" });
            Assert.Equal(3, ns["verbose"]);
        }

        [Fact]
        public void ParseArgs_Append_CollectsValues()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--item", action: "append");
            var ns = parser.ParseArgs(new[] { "--item", "a", "--item", "b" });
            var items = ns["item"] as List<object?>;
            Assert.NotNull(items);
            Assert.Equal(2, ((ICollection<object?>)items!).Count);
            Assert.Equal("a", items![0]);
            Assert.Equal("b", items[1]);
        }

        #endregion

        #region Choices

        [Fact]
        public void ParseArgs_ValidChoice_Accepted()
        {
            var parser = new ArgumentParser(addHelp: false);
            var choices = new List<string>();
            choices.Append("debug");
            choices.Append("info");
            choices.Append("warning");
            parser.AddOptionalArgument("--level", choices: choices);
            var ns = parser.ParseArgs(new[] { "--level", "info" });
            Assert.Equal("info", ns["level"]);
        }

        [Fact]
        public void ParseArgs_InvalidChoice_ThrowsError()
        {
            var parser = new ArgumentParser(addHelp: false);
            var choices = new List<string>();
            choices.Append("debug");
            choices.Append("info");
            parser.AddOptionalArgument("--level", choices: choices);
            var ex = Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "--level", "error" }));
            Assert.Contains("invalid choice", ex.Message, StringComparison.Ordinal);
        }

        #endregion

        #region Nargs

        [Fact]
        public void ParseArgs_NargsStar_CollectsZeroOrMore()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("files", nargs: "*");
            var ns = parser.ParseArgs(new[] { "a.txt", "b.txt" });
            var files = ns["files"] as List<string>;
            Assert.NotNull(files);
            Assert.Equal(2, ((ICollection<string>)files!).Count);
        }

        [Fact]
        public void ParseArgs_NargsStar_Empty_ReturnsEmptyList()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("files", nargs: "*");
            var ns = parser.ParseArgs(new string[0]);
            var files = ns["files"] as List<string>;
            Assert.NotNull(files);
            Assert.Empty(files!);
        }

        [Fact]
        public void ParseArgs_NargsPlus_RequiresAtLeastOne()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("files", nargs: "+");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new string[0]));
        }

        [Fact]
        public void ParseArgs_NargsQuestion_OptionalValue()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--output", nargs: "?", defaultValue: "stdout");
            var ns = parser.ParseArgs(new[] { "--output" });
            Assert.Equal("stdout", ns["output"]);
        }

        #endregion

        #region Mixed Arguments

        [Fact]
        public void ParseArgs_MixedPositionalAndOptional_Parsed()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("filename");
            parser.AddOptionalArgument("--verbose", shortName: "-v", action: "store_true");
            parser.AddOptionalArgument("--count", type: "int", defaultValue: 1);

            var ns = parser.ParseArgs(new[] { "test.txt", "-v", "--count", "5" });
            Assert.Equal("test.txt", ns["filename"]);
            Assert.Equal(true, ns["verbose"]);
            Assert.Equal(5, ns["count"]);
        }

        [Fact]
        public void ParseArgs_UnrecognizedArgument_ThrowsError()
        {
            var parser = new ArgumentParser(addHelp: false);
            var ex = Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "--unknown" }));
            Assert.Contains("unrecognized", ex.Message, StringComparison.Ordinal);
        }

        #endregion

        #region Help

        [Fact]
        public void ParseArgs_Help_ThrowsSystemExit()
        {
            var parser = new ArgumentParser(description: "A test program");
            parser.SetOutput(System.IO.TextWriter.Null);
            Assert.Throws<SystemExit>(() => parser.ParseArgs(new[] { "--help" }));
        }

        [Fact]
        public void FormatHelp_ContainsDescription()
        {
            var parser = new ArgumentParser(description: "A test program", prog: "myapp");
            parser.AddArgument("input");
            parser.AddOptionalArgument("--output", shortName: "-o", help: "output file");

            string help = parser.FormatHelp();
            Assert.Contains("A test program", help, StringComparison.Ordinal);
            Assert.Contains("myapp", help, StringComparison.Ordinal);
            Assert.Contains("input", help, StringComparison.Ordinal);
            Assert.Contains("output file", help, StringComparison.Ordinal);
        }

        [Fact]
        public void FormatHelp_ShowsHelpOption()
        {
            var parser = new ArgumentParser();
            string help = parser.FormatHelp();
            Assert.Contains("-h, --help", help, StringComparison.Ordinal);
            Assert.Contains("show this help message and exit", help, StringComparison.Ordinal);
        }

        #endregion

        #region Namespace

        [Fact]
        public void Namespace_Contains_Works()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", defaultValue: "test");
            var ns = parser.ParseArgs(new string[0]);
            Assert.True(ns.Contains("name"));
            Assert.False(ns.Contains("missing"));
        }

        [Fact]
        public void Namespace_Get_ReturnsTyped()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--count", type: "int", defaultValue: 5);
            var ns = parser.ParseArgs(new string[0]);
            Assert.Equal(5, ns.Get<int>("count"));
        }

        [Fact]
        public void Namespace_Get_WrongType_ThrowsTypeError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", defaultValue: "test");
            var ns = parser.ParseArgs(new string[0]);
            Assert.Throws<TypeError>(() => ns.Get<int>("name"));
        }

        [Fact]
        public void Namespace_MissingAttribute_ThrowsAttributeError()
        {
            var parser = new ArgumentParser(addHelp: false);
            var ns = parser.ParseArgs(new string[0]);
            Assert.Throws<AttributeError>(() => { var _ = ns["missing"]; });
        }

        [Fact]
        public void Namespace_ToString_ShowsValues()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--name", defaultValue: "test");
            parser.AddOptionalArgument("--verbose", action: "store_true");
            var ns = parser.ParseArgs(new[] { "--verbose" });

            string s = ns.ToString();
            Assert.Contains("Namespace(", s, StringComparison.Ordinal);
            Assert.Contains("name='test'", s, StringComparison.Ordinal);
            Assert.Contains("verbose=True", s, StringComparison.Ordinal);
        }

        #endregion

        #region Dest Normalization

        [Fact]
        public void ParseArgs_DashInName_NormalizedToUnderscore()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--my-flag", action: "store_true");
            var ns = parser.ParseArgs(new[] { "--my-flag" });
            Assert.Equal(true, ns["my_flag"]);
        }

        [Fact]
        public void ParseArgs_CustomDest_UsesCustomDest()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddOptionalArgument("--output-file", dest: "output");
            var ns = parser.ParseArgs(new[] { "--output-file", "out.txt" });
            Assert.Equal("out.txt", ns["output"]);
        }

        #endregion

        #region Type Conversion Errors

        [Fact]
        public void ParseArgs_InvalidInt_ThrowsArgumentError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("count", type: "int");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "abc" }));
        }

        [Fact]
        public void ParseArgs_InvalidFloat_ThrowsArgumentError()
        {
            var parser = new ArgumentParser(addHelp: false);
            parser.AddArgument("value", type: "float");
            Assert.Throws<ArgumentError>(() => parser.ParseArgs(new[] { "abc" }));
        }

        #endregion
    }
}

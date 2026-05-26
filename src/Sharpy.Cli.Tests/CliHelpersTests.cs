using FluentAssertions;
using Sharpy.Cli;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Cli.Tests;

public class CliHelpersTests
{
    [Fact]
    public void CreateLogger_None_ReturnsDisabledLogger()
    {
        var logger = CliHelpers.CreateLogger(CompilerLogLevel.None, logFile: null);

        logger.IsEnabled(CompilerLogLevel.Error).Should().BeFalse();
        logger.IsEnabled(CompilerLogLevel.Debug).Should().BeFalse();
    }

    [Fact]
    public void CreateLogger_None_ReturnsSharedInstance()
    {
        var a = CliHelpers.CreateLogger(CompilerLogLevel.None, logFile: null);
        var b = CliHelpers.CreateLogger(CompilerLogLevel.None, logFile: null);

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CreateLogger_Debug_EnablesLowerLevels()
    {
        var logger = CliHelpers.CreateLogger(CompilerLogLevel.Debug, logFile: null);

        logger.IsEnabled(CompilerLogLevel.Error).Should().BeTrue();
        logger.IsEnabled(CompilerLogLevel.Info).Should().BeTrue();
        logger.IsEnabled(CompilerLogLevel.Debug).Should().BeTrue();
        logger.IsEnabled(CompilerLogLevel.Trace).Should().BeFalse();
    }

    [Fact]
    public void CreateLogger_Error_OnlyEnablesError()
    {
        var logger = CliHelpers.CreateLogger(CompilerLogLevel.Error, logFile: null);

        logger.IsEnabled(CompilerLogLevel.Error).Should().BeTrue();
        logger.IsEnabled(CompilerLogLevel.Warning).Should().BeFalse();
    }

    [Fact]
    public void CreateLogger_WithLogFile_CreatesFileAndEnablesLogging()
    {
        using var ws = new TempWorkspace();
        var logPath = ws.PathFor("compiler.log");

        var logger = CliHelpers.CreateLogger(CompilerLogLevel.Debug, new FileInfo(logPath));

        // The log-file branch opens a StreamWriter on the target path, creating it eagerly.
        logger.IsEnabled(CompilerLogLevel.Debug).Should().BeTrue();
        File.Exists(logPath).Should().BeTrue();
    }

    [Fact]
    public void GetDefaultReferences_IncludesSharpyCore()
    {
        var refs = CliHelpers.GetDefaultReferences();

        refs.Should().NotBeEmpty();
        refs.Should().Contain(r => r.EndsWith("Sharpy.Core.dll"));
        refs.Should().OnlyContain(r => File.Exists(r));
    }

    [Fact]
    public void ParseNowarnCodes_Null_ReturnsEmpty()
    {
        CliHelpers.ParseNowarnCodes(null).Should().BeEmpty();
    }

    [Fact]
    public void ParseNowarnCodes_Whitespace_ReturnsEmpty()
    {
        CliHelpers.ParseNowarnCodes("   ").Should().BeEmpty();
    }

    [Fact]
    public void ParseNowarnCodes_SplitsAndTrims()
    {
        var codes = CliHelpers.ParseNowarnCodes(" SPY0451 , SPY0452 ,");

        codes.Should().HaveCount(2);
        codes.Should().Contain("SPY0451");
        codes.Should().Contain("SPY0452");
    }

    [Fact]
    public void ParseNowarnCodes_IsCaseInsensitive()
    {
        var codes = CliHelpers.ParseNowarnCodes("SPY0451");

        codes.Contains("spy0451").Should().BeTrue();
    }

    [Fact]
    public void StripLineDirectives_RemovesLineDirectiveLines()
    {
        var input = "int x = 1;\n#line 5 \"foo.spy\"\nint y = 2;\n    #line 6\nint z = 3;";

        var result = CliHelpers.StripLineDirectives(input);

        result.Should().NotContain("#line");
        result.Should().Contain("int x = 1;");
        result.Should().Contain("int y = 2;");
        result.Should().Contain("int z = 3;");
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatBytes_FormatsHumanReadable(long bytes, string expected)
    {
        CliHelpers.FormatBytes(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(CompilerPhase.Lexer, "Lexer errors")]
    [InlineData(CompilerPhase.Parser, "Parse errors")]
    [InlineData(CompilerPhase.TypeChecking, "Type errors")]
    [InlineData(CompilerPhase.CodeGeneration, "Code generation errors")]
    public void PhaseLabel_Errors(CompilerPhase phase, string expected)
    {
        CliHelpers.PhaseLabel(phase, isWarnings: false).Should().Be(expected);
    }

    [Fact]
    public void PhaseLabel_Warnings_UsesWarningsSuffix()
    {
        CliHelpers.PhaseLabel(CompilerPhase.Validation, isWarnings: true).Should().Be("Validation warnings");
    }

    [Theory]
    [InlineData("Lexer", "33")]
    [InlineData("Semantic", "31")]
    [InlineData("Validation", "34")]
    [InlineData("CodeGen", "32")]
    [InlineData("Infrastructure", "36")]
    [InlineData("Unknown", "37")]
    public void CategoryColor_MapsKnownCategories(string category, string expected)
    {
        CliHelpers.CategoryColor(category).Should().Be(expected);
    }

    [Fact]
    public void CliBold_PreservesText()
    {
        CliHelpers.CliBold("hello").Should().Contain("hello");
    }

    [Fact]
    public void CliColor_PreservesText()
    {
        CliHelpers.CliColor("hello", "31").Should().Contain("hello");
    }

    [Fact]
    public void RenderDiagnostic_WritesMessageAndCode()
    {
        var diagnostic = new CompilerDiagnostic(
            Message: "something went wrong",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 3,
            Column: 7,
            Code: "SPY0200",
            Phase: CompilerPhase.TypeChecking);

        using var writer = new StringWriter();
        CliHelpers.RenderDiagnostic(diagnostic, sourceText: null, writer);

        var output = writer.ToString();
        output.Should().Contain("something went wrong");
        output.Should().Contain("SPY0200");
    }

    [Fact]
    public void RenderDiagnostics_EmptyList_DoesNotThrow()
    {
        using var writer = new StringWriter();

        var act = () => CliHelpers.RenderDiagnostics(Array.Empty<CompilerDiagnostic>(), sourceText: null, writer);

        act.Should().NotThrow();
    }

    [Fact]
    public void OutputMetrics_NullMetrics_DoesNotThrow()
    {
        var act = () => CliHelpers.OutputMetrics(metrics: null, metricsFormat: "json", metricsOutput: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void PhaseOrder_EndsWithUnknown()
    {
        CliHelpers.PhaseOrder.Should().NotBeEmpty();
        CliHelpers.PhaseOrder[^1].Should().Be(CompilerPhase.Unknown);
    }
}

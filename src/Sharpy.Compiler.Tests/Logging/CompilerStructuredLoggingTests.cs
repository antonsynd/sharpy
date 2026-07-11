using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Logging;

/// <summary>
/// Phase-observability tests for the single-file compile facade.
///
/// The single-file <c>Compiler</c> now drives the unified <c>ProjectCompiler</c> pipeline
/// (#1038), which surfaces per-phase timing/counts through <c>CompilationResult.Metrics</c>
/// (the same surface the CLI's <c>--verbose</c> output uses) rather than through
/// <c>StructuredLogger</c> phase events. The structured <c>PhaseStart/End/CodeGenEvent</c>
/// stream was test-only and is tracked for project-mode parity in #1077; these tests assert
/// the production-observable metrics surface instead. The <c>StructuredLogger</c> component
/// itself is still covered directly below.
/// </summary>
public class CompilerStructuredLoggingTests
{
    // Valid Sharpy program with main() function
    private const string ValidProgram = @"
def main():
    print(42)
";

    private const string ValidProgramWithVar = @"
def main():
    x: int = 1
    print(x)
";

    [Fact]
    public void Compile_PopulatesPhaseMetrics()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        var phaseNames = result.Metrics!.Phases.Select(p => p.Name).ToList();
        phaseNames.Should().Contain(CompilerPhaseNames.LexicalAnalysis);
        phaseNames.Should().Contain(CompilerPhaseNames.SyntaxAnalysis);
        phaseNames.Should().Contain(CompilerPhaseNames.TypeChecking);
        phaseNames.Should().Contain(CompilerPhaseNames.CodeGeneration);
    }

    [Fact]
    public void Compile_PhaseMetricsHaveNonNegativeDurations()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();
        result.Metrics!.Phases.Should().OnlyContain(
            p => p.Duration >= TimeSpan.Zero, "each phase should have a non-negative duration");
        result.Metrics.TotalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Compile_SuccessfulCompilation_HasNoDiagnostics()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();
        result.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Compile_WithSyntaxError_SurfacesParserError()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile("def foo(", "test.spy");

        result.Success.Should().BeFalse();
        // The syntax error is surfaced with the Parser phase stamped on the diagnostic.
        result.Diagnostics.GetErrors().Should().Contain(d => d.Phase == CompilerPhase.Parser);
    }

    [Fact]
    public void Compile_WithTypeError_SurfacesTypeCheckingError()
    {
        var compiler = new Compiler(new CompilerOptions());

        var source = @"
def main():
    x: int = ""not an int""
    print(x)
";
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors().Should().Contain(d => d.Phase == CompilerPhase.TypeChecking);
    }

    [Fact]
    public void Compile_ProducesGeneratedCode()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();
        result.GeneratedCSharpCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SourceText_CarriesFilePath()
    {
        var compiler = new Compiler(new CompilerOptions());

        var result = compiler.Compile(ValidProgramWithVar, "myfile.spy");

        result.SourceText!.FilePath.Should().Be("myfile.spy");
    }

    [Fact]
    public void NullLogger_IgnoresStructuredEvents()
    {
        // Verify NullLogger implements the default interface method correctly (no-op)
        // Access through interface to use default interface methods
        ICompilerLogger logger = NullLogger.Instance;

        // This should not throw
        logger.LogEvent(new PhaseStartEvent("Test"));
        logger.LogEvent(new PhaseEndEvent("Test", TimeSpan.Zero));

        // NullLogger should not support structured logging (uses default from interface)
        logger.SupportsStructuredLogging.Should().BeFalse();
    }
}

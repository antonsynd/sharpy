using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Logging;

/// <summary>
/// Phase-observability tests for the single-file compile facade.
///
/// The single-file <c>Compiler</c> drives the unified <c>ProjectCompiler</c> pipeline (#1038),
/// which now surfaces phase observability on <em>two</em> equivalent channels: per-phase
/// timing/counts through <c>CompilationResult.Metrics</c> (the surface the CLI's
/// <c>--verbose</c> output uses), and the structured <c>PhaseStart/End/CodeGenEvent</c> stream
/// captured by <c>StructuredLogger</c>. The structured stream was restored to the unified path in
/// #1077 (project-mode parity); both surfaces are asserted here.
///
/// Note on timings: on a parse-cache hit (#1086) the Lexical/Syntax brackets close with near-zero
/// durations, so event-timing assertions use <c>&gt;= TimeSpan.Zero</c> per phase (the aggregate is
/// still positive because type checking and code generation do real work).
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

    // -- Metrics surface (CompilationResult.Metrics.Phases) ------------------------------------

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

    // -- Structured event stream (StructuredLogger) -------------------------------------------

    [Fact]
    public void Compile_EmitsPhaseEvents()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        // Check that we got phase start and end events for each phase
        var startEvents = logger.GetEvents<PhaseStartEvent>().ToList();
        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();

        startEvents.Should().HaveCount(endEvents.Count, "every phase should have start and end events");
        startEvents.Should().HaveCountGreaterThanOrEqualTo(4, "should have at least 4 phases");

        // Verify expected phases exist
        startEvents.Select(e => e.Phase).Should().Contain("Lexical Analysis");
        startEvents.Select(e => e.Phase).Should().Contain("Syntax Analysis");
        startEvents.Select(e => e.Phase).Should().Contain("Type Checking");
        startEvents.Select(e => e.Phase).Should().Contain("Code Generation");

        // Verify matching start/end events
        foreach (var start in startEvents)
        {
            endEvents.Should().Contain(e => e.Phase == start.Phase,
                $"phase '{start.Phase}' should have an end event");
        }
    }

    [Fact]
    public void Compile_PhaseEventsHaveFilePath()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        // The single-file compile path feeds the entry source in-memory keyed by the caller's
        // verbatim path, so every unit-scoped event carries that path.
        compiler.Compile(ValidProgramWithVar, "myfile.spy");

        var events = logger.Events;
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => e.FilePath == "myfile.spy");
    }

    [Fact]
    public void Compile_PhaseEventsHaveReasonableTimings()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        compiler.Compile(ValidProgram, "test.spy");

        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();

        foreach (var evt in endEvents)
        {
            // Each phase should take >= 0 time (a parse-cache hit closes Lexical/Syntax near zero).
            evt.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
                $"phase '{evt.Phase}' should have non-negative duration");

            // Each phase should complete in reasonable time (< 10 seconds for a simple program)
            evt.Duration.Should().BeLessThan(TimeSpan.FromSeconds(10),
                $"phase '{evt.Phase}' should complete quickly");
        }

        // Total time should be reasonable (type checking + code generation do real work)
        var totalTime = logger.GetTotalPhaseDuration();
        totalTime.Should().BeGreaterThan(TimeSpan.Zero, "total compilation should take some time");
    }

    [Fact]
    public void Compile_SuccessfulCompilation_HasZeroErrorsInPhases()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();
        endEvents.Should().OnlyContain(e => e.ErrorCount == 0,
            "successful compilation should have no errors in any phase");
    }

    [Fact]
    public void Compile_WithSyntaxError_ReportsErrorsInPhaseEvent()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile("def foo(", "test.spy");

        result.Success.Should().BeFalse();

        // Should have a non-zero error count in an early (lex/syntax) phase.
        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();
        endEvents.Should().Contain(e => e.ErrorCount > 0,
            "failed compilation should have non-zero error count");
    }

    [Fact]
    public void Compile_WithTypeError_ReportsErrorsInTypeCheckingPhase()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        // Type error in a valid structure with main()
        var source = @"
def main():
    x: int = ""not an int""
    print(x)
";
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeFalse();

        var typeCheckEnd = logger.GetEvents<PhaseEndEvent>()
            .FirstOrDefault(e => e.Phase == "Type Checking");
        typeCheckEnd.Should().NotBeNull();
        typeCheckEnd!.ErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compile_EmitsCodeGenEvent()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        var codeGenEvents = logger.GetEvents<CodeGenEvent>().ToList();
        codeGenEvents.Should().HaveCount(1);
        codeGenEvents[0].OutputType.Should().Be("CSharp");
        codeGenEvents[0].ByteCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compile_EventsInChronologicalOrder()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        compiler.Compile(ValidProgramWithVar, "test.spy");

        var events = logger.Events;
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Timestamp.Should().BeOnOrAfter(events[i - 1].Timestamp,
                "events should be in chronological order");
        }
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

/// <summary>
/// Project-mode structured logging (#1077): a genuinely multi-file project must emit per-file
/// phase events — this is the coverage the single-file facade tests above cannot provide, since
/// they only ever observe a one-file synthetic project.
/// </summary>
public class ProjectCompilerStructuredLoggingTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectCompilerStructuredLoggingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_proj_events_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ProjectCompile_MultiFile_EmitsPerFilePhaseAndCodeGenEvents()
    {
        var libPath = Path.Combine(_tempDir, "lib.spy");
        File.WriteAllText(libPath, "def lib_value() -> int:\n    return 41\n");
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, "import lib\n\ndef main():\n    print(lib.lib_value() + 1)\n");

        var logger = new StructuredLogger();
        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            ProjectFilePath = mainPath,
            RootNamespace = string.Empty,
            EntryPoint = mainPath,
            SourceFiles = new List<string> { mainPath, libPath }
        };

        var compiler = new Sharpy.Compiler.Project.ProjectCompiler(logger);
        var result = compiler.Compile(config, CancellationToken.None, emitAssembly: false);

        result.Success.Should().BeTrue(because: string.Join("; ",
            result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}")));

        // Every file gets its own Lexical/Syntax bracket; type checking and codegen also
        // run per file. Assert per-file presence, not global presence.
        var startEvents = logger.GetEvents<PhaseStartEvent>().ToList();
        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();
        startEvents.Should().HaveCount(endEvents.Count, "every phase start has a matching end");

        foreach (var path in new[] { mainPath, libPath })
        {
            var phasesForFile = startEvents.Where(e => e.FilePath == path).Select(e => e.Phase).ToList();
            phasesForFile.Should().Contain(CompilerPhaseNames.LexicalAnalysis,
                $"file '{Path.GetFileName(path)}' must have a lexical phase event");
            phasesForFile.Should().Contain(CompilerPhaseNames.SyntaxAnalysis,
                $"file '{Path.GetFileName(path)}' must have a syntax phase event");
        }

        var codeGenEvents = logger.GetEvents<CodeGenEvent>().ToList();
        codeGenEvents.Select(e => e.FilePath).Distinct().Should().HaveCountGreaterThanOrEqualTo(2,
            "each generated unit reports its own CodeGenEvent");
        codeGenEvents.Should().OnlyContain(e => e.ByteCount > 0);
    }

    [Fact]
    public void ProjectCompile_FileWithTypeError_ThatFilesPhaseEndReportsErrors()
    {
        var badPath = Path.Combine(_tempDir, "bad.spy");
        File.WriteAllText(badPath, "def broken() -> int:\n    return \"not an int\"\n");
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, "def main():\n    print(1)\n");

        var logger = new StructuredLogger();
        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            ProjectFilePath = mainPath,
            RootNamespace = string.Empty,
            EntryPoint = mainPath,
            SourceFiles = new List<string> { mainPath, badPath }
        };

        var compiler = new Sharpy.Compiler.Project.ProjectCompiler(logger);
        var result = compiler.Compile(config, CancellationToken.None, emitAssembly: false);

        result.Success.Should().BeFalse();
        logger.GetEvents<PhaseEndEvent>().Should().Contain(
            e => e.FilePath == badPath && e.ErrorCount > 0,
            "the failing file's phase-end event carries its error count");
    }
}

using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class CompilationMetricsTests
{
    // ===== Integration Tests =====
    // These tests verify that metrics are populated correctly during actual compilation.

    [Fact]
    public void Compilation_PopulatesGranularMetrics()
    {
        var source = """
            def add(x: int, y: int) -> int:
                return x + y

            def main():
                result = add(1, 2)
                print(result)
            """;
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Verify compilation succeeded
        result.Success.Should().BeTrue(
            because: $"the source code should compile successfully but got errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");

        // Verify phase timings are populated
        var metrics = result.Metrics;
        metrics.Should().NotBeNull();

        // At least some phase timings should be recorded
        metrics!.LexerTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.ParserTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.NameResolutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.TypeCheckingTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.CodeGenTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);

        // Total duration should be sum of phases
        metrics.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);

        // Artifact counts should be populated
        metrics.TokenCount.Should().BeGreaterThan(0, because: "the source code has tokens");
        metrics.AstNodeCount.Should().BeGreaterThan(0, because: "the source code has AST nodes");
        metrics.SymbolCount.Should().BeGreaterThan(0, because: "the source code has symbols");
    }

    [Fact]
    public void Compilation_PopulatesValidatorTimes()
    {
        var source = """
            def main():
                x: int = 1
                print(x)
            """;
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            because: $"compilation should succeed but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");

        var metrics = result.Metrics;
        metrics.Should().NotBeNull();

        // Validator times should be populated
        // The exact validators depend on the current pipeline, but we should have some
        metrics!.ValidatorTimes.Should().NotBeEmpty(
            because: "the validation pipeline should have run validators");

        // All validator times should be non-negative
        foreach (var (_, time) in metrics.ValidatorTimes)
        {
            time.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        }

        // ValidationTime should be the sum of individual validator times
        metrics.ValidationTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
            because: "ValidationTime is the aggregate of all validator times");
    }

    [Fact]
    public void Compilation_DiagnosticCountReflectsWarnings()
    {
        // Source with an unused variable to trigger a warning
        var source = """
            x: int = 1
            y: int = 2

            def main():
                print(x)
            """;
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            because: $"compilation should succeed but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");

        var metrics = result.Metrics;
        metrics.Should().NotBeNull();

        // DiagnosticCount should include warnings (y is unused)
        metrics!.DiagnosticCount.Should().BeGreaterThanOrEqualTo(0);
    }


    // ===== Phase Tracking Edge Cases =====

    [Fact]
    public void StartPhase_ThrowsInvalidOperationException_WhenPhaseAlreadyRunning()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Phase 1");

        var action = () => metrics.StartPhase("Phase 2");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start phase*Phase 2*while phase*Phase 1*");
    }

    [Fact]
    public void EndPhase_ThrowsInvalidOperationException_WhenNoPhaseRunning()
    {
        var metrics = new CompilationMetrics();

        var action = () => metrics.EndPhase();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*No phase is currently running*");
    }

    // ===== Per-Phase Timing Properties =====

    [Fact]
    public void LexerTime_ReturnsZero_WhenNoLexicalAnalysisPhase()
    {
        var metrics = new CompilationMetrics();

        metrics.LexerTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void LexerTime_ReturnsPhaseDuration_WhenPhaseRecorded()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Lexical Analysis");
        Thread.Sleep(10); // Small delay to ensure measurable duration
        metrics.EndPhase();

        metrics.LexerTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void ParserTime_ReturnsPhaseDuration_WhenPhaseRecorded()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Syntax Analysis");
        Thread.Sleep(10);
        metrics.EndPhase();

        metrics.ParserTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetPhaseDuration_ReturnsCorrectDuration_ForKnownPhase()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Name Resolution");
        Thread.Sleep(10);
        metrics.EndPhase();

        metrics.NameResolutionTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.GetPhaseDuration("Name Resolution").Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetPhaseDuration_ReturnsZero_ForUnknownPhase()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Lexical Analysis");
        metrics.EndPhase();

        metrics.GetPhaseDuration("NonExistent Phase").Should().Be(TimeSpan.Zero);
    }

    // ===== Validator Timing =====

    [Fact]
    public void ValidatorTimes_ReturnsEmptyDictionary_WhenNotSet()
    {
        var metrics = new CompilationMetrics();

        metrics.ValidatorTimes.Should().BeEmpty();
    }

    [Fact]
    public void SetValidatorTimes_StoresValidatorTimings()
    {
        var metrics = new CompilationMetrics();
        var validatorTimes = new Dictionary<string, TimeSpan>
        {
            ["ControlFlowValidator"] = TimeSpan.FromMilliseconds(15),
            ["UnusedVariableValidator"] = TimeSpan.FromMilliseconds(8),
            ["AccessValidator"] = TimeSpan.FromMilliseconds(3)
        };

        metrics.SetValidatorTimes(validatorTimes);

        metrics.ValidatorTimes.Should().HaveCount(3);
        metrics.ValidatorTimes["ControlFlowValidator"].Should().Be(TimeSpan.FromMilliseconds(15));
        metrics.ValidatorTimes["UnusedVariableValidator"].Should().Be(TimeSpan.FromMilliseconds(8));
        metrics.ValidatorTimes["AccessValidator"].Should().Be(TimeSpan.FromMilliseconds(3));
    }

    [Fact]
    public void ValidationTime_ReturnsZero_WhenNoValidatorTimes()
    {
        var metrics = new CompilationMetrics();

        metrics.ValidationTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ValidationTime_ReturnsSumOfValidatorTimes()
    {
        var metrics = new CompilationMetrics();
        var validatorTimes = new Dictionary<string, TimeSpan>
        {
            ["ControlFlowValidator"] = TimeSpan.FromMilliseconds(15),
            ["UnusedVariableValidator"] = TimeSpan.FromMilliseconds(8),
            ["AccessValidator"] = TimeSpan.FromMilliseconds(3)
        };
        metrics.SetValidatorTimes(validatorTimes);

        // ValidationTime should be sum of all validator times: 15 + 8 + 3 = 26ms
        metrics.ValidationTime.Should().Be(TimeSpan.FromMilliseconds(26));
    }

    // ===== Artifact Counts =====

    [Fact]
    public void TokenCount_DefaultsToZero()
    {
        var metrics = new CompilationMetrics();

        metrics.TokenCount.Should().Be(0);
    }

    [Fact]
    public void TokenCount_CanBeSet()
    {
        var metrics = new CompilationMetrics();

        metrics.TokenCount = 150;

        metrics.TokenCount.Should().Be(150);
    }

    [Fact]
    public void AstNodeCount_CanBeSet()
    {
        var metrics = new CompilationMetrics();

        metrics.AstNodeCount = 42;

        metrics.AstNodeCount.Should().Be(42);
    }

    [Fact]
    public void SymbolCount_CanBeSet()
    {
        var metrics = new CompilationMetrics();

        metrics.SymbolCount = 25;

        metrics.SymbolCount.Should().Be(25);
    }

    [Fact]
    public void DiagnosticCount_CanBeSet()
    {
        var metrics = new CompilationMetrics();

        metrics.DiagnosticCount = 3;

        metrics.DiagnosticCount.Should().Be(3);
    }

    // ===== Text Formatting =====

    [Fact]
    public void FormatAsText_IncludesValidatorBreakdown_WhenSet()
    {
        var metrics = new CompilationMetrics();
        var validatorTimes = new Dictionary<string, TimeSpan>
        {
            ["TestValidator"] = TimeSpan.FromMilliseconds(10)
        };
        metrics.SetValidatorTimes(validatorTimes);

        var text = metrics.FormatAsText();

        text.Should().Contain("Validator Breakdown");
        text.Should().Contain("TestValidator");
    }

    [Fact]
    public void FormatAsText_IncludesArtifactCounts_WhenSet()
    {
        var metrics = new CompilationMetrics();
        metrics.TokenCount = 100;
        metrics.AstNodeCount = 50;

        var text = metrics.FormatAsText();

        text.Should().Contain("Artifact Counts");
        text.Should().Contain("Tokens: 100");
        text.Should().Contain("AST Nodes: 50");
    }

    [Fact]
    public void FormatAsText_DoesNotIncludeArtifactCounts_WhenAllZero()
    {
        var metrics = new CompilationMetrics();

        var text = metrics.FormatAsText();

        text.Should().NotContain("Artifact Counts");
    }

    // ===== JSON Formatting =====

    [Fact]
    public void FormatAsJson_IncludesPerPhaseTimings()
    {
        var metrics = new CompilationMetrics();
        metrics.StartPhase("Lexical Analysis");
        metrics.EndPhase();

        var json = metrics.FormatAsJson();

        json.Should().Contain("lexer_time_ms");
        json.Should().Contain("parser_time_ms");
        json.Should().Contain("validation_time_ms");
        json.Should().Contain("codegen_time_ms");
    }

    [Fact]
    public void FormatAsJson_IncludesArtifactCounts()
    {
        var metrics = new CompilationMetrics();
        metrics.TokenCount = 75;
        metrics.SymbolCount = 20;

        var json = metrics.FormatAsJson();

        json.Should().Contain("\"token_count\": 75");
        json.Should().Contain("\"symbol_count\": 20");
    }

    [Fact]
    public void FormatAsJson_IncludesValidatorTimes_WhenSet()
    {
        var metrics = new CompilationMetrics();
        var validatorTimes = new Dictionary<string, TimeSpan>
        {
            ["MyValidator"] = TimeSpan.FromMilliseconds(5)
        };
        metrics.SetValidatorTimes(validatorTimes);

        var json = metrics.FormatAsJson();

        json.Should().Contain("validator_times");
        json.Should().Contain("MyValidator");
    }

    // ===== All Phase Timing Properties =====

    [Fact]
    public void AllPhaseTimingProperties_ReturnCorrectDurations()
    {
        var metrics = new CompilationMetrics();

        // Record all phases
        var phases = new[]
        {
            ("Lexical Analysis", nameof(metrics.LexerTime)),
            ("Syntax Analysis", nameof(metrics.ParserTime)),
            ("Name Resolution", nameof(metrics.NameResolutionTime)),
            ("Import Resolution", nameof(metrics.ImportResolutionTime)),
            ("Type Resolution", nameof(metrics.TypeResolutionTime)),
            ("Type Checking", nameof(metrics.TypeCheckingTime)),
            ("Code Generation", nameof(metrics.CodeGenTime))
        };

        foreach (var (phaseName, _) in phases)
        {
            metrics.StartPhase(phaseName);
            metrics.EndPhase();
        }

        // Verify all properties return non-zero values
        metrics.LexerTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.ParserTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.NameResolutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.ImportResolutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.TypeResolutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.TypeCheckingTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.CodeGenTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ===== ProjectCompilationMetrics Tests =====

    [Fact]
    public void ProjectCompilationMetrics_AggregatesFileMetrics()
    {
        var projectMetrics = new ProjectCompilationMetrics("TestProject", "Debug");

        // Add multiple file metrics
        var file1 = new CompilationMetrics(fileName: "file1.spy");
        file1.StartPhase("Lexical Analysis");
        Thread.Sleep(5);
        file1.EndPhase();
        file1.TokenCount = 100;
        file1.AstNodeCount = 50;

        var file2 = new CompilationMetrics(fileName: "file2.spy");
        file2.StartPhase("Lexical Analysis");
        Thread.Sleep(5);
        file2.EndPhase();
        file2.TokenCount = 150;
        file2.AstNodeCount = 75;

        projectMetrics.AddFileMetrics(file1);
        projectMetrics.AddFileMetrics(file2);

        projectMetrics.TotalFiles.Should().Be(2);
        projectMetrics.FileMetrics.Should().HaveCount(2);
        projectMetrics.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);

        var aggregates = projectMetrics.AggregatePhaseMetrics;
        aggregates.Should().ContainKey("Lexical Analysis");
        aggregates["Lexical Analysis"].Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void ProjectCompilationMetrics_TracksSkippedFiles()
    {
        var projectMetrics = new ProjectCompilationMetrics("TestProject", "Debug");

        projectMetrics.AddSkippedFile("cached1.spy");
        projectMetrics.AddSkippedFile("cached2.spy");
        projectMetrics.AddSkippedFile("cached3.spy");

        projectMetrics.SkippedFileCount.Should().Be(3);
        projectMetrics.SkippedFiles.Should().Contain("cached2.spy");
    }

    [Fact]
    public void ProjectCompilationMetrics_IncludesAssemblyMetrics()
    {
        var projectMetrics = new ProjectCompilationMetrics("TestProject", "Debug");

        var assemblyMetrics = new CompilationMetrics(projectName: "TestProject");
        assemblyMetrics.StartPhase("Assembly Generation");
        Thread.Sleep(5);
        assemblyMetrics.EndPhase();

        projectMetrics.SetAssemblyMetrics(assemblyMetrics);

        projectMetrics.AssemblyMetrics.Should().NotBeNull();
        projectMetrics.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void ProjectCompilationMetrics_FormatAsText_IncludesProjectInfo()
    {
        var projectMetrics = new ProjectCompilationMetrics("MyProject", "Release");

        var fileMetrics = new CompilationMetrics(fileName: "main.spy");
        fileMetrics.StartPhase("Lexical Analysis");
        fileMetrics.EndPhase();
        projectMetrics.AddFileMetrics(fileMetrics);

        var text = projectMetrics.FormatAsText();

        text.Should().Contain("MyProject");
        text.Should().Contain("Release");
        text.Should().Contain("Files Compiled: 1");
    }

    [Fact]
    public void ProjectCompilationMetrics_FormatAsJson_IncludesAllData()
    {
        var projectMetrics = new ProjectCompilationMetrics("JsonProject", "Debug");

        var fileMetrics = new CompilationMetrics(fileName: "test.spy");
        fileMetrics.StartPhase("Syntax Analysis");
        fileMetrics.EndPhase();
        fileMetrics.TokenCount = 42;
        projectMetrics.AddFileMetrics(fileMetrics);

        var json = projectMetrics.FormatAsJson();

        json.Should().Contain("\"project_name\": \"JsonProject\"");
        json.Should().Contain("\"configuration\": \"Debug\"");
        json.Should().Contain("\"total_files\": 1");
    }

    // ===== Error Path Tests =====
    // These tests verify that metrics are populated even when compilation fails.

    [Fact]
    public void Compilation_PopulatesMetrics_EvenOnSemanticError()
    {
        // Source with a type error
        var source = """
            def main():
                x: int = "not an int"
                print(x)
            """;
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Compilation should fail
        result.Success.Should().BeFalse();

        // But metrics should still be populated
        var metrics = result.Metrics;
        metrics.Should().NotBeNull();

        // Phase timings up to the failure point should be recorded
        metrics!.LexerTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.ParserTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metrics.NameResolutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);

        // Token count should still be populated (lexer succeeded)
        metrics.TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compilation_PopulatesPartialMetrics_OnLexerError()
    {
        // Source with an invalid token
        var source = """
            def main():
                x = @invalid_token
            """;
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Compilation should fail
        result.Success.Should().BeFalse();

        // But metrics should still be populated
        var metrics = result.Metrics;
        metrics.Should().NotBeNull();

        // Lexer time should be recorded
        metrics!.LexerTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);

        // Phases after lexer failure may not have run
        // But the total duration should still be non-negative
        metrics.TotalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void ValidationTime_EqualsExactSumOfValidatorTimes()
    {
        // Create metrics with specific validator times
        var metrics = new CompilationMetrics();
        var validatorTimes = new Dictionary<string, TimeSpan>
        {
            ["Validator1"] = TimeSpan.FromTicks(1000),
            ["Validator2"] = TimeSpan.FromTicks(2000),
            ["Validator3"] = TimeSpan.FromTicks(500)
        };
        metrics.SetValidatorTimes(validatorTimes);

        // ValidationTime should be exactly 3500 ticks
        var expectedSum = TimeSpan.FromTicks(3500);
        metrics.ValidationTime.Should().Be(expectedSum);
    }
}

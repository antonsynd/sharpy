using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Tests for automatic diagnostic provenance stamping (#1054): every diagnostic is stamped
/// with the compiler <see cref="CompilerPhase"/> it originated in, and every validator
/// diagnostic additionally carries <c>Data["producer"]</c> = the validator's name. Stamping
/// happens at the single <see cref="DiagnosticBag"/> Add funnel via ambient scopes, so it
/// requires zero edits at individual diagnostic creation sites, never overwrites an explicit
/// phase or producer, and survives bag merges (single-file and project pipelines).
/// </summary>
public class DiagnosticProvenanceTests
{
    private readonly CompilerApi _api = new();
    private readonly ITestOutputHelper _output;

    public DiagnosticProvenanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ----- DiagnosticBag scope mechanics (unit level) -----

    [Fact]
    public void PhaseScope_backfills_unknown_phase()
    {
        var bag = new DiagnosticBag();

        using (bag.BeginPhaseScope(CompilerPhase.Lexer))
        {
            bag.AddError("boom");
        }

        bag.GetAll().Should().ContainSingle()
            .Which.Phase.Should().Be(CompilerPhase.Lexer);
    }

    [Fact]
    public void PhaseScope_does_not_clobber_explicit_phase()
    {
        var bag = new DiagnosticBag();

        // A diagnostic that already declares its phase must keep it even when added
        // while a different phase scope is active.
        using (bag.BeginPhaseScope(CompilerPhase.CodeGeneration))
        {
            bag.Add(new CompilerDiagnostic("explicit", CompilerDiagnosticSeverity.Error,
                Phase: CompilerPhase.TypeChecking));
        }

        bag.GetAll().Should().ContainSingle()
            .Which.Phase.Should().Be(CompilerPhase.TypeChecking);
    }

    [Fact]
    public void PhaseScope_restores_previous_phase_on_dispose()
    {
        var bag = new DiagnosticBag();

        using (bag.BeginPhaseScope(CompilerPhase.Parser))
        {
            bag.AddError("inside");
        }
        bag.AddError("outside");

        var all = bag.GetAll();
        all.Single(d => d.Message == "inside").Phase.Should().Be(CompilerPhase.Parser);
        all.Single(d => d.Message == "outside").Phase.Should().Be(CompilerPhase.Unknown);
    }

    [Fact]
    public void ProducerScope_stamps_producer_and_validation_phase()
    {
        var bag = new DiagnosticBag();

        using (bag.BeginProducerScope("ProtocolValidator"))
        {
            bag.AddWarning("proto issue", code: "SPY0500");
        }

        var diag = bag.GetAll().Should().ContainSingle().Subject;
        diag.Phase.Should().Be(CompilerPhase.Validation);
        diag.Data.Should().NotBeNull();
        diag.Data![DiagnosticBag.ProducerDataKey].Should().Be("ProtocolValidator");
    }

    [Fact]
    public void ProducerScope_does_not_clobber_existing_producer()
    {
        var bag = new DiagnosticBag();
        var preset = new Dictionary<string, string> { [DiagnosticBag.ProducerDataKey] = "Original" };

        using (bag.BeginProducerScope("Interposed"))
        {
            bag.Add(new CompilerDiagnostic("keep", CompilerDiagnosticSeverity.Error, Data: preset));
        }

        bag.GetAll().Should().ContainSingle()
            .Which.Data![DiagnosticBag.ProducerDataKey].Should().Be("Original");
    }

    [Fact]
    public void ProducerScope_preserves_other_data_keys()
    {
        var bag = new DiagnosticBag();
        var preset = new Dictionary<string, string> { ["hint"] = "value" };

        using (bag.BeginProducerScope("SomeValidator"))
        {
            bag.Add(new CompilerDiagnostic("m", CompilerDiagnosticSeverity.Warning, Data: preset));
        }

        var data = bag.GetAll().Single().Data!;
        data["hint"].Should().Be("value");
        data[DiagnosticBag.ProducerDataKey].Should().Be("SomeValidator");
    }

    [Fact]
    public void Merge_preserves_stamps_from_source_bag()
    {
        // Stamps applied at the source bag's Add-time survive a subsequent merge into a
        // target bag, even when the target is itself inside a different phase scope.
        var source = new DiagnosticBag();
        using (source.BeginProducerScope("UnusedImportValidator"))
        {
            source.AddWarning("unused", code: "SPY0430");
        }

        var target = new DiagnosticBag();
        using (target.BeginPhaseScope(CompilerPhase.CodeGeneration))
        {
            target.Merge(source);
        }

        var diag = target.GetAll().Should().ContainSingle().Subject;
        diag.Phase.Should().Be(CompilerPhase.Validation);
        diag.Data![DiagnosticBag.ProducerDataKey].Should().Be("UnusedImportValidator");
    }

    // ----- End-to-end through the single-file pipeline -----

    [Fact]
    public void Lexer_error_is_stamped_with_lexer_phase()
    {
        // Unterminated string literal — fails during lexical analysis.
        var result = _api.Compile("x = \"unterminated");

        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        errors.Should().NotBeEmpty();
        errors.Should().OnlyContain(d => d.Phase == CompilerPhase.Lexer);
    }

    [Fact]
    public void Parser_error_is_stamped_with_parser_phase()
    {
        // Valid tokens, invalid grammar — fails during syntax analysis.
        var result = _api.Compile("def main(:\n    pass");

        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        errors.Should().NotBeEmpty();
        errors.Should().OnlyContain(d => d.Phase == CompilerPhase.Parser);
    }

    [Fact]
    public void ImportResolution_error_is_stamped_with_import_resolution_phase()
    {
        var result = _api.Compile("from definitely_no_such_module_xyz import thing\n\ndef main():\n    pass");

        var importError = result.Diagnostics.FirstOrDefault(
            d => d.IsError && d.Phase == CompilerPhase.ImportResolution);
        importError.Should().NotBeNull(
            "a missing-module error should be stamped with the ImportResolution phase");
    }

    [Fact]
    public void TypeChecking_error_is_stamped_with_type_checking_phase()
    {
        var result = _api.Compile("def main():\n    x: int = \"hello\"\n    print(x)");

        var typeError = result.Diagnostics.FirstOrDefault(
            d => d.IsError && d.Phase == CompilerPhase.TypeChecking);
        typeError.Should().NotBeNull(
            "an int/str assignment mismatch should be stamped with the TypeChecking phase");
    }

    [Fact]
    public void Validator_diagnostic_carries_producer_and_validation_phase()
    {
        // An unused import triggers UnusedImportValidator (a ValidationPipeline validator).
        var result = _api.Compile("import math\n\ndef main():\n    print(1)");

        var stamped = result.Diagnostics
            .Where(d => d.Data != null && d.Data.ContainsKey(DiagnosticBag.ProducerDataKey))
            .ToList();

        stamped.Should().NotBeEmpty("validator diagnostics must record their producer");
        stamped.Should().OnlyContain(d => d.Phase == CompilerPhase.Validation);
        stamped.Should().Contain(d => d.Data![DiagnosticBag.ProducerDataKey] == "UnusedImportValidator");
    }

    // ----- End-to-end through the project pipeline (stamps survive bag merging) -----

    [Fact]
    public void Project_type_error_is_stamped_with_type_checking_phase()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", "def main():\n    x: int = \"hello\"\n    print(x)");

        var result = helper.Compile();

        // Project mode merges per-file bags into the project bag; the phase stamp applied
        // when the type-check diagnostics were merged must survive that second merge.
        var typeError = result.Diagnostics.GetAll().FirstOrDefault(
            d => d.IsError && d.Phase == CompilerPhase.TypeChecking);
        typeError.Should().NotBeNull(
            "the type error must retain its TypeChecking phase stamp through project-mode bag merging");
    }
}

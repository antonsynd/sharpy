using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class DiagnosticBagTests
{
    [Fact]
    public void AddError_AddsErrorDiagnostic()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Test error", 10, 5);

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Equal("Test error", errors[0].Message);
        Assert.Equal(10, errors[0].Line);
        Assert.Equal(5, errors[0].Column);
    }

    [Fact]
    public void AddWarning_AddsWarningDiagnostic()
    {
        var bag = new DiagnosticBag();

        bag.AddWarning("Test warning", 10, 5);

        Assert.False(bag.HasErrors);
        Assert.Equal(1, bag.WarningCount);
        var warnings = bag.GetWarnings();
        Assert.Single(warnings);
        Assert.Equal("Test warning", warnings[0].Message);
    }

    [Fact]
    public void Merge_CombinesDiagnostics()
    {
        var bag1 = new DiagnosticBag();
        bag1.AddError("Error 1");

        var bag2 = new DiagnosticBag();
        bag2.AddError("Error 2");
        bag2.AddWarning("Warning 1");

        bag1.Merge(bag2);

        Assert.Equal(2, bag1.ErrorCount);
        Assert.Equal(1, bag1.WarningCount);
    }

    [Fact]
    public void Clear_RemovesAllDiagnostics()
    {
        var bag = new DiagnosticBag();
        bag.AddError("Error 1");
        bag.AddWarning("Warning 1");

        bag.Clear();

        Assert.False(bag.HasErrors);
        Assert.Equal(0, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void DiagnosticToString_FormatsCorrectly()
    {
        var diagnostic = new CompilerDiagnostic(
            "Test message",
            CompilerDiagnosticSeverity.Error,
            Line: 10,
            Column: 5,
            FilePath: "test.spy"
        );

        var result = diagnostic.ToString();

        Assert.Contains("test.spy", result);
        Assert.Contains("10", result);
        Assert.Contains("5", result);
        Assert.Contains("error", result);
        Assert.Contains("Test message", result);
    }

    [Fact]
    public void GetAll_ReturnsAllDiagnostics()
    {
        var bag = new DiagnosticBag();
        bag.AddError("Error 1");
        bag.AddWarning("Warning 1");
        bag.Add(new CompilerDiagnostic("Info 1", CompilerDiagnosticSeverity.Info));

        var all = bag.GetAll();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void AddRange_AddsMultipleDiagnostics()
    {
        var bag = new DiagnosticBag();
        var diagnostics = new List<CompilerDiagnostic>
        {
            new CompilerDiagnostic("Error 1", CompilerDiagnosticSeverity.Error, 1, 1),
            new CompilerDiagnostic("Error 2", CompilerDiagnosticSeverity.Error, 2, 1),
            new CompilerDiagnostic("Warning 1", CompilerDiagnosticSeverity.Warning, 3, 1)
        };

        bag.AddRange(diagnostics);

        Assert.Equal(2, bag.ErrorCount);
        Assert.Equal(1, bag.WarningCount);
    }

    [Fact]
    public void AddError_WithFilePath_SetsFilePath()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Test error", 10, 5, "myfile.spy");

        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Equal("myfile.spy", errors[0].FilePath);
    }

    [Fact]
    public void DiagnosticIsError_ReturnsCorrectValue()
    {
        var error = new CompilerDiagnostic("Error", CompilerDiagnosticSeverity.Error);
        var warning = new CompilerDiagnostic("Warning", CompilerDiagnosticSeverity.Warning);
        var info = new CompilerDiagnostic("Info", CompilerDiagnosticSeverity.Info);
        var hint = new CompilerDiagnostic("Hint", CompilerDiagnosticSeverity.Hint);

        Assert.True(error.IsError);
        Assert.False(warning.IsError);
        Assert.False(info.IsError);
        Assert.False(hint.IsError);
    }

    [Fact]
    public void CompilerDiagnostic_SpanProperty_DefaultsToNull()
    {
        var diagnostic = new CompilerDiagnostic("Test", CompilerDiagnosticSeverity.Error);

        Assert.Null(diagnostic.Span);
    }

    [Fact]
    public void CompilerDiagnostic_SpanProperty_CanBeSet()
    {
        var span = new TextSpan(10, 5);
        var diagnostic = new CompilerDiagnostic(
            "Test", CompilerDiagnosticSeverity.Error, Span: span);

        Assert.NotNull(diagnostic.Span);
        Assert.Equal(10, diagnostic.Span.Value.Start);
        Assert.Equal(5, diagnostic.Span.Value.Length);
    }

    [Fact]
    public void DiagnosticToString_IncludesSpanWhenPresent()
    {
        var span = new TextSpan(10, 5);
        var diagnostic = new CompilerDiagnostic(
            "Test message",
            CompilerDiagnosticSeverity.Error,
            Line: 3,
            Column: 5,
            FilePath: "test.spy",
            Span: span
        );

        var result = diagnostic.ToString();

        Assert.Contains("[10..15)", result);
        Assert.Contains("test.spy", result);
        Assert.Contains("Test message", result);
    }

    [Fact]
    public void DiagnosticToString_OmitsSpanWhenNull()
    {
        var diagnostic = new CompilerDiagnostic(
            "Test message",
            CompilerDiagnosticSeverity.Error,
            Line: 3,
            Column: 5,
            FilePath: "test.spy"
        );

        var result = diagnostic.ToString();

        Assert.DoesNotContain("[", result);
    }

    [Fact]
    public void AddError_WithTextSpan_SetsSpanOnDiagnostic()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(20, 10);

        bag.AddError("Test error", span, 5, 3, "test.spy", "SHP0001");

        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Equal(span, errors[0].Span);
        Assert.Equal(5, errors[0].Line);
        Assert.Equal(3, errors[0].Column);
    }

    [Fact]
    public void AddWarning_WithTextSpan_SetsSpanOnDiagnostic()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(30, 15);

        bag.AddWarning("Test warning", span, 8, 2, "test.spy", "SHP0451");

        var warnings = bag.GetWarnings();
        Assert.Single(warnings);
        Assert.Equal(span, warnings[0].Span);
    }

    [Fact]
    public void AddError_WithNullSpan_LeavesSpanNull()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Test error", (TextSpan?)null, 5, 3);

        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Null(errors[0].Span);
    }

    [Fact]
    public void AddError_WithILocatable_ExtractsSpanCorrectly()
    {
        var bag = new DiagnosticBag();
        var locatable = new TestLocatable(new TextSpan(42, 7));

        bag.AddError("Locatable error", locatable, "test.spy", "SHP0001");

        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.NotNull(errors[0].Span);
        Assert.Equal(42, errors[0].Span!.Value.Start);
        Assert.Equal(7, errors[0].Span!.Value.Length);
    }

    [Fact]
    public void AddWarning_WithILocatable_ExtractsSpanCorrectly()
    {
        var bag = new DiagnosticBag();
        var locatable = new TestLocatable(new TextSpan(100, 12));

        bag.AddWarning("Locatable warning", locatable, "test.spy", "SHP0451");

        var warnings = bag.GetWarnings();
        Assert.Single(warnings);
        Assert.NotNull(warnings[0].Span);
        Assert.Equal(100, warnings[0].Span!.Value.Start);
        Assert.Equal(12, warnings[0].Span!.Value.Length);
    }

    [Fact]
    public void AddError_WithILocatable_NullSpan_LeavesSpanNull()
    {
        var bag = new DiagnosticBag();
        var locatable = new TestLocatable(null);

        bag.AddError("Locatable error no span", locatable, "test.spy");

        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Null(errors[0].Span);
    }

    // --- Warning Suppression Tests ---

    [Fact]
    public void SuppressedWarning_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Unused variable", code: "SHP0451");

        Assert.Equal(0, bag.WarningCount);
        Assert.Empty(bag.GetAll());
    }

    [Fact]
    public void SuppressedWarning_OtherWarningsStillAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Unused variable", code: "SHP0451");
        bag.AddWarning("Unused import", code: "SHP0452");

        Assert.Equal(1, bag.WarningCount);
        Assert.Equal("SHP0452", bag.GetWarnings()[0].Code);
    }

    [Fact]
    public void SuppressedWarning_WithSpan_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Unused variable", new TextSpan(10, 5), code: "SHP0451");

        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_WithLocatable_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Unused variable", new TestLocatable(new TextSpan(10, 5)), code: "SHP0451");

        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_ErrorsNotAffected()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddError("Some error", code: "SHP0200");

        Assert.Equal(1, bag.ErrorCount);
    }

    // --- Warnings-as-Errors Tests ---

    [Fact]
    public void WarningsAsErrors_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", code: "SHP0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void WarningsAsErrors_WithSpan_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", new TextSpan(10, 5), code: "SHP0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_WithLocatable_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", new TestLocatable(new TextSpan(10, 5)), code: "SHP0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_ErrorsNotDoubled()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddError("Some error", code: "SHP0200");

        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_CombinedWithSuppression()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true, suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Unused variable", code: "SHP0451");
        bag.AddWarning("Unused import", code: "SHP0452");

        // SHP0451 suppressed, SHP0452 promoted to error
        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
        Assert.Equal("SHP0452", bag.GetErrors()[0].Code);
    }

    [Fact]
    public void WarningWithNoCode_NotSuppressed()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SHP0451" });

        bag.AddWarning("Some warning without code");

        Assert.Equal(1, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_CaseInsensitive()
    {
        // When suppressed codes are in a case-insensitive set, lowercase codes should match
        var suppressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "shp0451" };
        var bag = new DiagnosticBag(suppressedWarnings: suppressed);

        bag.AddWarning("Unused variable", code: "SHP0451");

        Assert.Equal(0, bag.WarningCount);
        Assert.Empty(bag.GetAll());
    }

    private class TestLocatable : ILocatable
    {
        public TestLocatable(TextSpan? span) => Span = span;
        public TextSpan? Span { get; }
    }

    // --- Deduplication Tests ---

    [Fact]
    public void Deduplication_SameCodeAndLocation_OnlyAddsOnce()
    {
        var bag = new DiagnosticBag();

        // Add same error twice with slightly different messages but same code and location
        bag.AddError("Cannot assign int to str", 10, 5, code: "SHP0001");
        bag.AddError("Cannot assign 'int' to 'str'", 10, 5, code: "SHP0001");  // Variant message

        var errors = bag.GetAll().ToList();
        Assert.Single(errors);  // Should deduplicate
        Assert.Equal("Cannot assign int to str", errors[0].Message);  // First one wins
    }

    [Fact]
    public void Deduplication_SameCodeDifferentLocations_AddsAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SHP0001");
        bag.AddError("Type mismatch", 15, 5, code: "SHP0001");  // Same code, different line

        var errors = bag.GetAll().ToList();
        Assert.Equal(2, errors.Count);  // Should NOT deduplicate
    }

    [Fact]
    public void Deduplication_DifferentCodesSameLocation_AddsAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SHP0001");
        bag.AddError("Missing return", 10, 5, code: "SHP0002");  // Different code, same location

        var errors = bag.GetAll().ToList();
        Assert.Equal(2, errors.Count);  // Should NOT deduplicate
    }

    [Fact]
    public void Deduplication_NoCode_UsesMessageForUniqueness()
    {
        var bag = new DiagnosticBag();

        // Diagnostics without codes should use message for uniqueness
        bag.AddError("Error message 1", 10, 5);
        bag.AddError("Error message 1", 10, 5);  // Exact duplicate
        bag.AddError("Error message 2", 10, 5);  // Same location, different message

        var errors = bag.GetAll().ToList();
        Assert.Equal(2, errors.Count);  // One duplicate removed
    }

    [Fact]
    public void Deduplication_MergedBags_DuplicatesRemoved()
    {
        var bag1 = new DiagnosticBag();
        bag1.AddError("Type mismatch", 10, 5, code: "SHP0001");

        var bag2 = new DiagnosticBag();
        bag2.AddError("Type mismatch", 10, 5, code: "SHP0001");  // Same diagnostic

        bag1.Merge(bag2);

        var errors = bag1.GetAll().ToList();
        Assert.Single(errors);  // Duplicate removed during merge
    }

    [Fact]
    public void Deduplication_ClearResetsDuplicateTracking()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SHP0001");
        bag.Clear();
        bag.AddError("Type mismatch", 10, 5, code: "SHP0001");  // Same as before clear

        var errors = bag.GetAll().ToList();
        Assert.Single(errors);  // Should be added after clear
    }

    [Fact]
    public void Deduplication_WarningsAlsoDeduped()
    {
        var bag = new DiagnosticBag();

        bag.AddWarning("Unused variable", 10, 5, code: "SHP0451");
        bag.AddWarning("Unused variable", 10, 5, code: "SHP0451");

        var warnings = bag.GetWarnings().ToList();
        Assert.Single(warnings);
    }

    [Fact]
    public void Deduplication_MixedSeverities_StillDeduplicatesWhenPromoted()
    {
        // When warnings-as-errors is enabled, the duplicate check happens after promotion
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", 10, 5, code: "SHP0451");
        bag.AddWarning("Unused variable", 10, 5, code: "SHP0451");

        var errors = bag.GetErrors().ToList();
        Assert.Single(errors);  // Deduplicated after promotion
    }
}

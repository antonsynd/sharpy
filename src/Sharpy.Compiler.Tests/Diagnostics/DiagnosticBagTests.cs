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

        bag.AddError("Test error", span, 5, 3, "test.spy", "SPY0001");

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

        bag.AddWarning("Test warning", span, 8, 2, "test.spy", "SPY0451");

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

        bag.AddError("Locatable error", locatable, "test.spy", "SPY0001");

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

        bag.AddWarning("Locatable warning", locatable, "test.spy", "SPY0451");

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
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Unused variable", code: "SPY0451");

        Assert.Equal(0, bag.WarningCount);
        Assert.Empty(bag.GetAll());
    }

    [Fact]
    public void SuppressedWarning_OtherWarningsStillAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Unused variable", code: "SPY0451");
        bag.AddWarning("Unused import", code: "SPY0452");

        Assert.Equal(1, bag.WarningCount);
        Assert.Equal("SPY0452", bag.GetWarnings()[0].Code);
    }

    [Fact]
    public void SuppressedWarning_WithSpan_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Unused variable", new TextSpan(10, 5), code: "SPY0451");

        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_WithLocatable_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Unused variable", new TestLocatable(new TextSpan(10, 5)), code: "SPY0451");

        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_ErrorsNotAffected()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddError("Some error", code: "SPY0200");

        Assert.Equal(1, bag.ErrorCount);
    }

    // --- Warnings-as-Errors Tests ---

    [Fact]
    public void WarningsAsErrors_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", code: "SPY0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void WarningsAsErrors_WithSpan_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", new TextSpan(10, 5), code: "SPY0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_WithLocatable_PromotesWarningToError()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", new TestLocatable(new TextSpan(10, 5)), code: "SPY0451");

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_ErrorsNotDoubled()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddError("Some error", code: "SPY0200");

        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void WarningsAsErrors_CombinedWithSuppression()
    {
        var bag = new DiagnosticBag(warningsAsErrors: true, suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Unused variable", code: "SPY0451");
        bag.AddWarning("Unused import", code: "SPY0452");

        // SPY0451 suppressed, SPY0452 promoted to error
        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
        Assert.Equal("SPY0452", bag.GetErrors()[0].Code);
    }

    [Fact]
    public void WarningWithNoCode_NotSuppressed()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0451" });

        bag.AddWarning("Some warning without code");

        Assert.Equal(1, bag.WarningCount);
    }

    [Fact]
    public void SuppressedWarning_CaseInsensitive()
    {
        // When suppressed codes are in a case-insensitive set, lowercase codes should match
        var suppressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "spy0451" };
        var bag = new DiagnosticBag(suppressedWarnings: suppressed);

        bag.AddWarning("Unused variable", code: "SPY0451");

        Assert.Equal(0, bag.WarningCount);
        Assert.Empty(bag.GetAll());
    }

    private class TestLocatable : ILocatable
    {
        public TestLocatable(TextSpan? span) => Span = span;
        public TextSpan? Span { get; }
    }

    // --- Root Cause Tracking Tests ---

    [Fact]
    public void AddRootCauseError_MarksIdentifierAsRootCause()
    {
        var bag = new DiagnosticBag();

        bag.AddRootCauseError("foo", "Cannot find module 'foo'", 1, 1, code: "SPY0301");

        Assert.True(bag.IsRootCause("foo"));
        Assert.Equal(1, bag.ErrorCount);
    }

    [Fact]
    public void IsRootCause_ReturnsFalseForUnknownIdentifier()
    {
        var bag = new DiagnosticBag();

        Assert.False(bag.IsRootCause("unknown"));
    }

    [Fact]
    public void IsRootCause_IsCaseInsensitive()
    {
        var bag = new DiagnosticBag();

        bag.AddRootCauseError("Foo", "Module not found", 1, 1);

        Assert.True(bag.IsRootCause("foo"));
        Assert.True(bag.IsRootCause("FOO"));
        Assert.True(bag.IsRootCause("Foo"));
    }

    [Fact]
    public void AddRootCauseError_WithTextSpan_MarksIdentifierAsRootCause()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(10, 5);

        bag.AddRootCauseError("bar", "Cannot find module 'bar'", span, 1, 1, code: "SPY0301");

        Assert.True(bag.IsRootCause("bar"));
        Assert.Equal(1, bag.ErrorCount);
        Assert.NotNull(bag.GetErrors()[0].Span);
    }

    [Fact]
    public void MarkAsRootCause_DoesNotAddError()
    {
        var bag = new DiagnosticBag();

        bag.MarkAsRootCause("foo");

        Assert.True(bag.IsRootCause("foo"));
        Assert.Equal(0, bag.ErrorCount);
    }

    [Fact]
    public void MarkAsRootCauses_MarksMultipleIdentifiers()
    {
        var bag = new DiagnosticBag();

        bag.MarkAsRootCauses(new[] { "foo", "bar", "baz" });

        Assert.True(bag.IsRootCause("foo"));
        Assert.True(bag.IsRootCause("bar"));
        Assert.True(bag.IsRootCause("baz"));
        Assert.False(bag.IsRootCause("qux"));
    }

    [Fact]
    public void Clear_ClearsRootCauses()
    {
        var bag = new DiagnosticBag();
        bag.AddRootCauseError("foo", "Module not found", 1, 1);

        Assert.True(bag.IsRootCause("foo"));

        bag.Clear();

        Assert.False(bag.IsRootCause("foo"));
    }

    [Fact]
    public void RootCauseTracking_WorksWithMultipleRootCauses()
    {
        var bag = new DiagnosticBag();

        bag.AddRootCauseError("math", "Cannot find module 'math'", 1, 1);
        bag.AddRootCauseError("os", "Cannot find module 'os'", 2, 1);

        Assert.True(bag.IsRootCause("math"));
        Assert.True(bag.IsRootCause("os"));
        Assert.False(bag.IsRootCause("sys"));
        Assert.Equal(2, bag.ErrorCount);
    }

    [Fact]
    public void RootCauseTracking_IndependentOfRegularErrors()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Some other error", 1, 1);
        bag.AddRootCauseError("foo", "Module not found", 2, 1);
        bag.AddError("Yet another error", 3, 1);

        Assert.True(bag.IsRootCause("foo"));
        Assert.False(bag.IsRootCause("other"));
        Assert.Equal(3, bag.ErrorCount);
    }

    [Fact]
    public void Merge_TransfersRootCauses()
    {
        var bag1 = new DiagnosticBag();
        bag1.AddRootCauseError("moduleA", "Cannot find module 'moduleA'", 1, 1);

        var bag2 = new DiagnosticBag();
        bag2.Merge(bag1);

        Assert.True(bag2.IsRootCause("moduleA"));
        Assert.Equal(1, bag2.ErrorCount);
    }

    [Fact]
    public void Merge_TransfersMultipleRootCauses()
    {
        var bag1 = new DiagnosticBag();
        bag1.MarkAsRootCauses(new[] { "foo", "bar", "baz" });

        var bag2 = new DiagnosticBag();
        bag2.Merge(bag1);

        Assert.True(bag2.IsRootCause("foo"));
        Assert.True(bag2.IsRootCause("bar"));
        Assert.True(bag2.IsRootCause("baz"));
    }

    [Fact]
    public void Merge_CombinesRootCausesFromBothBags()
    {
        var bag1 = new DiagnosticBag();
        bag1.MarkAsRootCause("a");

        var bag2 = new DiagnosticBag();
        bag2.MarkAsRootCause("b");

        bag1.Merge(bag2);

        Assert.True(bag1.IsRootCause("a"));
        Assert.True(bag1.IsRootCause("b"));
    }

    [Fact]
    public void GetRootCauses_ReturnsAllRootCauses()
    {
        var bag = new DiagnosticBag();
        bag.AddRootCauseError("alpha", "Error for alpha", 1, 1);
        bag.MarkAsRootCause("beta");
        bag.MarkAsRootCauses(new[] { "gamma", "delta" });

        var rootCauses = bag.GetRootCauses().ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(4, rootCauses.Count);
        Assert.Contains("alpha", rootCauses);
        Assert.Contains("beta", rootCauses);
        Assert.Contains("gamma", rootCauses);
        Assert.Contains("delta", rootCauses);
    }

    // --- Deduplication Tests ---

    [Fact]
    public void Deduplication_SameCodeAndLocation_OnlyAddsOnce()
    {
        var bag = new DiagnosticBag();

        // Add same error twice with slightly different messages but same code and location
        bag.AddError("Cannot assign int to str", 10, 5, code: "SPY0001");
        bag.AddError("Cannot assign 'int' to 'str'", 10, 5, code: "SPY0001");  // Variant message

        var errors = bag.GetAll().ToList();
        Assert.Single(errors);  // Should deduplicate
        Assert.Equal("Cannot assign int to str", errors[0].Message);  // First one wins
    }

    [Fact]
    public void Deduplication_SameCodeDifferentLocations_AddsAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SPY0001");
        bag.AddError("Type mismatch", 15, 5, code: "SPY0001");  // Same code, different line

        var errors = bag.GetAll().ToList();
        Assert.Equal(2, errors.Count);  // Should NOT deduplicate
    }

    [Fact]
    public void Deduplication_DifferentCodesSameLocation_AddsAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SPY0001");
        bag.AddError("Missing return", 10, 5, code: "SPY0002");  // Different code, same location

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
        bag1.AddError("Type mismatch", 10, 5, code: "SPY0001");

        var bag2 = new DiagnosticBag();
        bag2.AddError("Type mismatch", 10, 5, code: "SPY0001");  // Same diagnostic

        bag1.Merge(bag2);

        var errors = bag1.GetAll().ToList();
        Assert.Single(errors);  // Duplicate removed during merge
    }

    [Fact]
    public void Deduplication_ClearResetsDuplicateTracking()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Type mismatch", 10, 5, code: "SPY0001");
        bag.Clear();
        bag.AddError("Type mismatch", 10, 5, code: "SPY0001");  // Same as before clear

        var errors = bag.GetAll().ToList();
        Assert.Single(errors);  // Should be added after clear
    }

    [Fact]
    public void Deduplication_WarningsAlsoDeduped()
    {
        var bag = new DiagnosticBag();

        bag.AddWarning("Unused variable", 10, 5, code: "SPY0451");
        bag.AddWarning("Unused variable", 10, 5, code: "SPY0451");

        var warnings = bag.GetWarnings().ToList();
        Assert.Single(warnings);
    }

    [Fact]
    public void Deduplication_MixedSeverities_StillDeduplicatesWhenPromoted()
    {
        // When warnings-as-errors is enabled, the duplicate check happens after promotion
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddWarning("Unused variable", 10, 5, code: "SPY0451");
        bag.AddWarning("Unused variable", 10, 5, code: "SPY0451");

        var errors = bag.GetErrors().ToList();
        Assert.Single(errors);  // Deduplicated after promotion
    }

    [Fact]
    public void Deduplication_SameCodeSamePositionSameSpan_OnlyAddsOnce()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(10, 5);

        bag.Add(new CompilerDiagnostic("Error A", CompilerDiagnosticSeverity.Error, 1, 1, Code: "SPY0200", Span: span));
        bag.Add(new CompilerDiagnostic("Error A variant", CompilerDiagnosticSeverity.Error, 1, 1, Code: "SPY0200", Span: span));

        Assert.Single(bag.GetAll());
    }

    [Fact]
    public void Deduplication_SameCodeSamePositionDifferentSpans_AddsBoth()
    {
        var bag = new DiagnosticBag();
        var span1 = new TextSpan(10, 5);
        var span2 = new TextSpan(20, 5);

        bag.Add(new CompilerDiagnostic("Error A", CompilerDiagnosticSeverity.Error, 1, 1, Code: "SPY0200", Span: span1));
        bag.Add(new CompilerDiagnostic("Error A", CompilerDiagnosticSeverity.Error, 1, 1, Code: "SPY0200", Span: span2));

        Assert.Equal(2, bag.GetAll().Count);
    }

    [Fact]
    public void Deduplication_NoCodeSamePositionSameMessage_OnlyAddsOnce()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(10, 5);

        bag.Add(new CompilerDiagnostic("Some error", CompilerDiagnosticSeverity.Error, 1, 1, Span: span));
        bag.Add(new CompilerDiagnostic("Some error", CompilerDiagnosticSeverity.Error, 1, 1, Span: span));

        Assert.Single(bag.GetAll());
    }

    // --- Hint Severity Tests ---

    [Fact]
    public void AddHint_AddsHintDiagnostic()
    {
        var bag = new DiagnosticBag();

        bag.AddHint("String indexing returns UTF-16 code units", 10, 5, code: "SPY0470");

        Assert.False(bag.HasErrors);
        Assert.Equal(0, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
        Assert.Equal(1, bag.HintCount);
        var hints = bag.GetHints();
        Assert.Single(hints);
        Assert.Equal("String indexing returns UTF-16 code units", hints[0].Message);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hints[0].Severity);
        Assert.Equal("SPY0470", hints[0].Code);
        Assert.Equal(10, hints[0].Line);
        Assert.Equal(5, hints[0].Column);
    }

    [Fact]
    public void AddHint_WithTextSpan_SetsSpanOnDiagnostic()
    {
        var bag = new DiagnosticBag();
        var span = new TextSpan(20, 10);

        bag.AddHint("Behavioral note", span, 5, 3, "test.spy", "SPY0470");

        var hints = bag.GetHints();
        Assert.Single(hints);
        Assert.Equal(span, hints[0].Span);
        Assert.Equal(5, hints[0].Line);
        Assert.Equal(3, hints[0].Column);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hints[0].Severity);
    }

    [Fact]
    public void AddHint_WithILocatable_ExtractsSpanCorrectly()
    {
        var bag = new DiagnosticBag();
        var locatable = new TestLocatable(new TextSpan(42, 7));

        bag.AddHint("Locatable hint", locatable, "test.spy", "SPY0470");

        var hints = bag.GetHints();
        Assert.Single(hints);
        Assert.NotNull(hints[0].Span);
        Assert.Equal(42, hints[0].Span!.Value.Start);
        Assert.Equal(7, hints[0].Span!.Value.Length);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hints[0].Severity);
    }

    [Fact]
    public void HintCount_StartsAtZero()
    {
        var bag = new DiagnosticBag();

        Assert.Equal(0, bag.HintCount);
    }

    [Fact]
    public void HintCount_IncrementsForEachHint()
    {
        var bag = new DiagnosticBag();

        bag.AddHint("First hint", 1, 1, code: "SPY0470");
        bag.AddHint("Second hint", 2, 1, code: "SPY0471");
        bag.AddHint("Third hint", 3, 1, code: "SPY0472");

        Assert.Equal(3, bag.HintCount);
        Assert.Equal(0, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void HintCount_NotAffectedByErrorsOrWarnings()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Some error", 1, 1, code: "SPY0200");
        bag.AddWarning("Some warning", 2, 1, code: "SPY0451");
        bag.AddHint("Some hint", 3, 1, code: "SPY0470");

        Assert.Equal(1, bag.ErrorCount);
        Assert.Equal(1, bag.WarningCount);
        Assert.Equal(1, bag.HintCount);
    }

    [Fact]
    public void GetHints_ReturnsOnlyHintDiagnostics()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Error", 1, 1, code: "SPY0200");
        bag.AddWarning("Warning", 2, 1, code: "SPY0451");
        bag.AddHint("Hint 1", 3, 1, code: "SPY0470");
        bag.AddHint("Hint 2", 4, 1, code: "SPY0471");

        var hints = bag.GetHints();
        Assert.Equal(2, hints.Count);
        Assert.All(hints, h => Assert.Equal(CompilerDiagnosticSeverity.Hint, h.Severity));
    }

    [Fact]
    public void GetAll_IncludesHints()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Error", 1, 1, code: "SPY0200");
        bag.AddWarning("Warning", 2, 1, code: "SPY0451");
        bag.AddHint("Hint", 3, 1, code: "SPY0470");

        var all = bag.GetAll();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, d => d.Severity == CompilerDiagnosticSeverity.Hint);
    }

    [Fact]
    public void Clear_ResetsHintCount()
    {
        var bag = new DiagnosticBag();
        bag.AddHint("Hint 1", 1, 1, code: "SPY0470");
        bag.AddHint("Hint 2", 2, 1, code: "SPY0471");

        Assert.Equal(2, bag.HintCount);

        bag.Clear();

        Assert.Equal(0, bag.HintCount);
        Assert.Empty(bag.GetHints());
    }

    [Fact]
    public void SuppressedHint_IsNotAdded()
    {
        // Hints share the suppression mechanism with warnings
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0470" });

        bag.AddHint("Behavioral hint", code: "SPY0470");

        Assert.Equal(0, bag.HintCount);
        Assert.Empty(bag.GetAll());
    }

    [Fact]
    public void SuppressedHint_OtherHintsStillAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0470" });

        bag.AddHint("Suppressed hint", code: "SPY0470");
        bag.AddHint("Other hint", code: "SPY0471");

        Assert.Equal(1, bag.HintCount);
        Assert.Equal("SPY0471", bag.GetHints()[0].Code);
    }

    [Fact]
    public void SuppressedHint_WithSpan_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0470" });

        bag.AddHint("Behavioral hint", new TextSpan(10, 5), code: "SPY0470");

        Assert.Equal(0, bag.HintCount);
    }

    [Fact]
    public void SuppressedHint_WithLocatable_IsNotAdded()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0470" });

        bag.AddHint("Behavioral hint", new TestLocatable(new TextSpan(10, 5)), code: "SPY0470");

        Assert.Equal(0, bag.HintCount);
    }

    [Fact]
    public void SuppressedHint_CaseInsensitive()
    {
        var suppressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "spy0470" };
        var bag = new DiagnosticBag(suppressedWarnings: suppressed);

        bag.AddHint("Behavioral hint", code: "SPY0470");

        Assert.Equal(0, bag.HintCount);
    }

    [Fact]
    public void HintWithNoCode_NotSuppressed()
    {
        var bag = new DiagnosticBag(suppressedWarnings: new HashSet<string> { "SPY0470" });

        bag.AddHint("Hint without code");

        Assert.Equal(1, bag.HintCount);
    }

    [Fact]
    public void WarningsAsErrors_DoesNotPromoteHints()
    {
        // Hints are advisory; -Werror should NOT promote them to errors
        var bag = new DiagnosticBag(warningsAsErrors: true);

        bag.AddHint("Behavioral hint", code: "SPY0470");

        Assert.False(bag.HasErrors);
        Assert.Equal(0, bag.ErrorCount);
        Assert.Equal(1, bag.HintCount);
        var hints = bag.GetHints();
        Assert.Single(hints);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hints[0].Severity);
    }

    [Fact]
    public void Hint_RoundTrip_PreservesSeverity()
    {
        // Verify that hint severity flows through Add → GetAll → individual
        // diagnostic without being mutated (e.g., not promoted under -Werror).
        var bag = new DiagnosticBag(warningsAsErrors: true);
        var span = new TextSpan(10, 5);

        bag.AddHint("Round-trip hint", span, line: 5, column: 3,
            filePath: "test.spy", code: "SPY0470");

        var all = bag.GetAll();
        Assert.Single(all);
        var diag = all[0];
        Assert.Equal(CompilerDiagnosticSeverity.Hint, diag.Severity);
        Assert.True(diag.IsHint);
        Assert.False(diag.IsError);
        Assert.False(diag.IsWarning);
        Assert.Equal("Round-trip hint", diag.Message);
        Assert.Equal("SPY0470", diag.Code);
        Assert.Equal(5, diag.Line);
        Assert.Equal(3, diag.Column);
        Assert.Equal("test.spy", diag.FilePath);
        Assert.Equal(span, diag.Span);
    }

    [Fact]
    public void Hint_RoundTrip_DirectAdd_PreservesSeverity()
    {
        // Verify round-trip when adding via Add() directly with a hint diagnostic.
        var bag = new DiagnosticBag();
        var hint = new CompilerDiagnostic(
            "Direct hint",
            CompilerDiagnosticSeverity.Hint,
            Line: 1,
            Column: 1,
            Code: "SPY0471");

        bag.Add(hint);

        Assert.Equal(1, bag.HintCount);
        var hints = bag.GetHints();
        Assert.Single(hints);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hints[0].Severity);
        Assert.True(hints[0].IsHint);
    }

    [Fact]
    public void Merge_TransfersHints()
    {
        var bag1 = new DiagnosticBag();
        bag1.AddHint("Hint A", 1, 1, code: "SPY0470");

        var bag2 = new DiagnosticBag();
        bag2.AddHint("Hint B", 2, 1, code: "SPY0471");

        bag1.Merge(bag2);

        Assert.Equal(2, bag1.HintCount);
        var hints = bag1.GetHints();
        Assert.Equal(2, hints.Count);
    }

    [Fact]
    public void Deduplication_HintsDeduped()
    {
        var bag = new DiagnosticBag();

        bag.AddHint("Same hint", 10, 5, code: "SPY0470");
        bag.AddHint("Same hint variant", 10, 5, code: "SPY0470");

        var hints = bag.GetHints();
        Assert.Single(hints);
    }
}

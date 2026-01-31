#pragma warning disable CS0618 // SemanticError is obsolete
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;

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
    public void FromSemanticErrors_ConvertsLegacyErrors()
    {
        var legacyErrors = new List<SemanticError>
        {
            new SemanticError("Error 1", 10, 5),
            new SemanticError("Error 2", 20, 10)
        };

        var bag = DiagnosticBag.FromSemanticErrors(legacyErrors);

        Assert.Equal(2, bag.ErrorCount);
    }

    [Fact]
    public void ToSemanticErrors_ConvertsToLegacyFormat()
    {
        var bag = new DiagnosticBag();
        bag.AddError("Error 1", 10, 5);
        bag.AddError("Error 2", 20, 10);
        bag.AddWarning("Warning 1", 30, 15); // Should be excluded

        var legacyErrors = bag.ToSemanticErrors();

        Assert.Equal(2, legacyErrors.Count);
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
}

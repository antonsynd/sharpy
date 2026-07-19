using Microsoft.CodeAnalysis;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Unit tests for <see cref="AssemblyCompiler.MapGeneratedCodeDiagnostics"/> — the single
/// mapping shared by the on-disk assembly path and the REPL's in-memory emit so neither
/// front end can leak a raw <c>CSxxxx</c> code from generated C# to the user (#1059).
/// </summary>
public class GeneratedCodeDiagnosticMappingTests
{
    private static Diagnostic MakeDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id: id,
            title: message,
            messageFormat: message,
            category: "Test",
            defaultSeverity: severity,
            isEnabledByDefault: true);
        return Diagnostic.Create(descriptor, Location.None);
    }

    [Fact]
    public void CsError_IsRemappedToSpy0908_PreservingCsIdAndText()
    {
        var csError = MakeDiagnostic("CS0103", DiagnosticSeverity.Error,
            "The name 'x' does not exist in the current context");

        var mapped = AssemblyCompiler.MapGeneratedCodeDiagnostics(new[] { csError });

        var diag = Assert.Single(mapped);
        Assert.Equal(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, diag.Code); // SPY0908
        Assert.Equal(CompilerDiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(CompilerPhase.Assembly, diag.Phase);
        // The original CS id + text are preserved inside the message so a bug report loses
        // no information, even though the user-facing code is SPY0908.
        Assert.Contains("CS0103", diag.Message);
        Assert.Contains("The name 'x' does not exist", diag.Message);
    }

    [Fact]
    public void CsWarning_IsDropped()
    {
        var csWarning = MakeDiagnostic("CS0219", DiagnosticSeverity.Warning,
            "The variable 'x' is assigned but its value is never used");

        var mapped = AssemblyCompiler.MapGeneratedCodeDiagnostics(new[] { csWarning });

        // Warnings with a CS id are internal noise from the compiler's own generated C#.
        Assert.Empty(mapped);
    }

    [Fact]
    public void NonCsDiagnostics_ArePassedThrough_Unremapped()
    {
        var nonCsError = MakeDiagnostic("XY0001", DiagnosticSeverity.Error, "analyzer error");
        var nonCsWarning = MakeDiagnostic("XY0002", DiagnosticSeverity.Warning, "analyzer warning");

        var mapped = AssemblyCompiler.MapGeneratedCodeDiagnostics(new[] { nonCsError, nonCsWarning });

        Assert.Equal(2, mapped.Count);
        Assert.Contains(mapped, d => d.Code == "XY0001" && d.Severity == CompilerDiagnosticSeverity.Error);
        Assert.Contains(mapped, d => d.Code == "XY0002" && d.Severity == CompilerDiagnosticSeverity.Warning);
        Assert.DoesNotContain(mapped,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void InfoSeverityDiagnostic_IsDropped()
    {
        var info = MakeDiagnostic("XY0003", DiagnosticSeverity.Info, "info note");

        var mapped = AssemblyCompiler.MapGeneratedCodeDiagnostics(new[] { info });

        Assert.Empty(mapped);
    }
}

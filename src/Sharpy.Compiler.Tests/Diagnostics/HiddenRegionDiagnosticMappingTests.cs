using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Covers the walk that gives a generated-C# diagnostic a <c>.spy</c> coordinate when it lands in a
/// <c>#line hidden</c> gap (#1237).
/// <para>
/// <see cref="Sharpy.Compiler.LineDirectivePostProcessor"/> anchors a <c>#line</c> at the first mapped
/// line of each statement and frames the remainder in <c>#line hidden</c>, so parts of a statement that
/// are not themselves statements — a catch-clause header, a match pattern's type — fall in hidden gaps.
/// Diagnostics there used to report the generated file with no source line and no caret.
/// </para>
/// <para>
/// The C# below is written by hand rather than emitted, so the directive layout under test is explicit
/// and the assertions do not move when codegen changes.
/// </para>
/// </summary>
public class HiddenRegionDiagnosticMappingTests
{
    private static SyntaxTree Parse(string csharp) =>
        CSharpSyntaxTree.ParseText(csharp, path: "generated.cs");

    /// <summary>
    /// The first error that actually sits in the syntax tree. Compilation-level errors (a missing
    /// predefined type, no entry point) carry <see cref="Location.None"/> and would make every
    /// assertion below read <c>null</c> regardless of the walk — which is how a green run could mean
    /// nothing.
    /// </summary>
    private static Diagnostic FirstError(SyntaxTree tree)
    {
        var compilation = CSharpCompilation.Create(
            "t",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        return compilation.GetDiagnostics()
            .First(d => d.Severity == DiagnosticSeverity.Error && d.Location.IsInSource);
    }

    [Fact]
    public void ErrorInHiddenGap_ReportsEnclosingMappedSpyCoordinate()
    {
        // Reproduces the layout Sharpy actually emits, measured from a real compile: enhanced C# 10
        // SPAN-form directives (`#line (startLine, startChar) - (endLine, endChar) charOffset "file"`)
        // framed by `#line hidden`. A catch-clause header and a match pattern's type land in exactly
        // this position — inside a hidden gap, between two mapped statement anchors — which is why
        // SPY0908 reported them at a generated-C# coordinate (#1237).
        //
        // The classic `#line 12 "file"` form does NOT reproduce it: Roslyn keeps mapping the path
        // through the hidden region there and simply continues the line count, so a test written that
        // way passes without the walk ever running.
        var tree = Parse(@"
class C
{
    void M()
    {
#line (3, 5) - (7, 1) 8 ""prog.spy""
        var a = 1;
#line hidden
        NotAType b = default;
#line default
    }
}
");
        var mapped = AssemblyCompiler.ToCompilerDiagnostic(FirstError(tree));

        mapped.FilePath.Should().Be("prog.spy");
        mapped.Line.Should().Be(3);
    }

    [Fact]
    public void ErrorOnMappedLine_KeepsItsOwnCoordinate()
    {
        // Control: an error that DOES have a mapping must be unaffected by the walk. Without this,
        // a walk that always ran would silently coarsen every diagnostic to its statement anchor.
        var tree = Parse(@"
class C
{
    void M()
    {
#line (30, 5) - (30, 20) 8 ""prog.spy""
        NotAType b = default;
#line default
    }
}
");
        var mapped = AssemblyCompiler.ToCompilerDiagnostic(FirstError(tree));

        mapped.FilePath.Should().Be("prog.spy");
        mapped.Line.Should().Be(30);
    }

    [Fact]
    public void ErrorInTreeWithoutDirectives_KeepsGeneratedFileFallback()
    {
        // EmitLineDirectives is off for the REPL, so those trees have no mapped regions at all.
        // That path must keep reporting the generated file rather than inventing a coordinate.
        var tree = Parse(@"
class C
{
    void M()
    {
        NotAType b = default;
    }
}
");
        var mapped = AssemblyCompiler.ToCompilerDiagnostic(FirstError(tree));

        mapped.FilePath.Should().Be("generated.cs");
    }

    [Fact]
    public void ErrorBeforeAnyDirective_KeepsGeneratedFileFallback()
    {
        // Nothing precedes the error, so there is no enclosing region to attribute it to. Reporting
        // the LATER mapping would be worse than the fallback: it would name a .spy line the code
        // does not come from.
        var tree = Parse(@"
class C
{
    void M()
    {
        NotAType b = default;
#line (50, 5) - (50, 20) 8 ""prog.spy""
        var a = 1;
#line default
    }
}
");
        var mapped = AssemblyCompiler.ToCompilerDiagnostic(FirstError(tree));

        mapped.FilePath.Should().Be("generated.cs");
    }
}

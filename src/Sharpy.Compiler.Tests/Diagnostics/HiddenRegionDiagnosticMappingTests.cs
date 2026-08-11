using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
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
    public void ErrorInTreeWithoutDirectives_OmitsThePositionEntirely()
    {
        // EmitLineDirectives is off for the REPL, so those trees have no mapped regions at all.
        // There is nothing to map back to, and the generated file is not a place the user can look
        // (#1427) — so no position is reported at all, and the renderer names whatever source it
        // was handed instead.
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

        mapped.FilePath.Should().BeNull();
        mapped.Line.Should().BeNull();
        mapped.Column.Should().BeNull();
    }

    [Fact]
    public void ErrorBeforeAnyDirective_OmitsThePositionRatherThanNamingTheGeneratedFile()
    {
        // Nothing precedes the error, so there is no enclosing region to attribute it to. This is
        // the shape #1427 measured: a bracket attribute sits outside every statement anchor
        // LineDirectivePostProcessor plants, and SPY0908 blamed `probe_bracket_attr.cs:13:10` — a
        // file the user never wrote. Omit the position instead.
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

        mapped.FilePath.Should().BeNull();
        mapped.Line.Should().BeNull();

        // The load-bearing half, unchanged from when this test pinned the generated-file fallback:
        // attributing the error to the LATER mapping would name a .spy line the code does not come
        // from, which is worse than saying nothing.
        mapped.FilePath.Should().NotBe("prog.spy");
        mapped.Line.Should().NotBe(50);
    }

    /// <summary>
    /// #1427 in the position it was actually measured: an attribute on a type declaration sits
    /// ahead of every statement anchor, so nothing maps it. The SPY0908 remap must not carry a
    /// generated-C# coordinate out to the user.
    /// </summary>
    [Fact]
    public void UnresolvedAttributeAheadOfEveryAnchor_ReportsSpy0908_WithNoPosition()
    {
        var tree = Parse(@"
[NoSuchAttr]
class C
{
    void M()
    {
#line (7, 5) - (7, 12) 8 ""prog.spy""
        var a = 1;
#line default
    }
}
");
        var attributeError = CSharpCompilation.Create(
                "t",
                new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) })
            .GetDiagnostics()
            .First(d => d.Id == "CS0246");

        var mapped = AssemblyCompiler.ToCompilerDiagnostic(attributeError);

        // The net still fires — the user never sees a raw CS code (#1059) …
        mapped.Code.Should().Be(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        mapped.Message.Should().Contain("CS0246");

        // … but it points at nothing rather than at generated C#.
        mapped.FilePath.Should().BeNull();
        mapped.Line.Should().BeNull();
        mapped.Column.Should().BeNull();
    }

    /// <summary>
    /// Control for the three assertions above: a diagnostic that DOES map keeps its full .spy
    /// position through the SPY0908 remap. Without this, deleting the position unconditionally
    /// would pass every "no generated coordinate" test while destroying the useful case.
    /// </summary>
    [Fact]
    public void MappedError_KeepsItsSpyPositionThroughTheSpy0908Remap()
    {
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

        mapped.Code.Should().Be(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        mapped.FilePath.Should().Be("prog.spy");
        mapped.Line.Should().Be(30);
        mapped.Column.Should().Be(5);
    }
}

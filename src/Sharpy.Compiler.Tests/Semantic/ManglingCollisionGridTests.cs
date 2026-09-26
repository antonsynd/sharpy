using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The module-level mangling-collision grid (#1268, #1288) as a standing pin.
/// </summary>
/// <remarks>
/// <para>
/// #1268's second comment measured this grid by hand once — {class, struct, interface, enum,
/// delegate, type alias} × {<c>def h</c>, module var <c>h</c>, module const <c>h</c>} × {bare,
/// backtick-escaped}, 36 cells, name pair <c>H</c>/<c>h</c> throughout — and found 3 residual ICEs,
/// all of them the <c>delegate</c> arm missing from SPY0522's collision space. That arm was added in
/// <c>90c8c7327</c>. A hand measurement decays the moment nobody re-runs it, so the grid lives here
/// instead of in an issue comment.
/// </para>
/// <para>
/// The contract per cell is the one #1268 asked for: a colliding pair is <b>refused at binding</b>
/// with SPY0522, and a non-colliding pair <b>compiles into C# that actually binds</b>. Nothing may
/// reach Roslyn as CS0102 behind SPY0908, which is the shape the issue was filed for and the shape
/// that a front-end-only assertion cannot see — so every compiling cell's generated C# is bound here
/// rather than merely being checked for the absence of Sharpy diagnostics.
/// </para>
/// <para>
/// Type aliases are erased rather than emitted as a module-class member, so they have nothing to
/// collide with and all six of their cells compile. Backtick-escaping <b>both</b> names keeps the
/// two spellings apart (the escape emits verbatim), which is the sanctioned remedy the diagnostic
/// points at; escaping only the type still collides, because the bare partner mangles onto the
/// escaped spelling — pinned by <c>TestFixtures/name_collision/grid_delegate_both_escaped</c> and by
/// the existing <c>errors/delegate_mangling_collision</c> respectively.
/// </para>
/// <para>
/// #2039 (Decision 28 (c)/(h)) moved the grid: a module's types are siblings in its namespace and its
/// functions, variables and constants are members of <c>&lt;X&gt;</c>, two scopes — so a bare type
/// <c>H</c> beside a bare binder <c>h</c> no longer shares a C# scope, and all fifteen bare cells that
/// were SPY0522 compile. Each of them also RUNS, reading <c>h</c> as the member and <c>H</c> as the
/// type (<see cref="FlippedCell_Runs_AndBindsTypeAndMemberApart"/>). Two members, or two types, of
/// one C# spelling still share a scope and are still refused (<see cref="SameScopePairs"/>).
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ManglingCollisionGridTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public ManglingCollisionGridTests(ITestOutputHelper output) : base(output) => _output = output;

    /// <summary>What a cell must do. Measured per cell, never derived from the implementation.</summary>
    private enum Outcome
    {
        /// <summary>SPY0522 at binding, naming both spellings and the first declaration's line.</summary>
        Refused,

        /// <summary>No Sharpy diagnostic, and the generated C# binds.</summary>
        Compiles,
    }

    /// <summary>
    /// A declaration of the type <c>H</c>, in its bare and backtick-escaped spellings, and — for the
    /// run cells — the declarations a program needs to use it (<see cref="Support"/>) and a statement
    /// in <c>main</c> that prints <c>6</c> through the type (<see cref="Use"/>).
    /// </summary>
    private sealed record TypeForm(string Kind, string Bare, string Escaped, Outcome BareOutcome, string Support, string Use);

    /// <summary>
    /// A module-level binding of <c>h</c>, in its bare and backtick-escaped spellings, and the
    /// expression that reads its <c>7</c> (<see cref="Read"/>).
    /// </summary>
    private sealed record BinderForm(string Kind, string Bare, string Escaped, string Read);

    private static readonly TypeForm[] TypeForms =
    {
        // #2039: the four type kinds and delegate were SPY0522 bare while every top-level declaration
        // shared the module class; a type is now a namespace sibling of <X>, so each bare cell compiles.
        new("class", "class H:\n    v: int = 6\n", "class `H`:\n    v: int = 6\n", Outcome.Compiles,
            "", "print(H().v)"),
        new("struct", "struct H:\n    v: int = 6\n", "struct `H`:\n    v: int = 6\n", Outcome.Compiles,
            "", "print(H().v)"),
        new("interface", "interface H:\n    def m(self) -> int\n", "interface `H`:\n    def m(self) -> int\n", Outcome.Compiles,
            "class K(H):\n    def m(self) -> int:\n        return 6\n", "k: H = K()\n    print(k.m())"),
        new("enum", "enum H:\n    A = 1\n", "enum `H`:\n    A = 1\n", Outcome.Compiles,
            "", "print(H.A.value + 5)"),
        // The cell #1268 was filed for: the only bare form that ICEd before 90c8c7327.
        new("delegate", "delegate H() -> None\n", "delegate `H`() -> None\n", Outcome.Compiles,
            "def six() -> None:\n    print(6)\n", "d: H = six\n    d()"),
        // Erased at emission — there is no module-class member to collide with.
        new("alias", "type H = int\n", "type `H` = int\n", Outcome.Compiles,
            "", "x: H = 6\n    print(x)"),
    };

    private static readonly BinderForm[] BinderForms =
    {
        new("def", "def h() -> int:\n    return 7\n", "def `h`() -> int:\n    return 7\n", "h()"),
        new("modvar", "h: int = 7\n", "`h`: int = 7\n", "h"),
        new("modconst", "const h: int = 7\n", "const `h`: int = 7\n", "h"),
    };

    /// <summary>
    /// Two declarations that still share one C# scope under #2039's layout, and so still collide:
    /// two members of <c>&lt;X&gt;</c>, or two sibling types, of one C# spelling.
    /// </summary>
    private static readonly (string Key, string Source)[] SameScopePairs =
    {
        ("members__def_h__def_H", "def h() -> int:\n    return 7\n\ndef H() -> int:\n    return 6\n"),
        ("members__modvar_h__def_H", "h: int = 7\n\ndef H() -> int:\n    return 6\n"),
        ("types__class_H__enum_h", "class H:\n    v: int = 6\n\nenum h:\n    A = 1\n"),
        ("types__delegate_H__struct_h", "delegate H() -> None\n\nstruct h:\n    v: int = 6\n"),
    };

    public static IEnumerable<object[]> GridCells()
    {
        foreach (var type in TypeForms)
        {
            foreach (var binder in BinderForms)
            {
                yield return new object[]
                {
                    $"{type.Kind}__{binder.Kind}__bare",
                    type.Bare + "\n" + binder.Bare,
                    type.BareOutcome.ToString(),
                };
                // Escaping both names keeps them distinct in every cell measured, including the
                // three delegate cells that ICEd while bare.
                yield return new object[]
                {
                    $"{type.Kind}__{binder.Kind}__escaped",
                    type.Escaped + "\n" + binder.Escaped,
                    Outcome.Compiles.ToString(),
                };
            }
        }

        foreach (var (key, source) in SameScopePairs)
            yield return new object[] { key, source, Outcome.Refused.ToString() };
    }

    /// <summary>The bare cells #2039 flipped from SPY0522 to compiling: every type kind but the alias.</summary>
    public static IEnumerable<object[]> FlippedCells()
        => from type in TypeForms
           where type.Kind != "alias"
           from binder in BinderForms
           select new object[] { $"{type.Kind}__{binder.Kind}__bare", type.Kind, binder.Kind };

    [Theory]
    [MemberData(nameof(FlippedCells))]
    public void FlippedCell_Runs_AndBindsTypeAndMemberApart(string cellKey, string typeKind, string binderKind)
    {
        var type = TypeForms.Single(t => t.Kind == typeKind);
        var binder = BinderForms.Single(b => b.Kind == binderKind);
        var source = type.Bare + "\n" + type.Support + "\n" + binder.Bare + "\n" +
                     "def main():\n    " + type.Use + "\n    print(" + binder.Read + ")\n";

        var result = CompileAndExecute(source);

        // `H` reaches the type (6) and `h` the module member (7) — neither shadows the other.
        Assert.True(result.Success,
            $"{cellKey}: {string.Join(" ;; ", result.CompilationErrors)} {result.StandardError}\n{source}");
        Assert.Equal("6\n7\n", result.StandardOutput.Replace("\r\n", "\n"));
    }

    [Theory]
    [MemberData(nameof(GridCells))]
    public void GridCell_IsRefusedOrCompiles_NeverIces(string cellKey, string source, string expected)
    {
        var outcome = Enum.Parse<Outcome>(expected);
        var result = CompilerApiForGrid().Compile(source, new CompilerOptions { OutputType = "library" });

        var errors = result.Diagnostics
            .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .ToList();
        _output.WriteLine($"{cellKey}: expected {outcome}, {errors.Count} error(s)");
        foreach (var error in errors)
            _output.WriteLine($"  {error.Code}: {error.Message}");

        // No cell may ICE, whichever outcome it is meant to have — that is #1268's whole complaint.
        Assert.DoesNotContain(errors, d => d.Code != null && d.Code.StartsWith("SPY09", StringComparison.Ordinal));

        if (outcome == Outcome.Refused)
        {
            var collision = errors.SingleOrDefault(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision);
            Assert.True(collision != null, $"{cellKey}: expected SPY0522, got: {Describe(errors)}");
            Assert.Contains("'h'", collision!.Message, StringComparison.Ordinal);
            Assert.Contains("'H'", collision.Message, StringComparison.Ordinal);
            // Both positions, in both forms. The message keeps naming the first declaration's line
            // as prose, for renderers and `.error` sidecars that have no channel for a second
            // position; the same fact also rides along structurally for editors (#1388).
            Assert.Contains("(line 1)", collision.Message, StringComparison.Ordinal);
            Assert.True(collision.Line.HasValue, $"{cellKey}: SPY0522 carries no line");
            Assert.True(collision.Column is > 0,
                $"{cellKey}: SPY0522 carries no column (got {collision.Column?.ToString(CultureInfo.InvariantCulture) ?? "null"})");
            var related = collision.RelatedLocations;
            Assert.True(related is { Count: 1 },
                $"{cellKey}: SPY0522 carries no structured related location for the first declaration");
            Assert.Equal(1, related![0].Line);
            Assert.True(related[0].Column is > 0,
                $"{cellKey}: the related location carries no column");
            return;
        }

        Assert.True(errors.Count == 0, $"{cellKey}: expected a clean compile, got: {Describe(errors)}");

        var csErrors = BindGeneratedCSharp(result);
        Assert.True(csErrors.Count == 0,
            $"{cellKey}: generated C# does not bind (this is the CS0102-behind-SPY0908 shape #1268 " +
            $"was filed for): {string.Join(" ;; ", csErrors)}");
    }

    private static string Describe(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join(" ;; ", diagnostics.Select(d => $"{d.Code}: {d.Message}"));

    private static CompilerApi CompilerApiForGrid()
    {
        var binDir = Path.GetDirectoryName(typeof(ManglingCollisionGridTests).Assembly.Location)!;
        return new CompilerApi(NullLogger.Instance, new[]
        {
            Path.Combine(binDir, "Sharpy.Core.dll"),
            Path.Combine(binDir, "Sharpy.Stdlib.dll"),
        });
    }

    private static List<string> BindGeneratedCSharp(CompileResult result)
    {
        var sources = new List<string>();
        if (result.GeneratedCSharp != null)
            sources.Add(result.GeneratedCSharp);
        foreach (var generated in result.GeneratedCSharpFiles.Values)
        {
            if (!sources.Contains(generated))
                sources.Add(generated);
        }

        var trees = sources
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();
        Assert.True(trees.Length > 0, "a compiling cell produced no C#");

        return CSharpBase.Value.AddSyntaxTrees(trees).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .Take(4)
            .ToList();
    }

    private static readonly Lazy<CSharpCompilation> CSharpBase = new(() =>
    {
        var refs = new List<MetadataReference>(IntegrationTestBase.GetSharedReferences());
        var seen = refs.OfType<PortableExecutableReference>()
            .Select(r => Path.GetFileName(r.FilePath))
            .Where(n => n != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var binDir = Path.GetDirectoryName(typeof(ManglingCollisionGridTests).Assembly.Location)!;
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
        {
            if (!seen.Add(Path.GetFileName(dll)))
                continue;
            try
            {
                refs.Add(MetadataReference.CreateFromFile(dll));
            }
            catch (Exception)
            {
                // Not a managed assembly.
            }
        }

        return CSharpCompilation.Create("ManglingCollisionGridBase", Array.Empty<SyntaxTree>(), refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    });
}

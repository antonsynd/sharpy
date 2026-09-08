using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Dispatch matrix for user equality and ordering dunders (#1719, #1806, #1807; plan-499995 Design
/// Decision 6). The contract, one rule per row:
/// <list type="bullet">
/// <item>the RIGHT operand of a user dunder is a store into the SELECTED overload's slot — an
/// operand with no natural type (<c>None()</c>) selects the unique Optional overload, else is
/// refused by name (SPY0222);</item>
/// <item>a bare <c>None</c> dispatches to a dunder whose parameter ADMITS None (<c>T | None</c>,
/// <c>object</c>); otherwise it is the #1079 null check — <c>False</c> on a class, SPY0222 on a
/// struct — and the dunder is never called;</item>
/// <item>a comparison whose LEFT operand has no dunder reflects to the RIGHT operand's
/// (<c>1 == d</c> is <c>d.__eq__(1)</c>, <c>1 &lt; d</c> is <c>d.__gt__(1)</c>);</item>
/// <item>an inherited dunder decides exactly as an own one (RULED 2026-09-07: walk the base chain);</item>
/// <item>a <c>__ne__</c>-only type gets its <c>==</c> complement, typed on the <c>__ne__</c> operand.</item>
/// </list>
/// Axes: use × parameter shape {int, int?, int | None, str, object} × host {class, struct}
/// (inheritance rows are class-only — a struct has no base class). Every printing cell prints the
/// dunder's own marker, so a lowering that binds but never dispatches — the pre-fix <c>d == None</c>
/// on a class — fails on output, not only on the result. The refused half is a literal count, so a
/// cell that starts compiling shows up as a red anchor rather than a silent widening.
/// </summary>
[Collection("HeavyCompilation")]
public class DunderEqualitySynthesisMatrixTests : IntegrationTestBase
{
    public DunderEqualitySynthesisMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int ShapeCount = 5;
    private const int HostCount = 2;
    private const int EqualityUseCount = 8;
    private const int OrderingUseCount = 4;
    private const int NeOnlyUseCount = 4;
    private const int InheritedUseCount = 2;
    private const int CellCount =
        (EqualityUseCount + OrderingUseCount + NeOnlyUseCount) * ShapeCount * HostCount
        + InheritedUseCount * ShapeCount;
    private const int RefusedCellCount = 28;

    /// <param name="Value">The operand spelling the annotation admits (the store seam's accepted form).</param>
    /// <param name="AdmitsBareNone">Whether a bare <c>None</c> converts to the parameter (<c>T | None</c>, <c>object</c>).</param>
    private sealed record Shape(string Name, string Annotation, string Value, bool AdmitsBareNone, bool IsOptional);

    private sealed record Host(string Name, string Keyword, string Fields, string Construct, bool IsValueType);

    private sealed record Outcome(string? Output, string? RefusalCode)
    {
        public static Outcome Prints(string output) => new(output, null);
        public static readonly Outcome RefusedByName = new(null, DiagnosticCodes.Semantic.InvalidBinaryOperation);
    }

    private sealed record Use(string Name, string Kind, Func<Shape, string> Expression, Func<Shape, Host, Outcome> Expected);

    private static readonly Shape[] Shapes =
    {
        new("Int", "int", "1", AdmitsBareNone: false, IsOptional: false),
        new("OptionalInt", "int?", "Some(1)", AdmitsBareNone: false, IsOptional: true),
        new("NullableInt", "int | None", "1", AdmitsBareNone: true, IsOptional: false),
        new("Str", "str", "\"a\"", AdmitsBareNone: false, IsOptional: false),
        new("Object", "object", "1", AdmitsBareNone: true, IsOptional: false),
    };

    private static readonly Host[] Hosts =
    {
        new("Class", "class", "", "D()", IsValueType: false),
        new("Struct", "struct", "    v: int\n", "D(1)", IsValueType: true),
    };

    /// <summary>The bare-None row: dispatch when the parameter admits None, else #1079's null check.</summary>
    private static Outcome BareNone(Shape s, Host h, string dispatched)
        => s.AdmitsBareNone ? Outcome.Prints(dispatched)
            : h.IsValueType ? Outcome.RefusedByName
            : Outcome.Prints("False\n");

    private static readonly Use[] Uses =
    {
        // ── __eq__(shape) declared; `!=` is the synthesized complement ──
        new("EqValue", "eq", s => $"d == {s.Value}", (s, h) => Outcome.Prints("called\nTrue\n")),
        new("NeValue", "eq", s => $"d != {s.Value}", (s, h) => Outcome.Prints("called\nFalse\n")),
        // Some(1) has a natural type (int?): admitted by an Optional slot and by object, refused elsewhere.
        new("EqSome", "eq", s => "d == Some(1)",
            (s, h) => s.IsOptional || s.Name == "Object" ? Outcome.Prints("called\nTrue\n") : Outcome.RefusedByName),
        // None() has no natural type: the unique Optional overload, else refusal by name.
        new("EqNoneCall", "eq", s => "d == None()",
            (s, h) => s.IsOptional ? Outcome.Prints("called\nTrue\n") : Outcome.RefusedByName),
        new("NeNoneCall", "eq", s => "d != None()",
            (s, h) => s.IsOptional ? Outcome.Prints("called\nFalse\n") : Outcome.RefusedByName),
        new("EqBareNone", "eq", s => "d == None", (s, h) => BareNone(s, h, "called\nTrue\n")),
        new("ReflectedEq", "eq", s => $"{s.Value} == d", (s, h) => Outcome.Prints("called\nTrue\n")),
        new("ReflectedBareNone", "eq", s => "None == d", (s, h) => BareNone(s, h, "called\nTrue\n")),

        // ── __lt__ and __gt__ declared: a reflected ordering swaps to the mirror ──
        new("Lt", "ord", s => $"d < {s.Value}", (s, h) => Outcome.Prints("lt\nTrue\n")),
        new("ReflectedLt", "ord", s => $"{s.Value} < d", (s, h) => Outcome.Prints("gt\nTrue\n")),
        new("Gt", "ord", s => $"d > {s.Value}", (s, h) => Outcome.Prints("gt\nTrue\n")),
        new("ReflectedGt", "ord", s => $"{s.Value} > d", (s, h) => Outcome.Prints("lt\nTrue\n")),

        // ── only __ne__ declared: `==` is the synthesized complement, both directions ──
        new("NeOnlyNe", "ne", s => $"d != {s.Value}", (s, h) => Outcome.Prints("ne\nFalse\n")),
        new("NeOnlyEq", "ne", s => $"d == {s.Value}", (s, h) => Outcome.Prints("ne\nTrue\n")),
        new("NeOnlyReflectedNe", "ne", s => $"{s.Value} != d", (s, h) => Outcome.Prints("ne\nFalse\n")),
        new("NeOnlyReflectedEq", "ne", s => $"{s.Value} == d", (s, h) => Outcome.Prints("ne\nTrue\n")),

        // ── __eq__ declared on B, used on D(B) ──
        new("InheritedEq", "inh", s => $"d == {s.Value}", (s, h) => Outcome.Prints("called\nTrue\n")),
        new("InheritedReflectedEq", "inh", s => $"{s.Value} == d", (s, h) => Outcome.Prints("called\nTrue\n")),
    };

    private static string Program(Use use, Shape shape, Host host)
    {
        var hash = shape.Name == "Object"
            ? "    def __hash__(self) -> int:\n        return 0\n"
            : string.Empty;

        var eq = $"    def __eq__(self, other: {shape.Annotation}) -> bool:\n        print(\"called\")\n        return True\n{hash}";
        var members = use.Kind switch
        {
            "eq" or "inh" => eq,
            "ord" => $"    def __lt__(self, other: {shape.Annotation}) -> bool:\n        print(\"lt\")\n        return True\n"
                + $"    def __gt__(self, other: {shape.Annotation}) -> bool:\n        print(\"gt\")\n        return True\n",
            "ne" => $"    def __ne__(self, other: {shape.Annotation}) -> bool:\n        print(\"ne\")\n        return False\n",
            _ => throw new InvalidOperationException(use.Kind),
        };

        if (use.Kind == "inh")
        {
            return $"class B:\n{members}\nclass D(B):\n    pass\n\ndef main():\n    d = D()\n    print({use.Expression(shape)})\n";
        }

        return $"{host.Keyword} D:\n{host.Fields}{members}\ndef main():\n    d = {host.Construct}\n    print({use.Expression(shape)})\n";
    }

    private static IEnumerable<(Use Use, Shape Shape, Host Host)> AllCells()
        => from u in Uses
           from s in Shapes
           from h in Hosts
           where !(u.Kind == "inh" && h.IsValueType)
           select (u, s, h);

    public static IEnumerable<object[]> Cells
        => AllCells().Select(c => new object[] { c.Use.Name, c.Shape.Name, c.Host.Name });

    [Fact]
    public void Axes_AreAnchored_AndTheRefusedHalfIsWrittenDown()
    {
        Shapes.Length.Should().Be(ShapeCount);
        Hosts.Length.Should().Be(HostCount);
        Uses.Count(u => u.Kind == "eq").Should().Be(EqualityUseCount);
        Uses.Count(u => u.Kind == "ord").Should().Be(OrderingUseCount);
        Uses.Count(u => u.Kind == "ne").Should().Be(NeOnlyUseCount);
        Uses.Count(u => u.Kind == "inh").Should().Be(InheritedUseCount);
        AllCells().Count().Should().Be(CellCount);
        AllCells().Count(c => c.Use.Expected(c.Shape, c.Host).RefusalCode != null).Should().Be(RefusedCellCount,
            "the refused half is a literal: a refusal that starts compiling, or an accepted cell that "
            + "starts refusing, moves this count");
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_DispatchesOrIsRefusedByName(string useName, string shapeName, string hostName)
    {
        var use = Uses.Single(u => u.Name == useName);
        var shape = Shapes.Single(s => s.Name == shapeName);
        var host = Hosts.Single(h => h.Name == hostName);
        var source = Program(use, shape, host);
        var expected = use.Expected(shape, host);

        var result = CompileAndExecute(source);
        var diagnostics = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"));

        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.CodeGen.EmittedTreePrecedenceInversion,
            $"[{useName} × {shapeName} × {hostName}] must never reach Roslyn or the precedence guard: {diagnostics}\n{source}");

        if (expected.Output is { } output)
        {
            result.Success.Should().BeTrue($"[{useName} × {shapeName} × {hostName}] must compile and run: {diagnostics}\n{source}");
            result.StandardOutput.Should().Be(output, $"[{useName} × {shapeName} × {hostName}] must dispatch the dunder (or not) as the contract says\n{source}");
            return;
        }

        result.Success.Should().BeFalse($"[{useName} × {shapeName} × {hostName}] must be refused by name; it printed '{result.StandardOutput}'\n{source}");
        result.RawDiagnostics.Should().ContainSingle(d => d.Code == expected.RefusalCode,
            $"[{useName} × {shapeName} × {hostName}] must report {expected.RefusalCode} exactly once: {diagnostics}\n{source}");
        result.RawDiagnostics.Single(d => d.Code == expected.RefusalCode).Message
            .Should().Contain("does not support operator", $"[{useName} × {shapeName} × {hostName}] is the operator's own refusal\n{source}");
    }
}

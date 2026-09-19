using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The collection constructor-ring argument matrix (#1868, Design Decision 4):
/// <b>constructor × argument × slot</b>.
///
/// <para><b>Contract under test:</b> a single-argument call to <c>list()</c>/<c>set()</c>/
/// <c>frozenset()</c>/<c>dict()</c> is refused BY NAME (SPY0320, <see cref="NotIterableDiagnostic"/>)
/// when the argument's type does not answer <c>__iter__</c> — checked BEFORE the <c>_expectedType</c>
/// arm that otherwise silently supplies a declared SLOT's type arguments regardless of iterability
/// (the c23 bug: <c>xs: list[int] = list(42)</c> printed <c>[]</c>). The refusal fires identically
/// whether or not a slot is present, so the slot axis is a genuine cross, not a special case.</para>
///
/// <para><b>Controls, not crossed into the matrix:</b> <c>tuple()</c> is a DIFFERENT mechanism
/// entirely — <c>tuple(42)</c> is SPY0237 (cannot infer type arguments) and <c>tuple(&lt;iterable&gt;)</c>
/// is SPY0338 (variable-length tuple unsupported) — neither is this decider's SPY0320, so tuple is a
/// boundary control proving the matrix's four constructors are the whole roster, not a sample of it.
/// An argument whose type is itself <see cref="SemanticType.Unknown"/> (already-erroring, e.g. an
/// undefined identifier) is a second control: the guard explicitly excludes Unknown so an
/// already-reported error does not ALSO get a redundant "not iterable" diagnostic piled on top.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class CollectionConstructorArgumentMatrixTests : IntegrationTestBase
{
    public CollectionConstructorArgumentMatrixTests(ITestOutputHelper output) : base(output) { }

    // ───────────────────────────── axes ─────────────────────────────

    private sealed record Ctor(string Id, bool IsPairShaped);
    private sealed record Argument(string Id);
    private sealed record Slot(string Id);

    private static readonly Ctor[] Ctors =
    {
        new("list", false),
        new("set", false),
        new("frozenset", false),
        new("dict", true),
    };

    private static readonly Argument[] Arguments =
    {
        new("non-iterable"),   // a plain int — refused SPY0320 at every constructor, slot or no slot
        new("iterable"),       // a list[int]/list[tuple[str,int]] variable — accepted
        new("custom-iterator"),// a user class with a properly-annotated generator __iter__ — accepted
    };

    private static readonly Slot[] Slots =
    {
        new("no-slot"),   // bare call — print(list(42))
        new("slot"),      // declared target with element type — xs: list[int] = list(42)
    };

    // ───────────────────────────── N/A ─────────────────────────────

    /// <summary>
    /// Every constructor × argument × slot cell applies — dict's PAIR-shaped iterable/custom-iterator
    /// argument (list[tuple[str,int]]) keeps it uniform with the three scalar constructors rather
    /// than needing its own exclusion, and the refusal for "non-iterable" reads identically for all
    /// four constructor names (only the diagnostic's <c>X()</c> noun changes). No cell is N/A.
    /// </summary>
    private static string? NaReason(Ctor ctor, Argument arg, Slot slot) => null;

    // ───────────────────────────── program generation ─────────────────────────────

    private sealed record Cell(
        string Id, string Ctor, string Argument, string Slot,
        bool Accepted, string Source, string? ExpectedOutput, string? Code, string? MessageNeedle);

    /// <summary>The module-level class text a "custom-iterator" argument needs, or "" for anything else.</summary>
    private static string ClassPreamble(Ctor ctor, Argument arg)
    {
        if (arg.Id != "custom-iterator")
            return "";

        return ctor.IsPairShaped
            ? "class PairBag:\n"
              + "    items: list[tuple[str, int]]\n\n"
              + "    def __init__(self) -> None:\n"
              + "        self.items = [(\"a\", 1), (\"b\", 2), (\"c\", 3)]\n\n"
              + "    def __iter__(self) -> tuple[str, int]:\n"
              + "        for x in self.items:\n"
              + "            yield x\n\n"
            : "class ScalarBag:\n"
              + "    items: list[int]\n\n"
              + "    def __init__(self) -> None:\n"
              + "        self.items = [1, 2, 3]\n\n"
              + "    def __iter__(self) -> int:\n"
              + "        for x in self.items:\n"
              + "            yield x\n\n";
    }

    /// <summary>Setup lines (inside main, before the constructor call) and the argument expression.</summary>
    private static (string Setup, string Expr) ArgumentExpr(Ctor ctor, Argument arg)
    {
        switch (arg.Id)
        {
            case "non-iterable":
                return ("", "42");
            case "iterable":
                return ctor.IsPairShaped
                    ? ("    src: list[tuple[str, int]] = [(\"a\", 1), (\"b\", 2), (\"c\", 3)]\n", "src")
                    : ("    src: list[int] = [1, 2, 3]\n", "src");
            case "custom-iterator":
                return ("", ctor.IsPairShaped ? "PairBag()" : "ScalarBag()");
            default:
                throw new ArgumentOutOfRangeException(nameof(arg));
        }
    }

    private static string SlotAnnotation(Ctor ctor) => ctor.Id switch
    {
        "list" => "list[int]",
        "set" => "set[int]",
        "frozenset" => "frozenset[int]",
        "dict" => "dict[str, int]",
        _ => throw new ArgumentOutOfRangeException(nameof(ctor)),
    };

    /// <summary>The print statements that read back a successfully-constructed collection, chosen to
    /// be ORDER-INDEPENDENT for set/frozenset (sorted) and deterministic for dict (keyed lookups).</summary>
    private static string ReadBack(Ctor ctor, string varName) => ctor.Id switch
    {
        "list" => $"    print({varName})\n",
        "set" or "frozenset" => $"    print(sorted({varName}))\n",
        "dict" => $"    print({varName}[\"a\"])\n    print({varName}[\"b\"])\n    print({varName}[\"c\"])\n",
        _ => throw new ArgumentOutOfRangeException(nameof(ctor)),
    };

    private static string ExpectedOutput(Ctor ctor) => ctor.Id switch
    {
        "list" or "set" or "frozenset" => "[1, 2, 3]",
        "dict" => "1\n2\n3",
        _ => throw new ArgumentOutOfRangeException(nameof(ctor)),
    };

    private static Cell BuildCell(Ctor ctor, Argument arg, Slot slot)
    {
        var id = $"{ctor.Id}/{arg.Id}/{slot.Id}";
        var preamble = ClassPreamble(ctor, arg);
        var (setup, expr) = ArgumentExpr(ctor, arg);

        var body = "def main() -> None:\n" + setup;
        bool accepted = arg.Id != "non-iterable";

        if (slot.Id == "slot")
        {
            body += $"    xs: {SlotAnnotation(ctor)} = {ctor.Id}({expr})\n";
            body += accepted ? ReadBack(ctor, "xs") : "    print(xs)\n";
        }
        else
        {
            body += accepted
                ? $"    {(ctor.Id == "dict" ? "d = " : "xs = ")}{ctor.Id}({expr})\n"
                    + ReadBack(ctor, ctor.Id == "dict" ? "d" : "xs")
                : $"    print({ctor.Id}({expr}))\n";
        }

        var source = preamble + body;

        if (!accepted)
        {
            return new Cell(id, ctor.Id, arg.Id, slot.Id, false, source, null,
                NotIterableDiagnostic.Code,
                NotIterableDiagnostic.Message(SemanticType.Int, $"{ctor.Id}() argument"));
        }

        return new Cell(id, ctor.Id, arg.Id, slot.Id, true, source, ExpectedOutput(ctor), null, null);
    }

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new List<Cell>();
        foreach (var ctor in Ctors)
            foreach (var arg in Arguments)
                foreach (var slot in Slots)
                {
                    if (NaReason(ctor, arg, slot) != null)
                        continue;
                    cells.Add(BuildCell(ctor, arg, slot));
                }
        return cells;
    }

    public static IEnumerable<object[]> Cells => BuildCells().Select(c => new object[] { c.Id });

    // ───────────────────────────── runner ─────────────────────────────

    [Theory]
    [MemberData(nameof(Cells))]
    public void MatrixCellBehavesAsTheRulePredicts(string cellId)
    {
        var cell = BuildCells().Single(c => c.Id == cellId);
        var result = CompileAndExecute(cell.Source);

        if (cell.Accepted)
        {
            result.Success.Should().BeTrue(
                $"{cellId} is a real iterable and must be accepted: "
                + string.Join("; ", result.CompilationErrors) + "\n" + cell.Source);

            var printed = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            printed.Should().Equal(
                cell.ExpectedOutput!.Split('\n', StringSplitOptions.RemoveEmptyEntries),
                $"cell {cellId} value\n{cell.Source}");
            return;
        }

        result.Success.Should().BeFalse($"{cellId} must be refused\n{cell.Source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == cell.Code && d.Message.Contains(cell.MessageNeedle!, StringComparison.Ordinal),
            $"cell {cellId} must be refused with {cell.Code} naming '{cell.MessageNeedle}'; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))
            + "\n" + cell.Source);
    }

    // ───────────────────────────── totality pin ─────────────────────────────

    [Fact]
    public void MatrixIsTotalOverItsAxes()
    {
        var cells = BuildCells();
        cells.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        Ctors.Select(c => c.Id).Should().BeEquivalentTo(
            new[] { "list", "set", "frozenset", "dict" }, "the constructor axis");
        Arguments.Select(a => a.Id).Should().BeEquivalentTo(
            new[] { "non-iterable", "iterable", "custom-iterator" }, "the argument axis");
        Slots.Select(s => s.Id).Should().BeEquivalentTo(
            new[] { "no-slot", "slot" }, "the slot axis");

        // 4 constructors x 3 arguments x 2 slots = 24, all live (no N/A cell in this matrix).
        cells.Count.Should().Be(24);

        cells.Count(c => !c.Accepted).Should().Be(8, "non-iterable x 4 constructors x 2 slots");
        cells.Count(c => c.Accepted).Should().Be(16, "iterable/custom-iterator x 4 constructors x 2 slots");
    }

    // ──────────── tuple() is a DIFFERENT mechanism (control, not a matrix cell) ────────────

    [Fact]
    public void TupleWithNonIterableArgument_IsRefused_ButNotByThisContract()
    {
        var result = CompileAndExecute("def main() -> None:\n    print(tuple(42))\n");
        result.Success.Should().BeFalse("tuple(42) must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferGenericType,
            "tuple(42) fails type-argument inference (SPY0237), not #1868's SPY0320 — a DIFFERENT "
            + "mechanism; got: " + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == NotIterableDiagnostic.Code,
            "tuple() never reaches the #1868 constructor-ring check at all");
    }

    [Fact]
    public void TupleWithIterableArgument_IsRefused_ByVariableArity_ButNotByThisContract()
    {
        var result = CompileAndExecute(
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(tuple(xs))\n");
        result.Success.Should().BeFalse("tuple(<iterable>) must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UnsupportedVariableArityTuple,
            "tuple(<iterable>) is refused as a variable-arity tuple (SPY0338), not #1868's SPY0320; "
            + "got: " + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == NotIterableDiagnostic.Code,
            "tuple() never reaches the #1868 constructor-ring check at all");
    }

    // ──────────── Unknown stays permissive (error-cascade control) ────────────

    /// <summary>
    /// The constructor-ring guard explicitly excludes <c>singleArgType == SemanticType.Unknown</c>
    /// (<c>TypeChecker.Expressions.Access.Calls.Construction.cs</c>) — an already-erroring argument
    /// (e.g. an undefined identifier) must not ALSO collect a redundant "not iterable" diagnostic on
    /// top of its own. This is the positive control proving the exclusion is load-bearing: without
    /// it, <c>list(undefined_name)</c> would report BOTH SPY0200 and SPY0320 for one mistake.
    /// </summary>
    [Fact]
    public void UnknownTypedArgument_DoesNotDoubleReportNotIterable()
    {
        var result = CompileAndExecute("def main() -> None:\n    print(list(undefined_name))\n");
        result.Success.Should().BeFalse("an undefined identifier must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UndefinedVariable,
            "the identifier itself is undefined (SPY0200)");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == NotIterableDiagnostic.Code,
            "Unknown must not ALSO be refused as not-iterable — that would double-report one mistake; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }
}

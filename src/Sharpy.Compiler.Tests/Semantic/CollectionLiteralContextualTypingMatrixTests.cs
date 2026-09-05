using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance matrix for contextual typing of collection literals (#1671).
///
/// <para><b>Contract.</b> A collection literal records the CONTEXTUAL type at every depth and
/// kind: when an expected type is in scope and every element is assignable to the expected
/// element type, the recorded type is the expected one — not the least common ancestor of the
/// elements. A store the expectation cannot absorb is refused with SPY0220, never an
/// SPY0908 ICE from the C# compiler.</para>
///
/// <para><b>Axes.</b>
/// kind {list, set, dict-key, dict-value, tuple-element}
/// × depth {1, 2, 3}
/// × direction {covariant <c>Derived → Base</c>, exact, mistyped}
/// at the annotated-declaration context (45 cells), plus every constructible context at
/// list/depth-2/covariant (11 cells, 4 of them declared N/A with the refusal that makes them
/// so), plus the <c>return</c> position for every kind (5 cells) = the 61 cells #1671's close
/// criterion names, plus 5 cells where a literal-INFERRED type meets an annotated invariant
/// position (block E — the only cells that exercise the fix's second half). A <c>form</c> axis
/// for comprehensions carries 4 documented known-red cells.</para>
///
/// <para><b>Discrimination (verification-contract §4).</b> Every covariant and exact cell
/// inserts a <c>Base</c> into the innermost collection after the literal and prints the count.
/// If the literal were typed by its elements (<c>list[Derived]</c>) instead of by its context
/// (<c>list[Base]</c>), the emitted C# is <c>Sharpy.List&lt;Derived&gt;</c> and the cell fails
/// with CS0029/CS1503 behind SPY0908 — the cell cannot pass with the bug present. The
/// <b>exact</b> direction is the control arm: it passes either way, and pins that the
/// contextual record does not break the ordinary case.</para>
///
/// <para><b>Why the tuple-element cells carry a list payload.</b> A bare <c>Derived</c> tuple
/// element is not discriminating: C# has an implicit tuple conversion, so
/// <c>(Derived, int) → (Base, int)</c> compiles whichever type the literal recorded. The cell
/// would pass with the bug present. The element is therefore a <c>list[...]</c>, for which no
/// such conversion exists.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class CollectionLiteralContextualTypingMatrixTests : IntegrationTestBase
{
    public CollectionLiteralContextualTypingMatrixTests(ITestOutputHelper output) : base(output) { }

    #region Cell model

    private enum Outcome
    {
        /// <summary>Compiles, runs, prints the expected count.</summary>
        Prints,

        /// <summary>Refused by the checker with SPY0220 (and NOT with an SPY0908 ICE).</summary>
        RefusedTypeMismatch,

        /// <summary>Refused with SPY0400 — a mutable default value; the context cannot hold a literal.</summary>
        RefusedMutableDefault,

        /// <summary>Refused with SPY0331 — the context is gated behind an experimental feature.</summary>
        RefusedFeatureGate,

        /// <summary>
        /// Refused because the generated C# does not compile, with the documented C# error. Used
        /// only for declared-N/A contexts whose N/A reason IS a known ICE: when that ICE is fixed
        /// the cell becomes constructible and this row goes red, which is the signal to promote it.
        /// <para>The assertion names the C# error, not SPY0908: <c>CompileAndExecute</c> compiles
        /// the generated C# in-process and reports the C# diagnostic directly, while the CLI
        /// <c>run</c> path wraps the same failure as SPY0908 (measured @ c68a2683d — the CLI
        /// reports "SPY0908 … (CS1736 …)" for this very source).</para>
        /// </summary>
        RefusedKnownCsError
    }

    private sealed record Cell(
        string Id,
        string Axis,
        string Source,
        Outcome Expected,
        string? ExpectedOutput = null,
        string? ExpectedInnerCsError = null,
        string Note = "");

    private const string Prelude = """
        class Base:
            def describe(self) -> str:
                return "base"


        class Derived(Base):
            def describe(self) -> str:
                return "derived"


        """;

    private static string Program(string body) => Prelude + body;

    /// <summary>
    /// The (annotation, literal, insert, read) shape of one kind at one depth. <c>$T</c> is the
    /// innermost annotated element type, <c>$E</c> the literal's innermost element expression.
    /// The insert always adds a <c>Base</c> — that is the discriminator.
    /// </summary>
    private sealed record Shape(string Annotation, string Literal, string Insert, string Read);

    private static readonly IReadOnlyDictionary<(string Kind, int Depth), Shape> Shapes =
        new Dictionary<(string, int), Shape>
        {
            [("list", 1)] = new("list[$T]", "[$E]", "v.append(Base())", "print(len(v))"),
            [("list", 2)] = new("list[list[$T]]", "[[$E]]", "v[0].append(Base())", "print(len(v[0]))"),
            [("list", 3)] = new("list[list[list[$T]]]", "[[[$E]]]", "v[0][0].append(Base())", "print(len(v[0][0]))"),

            [("set", 1)] = new("set[$T]", "{$E}", "v.add(Base())", "print(len(v))"),
            [("set", 2)] = new("list[set[$T]]", "[{$E}]", "v[0].add(Base())", "print(len(v[0]))"),
            [("set", 3)] = new("list[list[set[$T]]]", "[[{$E}]]", "v[0][0].add(Base())", "print(len(v[0][0]))"),

            [("dict-key", 1)] = new("dict[$T, int]", "{$E: 1}", "v[Base()] = 2", "print(len(v))"),
            [("dict-key", 2)] = new("list[dict[$T, int]]", "[{$E: 1}]", "v[0][Base()] = 2", "print(len(v[0]))"),
            [("dict-key", 3)] = new("list[list[dict[$T, int]]]", "[[{$E: 1}]]", "v[0][0][Base()] = 2", "print(len(v[0][0]))"),

            // dict-value nests through dict (not list): the depth-2 cell IS #1671's second
            // reported cell, where the invariant type-argument comparison decided the outcome.
            [("dict-value", 1)] = new("dict[str, $T]", "{\"a\": $E}", "v[\"b\"] = Base()", "print(len(v))"),
            [("dict-value", 2)] = new("dict[str, dict[str, $T]]", "{\"a\": {\"b\": $E}}", "v[\"a\"][\"c\"] = Base()", "print(len(v[\"a\"]))"),
            [("dict-value", 3)] = new("dict[str, dict[str, dict[str, $T]]]", "{\"a\": {\"b\": {\"c\": $E}}}", "v[\"a\"][\"b\"][\"z\"] = Base()", "print(len(v[\"a\"][\"b\"]))"),

            [("tuple-element", 1)] = new("tuple[list[$T], int]", "([$E], 1)", "v[0].append(Base())", "print(len(v[0]))"),
            [("tuple-element", 2)] = new("list[tuple[list[$T], int]]", "[([$E], 1)]", "v[0][0].append(Base())", "print(len(v[0][0]))"),
            [("tuple-element", 3)] = new("list[list[tuple[list[$T], int]]]", "[[([$E], 1)]]", "v[0][0][0].append(Base())", "print(len(v[0][0][0]))"),
        };

    private static readonly string[] Kinds = { "list", "set", "dict-key", "dict-value", "tuple-element" };
    private static readonly int[] Depths = { 1, 2, 3 };
    private static readonly string[] Directions = { "covariant", "exact", "mistyped" };

    private static readonly string[] Contexts =
    {
        "annotated-decl", "assign-existing", "return", "call-argument", "class-field-init",
        "auto-property-default", "init-field-assignment", "default-parameter",
        "ctor-parameter-default", "dataclass-default", "observer-property-default"
    };

    /// <summary>The annotated element type and the literal element expression per direction.</summary>
    private static (string T, string E) Direction(string direction) => direction switch
    {
        // list[Base] <- [Derived()] : the literal must record its CONTEXT, not its elements.
        "covariant" => ("Base", "Derived()"),
        // list[Base] <- [Base()] : control arm, passes with or without the contextual record.
        "exact" => ("Base", "Base()"),
        // list[Derived] <- [Base()] : the expectation must NOT win; SPY0220, never SPY0908.
        "mistyped" => ("Derived", "Base()"),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
    };

    private static string Fill(string template, string t, string e)
        => template.Replace("$T", t).Replace("$E", e);

    #endregion

    #region Cell construction

    private static List<Cell> BuildCells()
    {
        var cells = new List<Cell>();
        cells.AddRange(BuildKindDepthDirectionCells());
        cells.AddRange(BuildContextCells());
        cells.AddRange(BuildReturnPositionCells());
        cells.AddRange(BuildInferredTypeMeetsAnnotationCells());
        return cells;
    }

    /// <summary>Block A — kind × depth × direction at the annotated-declaration context (45).</summary>
    private static IEnumerable<Cell> BuildKindDepthDirectionCells()
    {
        foreach (var kind in Kinds)
        {
            foreach (var depth in Depths)
            {
                var shape = Shapes[(kind, depth)];
                foreach (var direction in Directions)
                {
                    var (t, e) = Direction(direction);
                    var source = Program(
                        "def main() -> None:\n"
                        + $"    v: {Fill(shape.Annotation, t, e)} = {Fill(shape.Literal, t, e)}\n"
                        + $"    {shape.Insert}\n"
                        + $"    {shape.Read}\n");

                    // The three tuple-element mistyped cells were carried as known-red against
                    // #1701 (the checker accepted the store and it ICEd as SPY0908/CS0029). They
                    // are ordinary live cells since the store seam landed: measured @ 080fb4b03,
                    // all three are SPY0220 naming the tuple pair, at depths 1, 2 and 3.

                    yield return direction == "mistyped"
                        ? new Cell($"A/{kind}/d{depth}/{direction}", "kind x depth x direction",
                            source, Outcome.RefusedTypeMismatch)
                        : new Cell($"A/{kind}/d{depth}/{direction}", "kind x depth x direction",
                            source, Outcome.Prints, ExpectedOutput: "2\n");
                }
            }
        }
    }

    /// <summary>
    /// Block B — every context at list / depth 2 / covariant (11). The four N/A contexts assert
    /// the refusal that makes them N/A rather than merely declaring it: an unverified N/A is a
    /// claim, and when the refusal stops holding the row goes red and the cell is promotable.
    /// </summary>
    private static IEnumerable<Cell> BuildContextCells()
    {
        const string Ann = "list[list[Base]]";
        const string Lit = "[[Derived()]]";
        const string Axis = "context @ list x depth 2 x covariant";

        yield return new Cell("B/annotated-decl", Axis, Program(
            "def main() -> None:\n"
            + $"    v: {Ann} = {Lit}\n"
            + "    v[0].append(Base())\n"
            + "    print(len(v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/assign-existing", Axis, Program(
            "def main() -> None:\n"
            + $"    v: {Ann} = [[Base()]]\n"
            + $"    v = {Lit}\n"
            + "    v[0].append(Base())\n"
            + "    print(len(v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/return", Axis, Program(
            $"def f() -> {Ann}:\n"
            + $"    return {Lit}\n"
            + "\n"
            + "def main() -> None:\n"
            + $"    v: {Ann} = f()\n"
            + "    v[0].append(Base())\n"
            + "    print(len(v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/call-argument", Axis, Program(
            $"def g(p: {Ann}) -> int:\n"
            + "    p[0].append(Base())\n"
            + "    return len(p[0])\n"
            + "\n"
            + "def main() -> None:\n"
            + $"    print(g({Lit}))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/class-field-init", Axis, Program(
            "class Holder:\n"
            + $"    v: {Ann} = {Lit}\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    h.v[0].append(Base())\n"
            + "    print(len(h.v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/auto-property-default", Axis, Program(
            "class Holder:\n"
            + $"    property v: {Ann} = {Lit}\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    h.v[0].append(Base())\n"
            + "    print(len(h.v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("B/init-field-assignment", Axis, Program(
            "class Holder:\n"
            + $"    v: {Ann}\n"
            + "\n"
            + "    def __init__(self) -> None:\n"
            + $"        self.v = {Lit}\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    h.v[0].append(Base())\n"
            + "    print(len(h.v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        // N/A — a collection literal cannot appear as a parameter default at all (SPY0400),
        // so there is no contextual-typing cell here to measure.
        yield return new Cell("B/default-parameter", Axis, Program(
            $"def g(p: {Ann} = {Lit}) -> int:\n"
            + "    p[0].append(Base())\n"
            + "    return len(p[0])\n"
            + "\n"
            + "def main() -> None:\n"
            + "    print(g())\n"),
            Outcome.RefusedMutableDefault,
            Note: "N/A: mutable default value (SPY0400)");

        yield return new Cell("B/ctor-parameter-default", Axis, Program(
            "class Holder:\n"
            + $"    v: {Ann}\n"
            + "\n"
            + $"    def __init__(self, v: {Ann} = {Lit}) -> None:\n"
            + "        self.v = v\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    h.v[0].append(Base())\n"
            + "    print(len(h.v[0]))\n"),
            Outcome.RefusedMutableDefault,
            Note: "N/A: mutable default value (SPY0400)");

        // @dataclass lowers the field default to a constructor parameter default. Until
        // 4eee3b8ec (plan-757fbb remediation) the validator never saw dataclass fields and the
        // list literal reached C# as CS1736 behind SPY0908; the dataclass host now consults the
        // same constant-default table as `def`, and a list literal is a mutable default (SPY0400),
        // so this context cannot hold a collection literal at all — refused, not N/A.
        yield return new Cell("B/dataclass-default", Axis, Program(
            "@dataclass\n"
            + "class Holder:\n"
            + $"    v: {Ann} = {Lit}\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    h.v[0].append(Base())\n"
            + "    print(len(h.v[0]))\n"),
            Outcome.RefusedMutableDefault,
            Note: "dataclass field default: mutable default refused (SPY0400) since 4eee3b8ec; was CS1736 behind SPY0908 @ c68a2683d");

        // N/A — property observers are gated behind the experimental `property_observers`
        // feature; ungated use is SPY0331 before any typing happens.
        yield return new Cell("B/observer-property-default", Axis, Program(
            "class Holder:\n"
            + $"    property v: {Ann} = {Lit}\n"
            + "        after_set(new_value):\n"
            + "            print(\"set\")\n"
            + "\n"
            + "def main() -> None:\n"
            + "    h: Holder = Holder()\n"
            + "    print(len(h.v))\n"),
            Outcome.RefusedFeatureGate,
            Note: "N/A: requires experimental feature 'property_observers' (SPY0331)");
    }

    /// <summary>
    /// Block C — the <c>return</c> position for every kind (5). The plan called this cell
    /// "likely live": <c>GenerateReturn</c> never had a target-type setter, so the returned
    /// literal's element type can only come from the recorded semantic type.
    /// </summary>
    private static IEnumerable<Cell> BuildReturnPositionCells()
    {
        foreach (var kind in Kinds)
        {
            var shape = Shapes[(kind, 1)];
            var (t, e) = Direction("covariant");
            var annotation = Fill(shape.Annotation, t, e);

            yield return new Cell($"C/{kind}/return", "return position x kind", Program(
                $"def f() -> {annotation}:\n"
                + $"    return {Fill(shape.Literal, t, e)}\n"
                + "\n"
                + "def main() -> None:\n"
                + $"    v: {annotation} = f()\n"
                + $"    {shape.Insert}\n"
                + $"    {shape.Read}\n"),
                Outcome.Prints, ExpectedOutput: "2\n");
        }
    }

    /// <summary>
    /// Block E — a LITERAL-INFERRED collection type meeting an annotated INVARIANT position (3).
    ///
    /// <para>Blocks A–C never exercise the type comparison the other half of the #1671 fix
    /// changed: when the expectation wins, the recorded type IS the annotation's, so nothing is
    /// compared. These cells push a literal-inferred type (an unannotated local) into an invariant
    /// <c>dict</c> position, which is where the literal's <c>GenericDefinition</c> stamp and the
    /// invariant arm's mutual <c>IsAssignable</c> recursion decide the outcome.</para>
    ///
    /// <para><b>Measured @ c68a2683d.</b> The fix's two halves are redundant rescues: with the
    /// stamp removed the name-based definition fallback still accepts, and with the tolerant
    /// invariant arm removed the stamp still makes <c>Equals</c> agree — each alone leaves all 66
    /// cells green. Removing BOTH turns the two <c>E/inferred-nested/*</c> cells red with the
    /// tell #1671 reported: <c>Cannot assign type 'dict[str, dict[str, Base]]' to variable of type
    /// 'dict[str, dict[str, Base]]'</c>. The other three E cells compare at the TOP level, where
    /// the name-based fallback rescues them — they are coverage, not falsifiers, and are labelled
    /// so rather than left looking load-bearing.</para>
    /// </summary>
    private static IEnumerable<Cell> BuildInferredTypeMeetsAnnotationCells()
    {
        const string Axis = "literal-inferred type x annotated invariant position";

        yield return new Cell("E/dict-value/d2", Axis, Program(
            "def main() -> None:\n"
            + "    inner = {\"b\": Base()}\n"
            + "    v: dict[str, dict[str, Base]] = {\"a\": inner}\n"
            + "    v[\"a\"][\"c\"] = Base()\n"
            + "    print(len(v[\"a\"]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("E/dict-value/d3", Axis, Program(
            "def main() -> None:\n"
            + "    inner = {\"c\": Base()}\n"
            + "    v: dict[str, dict[str, dict[str, Base]]] = {\"a\": {\"b\": inner}}\n"
            + "    v[\"a\"][\"b\"][\"z\"] = Base()\n"
            + "    print(len(v[\"a\"][\"b\"]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        // The two cells above compare the literal-inferred type at the TOP level, where the
        // name-based definition fallback rescues it. These two put it in a type-ARGUMENT position
        // — the outer literal is assigned to an unannotated local first, so the contextual record
        // never replaces it — which is the invariant arm's actual subject.
        yield return new Cell("E/inferred-nested/d2", Axis, Program(
            "def main() -> None:\n"
            + "    inner = {\"b\": Base()}\n"
            + "    outer = {\"a\": inner}\n"
            + "    v: dict[str, dict[str, Base]] = outer\n"
            + "    v[\"a\"][\"c\"] = Base()\n"
            + "    print(len(v[\"a\"]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("E/inferred-nested/list-in-dict", Axis, Program(
            "def main() -> None:\n"
            + "    inner = [Base()]\n"
            + "    outer = {\"a\": inner}\n"
            + "    v: dict[str, list[Base]] = outer\n"
            + "    v[\"a\"].append(Base())\n"
            + "    print(len(v[\"a\"]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");

        yield return new Cell("E/dict-key/d2", Axis, Program(
            "def main() -> None:\n"
            + "    inner = {Base(): 1}\n"
            + "    v: list[dict[Base, int]] = [inner]\n"
            + "    v[0][Base()] = 2\n"
            + "    print(len(v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n");
    }

    /// <summary>
    /// Block D — the <c>form</c> axis. Comprehensions do not receive the contextual element
    /// expectation at all @ c68a2683d, so these cells exist and are counted but are skipped:
    /// list/set comprehensions ICE (SPY0908/CS0029) and the dict comprehension is falsely
    /// refused (SPY0220). Every cell's Expected outcome is what the contract requires, so each
    /// row goes green — not rewritten — when the comprehension arm lands.
    /// </summary>
    private static List<Cell> BuildKnownRedFormCells() => new()
    {
        new Cell("D/list-comprehension/d1", "form x kind", Program(
            "def main() -> None:\n"
            + "    v: list[Base] = [Derived() for _ in range(2)]\n"
            + "    v.append(Base())\n"
            + "    print(len(v))\n"),
            Outcome.Prints, ExpectedOutput: "3\n",
            Note: "@ c68a2683d: SPY0908 CS0029 List<Derived> -> List<Base>"),

        new Cell("D/list-comprehension/d2", "form x kind", Program(
            "def main() -> None:\n"
            + "    v: list[list[Base]] = [[Derived()] for _ in range(2)]\n"
            + "    v[0].append(Base())\n"
            + "    print(len(v[0]))\n"),
            Outcome.Prints, ExpectedOutput: "2\n",
            Note: "@ c68a2683d: SPY0908 CS0029 List<List<Derived>> -> List<List<Base>>"),

        new Cell("D/set-comprehension/d1", "form x kind", Program(
            "def main() -> None:\n"
            + "    v: set[Base] = {Derived() for _ in range(2)}\n"
            + "    v.add(Base())\n"
            + "    print(len(v))\n"),
            Outcome.Prints, ExpectedOutput: "3\n",
            Note: "@ c68a2683d: SPY0908 CS0029 Set<Derived> -> Set<Base>"),

        new Cell("D/dict-comprehension/d1", "form x kind", Program(
            "def main() -> None:\n"
            + "    v: dict[int, Base] = {i: Derived() for i in range(2)}\n"
            + "    v[9] = Base()\n"
            + "    print(len(v))\n"),
            Outcome.Prints, ExpectedOutput: "3\n",
            Note: "@ c68a2683d: SPY0220 false refusal dict[int32, Derived] -> dict[int32, Base]")
    };

    #endregion

    #region Theories

    private static readonly IReadOnlyDictionary<string, Cell> CellsById =
        BuildCells().ToDictionary(c => c.Id);

    private static readonly IReadOnlyDictionary<string, Cell> KnownRedCellsById =
        BuildKnownRedFormCells().ToDictionary(c => c.Id);

    public static IEnumerable<object[]> LiveCellIds()
        => BuildCells().Select(c => new object[] { c.Id });

    public static IEnumerable<object[]> KnownRedCellIds()
        => BuildKnownRedFormCells().Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(LiveCellIds))]
    public void MatrixCell(string id)
    {
        var cell = CellsById[id];
        Output.WriteLine($"[{cell.Axis}] {cell.Id}"
            + (string.IsNullOrEmpty(cell.Note) ? "" : $" — {cell.Note}"));
        Output.WriteLine(cell.Source);

        AssertCell(cell);
    }

    /// <summary>
    /// Comprehensions are the one form that does not receive the contextual expectation
    /// @ c68a2683d. The cells exist and are counted so the fix has a target to turn green.
    /// </summary>
    [Theory(Skip = "F51 — comprehension contextual typing, lead's Wave B")]
    [MemberData(nameof(KnownRedCellIds))]
    public void KnownRedFormCell(string id) => AssertCell(KnownRedCellsById[id]);

    private void AssertCell(Cell cell)
    {
        var result = CompileAndExecute(cell.Source);
        var diagnostics = string.Join("; ", result.CompilationErrors);
        var codes = result.RawDiagnostics.Select(d => d.Code).ToList();

        bool Has(string code) => codes.Contains(code) || diagnostics.Contains(code);

        switch (cell.Expected)
        {
            case Outcome.Prints:
                result.Success.Should().BeTrue(
                    $"[{cell.Id}] must compile and run; got: {diagnostics}");
                result.StandardOutput.Should().Be(cell.ExpectedOutput,
                    $"[{cell.Id}] insert-then-count discriminates the recorded element type");
                break;

            case Outcome.RefusedTypeMismatch:
                result.Success.Should().BeFalse($"[{cell.Id}] must be refused");
                Has(DiagnosticCodes.Semantic.TypeMismatch).Should().BeTrue(
                    $"[{cell.Id}] must be refused with SPY0220; got: {diagnostics}");
                Has(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError).Should().BeFalse(
                    $"[{cell.Id}] a type error must never reach codegen as SPY0908; got: {diagnostics}");
                break;

            case Outcome.RefusedMutableDefault:
                result.Success.Should().BeFalse($"[{cell.Id}] must be refused");
                Has(DiagnosticCodes.Validation.MutableDefault).Should().BeTrue(
                    $"[{cell.Id}] declared N/A because of SPY0400; got: {diagnostics}");
                break;

            case Outcome.RefusedFeatureGate:
                result.Success.Should().BeFalse($"[{cell.Id}] must be refused");
                Has(DiagnosticCodes.Semantic.FeatureNotEnabled).Should().BeTrue(
                    $"[{cell.Id}] declared N/A because of SPY0331; got: {diagnostics}");
                break;

            case Outcome.RefusedKnownCsError:
                result.Success.Should().BeFalse($"[{cell.Id}] must be refused");
                diagnostics.Should().Contain(cell.ExpectedInnerCsError!,
                    $"[{cell.Id}] the N/A reason is that specific C# error, not any failure");
                Has(DiagnosticCodes.Semantic.TypeMismatch).Should().BeFalse(
                    $"[{cell.Id}] the context is N/A because the generated C# is rejected, "
                    + $"not because the contextual type was refused; got: {diagnostics}");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(cell), cell.Expected, null);
        }
    }

    #endregion

    #region Totality

    /// <summary>
    /// The matrix is total over its declared axes: every (kind × depth × direction) triple, every
    /// context, and the return position of every kind is a cell — a cell that is quietly absent
    /// is a cell that cannot fail.
    /// </summary>
    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        var cells = BuildCells();
        var ids = cells.Select(c => c.Id).ToHashSet();
        var missing = new StringBuilder();

        foreach (var kind in Kinds)
            foreach (var depth in Depths)
                foreach (var direction in Directions)
                    if (!ids.Contains($"A/{kind}/d{depth}/{direction}"))
                        missing.AppendLine($"A/{kind}/d{depth}/{direction}");

        foreach (var context in Contexts)
            if (!ids.Contains($"B/{context}"))
                missing.AppendLine($"B/{context}");

        foreach (var kind in Kinds)
            if (!ids.Contains($"C/{kind}/return"))
                missing.AppendLine($"C/{kind}/return");

        foreach (var id in new[]
                 {
                     "E/dict-value/d2", "E/dict-value/d3", "E/dict-key/d2",
                     "E/inferred-nested/d2", "E/inferred-nested/list-in-dict"
                 })
            if (!ids.Contains(id))
                missing.AppendLine(id);

        missing.ToString().Should().BeEmpty("every declared axis combination must be a cell");

        cells.Should().HaveCount(66,
            "45 (kind x depth x direction) + 11 contexts + 5 return positions + 5 literal-inferred-type cells");
        cells.Select(c => c.Id).Should().OnlyHaveUniqueItems();
        BuildKnownRedFormCells().Should().HaveCount(4, "the form axis carries 4 known-red cells");
    }

    #endregion
}

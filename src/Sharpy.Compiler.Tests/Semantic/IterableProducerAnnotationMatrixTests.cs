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
/// The producer-annotation route matrix (#1832, #1850): <b>dunder × body × consumer</b>.
///
/// <para><b>Contract under test:</b> <see cref="IterableElementDecider"/> is the ONE decider for
/// "what is a producer's element" for BOTH <c>__iter__</c> and <c>__reversed__</c>, over BOTH
/// synthesis (the AST annotation) and the route (the resolved type) — a generator's return IS the
/// element, a non-generator's return must NAME a producer (<c>Iterator[T]</c>/<c>IEnumerator[T]</c>/
/// <c>IEnumerable[T]</c>) to count at all. This matrix drives that agreement through THREE consumers
/// (a bare <c>for</c>, a declared producer-interface slot, and <c>list()</c>/<c>reversed()</c>) so a
/// disagreement between what synthesis put on the base list and what a route can see shows up as a
/// FAILED cell, not a silently-wrong element.</para>
///
/// <para><b>Body axis:</b> <c>gen-anno</c> (generator, <c>-&gt; int</c>, the ordinary case),
/// <c>gen-unanno</c> (generator, no annotation — Void resolves, defaulted to <c>object</c> to agree
/// with synthesis's own AST-level "object" fallback, the fix landed alongside this matrix),
/// <c>nonprod-anno</c> (non-generator naming a real producer, <c>-&gt; Iterator[int]</c>, unpeeled to
/// its element), <c>nonprod-plain</c> (non-generator returning something else, <c>-&gt; int: return
/// self.items[0]</c> — a plain method, not an iteration producer, so it synthesizes NOTHING and the
/// consumer routes read the receiver's OWN native protocols instead of a producer that isn't
/// there).</para>
///
/// <para><b>N/A:</b> <c>iter × nonprod-plain × iterate</c> is #1915 — <c>ClassifyIterableSource</c>
/// (the <c>for</c> route's OWN classifier, ungated and independent of this decider) ICEs (CS0117)
/// instead of refusing by name; every other <c>nonprod-plain</c> cell goes through THIS decider's own
/// gated code and refuses cleanly (SPY0220/SPY0320/SPY0354).</para>
///
/// <para><b>A sibling cell this matrix found and fixed (not filed):</b> before this commit,
/// <c>SynthesisAnalyzer</c>'s <c>__reversed__</c> arm synthesized <c>IReverseEnumerable[T]</c>
/// UNCONDITIONALLY — no gate mirroring the <c>__iter__</c> arm's own
/// <c>isGenerator || isRecognizedProducer</c> check (added earlier in this same phase, #1832). A
/// <c>nonprod-plain</c> <c>__reversed__</c> (<c>-&gt; int: return self.items[0]</c>) synthesized
/// <c>IReverseEnumerable&lt;int&gt;</c> anyway, and <c>GetReverseEnumerator()</c> returned an
/// <c>int</c> where an <c>IEnumerator&lt;int&gt;</c> was declared — an ICE (CS0029) the moment ANY
/// consumer named the interface, where the identical Iter-side shape cleanly refuses. Fixed directly
/// in <c>SynthesisAnalyzer</c> (same file, same gate, same phase) rather than filed — it is this
/// decider's own contract, not a separate mechanism.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class IterableProducerAnnotationMatrixTests : IntegrationTestBase
{
    public IterableProducerAnnotationMatrixTests(ITestOutputHelper output) : base(output) { }

    // ───────────────────────────── axes ─────────────────────────────

    private sealed record Dunder(string Id, string ClassName, string VarName);
    private sealed record Body(string Id, string ElementSpelling);
    private sealed record Consumer(string Id);

    private static readonly Dunder[] Dunders =
    {
        new("iter", "C", "c"),
        new("reversed", "R", "r"),
    };

    private static readonly Body[] Bodies =
    {
        new("gen-anno", "int"),        // generator, -> int: yield x
        new("gen-unanno", "object"),   // generator, no annotation — Void defaults to object
        new("nonprod-anno", "int"),    // non-generator naming a real producer, -> Iterator[int]
        new("nonprod-plain", "int"),   // non-generator returning something else, -> int
    };

    private static readonly Consumer[] Consumers =
    {
        new("iterate"),      // for x in c: / for x in reversed(r):
        new("slot"),         // e: IEnumerable[E] = c / ir: IReverseEnumerable[E] = r
        new("materialize"),  // list(c) / list(reversed(r))
    };

    // ───────────────────────────── N/A ─────────────────────────────

    /// <summary>
    /// #1915: <c>iter × nonprod-plain × iterate</c> ICEs (CS0117) through <c>ClassifyIterableSource</c>
    /// — the <c>for</c> route's OWN classifier, a DIFFERENT mechanism from this decider's gated code,
    /// which every other <c>nonprod-plain</c> cell (including <c>reversed</c>'s own <c>iterate</c>,
    /// which goes through the GATED <c>InferReversedElementType</c>/<c>reversed()</c> builtin instead)
    /// reaches cleanly. Filed, not fixed here — a genuinely separate mechanism.
    /// </summary>
    private static string? NaReason(Dunder dunder, Body body, Consumer consumer)
    {
        if (dunder.Id == "iter" && body.Id == "nonprod-plain" && consumer.Id == "iterate")
            return "#1915: __iter__'s for-route ICEs (CS0117) via the ungated ClassifyIterableSource, "
                + "a separate mechanism from this decider's own gated code";

        return null;
    }

    // ───────────────────────────── program generation ─────────────────────────────

    private sealed record Cell(
        string Id, bool Accepted, string Source, string? ExpectedOutput, string? Code, string? MessageNeedle);

    private static string MethodBody(Dunder dunder, Body body)
    {
        var methodName = dunder.Id == "iter" ? DunderNames.Iter : DunderNames.Reversed;
        var loopSrc = dunder.Id == "iter" ? "self.items" : "reversed(self.items)";

        return body.Id switch
        {
            "gen-anno" => $"    def {methodName}(self) -> int:\n        for x in {loopSrc}:\n            yield x\n",
            "gen-unanno" => $"    def {methodName}(self):\n        for x in {loopSrc}:\n            yield x\n",
            "nonprod-anno" => dunder.Id == "iter"
                ? "    def __iter__(self) -> Iterator[int]:\n        return iter(self.items)\n"
                : "    def __reversed__(self) -> Iterator[int]:\n        return reversed(self.items)\n",
            "nonprod-plain" => $"    def {methodName}(self) -> int:\n        return self.items[0]\n",
            _ => throw new ArgumentOutOfRangeException(nameof(body)),
        };
    }

    private static string ConsumerBody(Dunder dunder, Consumer consumer, string elementSpelling)
    {
        if (dunder.Id == "iter")
        {
            return consumer.Id switch
            {
                "iterate" => "    for x in c:\n        print(x)\n",
                "slot" => $"    e: IEnumerable[{elementSpelling}] = c\n    for x in e:\n        print(x)\n",
                "materialize" => "    print(list(c))\n",
                _ => throw new ArgumentOutOfRangeException(nameof(consumer)),
            };
        }

        return consumer.Id switch
        {
            "iterate" => "    for x in reversed(r):\n        print(x)\n",
            "slot" => $"    ir: IReverseEnumerable[{elementSpelling}] = r\n    for x in reversed(ir):\n        print(x)\n",
            "materialize" => "    print(list(reversed(r)))\n",
            _ => throw new ArgumentOutOfRangeException(nameof(consumer)),
        };
    }

    /// <summary>Whether this body actually synthesizes a producer interface at all — the ONLY axis
    /// that decides acceptance; consumer never flips a body's own verdict.</summary>
    private static bool IsAccepted(Body body) => body.Id != "nonprod-plain";

    private static (string Code, string MessageNeedle) RefusalFor(Dunder dunder, Consumer consumer)
    {
        if (dunder.Id == "iter")
        {
            return consumer.Id switch
            {
                "slot" => (DiagnosticCodes.Semantic.TypeMismatch,
                    "Cannot assign type 'C' to variable of type 'IEnumerable[int32]'"),
                "materialize" => (NotIterableDiagnostic.Code,
                    "Type 'C' is not iterable (missing '__iter__' method) (the list() argument)."),
                _ => throw new ArgumentOutOfRangeException(nameof(consumer)),
            };
        }

        return consumer.Id switch
        {
            "iterate" => (DiagnosticCodes.Semantic.NoMatchingOverload,
                "No overload of 'reversed' matches the argument types (R)"),
            "slot" => (DiagnosticCodes.Semantic.TypeMismatch,
                "Cannot assign type 'R' to variable of type 'IReverseEnumerable[int32]'"),
            "materialize" => (DiagnosticCodes.Semantic.NoMatchingOverload,
                "No overload of 'reversed' matches the argument types (R)"),
            _ => throw new ArgumentOutOfRangeException(nameof(consumer)),
        };
    }

    private static string ExpectedOutput(Dunder dunder, Consumer consumer) => (dunder.Id, consumer.Id) switch
    {
        ("iter", "materialize") => "[4, 5]",
        ("iter", _) => "4\n5",
        ("reversed", "materialize") => "[5, 4]",
        ("reversed", _) => "5\n4",
        _ => throw new ArgumentOutOfRangeException(nameof(dunder)),
    };

    private static Cell BuildCell(Dunder dunder, Body body, Consumer consumer)
    {
        var id = $"{dunder.Id}/{body.Id}/{consumer.Id}";
        var accepted = IsAccepted(body);

        var source = $"class {dunder.ClassName}:\n"
            + "    items: list[int]\n\n"
            + "    def __init__(self) -> None:\n"
            + "        self.items = [4, 5]\n\n"
            + MethodBody(dunder, body)
            + "\ndef main() -> None:\n"
            + $"    {dunder.VarName} = {dunder.ClassName}()\n"
            + ConsumerBody(dunder, consumer, body.ElementSpelling);

        if (!accepted)
        {
            var (code, needle) = RefusalFor(dunder, consumer);
            return new Cell(id, false, source, null, code, needle);
        }

        return new Cell(id, true, source, ExpectedOutput(dunder, consumer), null, null);
    }

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new List<Cell>();
        foreach (var dunder in Dunders)
            foreach (var body in Bodies)
                foreach (var consumer in Consumers)
                {
                    if (NaReason(dunder, body, consumer) != null)
                        continue;
                    cells.Add(BuildCell(dunder, body, consumer));
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
                $"{cellId} synthesizes a real producer interface and must be accepted: "
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
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"cell {cellId} must be refused BY NAME, never an ICE behind SPY0908\n" + cell.Source);
    }

    // ───────────────────────────── totality pin ─────────────────────────────

    [Fact]
    public void MatrixIsTotalOverItsAxes()
    {
        var cells = BuildCells();
        cells.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        Dunders.Select(d => d.Id).Should().BeEquivalentTo(new[] { "iter", "reversed" }, "the dunder axis");
        Bodies.Select(b => b.Id).Should().BeEquivalentTo(
            new[] { "gen-anno", "gen-unanno", "nonprod-anno", "nonprod-plain" }, "the body axis");
        Consumers.Select(c => c.Id).Should().BeEquivalentTo(
            new[] { "iterate", "slot", "materialize" }, "the consumer axis");

        // 2 dunders x 4 bodies x 3 consumers = 24; 1 N/A (#1915) leaves 23 live.
        int naCount = 0;
        foreach (var dunder in Dunders)
            foreach (var body in Bodies)
                foreach (var consumer in Consumers)
                    if (NaReason(dunder, body, consumer) != null)
                        naCount++;

        naCount.Should().Be(1, "exactly one N/A cell (#1915)");
        (cells.Count + naCount).Should().Be(24, $"2 x 4 x 3 = 24; live ({cells.Count}) + N/A ({naCount})");

        cells.Count(c => !c.Accepted).Should().Be(
            5, "nonprod-plain x (2 dunders x 3 consumers - 1 N/A) = 5 refused cells");
        cells.Count(c => c.Accepted).Should().Be(18, "the other 3 bodies x 2 dunders x 3 consumers");
    }

    // ──────────── Decision 9 (#1850): a wrapped generator annotation is refused, not crossed ────────────

    /// <summary>
    /// A generator annotated with a PRODUCER wrapper (<c>-&gt; Iterator[int]</c>) instead of its
    /// element is refused at the DECLARATION itself (SPY0220, steered by
    /// <c>GeneratorReturnSteer</c>) — before any consumer is ever reached, so this is a single cell
    /// per dunder rather than crossed with the consumer axis (the same shape as the constructor-ring
    /// matrix's tuple() controls: the refusal fires at a point the consumer axis cannot see).
    /// </summary>
    [Theory]
    [InlineData("iter", "__iter__", "self.items")]
    [InlineData("reversed", "__reversed__", "reversed(self.items)")]
    public void GeneratorWithWrappedAnnotation_IsRefused_ByDecision9(string _, string methodName, string loopSrc)
    {
        var source = "class C:\n"
            + "    items: list[int]\n\n"
            + "    def __init__(self) -> None:\n"
            + "        self.items = [4, 5]\n\n"
            + $"    def {methodName}(self) -> Iterator[int]:\n"
            + $"        for x in {loopSrc}:\n"
            + "            yield x\n\n"
            + "def main() -> None:\n"
            + "    pass\n";

        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"a generator annotated -> Iterator[int] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains("a generator's return annotation is its element type", StringComparison.Ordinal),
            $"the refusal steers to the element spelling; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")) + "\n" + source);
    }

    // ──────────── generic host (#1859): a constructed generic receiver gets the SAME answer ────────────

    /// <summary>
    /// <c>TryGetGenericHostCore</c> (#1859) is a SEPARATE arm in <c>TypeInferenceService</c> from the
    /// plain <c>UserDefinedType</c> arm the main matrix exercises — a generic host's element must
    /// agree with the SAME decider, substituted through the receiver's own type arguments. Two spot
    /// checks (not a full cross with the main matrix, mirroring how <c>IterationRingRouteMatrixTests</c>
    /// treats its own slot axis): an unannotated generator accepted with its `T`-substituted element,
    /// and a non-generator-non-producer `__reversed__` refused exactly like the plain-host mirror.
    /// </summary>
    [Fact]
    public void GenericHost_UnannotatedGeneratorIter_AcceptsAndUnpeelsSubstitutedElement()
    {
        var result = CompileAndExecute(
            "class Box[T]:\n"
            + "    v: T\n\n"
            + "    def __init__(self, v: T) -> None:\n"
            + "        self.v = v\n\n"
            + "    def __iter__(self):\n"
            + "        yield self.v\n\n"
            + "def main() -> None:\n"
            + "    b = Box[int](5)\n"
            + "    for x in b:\n"
            + "        print(x)\n"
            + "    print(list(Box[int](7)))\n");

        result.Success.Should().BeTrue(
            "an unannotated generator __iter__ on a generic host must be accepted: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Should().Equal("5", "[7]");
    }

    [Fact]
    public void GenericHost_NonProducerReversed_IsRefused_LikeThePlainHostMirror()
    {
        var result = CompileAndExecute(
            "class Box[T]:\n"
            + "    v: T\n\n"
            + "    def __init__(self, v: T) -> None:\n"
            + "        self.v = v\n\n"
            + "    def __reversed__(self) -> T:\n"
            + "        return self.v\n\n"
            + "def main() -> None:\n"
            + "    b = Box[int](5)\n"
            + "    ir: IReverseEnumerable[int] = b\n"
            + "    print(ir)\n");

        result.Success.Should().BeFalse(
            "a non-generator, non-producer __reversed__ on a generic host synthesizes nothing");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains("Cannot assign type 'Box[int32]' to variable of type 'IReverseEnumerable[int32]'", StringComparison.Ordinal),
            "refused by name, matching the plain-host mirror exactly; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "refused BY NAME, never an ICE behind SPY0908");
    }
}

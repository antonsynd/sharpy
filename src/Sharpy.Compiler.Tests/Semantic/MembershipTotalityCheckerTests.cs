using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The checker half of P93 (plan-67a01f, #2645 #2648 #2653): what the type checker decides and records
/// for <c>in</c> on a class the iteration ring enumerates (D1), the binding type of a membership needle
/// (D2), the ONE link routine a binary operator and every comparison-chain link take (D3), and link 0
/// of a chain whose head was spelled like a type (D4). Analysis only: the emitted form and the answers
/// belong to the stage-0 generator and the fixtures.
/// </summary>
public class MembershipTotalityCheckerTests
{
    private const string Prelude = """
        from System.Drawing import Point, RectangleF

        enum Color:
            RED = 1
            GREEN = 2

        class Outer:
            class Inner:
                pass

        class Dog:
            pass

        class IterBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __iter__(self) -> int:
                for v in self.items:
                    yield v

        class SubIterBag(IterBag):
            def __init__(self) -> None:
                super().__init__(1, 2)

        class GIterBag[T]:
            items: list[T]

            def __init__(self, items: list[T]) -> None:
                self.items = items

            def __iter__(self) -> T:
                for v in self.items:
                    yield v

        class IntGIterBag(GIterBag[int]):
            def __init__(self) -> None:
                super().__init__([1, 2])

        class FwdGIterBag[U](GIterBag[U]):
            def __init__(self, items: list[U]) -> None:
                super().__init__(items)

        class ListOf[X](GIterBag[list[X]]):
            def __init__(self, items: list[list[X]]) -> None:
                super().__init__(items)

        class ProducerBase[K, V]:
            vals: list[V]

            def __init__(self, vals: list[V]) -> None:
                self.vals = vals

            def __iter__(self) -> Iterator[V]:
                return iter(self.vals)

        class StrProducer(ProducerBase[int, str]):
            def __init__(self) -> None:
                super().__init__(["a"])

        class GContainsBag[T]:
            items: list[T]

            def __init__(self, items: list[T]) -> None:
                self.items = items

            def __contains__(self, item: T) -> bool:
                return item in self.items

        class IntGContainsBag(GContainsBag[int]):
            def __init__(self) -> None:
                super().__init__([1])

        class ProducerBag:
            items: list[str]

            def __init__(self) -> None:
                self.items = ["a"]

            def __iter__(self) -> Iterator[str]:
                return iter(self.items)

        class NextBag:
            n: int

            def __init__(self) -> None:
                self.n = 0

            def __iter__(self) -> NextBag:
                return self

            def __next__(self) -> float:
                self.n += 1
                return 1.5

        class ContainsMethodBag:
            items: list[int]

            def __init__(self) -> None:
                self.items = [1]

            def contains(self, v: int) -> bool:
                return False

            def __iter__(self) -> int:
                for v in self.items:
                    yield v

        class DunderContainsBag:
            def __contains__(self, v: str) -> bool:
                return True

            def __iter__(self) -> int:
                yield 1

        class LB:
            def __iter__(self) -> list[int]:
                return [1, 2]

        class SeqBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __len__(self) -> int:
                return len(self.items)

            def __getitem__(self, i: int) -> int:
                return self.items[i]

        class GSeqBag[T]:
            items: list[T]

            def __init__(self, items: list[T]) -> None:
                self.items = items

            def __len__(self) -> int:
                return len(self.items)

            def __getitem__(self, i: int) -> T:
                return self.items[i]

        def nothing() -> None:
            pass


        """;

    private sealed record Analysis(
        Module Module, SemanticInfo Info, IReadOnlyList<CompilerDiagnostic> Errors, IReadOnlyList<CompilerDiagnostic> Warnings)
    {
        public IEnumerable<string> ErrorCodes => Errors.Select(e => e.Code ?? "?");

        /// <summary>The type checker's own diagnostics (the validators' — SPY0320, SPY0402 — are T4's).</summary>
        public IReadOnlyList<CompilerDiagnostic> CheckerDiagnostics
            => Errors.Concat(Warnings).Where(d => d.Phase == CompilerPhase.TypeChecking).ToList();

        public IReadOnlyList<CompilerDiagnostic> CheckerErrors
            => Errors.Where(d => d.Phase == CompilerPhase.TypeChecking).ToList();
        public string Describe() => string.Join("; ", Errors.Concat(Warnings).Select(d => $"{d.Code}@{d.Line}:{d.Column} {d.Message}"));
    }

    private static Analysis Analyze(string declarations, string expression)
    {
        var body = string.Concat(declarations
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => $"    {line}\n"));
        var source = Prelude + "def main() -> None:\n" + body + $"    b: bool = {expression}\n    print(b)\n";
        var options = new CompilerOptions
        {
            References = new[]
            {
                Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location,
                typeof(System.Drawing.PointF).Assembly.Location,
            },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var result = new Compiler(options, NullLogger.Instance).Analyze(source, "membership_totality.spy");
        Assert.True(result.Module != null && result.SemanticInfo != null, "analysis produced no module:\n" + source);
        return new Analysis(result.Module!, result.SemanticInfo!,
            result.Diagnostics.GetErrors().ToList(), result.Diagnostics.GetWarnings().ToList());
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        node.GetChildNodes().SelectMany(child => Descendants(child).Prepend(child));

    /// <summary>The value of `b: bool = …` in main: the expression under test.</summary>
    private static Expression TestedExpression(Analysis analysis)
    {
        var main = Descendants(analysis.Module).OfType<FunctionDef>().Single(f => f.Name == "main");
        return Descendants(main).OfType<VariableDeclaration>()
            .Single(d => d.Name == "b").InitialValue!;
    }

    // ---------------------------------------------------------------- D1: IterationMembershipElement

    /// <summary>
    /// D1: a Sharpy class with no <c>__contains__</c> whose iteration the ring synthesizes answers
    /// <c>in</c> with the element of that <c>IEnumerable[T]</c>, recorded on the container node as a
    /// <c>Direct</c> projection. Each <c>__iter__</c> shape that synthesizes one answers.
    /// </summary>
    [Theory]
    [InlineData("bag: IterBag = IterBag(1, 2)", "1 in bag", "int32")]                        // generator __iter__
    [InlineData("bag: IterBag = IterBag(1, 2)", "1 not in bag", "int32")]
    [InlineData("bag: GIterBag[int] = GIterBag[int]([1, 2])", "1 in bag", "int32")]          // constructed generic
    [InlineData("bag: GIterBag[str] = GIterBag[str]([\"a\"])", "\"a\" in bag", "str")]
    [InlineData("bag: ProducerBag = ProducerBag()", "\"a\" in bag", "str")]                // producer __iter__, unpeeled
    [InlineData("bag: NextBag = NextBag()", "1.5 in bag", "float64")]                        // __next__ beside __iter__
    [InlineData("bag: SubIterBag = SubIterBag()", "1 in bag", "int32")]                      // inherited __iter__
    [InlineData("bag: IntGIterBag = IntGIterBag()", "1 in bag", "int32")]                    // inherited from a closed generic base
    [InlineData("bag: FwdGIterBag[str] = FwdGIterBag[str]([\"a\"])", "\"a\" in bag", "str")] // a base clause forwarding the receiver's own
    [InlineData("bag: ListOf[int] = ListOf[int]([[1]])", "[1] in bag", "list[int32]")]     // a forwarded argument nested in the clause
    [InlineData("bag: StrProducer = StrProducer()", "\"a\" in bag", "str")]                  // the clause's SECOND argument, producer unpeeled
    [InlineData("bag: ContainsMethodBag = ContainsMethodBag()", "1 in bag", "int32")]        // a method `contains` is not __contains__
    public void IteratingClass_AnswersMembership_WithItsSynthesizedElement(string declarations, string expression, string element)
    {
        var analysis = Analyze(declarations, expression);

        Assert.True(analysis.CheckerErrors.Count == 0, $"`{expression}` is refused: {analysis.Describe()}");
        var membership = (BinaryOp)TestedExpression(analysis);
        var projection = analysis.Info.GetIterableProjection(membership.Right);
        Assert.NotNull(projection);
        Assert.Equal(IterableProjectionKind.Direct, projection!.Kind);
        Assert.Equal(element, projection.ElementType.GetDisplayName());
    }

    /// <summary>
    /// D1, the shapes that do NOT answer: an <c>__iter__</c> that names no producer (#1915), the legacy
    /// <c>__getitem__</c> + <c>__len__</c> pair (R-OA), and a class with <c>__contains__</c> (it answers
    /// by its own dunder). No D1 record; the first two are refused by the protocol check (SPY0320).
    /// </summary>
    [Theory]
    [InlineData("bag: LB = LB()", "1 in bag", true)]
    [InlineData("bag: SeqBag = SeqBag(1, 2)", "1 in bag", true)]
    [InlineData("bag: GSeqBag[int] = GSeqBag[int]([1, 2])", "1 in bag", true)]
    [InlineData("bag: DunderContainsBag = DunderContainsBag()", "\"a\" in bag", false)]
    public void NonIteratingShape_RecordsNoIterationElement(string declarations, string expression, bool refused)
    {
        var analysis = Analyze(declarations, expression);

        var membership = (BinaryOp)TestedExpression(analysis);
        Assert.Null(analysis.Info.GetIterableProjection(membership.Right));
        if (refused)
            Assert.Equal(new[] { DiagnosticCodes.Semantic.ProtocolMissingMethod }, analysis.ErrorCodes.ToArray());
        else
            Assert.True(analysis.Errors.Count == 0, analysis.Describe());
    }

    /// <summary>
    /// M5 (R-OA): a Sharpy-declared generic host takes no needle slot from <c>__getitem__</c>, so the
    /// literal <c>None</c> into <c>GSeqBag[int]</c> is the protocol refusal (SPY0320) alone — not a
    /// needle refusal (SPY0222) against a slot the class never offered for membership.
    /// </summary>
    [Fact]
    public void GenericSequencePair_TakesNoSlotFromGetItem_M5()
    {
        var analysis = Analyze("bag: GSeqBag[int] = GSeqBag[int]([1, 2])", "None in bag");

        Assert.Equal(new[] { DiagnosticCodes.Semantic.ProtocolMissingMethod }, analysis.ErrorCodes.ToArray());
    }

    /// <summary>D1's slot is a real slot: a needle it does not take is refused by the needle check.</summary>
    [Fact]
    public void IteratingClass_RefusesANeedleItsElementDoesNotTake()
    {
        var analysis = Analyze("bag: IterBag = IterBag(1, 2)", "\"a\" in bag");

        var error = Assert.Single(analysis.CheckerErrors);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidBinaryOperation, error.Code);
        Assert.Contains("does not support operator 'in' with operand of type 'IterBag'", error.Message);
    }

    /// <summary>
    /// An inherited dunder's slot is the base clause's written argument, not the base's open
    /// parameter: it refuses a needle that argument does not take, as a declared one does.
    /// </summary>
    [Theory]
    [InlineData("bag: IntGIterBag = IntGIterBag()", "\"a\" in bag", "IntGIterBag")]
    [InlineData("bag: FwdGIterBag[int] = FwdGIterBag[int]([1])", "\"a\" in bag", "FwdGIterBag[int32]")]
    [InlineData("bag: IntGContainsBag = IntGContainsBag()", "\"a\" in bag", "IntGContainsBag")]
    public void InheritedGenericDunder_RefusesANeedleTheClauseArgumentDoesNotTake(
        string declarations, string expression, string container)
    {
        var analysis = Analyze(declarations, expression);

        var error = Assert.Single(analysis.CheckerErrors);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidBinaryOperation, error.Code);
        Assert.Contains($"Type 'str' does not support operator 'in' with operand of type '{container}'", error.Message);
    }

    // ---------------------------------------------------------------- D2: the needle's binding type

    [Theory]
    [InlineData("", "1 in [1, 2]", "int32")]                                           // the slot
    [InlineData("n: int = 1", "n in [1.5, 2.5]", "float64")]                           // the slot, not the needle's own type
    [InlineData("x: int | None = None", "x in [1, 2]", "int32 | None")]                // a lifted needle keeps its own type
    [InlineData("x: str | None = None", "x in \"abc\"", "str | None")]               // MembershipNoneRaises
    [InlineData("xs: list[int | None] = [None]; x: int | None = None", "x in xs", "int32 | None")]
    [InlineData("xs: list[int | None] = [None]", "None in xs", "int32 | None")]       // the literal None, into its slot
    [InlineData("bag: IterBag = IterBag(1, 2)", "1 not in bag", "int32")]              // D1's slot
    [InlineData("bag: IntGIterBag = IntGIterBag()", "1 in bag", "int32")]              // D1's slot, inherited from GIterBag[int]
    [InlineData("bag: IntGContainsBag = IntGContainsBag()", "1 in bag", "int32")]      // an inherited __contains__(item: T) at T = int
    public void AcceptedNeedle_RecordsItsBindingType(string declarations, string expression, string binding)
    {
        var analysis = Analyze(declarations, expression);

        Assert.True(analysis.CheckerErrors.Count == 0, $"`{expression}` is refused: {analysis.Describe()}");
        var needle = ((BinaryOp)TestedExpression(analysis)).Left;
        Assert.True(analysis.Info.TryGetMembershipNeedleBinding(needle, out var recorded), $"`{expression}`: no binding recorded");
        Assert.Equal(binding, recorded!.GetDisplayName());
    }

    /// <summary>
    /// D2's absences, each beside a positive control on the same container shape: no record for a
    /// container with no slot (#2249), a needle only the CLR call seam takes (#2647), or a refused needle.
    /// </summary>
    [Theory]
    [InlineData("bag: SeqBag = SeqBag(1, 2)", "1 in bag")]
    [InlineData("rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "Point(1, 1) in rf")]
    [InlineData("", "\"a\" in [1, 2]")]
    public void SlotlessOrClrOnlyOrRefusedNeedle_RecordsNoBinding(string declarations, string expression)
    {
        var analysis = Analyze(declarations, expression);

        var needle = ((BinaryOp)TestedExpression(analysis)).Left;
        Assert.False(analysis.Info.TryGetMembershipNeedleBinding(needle, out var recorded),
            $"`{expression}` recorded a binding '{recorded?.GetDisplayName()}'");

        var control = Analyze("", "1 in [1, 2]");
        Assert.True(control.Info.TryGetMembershipNeedleBinding(((BinaryOp)TestedExpression(control)).Left, out _),
            "positive control: an accepted needle records its binding");
    }

    [Fact]
    public void ChainNeedles_RecordTheirBindings_AsTheBinaryTwinsDo()
    {
        var analysis = Analyze("x: int | None = None; xs: list[int] = [1, 2]", "x in xs in [xs]");

        Assert.True(analysis.CheckerErrors.Count == 0, analysis.Describe());
        var chain = (ComparisonChain)TestedExpression(analysis);
        Assert.True(analysis.Info.TryGetMembershipNeedleBinding(chain.Operands[0], out var first));
        Assert.Equal("int32 | None", first!.GetDisplayName());
        Assert.True(analysis.Info.TryGetMembershipNeedleBinding(chain.Operands[1], out var second));
        Assert.Equal("list[int32]", second!.GetDisplayName());
    }

    // ---------------------------------------------------------------- D3: the link routine

    private static string Positioned(CompilerDiagnostic d) => $"{d.Code}@{d.Line}:{d.Column} {d.Message}";

    /// <summary>
    /// D3: link 0 of <c>L op R tail</c> is the binary twin <c>L op R</c> — the same diagnostics (code,
    /// message and position: the link is reported where the twin is) and the same lowering kind and
    /// equality strategy — for a refusal as for an accepted link. Each tail is a link that is clean on
    /// its own, so every diagnostic of the chain is link 0's.
    /// </summary>
    [Theory]
    // in / not in: a refusal, a lift, D1's iterating container.
    [InlineData("xs: list[int] = [1]", "\"a\"", "in", "xs", "in [xs]", ComparisonLinkShape.Membership, OperatorLoweringKind.Native)]
    [InlineData("xs: list[int] = [1]", "\"a\"", "not in", "xs", "in [xs]", ComparisonLinkShape.Membership, OperatorLoweringKind.Native)]
    [InlineData("x: int | None = None; xs: list[int] = [1]", "x", "in", "xs", "in [xs]", ComparisonLinkShape.Membership, OperatorLoweringKind.MembershipNoneIsFalse)]
    [InlineData("x: int | None = None; xs: list[int] = [1]", "x", "not in", "xs", "in [xs]", ComparisonLinkShape.Membership, OperatorLoweringKind.MembershipNoneIsFalse)]
    [InlineData("x: str | None = None; s: str = \"abc\"", "x", "in", "s", "in [s]", ComparisonLinkShape.Membership, OperatorLoweringKind.MembershipNoneRaises)]
    [InlineData("bag: IterBag = IterBag(1, 2)", "1", "in", "bag", "in [bag]", ComparisonLinkShape.Membership, OperatorLoweringKind.Native)]
    // is / is not: enum identity, None tests, reference identity with its value-type warning.
    [InlineData("c: Color = Color.RED; d: Color = Color.RED", "c", "is", "d", "is d", ComparisonLinkShape.Identity, OperatorLoweringKind.EnumIdentityEquals)]
    [InlineData("c: Color = Color.RED; o: object = Color.RED", "c", "is not", "o", "is not o", ComparisonLinkShape.Identity, OperatorLoweringKind.EnumIdentityObjectEquals)]
    [InlineData("s: str | None = None", "s", "is", "None", "is None", ComparisonLinkShape.NoneTest, OperatorLoweringKind.ReferenceNoneTest)]
    [InlineData("n: int = 1", "n", "is not", "None", "is None", ComparisonLinkShape.NoneTest, OperatorLoweringKind.BoxedNoneTest)]
    [InlineData("s: str | None = None", "None", "is", "s", "is s", ComparisonLinkShape.NoneTest, OperatorLoweringKind.ReferenceNoneTest)]
    [InlineData("i: int = 1; j: int = 1", "i", "is", "j", "== j", ComparisonLinkShape.Identity, OperatorLoweringKind.Native)]
    [InlineData("p: Dog = Dog(); q: Dog = Dog()", "p", "is", "q", "is q", ComparisonLinkShape.Identity, OperatorLoweringKind.Native)]
    // == / != / <: refusals (SPY0222 with its steers, the void gate SPY0329), ordinal strings, constants.
    [InlineData("", "\"a\"", "<", "1", "< 2", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    [InlineData("n: int = 1", "n", "==", "None", "is None", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    [InlineData("n: int = 1", "nothing()", "==", "n", "== n", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    [InlineData("s: str = \"a\"; t: str = \"b\"", "s", "<", "t", "<= t", ComparisonLinkShape.Comparison, OperatorLoweringKind.StringOrdinalCompare)]
    [InlineData("u: uint64 = 3", "u", ">", "1", "< u", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    [InlineData("d: decimal = decimal(1); f: float = 1.5", "d", "<", "f", "< 2.0", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    [InlineData("t: tuple[int, int] = (1, 2)", "t", "==", "t", "== t", ComparisonLinkShape.Comparison, OperatorLoweringKind.Native)]
    public void ChainLink_IsItsBinaryTwin(
        string declarations, string left, string op, string right, string tail,
        ComparisonLinkShape shape, OperatorLoweringKind kind)
    {
        var twin = Analyze(declarations, $"{left} {op} {right}");
        var chained = Analyze(declarations, $"{left} {op} {right} {tail}");

        // The tail on its own is clean, so the chain's checker diagnostics are link 0's: the twin's,
        // verbatim (the validators' verdicts per link are ChainLinkValidationTests').
        Assert.Equal(
            twin.CheckerDiagnostics.Select(Positioned).OrderBy(s => s, StringComparer.Ordinal),
            chained.CheckerDiagnostics.Select(Positioned).OrderBy(s => s, StringComparer.Ordinal));

        var binary = (BinaryOp)TestedExpression(twin);
        var twinKind = twin.Info.GetOperatorLoweringForIr(binary)?.Kind ?? OperatorLoweringKind.Native;
        var link = chained.Info.GetComparisonChainLowering((ComparisonChain)TestedExpression(chained))!.Links[0];
        Assert.Equal(kind, twinKind);
        Assert.Equal(twinKind, link.Kind);
        Assert.Equal(shape, link.Shape);
        Assert.Equal(twin.Info.GetBinaryOpLoweringForIr(binary), link.Equality ?? BinaryOpLowering.NativeOperator);
    }

    /// <summary>Positive control for the checker-phase filter the twin theory compares through.</summary>
    [Fact]
    public void CheckerPhaseFilter_SeesTheCheckersRefusal()
    {
        var analysis = Analyze("xs: list[int] = [1]", "\"a\" in xs in [xs]");

        Assert.Contains(analysis.CheckerDiagnostics, d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);
    }

    /// <summary>
    /// D3: the void gate and the refusals are the link's own whatever its position — the second link of
    /// a chain whose first is clean draws its twin's diagnostic at the twin's position.
    /// </summary>
    [Fact]
    public void SecondLink_DrawsItsTwinsRefusal()
    {
        var twin = Analyze("ss: list[str] = [\"a\"]", "2 not in ss");
        var chained = Analyze("ss: list[str] = [\"a\"]", "1 < 2 not in ss");

        var expected = Assert.Single(twin.Errors);
        var actual = Assert.Single(chained.Errors);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Contains("operator 'not in'", actual.Message);
    }

    /// <summary>
    /// D3 for consecutive membership links: in <c>h op0 m op1 c</c> with both operators <c>in</c> /
    /// <c>not in</c>, <c>m</c> is link 1's needle AND link 0's container. Link 1 is still its twin
    /// <c>m op1 c</c>: its container carries the twin's setup (the tuple array bridge, D1's
    /// <c>Direct</c> projection), its needle the twin's binding, the link the twin's lowering and
    /// diagnostics. The head link is clean, so every checker diagnostic is link 1's.
    /// </summary>
    [Theory]
    [InlineData("xs: list[int] = [1]; t: tuple[list[int], list[int]] = ([1], [2])", "1", "in", "xs", "in", "t")]
    [InlineData("xs: list[int] = [1]; t: tuple[list[int], list[int]] = ([1], [2])", "1", "not in", "xs", "in", "t")]
    [InlineData("xs: list[int] = [1]; t: tuple[list[int], list[int]] = ([1], [2])", "1", "in", "xs", "not in", "t")]
    [InlineData("xs: list[int] = [1]; t: tuple[list[int], list[int]] = ([1], [2])", "1", "in", "[]", "in", "t")]
    [InlineData("ss: list[str] = [\"a\"]; t: tuple[list[int], list[int]] = ([1], [2])", "\"a\"", "in", "ss", "in", "t")]
    [InlineData("xs: list[int] = [1]; bag: ListOf[int] = ListOf[int]([[1]])", "1", "in", "xs", "in", "bag")]
    [InlineData("xs: list[int] = [1]; bag: ListOf[int] = ListOf[int]([[1]])", "1", "not in", "xs", "not in", "bag")]
    [InlineData("xs: list[int] = [1]", "1", "in", "xs", "in", "[xs]")]
    [InlineData("x: int | None = None; xs: list[int | None] = [None]", "x", "in", "xs", "in", "[xs]")]
    public void ConsecutiveInLink_IsItsBinaryTwin(
        string declarations, string head, string op0, string middle, string op1, string container)
    {
        var twin = Analyze(declarations, $"{middle} {op1} {container}");
        var chained = Analyze(declarations, $"{head} {op0} {middle} {op1} {container}");

        Assert.Equal(
            twin.CheckerDiagnostics.Select(d => $"{d.Code} {d.Message}").OrderBy(s => s, StringComparer.Ordinal),
            chained.CheckerDiagnostics.Select(d => $"{d.Code} {d.Message}").OrderBy(s => s, StringComparer.Ordinal));

        var binary = (BinaryOp)TestedExpression(twin);
        var chain = (ComparisonChain)TestedExpression(chained);
        // Two compilations: the projection is compared by its facts, its element by display name.
        static string? Facts(IterableArgumentProjection? p)
            => p == null ? null : $"{p.Kind} {p.ElementType.GetDisplayName()} {p.TupleArity}";
        Assert.Equal(
            Facts(twin.Info.GetIterableProjection(binary.Right)),
            Facts(chained.Info.GetIterableProjection(chain.Operands[2])));
        Assert.Equal(
            twin.Info.TryGetMembershipNeedleBinding(binary.Left, out var twinBinding) ? twinBinding!.GetDisplayName() : null,
            chained.Info.TryGetMembershipNeedleBinding(chain.Operands[1], out var linkBinding) ? linkBinding!.GetDisplayName() : null);
        Assert.Equal(
            twin.Info.GetExpressionType(binary.Left)?.GetDisplayName(),
            chained.Info.GetExpressionType(chain.Operands[1])?.GetDisplayName());
        var link = chained.Info.GetComparisonChainLowering(chain)!.Links[1];
        Assert.Equal(twin.Info.GetOperatorLoweringForIr(binary)?.Kind ?? OperatorLoweringKind.Native, link.Kind);
        Assert.Equal(ComparisonLinkShape.Membership, link.Shape);
    }

    /// <summary>
    /// Positive control for <see cref="ConsecutiveInLink_IsItsBinaryTwin"/>'s projection comparison:
    /// the containers it compares carry a projection (null = null would pass vacuously), and the
    /// middle operand, a list, is still set up as link 0's container (no projection: a list has its
    /// own <c>Contains</c>) with link 0's needle bound under its slot.
    /// </summary>
    [Theory]
    [InlineData("t: tuple[list[int], list[int]] = ([1], [2])", "t", IterableProjectionKind.TupleToArray, "list[int32]")]
    [InlineData("bag: ListOf[int] = ListOf[int]([[1]])", "bag", IterableProjectionKind.Direct, "list[int32]")]
    public void ConsecutiveInLinks_SetUpBothContainers(
        string declaration, string container, IterableProjectionKind kind, string element)
    {
        var analysis = Analyze($"xs: list[int] = [1]; {declaration}", $"1 in xs in {container}");

        Assert.True(analysis.Errors.Count == 0, analysis.Describe());
        var chain = (ComparisonChain)TestedExpression(analysis);
        var projection = analysis.Info.GetIterableProjection(chain.Operands[2]);
        Assert.NotNull(projection);
        Assert.Equal(kind, projection!.Kind);
        Assert.Equal(element, projection.ElementType.GetDisplayName());
        Assert.Null(analysis.Info.GetIterableProjection(chain.Operands[1]));
        Assert.True(analysis.Info.TryGetMembershipNeedleBinding(chain.Operands[0], out var head));
        Assert.Equal("int32", head!.GetDisplayName());
        Assert.True(analysis.Info.TryGetMembershipNeedleBinding(chain.Operands[1], out var middle));
        Assert.Equal("list[int32]", middle!.GetDisplayName());
    }

    /// <summary>
    /// D3 ruling: a middle needle's capture type (<see cref="ComparisonLinkLowering.CaptureType"/>, on the
    /// membership link that binds it) is its binding only where that cannot change the link reading it
    /// first — the operand's own type or its nullable lift (a conditional with a <c>None</c> arm, typed by
    /// the slot alone). A reference up-cast or a numeric conversion records none: the capture is uncast.
    /// </summary>
    [Theory]
    [InlineData("a: int | None = 1; flag: bool = True; ys: list[int | None] = [1, None]",
        "a == (len(ys) if flag else None) in ys", "int32 | None")]
    [InlineData("a: int = 1; xs: list[int] = [1]", "a == len(xs) in xs", "int32")]
    [InlineData("s: str = \"ab\"; objs: list[object] = [s]", "s == s.upper() in objs", null)]
    [InlineData("n: long = 1; fs: list[float] = [1.0]", "n == abs(n) in fs", null)]
    public void MiddleNeedle_CapturesAtItsBinding_OnlyWhereItCannotChangeTheComparison(
        string declarations, string expression, string? captureType)
    {
        var analysis = Analyze(declarations, expression);

        Assert.True(analysis.CheckerErrors.Count == 0, analysis.Describe());
        var chain = (ComparisonChain)TestedExpression(analysis);
        var links = analysis.Info.GetComparisonChainLowering(chain)!.Links;
        Assert.Equal(ComparisonLinkShape.Membership, links[1].Shape);
        Assert.Null(links[0].CaptureType);
        Assert.Equal(captureType, links[1].CaptureType?.GetDisplayName());
    }

    // ---------------------------------------------------------------- D4: a chain head spelled like a type

    [Theory]
    [InlineData("c is Color.RED is d", ComparisonLinkShape.Identity)]
    [InlineData("c is Color.RED == d", ComparisonLinkShape.Comparison)]
    [InlineData("c is Color.RED is not d", ComparisonLinkShape.Identity)]
    public void EnumMemberHead_IsAnIdentityLink(string expression, ComparisonLinkShape secondShape)
    {
        var analysis = Analyze("c: Color = Color.RED; d: Color = Color.RED", expression);

        Assert.True(analysis.Errors.Count == 0 && analysis.CheckerDiagnostics.Count == 0, analysis.Describe());
        var chain = (ComparisonChain)TestedExpression(analysis);
        Assert.NotNull(chain.HeadCheckType);
        var links = analysis.Info.GetComparisonChainLowering(chain)!.Links;
        Assert.Equal(ComparisonLinkShape.Identity, links[0].Shape);
        Assert.Equal(OperatorLoweringKind.EnumIdentityEquals, links[0].Kind);
        Assert.Equal(secondShape, links[1].Shape);
        Assert.Equal("Color", analysis.Info.GetExpressionType(chain.Operands[1])!.GetDisplayName());
    }

    /// <summary>
    /// D4: a head whose dotted name is not an enum member is SPY0349 with the type-test steer — the
    /// binary type test's message verbatim — and its operand is recorded Unknown, unchecked, so the
    /// link after it draws nothing.
    /// </summary>
    [Theory]
    [InlineData("x is Outer.Inner is d")]
    [InlineData("x is Outer.Inner == d")]
    public void NonMemberHead_IsTheTypeTestRefusal(string expression)
    {
        var twin = Analyze("x: object = Dog(); d: object = Dog()", "x is Outer.Inner");
        var analysis = Analyze("x: object = Dog(); d: object = Dog()", expression);

        var expected = Assert.Single(twin.Errors);
        Assert.Equal(DiagnosticCodes.Semantic.IsTypeTestRetired, expected.Code);
        var error = Assert.Single(analysis.Errors);
        Assert.Equal(Positioned(expected), Positioned(error));
        Assert.Contains("Use 'isinstance(x, Outer.Inner)'", error.Message);

        var chain = (ComparisonChain)TestedExpression(analysis);
        Assert.IsType<UnknownType>(analysis.Info.GetExpressionType(chain.Operands[1]));
        Assert.Equal(OperatorLoweringKind.Native, analysis.Info.GetComparisonChainLowering(chain)!.Links[0].Kind);
    }

    /// <summary>D4 leaves a head with no dotted-name reading a type-test operand: still SPY0349.</summary>
    [Fact]
    public void UndottedTypeHead_StaysTheTypeTestRefusal()
    {
        var analysis = Analyze("x: object = Dog(); d: object = Dog()", "x is Dog is d");

        Assert.Contains(analysis.Errors, e => e.Code == DiagnosticCodes.Semantic.IsTypeTestRetired);
    }
}

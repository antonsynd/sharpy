using System.Text.RegularExpressions;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// The persistent form of P82 tranche 1's temporary agreement sweep (#2443, b67ca2ded). The sweep
/// compared the lowering pass's derived Optional store wraps against the checker's
/// <c>_optionalStoreWraps</c> table; that table is deleted, so the comparison cannot be rerun. What
/// can be checked forever is the property the derivation relies on.
///
/// <para><b>The property.</b> <c>LoweringPass</c> decides a payload store into a declared Optional
/// slot <c>S</c> by reading the recorded type of the store's TARGET (a plain store and an unpacking
/// element: <c>type(t)</c> is not Optional/Unknown; a walrus: <c>type(walrus)</c> is not
/// Optional/Unknown). The emitted store is right only if that agrees with the recorded type of the
/// stored VALUE, an independent checker fact. Over every fixture with an Optional-typed slot, and
/// the generated probes below, for every store into an identifier whose declared slot is an
/// <c>OptionalType</c>:
/// <list type="bullet">
/// <item>plain <c>t = v</c>, a tuple-literal unpacking element, and a walrus <c>(t := v)</c>: the
/// lowering wraps (<see cref="IrOptionalStoreWrap"/> / <see cref="IrOptionalWrappedElement"/> /
/// <see cref="IrOptionalWalrusStore"/>, <c>WrapAs == S</c>) <b>iff</b> <c>type(v)</c> is neither
/// <c>OptionalType</c> nor <c>None</c> (<c>VoidType</c>). A wrap over an Optional value would
/// double-wrap; a missing wrap over a payload value would leave a bare <c>T</c> in an
/// <c>Optional&lt;T&gt;</c> slot (CS0029 or the #1720 implicit operator). Both sides of the iff are
/// floored for each of the three.</item>
/// <item>an augmented store <c>t op= v</c> that is not a collection mutation: the lowering wraps
/// unconditionally, which is right only because the checker refuses an un-narrowed Optional target
/// (SPY0222), so <c>type(t)</c> is never <c>OptionalType</c> there. One-sided by construction.</item>
/// </list>
/// <c>??=</c> (D1 (d)) is not checked: its derivation already reads <c>type(v)</c> directly, so the
/// property would restate the code.</para>
///
/// <para><b>What is independent.</b> The slot <c>S</c> is read the way
/// <c>LoweringPass.DeclaredSlotOf</c> reads it (root binding's <c>SemanticBinding</c> type, else the
/// symbol's own type) — that half mirrors the derivation. The independent half is <c>type(v)</c>, the
/// value's recorded type, which the derivation never reads.</para>
///
/// <para><b>No silent skips.</b> A fixture whose compile reports an internal compiler error
/// (SPY0909) or throws FAILS the sweep, naming it: a regression in <c>Splice</c>,
/// <c>AssertMarksConsumed</c> or a required IR read must not shrink the count instead. Fixtures
/// with ordinary front-end errors (error fixtures) are not examined.</para>
///
/// <para>Categorised <c>GapDiscovery</c>: a standing corpus sweep rather than an edit-loop test (the
/// #1720 executing-corpus arm is the precedent).</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class OptionalStoreWrapAgreementTests
{
    private readonly ITestOutputHelper _output;

    public OptionalStoreWrapAgreementTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// A fixture declares an Optional-typed slot only by spelling a <c>T?</c> annotation (a name or
    /// closing bracket followed by <c>?</c>, not <c>??</c> or <c>?.</c>), <c>Optional[</c>, or
    /// <c>| None</c>.
    /// </summary>
    private static readonly Regex HasOptionalSlot = new(
        @"[\w\]]\?(?![?.])|Optional\[|\|\s*None\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Generated probes for the sides the corpus leaves thin: each derivation's wrap and no-wrap
    /// side, a value that is already Optional (no wrap) and a payload (wrap).
    /// </summary>
    private static readonly (string Name, string Source)[] Probes =
    {
        ("walrus-optional-value", """
            def opt_of(n: int) -> int?:
                if n > 0:
                    return Some(n)
                return None()

            def main() -> None:
                x: int? = Some(1)
                print((x := opt_of(3)) is not None)
            """),
        ("walrus-payload-value", """
            def main() -> None:
                x: int? = Some(1)
                if x is not None:
                    print((x := 4) + 1)
            """),
        ("element-optional-and-payload", """
            def opt_of(n: int) -> int?:
                if n > 0:
                    return Some(n)
                return None()

            def main() -> None:
                a: int? = Some(1)
                b: int? = Some(2)
                if a is not None and b is not None:
                    a, b = (opt_of(0), 5)
                    print(a is None, b)
            """),
        ("plain-optional-and-payload", """
            def opt_of(n: int) -> int?:
                if n > 0:
                    return Some(n)
                return None()

            def main() -> None:
                x: int? = Some(1)
                if x is not None:
                    x = 5
                    print(x)
                x = opt_of(2)
                print(x is not None)
            """),
    };

    [Fact]
    public void FixtureCorpus_DerivedWrapsAgreeWithTheStoredValueType()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsSkipped)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();
        Assert.True(fixtures.Count > 0, "No fixtures discovered — corpus is empty, guard is vacuous");

        var tally = new Tally();
        var disagreements = new List<string>();
        var internalErrors = new List<string>();
        int examined = 0, compiled = 0, units = 0, emptyIrContexts = 0;

        var refusedProbes = new List<string>();

        void Examine(string name, Func<LoweredCompilationCapture.Result> compile, bool isProbe = false)
        {
            examined++;
            LoweredCompilationCapture.Result result;
            try
            {
                result = compile();
            }
            catch (Exception ex)
            {
                internalErrors.Add($"{name}: compile threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            if (result.InternalCompilerErrors.Count > 0)
            {
                internalErrors.Add($"{name}: {string.Join(" | ", result.InternalCompilerErrors)}");
                return;
            }

            if (!result.FrontEndClean)
            {
                if (isProbe)
                {
                    refusedProbes.Add($"{name}: " + string.Join(" | ",
                        result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}")));
                }

                return;
            }

            compiled++;
            foreach (var (context, module) in result.Units)
            {
                units++;
                if (ReferenceEquals(context.Ir, IrCompilation.Empty))
                {
                    emptyIrContexts++;
                    continue;
                }

                CheckModule(name, module, context, tally, disagreements);
            }
        }

        foreach (var fixture in fixtures)
        {
            if (LoweredCompilationCapture.SourcesOf(fixture).Any(s => HasOptionalSlot.IsMatch(File.ReadAllText(s))))
                Examine(fixture.TestName, () => LoweredCompilationCapture.Compile(fixture));
        }

        var fixturesExamined = examined;
        foreach (var (name, source) in Probes)
            Examine("probe:" + name, () => LoweredCompilationCapture.CompileProgram(source), isProbe: true);

        _output.WriteLine(
            $"examined {fixturesExamined} fixture(s) + {Probes.Length} probe(s), compiled clean {compiled}, "
            + $"{units} emitter unit(s); {tally}");

        Assert.True(internalErrors.Count == 0,
            $"{internalErrors.Count} program(s) hit an internal compiler error (a lowering invariant or a "
            + "required IR read, #2443); the sweep does not skip them:\n" + string.Join("\n", internalErrors.Take(25)));

        Assert.True(refusedProbes.Count == 0,
            "every generated probe is a valid program, so a refused one checks nothing:\n" + string.Join("\n", refusedProbes));

        // The emitter's required IR reads (RequiredIrNodeFor) skip only over IrCompilation.Empty; no
        // pipeline context may be one, or that skip path is what ran.
        Assert.Equal(0, emptyIrContexts);

        // Non-vacuity: the prefilter still selects a corpus, and both sides of the iff were
        // exercised for each of the three derivations it covers.
        Assert.True(fixturesExamined > 0, "no fixture declares an Optional slot — the prefilter selects nothing");
        Assert.True(tally.PlainWrap > 0 && tally.PlainNoWrap > 0, $"plain stores need both sides: {tally}");
        Assert.True(tally.ElementWrap > 0 && tally.ElementNoWrap > 0, $"unpacking elements need both sides: {tally}");
        Assert.True(tally.WalrusWrap > 0 && tally.WalrusNoWrap > 0, $"walrus stores need both sides: {tally}");
        Assert.True(tally.Augmented > 0, $"no augmented store under an Optional slot was checked: {tally}");

        Assert.True(disagreements.Count == 0,
            $"{disagreements.Count} Optional store(s) where the lowering's wrap decision (read from the "
            + "target's recorded type) disagrees with the stored value's recorded type (#2443):\n"
            + string.Join("\n", disagreements.Take(50)));
    }

    private static void CheckModule(
        string fixture, Module module, CodeGenContext context, Tally tally, List<string> disagreements)
    {
        var info = context.SemanticInfo!;
        var index = context.Ir.Index;

        OptionalType? SlotOf(Symbol? symbol)
        {
            if (symbol is not VariableSymbol variable)
                return null;
            var root = info.GetRootBinding(variable);
            var bound = context.SemanticBinding.GetVariableType(root);
            return (bound != SemanticType.Unknown ? bound : root.Type) as OptionalType;
        }

        static bool IsOptionalLike(SemanticType? type) => type is OptionalType or VoidType;

        IrNode? IrOf(Node ast) => index.TryGetValue(ast, out var ir) ? ir : null;

        void Check(string cell, Node site, OptionalType slot, OptionalType? wrapAs, Expression value)
        {
            var valueType = info.GetExpressionType(value);
            var expectWrap = !IsOptionalLike(valueType);

            if ((wrapAs != null) != expectWrap || (wrapAs != null && wrapAs != slot))
            {
                disagreements.Add(
                    $"{fixture} {cell} @ {site.LineStart}:{site.ColumnStart}: slot={slot.GetDisplayName()}, "
                    + $"value type={valueType?.GetDisplayName()}, lowered wrap={wrapAs?.GetDisplayName() ?? "none"}");
            }

            tally.Count(cell, wrapAs != null);
        }

        foreach (var node in Walk(module))
        {
            switch (node)
            {
                case Assignment { Operator: AssignmentOperator.Assign, Target: Identifier target } plain
                    when SlotOf(info.GetIdentifierSymbol(target)) is { } slot:
                    Check(Tally.Plain, plain, slot, (IrOf(plain) as IrOptionalStoreWrap)?.WrapAs, plain.Value);
                    break;

                case Assignment { Operator: AssignmentOperator.Assign, Target: TupleLiteral targets } unpack:
                    CheckUnpacking(targets.Elements, unpack.Value);
                    break;

                case Assignment { Target: Identifier augTarget } augmented
                    when augmented.Operator is not (AssignmentOperator.Assign or AssignmentOperator.NullCoalesceAssign)
                         && SlotOf(info.GetIdentifierSymbol(augTarget)) is { } augSlot
                         && index.TryGetValue(augmented, out var augIr)
                         && augIr is not IrCollectionMutation:
                    tally.Augmented++;
                    if (info.GetExpressionType(augTarget) is OptionalType || augIr is not IrOptionalStoreWrap { WrapAs: var augWrap } || augWrap != augSlot)
                    {
                        disagreements.Add(
                            $"{fixture} augmented @ {augmented.LineStart}:{augmented.ColumnStart}: slot={augSlot.GetDisplayName()}, "
                            + $"target type={info.GetExpressionType(augTarget)?.GetDisplayName()}, lowered={augIr.GetType().Name}");
                    }
                    break;

                case WalrusExpression walrus when SlotOf(info.GetWalrusSymbol(walrus)) is { } walrusSlot:
                    // An unpacking element's wrap holds the walrus's own node as its single child.
                    var walrusIr = IrOf(walrus) is IrOptionalWrappedElement { Children: [IrNode inner] } ? inner : IrOf(walrus);
                    Check(Tally.Walrus, walrus, walrusSlot, (walrusIr as IrOptionalWalrusStore)?.WrapAs, walrus.Value);
                    break;
            }
        }

        // Mirrors the derivation's pairing (D1 (b)): spread-free tuple literal, equal arity, nested
        // tuple targets recurse.
        void CheckUnpacking(System.Collections.Immutable.ImmutableArray<Expression> targets, Expression value)
        {
            if (AstHelper.UnwrapParenthesized(value) is not TupleLiteral literal
                || literal.Elements.Any(e => e is SpreadElement)
                || targets.Any(t => t is StarExpression)
                || literal.Elements.Length != targets.Length)
            {
                return;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                var element = literal.Elements[i];
                switch (targets[i])
                {
                    case Identifier target when SlotOf(info.GetIdentifierSymbol(target)) is { } slot:
                        Check(Tally.Element, element, slot, (IrOf(element) as IrOptionalWrappedElement)?.WrapAs, element);
                        break;
                    case TupleLiteral nested:
                        CheckUnpacking(nested.Elements, element);
                        break;
                }
            }
        }
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        foreach (var child in root.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Walk(child))
                yield return descendant;
        }
    }

    private sealed class Tally
    {
        public const string Plain = "plain";
        public const string Element = "element";
        public const string Walrus = "walrus";

        public int PlainWrap, PlainNoWrap, ElementWrap, ElementNoWrap, WalrusWrap, WalrusNoWrap, Augmented;

        public void Count(string cell, bool wrapped)
        {
            switch (cell)
            {
                case Plain when wrapped:
                    PlainWrap++;
                    break;
                case Plain:
                    PlainNoWrap++;
                    break;
                case Element when wrapped:
                    ElementWrap++;
                    break;
                case Element:
                    ElementNoWrap++;
                    break;
                case Walrus when wrapped:
                    WalrusWrap++;
                    break;
                case Walrus:
                    WalrusNoWrap++;
                    break;
            }
        }

        public override string ToString() =>
            $"plain wrap/no-wrap {PlainWrap}/{PlainNoWrap}, element {ElementWrap}/{ElementNoWrap}, "
            + $"walrus {WalrusWrap}/{WalrusNoWrap}, augmented {Augmented}";
    }
}

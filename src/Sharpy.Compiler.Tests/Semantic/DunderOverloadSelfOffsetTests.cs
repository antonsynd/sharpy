using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1395: the operator-dunder path's self-skip was keyed on the leading parameter being literally
/// named <c>self</c>. CLR-discovered operators reflect as <c>left</c>/<c>right</c> (or <c>a</c>/
/// <c>b</c>), so every CLR operator candidate was dropped by the arity filter — with
/// <c>TotalArgCount: 1</c> and an offset of 0 the filter asks <c>1 &gt;= 2</c> — and multi-candidate
/// resolution never ran. The cells still worked, because <c>TryInferClrBinaryOp</c> caught them
/// underneath, which is what made the defect latent.
/// </summary>
/// <remarks>
/// These are unit assertions on <see cref="TypeChecker.ResolveDunderOverload"/> rather than
/// <c>.spy</c> fixtures, and that is deliberate: the resolver path and the fallback path emit
/// identical output by construction, so NO fixture can distinguish them. A corpus that stays green
/// is evidence of nothing here. The discriminating observation is which path answered, and the only
/// place that is observable is the resolver's own return value.
/// </remarks>
public class DunderOverloadSelfOffsetTests
{
    private static TypeChecker NewChecker()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new TypeChecker(symbolTable, semanticInfo, typeResolver);
    }

    private static FunctionSymbol Dunder(string name, params (string Name, SemanticType Type)[] parameters) =>
        new()
        {
            Name = name,
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Int,
            Parameters = parameters
                .Select(p => new ParameterSymbol { Name = p.Name, Type = p.Type })
                .ToList()
        };

    /// <summary>
    /// The cell #1395 is about. Two CLR-shaped candidates, receiver named <c>left</c>: before the
    /// fix this returns null and every caller falls through to the CLR fallback.
    /// </summary>
    [Fact]
    public void ClrShapedOperands_ResolveThroughTheResolver_NotTheFallback()
    {
        var candidates = new[]
        {
            Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Int)),
            Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Str))
        };

        var match = NewChecker().ResolveDunderOverload(candidates, SemanticType.Str);

        match.Should().NotBeNull("a CLR operator's receiver is its FIRST parameter regardless of "
            + "its reflected name, so the single argument must be matched against the one after it");
        match!.Parameters[1].Type.Should().Be(SemanticType.Str);
    }

    /// <summary>Control: the name-based path must keep working — this passes before the fix too.</summary>
    [Fact]
    public void SharpyShapedOperands_StillResolve()
    {
        var candidates = new[]
        {
            Dunder("__or__", (PythonNames.Self, SemanticType.Int), ("other", SemanticType.Int)),
            Dunder("__or__", (PythonNames.Self, SemanticType.Int), ("other", SemanticType.Str))
        };

        var match = NewChecker().ResolveDunderOverload(candidates, SemanticType.Str);

        match.Should().NotBeNull();
        match!.Parameters[1].Type.Should().Be(SemanticType.Str);
    }

    /// <summary>
    /// The guard against fixing this too broadly. A CLR dunder that carries ONLY the operand — an
    /// indexer reflected as <c>this[int index]</c>, reached through the same
    /// <c>__getitem__</c> path — has no receiver parameter to skip. A blanket "skip the leading
    /// parameter whenever SkipSelfParam is set" would consume the operand and match nothing.
    /// </summary>
    [Fact]
    public void SingleOperandClrDunder_HasNoReceiverToSkip()
    {
        var candidates = new[] { Dunder("__getitem__", ("index", SemanticType.Int)) };

        var match = NewChecker().ResolveDunderOverload(candidates, SemanticType.Int);

        match.Should().NotBeNull("the sole parameter IS the operand; skipping it would match nothing");
        match!.Parameters[0].Name.Should().Be("index");
    }

    /// <summary>
    /// The offset is read in TWO places — the arity filter and the betterness comparison — and the
    /// tests above only reach the first, because they leave exactly one type-matching candidate.
    /// This cell forces the specificity tiebreak: both candidates accept an <c>int</c> operand, so
    /// resolution falls through exact-arity and type-parameter tiebreaks into
    /// <c>IsMoreSpecificOverload</c>.
    /// </summary>
    /// <remarks>
    /// With only the arity copy corrected, this returns null rather than the <c>int</c> overload:
    /// the betterness comparison reads <c>Parameters[0 + 0]</c>, which is the RECEIVER on both
    /// candidates, so the two look identical, neither is strictly better, and the result is
    /// reported ambiguous. That is the parallel-site failure the plan warns about, and it is
    /// invisible to every other cell here.
    /// </remarks>
    [Fact]
    public void SpecificityTiebreak_UsesTheSameOffsetAsTheArityFilter()
    {
        var intOverload = Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Int));
        var floatOverload = Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Float));

        var match = NewChecker().ResolveDunderOverload(
            new[] { floatOverload, intOverload }, SemanticType.Int);

        match.Should().BeSameAs(intOverload,
            "int is assignable to float but not the reverse, so the int overload is strictly more "
            + "specific — a comparison that only happens if betterness skips the receiver too");
    }

    /// <summary>
    /// The agreement the issue asks for: the multi-candidate resolver picks the same overload the
    /// single-candidate path would. <c>AcceptsArgument</c> locates the operand as
    /// <c>Parameters[^1]</c> — by SHAPE, never by name — so with a shape-based offset the two paths
    /// agree by construction rather than by coincidence.
    /// </summary>
    [Fact]
    public void MultiCandidateResolution_AgreesWithTheSingleCandidateOperandRule()
    {
        var intOverload = Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Int));
        var strOverload = Dunder("__or__", ("left", SemanticType.Int), ("right", SemanticType.Str));
        var checker = NewChecker();

        foreach (var (argType, expected) in new[]
        {
            (SemanticType.Int, intOverload),
            (SemanticType.Str, strOverload)
        })
        {
            var match = checker.ResolveDunderOverload(new[] { intOverload, strOverload }, argType);

            match.Should().BeSameAs(expected,
                "the operand is Parameters[^1] on both paths, so both must choose the same overload");
        }
    }

    // A synthesized generic definition (a stand-in for a CLR-discovered generic collection) whose
    // operator parameters reference its own type parameter T.
    private static (TypeSymbol Definition, GenericType Instantiation) GenericBox(SemanticType typeArgument)
    {
        var definition = new TypeSymbol
        {
            Name = "Box",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            TypeParameters = new List<TypeParameterDef> { new() { Name = "T" } }
        };
        var instantiation = new GenericType
        {
            Name = "Box",
            GenericDefinition = definition,
            TypeArguments = new List<SemanticType> { typeArgument }
        };
        return (definition, instantiation);
    }

    /// <summary>
    /// #1395 Task 2 — the SUBSTITUTION, which the offset cells above cannot reach because their
    /// operands are closed already. A generic operator's parameter is the definition's open <c>T</c>;
    /// resolving it at the receiver's arguments is what lets it be judged as the closed type it
    /// actually is. Here the receiver is <c>Box[int]</c> and two overloads accept the operand at
    /// <c>T</c> (closes to <c>int</c>, an EXACT match) and at <c>float</c> (an int→float widening).
    /// With the receiver substitution the exact <c>int</c> overload is strictly more specific and
    /// wins; WITHOUT it, <c>T</c> stays an open wildcard that betterness ranks BELOW the structured
    /// <c>float</c>, so the wrong overload is chosen — the discriminating observation.
    /// </summary>
    [Fact]
    public void GenericReceiver_ClosesTheOperandAtReceiverArguments_BeforeBetterness()
    {
        var (_, boxInt) = GenericBox(SemanticType.Int);
        var tParam = new TypeParameterType { Name = "T" };

        var tOverload = Dunder("__or__", ("left", boxInt), ("right", tParam));
        var floatOverload = Dunder("__or__", ("left", boxInt), ("right", SemanticType.Float));

        var match = NewChecker().ResolveDunderOverload(
            new[] { floatOverload, tOverload }, SemanticType.Int, boxInt);

        match.Should().BeSameAs(tOverload,
            "the receiver Box[int] closes T to int — an exact match strictly better than the "
            + "int→float widening — which only happens when the operand is substituted before "
            + "betterness runs");
    }

    /// <summary>
    /// The single/multi-candidate AGREEMENT with a generic receiver: the shape-based offset and the
    /// receiver substitution together make the multi-candidate resolver choose the overload the
    /// single-candidate operand rule (AcceptsArgument) would — the guard #1395 asks for. A str
    /// operand against Box[int] must select the str overload on BOTH the single and the multi path.
    /// </summary>
    [Fact]
    public void GenericReceiver_MultiAndSingleCandidate_SelectTheSameOverload()
    {
        var (_, boxInt) = GenericBox(SemanticType.Int);
        var tParam = new TypeParameterType { Name = "T" };

        var tOverload = Dunder("__or__", ("left", boxInt), ("right", tParam));
        var strOverload = Dunder("__or__", ("left", boxInt), ("right", SemanticType.Str));
        var checker = NewChecker();

        // Multi-candidate: T closes to int, so a str operand is refused there and matched to str.
        var multi = checker.ResolveDunderOverload(new[] { tOverload, strOverload }, SemanticType.Str, boxInt);
        // Single-candidate: the str overload alone must also accept the str operand.
        var single = checker.ResolveDunderOverload(new[] { strOverload }, SemanticType.Str, boxInt);

        multi.Should().BeSameAs(strOverload);
        single.Should().BeSameAs(strOverload);
        multi.Should().BeSameAs(single, "the operand rule is identical on both paths (#1395)");
    }
}

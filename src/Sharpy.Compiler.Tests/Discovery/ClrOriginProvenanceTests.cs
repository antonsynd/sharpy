extern alias SharpyRT;
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Conformance;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Provenance on bridge-mapped generics (#1260, #1252).
///
/// <para>
/// <see cref="ClrTypeBridge"/> collapses several CLR types onto one Sharpy spelling —
/// <c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c> and
/// <c>IEnumerable&lt;T&gt;</c> all become <c>list[T]</c>. The collapse is deliberate, but it left
/// assignability unable to tell a CLR formal from a Sharpy-native one, so calls .NET binds were
/// refused. <see cref="GenericType.ClrOriginTypeName"/> records which CLR definition a mapped type
/// came from; these tests pin that the stamp is applied to EVERY collapsing arm rather than the one
/// arm an issue happened to name, and that it survives the symbol cache.
/// </para>
/// </summary>
public class ClrOriginProvenanceTests
{
    private readonly ClrTypeBridge _bridge = new();

    private static string Definition(Type closedGeneric)
        => closedGeneric.GetGenericTypeDefinition().FullName!;

    /// <summary>
    /// Every arm that collapses a CLR generic onto a Sharpy collection name, with the name it must
    /// collapse to. Stamping one and leaving its siblings bare is the parallel-site defect class this
    /// batch exists to close, so the arms are enumerated rather than sampled.
    ///
    /// <para>
    /// The expected Sharpy name has to be written by hand — deriving it from the bridge would make
    /// the theory assert the mapping against itself. Completeness is what gets derived instead:
    /// <see cref="CollapsingArms_EnumeratesEveryArmThatCollapsesOntoACollectionName"/> probes the
    /// bridge and fails if this list has fallen behind. It had: <c>ICollection&lt;T&gt;</c> (#1295),
    /// <c>Sharpy.FrozenDict&lt;K,V&gt;</c> (#1310) and <c>IOrderedEnumerable&lt;T&gt;</c> (#1332) were
    /// added to the bridge while the comment above still claimed the list was exhaustive.
    /// </para>
    ///
    /// <para>
    /// <c>IOrderedEnumerable&lt;T&gt;</c> left again (#1390): it is still a stamped arm, but it no
    /// longer collapses onto a collection name — it maps to itself, so a slotted <c>then_by</c> keeps
    /// a receiver <c>ThenBy</c> accepts. It is subtracted from the completeness floor below rather
    /// than removed from it, so the probe still has to find it.
    /// </para>
    ///
    /// <para>
    /// Concrete <c>List&lt;T&gt;</c>/<c>Dictionary&lt;K,V&gt;</c>/<c>HashSet&lt;T&gt;</c> left the
    /// same way under honest borders (#1517) — they keep honest identities instead of collapsing —
    /// and moved to <see cref="HonestConcreteArms"/> below, joining
    /// <c>ClrBridgeArmProbe.RequiredNonCollectionArms</c> so the completeness floor stays honest.
    /// </para>
    /// </summary>
    private static readonly (Type ClrType, string SharpyName)[] Arms =
    {
        (typeof(IList<int>), BuiltinNames.List),
        (typeof(ICollection<int>), BuiltinNames.List),
        (typeof(IReadOnlyList<int>), BuiltinNames.List),
        (typeof(IReadOnlyCollection<int>), BuiltinNames.List),
        (typeof(IEnumerable<int>), BuiltinNames.List),
        (typeof(IDictionary<string, int>), BuiltinNames.Dict),
        (typeof(IReadOnlyDictionary<string, int>), BuiltinNames.Dict),
        (typeof(ISet<int>), BuiltinNames.Set),
        (typeof(SharpyRT::Sharpy.List<int>), BuiltinNames.List),
        (typeof(SharpyRT::Sharpy.Dict<string, int>), BuiltinNames.Dict),
        (typeof(SharpyRT::Sharpy.Set<int>), BuiltinNames.Set),
        (typeof(SharpyRT::Sharpy.FrozenSet<int>), BuiltinNames.FrozenSet),
        (typeof(SharpyRT::Sharpy.FrozenDict<string, int>), BuiltinNames.FrozenDict),
    };

    /// <summary>
    /// The concrete BCL collections left the collapsing list under honest borders (#1517): the bridge
    /// maps them to their own StripArity names with a live <see cref="GenericType.GenericDefinition"/>,
    /// the <c>IOrderedEnumerable&lt;T&gt;</c> shape (#1390). Their provenance stamp — asserted by
    /// <see cref="EveryHonestConcreteArm_KeepsItsClrIdentity"/> — must survive that move, or the
    /// assignability rule that reads the stamp loses exactly these types.
    /// </summary>
    private static readonly (Type ClrType, string HonestName)[] HonestConcreteArms =
    {
        (typeof(List<int>), "List"),
        (typeof(Dictionary<string, int>), "Dictionary"),
        (typeof(HashSet<int>), "HashSet"),
    };

    public static TheoryData<Type, string> CollapsingArms()
    {
        var data = new TheoryData<Type, string>();
        foreach (var (clrType, sharpyName) in Arms)
        {
            data.Add(clrType, sharpyName);
        }

        return data;
    }

    /// <summary>
    /// The enumeration above cannot rot again: the bridge is probed for every arm that collapses a
    /// CLR generic onto a Sharpy collection name, and an arm missing from <see cref="Arms"/> fails
    /// here rather than being silently un-swept.
    /// </summary>
    [Fact]
    public void CollapsingArms_EnumeratesEveryArmThatCollapsesOntoACollectionName()
    {
        var collectionNames = new HashSet<string>(StringComparer.Ordinal)
        {
            BuiltinNames.List,
            BuiltinNames.Dict,
            BuiltinNames.Set,
            BuiltinNames.FrozenSet,
            BuiltinNames.FrozenDict
        };

        var probed = ClrBridgeArmProbe.DiscoverCollapsingArms(_bridge)
            .Where(a => collectionNames.Contains(a.Mapped.Name))
            .Select(a => a.Closed.GetGenericTypeDefinition().FullName!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Anti-vacuity control: a probe that found nothing would make the comparison below pass with
        // an empty list, which is how a completeness check turns into decoration. The floor is the
        // stamped-arm list MINUS the arms that stamp provenance without collapsing onto a collection
        // name — those are real arms (and the unfiltered sweeps still require them), they just cannot
        // appear in a list filtered to collection names.
        probed.Should().Contain(
            ClrBridgeArmProbe.RequiredArms.Except(
                ClrBridgeArmProbe.RequiredNonCollectionArms, StringComparer.Ordinal),
            "the probe must rediscover the arms we already know exist before its silence about others "
            + "means anything");

        var enumerated = Arms
            .Select(a => a.ClrType.GetGenericTypeDefinition().FullName!)
            .ToHashSet(StringComparer.Ordinal);

        probed.Where(p => !enumerated.Contains(p)).OrderBy(p => p, StringComparer.Ordinal)
            .Should().BeEmpty(
                "every CLR generic the bridge collapses onto a collection name must appear in `Arms`, "
                + "or its provenance stamp is never asserted — the parallel-site gap that let three "
                + "arms be added without joining this sweep.\nMissing from Arms:");
    }

    [Theory]
    [MemberData(nameof(CollapsingArms))]
    public void EveryCollapsingArm_StampsItsClrOrigin(Type clrType, string expectedSharpyName)
    {
        var mapped = _bridge.MapClrTypeToSemanticType(clrType);

        var generic = mapped.Should().BeOfType<GenericType>().Subject;
        generic.Name.Should().Be(expectedSharpyName);
        generic.ClrOriginTypeName.Should().Be(
            Definition(clrType),
            "a mapped formal must remember the CLR definition it came from, or assignability cannot "
            + "tell it apart from a list[T] the user wrote");
    }

    public static TheoryData<Type, string> HonestArms()
    {
        var data = new TheoryData<Type, string>();
        foreach (var (clrType, honestName) in HonestConcreteArms)
        {
            data.Add(clrType, honestName);
        }

        return data;
    }

    /// <summary>
    /// The honest half of #1517: a concrete BCL collection keeps its own name, carries a live
    /// definition symbol, and still stamps provenance. The stamp assertion moved here from
    /// <see cref="EveryCollapsingArm_StampsItsClrOrigin"/> when these types stopped collapsing —
    /// losing it would revive the exact class #1294 closed (a warm-cache formal silently stops
    /// matching the assignability rule that reads the stamp).
    /// </summary>
    [Theory]
    [MemberData(nameof(HonestArms))]
    public void EveryHonestConcreteArm_KeepsItsClrIdentity(Type clrType, string expectedHonestName)
    {
        var mapped = _bridge.MapClrTypeToSemanticType(clrType);

        var generic = mapped.Should().BeOfType<GenericType>().Subject;
        generic.Name.Should().Be(
            expectedHonestName,
            "a concrete CLR collection keeps its honest, distinct identity under #1517");
        generic.GenericDefinition.Should().NotBeNull(
            "the honest shape carries the CLR definition symbol (the IOrderedEnumerable precedent), "
            + "or member resolution has no surface to reflect");
        generic.ClrOriginTypeName.Should().Be(
            Definition(clrType),
            "an honest arm still stamps provenance — leaving the collapsing list must not cost the "
            + "stamp that assignability and the warm cache read");
    }

    [Fact]
    public void SharpyWrittenGeneric_HasNoOrigin()
    {
        // The scope guard in type form: nothing but the bridge sets provenance, so a type constructed
        // the way the TypeResolver constructs one from source stays strict.
        var written = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        written.ClrOriginTypeName.Should().BeNull();
    }

    [Fact]
    public void Provenance_IsNotPartOfTypeIdentity()
    {
        // Deliberate: a mapped list[int] and a written list[int] are the SAME Sharpy type. If equality
        // took provenance into account, every cache and substitution keyed on SemanticType would split
        // into two populations. Assignability reads the field; identity must not.
        var mapped = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { SemanticType.Int },
            ClrOriginTypeName = Definition(typeof(IEnumerable<int>))
        };
        var written = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        mapped.Should().Be(written);
        mapped.GetHashCode().Should().Be(written.GetHashCode());
    }

    [Theory]
    // A CLR List<int> satisfies an IEnumerable<int>-origin formal — the #1260 shape.
    [InlineData(typeof(List<int>), true)]
    // So does an array, for free: string[]/int[] implement IEnumerable<T>.
    [InlineData(typeof(int[]), true)]
    // And the origin itself.
    [InlineData(typeof(IEnumerable<int>), true)]
    // A sequence of the WRONG element type does not.
    [InlineData(typeof(List<string>), false)]
    // Nor does something that is not a sequence at all.
    [InlineData(typeof(int), false)]
    public void ClrOriginIsSatisfiedBy_AnswersWhatDotNetBinds(Type sourceClr, bool expected)
    {
        var formal = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { SemanticType.Int },
            ClrOriginTypeName = Definition(typeof(IEnumerable<int>))
        };

        ClrTypeHelper.ClrOriginIsSatisfiedBy(formal, sourceClr, ResolveClr).Should().Be(expected);
    }

    [Fact]
    public void ClrOriginIsSatisfiedBy_RefusesAFormalWithNoProvenance()
    {
        // The guard that keeps `def take(xs: list[int])` strict. Without it the widening would silently
        // introduce a copy at every native collection parameter.
        var written = new GenericType
        {
            Name = BuiltinNames.List,
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };

        ClrTypeHelper.ClrOriginIsSatisfiedBy(written, typeof(List<int>), ResolveClr).Should().BeFalse();
    }

    [Fact]
    public void Provenance_SurvivesTheSymbolCache()
    {
        // A warm incremental build reads formals from the cache without re-running the bridge. A formal
        // that lost its origin on the way through would revert to the strict comparison and start
        // declining again with no diagnostic — which is why CurrentSchemaVersion is bumped alongside.
        var mapped = (GenericType)_bridge.MapClrTypeToSemanticType(typeof(IEnumerable<int>));
        var symbol = new VariableSymbol
        {
            Name = "xs",
            Kind = SymbolKind.Variable,
            Type = mapped
        };

        var restored = (VariableSymbol)SymbolSerializer.Deserialize(
            SymbolSerializer.Serialize(symbol, "test.spy"),
            new Dictionary<string, Symbol>());

        var restoredGeneric = restored.Type.Should().BeOfType<GenericType>().Subject;
        restoredGeneric.Name.Should().Be(BuiltinNames.List);
        restoredGeneric.ClrOriginTypeName.Should().Be(mapped.ClrOriginTypeName);
        restoredGeneric.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void HonestConcreteShape_SurvivesTheSymbolCache()
    {
        // #1517's honest shapes are a wire-format change (schema v28): a cache written before the
        // split holds `set[int]` where a cold build now says `HashSet[int]`. The plan asked for
        // warm/cold RECORD equality here; measuring it showed that is structurally unattainable —
        // GenericType.Equals deliberately includes GenericDefinition, a reference-compared
        // TypeSymbol the serializer does not (and cannot usefully) round-trip. What the cache
        // actually guarantees, pinned here: Name, ClrOriginTypeName, and TypeArguments survive;
        // GenericDefinition is null on restore and consumers re-resolve it lazily from the origin
        // (#1496). Consumers that key on GenericDefinition without that fallback diverge warm from
        // cold — #1568 tracks them (the #1533 gate is one).
        var mapped = (GenericType)_bridge.MapClrTypeToSemanticType(typeof(HashSet<int>));
        var symbol = new VariableSymbol
        {
            Name = "hs",
            Kind = SymbolKind.Variable,
            Type = mapped
        };

        var restored = (VariableSymbol)SymbolSerializer.Deserialize(
            SymbolSerializer.Serialize(symbol, "test.spy"),
            new Dictionary<string, Symbol>());

        var restoredGeneric = restored.Type.Should().BeOfType<GenericType>().Subject;
        restoredGeneric.Name.Should().Be("HashSet", "a stale-cache decode of `set` here is exactly "
            + "the warm≠cold disease the v28 bump discards");
        restoredGeneric.ClrOriginTypeName.Should().Be("System.Collections.Generic.HashSet`1");
        restoredGeneric.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
        restoredGeneric.GenericDefinition.Should().BeNull(
            "the definition symbol is per-analysis state, not cache content — if this starts "
            + "round-tripping, #1568's consumers can key on it again and this pin should flip");
    }

    private static Type? ResolveClr(SemanticType type) => type switch
    {
        BuiltinType bt => bt.ClrType,
        _ => null
    };
}

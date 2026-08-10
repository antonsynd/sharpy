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
    /// </summary>
    private static readonly (Type ClrType, string SharpyName)[] Arms =
    {
        (typeof(List<int>), BuiltinNames.List),
        (typeof(IList<int>), BuiltinNames.List),
        (typeof(ICollection<int>), BuiltinNames.List),
        (typeof(IReadOnlyList<int>), BuiltinNames.List),
        (typeof(IReadOnlyCollection<int>), BuiltinNames.List),
        (typeof(IOrderedEnumerable<int>), BuiltinNames.List),
        (typeof(IEnumerable<int>), BuiltinNames.List),
        (typeof(Dictionary<string, int>), BuiltinNames.Dict),
        (typeof(IDictionary<string, int>), BuiltinNames.Dict),
        (typeof(IReadOnlyDictionary<string, int>), BuiltinNames.Dict),
        (typeof(HashSet<int>), BuiltinNames.Set),
        (typeof(ISet<int>), BuiltinNames.Set),
        (typeof(SharpyRT::Sharpy.List<int>), BuiltinNames.List),
        (typeof(SharpyRT::Sharpy.Dict<string, int>), BuiltinNames.Dict),
        (typeof(SharpyRT::Sharpy.Set<int>), BuiltinNames.Set),
        (typeof(SharpyRT::Sharpy.FrozenSet<int>), BuiltinNames.FrozenSet),
        (typeof(SharpyRT::Sharpy.FrozenDict<string, int>), BuiltinNames.FrozenDict),
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
        // an empty list, which is how a completeness check turns into decoration.
        probed.Should().Contain(
            ClrBridgeArmProbe.RequiredArms,
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

    private static Type? ResolveClr(SemanticType type) => type switch
    {
        BuiltinType bt => bt.ClrType,
        _ => null
    };
}

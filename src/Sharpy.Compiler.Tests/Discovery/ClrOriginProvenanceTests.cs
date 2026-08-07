extern alias SharpyRT;
using System;
using System.Collections.Generic;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
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
    /// Every arm that collapses a CLR generic onto a Sharpy collection name. Stamping one and leaving
    /// its siblings bare is the parallel-site defect class this batch exists to close, so the arms are
    /// enumerated here rather than sampled.
    /// </summary>
    public static TheoryData<Type, string> CollapsingArms() => new()
    {
        { typeof(List<int>), BuiltinNames.List },
        { typeof(IList<int>), BuiltinNames.List },
        { typeof(IReadOnlyList<int>), BuiltinNames.List },
        { typeof(IReadOnlyCollection<int>), BuiltinNames.List },
        { typeof(IEnumerable<int>), BuiltinNames.List },
        { typeof(Dictionary<string, int>), BuiltinNames.Dict },
        { typeof(IDictionary<string, int>), BuiltinNames.Dict },
        { typeof(IReadOnlyDictionary<string, int>), BuiltinNames.Dict },
        { typeof(HashSet<int>), BuiltinNames.Set },
        { typeof(ISet<int>), BuiltinNames.Set },
        { typeof(SharpyRT::Sharpy.List<int>), BuiltinNames.List },
        { typeof(SharpyRT::Sharpy.Dict<string, int>), BuiltinNames.Dict },
        { typeof(SharpyRT::Sharpy.Set<int>), BuiltinNames.Set },
    };

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

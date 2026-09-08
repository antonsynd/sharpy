using System.Collections.Immutable;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

using FunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for the type walkers over the 20 <see cref="SemanticType"/> leaves (#1797's
/// latent class, plan-499995 Phase 3 task 4). <see cref="TypeSubstitution.Apply"/> must DESCEND
/// every leaf that can carry a type parameter — a parameter that survives substitution inside a
/// wrapper leaks into the emitted C# as a bare <c>T</c> (CS0246) — and pass every childless leaf
/// through unchanged. Both rosters are literal lists whose union is compared to the reflection
/// census, so a 21st leaf fails here until it is placed. Each descending leaf is exercised by an
/// instance wrapping <c>T</c>: after <c>T ↦ int</c> its canonical key must name <c>int</c> and no
/// longer mention <c>T</c> — removing that leaf's arm from <c>Apply</c> turns exactly its row red.
/// </summary>
public class TypeWalkerTotalityTests
{
    private const int ExpectedLeafCount = 20;

    /// <summary>Leaves that can contain another type and therefore a type parameter.</summary>
    private static readonly string[] DescendingLeaves =
    {
        nameof(GenericType), nameof(OptionalType), nameof(NullableType), nameof(ResultType),
        nameof(FunctionType), nameof(TupleType), nameof(UnionType), nameof(TaskType),
    };

    /// <summary>
    /// Childless leaves, plus the two that carry a type parameter by REFERENCE rather than as a
    /// child: <see cref="TypeParameterType"/> is the subject substitution replaces, and
    /// <see cref="GenericFunctionType"/> holds WRITTEN arguments that are already closed.
    /// </summary>
    private static readonly string[] PassThroughLeaves =
    {
        nameof(UnknownType), nameof(VoidType), nameof(BuiltinType), nameof(UserDefinedType),
        nameof(ModuleType), nameof(TypeParameterType), nameof(SelfType), nameof(GenericFunctionType),
        nameof(ConstructorReferenceType), nameof(TemplateType), nameof(LiteralStringType),
        nameof(UnmappedClrType),
    };

    private static readonly TypeParameterType T = new() { Name = "T" };
    private static readonly IReadOnlyDictionary<string, SemanticType> ToInt =
        new Dictionary<string, SemanticType>(StringComparer.Ordinal) { ["T"] = SemanticType.Int };

    [Fact]
    public void Rosters_PartitionTheCensus_AnchoredToLiteral20()
    {
        var census = typeof(SemanticType).Assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && t.IsSubclassOf(typeof(SemanticType)))
            .Select(t => t.Name).OrderBy(n => n).ToList();

        census.Count.Should().Be(ExpectedLeafCount);
        (DescendingLeaves.Length + PassThroughLeaves.Length).Should().Be(ExpectedLeafCount);
        DescendingLeaves.Intersect(PassThroughLeaves).Should().BeEmpty();
        DescendingLeaves.Concat(PassThroughLeaves).OrderBy(n => n).Should().Equal(census);
    }

    public static IEnumerable<object[]> WrappedParameters()
    {
        yield return new object[] { nameof(GenericType), new GenericType { Name = "list", TypeArguments = new List<SemanticType> { T } } };
        yield return new object[] { nameof(OptionalType), new OptionalType { UnderlyingType = T } };
        yield return new object[] { nameof(NullableType), new NullableType { UnderlyingType = new GenericType { Name = "list", TypeArguments = new List<SemanticType> { T } } } };
        yield return new object[] { nameof(ResultType), new ResultType { OkType = T, ErrorType = SemanticType.Str } };
        yield return new object[] { nameof(FunctionType), new FunctionType { ParameterTypes = new List<SemanticType> { T }, ReturnType = T } };
        yield return new object[] { nameof(TupleType), new TupleType { ElementTypes = new List<SemanticType> { SemanticType.Int, T } } };
        yield return new object[] { nameof(UnionType), new UnionType { Name = "U", CaseTypes = new List<SemanticType> { T, SemanticType.Str } } };
        yield return new object[] { nameof(TaskType), new TaskType { ResultType = T } };
    }

    [Theory]
    [MemberData(nameof(WrappedParameters))]
    public void Apply_Descends_EveryWrapperLeaf(string leafName, SemanticType wrapped)
    {
        DescendingLeaves.Should().Contain(leafName);
        wrapped.CanonicalKey.Should().Contain("T", "the instance wraps the parameter");

        var closed = TypeSubstitution.Apply(wrapped, ToInt);

        closed.GetType().Name.Should().Be(leafName, "substitution preserves the wrapper");
        closed.CanonicalKey.Should().Contain("int");
        MentionsBareT(closed).Should().BeFalse($"{leafName}: T must not survive substitution (got {closed.CanonicalKey})");
    }

    [Fact]
    public void EveryDescendingLeaf_HasAWrappedInstance()
    {
        var covered = WrappedParameters().Select(row => (string)row[0]).ToHashSet();
        DescendingLeaves.Except(covered).Should().BeEmpty();
    }

    [Fact]
    public void NullableOverAValueTypeParameter_CollapsesToTheValueType()
    {
        // `T | None` written on a type parameter is C#'s unconstrained `T?`: plain `T` for a value
        // instantiation, nullable for a reference one (plan-499995 Design Decision 3, Axiom 1).
        TypeSubstitution.Apply(new NullableType { UnderlyingType = T }, ToInt)
            .Should().Be(SemanticType.Int);
        TypeSubstitution.Apply(new NullableType { UnderlyingType = T },
                new Dictionary<string, SemanticType> { ["T"] = SemanticType.Str })
            .Should().BeOfType<NullableType>()
            .Which.UnderlyingType.Should().Be(SemanticType.Str);
    }

    public static IEnumerable<object[]> PassThroughInstances()
    {
        yield return new object[] { nameof(UnknownType), SemanticType.Unknown };
        yield return new object[] { nameof(VoidType), SemanticType.Void };
        yield return new object[] { nameof(BuiltinType), SemanticType.Int };
        yield return new object[] { nameof(UserDefinedType), new UserDefinedType { Name = "P", Symbol = new TypeSymbol { Name = "P", DefiningModule = "m" } } };
        yield return new object[] { nameof(ModuleType), new ModuleType { Symbol = new ModuleSymbol { Name = "m" } } };
        yield return new object[] { nameof(TypeParameterType), new TypeParameterType { Name = "U" } };
        yield return new object[] { nameof(SelfType), new SelfType { DeclaringType = new TypeSymbol { Name = "C", DefiningModule = "m" } } };
        yield return new object[] { nameof(GenericFunctionType), new GenericFunctionType { FunctionSymbol = new FunctionSymbol { Name = "f" }, TypeArguments = new List<SemanticType> { SemanticType.Int } } };
        yield return new object[] { nameof(ConstructorReferenceType), new ConstructorReferenceType { Name = "C" } };
        yield return new object[] { nameof(TemplateType), new TemplateType() };
        yield return new object[] { nameof(LiteralStringType), LiteralStringType.Instance };
        yield return new object[] { nameof(UnmappedClrType), new UnmappedClrType { ClrTypeName = "System.Object" } };
    }

    [Theory]
    [MemberData(nameof(PassThroughInstances))]
    public void Apply_PassesChildlessLeavesThrough(string leafName, SemanticType leaf)
    {
        PassThroughLeaves.Should().Contain(leafName);
        TypeSubstitution.Apply(leaf, ToInt).CanonicalKey.Should().Be(leaf.CanonicalKey);
    }

    [Fact]
    public void EveryPassThroughLeaf_HasAnInstance()
    {
        var covered = PassThroughInstances().Select(row => (string)row[0]).ToHashSet();
        PassThroughLeaves.Except(covered).Should().BeEmpty();
    }

    /// <summary>Whether the key names the parameter <c>T</c> as a whole token (not the `T` in `Task` or `int`).</summary>
    private static bool MentionsBareT(SemanticType type)
        => System.Text.RegularExpressions.Regex.IsMatch(type.CanonicalKey, @"(?<![A-Za-z0-9_])T(?![A-Za-z0-9_])");
}

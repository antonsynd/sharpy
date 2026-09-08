using System.Collections.Immutable;
using Sharpy.Compiler.Semantic;
using Xunit;

using FunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <see cref="ClrSignatureKey"/> (#1721, plan-499995 contract (ii)): the one
/// CLR-mapped signature key is total over the 20 <see cref="SemanticType"/> leaves. Every leaf is
/// either an ERASING arm (its CLR identity differs from its <see cref="SemanticType.CanonicalKey"/>
/// for some value, and the arm recurses) or an IDENTITY leaf (the CLR key IS the canonical key).
/// Both rosters are literal lists; their union is compared to the reflection census, so a 21st
/// leaf fails here until it is placed. Each erasing arm is exercised by a pair whose canonical
/// keys DIFFER and whose CLR keys AGREE — removing that arm from <see cref="ClrSignatureKey.OfType"/>
/// turns exactly that pair red (mutation-tested: see the commit body).
/// </summary>
public class ClrSignatureKeyTotalityTests
{
    private const int ExpectedLeafCount = 20;

    /// <summary>Leaves whose CLR identity erases something C# does not see, recursing inward.</summary>
    private static readonly string[] ErasingLeaves =
    {
        nameof(TupleType),          // element names
        nameof(NullableType),       // reference-type `| None` is an annotation
        nameof(LiteralStringType),  // emits string
        nameof(OptionalType),       // recurses into the payload
        nameof(ResultType),         // recurses into Ok / Error
        nameof(GenericType),        // recurses into type arguments
        nameof(FunctionType),       // Func/Action: parameter and return types only
        nameof(TaskType),           // recurses into the result
    };

    /// <summary>Leaves whose CLR identity is their canonical identity.</summary>
    private static readonly string[] IdentityLeaves =
    {
        nameof(UnknownType), nameof(VoidType), nameof(BuiltinType), nameof(UserDefinedType),
        nameof(ModuleType), nameof(TypeParameterType), nameof(SelfType), nameof(GenericFunctionType),
        nameof(ConstructorReferenceType), nameof(UnionType), nameof(TemplateType), nameof(UnmappedClrType),
    };

    [Fact]
    public void LeafRosters_PartitionTheCensus_AnchoredToLiteral20()
    {
        var census = typeof(SemanticType).Assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && t.IsSubclassOf(typeof(SemanticType)))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(ExpectedLeafCount, census.Count);
        Assert.Equal(ExpectedLeafCount, ErasingLeaves.Length + IdentityLeaves.Length);
        Assert.Empty(ErasingLeaves.Intersect(IdentityLeaves));
        Assert.Equal(census, ErasingLeaves.Concat(IdentityLeaves).OrderBy(n => n).ToList());
    }

    // ── Erasing arms: canonical keys differ, CLR keys agree ─────────────────────────────

    public static IEnumerable<object[]> ErasedPairs()
    {
        var str = SemanticType.Str;
        var strOrNone = new NullableType { UnderlyingType = str };
        var plainTuple = new TupleType { ElementTypes = new List<SemanticType> { SemanticType.Int, str } };
        var namedTuple = new TupleType
        {
            ElementTypes = new List<SemanticType> { SemanticType.Int, str },
            ElementNames = ImmutableArray.Create<string?>("a", "b"),
        };

        yield return new object[] { nameof(TupleType), namedTuple, plainTuple };
        yield return new object[] { nameof(NullableType), strOrNone, str };
        yield return new object[] { nameof(LiteralStringType), LiteralStringType.Instance, str };
        yield return new object[]
        {
            nameof(OptionalType),
            new OptionalType { UnderlyingType = namedTuple },
            new OptionalType { UnderlyingType = plainTuple },
        };
        yield return new object[]
        {
            nameof(ResultType),
            new ResultType { OkType = strOrNone, ErrorType = str },
            new ResultType { OkType = str, ErrorType = str },
        };
        yield return new object[]
        {
            nameof(GenericType),
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { strOrNone } },
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { str } },
        };
        yield return new object[]
        {
            nameof(FunctionType),
            new FunctionType { ParameterTypes = new List<SemanticType> { strOrNone }, ReturnType = str },
            new FunctionType { ParameterTypes = new List<SemanticType> { str }, ReturnType = str },
        };
        yield return new object[]
        {
            nameof(TaskType),
            new TaskType { ResultType = strOrNone },
            new TaskType { ResultType = str },
        };
    }

    [Theory]
    [MemberData(nameof(ErasedPairs))]
    public void ErasingArm_CollapsesWhatCSharpDoesNotSee(string leafName, SemanticType a, SemanticType b)
    {
        Assert.Contains(leafName, ErasingLeaves);
        Assert.NotEqual(a.CanonicalKey, b.CanonicalKey);
        Assert.Equal(ClrSignatureKey.OfType(a), ClrSignatureKey.OfType(b));
    }

    [Fact]
    public void EveryErasingLeaf_HasAnErasedPair()
    {
        var covered = ErasedPairs().Select(row => (string)row[0]).ToHashSet();
        Assert.Empty(ErasingLeaves.Except(covered));
    }

    // ── Distinctions C# DOES see stay distinct ───────────────────────────────────────────

    public static IEnumerable<object[]> DistinctPairs()
    {
        var pointA = new UserDefinedType
        {
            Name = "Point",
            Symbol = new TypeSymbol { Name = "Point", DefiningModule = "a" },
        };
        var pointB = new UserDefinedType
        {
            Name = "Point",
            Symbol = new TypeSymbol { Name = "Point", DefiningModule = "b" },
        };
        var pointFileA = new UserDefinedType
        {
            Name = "Point",
            Symbol = new TypeSymbol { Name = "Point", DefiningFilePath = "/p/a.spy" },
        };
        var pointFileB = new UserDefinedType
        {
            Name = "Point",
            Symbol = new TypeSymbol { Name = "Point", DefiningFilePath = "/p/b.spy" },
        };

        // Nullable VALUE type is Nullable<T> — a distinct C# type.
        yield return new object[] { "int | None vs int", new NullableType { UnderlyingType = SemanticType.Int }, SemanticType.Int };
        yield return new object[] { "int? vs int", new OptionalType { UnderlyingType = SemanticType.Int }, SemanticType.Int };
        yield return new object[] { "int? vs int | None", new OptionalType { UnderlyingType = SemanticType.Int }, new NullableType { UnderlyingType = SemanticType.Int } };
        yield return new object[] { "tuple[int,str] vs tuple[int,int]",
            new TupleType { ElementTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Str } },
            new TupleType { ElementTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Int } } };
        yield return new object[] { "list[int] vs List[int]",
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Int } },
            new GenericType { Name = "List", TypeArguments = new List<SemanticType> { SemanticType.Int } } };
        yield return new object[] { "a.Point vs b.Point (modules)", pointA, pointB };
        yield return new object[] { "a.spy Point vs b.spy Point (files)", pointFileA, pointFileB };
    }

    [Theory]
    [MemberData(nameof(DistinctPairs))]
    public void CSharpVisibleDistinction_StaysDistinct(string label, SemanticType a, SemanticType b)
    {
        _ = label;
        Assert.NotEqual(ClrSignatureKey.OfType(a), ClrSignatureKey.OfType(b));
    }

    // ── Identity leaves pass through unchanged ───────────────────────────────────────────

    public static IEnumerable<object[]> IdentityInstances()
    {
        yield return new object[] { nameof(UnknownType), SemanticType.Unknown };
        yield return new object[] { nameof(VoidType), SemanticType.Void };
        yield return new object[] { nameof(BuiltinType), SemanticType.Int };
        yield return new object[] { nameof(UserDefinedType), new UserDefinedType { Name = "Point", Symbol = new TypeSymbol { Name = "Point", DefiningModule = "m" } } };
        yield return new object[] { nameof(ModuleType), new ModuleType { Symbol = new ModuleSymbol { Name = "m" } } };
        yield return new object[] { nameof(TypeParameterType), new TypeParameterType { Name = "T" } };
        yield return new object[] { nameof(SelfType), new SelfType { DeclaringType = new TypeSymbol { Name = "C", DefiningModule = "m" } } };
        yield return new object[] { nameof(GenericFunctionType), new GenericFunctionType { FunctionSymbol = new FunctionSymbol { Name = "f" }, TypeArguments = new List<SemanticType> { SemanticType.Int } } };
        yield return new object[] { nameof(ConstructorReferenceType), new ConstructorReferenceType { Name = "C" } };
        yield return new object[] { nameof(UnionType), new UnionType { Name = "U", Symbol = new TypeSymbol { Name = "U", DefiningModule = "m" }, CaseTypes = new List<SemanticType> { SemanticType.Int } } };
        yield return new object[] { nameof(TemplateType), new TemplateType() };
        yield return new object[] { nameof(UnmappedClrType), new UnmappedClrType { ClrTypeName = "System.Object" } };
    }

    [Theory]
    [MemberData(nameof(IdentityInstances))]
    public void IdentityLeaf_ClrKeyIsCanonicalKey(string leafName, SemanticType t)
    {
        Assert.Contains(leafName, IdentityLeaves);
        Assert.Equal(t.CanonicalKey, ClrSignatureKey.OfType(t));
    }

    [Fact]
    public void EveryIdentityLeaf_HasAnInstance()
    {
        var covered = IdentityInstances().Select(row => (string)row[0]).ToHashSet();
        Assert.Empty(IdentityLeaves.Except(covered));
    }

    // ── The function-level key omits the receiver ────────────────────────────────────────

    [Fact]
    public void Of_OmitsSelfAndUsesErasedParameterKeys()
    {
        var func = new FunctionSymbol
        {
            Name = "f",
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "C" } },
                new ParameterSymbol { Name = "x", Type = new NullableType { UnderlyingType = SemanticType.Str } },
                new ParameterSymbol { Name = "y", Type = LiteralStringType.Instance },
            },
        };

        Assert.Equal("str,str", ClrSignatureKey.Of(func));
    }
}

using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

using FunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality test for <see cref="SemanticType.CanonicalKey"/> (#1718).
/// For each of the 20 sealed leaf types: construct an equal-by-value pair and a distinct pair;
/// assert <c>a.CanonicalKey == b.CanonicalKey ⟺ a.Equals(b)</c> and <c>GetHashCode</c>
/// consistency. The leaf count is anchored to the literal 20, not derived.
/// <para>
/// A third family covers the wrapper leaves: a wrapper never keys as its payload. Without it a
/// wrapper whose key arm dropped its marker (<c>int?</c> keying as <c>int</c>) survived every
/// same-leaf pair above and was caught only downstream, by the SPY0607 closure matrix (the
/// verify round's M52, 2026-09-08). The wrapper roster is a literal; when
/// <see cref="ExpectedLeafCount"/> moves, decide whether the new leaf wraps a payload.
/// </para>
/// </summary>
public class SemanticTypeIdentityTotalityTests
{
    /// <summary>
    /// The number of sealed leaf types in the SemanticType hierarchy. A 21st test-double leaf
    /// must fail this anchor.
    /// </summary>
    private const int ExpectedLeafCount = 20;

    [Fact]
    public void LeafCount_MatchesExpectedLiteral()
    {
        var leaves = GetLeafTypes();
        Assert.Equal(ExpectedLeafCount, leaves.Count);
    }

    [Fact]
    public void AllLeaves_AreRepresentedInEqualPairs()
    {
        var leafNames = GetLeafTypes().Select(t => t.Name).ToHashSet();
        var testedNames = GetEqualPairs().Select(p => p.LeafName).ToHashSet();
        var missing = leafNames.Except(testedNames).ToList();
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(EqualPairData))]
    public void EqualPair_KeysAgree(string leafName, SemanticType a, SemanticType b)
    {
        _ = leafName;
        Assert.Equal(a.CanonicalKey, b.CanonicalKey);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(DistinctPairData))]
    public void DistinctPair_KeysDisagree(string leafName, SemanticType a, SemanticType b)
    {
        _ = leafName;
        Assert.NotEqual(a.CanonicalKey, b.CanonicalKey);
        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// The leaves that wrap one or more payload types. Literal, not derived: a leaf that is added
    /// to the hierarchy and wraps a payload must be added here by hand, and
    /// <see cref="WrapperLeaves_AreEachLeavesWithAtLeastOnePayloadPair"/> refuses a roster name
    /// that is not a leaf or a roster leaf without a pair.
    /// </summary>
    private static readonly string[] WrapperLeafNames =
    {
        "GenericType", "OptionalType", "ResultType", "NullableType", "FunctionType", "TupleType", "TaskType",
    };

    [Fact]
    public void WrapperLeaves_AreEachLeavesWithAtLeastOnePayloadPair()
    {
        var leafNames = GetLeafTypes().Select(t => t.Name).ToHashSet();
        var pairedNames = GetWrapperVsPayloadPairs().Select(p => p.LeafName).ToHashSet();

        Assert.Equal(7, WrapperLeafNames.Length);
        Assert.Empty(WrapperLeafNames.Except(leafNames));
        Assert.Empty(WrapperLeafNames.Except(pairedNames));
        Assert.Empty(pairedNames.Except(WrapperLeafNames));
    }

    [Theory]
    [MemberData(nameof(WrapperVsPayloadData))]
    public void WrapperVsPayload_KeysDisagree(string leafName, SemanticType wrapper, SemanticType payload)
    {
        _ = leafName;
        Assert.NotEqual(wrapper.CanonicalKey, payload.CanonicalKey);
        Assert.NotEqual(wrapper, payload);
        Assert.NotEqual(payload, wrapper);
    }

    [Fact]
    public void Object_WithAndWithoutSymbol_AreEqual()
    {
        var objectSymbol = new TypeSymbol { Name = "object" };
        var withSymbol = new UserDefinedType { Name = "object", Symbol = objectSymbol };
        var withoutSymbol = new UserDefinedType { Name = "object" };

        Assert.Equal(withSymbol.CanonicalKey, withoutSymbol.CanonicalKey);
        Assert.Equal(withSymbol, withoutSymbol);
        Assert.Equal(withSymbol.GetHashCode(), withoutSymbol.GetHashCode());
    }

    [Fact]
    public void UnmappedClrType_EqualsObject_RosteredExemption()
    {
        var unmapped = new UnmappedClrType { ClrTypeName = "System.Object" };
        var objectType = new UserDefinedType { Name = "object" };

        Assert.Equal("object", unmapped.CanonicalKey);
        Assert.Equal("object", objectType.CanonicalKey);
        Assert.True(unmapped.IsObjectLike);
        Assert.True(objectType.IsObjectLike);
    }

    [Fact]
    public void TwoSameNamedTypes_FromDistinctModules_AreDistinct()
    {
        var symbolA = new TypeSymbol { Name = "Point", DefiningModule = "module_a" };
        var symbolB = new TypeSymbol { Name = "Point", DefiningModule = "module_b" };
        var a = new UserDefinedType { Name = "Point", Symbol = symbolA };
        var b = new UserDefinedType { Name = "Point", Symbol = symbolB };

        Assert.NotEqual(a.CanonicalKey, b.CanonicalKey);
        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Same-project source types carry <c>DefiningFilePath</c>, not <c>DefiningModule</c> (the
    /// rule <c>TypeHierarchyService.IsSameType</c> applies). Keying on the module alone made two
    /// project files' <c>Point</c>s one identity: their overload twins collided (SPY0701) and every
    /// call to either was ambiguous (SPY0353) (#1718, #1721).
    /// </summary>
    [Fact]
    public void TwoSameNamedTypes_FromDistinctFiles_AreDistinct()
    {
        var symbolA = new TypeSymbol { Name = "Point", DefiningFilePath = "/proj/a.spy" };
        var symbolB = new TypeSymbol { Name = "Point", DefiningFilePath = "/proj/b.spy" };
        var a = new UserDefinedType { Name = "Point", Symbol = symbolA };
        var b = new UserDefinedType { Name = "Point", Symbol = symbolB };

        Assert.NotEqual(a.CanonicalKey, b.CanonicalKey);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void UserDefinedType_SameName_NullSymbol_AreEqual()
    {
        var a = new UserDefinedType { Name = "SomeType" };
        var b = new UserDefinedType { Name = "SomeType" };

        Assert.Equal(a.CanonicalKey, b.CanonicalKey);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void IsObjectLike_OnlyTrueForObjectAndUnmapped()
    {
        var objectLikeTypes = new SemanticType[]
        {
            new UserDefinedType { Name = "object" },
            new UnmappedClrType { ClrTypeName = "" },
        };

        var nonObjectTypes = new SemanticType[]
        {
            SemanticType.Int,
            SemanticType.Str,
            new UserDefinedType { Name = "MyClass" },
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Int } },
            new OptionalType { UnderlyingType = SemanticType.Int },
            new NullableType { UnderlyingType = SemanticType.Int },
        };

        foreach (var t in objectLikeTypes)
            Assert.True(t.IsObjectLike, $"{t.GetType().Name} should be object-like");

        foreach (var t in nonObjectTypes)
            Assert.False(t.IsObjectLike, $"{t.GetType().Name} ({t.GetDisplayName()}) should NOT be object-like");
    }

    public static IEnumerable<object[]> EqualPairData()
        => GetEqualPairs().Select(p => new object[] { p.LeafName, p.A, p.B });

    public static IEnumerable<object[]> DistinctPairData()
        => GetDistinctPairs().Select(p => new object[] { p.LeafName, p.A, p.B });

    public static IEnumerable<object[]> WrapperVsPayloadData()
        => GetWrapperVsPayloadPairs().Select(p => new object[] { p.LeafName, p.Wrapper, p.Payload });

    private static List<Type> GetLeafTypes()
    {
        return typeof(SemanticType).Assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && t.IsSubclassOf(typeof(SemanticType)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    private static TypeSymbol MakeTypeSymbol(string name, string? module = null)
        => new TypeSymbol { Name = name, DefiningModule = module };

    private static ModuleSymbol MakeModuleSymbol(string name)
        => new ModuleSymbol { Name = name };

    private static FunctionSymbol MakeFunctionSymbol(string name)
        => new FunctionSymbol { Name = name };

    private static IEnumerable<(string LeafName, SemanticType A, SemanticType B)> GetEqualPairs()
    {
        var intType = new BuiltinType { Name = "int32", ClrType = typeof(int) };
        var strType = new BuiltinType { Name = "str", ClrType = typeof(string) };
        var sym = MakeTypeSymbol("Point", "mymod");
        var modSym = MakeModuleSymbol("json");
        var funcSym = MakeFunctionSymbol("identity");

        yield return ("UnknownType", new UnknownType(), new UnknownType());
        yield return ("VoidType", new VoidType(), new VoidType());
        yield return ("BuiltinType",
            new BuiltinType { Name = "int32" },
            new BuiltinType { Name = "int32" });
        yield return ("GenericType",
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { intType } },
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { intType } });
        yield return ("UserDefinedType",
            new UserDefinedType { Name = "Point", Symbol = sym },
            new UserDefinedType { Name = "Point", Symbol = sym });
        yield return ("OptionalType",
            new OptionalType { UnderlyingType = intType },
            new OptionalType { UnderlyingType = intType });
        yield return ("ResultType",
            new ResultType { OkType = intType, ErrorType = strType },
            new ResultType { OkType = intType, ErrorType = strType });
        yield return ("NullableType",
            new NullableType { UnderlyingType = intType },
            new NullableType { UnderlyingType = intType });
        yield return ("FunctionType",
            new FunctionType { ParameterTypes = new List<SemanticType> { intType }, ReturnType = strType },
            new FunctionType { ParameterTypes = new List<SemanticType> { intType }, ReturnType = strType });
        yield return ("TupleType",
            new TupleType { ElementTypes = new List<SemanticType> { intType, strType } },
            new TupleType { ElementTypes = new List<SemanticType> { intType, strType } });
        yield return ("ModuleType",
            new ModuleType { Symbol = modSym },
            new ModuleType { Symbol = modSym });
        yield return ("TypeParameterType",
            new TypeParameterType { Name = "T" },
            new TypeParameterType { Name = "T" });
        yield return ("SelfType",
            new SelfType { DeclaringType = sym },
            new SelfType { DeclaringType = sym });
        yield return ("GenericFunctionType",
            new GenericFunctionType { FunctionSymbol = funcSym, TypeArguments = new List<SemanticType> { intType } },
            new GenericFunctionType { FunctionSymbol = funcSym, TypeArguments = new List<SemanticType> { intType } });
        yield return ("ConstructorReferenceType",
            new ConstructorReferenceType { Name = "Point", Symbol = sym },
            new ConstructorReferenceType { Name = "Point", Symbol = sym });
        yield return ("UnionType",
            new UnionType { Name = "Result", Symbol = sym, CaseTypes = new List<SemanticType> { intType } },
            new UnionType { Name = "Result", Symbol = sym, CaseTypes = new List<SemanticType> { intType } });
        yield return ("TaskType",
            new TaskType { ResultType = intType },
            new TaskType { ResultType = intType });
        yield return ("TemplateType", TemplateType.Instance, TemplateType.Instance);
        yield return ("LiteralStringType", LiteralStringType.Instance, LiteralStringType.Instance);
        yield return ("UnmappedClrType",
            new UnmappedClrType { ClrTypeName = "System.Linq.IGrouping" },
            new UnmappedClrType { ClrTypeName = "System.Linq.IGrouping" });
    }

    private static IEnumerable<(string LeafName, SemanticType A, SemanticType B)> GetDistinctPairs()
    {
        var intType = new BuiltinType { Name = "int32" };
        var strType = new BuiltinType { Name = "str" };
        var symA = MakeTypeSymbol("Point", "mod_a");
        var symB = MakeTypeSymbol("Point", "mod_b");
        var funcSymA = MakeFunctionSymbol("f");
        var funcSymB = MakeFunctionSymbol("g");

        yield return ("UnknownType_vs_VoidType",
            new UnknownType(),
            new VoidType());
        yield return ("BuiltinType",
            new BuiltinType { Name = "int32" },
            new BuiltinType { Name = "str" });
        yield return ("GenericType_DifferentArgs",
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { intType } },
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { strType } });
        yield return ("UserDefinedType_DifferentModules",
            new UserDefinedType { Name = "Point", Symbol = symA },
            new UserDefinedType { Name = "Point", Symbol = symB });
        yield return ("OptionalType_DifferentUnderlying",
            new OptionalType { UnderlyingType = intType },
            new OptionalType { UnderlyingType = strType });
        yield return ("OptionalType_vs_NullableType",
            new OptionalType { UnderlyingType = intType },
            new NullableType { UnderlyingType = intType });
        yield return ("ResultType",
            new ResultType { OkType = intType, ErrorType = strType },
            new ResultType { OkType = strType, ErrorType = intType });
        yield return ("NullableType",
            new NullableType { UnderlyingType = intType },
            new NullableType { UnderlyingType = strType });
        yield return ("FunctionType_DifferentParams",
            new FunctionType { ParameterTypes = new List<SemanticType> { intType }, ReturnType = strType },
            new FunctionType { ParameterTypes = new List<SemanticType> { strType }, ReturnType = intType });
        yield return ("TupleType_DifferentElements",
            new TupleType { ElementTypes = new List<SemanticType> { intType, strType } },
            new TupleType { ElementTypes = new List<SemanticType> { strType, intType } });
        yield return ("TypeParameterType",
            new TypeParameterType { Name = "T" },
            new TypeParameterType { Name = "U" });
        yield return ("TypeParameterType_WithConstraints",
            new TypeParameterType { Name = "T", Variance = TypeParameterVariance.Covariant },
            new TypeParameterType { Name = "T", Variance = TypeParameterVariance.Contravariant });
        yield return ("GenericFunctionType",
            new GenericFunctionType { FunctionSymbol = funcSymA, TypeArguments = new List<SemanticType> { intType } },
            new GenericFunctionType { FunctionSymbol = funcSymB, TypeArguments = new List<SemanticType> { intType } });
        yield return ("UnionType_DifferentCases",
            new UnionType { Name = "U", CaseTypes = new List<SemanticType> { intType } },
            new UnionType { Name = "U", CaseTypes = new List<SemanticType> { strType } });
        yield return ("TaskType",
            new TaskType { ResultType = intType },
            new TaskType { ResultType = strType });
    }

    /// <summary>
    /// One (wrapper, payload) pair per payload slot of every wrapper leaf. The payload is the
    /// exact instance the wrapper was built over, so a key arm that forgets its marker collapses
    /// onto the payload's key and the pair fails.
    /// </summary>
    private static IEnumerable<(string LeafName, SemanticType Wrapper, SemanticType Payload)> GetWrapperVsPayloadPairs()
    {
        var intType = new BuiltinType { Name = "int32" };
        var strType = new BuiltinType { Name = "str" };

        yield return ("GenericType",
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { intType } },
            intType);
        yield return ("OptionalType",
            new OptionalType { UnderlyingType = intType },
            intType);
        yield return ("ResultType",
            new ResultType { OkType = intType, ErrorType = strType },
            intType);
        yield return ("ResultType",
            new ResultType { OkType = intType, ErrorType = strType },
            strType);
        yield return ("NullableType",
            new NullableType { UnderlyingType = intType },
            intType);
        yield return ("FunctionType",
            new FunctionType { ParameterTypes = new List<SemanticType>(), ReturnType = intType },
            intType);
        yield return ("FunctionType",
            new FunctionType { ParameterTypes = new List<SemanticType> { intType }, ReturnType = new VoidType() },
            intType);
        yield return ("TupleType",
            new TupleType { ElementTypes = new List<SemanticType> { intType } },
            intType);
        yield return ("TaskType",
            new TaskType { ResultType = intType },
            intType);
    }
}

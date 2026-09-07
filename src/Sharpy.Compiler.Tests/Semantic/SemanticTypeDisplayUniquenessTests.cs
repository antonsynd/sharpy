using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

using FunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Display-name uniqueness over all 20 <see cref="SemanticType"/> leaves at depth 2 (#1718).
/// No longer load-bearing for identity (that is <see cref="SemanticTypeIdentityTotalityTests"/>);
/// guards diagnostic message clarity — two semantically distinct types must not render identically.
/// The ONE rostered exemption is <c>UserDefinedType{Name:"object"}</c> ≡ <c>UnmappedClrType</c>
/// (both display <c>"object"</c>).
/// </summary>
public class SemanticTypeDisplayUniquenessTests
{
    private static readonly BuiltinType Int64 = new() { Name = "int64" };
    private static readonly BuiltinType Str = new() { Name = "str", ClrType = typeof(string) };

    private static readonly (string Collision, string A, string B) RosteredExemption =
        ("object", "UserDefinedType(object)", "UnmappedClrType");

    [Fact]
    public void AllLeaves_PairwiseDistinctDisplayNames_AtDepth1()
    {
        var leaves = BuildLeafInstances();
        AssertPairwiseDistinct(leaves, "depth-1");
    }

    [Fact]
    public void WrapperTypes_PairwiseDistinctDisplayNames_AtDepth2()
    {
        var inner = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { Int64 }
        };

        var depth2 = new (string Label, SemanticType Type)[]
        {
            ("Optional[list[int64]]", new OptionalType { UnderlyingType = inner }),
            ("Nullable[list[int64]]", new NullableType { UnderlyingType = inner }),
            ("Result[list[int64],str]", new ResultType { OkType = inner, ErrorType = Str }),
            ("Task[list[int64]]", new TaskType { ResultType = inner }),
            ("Generic(dict,[list[int64]])", new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType> { Str, inner }
            }),
            ("Tuple[list[int64],str]", new TupleType
            {
                ElementTypes = new List<SemanticType> { inner, Str }
            }),
            ("Function(list[int64])->str", new FunctionType
            {
                ParameterTypes = new List<SemanticType> { inner },
                ReturnType = Str
            }),
        };

        AssertPairwiseDistinct(depth2, "depth-2");
    }

    [Fact]
    public void NullableType_DisplaysAsUnionWithNone()
    {
        var nullable = new NullableType { UnderlyingType = Int64 };
        Assert.Equal("int64 | None", nullable.GetDisplayName());
    }

    [Fact]
    public void OptionalType_DisplaysWithQuestionMark()
    {
        var optional = new OptionalType { UnderlyingType = Int64 };
        Assert.Equal("int64?", optional.GetDisplayName());
    }

    private static (string Label, SemanticType Type)[] BuildLeafInstances()
    {
        var sym = new TypeSymbol { Name = "Point" };
        var modSym = new ModuleSymbol { Name = "json" };
        var funcSym = new FunctionSymbol { Name = "identity" };

        return new (string Label, SemanticType Type)[]
        {
            ("UnknownType", new UnknownType()),
            ("VoidType", new VoidType()),
            ("BuiltinType(int64)", Int64),
            ("BuiltinType(str)", Str),
            ("GenericType(list)", new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { Int64 }
            }),
            ("UserDefinedType(Point)", new UserDefinedType { Name = "Point", Symbol = sym }),
            ("UserDefinedType(object)", new UserDefinedType { Name = "object" }),
            ("OptionalType", new OptionalType { UnderlyingType = Int64 }),
            ("ResultType", new ResultType { OkType = Int64, ErrorType = Str }),
            ("NullableType", new NullableType { UnderlyingType = Int64 }),
            ("FunctionType", new FunctionType
            {
                ParameterTypes = new List<SemanticType> { Int64 },
                ReturnType = Str
            }),
            ("TupleType", new TupleType
            {
                ElementTypes = new List<SemanticType> { Int64, Str }
            }),
            ("ModuleType", new ModuleType { Symbol = modSym }),
            ("TypeParameterType", new TypeParameterType { Name = "T" }),
            ("SelfType", new SelfType { DeclaringType = sym }),
            ("GenericFunctionType", new GenericFunctionType
            {
                FunctionSymbol = funcSym,
                TypeArguments = new List<SemanticType> { Int64 }
            }),
            ("ConstructorReferenceType", new ConstructorReferenceType
            {
                Name = "Point",
                Symbol = sym
            }),
            ("UnionType", new UnionType
            {
                Name = "MyUnion",
                CaseTypes = new List<SemanticType> { Int64 }
            }),
            ("TaskType", new TaskType { ResultType = Int64 }),
            ("TemplateType", TemplateType.Instance),
            ("LiteralStringType", LiteralStringType.Instance),
            ("UnmappedClrType", new UnmappedClrType { ClrTypeName = "System.Linq.IGrouping" }),
        };
    }

    private static void AssertPairwiseDistinct(
        (string Label, SemanticType Type)[] types,
        string depth)
    {
        var seen = new Dictionary<string, string>();
        var collisions = new List<string>();

        foreach (var (label, type) in types)
        {
            var display = type.GetDisplayName();
            if (seen.TryGetValue(display, out var existing))
            {
                if (IsRosteredExemption(existing, label))
                    continue;
                collisions.Add($"[{depth}] '{display}' collides: {existing} vs {label}");
            }
            else
            {
                seen[display] = label;
            }
        }

        Assert.Empty(collisions);
    }

    private static bool IsRosteredExemption(string a, string b)
    {
        var pair = (a, b);
        var reverse = (b, a);
        return pair == (RosteredExemption.A, RosteredExemption.B)
            || reverse == (RosteredExemption.A, RosteredExemption.B);
    }
}

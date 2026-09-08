using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeSubstitutionTests
{
    private static readonly Dictionary<string, SemanticType> Substitutions = new()
    {
        ["T"] = BuiltinType.Int,
        ["U"] = BuiltinType.Str,
    };

    [Fact]
    public void Apply_TypeParameterType_FoundInMap_ReturnsSubstitution()
    {
        var tpt = new TypeParameterType { Name = "T" };
        var result = TypeSubstitution.Apply(tpt, Substitutions);
        Assert.Same(BuiltinType.Int, result);
    }

    [Fact]
    public void Apply_TypeParameterType_NotInMap_ReturnsUnchanged()
    {
        var tpt = new TypeParameterType { Name = "V" };
        var result = TypeSubstitution.Apply(tpt, Substitutions);
        Assert.Same(tpt, result);
    }

    [Fact]
    public void Apply_GenericType_SubstitutesTypeArguments()
    {
        var generic = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
        };

        var result = TypeSubstitution.Apply(generic, Substitutions);

        var gt = Assert.IsType<GenericType>(result);
        Assert.Equal("list", gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Same(BuiltinType.Int, gt.TypeArguments[0]);
    }

    /// <summary>
    /// <c>T | None</c> closed at a reference type keeps its nullable wrapper.
    /// </summary>
    [Fact]
    public void Apply_NullableType_OverReferenceTypeParameter_SubstitutesUnderlyingType()
    {
        var nullable = new NullableType
        {
            UnderlyingType = new TypeParameterType { Name = "U" }
        };

        var result = TypeSubstitution.Apply(nullable, Substitutions);

        var nt = Assert.IsType<NullableType>(result);
        Assert.Same(BuiltinType.Str, nt.UnderlyingType);
    }

    /// <summary>
    /// <c>T | None</c> closed at a value type IS that value type (plan-499995 Design Decision 3,
    /// #1797; Axiom 1): there is no <c>Nullable&lt;T&gt;</c> behind an unconstrained <c>T</c>, so
    /// the substitution collapses the wrapper rather than inventing an <c>int?</c> the emitted
    /// C# cannot spell.
    /// </summary>
    [Fact]
    public void Apply_NullableType_OverValueTypeParameter_CollapsesToTheValueType()
    {
        var nullable = new NullableType
        {
            UnderlyingType = new TypeParameterType { Name = "T" }
        };

        var result = TypeSubstitution.Apply(nullable, Substitutions);

        Assert.Same(BuiltinType.Int, result);
    }

    /// <summary>
    /// The collapse is for a BARE type parameter only: <c>list[T] | None</c> is an ordinary
    /// reference slot whatever <c>T</c> closes at.
    /// </summary>
    [Fact]
    public void Apply_NullableType_OverCompositeOfValueTypeParameter_KeepsTheWrapper()
    {
        var nullable = new NullableType
        {
            UnderlyingType = new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
            }
        };

        var result = TypeSubstitution.Apply(nullable, Substitutions);

        var nt = Assert.IsType<NullableType>(result);
        var gt = Assert.IsType<GenericType>(nt.UnderlyingType);
        Assert.Same(BuiltinType.Int, gt.TypeArguments[0]);
    }

    [Fact]
    public void Apply_OptionalType_SubstitutesUnderlyingType()
    {
        var optional = new OptionalType
        {
            UnderlyingType = new TypeParameterType { Name = "U" }
        };

        var result = TypeSubstitution.Apply(optional, Substitutions);

        var ot = Assert.IsType<OptionalType>(result);
        Assert.Same(BuiltinType.Str, ot.UnderlyingType);
    }

    [Fact]
    public void Apply_ResultType_SubstitutesOkAndErrorTypes()
    {
        var resultType = new ResultType
        {
            OkType = new TypeParameterType { Name = "T" },
            ErrorType = new TypeParameterType { Name = "U" }
        };

        var result = TypeSubstitution.Apply(resultType, Substitutions);

        var rt = Assert.IsType<ResultType>(result);
        Assert.Same(BuiltinType.Int, rt.OkType);
        Assert.Same(BuiltinType.Str, rt.ErrorType);
    }

    [Fact]
    public void Apply_FunctionType_SubstitutesParameterAndReturnTypes()
    {
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "U" }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        var result = TypeSubstitution.Apply(funcType, Substitutions);

        var ft = Assert.IsType<FunctionType>(result);
        Assert.Equal(2, ft.ParameterTypes.Count);
        Assert.Same(BuiltinType.Int, ft.ParameterTypes[0]);
        Assert.Same(BuiltinType.Str, ft.ParameterTypes[1]);
        Assert.Same(BuiltinType.Int, ft.ReturnType);
    }

    [Fact]
    public void Apply_TupleType_SubstitutesElementTypes()
    {
        var tupleType = new TupleType
        {
            ElementTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "U" }
            }
        };

        var result = TypeSubstitution.Apply(tupleType, Substitutions);

        var tt = Assert.IsType<TupleType>(result);
        Assert.Equal(2, tt.ElementTypes.Count);
        Assert.Same(BuiltinType.Int, tt.ElementTypes[0]);
        Assert.Same(BuiltinType.Str, tt.ElementTypes[1]);
    }

    [Fact]
    public void Apply_NonParameterizedType_ReturnsUnchanged()
    {
        var result = TypeSubstitution.Apply(BuiltinType.Int, Substitutions);
        Assert.Same(BuiltinType.Int, result);
    }

    [Fact]
    public void Apply_VoidType_ReturnsUnchanged()
    {
        var result = TypeSubstitution.Apply(SemanticType.Void, Substitutions);
        Assert.Same(SemanticType.Void, result);
    }

    [Fact]
    public void Apply_NestedGenericType_SubstitutesRecursively()
    {
        // Dict[T, List[U]] → Dict[int, List[str]]
        var innerGeneric = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "U" } }
        };
        var outerGeneric = new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                innerGeneric
            }
        };

        var result = TypeSubstitution.Apply(outerGeneric, Substitutions);

        var outer = Assert.IsType<GenericType>(result);
        Assert.Equal("dict", outer.Name);
        Assert.Equal(2, outer.TypeArguments.Count);
        Assert.Same(BuiltinType.Int, outer.TypeArguments[0]);

        var inner = Assert.IsType<GenericType>(outer.TypeArguments[1]);
        Assert.Equal("list", inner.Name);
        Assert.Single(inner.TypeArguments);
        Assert.Same(BuiltinType.Str, inner.TypeArguments[0]);
    }

    [Fact]
    public void Apply_EmptySubstitutions_ReturnsTypeUnchanged()
    {
        var tpt = new TypeParameterType { Name = "T" };
        var empty = new Dictionary<string, SemanticType>();

        var result = TypeSubstitution.Apply(tpt, empty);

        Assert.Same(tpt, result);
    }

    [Fact]
    public void Apply_GenericType_PreservesGenericDefinition()
    {
        var definition = new TypeSymbol { Name = "List" };
        var generic = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } },
            GenericDefinition = definition
        };

        var result = TypeSubstitution.Apply(generic, Substitutions);

        var gt = Assert.IsType<GenericType>(result);
        Assert.Same(definition, gt.GenericDefinition);
    }
}

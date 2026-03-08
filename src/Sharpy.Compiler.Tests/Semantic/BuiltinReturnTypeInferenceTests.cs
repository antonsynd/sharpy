using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using static Sharpy.Compiler.Shared.BuiltinNames;

namespace Sharpy.Compiler.Tests.Semantic;

public class BuiltinReturnTypeInferenceTests
{
    private readonly TypeInferenceService _typeInference = new(new SymbolTable(new BuiltinRegistry()));

    [Fact]
    public void Len_Returns_Int_For_List()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Len, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Hash_Returns_Int()
    {
        var argTypes = new List<SemanticType> { SemanticType.Int };
        var result = BuiltinReturnTypeInference.InferReturnType(Hash, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Reversed_Returns_Iterator_Of_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Reversed, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Str, gt.TypeArguments[0]);
    }

    [Fact]
    public void Sorted_Returns_List_Of_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Sorted, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(List, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Int, gt.TypeArguments[0]);
    }

    [Fact]
    public void Min_Returns_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Min, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Max_Returns_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = Set, TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Max, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Str, result);
    }

    [Fact]
    public void Enumerate_Returns_Iterator_Of_Tuple_Int_Element()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Enumerate, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.IsType<TupleType>(gt.TypeArguments[0]);
        var tuple = (TupleType)gt.TypeArguments[0];
        Assert.Equal(2, tuple.ElementTypes.Count);
        Assert.Equal(SemanticType.Int, tuple.ElementTypes[0]);
        Assert.Equal(SemanticType.Str, tuple.ElementTypes[1]);
    }

    [Fact]
    public void Enumerate_With_Start_Returns_Iterator_Of_Tuple()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } },
            SemanticType.Int
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Enumerate, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        var tuple = (TupleType)gt.TypeArguments[0];
        Assert.Equal(SemanticType.Int, tuple.ElementTypes[0]);
        Assert.Equal(SemanticType.Int, tuple.ElementTypes[1]);
    }

    [Fact]
    public void Zip_Returns_Iterator_Of_Tuple_Two_Elements()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } },
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Zip, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.IsType<TupleType>(gt.TypeArguments[0]);
        var tuple = (TupleType)gt.TypeArguments[0];
        Assert.Equal(2, tuple.ElementTypes.Count);
        Assert.Equal(SemanticType.Int, tuple.ElementTypes[0]);
        Assert.Equal(SemanticType.Str, tuple.ElementTypes[1]);
    }

    [Fact]
    public void Zip_Returns_Iterator_Of_Tuple_Three_Elements()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } },
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Str } },
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Bool } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Zip, argTypes, _typeInference);
        Assert.NotNull(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        var tuple = (TupleType)gt.TypeArguments[0];
        Assert.Equal(3, tuple.ElementTypes.Count);
        Assert.Equal(SemanticType.Int, tuple.ElementTypes[0]);
        Assert.Equal(SemanticType.Str, tuple.ElementTypes[1]);
        Assert.Equal(SemanticType.Bool, tuple.ElementTypes[2]);
    }

    [Fact]
    public void Map_Returns_Iterator_Of_Return_Type()
    {
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType> { SemanticType.Int },
            ReturnType = SemanticType.Str
        };
        var argTypes = new List<SemanticType>
        {
            funcType,
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Map, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Str, gt.TypeArguments[0]);
    }

    [Fact]
    public void Map_With_GenericFunctionType_Returns_Iterator_Of_Return_Type()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "str",
            ReturnType = SemanticType.Str,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "value", Type = SemanticType.Int }
            }
        };
        var genFuncType = new GenericFunctionType
        {
            FunctionSymbol = funcSymbol,
            TypeArguments = new List<SemanticType>()
        };
        var argTypes = new List<SemanticType>
        {
            genFuncType,
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Map, argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal(Iterator, gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Str, gt.TypeArguments[0]);
    }

    [Fact]
    public void Map_With_Non_FunctionType_Returns_Null()
    {
        var argTypes = new List<SemanticType>
        {
            SemanticType.Str,
            new GenericType { Name = List, TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType(Map, argTypes, _typeInference);
        Assert.Null(result);
    }

    [Fact]
    public void Unknown_Function_Returns_Null()
    {
        var argTypes = new List<SemanticType> { SemanticType.Int };
        var result = BuiltinReturnTypeInference.InferReturnType("unknown_builtin", argTypes, _typeInference);
        Assert.Null(result);
    }

    [Fact]
    public void Len_With_Wrong_ArgCount_Returns_Null()
    {
        var argTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Int };
        var result = BuiltinReturnTypeInference.InferReturnType(Len, argTypes, _typeInference);
        Assert.Null(result);
    }
}

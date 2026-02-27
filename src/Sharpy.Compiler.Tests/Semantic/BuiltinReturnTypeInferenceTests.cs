using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class BuiltinReturnTypeInferenceTests
{
    private readonly TypeInferenceService _typeInference = new(new SymbolTable(new BuiltinRegistry()));

    [Fact]
    public void Len_Returns_Int_For_List()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType("len", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Hash_Returns_Int()
    {
        var argTypes = new List<SemanticType> { SemanticType.Int };
        var result = BuiltinReturnTypeInference.InferReturnType("hash", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Reversed_Returns_Iterator_Of_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType("reversed", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal("Iterator", gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Str, gt.TypeArguments[0]);
    }

    [Fact]
    public void Sorted_Returns_List_Of_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType("sorted", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.IsType<GenericType>(result);
        var gt = (GenericType)result!;
        Assert.Equal("list", gt.Name);
        Assert.Single(gt.TypeArguments);
        Assert.Equal(SemanticType.Int, gt.TypeArguments[0]);
    }

    [Fact]
    public void Min_Returns_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = "list", TypeArguments = new List<SemanticType> { SemanticType.Int } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType("min", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void Max_Returns_Element_Type()
    {
        var argTypes = new List<SemanticType>
        {
            new GenericType { Name = "set", TypeArguments = new List<SemanticType> { SemanticType.Str } }
        };
        var result = BuiltinReturnTypeInference.InferReturnType("max", argTypes, _typeInference);
        Assert.NotNull(result);
        Assert.Equal(SemanticType.Str, result);
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
        var result = BuiltinReturnTypeInference.InferReturnType("len", argTypes, _typeInference);
        Assert.Null(result);
    }
}

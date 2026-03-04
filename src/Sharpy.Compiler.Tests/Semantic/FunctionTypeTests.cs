using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class FunctionTypeTests
{
    [Fact]
    public void FunctionType_DifferingOptionalParameterCount_AreEqual()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int, SemanticType.Int },
            ReturnType = SemanticType.Int,
            OptionalParameterCount = 0
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int, SemanticType.Int },
            ReturnType = SemanticType.Int,
            OptionalParameterCount = 1
        };

        Assert.Equal(ft1, ft2);
    }

    [Fact]
    public void FunctionType_DifferingOptionalParameterCount_HaveEqualHashCodes()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int, SemanticType.Int },
            ReturnType = SemanticType.Int,
            OptionalParameterCount = 0
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int, SemanticType.Int },
            ReturnType = SemanticType.Int,
            OptionalParameterCount = 1
        };

        Assert.Equal(ft1.GetHashCode(), ft2.GetHashCode());
    }

    [Fact]
    public void FunctionType_SameSignature_AreEqual()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Str },
            ReturnType = SemanticType.Bool,
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Str },
            ReturnType = SemanticType.Bool,
        };

        Assert.Equal(ft1, ft2);
        Assert.Equal(ft1.GetHashCode(), ft2.GetHashCode());
    }

    [Fact]
    public void FunctionType_DifferentParameterTypes_AreNotEqual()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int },
            ReturnType = SemanticType.Int,
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Str },
            ReturnType = SemanticType.Int,
        };

        Assert.NotEqual(ft1, ft2);
    }

    [Fact]
    public void FunctionType_DifferentReturnTypes_AreNotEqual()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int },
            ReturnType = SemanticType.Int,
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int },
            ReturnType = SemanticType.Str,
        };

        Assert.NotEqual(ft1, ft2);
    }

    [Fact]
    public void FunctionType_DifferentParameterCount_AreNotEqual()
    {
        var ft1 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int },
            ReturnType = SemanticType.Int,
        };
        var ft2 = new FunctionType
        {
            ParameterTypes = { SemanticType.Int, SemanticType.Int },
            ReturnType = SemanticType.Int,
        };

        Assert.NotEqual(ft1, ft2);
    }
}

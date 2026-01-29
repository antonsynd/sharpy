using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ResultTypeTests
{
    private static readonly SemanticType ValueError = new UserDefinedType { Name = "ValueError" };
    private static readonly SemanticType IOError = new UserDefinedType { Name = "IOError" };

    [Fact]
    public void ResultType_DisplayName_ShowsBangSyntax()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.Equal("int !ValueError", result.GetDisplayName());
    }

    [Fact]
    public void ResultType_IsValueType_ReturnsTrue()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.True(result.IsValueType);
    }

    [Fact]
    public void ResultType_AssignableToSameResult()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.True(r1.IsAssignableTo(r2));
    }

    [Fact]
    public void ResultType_NotAssignableToDifferentOkType()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Str, ErrorType = ValueError };
        Assert.False(r1.IsAssignableTo(r2));
    }

    [Fact]
    public void ResultType_NotAssignableToDifferentErrorType()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Int, ErrorType = IOError };
        Assert.False(r1.IsAssignableTo(r2));
    }

    [Fact]
    public void ResultType_NotAssignableToOptional()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.False(result.IsAssignableTo(opt));
    }

    [Fact]
    public void ResultType_NotAssignableToRawType()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.False(result.IsAssignableTo(SemanticType.Int));
    }

    [Fact]
    public void ResultType_MakeNullable_WrapsInNullableType()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var nullable = result.MakeNullable();

        Assert.IsType<NullableType>(nullable);
        var nt = (NullableType)nullable;
        Assert.IsType<ResultType>(nt.UnderlyingType);
    }

    [Fact]
    public void ResultType_ComplexTypes_DisplaysCorrectly()
    {
        var okType = new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { SemanticType.Int }
        };
        var result = new ResultType { OkType = okType, ErrorType = ValueError };
        Assert.Equal("list[int] !ValueError", result.GetDisplayName());
    }
}

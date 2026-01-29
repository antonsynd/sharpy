using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Ast;

public class TypeAnnotationTests
{
    [Fact]
    public void TypeAnnotation_Default_NoModifiers()
    {
        var type = new TypeAnnotation { Name = "int" };

        Assert.False(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
        Assert.Null(type.ErrorType);
    }

    [Fact]
    public void TypeAnnotation_Optional_IsOptionalTrue()
    {
        var type = new TypeAnnotation { Name = "int", IsOptional = true };

        Assert.True(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }

    [Fact]
    public void TypeAnnotation_CSharpNullable_IsCSharpNullableTrue()
    {
        var type = new TypeAnnotation { Name = "str", IsCSharpNullable = true };

        Assert.False(type.IsOptional);
        Assert.True(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }

    [Fact]
    public void TypeAnnotation_Result_HasErrorType()
    {
        var errorType = new TypeAnnotation { Name = "ValueError" };
        var type = new TypeAnnotation { Name = "int", ErrorType = errorType };

        Assert.False(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.True(type.IsResult);
        Assert.NotNull(type.ErrorType);
        Assert.Equal("ValueError", type.ErrorType!.Name);
    }

    [Fact]
    public void TypeAnnotation_ResultWithNullable_BothModifiers()
    {
        // int !ValueError | None -> Result[int, ValueError] | None
        var errorType = new TypeAnnotation { Name = "ValueError" };
        var type = new TypeAnnotation
        {
            Name = "int",
            ErrorType = errorType,
            IsCSharpNullable = true
        };

        Assert.True(type.IsResult);
        Assert.True(type.IsCSharpNullable);
        Assert.False(type.IsOptional);
    }

    [Fact]
    public void TypeAnnotation_WithImmutability_RecordCopyWorks()
    {
        var original = new TypeAnnotation { Name = "int" };
        var optional = original with { IsOptional = true };

        Assert.False(original.IsOptional);
        Assert.True(optional.IsOptional);
        Assert.Equal("int", optional.Name);
    }
}

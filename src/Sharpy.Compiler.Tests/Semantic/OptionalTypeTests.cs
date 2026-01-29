using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class OptionalTypeTests
{
    [Fact]
    public void OptionalType_DisplayName_ShowsQuestionMark()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.Equal("int?", opt.GetDisplayName());
    }

    [Fact]
    public void OptionalType_IsNullable_ReturnsTrue()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Str };
        Assert.True(opt.IsNullable);
    }

    [Fact]
    public void OptionalType_IsValueType_ReturnsTrue()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.True(opt.IsValueType);
    }

    [Fact]
    public void OptionalType_AssignableToSameOptional()
    {
        var opt1 = new OptionalType { UnderlyingType = SemanticType.Int };
        var opt2 = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.True(opt1.IsAssignableTo(opt2));
    }

    [Fact]
    public void OptionalType_NotAssignableToDifferentOptional()
    {
        var optInt = new OptionalType { UnderlyingType = SemanticType.Int };
        var optStr = new OptionalType { UnderlyingType = SemanticType.Str };
        Assert.False(optInt.IsAssignableTo(optStr));
    }

    [Fact]
    public void OptionalType_NotAssignableToNullableType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var nullable = new NullableType { UnderlyingType = SemanticType.Int };
        Assert.False(opt.IsAssignableTo(nullable));
    }

    [Fact]
    public void OptionalType_NotAssignableToRawType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.False(opt.IsAssignableTo(SemanticType.Int));
    }

    [Fact]
    public void OptionalType_MakeNullable_WrapsInNullableType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var nullable = opt.MakeNullable();

        Assert.IsType<NullableType>(nullable);
        var nt = (NullableType)nullable;
        Assert.IsType<OptionalType>(nt.UnderlyingType);
    }

    [Fact]
    public void OptionalType_UnwrapNullable_ReturnsSelf()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var unwrapped = opt.UnwrapNullable();
        Assert.Same(opt, unwrapped);
    }

    [Fact]
    public void OptionalType_NestedOptional_DisplaysCorrectly()
    {
        var inner = new OptionalType { UnderlyingType = SemanticType.Int };
        var outer = new OptionalType { UnderlyingType = inner };
        Assert.Equal("int??", outer.GetDisplayName());
    }
}

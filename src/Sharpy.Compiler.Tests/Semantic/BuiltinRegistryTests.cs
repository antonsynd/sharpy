using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class BuiltinRegistryTests
{
    private readonly BuiltinRegistry _registry = new();

    [Theory]
    [InlineData("TypeError")]
    [InlineData("ValueError")]
    [InlineData("RuntimeError")]
    [InlineData("NotImplementedError")]
    [InlineData("AttributeError")]
    [InlineData("ZeroDivisionError")]
    [InlineData("OverflowError")]
    public void LoadBuiltinTypes_RegistersExceptionType(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert
        Assert.NotNull(typeSymbol);
        Assert.Equal(typeName, typeSymbol.Name);
        Assert.NotNull(typeSymbol.ClrType);
        Assert.True(typeof(Exception).IsAssignableFrom(typeSymbol.ClrType),
            $"{typeName} should be assignable to Exception");
    }

    [Fact]
    public void LoadBuiltinTypes_ExceptionBaseTypeIsRegistered()
    {
        // Act
        var exceptionType = _registry.GetType("Exception");

        // Assert
        Assert.NotNull(exceptionType);
        Assert.Equal(typeof(System.Exception), exceptionType.ClrType);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("str")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("long")]
    [InlineData("double")]
    [InlineData("decimal")]
    public void LoadBuiltinTypes_PrimitivesStillRegistered(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert - regression guard: primitives must not be disrupted by auto-discovery
        Assert.NotNull(typeSymbol);
        Assert.NotNull(typeSymbol.ClrType);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("dict")]
    [InlineData("set")]
    public void LoadBuiltinTypes_CollectionsStillRegistered(string typeName)
    {
        // Act
        var typeSymbol = _registry.GetType(typeName);

        // Assert - regression guard
        Assert.NotNull(typeSymbol);
        Assert.True(typeSymbol.IsGeneric);
    }
}

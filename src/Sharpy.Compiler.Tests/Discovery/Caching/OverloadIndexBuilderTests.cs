using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery.Caching;

public class OverloadIndexBuilderTests
{
    private readonly OverloadIndexBuilder _builder = new();

    [Fact]
    public void BuildFromAssembly_DiscoversSharpyCoreExports()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        Assert.NotNull(index);
        Assert.NotNull(index.Identity);
        Assert.Equal("Sharpy.Core", index.Identity.Name);
        Assert.True(index.Modules.Count > 0, "Should discover at least one module");
        Assert.True(index.Modules.ContainsKey("builtins"), "Should discover builtins module");
    }

    [Fact]
    public void BuildFromAssembly_DiscoversRangeOverloads()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        Assert.True(index.Modules.TryGetValue("builtins", out var builtins), "Should discover builtins module");

        Assert.True(builtins.Functions.TryGetValue("range", out var rangeOverloads), "Should discover range function");

        Assert.Equal(3, rangeOverloads.Count);

        // Verify the three overloads
        Assert.Contains(rangeOverloads, s => s.Parameters.Count == 1);  // range(stop)
        Assert.Contains(rangeOverloads, s => s.Parameters.Count == 2);  // range(start, stop)
        Assert.Contains(rangeOverloads, s => s.Parameters.Count == 3);  // range(start, stop, step)
    }

    [Fact]
    public void BuildFromAssembly_DiscoversPrintFunction()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        Assert.True(builtins.Functions.TryGetValue("print", out var printOverloads), "Should discover print function");
        Assert.NotEmpty(printOverloads);

        // Print should have a params array parameter
        var printSig = printOverloads[0];
        Assert.NotEmpty(printSig.Parameters);
        Assert.True(printSig.Parameters[0].IsVariadic, "Print should have variadic parameter");
    }

    [Fact]
    public void BuildFromAssembly_DiscoversLenFunction()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        Assert.True(builtins.Functions.TryGetValue("len", out var lenOverloads), "Should discover len function");
        Assert.NotEmpty(lenOverloads);

        var lenSig = lenOverloads[0];
        Assert.Single(lenSig.Parameters);
        Assert.Equal("obj", lenSig.Parameters[0].Name);
    }

    [Fact]
    public void BuildFromAssembly_CreatesValidMethodTokens()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        var anyFunction = builtins.Functions.Values.First().First();

        Assert.NotEmpty(anyFunction.MethodToken);
        Assert.Contains("|", anyFunction.MethodToken);  // Should have delimiter
    }
}

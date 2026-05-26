using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class NameCasingTests
{
    [Theory]
    [InlineData("my_class", false, "MyClass")]
    [InlineData("my_class", true, "my_class")]
    [InlineData("Widget", false, "Widget")]
    [InlineData("Widget", true, "Widget")]
    public void ResolveType_AppliesPascalCaseUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveType(name, escaped));
    }

    [Theory]
    [InlineData("do_work", false, "DoWork")]
    [InlineData("do_work", true, "do_work")]
    [InlineData("Run", false, "Run")]
    public void ResolveMethod_AppliesPascalCaseUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveMethod(name, escaped));
    }

    [Theory]
    [InlineData("my_field", false, "MyField")]
    [InlineData("my_field", true, "my_field")]
    public void ResolveField_AppliesPascalCaseUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveField(name, escaped));
    }

    [Theory]
    [InlineData("my_var", false, "myVar")]
    [InlineData("my_var", true, "my_var")]
    [InlineData("count", false, "count")]
    public void ResolveVariable_AppliesCamelCaseUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveVariable(name, escaped));
    }

    [Theory]
    [InlineData("max_size", false, "MaxSize")]
    [InlineData("max_size", true, "max_size")]
    [InlineData("MAX_SIZE", false, "MAX_SIZE")]
    public void ResolveConstant_AppliesConstantCaseUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveConstant(name, escaped));
    }

    [Theory]
    [InlineData("my_module", false, "MyModule")]
    [InlineData("my_module", true, "my_module")]
    public void ResolveNamespace_AppliesNamespacePartUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveNamespace(name, escaped));
    }

    [Theory]
    [InlineData("drawable", false, "drawable")]
    [InlineData("drawable", true, "drawable")]
    [InlineData("IDrawable", false, "IDrawable")]
    [InlineData("IDrawable", true, "IDrawable")]
    public void ResolveInterface_PreservesCasingUnlessEscaped(string name, bool escaped, string expected)
    {
        Assert.Equal(expected, NameCasing.ResolveInterface(name, escaped));
    }
}

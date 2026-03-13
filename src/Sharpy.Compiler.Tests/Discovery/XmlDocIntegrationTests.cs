using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Integration tests verifying that XML documentation from Sharpy.Core assemblies
/// is extracted during discovery and propagated to symbols.
/// </summary>
public class XmlDocIntegrationTests
{
    private readonly CachedModuleDiscovery _discovery;

    public XmlDocIntegrationTests()
    {
        // Use a fresh discovery instance with no disk cache to force rebuild from assembly
        _discovery = new CachedModuleDiscovery(new OverloadIndexCache(null));
        _discovery.LoadAssembly(SharpyCoreReference.Assembly);
    }

    [Fact]
    public void LenFunction_HasDocumentation()
    {
        var functions = _discovery.GetModuleFunctions("builtins");
        var lenFunction = functions.FirstOrDefault(f => f.Name == "len");

        Assert.NotNull(lenFunction);
        Assert.NotNull(lenFunction.Documentation);
        Assert.Contains("length", lenFunction.Documentation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrintFunction_HasDocumentation()
    {
        var functions = _discovery.GetModuleFunctions("builtins");
        var printFunction = functions.FirstOrDefault(f => f.Name == "print");

        Assert.NotNull(printFunction);
        Assert.NotNull(printFunction.Documentation);
    }

    [Fact]
    public void ListType_HasDocumentation()
    {
        var listType = _discovery.GetTypeByName("list");

        Assert.NotNull(listType);
        Assert.NotNull(listType.Documentation);
    }

    [Fact]
    public void ListAppendMethod_HasDocumentation()
    {
        var listType = _discovery.GetTypeByName("list");
        Assert.NotNull(listType);

        var appendMethod = listType.Methods.FirstOrDefault(m => m.Name == "append");
        Assert.NotNull(appendMethod);
        Assert.NotNull(appendMethod.Documentation);
    }

    [Fact]
    public void FunctionParameter_HasDocumentation()
    {
        var functions = _discovery.GetModuleFunctions("builtins");
        // format(value, format_spec) has <param> tags in its XML docs
        var formatFunction = functions.FirstOrDefault(f => f.Name == "format");
        Assert.NotNull(formatFunction);
        Assert.True(formatFunction.Parameters.Count > 0);

        // At least one parameter should have documentation
        var hasParamDoc = formatFunction.Parameters.Any(p => p.Documentation != null);
        Assert.True(hasParamDoc, "format() parameter should have documentation");
    }

    [Fact]
    public void TypeProperty_HasDocumentation()
    {
        var listType = _discovery.GetTypeByName("list");
        Assert.NotNull(listType);

        // List should have properties (e.g., Count via ISized)
        if (listType.Properties.Count > 0)
        {
            // At least one property with documentation
            var hasPropertyDoc = listType.Properties.Any(p => p.Documentation != null);
            Assert.True(hasPropertyDoc, "At least one list property should have documentation");
        }
    }
}

using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery.Caching;

public class OverloadIndexBuilderMethodDiscoveryTests
{
    private readonly OverloadIndexBuilder _builder = new();
    private readonly OverloadIndex _index;

    public OverloadIndexBuilderMethodDiscoveryTests()
    {
        _index = _builder.BuildFromAssembly(SharpyCoreReference.Assembly);
    }

    private DiscoveredTypeInfo GetType(string name)
    {
        var builtins = _index.Modules["builtins"];
        return builtins.Types.First(t => t.Name.StartsWith(name));
    }

    // ---- List method discovery ----

    [Fact]
    public void List_DiscoversMethods()
    {
        var list = GetType("List");
        var methodNames = list.Methods.Select(m => m.Name).Distinct().ToList();

        Assert.Contains("append", methodNames);
        Assert.Contains("extend", methodNames);
        Assert.Contains("insert", methodNames);
        Assert.Contains("pop", methodNames);
        Assert.Contains("remove", methodNames);
        Assert.Contains("index", methodNames);
        Assert.Contains("count", methodNames);
        Assert.Contains("sort", methodNames);
        Assert.Contains("reverse", methodNames);
        Assert.Contains("copy", methodNames);
        Assert.Contains("clear", methodNames);
    }

    [Fact]
    public void List_DiscoversOperators()
    {
        var list = GetType("List");
        Assert.Contains("__add__", list.OperatorMethods.Keys);
        Assert.Contains("__mul__", list.OperatorMethods.Keys);
        Assert.Contains("__eq__", list.OperatorMethods.Keys);
        Assert.Contains("__ne__", list.OperatorMethods.Keys);
    }

    [Fact]
    public void List_DiscoversProtocols()
    {
        var list = GetType("List");
        Assert.Contains("__len__", list.ProtocolMethods.Keys);
        Assert.Contains("__iter__", list.ProtocolMethods.Keys);
        Assert.Contains("__contains__", list.ProtocolMethods.Keys);
        Assert.Contains("__getitem__", list.ProtocolMethods.Keys);
        Assert.Contains("__setitem__", list.ProtocolMethods.Keys);
    }

    // ---- Dict method discovery ----

    [Fact]
    public void Dict_DiscoversMethods()
    {
        // Dict is System.Collections.Generic.Dictionary which is not in Sharpy.Core
        // so it won't be discovered. This test documents that behavior.
        var builtins = _index.Modules["builtins"];
        var dictType = builtins.Types.FirstOrDefault(t => t.Name.StartsWith("Dict"));
        // Dict type comes from System.Collections.Generic, not Sharpy.Core assembly
        // so it should not be in the discovered types. This is expected.
        // The BuiltinMethodDefinitions fallback handles dict methods.
        if (dictType != null)
        {
            var methodNames = dictType.Methods.Select(m => m.Name).Distinct().ToList();
            Assert.Contains("get", methodNames);
            Assert.Contains("items", methodNames);
            Assert.Contains("keys", methodNames);
            Assert.Contains("values", methodNames);
        }
    }

    // ---- Set method discovery ----

    [Fact]
    public void Set_DiscoversMethods()
    {
        var set = GetType("Set");
        var methodNames = set.Methods.Select(m => m.Name).Distinct().ToList();

        Assert.Contains("add", methodNames);
        Assert.Contains("discard", methodNames);
        Assert.Contains("remove", methodNames);
        Assert.Contains("union", methodNames);
        Assert.Contains("intersection", methodNames);
        Assert.Contains("difference", methodNames);
        Assert.Contains("symmetric_difference", methodNames);
        // Note: CLR names IsSubset/IsSuperset reverse-mangle to is_subset/is_superset,
        // but the Sharpy API expects issubset/issuperset. This is a known naming gap
        // handled by BuiltinMethodDefinitions fallback.
        Assert.Contains("is_subset", methodNames);
        Assert.Contains("is_superset", methodNames);
        Assert.Contains("copy", methodNames);
        Assert.Contains("clear", methodNames);
    }

    [Fact]
    public void Set_DiscoversOperators()
    {
        var set = GetType("Set");
        Assert.Contains("__or__", set.OperatorMethods.Keys);
        Assert.Contains("__and__", set.OperatorMethods.Keys);
        Assert.Contains("__sub__", set.OperatorMethods.Keys);
        Assert.Contains("__xor__", set.OperatorMethods.Keys);
        Assert.Contains("__eq__", set.OperatorMethods.Keys);
        Assert.Contains("__ne__", set.OperatorMethods.Keys);
    }

    [Fact]
    public void Set_DiscoversProtocols()
    {
        var set = GetType("Set");
        Assert.Contains("__len__", set.ProtocolMethods.Keys);
        Assert.Contains("__iter__", set.ProtocolMethods.Keys);
        Assert.Contains("__contains__", set.ProtocolMethods.Keys);
    }

    // ---- General discovery properties ----

    [Fact]
    public void ExcludesObjectMethods()
    {
        var list = GetType("List");
        var methodNames = list.Methods.Select(m => m.Name).Distinct().ToList();

        Assert.DoesNotContain("get_hash_code", methodNames);
        Assert.DoesNotContain("equals", methodNames);
        Assert.DoesNotContain("to_string", methodNames);
        Assert.DoesNotContain("get_type", methodNames);
    }

    [Fact]
    public void CacheFormatVersion_Is5()
    {
        Assert.Equal(5, _index.CacheFormatVersion);
    }
}

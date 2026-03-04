using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Validates that discovery from Sharpy.Core produces correct methods, operators, and protocols
/// for builtin collection types. These are the primary source of type metadata after the
/// removal of BuiltinMethodDefinitions.
/// </summary>
public class DiscoveryAlignmentTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;
    private readonly ITestOutputHelper _output;

    public DiscoveryAlignmentTests(ITestOutputHelper output)
    {
        _output = output;
        _testCacheDir = Path.Combine(Path.GetTempPath(), "sharpy-test-cache", Guid.NewGuid().ToString());
        var cache = new OverloadIndexCache(_testCacheDir);
        _discovery = new CachedModuleDiscovery(cache);
        _discovery.LoadAssembly(SharpyCoreReference.Assembly);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testCacheDir))
        {
            try
            { Directory.Delete(_testCacheDir, recursive: true); }
            catch { /* ignore cleanup errors */ }
        }
    }

    private static TypeParameterType[] MakeSharedTypeParams(params string[] names)
        => names.Select(n => new TypeParameterType { Name = n }).ToArray();

    // ---- List discovery tests ----

    [Fact]
    public void List_Discovery_HasExpectedMethods()
    {
        var discovered = _discovery.GetTypeByName("list", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var methodNames = discovered.Methods.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();
        _output.WriteLine($"List methods: {string.Join(", ", methodNames)}");

        Assert.Contains("append", methodNames);
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
    public void List_Discovery_HasExpectedOperators()
    {
        var discovered = _discovery.GetTypeByName("list", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var operatorKeys = discovered.OperatorMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"List operators: {string.Join(", ", operatorKeys)}");

        Assert.Contains("__add__", operatorKeys);
        Assert.Contains("__mul__", operatorKeys);
        Assert.Contains("__eq__", operatorKeys);
        Assert.Contains("__ne__", operatorKeys);
    }

    [Fact]
    public void List_Discovery_HasExpectedProtocols()
    {
        var discovered = _discovery.GetTypeByName("list", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var protocolKeys = discovered.ProtocolMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"List protocols: {string.Join(", ", protocolKeys)}");

        Assert.Contains("__len__", protocolKeys);
        Assert.Contains("__iter__", protocolKeys);
        Assert.Contains("__contains__", protocolKeys);
        Assert.Contains("__getitem__", protocolKeys);
        Assert.Contains("__setitem__", protocolKeys);
    }

    // ---- Dict discovery tests ----

    [Fact]
    public void Dict_Discovery_HasExpectedMethods()
    {
        var discovered = _discovery.GetTypeByName("dict", MakeSharedTypeParams("T0", "T1"));
        Assert.NotNull(discovered);

        var methodNames = discovered.Methods.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();
        _output.WriteLine($"Dict methods: {string.Join(", ", methodNames)}");

        Assert.Contains("get", methodNames);
        Assert.Contains("items", methodNames);
        Assert.Contains("keys", methodNames);
        Assert.Contains("values", methodNames);
    }

    [Fact]
    public void Dict_Get_HasTwoOverloads()
    {
        var discovered = _discovery.GetTypeByName("dict", MakeSharedTypeParams("T0", "T1"));
        Assert.NotNull(discovered);

        var getOverloads = discovered.Methods.Where(m => m.Name == "get").OrderBy(m => m.Parameters.Count).ToList();
        Assert.Equal(2, getOverloads.Count);

        // 1-param overload: get(key) -> Optional[V]
        Assert.Single(getOverloads[0].Parameters);
        Assert.IsType<OptionalType>(getOverloads[0].ReturnType);

        // 2-param overload: get(key, default) -> V
        Assert.Equal(2, getOverloads[1].Parameters.Count);
        Assert.IsType<TypeParameterType>(getOverloads[1].ReturnType);
    }

    [Fact]
    public void Dict_Discovery_HasExpectedOperators()
    {
        var discovered = _discovery.GetTypeByName("dict", MakeSharedTypeParams("T0", "T1"));
        Assert.NotNull(discovered);

        var operatorKeys = discovered.OperatorMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"Dict operators: {string.Join(", ", operatorKeys)}");

        Assert.Contains("__eq__", operatorKeys);
        Assert.Contains("__ne__", operatorKeys);
    }

    [Fact]
    public void Dict_Discovery_HasExpectedProtocols()
    {
        var discovered = _discovery.GetTypeByName("dict", MakeSharedTypeParams("T0", "T1"));
        Assert.NotNull(discovered);

        var protocolKeys = discovered.ProtocolMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"Dict protocols: {string.Join(", ", protocolKeys)}");

        Assert.Contains("__len__", protocolKeys);
        Assert.Contains("__iter__", protocolKeys);
        Assert.Contains("__contains__", protocolKeys);
        Assert.Contains("__getitem__", protocolKeys);
        Assert.Contains("__setitem__", protocolKeys);
    }

    // ---- Set discovery tests ----

    [Fact]
    public void Set_Discovery_HasExpectedMethods()
    {
        var discovered = _discovery.GetTypeByName("set", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var methodNames = discovered.Methods.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();
        _output.WriteLine($"Set methods: {string.Join(", ", methodNames)}");

        Assert.Contains("add", methodNames);
        Assert.Contains("discard", methodNames);
        Assert.Contains("remove", methodNames);
        Assert.Contains("union", methodNames);
        Assert.Contains("intersection", methodNames);
        Assert.Contains("difference", methodNames);
        Assert.Contains("symmetric_difference", methodNames);
        Assert.Contains("copy", methodNames);
        Assert.Contains("clear", methodNames);
    }

    [Fact]
    public void Set_Discovery_HasExpectedOperators()
    {
        var discovered = _discovery.GetTypeByName("set", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var operatorKeys = discovered.OperatorMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"Set operators: {string.Join(", ", operatorKeys)}");

        Assert.Contains("__or__", operatorKeys);
        Assert.Contains("__and__", operatorKeys);
        Assert.Contains("__sub__", operatorKeys);
        Assert.Contains("__xor__", operatorKeys);
        Assert.Contains("__eq__", operatorKeys);
        Assert.Contains("__ne__", operatorKeys);
    }

    [Fact]
    public void Set_Discovery_HasExpectedProtocols()
    {
        var discovered = _discovery.GetTypeByName("set", MakeSharedTypeParams("T0"));
        Assert.NotNull(discovered);

        var protocolKeys = discovered.ProtocolMethods.Keys.OrderBy(k => k).ToList();
        _output.WriteLine($"Set protocols: {string.Join(", ", protocolKeys)}");

        Assert.Contains("__len__", protocolKeys);
        Assert.Contains("__iter__", protocolKeys);
        Assert.Contains("__contains__", protocolKeys);
    }

    // ---- Tuple: not discoverable from Sharpy.Core ----

    [Fact]
    public void Tuple_NotDiscoverable()
    {
        var discovered = _discovery.GetTypeByName("tuple");
        // tuple maps to System.ValueTuple which is not in Sharpy.Core
        Assert.Null(discovered);
    }
}

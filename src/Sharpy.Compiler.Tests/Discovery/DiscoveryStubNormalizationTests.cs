using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Verifies that discovered protocol and operator stubs are normalized to marker-only format,
/// matching the format produced by BuiltinMethodDefinitions.MakeDunderDict.
/// </summary>
public class DiscoveryStubNormalizationTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;

    public DiscoveryStubNormalizationTests()
    {
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

    // ---- Protocol stub normalization ----

    [Fact]
    public void List_ProtocolStubs_AreMarkerOnly()
    {
        var type = _discovery.GetTypeByName("List");
        Assert.NotNull(type);

        foreach (var (dunderName, stubs) in type.ProtocolMethods)
        {
            Assert.Single(stubs);
            var stub = stubs[0];
            Assert.Equal(dunderName, stub.Name);
            Assert.Equal(SymbolKind.Function, stub.Kind);
            Assert.Equal(AccessLevel.Public, stub.AccessLevel);
            Assert.Empty(stub.Parameters);
            Assert.IsType<UnknownType>(stub.ReturnType);
        }
    }

    [Fact]
    public void List_HasExpectedProtocols()
    {
        var type = _discovery.GetTypeByName("List");
        Assert.NotNull(type);

        var protocols = type.ProtocolMethods.Keys;
        Assert.Contains(DunderNames.Len, protocols);
        Assert.Contains(DunderNames.Iter, protocols);
        Assert.Contains(DunderNames.Contains, protocols);
        Assert.Contains(DunderNames.GetItem, protocols);
        Assert.Contains(DunderNames.SetItem, protocols);
    }

    [Fact]
    public void Dict_ProtocolStubs_AreMarkerOnly()
    {
        var type = _discovery.GetTypeByName("Dict");
        Assert.NotNull(type);

        foreach (var (dunderName, stubs) in type.ProtocolMethods)
        {
            Assert.Single(stubs);
            var stub = stubs[0];
            Assert.Equal(dunderName, stub.Name);
            Assert.Equal(SymbolKind.Function, stub.Kind);
            Assert.Equal(AccessLevel.Public, stub.AccessLevel);
            Assert.Empty(stub.Parameters);
            Assert.IsType<UnknownType>(stub.ReturnType);
        }
    }

    // ---- Operator stub normalization ----

    [Fact]
    public void List_OperatorStubs_AreMarkerOnly()
    {
        var type = _discovery.GetTypeByName("List");
        Assert.NotNull(type);

        foreach (var (dunderName, stubs) in type.OperatorMethods)
        {
            Assert.Single(stubs);
            var stub = stubs[0];
            Assert.Equal(dunderName, stub.Name);
            Assert.Equal(SymbolKind.Function, stub.Kind);
            Assert.Equal(AccessLevel.Public, stub.AccessLevel);
            Assert.Empty(stub.Parameters);
            Assert.IsType<UnknownType>(stub.ReturnType);
        }
    }

    [Fact]
    public void List_HasExpectedOperators()
    {
        var type = _discovery.GetTypeByName("List");
        Assert.NotNull(type);

        var operators = type.OperatorMethods.Keys;
        Assert.Contains(DunderNames.Add, operators);
        Assert.Contains(DunderNames.Mul, operators);
        Assert.Contains(DunderNames.Eq, operators);
        Assert.Contains(DunderNames.Ne, operators);
    }

    [Fact]
    public void Set_OperatorStubs_AreMarkerOnly()
    {
        var type = _discovery.GetTypeByName("Set");
        Assert.NotNull(type);

        foreach (var (dunderName, stubs) in type.OperatorMethods)
        {
            Assert.Single(stubs);
            var stub = stubs[0];
            Assert.Equal(dunderName, stub.Name);
            Assert.Empty(stub.Parameters);
            Assert.IsType<UnknownType>(stub.ReturnType);
        }
    }

    [Fact]
    public void Dict_OperatorStubs_AreMarkerOnly()
    {
        var type = _discovery.GetTypeByName("Dict");
        Assert.NotNull(type);

        foreach (var (dunderName, stubs) in type.OperatorMethods)
        {
            Assert.Single(stubs);
            var stub = stubs[0];
            Assert.Equal(dunderName, stub.Name);
            Assert.Empty(stub.Parameters);
            Assert.IsType<UnknownType>(stub.ReturnType);
        }
    }
}

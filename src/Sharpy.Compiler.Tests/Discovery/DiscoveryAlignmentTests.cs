using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Cross-validates discovery-backed method population against hand-coded BuiltinMethodDefinitions.
/// This serves as the gating check before removing BuiltinMethodDefinitions.
/// </summary>
/// <remarks>
/// Discovery produces a superset of the hand-coded methods (CLR reflection discovers all public
/// methods, not just the Python-API ones). Some naming differences exist between discovery
/// (reverse-mangled from CLR) and hand-coded (Python convention). These known gaps are documented.
/// </remarks>
public class DiscoveryAlignmentTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Known method name mappings where discovery produces a different name than hand-coded.
    /// Key: hand-coded name, Value: discovery name.
    /// </summary>
    private static readonly Dictionary<string, string> KnownNameMappings = new()
    {
        // CLR IsSubsetOf -> reverse-mangled as is_subset_of, shortened to is_subset
        ["issubset"] = "is_subset",
        ["issuperset"] = "is_superset",
    };

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

    private static List<TypeParameterDef> MakeTypeParams(params string[] names)
        => names.Select(n => new TypeParameterDef { Name = n }).ToList();

    private static TypeParameterType[] MakeSharedTypeParams(params string[] names)
        => names.Select(n => new TypeParameterType { Name = n }).ToArray();

    private static string MapMethodName(string handCodedName)
        => KnownNameMappings.GetValueOrDefault(handCodedName, handCodedName);

    // ---- Method alignment tests ----

    [Theory]
    [InlineData("list", new[] { "T0" })]
    [InlineData("dict", new[] { "T0", "T1" })]
    [InlineData("set", new[] { "T0" })]
    public void Discovery_Methods_ContainAllHandCoded_ByName(string typeName, string[] typeParamNames)
    {
        var typeParams = MakeTypeParams(typeParamNames);
        var sharedTypeParams = MakeSharedTypeParams(typeParamNames);

        var handCoded = BuiltinMethodDefinitions.GetMethods(typeName, typeParams);
        var discovered = _discovery.GetTypeByName(typeName, sharedTypeParams);

        Assert.NotNull(discovered);

        var handCodedNames = handCoded.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();
        var discoveredNames = discovered.Methods.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();

        _output.WriteLine($"Hand-coded methods for {typeName}: {string.Join(", ", handCodedNames)}");
        _output.WriteLine($"Discovered methods for {typeName}: {string.Join(", ", discoveredNames)}");

        // Every hand-coded method should be present in discovery (possibly with a mapped name)
        foreach (var name in handCodedNames)
        {
            var discoveryName = MapMethodName(name);
            Assert.True(discoveredNames.Contains(discoveryName),
                $"Method '{name}' (mapped to '{discoveryName}') not found in discovery for {typeName}");
        }
    }

    [Theory]
    [InlineData("list", new[] { "T0" })]
    [InlineData("dict", new[] { "T0", "T1" })]
    [InlineData("set", new[] { "T0" })]
    public void Discovery_Methods_ContainAllHandCoded_ByParamCount(string typeName, string[] typeParamNames)
    {
        var typeParams = MakeTypeParams(typeParamNames);
        var sharedTypeParams = MakeSharedTypeParams(typeParamNames);

        var handCoded = BuiltinMethodDefinitions.GetMethods(typeName, typeParams);
        var discovered = _discovery.GetTypeByName(typeName, sharedTypeParams);

        Assert.NotNull(discovered);

        // Group by (name, paramCount) to compare individual overloads
        var discoveredSigs = discovered.Methods
            .Select(m => (m.Name, ParamCount: m.Parameters.Count))
            .ToHashSet();

        _output.WriteLine($"Hand-coded sigs for {typeName}: {string.Join(", ", handCoded.Select(m => $"{m.Name}({m.Parameters.Count})"))}");
        _output.WriteLine($"Discovered sigs for {typeName}: {string.Join(", ", discoveredSigs.Select(s => $"{s.Name}({s.ParamCount})"))}");

        // Every hand-coded signature should exist in discovery (with name mapping)
        foreach (var method in handCoded)
        {
            var discoveryName = MapMethodName(method.Name);
            Assert.True(discoveredSigs.Contains((discoveryName, method.Parameters.Count)),
                $"Method '{method.Name}({method.Parameters.Count})' (mapped to '{discoveryName}') not found in discovery for {typeName}");
        }
    }

    // ---- Operator alignment tests ----

    [Theory]
    [InlineData("list", new[] { "T0" })]
    [InlineData("dict", new[] { "T0", "T1" })]
    [InlineData("set", new[] { "T0" })]
    public void Discovery_Operators_ContainAllHandCoded(string typeName, string[] typeParamNames)
    {
        var typeParams = MakeTypeParams(typeParamNames);
        var sharedTypeParams = MakeSharedTypeParams(typeParamNames);

        var handCoded = BuiltinMethodDefinitions.GetOperatorMethods(typeName, typeParams);
        var discovered = _discovery.GetTypeByName(typeName, sharedTypeParams);

        Assert.NotNull(discovered);

        var handCodedKeys = handCoded.Keys.OrderBy(k => k).ToList();
        var discoveredKeys = discovered.OperatorMethods.Keys.OrderBy(k => k).ToList();

        _output.WriteLine($"Hand-coded operators for {typeName}: {string.Join(", ", handCodedKeys)}");
        _output.WriteLine($"Discovered operators for {typeName}: {string.Join(", ", discoveredKeys)}");

        foreach (var key in handCodedKeys)
        {
            Assert.Contains(key, discoveredKeys);
        }
    }

    // ---- Protocol alignment tests ----

    [Theory]
    [InlineData("list", new[] { "T0" })]
    [InlineData("dict", new[] { "T0", "T1" })]
    [InlineData("set", new[] { "T0" })]
    public void Discovery_Protocols_ContainAllHandCoded(string typeName, string[] typeParamNames)
    {
        var typeParams = MakeTypeParams(typeParamNames);
        var sharedTypeParams = MakeSharedTypeParams(typeParamNames);

        var handCoded = BuiltinMethodDefinitions.GetProtocolMethods(typeName, typeParams);
        var discovered = _discovery.GetTypeByName(typeName, sharedTypeParams);

        Assert.NotNull(discovered);

        var handCodedKeys = handCoded.Keys.OrderBy(k => k).ToList();
        var discoveredKeys = discovered.ProtocolMethods.Keys.OrderBy(k => k).ToList();

        _output.WriteLine($"Hand-coded protocols for {typeName}: {string.Join(", ", handCodedKeys)}");
        _output.WriteLine($"Discovered protocols for {typeName}: {string.Join(", ", discoveredKeys)}");

        foreach (var key in handCodedKeys)
        {
            Assert.Contains(key, discoveredKeys);
        }
    }

    // ---- Tuple: not discoverable from Sharpy.Core (uses System.ValueTuple) ----

    [Fact]
    public void Tuple_NotDiscoverable_FallbackToHandCoded()
    {
        var discovered = _discovery.GetTypeByName("tuple");
        // tuple maps to System.ValueTuple which is not in Sharpy.Core
        Assert.Null(discovered);

        // Verify hand-coded definitions exist (used as fallback)
        var typeParams = MakeTypeParams("T0");
        var operators = BuiltinMethodDefinitions.GetOperatorMethods("tuple", typeParams);
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods("tuple", typeParams);
        Assert.NotEmpty(operators);
        Assert.NotEmpty(protocols);
    }

    // ---- Return type alignment ----

    [Fact]
    public void Dict_Get_HandCoded_ReturnTypes_AreCorrect()
    {
        var typeParams = MakeTypeParams("T0", "T1");

        var handCoded = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        var handCodedGet = handCoded.Where(m => m.Name == "get").OrderBy(m => m.Parameters.Count).ToList();

        Assert.Equal(2, handCodedGet.Count);

        // 1-param overload: returns Optional[V]
        Assert.IsType<OptionalType>(handCodedGet[0].ReturnType);

        // 2-param overload: returns V directly
        Assert.IsType<TypeParameterType>(handCodedGet[1].ReturnType);
    }

    [Fact]
    public void Dict_Get_Discovery_HasTwoOverloads()
    {
        var sharedTypeParams = MakeSharedTypeParams("T0", "T1");
        var discovered = _discovery.GetTypeByName("dict", sharedTypeParams);

        Assert.NotNull(discovered);

        var discoveredGet = discovered.Methods.Where(m => m.Name == "get").OrderBy(m => m.Parameters.Count).ToList();
        Assert.Equal(2, discoveredGet.Count);

        // Verify overload parameter counts
        Assert.Single(discoveredGet[0].Parameters);
        Assert.Equal(2, discoveredGet[1].Parameters.Count);

        // Note: return types from discovery currently differ from hand-coded because
        // ClrTypeMapper doesn't map Sharpy.Optional<V> to OptionalType.
        // The 1-param overload returns UserDefinedType (from CLR Optional<V>) instead
        // of OptionalType. This is a known gap tracked in #290 Phase 6.
        _output.WriteLine($"dict.get(key) discovered return type: {discoveredGet[0].ReturnType.GetType().Name}");
        _output.WriteLine($"dict.get(key, default) discovered return type: {discoveredGet[1].ReturnType.GetType().Name}");
    }

    // ---- Discovery produces superset (informational) ----

    [Theory]
    [InlineData("list", new[] { "T0" })]
    [InlineData("dict", new[] { "T0", "T1" })]
    [InlineData("set", new[] { "T0" })]
    public void Discovery_HasMoreMethods_ThanHandCoded(string typeName, string[] typeParamNames)
    {
        var typeParams = MakeTypeParams(typeParamNames);
        var sharedTypeParams = MakeSharedTypeParams(typeParamNames);

        var handCoded = BuiltinMethodDefinitions.GetMethods(typeName, typeParams);
        var discovered = _discovery.GetTypeByName(typeName, sharedTypeParams);

        Assert.NotNull(discovered);

        var handCodedCount = handCoded.Select(m => m.Name).Distinct().Count();
        var discoveredCount = discovered.Methods.Select(m => m.Name).Distinct().Count();

        _output.WriteLine($"{typeName}: hand-coded has {handCodedCount} unique methods, discovery has {discoveredCount}");

        // Discovery should have at least as many unique method names
        Assert.True(discoveredCount >= handCodedCount,
            $"Discovery should have at least as many methods as hand-coded for {typeName}");
    }
}

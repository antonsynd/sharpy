using System.Reflection;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #1633: the master <see cref="Semantic.Registry.BuiltinRegistry"/> held by the analysis cache
/// must never be mutated by a compilation. This test snapshots every mutable property on every
/// <see cref="Symbol"/> reachable from the master, runs N analyses with the per-compilation
/// clone bypassed, and asserts no property changed.
///
/// With the clone bypassed, this test verifies CodeGenInfo writes stay in
/// the binding and never leak onto the master's symbols (#1633).
/// </summary>
public class MasterRegistryImmutabilityTests
{
    private readonly ITestOutputHelper _output;

    public MasterRegistryImmutabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly PropertyInfo[] MutableSymbolProperties =
        typeof(Symbol)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetSetMethod(nonPublic: true) is { } setter
                        && (setter.IsPublic || setter.IsAssembly || setter.IsFamilyOrAssembly))
            .OrderBy(p => p.Name)
            .ToArray();

    [Fact]
    public void MasterSymbols_UnchangedAfterAnalysis_WithCloneBypassed()
    {
        var api = new CompilerApi();

        // Warm the cache with a first analysis (cloned, normal).
        var warmSource = "def main() -> None:\n    x: int = len([1, 2, 3])\n    print(x)\n";
        var warmResult = api.Analyze(warmSource);
        Assert.True(warmResult.Success, $"Warm analysis failed: {string.Join("; ", warmResult.Diagnostics.Select(d => d.Message))}");

        var master = api.GetCachedBuiltinsForTests();
        Assert.NotNull(master);

        // Snapshot every mutable property on every master symbol.
        var allSymbols = EnumerateMasterSymbols(master!);
        Assert.True(allSymbols.Count > 0, "No symbols found in the master registry");
        _output.WriteLine($"Snapshotting {allSymbols.Count} master symbols, {MutableSymbolProperties.Length} mutable properties each");

        var snapshot = TakeSnapshot(allSymbols);

        // Enable bypass — the master is now handed to the analysis directly.
        api.BypassRegistryCloneForTests = true;

        // Run analyses that use builtins; MaterializeCodeGenInfo will write to master symbols
        // if the poisoner is still present.
        var sources = new[]
        {
            "def main() -> None:\n    a: int = len('hello')\n    print(a)\n",
            "def main() -> None:\n    b: str = str(42)\n    print(b)\n",
            "def main() -> None:\n    c: bool = isinstance(1, int)\n    print(c)\n",
        };

        foreach (var source in sources)
        {
            try
            {
                api.Analyze(source);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Analysis threw (expected with bypass): {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Compare snapshots.
        var mutations = FindMutations(allSymbols, snapshot);

        foreach (var (symbolName, propertyName, before, after) in mutations)
        {
            _output.WriteLine($"MUTATION: {symbolName}.{propertyName}: {FormatValue(before)} -> {FormatValue(after)}");
        }

        Assert.Empty(mutations);
    }

    [Fact]
    public void SequentialReuse_WithCloneBypassed_Succeeds()
    {
        var api = new CompilerApi();

        // Warm the cache.
        var source1 = "def main() -> None:\n    x: int = len([1])\n    print(x)\n";
        var result1 = api.Analyze(source1);
        Assert.True(result1.Success, $"First analysis failed: {string.Join("; ", result1.Diagnostics.Select(d => d.Message))}");

        // Enable bypass — with the fix in place, the master is never mutated
        // even without cloning.
        api.BypassRegistryCloneForTests = true;

        // Multiple sequential analyses through the same (uncloned) master
        // must all succeed — no DualWriteAssertions, no stale CodeGenInfo.
        for (int i = 0; i < 3; i++)
        {
            var source = $"def main() -> None:\n    v{i}: int = len([{i}])\n    print(v{i})\n";
            var result = api.Analyze(source);
            Assert.True(result.Success,
                $"Analysis {i + 1} with bypass failed: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        }

        _output.WriteLine("3 sequential analyses with bypass: no mutation, no exception");
    }

    private static List<(string Name, Symbol Symbol)> EnumerateMasterSymbols(
        BuiltinRegistry registry)
    {
        var symbols = new List<(string, Symbol)>();
        var seen = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);

        foreach (var (name, type) in registry.GetAllTypes())
        {
            if (seen.Add(type))
                symbols.Add((name, type));

            if (type is TypeSymbol ts)
            {
                foreach (var method in ts.Methods)
                    if (seen.Add(method)) symbols.Add(($"{name}.{method.Name}", method));
                foreach (var ctor in ts.Constructors)
                    if (seen.Add(ctor)) symbols.Add(($"{name}.__init__", ctor));
                foreach (var (opName, overloads) in ts.OperatorMethods)
                    foreach (var op in overloads)
                        if (seen.Add(op)) symbols.Add(($"{name}.{opName}", op));
                foreach (var (protoName, overloads) in ts.ProtocolMethods)
                    foreach (var op in overloads)
                        if (seen.Add(op)) symbols.Add(($"{name}.{protoName}", op));
            }
        }

        foreach (var (name, func) in registry.GetAllFunctions())
        {
            if (seen.Add(func))
                symbols.Add((name, func));
        }

        return symbols;
    }

    private Dictionary<(Symbol, PropertyInfo), object?> TakeSnapshot(
        List<(string Name, Symbol Symbol)> symbols)
    {
        var snap = new Dictionary<(Symbol, PropertyInfo), object?>(
            new SymbolPropertyComparer());

        foreach (var (_, symbol) in symbols)
        {
            foreach (var prop in MutableSymbolProperties)
            {
                try
                {
                    snap[(symbol, prop)] = prop.GetValue(symbol);
                }
                catch
                {
                    snap[(symbol, prop)] = "<error>";
                }
            }
        }

        return snap;
    }

    private List<(string SymbolName, string PropertyName, object? Before, object? After)> FindMutations(
        List<(string Name, Symbol Symbol)> symbols,
        Dictionary<(Symbol, PropertyInfo), object?> snapshot)
    {
        var mutations = new List<(string, string, object?, object?)>();

        foreach (var (name, symbol) in symbols)
        {
            foreach (var prop in MutableSymbolProperties)
            {
                object? current;
                try
                {
                    current = prop.GetValue(symbol);
                }
                catch
                {
                    continue;
                }

                if (!snapshot.TryGetValue((symbol, prop), out var before))
                    continue;

                if (!Equals(before, current))
                    mutations.Add((name, prop.Name, before, current));
            }
        }

        return mutations;
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            _ => value.ToString() ?? "null"
        };

    private sealed class SymbolPropertyComparer : IEqualityComparer<(Symbol, PropertyInfo)>
    {
        public bool Equals((Symbol, PropertyInfo) x, (Symbol, PropertyInfo) y) =>
            ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;

        public int GetHashCode((Symbol, PropertyInfo) obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Item1), obj.Item2.GetHashCode());
    }
}

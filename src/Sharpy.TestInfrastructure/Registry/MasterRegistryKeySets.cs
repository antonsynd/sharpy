using System.Collections;
using System.Reflection;
using Sharpy.Compiler;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;

namespace Sharpy.TestInfrastructure.Registry;

/// <summary>
/// A KEY-SET snapshot of the dictionaries a <see cref="CompilerApi"/>'s cached master registries grow after
/// construction (P54 Design Decision 4, #2250): <see cref="BuiltinRegistry"/>'s <c>_interfaceSymbols</c>,
/// <c>_clrTypeCache</c> (with the CLR type each key resolved to — the value is what the process-wide scan answered),
/// <c>_types</c>/<c>_functions</c> (with each overload set's size; the deferred interface work list is a local of the
/// constructor since Phase 2 — no registration state survives construction), the
/// <see cref="CachedModuleDiscovery"/> and <see cref="ClrTypeBridge"/> caches each master holds, and
/// <see cref="ModuleRegistry"/>'s <c>_loadedAssemblies</c> / <c>_usedAssemblyPaths</c>.
/// Every field is read by reflection on its NAME and a missing field THROWS, so a rename is red, never a silently
/// empty set. The loaded-assembly names are recorded as a <see cref="Scope.Process"/> set: concurrent test classes
/// grow it too, so it is evidence, never an assertion. (The process-wide name-keyed statics this once also recorded —
/// <c>ModuleRegistry._loadedRuntimeNamespaces</c>, <c>ClrTypeHelper._originNameDefinitionCache</c> /
/// <c>_displayNameDefinitionCache</c> — are gone: every CLR name now resolves against the registry's
/// <c>AssemblyVisibility</c>, #2250.)
///
/// <para>Symbol-property writes on the masters are <c>MasterRegistryImmutabilityTests</c>' reflection snapshot;
/// this is the membership half the plan says that guard lacks. Take it only between steps: the masters'
/// plain dictionaries are not safe to enumerate while an analysis writes them.</para>
/// </summary>
public sealed class MasterRegistryKeySets
{
    /// <summary>Whether a set belongs to the compiler instance (asserted) or to the process (recorded only).</summary>
    public enum Scope
    {
        Instance,
        Process,
    }

    /// <summary>One snapshotted dictionary: its path, scope and rendered entries (sorted, ordinal).</summary>
    public sealed record KeySet(string Path, Scope Scope, SCG.IReadOnlyList<string> Keys);

    /// <summary>One set's difference between two snapshots.</summary>
    public sealed record Divergence(string Path, Scope Scope, SCG.IReadOnlyList<string> Added, SCG.IReadOnlyList<string> Removed)
    {
        public override string ToString()
            => $"{Path} [{Scope}] +{Added.Count} -{Removed.Count}"
                + (Added.Count > 0 ? $" first added: {Added[0]}" : "")
                + (Removed.Count > 0 ? $" first removed: {Removed[0]}" : "");
    }

    /// <summary>The instance dictionaries of <see cref="BuiltinRegistry"/>, by field name (a ROSTER — a rename throws).</summary>
    public static readonly string[] BuiltinRegistryFields =
    {
        "_interfaceSymbols", "_clrTypeCache", "_types", "_functions",
    };

    /// <summary>The <see cref="CachedModuleDiscovery"/> dictionaries, by field name.</summary>
    public static readonly string[] DiscoveryFields =
    {
        "_moduleTypeSymbols", "_loadedIndices", "_moduleTypeNames", "_skeletonTypeParams",
    };

    /// <summary>The <see cref="ClrTypeBridge"/> dictionaries, by field name.</summary>
    public static readonly string[] BridgeFields = { "_interfaceSymbolCache", "_typeCache" };

    /// <summary>The <see cref="ModuleRegistry"/> dictionaries, by field name.</summary>
    public static readonly string[] ModuleRegistryFields = { "_loadedAssemblies", "_usedAssemblyPaths" };

    /// <summary>Process-wide name-keyed statics: (declaring type, field name).</summary>
    /// <remarks>Empty since #2250 drained them (see the class summary); kept so a future process-wide static is a row here.</remarks>
    public static readonly (Type Owner, string Field)[] ProcessFields = System.Array.Empty<(Type Owner, string Field)>();

    private MasterRegistryKeySets(SCG.IReadOnlyList<KeySet> sets) => Sets = sets;

    /// <summary>Every snapshotted set, in roster order.</summary>
    public SCG.IReadOnlyList<KeySet> Sets { get; }

    /// <summary>The total number of entries over the instance sets (a floor for "the snapshot saw something").</summary>
    public int InstanceKeyCount => Sets.Where(s => s.Scope == Scope.Instance).Sum(s => s.Keys.Count);

    /// <summary>
    /// Snapshots <paramref name="api"/>'s cached masters (<see cref="CompilerApi.CachedBuiltinsForTests"/>,
    /// <see cref="CompilerApi.CachedModuleRegistryForTests"/>). Throws when no analysis context is cached yet, or when a
    /// rostered field is gone.
    /// </summary>
    public static MasterRegistryKeySets Take(CompilerApi api)
    {
        var builtins = api.CachedBuiltinsForTests
            ?? throw new InvalidOperationException("the CompilerApi has no cached analysis context yet: analyse or prime first");
        return Take(builtins, api.CachedModuleRegistryForTests);
    }

    /// <summary>Snapshots one master <see cref="BuiltinRegistry"/> and (when present) its <see cref="ModuleRegistry"/>.</summary>
    private static MasterRegistryKeySets Take(BuiltinRegistry builtins, ModuleRegistry? modules)
    {
        var sets = new SCG.List<KeySet>();
        foreach (var field in BuiltinRegistryFields)
            sets.Add(SetOf($"BuiltinRegistry.{field}", Scope.Instance, Read(builtins, field)));
        AddDiscovery(sets, "BuiltinRegistry._discovery", Read(builtins, "_discovery"));
        AddBridge(sets, "BuiltinRegistry._clrTypeMapper", Read(builtins, "_clrTypeMapper"));
        if (modules is not null)
        {
            foreach (var field in ModuleRegistryFields)
                sets.Add(SetOf($"ModuleRegistry.{field}", Scope.Instance, Read(modules, field)));
            var discovery = Read(modules, "_discovery")
                ?? throw new InvalidOperationException("ModuleRegistry._discovery is null");
            AddDiscovery(sets, "ModuleRegistry._discovery", discovery);
            AddBridge(sets, "ModuleRegistry._discovery._typeMapper", Read(discovery, "_typeMapper"));
        }

        foreach (var (owner, field) in ProcessFields)
            sets.Add(SetOf($"{owner.Name}.{field}", Scope.Process, ReadStatic(owner, field)));
        sets.Add(new KeySet("AppDomain.GetAssemblies", Scope.Process,
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? "?").Distinct()
                .OrderBy(n => n, StringComparer.Ordinal).ToArray()));
        return new MasterRegistryKeySets(sets);
    }

    /// <summary>The sets whose entries differ in <paramref name="later"/>, instance sets first, then roster order.</summary>
    public SCG.IReadOnlyList<Divergence> DiffTo(MasterRegistryKeySets later)
    {
        var result = new SCG.List<Divergence>();
        var after = later.Sets.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var set in Sets.OrderBy(s => s.Scope))
        {
            if (!after.TryGetValue(set.Path, out var other))
                throw new InvalidOperationException($"{set.Path} is missing from the later snapshot");
            var before = new SCG.HashSet<string>(set.Keys, StringComparer.Ordinal);
            var now = new SCG.HashSet<string>(other.Keys, StringComparer.Ordinal);
            var added = other.Keys.Where(k => !before.Contains(k)).ToArray();
            var removed = set.Keys.Where(k => !now.Contains(k)).ToArray();
            if (added.Length > 0 || removed.Length > 0)
                result.Add(new Divergence(set.Path, set.Scope, added, removed));
        }

        return result;
    }

    /// <summary>The instance-scope divergences only (what an assertion may rest on).</summary>
    public SCG.IReadOnlyList<Divergence> InstanceDiffTo(MasterRegistryKeySets later)
        => DiffTo(later).Where(d => d.Scope == Scope.Instance).ToArray();

    /// <summary>One line naming the first diverging entry (instance sets first), or <c>none</c>.</summary>
    public string FirstDivergence(MasterRegistryKeySets later)
    {
        var divergences = DiffTo(later);
        return divergences.Count == 0 ? "none" : divergences[0].ToString();
    }

    /// <summary>Every divergence, one set per line, at most <paramref name="keysPerSet"/> entries each.</summary>
    public string Describe(MasterRegistryKeySets later, int keysPerSet = 8)
    {
        var divergences = DiffTo(later);
        if (divergences.Count == 0)
            return "no key-set change";
        var lines = new SCG.List<string>();
        foreach (var d in divergences)
        {
            lines.Add($"{d.Path} [{d.Scope}]");
            lines.AddRange(d.Added.Take(keysPerSet).Select(key => $"    + {key}"));
            if (d.Added.Count > keysPerSet)
                lines.Add($"    + ... ({d.Added.Count} in all)");
            lines.AddRange(d.Removed.Take(keysPerSet).Select(key => $"    - {key}"));
            if (d.Removed.Count > keysPerSet)
                lines.Add($"    - ... ({d.Removed.Count} in all)");
        }

        return string.Join("\n", lines);
    }

    private static void AddDiscovery(SCG.List<KeySet> sets, string path, object? discovery)
    {
        if (discovery is null)
            throw new InvalidOperationException($"{path} is null");
        foreach (var field in DiscoveryFields)
            sets.Add(SetOf($"{path}.{field}", Scope.Instance, Read(discovery, field)));
    }

    private static void AddBridge(SCG.List<KeySet> sets, string path, object? bridge)
    {
        if (bridge is null)
            throw new InvalidOperationException($"{path} is null");
        foreach (var field in BridgeFields)
            sets.Add(SetOf($"{path}.{field}", Scope.Instance, Read(bridge, field)));
    }

    private static object? Read(object owner, string field)
    {
        var info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(owner.GetType().FullName, field);
        return info.GetValue(owner);
    }

    private static object? ReadStatic(Type owner, string field)
    {
        var info = owner.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(owner.FullName, field);
        return info.GetValue(null);
    }

    private static KeySet SetOf(string path, Scope scope, object? container)
    {
        if (container is null)
            throw new InvalidOperationException($"{path} is null");
        if (container is not IEnumerable enumerable)
            throw new InvalidOperationException($"{path} is a {container.GetType().Name}, not a collection");

        var keys = new SCG.List<string>();
        if (IsKeyValueSequence(container.GetType()))
        {
            foreach (var entry in enumerable)
                keys.Add(RenderEntry(entry));
        }
        else if (container is ICollection collection)
        {
            // A list is recorded as its count: its elements are work items, not keys.
            keys.Add($"count={collection.Count}");
        }
        else
        {
            throw new InvalidOperationException($"{path} is a {container.GetType().Name}: neither a dictionary nor a counted collection");
        }

        keys.Sort(StringComparer.Ordinal);
        return new KeySet(path, scope, keys);
    }

    private static bool IsKeyValueSequence(Type type)
        => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(SCG.IEnumerable<>)
            && i.GetGenericArguments()[0] is { IsGenericType: true } element
            && element.GetGenericTypeDefinition() == typeof(SCG.KeyValuePair<,>));

    private static string RenderEntry(object? entry)
    {
        if (entry is null)
            return "<null>";
        var type = entry.GetType();
        var key = type.GetProperty("Key")!.GetValue(entry);
        var value = type.GetProperty("Value")!.GetValue(entry);
        return $"{Render(key)} => {RenderValue(value)}";
    }

    private static string Render(object? value) => value switch
    {
        null => "<null>",
        Type t => t.FullName ?? t.Name,
        string s => s,
        _ => value.ToString() ?? "?",
    };

    /// <summary>A value is rendered where it carries the seam's evidence: which CLR type a name resolved to, an overload set's size.</summary>
    private static string RenderValue(object? value) => value switch
    {
        null => "<null>",
        TypeSymbol { ClrType: { } clr } => $"{clr.FullName} @ {clr.Assembly.GetName().Name}",
        TypeSymbol symbol => $"symbol {symbol.Name}",
        string s => s,
        Type t => t.FullName ?? t.Name,
        ICollection collection => $"count={collection.Count}",
        _ => value.GetType().Name,
    };
}

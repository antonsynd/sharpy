using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Conformance test banning unlisted mutable static state in the compiler assembly (#1036).
///
/// Mutable process-global state is a determinism hazard: it makes compilation output depend
/// on invocation history rather than only on inputs. Two rules are enforced by reflecting
/// over <c>typeof(Sharpy.Compiler.Compiler).Assembly</c>:
///
/// <list type="number">
///   <item>Every static field must be <c>readonly</c> (IsInitOnly) or <c>const</c>
///     (IsLiteral) — a plain mutable static (e.g. <c>static int _counter</c>) fails.</item>
///   <item>A <c>static readonly</c> field whose type is a mutable collection
///     (<see cref="HashSet{T}"/>, <see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>)
///     is flagged, because <c>readonly</c> freezes the reference, not the contents — this
///     catches <c>_loadedRuntimeNamespaces</c>-style caches that rule 1 misses.</item>
///   <item>(2b, #2250) A <c>static readonly</c> PROCESS-WIDE CACHE — <see cref="ConcurrentDictionary{TKey,TValue}"/>,
///     <see cref="Lazy{T}"/>, <see cref="ConditionalWeakTable{TKey,TValue}"/>, including the backing field of a
///     static auto-property — needs a <see cref="ProcessWideCacheRoster"/> row. These were once exempt by design
///     ("their contents are a deterministic function of the key"); that premise is only as good as the key, and a
///     name-keyed cache whose value is resolved against the process's loaded assemblies pins whoever asked first
///     for every later compile. So each row states its key-purity justification, and the census is anchored to a
///     literal count.</item>
/// </list>
///
/// Every rule reads a static auto-property's backing field as the property it stands for — keyed by
/// the property name — so <c>static int Counter { get; set; }</c> is rule 1's and
/// <c>static Dictionary&lt;K, V&gt; Map { get; } = new();</c> rule 2's, exactly like the fields they hide.
///
/// Rules 1 and 2 consult a single <see cref="Allowlist"/> keyed by declaring-type + field name.
/// Every entry carries a justification: either an init-once lookup table (populated at
/// declaration/static-ctor and never mutated afterward, so effectively immutable) or a
/// consciously-reviewed runtime-mutable field whose mutation is determinism-safe. Adding a
/// new mutable static forces a conscious allowlist entry, which is the point.
///
/// Scope note: this reflects over the compiler assembly only, not Sharpy.Core. Core carries
/// runtime-semantics state (interned singletons, caches) that would produce false-positive
/// noise here; the determinism-relevant surface is the compiler.
/// </summary>
public class StaticStateConformanceTests
{
    private readonly ITestOutputHelper _output;

    public StaticStateConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Allowlist of known static mutable-typed fields: (declaring type full name, field name)
    /// → justification. Keep this list minimal and justified; new entries mean a new static
    /// mutable field was introduced and must be reviewed for determinism safety.
    /// </summary>
    // Process-wide caches (ConcurrentDictionary / Lazy / ConditionalWeakTable) are rule 2b's,
    // rostered in ProcessWideCacheRoster below — no longer exempt by type (#2250).
    private static readonly IReadOnlyDictionary<(string Type, string Field), string> Allowlist =
        new Dictionary<(string, string), string>
        {
            // ---- Genuinely runtime-mutable / thread-local (determinism-reviewed, #1036) ----
            [("Sharpy.Compiler.Discovery.ClrTypeBridge", "_interfaceListInProgress")] =
                "[ThreadStatic] re-entrancy guard for recursive interface expansion; lazily " +
                "created and cleared per traversal, thread-local so it cannot leak across compiles.",

            // ---- Init-once lookup tables (populated at declaration / static ctor, then only
            //      read — effectively immutable; listed so any NEW static collection is a
            //      conscious review, per #1036). ----
            [("Sharpy.Compiler.Project.SymbolSerializer+TypeCodecRegistry", "s_encoders")] =
                "Type→encoder table populated once in the static constructor, read-only after.",
            [("Sharpy.Compiler.Project.SymbolSerializer+TypeCodecRegistry", "s_decoders")] =
                "Prefix→decoder table populated once in the static constructor, read-only after.",
            [("Sharpy.Compiler.CodeGen.CollectionTypeRegistry", "_entries")] =
                "Collection-type metadata table, initialized inline and read-only after.",
            [("Sharpy.Compiler.Diagnostics.DiagnosticExplanations", "_explanations")] =
                "Diagnostic-code explanation table built once via BuildExplanations(), read-only after.",
            [("Sharpy.Compiler.Discovery.CachedModuleDiscovery", "SharpyToClrNameMap")] =
                "Sharpy→CLR name map, initialized inline and read-only after.",
            [("Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder", "ClrOperatorToDunder")] =
                "CLR-operator→dunder map, initialized inline and read-only after.",
            [("Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder", "ExcludedObjectMethods")] =
                "Set of object methods excluded from overload indexing, initialized inline.",
            [("Sharpy.Compiler.Discovery.OverloadExpander", "ReturnTypeOverrides")] =
                "Return-type override table for known overloads, initialized inline and read-only.",
            [("Sharpy.Compiler.Lexer.Lexer", "Keywords")] =
                "Keyword→token-type table, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "PreferredPrimitiveNames")] =
                "CLR→canonical Sharpy name map for dedup ordering, initialized inline and read-only after (#1667).",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "SeparatelyRegisteredClrTypes")] =
                "CLR types registered separately from the catalog, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "TaggedUnionConstructors")] =
                "Tagged-union constructor names, initialized inline and read-only after.",
            [("Sharpy.Compiler.Semantic.SynthesisAnalyzer", "SynthesizableSharpyCoreInterfaces")] =
                "Set of synthesizable Sharpy.Core interfaces, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "AccessModifierDecorators")] =
                "Access-modifier decorator names, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "AllKnownDecorators")] =
                "All known decorator names, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "InvalidOnInterface")] =
                "Decorators invalid on interfaces, initialized inline and read-only.",
            [("Sharpy.Compiler.Semantic.Validation.DecoratorValidator", "UnsupportedDecorators")] =
                "Unsupported decorator→message map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.CSharpKeywords", "All")] =
                "Set of C# reserved keywords for identifier escaping, initialized inline.",
            [("Sharpy.Compiler.Shared.SelfMatchingBuiltins", "Names")] =
                "PEP 634 self-matching builtin names, initialized inline and read-only (#1653).",
            [("Sharpy.Compiler.Shared.DataclassesEquivalences", "Messages")] =
                "dataclasses guidance-message table, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.DunderDetector", "DunderProperties")] =
                "Set of dunder property names, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.DunderNameMapping", "_dunderMethodMap")] =
                "Dunder→C# operator/method map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.NameMangler", "_collectionMethodMap")] =
                "Builtin-collection method-name map, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.NameMangler", "_clrCollectionVerbMap")] =
                "CLR collection verb→instance method map (#1571), initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.NameMangler", "_upperCaseAcronyms")] =
                "Uppercase-acronym set for namespace casing, initialized inline and read-only.",
            [("Sharpy.Compiler.Shared.TypingEquivalences", "Messages")] =
                "typing guidance-message table, initialized inline and read-only.",
        };

    private const string TypeIdentity =
        " A Type is an identity, so the value is a function of the key; P54 Phase 2 gates GetOrAdd on types " +
        "wholly in the default load context, so a per-registry type is answered uncached and never pins its " +
        "context (Design Decision 3).";

    /// <summary>
    /// Rule 2b roster (#2250): every static process-wide cache in the compiler assembly, with why its value is a
    /// function of its key alone. Rows citing #2250 are NOT key-pure — their value depends on what the process
    /// has loaded — and drain when P54 Phase 2 moves them into the compilation's registry; every other row is
    /// permanent.
    /// </summary>
    private static readonly IReadOnlyDictionary<(string Type, string Field), string> ProcessWideCacheRoster =
        new Dictionary<(string, string), string>
        {
            // ---- Content-addressed (D2 warm-compile caches, #1049) ----
            [("Sharpy.Compiler.AssemblyCompiler", "s_referenceCache")] =
                "MetadataReference keyed by (assembly path, last-write time UTC): a pure function of the file's " +
                "bytes; a changed assembly gets a new mtime and thus a new key. Append-only.",
            [("Sharpy.Compiler.Discovery.Caching.OverloadIndexCache", "s_inMemoryIndices")] =
                "Deserialized OverloadIndex keyed by the cache-file path, which embeds the content-addressed " +
                "AssemblyIdentity key (name-version-hash); a hit is re-validated against the identity. ClearAll " +
                "evicts the directory's entries.",
            [("Sharpy.Compiler.Shared.CompilerIdentity", "s_version")] =
                "Lazy<string> with no key: the compiler assembly's own version + SHA-256, a process constant.",

            // ---- Fixed framework surface ----
            [("Sharpy.Compiler.Discovery.ClrExtensionMethodResolver", "_byName")] =
                "Lazy index over the fixed SurfaceTypes = { typeof(Enumerable) } — a framework type, a process " +
                "constant; no key.",
            [("Sharpy.Compiler.Discovery.ClrExtensionMethodResolver", "_ancestorCache")] =
                "Type -> its base chain + interfaces, read off the Type alone." + TypeIdentity,

            // ---- Type-keyed reflection memos ----
            [("Sharpy.Compiler.Discovery.BuiltinExceptionSurface", "_typeCache")] =
                "Type -> is a builtin exception (Exception-derived, Sharpy.Core assembly, Sharpy namespace), read " +
                "off the Type alone." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.BuiltinExceptionSurface", "_refusedCache")] =
                "(Type, member name) -> refused System.* member, by reflection over the Type alone." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeBridge", "_mapsToSharpyBuiltinCache")] =
                "Generic definition -> collapsed onto a Sharpy builtin, probed through the bridge's own arm table " +
                "keyed on the definition's FullName (ClrTypeBridge is parameterless — no registry input)." +
                TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_paramsIndexerCache")] =
                "Type -> has a params indexer, by reflection over the Type alone." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_clrMethodNameCache")] =
                "(Type, Sharpy member name) -> CLR method name via the pure NameMangler reverse map." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_clrPropertyNameCache")] =
                "(Type, Sharpy member name) -> CLR property name via the pure NameMangler reverse map." +
                TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_originDefinitionCache")] =
                "(Type, definition full name) -> that definition in the Type's own supertype closure." +
                TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_genericInstanceMethodCache")] =
                "(Type, Sharpy member name) -> generic method definitions on the Type." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_memberNameSurfaceCache")] =
                "Type -> every member name it and its interfaces declare." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_overridableMemberCache")] =
                "(Type, Sharpy method name, arity) -> overridable member kind on the Type." + TypeIdentity,
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_extensionMethodNameCache")] =
                "Assembly -> names of the extension methods it exports. An Assembly is an identity, so the value " +
                "is a function of the key; Phase 2's default-context gate applies to Assembly keys as to Type " +
                "keys (Design Decision 3).",
            [("Sharpy.Compiler.Discovery.ClrTypeHelper", "_extensionMethodsByVerbatimNameCache")] =
                "Assembly -> its extension methods by verbatim CLR name. An Assembly is an identity, so the value " +
                "is a function of the key; Phase 2's default-context gate applies to Assembly keys as to Type " +
                "keys (Design Decision 3).",

            // ---- The visibility seam's framework index (#2250, P54 Design Decision 1) ----
            [("Sharpy.Compiler.Discovery.AssemblyVisibility", "s_framework")] =
                "Lazy<TypeIndex> with no key: the framework set — the trusted-platform System.*/Microsoft.*/" +
                "netstandard/mscorlib assemblies, Sharpy.Core, the compiler and its direct dependencies — loaded " +
                "and indexed once; the trusted-platform list is fixed at process start, so the value is a process " +
                "constant. ExecutionAndPublication: a reader blocks until the index is complete. The five " +
                "name-keyed rows it replaced (_originNameDefinitionCache, _displayNameDefinitionCache, " +
                "s_typePresentCache, _probedNamespaces, _loadedRuntimeNamespaces) drained into per-visibility state.",
        };

    [Fact]
    public void CompilerAssembly_HasNoUnlistedMutableStaticState()
    {
        var assembly = typeof(Sharpy.Compiler.Compiler).Assembly;
        var violations = new List<string>();
        var matchedAllowlistKeys = new HashSet<(string, string)>();
        var flaggedCaches = new List<(string, string)>();

        void CheckProcessWideCache((string Type, string Field) cacheKey, Type fieldType)
        {
            flaggedCaches.Add(cacheKey);
            if (!ProcessWideCacheRoster.ContainsKey(cacheKey))
            {
                violations.Add(
                    $"[rule 2b: process-wide cache] {cacheKey.Type}.{cacheKey.Field} : {FriendlyType(fieldType)}");
            }
        }

        foreach (var type in assembly.GetTypes())
        {
            if (IsCompilerGenerated(type))
            {
                continue;
            }

            foreach (var field in type.GetFields(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                // A static auto-property's backing field is compiler-generated, but `static int Counter
                // { get; set; }` is a mutable static exactly like the field it stands for, and
                // `static Dictionary<K, V> Map { get; } = new();` a mutable static collection: every rule
                // applies to it, keyed by the PROPERTY name (#1036, #2250). Any other compiler-generated
                // field (a lambda cache, a fixed buffer) is the compiler's, not ours.
                var memberName = field.Name;
                if (IsCompilerGenerated(field))
                {
                    if (!TryGetAutoPropertyName(field, out memberName))
                    {
                        continue;
                    }
                }

                if (field.IsLiteral && field.FieldType.IsEnum)
                {
                    continue;
                }

                var key = (type.FullName ?? type.Name, memberName);
                var allowlisted = Allowlist.ContainsKey(key);

                // Rule 1: static fields must be readonly or const.
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    if (allowlisted)
                    {
                        matchedAllowlistKeys.Add(key);
                    }
                    else
                    {
                        violations.Add(
                            $"[rule 1: not readonly/const] {key.Item1}.{memberName} : {FriendlyType(field.FieldType)}");
                    }
                    continue;
                }

                // Rule 2: static readonly mutable collections must be allowlisted.
                if (field.IsInitOnly && IsMutableCollection(field.FieldType))
                {
                    if (allowlisted)
                    {
                        matchedAllowlistKeys.Add(key);
                    }
                    else
                    {
                        violations.Add(
                            $"[rule 2: mutable static collection] {key.Item1}.{memberName} : {FriendlyType(field.FieldType)}");
                    }
                }

                // Rule 2b: static readonly process-wide caches must be rostered (#2250).
                if (field.IsInitOnly && IsProcessWideCache(field.FieldType))
                {
                    CheckProcessWideCache(key, field.FieldType);
                }
            }
        }

        if (violations.Count > 0)
        {
            _output.WriteLine("Unlisted mutable static state found:");
            foreach (var v in violations.OrderBy(v => v, StringComparer.Ordinal))
            {
                _output.WriteLine("  " + v);
            }
        }

        Assert.True(violations.Count == 0,
            "Unlisted mutable static state in the compiler assembly (determinism hazard). " +
            "If intentional, add an allowlist entry with a determinism justification in " +
            $"StaticStateConformanceTests.Allowlist:\n  {string.Join("\n  ", violations.OrderBy(v => v, StringComparer.Ordinal))}");

        var staleEntries = Allowlist.Keys.Where(k => !matchedAllowlistKeys.Contains(k)).ToList();
        Assert.True(staleEntries.Count == 0,
            "Stale allowlist entries (no matching flagged field — remove them):\n  " +
            string.Join("\n  ", staleEntries
                .OrderBy(k => $"{k.Item1}.{k.Item2}", StringComparer.Ordinal)
                .Select(k => $"{k.Item1}.{k.Item2}")));

        var staleCacheRows = ProcessWideCacheRoster.Keys.Where(k => !flaggedCaches.Contains(k)).ToList();
        Assert.True(staleCacheRows.Count == 0,
            "Stale ProcessWideCacheRoster rows (no matching static cache — drained or renamed; remove them):\n  " +
            string.Join("\n  ", staleCacheRows
                .OrderBy(k => $"{k.Type}.{k.Field}", StringComparer.Ordinal)
                .Select(k => $"{k.Type}.{k.Field}")));

        // Literal census @ 486c9a88a (measured): 20 ConcurrentDictionary + 2 Lazy, 0 ConditionalWeakTable.
        // P54 Phase 2 task 1 (measured): the five name-keyed rows drained (-5) and the framework index Lazy
        // was rostered (+1): 15 ConcurrentDictionary + 3 Lazy = 18.
        Assert.Equal(18, flaggedCaches.Count);
    }

    /// <summary>
    /// Rule 2b's type test, per arm — the census has no ConditionalWeakTable today, so each arm is pinned
    /// here rather than trusted (positive and negative controls).
    /// </summary>
    [Theory]
    [InlineData(typeof(ConcurrentDictionary<string, int>), true)]
    [InlineData(typeof(Lazy<string>), true)]
    [InlineData(typeof(ConditionalWeakTable<object, object>), true)]
    [InlineData(typeof(System.Collections.Immutable.ImmutableDictionary<string, int>), false)]
    [InlineData(typeof(System.Collections.Frozen.FrozenSet<string>), false)]
    [InlineData(typeof(Dictionary<string, int>), false)]
    [InlineData(typeof(string), false)]
    public void IsProcessWideCache_ClassifiesEachArm(Type type, bool expected)
    {
        Assert.Equal(expected, IsProcessWideCache(type));
    }

    private static bool IsProcessWideCache(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var def = type.GetGenericTypeDefinition();
        return def == typeof(ConcurrentDictionary<,>)
            || def == typeof(Lazy<>)
            || def == typeof(ConditionalWeakTable<,>);
    }

    private static bool TryGetAutoPropertyName(FieldInfo field, out string propertyName)
    {
        // `<Name>k__BackingField`
        const string suffix = ">k__BackingField";
        var name = field.Name;
        if (name.StartsWith('<') && name.EndsWith(suffix, StringComparison.Ordinal))
        {
            propertyName = name.Substring(1, name.Length - 1 - suffix.Length);
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

    private static bool IsMutableCollection(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var def = type.GetGenericTypeDefinition();
        return def == typeof(HashSet<>)
            || def == typeof(Dictionary<,>)
            || def == typeof(List<>);
        // ConcurrentDictionary<,> / Lazy<> / ConditionalWeakTable<,> are rule 2b's (IsProcessWideCache);
        // immutable/frozen/read-only collection types are intentionally not treated as mutable here.
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>() != null
        || (member.Name?.IndexOf('<') >= 0);

    private static string FriendlyType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name.Substring(0, tick);
        }

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyType))}>";
    }

    // ---- Rule 3: Symbol-vending instance caches reachable from AnalysisCacheEntry ----

    /// <summary>
    /// Known instance fields reachable from AnalysisCacheEntry whose value type (or a
    /// generic argument thereof) derives from Symbol. Each entry carries an issue reference
    /// justifying why the field is acceptable (#1633 root-cause analysis).
    /// </summary>
    private static readonly IReadOnlyDictionary<(string Type, string Field), string> SymbolVendingRoster =
        new Dictionary<(string, string), string>
        {
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "_types")] =
                "Dictionary<string, TypeSymbol> — builtin type cache (#1633)",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "_functions")] =
                "Dictionary<string, List<FunctionSymbol>> — builtin function cache (#1633)",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "_interfaceSymbols")] =
                "ConcurrentDictionary<Type, TypeSymbol> — interface definition cache, GetOrAdd with a value that is a " +
                "function of the CLR definition alone; its symbols are frozen with the registry (#1633, #2250)",
            [("Sharpy.Compiler.Discovery.CachedModuleDiscovery", "_moduleTypeSymbols")] =
                "ConcurrentDictionary<string, TypeSymbol> — module type symbol cache (#1633)",
            [("Sharpy.Compiler.Discovery.ClrTypeBridge", "_interfaceSymbolCache")] =
                "ConcurrentDictionary<Type, TypeSymbol> — interface definition cache (#1633)",
            [("Sharpy.Compiler.Semantic.Registry.BuiltinRegistry", "_clrTypeCache")] =
                "ConcurrentDictionary<(string, int), TypeSymbol?> — CLR type lookup cache (#1633)",
        };

    [Fact]
    public void AnalysisCacheEntry_SymbolVendingFields_AreRostered()
    {
        var compilerAssembly = typeof(Sharpy.Compiler.Compiler).Assembly;
        var compilerApiType = compilerAssembly.GetType("Sharpy.Compiler.CompilerApi");
        Assert.NotNull(compilerApiType);

        var cacheEntryType = compilerApiType!.GetNestedType(
            "AnalysisCacheEntry", BindingFlags.NonPublic);
        Assert.NotNull(cacheEntryType);

        var symbolBase = compilerAssembly.GetType("Sharpy.Compiler.Semantic.Symbol");
        Assert.NotNull(symbolBase);

        var found = new HashSet<(string Type, string Field)>();
        var visited = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(cacheEntryType!);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            var fields = current.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (var field in fields)
            {
                if (IsCompilerGenerated(field))
                    continue;

                if (TypeInvolvesSymbol(field.FieldType, symbolBase!))
                {
                    var key = (current.FullName ?? current.Name, field.Name);
                    found.Add(key);
                }

                EnqueueFieldType(field.FieldType, compilerAssembly, queue);
            }

            foreach (var prop in current.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                EnqueueFieldType(prop.PropertyType, compilerAssembly, queue);
            }
        }

        var unlisted = found.Where(f => !SymbolVendingRoster.ContainsKey(f)).ToList();
        var stale = SymbolVendingRoster.Keys.Where(r => !found.Contains(r)).ToList();

        if (unlisted.Count > 0)
        {
            _output.WriteLine("Unlisted Symbol-vending instance fields:");
            foreach (var u in unlisted.OrderBy(u => $"{u.Type}.{u.Field}", StringComparer.Ordinal))
                _output.WriteLine($"  {u.Type}.{u.Field}");
        }

        Assert.True(unlisted.Count == 0,
            "Rule 3: unlisted Symbol-vending instance fields reachable from AnalysisCacheEntry. " +
            "Add to SymbolVendingRoster with an issue reference:\n  " +
            string.Join("\n  ", unlisted
                .OrderBy(u => $"{u.Type}.{u.Field}", StringComparer.Ordinal)
                .Select(u => $"{u.Type}.{u.Field}")));

        Assert.True(stale.Count == 0,
            "Rule 3: stale SymbolVendingRoster entries (field no longer reachable). Remove them:\n  " +
            string.Join("\n  ", stale
                .OrderBy(s => $"{s.Type}.{s.Field}", StringComparer.Ordinal)
                .Select(s => $"{s.Type}.{s.Field}")));
    }

    private static bool TypeInvolvesSymbol(Type type, Type symbolBase)
    {
        if (symbolBase.IsAssignableFrom(type))
            return true;

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (TypeInvolvesSymbol(arg, symbolBase))
                    return true;
            }
        }

        if (type.IsArray)
            return TypeInvolvesSymbol(type.GetElementType()!, symbolBase);

        return false;
    }

    private static void EnqueueFieldType(Type fieldType, Assembly compilerAssembly, Queue<Type> queue)
    {
        var concrete = fieldType;
        if (concrete.IsArray)
            concrete = concrete.GetElementType()!;
        if (concrete.IsGenericType)
        {
            foreach (var arg in concrete.GetGenericArguments())
                EnqueueFieldType(arg, compilerAssembly, queue);
        }

        if (concrete.Assembly == compilerAssembly && !concrete.IsEnum && !concrete.IsAbstract)
            queue.Enqueue(concrete);
    }
}

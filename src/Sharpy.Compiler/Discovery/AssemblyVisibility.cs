extern alias SharpyRT;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// The ONE answer to "which CLR types and namespaces exist" for a compilation (#2250, P54 Design
/// Decision 1): <c>{the framework set} ∪ {the assemblies THIS registry loaded}</c>. Every type-name,
/// namespace and generic-definition lookup the compiler makes against CLR metadata goes through an
/// instance of this class — never through <c>AppDomain.CurrentDomain.GetAssemblies()</c>, the static
/// <c>Type.GetType(string)</c> or <c>Assembly.LoadFrom</c>, which answer "whatever this process has
/// loaded so far" and so made a compile a function of the process's history.
///
/// <para><b>The framework set is a process CONSTANT</b> (option A of Design Decision 1): a process
/// <see cref="Lazy{T}"/> loads, once, every trusted-platform assembly named <c>System</c>,
/// <c>System.*</c>, <c>Microsoft.*</c>, <c>netstandard</c> or <c>mscorlib</c>, plus <c>Sharpy.Core</c>,
/// the compiler itself and the compiler's direct dependencies, and indexes their public types by
/// namespace and by (simple name, arity). The trusted-platform list is fixed at process start, so the
/// index is a function of nothing a compile can change. There is no namespace-driven loading left to
/// race (the former <c>ModuleRegistry._loadedRuntimeNamespaces</c> and
/// <c>ClrAttributeResolver._probedNamespaces</c> gates are subsumed), and the namespace-less queries
/// (a generic definition by display name, "does any assembly declare this namespace", the generic
/// twin of a non-generic CLR type) answer from the whole constant set, never from "the framework
/// assemblies loaded so far".</para>
///
/// <para><b>The registry's own set</b> is the references its <c>ModuleRegistry</c> loaded, in load
/// order. A reference whose simple name is a framework assembly's (Sharpy.Core is a default
/// reference) is not added: the framework copy is the one identity. A compile with no registry sees
/// <see cref="FrameworkOnly"/>.</para>
///
/// <para>Lookup caches live on the instance and are replaced when the own set grows, so a name probed
/// before a reference arrived is not answered from the smaller world afterwards.</para>
/// </summary>
[ThreadSafe]
internal sealed class AssemblyVisibility
{
    /// <summary>
    /// The framework set and its index — built once per process on first use. Key-pure: it has no
    /// key, and its value is a function of the trusted-platform list fixed at process start.
    /// </summary>
    private static readonly Lazy<TypeIndex> s_framework =
        new(BuildFrameworkIndex, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The visibility of a compilation with no registry, of the master builtin registry's CLR
    /// fallback (shared by every compile of one reference set) and of the emitter's one reflection
    /// site: the framework set alone. Its caches are process-wide by being on a static instance —
    /// and key-pure, because the framework set is constant.
    /// </summary>
    public static AssemblyVisibility FrameworkOnly { get; } = new(isFrameworkOnly: true);

    private readonly bool _isFrameworkOnly;
    private readonly object _gate = new();
    private volatile State _state;

    /// <summary>A visibility whose own set starts empty; the owning registry adds its references.</summary>
    public AssemblyVisibility() : this(isFrameworkOnly: false)
    {
    }

    private AssemblyVisibility(bool isFrameworkOnly)
    {
        _isFrameworkOnly = isFrameworkOnly;
        _state = new State(ImmutableArray<Assembly>.Empty);
    }

    /// <summary>The framework index (first use builds it).</summary>
    private static TypeIndex Framework => s_framework.Value;

    /// <summary>The assemblies this registry loaded, in load order (framework assemblies excluded).</summary>
    public ImmutableArray<Assembly> OwnAssemblies => _state.Own;

    /// <summary>Every visible assembly: the framework set first, then the own set in load order.</summary>
    public IEnumerable<Assembly> Assemblies => Framework.Assemblies.Concat(_state.Own);

    /// <summary>
    /// Adds an assembly this registry loaded. A framework assembly (by simple name) is not added — the
    /// framework copy is the one identity — and an assembly already in the own set is a no-op.
    /// Returns true when the own set grew.
    /// </summary>
    internal bool Add(Assembly assembly)
    {
        if (_isFrameworkOnly)
            throw new InvalidOperationException(
                "AssemblyVisibility.FrameworkOnly is constant; a registry owns its own visibility.");

        var name = SafeSimpleName(assembly);
        if (name == null || Framework.BySimpleName.ContainsKey(name))
            return false;

        lock (_gate)
        {
            var state = _state;
            if (state.Own.Contains(assembly))
                return false;
            // A NEW state: every cached answer was reached against the smaller set.
            _state = new State(state.Own.Add(assembly));
            return true;
        }
    }

    /// <summary>
    /// The visible assembly named <paramref name="simpleName"/>: the framework's, else the first in the
    /// own set. Null when neither has one.
    /// </summary>
    public Assembly? FindAssembly(string simpleName)
    {
        if (Framework.BySimpleName.TryGetValue(simpleName, out var framework))
            return framework;
        foreach (var own in _state.Own)
        {
            if (string.Equals(SafeSimpleName(own), simpleName, StringComparison.Ordinal))
                return own;
        }
        return null;
    }

    /// <summary>The framework assembly named <paramref name="simpleName"/>, or null.</summary>
    public static Assembly? FindFrameworkAssembly(string simpleName)
        => Framework.BySimpleName.TryGetValue(simpleName, out var assembly) ? assembly : null;

    /// <summary>
    /// Resolves a CLR type by a stored name — a namespace-qualified full name (<c>System.IO.File</c>,
    /// <c>Sharpy.SocketModule+Error</c>, <c>System.Collections.Generic.List`1</c>) or an
    /// assembly-qualified one (<c>Sharpy.Foo, Sharpy.Stdlib, Version=…</c>), whose assembly must be
    /// visible. Cached per name, nulls included; the cache is replaced when the own set grows.
    /// </summary>
    public Type? ResolveType(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var state = _state;
        return state.ResolveCache.GetOrAdd(name, n => ResolveTypeUncached(state, n));
    }

    /// <summary>
    /// The public top-level types of <paramref name="namespaceName"/>: the framework's, then the own
    /// set's in load order.
    /// </summary>
    public IReadOnlyList<Type> TypesInNamespace(string namespaceName)
    {
        var framework = Framework.TypesByNamespace.TryGetValue(namespaceName, out var f)
            ? f
            : ImmutableArray<Type>.Empty;
        var own = _state.OwnIndex.Value.TypesByNamespace.TryGetValue(namespaceName, out var o)
            ? o
            : ImmutableArray<Type>.Empty;
        return own.IsEmpty ? framework : framework.AddRange(own);
    }

    /// <summary>Whether any visible assembly declares a public top-level type in <paramref name="namespaceName"/>.</summary>
    public bool NamespaceExists(string namespaceName)
        => Framework.TypesByNamespace.ContainsKey(namespaceName)
           || _state.OwnIndex.Value.TypesByNamespace.ContainsKey(namespaceName);

    /// <summary>
    /// The visible generic type definitions whose arity-stripped simple name is
    /// <paramref name="simpleName"/> and whose generic-argument count is <paramref name="arity"/> —
    /// framework first, then the own set, each in index order.
    /// </summary>
    public IEnumerable<Type> GenericDefinitionsNamed(string simpleName, int arity)
    {
        var key = (simpleName, arity);
        var own = _state.OwnIndex.Value;
        if (Framework.GenericDefinitions.TryGetValue(key, out var framework))
        {
            foreach (var t in framework)
                yield return t;
        }
        if (own.GenericDefinitions.TryGetValue(key, out var ownDefinitions))
        {
            foreach (var t in ownDefinitions)
                yield return t;
        }
    }

    /// <summary>
    /// Last-resort resolution of a CLR generic definition from a bare Sharpy DISPLAY name (no
    /// <c>GenericType.ClrOriginTypeName</c>, no symbol) — the shape a return type takes after losing
    /// its identity through the overload-index → builtin-substitution chain (#1517). Probes
    /// <c>System.Collections.Generic</c> first, then any visible generic definition of that simple
    /// name and arity. Cached per name + arity, nulls included.
    /// </summary>
    public Type? ResolveGenericDefinitionByDisplayName(string displayName, int arity)
    {
        var state = _state;
        return state.DisplayNameCache.GetOrAdd((displayName, arity), key =>
        {
            var candidateName = key.Name.Contains('`', StringComparison.Ordinal)
                ? key.Name
                : key.Name + "`" + key.Arity;
            var scg = ResolveTypeUncached(state, "System.Collections.Generic." + candidateName);
            if (scg != null)
                return scg;
            return GenericDefinitionsNamed(ClrNameHelper.StripArity(key.Name), key.Arity).FirstOrDefault();
        });
    }

    private Type? ResolveTypeUncached(State state, string name)
    {
        try
        {
            if (IsAssemblyQualifiedOrConstructed(name))
            {
                // The assembly qualifier binds against the VISIBLE set by simple name — never against
                // the process's binder — and an unqualified (generic-argument) name is searched in the
                // visible set too.
                return TypeNameResolution(
                    name,
                    assemblyName => assemblyName.Name is { } simple
                        ? FindAssembly(simple) ?? LoadDependencyOfOwn(state, assemblyName)
                        : null,
                    (assembly, typeName) => assembly != null
                        ? SafeGetType(assembly, typeName)
                        : FindTypeInVisibleSet(state, typeName));
            }

            return FindTypeInVisibleSet(state, name);
        }
        catch (Exception ex) when (ex is ArgumentException or TypeLoadException or IOException
                                      or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// An assembly-qualified name in a reference's own signatures may name one of that reference's
    /// DEPENDENCIES (a stdlib method returning a type from a package the stdlib uses). It binds the way
    /// the runtime binds it for that reference — through the reference's own load context — and only
    /// when an own assembly actually references the name; the framework is never asked to guess.
    /// </summary>
    private static Assembly? LoadDependencyOfOwn(State state, AssemblyName dependency)
    {
        foreach (var own in state.Own)
        {
            AssemblyName[] referenced;
            try
            {
                referenced = own.GetReferencedAssemblies();
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or NotSupportedException)
            {
                continue;
            }

            if (!referenced.Any(r => string.Equals(r.Name, dependency.Name, StringComparison.Ordinal)))
                continue;

            try
            {
                return AssemblyLoadContext.GetLoadContext(own)?.LoadFromAssemblyName(dependency);
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException)
            {
                return null;
            }
        }
        return null;
    }

    private static Type? FindTypeInVisibleSet(State state, string fullName)
    {
        foreach (var assembly in Framework.Assemblies)
        {
            var found = SafeGetType(assembly, fullName);
            if (found != null)
                return found;
        }
        foreach (var assembly in state.Own)
        {
            var found = SafeGetType(assembly, fullName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// The runtime's type-name parser with BOTH resolvers supplied, so neither the assembly qualifier
    /// nor an unqualified (generic-argument) name is bound against the process. The compiler's one
    /// static <c>Type.GetType</c>, key-pure in the visible set (the scan guard's roster row).
    /// </summary>
    private static Type? TypeNameResolution(
        string name,
        Func<AssemblyName, Assembly?> assemblyResolver,
        Func<Assembly?, string, Type?> typeResolver)
        => Type.GetType(
            name,
            assemblyResolver,
            (assembly, typeName, _) => typeResolver(assembly, typeName),
            throwOnError: false);

    private static bool IsAssemblyQualifiedOrConstructed(string name)
        => name.Contains(',', StringComparison.Ordinal) || name.Contains('[', StringComparison.Ordinal);

    /// <summary>
    /// <c>assembly.GetType(fullName)</c>, null when the assembly cannot answer — an assembly whose own
    /// references do not resolve contributes no type.
    /// </summary>
    internal static Type? SafeGetType(Assembly assembly, string fullName)
    {
        try
        {
            return assembly.GetType(fullName, throwOnError: false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException
                                      or BadImageFormatException or ArgumentException)
        {
            return null;
        }
    }

    // ---- Design Decision 3: a static Type-keyed cache holds only process-stable keys ----

    /// <summary>
    /// Whether <paramref name="type"/> is WHOLLY in the default load context — its assembly and every
    /// constituent (element type, generic arguments, declaring type). A static cache keyed by a type
    /// that is not would pin that type's per-registry load context for the life of the process, so
    /// such a type is answered uncached (P54 Design Decision 3). A constructed generic needs the walk:
    /// <c>Sharpy.List&lt;Greeter&gt;</c>'s own assembly is Sharpy.Core, yet it pins Greeter's context.
    /// </summary>
    internal static bool IsProcessStable(Type type)
    {
        try
        {
            if (type.HasElementType)
                return type.GetElementType() is { } element && IsProcessStable(element);

            if (!IsProcessStable(type.Assembly))
                return false;

            if (type.IsGenericParameter)
                return true;

            if (type.IsConstructedGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (!IsProcessStable(argument))
                        return false;
                }
            }

            return type.DeclaringType is not { } declaring || IsProcessStable(declaring);
        }
        catch (NotSupportedException)
        {
            // A type that cannot describe its constituents is not cached.
            return false;
        }
    }

    /// <summary>Whether <paramref name="assembly"/> lives in the default load context and is not collectible.</summary>
    internal static bool IsProcessStable(Assembly assembly)
        => !assembly.IsCollectible && AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default;

    /// <summary>
    /// <c>cache.GetOrAdd(key, factory)</c> when <paramref name="keyType"/> is process-stable, else
    /// <c>factory(key)</c> uncached (Design Decision 3).
    /// </summary>
    internal static TValue GetOrAddIfProcessStable<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> cache, TKey key, Type keyType, Func<TKey, TValue> factory)
        where TKey : notnull
        => IsProcessStable(keyType) ? cache.GetOrAdd(key, factory) : factory(key);

    /// <summary>The Assembly-keyed twin of <see cref="GetOrAddIfProcessStable{TKey,TValue}"/>.</summary>
    internal static TValue GetOrAddIfProcessStable<TValue>(
        ConcurrentDictionary<Assembly, TValue> cache, Assembly key, Func<Assembly, TValue> factory)
        => IsProcessStable(key) ? cache.GetOrAdd(key, factory) : factory(key);

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/> only when <paramref name="keyType"/> is process-stable.</summary>
    internal static void SetIfProcessStable<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> cache, TKey key, Type keyType, TValue value)
        where TKey : notnull
    {
        if (IsProcessStable(keyType))
            cache[key] = value;
    }

    // ---- Names ----

    /// <summary>
    /// The simple name of <paramref name="assembly"/>, or null when it cannot describe itself — a
    /// transient assembly whose file has been replaced throws <see cref="BadImageFormatException"/>
    /// from <see cref="Assembly.GetName()"/> (#1840); one unreadable assembly is never a reason to
    /// fail a compilation.
    /// </summary>
    internal static string? SafeSimpleName(Assembly assembly)
    {
        try
        {
            return assembly.GetName().Name;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileNotFoundException
                                      or ReflectionTypeLoadException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The simple names of <paramref name="assemblies"/>, skipping any that cannot describe itself (#1840).</summary>
    internal static HashSet<string> SafeSimpleNames(IEnumerable<Assembly> assemblies)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            if (SafeSimpleName(assembly) is { } name)
                names.Add(name);
        }
        return names;
    }

    // ---- The framework set ----

    /// <summary>Whether a trusted-platform simple name belongs to the framework set.</summary>
    internal static bool IsFrameworkFileName(string simpleName)
        => simpleName is "System" or "mscorlib" or "netstandard"
           || simpleName.StartsWith("System.", StringComparison.Ordinal)
           || simpleName.StartsWith("Microsoft.", StringComparison.Ordinal);

    /// <summary>The framework index's size and first-use cost (Phase 2 task 6's record).</summary>
    internal static (int Assemblies, int Types, long Milliseconds) FrameworkIndexStatistics
        => (Framework.Assemblies.Length, Framework.TypeCount, Framework.BuildMilliseconds);

    private static TypeIndex BuildFrameworkIndex()
    {
        var stopwatch = Stopwatch.StartNew();
        var assemblies = new List<Assembly>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Include(Assembly? assembly)
        {
            if (assembly == null || assembly.IsDynamic)
                return;
            if (SafeSimpleName(assembly) is { } name && seen.Add(name))
                assemblies.Add(assembly);
        }

        // The compiler's own identities first: CoreLib (what the static Type.GetType searched first),
        // Sharpy.Core, the compiler itself, then its direct dependencies.
        Include(typeof(object).Assembly);
        Include(typeof(SharpyRT::Sharpy.Builtins).Assembly);
        var compiler = typeof(AssemblyVisibility).Assembly;
        Include(compiler);
        foreach (var dependency in compiler.GetReferencedAssemblies().OrderBy(n => n.Name, StringComparer.Ordinal))
            Include(LoadFrameworkAssembly(dependency));

        // The trusted-platform System.* / Microsoft.* / netstandard / mscorlib assemblies, in ordinal
        // name order — a process constant.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            var names = tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                .Where(n => n.Length > 0 && IsFrameworkFileName(n))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (!seen.Contains(name))
                    Include(LoadFrameworkAssembly(new AssemblyName(name)));
            }
        }

        var index = TypeIndex.Build(assemblies);
        stopwatch.Stop();
        return index with { BuildMilliseconds = stopwatch.ElapsedMilliseconds };
    }

    /// <summary>
    /// Loads a framework assembly by NAME into the default context — the trusted-platform binding,
    /// a process constant (the copy every by-name bind in the process gets).
    /// </summary>
    private static Assembly? LoadFrameworkAssembly(AssemblyName name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException
                                      or UnauthorizedAccessException or ArgumentException)
        {
            // An assembly that will not load contributes no type.
            return null;
        }
    }

    /// <summary>An immutable index over an ordered assembly list.</summary>
    private sealed record TypeIndex(
        ImmutableArray<Assembly> Assemblies,
        ImmutableDictionary<string, Assembly> BySimpleName,
        ImmutableDictionary<string, ImmutableArray<Type>> TypesByNamespace,
        ImmutableDictionary<(string Name, int Arity), ImmutableArray<Type>> GenericDefinitions,
        int TypeCount)
    {
        public long BuildMilliseconds { get; init; }

        public static TypeIndex Build(IEnumerable<Assembly> ordered)
        {
            var assemblies = ordered.ToImmutableArray();
            var bySimpleName = ImmutableDictionary.CreateBuilder<string, Assembly>(StringComparer.Ordinal);
            var byNamespace = new Dictionary<string, ImmutableArray<Type>.Builder>(StringComparer.Ordinal);
            var generics = new Dictionary<(string, int), ImmutableArray<Type>.Builder>();
            var typeCount = 0;

            foreach (var assembly in assemblies)
            {
                if (SafeSimpleName(assembly) is { } simple && !bySimpleName.ContainsKey(simple))
                    bySimpleName[simple] = assembly;

                foreach (var type in VisibleTypes(assembly))
                {
                    typeCount++;
                    if (type.IsPublic && type.Namespace is { } ns)
                    {
                        if (!byNamespace.TryGetValue(ns, out var inNamespace))
                            byNamespace[ns] = inNamespace = ImmutableArray.CreateBuilder<Type>();
                        inNamespace.Add(type);
                    }

                    if (type.IsGenericTypeDefinition)
                    {
                        var key = (ClrNameHelper.StripArity(type.Name), type.GetGenericArguments().Length);
                        if (!generics.TryGetValue(key, out var named))
                            generics[key] = named = ImmutableArray.CreateBuilder<Type>();
                        named.Add(type);
                    }
                }
            }

            return new TypeIndex(
                assemblies,
                bySimpleName.ToImmutable(),
                byNamespace.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable(), StringComparer.Ordinal),
                generics.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable()),
                typeCount);
        }

        /// <summary>
        /// The assembly's visible (public, or nested public) types; a partially loadable assembly
        /// contributes what loads.
        /// </summary>
        private static IEnumerable<Type> VisibleTypes(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Select(t => t!).ToArray();
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException
                                          or BadImageFormatException or NotSupportedException)
            {
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException inner)
                {
                    types = inner.Types.Where(t => t != null).Select(t => t!).ToArray();
                }
                catch (Exception inner) when (inner is FileNotFoundException or FileLoadException
                                                 or TypeLoadException or BadImageFormatException
                                                 or NotSupportedException)
                {
                    return Array.Empty<Type>();
                }
            }

            return types.Where(IsVisibleSafe);
        }

        private static bool IsVisibleSafe(Type type)
        {
            try
            {
                return type.IsVisible;
            }
            catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// One generation of the own set and the caches reached against it. Replaced wholesale when the
    /// own set grows, so no cached answer outlives the set it was computed from.
    /// </summary>
    private sealed class State
    {
        public State(ImmutableArray<Assembly> own)
        {
            Own = own;
            OwnIndex = new Lazy<TypeIndex>(() => TypeIndex.Build(own), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public ImmutableArray<Assembly> Own { get; }
        public Lazy<TypeIndex> OwnIndex { get; }
        public ConcurrentDictionary<string, Type?> ResolveCache { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<(string Name, int Arity), Type?> DisplayNameCache { get; } = new();
    }
}

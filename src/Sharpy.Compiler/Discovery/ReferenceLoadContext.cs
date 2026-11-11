using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// A load context for a compilation's references, keyed by its reference set (#2250, P54 Design
/// Decision 2, R-HI's "isolated registry per compilation"). A <c>ModuleRegistry</c> loads each
/// non-framework reference with <see cref="AssemblyLoadContext.LoadFromAssemblyPath"/> into a context
/// obtained here — never into the process's default context with <c>Assembly.LoadFrom</c> — so a
/// same-named assembly another compilation loaded from another path is that compilation's own copy,
/// invisible here, and "already loaded from another path" (the old SPY0306 arm) cannot occur.
///
/// <para><b>Keyed by the reference set.</b> A context is identified by its reference file's content
/// identity (<see cref="FileContentIdentity"/>: full path, length, last-write time and SHA-256 — the
/// convention <c>AssemblyCompiler.s_referenceCache</c> and the incremental cache share), the module
/// version ids of the images the registry loaded BEFORE it, in order, and the registry's module paths
/// at that time. Its contents are a pure function of that key, so
/// registries with identical inputs share one context (observably one context per compilation) and
/// any different input — another path, a rebuilt file — gets a different one. Sharing is what keeps
/// a host that analyses many documents (the LSP, the test suites) from loading one copy of the
/// standard library per analysis: loader memory is native, invisible to GC pressure, and a context
/// per registry ran Lsp.Tests out of memory. The key is applied incrementally, one reference at a
/// time, so loading is never deferred and no refusal moves.</para>
///
/// <para><b>One identity for the framework.</b> <see cref="Load"/> answers a dependency from the
/// references loaded before this one (part of the key) or this context's own loads, and otherwise
/// returns null — which defers to the default context, so Sharpy.Core and the framework keep the ONE
/// identity the compiler was built against. A dependency the default context cannot find is searched
/// for, by <see cref="AssemblyLoadContext.Resolving"/>, in the reference's own directory and the module
/// paths in the key — the directories <c>ModuleRegistry.ResolveAssemblyPath</c> searches, no wider; one
/// that resolves nowhere stays #2182's cell.</para>
///
/// <para><b>Collectible, weakly shared.</b> Registries hold their contexts strongly; the process-wide
/// share holds them weakly, so a context unloads once no registry uses it. A lookup that finds a dead
/// entry replaces it, and every creation prunes dead entries, so the share is bounded by the contexts
/// alive. The process-wide Type-keyed caches cache only process-stable types
/// (<see cref="AssemblyVisibility.IsProcessStable(System.Type)"/>, Design Decision 3), so nothing else
/// pins a context.</para>
///
/// <para><b>The file now at the path, not the image the process mapped from it.</b> The runtime keeps
/// one image per file path while any context holds it, so a new context loading a path a live context
/// already loaded is handed the OLD image even after the file was replaced: a rebuilt library bound
/// v1's exports in every later compile of the process (#2114's in-process cell, SPY0908 CS0117 where a
/// fresh process reports SPY0301), and the overload index built from that image was filed under v2's
/// content hash. <see cref="LoadCurrent"/> loads a path whose identity moved since this process first
/// loaded it from a copy of its current bytes, which no live context can hold an image of; the copies live
/// in a per-user, content-addressed, verified and pruned cache (<see cref="ReferenceImageCache"/>).</para>
/// </summary>
[ThreadSafe]
internal sealed class ReferenceLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// The share: reference-set key -> the context loaded for it, held weakly. Key-pure — a context's
    /// contents are a function of the files and directories its key names.
    /// </summary>
    private static readonly ConcurrentDictionary<string, WeakReference<ReferenceLoadContext>> s_shared =
        new(StringComparer.Ordinal);

    private static readonly object s_createGate = new();

    /// <summary>
    /// Path -> the identity of the file this process first loaded from it (<see cref="LoadCurrent"/>).
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> s_firstLoadedIdentity =
        new(StringComparer.Ordinal);

    private readonly IReadOnlyList<Assembly> _prior;
    private readonly IReadOnlyList<string> _searchDirectories;
    private readonly ConcurrentDictionary<string, Assembly> _own = new(StringComparer.Ordinal);
    private readonly FileContentIdentity _identities;
    private readonly Action<string>? _notice;

    private ReferenceLoadContext(string fullPath, IReadOnlyList<Assembly> prior, IReadOnlyList<string> modulePaths,
        FileContentIdentity identities, Action<string>? notice)
        : base("sharpy-references-" + Path.GetFileNameWithoutExtension(fullPath) + "-" + Guid.NewGuid().ToString("N"),
            isCollectible: true)
    {
        _prior = prior;
        _identities = identities;
        _notice = notice;
        var directories = new List<string>();
        if (Path.GetDirectoryName(fullPath) is { Length: > 0 } directory)
            directories.Add(directory);
        directories.AddRange(modulePaths);
        _searchDirectories = directories;
        Resolving += ResolveFromSearchDirectories;

        Reference = LoadCurrent(fullPath);
        if (AssemblyVisibility.SafeSimpleName(Reference) is { } simple)
            _own.TryAdd(simple, Reference);
    }

    /// <summary>The reference this context was created for.</summary>
    public Assembly Reference { get; }

    /// <summary>
    /// The context for the reference at <paramref name="fullPath"/>, given the references the registry
    /// loaded before it (in order) and its module paths: the shared one when the key's context is
    /// alive, else a new one. The compiler's one construction of a reference context.
    /// <paramref name="identities"/> is the compilation's file-identity scope (content, not size and
    /// write time); <paramref name="notice"/> receives non-fatal notices (the private-copy fallback).
    /// </summary>
    public static ReferenceLoadContext GetOrLoad(
        string fullPath, IReadOnlyList<Assembly> prior, IReadOnlyList<string> modulePaths,
        FileContentIdentity? identities = null, Action<string>? notice = null)
    {
        identities ??= new FileContentIdentity();
        var key = KeyOf(fullPath, prior, modulePaths, identities);
        lock (s_createGate)
        {
            if (s_shared.TryGetValue(key, out var weak) && weak.TryGetTarget(out var alive))
                return alive;

            // A dead entry (or none): prune every dead entry, then load and publish.
            foreach (var entry in s_shared)
            {
                if (!entry.Value.TryGetTarget(out _))
                    s_shared.TryRemove(entry.Key, out _);
            }

            var created = new ReferenceLoadContext(fullPath, prior, modulePaths, identities, notice);
            s_shared[key] = new WeakReference<ReferenceLoadContext>(created);
            return created;
        }
    }

    /// <summary>The share's keys (live and not yet pruned), for the retention probe.</summary>
    internal static IReadOnlyCollection<string> SharedKeys => s_shared.Keys.ToList();

    /// <summary>
    /// The share key: the reference file's content identity, the image identity (module version id) of
    /// each reference loaded before it — what was actually loaded, not the file now at its path — and
    /// the module paths.
    /// </summary>
    private static string KeyOf(string fullPath, IReadOnlyList<Assembly> prior, IReadOnlyList<string> modulePaths,
        FileContentIdentity identities)
        => identities.Of(fullPath)
           + "|" + string.Join(";", prior.Select(a => a.ManifestModule.ModuleVersionId.ToString("N")))
           + "|" + string.Join(";", modulePaths);

    /// <summary>
    /// Loads the file now at <paramref name="fullPath"/>. The path itself while its content identity is
    /// the one this process first loaded from it; otherwise a verified copy of its current bytes
    /// (<see cref="ReferenceImageCache.Publish"/>), because the runtime would answer the path with the
    /// image it already mapped (see the summary). A copy pruned between publishing and loading (another
    /// process's prune) is published again once.
    /// </summary>
    private Assembly LoadCurrent(string fullPath)
    {
        var identity = _identities.Of(fullPath);
        var first = s_firstLoadedIdentity.GetOrAdd(fullPath, identity);
        if (string.Equals(first, identity, StringComparison.Ordinal))
            return LoadFromAssemblyPath(fullPath);

        var copy = ReferenceImageCache.Publish(fullPath, notice: _notice);
        try
        {
            return LoadFromAssemblyPath(copy);
        }
        catch (FileNotFoundException)
        {
            return LoadFromAssemblyPath(ReferenceImageCache.Publish(fullPath, notice: _notice));
        }
    }

    /// <summary>
    /// A dependency named by a reference: a reference loaded before this one, or this context's own
    /// load, by simple name; else null — the default context answers (one framework / Sharpy.Core
    /// identity).
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } simple)
            return null;
        if (_own.TryGetValue(simple, out var own))
            return own;
        foreach (var prior in _prior)
        {
            if (string.Equals(AssemblyVisibility.SafeSimpleName(prior), simple, StringComparison.Ordinal))
                return prior;
        }
        return null;
    }

    private Assembly? ResolveFromSearchDirectories(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { Length: > 0 } simple)
            return null;

        foreach (var directory in _searchDirectories)
        {
            var candidate = Path.Combine(directory, simple + ".dll");
            if (!File.Exists(candidate))
                continue;
            try
            {
                var assembly = LoadCurrent(Path.GetFullPath(candidate));
                _own.TryAdd(simple, assembly);
                return assembly;
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            {
                // A candidate that will not load is not the dependency; keep looking.
            }
        }
        return null;
    }
}

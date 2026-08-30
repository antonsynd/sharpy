using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Sharpy.Compiler.Project;

namespace Sharpy.Compiler;

/// <summary>
/// Computes the transitive runtime dependency closure of a set of reference
/// assemblies by reading each assembly's <see cref="MetadataReader.AssemblyReferences"/>
/// and resolving every referenced simple name against sibling <c>.dll</c> files in the
/// referencing assembly's directory (breadth-first, to a fixpoint).
///
/// <para>
/// "Exists as a sibling file" is exactly the right shared-framework filter: a
/// framework-dependent build output contains only project and NuGet assemblies, never
/// shared-framework ones (<c>System.Runtime</c> and friends resolve from the runtime pack,
/// so they never appear as siblings and are naturally excluded).
/// </para>
///
/// <para>
/// A bounded companion pass additionally captures runtime-registration assemblies the reference
/// graph cannot reach — a bundle like SQLitePCLRaw registers its provider from a module
/// initializer that only runs if the assembly is deployed and listed, yet nothing references it.
/// See <see cref="RunCompanionPass"/>.
/// </para>
///
/// <para>
/// This replaces the two hand-maintained dependency lists that previously drifted apart
/// (<see cref="RuntimeClosure.ManagedAssemblies"/> feeds <c>deps.json</c> generation and the
/// CLI's runtime-dependency copy) with a single mechanical source of truth (#1084).
/// </para>
/// </summary>
internal static class RuntimeClosureResolver
{
    /// <summary>
    /// The resolved runtime closure. <see cref="ManagedAssemblies"/> are full paths to every
    /// managed assembly in the transitive closure (including the input references). Managed
    /// assemblies must be listed in <c>deps.json</c> for the host to resolve them into the
    /// trusted-platform-assembly set. <see cref="NativeAssets"/> are full paths to native
    /// libraries (e.g. SQLitePCLRaw's <c>e_sqlite3</c>) shipped under a closure member's
    /// <c>runtimes/&lt;rid&gt;/native/</c> layout; native probing always includes the app
    /// directory, so these are copied flat beside the output rather than declared.
    /// </summary>
    internal readonly record struct RuntimeClosure(
        IReadOnlyList<string> ManagedAssemblies,
        IReadOnlyList<string> NativeAssets);

    /// <summary>
    /// Resolve the transitive managed + native runtime closure of <paramref name="referencePaths"/>.
    /// Only inputs that exist on disk seed the walk; missing files are skipped silently.
    /// </summary>
    public static RuntimeClosure Resolve(IEnumerable<string> referencePaths)
    {
        // Keyed by full path (case-insensitive) so an assembly is visited at most once even
        // if it is referenced from several directories or under differing case.
        var closure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var closureSimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var directoryIndexCache = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        var referenceNamesCache = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.OrdinalIgnoreCase);

        void Add(string fullPath)
        {
            closure[fullPath] = fullPath;
            closureSimpleNames.Add(Path.GetFileNameWithoutExtension(fullPath));
            queue.Enqueue(fullPath);
        }

        foreach (var referencePath in referencePaths)
        {
            if (string.IsNullOrEmpty(referencePath))
                continue;

            var fullPath = Path.GetFullPath(referencePath);
            if (!File.Exists(fullPath) || closure.ContainsKey(fullPath))
                continue;

            Add(fullPath);
        }

        // Alternate forward (reference-graph) walking and the companion pass to a fixpoint: the
        // companion pass may add a runtime-registration assembly (e.g. SQLitePCLRaw.batteries_v2)
        // whose own references then pull further siblings forward (e.g. the native provider).
        var changed = true;
        while (changed)
        {
            DrainForwardReferences(queue, closure, directoryIndexCache, referenceNamesCache, Add);
            changed = RunCompanionPass(closure, closureSimpleNames, directoryIndexCache, referenceNamesCache, Add);
        }

        var managed = closure.Values
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var native = ResolveNativeAssets(managed);

        return new RuntimeClosure(managed, native);
    }

    /// <summary>
    /// Forward BFS: for every queued assembly, resolve each of its declared assembly references
    /// against sibling <c>.dll</c> files in the same directory and add any not yet in the closure.
    /// </summary>
    private static void DrainForwardReferences(
        Queue<string> queue,
        Dictionary<string, string> closure,
        Dictionary<string, Dictionary<string, string>> directoryIndexCache,
        Dictionary<string, IReadOnlyList<string>> referenceNamesCache,
        Action<string> add)
    {
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var directory = Path.GetDirectoryName(current);
            if (directory is null)
                continue;

            var index = GetDirectoryIndex(directory, directoryIndexCache);

            foreach (var referencedName in GetReferenceNames(current, referenceNamesCache))
            {
                if (index.TryGetValue(referencedName, out var siblingPath)
                    && !closure.ContainsKey(siblingPath))
                {
                    add(siblingPath);
                }
            }
        }
    }

    /// <summary>
    /// Discover runtime-registration companions that the reference graph cannot reach. Some NuGet
    /// bundles register themselves at load time rather than being referenced: nothing in the
    /// closure names <c>SQLitePCLRaw.batteries_v2</c>, yet it must ship (and be listed in
    /// <c>deps.json</c>) so its module initializer runs and calls <c>SetProvider</c>. A sibling is
    /// pulled in when it belongs to the same third-party package family as a closure member (shares
    /// the leading dotted name segment) and itself references that member. Umbrella vendor prefixes
    /// (<c>Microsoft</c>, <c>System</c>) and first-party <c>Sharpy.*</c> assemblies are excluded so
    /// the scan stays scoped to a handful of same-family siblings and never drags in unrelated
    /// assemblies (Roslyn, test frameworks). Returns whether anything was added.
    /// </summary>
    private static bool RunCompanionPass(
        Dictionary<string, string> closure,
        HashSet<string> closureSimpleNames,
        Dictionary<string, Dictionary<string, string>> directoryIndexCache,
        Dictionary<string, IReadOnlyList<string>> referenceNamesCache,
        Action<string> add)
    {
        var familyRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in closureSimpleNames)
        {
            if (IsFirstParty(name))
                continue;

            var segment = FirstSegment(name);
            if (IsUmbrellaVendor(segment))
                continue;

            familyRoots.Add(segment);
        }

        if (familyRoots.Count == 0)
            return false;

        // Materialize member directories before mutating the closure (Add updates closure.Values).
        var memberDirectories = closure.Values
            .Select(Path.GetDirectoryName)
            .Where(d => d is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var added = false;
        foreach (var directory in memberDirectories)
        {
            var index = GetDirectoryIndex(directory!, directoryIndexCache);
            foreach (var (simpleName, path) in index)
            {
                if (closure.ContainsKey(path) || IsFirstParty(simpleName))
                    continue;

                var segment = FirstSegment(simpleName);
                if (!familyRoots.Contains(segment))
                    continue;

                foreach (var referencedName in GetReferenceNames(path, referenceNamesCache))
                {
                    if (closureSimpleNames.Contains(referencedName)
                        && FirstSegment(referencedName).Equals(segment, StringComparison.OrdinalIgnoreCase))
                    {
                        add(path);
                        added = true;
                        break;
                    }
                }
            }
        }

        return added;
    }

    private static IReadOnlyList<string> GetReferenceNames(
        string assemblyPath,
        Dictionary<string, IReadOnlyList<string>> cache)
    {
        if (cache.TryGetValue(assemblyPath, out var cached))
            return cached;

        var names = ReadAssemblyReferenceNames(assemblyPath);
        cache[assemblyPath] = names;
        return names;
    }

    private static bool IsFirstParty(string simpleName) =>
        simpleName.StartsWith("Sharpy.", StringComparison.OrdinalIgnoreCase)
        || simpleName.Equals("Sharpy", StringComparison.OrdinalIgnoreCase);

    private static bool IsUmbrellaVendor(string firstSegment) =>
        firstSegment.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)
        || firstSegment.Equals("System", StringComparison.OrdinalIgnoreCase)
        || firstSegment.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
        || firstSegment.Equals("mscorlib", StringComparison.OrdinalIgnoreCase);

    private static string FirstSegment(string simpleName)
    {
        var dot = simpleName.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 ? simpleName.Substring(0, dot) : simpleName;
    }

    /// <summary>
    /// Index a directory's <c>.dll</c> files by their simple (file) name, case-insensitively,
    /// so assembly-reference names resolve to sibling paths without repeated directory scans.
    /// </summary>
    private static Dictionary<string, string> GetDirectoryIndex(
        string directory,
        Dictionary<string, Dictionary<string, string>> cache)
    {
        if (cache.TryGetValue(directory, out var cached))
            return cached;

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in SourceGlob.EnumerateArtifacts(directory, "*.dll"))
            {
                index[Path.GetFileNameWithoutExtension(file)] = file;
            }
        }
        catch
        {
            // An unreadable directory contributes no siblings; the closure simply stops here.
        }

        cache[directory] = index;
        return index;
    }

    /// <summary>
    /// Read the simple names of every assembly reference declared by the assembly at
    /// <paramref name="assemblyPath"/>. Non-managed or malformed files yield nothing.
    /// </summary>
    private static IReadOnlyList<string> ReadAssemblyReferenceNames(string assemblyPath)
    {
        var names = new List<string>();
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return names;

            var reader = peReader.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                var name = reader.GetString(reference.Name);
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
        }
        catch
        {
            // Native DLLs (no CLI metadata) and unreadable files are simply leaves.
        }

        return names;
    }

    /// <summary>
    /// Collect native assets shipped under <c>runtimes/&lt;rid&gt;/native/</c> beside any closure
    /// member, matching the current runtime identifier with portable fallbacks (e.g.
    /// <c>osx-arm64</c> → <c>osx</c> → <c>unix</c>). For each distinct member directory the most
    /// specific RID that ships native files wins; results are de-duplicated by file name.
    /// </summary>
    private static IReadOnlyList<string> ResolveNativeAssets(IReadOnlyList<string> managedAssemblies)
    {
        var result = new List<string>();
        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ridCandidates = GetRidCandidates(RuntimeInformation.RuntimeIdentifier).ToList();

        foreach (var member in managedAssemblies)
        {
            var directory = Path.GetDirectoryName(member);
            if (directory is null || !visitedDirectories.Add(directory))
                continue;

            var runtimesDirectory = Path.Combine(directory, "runtimes");
            if (!Directory.Exists(runtimesDirectory))
                continue;

            foreach (var rid in ridCandidates)
            {
                var nativeDirectory = Path.Combine(runtimesDirectory, rid, "native");
                if (!Directory.Exists(nativeDirectory))
                    continue;

                var nativeFiles = SourceGlob.EnumerateArtifacts(nativeDirectory, "*").ToList();
                if (nativeFiles.Count == 0)
                    continue;

                foreach (var file in nativeFiles)
                {
                    if (seenFileNames.Add(Path.GetFileName(file)))
                        result.Add(file);
                }

                // The most specific RID that ships native files is authoritative for this
                // directory; broader fallbacks would duplicate the same logical libraries.
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Produce runtime-identifier fallback candidates for <paramref name="rid"/>, from most
    /// specific to most general (e.g. <c>osx-arm64</c> → <c>osx</c> → <c>unix-arm64</c> →
    /// <c>unix</c> → <c>any</c>). De-duplicated, order preserved.
    /// </summary>
    internal static IEnumerable<string> GetRidCandidates(string rid)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in Enumerate(rid))
        {
            if (seen.Add(candidate))
                yield return candidate;
        }

        static IEnumerable<string> Enumerate(string rid)
        {
            if (string.IsNullOrEmpty(rid))
            {
                yield return "any";
                yield break;
            }

            yield return rid;

            var dash = rid.LastIndexOf('-');
            var os = dash >= 0 ? rid.Substring(0, dash) : rid;
            var arch = dash >= 0 ? rid.Substring(dash + 1) : string.Empty;

            // Strip an OS version portion, e.g. "osx.15" → "osx".
            var dot = os.IndexOf('.', StringComparison.Ordinal);
            var osBase = dot >= 0 ? os.Substring(0, dot) : os;

            if (arch.Length > 0)
                yield return $"{osBase}-{arch}";
            yield return osBase;

            var isWindows = osBase.StartsWith("win", StringComparison.OrdinalIgnoreCase);
            if (!isWindows)
            {
                if (arch.Length > 0)
                    yield return $"unix-{arch}";
                yield return "unix";
            }

            yield return "any";
        }
    }
}

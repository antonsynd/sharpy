using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Answers, at semantic time, whether the C# name a bracket attribute (<c>@[...]</c>) is emitted
/// as denotes a type that is actually in scope in the generated file.
/// </summary>
/// <remarks>
/// The emitter writes a bracket attribute's mangled spelling verbatim
/// (<c>RoslynEmitter.MangleBracketPart</c> -> <see cref="Shared.NameCasing.ResolveType"/>), so
/// a name that denotes nothing used to reach Roslyn and come back as CS0246 wrapped in SPY0908
/// — an "internal compiler error" for an ordinary typo (#1427). This resolver is the proof of
/// absence that lets <c>DecoratorValidator</c> refuse the name itself. All CLR inspection lives
/// here rather than in the validator, per the rule that reflection belongs to
/// <c>Discovery/</c> (see also <see cref="ClrTypeHelper"/>).
///
/// Two spellings are tried for every candidate namespace: the mangled name as written and the
/// same name with an <c>Attribute</c> suffix. The suffix is C#'s own attribute-name lookup rule,
/// not the emitter's — which is why <c>@[abstract]</c> comes back from Roslyn naming
/// <c>AbstractAttribute</c>.
///
/// The check is deliberately one-sided: it proves absence, never attribute-hood. A name that
/// resolves to a type which is not an <see cref="Attribute"/> subclass is left to Roslyn, because
/// the failure mode that matters here is refusing a bracket attribute that works today.
/// </remarks>
internal static class ClrAttributeResolver
{
    /// <summary>
    /// Namespaces every generated file brings into scope unconditionally, mirroring
    /// <c>RoslynEmitter.GenerateUsingDirectives</c>. <c>Xunit</c> is emitted only for modules that
    /// declare tests, but listing it unconditionally can only make this resolver more permissive,
    /// and over-permissiveness is the safe direction.
    /// </summary>
    private static readonly string[] AlwaysInScopeNamespaces =
    {
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Threading.Tasks",
        "Sharpy",
        "Xunit",
    };

    /// <summary>
    /// Cache of full type name -> "some loaded assembly declares it". Content-addressed: the
    /// answer is a function of the key and the loaded assembly set, and the one event that can
    /// change the latter (<see cref="EnsureFrameworkAssembliesLoaded"/>) clears the cache.
    /// TODO(#1493): that premise fails for OTHER load events — ModuleRegistry.LoadReference (or
    /// any later assembly load) never clears this, so a long-lived process (the LSP) can serve a
    /// stale "absent" verdict forever. Scope per compilation like TypeChecker._bclMemberAbsenceMemo,
    /// or invalidate on the AppDomain's loaded-assembly count.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> _typeExistsCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Namespaces whose shared-framework assemblies have already been pulled in. Monotonic and
    /// idempotent, the same shape as <c>ModuleRegistry._loadedRuntimeNamespaces</c>: it gates a
    /// one-time load side effect, never per-compile output.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> _probedNamespaces = new(StringComparer.Ordinal);

    /// <summary>
    /// Every full type name the generated attribute usage could bind to, most likely first:
    /// the mangled name as written (which is already fully qualified for a dotted bracket
    /// attribute), then each in-scope namespace, each in both the bare and <c>Attribute</c>-suffixed
    /// spelling. Exposed so the refusal diagnostic can name what was attempted.
    /// </summary>
    internal static IReadOnlyList<string> CandidateTypeNames(
        string mangledName,
        IEnumerable<string>? importedNamespaces = null)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddPair(string namespaceName)
        {
            var prefix = namespaceName.Length == 0 ? "" : namespaceName + ".";
            var bare = prefix + mangledName;
            var suffixed = bare + "Attribute";
            if (seen.Add(bare))
                candidates.Add(bare);
            if (seen.Add(suffixed))
                candidates.Add(suffixed);
        }

        AddPair("");
        foreach (var ns in AlwaysInScopeNamespaces)
            AddPair(ns);
        if (importedNamespaces != null)
        {
            foreach (var ns in importedNamespaces)
            {
                if (!string.IsNullOrEmpty(ns))
                    AddPair(ns);
            }
        }

        return candidates;
    }

    /// <summary>
    /// True when at least one candidate spelling of <paramref name="mangledName"/> names a type
    /// that exists. False is a proof of absence and is safe to refuse on.
    /// TODO(#1492): "safe to refuse on" overstates the evidence base — the proof consults loaded
    /// assemblies and the shared framework only, never the project's .spyproj References/
    /// PackageReferences (those are consumed first in Phase 7, AssemblyCompiler). An attribute
    /// type living only in a project-referenced assembly is falsely refused with SPY0495.
    /// </summary>
    internal static bool ResolvesToClrType(string mangledName, IEnumerable<string>? importedNamespaces = null)
    {
        // Materialized because the namespaces are walked twice (candidate construction, then the
        // second-chance load) and the caller may hand over a lazy sequence.
        var namespaces = importedNamespaces as IReadOnlyList<string> ?? importedNamespaces?.ToList();
        var candidates = CandidateTypeNames(mangledName, namespaces);

        foreach (var candidate in candidates)
        {
            if (TypeExists(candidate))
                return true;
        }

        // Nothing among the assemblies this process happens to have touched. The type may still
        // live in a shared-framework assembly nothing has loaded yet (System.ComponentModel.
        // Primitives, System.IO.FileSystem, ...), so pull those in and retry — the same second
        // chance ModuleRegistry.EnsureRuntimeAssembliesLoaded gives namespace imports. Only the
        // namespaces that can plausibly be unloaded are probed: the mangled name's own qualifier
        // and the file's namespace imports. The always-in-scope namespaces are already resident
        // in any process that got far enough to run a validator.
        var loadedSomething = false;
        var ownQualifier = mangledName.LastIndexOf('.');
        if (ownQualifier > 0)
            loadedSomething |= EnsureFrameworkAssembliesLoaded(mangledName.Substring(0, ownQualifier));
        if (namespaces != null)
        {
            foreach (var ns in namespaces)
            {
                if (!string.IsNullOrEmpty(ns))
                    loadedSomething |= EnsureFrameworkAssembliesLoaded(ns);
            }
        }

        if (!loadedSomething)
            return false;

        // New assemblies invalidate every cached "not found".
        _typeExistsCache.Clear();

        foreach (var candidate in candidates)
        {
            if (TypeExists(candidate))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The imported namespace REQUIRED to resolve <paramref name="mangledName"/> — the first of
    /// <paramref name="importedNamespaces"/> (C# spelling) whose <c>using</c> makes it resolve, and
    /// only when NO import-free spelling does (neither the bare/already-qualified name nor any
    /// always-in-scope namespace). Null when the name resolves without any import (so a redundant
    /// import is NOT counted as used) or does not resolve at all (#1429).
    ///
    /// <para>Kept separate from <see cref="ResolvesToClrType"/>, which answers the different
    /// question the SPY0495 refusal asks — "does it resolve anywhere". Both share the type cache and
    /// the second-chance framework load, so calling both costs one resolution's worth of reflection.</para>
    /// </summary>
    internal static string? RequiredImportNamespace(
        string mangledName, IEnumerable<string>? importedNamespaces)
    {
        var namespaces = importedNamespaces as IReadOnlyList<string> ?? importedNamespaces?.ToList();
        if (namespaces == null || namespaces.Count == 0)
            return null;

        var found = FindRequiredImportNamespace(mangledName, namespaces);
        if (found != null)
            return found;

        // Second chance: the required type may live in a shared-framework assembly nothing has
        // loaded yet. Pull in the imports' assemblies and retry (mirrors ResolvesToClrType).
        var loadedSomething = false;
        foreach (var ns in namespaces)
        {
            if (!string.IsNullOrEmpty(ns))
                loadedSomething |= EnsureFrameworkAssembliesLoaded(ns);
        }

        if (!loadedSomething)
            return null;

        _typeExistsCache.Clear();
        return FindRequiredImportNamespace(mangledName, namespaces);
    }

    private static string? FindRequiredImportNamespace(
        string mangledName, IReadOnlyList<string> importedNamespaces)
    {
        // Any import-free spelling resolving means no import is required — a redundant import that
        // merely re-imports an always-in-scope namespace stays (correctly) unused.
        if (NamespaceResolves("", mangledName))
            return null;
        foreach (var ns in AlwaysInScopeNamespaces)
        {
            if (NamespaceResolves(ns, mangledName))
                return null;
        }

        // Only an import can make it resolve — return the first that does.
        foreach (var ns in importedNamespaces)
        {
            if (!string.IsNullOrEmpty(ns) && NamespaceResolves(ns, mangledName))
                return ns;
        }

        return null;
    }

    private static bool NamespaceResolves(string namespaceName, string mangledName)
    {
        var prefix = namespaceName.Length == 0 ? "" : namespaceName + ".";
        return TypeExists(prefix + mangledName) || TypeExists(prefix + mangledName + "Attribute");
    }

    private static bool TypeExists(string fullName)
        => _typeExistsCache.GetOrAdd(fullName, static name => FindType(name) != null);

    private static Type? FindType(string fullName)
    {
        var direct = Type.GetType(fullName, throwOnError: false);
        if (direct != null)
            return direct;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var found = SafeGetType(assembly, fullName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Type? SafeGetType(Assembly assembly, string fullName)
    {
        try
        {
            return assembly.GetType(fullName, throwOnError: false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or BadImageFormatException)
        {
            // An assembly whose own references cannot be resolved cannot contribute a type.
            return null;
        }
    }

    /// <summary>
    /// Loads the trusted-platform assemblies whose simple name matches <paramref name="namespaceName"/>
    /// or one of its parents. Returns true when at least one new assembly was loaded.
    /// </summary>
    private static bool EnsureFrameworkAssembliesLoaded(string namespaceName)
    {
        if (namespaceName.Length == 0)
            return false;
        if (!_probedNamespaces.TryAdd(namespaceName, 0))
            return false;

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string tpaString)
            return false;

        // .NET does not distribute types along namespace lines (System.Net.Sockets.AddressFamily
        // lives in System.Net.Primitives.dll), so parent-namespace assemblies are matched too.
        var prefixes = new List<string> { namespaceName + ".", namespaceName };
        var parts = namespaceName.Split('.');
        for (var i = parts.Length - 1; i >= 2; i--)
            prefixes.Add(string.Join(".", parts, 0, i) + ".");

        var loadedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name != null)
                loadedNames.Add(name);
        }

        var loadedAny = false;
        foreach (var path in tpaString.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName) || loadedNames.Contains(fileName))
                continue;

            var matches = false;
            foreach (var prefix in prefixes)
            {
                if (fileName.StartsWith(prefix, StringComparison.Ordinal)
                    || string.Equals(fileName, prefix.TrimEnd('.'), StringComparison.Ordinal))
                {
                    matches = true;
                    break;
                }
            }
            if (!matches)
                continue;

            try
            {
                Assembly.LoadFrom(path);
                loadedAny = true;
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            {
                // An assembly that will not load cannot contribute a type.
            }
        }

        return loadedAny;
    }
}

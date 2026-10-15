using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
internal sealed class ClrAttributeResolver
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
    /// The CLR types this compilation can see — the framework set plus its registry's references
    /// (#2250). Presence and absence are both answered from it, and it is a function of the
    /// compilation's inputs, so neither polarity can be another compilation's verdict. It replaces a
    /// process-wide presence cache (<c>s_typePresentCache</c>) and namespace-driven framework loading
    /// behind a process-wide gate (<c>_probedNamespaces</c>), both answered from whatever the process
    /// had loaded.
    /// </summary>
    private readonly AssemblyVisibility _visibility;

    /// <summary>
    /// ABSENCE verdicts, scoped to ONE resolver instance — that is, to one compilation.
    ///
    /// <para>#1493: an absence verdict that outlived its compilation was an SPY0495 that is
    /// order-dependent, silent, and unfixable by editing the file. Scoped here, it lives exactly as
    /// long as the facts it summarizes — the instance-scoped shape of
    /// <c>TypeChecker._bclMemberAbsenceMemo</c> (#1344).</para>
    /// </summary>
    private readonly HashSet<string> _typeAbsentThisCompilation = new(StringComparer.Ordinal);

    /// <summary>A resolver over <paramref name="visibility"/> — the compilation's own.</summary>
    internal ClrAttributeResolver(AssemblyVisibility visibility)
    {
        _visibility = visibility;
    }

    /// <summary>
    /// The project's reference closure, threaded in from the compilation that owns this resolver.
    /// <see cref="ReferenceClosure.Empty"/> for single-file and REPL shapes, where the proof
    /// behaves exactly as it did before #1492.
    /// </summary>
    private ReferenceClosure _references = ReferenceClosure.Empty;

    /// <summary>
    /// A metadata-only view of <see cref="_references"/>, built at most once per resolver and only
    /// when a name has already missed everywhere cheaper.
    /// </summary>
    private CSharpCompilation? _referenceProbe;
    private bool _referenceProbeBuilt;

    /// <summary>
    /// Points the absence proof at the project's references. Call before validation; changing the
    /// closure discards any probe built from the previous one.
    /// </summary>
    internal void SetReferenceClosure(ReferenceClosure references)
    {
        if (ReferenceEquals(_references, references))
            return;

        _references = references;
        _referenceProbe = null;
        _referenceProbeBuilt = false;
        // Verdicts reached without these references were reached against a smaller world.
        InvalidateAbsenceVerdicts();
    }

    /// <summary>
    /// True when this project carries references that could not be reduced to a probe-able path
    /// before validation runs. Callers must not refuse an absence they cannot prove (#1492).
    /// </summary>
    internal bool HasUnprobedReferences => _references.HasUnprobedReferences;

    /// <summary>
    /// Whether any REFERENCED assembly declares <paramref name="fullName"/>.
    ///
    /// <para>Read as metadata, never loaded into the execution context: probing must not have the
    /// side effect of loading a user assembly into the compiler's own process.</para>
    ///
    /// <para>DEVIATION from the plan's letter, same intent: the plan named
    /// <c>MetadataLoadContext</c>. Roslyn is used instead because it is already a dependency, it is
    /// how Phase 7 reads these exact assemblies (so the two phases agree by construction), and it
    /// needs no <c>PathAssemblyResolver</c> / core-assembly plumbing. Both are reflection-only;
    /// this one adds no NuGet dependency.</para>
    /// </summary>
    private bool ReferencedAssembliesDeclare(string fullName)
    {
        if (_references.AssemblyPaths.Count == 0)
            return false;

        if (!_referenceProbeBuilt)
        {
            _referenceProbeBuilt = true;
            _referenceProbe = BuildReferenceProbe(_references.AssemblyPaths);
        }

        return _referenceProbe?.GetTypeByMetadataName(fullName) != null;
    }

    private static CSharpCompilation? BuildReferenceProbe(IReadOnlyList<string> paths)
    {
        var references = new List<MetadataReference>();
        foreach (var path in paths)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            {
                // An assembly that cannot be read contributes no types. It also cannot prove an
                // absence — but the caller already treats a path that does not resolve as unprobed,
                // so nothing is refused on the strength of this miss.
            }
        }

        return references.Count == 0
            ? null
            : CSharpCompilation.Create("sharpy.reference.probe", references: references);
    }

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
    /// TODO(#1492): "safe to refuse on" overstates the evidence base — the proof consults the
    /// compilation's visibility (the framework set and the registry's references) and the project's
    /// reference closure; a PackageReference consumed only in Phase 7 (AssemblyCompiler) is not
    /// probed, so an attribute type living only there is falsely refused with SPY0495.
    /// </summary>
    internal bool ResolvesToClrType(string mangledName, IEnumerable<string>? importedNamespaces = null)
    {
        var namespaces = importedNamespaces as IReadOnlyList<string> ?? importedNamespaces?.ToList();
        foreach (var candidate in CandidateTypeNames(mangledName, namespaces))
        {
            if (TypeExists(candidate))
                return true;
        }

        // The framework set is complete — a process constant indexed once — so there is no second
        // chance to give: no later load can add a framework type it lacks (#2250).
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
    /// question the SPY0495 refusal asks — "does it resolve anywhere". Both read the compilation's
    /// visibility, so calling both costs one resolution's worth of reflection.</para>
    /// </summary>
    internal string? RequiredImportNamespace(
        string mangledName, IEnumerable<string>? importedNamespaces)
    {
        var namespaces = importedNamespaces as IReadOnlyList<string> ?? importedNamespaces?.ToList();
        if (namespaces == null || namespaces.Count == 0)
            return null;

        return FindRequiredImportNamespace(mangledName, namespaces);
    }

    private string? FindRequiredImportNamespace(
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

    private bool NamespaceResolves(string namespaceName, string mangledName)
    {
        var prefix = namespaceName.Length == 0 ? "" : namespaceName + ".";
        return TypeExists(prefix + mangledName) || TypeExists(prefix + mangledName + "Attribute");
    }

    private bool TypeExists(string fullName)
    {
        if (_typeAbsentThisCompilation.Contains(fullName))
            return false;

        // The compilation's visibility caches the answer per name (nulls too) and replaces that
        // cache when its own set grows (#2250).
        if (_visibility.ResolveType(fullName) != null)
            return true;

        // Last, because it is the most expensive and the least often needed: the project's own
        // reference closure, read as metadata (#1492).
        if (ReferencedAssembliesDeclare(fullName))
            return true;

        _typeAbsentThisCompilation.Add(fullName);
        return false;
    }

    /// <summary>
    /// Forgets this compilation's absence verdicts. Called when the reference closure changes, so a
    /// verdict reached against a smaller world is not replayed.
    /// </summary>
    private void InvalidateAbsenceVerdicts() => _typeAbsentThisCompilation.Clear();
}

using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// ImportResolver partial class: the Sharpy modules an import statement makes Python run, in Python's
/// order (plan-775c50 D5, P77 T5: an importer requires each at the import's position). Recorded here,
/// where the module resolver is, as file paths on the shared <see cref="SemanticBinding"/>; the
/// importer's <c>CodeGenInfoComputer</c> turns them into <see cref="SemanticInfo.GetImportRequirements"/>.
/// <para><b>Python's order</b> (CPython 3.15.2, measured on a package tree printing at import):
/// <c>import a.b.c</c> runs a, a.b, a.b.c; <c>import a.b as x</c> runs a, a.b; several aliases run
/// each chain in turn, a module once; <c>from a.b import n</c> runs a, a.b, then a.b.n when n is no
/// attribute of a.b and a.b.n is a submodule (python's <c>_handle_fromlist</c>); <c>from a.b import *</c>
/// runs a, a.b. A namespace package (a directory with no <c>__init__</c>) runs nothing.</para>
/// <para><b>Recorded.</b> Each such module, in that order, each once, that is a Sharpy source of this
/// compilation, or a Sharpy module imported by reference whose discovered class carries a load entry
/// (<see cref="ModuleRegistry.GetModuleLoadEntry"/>, read from <c>Sharpy.SharpyLoadEntryAttribute</c>
/// alone: every regenerated Sharpy.Stdlib module, a referenced Sharpy library; plan-775c50 D10).
/// <b>Not recorded:</b> a .NET module with no load entry (a plain .NET namespace, a hand-written C#
/// module: it has no Sharpy body to run); a synthetic or unresolved module; a namespace-only package (no
/// <c>__init__.spy</c>: nothing stands for it — its submodules are still recorded); a file outside
/// the compilation (no load entry exists); and the importing module itself (a package <c>__init__</c>
/// importing its own submodule is the module already running).</para>
/// <para>An ancestor package and a submodule named by a from-import are not import edges (no dependency
/// is recorded for them, so they add no cycle): <see cref="ImportRequirement.IsImported"/> is false, and
/// the importer reads their <c>HasSweep</c> through <see cref="ImportedStaticFacts.RequiredSweep"/>.</para>
/// </summary>
internal partial class ImportResolver
{
    /// <summary>
    /// Appends to <paramref name="into"/> the ancestor packages of <paramref name="dottedName"/>, outermost
    /// first, then the module itself, <paramref name="module"/> (null: the module stands for nothing).
    /// </summary>
    private void AppendImportChain(string dottedName, ImportRequirement? module, List<ImportRequirement> into)
    {
        // A relative name the resolver gave no canonical spelling for names no ancestor here.
        var parts = dottedName.StartsWith('.') ? new[] { dottedName } : dottedName.Split('.');
        for (var depth = 1; depth < parts.Length; depth++)
        {
            var ancestor = string.Join(".", parts, 0, depth);
            if (ResolveRequirement(ancestor, isImported: false) is { } requirement)
                AppendRequirement(requirement, into);
        }
        if (module != null)
            AppendRequirement(module, into);
    }

    /// <summary>
    /// The import statement's chain for a module that resolved as a .NET module: the Sharpy modules
    /// imported by reference along it, each by its load entry (D10).
    /// </summary>
    private void AppendByReferenceChain(string moduleName, List<ImportRequirement> into)
        => AppendImportChain(moduleName, ByReference(moduleName, isImported: true), into);

    /// <summary>
    /// The from-import's own chain (<see cref="AppendImportChain"/>), then each imported name that is no
    /// export of the module and resolves as its submodule, in the statement's order.
    /// </summary>
    private void RecordFromImportChain(FromImportStatement fromImport, string canonicalModule, ImportRequirement? module,
        ModuleInfo? moduleInfo)
    {
        var chain = new List<ImportRequirement>();
        AppendImportChain(canonicalModule, module, chain);
        if (!fromImport.ImportAll)
        {
            foreach (var imported in fromImport.Names)
            {
                if (moduleInfo != null && moduleInfo.ExportedSymbols.ContainsKey(imported.Name))
                    continue;
                if (ResolveRequirement(canonicalModule + "." + imported.Name, isImported: false) is { } submodule)
                    AppendRequirement(submodule, chain);
            }
        }
        _semanticBinding.SetImportChain(fromImport, chain);
    }

    /// <summary>
    /// The module a name resolves to, as a requirement: a Sharpy module imported by reference when
    /// discovery found its load entry; else nothing for a name that resolves to a .NET module first
    /// (the precedence <see cref="TryResolveNetModule"/> applies); else its Sharpy source, or nothing.
    /// </summary>
    private ImportRequirement? ResolveRequirement(string moduleName, bool isImported)
    {
        if (ByReference(moduleName, isImported) is { } byReference)
            return byReference;
        if (_moduleRegistry != null
            && ((_moduleRegistry.IsNetNamespace(moduleName) && !_moduleRegistry.IsAttributeDeclaredModule(moduleName))
                || _moduleRegistry.IsModuleLoaded(moduleName)))
            return null;
        return ResolveModuleWithResult(moduleName)?.FullPath is { } path ? ImportRequirement.Source(path, isImported) : null;
    }

    /// <summary>The by-reference requirement of <paramref name="moduleName"/>: its discovered load entry
    /// (D10, the attribute alone), or null.</summary>
    private ImportRequirement? ByReference(string moduleName, bool isImported)
        => _moduleRegistry?.GetModuleLoadEntry(moduleName) is { } entry
            ? new ImportRequirement(null, isImported, entry)
            : null;

    private void AppendRequirement(ImportRequirement requirement, List<ImportRequirement> into)
    {
        if (requirement.FilePath is { } path)
        {
            if (IsCompiledSource is { } isCompiled && !isCompiled(path))
                return;
            if (_currentModulePath != null && PathNormalizer.Normalize(_currentModulePath) == PathNormalizer.Normalize(path))
                return;
        }
        var existing = into.FindIndex(r => r.Key == requirement.Key);
        if (existing < 0)
            into.Add(requirement);
        else if (requirement.IsImported && !into[existing].IsImported)
            into[existing] = into[existing] with { IsImported = true };
    }
}

/// <summary>One module an import statement requires (plan-775c50 D5): a Sharpy source of this compilation
/// (<paramref name="FilePath"/>), or a Sharpy module imported by reference (<paramref name="LoadEntry"/>,
/// D10); and whether the statement imports it (for a source, a dependency edge, so it is checked before
/// the importer) or runs it as an ancestor package or a from-import's submodule.</summary>
public sealed record ImportRequirement(string? FilePath, bool IsImported, ModuleLoadEntry? LoadEntry = null)
{
    /// <summary>A Sharpy source requirement.</summary>
    public static ImportRequirement Source(string filePath, bool isImported) => new(filePath, isImported);

    /// <summary>The identity a statement's chain holds each module once by.</summary>
    internal string Key => FilePath is { } path ? "file:" + PathNormalizer.Normalize(path) : "clr:" + LoadEntry?.MembersClassFullName;
}

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
/// <para><b>Recorded.</b> Each such module that resolves to a Sharpy source of this compilation, in that
/// order, each once. <b>Not recorded:</b> a .NET module and every name of its chain (the alias whose
/// module resolves to .NET — Python has no twin of it); a synthetic or unresolved module; a
/// namespace-only package (no <c>__init__.spy</c>: there is no Sharpy body to run, so nothing stands
/// for it — its submodules are still recorded); a file outside the compilation (no load entry exists);
/// and the importing module itself (a package <c>__init__</c> importing its own submodule is the
/// module already running).</para>
/// <para>An ancestor package and a submodule named by a from-import are not import edges (no dependency
/// is recorded for them, so they add no cycle): <see cref="ImportRequirement.IsImported"/> is false, and
/// the importer reads their <c>HasSweep</c> through <see cref="ImportedStaticFacts.RequiredSweep"/>.</para>
/// </summary>
internal partial class ImportResolver
{
    /// <summary>
    /// Appends to <paramref name="into"/> the ancestor packages of <paramref name="dottedName"/>, outermost
    /// first, then the module itself at <paramref name="modulePath"/> (an import edge).
    /// </summary>
    private void AppendImportChain(string dottedName, string modulePath, List<ImportRequirement> into)
    {
        // A relative name the resolver gave no canonical spelling for names no ancestor here.
        var parts = dottedName.StartsWith('.') ? new[] { dottedName } : dottedName.Split('.');
        for (var depth = 1; depth < parts.Length; depth++)
        {
            var ancestor = string.Join(".", parts, 0, depth);
            if (ResolveSharpyModuleFile(ancestor) is { } ancestorPath)
                AppendRequirement(ancestorPath, isImported: false, into);
        }
        AppendRequirement(modulePath, isImported: true, into);
    }

    /// <summary>
    /// The from-import's own chain (<see cref="AppendImportChain"/>), then each imported name that is no
    /// export of the module and resolves as its submodule, in the statement's order.
    /// </summary>
    private void RecordFromImportChain(FromImportStatement fromImport, string canonicalModule, string modulePath,
        ModuleInfo? moduleInfo)
    {
        var chain = new List<ImportRequirement>();
        AppendImportChain(canonicalModule, modulePath, chain);
        if (!fromImport.ImportAll)
        {
            foreach (var imported in fromImport.Names)
            {
                if (moduleInfo != null && moduleInfo.ExportedSymbols.ContainsKey(imported.Name))
                    continue;
                if (ResolveSharpyModuleFile(canonicalModule + "." + imported.Name) is { } submodulePath)
                    AppendRequirement(submodulePath, isImported: false, chain);
            }
        }
        _semanticBinding.SetImportChain(fromImport, chain);
    }

    /// <summary>
    /// The Sharpy source a module name resolves to, or null for a name that resolves to a .NET module
    /// first (the precedence <see cref="TryResolveNetModule"/> applies) or to no source at all.
    /// </summary>
    private string? ResolveSharpyModuleFile(string moduleName)
    {
        if (_moduleRegistry != null
            && ((_moduleRegistry.IsNetNamespace(moduleName) && !_moduleRegistry.IsAttributeDeclaredModule(moduleName))
                || _moduleRegistry.IsModuleLoaded(moduleName)))
            return null;
        return ResolveModuleWithResult(moduleName)?.FullPath;
    }

    private void AppendRequirement(string path, bool isImported, List<ImportRequirement> into)
    {
        var normalized = PathNormalizer.Normalize(path);
        if (IsCompiledSource is { } isCompiled && !isCompiled(path))
            return;
        if (_currentModulePath != null && PathNormalizer.Normalize(_currentModulePath) == normalized)
            return;
        var existing = into.FindIndex(r => PathNormalizer.Normalize(r.FilePath) == normalized);
        if (existing < 0)
            into.Add(new ImportRequirement(path, isImported));
        else if (isImported && !into[existing].IsImported)
            into[existing] = into[existing] with { IsImported = true };
    }
}

/// <summary>One module an import statement requires (plan-775c50 D5): its source file, and whether the
/// statement imports it (a dependency edge, so it is checked before the importer) or runs it as an
/// ancestor package or a from-import's submodule.</summary>
public sealed record ImportRequirement(string FilePath, bool IsImported);

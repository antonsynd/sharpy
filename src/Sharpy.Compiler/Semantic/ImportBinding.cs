namespace Sharpy.Compiler.Semantic;

/// <summary>The import spelling that bound a module-level name (#2359).</summary>
internal enum ImportBindingForm
{
    /// <summary><c>import m as N</c> — <c>N</c> names the module <c>m</c>.</summary>
    ModuleAlias,

    /// <summary><c>import a</c> / <c>import a.b</c> — the root segment <c>a</c> names the package.</summary>
    ModuleRoot,

    /// <summary><c>from m import x</c> / <c>from m import x as N</c> (including <c>*</c>).</summary>
    FromImport,
}

/// <summary>
/// What an import bound under one module-level name: the spelling, the identity of what it bound
/// (a module's canonical name or file for a module form, the source module for a from-import), the
/// spelling a diagnostic quotes, and the importing file.
/// </summary>
/// <remarks>
/// Recorded on the <see cref="Scope"/> by <see cref="ImportResolver.BindImportName"/> — the one seam
/// every import spelling in both compile routes binds through — so a later binding of the same name
/// can tell an import from a declaration: two imports of one thing merge, anything else collides
/// (SPY0204, R-II). The type checker reads it too: a module variable declared after the imports
/// re-declares an imported name, whatever symbol kind the import bound.
/// </remarks>
internal sealed record ImportBinding(ImportBindingForm Form, string Identity, string Display, string? FilePath);

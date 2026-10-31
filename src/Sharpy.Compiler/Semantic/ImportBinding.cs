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
/// (a module's canonical name or file for a module form; for a from-import the DEFINING
/// <c>module.symbol</c> of the declaration it names — <see cref="ImportResolver.DefiningIdentityOf"/>,
/// R-JK: a re-export resolves to its declaration, so one declaration reached through two module
/// spellings is one identity; never the alias, and never the module alone), the spelling a
/// diagnostic quotes (<see cref="Display"/>: <c>"{module as spelled}.{exported name}"</c>), the
/// importing file, and the binding's site (the <c>ImportAlias</c> of an explicit spelling, the
/// statement of a star).
/// </summary>
/// <remarks>
/// Recorded on the <see cref="Scope"/> by <see cref="ImportResolver.BindImportName"/> — the one seam
/// every import spelling binds through (ProjectCompiler Phase 4) — so a later binding of the same name
/// can tell an import from a declaration: two imports of one thing merge, anything else collides
/// (SPY0204, R-II). Two from-imports of ONE module that bind DIFFERENT symbols collide because the
/// identity is what each import NAMES (K1, #2359); two imports of one declaration through different
/// modules merge because the identity is where that declaration is DEFINED (R-JK, #2389). The site lets a verdict land on the PRIOR import
/// when the ruling puts it there: an explicit import beside a star import is refused at the explicit
/// import whichever comes first (R-IZ). The type checker reads the record too: a module variable
/// declared after the imports re-declares an imported name, whatever symbol kind the import bound.
/// </remarks>
internal sealed record ImportBinding(
    ImportBindingForm Form, string Identity, string Display, string? FilePath,
    int Line, int Column, Text.TextSpan? Span)
{
    /// <summary>
    /// The name was bound by <c>from m import *</c>. A star-imported name is a default a module-level
    /// declaration of the file may take over (a module variable re-declares it, as before #2359), never
    /// a binding that refuses the declaration — the type checker's module-variable arm reads this.
    /// Against another IMPORT it is a binding like any other: an explicit import of a different symbol
    /// under a star-exported name is refused at the explicit import, in either order (R-IZ).
    /// </summary>
    public bool IsStar { get; init; }
}

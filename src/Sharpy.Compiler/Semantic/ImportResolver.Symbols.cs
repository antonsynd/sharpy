using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// ImportResolver partial class: symbol-level operations for from-imports and re-exports
/// (extracting re-exported symbols, building re-export symbols, cloning, and conflict-aware
/// symbol-table registration).
/// </summary>
internal partial class ImportResolver
{
    /// <summary>
    /// Extract re-exported symbols from a from-import statement.
    /// When a module does "from .submodule import func", func becomes an export of that module.
    /// </summary>
    private void ExtractReExportedSymbols(FromImportStatement fromImport, ModuleInfo moduleInfo)
    {
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.Name));
        _logger.LogDebug($"[ImportResolver] ExtractReExportedSymbols: from {fromImport.Module} import {importedNames}");
        _logger.LogDebug($"[ImportResolver]   In module: {Path.GetFileName(moduleInfo.Path)}");

        // Resolve the source module to get its exported symbols
        var sourceModulePath = ResolveModulePath(fromImport.Module, Path.GetDirectoryName(moduleInfo.Path));
        if (sourceModulePath == null)
        {
            _logger.LogDebug($"[ImportResolver]   Source module '{fromImport.Module}' not found during re-export extraction");
            return;
        }

        _logger.LogDebug($"[ImportResolver]   Source module path: {sourceModulePath}");

        // Load the source module to get its symbols
        var sourceModule = LoadModule(sourceModulePath, fromImport.LineStart, fromImport.ColumnStart);
        if (sourceModule == null)
        {
            _logger.LogDebug($"[ImportResolver]   Failed to load source module");
            return;
        }

        _logger.LogDebug($"[ImportResolver]   Source module exports: {string.Join(", ", sourceModule.ExportedSymbols.Keys)}");

        // The re-exports extend the module's export table only. No re-export record is written here: this
        // statement is the LOADER's own parse of the module, a node no reader keys on (ProjectCompiler
        // Phase 4 and the emitter key on the compilation unit's node); the live record is
        // ResolveFromImport's, which records a star's set even when empty (P21i D2, #2385).
        if (fromImport.ImportAll)
        {
            foreach (var (name, symbol) in sourceModule.ExportedSymbols)
            {
                if (!name.StartsWith("_"))
                {
                    moduleInfo.ExportedSymbols.Add(name, CreateReExportSymbol(symbol, fromImport));
                    _logger.LogDebug($"[ImportResolver]     Re-exporting (wildcard): {name}");
                }
            }
        }
        else
        {
            foreach (var importAlias in fromImport.Names)
            {
                var sourceName = importAlias.Name;
                var targetName = importAlias.AsName ?? importAlias.Name;

                if (sourceModule.ExportedSymbols.TryGetValue(sourceName, out var symbol))
                {
                    moduleInfo.ExportedSymbols.Add(targetName, CreateReExportSymbol(symbol, fromImport, targetName));

                    if (symbol is TypeSymbol typeSymbol)
                    {
                        _logger.LogDebug($"[ImportResolver]     Re-exporting type: {sourceName} -> {targetName}, Original DefiningModule: {typeSymbol.DefiningModule ?? "null"}");
                    }
                    else
                    {
                        _logger.LogDebug($"[ImportResolver]     Re-exporting: {sourceName} -> {targetName} ({symbol.Kind})");
                    }
                }
                else
                {
                    _logger.LogDebug($"[ImportResolver]     Symbol '{sourceName}' NOT FOUND in source module exports");
                }
            }
        }
    }

    /// <summary>
    /// Create a symbol for a re-exported item
    /// </summary>
    private Symbol CreateReExportSymbol(Symbol originalSymbol, FromImportStatement fromImport, string? newName = null)
    {
        var effectiveName = newName ?? originalSymbol.Name;

        // For the builtins module, preserve the registry's own symbol so
        // BuiltinRegistry.IsBuiltinSymbol stays true via reference identity (#1322).
        if (string.Equals(effectiveName, originalSymbol.Name, StringComparison.Ordinal)
            && string.Equals(fromImport.Module, "builtins", StringComparison.Ordinal))
            return originalSymbol;

        // An alias (`import x as y`) is a fresh binding the importer spelled — bare, since an import
        // alias records no escape — so it does not carry the declaration's backtick escape: with the
        // flag copied, `from lib import `zed` as z` refused its own bare `z` (SPY0200). The C# name
        // still follows the declaration (CodeGenInfoComputer reads the origin's). A TYPE alias keeps
        // the flag for now: its references are spelled by the emitter from the clone's escape.
        var isNameBacktickEscaped = string.Equals(effectiveName, originalSymbol.Name, StringComparison.Ordinal)
            && originalSymbol.IsNameBacktickEscaped;

        var result = originalSymbol switch
        {
            FunctionSymbol func => func with
            {
                Name = effectiveName,
                IsNameBacktickEscaped = isNameBacktickEscaped,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module,
                OriginSymbol = originalSymbol
            },
            TypeSymbol type => CreateReExportedTypeSymbol(type, fromImport, effectiveName),
            // Carry unless listed — the same shape as the function arm above (#1440). The hand-built
            // initializer this replaces restated fourteen of the twenty-one VariableSymbol facts, and
            // the seven it forgot (IsFinal, IsStatic, HasDefaultValue, Documentation,
            // DeprecationMessage, DeclarationSpan, DeclaringFilePath) were each a user-visible defect
            // on the from-import spelling alone: an imported @final field freely assignable, hover
            // with no docs, go-to-definition with nowhere to go.
            VariableSymbol var => var with
            {
                Name = effectiveName,
                IsNameBacktickEscaped = isNameBacktickEscaped,
                DeclarationLine = fromImport.LineStart,
                DeclarationColumn = fromImport.ColumnStart,
                IsReExport = true,
                OriginalModule = fromImport.Module,
                OriginSymbol = originalSymbol
            },
            _ => originalSymbol
        };

        return result;
    }

    /// <summary>
    /// Create a re-exported type symbol, properly tracking the DefiningModule through the re-export chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>with</c> expression restating ONLY what a re-export genuinely changes — its name, the
    /// position of the import statement that binds it, and the provenance fields. Everything else
    /// carries because a re-export IS the declaration, seen from another module.
    /// </para>
    /// <para>
    /// This inverts the default deliberately (#1440). The hand-built initializer it replaces named
    /// twenty-odd facts and dropped sixteen — <c>NestedTypes</c> (which silently un-did #1363 on this
    /// spelling), <c>Documentation</c>, <c>DeprecationMessage</c> (SPY0466 never fired),
    /// <c>IsMustUse</c> (SPY0480 never fired), <c>DeclarationSpan</c> (go-to-definition landed
    /// nowhere), <c>Events</c>, <c>IsStringEnum</c>, <c>IsDataclass</c>, the file paths and the rest.
    /// Each drop was invisible: nothing fails when a fact is simply not restated, and the same is
    /// true of every fact ADDED to <see cref="TypeSymbol"/> later. "Carry unless listed" is what
    /// makes that class of bug unrepresentable rather than merely fixed.
    /// </para>
    /// <para>
    /// Reference-typed members are ALIASED by <c>with</c>, not copied — <c>Interfaces</c> above all,
    /// and equally <c>Fields</c>/<c>Methods</c>/<c>Constructors</c>/<c>Properties</c>/
    /// <c>UnresolvedInterfaces</c> (#1403). That is the required behaviour, not a hazard: inheritance
    /// is materialized ONCE, onto whichever object <c>InheritanceResolver</c> reaches, so a defensive
    /// copy here would freeze the list at re-export time and leave the alias permanently
    /// interface-less when materialization runs afterwards. The lists are append-only during
    /// resolution and never mutated per alias, so sharing is the correctness condition.
    /// </para>
    /// <para>
    /// The result is still a NEW object, which is correct: a re-export is a distinct binding with its
    /// own name and position. Identity with the declaration is the in-source-set path's job
    /// (<c>ProjectCompiler.ResolveOwnExportedSymbol</c>, #1366/#1407/#1491), and where that applies
    /// this clone is never reached.
    /// </para>
    /// <para>
    /// <b>Mutation test</b> (performed 2026-08-14): replacing this <c>with</c> by a hand-built
    /// <c>new TypeSymbol { … }</c> restating eighteen fields reddens
    /// <c>SymbolFactMirrorConformanceTests.FactMirror_AgreesForEveryCell</c> on exactly the
    /// <c>::fromImport</c> cells of the facts the initializer forgot — <c>Documentation</c>,
    /// <c>DeprecationMessage</c>, <c>IsMustUse</c>, <c>IsDataclass</c>, <c>Events</c>,
    /// <c>Properties</c>, <c>IsStringEnum</c>. That is the guard measuring the property this
    /// expression exists for: not "the facts are right today" but "forgetting one is caught".
    /// </para>
    /// </remarks>
    private TypeSymbol CreateReExportedTypeSymbol(TypeSymbol originalType, FromImportStatement fromImport, string effectiveName)
    {
        var definingModule = originalType.DefiningModule ?? GetResolvedModulePath(fromImport) ?? fromImport.Module;

        _logger.LogDebug($"[ImportResolver] CreateReExportedTypeSymbol: {originalType.Name} -> {effectiveName}");
        _logger.LogDebug($"[ImportResolver]   Original DefiningModule: {originalType.DefiningModule ?? "null"}");
        _logger.LogDebug($"[ImportResolver]   Original IsReExport: {originalType.IsReExport}");
        _logger.LogDebug($"[ImportResolver]   New DefiningModule: {definingModule}");
        _logger.LogDebug($"[ImportResolver]   FromImport.Module: {fromImport.Module}");

        var reExported = originalType with
        {
            Name = effectiveName,
            DeclarationLine = fromImport.LineStart,
            DeclarationColumn = fromImport.ColumnStart,
            IsReExport = true,
            OriginalModule = fromImport.Module,
            DefiningModule = definingModule
        };

        // When the type is aliased (effectiveName != originalType.Name), store the original
        // name so code generation uses the correct C# type name (e.g., "Config" not "Cfg").
        if (!string.Equals(effectiveName, originalType.Name, StringComparison.Ordinal))
        {
            _semanticBinding.SetCodeGenInfo(reExported, new CodeGenInfo
            {
                CSharpName = originalType.ClrType?.Name ?? NameMangler.ToPascalCase(originalType.Name),
                OriginalName = effectiveName,
                ImportKind = ImportKind.FromImportWithAlias,
                OriginalImportName = originalType.Name
            });
        }

        return reExported;
    }

    /// <summary>
    /// Gets the resolved module path from SemanticBinding or the AST property.
    /// </summary>
    private string? GetResolvedModulePath(FromImportStatement fromImport)
    {
        return _semanticBinding.GetResolvedModulePath(fromImport)
            ?? fromImport.ResolvedModulePath;
    }

    /// <summary>
    /// A verdict <see cref="BindImportName"/> decided. The caller reports it at the binding's own
    /// location unless <see cref="At"/> names another import's site — the explicit import a star
    /// import collides with when the star comes second (R-IZ).
    /// </summary>
    internal readonly record struct ImportBindingDiagnostic(
        string Message, string Code, bool IsError, ImportBindingSite? At = null);

    /// <summary>A source site a verdict is reported at (an import's <c>ImportAlias</c> or statement).</summary>
    internal readonly record struct ImportBindingSite(int Line, int Column, Text.TextSpan? Span);

    /// <summary>
    /// Binds the module-level name an import introduces — THE seam for every import spelling
    /// (<c>import m as N</c>, <c>import a.b</c>, <c>from m import x [as N]</c>, <c>from m import *</c>)
    /// on the one compile route (<c>ProjectCompiler</c> Phase 4 — every command compiles through it).
    /// Returns true when the name was newly bound.
    /// </summary>
    /// <remarks>
    /// <para><b>An import binding is a module binding (R-II, #2359).</b> The module body is declared
    /// BEFORE imports bind (NameResolver ahead of import resolution; ProjectCompiler's Phase 3 ahead
    /// of Phase 4), so against a class, def, <c>type</c> alias, const or module property the import is
    /// always the later binding, whatever the source order. Its <c>TryDefine</c> failing used to be
    /// ignored at four sites — the declaration silently won, or both reached C# and collided there
    /// (CS0117 / CS1537 / CS0119 behind SPY0908). The verdict on a failed bind, by what holds the
    /// name: the same declaration again → nothing; two plain imports of one root (<c>import os</c> +
    /// <c>import os.path</c>, either order) → the exports merge; the same module or the same
    /// exported SYMBOL again → nothing (idempotent — a from-import's identity is
    /// <c>"{module}.{exported name}"</c>, so <c>from math import *</c> + <c>from math import pi</c>
    /// is one binding); a from-import of a different symbol, from another module OR THE SAME one
    /// (K1, #2359) → SPY0204 "already names 'm.x'"; another module → SPY0204 "already bound to
    /// module"; a module-level declaration → SPY0204 "already defined in this module (kind, line)".
    /// A refused import binds NOTHING, so the declaration keeps its meaning. A module VARIABLE is the
    /// one module binding declared AFTER the imports (by the type checker), so against it the import
    /// binds and the variable is refused (the checker's SPY0204 at the variable).</para>
    /// <para><b>A star import</b> (<c>from m import *</c>) never refuses a module-level declaration:
    /// a declaration of the file that spells a star-exported name takes the name over (nothing is
    /// reported here, and a module variable re-declares a star-imported variable, see
    /// <see cref="ImportBinding.IsStar"/>). Against another IMPORT it collides like any binding, and
    /// the verdict lands on the EXPLICIT import (R-IZ): an explicit import of a different symbol
    /// under a star-exported name is SPY0204 at its <c>ImportAlias</c> whichever comes first — when
    /// the star comes second the verdict carries the prior binding's recorded site
    /// (<see cref="ImportBindingDiagnostic.At"/>), with no reordering of the binding pass. Two stars
    /// exporting one name from different sources are SPY0204 at the second star.</para>
    /// <para><b>Builtin names</b>, decided by <see cref="BuiltinNameShadowing.Classify(Registry.BuiltinRegistry, string, bool, bool)"/>
    /// as a TYPE-namespace binding (an import is visible in annotation position): a builtin type name
    /// or tagged-union constructor under an ALIAS the importer chose (<c>import math as str</c>,
    /// <c>from math import pi as str</c>) → SPY0212, not bound (the backtick-escaped alias is the
    /// opt-in); the same name bound by a PLAIN spelling (<c>from mathutils import double</c> — the
    /// exporter's name) and a builtin function name → SPY0484, bound (the rebinding is honoured,
    /// #1324) — for the module spellings as well as the from-import ones. The name a <c>from builtins import len</c> binds is
    /// the registry's own symbol (#1322) and shadows nothing: <paramref name="isRegistryOwnBinding"/>
    /// skips the verdict. A star import's builtin names are recorded where they are used, not here
    /// (#1324), so <paramref name="alias"/> is null for one and the verdict is skipped.</para>
    /// <para>Every successful bind is recorded on the scope (<see cref="Scope.RecordImportBinding"/>)
    /// so the next binding of the name — and the checker's module-variable declaration — can tell an
    /// import from a declaration.</para>
    /// </remarks>
    internal static bool BindImportName(
        SymbolTable symbolTable,
        Symbol symbol,
        string registerName,
        ImportAlias? alias,
        ImportBinding binding,
        string? warningTarget,
        bool isRegistryOwnBinding,
        Action<ImportBindingDiagnostic> report)
    {
        // A builtin function name's SPY0484 says the import TOOK EFFECT ("a bare 'print' here names the module"),
        // so it is reported only once the name is bound: a refused import draws its SPY0204 alone, an idempotent
        // re-import (nothing newly bound) draws nothing — the first import already warned.
        ImportBindingDiagnostic? warning = null;
        if (alias != null && !isRegistryOwnBinding)
        {
            // The escape of the spelling that binds: the `as` name, or the plain spelling's first
            // segment (`import a.b` binds `a`; a from-import item is one segment).
            var escaped = alias.AsName != null
                ? alias.IsAsNameBacktickEscaped
                : alias.BacktickEscapedParts.Length > 0 && alias.BacktickEscapedParts[0];
            var verdict = BuiltinNameShadowing.Classify(symbolTable.BuiltinRegistry, registerName, escaped,
                isTypeDeclaration: true);
            // The refusal is for a name the IMPORTER chose (R-II: "an import alias"). A plain spelling
            // binds the exporter's name — `from mathutils import double` — and refusing it would punish
            // the importer for the library's naming, so it is warned and honoured like `def double`
            // (owner ruling at P21f Phase 4; the one-way tightening to refuse every spelling stays open).
            if (verdict == BuiltinShadowVerdict.Refused && alias.AsName == null)
                verdict = BuiltinShadowVerdict.Warned;
            switch (verdict)
            {
                case BuiltinShadowVerdict.Refused:
                    report(new ImportBindingDiagnostic(BuiltinNameShadowing.ImportRefusalMessage(registerName),
                        BuiltinNameShadowing.RefusalCode, IsError: true));
                    return false;
                case BuiltinShadowVerdict.Warned:
                    warning = new ImportBindingDiagnostic(
                        binding.Form == ImportBindingForm.FromImport
                            ? BuiltinNameShadowing.FromImportWarningMessage(registerName, warningTarget ?? registerName)
                            : BuiltinNameShadowing.ModuleImportWarningMessage(registerName, binding.Display),
                        DiagnosticCodes.Validation.BuiltinRebornByExplicitImport, IsError: false);
                    break;
            }
        }

        var scope = symbolTable.CurrentScope;
        if (symbolTable.TryDefine(symbol))
        {
            scope.RecordImportBinding(registerName, binding with { IsStar = alias == null });
            if (warning is { } bound)
                report(bound);
            return true;
        }

        var collision = CollisionMessage(scope.Lookup(registerName, searchParent: false),
            scope.LookupImportBinding(registerName), symbol, binding, registerName, isStar: alias == null);
        if (collision is { } refused)
            report(new ImportBindingDiagnostic(refused.Message, DiagnosticCodes.Semantic.DuplicateDefinition,
                IsError: true, refused.At));
        return false;
    }

    /// <summary>
    /// The SPY0204 text for an import whose name is already bound in its scope — with the site to
    /// report it at when that is not the incoming binding's own — or null when the second binding is
    /// the same thing again (see <see cref="BindImportName"/>). Merges a plain import's root into the
    /// one already bound as a side effect.
    /// </summary>
    private static (string Message, ImportBindingSite? At)? CollisionMessage(
        Symbol? existing, ImportBinding? prior, Symbol incoming, ImportBinding binding, string name, bool isStar)
    {
        if (existing == null)
            return null;

        // Two imports: decided by what each import NAMES (#514's rule: two sources re-exporting one declaration
        // still collide; K1: one module's two symbols collide), never by the bound symbols' identity.
        if (prior != null)
        {
            // `import os` + `import os.path` (either order): a plain import binds the ROOT segment, so two
            // plain imports of one name are one package — merge (first import wins, as the project route did).
            if (prior.Form == ImportBindingForm.ModuleRoot && binding.Form == ImportBindingForm.ModuleRoot)
            {
                if (existing is ModuleSymbol target && incoming is ModuleSymbol source)
                    target.Exports.MergeFrom(source.Exports, firstImportWins: true);
                return null;
            }

            if (existing is ModuleSymbol existingModule && incoming is ModuleSymbol incomingModule
                && IsSameModule(existingModule, incomingModule))
                return null;

            if (prior.Form == binding.Form && string.Equals(prior.Identity, binding.Identity, StringComparison.Ordinal))
                return null;

            // R-IZ: a star import and an explicit import of another symbol under one name — the EXPLICIT import is
            // refused, in either order; when the star comes second the verdict lands on the prior's recorded site.
            if (prior.IsStar != isStar)
            {
                var (explicitImport, starImport) = isStar ? (prior, binding) : (binding, prior);
                var starModule = SourceModuleOf(starImport, name);
                var message = $"'{name}' is imported explicitly ('{ExplicitSpelling(explicitImport, name)}', line "
                    + $"{explicitImport.Line}) and also exported by 'from {starModule} import *' (line {starImport.Line}); "
                    + $"write '{starModule}.{name}', or drop the star import";
                return (message, isStar ? new ImportBindingSite(prior.Line, prior.Column, prior.Span) : null);
            }

            // Two star imports of different sources export one name: refused at the second star.
            if (prior.IsStar && isStar)
                return ($"'{name}' is exported by both 'from {SourceModuleOf(prior, name)} import *' (line {prior.Line}) "
                    + $"and 'from {SourceModuleOf(binding, name)} import *'; drop one star import", null);

            if (prior.Form == ImportBindingForm.FromImport)
                return ($"'{name}' already names '{prior.Display}' (imported at line {prior.Line}); "
                    + $"import '{binding.Display}' under another name", null);

            return ($"'{name}' is already bound to module '{prior.Display}' in this module; import under another name",
                null);
        }

        // A star import never displaces a module-level declaration (unchanged; outside R-II's spellings).
        // Nor does a module importing its OWN declaration (the project route binds the declaration itself).
        if (isStar || ReferenceEquals(RootOrigin(existing), RootOrigin(incoming)))
            return null;

        var line = existing.EffectiveNameLine ?? existing.DeclarationLine;
        return ($"'{name}' is already defined in this module ({DeclarationKindWord(existing)}"
            + (line is { } l ? $", line {l}" : "") + "); import under another name", null);
    }

    /// <summary>The module a from-import binding names <paramref name="name"/> out of — its identity is
    /// <c>"{module}.{exported name}"</c>, and a star binds every export under its own name.</summary>
    private static string SourceModuleOf(ImportBinding fromImport, string name)
        => fromImport.Identity.EndsWith("." + name, StringComparison.Ordinal)
            ? fromImport.Identity[..^(name.Length + 1)]
            : fromImport.Identity;

    /// <summary>The source spelling of an explicit import binding <paramref name="name"/>, as a diagnostic quotes it.</summary>
    private static string ExplicitSpelling(ImportBinding binding, string name)
    {
        switch (binding.Form)
        {
            case ImportBindingForm.ModuleAlias:
                return $"import {binding.Display} as {name}";
            case ImportBindingForm.ModuleRoot:
                return $"import {binding.Display}";
            default:
                var dot = binding.Display.LastIndexOf('.');
                var (module, exported) = dot <= 0
                    ? (binding.Display, name)
                    : (binding.Display[..dot], binding.Display[(dot + 1)..]);
                return string.Equals(exported, name, StringComparison.Ordinal)
                    ? $"from {module} import {exported}"
                    : $"from {module} import {exported} as {name}";
        }
    }

    /// <summary>Two module bindings of one module: the same file or the same canonical module name.</summary>
    private static bool IsSameModule(ModuleSymbol a, ModuleSymbol b)
        => (a.FilePath.Length > 0 && string.Equals(a.FilePath, b.FilePath, StringComparison.Ordinal))
           || (a.CanonicalModuleName != null
               && string.Equals(a.CanonicalModuleName, b.CanonicalModuleName, StringComparison.Ordinal));

    /// <summary>The declaration a re-export or alias clone was made from (#1525's identity rule).</summary>
    private static Symbol RootOrigin(Symbol symbol)
    {
        var current = symbol;
        while (current.OriginSymbol is { } origin && !ReferenceEquals(origin, current))
            current = origin;
        return current;
    }

    /// <summary>The kind a SPY0204 import collision names for the module-level declaration it met.</summary>
    private static string DeclarationKindWord(Symbol symbol) => symbol switch
    {
        TypeSymbol { TypeKind: TypeKind.Struct } => "struct",
        TypeSymbol { TypeKind: TypeKind.Interface } => "interface",
        TypeSymbol { TypeKind: TypeKind.Enum } => "enum",
        TypeSymbol { TypeKind: TypeKind.Union } => "union",
        TypeSymbol { TypeKind: TypeKind.Delegate } => "delegate",
        TypeSymbol => "class",
        FunctionSymbol => "function",
        TypeAliasSymbol => "type alias",
        VariableSymbol { IsModuleProperty: true } => "property",
        VariableSymbol { IsConstant: true } => "constant",
        VariableSymbol => "variable",
        ModuleSymbol => "module",
        _ => "declaration",
    };
}

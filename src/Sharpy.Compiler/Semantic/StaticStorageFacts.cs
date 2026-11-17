using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The one classification of a module or static value's storage (plan-775c50 D2, rule N3):
/// <see cref="StaticStorage"/> and the generic-host flag, recorded by <c>CodeGenInfoComputer</c> on
/// <see cref="CodeGenInfo"/> (and on <see cref="PropertySymbol"/> for a class-level <c>@static</c>
/// property). Code generation reads the facts and never re-decides them.
/// </summary>
internal static class StaticStorageFacts
{
    /// <summary>
    /// A value's storage: a C# <c>const</c> (<paramref name="isCSharpConst"/>: a <c>const</c> whose
    /// initializer is compile-time foldable, the ONE test both emitter declaration paths read,
    /// <c>IsConst &amp;&amp; IsCompileTimeConstant</c>) is <see cref="StaticStorage.Const"/>; any other
    /// value with an initializer is <see cref="StaticStorage.Guarded"/>; a value with none is
    /// <see cref="StaticStorage.Plain"/>, as today (owner Q4).
    /// </summary>
    public static StaticStorage Classify(bool hasInitializer, bool isCSharpConst)
    {
        if (isCSharpConst)
            return StaticStorage.Const;
        return hasInitializer ? StaticStorage.Guarded : StaticStorage.Plain;
    }

    /// <summary>
    /// The storage of the property every <see cref="PropertyDef"/> in <paramref name="defs"/> declares
    /// (split accessors merge into one property): an auto-property default is a value with an
    /// initializer (<see cref="StaticStorage.Guarded"/>), an auto half with no default is
    /// <see cref="StaticStorage.Plain"/>, and a property made only of function-style accessors has
    /// no storage at all (null).
    /// </summary>
    public static StaticStorage? ForProperty(IEnumerable<PropertyDef> defs)
    {
        var autos = defs.Where(d => !d.IsFunctionStyle).ToList();
        if (autos.Count == 0)
            return null;
        return Classify(autos.Any(d => d.DefaultValue != null), isCSharpConst: false);
    }

    /// <summary>
    /// Whether a value declared in <paramref name="host"/> is stored per closed generic type: the
    /// host, or a type enclosing it, declares a type parameter. False for a module value (no host).
    /// </summary>
    public static bool IsGenericHost(TypeSymbol? host)
    {
        for (var type = host; type != null; type = type.DeclaringType)
        {
            if (type.IsGeneric)
                return true;
        }
        return false;
    }
}

/// <summary>
/// The one importer-side reader of what a declaring module recorded of itself (plan-775c50 D2; lead's
/// rulings on T2): its <c>HasSweep</c> and each module value's <see cref="StaticStorage"/>. Each Sharpy
/// module records both declarer-side, keyed by its normalized file path on the shared
/// <see cref="SemanticBinding"/>; an importer reads them here and nowhere else, so no fact is computed
/// twice.
/// <para><b>HasSweep.</b> A module has a sweep when it declares a <see cref="StaticStorage.Guarded"/>
/// value in any host, or imports (<c>import</c> or <c>from … import</c>) a module that has one —
/// transitive, so a value-less middle module still loads its own imports at its import position. The
/// record is persisted on the incremental cache's file entry and restored on a warm build; an importer
/// reads it through <see cref="ImportedSweep"/> into both carriers — the imported
/// <see cref="ModuleSymbol"/>'s <see cref="CodeGenInfo.HasSweep"/> and a from-import's
/// <see cref="ModuleLayout.HasSweep"/>.</para>
/// <para><b>An imported value's storage.</b> <see cref="ImportedStorage"/>: the declaration's own
/// CodeGenInfo when the imported symbol reaches it, else the declarer's keyed record — the bridge for a
/// module VARIABLE, whose cold export is not its declared symbol (findings ledger, P77 T2: the
/// identity split, candidate class P78 / #2423).</para>
/// </summary>
internal static class ImportedStaticFacts
{
    /// <summary>A module's own fact: it declares a Guarded value, or one of its imports has a sweep.</summary>
    public static bool HasSweep(bool declaresGuardedValue, bool anyImportHasSweep)
        => declaresGuardedValue || anyImportHasSweep;

    /// <summary>
    /// What an importer reads for the module at <paramref name="importedFilePath"/>: the declaring
    /// module's recorded fact. A module this compilation does not compile from Sharpy source has no
    /// record and reads false (a registry, stdlib or referenced module: D10's attributes, T8).
    /// </summary>
    /// <remarks>
    /// The conservative arm (lead's ruling on T2, 2026-11-17): a peer in a deferred import cycle is
    /// type-checked AFTER the member importing it (Kahn's order appends cycle files), so it has no
    /// record yet when the importer's facts freeze. It reads true: every Sharpy-compiled module emits
    /// a load entry even when its sweep is empty (binding on T5), and a load of a host already
    /// sweeping on the same thread returns at once (binding on T3 and T5), so a true here only ever
    /// adds a call that does nothing, never a wrong one.
    /// </remarks>
    public static bool ImportedSweep(SemanticBinding facts, string? importedFilePath, IReadOnlySet<string>? deferredCycleFiles)
    {
        if (string.IsNullOrEmpty(importedFilePath))
            return false;
        if (facts.GetModuleHasSweep(importedFilePath) is { } recorded)
            return recorded;
        // The conservative arm: a deferred-cycle peer not yet recorded (lead's ruling on T2).
        return deferredCycleFiles != null && deferredCycleFiles.Contains(PathNormalizer.Normalize(importedFilePath));
    }

    /// <summary>
    /// The <c>HasSweep</c> of one module an import statement requires (plan-775c50 D5): for a module the
    /// statement imports (a dependency edge, checked first), <see cref="ImportedSweep"/>; for an ancestor
    /// package or a from-import's submodule — a Sharpy source of this compilation that is no edge, so it
    /// may be checked after the importer — its record, else true. The conservative arm is the lead's
    /// T2 ruling's: every Sharpy-compiled module emits a load entry, and a load of a host already
    /// loaded or sweeping returns at once, so a true only ever adds a call that does nothing.
    /// </summary>
    public static bool RequiredSweep(SemanticBinding facts, ImportRequirement requirement, IReadOnlySet<string>? deferredCycleFiles)
        => requirement.IsImported && requirement.FilePath != null
            ? ImportedSweep(facts, requirement.FilePath, deferredCycleFiles)
            : requirement.FilePath is { } path ? facts.GetModuleHasSweep(path) ?? true : requirement.LoadEntry?.HasSweep ?? true;

    /// <summary>
    /// The storage facts of an imported value (<paramref name="imported"/>: an export, or a from-import's
    /// bound symbol or alias clone): its declaration's <see cref="CodeGenInfo.StaticStorage"/> and
    /// generic-host flag, so an importer picks the store and sweep arms the declaring module does.
    /// Null storage for anything that is no module or static value, and for a .NET module's member
    /// (D10's attributes, T8).
    /// </summary>
    public static (StaticStorage? Storage, bool IsOnGenericHost) ImportedStorage(SemanticBinding facts, Symbol imported)
    {
        var declaration = imported.DeclarationRoot();
        if (facts.GetCodeGenInfo(declaration) is { StaticStorage: { } storage } declared)
            return (storage, declared.IsOnGenericHost);

        // The identity bridge (findings ledger, P77 T2; candidate class P78 / #2423): in a cold build
        // a module VARIABLE's export is ModuleLoader's extracted symbol, not the declared one the
        // checker created, so its declaration's CodeGenInfo is unreachable from it; the declaring
        // module's keyed record answers instead. A module value has no host, so no generic host.
        // Delete this arm and SemanticBinding's module-value map once one symbol is both.
        if (declaration is VariableSymbol { DeclaringFilePath: { Length: > 0 } path }
            && facts.GetModuleValueStorage(path, declaration.Name) is { } recorded)
            return (recorded, false);

        return (null, false);
    }
}

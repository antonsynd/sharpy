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
/// The one predicate for a module's <c>HasSweep</c> (plan-775c50 D2, D5; lead's ruling on T2): a
/// module has a sweep when it declares a <see cref="StaticStorage.Guarded"/> value in any host, or
/// imports (<c>import</c> or <c>from … import</c>) a module that has one — transitive, so a
/// value-less middle module still loads its own imports at its import position. Each Sharpy module
/// records its own fact once, declarer-side, keyed by its file path on the shared
/// <see cref="SemanticBinding"/> (persisted on the incremental cache's file entry and restored on a
/// warm build); an importer reads it through <see cref="Imported"/> into both carriers — the
/// imported <see cref="ModuleSymbol"/>'s <see cref="CodeGenInfo.HasSweep"/> and a from-import's
/// <see cref="ModuleLayout.HasSweep"/> — and never computes it twice.
/// </summary>
internal static class ModuleSweepFacts
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
    public static bool Imported(SemanticBinding facts, string? importedFilePath, IReadOnlySet<string>? deferredCycleFiles)
    {
        if (string.IsNullOrEmpty(importedFilePath))
            return false;
        if (facts.GetModuleHasSweep(importedFilePath) is { } recorded)
            return recorded;
        // The conservative arm: a deferred-cycle peer not yet recorded (lead's ruling on T2).
        return deferredCycleFiles != null && deferredCycleFiles.Contains(PathNormalizer.Normalize(importedFilePath));
    }
}

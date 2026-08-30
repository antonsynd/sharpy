using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The compilation phase at which a feature takes effect. This determines how a
/// feature may be enabled:
/// <list type="bullet">
///   <item>
///     <b>Parser-scoped</b> features change how source text is tokenized or parsed.
///     Because import resolution is Pass 1.5 (it runs <i>after</i> parsing), a
///     <c>from __future__ import</c> statement can never enable a parser-scoped
///     feature — the syntax it would unlock has already been rejected by the time
///     the import is seen. Parser-scoped features may only be enabled compilation-wide
///     via <c>--enable-feature</c> or <c>&lt;Features&gt;</c> in a <c>.spyproj</c>.
///   </item>
///   <item>
///     <b>Semantic</b>- and <b>CodeGen</b>-scoped features affect only analysis or
///     emission and can therefore be enabled per-file via
///     <c>from __future__ import &lt;feature&gt;</c>, in addition to the two
///     compilation-wide mechanisms.
///   </item>
/// </list>
/// This asymmetry is intentional and is why <see cref="ImportResolver"/>'s
/// <c>__future__</c> handling rejects parser-scoped names with guidance to use the
/// compilation-wide flags instead.
/// </summary>
public enum FeatureScope
{
    /// <summary>Affects lexing/parsing — cannot be enabled via <c>from __future__ import</c>.</summary>
    Parser,

    /// <summary>Affects semantic analysis — may be enabled per-file via <c>from __future__ import</c>.</summary>
    Semantic,

    /// <summary>Affects code generation — may be enabled per-file via <c>from __future__ import</c>.</summary>
    CodeGen,
}

/// <summary>
/// Metadata describing a known experimental feature. Entries live in
/// <see cref="FeatureFlags.KnownFeatures"/>, which is the single source of truth for
/// which feature names are valid at any boundary (CLI, <c>.spyproj</c>, or
/// <c>from __future__ import</c>).
/// </summary>
/// <param name="Name">The canonical feature name as written by users.</param>
/// <param name="Description">A short human-readable description.</param>
/// <param name="Scope">The phase at which the feature takes effect (see <see cref="FeatureScope"/>).</param>
/// <param name="Hidden">
/// When true, the feature is omitted from user-facing <c>--help</c> listings. Used for
/// test-only plumbing features that ship disabled and undocumented.
/// </param>
/// <param name="IsNoOp">
/// When true, the feature has <b>graduated</b>: its gate has been removed and the construct is now
/// unconditional language surface, but the name is retained in <see cref="FeatureFlags.KnownFeatures"/>
/// so that existing <c>--enable-feature</c>, <c>&lt;Features&gt;</c>, and <c>from __future__ import</c>
/// sites keep compiling instead of erroring on a now-unknown name. Per the graduation policy
/// (docs/design/feature-lifecycle.md step 1: "The <c>FeatureInfo</c> is marked so tooling can note it
/// is a no-op"), feature-listing surfaces annotate no-op entries so users know the flag is safe to
/// remove. The name is dropped from <c>KnownFeatures</c> after at least one release as a no-op.
/// </param>
public sealed record FeatureInfo(string Name, string Description, FeatureScope Scope, bool Hidden = false, bool IsNoOp = false);

/// <summary>
/// An immutable set of enabled experimental feature names, carried on
/// <see cref="CompilerOptions.Features"/> and threaded through the compilation phases.
/// </summary>
/// <remarks>
/// A feature can be enabled per-invocation (<c>--enable-feature=x</c>), per-project
/// (<c>&lt;Features&gt;</c> in <c>.spyproj</c>), or — for semantic/codegen-scoped
/// features only — per-file (<c>from __future__ import x</c>). Unknown names are errors
/// at every boundary, never silent no-ops, so a typo cannot silently disable a feature.
/// See <see cref="FeatureScope"/> for the parser-vs-semantic scope asymmetry that governs
/// which mechanisms may enable a given feature.
/// <para>
/// <b>Storage:</b> This instance on <see cref="CompilerOptions.Features"/> is
/// <i>compilation-wide</i>. Per-file <c>from __future__ import</c> features cannot live
/// here (they must not leak across files), so they are stored separately on the
/// <see cref="ImportResolver"/>, keyed by module path, and unioned with the
/// compilation-wide set into the feature flags passed to the semantic phase for that
/// file. See <c>ImportResolver.GetFileFutureFeatures</c>.
/// </para>
/// </remarks>
public sealed class FeatureFlags
{
    private readonly ImmutableHashSet<string> _enabled;

    private FeatureFlags(ImmutableHashSet<string> enabled) => _enabled = enabled;

    /// <summary>An empty flag set with no features enabled.</summary>
    public static readonly FeatureFlags None = new(ImmutableHashSet<string>.Empty);

    /// <summary>
    /// The single source of truth for all known feature names. <c>__test_feature</c> (hidden
    /// from <c>--help</c>) exists so the enable-a-feature plumbing is testable end-to-end;
    /// real experimental features register here per docs/design/feature-lifecycle.md — the
    /// parser/semantic <i>syntax</i> features (<c>matmul</c>, <c>defer</c>,
    /// <c>property_observers</c>) and the CodeGen-scoped <i>behavioral</i> flags for the E3 IR
    /// optimization passes (<c>opt_const_fold</c>, <c>opt_comprehension_fusion</c>,
    /// <c>opt_stack_collections</c>; a disabled behavioral flag means its pass does not run).
    /// <c>opt_devirt</c> was evaluated and retired (the sealed collection types leave nothing to
    /// devirtualize that RyuJIT does not already do — see the retirement note below).
    /// </summary>
    public static IReadOnlyDictionary<string, FeatureInfo> KnownFeatures { get; } =
        new Dictionary<string, FeatureInfo>(StringComparer.Ordinal)
        {
            ["__test_feature"] = new FeatureInfo(
                "__test_feature",
                "Internal test-only feature used to validate the feature-flag plumbing. Not a real language feature.",
                FeatureScope.Semantic,
                Hidden: true),
            ["matmul"] = new FeatureInfo(
                "matmul",
                "Experimental `@` matrix-multiplication operator (PEP 465), including the `@=` " +
                "augmented assignment. Dispatches to __matmul__ / stdlib NdArray.",
                // Parser-scoped: `@` is always parsed but its use is a syntactic construct, so it
                // cannot be unlocked per-file via `from __future__ import` — only compilation-wide.
                FeatureScope.Parser),
            ["defer"] = new FeatureInfo(
                "defer",
                "Experimental `defer` statement for scope-exit cleanup. The deferred statement or " +
                "block runs on every exit path of its enclosing block (fall-through, return, break, " +
                "continue, exception) in reverse declaration order; lowers to nested try/finally.",
                // Parser-scoped: `defer` is a new statement syntax. It is always parsed but its use
                // is gated; a `from __future__ import` cannot unlock parser-scoped syntax.
                FeatureScope.Parser),
            // `failable_cast` graduated in #1096 and rode two releases (v0.8.0, v0.9.0) as a no-op; the
            // name was deleted from this registry in #1128 per the two-step retirement in
            // docs/design/feature-lifecycle.md. `--enable-feature failable_cast` is now an unknown-name
            // error — the `as?`/`as!` operators are unconditional language surface and need no flag.
            ["property_observers"] = new FeatureInfo(
                "property_observers",
                "Experimental `before_set` / `after_set` property observers (#416). An auto-property " +
                "may carry a `before_set(new_value):` and/or `after_set(old_value):` suite that runs " +
                "around every store to its backing field (including constructor assignments); lowers " +
                "to an expanded setter. Valid only on auto-properties with a setter.",
                // Parser-scoped: the observer suite is new statement syntax. Always parsed but gated;
                // a `from __future__ import` cannot unlock parser-scoped syntax.
                FeatureScope.Parser),
            // E3 IR optimization passes (#1057) — behavioral flags (Design Decision 5): they gate how a
            // valid program is compiled, not whether it parses, so they carry no GatedConstruct entry
            // and no SPY0331 rejection. CodeGen-scoped, so each is per-file enableable via
            // `from __future__ import`. Default-off; a disabled flag means the pass does not run.
            ["opt_const_fold"] = new FeatureInfo(
                "opt_const_fold",
                "Experimental constant-folding IR pass. Folds compile-time-constant arithmetic, " +
                "comparison, boolean (short-circuit), unary, and bitwise expressions to literals, " +
                "using exactly the emitted C#'s wrapping semantics (no folding of traps like x/0; " +
                "strings are not folded in v1 — recorded on #640).",
                FeatureScope.CodeGen),
            ["opt_comprehension_fusion"] = new FeatureInfo(
                "opt_comprehension_fusion",
                "Experimental generalized-preallocation IR pass. Presizes multi-clause " +
                "comprehensions over sized, effect-free sources (product capacity). Fusion of " +
                "comprehension chains is not yet implemented — see #1102.",
                FeatureScope.CodeGen),
            // opt_devirt was evaluated (E3 Phase 8, #1057) and RETIRED before shipping a pass:
            // Sharpy.List/Dict/Set are `sealed`, so RyuJIT already devirtualizes every call on them and
            // the emitter already emits direct calls on concrete receivers — the pass would produce
            // byte-identical output. The scope is obsolete, not deferred. Recorded as E4 plateau
            // evidence (a sealed-collection design leaves no devirt headroom for a custom IL backend).
            ["opt_stack_collections"] = new FeatureInfo(
                "opt_stack_collections",
                "Experimental non-escaping-collection IR pass. Collection literals that provably never " +
                "escape (v1: iterated directly by a for only; len()/constant-index positions are " +
                "deferred to #1103) lower to raw arrays instead of Sharpy.List allocations.",
                FeatureScope.CodeGen),
            // #1428/#1614 in-place augmented assignment on collections — graduated 2026-08-29: the
            // mutation-call lowering is the language on every TFM, SPY0478 is retired, and the flag is
            // a no-op kept for one release so existing .features files keep parsing (#1679).
            ["inplace_augassign"] = new FeatureInfo(
                "inplace_augassign",
                "Graduated (#1614): in-place augmented assignment on collections is now the default. " +
                "`xs += ys` / `s |= t` / `d |= e` on list/set/dict mutate the receiver in place " +
                "(CPython `__iadd__`-family semantics). Safe to remove from .features files.",
                FeatureScope.Semantic,
                IsNoOp: true),
        };

    /// <summary>The names of all enabled features, in ordinal order.</summary>
    public IEnumerable<string> EnabledFeatures => _enabled.OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>Returns true if <paramref name="feature"/> is enabled in this set.</summary>
    public bool IsEnabled(string feature) => _enabled.Contains(feature);

    /// <summary>
    /// Returns a new <see cref="FeatureFlags"/> with the given feature names added.
    /// This instance is not modified. Names are not validated here — validate at the
    /// boundary with <see cref="TryValidate"/> before enabling.
    /// </summary>
    public FeatureFlags Enable(IEnumerable<string> features)
    {
        if (features is null)
            throw new ArgumentNullException(nameof(features));

        var updated = _enabled.Union(features);
        return updated == _enabled ? this : new FeatureFlags(updated);
    }

    /// <summary>
    /// Returns a new <see cref="FeatureFlags"/> with the given feature name added.
    /// </summary>
    public FeatureFlags Enable(string feature) => Enable(new[] { feature });

    /// <summary>
    /// Validates a feature name against <see cref="KnownFeatures"/>. Returns true when the
    /// name is known; otherwise returns false and sets <paramref name="error"/> to a message
    /// listing the known (non-hidden) feature names.
    /// </summary>
    public static bool TryValidate(string name, out string? error)
    {
        if (KnownFeatures.ContainsKey(name))
        {
            error = null;
            return true;
        }

        error = $"Unknown feature '{name}'. {KnownFeatureListMessage()}";
        return false;
    }

    /// <summary>
    /// Returns a human-readable list of known, user-visible feature names for error
    /// messages, or a note that there are none.
    /// </summary>
    public static string KnownFeatureListMessage() => DescribeFeatures(KnownFeatures.Values);

    /// <summary>
    /// Formats an arbitrary feature set for <see cref="KnownFeatureListMessage"/>. Split out so the
    /// no-op annotation stays testable even when no registered feature is currently mid-graduation
    /// (the registry holds no <see cref="FeatureInfo.IsNoOp"/> entry between graduations).
    /// </summary>
    internal static string DescribeFeatures(IEnumerable<FeatureInfo> features)
    {
        // No-op (graduated) features are annotated so the listing tells users the flag is safe to
        // delete — see FeatureInfo.IsNoOp and docs/design/feature-lifecycle.md.
        var visible = features
            .Where(f => !f.Hidden)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => f.IsNoOp ? $"{f.Name} (no-op — graduated, safe to remove)" : f.Name)
            .ToList();

        return visible.Count == 0
            ? "There are no available features."
            : $"Known features: {string.Join(", ", visible)}.";
    }
}

using System;
using System.Collections.Generic;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Describes a syntactic construct that is only permitted when a specific experimental
/// feature is enabled. A construct is identified structurally by a predicate over AST
/// nodes so that a single node type (e.g. <see cref="BinaryOp"/>) can be gated for only
/// some of its shapes (e.g. only the <c>@</c> matrix-multiplication operator).
/// </summary>
/// <param name="Feature">
/// The feature name that unlocks this construct. Must be a key in
/// <see cref="Shared.FeatureFlags.KnownFeatures"/>; its <see cref="Shared.FeatureScope"/>
/// determines whether the diagnostic suggests <c>from __future__ import</c>.
/// </param>
/// <param name="Description">
/// A short, user-facing noun phrase naming the construct, used to open the SPY0331
/// message — e.g. <c>"the '@' matrix-multiplication operator"</c> or
/// <c>"the 'defer' statement"</c>.
/// </param>
/// <param name="Matches">
/// Returns true when <paramref name="Matches"/>'s argument is an occurrence of this
/// gated construct.
/// </param>
public sealed record GatedConstruct(string Feature, string Description, Func<Node, bool> Matches);

/// <summary>
/// The single source of truth mapping gated syntactic constructs to the experimental
/// feature that unlocks them. <see cref="FeatureGateChecker"/> walks each module against
/// this list after import resolution and reports
/// <see cref="Diagnostics.DiagnosticCodes.Semantic.FeatureNotEnabled"/> (SPY0331) for any
/// ungated usage.
/// </summary>
/// <remarks>
/// Follow-up pilot features register their constructs here — one entry each — so gating
/// stays data-driven and lives in exactly one place. Wave 2 ships no real gated construct
/// yet; the list is empty and <see cref="FeatureGateChecker"/> short-circuits, so gating is
/// a no-op until the first pilot (matmul <c>@</c> / <c>defer</c>) lands. Tests exercise the
/// checker by passing a custom construct list to <see cref="FeatureGateChecker"/> directly.
/// </remarks>
public static class GatedConstructRegistry
{
    /// <summary>All registered gated constructs. Empty until the first pilot feature lands.</summary>
    public static IReadOnlyList<GatedConstruct> All { get; } = new List<GatedConstruct>();
}

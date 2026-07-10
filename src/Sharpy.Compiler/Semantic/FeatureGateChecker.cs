using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Reports <see cref="DiagnosticCodes.Semantic.FeatureNotEnabled"/> (SPY0331) for every use
/// of a syntactic construct that is gated behind an experimental feature which is not enabled.
/// </summary>
/// <remarks>
/// <para>
/// Runs after import resolution (Pass 1.5) and before type resolution, so the flag set it
/// checks against is the union of the compilation-wide features and this file's per-file
/// <c>from __future__ import</c> features — matching the union computed for the type checker
/// (see <c>ImportResolver.GetFileFutureFeatures</c> and <c>Compiler.cs</c>). Parser-scoped
/// gated constructs (which cannot be unlocked by a future import, by design) are validated
/// here too: only the compilation-wide portion of the union will ever enable them, which is
/// exactly the correct behavior.
/// </para>
/// <para>
/// The set of gated constructs is data-driven via <see cref="GatedConstructRegistry.All"/>.
/// Tests may inject a custom construct list. When the list is empty the checker short-circuits.
/// </para>
/// </remarks>
internal sealed class FeatureGateChecker : AstVisitor
{
    private readonly DiagnosticBag _diagnostics;
    private readonly FeatureFlags _features;
    private readonly string? _filePath;
    private readonly IReadOnlyList<GatedConstruct> _gatedConstructs;

    public FeatureGateChecker(
        DiagnosticBag diagnostics,
        FeatureFlags features,
        string? filePath = null,
        IReadOnlyList<GatedConstruct>? gatedConstructs = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _features = features ?? throw new ArgumentNullException(nameof(features));
        _filePath = filePath;
        _gatedConstructs = gatedConstructs ?? GatedConstructRegistry.All;
    }

    /// <summary>
    /// Walks <paramref name="module"/> and reports SPY0331 for each ungated construct usage.
    /// </summary>
    public void Check(Module module)
    {
        if (module is null || _gatedConstructs.Count == 0)
            return;

        Visit(module);
    }

    /// <inheritdoc/>
    public override void Visit(Node node)
    {
        if (node is null)
            return;

        foreach (var gate in _gatedConstructs)
        {
            if (gate.Matches(node) && !_features.IsEnabled(gate.Feature))
                Report(node, gate);
        }

        base.Visit(node);
    }

    private void Report(Node node, GatedConstruct gate)
    {
        var feature = gate.Feature;

        // Semantic/CodeGen-scoped features can also be enabled per-file via a future import;
        // parser-scoped features cannot, so we omit that suggestion for them. An unregistered
        // feature name is a programming error in the gated-construct registry.
        var known = FeatureFlags.KnownFeatures.TryGetValue(feature, out var info);
        Debug.Assert(known, $"Gated construct references unknown feature '{feature}'.");
        var suggestFutureImport = known && info!.Scope != FeatureScope.Parser;

        var message =
            $"{gate.Description} requires experimental feature '{feature}' — " +
            $"enable with --enable-feature={feature} or <Features>{feature}</Features> in .spyproj" +
            (suggestFutureImport ? $" or from __future__ import {feature}" : string.Empty);

        _diagnostics.AddError(
            message,
            node.Span,
            node.LineStart,
            node.ColumnStart,
            _filePath,
            DiagnosticCodes.Semantic.FeatureNotEnabled,
            CompilerPhase.TypeChecking);
    }
}

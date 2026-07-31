namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// An exception-safe bracket around one <see cref="CompilationMetrics"/> phase that tolerates a
/// null metrics object, so a caller that is not collecting attribution pays only a null check.
/// </summary>
/// <remarks>
/// <para>
/// A <c>ref struct</c> because the stages it brackets sit on the LSP's per-keystroke analysis path
/// (#1140): instrumentation that allocates on the path being measured corrupts the measurement.
/// </para>
/// <para>
/// The <c>using</c> form matters beyond tidiness. <see cref="CompilationMetrics.StartPhase"/>
/// throws when a phase is already running, so a stage that threw and never called
/// <see cref="CompilationMetrics.EndPhase"/> would leave the metrics object wedged and the *next*
/// bracket would throw an <see cref="InvalidOperationException"/> that masks the original failure.
/// </para>
/// </remarks>
internal readonly ref struct MetricsStage
{
    private readonly CompilationMetrics? _metrics;

    private MetricsStage(CompilationMetrics? metrics) => _metrics = metrics;

    /// <summary>
    /// Starts <paramref name="name"/> on <paramref name="metrics"/>, or does nothing at all when
    /// <paramref name="metrics"/> is null.
    /// </summary>
    public static MetricsStage Begin(CompilationMetrics? metrics, string name)
    {
        metrics?.StartPhase(name);
        return new MetricsStage(metrics);
    }

    /// <summary>Ends the phase started by <see cref="Begin"/>.</summary>
    public void Dispose() => _metrics?.EndPhase();
}

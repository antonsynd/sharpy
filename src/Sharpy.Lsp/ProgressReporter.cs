using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace Sharpy.Lsp;

/// <summary>
/// Wraps the OmniSharp work-done progress API to report indexing and analysis progress
/// to the LSP client. Falls back to no-op if the client doesn't support progress.
/// </summary>
internal sealed class ProgressReporter
{
    private readonly IServerWorkDoneManager? _workDoneManager;
    private readonly ILogger<ProgressReporter> _logger;

    public ProgressReporter(IServerWorkDoneManager? workDoneManager, ILogger<ProgressReporter> logger)
    {
        _workDoneManager = workDoneManager;
        _logger = logger;
    }

    /// <summary>
    /// Whether the client supports work-done progress reporting.
    /// </summary>
    public bool IsSupported => _workDoneManager?.IsSupported == true;

    /// <summary>
    /// Creates a progress scope for a named operation. Sends Begin on creation,
    /// allows Report updates, and sends End on disposal.
    /// Returns a no-op scope if progress is not supported.
    /// </summary>
    /// <param name="title">The progress title shown to the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A disposable progress scope.</returns>
    public async Task<ProgressScope> BeginAsync(string title, CancellationToken ct = default)
    {
        if (_workDoneManager == null || !_workDoneManager.IsSupported)
        {
            _logger.LogDebug("Progress not supported, using no-op scope for '{Title}'", title);
            return ProgressScope.NoOp;
        }

        try
        {
            var observer = await _workDoneManager.Create(
                new WorkDoneProgressBegin
                {
                    Title = title,
                    Cancellable = true,
                    Percentage = 0
                },
                onComplete: null,
                cancellationToken: ct).ConfigureAwait(false);

            return new ProgressScope(observer, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create progress for '{Title}'", title);
            return ProgressScope.NoOp;
        }
    }
}

/// <summary>
/// A progress scope that reports updates and completes on disposal.
/// </summary>
internal sealed class ProgressScope : IDisposable
{
    /// <summary>
    /// Creates a new no-op progress scope that does nothing.
    /// Returns a fresh instance each time to avoid shared mutable state.
    /// </summary>
    public static ProgressScope NoOp => new(null, null);

    private readonly IWorkDoneObserver? _observer;
    private readonly ILogger? _logger;
    private bool _disposed;

    internal ProgressScope(IWorkDoneObserver? observer, ILogger? logger)
    {
        _observer = observer;
        _logger = logger;
    }

    /// <summary>
    /// Reports progress with a message and optional percentage.
    /// </summary>
    /// <param name="message">Status message to display.</param>
    /// <param name="percentage">Optional percentage (0-100).</param>
    public void Report(string message, int? percentage = null)
    {
        if (_observer == null || _disposed)
            return;

        try
        {
            _observer.OnNext(new WorkDoneProgressReport
            {
                Message = message,
                Percentage = percentage,
                Cancellable = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to report progress");
        }
    }

    /// <summary>
    /// Completes the progress with an optional final message.
    /// </summary>
    /// <param name="message">Optional completion message.</param>
    public void Complete(string? message = null)
    {
        if (_observer == null || _disposed)
            return;

        _disposed = true;

        try
        {
            _observer.OnNext(new WorkDoneProgressEnd
            {
                Message = message
            });
            _observer.OnCompleted();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to complete progress");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Complete();
        }
    }
}

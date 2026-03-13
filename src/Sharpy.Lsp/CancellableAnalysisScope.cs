using System.Collections.Concurrent;

namespace Sharpy.Lsp;

/// <summary>
/// A lightweight IDisposable that manages cancel-previous-create-new CTS patterns.
/// On creation, cancels and disposes any existing CTS for the given key, then
/// registers a new linked CTS. On dispose, removes the CTS from the registry.
/// </summary>
internal sealed class CancellableAnalysisScope : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _registry;
    private readonly string _key;
    private readonly CancellationTokenSource _cts;
    private int _disposed;

    public CancellableAnalysisScope(
        ConcurrentDictionary<string, CancellationTokenSource> registry,
        string key,
        CancellationToken external)
    {
        _registry = registry;
        _key = key;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(external);

        // Cancel and dispose any previous CTS for this key
        if (_registry.TryGetValue(key, out var oldCts))
        {
            try
            { oldCts.Cancel(); }
            catch (ObjectDisposedException) { }
            oldCts.Dispose();
        }

        _registry[key] = _cts;
    }

    /// <summary>
    /// The linked cancellation token for this scope.
    /// </summary>
    public CancellationToken Token => _cts.Token;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Only remove from registry if we're still the current CTS
        _registry.TryRemove(
            new KeyValuePair<string, CancellationTokenSource>(_key, _cts));

        _cts.Dispose();
    }
}

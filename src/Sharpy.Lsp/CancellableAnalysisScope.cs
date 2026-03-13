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

        // Atomically swap the CTS for this key, cancelling any previous one.
        // Uses CAS loop to avoid a race where two concurrent constructors for the
        // same key both read the same oldCts, then one overwrites the other's CTS
        // without cancelling it.
        while (true)
        {
            if (_registry.TryGetValue(key, out var oldCts))
            {
                if (_registry.TryUpdate(key, _cts, oldCts))
                {
                    try
                    { oldCts.Cancel(); }
                    catch (ObjectDisposedException) { }
                    oldCts.Dispose();
                    break;
                }
                // Another thread updated the key concurrently; retry
            }
            else
            {
                if (_registry.TryAdd(key, _cts))
                    break;
                // Another thread added the key concurrently; retry
            }
        }
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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharpy.Compiler.Utilities;

/// <summary>
/// A tiny free-list pool for short-lived scratch <see cref="List{T}"/> buffers that are
/// filled, converted into an immutable Roslyn node (which copies their contents), and then
/// dropped. Reusing the backing list avoids one allocation — plus its growth reallocations —
/// per rent on hot emission paths.
/// </summary>
/// <remarks>
/// <para>
/// <b>Instance-owned, single-threaded.</b> A pool must be owned by a single compilation-scoped
/// object (e.g. one <c>RoslynEmitter</c> instance). <c>ProjectCompiler</c> compiles files
/// sequentially and concurrent compilations each own their own emitter, so no synchronization
/// is needed. This is deliberately not a static/shared pool (see the reviewed-mutable-static
/// doctrine enforced by <c>StaticStateConformanceTests</c>).
/// </para>
/// <para>
/// <b>Reentrancy.</b> Rent/return follow stack discipline via the returned <see cref="Scope"/>
/// (<c>using</c>). Nested rents — emission recurses into nested blocks and nested classes — pop
/// distinct instances from the free list, so a rented list is never handed out again until it
/// has been returned. The pooled list must be fully consumed (copied) before its scope is
/// disposed.
/// </para>
/// </remarks>
/// <typeparam name="T">Element type of the pooled lists.</typeparam>
internal sealed class ScratchListPool<T>
{
    // Bounds the retained free list. Rent depth is bounded by emission nesting depth, which is
    // small; this cap only guards against pathological retention, never correctness.
    private const int MaxRetained = 32;

    private readonly Stack<List<T>> _free = new();

    /// <summary>
    /// Rents a cleared scratch list. Dispose the returned <see cref="Scope"/> (via
    /// <c>using</c>) to return the list to the pool after its contents have been copied out.
    /// </summary>
    public Scope Rent(out List<T> list)
    {
        list = _free.Count > 0 ? _free.Pop() : new List<T>();
        Debug.Assert(list.Count == 0, "Rented scratch list was not empty — a prior user returned it without clearing.");
        return new Scope(this, list);
    }

    private void Return(List<T> list)
    {
        Debug.Assert(!_free.Contains(list), "Scratch list returned to the pool twice.");
        list.Clear();
        if (_free.Count < MaxRetained)
            _free.Push(list);
    }

    /// <summary>
    /// Scope handle that returns a rented list to its pool on disposal. A <c>readonly struct</c>
    /// so <c>using</c> introduces no heap allocation.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly ScratchListPool<T> _pool;
        private readonly List<T> _list;

        internal Scope(ScratchListPool<T> pool, List<T> list)
        {
            _pool = pool;
            _list = list;
        }

        public void Dispose() => _pool.Return(_list);
    }
}

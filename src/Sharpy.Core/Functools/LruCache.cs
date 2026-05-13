using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// Snapshot of cache statistics returned by
    /// <see cref="LruCache{TKey, TResult}.CacheInfo"/>.
    /// </summary>
    /// <param name="Hits">The number of cache hits.</param>
    /// <param name="Misses">The number of cache misses.</param>
    /// <param name="MaxSize">The maximum size, or <c>null</c> for unbounded.</param>
    /// <param name="CurrentSize">The current number of cached entries.</param>
    public sealed record CacheInfo(int Hits, int Misses, int? MaxSize, int CurrentSize);

    /// <summary>
    /// Thread-safe memoization cache backing the <c>@functools.lru_cache</c> and
    /// <c>@functools.cache</c> decorators.
    /// </summary>
    /// <remarks>
    /// When constructed with a <c>null</c> maxsize the cache is unbounded and uses
    /// a <see cref="ConcurrentDictionary{TKey, TResult}"/> internally. When a
    /// positive maxsize is supplied, the cache evicts entries in least-recently-used
    /// order once it reaches capacity.
    /// </remarks>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TResult">The cached value type.</typeparam>
    public sealed class LruCache<TKey, TResult> where TKey : notnull
    {
        private readonly int? _maxSize;

        // Unbounded path: simple thread-safe dictionary.
        private readonly ConcurrentDictionary<TKey, TResult>? _unbounded;

        // Bounded path: dictionary + linked list under a single lock.
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TResult>>>? _boundedMap;
        private readonly LinkedList<KeyValuePair<TKey, TResult>>? _boundedOrder;
        private readonly object? _boundedLock;

        private int _hits;
        private int _misses;

        /// <summary>
        /// Creates an unbounded memoization cache.
        /// </summary>
        public LruCache()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a memoization cache with the given maximum size, or
        /// <c>null</c> for an unbounded cache.
        /// </summary>
        /// <param name="maxSize">The maximum number of cached entries, or
        /// <c>null</c> for unbounded.</param>
        public LruCache(int? maxSize)
        {
            if (maxSize is int max && max <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxSize),
                    "maxsize must be a positive integer or None.");
            }

            _maxSize = maxSize;

            if (maxSize is null)
            {
                _unbounded = new ConcurrentDictionary<TKey, TResult>();
            }
            else
            {
                _boundedMap = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TResult>>>();
                _boundedOrder = new LinkedList<KeyValuePair<TKey, TResult>>();
                _boundedLock = new object();
            }
        }

        /// <summary>
        /// Looks up the cached value for <paramref name="key"/>, or computes it
        /// via <paramref name="factory"/> and stores the result.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">A factory invoked on miss to compute the value.</param>
        /// <returns>The cached or freshly-computed value.</returns>
        public TResult GetOrAdd(TKey key, Func<TKey, TResult> factory)
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_unbounded is not null)
            {
                if (_unbounded.TryGetValue(key, out TResult? existing))
                {
                    Interlocked.Increment(ref _hits);
                    return existing;
                }

                // Compute outside the dictionary lock; GetOrAdd handles races.
                TResult computed = factory(key);
                TResult stored = _unbounded.GetOrAdd(key, computed);
                if (ReferenceEquals(stored, computed) || EqualityComparer<TResult>.Default.Equals(stored, computed))
                {
                    Interlocked.Increment(ref _misses);
                }
                else
                {
                    // A racing thread populated the entry first.
                    Interlocked.Increment(ref _hits);
                }

                return stored;
            }

            // Bounded path
            lock (_boundedLock!)
            {
                if (_boundedMap!.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TResult>>? node))
                {
                    _hits++;
                    // Move to most-recently-used position (end of list).
                    _boundedOrder!.Remove(node);
                    _boundedOrder.AddLast(node);
                    return node.Value.Value;
                }

                _misses++;
                TResult computed = factory(key);

                if (_boundedMap.Count >= _maxSize!.Value)
                {
                    LinkedListNode<KeyValuePair<TKey, TResult>>? oldest = _boundedOrder!.First;
                    if (oldest is not null)
                    {
                        _boundedOrder.RemoveFirst();
                        _boundedMap.Remove(oldest.Value.Key);
                    }
                }

                LinkedListNode<KeyValuePair<TKey, TResult>> newNode =
                    new LinkedListNode<KeyValuePair<TKey, TResult>>(
                        new KeyValuePair<TKey, TResult>(key, computed));
                _boundedOrder!.AddLast(newNode);
                _boundedMap[key] = newNode;
                return computed;
            }
        }

        /// <summary>
        /// Returns a snapshot of cache statistics.
        /// </summary>
        public CacheInfo CacheInfo()
        {
            if (_unbounded is not null)
            {
                int hits = Volatile.Read(ref _hits);
                int misses = Volatile.Read(ref _misses);
                return new CacheInfo(hits, misses, _maxSize, _unbounded.Count);
            }

            lock (_boundedLock!)
            {
                return new CacheInfo(_hits, _misses, _maxSize, _boundedMap!.Count);
            }
        }

        /// <summary>
        /// Clears all cached entries and resets the hit/miss counters.
        /// </summary>
        public void CacheClear()
        {
            if (_unbounded is not null)
            {
                _unbounded.Clear();
                Interlocked.Exchange(ref _hits, 0);
                Interlocked.Exchange(ref _misses, 0);
                return;
            }

            lock (_boundedLock!)
            {
                _boundedMap!.Clear();
                _boundedOrder!.Clear();
                _hits = 0;
                _misses = 0;
            }
        }
    }
}

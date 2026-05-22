using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class LruCacheTests
{
    [Fact]
    public void Unbounded_GetOrAdd_StoresAndReturnsValue()
    {
        var cache = new LruCache<int, int>();

        var result = cache.GetOrAdd(5, k => k * 2);

        result.Should().Be(10);
    }

    [Fact]
    public void Unbounded_GetOrAdd_Hit_DoesNotInvokeFactory()
    {
        var cache = new LruCache<int, int>();
        int factoryCalls = 0;
        Func<int, int> factory = k =>
        {
            factoryCalls++;
            return k * 2;
        };

        cache.GetOrAdd(5, factory);
        cache.GetOrAdd(5, factory);
        cache.GetOrAdd(5, factory);

        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void Unbounded_CacheInfo_TracksHitsAndMisses()
    {
        var cache = new LruCache<int, int>();

        cache.GetOrAdd(1, k => k); // miss
        cache.GetOrAdd(2, k => k); // miss
        cache.GetOrAdd(1, k => k); // hit
        cache.GetOrAdd(1, k => k); // hit
        cache.GetOrAdd(2, k => k); // hit

        var info = cache.CacheInfo();
        info.Hits.Should().Be(3);
        info.Misses.Should().Be(2);
        info.MaxSize.Should().BeNull();
        info.CurrentSize.Should().Be(2);
    }

    [Fact]
    public void Unbounded_CacheClear_RemovesEntriesAndResetsCounters()
    {
        var cache = new LruCache<int, int>();

        cache.GetOrAdd(1, k => k);
        cache.GetOrAdd(1, k => k);

        cache.CacheClear();

        var info = cache.CacheInfo();
        info.Hits.Should().Be(0);
        info.Misses.Should().Be(0);
        info.CurrentSize.Should().Be(0);
    }

    [Fact]
    public void Bounded_EvictsLeastRecentlyUsed()
    {
        var cache = new LruCache<int, int>(maxSize: 2);

        cache.GetOrAdd(1, k => k);
        cache.GetOrAdd(2, k => k);
        cache.GetOrAdd(3, k => k); // evicts 1 (least recently used) -> [2, 3]

        var info = cache.CacheInfo();
        info.CurrentSize.Should().Be(2);
        info.MaxSize.Should().Be(2);

        int factoryCalls = 0;
        Func<int, int> tracking = k => { factoryCalls++; return k; };

        cache.GetOrAdd(2, tracking); // hit
        cache.GetOrAdd(3, tracking); // hit
        cache.GetOrAdd(1, tracking); // miss (was evicted)

        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void Bounded_AccessingKeyMakesItMostRecentlyUsed()
    {
        var cache = new LruCache<int, int>(maxSize: 2);

        cache.GetOrAdd(1, k => k);
        cache.GetOrAdd(2, k => k);
        // Touch 1: now 2 is the LRU.
        cache.GetOrAdd(1, k => k);
        // Insert 3 should evict 2.
        cache.GetOrAdd(3, k => k);

        int factoryCalls = 0;
        cache.GetOrAdd(1, k => { factoryCalls++; return k; }); // hit
        cache.GetOrAdd(3, k => { factoryCalls++; return k; }); // hit
        cache.GetOrAdd(2, k => { factoryCalls++; return k; }); // miss

        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void Bounded_CacheInfo_ReportsMaxSize()
    {
        var cache = new LruCache<string, int>(maxSize: 16);

        cache.GetOrAdd("a", _ => 1);

        var info = cache.CacheInfo();
        info.MaxSize.Should().Be(16);
        info.CurrentSize.Should().Be(1);
        info.Hits.Should().Be(0);
        info.Misses.Should().Be(1);
    }

    [Fact]
    public void Constructor_InvalidMaxSize_Throws()
    {
        FluentActions.Invoking(() => new LruCache<int, int>(maxSize: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new LruCache<int, int>(maxSize: -1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetOrAdd_NullFactory_Throws()
    {
        var cache = new LruCache<int, int>();

        FluentActions.Invoking(() => cache.GetOrAdd(1, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unbounded_ThreadSafe_ConcurrentAccess()
    {
        var cache = new LruCache<int, int>();
        const int threads = 8;
        const int iterations = 1000;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < iterations; i++)
            {
                int key = i % 50;
                int value = cache.GetOrAdd(key, k => k * k);
                value.Should().Be(key * key);
            }
        });

        var info = cache.CacheInfo();
        info.CurrentSize.Should().Be(50);
        (info.Hits + info.Misses).Should().Be(threads * iterations);
    }

    [Fact]
    public void Bounded_ThreadSafe_ConcurrentAccess()
    {
        var cache = new LruCache<int, int>(maxSize: 16);
        const int threads = 8;
        const int iterations = 500;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < iterations; i++)
            {
                int key = i % 32;
                int value = cache.GetOrAdd(key, k => k * 2);
                value.Should().Be(key * 2);
            }
        });

        var info = cache.CacheInfo();
        info.CurrentSize.Should().BeLessThanOrEqualTo(16);
        (info.Hits + info.Misses).Should().Be(threads * iterations);
    }

    [Fact]
    public void Bounded_CacheClear_AllowsRefilling()
    {
        var cache = new LruCache<int, int>(maxSize: 3);
        cache.GetOrAdd(1, k => k);
        cache.GetOrAdd(2, k => k);

        cache.CacheClear();

        var info = cache.CacheInfo();
        info.CurrentSize.Should().Be(0);
        info.Hits.Should().Be(0);
        info.Misses.Should().Be(0);

        cache.GetOrAdd(1, k => k);
        cache.CacheInfo().CurrentSize.Should().Be(1);
    }

    [Fact]
    public void CacheInfo_IsImmutableSnapshot()
    {
        var cache = new LruCache<int, int>();
        cache.GetOrAdd(1, k => k);

        var info1 = cache.CacheInfo();

        cache.GetOrAdd(2, k => k);

        var info2 = cache.CacheInfo();

        info1.CurrentSize.Should().Be(1);
        info2.CurrentSize.Should().Be(2);
    }
}

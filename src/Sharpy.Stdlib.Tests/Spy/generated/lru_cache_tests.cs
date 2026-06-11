// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Functools.LruCacheTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Functools
    {
        [global::Sharpy.SharpyModule("functools.lru_cache_tests")]
        public static partial class LruCacheTests
        {
            private static readonly global::Sharpy.LruCache<int, int> __DoubleValCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __DoubleVal(int k)
            {
#line (9, 5) - (9, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k * 2;
            }

            public static int DoubleVal(int k) => __DoubleValCache.GetOrAdd(k, __key => __DoubleVal(__key));
            public static global::Sharpy.CacheInfo DoubleValCacheInfo() => __DoubleValCache.CacheInfo();
            public static void DoubleValCacheClear() => __DoubleValCache.CacheClear();
            public static Sharpy.List<int> UnboundedCalls = new Sharpy.List<int>()
            {
            };
            private static readonly global::Sharpy.LruCache<int, int> __TrackedDoubleCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __TrackedDouble(int k)
            {
#line (23, 5) - (23, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                UnboundedCalls.Append(1);
#line (24, 5) - (24, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k * 2;
            }

            public static int TrackedDouble(int k) => __TrackedDoubleCache.GetOrAdd(k, __key => __TrackedDouble(__key));
            public static global::Sharpy.CacheInfo TrackedDoubleCacheInfo() => __TrackedDoubleCache.CacheInfo();
            public static void TrackedDoubleCacheClear() => __TrackedDoubleCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __IdentityFnCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __IdentityFn(int k)
            {
#line (41, 5) - (41, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int IdentityFn(int k) => __IdentityFnCache.GetOrAdd(k, __key => __IdentityFn(__key));
            public static global::Sharpy.CacheInfo IdentityFnCacheInfo() => __IdentityFnCache.CacheInfo();
            public static void IdentityFnCacheClear() => __IdentityFnCache.CacheClear();
            public static Sharpy.List<int> CountedCalls = new Sharpy.List<int>()
            {
            };
            private static readonly global::Sharpy.LruCache<int, int> __CountedIdentityCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __CountedIdentity(int k)
            {
#line (57, 5) - (57, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                CountedCalls.Append(1);
#line (58, 5) - (58, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int CountedIdentity(int k) => __CountedIdentityCache.GetOrAdd(k, __key => __CountedIdentity(__key));
            public static global::Sharpy.CacheInfo CountedIdentityCacheInfo() => __CountedIdentityCache.CacheInfo();
            public static void CountedIdentityCacheClear() => __CountedIdentityCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __BoundedSquareCache = new global::Sharpy.LruCache<int, int>(2);
            private static int __BoundedSquare(int k)
            {
#line (74, 5) - (74, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k * k;
            }

            public static int BoundedSquare(int k) => __BoundedSquareCache.GetOrAdd(k, __key => __BoundedSquare(__key));
            public static global::Sharpy.CacheInfo BoundedSquareCacheInfo() => __BoundedSquareCache.CacheInfo();
            public static void BoundedSquareCacheClear() => __BoundedSquareCache.CacheClear();
            public static Sharpy.List<int> TouchCalls = new Sharpy.List<int>()
            {
            };
            private static readonly global::Sharpy.LruCache<int, int> __BoundedTouchCache = new global::Sharpy.LruCache<int, int>(2);
            private static int __BoundedTouch(int k)
            {
#line (92, 5) - (92, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                TouchCalls.Append(k);
#line (93, 5) - (93, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int BoundedTouch(int k) => __BoundedTouchCache.GetOrAdd(k, __key => __BoundedTouch(__key));
            public static global::Sharpy.CacheInfo BoundedTouchCacheInfo() => __BoundedTouchCache.CacheInfo();
            public static void BoundedTouchCacheClear() => __BoundedTouchCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __BoundedFnCache = new global::Sharpy.LruCache<int, int>(16);
            private static int __BoundedFn(int k)
            {
#line (111, 5) - (111, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return 1;
            }

            public static int BoundedFn(int k) => __BoundedFnCache.GetOrAdd(k, __key => __BoundedFn(__key));
            public static global::Sharpy.CacheInfo BoundedFnCacheInfo() => __BoundedFnCache.CacheInfo();
            public static void BoundedFnCacheClear() => __BoundedFnCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __TinyCacheCache = new global::Sharpy.LruCache<int, int>(1);
            private static int __TinyCache(int k)
            {
#line (123, 5) - (123, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int TinyCache(int k) => __TinyCacheCache.GetOrAdd(k, __key => __TinyCache(__key));
            public static global::Sharpy.CacheInfo TinyCacheCacheInfo() => __TinyCacheCache.CacheInfo();
            public static void TinyCacheCacheClear() => __TinyCacheCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __SquareValCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __SquareVal(int k)
            {
#line (144, 5) - (144, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k * k;
            }

            public static int SquareVal(int k) => __SquareValCache.GetOrAdd(k, __key => __SquareVal(__key));
            public static global::Sharpy.CacheInfo SquareValCacheInfo() => __SquareValCache.CacheInfo();
            public static void SquareValCacheClear() => __SquareValCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __BoundedDoubleCache = new global::Sharpy.LruCache<int, int>(16);
            private static int __BoundedDouble(int k)
            {
#line (161, 5) - (161, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k * 2;
            }

            public static int BoundedDouble(int k) => __BoundedDoubleCache.GetOrAdd(k, __key => __BoundedDouble(__key));
            public static global::Sharpy.CacheInfo BoundedDoubleCacheInfo() => __BoundedDoubleCache.CacheInfo();
            public static void BoundedDoubleCacheClear() => __BoundedDoubleCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __BoundedClearFnCache = new global::Sharpy.LruCache<int, int>(3);
            private static int __BoundedClearFn(int k)
            {
#line (176, 5) - (176, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int BoundedClearFn(int k) => __BoundedClearFnCache.GetOrAdd(k, __key => __BoundedClearFn(__key));
            public static global::Sharpy.CacheInfo BoundedClearFnCacheInfo() => __BoundedClearFnCache.CacheInfo();
            public static void BoundedClearFnCacheClear() => __BoundedClearFnCache.CacheClear();
            private static readonly global::Sharpy.LruCache<int, int> __SnapshotFnCache = new global::Sharpy.LruCache<int, int>(null);
            private static int __SnapshotFn(int k)
            {
#line (189, 5) - (189, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                return k;
            }

            public static int SnapshotFn(int k) => __SnapshotFnCache.GetOrAdd(k, __key => __SnapshotFn(__key));
            public static global::Sharpy.CacheInfo SnapshotFnCacheInfo() => __SnapshotFnCache.CacheInfo();
            public static void SnapshotFnCacheClear() => __SnapshotFnCache.CacheClear();
        }
    }

    public static partial class Functools
    {
        public partial class LruCacheTestsTests
        {
            [Xunit.FactAttribute]
            public void TestUnboundedGetOrAddStoresAndReturnsValue()
            {
#line (13, 5) - (13, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                int result = DoubleVal(5);
#line (14, 5) - (14, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(10, result);
            }

            [Xunit.FactAttribute]
            public void TestUnboundedGetOrAddHitDoesNotInvokeFactory()
            {
#line (29, 5) - (31, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                while (global::Sharpy.Builtins.Len(UnboundedCalls) > 0)
                {
#line (30, 9) - (30, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    UnboundedCalls.Pop();
                }

#line (31, 5) - (31, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                TrackedDouble(5);
#line (32, 5) - (32, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                TrackedDouble(5);
#line (33, 5) - (33, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                TrackedDouble(5);
#line (35, 5) - (35, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(UnboundedCalls));
            }

            [Xunit.FactAttribute]
            public void TestUnboundedCacheTracksMultipleKeys()
            {
#line (46, 5) - (46, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(100, IdentityFn(100));
#line (47, 5) - (47, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(200, IdentityFn(200));
#line (48, 5) - (48, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(100, IdentityFn(100));
#line (49, 5) - (49, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(200, IdentityFn(200));
            }

            [Xunit.FactAttribute]
            public void TestUnboundedCacheReturnsCorrectValuesAfterHits()
            {
#line (62, 5) - (64, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                while (global::Sharpy.Builtins.Len(CountedCalls) > 0)
                {
#line (63, 9) - (63, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    CountedCalls.Pop();
                }

#line (64, 5) - (64, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                CountedIdentity(10);
#line (65, 5) - (65, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                CountedIdentity(10);
#line (66, 5) - (66, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(CountedCalls));
#line (67, 5) - (67, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                CountedIdentity(20);
#line (68, 5) - (68, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(CountedCalls));
            }

            [Xunit.FactAttribute]
            public void TestBoundedEvictsLeastRecentlyUsed()
            {
#line (79, 5) - (79, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1001 * 1001, BoundedSquare(1001));
#line (80, 5) - (80, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1002 * 1002, BoundedSquare(1002));
#line (82, 5) - (82, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1003 * 1003, BoundedSquare(1003));
#line (84, 5) - (84, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1001 * 1001, BoundedSquare(1001));
            }

            [Xunit.FactAttribute]
            public void TestBoundedAccessingKeyMakesItMostRecentlyUsed()
            {
#line (97, 5) - (99, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                while (global::Sharpy.Builtins.Len(TouchCalls) > 0)
                {
#line (98, 9) - (98, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    TouchCalls.Pop();
                }

#line (99, 5) - (99, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                BoundedTouch(2001);
#line (100, 5) - (100, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                BoundedTouch(2002);
#line (101, 5) - (101, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                BoundedTouch(2001);
#line (102, 5) - (102, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                BoundedTouch(2003);
#line (103, 5) - (103, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                int prevLen = global::Sharpy.Builtins.Len(TouchCalls);
#line (104, 5) - (104, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                BoundedTouch(2002);
#line (105, 5) - (105, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(prevLen + 1, global::Sharpy.Builtins.Len(TouchCalls));
            }

            [Xunit.FactAttribute]
            public void TestBoundedCacheInfoReportsMaxSize()
            {
#line (116, 5) - (116, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1, BoundedFn(3001));
#line (117, 5) - (117, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(1, BoundedFn(3001));
            }

            [Xunit.FactAttribute]
            public void TestConstructorInvalidMaxSizeUsesValidCache()
            {
#line (127, 5) - (127, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(4001, TinyCache(4001));
#line (128, 5) - (128, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(4002, TinyCache(4002));
#line (130, 5) - (130, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(4001, TinyCache(4001));
            }

            [Xunit.FactAttribute]
            public void TestGetOrAddReturnsCorrectValue()
            {
#line (136, 5) - (136, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(20, DoubleVal(10));
#line (137, 5) - (137, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(0, DoubleVal(0));
#line (138, 5) - (138, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(-10, DoubleVal(-5));
            }

            [Xunit.FactAttribute]
            public void TestUnboundedManyKeys()
            {
#line (148, 5) - (148, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                int i = 5000;
#line (149, 5) - (154, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                while (i < 5050)
                {
#line (150, 9) - (150, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    int val = SquareVal(i);
#line (151, 9) - (151, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    Xunit.Assert.Equal(i * i, val);
#line (152, 9) - (152, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    i = i + 1;
                }

#line (154, 5) - (154, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(5000 * 5000, SquareVal(5000));
#line (155, 5) - (155, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(5049 * 5049, SquareVal(5049));
            }

            [Xunit.FactAttribute]
            public void TestBoundedManyKeysWithEviction()
            {
#line (165, 5) - (165, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                int i = 0;
#line (166, 5) - (174, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                while (i < 100)
                {
#line (167, 9) - (167, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    int key = 6000 + (i % 32);
#line (168, 9) - (168, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    int val = BoundedDouble(key);
#line (169, 9) - (169, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    Xunit.Assert.Equal(key * 2, val);
#line (170, 9) - (170, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestBoundedCacheClearAllowsRefilling()
            {
#line (180, 5) - (180, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(7001, BoundedClearFn(7001));
#line (181, 5) - (181, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(7002, BoundedClearFn(7002));
#line (183, 5) - (183, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(7001, BoundedClearFn(7001));
            }

            [Xunit.FactAttribute]
            public void TestCacheReturnsConsistentValues()
            {
#line (193, 5) - (193, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(8001, SnapshotFn(8001));
#line (194, 5) - (194, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(8002, SnapshotFn(8002));
#line (196, 5) - (196, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(8001, SnapshotFn(8001));
#line (197, 5) - (197, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/functools/lru_cache_tests.spy"
                Xunit.Assert.Equal(8002, SnapshotFn(8002));
            }
        }
    }
}

// Generated from src/Sharpy.Stdlib/spy/random_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/random_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.RandomModule
{
    /// <summary>
    /// Generate pseudo-random numbers with various distributions.
    /// </summary>
    public static partial class RandomModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Random _Random__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte _Random__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Random__init()
        {
            _Random__field = new global::System.Random();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Random__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref _Random__state, "_random", _Random__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::System.Random _Random
        {
            get
            {
                if (!__Swept.Done)
                    _Random__slow();
                return ref _Random__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void _Random__ensure()
        {
            if (!__Swept.Done)
                _Random__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Random__storeSlow(global::System.Random value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref _Random__state, ref _Random__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Random _Random__set(global::System.Random value)
        {
            if (__Swept.Done)
                _Random__field = value;
            else
                _Random__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::System.Random>? _Random__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref _Random__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::System.Random>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::System.Random _Random__out(out global::System.Runtime.CompilerServices.StrongBox<global::System.Random>? spill)
        {
            spill = _Random__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref _Random__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R _Random__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::System.Random>? spill)
        {
            if (spill != null)
                _Random__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::Sharpy.Lock _Lock__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte _Lock__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Lock__init()
        {
            _Lock__field = new global::Sharpy.Lock();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Lock__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref _Lock__state, "_lock", _Lock__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::Sharpy.Lock _Lock
        {
            get
            {
                if (!__Swept.Done)
                    _Lock__slow();
                return ref _Lock__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void _Lock__ensure()
        {
            if (!__Swept.Done)
                _Lock__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Lock__storeSlow(global::Sharpy.Lock value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref _Lock__state, ref _Lock__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::Sharpy.Lock _Lock__set(global::Sharpy.Lock value)
        {
            if (__Swept.Done)
                _Lock__field = value;
            else
                _Lock__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? _Lock__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref _Lock__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::Sharpy.Lock _Lock__out(out global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? spill)
        {
            spill = _Lock__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref _Lock__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R _Lock__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? spill)
        {
            if (spill != null)
                _Lock__set(spill.Value!);
            return result;
        }

        /// <summary>
        /// Initialize the random number generator with the given seed.
        /// </summary>
        public static void Seed(int s)
        {
            {
                var __ctx_0 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_0.Enter();
                try
                {
                    _Random__set(new global::System.Random(s));
                }
                finally
                {
                    __ctx_0.Exit();
                }
            }
        }

        /// <summary>
        /// Return the next random floating point number in the range [0.0, 1.0).
        /// </summary>
        public static double Random()
        {
            {
                var __ctx_1 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_1.Enter();
                try
                {
                    return global::Sharpy.RandomModule.RandomModuleModule._Random.NextDouble();
                }
                finally
                {
                    __ctx_1.Exit();
                }
            }
        }

        /// <summary>
        /// Return a random integer in the range [a, b], including both end points.
        /// </summary>
        public static int Randint(int a, int b)
        {
            {
                var __ctx_2 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_2.Enter();
                try
                {
                    return global::Sharpy.RandomModule.RandomModuleModule._Random.Next(a, b + 1);
                }
                finally
                {
                    __ctx_2.Exit();
                }
            }
        }

        /// <summary>
        /// Return a random floating point number in the range [a, b].
        /// </summary>
        public static double Uniform(double a, double b)
        {
            {
                var __ctx_3 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_3.Enter();
                try
                {
                    return a + (global::Sharpy.RandomModule.RandomModuleModule._Random.NextDouble() * (b - a));
                }
                finally
                {
                    __ctx_3.Exit();
                }
            }
        }

        /// <summary>
        /// Return a random element from the non-empty sequence seq.
        /// </summary>
        public static T Choice<T>(Sharpy.List<T> seq)
        {
            if (seq is null || global::Sharpy.Builtins.Len(seq) == 0)
            {
                throw new global::Sharpy.IndexError("Cannot choose from an empty sequence");
            }

            int index = 0;
            {
                var __ctx_4 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_4.Enter();
                try
                {
                    index = global::Sharpy.RandomModule.RandomModuleModule._Random.Next(global::Sharpy.Builtins.Len(seq));
                }
                finally
                {
                    __ctx_4.Exit();
                }
            }

            return seq[index];
        }

        /// <summary>
        /// Shuffle the sequence x in place.
        /// </summary>
        public static void Shuffle<T>(Sharpy.List<T> x)
        {
            if (x is null)
            {
                throw new global::Sharpy.TypeError("'NoneType' object cannot be shuffled");
            }

            int n = global::Sharpy.Builtins.Len(x);
            int i = n - 1;
            while (i > 0)
            {
                int j = 0;
                {
                    var __ctx_5 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                    __ctx_5.Enter();
                    try
                    {
                        j = global::Sharpy.RandomModule.RandomModuleModule._Random.Next(i + 1);
                    }
                    finally
                    {
                        __ctx_5.Exit();
                    }
                }

                T temp = x[i];
                x[i] = x[j];
                x[j] = temp;
                i = i - 1;
            }
        }

        /// <summary>
        /// Return a randomly selected element from range(stop).
        /// </summary>
        public static int Randrange(int stop)
        {
            if (stop <= 0)
            {
                throw new global::Sharpy.ValueError(FormattableString.Invariant($"empty range for randrange() (0, {(global::Sharpy.Builtins.Str(stop))})"));
            }

            {
                var __ctx_6 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_6.Enter();
                try
                {
                    return global::Sharpy.RandomModule.RandomModuleModule._Random.Next(stop);
                }
                finally
                {
                    __ctx_6.Exit();
                }
            }
        }

        /// <summary>
        /// Return a randomly selected element from range(start, stop, step).
        /// </summary>
        public static int Randrange(int start, int stop, int step = 1)
        {
            if (step == 0)
            {
                throw new global::Sharpy.ValueError("zero step for randrange()");
            }

            int width = stop - start;
            if (step == 1)
            {
                if (width <= 0)
                {
                    throw new global::Sharpy.ValueError(FormattableString.Invariant($"empty range for randrange() ({(global::Sharpy.Builtins.Str(start))}, {(global::Sharpy.Builtins.Str(stop))}, {(global::Sharpy.Builtins.Str(step))})"));
                }

                {
                    var __ctx_7 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                    __ctx_7.Enter();
                    try
                    {
                        return start + global::Sharpy.RandomModule.RandomModuleModule._Random.Next(width);
                    }
                    finally
                    {
                        __ctx_7.Exit();
                    }
                }
            }

            int n = 0;
            if (step > 0)
            {
                n = global::Sharpy.Builtins.FloorDiv((width + step - 1), step);
            }
            else
            {
                n = global::Sharpy.Builtins.FloorDiv((width + step + 1), step);
            }

            if (n <= 0)
            {
                throw new global::Sharpy.ValueError(FormattableString.Invariant($"empty range for randrange() ({(global::Sharpy.Builtins.Str(start))}, {(global::Sharpy.Builtins.Str(stop))}, {(global::Sharpy.Builtins.Str(step))})"));
            }

            {
                var __ctx_8 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_8.Enter();
                try
                {
                    return start + step * global::Sharpy.RandomModule.RandomModuleModule._Random.Next(n);
                }
                finally
                {
                    __ctx_8.Exit();
                }
            }
        }

        /// <summary>
        /// Return a random floating point number with Gaussian distribution.
        /// </summary>
        public static double Gauss(double mu, double sigma)
        {
            double u1 = 0.0d;
            double u2 = 0.0d;
            {
                var __ctx_9 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_9.Enter();
                try
                {
                    u1 = global::Sharpy.RandomModule.RandomModuleModule._Random.NextDouble();
                    u2 = global::Sharpy.RandomModule.RandomModuleModule._Random.NextDouble();
                }
                finally
                {
                    __ctx_9.Exit();
                }
            }

            double z0 = global::System.Math.Sqrt(-2.0d * global::System.Math.Log(u1)) * global::System.Math.Cos(2.0d * 3.141592653589793d * u2);
            return mu + sigma * z0;
        }

        /// <summary>
        /// Return an int with k random bits.
        /// </summary>
        public static int Getrandbits(int k)
        {
            if (k < 0)
            {
                throw new global::Sharpy.ValueError("number of bits must be non-negative");
            }

            if (k == 0)
            {
                return 0;
            }

            if (k > 30)
            {
                throw new global::Sharpy.ValueError("number of bits must be <= 30 for int return");
            }

            {
                var __ctx_10 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                __ctx_10.Enter();
                try
                {
                    return global::Sharpy.RandomModule.RandomModuleModule._Random.Next(1 << k);
                }
                finally
                {
                    __ctx_10.Exit();
                }
            }
        }

        /// <summary>
        /// Return a list of k elements chosen from population with replacement.
        /// </summary>
        public static Sharpy.List<T> Choices<T>(Sharpy.List<T> population, int k = 1)
        {
            int n = global::Sharpy.Builtins.Len(population);
            if (n == 0)
            {
                throw new global::Sharpy.IndexError("Cannot choose from an empty population");
            }

            Sharpy.List<T> result = new Sharpy.List<T>()
            {
            };
            int i = 0;
            while (i < k)
            {
                int index = 0;
                {
                    var __ctx_11 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                    __ctx_11.Enter();
                    try
                    {
                        index = global::Sharpy.RandomModule.RandomModuleModule._Random.Next(n);
                    }
                    finally
                    {
                        __ctx_11.Exit();
                    }
                }

                result.Append(population[index]);
                i = i + 1;
            }

            return result;
        }

        /// <summary>
        /// Return k unique elements from population without replacement.
        /// </summary>
        public static Sharpy.List<T> Sample<T>(Sharpy.List<T> population, int k)
        {
            int n = global::Sharpy.Builtins.Len(population);
            if (k < 0)
            {
                throw new global::Sharpy.ValueError("Sample larger than population or is negative");
            }

            if (k > n)
            {
                throw new global::Sharpy.ValueError("Sample larger than population or is negative");
            }

            Sharpy.List<T> pool = new global::Sharpy.List<T>(population);
            int i = 0;
            while (i < k)
            {
                int j = 0;
                {
                    var __ctx_12 = global::Sharpy.RandomModule.RandomModuleModule._Lock;
                    __ctx_12.Enter();
                    try
                    {
                        j = global::Sharpy.RandomModule.RandomModuleModule._Random.Next(i, n);
                    }
                    finally
                    {
                        __ctx_12.Exit();
                    }
                }

                T temp = pool[i];
                pool[i] = pool[j];
                pool[j] = temp;
                i = i + 1;
            }

            Sharpy.List<T> result = new Sharpy.List<T>()
            {
            };
            i = 0;
            while (i < k)
            {
                result.Append(pool[i]);
                i = i + 1;
            }

            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref _Random__state, "_random", _Random__init);
            global::Sharpy.StaticInit.Run(ref _Lock__state, "_lock", _Lock__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }
}

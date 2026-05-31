// Generated from src/Sharpy.Stdlib/spy/random_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/random_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Generate pseudo-random numbers with various distributions.
    /// </summary>
    public static partial class RandomModule
    {
        public static global::System.Random _Random = new global::System.Random();
        /// <summary>
        /// Initialize the random number generator with the given seed.
        /// </summary>
        public static void Seed(int s)
        {
            _Random = new global::System.Random(s);
        }

        /// <summary>
        /// Return the next random floating point number in the range [0.0, 1.0).
        /// </summary>
        public static double NextDouble()
        {
            return _Random.NextDouble();
        }

        /// <summary>
        /// Return a random integer in the range [a, b], including both end points.
        /// </summary>
        public static int Randint(int a, int b)
        {
            return _Random.Next(a, b + 1);
        }

        /// <summary>
        /// Return a random floating point number in the range [a, b].
        /// </summary>
        public static double Uniform(double a, double b)
        {
            return a + (_Random.NextDouble() * (b - a));
        }

        /// <summary>
        /// Return a random element from the non-empty sequence seq.
        /// </summary>
        public static T Choice<T>(Sharpy.List<T> seq)
        {
            if (seq == null || global::Sharpy.Builtins.Len(seq) == 0)
            {
                throw new global::Sharpy.IndexError("Cannot choose from an empty sequence");
            }

            int index = _Random.Next(global::Sharpy.Builtins.Len(seq));
            return seq[index];
        }

        /// <summary>
        /// Shuffle the sequence x in place.
        /// </summary>
        public static void Shuffle<T>(Sharpy.List<T> x)
        {
            if (x == null)
            {
                throw new global::Sharpy.TypeError("'NoneType' object cannot be shuffled");
            }

            int n = global::Sharpy.Builtins.Len(x);
            int i = n - 1;
            while (i > 0)
            {
                int j = _Random.Next(i + 1);
                T temp = x[i];
                x[i] = x[j];
                x[j] = temp;
                i = i - 1;
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
                    throw new global::Sharpy.ValueError(FormattableString.Invariant($"empty range for randrange() ({(start)}, {(stop)}, {(step)})"));
                }

                return start + _Random.Next(width);
            }

            int n = 0;
            if (step > 0)
            {
                n = (step == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((width + step - 1)) / step)));
            }
            else
            {
                n = (step == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((width + step + 1)) / step)));
            }

            if (n <= 0)
            {
                throw new global::Sharpy.ValueError(FormattableString.Invariant($"empty range for randrange() ({(start)}, {(stop)}, {(step)})"));
            }

            return start + step * _Random.Next(n);
        }

        /// <summary>
        /// Return a random floating point number with Gaussian distribution.
        /// </summary>
        public static double Gauss(double mu, double sigma)
        {
            double u1 = _Random.NextDouble();
            double u2 = _Random.NextDouble();
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

            return _Random.Next(1 << k);
        }
    }
}

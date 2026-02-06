using System;
using System.Collections.Generic;
namespace Sharpy.Random
{
    using Sharpy.Core;

    /// <summary>
    /// Pseudo-random number generators for various distributions, similar to Python's random module.
    /// </summary>
    [SharpyModule("random")]
    public static class Random
    {
        private static System.Random _random = new System.Random();
        private static readonly object _lock = new object();

        /// <summary>
        /// Initialize the random number generator with a seed.
        /// </summary>
        /// <param name="seed">The seed value</param>
        public static void Seed(int seed)
        {
            lock (_lock)
            {
                _random = new System.Random(seed);
            }
        }

        /// <summary>
        /// Return a random floating point number in the range [0.0, 1.0).
        /// Renamed from Random() to NextDouble() to avoid CS0542 (member name
        /// matching enclosing type). Matches System.Random.NextDouble() convention.
        /// </summary>
        public static double NextDouble()
        {
            lock (_lock)
            {
                return _random.NextDouble();
            }
        }

        /// <summary>
        /// Return a random integer N such that a &lt;= N &lt;= b.
        /// </summary>
        public static int Randint(int a, int b)
        {
            lock (_lock)
            {
                return _random.Next(a, b + 1);
            }
        }

        /// <summary>
        /// Return a random floating point number N such that a &lt;= N &lt;= b for a &lt;= b
        /// and b &lt;= N &lt;= a for b &lt; a.
        /// </summary>
        public static double Uniform(double a, double b)
        {
            lock (_lock)
            {
                return a + (_random.NextDouble() * (b - a));
            }
        }

        /// <summary>
        /// Return a randomly selected element from a non-empty sequence.
        /// </summary>
        public static T Choice<T>(IList<T> seq)
        {
            if (seq == null || seq.Count == 0)
            {
                throw new IndexError("Cannot choose from an empty sequence");
            }

            lock (_lock)
            {
                int index = _random.Next(seq.Count);
                return seq[index];
            }
        }

        /// <summary>
        /// Return a randomly selected element from a non-empty sequence.
        /// </summary>
        public static T Choice<T>(T[] seq)
        {
            if (seq == null || seq.Length == 0)
            {
                throw new IndexError("Cannot choose from an empty sequence");
            }

            lock (_lock)
            {
                int index = _random.Next(seq.Length);
                return seq[index];
            }
        }

        /// <summary>
        /// Return a randomly selected element from a Sharpy list.
        /// </summary>
        public static T Choice<T>(Sharpy.Core.List<T> seq)
        {
            if (seq == null || seq.__Len__() == 0)
            {
                throw new IndexError("Cannot choose from an empty sequence");
            }

            lock (_lock)
            {
                uint index = (uint)_random.Next((int)seq.__Len__());
                return seq.__GetItem__((int)index);
            }
        }

        /// <summary>
        /// Shuffle the sequence x in place.
        /// </summary>
        public static void Shuffle<T>(IList<T> x)
        {
            if (x == null)
            {
                throw new TypeError("'NoneType' object cannot be shuffled");
            }

            lock (_lock)
            {
                int n = x.Count;
                for (int i = n - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    T temp = x[i];
                    x[i] = x[j];
                    x[j] = temp;
                }
            }
        }

        /// <summary>
        /// Shuffle a Sharpy list in place.
        /// </summary>
        public static void Shuffle<T>(Sharpy.Core.List<T> x)
        {
            if (x == null)
            {
                throw new TypeError("'NoneType' object cannot be shuffled");
            }

            lock (_lock)
            {
                int n = (int)x.__Len__();
                for (int i = n - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    T temp = x.__GetItem__(i);
                    x.__SetItem__(i, x.__GetItem__(j));
                    x.__SetItem__(j, temp);
                }
            }
        }

        /// <summary>
        /// Return a k length list of unique elements chosen from the population sequence.
        /// </summary>
        public static Sharpy.Core.List<T> Sample<T>(IList<T> population, int k)
        {
            if (population == null)
            {
                throw new TypeError("'NoneType' object cannot be sampled");
            }

            if (k < 0)
            {
                throw new ValueError("Sample size cannot be negative");
            }

            if (k > population.Count)
            {
                throw new ValueError("Sample larger than population");
            }

            lock (_lock)
            {
                // Use Fisher-Yates shuffle for large k, HashSet for small k
                if (k > population.Count / 2)
                {
                    // Fisher-Yates shuffle: copy population, shuffle first k elements
                    var copy = new System.Collections.Generic.List<T>(population);
                    int n = copy.Count;
                    for (int i = 0; i < k; i++)
                    {
                        int j = _random.Next(i, n);
                        // Swap copy[i] and copy[j]
                        T temp = copy[i];
                        copy[i] = copy[j];
                        copy[j] = temp;
                    }
                    return new Sharpy.Core.List<T>(copy.GetRange(0, k));
                }
                else
                {
                    var indices = new System.Collections.Generic.HashSet<int>();
                    var result = new System.Collections.Generic.List<T>();

                    while (indices.Count < k)
                    {
                        int index = _random.Next(population.Count);
                        if (indices.Add(index))
                        {
                            result.Add(population[index]);
                        }
                    }

                    return new Sharpy.Core.List<T>(result);
                }
            }
        }
    }
}

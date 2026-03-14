using System;
using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// Pseudo-random number generators for various distributions, similar to Python's random module.
    /// </summary>
    public static partial class Random
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
        /// <param name="a">The lower bound (inclusive).</param>
        /// <param name="b">The upper bound (inclusive).</param>
        /// <returns>A random integer between <paramref name="a"/> and <paramref name="b"/>.</returns>
        /// <example>
        /// <code>
        /// random.randint(1, 6)    # 4 (random die roll)
        /// random.randint(0, 1)    # 0 or 1
        /// </code>
        /// </example>
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
        /// <param name="seq">A non-empty sequence to choose from.</param>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>A randomly selected element.</returns>
        /// <example>
        /// <code>
        /// random.choice([1, 2, 3])       # 2 (random)
        /// random.choice(["a", "b"])      # "a" or "b"
        /// </code>
        /// </example>
        /// <exception cref="IndexError">Thrown if the sequence is null or empty.</exception>
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
        /// <exception cref="IndexError">Thrown if the sequence is null or empty.</exception>
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
        /// Shuffle the sequence x in place.
        /// </summary>
        /// <param name="x">The sequence to shuffle.</param>
        /// <typeparam name="T">The element type.</typeparam>
        /// <example>
        /// <code>
        /// items = [1, 2, 3, 4, 5]
        /// random.shuffle(items)    # items is now shuffled in place
        /// </code>
        /// </example>
        /// <exception cref="TypeError">Thrown if <paramref name="x"/> is null.</exception>
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
        /// Return a randomly-selected element from range(stop) or range(start, stop, step).
        /// </summary>
        public static int Randrange(int stop)
        {
            return Randrange(0, stop, 1);
        }

        /// <summary>
        /// Return a randomly-selected element from range(start, stop).
        /// </summary>
        public static int Randrange(int start, int stop)
        {
            return Randrange(start, stop, 1);
        }

        /// <summary>
        /// Return a randomly-selected element from range(start, stop, step).
        /// </summary>
        /// <exception cref="ValueError">Thrown if step is zero or the range is empty.</exception>
        public static int Randrange(int start, int stop, int step)
        {
            if (step == 0)
            {
                throw new ValueError("zero step for randrange()");
            }

            int width = stop - start;

            if (step == 1)
            {
                if (width <= 0)
                {
                    throw new ValueError($"empty range for randrange() ({start}, {stop}, {step})");
                }

                lock (_lock)
                {
                    return start + _random.Next(width);
                }
            }

            int n;
            if (step > 0)
            {
                n = (width + step - 1) / step;
            }
            else
            {
                n = (width + step + 1) / step;
            }

            if (n <= 0)
            {
                throw new ValueError($"empty range for randrange() ({start}, {stop}, {step})");
            }

            lock (_lock)
            {
                return start + step * _random.Next(n);
            }
        }

        /// <summary>
        /// Gaussian distribution. mu is the mean, and sigma is the standard deviation.
        /// Uses the Box-Muller transform.
        /// </summary>
        public static double Gauss(double mu, double sigma)
        {
            double u1, u2;
            lock (_lock)
            {
                u1 = _random.NextDouble();
                u2 = _random.NextDouble();
            }

            // Box-Muller transform
            double z0 = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return mu + sigma * z0;
        }

        /// <summary>
        /// Returns a non-negative integer with k random bits.
        /// </summary>
        /// <exception cref="ValueError">Thrown if <paramref name="k"/> is negative or greater than 30.</exception>
        public static int Getrandbits(int k)
        {
            if (k < 0)
            {
                throw new ValueError("number of bits must be non-negative");
            }

            if (k == 0)
            {
                return 0;
            }

            if (k > 30)
            {
                throw new ValueError("number of bits must be <= 30 for int return");
            }

            lock (_lock)
            {
                // Generate random bits by getting a random number in [0, 2^k)
                return _random.Next(1 << k);
            }
        }

        /// <summary>
        /// Return a k sized list of elements chosen from the population with replacement,
        /// optionally weighted.
        /// </summary>
        /// <exception cref="ValueError">Thrown if population is empty, k is negative, weights count mismatches, or total weight is non-positive.</exception>
        public static Sharpy.List<T> Choices<T>(IList<T> population, IList<double>? weights = null, int k = 1)
        {
            if (population == null || population.Count == 0)
            {
                throw new ValueError("Cannot choose from an empty population");
            }

            if (k < 0)
            {
                throw new ValueError("k must be non-negative");
            }

            if (weights != null && weights.Count != population.Count)
            {
                throw new ValueError("The number of weights does not match the population");
            }

            var result = new System.Collections.Generic.List<T>(k);

            if (weights == null)
            {
                // Uniform selection
                lock (_lock)
                {
                    for (int i = 0; i < k; i++)
                    {
                        result.Add(population[_random.Next(population.Count)]);
                    }
                }
            }
            else
            {
                // Build cumulative weights
                var cumWeights = new double[weights.Count];
                double total = 0;
                for (int i = 0; i < weights.Count; i++)
                {
                    total += weights[i];
                    cumWeights[i] = total;
                }

                if (total <= 0)
                {
                    throw new ValueError("Total of weights must be greater than zero");
                }

                lock (_lock)
                {
                    for (int i = 0; i < k; i++)
                    {
                        double r = _random.NextDouble() * total;
                        // Binary search for the index
                        int idx = System.Array.BinarySearch(cumWeights, r);
                        if (idx < 0)
                        {
                            idx = ~idx;
                        }
                        if (idx >= population.Count)
                        {
                            idx = population.Count - 1;
                        }
                        result.Add(population[idx]);
                    }
                }
            }

            return new Sharpy.List<T>(result);
        }

        /// <summary>
        /// Return a k length list of unique elements chosen from the population sequence.
        /// </summary>
        /// <exception cref="TypeError">Thrown if <paramref name="population"/> is null.</exception>
        /// <exception cref="ValueError">Thrown if <paramref name="k"/> is negative or larger than the population.</exception>
        public static Sharpy.List<T> Sample<T>(IList<T> population, int k)
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
                    return new Sharpy.List<T>(copy.GetRange(0, k));
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

                    return new Sharpy.List<T>(result);
                }
            }
        }
    }
}

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
        /// Return a randomly selected integer from range(stop) or range(start, stop[, step]).
        /// </summary>
        public static int Randrange(int stop)
        {
            if (stop <= 0)
            {
                throw new ValueError("empty range for randrange()");
            }

            lock (_lock)
            {
                return _random.Next(stop);
            }
        }

        /// <summary>
        /// Return a randomly selected integer from range(start, stop).
        /// </summary>
        public static int Randrange(int start, int stop)
        {
            if (start >= stop)
            {
                throw new ValueError("empty range for randrange()");
            }

            lock (_lock)
            {
                return _random.Next(start, stop);
            }
        }

        /// <summary>
        /// Return a randomly selected integer from range(start, stop, step).
        /// </summary>
        public static int Randrange(int start, int stop, int step)
        {
            if (step == 0)
            {
                throw new ValueError("zero step for randrange()");
            }

            int width;
            if (step > 0)
            {
                width = (stop - start + step - 1) / step;
            }
            else
            {
                width = (start - stop - step - 1) / (-step);
            }

            if (width <= 0)
            {
                throw new ValueError("empty range for randrange()");
            }

            lock (_lock)
            {
                return start + _random.Next(width) * step;
            }
        }

        /// <summary>
        /// Return a random floating point number N from a Gaussian distribution
        /// with mean mu and standard deviation sigma.
        /// </summary>
        public static double Gauss(double mu, double sigma)
        {
            // Box-Muller transform
            double u1, u2;
            lock (_lock)
            {
                u1 = _random.NextDouble();
                u2 = _random.NextDouble();
            }

            double z = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return mu + sigma * z;
        }

        /// <summary>
        /// Return a non-negative integer with k random bits.
        /// </summary>
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
                throw new ValueError("number of bits must be <= 30");
            }

            int maxVal = 1 << k;
            lock (_lock)
            {
                return _random.Next(maxVal);
            }
        }

        /// <summary>
        /// Return a k-sized list of elements chosen from the population with replacement,
        /// optionally weighted.
        /// </summary>
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

            var result = new Sharpy.List<T>();

            if (weights != null)
            {
                if (weights.Count != population.Count)
                {
                    throw new ValueError("The number of weights does not match the population");
                }

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
                        int lo = 0, hi = cumWeights.Length;
                        while (lo < hi)
                        {
                            int mid = (lo + hi) / 2;
                            if (cumWeights[mid] <= r)
                            {
                                lo = mid + 1;
                            }
                            else
                            {
                                hi = mid;
                            }
                        }

                        result.Append(population[lo < population.Count ? lo : population.Count - 1]);
                    }
                }
            }
            else
            {
                lock (_lock)
                {
                    for (int i = 0; i < k; i++)
                    {
                        result.Append(population[_random.Next(population.Count)]);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Return a k length list of unique elements chosen from the population sequence.
        /// </summary>
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

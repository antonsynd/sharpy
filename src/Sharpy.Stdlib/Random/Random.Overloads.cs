using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Generate pseudo-random numbers with various distributions.</summary>
    public static partial class RandomModule
    {
        /// <summary>
        /// Return a randomly selected element from a non-empty array.
        /// </summary>
        /// <exception cref="IndexError">Thrown if the sequence is null or empty.</exception>
        public static T Choice<T>(T[] seq)
        {
            if (seq == null || seq.Length == 0)
            {
                throw new IndexError("Cannot choose from an empty sequence");
            }

            int index = _Random.Next(seq.Length);
            return seq[index];
        }

        /// <summary>
        /// Return a randomly selected element from a non-empty sequence.
        /// </summary>
        /// <exception cref="IndexError">Thrown if the sequence is null or empty.</exception>
        public static T Choice<T>(IList<T> seq)
        {
            if (seq == null || seq.Count == 0)
            {
                throw new IndexError("Cannot choose from an empty sequence");
            }

            int index = _Random.Next(seq.Count);
            return seq[index];
        }

        /// <summary>
        /// Shuffle the sequence x in place.
        /// </summary>
        /// <exception cref="TypeError">Thrown if <paramref name="x"/> is null.</exception>
        public static void Shuffle<T>(IList<T> x)
        {
            if (x == null)
            {
                throw new TypeError("'NoneType' object cannot be shuffled");
            }

            int n = x.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _Random.Next(i + 1);
                T temp = x[i];
                x[i] = x[j];
                x[j] = temp;
            }
        }

        /// <summary>
        /// Return a randomly-selected element from range(stop).
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
                for (int i = 0; i < k; i++)
                {
                    result.Add(population[_Random.Next(population.Count)]);
                }
            }
            else
            {
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

                for (int i = 0; i < k; i++)
                {
                    double r = _Random.NextDouble() * total;
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

            if (k > population.Count / 2)
            {
                var copy = new System.Collections.Generic.List<T>(population);
                int n = copy.Count;
                for (int i = 0; i < k; i++)
                {
                    int j = _Random.Next(i, n);
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
                    int index = _Random.Next(population.Count);
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

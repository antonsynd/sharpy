using System.Collections.Generic;
using System.Linq;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Sums a sequence of integers.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <example>
        /// <code>
        /// sum([1, 2, 3])       # 6
        /// sum(range(10))       # 45
        /// sum([])              # 0
        /// </code>
        /// </example>
        public static int Sum(IEnumerable<int> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of longs.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        public static long Sum(IEnumerable<long> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of floats.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        public static float Sum(IEnumerable<float> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of doubles.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        public static double Sum(IEnumerable<double> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of decimals.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        public static decimal Sum(IEnumerable<decimal> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of integers with a start value.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <param name="start">The initial accumulator value</param>
        /// <returns>The total sum plus start</returns>
        public static int Sum(IEnumerable<int> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return start + iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of longs with a start value.
        /// </summary>
        public static long Sum(IEnumerable<long> iterable, long start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return start + iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of floats with a start value.
        /// </summary>
        public static float Sum(IEnumerable<float> iterable, float start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return start + iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of doubles with a start value.
        /// </summary>
        public static double Sum(IEnumerable<double> iterable, double start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return start + iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of decimals with a start value.
        /// </summary>
        public static decimal Sum(IEnumerable<decimal> iterable, decimal start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            return start + iterable.Sum();
        }

        /// <summary>
        /// Sums a sequence of signed bytes, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<sbyte> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of bytes, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<byte> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of short integers, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<short> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<ushort> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned integers.
        /// </summary>
        public static uint Sum(IEnumerable<uint> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                uint result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers.
        /// </summary>
        public static ulong Sum(IEnumerable<ulong> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                ulong result = 0;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of signed bytes with a start value, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<sbyte> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of bytes with a start value, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<byte> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of short integers with a start value, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<short> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers with a start value, accumulating into int.
        /// </summary>
        public static int Sum(IEnumerable<ushort> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                int result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned integers with a start value.
        /// </summary>
        public static uint Sum(IEnumerable<uint> iterable, uint start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                uint result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers with a start value.
        /// </summary>
        public static ulong Sum(IEnumerable<ulong> iterable, ulong start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            checked
            {
                ulong result = start;
                foreach (var item in iterable)
                    result += item;
                return result;
            }
        }
    }
}

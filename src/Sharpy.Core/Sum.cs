using System.Collections.Generic;
using System.Linq;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Converts the CLR's <see cref="System.OverflowException"/> from a checked accumulation into
        /// the <see cref="OverflowError"/> Python raises — the convention <c>Pow</c>, <c>FloorDiv</c>
        /// and <c>NumericCheckedCast</c> follow — so <c>except OverflowError:</c> catches a
        /// <c>sum</c> overflow at every integer width (#1749).
        /// </summary>
        /// <param name="resultType">The Sharpy name of the accumulator type the result did not fit.</param>
        /// <param name="inner">The CLR exception the checked arithmetic raised.</param>
        private static OverflowError SumOverflow(string resultType, System.OverflowException inner)
        {
            return new OverflowError("sum result too large for " + resultType, inner);
        }

        /// <summary>
        /// Sums a sequence of integers.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
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

            try
            {
                return iterable.Sum();
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of longs.
        /// </summary>
        /// <param name="iterable">The sequence to sum</param>
        /// <returns>The total sum</returns>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int64</c></exception>
        public static long Sum(IEnumerable<long> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                return iterable.Sum();
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int64", ex);
            }
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
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<int> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                return checked(start + iterable.Sum());
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of longs with a start value.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int64</c></exception>
        public static long Sum(IEnumerable<long> iterable, long start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                return checked(start + iterable.Sum());
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int64", ex);
            }
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
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<sbyte> iterable)
        {
            return Sum(iterable, 0);
        }

        /// <summary>
        /// Sums a sequence of bytes, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<byte> iterable)
        {
            return Sum(iterable, 0);
        }

        /// <summary>
        /// Sums a sequence of short integers, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<short> iterable)
        {
            return Sum(iterable, 0);
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<ushort> iterable)
        {
            return Sum(iterable, 0);
        }

        /// <summary>
        /// Sums a sequence of unsigned integers.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit a <c>uint32</c></exception>
        public static uint Sum(IEnumerable<uint> iterable)
        {
            return Sum(iterable, 0u);
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit a <c>uint64</c></exception>
        public static ulong Sum(IEnumerable<ulong> iterable)
        {
            return Sum(iterable, 0UL);
        }

        /// <summary>
        /// Sums a sequence of signed bytes with a start value, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<sbyte> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of bytes with a start value, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<byte> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of short integers with a start value, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<short> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers with a start value, accumulating into int.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit an <c>int32</c></exception>
        public static int Sum(IEnumerable<ushort> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned integers with a start value.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit a <c>uint32</c></exception>
        public static uint Sum(IEnumerable<uint> iterable, uint start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    uint result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("uint32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers with a start value.
        /// </summary>
        /// <exception cref="TypeError">Thrown when <paramref name="iterable"/> is null</exception>
        /// <exception cref="OverflowError">Thrown when the sum does not fit a <c>uint64</c></exception>
        public static ulong Sum(IEnumerable<ulong> iterable, ulong start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    ulong result = start;
                    foreach (var item in iterable)
                        result += item;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("uint64", ex);
            }
        }

        // ── bool (#1772) ──

        /// <summary>
        /// Sums a sequence of booleans, counting <see langword="true"/> as 1.
        /// </summary>
        public static int Sum(IEnumerable<bool> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = 0;
                    foreach (var b in iterable)
                        if (b)
                            result++;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of booleans with an integer start value.
        /// </summary>
        public static int Sum(IEnumerable<bool> iterable, int start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            try
            {
                checked
                {
                    int result = start;
                    foreach (var b in iterable)
                        if (b)
                            result++;
                    return result;
                }
            }
            catch (System.OverflowException ex)
            {
                throw SumOverflow("int32", ex);
            }
        }

        /// <summary>
        /// Sums a sequence of booleans with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<bool> iterable, double start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            double result = start;
            foreach (var b in iterable)
                if (b)
                    result += 1.0;
            return result;
        }

        /// <summary>
        /// Sums a sequence of booleans with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<bool> iterable, decimal start)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("sum", "iterable");
            }

            decimal result = start;
            foreach (var b in iterable)
                if (b)
                    result += 1m;
            return result;
        }

        // ── cross-family double starts (#1780) ──

        /// <summary>
        /// Sums a sequence of signed bytes with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<sbyte> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of bytes with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<byte> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of short integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<short> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<ushort> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<int> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<uint> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of long integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<long> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<ulong> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of floats with a double start value.
        /// </summary>
        public static double Sum(IEnumerable<float> iterable, double start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            double result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        // ── cross-family decimal starts (#1780) ──

        /// <summary>
        /// Sums a sequence of signed bytes with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<sbyte> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of bytes with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<byte> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of short integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<short> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned short integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<ushort> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<int> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<uint> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of long integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<long> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }

        /// <summary>
        /// Sums a sequence of unsigned long integers with a decimal start value.
        /// </summary>
        public static decimal Sum(IEnumerable<ulong> iterable, decimal start)
        {
            if (iterable is null)
                throw TypeError.ArgNone("sum", "iterable");
            decimal result = start;
            foreach (var x in iterable)
                result += x;
            return result;
        }
    }
}

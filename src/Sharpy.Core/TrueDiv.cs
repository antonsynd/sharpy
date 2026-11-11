using System.Runtime.CompilerServices;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Returns <paramref name="x"/> divided by <paramref name="y"/> with Python's true-division
        /// semantics: an exact zero divisor (<c>0.0</c> or <c>-0.0</c>) raises
        /// <see cref="ZeroDivisionError"/> instead of yielding IEEE <c>inf</c>/<c>nan</c>.
        /// </summary>
        /// <remarks>
        /// This is the <c>/</c> lowering for every float64 quotient, including <c>int / int</c>
        /// (whose left operand the emitter casts to <c>double</c>). The guard is one branch on the
        /// divisor and the method is aggressively inlined, so a constant non-zero divisor folds the
        /// branch away after inlining and a variable divisor costs one predictable compare. The
        /// <c>throw</c> stays inline on purpose: the JIT moves a throw block out of the loop, whereas a
        /// call to a throw-helper method (even <c>[DoesNotReturn]</c>) made it spill the caller's loop
        /// accumulator around the cold call, doubling a tight division loop (#2517, measured).
        /// Like <see cref="FloorDiv(double, double)"/>, the guard lives in the helper so each
        /// operand is spliced exactly once and a side-effecting divisor runs once.
        /// </remarks>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The IEEE quotient <c>x / y</c></returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double TrueDiv(double x, double y)
        {
            if (y == 0.0)
            {
                throw new ZeroDivisionError("division by zero");
            }

            return x / y;
        }

        /// <summary>
        /// Returns <paramref name="x"/> divided by <paramref name="y"/> as a <c>float32</c>; a zero
        /// divisor raises <see cref="ZeroDivisionError"/>. See <see cref="TrueDiv(double, double)"/>.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The IEEE quotient <c>x / y</c></returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TrueDiv(float x, float y)
        {
            if (y == 0.0f)
            {
                throw new ZeroDivisionError("division by zero");
            }

            return x / y;
        }

        /// <summary>
        /// Returns <paramref name="x"/> divided by <paramref name="y"/> as a <c>decimal</c>; a zero
        /// divisor raises <see cref="ZeroDivisionError"/> (CPython raises
        /// <c>decimal.DivisionByZero</c>, a <c>ZeroDivisionError</c> subclass) rather than the CLR's
        /// <see cref="System.DivideByZeroException"/>. As a runtime parameter the divisor is never a
        /// C# constant, so a literal <c>7m / 0m</c> no longer reaches Roslyn as CS0020.
        /// </summary>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The quotient <c>x / y</c></returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal TrueDiv(decimal x, decimal y)
        {
            if (y == 0m)
            {
                throw new ZeroDivisionError("division by zero");
            }

            return x / y;
        }

        /// <summary>
        /// Returns <paramref name="x"/> raised to the power <paramref name="y"/> with Python's
        /// <c>float.__pow__</c> zero rule: zero (<c>0.0</c> or <c>-0.0</c>) raised to a finite
        /// negative power raises <see cref="ZeroDivisionError"/> instead of yielding <c>inf</c>.
        /// A negative-infinity exponent keeps the IEEE answer (<c>0.0 ** -inf</c> is <c>inf</c> in
        /// CPython too), and every other case is <see cref="System.Math.Pow(double, double)"/>.
        /// </summary>
        /// <remarks>
        /// This is the <c>**</c> lowering for float64 and float32 operands (float32 casts the
        /// result back) and the body of the <c>pow()</c> builtin's float overloads.
        /// </remarks>
        /// <param name="x">The base</param>
        /// <param name="y">The exponent</param>
        /// <returns>x raised to the power y</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="x"/> is zero and
        /// <paramref name="y"/> is a finite negative number</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FloatPow(double x, double y)
        {
            if (x == 0.0 && y < 0.0 && !double.IsNegativeInfinity(y))
            {
                throw new ZeroDivisionError("zero to a negative power");
            }

            return System.Math.Pow(x, y);
        }
    }
}

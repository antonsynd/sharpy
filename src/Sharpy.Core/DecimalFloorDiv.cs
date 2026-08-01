namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Returns the truncated quotient of <paramref name="x"/> divided by
        /// <paramref name="y"/>, matching CPython's <c>Decimal.__floordiv__</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Decimal <c>//</c> deliberately does NOT floor: the quotient truncates toward zero,
        /// so <c>Decimal(-7) // Decimal(3)</c> is <c>-2</c> where int <c>-7 // 3</c> is
        /// <c>-3</c>. That is both the spec's native-decimal policy and CPython's own decimal
        /// behavior (#1174), which is why this is not an overload of
        /// <see cref="FloorDiv(double, double)"/> — it computes a different function.
        /// </para>
        /// <para>
        /// The zero guard lives here, not in the emitted C#, so the <c>//</c> lowering splices
        /// each operand expression exactly once and a side-effecting divisor runs once
        /// (#1216) — the same reason <see cref="FloorDiv(double, double)"/> and
        /// <see cref="FloorMod(double, double)"/> own their guards.
        /// </para>
        /// <para>
        /// The emitter previously used <c>Decimal.Divide</c> rather than <c>/</c> because a
        /// literal zero divisor (<c>7m // 0m</c>) through <c>/</c> is a compile-time C# error
        /// (CS0020, "division by <b>constant</b> zero") even in the unreachable arm of a
        /// guarding ternary. That workaround does not apply inside this helper — <paramref
        /// name="x"/> and <paramref name="y"/> are runtime parameters, never constants — so
        /// plain <c>/</c> is used here and the workaround must not be restored.
        /// </para>
        /// </remarks>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The quotient truncated toward zero</returns>
        /// <exception cref="ZeroDivisionError">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// DecimalFloorDiv(7m, 3m)    // 2
        /// DecimalFloorDiv(-7m, 3m)   // -2  (truncated; int -7 // 3 is -3)
        /// DecimalFloorDiv(7m, -3m)   // -2
        /// DecimalFloorDiv(-7m, -3m)  // 2
        /// </code>
        /// </example>
        public static decimal DecimalFloorDiv(decimal x, decimal y)
        {
            // CPython raises decimal.DivisionByZero here, which IS a ZeroDivisionError
            // subclass, so the Sharpy surface uses ZeroDivisionError like every other
            // division. (Decimal `%` deliberately differs — see DecimalMod.)
            if (y == 0m)
            {
                throw new ZeroDivisionError("decimal floor division by zero");
            }

            return decimal.Truncate(x / y);
        }
    }
}

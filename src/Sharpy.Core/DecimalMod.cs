namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Returns the native truncating remainder of <paramref name="x"/> divided by
        /// <paramref name="y"/>, matching CPython's <c>Decimal.__mod__</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The result takes the sign of the DIVIDEND, so <c>Decimal(-7) % Decimal(3)</c> is
        /// <c>-1</c> where int <c>-7 % 3</c> is <c>2</c>. Decimal sits outside the floored-`%`
        /// allowlist by design (#1153, #1189), which is why this is not an overload of
        /// <see cref="FloorMod(double, double)"/> — it computes a different function.
        /// </para>
        /// <para>
        /// A zero divisor raises <see cref="InvalidOperation"/>, NOT
        /// <see cref="ZeroDivisionError"/>: CPython raises <c>decimal.InvalidOperation</c>
        /// here, a sibling of <c>ZeroDivisionError</c> rather than a subclass — unlike decimal
        /// <c>//</c>, whose <c>decimal.DivisionByZero</c> IS a <c>ZeroDivisionError</c>. The
        /// asymmetry with <see cref="DecimalFloorDiv"/> is deliberate; do not unify them.
        /// </para>
        /// <para>
        /// The zero guard lives here, not in the emitted C#, so the <c>%</c> lowering splices
        /// each operand expression exactly once and a side-effecting divisor runs once
        /// (#1216) — the same reason <see cref="FloorMod(double, double)"/> owns its guard.
        /// </para>
        /// <para>
        /// The emitter previously used <c>Decimal.Remainder</c> rather than <c>%</c> because a
        /// literal zero divisor (<c>7m % 0m</c>) through <c>%</c> is a compile-time C# error
        /// (CS0020, "division by <b>constant</b> zero") even in the unreachable arm of a
        /// guarding ternary. That workaround does not apply inside this helper — <paramref
        /// name="x"/> and <paramref name="y"/> are runtime parameters, never constants — so
        /// plain <c>%</c> is used here and the workaround must not be restored. It is the same
        /// operation either way: <c>decimal.op_Modulus</c> invokes <c>Decimal.Remainder</c>.
        /// </para>
        /// </remarks>
        /// <param name="x">The dividend</param>
        /// <param name="y">The divisor</param>
        /// <returns>The truncating remainder (sign of the dividend)</returns>
        /// <exception cref="InvalidOperation">Thrown when <paramref name="y"/> is zero</exception>
        /// <example>
        /// <code>
        /// DecimalMod(7m, 3m)    // 1
        /// DecimalMod(-7m, 3m)   // -1  (sign of dividend; int -7 % 3 is 2)
        /// DecimalMod(7m, -3m)   // 1
        /// DecimalMod(-7m, -3m)  // -1
        /// </code>
        /// </example>
        public static decimal DecimalMod(decimal x, decimal y)
        {
            if (y == 0m)
            {
                throw new InvalidOperation("decimal modulo by zero");
            }

            return x % y;
        }
    }
}

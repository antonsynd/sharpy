using System;

namespace Sharpy
{
    /// <summary>
    /// A complex number type, similar to Python's complex.
    /// </summary>
    public readonly struct Complex
    {
        private readonly System.Numerics.Complex _inner;

        private Complex(System.Numerics.Complex c)
        {
            _inner = c;
        }

        /// <summary>Create a complex number with the given real and imaginary parts.</summary>
        public Complex(double real, double imaginary)
        {
            _inner = new System.Numerics.Complex(real, imaginary);
        }

        /// <summary>Implicit conversion to System.Numerics.Complex.</summary>
        public static implicit operator System.Numerics.Complex(Complex c) => c._inner;
        /// <summary>Implicit conversion from System.Numerics.Complex.</summary>
        public static implicit operator Complex(System.Numerics.Complex c) => new(c);

        /// <summary>The real part of the complex number.</summary>
        public double Real => _inner.Real;
        /// <summary>The imaginary part of the complex number.</summary>
        public double Imag => _inner.Imaginary;

        /// <summary>Return the complex conjugate.</summary>
        public Complex Conjugate()
        {
            return System.Numerics.Complex.Conjugate(_inner);
        }

        /// <summary>Add two complex numbers.</summary>
        public static Complex operator +(Complex a, Complex b) => a._inner + b._inner;

        /// <summary>Subtract <paramref name="b"/> from <paramref name="a"/>.</summary>
        public static Complex operator -(Complex a, Complex b) => a._inner - b._inner;

        /// <summary>Multiply two complex numbers.</summary>
        public static Complex operator *(Complex a, Complex b) => a._inner * b._inner;

        /// <summary>Divide <paramref name="a"/> by <paramref name="b"/>.</summary>
        public static Complex operator /(Complex a, Complex b) => a._inner / b._inner;

        /// <summary>Negate a complex number.</summary>
        public static Complex operator -(Complex a) => -a._inner;

        /// <summary>Determine whether two complex numbers are equal.</summary>
        public static bool operator ==(Complex a, Complex b) => a._inner == b._inner;

        /// <summary>Determine whether two complex numbers differ.</summary>
        public static bool operator !=(Complex a, Complex b) => a._inner != b._inner;

        // ---------------------------------------------------------------------
        // The numeric tower: complex mixed with int/long/float (#1507)
        // ---------------------------------------------------------------------
        //
        // CPython promotes the real operand to complex and returns complex, so `complex(1,2) + 1`
        // is `(2+2j)`. Sharpy declared only Complex(+)Complex, so the same expression drew
        // SPY0222. Values below were measured against python3 3.12 before implementing.
        //
        // THE int OVERLOADS ARE REQUIRED, NOT CONVENIENCE. With (Complex, long) and
        // (Complex, double) both present and no (Complex, int), a C# `int` argument is ambiguous —
        // int->long and int->double are both implicit conversions and neither is better — so the
        // call fails with CS0121 rather than silently picking one. Every operand pair is therefore
        // spelled out: six per operator.
        //
        // `bool` is EXCLUDED by ruling (2026-08-13), and it is a real exclusion rather than an
        // oversight: CPython does promote it, because bool subclasses int there, so `complex(1,2) +
        // True` is `(2+2j)` (measured). Sharpy scopes the tower to {int, long, float}, so a bool
        // operand keeps drawing SPY0222.
        //
        // Each overload widens its scalar to a real-valued Complex and defers to the
        // Complex(+)Complex operator above rather than reaching into System.Numerics directly, so
        // there is exactly one place where each operation's arithmetic lives.

        /// <summary>A real number as a complex one, for the mixed-operand overloads below.</summary>
        private static Complex FromReal(double value) => new Complex(value, 0.0);

        /// <summary>Add a complex number and an <see cref="int"/>.</summary>
        public static Complex operator +(Complex a, int b) => a + FromReal(b);
        /// <summary>Add an <see cref="int"/> and a complex number.</summary>
        public static Complex operator +(int a, Complex b) => FromReal(a) + b;
        /// <summary>Add a complex number and a <see cref="long"/>.</summary>
        public static Complex operator +(Complex a, long b) => a + FromReal(b);
        /// <summary>Add a <see cref="long"/> and a complex number.</summary>
        public static Complex operator +(long a, Complex b) => FromReal(a) + b;
        /// <summary>Add a complex number and a <see cref="double"/>.</summary>
        public static Complex operator +(Complex a, double b) => a + FromReal(b);
        /// <summary>Add a <see cref="double"/> and a complex number.</summary>
        public static Complex operator +(double a, Complex b) => FromReal(a) + b;

        /// <summary>Subtract an <see cref="int"/> from a complex number.</summary>
        public static Complex operator -(Complex a, int b) => a - FromReal(b);
        /// <summary>Subtract a complex number from an <see cref="int"/>.</summary>
        public static Complex operator -(int a, Complex b) => FromReal(a) - b;
        /// <summary>Subtract a <see cref="long"/> from a complex number.</summary>
        public static Complex operator -(Complex a, long b) => a - FromReal(b);
        /// <summary>Subtract a complex number from a <see cref="long"/>.</summary>
        public static Complex operator -(long a, Complex b) => FromReal(a) - b;
        /// <summary>Subtract a <see cref="double"/> from a complex number.</summary>
        public static Complex operator -(Complex a, double b) => a - FromReal(b);
        /// <summary>Subtract a complex number from a <see cref="double"/>.</summary>
        public static Complex operator -(double a, Complex b) => FromReal(a) - b;

        /// <summary>Multiply a complex number by an <see cref="int"/>.</summary>
        public static Complex operator *(Complex a, int b) => a * FromReal(b);
        /// <summary>Multiply an <see cref="int"/> by a complex number.</summary>
        public static Complex operator *(int a, Complex b) => FromReal(a) * b;
        /// <summary>Multiply a complex number by a <see cref="long"/>.</summary>
        public static Complex operator *(Complex a, long b) => a * FromReal(b);
        /// <summary>Multiply a <see cref="long"/> by a complex number.</summary>
        public static Complex operator *(long a, Complex b) => FromReal(a) * b;
        /// <summary>Multiply a complex number by a <see cref="double"/>.</summary>
        public static Complex operator *(Complex a, double b) => a * FromReal(b);
        /// <summary>Multiply a <see cref="double"/> by a complex number.</summary>
        public static Complex operator *(double a, Complex b) => FromReal(a) * b;

        /// <summary>Divide a complex number by an <see cref="int"/>.</summary>
        public static Complex operator /(Complex a, int b) => a / FromReal(b);
        /// <summary>Divide an <see cref="int"/> by a complex number.</summary>
        public static Complex operator /(int a, Complex b) => FromReal(a) / b;
        /// <summary>Divide a complex number by a <see cref="long"/>.</summary>
        public static Complex operator /(Complex a, long b) => a / FromReal(b);
        /// <summary>Divide a <see cref="long"/> by a complex number.</summary>
        public static Complex operator /(long a, Complex b) => FromReal(a) / b;
        /// <summary>Divide a complex number by a <see cref="double"/>.</summary>
        public static Complex operator /(Complex a, double b) => a / FromReal(b);
        /// <summary>Divide a <see cref="double"/> by a complex number.</summary>
        public static Complex operator /(double a, Complex b) => FromReal(a) / b;

        // Comparison against a real. CPython: complex(3,0) == 3 is True and complex(1,2) == 1 is
        // False — a complex equals a real exactly when its imaginary part is zero and the reals
        // match, which is what widening to Complex(b, 0.0) already says.

        /// <summary>Whether a complex number equals an <see cref="int"/>.</summary>
        public static bool operator ==(Complex a, int b) => a == FromReal(b);
        /// <summary>Whether an <see cref="int"/> equals a complex number.</summary>
        public static bool operator ==(int a, Complex b) => FromReal(a) == b;
        /// <summary>Whether a complex number equals a <see cref="long"/>.</summary>
        public static bool operator ==(Complex a, long b) => a == FromReal(b);
        /// <summary>Whether a <see cref="long"/> equals a complex number.</summary>
        public static bool operator ==(long a, Complex b) => FromReal(a) == b;
        /// <summary>Whether a complex number equals a <see cref="double"/>.</summary>
        public static bool operator ==(Complex a, double b) => a == FromReal(b);
        /// <summary>Whether a <see cref="double"/> equals a complex number.</summary>
        public static bool operator ==(double a, Complex b) => FromReal(a) == b;

        /// <summary>Whether a complex number differs from an <see cref="int"/>.</summary>
        public static bool operator !=(Complex a, int b) => !(a == b);
        /// <summary>Whether an <see cref="int"/> differs from a complex number.</summary>
        public static bool operator !=(int a, Complex b) => !(a == b);
        /// <summary>Whether a complex number differs from a <see cref="long"/>.</summary>
        public static bool operator !=(Complex a, long b) => !(a == b);
        /// <summary>Whether a <see cref="long"/> differs from a complex number.</summary>
        public static bool operator !=(long a, Complex b) => !(a == b);
        /// <summary>Whether a complex number differs from a <see cref="double"/>.</summary>
        public static bool operator !=(Complex a, double b) => !(a == b);
        /// <summary>Whether a <see cref="double"/> differs from a complex number.</summary>
        public static bool operator !=(double a, Complex b) => !(a == b);

        // == and != on a type that does not also override Equals(object)/GetHashCode() raise
        // CS0660/CS0661, which are ERRORS here rather than warnings because TreatWarningsAsErrors is
        // on solution-wide. All four ship together or none of them build.

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Complex other && _inner == other._inner;

        /// <inheritdoc/>
        /// <remarks>
        /// KNOWN DIVERGENCE (#1543), recorded rather than silently left. #1507 widened <c>==</c>
        /// so that <c>complex(3,0) == 3</c> is <c>true</c>, matching CPython. CPython ALSO keeps
        /// its hash consistent with that equality — <c>hash(complex(3,0)) == hash(3) == 3</c>
        /// (measured, python3 3.12) — because Python's numeric tower guarantees equal numbers hash
        /// equally across types. This delegates to <c>System.Numerics.Complex.GetHashCode()</c>,
        /// which combines both components and so does NOT agree with <c>3.GetHashCode()</c>.
        ///
        /// <para>
        /// The .NET contract this breaks is narrower than it looks: <c>Equals(object)</c> is still
        /// consistent with <c>GetHashCode</c>, because it only ever returns <c>true</c> for
        /// another <c>Complex</c>. The gap is cross-TYPE — a real-valued Complex and the equal
        /// <c>int</c> hash differently — which matters only for a heterogeneous hash container
        /// keyed on <c>object</c>. Closing it means deciding the whole tower's hash contract
        /// (int/long/double/decimal/complex together), not patching this one method: a per-type
        /// patch would leave SOME cross-type pairs consistent and others not, which is harder to
        /// reason about than the current uniform divergence. Tracked as #1543, which carries the
        /// measured cells and the Axiom 1/2 tension; #1507 delivered the arithmetic and equality
        /// half and closes without it.
        /// </para>
        /// </remarks>
        public override int GetHashCode() => _inner.GetHashCode();

        /// <summary>
        /// Render in CPython's <c>complex</c> form: <c>(4+1j)</c>, <c>1j</c>, <c>(-1-2j)</c>.
        /// </summary>
        /// <remarks>
        /// Every rule below was measured against python3 3.12 rather than inferred, because the
        /// shape is less regular than it looks:
        /// <list type="bullet">
        /// <item><description>
        /// A real part of POSITIVE zero drops the parentheses and the real term entirely —
        /// <c>complex(0, 1)</c> is <c>1j</c>, not <c>(0+1j)</c>, and <c>complex(0, 0)</c> is
        /// <c>0j</c>. NEGATIVE zero does not: <c>complex(-0.0, 1.0)</c> is <c>(-0+1j)</c>. So the
        /// test is on the sign bit, not on <c>== 0</c>.
        /// </description></item>
        /// <item><description>
        /// The imaginary sign comes from ITS sign bit too, so <c>complex(-0.0, -0.0)</c> is
        /// <c>(-0-0j)</c> while <c>complex(1, nan)</c> is <c>(1+nanj)</c>.
        /// </description></item>
        /// <item><description>
        /// Components print SHORTEST-ROUND-TRIP WITHOUT a trailing <c>.0</c>: <c>complex(4.0, 1.0)</c>
        /// is <c>(4+1j)</c> even though <c>repr(4.0)</c> is <c>4.0</c>. Precision is not otherwise
        /// reduced — <c>complex(1,2)/complex(3,-1)</c> is <c>(0.1+0.7000000000000001j)</c>, which is
        /// the cell a naive formatter gets wrong.
        /// </description></item>
        /// </list>
        /// </remarks>
        public override string ToString()
        {
            double real = _inner.Real;
            double imag = _inner.Imaginary;

            // Positive zero real: CPython omits the real term and the parentheses.
            if (real == 0.0 && !IsNegative(real))
            {
                return Component(imag) + "j";
            }

            string sign = IsNegative(imag) ? "-" : "+";
            return "(" + Component(real) + sign + Component(Math.Abs(imag)) + "j)";
        }

        /// <summary>
        /// <see cref="Builtins.FormatFloat(double)"/> with any trailing <c>.0</c> removed, which is
        /// the only way complex component formatting differs from <c>float</c>'s.
        /// </summary>
        private static string Component(double value)
        {
            string text = Builtins.FormatFloat(value);
            return text.EndsWith(".0", StringComparison.Ordinal)
                ? text.Substring(0, text.Length - 2)
                : text;
        }

        /// <summary>
        /// Whether <paramref name="value"/>'s sign bit is set — true for negative zero, which is
        /// what <c>value &lt; 0</c> would miss.
        /// </summary>
        /// <remarks>
        /// NaN is reported as NOT negative, and that is a deliberate divergence from the raw sign
        /// bit rather than an oversight. The two runtimes disagree on which NaN they hand you:
        /// CPython's is <c>0x7ff8000000000000</c> (sign clear) while .NET's <c>double.NaN</c> is
        /// <c>0xfff8000000000000</c> (sign SET). Reading the bit faithfully would print
        /// <c>(1-nanj)</c> where CPython prints <c>(1+nanj)</c> — a divergence produced by copying
        /// the mechanism instead of the behaviour. Measured on both sides, not assumed.
        /// </remarks>
        private static bool IsNegative(double value)
            => !double.IsNaN(value) && BitConverter.DoubleToInt64Bits(value) < 0;
    }
}

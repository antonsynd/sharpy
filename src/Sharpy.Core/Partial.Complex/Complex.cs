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

        // == and != on a type that does not also override Equals(object)/GetHashCode() raise
        // CS0660/CS0661, which are ERRORS here rather than warnings because TreatWarningsAsErrors is
        // on solution-wide. All four ship together or none of them build.

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Complex other && _inner == other._inner;

        /// <inheritdoc/>
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

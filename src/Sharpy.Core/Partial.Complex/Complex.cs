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
    }
}

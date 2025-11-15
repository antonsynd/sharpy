namespace Sharpy.Core;

public readonly struct Complex
{
    private readonly System.Numerics.Complex _inner;

    private Complex(System.Numerics.Complex c)
    {
        _inner = c;
    }

    public Complex(double real, double imaginary)
    {
        _inner = new System.Numerics.Complex(real, imaginary);
    }

    public static implicit operator System.Numerics.Complex(Complex c) => c._inner;
    public static implicit operator Complex(System.Numerics.Complex c) => new(c);

    public double Real => _inner.Real;
    public double Imag => _inner.Imaginary;

    public Complex Conjugate()
    {
        return System.Numerics.Complex.Conjugate(_inner);
    }
}

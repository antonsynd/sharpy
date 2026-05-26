using System;
using Xunit;
using FluentAssertions;
using BclComplex = System.Numerics.Complex;

namespace Sharpy.Core.Tests;

public class NumpyFftTests
{
    [Fact]
    public void Fft_ConstantSignal_HasOnlyDcComponent()
    {
        var arr = Numpy.Array(new[] { 1.0, 1.0, 1.0, 1.0 });
        var result = NumpyFft.Fft(arr);
        result.Shape.Should().Equal(new[] { 4 });
        result._data[0].Real.Should().BeApproximately(4.0, 1e-9);
        result._data[0].Imaginary.Should().BeApproximately(0.0, 1e-9);
        for (int i = 1; i < 4; i++)
        {
            result._data[i].Magnitude.Should().BeApproximately(0.0, 1e-9);
        }
    }

    [Fact]
    public void Fft_Ifft_Roundtrip_RecoversInput()
    {
        var input = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 });
        var forward = NumpyFft.Fft(input);
        var inverse = NumpyFft.Ifft(forward);
        for (int i = 0; i < input.Size; i++)
        {
            inverse._data[i].Real.Should().BeApproximately(input._data[i], 1e-9);
            inverse._data[i].Imaginary.Should().BeApproximately(0.0, 1e-9);
        }
    }

    [Fact]
    public void Fft_ThrowsForNon1D()
    {
        var arr = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        Action act = () => NumpyFft.Fft(arr);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Ifft_ThrowsForNon1D()
    {
        var arr = new NdArray<BclComplex>(
            new[] { BclComplex.One, BclComplex.Zero, BclComplex.One, BclComplex.Zero },
            new[] { 2, 2 });
        Action act = () => NumpyFft.Ifft(arr);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fftfreq_EvenLength_MatchesNumPy()
    {
        var freqs = NumpyFft.Fftfreq(8, 1.0);
        var expected = new[] { 0.0, 0.125, 0.25, 0.375, -0.5, -0.375, -0.25, -0.125 };
        for (int i = 0; i < 8; i++)
        {
            freqs._data[i].Should().BeApproximately(expected[i], 1e-12);
        }
    }

    [Fact]
    public void Fftfreq_OddLength_MatchesNumPy()
    {
        var freqs = NumpyFft.Fftfreq(7, 1.0);
        var expected = new[] { 0.0, 1.0 / 7, 2.0 / 7, 3.0 / 7, -3.0 / 7, -2.0 / 7, -1.0 / 7 };
        for (int i = 0; i < 7; i++)
        {
            freqs._data[i].Should().BeApproximately(expected[i], 1e-12);
        }
    }

    [Fact]
    public void Fftfreq_WithCustomSpacing()
    {
        var freqs = NumpyFft.Fftfreq(4, 0.5);
        var expected = new[] { 0.0, 0.5, -1.0, -0.5 };
        for (int i = 0; i < 4; i++)
        {
            freqs._data[i].Should().BeApproximately(expected[i], 1e-12);
        }
    }

    [Fact]
    public void Fftfreq_ZeroLength()
    {
        var freqs = NumpyFft.Fftfreq(0);
        freqs.Shape.Should().Equal(new[] { 0 });
        freqs.Size.Should().Be(0);
    }

    [Fact]
    public void Fftfreq_NegativeN_Throws()
    {
        Action act = () => NumpyFft.Fftfreq(-1);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Fftfreq_LengthOne_ReturnsSingleZero()
    {
        // np.fft.fftfreq(1) == [0.]
        var freqs = NumpyFft.Fftfreq(1);
        freqs.Shape.Should().Equal(new[] { 1 });
        freqs._data[0].Should().BeApproximately(0.0, 1e-12);
    }

    [Fact]
    public void Fftfreq_NegativeSpacing_MatchesNumPy()
    {
        // np.fft.fftfreq(4, -1.0) == [-0., -0.25, 0.5, 0.25]
        var freqs = NumpyFft.Fftfreq(4, -1.0);
        var expected = new[] { 0.0, -0.25, 0.5, 0.25 };
        for (int i = 0; i < 4; i++)
        {
            freqs._data[i].Should().BeApproximately(expected[i], 1e-12);
        }
    }

    [Fact]
    public void Fft_SingleElement_ReturnsSameValue()
    {
        // np.fft.fft([3.0]) == [3.+0.j]
        var arr = Numpy.Array(new[] { 3.0 });
        var result = NumpyFft.Fft(arr);
        result.Shape.Should().Equal(new[] { 1 });
        result._data[0].Real.Should().BeApproximately(3.0, 1e-9);
        result._data[0].Imaginary.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void Fft_TwoElements_MatchesNumPy()
    {
        // np.fft.fft([2.0, 7.0]) == [9.+0.j, -5.+0.j]
        var arr = Numpy.Array(new[] { 2.0, 7.0 });
        var result = NumpyFft.Fft(arr);
        result._data[0].Real.Should().BeApproximately(9.0, 1e-9);
        result._data[0].Imaginary.Should().BeApproximately(0.0, 1e-9);
        result._data[1].Real.Should().BeApproximately(-5.0, 1e-9);
        result._data[1].Imaginary.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void Fft_NonPowerOfTwoLength_MatchesNumPy()
    {
        // np.fft.fft([1,2,3]) == [6+0j, -1.5+0.8660254j, -1.5-0.8660254j]
        var arr = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var result = NumpyFft.Fft(arr);
        result.Shape.Should().Equal(new[] { 3 });
        result._data[0].Real.Should().BeApproximately(6.0, 1e-9);
        result._data[0].Imaginary.Should().BeApproximately(0.0, 1e-9);
        result._data[1].Real.Should().BeApproximately(-1.5, 1e-9);
        result._data[1].Imaginary.Should().BeApproximately(0.8660254, 1e-6);
        result._data[2].Real.Should().BeApproximately(-1.5, 1e-9);
        result._data[2].Imaginary.Should().BeApproximately(-0.8660254, 1e-6);
    }

    [Fact]
    public void Fft_Ifft_Roundtrip_OddNonPowerOfTwoLength()
    {
        // ifft(fft(x)) ~= x even for non-power-of-two odd lengths.
        var input = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });
        var roundtrip = NumpyFft.Ifft(NumpyFft.Fft(input));
        for (int i = 0; i < input.Size; i++)
        {
            roundtrip._data[i].Real.Should().BeApproximately(input._data[i], 1e-9);
            roundtrip._data[i].Imaginary.Should().BeApproximately(0.0, 1e-9);
        }
    }

    [Fact]
    public void Ifft_SingleElement_ReturnsSameValue()
    {
        var arr = new NdArray<BclComplex>(new[] { new BclComplex(5.0, 0.0) }, new[] { 1 });
        var result = NumpyFft.Ifft(arr);
        result.Shape.Should().Equal(new[] { 1 });
        result._data[0].Real.Should().BeApproximately(5.0, 1e-9);
        result._data[0].Imaginary.Should().BeApproximately(0.0, 1e-9);
    }
}

// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using np = global::Sharpy.Numpy;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyFftTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_fft_tests")]
        public static partial class NumpyFftTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyFftTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFftfreqEvenLength()
            {
#line (23, 5) - (23, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(8, 1.0d);
#line (24, 5) - (24, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var expected = np.Array(new Sharpy.List<double>() { 0.0d, 0.125d, 0.25d, 0.375d, -0.5d, -0.375d, -0.25d, -0.125d });
#line (25, 5) - (25, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.True(np.Allclose(freqs, expected, rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestFftfreqOddLength()
            {
#line (29, 5) - (29, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(7, 1.0d);
#line (30, 5) - (30, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.Equal(7, freqs.Size);
#line (31, 5) - (31, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(freqs[0] - 0.0d) < 1e-12d);
            }

            [Xunit.FactAttribute]
            public void TestFftfreqCustomSpacing()
            {
#line (35, 5) - (35, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(4, 0.5d);
#line (36, 5) - (36, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var expected = np.Array(new Sharpy.List<double>() { 0.0d, 0.5d, -1.0d, -0.5d });
#line (37, 5) - (37, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.True(np.Allclose(freqs, expected, rtol: 0.0d, atol: 1e-12d));
            }

            [Xunit.FactAttribute]
            public void TestFftfreqZeroLength()
            {
#line (41, 5) - (41, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(0);
#line (42, 5) - (42, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.Equal(0, freqs.Shape[0]);
#line (43, 5) - (43, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.Equal(0, freqs.Size);
            }

            [Xunit.FactAttribute]
            public void TestFftfreqNegativeNThrows()
            {
#line (47, 5) - (50, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (48, 9) - (48, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                    global::Sharpy.NumpyFft.Fftfreq(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFftfreqLengthOne()
            {
#line (52, 5) - (52, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(1);
#line (53, 5) - (53, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.Equal(1, freqs.Shape[0]);
#line (54, 5) - (54, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(freqs[0]) < 1e-12d);
            }

            [Xunit.FactAttribute]
            public void TestFftfreqNegativeSpacing()
            {
#line (58, 5) - (58, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var freqs = global::Sharpy.NumpyFft.Fftfreq(4, -1.0d);
#line (59, 5) - (59, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                var expected = np.Array(new Sharpy.List<double>() { 0.0d, -0.25d, 0.5d, 0.25d });
#line (60, 5) - (60, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_fft_tests.spy"
                Xunit.Assert.True(np.Allclose(freqs, expected, rtol: 0.0d, atol: 1e-12d));
            }
        }
    }
}

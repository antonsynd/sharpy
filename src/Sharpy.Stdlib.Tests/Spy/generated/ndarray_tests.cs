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
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NdarrayTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.ndarray_tests")]
        public static partial class NdarrayTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NdarrayTestsTests
        {
            [Xunit.FactAttribute]
            public void TestProperties1d()
            {
#line (31, 5) - (31, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (32, 5) - (32, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(1, arr.Ndim);
#line (33, 5) - (33, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, arr.Size);
#line (34, 5) - (34, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProperties2d()
            {
#line (38, 5) - (38, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (39, 5) - (39, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (40, 5) - (40, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(6, arr.Size);
#line (41, 5) - (41, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (42, 5) - (42, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestProperties3d()
            {
#line (46, 5) - (46, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Zeros(24).Reshape(2, 3, 4);
#line (47, 5) - (47, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, arr.Ndim);
#line (48, 5) - (48, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(24, arr.Size);
#line (49, 5) - (49, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (50, 5) - (50, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (51, 5) - (51, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStrides1d()
            {
#line (57, 5) - (57, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (59, 5) - (59, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStrides2d()
            {
#line (63, 5) - (63, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (65, 5) - (65, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 0));
#line (66, 5) - (66, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStrides3d()
            {
#line (70, 5) - (70, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                var arr = np.Zeros(24).Reshape(2, 3, 4);
#line (72, 5) - (72, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 0));
#line (73, 5) - (73, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 1));
#line (74, 5) - (74, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/ndarray_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Strides, 2));
#line hidden
            }
        }
    }
}
#line default

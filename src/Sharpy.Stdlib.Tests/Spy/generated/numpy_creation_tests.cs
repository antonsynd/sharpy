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
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyCreationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_creation_tests")]
        public static partial class NumpyCreationTests
        {
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyCreationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestArrayFrom1dDataCreatesNdarray()
            {
#line (25, 5) - (25, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (26, 5) - (26, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(1, arr.Ndim);
#line (27, 5) - (27, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, arr.Size);
#line (28, 5) - (28, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (29, 5) - (29, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArrayCopiesData()
            {
#line (33, 5) - (33, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var data = new Sharpy.List<double>()
                {
                    1.0d,
                    2.0d,
                    3.0d
                };
#line (34, 5) - (34, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(data);
#line (35, 5) - (35, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                data[0] = 99.0d;
#line (37, 5) - (37, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArrayEmptyCreatesEmpty()
            {
#line (41, 5) - (41, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
                {
                };
#line (42, 5) - (42, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(empty);
#line (43, 5) - (43, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line (44, 5) - (44, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
            }

            [Xunit.FactAttribute]
            public void TestZeros1dAllElementsZero()
            {
#line (50, 5) - (50, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(5);
#line (51, 5) - (51, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (52, 5) - (52, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 0.0d, 0.0d })));
            }

            [Xunit.FactAttribute]
            public void TestZeros2dAllElementsZero()
            {
#line (56, 5) - (56, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(2, 3);
#line (57, 5) - (57, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (58, 5) - (58, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (59, 5) - (59, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (60, 5) - (60, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(6, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestZerosDtypeIsFloat64()
            {
#line (64, 5) - (64, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(3);
#line (65, 5) - (65, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal("float64", arr.Dtype);
            }

            [Xunit.FactAttribute]
            public void TestZerosNegativeDimensionThrows()
            {
#line (69, 5) - (74, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (70, 9) - (70, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Zeros(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestOnes1dAllElementsOne()
            {
#line (76, 5) - (76, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Ones(4);
#line (77, 5) - (77, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (78, 5) - (78, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d, 1.0d })));
            }

            [Xunit.FactAttribute]
            public void TestOnes2dAllElementsOne()
            {
#line (82, 5) - (82, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Ones(2, 2);
#line (83, 5) - (83, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (84, 5) - (84, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (85, 5) - (85, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (87, 5) - (87, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(4.0d, np.Sum(arr));
            }

            [Xunit.FactAttribute]
            public void TestEye2x2IsIdentity()
            {
#line (93, 5) - (93, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(2);
#line (94, 5) - (94, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (95, 5) - (95, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (96, 5) - (96, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (98, 5) - (98, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2.0d, np.Sum(arr));
            }

            [Xunit.FactAttribute]
            public void TestEye3x3IsIdentity()
            {
#line (102, 5) - (102, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(3);
#line (103, 5) - (103, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (104, 5) - (104, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (105, 5) - (105, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3.0d, np.Sum(arr));
            }

            [Xunit.FactAttribute]
            public void TestEyeZeroReturnsEmptyMatrix()
            {
#line (109, 5) - (109, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(0);
#line (110, 5) - (110, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (111, 5) - (111, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (112, 5) - (112, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestEyeNegativeThrows()
            {
#line (116, 5) - (121, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (117, 9) - (117, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Eye(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestArangeDefaultStepGenerates()
            {
#line (123, 5) - (123, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(0.0d, 5.0d);
#line (124, 5) - (124, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (125, 5) - (125, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArangeCustomStepGenerates()
            {
#line (129, 5) - (129, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(0.0d, 10.0d, 2.0d);
#line (130, 5) - (130, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (131, 5) - (131, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 2.0d, 4.0d, 6.0d, 8.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArangeStopEqualsStartReturnsEmpty()
            {
#line (135, 5) - (135, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 5.0d);
#line (136, 5) - (136, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestArangeNegativeStepDecreases()
            {
#line (140, 5) - (140, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 0.0d, -1.0d);
#line (141, 5) - (141, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (142, 5) - (142, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 5.0d, 4.0d, 3.0d, 2.0d, 1.0d })));
            }

            [Xunit.FactAttribute]
            public void TestArangeZeroStepThrows()
            {
#line (146, 5) - (149, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (147, 9) - (147, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Arange(0.0d, 5.0d, 0.0d);
                }));
            }

            [Xunit.FactAttribute]
            public void TestArangeStopLessThanStartWithPositiveStepReturnsEmpty()
            {
#line (151, 5) - (151, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 0.0d, 1.0d);
#line (152, 5) - (152, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestLinspaceEndpointsAreExact()
            {
#line (158, 5) - (158, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 5);
#line (159, 5) - (159, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (160, 5) - (160, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d })));
            }

            [Xunit.FactAttribute]
            public void TestLinspaceEvenlySpaced()
            {
#line (164, 5) - (164, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 5);
#line (165, 5) - (165, 89) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d }), rtol: 0.0d, atol: 1e-9d));
            }

            [Xunit.FactAttribute]
            public void TestLinspaceDefaultNumIs50()
            {
#line (169, 5) - (169, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d);
#line (170, 5) - (170, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(50, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNumOneReturnsStart()
            {
#line (174, 5) - (174, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(2.0d, 5.0d, 1);
#line (175, 5) - (175, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (176, 5) - (176, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 2.0d })));
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNumZeroReturnsEmpty()
            {
#line (180, 5) - (180, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 0);
#line (181, 5) - (181, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line (182, 5) - (182, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNegativeNumThrows()
            {
#line (186, 5) - (191, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Throws<ArgumentException>((global::System.Action)(() =>
                {
#line (187, 9) - (187, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Linspace(0.0d, 1.0d, -1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestEmptyHasCorrectShape()
            {
#line (193, 5) - (193, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Empty(2, 3);
#line (194, 5) - (194, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (195, 5) - (195, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (196, 5) - (196, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(6, arr.Size);
            }

            [Xunit.FactAttribute]
            public void TestEmptyDtypeIsFloat64()
            {
#line (200, 5) - (200, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Empty(3);
#line (201, 5) - (201, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal("float64", arr.Dtype);
            }
        }
    }
}

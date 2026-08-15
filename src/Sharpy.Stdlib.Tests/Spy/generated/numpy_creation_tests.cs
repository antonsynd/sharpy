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
#line (25, 5) - (25, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (26, 5) - (26, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(1, arr.Ndim);
#line (27, 5) - (27, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, arr.Size);
#line (28, 5) - (28, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (29, 5) - (29, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArrayCopiesData()
            {
#line (33, 5) - (33, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var data = new Sharpy.List<double>()
#line hidden
                {
                    1.0d,
                    2.0d,
                    3.0d
                };
#line (34, 5) - (34, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(data);
#line (35, 5) - (35, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                data[0] = 99.0d;
#line (37, 5) - (37, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArrayEmptyCreatesEmpty()
            {
#line (41, 5) - (41, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
#line hidden
                {
                };
#line (42, 5) - (42, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Array(empty);
#line (43, 5) - (43, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line (44, 5) - (44, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZeros1dAllElementsZero()
            {
#line (50, 5) - (50, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(5);
#line (51, 5) - (51, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (52, 5) - (52, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 0.0d, 0.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZeros2dAllElementsZero()
            {
#line (56, 5) - (56, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(2, 3);
#line (57, 5) - (57, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (58, 5) - (58, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (59, 5) - (59, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (60, 5) - (60, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(6, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZerosDtypeIsFloat64()
            {
#line (64, 5) - (64, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Zeros(3);
#line (65, 5) - (65, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal("float64", arr.Dtype);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZerosNegativeDimensionThrows()
            {
#line (69, 5) - (74, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (70, 9) - (70, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Zeros(-1);
#line hidden
                }
                catch (ArgumentException)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected ArgumentException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestOnes1dAllElementsOne()
            {
#line (76, 5) - (76, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Ones(4);
#line (77, 5) - (77, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (78, 5) - (78, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOnes2dAllElementsOne()
            {
#line (82, 5) - (82, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Ones(2, 2);
#line (83, 5) - (83, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (84, 5) - (84, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (85, 5) - (85, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (87, 5) - (87, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(4.0d, np.Sum(arr));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEye2x2IsIdentity()
            {
#line (93, 5) - (93, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(2);
#line (94, 5) - (94, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, arr.Ndim);
#line (95, 5) - (95, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (96, 5) - (96, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (98, 5) - (98, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2.0d, np.Sum(arr));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEye3x3IsIdentity()
            {
#line (102, 5) - (102, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(3);
#line (103, 5) - (103, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (104, 5) - (104, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (105, 5) - (105, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3.0d, np.Sum(arr));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEyeZeroReturnsEmptyMatrix()
            {
#line (109, 5) - (109, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Eye(0);
#line (110, 5) - (110, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (111, 5) - (111, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (112, 5) - (112, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEyeNegativeThrows()
            {
#line (116, 5) - (121, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (117, 9) - (117, 19) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Eye(-1);
#line hidden
                }
                catch (ArgumentException)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected ArgumentException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestArangeSingleArgumentReadsItAsStop()
            {
#line (126, 5) - (126, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                global::Sharpy.NdArray<double> arr = np.Arange(6.0d);
#line (127, 5) - (127, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(6, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (128, 5) - (128, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeSingleArgumentTruncatesAFractionalStop()
            {
#line (134, 5) - (134, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                global::Sharpy.NdArray<double> arr = np.Arange(2.5d);
#line (135, 5) - (135, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (136, 5) - (136, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeSingleArgumentWithNonPositiveStopIsEmpty()
            {
#line (141, 5) - (141, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                global::Sharpy.NdArray<double> zero = np.Arange(0.0d);
#line (142, 5) - (142, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, zero.Size);
#line (144, 5) - (144, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                global::Sharpy.NdArray<double> negative = np.Arange(-3.0d);
#line (145, 5) - (145, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, negative.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeSingleArgumentAgreesWithTheExplicitTwoArgumentForm()
            {
#line (150, 5) - (150, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(np.Arange(6.0d), np.Arange(0.0d, 6.0d)));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeDefaultStepGenerates()
            {
#line (154, 5) - (154, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(0.0d, 5.0d);
#line (155, 5) - (155, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (156, 5) - (156, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeCustomStepGenerates()
            {
#line (160, 5) - (160, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(0.0d, 10.0d, 2.0d);
#line (161, 5) - (161, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (162, 5) - (162, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 2.0d, 4.0d, 6.0d, 8.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeStopEqualsStartReturnsEmpty()
            {
#line (166, 5) - (166, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 5.0d);
#line (167, 5) - (167, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeNegativeStepDecreases()
            {
#line (171, 5) - (171, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 0.0d, -1.0d);
#line (172, 5) - (172, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (173, 5) - (173, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 5.0d, 4.0d, 3.0d, 2.0d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArangeZeroStepThrows()
            {
#line (177, 5) - (180, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (178, 9) - (178, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Arange(0.0d, 5.0d, 0.0d);
#line hidden
                }
                catch (ArgumentException)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected ArgumentException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestArangeStopLessThanStartWithPositiveStepReturnsEmpty()
            {
#line (182, 5) - (182, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Arange(5.0d, 0.0d, 1.0d);
#line (183, 5) - (183, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceEndpointsAreExact()
            {
#line (189, 5) - (189, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 5);
#line (190, 5) - (190, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (191, 5) - (191, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceEvenlySpaced()
            {
#line (195, 5) - (195, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 5);
#line (196, 5) - (196, 89) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d }), rtol: 0.0d, atol: 1e-9d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceDefaultNumIs50()
            {
#line (200, 5) - (200, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d);
#line (201, 5) - (201, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(50, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNumOneReturnsStart()
            {
#line (205, 5) - (205, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(2.0d, 5.0d, 1);
#line (206, 5) - (206, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (207, 5) - (207, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 2.0d })));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNumZeroReturnsEmpty()
            {
#line (211, 5) - (211, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Linspace(0.0d, 1.0d, 0);
#line (212, 5) - (212, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, arr.Size);
#line (213, 5) - (213, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLinspaceNegativeNumThrows()
            {
#line (217, 5) - (222, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (218, 9) - (218, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                    np.Linspace(0.0d, 1.0d, -1);
#line hidden
                }
                catch (ArgumentException)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected ArgumentException to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestEmptyHasCorrectShape()
            {
#line (224, 5) - (224, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Empty(2, 3);
#line (225, 5) - (225, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (226, 5) - (226, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (227, 5) - (227, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal(6, arr.Size);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEmptyDtypeIsFloat64()
            {
#line (231, 5) - (231, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                var arr = np.Empty(3);
#line (232, 5) - (232, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                Xunit.Assert.Equal("float64", arr.Dtype);
#line hidden
            }
        }
    }
}
#line default

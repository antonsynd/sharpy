// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using np = global::Sharpy.Numpy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Numpy.NumpyCreationTests
{
    [global::Sharpy.SharpyModule("numpy.numpy_creation_tests")]
    public static partial class NumpyCreationTestsModule
    {
    }

    public partial class NumpyCreationTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestArrayFrom1dDataCreatesNdarray()
        {
#line (24, 5) - (24, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (25, 5) - (25, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(1, arr.Ndim);
#line (26, 5) - (26, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, arr.Size);
#line (27, 5) - (27, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (28, 5) - (28, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArrayCopiesData()
        {
#line (32, 5) - (32, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var data = new Sharpy.List<double>()
#line hidden
            {
                1.0d,
                2.0d,
                3.0d
            };
#line (33, 5) - (33, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Array(data);
#line (34, 5) - (34, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            data[0] = 99.0d;
#line (36, 5) - (36, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArrayEmptyCreatesEmpty()
        {
#line (40, 5) - (40, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Sharpy.List<double> empty = new Sharpy.List<double>()
#line hidden
            {
            };
#line (41, 5) - (41, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Array(empty);
#line (42, 5) - (42, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, arr.Size);
#line (43, 5) - (43, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestZeros1dAllElementsZero()
        {
#line (49, 5) - (49, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Zeros(5);
#line (50, 5) - (50, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (51, 5) - (51, 66) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d, 0.0d, 0.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestZeros2dAllElementsZero()
        {
#line (55, 5) - (55, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Zeros(2, 3);
#line (56, 5) - (56, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, arr.Ndim);
#line (57, 5) - (57, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (58, 5) - (58, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (59, 5) - (59, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(6, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestZerosDtypeIsFloat64()
        {
#line (63, 5) - (63, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Zeros(3);
#line (64, 5) - (64, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal("float64", arr.Dtype);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestZerosNegativeDimensionThrows()
        {
#line (68, 5) - (69, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (69, 9) - (69, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                np.Zeros(-1);
#line hidden
            }
            catch (ValueError)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestOnes1dAllElementsOne()
        {
#line (75, 5) - (75, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Ones(4);
#line (76, 5) - (76, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (77, 5) - (77, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 1.0d, 1.0d, 1.0d, 1.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestOnes2dAllElementsOne()
        {
#line (81, 5) - (81, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Ones(2, 2);
#line (82, 5) - (82, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, arr.Ndim);
#line (83, 5) - (83, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (84, 5) - (84, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (86, 5) - (86, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(4.0d, np.Sum(arr));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEye2x2IsIdentity()
        {
#line (92, 5) - (92, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Eye(2);
#line (93, 5) - (93, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, arr.Ndim);
#line (94, 5) - (94, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (95, 5) - (95, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (97, 5) - (97, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2.0d, np.Sum(arr));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEye3x3IsIdentity()
        {
#line (101, 5) - (101, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Eye(3);
#line (102, 5) - (102, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (103, 5) - (103, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (104, 5) - (104, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3.0d, np.Sum(arr));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEyeZeroReturnsEmptyMatrix()
        {
#line (108, 5) - (108, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Eye(0);
#line (109, 5) - (109, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (110, 5) - (110, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (111, 5) - (111, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEyeNegativeThrows()
        {
#line (115, 5) - (116, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (116, 9) - (116, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                np.Eye(-1);
#line hidden
            }
            catch (ValueError)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestArangeSingleArgumentReadsItAsStop()
        {
#line (125, 5) - (125, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            global::Sharpy.NdArray<double> arr = np.Arange(6.0d);
#line (126, 5) - (126, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (127, 5) - (127, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d, 5.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeSingleArgumentTruncatesAFractionalStop()
        {
#line (133, 5) - (133, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            global::Sharpy.NdArray<double> arr = np.Arange(2.5d);
#line (134, 5) - (134, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (135, 5) - (135, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeSingleArgumentWithNonPositiveStopIsEmpty()
        {
#line (140, 5) - (140, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            global::Sharpy.NdArray<double> zero = np.Arange(0.0d);
#line (141, 5) - (141, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, zero.Size);
#line (143, 5) - (143, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            global::Sharpy.NdArray<double> negative = np.Arange(-3.0d);
#line (144, 5) - (144, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, negative.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeSingleArgumentAgreesWithTheExplicitTwoArgumentForm()
        {
#line (149, 5) - (149, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(np.Arange(6.0d), np.Arange(0.0d, 6.0d)));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeDefaultStepGenerates()
        {
#line (153, 5) - (153, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Arange(0.0d, 5.0d);
#line (154, 5) - (154, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (155, 5) - (155, 66) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 1.0d, 2.0d, 3.0d, 4.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeCustomStepGenerates()
        {
#line (159, 5) - (159, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Arange(0.0d, 10.0d, 2.0d);
#line (160, 5) - (160, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (161, 5) - (161, 66) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 2.0d, 4.0d, 6.0d, 8.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeStopEqualsStartReturnsEmpty()
        {
#line (165, 5) - (165, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Arange(5.0d, 5.0d);
#line (166, 5) - (166, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeNegativeStepDecreases()
        {
#line (170, 5) - (170, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Arange(5.0d, 0.0d, -1.0d);
#line (171, 5) - (171, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (172, 5) - (172, 66) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 5.0d, 4.0d, 3.0d, 2.0d, 1.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestArangeZeroStepThrows()
        {
#line (177, 5) - (178, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (178, 9) - (178, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                np.Arange(0.0d, 5.0d, 0.0d);
#line hidden
            }
            catch (ZeroDivisionError)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected ZeroDivisionError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestArangeStopLessThanStartWithPositiveStepReturnsEmpty()
        {
#line (182, 5) - (182, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Arange(5.0d, 0.0d, 1.0d);
#line (183, 5) - (183, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceEndpointsAreExact()
        {
#line (189, 5) - (189, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Linspace(0.0d, 1.0d, 5);
#line (190, 5) - (190, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(5, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (191, 5) - (191, 68) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceEvenlySpaced()
        {
#line (195, 5) - (195, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Linspace(0.0d, 1.0d, 5);
#line (196, 5) - (196, 89) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 0.0d, 0.25d, 0.5d, 0.75d, 1.0d }), rtol: 0.0d, atol: 1e-9d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceDefaultNumIs50()
        {
#line (200, 5) - (200, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Linspace(0.0d, 1.0d);
#line (201, 5) - (201, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(50, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceNumOneReturnsStart()
        {
#line (205, 5) - (205, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Linspace(2.0d, 5.0d, 1);
#line (206, 5) - (206, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (207, 5) - (207, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.True(np.Allclose(arr, np.Array(new Sharpy.List<double>() { 2.0d })));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceNumZeroReturnsEmpty()
        {
#line (211, 5) - (211, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Linspace(0.0d, 1.0d, 0);
#line (212, 5) - (212, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, arr.Size);
#line (213, 5) - (213, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLinspaceNegativeNumThrows()
        {
#line (217, 5) - (218, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (218, 9) - (218, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
                np.Linspace(0.0d, 1.0d, -1);
#line hidden
            }
            catch (ValueError)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestEmptyHasCorrectShape()
        {
#line (224, 5) - (224, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Empty(2, 3);
#line (225, 5) - (225, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 0));
#line (226, 5) - (226, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.ArrayHelpers.GetItem(arr.Shape, 1));
#line (227, 5) - (227, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal(6, arr.Size);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEmptyDtypeIsFloat64()
        {
#line (231, 5) - (231, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            var arr = np.Empty(3);
#line (232, 5) - (232, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_creation_tests.spy"
            Xunit.Assert.Equal("float64", arr.Dtype);
#line hidden
        }
    }
}
#line default

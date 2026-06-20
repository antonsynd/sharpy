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
using math = global::Sharpy.MathModule;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Numpy.NumpyLinalgTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Numpy
    {
        [global::Sharpy.SharpyModule("numpy.numpy_linalg_tests")]
        public static partial class NumpyLinalgTests
        {
            public static void AssertAlmostEqual(double actual, double expected, double tol = 1e-9d)
            {
#line (18, 5) - (18, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                System.Diagnostics.Debug.Assert(global::Sharpy.Builtins.Abs(actual - expected) < tol);
            }
        }
    }

    public static partial class Numpy
    {
        public partial class NumpyLinalgTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDotVectorVector()
            {
#line (24, 5) - (24, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (25, 5) - (25, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (26, 5) - (26, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = global::Sharpy.NumpyLinalg.Dot(a, b);
#line (27, 5) - (27, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(32.0d, np.Sum(result), 7);
            }

            [Xunit.FactAttribute]
            public void TestDotMatrixMatrix()
            {
#line (31, 5) - (31, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (32, 5) - (32, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 2);
#line (33, 5) - (33, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = global::Sharpy.NumpyLinalg.Dot(a, b);
#line (34, 5) - (34, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, result.Shape[0]);
#line (35, 5) - (35, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, result.Shape[1]);
#line (36, 5) - (36, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(19.0d, result[0, 0], 7);
#line (37, 5) - (37, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(22.0d, result[0, 1], 7);
#line (38, 5) - (38, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(43.0d, result[1, 0], 7);
#line (39, 5) - (39, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(50.0d, result[1, 1], 7);
            }

            [Xunit.FactAttribute]
            public void TestDotMatrixVector()
            {
#line (43, 5) - (43, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var m = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (44, 5) - (44, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var v = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d });
#line (45, 5) - (45, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = global::Sharpy.NumpyLinalg.Dot(m, v);
#line (46, 5) - (46, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, result.Shape[0]);
#line (47, 5) - (47, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(17.0d, result[0], 7);
#line (48, 5) - (48, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(39.0d, result[1], 7);
            }

            [Xunit.FactAttribute]
            public void TestDotVectorMatrix()
            {
#line (52, 5) - (52, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var v = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (53, 5) - (53, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var m = np.Array(new Sharpy.List<double>() { 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 2);
#line (54, 5) - (54, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = global::Sharpy.NumpyLinalg.Dot(v, m);
#line (55, 5) - (55, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, result.Shape[0]);
#line (56, 5) - (56, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(13.0d, result[0], 7);
#line (57, 5) - (57, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(16.0d, result[1], 7);
            }

            [Xunit.FactAttribute]
            public void TestDotShapeMismatchThrows()
            {
#line (61, 5) - (61, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (62, 5) - (62, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (63, 5) - (66, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (64, 9) - (64, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Dot(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDotMatrixDimMismatchThrows()
            {
#line (68, 5) - (68, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (69, 5) - (69, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (70, 5) - (73, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (71, 9) - (71, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Dot(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDotHigherRankThrows()
            {
#line (75, 5) - (75, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Zeros(8).Reshape(2, 2, 2);
#line (76, 5) - (76, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Zeros(8).Reshape(2, 2, 2);
#line (77, 5) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (78, 9) - (78, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Dot(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMatmulDelegatesToDot()
            {
#line (82, 5) - (82, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (83, 5) - (83, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 2);
#line (84, 5) - (84, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var dotResult = global::Sharpy.NumpyLinalg.Dot(a, b);
#line (85, 5) - (85, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var matmulResult = global::Sharpy.NumpyLinalg.Matmul(a, b);
#line (86, 5) - (86, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(np.Allclose(dotResult.Flatten(), matmulResult.Flatten()));
            }

            [Xunit.FactAttribute]
            public void TestDotTransposedView()
            {
#line (90, 5) - (90, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (91, 5) - (91, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var at = a.Transpose();
#line (92, 5) - (92, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = global::Sharpy.NumpyLinalg.Dot(at, a);
#line (93, 5) - (93, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(10.0d, result[0, 0], 7);
#line (94, 5) - (94, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(14.0d, result[0, 1], 7);
#line (95, 5) - (95, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(14.0d, result[1, 0], 7);
#line (96, 5) - (96, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(20.0d, result[1, 1], 7);
            }

            [Xunit.FactAttribute]
            public void TestDotTopLevelAlias()
            {
#line (100, 5) - (100, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (101, 5) - (101, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 4.0d, 5.0d, 6.0d });
#line (102, 5) - (102, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = np.Dot(a, b);
#line (103, 5) - (103, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(32.0d, np.Sum(result), 7);
            }

            [Xunit.FactAttribute]
            public void TestMatmulTopLevelAlias()
            {
#line (107, 5) - (107, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (108, 5) - (108, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 6.0d, 7.0d, 8.0d }).Reshape(2, 2);
#line (109, 5) - (109, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var result = np.Matmul(a, b);
#line (110, 5) - (110, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(19.0d, result[0, 0], 7);
            }

            [Xunit.FactAttribute]
            public void TestInvTwoByTwo()
            {
#line (116, 5) - (116, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (117, 5) - (117, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var inv = global::Sharpy.NumpyLinalg.Inv(a);
#line (118, 5) - (118, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, inv.Shape[0]);
#line (119, 5) - (119, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, inv.Shape[1]);
#line (120, 5) - (120, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(-2.0d, inv[0, 0], 7);
#line (121, 5) - (121, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.0d, inv[0, 1], 7);
#line (122, 5) - (122, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.5d, inv[1, 0], 7);
#line (123, 5) - (123, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(-0.5d, inv[1, 1], 7);
            }

            [Xunit.FactAttribute]
            public void TestInvTimesOriginalGivesIdentity()
            {
#line (127, 5) - (127, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 4.0d, 7.0d, 2.0d, 6.0d }).Reshape(2, 2);
#line (128, 5) - (128, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var inv = global::Sharpy.NumpyLinalg.Inv(a);
#line (129, 5) - (129, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var product = global::Sharpy.NumpyLinalg.Dot(a, inv);
#line (130, 5) - (130, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.0d, product[0, 0], 7);
#line (131, 5) - (131, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(0.0d, product[0, 1], 7);
#line (132, 5) - (132, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(0.0d, product[1, 0], 7);
#line (133, 5) - (133, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.0d, product[1, 1], 7);
            }

            [Xunit.FactAttribute]
            public void TestInvSingularThrows()
            {
#line (137, 5) - (137, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 2.0d, 4.0d }).Reshape(2, 2);
#line (138, 5) - (141, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (139, 9) - (139, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Inv(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestInvNonSquareThrows()
            {
#line (143, 5) - (143, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (144, 5) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (145, 9) - (145, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Inv(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestInvNot2dThrows()
            {
#line (149, 5) - (149, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (150, 5) - (155, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (151, 9) - (151, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Inv(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestDetTwoByTwo()
            {
#line (157, 5) - (157, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (158, 5) - (158, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(-2.0d, global::Sharpy.NumpyLinalg.Det(a), 7);
            }

            [Xunit.FactAttribute]
            public void TestDetIdentityIsOne()
            {
#line (162, 5) - (162, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var ident = np.Eye(4);
#line (163, 5) - (163, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.0d, global::Sharpy.NumpyLinalg.Det(ident), 7);
            }

            [Xunit.FactAttribute]
            public void TestDetSingularIsZero()
            {
#line (167, 5) - (167, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 2.0d, 4.0d }).Reshape(2, 2);
#line (168, 5) - (168, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(0.0d, global::Sharpy.NumpyLinalg.Det(a), 7);
            }

            [Xunit.FactAttribute]
            public void TestDetNonSquareThrows()
            {
#line (172, 5) - (172, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (173, 5) - (178, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (174, 9) - (174, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Det(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestEigDiagonalMatrix()
            {
#line (180, 5) - (180, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 3.0d, 0.0d, 0.0d, 5.0d }).Reshape(2, 2);
#line (181, 5) - (181, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var (values, vectors) = global::Sharpy.NumpyLinalg.Eig(a);
#line (182, 5) - (182, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, values.Shape[0]);
#line (183, 5) - (183, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, vectors.Shape[0]);
#line (184, 5) - (184, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, vectors.Shape[1]);
#line (185, 5) - (185, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var sortedVals = np.Sort(values);
#line (186, 5) - (186, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(sortedVals[0] - 3.0d) < 1e-6d);
#line (187, 5) - (187, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(sortedVals[1] - 5.0d) < 1e-6d);
            }

            [Xunit.FactAttribute]
            public void TestEigSymmetricMatrix()
            {
#line (191, 5) - (191, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 2.0d, 1.0d, 1.0d, 2.0d }).Reshape(2, 2);
#line (192, 5) - (192, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var (values, _) = global::Sharpy.NumpyLinalg.Eig(a);
#line (193, 5) - (193, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var sortedVals = np.Sort(values);
#line (194, 5) - (194, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(sortedVals[0] - 1.0d) < 1e-6d);
#line (195, 5) - (195, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Abs(sortedVals[1] - 3.0d) < 1e-6d);
            }

            [Xunit.FactAttribute]
            public void TestEigNonSquareThrows()
            {
#line (199, 5) - (199, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (200, 5) - (205, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (201, 9) - (201, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Eig(a);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSvdSingularValuesNonNegative()
            {
#line (207, 5) - (207, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(3, 2);
#line (208, 5) - (208, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var (u, s, vh) = global::Sharpy.NumpyLinalg.Svd(a);
#line (209, 5) - (209, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(3, u.Shape[0]);
#line (210, 5) - (210, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, vh.Shape[0]);
#line (211, 5) - (211, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(s[0] >= 0.0d);
#line (212, 5) - (212, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(s[1] >= 0.0d);
#line (213, 5) - (213, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.True(s[0] >= s[1]);
            }

            [Xunit.FactAttribute]
            public void TestSolveTwoByTwo()
            {
#line (219, 5) - (219, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (220, 5) - (220, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 5.0d, 11.0d });
#line (221, 5) - (221, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var x = global::Sharpy.NumpyLinalg.Solve(a, b);
#line (222, 5) - (222, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2, x.Shape[0]);
#line (223, 5) - (223, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(1.0d, x[0], 7);
#line (224, 5) - (224, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(2.0d, x[1], 7);
            }

            [Xunit.FactAttribute]
            public void TestSolveSingularThrows()
            {
#line (228, 5) - (228, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 2.0d, 4.0d }).Reshape(2, 2);
#line (229, 5) - (229, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (230, 5) - (233, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (231, 9) - (231, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Solve(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSolveNonSquareThrows()
            {
#line (235, 5) - (235, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }).Reshape(2, 3);
#line (236, 5) - (236, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d });
#line (237, 5) - (240, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (238, 9) - (238, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Solve(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSolveDimensionMismatchThrows()
            {
#line (242, 5) - (242, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (243, 5) - (243, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var b = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d });
#line (244, 5) - (249, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (245, 9) - (245, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Solve(a, b);
                }));
            }

            [Xunit.FactAttribute]
            public void TestNormVectorL2()
            {
#line (251, 5) - (251, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var v = np.Array(new Sharpy.List<double>() { 3.0d, 4.0d });
#line (252, 5) - (252, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(5.0d, global::Sharpy.NumpyLinalg.Norm(v), 7);
            }

            [Xunit.FactAttribute]
            public void TestNormVectorHigherDim()
            {
#line (256, 5) - (256, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var v = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d });
#line (257, 5) - (257, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(math.Sqrt(55.0d), global::Sharpy.NumpyLinalg.Norm(v), 7);
            }

            [Xunit.FactAttribute]
            public void TestNormMatrixFrobenius()
            {
#line (261, 5) - (261, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Array(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }).Reshape(2, 2);
#line (262, 5) - (262, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(math.Sqrt(30.0d), global::Sharpy.NumpyLinalg.Norm(a), 7);
            }

            [Xunit.FactAttribute]
            public void TestNormZeroVector()
            {
#line (266, 5) - (266, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var v = np.Array(new Sharpy.List<double>() { 0.0d, 0.0d, 0.0d });
#line (267, 5) - (267, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Equal(0.0d, global::Sharpy.NumpyLinalg.Norm(v), 7);
            }

            [Xunit.FactAttribute]
            public void TestNormHigherRankThrows()
            {
#line (271, 5) - (271, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                var a = np.Zeros(8).Reshape(2, 2, 2);
#line (272, 5) - (274, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (273, 9) - (273, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/numpy/numpy_linalg_tests.spy"
                    global::Sharpy.NumpyLinalg.Norm(a);
                }));
            }
        }
    }
}

// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using colorsys = global::Sharpy.Colorsys;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonColorsysTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_colorsys_tests")]
        public static partial class CpythonColorsysTests
        {
            internal static void _AssertTriple(double a0, double a1, double a2, double b0, double b1, double b2)
            {
#line (61, 5) - (63, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a0 - b0) >= 1e-7d)
                {
#line (62, 9) - (62, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    throw new global::Sharpy.ValueError("colorsys triple mismatch at position 0");
                }

#line (63, 5) - (65, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a1 - b1) >= 1e-7d)
                {
#line (64, 9) - (64, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    throw new global::Sharpy.ValueError("colorsys triple mismatch at position 1");
                }

#line (65, 5) - (68, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a2 - b2) >= 1e-7d)
                {
#line (66, 9) - (66, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    throw new global::Sharpy.ValueError("colorsys triple mismatch at position 2");
                }
            }
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonColorsysTestsTests
        {
            [Xunit.FactAttribute]
            public void TestHsvRoundtrip()
            {
#line (20, 5) - (20, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (21, 5) - (30, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_0 in vals)
                {
                    var r = __loopVar_0;
#line (22, 9) - (30, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_1 in vals)
                    {
                        var g = __loopVar_1;
#line (23, 13) - (30, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_2 in vals)
                        {
                            var b = __loopVar_2;
#line (24, 17) - (24, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (h, s, v) = colorsys.RgbToHsv(r, g, b);
#line (25, 17) - (25, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.HsvToRgb(h, s, v);
#line (26, 17) - (26, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (27, 17) - (27, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (28, 17) - (28, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestHlsRoundtrip()
            {
#line (32, 5) - (32, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (33, 5) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_3 in vals)
                {
                    var r = __loopVar_3;
#line (34, 9) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_4 in vals)
                    {
                        var g = __loopVar_4;
#line (35, 13) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_5 in vals)
                        {
                            var b = __loopVar_5;
#line (36, 17) - (36, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (h, l, s) = colorsys.RgbToHls(r, g, b);
#line (37, 17) - (37, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.HlsToRgb(h, l, s);
#line (38, 17) - (38, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (39, 17) - (39, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (40, 17) - (40, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestYiqRoundtrip()
            {
#line (44, 5) - (44, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (45, 5) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_6 in vals)
                {
                    var r = __loopVar_6;
#line (46, 9) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_7 in vals)
                    {
                        var g = __loopVar_7;
#line (47, 13) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_8 in vals)
                        {
                            var b = __loopVar_8;
#line (48, 17) - (48, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (y, i, q) = colorsys.RgbToYiq(r, g, b);
#line (49, 17) - (49, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.YiqToRgb(y, i, q);
#line (50, 17) - (50, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (51, 17) - (51, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (52, 17) - (52, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestHsvValues()
            {
#line (71, 5) - (71, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 0.0d);
#line (72, 5) - (72, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 0.0d);
#line (73, 5) - (73, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 1.0d);
#line (74, 5) - (74, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 4.0d / 6.0d, 1.0d, 1.0d);
#line (75, 5) - (75, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 1.0d, 0.0d);
#line (76, 5) - (76, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 2.0d / 6.0d, 1.0d, 1.0d);
#line (77, 5) - (77, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 1.0d, 1.0d);
#line (78, 5) - (78, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 3.0d / 6.0d, 1.0d, 1.0d);
#line (79, 5) - (79, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 0.0d, 0.0d);
#line (80, 5) - (80, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 1.0d, 1.0d);
#line (81, 5) - (81, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 0.0d, 1.0d);
#line (82, 5) - (82, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 5.0d / 6.0d, 1.0d, 1.0d);
#line (83, 5) - (83, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 1.0d, 0.0d);
#line (84, 5) - (84, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 1.0d / 6.0d, 1.0d, 1.0d);
#line (85, 5) - (85, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 1.0d, 1.0d);
#line (86, 5) - (86, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 1.0d);
#line (87, 5) - (87, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.5d, 0.5d, 0.5d);
#line (88, 5) - (88, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 0.5d);
#line (90, 5) - (90, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.HsvToRgb(4.0d / 6.0d, 1.0d, 1.0d);
#line (91, 5) - (91, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 1.0d);
#line (92, 5) - (92, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HsvToRgb(2.0d / 6.0d, 1.0d, 1.0d);
#line (93, 5) - (93, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 1.0d, 0.0d);
#line (94, 5) - (94, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HsvToRgb(0.0d, 0.0d, 0.5d);
#line (95, 5) - (95, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.5d, 0.5d, 0.5d);
            }

            [Xunit.FactAttribute]
            public void TestHlsValues()
            {
#line (99, 5) - (99, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(0.0d, 0.0d, 0.0d);
#line (100, 5) - (100, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.0d, 0.0d);
#line (101, 5) - (101, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.0d, 0.0d, 1.0d);
#line (102, 5) - (102, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 4.0d / 6.0d, 0.5d, 1.0d);
#line (103, 5) - (103, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.0d, 1.0d, 0.0d);
#line (104, 5) - (104, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 2.0d / 6.0d, 0.5d, 1.0d);
#line (105, 5) - (105, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 0.0d, 0.0d);
#line (106, 5) - (106, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.5d, 1.0d);
#line (107, 5) - (107, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 1.0d, 1.0d);
#line (108, 5) - (108, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 1.0d, 0.0d);
#line (109, 5) - (109, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.5d, 0.5d, 0.5d);
#line (110, 5) - (110, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.5d, 0.0d);
#line (112, 5) - (112, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.HlsToRgb(4.0d / 6.0d, 0.5d, 1.0d);
#line (113, 5) - (113, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 1.0d);
#line (114, 5) - (114, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HlsToRgb(2.0d / 6.0d, 0.5d, 1.0d);
#line (115, 5) - (115, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 1.0d, 0.0d);
            }

            [Xunit.FactAttribute]
            public void TestYiqValues()
            {
#line (119, 5) - (119, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (y, i, q) = colorsys.RgbToYiq(0.0d, 0.0d, 0.0d);
#line (120, 5) - (120, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.0d, 0.0d, 0.0d);
#line (121, 5) - (121, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.0d, 0.0d, 1.0d);
#line (122, 5) - (122, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.11d, -0.3217d, 0.3121d);
#line (123, 5) - (123, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.0d, 1.0d, 0.0d);
#line (124, 5) - (124, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.59d, -0.2773d, -0.5251d);
#line (125, 5) - (125, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(1.0d, 0.0d, 0.0d);
#line (126, 5) - (126, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.3d, 0.599d, 0.213d);
#line (127, 5) - (127, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(1.0d, 1.0d, 1.0d);
#line (128, 5) - (128, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 1.0d, 0.0d, 0.0d);
#line (129, 5) - (129, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.5d, 0.5d, 0.5d);
#line (130, 5) - (130, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.5d, 0.0d, 0.0d);
#line (132, 5) - (132, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.YiqToRgb(1.0d, 0.0d, 0.0d);
#line (133, 5) - (133, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 1.0d, 1.0d, 1.0d);
#line (134, 5) - (134, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.YiqToRgb(0.0d, 0.0d, 0.0d);
#line (135, 5) - (135, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 0.0d);
            }
        }
    }
}

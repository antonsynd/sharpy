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
#line (57, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a0 - b0) >= 1e-7d)
                {
#line (58, 9) - (58, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    throw new global::Sharpy.ValueError("colorsys triple mismatch at position 0");
                }

#line (59, 5) - (61, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a1 - b1) >= 1e-7d)
                {
#line (60, 9) - (60, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    throw new global::Sharpy.ValueError("colorsys triple mismatch at position 1");
                }

#line (61, 5) - (64, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                if (global::Sharpy.Builtins.Abs(a2 - b2) >= 1e-7d)
                {
#line (62, 9) - (62, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
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
#line (16, 5) - (16, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (17, 5) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_0 in vals)
                {
                    var r = __loopVar_0;
#line (18, 9) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_1 in vals)
                    {
                        var g = __loopVar_1;
#line (19, 13) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_2 in vals)
                        {
                            var b = __loopVar_2;
#line (20, 17) - (20, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (h, s, v) = colorsys.RgbToHsv(r, g, b);
#line (21, 17) - (21, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.HsvToRgb(h, s, v);
#line (22, 17) - (22, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (23, 17) - (23, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (24, 17) - (24, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestHlsRoundtrip()
            {
#line (28, 5) - (28, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (29, 5) - (38, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_3 in vals)
                {
                    var r = __loopVar_3;
#line (30, 9) - (38, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_4 in vals)
                    {
                        var g = __loopVar_4;
#line (31, 13) - (38, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_5 in vals)
                        {
                            var b = __loopVar_5;
#line (32, 17) - (32, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (h, l, s) = colorsys.RgbToHls(r, g, b);
#line (33, 17) - (33, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.HlsToRgb(h, l, s);
#line (34, 17) - (34, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (35, 17) - (35, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (36, 17) - (36, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestYiqRoundtrip()
            {
#line (40, 5) - (40, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                Sharpy.List<double> vals = new Sharpy.List<double>()
                {
                    0.0d,
                    0.2d,
                    0.4d,
                    0.6d,
                    0.8d,
                    1.0d
                };
#line (41, 5) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                foreach (var __loopVar_6 in vals)
                {
                    var r = __loopVar_6;
#line (42, 9) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                    foreach (var __loopVar_7 in vals)
                    {
                        var g = __loopVar_7;
#line (43, 13) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                        foreach (var __loopVar_8 in vals)
                        {
                            var b = __loopVar_8;
#line (44, 17) - (44, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (y, i, q) = colorsys.RgbToYiq(r, g, b);
#line (45, 17) - (45, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            var (r2, g2, b2) = colorsys.YiqToRgb(y, i, q);
#line (46, 17) - (46, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(r, r2, 1e-7d);
#line (47, 17) - (47, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(g, g2, 1e-7d);
#line (48, 17) - (48, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                            Xunit.Assert.Equal(b, b2, 1e-7d);
                        }
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestHsvValues()
            {
#line (67, 5) - (67, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 0.0d);
#line (68, 5) - (68, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 0.0d);
#line (69, 5) - (69, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 1.0d);
#line (70, 5) - (70, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 4.0d / 6.0d, 1.0d, 1.0d);
#line (71, 5) - (71, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 1.0d, 0.0d);
#line (72, 5) - (72, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 2.0d / 6.0d, 1.0d, 1.0d);
#line (73, 5) - (73, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.0d, 1.0d, 1.0d);
#line (74, 5) - (74, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 3.0d / 6.0d, 1.0d, 1.0d);
#line (75, 5) - (75, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 0.0d, 0.0d);
#line (76, 5) - (76, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 1.0d, 1.0d);
#line (77, 5) - (77, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 0.0d, 1.0d);
#line (78, 5) - (78, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 5.0d / 6.0d, 1.0d, 1.0d);
#line (79, 5) - (79, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 1.0d, 0.0d);
#line (80, 5) - (80, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 1.0d / 6.0d, 1.0d, 1.0d);
#line (81, 5) - (81, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(1.0d, 1.0d, 1.0d);
#line (82, 5) - (82, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 1.0d);
#line (83, 5) - (83, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, s, v) = colorsys.RgbToHsv(0.5d, 0.5d, 0.5d);
#line (84, 5) - (84, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, s, v, 0.0d, 0.0d, 0.5d);
#line (86, 5) - (86, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.HsvToRgb(4.0d / 6.0d, 1.0d, 1.0d);
#line (87, 5) - (87, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 1.0d);
#line (88, 5) - (88, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HsvToRgb(2.0d / 6.0d, 1.0d, 1.0d);
#line (89, 5) - (89, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 1.0d, 0.0d);
#line (90, 5) - (90, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HsvToRgb(0.0d, 0.0d, 0.5d);
#line (91, 5) - (91, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.5d, 0.5d, 0.5d);
            }

            [Xunit.FactAttribute]
            public void TestHlsValues()
            {
#line (95, 5) - (95, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(0.0d, 0.0d, 0.0d);
#line (96, 5) - (96, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.0d, 0.0d);
#line (97, 5) - (97, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.0d, 0.0d, 1.0d);
#line (98, 5) - (98, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 4.0d / 6.0d, 0.5d, 1.0d);
#line (99, 5) - (99, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.0d, 1.0d, 0.0d);
#line (100, 5) - (100, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 2.0d / 6.0d, 0.5d, 1.0d);
#line (101, 5) - (101, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 0.0d, 0.0d);
#line (102, 5) - (102, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.5d, 1.0d);
#line (103, 5) - (103, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 1.0d, 1.0d);
#line (104, 5) - (104, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 1.0d, 0.0d);
#line (105, 5) - (105, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(0.5d, 0.5d, 0.5d);
#line (106, 5) - (106, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.0d, 0.5d, 0.0d);
#line (108, 5) - (108, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.HlsToRgb(4.0d / 6.0d, 0.5d, 1.0d);
#line (109, 5) - (109, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 1.0d);
#line (110, 5) - (110, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.HlsToRgb(2.0d / 6.0d, 0.5d, 1.0d);
#line (111, 5) - (111, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 1.0d, 0.0d);
            }

            [Xunit.FactAttribute]
            public void TestHlsNearwhite()
            {
#line (118, 5) - (118, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(0.9999999999999999d, 1.0d, 1.0d);
#line (119, 5) - (119, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.5d, 1.0d, 1.0d);
#line (120, 5) - (120, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 0.9999999999999999d, 1.0d);
#line (121, 5) - (121, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.8333333333333334d, 1.0d, 1.0d);
#line (122, 5) - (122, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (h, l, s) = colorsys.RgbToHls(1.0d, 1.0d, 0.9999999999999999d);
#line (123, 5) - (123, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(h, l, s, 0.16666666666666666d, 1.0d, 1.0d);
            }

            [Xunit.FactAttribute]
            public void TestYiqValues()
            {
#line (127, 5) - (127, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (y, i, q) = colorsys.RgbToYiq(0.0d, 0.0d, 0.0d);
#line (128, 5) - (128, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.0d, 0.0d, 0.0d);
#line (129, 5) - (129, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.0d, 0.0d, 1.0d);
#line (130, 5) - (130, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.11d, -0.3217d, 0.3121d);
#line (131, 5) - (131, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.0d, 1.0d, 0.0d);
#line (132, 5) - (132, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.59d, -0.2773d, -0.5251d);
#line (133, 5) - (133, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(1.0d, 0.0d, 0.0d);
#line (134, 5) - (134, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.3d, 0.599d, 0.213d);
#line (135, 5) - (135, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(1.0d, 1.0d, 1.0d);
#line (136, 5) - (136, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 1.0d, 0.0d, 0.0d);
#line (137, 5) - (137, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (y, i, q) = colorsys.RgbToYiq(0.5d, 0.5d, 0.5d);
#line (138, 5) - (138, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(y, i, q, 0.5d, 0.0d, 0.0d);
#line (140, 5) - (140, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                var (r, g, b) = colorsys.YiqToRgb(1.0d, 0.0d, 0.0d);
#line (141, 5) - (141, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 1.0d, 1.0d, 1.0d);
#line (142, 5) - (142, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                (r, g, b) = colorsys.YiqToRgb(0.0d, 0.0d, 0.0d);
#line (143, 5) - (143, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_colorsys_tests.spy"
                _AssertTriple(r, g, b, 0.0d, 0.0d, 0.0d);
            }
        }
    }
}

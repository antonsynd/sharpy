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
using static Sharpy.Stdlib.Tests.Spy.Colorsys.ColorsysTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Colorsys
    {
        [global::Sharpy.SharpyModule("colorsys.colorsys_tests")]
        public static partial class ColorsysTests
        {
        }
    }

    public static partial class Colorsys
    {
        public partial class ColorsysTestsTests
        {
            [Xunit.FactAttribute]
            public void TestRgbToHsvTypicalValue()
            {
#line (9, 5) - (9, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.2d, 0.4d, 0.6d);
#line (10, 5) - (10, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.5833333333333334d, h, 1e-10d);
#line (11, 5) - (11, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.6666666666666666d, s, 1e-10d);
#line (12, 5) - (12, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.6d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHsvBlack()
            {
#line (16, 5) - (16, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 0.0d);
#line (17, 5) - (17, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, h, 1e-10d);
#line (18, 5) - (18, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, s, 1e-10d);
#line (19, 5) - (19, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHsvWhite()
            {
#line (23, 5) - (23, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(1.0d, 1.0d, 1.0d);
#line (24, 5) - (24, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, h, 1e-10d);
#line (25, 5) - (25, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, s, 1e-10d);
#line (26, 5) - (26, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHsvPureRed()
            {
#line (30, 5) - (30, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(1.0d, 0.0d, 0.0d);
#line (31, 5) - (31, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, h, 1e-10d);
#line (32, 5) - (32, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, s, 1e-10d);
#line (33, 5) - (33, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHsvPureGreen()
            {
#line (37, 5) - (37, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.0d, 1.0d, 0.0d);
#line (38, 5) - (38, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.3333333333333333d, h, 1e-10d);
#line (39, 5) - (39, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, s, 1e-10d);
#line (40, 5) - (40, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHsvPureBlue()
            {
#line (44, 5) - (44, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(0.0d, 0.0d, 1.0d);
#line (45, 5) - (45, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.6666666666666666d, h, 1e-10d);
#line (46, 5) - (46, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, s, 1e-10d);
#line (47, 5) - (47, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, v, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHlsTypicalValue()
            {
#line (53, 5) - (53, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(0.2d, 0.4d, 0.6d);
#line (54, 5) - (54, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.5833333333333334d, h, 1e-10d);
#line (55, 5) - (55, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.4d, l, 1e-10d);
#line (56, 5) - (56, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.49999999999999994d, s, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHlsBlack()
            {
#line (60, 5) - (60, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(0.0d, 0.0d, 0.0d);
#line (61, 5) - (61, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, h, 1e-10d);
#line (62, 5) - (62, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, l, 1e-10d);
#line (63, 5) - (63, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, s, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToHlsWhite()
            {
#line (67, 5) - (67, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(1.0d, 1.0d, 1.0d);
#line (68, 5) - (68, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, h, 1e-10d);
#line (69, 5) - (69, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, l, 1e-10d);
#line (70, 5) - (70, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, s, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToYiqTypicalValue()
            {
#line (76, 5) - (76, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (y, i, q) = colorsys.RgbToYiq(0.2d, 0.4d, 0.6d);
#line (77, 5) - (77, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.362d, y, 1e-10d);
#line (78, 5) - (78, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(-0.18413999999999997d, i, 1e-10d);
#line (79, 5) - (79, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.019820000000000004d, q, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestRgbToYiqWhite()
            {
#line (83, 5) - (83, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (y, i, q) = colorsys.RgbToYiq(1.0d, 1.0d, 1.0d);
#line (84, 5) - (84, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(1.0d, y, 1e-10d);
#line (85, 5) - (85, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, i, 1e-10d);
#line (86, 5) - (86, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(0.0d, q, 1e-10d);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute(0.2d, 0.4d, 0.6d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(1.0d, 1.0d, 1.0d)]
            [Xunit.InlineDataAttribute(1.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 1.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 1.0d)]
            [Xunit.InlineDataAttribute(0.75d, 0.25d, 0.5d)]
            public void TestHsvRoundtripMatchesOriginal(double r, double g, double b)
            {
#line (100, 5) - (100, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, s, v) = colorsys.RgbToHsv(r, g, b);
#line (101, 5) - (101, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (r2, g2, b2) = colorsys.HsvToRgb(h, s, v);
#line (102, 5) - (102, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(r, r2, 1e-10d);
#line (103, 5) - (103, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(g, g2, 1e-10d);
#line (104, 5) - (104, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(b, b2, 1e-10d);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute(0.2d, 0.4d, 0.6d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(1.0d, 1.0d, 1.0d)]
            [Xunit.InlineDataAttribute(1.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 1.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 1.0d)]
            [Xunit.InlineDataAttribute(0.75d, 0.25d, 0.5d)]
            public void TestHlsRoundtripMatchesOriginal(double r, double g, double b)
            {
#line (118, 5) - (118, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (h, l, s) = colorsys.RgbToHls(r, g, b);
#line (119, 5) - (119, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (r2, g2, b2) = colorsys.HlsToRgb(h, l, s);
#line (120, 5) - (120, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(r, r2, 1e-10d);
#line (121, 5) - (121, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(g, g2, 1e-10d);
#line (122, 5) - (122, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(b, b2, 1e-10d);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute(0.2d, 0.4d, 0.6d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(1.0d, 1.0d, 1.0d)]
            [Xunit.InlineDataAttribute(1.0d, 0.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 1.0d, 0.0d)]
            [Xunit.InlineDataAttribute(0.0d, 0.0d, 1.0d)]
            [Xunit.InlineDataAttribute(0.75d, 0.25d, 0.5d)]
            public void TestYiqRoundtripMatchesOriginal(double r, double g, double b)
            {
#line (136, 5) - (136, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (y, i, q) = colorsys.RgbToYiq(r, g, b);
#line (137, 5) - (137, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                var (r2, g2, b2) = colorsys.YiqToRgb(y, i, q);
#line (138, 5) - (138, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(r, r2, 1e-10d);
#line (139, 5) - (139, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(g, g2, 1e-10d);
#line (140, 5) - (140, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/colorsys/colorsys_tests.spy"
                Xunit.Assert.Equal(b, b2, 1e-10d);
            }
        }
    }
}

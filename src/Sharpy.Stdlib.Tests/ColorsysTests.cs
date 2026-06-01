using Xunit;

namespace Sharpy.Stdlib.Tests
{
    public class ColorsysTests
    {
        private const int Precision = 10;

        // --- RgbToHsv known values (verified against Python colorsys) ---

        [Fact]
        public void RgbToHsv_TypicalValue()
        {
            var (h, s, v) = Colorsys.RgbToHsv(0.2, 0.4, 0.6);
            Assert.Equal(0.5833333333333334, h, Precision);
            Assert.Equal(0.6666666666666666, s, Precision);
            Assert.Equal(0.6, v, Precision);
        }

        [Fact]
        public void RgbToHsv_Black()
        {
            var (h, s, v) = Colorsys.RgbToHsv(0.0, 0.0, 0.0);
            Assert.Equal(0.0, h, Precision);
            Assert.Equal(0.0, s, Precision);
            Assert.Equal(0.0, v, Precision);
        }

        [Fact]
        public void RgbToHsv_White()
        {
            var (h, s, v) = Colorsys.RgbToHsv(1.0, 1.0, 1.0);
            Assert.Equal(0.0, h, Precision);
            Assert.Equal(0.0, s, Precision);
            Assert.Equal(1.0, v, Precision);
        }

        [Fact]
        public void RgbToHsv_PureRed()
        {
            var (h, s, v) = Colorsys.RgbToHsv(1.0, 0.0, 0.0);
            Assert.Equal(0.0, h, Precision);
            Assert.Equal(1.0, s, Precision);
            Assert.Equal(1.0, v, Precision);
        }

        [Fact]
        public void RgbToHsv_PureGreen()
        {
            var (h, s, v) = Colorsys.RgbToHsv(0.0, 1.0, 0.0);
            Assert.Equal(0.3333333333333333, h, Precision);
            Assert.Equal(1.0, s, Precision);
            Assert.Equal(1.0, v, Precision);
        }

        [Fact]
        public void RgbToHsv_PureBlue()
        {
            var (h, s, v) = Colorsys.RgbToHsv(0.0, 0.0, 1.0);
            Assert.Equal(0.6666666666666666, h, Precision);
            Assert.Equal(1.0, s, Precision);
            Assert.Equal(1.0, v, Precision);
        }

        // --- RgbToHls known values ---

        [Fact]
        public void RgbToHls_TypicalValue()
        {
            var (h, l, s) = Colorsys.RgbToHls(0.2, 0.4, 0.6);
            Assert.Equal(0.5833333333333334, h, Precision);
            Assert.Equal(0.4, l, Precision);
            Assert.Equal(0.49999999999999994, s, Precision);
        }

        [Fact]
        public void RgbToHls_Black()
        {
            var (h, l, s) = Colorsys.RgbToHls(0.0, 0.0, 0.0);
            Assert.Equal(0.0, h, Precision);
            Assert.Equal(0.0, l, Precision);
            Assert.Equal(0.0, s, Precision);
        }

        [Fact]
        public void RgbToHls_White()
        {
            var (h, l, s) = Colorsys.RgbToHls(1.0, 1.0, 1.0);
            Assert.Equal(0.0, h, Precision);
            Assert.Equal(1.0, l, Precision);
            Assert.Equal(0.0, s, Precision);
        }

        // --- RgbToYiq known values ---

        [Fact]
        public void RgbToYiq_TypicalValue()
        {
            var (y, i, q) = Colorsys.RgbToYiq(0.2, 0.4, 0.6);
            Assert.Equal(0.362, y, Precision);
            Assert.Equal(-0.18413999999999997, i, Precision);
            Assert.Equal(0.019820000000000004, q, Precision);
        }

        [Fact]
        public void RgbToYiq_White()
        {
            var (y, i, q) = Colorsys.RgbToYiq(1.0, 1.0, 1.0);
            Assert.Equal(1.0, y, Precision);
            Assert.Equal(0.0, i, Precision);
            Assert.Equal(0.0, q, Precision);
        }

        // --- HSV roundtrip ---

        [Theory]
        [InlineData(0.2, 0.4, 0.6)]
        [InlineData(0.0, 0.0, 0.0)]
        [InlineData(1.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 0.0)]
        [InlineData(0.0, 1.0, 0.0)]
        [InlineData(0.0, 0.0, 1.0)]
        [InlineData(0.75, 0.25, 0.5)]
        public void HsvRoundtrip_MatchesOriginal(double r, double g, double b)
        {
            var (h, s, v) = Colorsys.RgbToHsv(r, g, b);
            var (r2, g2, b2) = Colorsys.HsvToRgb(h, s, v);
            Assert.Equal(r, r2, Precision);
            Assert.Equal(g, g2, Precision);
            Assert.Equal(b, b2, Precision);
        }

        // --- HLS roundtrip ---

        [Theory]
        [InlineData(0.2, 0.4, 0.6)]
        [InlineData(0.0, 0.0, 0.0)]
        [InlineData(1.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 0.0)]
        [InlineData(0.0, 1.0, 0.0)]
        [InlineData(0.0, 0.0, 1.0)]
        [InlineData(0.75, 0.25, 0.5)]
        public void HlsRoundtrip_MatchesOriginal(double r, double g, double b)
        {
            var (h, l, s) = Colorsys.RgbToHls(r, g, b);
            var (r2, g2, b2) = Colorsys.HlsToRgb(h, l, s);
            Assert.Equal(r, r2, Precision);
            Assert.Equal(g, g2, Precision);
            Assert.Equal(b, b2, Precision);
        }

        // --- YIQ roundtrip ---

        [Theory]
        [InlineData(0.2, 0.4, 0.6)]
        [InlineData(0.0, 0.0, 0.0)]
        [InlineData(1.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 0.0)]
        [InlineData(0.0, 1.0, 0.0)]
        [InlineData(0.0, 0.0, 1.0)]
        [InlineData(0.75, 0.25, 0.5)]
        public void YiqRoundtrip_MatchesOriginal(double r, double g, double b)
        {
            var (y, i, q) = Colorsys.RgbToYiq(r, g, b);
            var (r2, g2, b2) = Colorsys.YiqToRgb(y, i, q);
            Assert.Equal(r, r2, Precision);
            Assert.Equal(g, g2, Precision);
            Assert.Equal(b, b2, Precision);
        }
    }
}

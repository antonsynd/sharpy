using System;
using Xunit;

namespace Sharpy.Core.Tests
{
    /// <summary>
    /// Pins the runtime contract of the unittest assertion markers added for #837:
    /// they exist for type resolution only, and calling them directly (i.e., outside
    /// a compiler-rewritten @test function) throws NotSupportedException.
    /// </summary>
    public class UnittestMarkerTests
    {
        [Fact]
        public void Approx_CalledDirectly_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Unittest.Approx(0.3));
        }

        [Fact]
        public void AssertCountEqual_CalledDirectly_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Unittest.AssertCountEqual(new[] { 1 }, new[] { 1 }));
        }

        [Fact]
        public void AssertRegex_CalledDirectly_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Unittest.AssertRegex("text", "pattern"));
        }

        [Fact]
        public void AssertRaises_WithMatch_CalledDirectly_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => Unittest.AssertRaises(typeof(ValueError), "match"));
        }
    }
}

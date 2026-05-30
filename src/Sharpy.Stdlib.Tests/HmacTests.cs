using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HmacTests
{
    [Fact]
    public void HmacSha256_KnownValue()
    {
        var h = Sharpy.HmacModule.New("secret", "message", "sha256");
        h.Hexdigest().Should().Be("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b");
    }

    [Fact]
    public void HmacSha256_IncrementalUpdate()
    {
        var h = Sharpy.HmacModule.New("secret", digestmod: "sha256");
        h.Update("message");
        h.Hexdigest().Should().Be("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b");
    }

    [Fact]
    public void HmacSha256_BytesOverload()
    {
        var key = new Sharpy.Bytes(System.Text.Encoding.UTF8.GetBytes("secret"));
        var msg = new Sharpy.Bytes(System.Text.Encoding.UTF8.GetBytes("message"));
        var h = Sharpy.HmacModule.New(key, msg, "sha256");
        h.Hexdigest().Should().Be("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b");
    }

    [Fact]
    public void Copy_ProducesIndependentClone()
    {
        var h1 = Sharpy.HmacModule.New("key", "hello", "sha256");
        var h2 = h1.Copy();
        h2.Update(" world");
        h1.Hexdigest().Should().NotBe(h2.Hexdigest());
    }

    [Fact]
    public void Name_Property()
    {
        var h = Sharpy.HmacModule.New("key", digestmod: "sha512");
        h.Name.Should().Be("hmac-sha512");
    }

    [Fact]
    public void DigestSize_Property()
    {
        Sharpy.HmacModule.New("key", digestmod: "sha256").DigestSize.Should().Be(32);
        Sharpy.HmacModule.New("key", digestmod: "sha512").DigestSize.Should().Be(64);
        Sharpy.HmacModule.New("key", digestmod: "md5").DigestSize.Should().Be(16);
    }

    [Fact]
    public void BlockSize_Property()
    {
        Sharpy.HmacModule.New("key", digestmod: "sha256").BlockSize.Should().Be(64);
        Sharpy.HmacModule.New("key", digestmod: "sha512").BlockSize.Should().Be(128);
    }

    [Fact]
    public void CompareDigest_MatchingStrings()
    {
        Sharpy.HmacModule.CompareDigest("hello", "hello").Should().BeTrue();
    }

    [Fact]
    public void CompareDigest_NonMatchingStrings()
    {
        Sharpy.HmacModule.CompareDigest("hello", "world").Should().BeFalse();
    }

    [Fact]
    public void CompareDigest_Bytes()
    {
        var a = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
        var b = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
        Sharpy.HmacModule.CompareDigest(a, b).Should().BeTrue();
    }

    [Fact]
    public void Digest_OneShotReturnsBytes()
    {
        var result = Sharpy.HmacModule.Digest("secret", "message", "sha256");
        result.Length.Should().Be(32);
    }

    [Fact]
    public void UnsupportedAlgorithm_ThrowsValueError()
    {
        var act = () => Sharpy.HmacModule.New("key", digestmod: "unsupported");
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Update_WithBytes()
    {
        var h = Sharpy.HmacModule.New("secret", digestmod: "sha256");
        h.Update(new Sharpy.Bytes(System.Text.Encoding.UTF8.GetBytes("message")));
        h.Hexdigest().Should().Be("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b");
    }
}

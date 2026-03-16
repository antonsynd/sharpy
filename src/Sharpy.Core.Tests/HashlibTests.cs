using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HashlibTests
{
    [Fact]
    public void Sha256_HelloProducesKnownHash()
    {
        var h = Sharpy.Hashlib.Sha256("hello");
        h.Hexdigest().Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public void Md5_HelloProducesKnownHash()
    {
        var h = Sharpy.Hashlib.Md5("hello");
        h.Hexdigest().Should().Be("5d41402abc4b2a76b9719d911017c592");
    }

    [Fact]
    public void Sha1_HelloProducesKnownHash()
    {
        var h = Sharpy.Hashlib.Sha1("hello");
        h.Hexdigest().Should().Be("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d");
    }

    [Fact]
    public void Sha384_HelloProducesKnownHash()
    {
        var h = Sharpy.Hashlib.Sha384("hello");
        h.Hexdigest().Should().Be("59e1748777448c69de6b800d7a33bbfb9ff1b463e44354c3553bcdb9c666fa90125a3c79f90397bdf5f6a13de828684f");
    }

    [Fact]
    public void Sha512_HelloProducesKnownHash()
    {
        var h = Sharpy.Hashlib.Sha512("hello");
        h.Hexdigest().Should().Be("9b71d224bd62f3785d96d46ad3ea3d73319bfbc2890caadae2dff72519673ca72323c3d99ba5c11d7c7acc6e14b8c5da0c4663475c2e5c3adef46f73bcdec043");
    }

    [Fact]
    public void Update_ThenHexdigest_MatchesPython()
    {
        var h = Sharpy.Hashlib.Sha256();
        h.Update("hello");
        h.Hexdigest().Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public void Update_MultipleCallsAccumulate()
    {
        // Python: hashlib.sha256(bhelloworld).hexdigest() ==
        //         h = hashlib.sha256(bhello); h.update(bworld); h.hexdigest()
        var h1 = Sharpy.Hashlib.Sha256("helloworld");
        var h2 = Sharpy.Hashlib.Sha256("hello");
        h2.Update("world");
        h2.Hexdigest().Should().Be(h1.Hexdigest());
    }

    [Fact]
    public void DigestSize_ReturnsCorrectValues()
    {
        Sharpy.Hashlib.Md5().DigestSize.Should().Be(16);
        Sharpy.Hashlib.Sha1().DigestSize.Should().Be(20);
        Sharpy.Hashlib.Sha256().DigestSize.Should().Be(32);
        Sharpy.Hashlib.Sha384().DigestSize.Should().Be(48);
        Sharpy.Hashlib.Sha512().DigestSize.Should().Be(64);
    }

    [Fact]
    public void Name_ReturnsAlgorithmName()
    {
        Sharpy.Hashlib.Md5().Name.Should().Be("md5");
        Sharpy.Hashlib.Sha1().Name.Should().Be("sha1");
        Sharpy.Hashlib.Sha256().Name.Should().Be("sha256");
        Sharpy.Hashlib.Sha384().Name.Should().Be("sha384");
        Sharpy.Hashlib.Sha512().Name.Should().Be("sha512");
    }

    [Fact]
    public void Digest_ReturnsRawBytes()
    {
        var h = Sharpy.Hashlib.Md5("hello");
        var digest = h.Digest();
        digest.Should().HaveCount(16);
        // First byte of MD5("hello") is 93 (0x5d)
        digest[0].Should().Be(93);
    }

    [Fact]
    public void Copy_ProducesIndependentClone()
    {
        var h = Sharpy.Hashlib.Sha256("hello");
        var copy = h.Copy();
        copy.Update("world");

        // Original should be unchanged
        h.Hexdigest().Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
        // Copy should have "helloworld"
        copy.Hexdigest().Should().Be(Sharpy.Hashlib.Sha256("helloworld").Hexdigest());
    }

    [Fact]
    public void EmptyString_ProducesKnownHash()
    {
        // Python: hashlib.sha256(b).hexdigest()
        var h = Sharpy.Hashlib.Sha256();
        h.Hexdigest().Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }
}

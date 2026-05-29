using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HmacTests
{
    // RFC 2104 / RFC 4231 test vectors
    // HMAC-SHA256("key", "The quick brown fox jumps over the lazy dog")
    // = f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8
    private const string Rfc4231Key = "key";
    private const string Rfc4231Msg = "The quick brown fox jumps over the lazy dog";
    private const string Rfc4231Sha256Expected = "f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8";

    [Fact]
    public void New_Sha256_ProducesKnownHmac()
    {
        var h = Sharpy.HmacModule.New(Rfc4231Key, Rfc4231Msg, "sha256");
        h.Hexdigest().Should().Be(Rfc4231Sha256Expected);
    }

    [Fact]
    public void Digest_OneShot_Sha256_ProducesKnownHmac()
    {
        var digest = Sharpy.HmacModule.Digest(Rfc4231Key, Rfc4231Msg, "sha256");
        digest.Should().HaveCount(32);
        // First byte: 0xf7 = 247
        digest[0].Should().Be(0xf7);
    }

    [Fact]
    public void New_IncrementalUpdate_MatchesOneShot()
    {
        var h = Sharpy.HmacModule.New("secret", digestmod: "sha256");
        h.Update("hello");
        h.Update("world");

        var oneShot = Sharpy.HmacModule.New("secret", "helloworld", "sha256");
        h.Hexdigest().Should().Be(oneShot.Hexdigest());
    }

    [Fact]
    public void New_Sha512_ProducesCorrectDigestSize()
    {
        var h = Sharpy.HmacModule.New("key", "msg", "sha512");
        h.Digest().Should().HaveCount(64);
        h.DigestSize.Should().Be(64);
    }

    [Fact]
    public void New_Sha1_ProducesKnownHmac()
    {
        // HMAC-SHA1("key", "The quick brown fox jumps over the lazy dog")
        // = de7c9b85b8b78aa6bc8a7a36f70a90701c9db4d9
        var h = Sharpy.HmacModule.New("key", "The quick brown fox jumps over the lazy dog", "sha1");
        h.Hexdigest().Should().Be("de7c9b85b8b78aa6bc8a7a36f70a90701c9db4d9");
    }

    [Fact]
    public void New_Md5_ProducesKnownHmac()
    {
        // HMAC-MD5("key", "The quick brown fox jumps over the lazy dog")
        // = 80070713463e7749b90c2dc24911e275
        var h = Sharpy.HmacModule.New("key", "The quick brown fox jumps over the lazy dog", "md5");
        h.Hexdigest().Should().Be("80070713463e7749b90c2dc24911e275");
    }

    [Fact]
    public void CompareDigest_EqualStrings_ReturnsTrue()
    {
        Sharpy.HmacModule.CompareDigest("abc123", "abc123").Should().BeTrue();
    }

    [Fact]
    public void CompareDigest_DifferentStrings_ReturnsFalse()
    {
        Sharpy.HmacModule.CompareDigest("abc123", "abc124").Should().BeFalse();
    }

    [Fact]
    public void CompareDigest_DifferentLengths_ReturnsFalse()
    {
        Sharpy.HmacModule.CompareDigest("short", "longer_string").Should().BeFalse();
    }

    [Fact]
    public void CompareDigest_EmptyStrings_ReturnsTrue()
    {
        Sharpy.HmacModule.CompareDigest("", "").Should().BeTrue();
    }

    [Fact]
    public void New_EmptyMessage_ProducesValidHmac()
    {
        var h = Sharpy.HmacModule.New("key", "", "sha256");
        h.Hexdigest().Should().NotBeNullOrEmpty();
        h.Hexdigest().Length.Should().Be(64); // SHA256 hex is 64 chars
    }

    [Fact]
    public void New_EmptyKey_ProducesValidHmac()
    {
        var h = Sharpy.HmacModule.New("", "message", "sha256");
        h.Hexdigest().Should().NotBeNullOrEmpty();
        h.Hexdigest().Length.Should().Be(64);
    }

    [Fact]
    public void Name_ReturnsHmacPrefixedAlgorithm()
    {
        Sharpy.HmacModule.New("k", digestmod: "sha256").Name.Should().Be("hmac-sha256");
        Sharpy.HmacModule.New("k", digestmod: "sha512").Name.Should().Be("hmac-sha512");
        Sharpy.HmacModule.New("k", digestmod: "md5").Name.Should().Be("hmac-md5");
    }

    [Fact]
    public void DigestSize_ReturnsCorrectValues()
    {
        Sharpy.HmacModule.New("k", digestmod: "md5").DigestSize.Should().Be(16);
        Sharpy.HmacModule.New("k", digestmod: "sha1").DigestSize.Should().Be(20);
        Sharpy.HmacModule.New("k", digestmod: "sha256").DigestSize.Should().Be(32);
        Sharpy.HmacModule.New("k", digestmod: "sha384").DigestSize.Should().Be(48);
        Sharpy.HmacModule.New("k", digestmod: "sha512").DigestSize.Should().Be(64);
    }

    [Fact]
    public void Copy_ProducesIndependentClone()
    {
        var h = Sharpy.HmacModule.New("key", "hello", "sha256");
        var copy = h.Copy();
        copy.Update("world");

        // Original should be unchanged
        h.Hexdigest().Should().Be(Sharpy.HmacModule.New("key", "hello", "sha256").Hexdigest());
        // Copy should have "helloworld"
        copy.Hexdigest().Should().Be(Sharpy.HmacModule.New("key", "helloworld", "sha256").Hexdigest());
    }

    [Fact]
    public void New_UnsupportedAlgorithm_ThrowsValueError()
    {
        var act = () => Sharpy.HmacModule.New("key", "msg", "unsupported");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Digest_UnsupportedAlgorithm_ThrowsValueError()
    {
        var act = () => Sharpy.HmacModule.Digest("key", "msg", "unsupported");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void New_Sha384_ProducesCorrectLength()
    {
        var h = Sharpy.HmacModule.New("key", "msg", "sha384");
        h.Hexdigest().Length.Should().Be(96); // SHA384 hex is 96 chars
        h.DigestSize.Should().Be(48);
    }
}

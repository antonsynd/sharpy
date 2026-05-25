using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Hashlib tests not covered by HashlibTests.cs (12 tests).
/// Covers empty-string hashes, Digest() for all algorithms, Copy(), and more.
/// </summary>
public class HashlibCompleteTests
{
    // --- Empty string hashes (known values) ---

    [Fact]
    public void Md5_EmptyString_ProducesKnownHash()
    {
        var h = Sharpy.HashlibModule.Md5();
        h.Hexdigest().Should().Be("d41d8cd98f00b204e9800998ecf8427e");
    }

    [Fact]
    public void Sha1_EmptyString_ProducesKnownHash()
    {
        var h = Sharpy.HashlibModule.Sha1();
        h.Hexdigest().Should().Be("da39a3ee5e6b4b0d3255bfef95601890afd80709");
    }

    [Fact]
    public void Sha384_EmptyString_ProducesKnownHash()
    {
        var h = Sharpy.HashlibModule.Sha384();
        // Python: hashlib.sha384(b'').hexdigest()
        h.Hexdigest().Should().Be("38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b");
    }

    [Fact]
    public void Sha512_EmptyString_ProducesKnownHash()
    {
        var h = Sharpy.HashlibModule.Sha512();
        // Python: hashlib.sha512(b'').hexdigest()
        h.Hexdigest().Should().Be("cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e");
    }

    // --- Digest() lengths for all algorithms ---

    [Fact]
    public void Sha1_Digest_LengthIs20()
    {
        var h = Sharpy.HashlibModule.Sha1("hello");
        h.Digest().Should().HaveCount(20);
    }

    [Fact]
    public void Sha256_Digest_LengthIs32()
    {
        var h = Sharpy.HashlibModule.Sha256("hello");
        h.Digest().Should().HaveCount(32);
    }

    [Fact]
    public void Sha384_Digest_LengthIs48()
    {
        var h = Sharpy.HashlibModule.Sha384("hello");
        h.Digest().Should().HaveCount(48);
    }

    [Fact]
    public void Sha512_Digest_LengthIs64()
    {
        var h = Sharpy.HashlibModule.Sha512("hello");
        h.Digest().Should().HaveCount(64);
    }

    // --- Digest() byte value verification ---

    [Fact]
    public void Sha256_Digest_FirstByte_MatchesHexdigest()
    {
        var h = Sharpy.HashlibModule.Sha256("hello");
        // sha256("hello") starts with "2c" = 0x2c = 44
        var digest = h.Digest();
        digest[0].Should().Be(0x2c);
    }

    [Fact]
    public void Sha1_Digest_FirstByte_MatchesHexdigest()
    {
        var h = Sharpy.HashlibModule.Sha1("hello");
        // sha1("hello") starts with "aa" = 0xaa = 170
        var digest = h.Digest();
        digest[0].Should().Be(0xaa);
    }

    // --- DigestSize matches Hexdigest length ---

    [Fact]
    public void Md5_HexdigestLength_Is_DigestSize_Times_Two()
    {
        var h = Sharpy.HashlibModule.Md5("hello");
        h.Hexdigest().Length.Should().Be(h.DigestSize * 2);
    }

    [Fact]
    public void Sha256_HexdigestLength_Is_DigestSize_Times_Two()
    {
        var h = Sharpy.HashlibModule.Sha256("hello");
        h.Hexdigest().Length.Should().Be(h.DigestSize * 2);
    }

    [Fact]
    public void Sha512_HexdigestLength_Is_DigestSize_Times_Two()
    {
        var h = Sharpy.HashlibModule.Sha512("hello");
        h.Hexdigest().Length.Should().Be(h.DigestSize * 2);
    }

    // --- Hexdigest is lowercase ---

    [Fact]
    public void Md5_Hexdigest_IsLowercase()
    {
        var h = Sharpy.HashlibModule.Md5("hello");
        string hex = h.Hexdigest();
        hex.Should().Be(hex.ToLowerInvariant());
    }

    [Fact]
    public void Sha256_Hexdigest_IsLowercase()
    {
        var h = Sharpy.HashlibModule.Sha256("hello");
        string hex = h.Hexdigest();
        hex.Should().Be(hex.ToLowerInvariant());
    }

    // --- Copy() for various algorithms ---

    [Fact]
    public void Md5_Copy_ProducesIndependentClone()
    {
        var h = Sharpy.HashlibModule.Md5("hello");
        var copy = h.Copy();
        copy.Update("world");

        // Original should be unchanged
        h.Hexdigest().Should().Be("5d41402abc4b2a76b9719d911017c592");
        // Copy should have "helloworld"
        copy.Hexdigest().Should().Be(Sharpy.HashlibModule.Md5("helloworld").Hexdigest());
    }

    [Fact]
    public void Sha1_Copy_ProducesIndependentClone()
    {
        var h = Sharpy.HashlibModule.Sha1("hello");
        var copy = h.Copy();
        copy.Update("world");

        // Original should be unchanged
        h.Hexdigest().Should().Be("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d");
        // Copy should have "helloworld"
        copy.Hexdigest().Should().Be(Sharpy.HashlibModule.Sha1("helloworld").Hexdigest());
    }

    // --- Update with empty string ---

    [Fact]
    public void Update_EmptyString_DoesNotChangeHash()
    {
        var h1 = Sharpy.HashlibModule.Sha256("hello");
        var h2 = Sharpy.HashlibModule.Sha256("hello");
        h2.Update("");

        // sha256("hello") + sha256("") should still equal sha256("hello") since no data was added
        h2.Hexdigest().Should().Be(h1.Hexdigest());
    }

    // --- Update chaining equivalence ---

    [Fact]
    public void Md5_IncrementalUpdate_MatchesSingleUpdate()
    {
        var h1 = Sharpy.HashlibModule.Md5("helloworld");
        var h2 = Sharpy.HashlibModule.Md5("hello");
        h2.Update("world");

        h2.Hexdigest().Should().Be(h1.Hexdigest());
    }

    [Fact]
    public void Sha512_IncrementalUpdate_MatchesSingleUpdate()
    {
        var h1 = Sharpy.HashlibModule.Sha512("helloworld");
        var h2 = Sharpy.HashlibModule.Sha512("hello");
        h2.Update("world");

        h2.Hexdigest().Should().Be(h1.Hexdigest());
    }

    // --- Name property ---

    [Fact]
    public void Md5_Name_IsMd5()
    {
        Sharpy.HashlibModule.Md5().Name.Should().Be("md5");
    }

    [Fact]
    public void Sha256_Name_IsSha256()
    {
        Sharpy.HashlibModule.Sha256().Name.Should().Be("sha256");
    }
}

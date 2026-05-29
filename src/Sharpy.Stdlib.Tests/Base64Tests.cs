using FluentAssertions;
using Xunit;

namespace Sharpy.Stdlib.Tests;

public class Base64Tests
{
    // ─── b64encode / b64decode ───────────────────────────────────────────

    [Fact]
    public void B64encode_HelloWorld()
    {
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes("Hello, World!"));
        var encoded = Base64Module.B64encode(data);
        System.Text.Encoding.ASCII.GetString(encoded.ToArray()).Should().Be("SGVsbG8sIFdvcmxkIQ==");
    }

    [Fact]
    public void B64decode_HelloWorld()
    {
        var encoded = new Bytes(System.Text.Encoding.ASCII.GetBytes("SGVsbG8sIFdvcmxkIQ=="));
        var decoded = Base64Module.B64decode(encoded);
        System.Text.Encoding.UTF8.GetString(decoded.ToArray()).Should().Be("Hello, World!");
    }

    [Fact]
    public void B64_Roundtrip()
    {
        var data = new Bytes(new byte[] { 0, 1, 2, 3, 255, 254, 253 });
        var result = Base64Module.B64decode(Base64Module.B64encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void B64encode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var encoded = Base64Module.B64encode(data);
        encoded.Length.Should().Be(0);
    }

    [Fact]
    public void B64decode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var decoded = Base64Module.B64decode(data);
        decoded.Length.Should().Be(0);
    }

    [Fact]
    public void B64encode_WithAltchars()
    {
        var data = new Bytes(new byte[] { 251, 255, 254 }); // produces +/
        var altchars = new Bytes(new byte[] { (byte)'-', (byte)'_' });
        var encoded = Base64Module.B64encode(data, altchars);
        var encodedStr = System.Text.Encoding.ASCII.GetString(encoded.ToArray());
        encodedStr.Should().NotContain("+").And.NotContain("/");
    }

    [Fact]
    public void B64decode_WithAltchars_Roundtrip()
    {
        var data = new Bytes(new byte[] { 251, 255, 254 });
        var altchars = new Bytes(new byte[] { (byte)'-', (byte)'_' });
        var encoded = Base64Module.B64encode(data, altchars);
        var decoded = Base64Module.B64decode(encoded, altchars);
        decoded.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void B64decode_InvalidInput_ThrowsValueError()
    {
        var invalid = new Bytes(System.Text.Encoding.ASCII.GetBytes("!!!"));
        var act = () => Base64Module.B64decode(invalid, validate: true);
        act.Should().Throw<ValueError>();
    }

    // ─── urlsafe_b64encode / urlsafe_b64decode ───────────────────────────

    [Fact]
    public void UrlsafeB64encode_NoSlashOrPlus()
    {
        var data = new Bytes(new byte[] { 251, 255, 254, 251, 255, 254 });
        var encoded = Base64Module.UrlsafeB64encode(data);
        var str = System.Text.Encoding.ASCII.GetString(encoded.ToArray());
        str.Should().NotContain("+").And.NotContain("/");
    }

    [Fact]
    public void UrlsafeB64_Roundtrip()
    {
        var data = new Bytes(new byte[] { 0, 128, 255, 63, 191 });
        var result = Base64Module.UrlsafeB64decode(Base64Module.UrlsafeB64encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    // ─── b32encode / b32decode ───────────────────────────────────────────

    [Fact]
    public void B32encode_Hello()
    {
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes("hello"));
        var encoded = Base64Module.B32encode(data);
        System.Text.Encoding.ASCII.GetString(encoded.ToArray()).Should().Be("NBSWY3DP");
    }

    [Fact]
    public void B32decode_Hello()
    {
        var encoded = new Bytes(System.Text.Encoding.ASCII.GetBytes("NBSWY3DP"));
        var decoded = Base64Module.B32decode(encoded);
        System.Text.Encoding.UTF8.GetString(decoded.ToArray()).Should().Be("hello");
    }

    [Fact]
    public void B32_Roundtrip()
    {
        var data = new Bytes(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var result = Base64Module.B32decode(Base64Module.B32encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void B32encode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var encoded = Base64Module.B32encode(data);
        encoded.Length.Should().Be(0);
    }

    [Fact]
    public void B32decode_CaseInsensitive()
    {
        var upper = new Bytes(System.Text.Encoding.ASCII.GetBytes("NBSWY3DP"));
        var lower = new Bytes(System.Text.Encoding.ASCII.GetBytes("nbswy3dp"));
        var decoded1 = Base64Module.B32decode(upper);
        var decoded2 = Base64Module.B32decode(lower);
        decoded1.ToArray().Should().BeEquivalentTo(decoded2.ToArray());
    }

    // ─── b16encode / b16decode ───────────────────────────────────────────

    [Fact]
    public void B16encode_Hello()
    {
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes("hello"));
        var encoded = Base64Module.B16encode(data);
        System.Text.Encoding.ASCII.GetString(encoded.ToArray()).Should().Be("68656C6C6F");
    }

    [Fact]
    public void B16decode_Hello()
    {
        var encoded = new Bytes(System.Text.Encoding.ASCII.GetBytes("68656C6C6F"));
        var decoded = Base64Module.B16decode(encoded);
        System.Text.Encoding.UTF8.GetString(decoded.ToArray()).Should().Be("hello");
    }

    [Fact]
    public void B16_Roundtrip()
    {
        var data = new Bytes(new byte[] { 0, 127, 128, 255 });
        var result = Base64Module.B16decode(Base64Module.B16encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void B16encode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var encoded = Base64Module.B16encode(data);
        encoded.Length.Should().Be(0);
    }

    [Fact]
    public void B16decode_OddLength_ThrowsValueError()
    {
        var invalid = new Bytes(System.Text.Encoding.ASCII.GetBytes("ABC"));
        var act = () => Base64Module.B16decode(invalid);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void B16decode_InvalidHex_ThrowsValueError()
    {
        var invalid = new Bytes(System.Text.Encoding.ASCII.GetBytes("ZZZZ"));
        var act = () => Base64Module.B16decode(invalid);
        act.Should().Throw<ValueError>();
    }

    // ─── b85encode / b85decode ───────────────────────────────────────────

    [Fact]
    public void B85_Roundtrip()
    {
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes("Hello, World!"));
        var result = Base64Module.B85decode(Base64Module.B85encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void B85encode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var encoded = Base64Module.B85encode(data);
        encoded.Length.Should().Be(0);
    }

    [Fact]
    public void B85_Roundtrip_Binary()
    {
        var data = new Bytes(new byte[] { 0, 1, 2, 3, 4, 5, 255, 254, 253 });
        var result = Base64Module.B85decode(Base64Module.B85encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    // ─── a85encode / a85decode ───────────────────────────────────────────

    [Fact]
    public void A85_Roundtrip()
    {
        var data = new Bytes(System.Text.Encoding.UTF8.GetBytes("Hello, World!"));
        var result = Base64Module.A85decode(Base64Module.A85encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Fact]
    public void A85encode_EmptyInput()
    {
        var data = new Bytes(System.Array.Empty<byte>());
        var encoded = Base64Module.A85encode(data);
        encoded.Length.Should().Be(0);
    }

    [Fact]
    public void A85_ZeroBlock_UsesZShorthand()
    {
        // 4 zero bytes should produce 'z' in Ascii85
        var data = new Bytes(new byte[] { 0, 0, 0, 0 });
        var encoded = Base64Module.A85encode(data);
        var str = System.Text.Encoding.ASCII.GetString(encoded.ToArray());
        str.Should().Be("z");
    }

    [Fact]
    public void A85_Roundtrip_Binary()
    {
        var data = new Bytes(new byte[] { 0, 0, 0, 0, 1, 2, 3, 4, 255, 254, 253 });
        var result = Base64Module.A85decode(Base64Module.A85encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    // ─── Padding handling ────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void B64_Roundtrip_VariousLengths(int length)
    {
        var data = new Bytes(new byte[length]);
        var result = Base64Module.B64decode(Base64Module.B64encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void B32_Roundtrip_VariousLengths(int length)
    {
        var data = new Bytes(new byte[length]);
        var result = Base64Module.B32decode(Base64Module.B32encode(data));
        result.ToArray().Should().BeEquivalentTo(data.ToArray());
    }
}

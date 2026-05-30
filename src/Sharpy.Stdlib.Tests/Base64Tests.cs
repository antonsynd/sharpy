using Xunit;
using FluentAssertions;
using System.Text;

namespace Sharpy.Core.Tests;

public class Base64Tests
{
    [Fact]
    public void B64encode_HelloWorld()
    {
        var result = Sharpy.Base64Module.B64encode(new Sharpy.Bytes(Encoding.ASCII.GetBytes("hello world")));
        Encoding.ASCII.GetString(result.ToArray()).Should().Be("aGVsbG8gd29ybGQ=");
    }

    [Fact]
    public void B64decode_Roundtrip()
    {
        var original = new Sharpy.Bytes(Encoding.ASCII.GetBytes("test data"));
        var encoded = Sharpy.Base64Module.B64encode(original);
        var decoded = Sharpy.Base64Module.B64decode(encoded);
        decoded.ToArray().Should().BeEquivalentTo(original.ToArray());
    }

    [Fact]
    public void UrlsafeB64encode_ReplacesChars()
    {
        var data = new Sharpy.Bytes(new byte[] { 0xFF, 0xFE, 0xFD });
        var result = Sharpy.Base64Module.UrlsafeB64encode(data);
        var str = Encoding.ASCII.GetString(result.ToArray());
        str.Should().NotContain("+").And.NotContain("/");
    }

    [Fact]
    public void B32encode_Hello()
    {
        var result = Sharpy.Base64Module.B32encode(new Sharpy.Bytes(Encoding.ASCII.GetBytes("hello")));
        Encoding.ASCII.GetString(result.ToArray()).Should().Be("NBSWY3DP");
    }

    [Fact]
    public void B32decode_RejectsLowercaseByDefault()
    {
        var input = new Sharpy.Bytes(Encoding.ASCII.GetBytes("nbswy3dp"));
        var act = () => Sharpy.Base64Module.B32decode(input);
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void B32decode_AcceptsLowercaseWithCasefold()
    {
        var input = new Sharpy.Bytes(Encoding.ASCII.GetBytes("nbswy3dp"));
        var result = Sharpy.Base64Module.B32decode(input, casefold: true);
        Encoding.ASCII.GetString(result.ToArray()).Should().Be("hello");
    }

    [Fact]
    public void B16encode_ProducesUppercase()
    {
        var result = Sharpy.Base64Module.B16encode(new Sharpy.Bytes(new byte[] { 0xDE, 0xAD }));
        Encoding.ASCII.GetString(result.ToArray()).Should().Be("DEAD");
    }

    [Fact]
    public void B16decode_RejectsLowercaseByDefault()
    {
        var input = new Sharpy.Bytes(Encoding.ASCII.GetBytes("dead"));
        var act = () => Sharpy.Base64Module.B16decode(input);
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void B16decode_AcceptsLowercaseWithCasefold()
    {
        var input = new Sharpy.Bytes(Encoding.ASCII.GetBytes("dead"));
        var result = Sharpy.Base64Module.B16decode(input, casefold: true);
        result.ToArray().Should().BeEquivalentTo(new byte[] { 0xDE, 0xAD });
    }

    [Fact]
    public void B85encode_Roundtrip()
    {
        var original = new Sharpy.Bytes(Encoding.ASCII.GetBytes("Hello World!"));
        var encoded = Sharpy.Base64Module.B85encode(original);
        var decoded = Sharpy.Base64Module.B85decode(encoded);
        decoded.ToArray().Should().BeEquivalentTo(original.ToArray());
    }

    [Fact]
    public void A85encode_Roundtrip()
    {
        var original = new Sharpy.Bytes(Encoding.ASCII.GetBytes("Hello World!"));
        var encoded = Sharpy.Base64Module.A85encode(original);
        var decoded = Sharpy.Base64Module.A85decode(encoded);
        decoded.ToArray().Should().BeEquivalentTo(original.ToArray());
    }

    [Fact]
    public void B64decode_StringOverload()
    {
        var result = Sharpy.Base64Module.B64decode("aGVsbG8=");
        Encoding.ASCII.GetString(result.ToArray()).Should().Be("hello");
    }

    [Fact]
    public void B64encode_Empty()
    {
        var result = Sharpy.Base64Module.B64encode(new Sharpy.Bytes(System.Array.Empty<byte>()));
        Encoding.ASCII.GetString(result.ToArray()).Should().BeEmpty();
    }
}

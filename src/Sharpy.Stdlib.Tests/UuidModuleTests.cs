using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class UuidModuleTests
{
    [Fact]
    public void Uuid4_GeneratesValidVersion4()
    {
        var id = Sharpy.UuidModule.Uuid4();
        id.Version.Should().Be(4);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void Uuid4_GeneratesUniqueValues()
    {
        var id1 = Sharpy.UuidModule.Uuid4();
        var id2 = Sharpy.UuidModule.Uuid4();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UUID_ParseFromString_StandardFormat()
    {
        var id = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id.ToString().Should().Be("12345678-1234-5678-1234-567812345678");
    }

    [Fact]
    public void UUID_ParseFromString_NoBraces()
    {
        var id = new Sharpy.UUID("12345678123456781234567812345678");
        id.Hex.Should().Be("12345678123456781234567812345678");
    }

    [Fact]
    public void UUID_ParseFromString_Invalid_ThrowsValueError()
    {
        var act = () => new Sharpy.UUID("not-a-uuid");
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void UUID_Hex_ReturnsNoDashes()
    {
        var id = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id.Hex.Should().Be("12345678123456781234567812345678");
        id.Hex.Should().HaveLength(32);
    }

    [Fact]
    public void UUID_UuidBytes_Returns16Bytes()
    {
        var id = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id.UuidBytes.Length.Should().Be(16);
    }

    [Fact]
    public void UUID_Equality()
    {
        var id1 = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        var id2 = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id1.Should().Be(id2);
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void UUID_Urn()
    {
        var id = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id.Urn.Should().Be("urn:uuid:12345678-1234-5678-1234-567812345678");
    }

    [Fact]
    public void Uuid3_KnownValue()
    {
        var result = Sharpy.UuidModule.Uuid3(Sharpy.UuidModule.NAMESPACE_DNS, "example.com");
        result.ToString().Should().Be("9073926b-929f-31c2-abc9-fad77ae3e8eb");
        result.Version.Should().Be(3);
    }

    [Fact]
    public void Uuid5_KnownValue()
    {
        var result = Sharpy.UuidModule.Uuid5(Sharpy.UuidModule.NAMESPACE_DNS, "example.com");
        result.ToString().Should().Be("cfbff0d1-9375-5685-968c-48ce8b15ae17");
        result.Version.Should().Be(5);
    }

    [Fact]
    public void Uuid1_GeneratesVersion1()
    {
        var id = Sharpy.UuidModule.Uuid1();
        id.Version.Should().Be(1);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void UUID_RfcFields()
    {
        var id = new Sharpy.UUID("12345678-1234-5678-1234-567812345678");
        id.TimeLow.Should().Be(0x12345678);
        id.TimeMid.Should().Be(0x1234);
        id.TimeHiVersion.Should().Be(0x5678);
    }
}

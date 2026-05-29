using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class UuidModuleTests
{
    [Fact]
    public void Uuid4_GeneratesValidVersion4()
    {
        var id = UuidModule.Uuid4();
        id.Version.Should().Be(4);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void Uuid4_GeneratesUniqueValues()
    {
        var id1 = UuidModule.Uuid4();
        var id2 = UuidModule.Uuid4();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UUID_ParseFromString_StandardFormat()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.ToString().Should().Be("12345678-1234-5678-1234-567812345678");
    }

    [Fact]
    public void UUID_ParseFromString_NoBraces()
    {
        var id = new UUID("12345678123456781234567812345678");
        id.Hex.Should().Be("12345678123456781234567812345678");
    }

    [Fact]
    public void UUID_ParseFromString_WithBraces()
    {
        var id = new UUID("{12345678-1234-5678-1234-567812345678}");
        id.ToString().Should().Be("12345678-1234-5678-1234-567812345678");
    }

    [Fact]
    public void UUID_ParseFromString_Invalid_ThrowsValueError()
    {
        var act = () => new UUID("not-a-uuid");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void UUID_ParseFromString_Null_ThrowsValueError()
    {
        var act = () => new UUID((string)null!);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void UUID_Hex_ReturnsNoDashes()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.Hex.Should().Be("12345678123456781234567812345678");
        id.Hex.Should().HaveLength(32);
    }

    [Fact]
    public void UUID_Bytes_Returns16Elements()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.Bytes.Should().HaveCount(16);
    }

    [Fact]
    public void UUID_Equality()
    {
        var id1 = new UUID("12345678-1234-5678-1234-567812345678");
        var id2 = new UUID("12345678-1234-5678-1234-567812345678");
        id1.Should().Be(id2);
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void UUID_Inequality()
    {
        var id1 = new UUID("12345678-1234-5678-1234-567812345678");
        var id2 = new UUID("87654321-4321-8765-4321-876543218765");
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UUID_CompareTo()
    {
        var id1 = new UUID("00000000-0000-0000-0000-000000000001");
        var id2 = new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff");
        id1.CompareTo(id2).Should().BeNegative();
    }

    [Fact]
    public void Uuid1_GeneratesVersion1()
    {
        var id = UuidModule.Uuid1();
        id.Version.Should().Be(1);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void Uuid1_GeneratesUniqueValues()
    {
        var id1 = UuidModule.Uuid1();
        var id2 = UuidModule.Uuid1();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Uuid3_GeneratesVersion3()
    {
        var id = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "example.com");
        id.Version.Should().Be(3);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void Uuid3_IsDeterministic()
    {
        var id1 = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "example.com");
        var id2 = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "example.com");
        id1.Should().Be(id2);
    }

    [Fact]
    public void Uuid3_DifferentNames_DifferentResults()
    {
        var id1 = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "example.com");
        var id2 = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "example.org");
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Uuid3_KnownValue()
    {
        // RFC 4122 Appendix B: uuid3(NAMESPACE_DNS, "python.org") = 6fa459ea-ee8a-3ca4-894e-db77e160355e
        var id = UuidModule.Uuid3(UuidModule.NAMESPACE_DNS, "python.org");
        id.ToString().Should().Be("6fa459ea-ee8a-3ca4-894e-db77e160355e");
    }

    [Fact]
    public void Uuid5_GeneratesVersion5()
    {
        var id = UuidModule.Uuid5(UuidModule.NAMESPACE_DNS, "example.com");
        id.Version.Should().Be(5);
        id.Variant.Should().Be("specified in RFC 4122");
    }

    [Fact]
    public void Uuid5_IsDeterministic()
    {
        var id1 = UuidModule.Uuid5(UuidModule.NAMESPACE_DNS, "example.com");
        var id2 = UuidModule.Uuid5(UuidModule.NAMESPACE_DNS, "example.com");
        id1.Should().Be(id2);
    }

    [Fact]
    public void Uuid5_KnownValue()
    {
        // uuid5(NAMESPACE_DNS, "python.org") = 886313e1-3b8a-5372-9b90-0c9aee199e5d
        var id = UuidModule.Uuid5(UuidModule.NAMESPACE_DNS, "python.org");
        id.ToString().Should().Be("886313e1-3b8a-5372-9b90-0c9aee199e5d");
    }

    [Fact]
    public void NamespaceConstants_AreCorrect()
    {
        UuidModule.NAMESPACE_DNS.ToString().Should().Be("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        UuidModule.NAMESPACE_URL.ToString().Should().Be("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
        UuidModule.NAMESPACE_OID.ToString().Should().Be("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
        UuidModule.NAMESPACE_X500.ToString().Should().Be("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
    }

    [Fact]
    public void UUID_FieldAccessors_TimeLow()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.TimeLow.Should().Be(0x12345678);
    }

    [Fact]
    public void UUID_FieldAccessors_TimeMid()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.TimeMid.Should().Be(0x1234);
    }

    [Fact]
    public void UUID_FieldAccessors_TimeHiVersion()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.TimeHiVersion.Should().Be(0x5678);
    }

    [Fact]
    public void UUID_FieldAccessors_ClockSeqHiVariant()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.ClockSeqHiVariant.Should().Be(0x12);
    }

    [Fact]
    public void UUID_FieldAccessors_ClockSeqLow()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.ClockSeqLow.Should().Be(0x34);
    }

    [Fact]
    public void UUID_FieldAccessors_Node()
    {
        var id = new UUID("12345678-1234-5678-1234-567812345678");
        id.Node.Should().Be(0x567812345678);
    }

    [Fact]
    public void UUID_ToString_Format()
    {
        var id = UuidModule.Uuid4();
        var str = id.ToString();
        str.Should().MatchRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
    }
}

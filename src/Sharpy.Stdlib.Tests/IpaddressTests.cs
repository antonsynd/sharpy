using System.Linq;
using System.Numerics;
using FluentAssertions;
using Sharpy;
using Xunit;

namespace Sharpy.Stdlib.Tests;

public class IpaddressTests
{
    // IPv4Address tests

    [Fact]
    public void IPv4Address_FromString()
    {
        var addr = new IPv4Address("192.168.1.1");
        addr.ToString().Should().Be("192.168.1.1");
        addr.Version.Should().Be(4);
    }

    [Fact]
    public void IPv4Address_FromInt()
    {
        var addr = new IPv4Address(3232235777L); // 192.168.1.1
        addr.ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IPv4Address_FromBytes()
    {
        var addr = new IPv4Address(new Bytes(new byte[] { 192, 168, 1, 1 }));
        addr.ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IPv4Address_Invalid()
    {
        var act = () => new IPv4Address("invalid");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void IPv4Address_IsPrivate()
    {
        new IPv4Address("192.168.1.1").IsPrivate.Should().BeTrue();
        new IPv4Address("10.0.0.1").IsPrivate.Should().BeTrue();
        new IPv4Address("172.16.0.1").IsPrivate.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsPrivate.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsLoopback()
    {
        new IPv4Address("127.0.0.1").IsLoopback.Should().BeTrue();
        new IPv4Address("127.255.255.255").IsLoopback.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsLoopback.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsMulticast()
    {
        new IPv4Address("224.0.0.1").IsMulticast.Should().BeTrue();
        new IPv4Address("239.255.255.255").IsMulticast.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsMulticast.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsReserved()
    {
        new IPv4Address("240.0.0.1").IsReserved.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsReserved.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsLinkLocal()
    {
        new IPv4Address("169.254.1.1").IsLinkLocal.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsLinkLocal.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsGlobal()
    {
        new IPv4Address("8.8.8.8").IsGlobal.Should().BeTrue();
        new IPv4Address("192.168.1.1").IsGlobal.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_IsUnspecified()
    {
        new IPv4Address("0.0.0.0").IsUnspecified.Should().BeTrue();
        new IPv4Address("8.8.8.8").IsUnspecified.Should().BeFalse();
    }

    [Fact]
    public void IPv4Address_Packed()
    {
        var addr = new IPv4Address("192.168.1.1");
        var packed = addr.Packed;
        packed.Length.Should().Be(4);
        packed[0].Should().Be(192);
        packed[1].Should().Be(168);
        packed[2].Should().Be(1);
        packed[3].Should().Be(1);
    }

    [Fact]
    public void IPv4Address_ToInt()
    {
        new IPv4Address("192.168.1.1").ToInt().Should().Be(3232235777L);
    }

    [Fact]
    public void IPv4Address_Arithmetic()
    {
        var addr = new IPv4Address("192.168.1.1");
        (addr + 1).ToString().Should().Be("192.168.1.2");
        (addr - 1).ToString().Should().Be("192.168.1.0");
    }

    [Fact]
    public void IPv4Address_Comparison()
    {
        var a = new IPv4Address("1.0.0.0");
        var b = new IPv4Address("2.0.0.0");
        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
    }

    [Fact]
    public void IPv4Address_Equality()
    {
        var a = new IPv4Address("1.1.1.1");
        var b = new IPv4Address("1.1.1.1");
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void IPv4Address_OverflowThrows()
    {
        var addr = new IPv4Address("255.255.255.255");
        var act = () => addr + 1;
        act.Should().Throw<ValueError>();
    }

    // IPv6Address tests

    [Fact]
    public void IPv6Address_FromString()
    {
        var addr = new IPv6Address("::1");
        addr.Version.Should().Be(6);
        addr.IsLoopback.Should().BeTrue();
    }

    [Fact]
    public void IPv6Address_Compressed()
    {
        var addr = new IPv6Address("2001:db8::1");
        addr.Compressed.Should().Contain("2001:db8::1");
    }

    [Fact]
    public void IPv6Address_Exploded()
    {
        var addr = new IPv6Address("::1");
        addr.Exploded.Should().Be("0000:0000:0000:0000:0000:0000:0000:0001");
    }

    [Fact]
    public void IPv6Address_IsLinkLocal()
    {
        new IPv6Address("fe80::1").IsLinkLocal.Should().BeTrue();
        new IPv6Address("2001:db8::1").IsLinkLocal.Should().BeFalse();
    }

    [Fact]
    public void IPv6Address_IsMulticast()
    {
        new IPv6Address("ff02::1").IsMulticast.Should().BeTrue();
    }

    [Fact]
    public void IPv6Address_Ipv4Mapped()
    {
        var addr = new IPv6Address("::ffff:192.168.1.1");
        addr.Ipv4Mapped.Should().NotBeNull();
        addr.Ipv4Mapped!.ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IPv6Address_Ipv4Mapped_Null()
    {
        var addr = new IPv6Address("2001:db8::1");
        addr.Ipv4Mapped.Should().BeNull();
    }

    [Fact]
    public void IPv6Address_Arithmetic()
    {
        var addr = new IPv6Address("::1");
        (addr + 1).ToString().Should().Contain("::2");
    }

    [Fact]
    public void IPv6Address_Equality()
    {
        var a = new IPv6Address("::1");
        var b = new IPv6Address("::1");
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void IPv6Address_FromBigInteger()
    {
        var addr = new IPv6Address(BigInteger.One);
        addr.ToString().Should().Contain("::1");
    }

    // IPv4Network tests

    [Fact]
    public void IPv4Network_Basic()
    {
        var net = new IPv4Network("192.168.1.0/24");
        net.NetworkAddress.ToString().Should().Be("192.168.1.0");
        net.BroadcastAddress.ToString().Should().Be("192.168.1.255");
        net.Prefixlen.Should().Be(24);
        net.Netmask.ToString().Should().Be("255.255.255.0");
    }

    [Fact]
    public void IPv4Network_NumAddresses()
    {
        new IPv4Network("192.168.1.0/24").NumAddresses.Should().Be(256);
        new IPv4Network("192.168.1.0/32").NumAddresses.Should().Be(1);
        new IPv4Network("0.0.0.0/0").NumAddresses.Should().Be(4294967296L);
    }

    [Fact]
    public void IPv4Network_Hosts_24()
    {
        var net = new IPv4Network("192.168.1.0/24");
        var hosts = net.Hosts().ToList();
        hosts.Count.Should().Be(254);
        hosts.First().ToString().Should().Be("192.168.1.1");
        hosts.Last().ToString().Should().Be("192.168.1.254");
    }

    [Fact]
    public void IPv4Network_Hosts_31()
    {
        var net = new IPv4Network("192.168.1.0/31");
        var hosts = net.Hosts().ToList();
        hosts.Count.Should().Be(2);
    }

    [Fact]
    public void IPv4Network_Hosts_32()
    {
        var net = new IPv4Network("192.168.1.1/32");
        var hosts = net.Hosts().ToList();
        hosts.Count.Should().Be(1);
        hosts[0].ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IPv4Network_Contains()
    {
        var net = new IPv4Network("192.168.1.0/24");
        net.Contains(new IPv4Address("192.168.1.5")).Should().BeTrue();
        net.Contains(new IPv4Address("10.0.0.1")).Should().BeFalse();
    }

    [Fact]
    public void IPv4Network_Overlaps()
    {
        var a = new IPv4Network("192.168.1.0/24");
        var b = new IPv4Network("192.168.1.128/25");
        a.Overlaps(b).Should().BeTrue();
    }

    [Fact]
    public void IPv4Network_Subnets()
    {
        var net = new IPv4Network("192.168.1.0/24");
        var subnets = net.Subnets();
        subnets.Should().HaveCount(2);
        subnets[0].ToString().Should().Be("192.168.1.0/25");
        subnets[1].ToString().Should().Be("192.168.1.128/25");
    }

    [Fact]
    public void IPv4Network_Supernet()
    {
        var net = new IPv4Network("192.168.1.0/24");
        var super = net.Supernet();
        super.ToString().Should().Be("192.168.0.0/23");
    }

    [Fact]
    public void IPv4Network_SubnetOf()
    {
        var small = new IPv4Network("192.168.1.0/25");
        var big = new IPv4Network("192.168.1.0/24");
        small.SubnetOf(big).Should().BeTrue();
        big.SubnetOf(small).Should().BeFalse();
    }

    [Fact]
    public void IPv4Network_Strict_HostBitsSet()
    {
        var act = () => new IPv4Network("192.168.1.1/24");
        act.Should().Throw<ValueError>().WithMessage("*host bits set*");
    }

    [Fact]
    public void IPv4Network_NotStrict_MasksHostBits()
    {
        var net = new IPv4Network("192.168.1.1/24", strict: false);
        net.NetworkAddress.ToString().Should().Be("192.168.1.0");
    }

    [Fact]
    public void IPv4Network_SingleAddress()
    {
        var net = new IPv4Network("192.168.1.1");
        net.Prefixlen.Should().Be(32);
        net.NetworkAddress.ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IPv4Network_WithPrefixlen()
    {
        var net = new IPv4Network("192.168.1.0/24");
        net.WithPrefixlen.Should().Be("192.168.1.0/24");
    }

    [Fact]
    public void IPv4Network_Iteration()
    {
        var net = new IPv4Network("192.168.1.0/30");
        var all = net.ToList();
        all.Count.Should().Be(4);
        all[0].ToString().Should().Be("192.168.1.0");
        all[3].ToString().Should().Be("192.168.1.3");
    }

    // IPv6Network tests

    [Fact]
    public void IPv6Network_Basic()
    {
        var net = new IPv6Network("2001:db8::/32");
        net.Prefixlen.Should().Be(32);
        net.Version.Should().Be(6);
    }

    [Fact]
    public void IPv6Network_Contains()
    {
        var net = new IPv6Network("2001:db8::/32");
        net.Contains(new IPv6Address("2001:db8::1")).Should().BeTrue();
        net.Contains(new IPv6Address("2001:db9::1")).Should().BeFalse();
    }

    [Fact]
    public void IPv6Network_NumAddresses()
    {
        new IPv6Network("::1/128").NumAddresses.Should().Be(1);
        new IPv6Network("::/0").NumAddresses.Should().Be(BigInteger.One << 128);
    }

    // Interface tests

    [Fact]
    public void IPv4Interface_Basic()
    {
        var iface = new IPv4Interface("192.168.1.1/24");
        iface.Ip.ToString().Should().Be("192.168.1.1");
        iface.Network.NetworkAddress.ToString().Should().Be("192.168.1.0");
        iface.Network.Prefixlen.Should().Be(24);
        iface.ToString().Should().Be("192.168.1.1/24");
    }

    [Fact]
    public void IPv6Interface_Basic()
    {
        var iface = new IPv6Interface("2001:db8::1/32");
        iface.Ip.ToString().Should().Contain("2001:db8::1");
        iface.Network.Prefixlen.Should().Be(32);
    }

    // Factory function tests

    [Fact]
    public void IpAddress_IPv4()
    {
        var result = IpaddressModule.IpAddress("192.168.1.1");
        result.Should().BeOfType<IPv4Address>();
        result.ToString().Should().Be("192.168.1.1");
    }

    [Fact]
    public void IpAddress_IPv6()
    {
        var result = IpaddressModule.IpAddress("::1");
        result.Should().BeOfType<IPv6Address>();
    }

    [Fact]
    public void IpAddress_Invalid()
    {
        var act = () => IpaddressModule.IpAddress("invalid");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void IpNetwork_IPv4()
    {
        var result = IpaddressModule.IpNetwork("192.168.1.0/24");
        result.Should().BeOfType<IPv4Network>();
    }

    [Fact]
    public void IpInterface_IPv4()
    {
        var result = IpaddressModule.IpInterface("192.168.1.1/24");
        result.Should().BeOfType<IPv4Interface>();
    }

    [Fact]
    public void CollapseAddresses_MergesAdjacentNetworks()
    {
        var networks = new Sharpy.List<object>
        {
            new IPv4Network("192.168.1.0/25"),
            new IPv4Network("192.168.1.128/25")
        };

        var result = IpaddressModule.CollapseAddresses(networks);
        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("192.168.1.0/24");
    }

    [Fact]
    public void SummarizeAddressRange_SingleNetwork()
    {
        var result = IpaddressModule.SummarizeAddressRange(
            new IPv4Address("192.168.1.0"),
            new IPv4Address("192.168.1.255"));

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("192.168.1.0/24");
    }

    // Regression: /0 network host-mask arithmetic. C# masks uint shift counts mod 32,
    // so the broadcast/hostmask computation must explicitly guard the /0 case.
    [Fact]
    public void IPv4Network_ZeroPrefix_BroadcastAndHostmask()
    {
        var net = new IPv4Network("0.0.0.0/0");
        net.BroadcastAddress.ToString().Should().Be("255.255.255.255");
        net.Hostmask.ToString().Should().Be("255.255.255.255");
        net.Netmask.ToString().Should().Be("0.0.0.0");
        net.NumAddresses.Should().Be(4294967296L);
    }

    // Regression: Subnets() used `int` count, which overflows for large prefix diffs from /0.
    [Fact]
    public void IPv4Network_ZeroPrefix_SubnetsDoesNotOverflow()
    {
        var net = new IPv4Network("0.0.0.0/0");
        net.Subnets(prefixlenDiff: 1).Should().HaveCount(2);
    }

    // Regression: 255.255.255.255 is in 240.0.0.0/4 and is reserved (not global) in Python.
    [Fact]
    public void IPv4Address_Broadcast_IsReservedNotGlobal()
    {
        var addr = new IPv4Address("255.255.255.255");
        addr.IsReserved.Should().BeTrue();
        addr.IsGlobal.Should().BeFalse();
    }

    // IPv6Network parity with IPv4Network (plan task 14: "same API surface").
    [Fact]
    public void IPv6Network_Hosts_ExcludesAnycastFirstAddress()
    {
        var net = new IPv6Network("2001:db8::/120");
        net.Hosts().Count().Should().Be(255);
    }

    [Fact]
    public void IPv6Network_Hosts_SingleAndPointToPoint()
    {
        new IPv6Network("2001:db8::/128").Hosts().Count().Should().Be(1);
        new IPv6Network("2001:db8::/127").Hosts().Count().Should().Be(2);
    }

    [Fact]
    public void IPv6Network_Subnets()
    {
        var subnets = new IPv6Network("2001:db8::/126").Subnets();
        subnets.Should().HaveCount(2);
        subnets[0].ToString().Should().Be("2001:db8::/127");
        subnets[1].ToString().Should().Be("2001:db8::2/127");
    }

    [Fact]
    public void IPv6Network_Supernet()
    {
        new IPv6Network("2001:db8::/120").Supernet().ToString().Should().Be("2001:db8::/119");
    }

    [Fact]
    public void IPv6Network_Hostmask_And_WithNetmask()
    {
        var net = new IPv6Network("2001:db8::/120");
        net.WithNetmask.Should().Be("2001:db8::/ffff:ffff:ffff:ffff:ffff:ffff:ffff:ff00");
        net.WithPrefixlen.Should().Be("2001:db8::/120");
    }

    [Fact]
    public void IPv6Network_Iteration()
    {
        var net = new IPv6Network("2001:db8::/126");
        net.Count().Should().Be(4);
    }

    [Fact]
    public void IPv6Network_ClassificationProperties()
    {
        new IPv6Network("fc00::/7").IsPrivate.Should().BeTrue();
        new IPv6Network("2001:db8::/32").IsGlobal.Should().BeFalse();
    }

    // Regression: IPv6 range summarization was a NotImplementedException stub.
    [Fact]
    public void SummarizeAddressRange_IPv6()
    {
        var result = IpaddressModule.SummarizeAddressRange(
            new IPv6Address("2001:db8::"),
            new IPv6Address("2001:db8::ff"));

        result.Should().HaveCount(1);
        result[0].ToString().Should().Be("2001:db8::/120");
    }

    // CPython 3.12.13 classification parity tests (#791)

    [Theory]
    [InlineData("0.0.0.1", true, false, false)]
    [InlineData("10.5.5.5", true, false, false)]
    [InlineData("100.64.1.1", false, false, false)]
    [InlineData("169.254.1.1", true, false, false)]
    [InlineData("172.16.5.5", true, false, false)]
    [InlineData("192.0.0.1", true, false, false)]
    [InlineData("192.0.0.10", false, true, false)]
    [InlineData("192.0.2.5", true, false, false)]
    [InlineData("192.88.99.1", false, true, false)]
    [InlineData("192.168.1.1", true, false, false)]
    [InlineData("198.18.5.5", true, false, false)]
    [InlineData("198.51.100.5", true, false, false)]
    [InlineData("203.0.113.5", true, false, false)]
    [InlineData("240.0.0.1", true, false, true)]
    [InlineData("255.255.255.255", true, false, true)]
    [InlineData("8.8.8.8", false, true, false)]
    public void IPv4Address_Classification_MatchesCPython312(string addr, bool isPrivate, bool isGlobal, bool isReserved)
    {
        var a = new IPv4Address(addr);
        Assert.Equal(isPrivate, a.IsPrivate);
        Assert.Equal(isGlobal, a.IsGlobal);
        Assert.Equal(isReserved, a.IsReserved);
    }

    [Theory]
    [InlineData("10.0.0.0/7", false, true, false)]
    [InlineData("10.0.0.0/8", true, false, false)]
    [InlineData("100.64.0.0/10", false, false, false)]
    [InlineData("172.16.0.0/12", true, false, false)]
    [InlineData("192.0.2.0/24", true, false, false)]
    [InlineData("198.18.0.0/15", true, false, false)]
    [InlineData("203.0.113.0/24", true, false, false)]
    [InlineData("8.8.8.0/24", false, true, false)]
    [InlineData("0.0.0.0/0", false, true, false)]
    [InlineData("240.0.0.0/4", true, false, true)]
    public void IPv4Network_Classification_MatchesCPython312(string net, bool isPrivate, bool isGlobal, bool isReserved)
    {
        var n = new IPv4Network(net);
        Assert.Equal(isPrivate, n.IsPrivate);
        Assert.Equal(isGlobal, n.IsGlobal);
        Assert.Equal(isReserved, n.IsReserved);
    }

    [Theory]
    [InlineData("::1", true, false)]
    [InlineData("fe80::1", true, false)]
    [InlineData("fc00::1", true, false)]
    [InlineData("2001:db8::1", true, false)]
    [InlineData("2002::1", true, false)]
    [InlineData("2001:1::1", false, true)]
    [InlineData("64:ff9b:1::1", true, false)]
    [InlineData("2606:4700::1", false, true)]
    [InlineData("100::1", true, false)]
    [InlineData("3fff::1", true, false)]
    public void IPv6Address_Classification_MatchesCPython312(string addr, bool isPrivate, bool isGlobal)
    {
        var a = new IPv6Address(addr);
        Assert.Equal(isPrivate, a.IsPrivate);
        Assert.Equal(isGlobal, a.IsGlobal);
    }

    [Theory]
    [InlineData("fc00::/7", true, false)]
    [InlineData("2001:db8::/32", true, false)]
    [InlineData("::1/128", true, false)]
    [InlineData("fe80::/10", true, false)]
    [InlineData("2001::/23", true, false)]
    [InlineData("2606:4700::/32", false, true)]
    [InlineData("100::/64", true, false)]
    [InlineData("::/0", false, true)]
    [InlineData("2002::/16", true, false)]
    [InlineData("3fff::/20", true, false)]
    [InlineData("64:ff9b:1::/48", true, false)]
    [InlineData("2001::/16", false, true)]
    [InlineData("2001:1::/32", true, false)]
    public void IPv6Network_Classification_MatchesCPython312(string net, bool isPrivate, bool isGlobal)
    {
        var n = new IPv6Network(net);
        Assert.Equal(isPrivate, n.IsPrivate);
        Assert.Equal(isGlobal, n.IsGlobal);
    }
}

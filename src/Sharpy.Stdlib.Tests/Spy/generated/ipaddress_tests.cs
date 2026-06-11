// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using ipaddress = global::Sharpy.IpaddressModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Ipaddress.IpaddressTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Ipaddress
    {
        [global::Sharpy.SharpyModule("ipaddress.ipaddress_tests")]
        public static partial class IpaddressTests
        {
        }
    }

    public static partial class Ipaddress
    {
        public partial class IpaddressTestsTests
        {
            [Xunit.FactAttribute]
            public void TestIpv4AddressFromString()
            {
#line (11, 5) - (11, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address("192.168.1.1");
#line (12, 5) - (12, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(addr));
#line (13, 5) - (13, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4, addr.Version);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressFromInt()
            {
#line (17, 5) - (17, 69) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address(3232235777L);
#line (18, 5) - (18, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(addr));
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressInvalid()
            {
#line (24, 5) - (27, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (25, 9) - (25, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                    new global::Sharpy.IPv4Address("invalid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsPrivate()
            {
#line (29, 5) - (29, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("192.168.1.1").IsPrivate);
#line (30, 5) - (30, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("10.0.0.1").IsPrivate);
#line (31, 5) - (31, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("172.16.0.1").IsPrivate);
#line (32, 5) - (32, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsPrivate);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsLoopback()
            {
#line (36, 5) - (36, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("127.0.0.1").IsLoopback);
#line (37, 5) - (37, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("127.255.255.255").IsLoopback);
#line (38, 5) - (38, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsLoopback);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsMulticast()
            {
#line (42, 5) - (42, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("224.0.0.1").IsMulticast);
#line (43, 5) - (43, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("239.255.255.255").IsMulticast);
#line (44, 5) - (44, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsMulticast);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsReserved()
            {
#line (48, 5) - (48, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("240.0.0.1").IsReserved);
#line (49, 5) - (49, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsReserved);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsLinkLocal()
            {
#line (53, 5) - (53, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("169.254.1.1").IsLinkLocal);
#line (54, 5) - (54, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsLinkLocal);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsGlobal()
            {
#line (58, 5) - (58, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("8.8.8.8").IsGlobal);
#line (59, 5) - (59, 62) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("192.168.1.1").IsGlobal);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressIsUnspecified()
            {
#line (63, 5) - (63, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv4Address("0.0.0.0").IsUnspecified);
#line (64, 5) - (64, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv4Address("8.8.8.8").IsUnspecified);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressPacked()
            {
#line (68, 5) - (68, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address("192.168.1.1");
#line (69, 5) - (69, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.Bytes packed = addr.Packed;
#line (70, 5) - (70, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(packed));
#line (71, 5) - (71, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(192, packed[0]);
#line (72, 5) - (72, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(168, packed[1]);
#line (73, 5) - (73, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, packed[2]);
#line (74, 5) - (74, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, packed[3]);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressToInt()
            {
#line (78, 5) - (78, 71) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(3232235777L, new global::Sharpy.IPv4Address("192.168.1.1").ToInt());
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressArithmetic()
            {
#line (82, 5) - (82, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address("192.168.1.1");
#line (83, 5) - (83, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.2", global::Sharpy.Builtins.Str(addr + 1));
#line (84, 5) - (84, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0", global::Sharpy.Builtins.Str(addr - 1));
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressComparison()
            {
#line (88, 5) - (88, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address a = new global::Sharpy.IPv4Address("1.0.0.0");
#line (89, 5) - (89, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address b = new global::Sharpy.IPv4Address("2.0.0.0");
#line (90, 5) - (90, 18) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(a < b);
#line (91, 5) - (91, 18) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(b > a);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressEquality()
            {
#line (95, 5) - (95, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address a = new global::Sharpy.IPv4Address("1.1.1.1");
#line (96, 5) - (96, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address b = new global::Sharpy.IPv4Address("1.1.1.1");
#line (97, 5) - (97, 19) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(b, a);
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressOverflowThrows()
            {
#line (101, 5) - (101, 76) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address("255.255.255.255");
#line (102, 5) - (109, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (103, 9) - (103, 21) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                    var _ = addr + 1;
                }));
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressFromString()
            {
#line (111, 5) - (111, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("::1");
#line (112, 5) - (112, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(6, addr.Version);
#line (113, 5) - (113, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(addr.IsLoopback);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressCompressed()
            {
#line (117, 5) - (117, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("2001:db8::1");
#line (118, 5) - (118, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Contains("2001:db8::1", addr.Compressed);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressExploded()
            {
#line (122, 5) - (122, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("::1");
#line (123, 5) - (123, 71) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("0000:0000:0000:0000:0000:0000:0000:0001", addr.Exploded);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressIsLinkLocal()
            {
#line (127, 5) - (127, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv6Address("fe80::1").IsLinkLocal);
#line (128, 5) - (128, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv6Address("2001:db8::1").IsLinkLocal);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressIsMulticast()
            {
#line (132, 5) - (132, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv6Address("ff02::1").IsMulticast);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressIpv4Mapped()
            {
#line (136, 5) - (136, 79) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("::ffff:192.168.1.1");
#line (137, 5) - (137, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.NotNull(addr.Ipv4Mapped);
#line (138, 5) - (138, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(addr.Ipv4Mapped));
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressIpv4MappedNull()
            {
#line (142, 5) - (142, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("2001:db8::1");
#line (143, 5) - (143, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Null(addr.Ipv4Mapped);
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressArithmetic()
            {
#line (147, 5) - (147, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address addr = new global::Sharpy.IPv6Address("::1");
#line (148, 5) - (148, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Contains("::2", global::Sharpy.Builtins.Str(addr + 1));
            }

            [Xunit.FactAttribute]
            public void TestIpv6AddressEquality()
            {
#line (152, 5) - (152, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address a = new global::Sharpy.IPv6Address("::1");
#line (153, 5) - (153, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address b = new global::Sharpy.IPv6Address("::1");
#line (154, 5) - (154, 19) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(b, a);
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkBasic()
            {
#line (164, 5) - (164, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (165, 5) - (165, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0", global::Sharpy.Builtins.Str(net.NetworkAddress));
#line (166, 5) - (166, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.255", global::Sharpy.Builtins.Str(net.BroadcastAddress));
#line (167, 5) - (167, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(24, net.Prefixlen);
#line (168, 5) - (168, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("255.255.255.0", global::Sharpy.Builtins.Str(net.Netmask));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkNumAddresses()
            {
#line (172, 5) - (172, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(256, new global::Sharpy.IPv4Network("192.168.1.0/24").NumAddresses);
#line (173, 5) - (173, 70) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, new global::Sharpy.IPv4Network("192.168.1.0/32").NumAddresses);
#line (174, 5) - (174, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4294967296L, new global::Sharpy.IPv4Network("0.0.0.0/0").NumAddresses);
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkHosts24()
            {
#line (178, 5) - (178, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (179, 5) - (179, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv4Address> hosts = new global::Sharpy.List<global::Sharpy.IPv4Address>(net.Hosts());
#line (180, 5) - (180, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(254, global::Sharpy.Builtins.Len(hosts));
#line (181, 5) - (181, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(hosts[0]));
#line (182, 5) - (182, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.254", global::Sharpy.Builtins.Str(hosts[253]));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkHosts31()
            {
#line (186, 5) - (186, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/31");
#line (187, 5) - (187, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv4Address> hosts = new global::Sharpy.List<global::Sharpy.IPv4Address>(net.Hosts());
#line (188, 5) - (188, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(hosts));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkHosts32()
            {
#line (192, 5) - (192, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.1/32");
#line (193, 5) - (193, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv4Address> hosts = new global::Sharpy.List<global::Sharpy.IPv4Address>(net.Hosts());
#line (194, 5) - (194, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(hosts));
#line (195, 5) - (195, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(hosts[0]));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkContains()
            {
#line (199, 5) - (199, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (200, 5) - (200, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(net.Contains(new global::Sharpy.IPv4Address("192.168.1.5")));
#line (201, 5) - (201, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(net.Contains(new global::Sharpy.IPv4Address("10.0.0.1")));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkOverlaps()
            {
#line (205, 5) - (205, 72) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network a = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (206, 5) - (206, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network b = new global::Sharpy.IPv4Network("192.168.1.128/25");
#line (207, 5) - (207, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(a.Overlaps(b));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkSubnets()
            {
#line (211, 5) - (211, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (212, 5) - (212, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv4Network> subnets = net.Subnets();
#line (213, 5) - (213, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(subnets));
#line (214, 5) - (214, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0/25", global::Sharpy.Builtins.Str(subnets[0]));
#line (215, 5) - (215, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.128/25", global::Sharpy.Builtins.Str(subnets[1]));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkSupernet()
            {
#line (219, 5) - (219, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (220, 5) - (220, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network sup = net.Supernet();
#line (221, 5) - (221, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.0.0/23", global::Sharpy.Builtins.Str(sup));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkSubnetOf()
            {
#line (225, 5) - (225, 76) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network small = new global::Sharpy.IPv4Network("192.168.1.0/25");
#line (226, 5) - (226, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network big = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (227, 5) - (227, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(small.SubnetOf(big));
#line (228, 5) - (228, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(big.SubnetOf(small));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkStrictHostBitsSet()
            {
#line (232, 5) - (235, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (233, 9) - (233, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                    new global::Sharpy.IPv4Network("192.168.1.1/24");
                }));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkNotStrictMasksHostBits()
            {
#line (237, 5) - (237, 88) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.1/24", strict: false);
#line (238, 5) - (238, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0", global::Sharpy.Builtins.Str(net.NetworkAddress));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkSingleAddress()
            {
#line (242, 5) - (242, 71) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.1");
#line (243, 5) - (243, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(32, net.Prefixlen);
#line (244, 5) - (244, 53) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(net.NetworkAddress));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkWithPrefixlen()
            {
#line (248, 5) - (248, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/24");
#line (249, 5) - (249, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0/24", net.WithPrefixlen);
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkIteration()
            {
#line (253, 5) - (253, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("192.168.1.0/30");
#line (254, 5) - (254, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv4Address> allAddrs = new global::Sharpy.List<global::Sharpy.IPv4Address>(net);
#line (255, 5) - (255, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(allAddrs));
#line (256, 5) - (256, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0", global::Sharpy.Builtins.Str(allAddrs[0]));
#line (257, 5) - (257, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.3", global::Sharpy.Builtins.Str(allAddrs[3]));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkBasic()
            {
#line (265, 5) - (265, 73) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network net = new global::Sharpy.IPv6Network("2001:db8::/32");
#line (266, 5) - (266, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(32, net.Prefixlen);
#line (267, 5) - (267, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(6, net.Version);
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkContains()
            {
#line (271, 5) - (271, 73) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network net = new global::Sharpy.IPv6Network("2001:db8::/32");
#line (272, 5) - (272, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(net.Contains(new global::Sharpy.IPv6Address("2001:db8::1")));
#line (273, 5) - (273, 67) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(net.Contains(new global::Sharpy.IPv6Address("2001:db9::1")));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkNumAddresses128()
            {
#line (277, 5) - (277, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, new global::Sharpy.IPv6Network("::1/128").NumAddresses);
            }

            [Xunit.FactAttribute]
            public void TestIpv4InterfaceBasic()
            {
#line (287, 5) - (287, 80) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Interface iface = new global::Sharpy.IPv4Interface("192.168.1.1/24");
#line (288, 5) - (288, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(iface.Ip));
#line (289, 5) - (289, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0", global::Sharpy.Builtins.Str(iface.Network.NetworkAddress));
#line (290, 5) - (290, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(24, iface.Network.Prefixlen);
#line (291, 5) - (291, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1/24", global::Sharpy.Builtins.Str(iface));
            }

            [Xunit.FactAttribute]
            public void TestIpv6InterfaceBasic()
            {
#line (295, 5) - (295, 80) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Interface iface = new global::Sharpy.IPv6Interface("2001:db8::1/32");
#line (296, 5) - (296, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Contains("2001:db8::1", global::Sharpy.Builtins.Str(iface.Ip));
#line (297, 5) - (297, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(32, iface.Network.Prefixlen);
            }

            [Xunit.FactAttribute]
            public void TestIpAddressIpv4()
            {
#line (305, 5) - (305, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                object result = ipaddress.IpAddress("192.168.1.1");
#line (306, 5) - (306, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.1", global::Sharpy.Builtins.Str(result));
            }

            [Xunit.FactAttribute]
            public void TestIpAddressIpv6()
            {
#line (310, 5) - (310, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                object result = ipaddress.IpAddress("::1");
#line (311, 5) - (311, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.NotNull(global::Sharpy.Builtins.Str(result));
            }

            [Xunit.FactAttribute]
            public void TestIpAddressInvalid()
            {
#line (315, 5) - (318, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (316, 9) - (316, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                    ipaddress.IpAddress("invalid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestIpNetworkIpv4()
            {
#line (320, 5) - (320, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                object result = ipaddress.IpNetwork("192.168.1.0/24");
#line (321, 5) - (321, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.NotNull(global::Sharpy.Builtins.Str(result));
            }

            [Xunit.FactAttribute]
            public void TestIpInterfaceIpv4()
            {
#line (325, 5) - (325, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                object result = ipaddress.IpInterface("192.168.1.1/24");
#line (326, 5) - (326, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.NotNull(global::Sharpy.Builtins.Str(result));
            }

            [Xunit.FactAttribute]
            public void TestCollapseAddressesMergesAdjacentNetworks()
            {
#line (330, 5) - (330, 115) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<object> networks = new Sharpy.List<object>()
                {
                    new global::Sharpy.IPv4Network("192.168.1.0/25"),
                    new global::Sharpy.IPv4Network("192.168.1.128/25")
                };
#line (331, 5) - (331, 67) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<object> result = ipaddress.CollapseAddresses(networks);
#line (332, 5) - (332, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (333, 5) - (333, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0/24", global::Sharpy.Builtins.Str(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestSummarizeAddressRangeSingleNetwork()
            {
#line (337, 5) - (339, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<object> result = ipaddress.SummarizeAddressRange(new global::Sharpy.IPv4Address("192.168.1.0"), new global::Sharpy.IPv4Address("192.168.1.255"));
#line (340, 5) - (340, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (341, 5) - (341, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("192.168.1.0/24", global::Sharpy.Builtins.Str(result[0]));
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkZeroPrefixBroadcastAndHostmask()
            {
#line (349, 5) - (349, 69) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("0.0.0.0/0");
#line (350, 5) - (350, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("255.255.255.255", global::Sharpy.Builtins.Str(net.BroadcastAddress));
#line (351, 5) - (351, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("255.255.255.255", global::Sharpy.Builtins.Str(net.Hostmask));
#line (352, 5) - (352, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("0.0.0.0", global::Sharpy.Builtins.Str(net.Netmask));
#line (353, 5) - (353, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4294967296L, net.NumAddresses);
            }

            [Xunit.FactAttribute]
            public void TestIpv4NetworkZeroPrefixSubnetsDoesNotOverflow()
            {
#line (357, 5) - (357, 69) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network net = new global::Sharpy.IPv4Network("0.0.0.0/0");
#line (358, 5) - (358, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(net.Subnets(prefixlenDiff: 1)));
            }

            [Xunit.FactAttribute]
            public void TestIpv4AddressBroadcastIsReservedNotGlobal()
            {
#line (362, 5) - (362, 76) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address addr = new global::Sharpy.IPv4Address("255.255.255.255");
#line (363, 5) - (363, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(addr.IsReserved);
#line (364, 5) - (364, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(addr.IsGlobal);
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkHostsExcludesAnycastFirstAddress()
            {
#line (368, 5) - (368, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network net = new global::Sharpy.IPv6Network("2001:db8::/120");
#line (369, 5) - (369, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv6Address> hosts = new global::Sharpy.List<global::Sharpy.IPv6Address>(net.Hosts());
#line (370, 5) - (370, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(255, global::Sharpy.Builtins.Len(hosts));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkHostsSingleAndPointToPoint()
            {
#line (374, 5) - (374, 100) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv6Address> hosts128 = new global::Sharpy.List<global::Sharpy.IPv6Address>(new global::Sharpy.IPv6Network("2001:db8::/128").Hosts());
#line (375, 5) - (375, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(hosts128));
#line (376, 5) - (376, 100) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv6Address> hosts127 = new global::Sharpy.List<global::Sharpy.IPv6Address>(new global::Sharpy.IPv6Network("2001:db8::/127").Hosts());
#line (377, 5) - (377, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(hosts127));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkSubnets()
            {
#line (381, 5) - (381, 94) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv6Network> subnets = new global::Sharpy.IPv6Network("2001:db8::/126").Subnets();
#line (382, 5) - (382, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(subnets));
#line (383, 5) - (383, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::/127", global::Sharpy.Builtins.Str(subnets[0]));
#line (384, 5) - (384, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::2/127", global::Sharpy.Builtins.Str(subnets[1]));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkSupernet()
            {
#line (388, 5) - (388, 88) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::/119", global::Sharpy.Builtins.Str(new global::Sharpy.IPv6Network("2001:db8::/120").Supernet()));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkHostmaskAndWithNetmask()
            {
#line (392, 5) - (392, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network net = new global::Sharpy.IPv6Network("2001:db8::/120");
#line (393, 5) - (393, 84) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::/ffff:ffff:ffff:ffff:ffff:ffff:ffff:ff00", net.WithNetmask);
#line (394, 5) - (394, 50) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::/120", net.WithPrefixlen);
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkIteration()
            {
#line (398, 5) - (398, 74) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network net = new global::Sharpy.IPv6Network("2001:db8::/126");
#line (399, 5) - (399, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<global::Sharpy.IPv6Address> allAddrs = new global::Sharpy.List<global::Sharpy.IPv6Address>(net);
#line (400, 5) - (400, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(allAddrs));
            }

            [Xunit.FactAttribute]
            public void TestIpv6NetworkClassificationProperties()
            {
#line (404, 5) - (404, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.True(new global::Sharpy.IPv6Network("fc00::/7").IsPrivate);
#line (405, 5) - (405, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.False(new global::Sharpy.IPv6Network("2001:db8::/32").IsGlobal);
            }

            [Xunit.FactAttribute]
            public void TestSummarizeAddressRangeIpv6()
            {
#line (409, 5) - (411, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Sharpy.List<object> result = ipaddress.SummarizeAddressRange(new global::Sharpy.IPv6Address("2001:db8::"), new global::Sharpy.IPv6Address("2001:db8::ff"));
#line (412, 5) - (412, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(result));
#line (413, 5) - (413, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal("2001:db8::/120", global::Sharpy.Builtins.Str(result[0]));
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("0.0.0.1", true, false, false)]
            [Xunit.InlineDataAttribute("10.5.5.5", true, false, false)]
            [Xunit.InlineDataAttribute("100.64.1.1", false, false, false)]
            [Xunit.InlineDataAttribute("169.254.1.1", true, false, false)]
            [Xunit.InlineDataAttribute("172.16.5.5", true, false, false)]
            [Xunit.InlineDataAttribute("192.0.0.1", true, false, false)]
            [Xunit.InlineDataAttribute("192.0.0.10", false, true, false)]
            [Xunit.InlineDataAttribute("192.0.2.5", true, false, false)]
            [Xunit.InlineDataAttribute("192.88.99.1", false, true, false)]
            [Xunit.InlineDataAttribute("192.168.1.1", true, false, false)]
            [Xunit.InlineDataAttribute("198.18.5.5", true, false, false)]
            [Xunit.InlineDataAttribute("198.51.100.5", true, false, false)]
            [Xunit.InlineDataAttribute("203.0.113.5", true, false, false)]
            [Xunit.InlineDataAttribute("240.0.0.1", true, false, true)]
            [Xunit.InlineDataAttribute("255.255.255.255", true, false, true)]
            [Xunit.InlineDataAttribute("8.8.8.8", false, true, false)]
            public void TestIpv4AddressClassificationMatchesCpython312(string addr, bool isPrivate, bool isGlobal, bool isReserved)
            {
#line (439, 5) - (439, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Address a = new global::Sharpy.IPv4Address(addr);
#line (440, 5) - (440, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isPrivate, a.IsPrivate);
#line (441, 5) - (441, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isGlobal, a.IsGlobal);
#line (442, 5) - (442, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isReserved, a.IsReserved);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("10.0.0.0/7", false, true, false)]
            [Xunit.InlineDataAttribute("10.0.0.0/8", true, false, false)]
            [Xunit.InlineDataAttribute("100.64.0.0/10", false, false, false)]
            [Xunit.InlineDataAttribute("172.16.0.0/12", true, false, false)]
            [Xunit.InlineDataAttribute("192.0.2.0/24", true, false, false)]
            [Xunit.InlineDataAttribute("198.18.0.0/15", true, false, false)]
            [Xunit.InlineDataAttribute("203.0.113.0/24", true, false, false)]
            [Xunit.InlineDataAttribute("8.8.8.0/24", false, true, false)]
            [Xunit.InlineDataAttribute("0.0.0.0/0", false, true, false)]
            [Xunit.InlineDataAttribute("240.0.0.0/4", true, false, true)]
            public void TestIpv4NetworkClassificationMatchesCpython312(string net, bool isPrivate, bool isGlobal, bool isReserved)
            {
#line (458, 5) - (458, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv4Network n = new global::Sharpy.IPv4Network(net);
#line (459, 5) - (459, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isPrivate, n.IsPrivate);
#line (460, 5) - (460, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isGlobal, n.IsGlobal);
#line (461, 5) - (461, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isReserved, n.IsReserved);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("::1", true, false)]
            [Xunit.InlineDataAttribute("fe80::1", true, false)]
            [Xunit.InlineDataAttribute("fc00::1", true, false)]
            [Xunit.InlineDataAttribute("2001:db8::1", true, false)]
            [Xunit.InlineDataAttribute("2002::1", true, false)]
            [Xunit.InlineDataAttribute("2001:1::1", false, true)]
            [Xunit.InlineDataAttribute("64:ff9b:1::1", true, false)]
            [Xunit.InlineDataAttribute("2606:4700::1", false, true)]
            [Xunit.InlineDataAttribute("100::1", true, false)]
            [Xunit.InlineDataAttribute("3fff::1", true, false)]
            public void TestIpv6AddressClassificationMatchesCpython312(string addr, bool isPrivate, bool isGlobal)
            {
#line (477, 5) - (477, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Address a = new global::Sharpy.IPv6Address(addr);
#line (478, 5) - (478, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isPrivate, a.IsPrivate);
#line (479, 5) - (479, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isGlobal, a.IsGlobal);
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute("fc00::/7", true, false)]
            [Xunit.InlineDataAttribute("2001:db8::/32", true, false)]
            [Xunit.InlineDataAttribute("::1/128", true, false)]
            [Xunit.InlineDataAttribute("fe80::/10", true, false)]
            [Xunit.InlineDataAttribute("2001::/23", true, false)]
            [Xunit.InlineDataAttribute("2606:4700::/32", false, true)]
            [Xunit.InlineDataAttribute("100::/64", true, false)]
            [Xunit.InlineDataAttribute("::/0", false, true)]
            [Xunit.InlineDataAttribute("2002::/16", true, false)]
            [Xunit.InlineDataAttribute("3fff::/20", true, false)]
            [Xunit.InlineDataAttribute("64:ff9b:1::/48", true, false)]
            [Xunit.InlineDataAttribute("2001::/16", false, true)]
            [Xunit.InlineDataAttribute("2001:1::/32", true, false)]
            public void TestIpv6NetworkClassificationMatchesCpython312(string net, bool isPrivate, bool isGlobal)
            {
#line (498, 5) - (498, 59) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                global::Sharpy.IPv6Network n = new global::Sharpy.IPv6Network(net);
#line (499, 5) - (499, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isPrivate, n.IsPrivate);
#line (500, 5) - (500, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/ipaddress/ipaddress_tests.spy"
                Xunit.Assert.Equal(isGlobal, n.IsGlobal);
            }
        }
    }
}

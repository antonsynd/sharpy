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
using socket = global::Sharpy.SocketModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Socket
    {
        [global::Sharpy.SharpyModule("socket.socket_module_tests")]
        public static partial class SocketModuleTests
        {
        }
    }

    public static partial class Socket
    {
        public partial class SocketModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestAfInetHasCorrectValue()
            {
#line (50, 5) - (50, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.AF_INET);
            }

            [Xunit.FactAttribute]
            public void TestAfInet6HasCorrectValue()
            {
#line (54, 5) - (54, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(socket.AF_INET6 == 23 || socket.AF_INET6 == 10 || socket.AF_INET6 == 30);
            }

            [Xunit.FactAttribute]
            public void TestSockStreamHasCorrectValue()
            {
#line (58, 5) - (58, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(1, socket.SOCK_STREAM);
            }

            [Xunit.FactAttribute]
            public void TestSockDgramHasCorrectValue()
            {
#line (62, 5) - (62, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.SOCK_DGRAM);
            }

            [Xunit.FactAttribute]
            public void TestSockRawHasCorrectValue()
            {
#line (66, 5) - (66, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(3, socket.SOCK_RAW);
            }

            [Xunit.FactAttribute]
            public void TestIpprotoTcpHasCorrectValue()
            {
#line (70, 5) - (70, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(6, socket.IPPROTO_TCP);
            }

            [Xunit.FactAttribute]
            public void TestIpprotoUdpHasCorrectValue()
            {
#line (74, 5) - (74, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(17, socket.IPPROTO_UDP);
            }

            [Xunit.FactAttribute]
            public void TestShutRdHasCorrectValue()
            {
#line (78, 5) - (78, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(0, socket.SHUT_RD);
            }

            [Xunit.FactAttribute]
            public void TestShutWrHasCorrectValue()
            {
#line (82, 5) - (82, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(1, socket.SHUT_WR);
            }

            [Xunit.FactAttribute]
            public void TestShutRdwrHasCorrectValue()
            {
#line (86, 5) - (86, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.SHUT_RDWR);
            }

            [Xunit.FactAttribute]
            public void TestTcpNodelayHasCorrectValue()
            {
#line (90, 5) - (90, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.TCP_NODELAY);
            }

            [Xunit.FactAttribute]
            public void TestSoRcvbufHasCorrectValue()
            {
#line (94, 5) - (94, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.SO_RCVBUF);
            }

            [Xunit.FactAttribute]
            public void TestSoSndbufHasCorrectValue()
            {
#line (98, 5) - (98, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.SO_SNDBUF);
            }

            [Xunit.FactAttribute]
            public void TestSomaxconnIs128()
            {
#line (102, 5) - (102, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(128, socket.SOMAXCONN);
            }

            [Xunit.FactAttribute]
            public void TestSocketCreatesStreamSocket()
            {
#line (111, 5) - (111, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (112, 5) - (112, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (113, 5) - (113, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSocketCreatesDatagramSocket()
            {
#line (117, 5) - (117, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (118, 5) - (118, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (119, 5) - (119, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSocketDefaultParamsCreatesIpv4Stream()
            {
#line (123, 5) - (123, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket();
#line (124, 5) - (124, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (125, 5) - (125, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestTcpServerBindListenAcceptWorks()
            {
#line (131, 5) - (131, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (132, 5) - (132, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (133, 5) - (133, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (134, 5) - (134, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(5);
#line (136, 5) - (136, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var serverAddr = server.Getsockname();
#line (137, 5) - (137, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", serverAddr.Item1);
#line (138, 5) - (138, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(serverAddr.Item2 > 0);
#line (140, 5) - (140, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (141, 5) - (141, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", serverAddr.Item2));
#line (143, 5) - (143, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, addr) = server.Accept();
#line (144, 5) - (144, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (145, 5) - (145, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(addr.Item2 > 0);
#line (147, 5) - (147, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (148, 5) - (148, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (149, 5) - (149, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestTcpSendRecvWorks()
            {
#line (153, 5) - (153, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (154, 5) - (154, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (155, 5) - (155, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (156, 5) - (156, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (157, 5) - (157, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (159, 5) - (159, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (160, 5) - (160, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", port));
#line (162, 5) - (162, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (164, 5) - (164, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 });
#line (165, 5) - (165, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Sendall(message);
#line (167, 5) - (167, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes received = conn.Recv(1024);
#line (168, 5) - (168, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 }), received);
#line (170, 5) - (170, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (171, 5) - (171, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (172, 5) - (172, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestUdpSendtoRecvfromWorks()
            {
#line (178, 5) - (178, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var receiver = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (179, 5) - (179, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                receiver.Bind(("127.0.0.1", 0));
#line (180, 5) - (180, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = receiver.Getsockname().Item2;
#line (182, 5) - (182, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var sender = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (183, 5) - (183, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 });
#line (184, 5) - (184, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                sender.Sendto(message, ("127.0.0.1", port));
#line (186, 5) - (186, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (data, addr) = receiver.Recvfrom(1024);
#line (187, 5) - (187, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 }), data);
#line (188, 5) - (188, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (190, 5) - (190, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                sender.Close();
#line (191, 5) - (191, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                receiver.Close();
            }

            [Xunit.FactAttribute]
            public void TestGethostbynameLocalhostReturnsLoopback()
            {
#line (197, 5) - (197, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = socket.Gethostbyname("localhost");
#line (198, 5) - (198, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(ip == "127.0.0.1" || ip == "::1");
            }

            [Xunit.FactAttribute]
            public void TestGethostnameReturnsNonEmpty()
            {
#line (202, 5) - (202, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string hostname = socket.Gethostname();
#line (203, 5) - (203, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual("", hostname);
            }

            [Xunit.FactAttribute]
            public void TestGethostbynameInvalidHostRaisesGaierror()
            {
#line (207, 5) - (212, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.SocketModule.Gaierror>((global::System.Action)(() =>
                {
#line (208, 9) - (208, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Gethostbyname("this.host.does.not.exist.invalid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSetsockoptGetsockoptReuseAddr()
            {
#line (214, 5) - (214, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (215, 5) - (215, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (216, 5) - (216, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int val = s.Getsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR);
#line (217, 5) - (217, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, val);
#line (218, 5) - (218, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSettimeoutGettimeoutWorks()
            {
#line (224, 5) - (224, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (225, 5) - (225, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(5.0d);
#line (226, 5) - (226, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var t = s.Gettimeout();
#line (227, 5) - (227, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(t);
#line (228, 5) - (231, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (t != null)
                {
#line (229, 9) - (229, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(5.0d, t.Value);
                }

#line (231, 5) - (231, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(null);
#line (232, 5) - (232, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(s.Gettimeout());
#line (233, 5) - (233, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetdefaulttimeoutReturnsNullByDefault()
            {
#line (239, 5) - (239, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line (240, 5) - (240, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(socket.Getdefaulttimeout());
            }

            [Xunit.FactAttribute]
            public void TestSetdefaulttimeoutSetsAndGets()
            {
#line (244, 5) - (244, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(10.0d);
#line (245, 5) - (245, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var d = socket.Getdefaulttimeout();
#line (246, 5) - (246, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(d);
#line (247, 5) - (249, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (d != null)
                {
#line (248, 9) - (248, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(10.0d, d.Value);
                }

#line (249, 5) - (249, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
            }

            [Xunit.FactAttribute]
            public void TestSetdefaulttimeoutAppliedToNewSockets()
            {
#line (253, 5) - (253, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(3.0d);
#line (254, 5) - (254, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket();
#line (255, 5) - (255, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var t = s.Gettimeout();
#line (256, 5) - (256, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(t);
#line (257, 5) - (259, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (t != null)
                {
#line (258, 9) - (258, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(3.0d, t.Value);
                }

#line (259, 5) - (259, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
#line (260, 5) - (260, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
            }

            [Xunit.FactAttribute]
            public void TestHtonsNtohsRoundTrips()
            {
#line (266, 5) - (266, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int original = 8080;
#line (267, 5) - (267, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int network = socket.Htons(original);
#line (268, 5) - (268, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int host = socket.Ntohs(network);
#line (269, 5) - (269, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(original, host);
            }

            [Xunit.FactAttribute]
            public void TestHtonlNtohlRoundTrips()
            {
#line (273, 5) - (273, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int original = 305419896;
#line (274, 5) - (274, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int network = socket.Htonl(original);
#line (275, 5) - (275, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int host = socket.Ntohl(network);
#line (276, 5) - (276, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(original, host);
            }

            [Xunit.FactAttribute]
            public void TestInetAtonNtoaRoundTrips()
            {
#line (282, 5) - (282, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "192.168.1.1";
#line (283, 5) - (283, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_aton(ip);
#line (284, 5) - (284, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntoa(packed);
#line (285, 5) - (285, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestInetPtonNtopIpv4RoundTrips()
            {
#line (289, 5) - (289, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "10.0.0.1";
#line (290, 5) - (290, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_pton(socket.AF_INET, ip);
#line (291, 5) - (291, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(packed));
#line (292, 5) - (292, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntop(socket.AF_INET, packed);
#line (293, 5) - (293, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestInetPtonNtopIpv6RoundTrips()
            {
#line (297, 5) - (297, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "::1";
#line (298, 5) - (298, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_pton(socket.AF_INET6, ip);
#line (299, 5) - (299, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(packed));
#line (300, 5) - (300, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntop(socket.AF_INET6, packed);
#line (301, 5) - (301, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestSocketIpv6CanCreate()
            {
#line (307, 5) - (307, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET6, socket.SOCK_STREAM);
#line (308, 5) - (308, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET6, s.Family);
#line (309, 5) - (309, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestCreateConnectionConnectsToLocalServer()
            {
#line (344, 5) - (344, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (345, 5) - (345, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (346, 5) - (346, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (347, 5) - (347, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (348, 5) - (348, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (350, 5) - (350, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (352, 5) - (352, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var peer = client.Getpeername();
#line (353, 5) - (353, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (354, 5) - (354, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(port, peer.Item2);
#line (356, 5) - (356, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (357, 5) - (357, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (358, 5) - (358, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (359, 5) - (359, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestStrContainsSocketInfo()
            {
#line (371, 5) - (371, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (372, 5) - (372, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string text = global::Sharpy.Builtins.Str(s);
#line (373, 5) - (373, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("socket", text);
#line (374, 5) - (374, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("family=", text);
#line (375, 5) - (375, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("type=", text);
#line (376, 5) - (376, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetpeernameAfterConnectReturnsRemoteAddr()
            {
#line (382, 5) - (382, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (383, 5) - (383, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (384, 5) - (384, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (385, 5) - (385, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (386, 5) - (386, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (388, 5) - (388, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (389, 5) - (389, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", port));
#line (391, 5) - (391, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var peer = client.Getpeername();
#line (392, 5) - (392, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (393, 5) - (393, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(port, peer.Item2);
#line (395, 5) - (395, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (396, 5) - (396, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (397, 5) - (397, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (398, 5) - (398, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestSetblockingFalseSetsNonBlocking()
            {
#line (404, 5) - (404, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (405, 5) - (405, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(false);
#line (406, 5) - (406, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.False(s.Getblocking());
#line (407, 5) - (407, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSetblockingTrueSetsBlocking()
            {
#line (411, 5) - (411, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (412, 5) - (412, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(false);
#line (413, 5) - (413, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(true);
#line (414, 5) - (414, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(s.Getblocking());
#line (415, 5) - (415, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetnameinfoLocalhostReturnsHostAndService()
            {
#line (421, 5) - (421, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (422, 5) - (422, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual("", host);
#line (423, 5) - (423, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("80", service);
            }
        }
    }
}

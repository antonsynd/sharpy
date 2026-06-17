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
#line (45, 5) - (45, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.AF_INET);
            }

            [Xunit.FactAttribute]
            public void TestAfInet6HasCorrectValue()
            {
#line (49, 5) - (49, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(socket.AF_INET6 == 23 || socket.AF_INET6 == 10 || socket.AF_INET6 == 30);
            }

            [Xunit.FactAttribute]
            public void TestSockStreamHasCorrectValue()
            {
#line (53, 5) - (53, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(1, socket.SOCK_STREAM);
            }

            [Xunit.FactAttribute]
            public void TestSockDgramHasCorrectValue()
            {
#line (57, 5) - (57, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.SOCK_DGRAM);
            }

            [Xunit.FactAttribute]
            public void TestSockRawHasCorrectValue()
            {
#line (61, 5) - (61, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(3, socket.SOCK_RAW);
            }

            [Xunit.FactAttribute]
            public void TestIpprotoTcpHasCorrectValue()
            {
#line (65, 5) - (65, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(6, socket.IPPROTO_TCP);
            }

            [Xunit.FactAttribute]
            public void TestIpprotoUdpHasCorrectValue()
            {
#line (69, 5) - (69, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(17, socket.IPPROTO_UDP);
            }

            [Xunit.FactAttribute]
            public void TestShutRdHasCorrectValue()
            {
#line (73, 5) - (73, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(0, socket.SHUT_RD);
            }

            [Xunit.FactAttribute]
            public void TestShutWrHasCorrectValue()
            {
#line (77, 5) - (77, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(1, socket.SHUT_WR);
            }

            [Xunit.FactAttribute]
            public void TestShutRdwrHasCorrectValue()
            {
#line (81, 5) - (81, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2, socket.SHUT_RDWR);
            }

            [Xunit.FactAttribute]
            public void TestTcpNodelayHasCorrectValue()
            {
#line (85, 5) - (85, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.TCP_NODELAY);
            }

            [Xunit.FactAttribute]
            public void TestSoRcvbufHasCorrectValue()
            {
#line (89, 5) - (89, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.SO_RCVBUF);
            }

            [Xunit.FactAttribute]
            public void TestSoSndbufHasCorrectValue()
            {
#line (93, 5) - (93, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, socket.SO_SNDBUF);
            }

            [Xunit.FactAttribute]
            public void TestSomaxconnIs128()
            {
#line (97, 5) - (97, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(128, socket.SOMAXCONN);
            }

            [Xunit.FactAttribute]
            public void TestSocketCreatesStreamSocket()
            {
#line (106, 5) - (106, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (107, 5) - (107, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (108, 5) - (108, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSocketCreatesDatagramSocket()
            {
#line (112, 5) - (112, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (113, 5) - (113, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (114, 5) - (114, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSocketDefaultParamsCreatesIpv4Stream()
            {
#line (118, 5) - (118, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket();
#line (119, 5) - (119, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET, s.Family);
#line (120, 5) - (120, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestTcpServerBindListenAcceptWorks()
            {
#line (126, 5) - (126, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (127, 5) - (127, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (128, 5) - (128, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (129, 5) - (129, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(5);
#line (131, 5) - (131, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var serverAddr = server.Getsockname();
#line (132, 5) - (132, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", serverAddr.Item1);
#line (133, 5) - (133, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(serverAddr.Item2 > 0);
#line (135, 5) - (135, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (136, 5) - (136, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", serverAddr.Item2));
#line (138, 5) - (138, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, addr) = server.Accept();
#line (139, 5) - (139, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (140, 5) - (140, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(addr.Item2 > 0);
#line (142, 5) - (142, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (143, 5) - (143, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (144, 5) - (144, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestTcpSendRecvWorks()
            {
#line (148, 5) - (148, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (149, 5) - (149, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (150, 5) - (150, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (151, 5) - (151, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (152, 5) - (152, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (154, 5) - (154, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (155, 5) - (155, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", port));
#line (157, 5) - (157, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (159, 5) - (159, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 });
#line (160, 5) - (160, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Sendall(message);
#line (162, 5) - (162, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes received = conn.Recv(1024);
#line (163, 5) - (163, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 }), received);
#line (165, 5) - (165, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (166, 5) - (166, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (167, 5) - (167, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestUdpSendtoRecvfromWorks()
            {
#line (173, 5) - (173, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var receiver = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (174, 5) - (174, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                receiver.Bind(("127.0.0.1", 0));
#line (175, 5) - (175, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = receiver.Getsockname().Item2;
#line (177, 5) - (177, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var sender = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_DGRAM);
#line (178, 5) - (178, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 });
#line (179, 5) - (179, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                sender.Sendto(message, ("127.0.0.1", port));
#line (181, 5) - (181, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (data, addr) = receiver.Recvfrom(1024);
#line (182, 5) - (182, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 }), data);
#line (183, 5) - (183, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (185, 5) - (185, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                sender.Close();
#line (186, 5) - (186, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                receiver.Close();
            }

            [Xunit.FactAttribute]
            public void TestGethostbynameLocalhostReturnsLoopback()
            {
#line (192, 5) - (192, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = socket.Gethostbyname("localhost");
#line (193, 5) - (193, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(ip == "127.0.0.1" || ip == "::1");
            }

            [Xunit.FactAttribute]
            public void TestGethostnameReturnsNonEmpty()
            {
#line (197, 5) - (197, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string hostname = socket.Gethostname();
#line (198, 5) - (198, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual("", hostname);
            }

            [Xunit.FactAttribute]
            public void TestGethostbynameInvalidHostRaisesGaierror()
            {
#line (202, 5) - (207, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.SocketModule.Gaierror>((global::System.Action)(() =>
                {
#line (203, 9) - (203, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Gethostbyname("this.host.does.not.exist.invalid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestSetsockoptGetsockoptReuseAddr()
            {
#line (209, 5) - (209, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (210, 5) - (210, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (211, 5) - (211, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int val = s.Getsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR);
#line (212, 5) - (212, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual(0, val);
#line (213, 5) - (213, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSettimeoutGettimeoutWorks()
            {
#line (219, 5) - (219, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (220, 5) - (220, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(5.0d);
#line (221, 5) - (221, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var t = s.Gettimeout();
#line (222, 5) - (222, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(t);
#line (223, 5) - (226, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (t != null)
                {
#line (224, 9) - (224, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(5.0d, t.Value);
                }

#line (226, 5) - (226, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(null);
#line (227, 5) - (227, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(s.Gettimeout());
#line (228, 5) - (228, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetdefaulttimeoutReturnsNullByDefault()
            {
#line (234, 5) - (234, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line (235, 5) - (235, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(socket.Getdefaulttimeout());
            }

            [Xunit.FactAttribute]
            public void TestSetdefaulttimeoutSetsAndGets()
            {
#line (239, 5) - (239, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(10.0d);
#line (240, 5) - (240, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var d = socket.Getdefaulttimeout();
#line (241, 5) - (241, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(d);
#line (242, 5) - (244, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (d != null)
                {
#line (243, 9) - (243, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(10.0d, d.Value);
                }

#line (244, 5) - (244, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
            }

            [Xunit.FactAttribute]
            public void TestSetdefaulttimeoutAppliedToNewSockets()
            {
#line (248, 5) - (248, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(3.0d);
#line (249, 5) - (249, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket();
#line (250, 5) - (250, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var t = s.Gettimeout();
#line (251, 5) - (251, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotNull(t);
#line (252, 5) - (254, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (t != null)
                {
#line (253, 9) - (253, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    Xunit.Assert.Equal(3.0d, t.Value);
                }

#line (254, 5) - (254, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
#line (255, 5) - (255, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
            }

            [Xunit.FactAttribute]
            public void TestHtonsNtohsRoundTrips()
            {
#line (261, 5) - (261, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int original = 8080;
#line (262, 5) - (262, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int network = socket.Htons(original);
#line (263, 5) - (263, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int host = socket.Ntohs(network);
#line (264, 5) - (264, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(original, host);
            }

            [Xunit.FactAttribute]
            public void TestHtonlNtohlRoundTrips()
            {
#line (268, 5) - (268, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int original = 305419896;
#line (269, 5) - (269, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int network = socket.Htonl(original);
#line (270, 5) - (270, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int host = socket.Ntohl(network);
#line (271, 5) - (271, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(original, host);
            }

            [Xunit.FactAttribute]
            public void TestInetAtonNtoaRoundTrips()
            {
#line (277, 5) - (277, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "192.168.1.1";
#line (278, 5) - (278, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_aton(ip);
#line (279, 5) - (279, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntoa(packed);
#line (280, 5) - (280, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestInetPtonNtopIpv4RoundTrips()
            {
#line (284, 5) - (284, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "10.0.0.1";
#line (285, 5) - (285, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_pton(socket.AF_INET, ip);
#line (286, 5) - (286, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(packed));
#line (287, 5) - (287, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntop(socket.AF_INET, packed);
#line (288, 5) - (288, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestInetPtonNtopIpv6RoundTrips()
            {
#line (292, 5) - (292, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string ip = "::1";
#line (293, 5) - (293, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Sharpy.Bytes packed = socket.Inet_pton(socket.AF_INET6, ip);
#line (294, 5) - (294, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(packed));
#line (295, 5) - (295, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string result = socket.Inet_ntop(socket.AF_INET6, packed);
#line (296, 5) - (296, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(ip, result);
            }

            [Xunit.FactAttribute]
            public void TestSocketIpv6CanCreate()
            {
#line (302, 5) - (302, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET6, socket.SOCK_STREAM);
#line (303, 5) - (303, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(socket.AF_INET6, s.Family);
#line (304, 5) - (304, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestConnectInvalidAddressRaisesError()
            {
#line (315, 5) - (315, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (316, 5) - (316, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(1.0d);
#line (317, 5) - (317, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool caught = false;
#line (318, 5) - (322, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
                {
#line (319, 9) - (319, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    s.Connect(("192.0.2.1", 1));
                }
                catch (global::Sharpy.SocketModule.Error)
                {
#line (321, 9) - (321, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    caught = true;
                }

#line (322, 5) - (322, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(caught);
#line (323, 5) - (323, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestErrorHierarchy()
            {
#line (331, 5) - (331, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool timeoutCaught = false;
#line (332, 5) - (336, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
                {
#line (333, 9) - (333, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    throw new global::Sharpy.SocketModule.Timeout("timed out");
                }
                catch (global::Sharpy.SocketModule.Error)
                {
#line (335, 9) - (335, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    timeoutCaught = true;
                }

#line (336, 5) - (336, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(timeoutCaught);
#line (338, 5) - (338, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool gaierrorCaught = false;
#line (339, 5) - (343, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
                {
#line (340, 9) - (340, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    throw new global::Sharpy.SocketModule.Gaierror("name resolution failed");
                }
                catch (global::Sharpy.SocketModule.Error)
                {
#line (342, 9) - (342, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    gaierrorCaught = true;
                }

#line (343, 5) - (343, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(gaierrorCaught);
#line (345, 5) - (345, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool herrorCaught = false;
#line (346, 5) - (350, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
                {
#line (347, 9) - (347, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    throw new global::Sharpy.SocketModule.Herror("host error");
                }
                catch (global::Sharpy.SocketModule.Error)
                {
#line (349, 9) - (349, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    herrorCaught = true;
                }

#line (350, 5) - (350, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(herrorCaught);
            }

            [Xunit.FactAttribute]
            public void TestCreateConnectionConnectsToLocalServer()
            {
#line (356, 5) - (356, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (357, 5) - (357, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (358, 5) - (358, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (359, 5) - (359, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (360, 5) - (360, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (362, 5) - (362, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (364, 5) - (364, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var peer = client.Getpeername();
#line (365, 5) - (365, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (366, 5) - (366, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(port, peer.Item2);
#line (368, 5) - (368, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (369, 5) - (369, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (370, 5) - (370, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (371, 5) - (371, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestCreateConnectionInvalidAddressRaises()
            {
#line (378, 5) - (378, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool caught = false;
#line (379, 5) - (383, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
                {
#line (380, 9) - (380, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.CreateConnection(("192.0.2.1", 1), timeout: 1.0d);
                }
                catch (global::Sharpy.SocketModule.Error)
                {
#line (382, 9) - (382, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    caught = true;
                }

#line (383, 5) - (383, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(caught);
            }

            [Xunit.FactAttribute]
            public void TestStrContainsSocketInfo()
            {
#line (389, 5) - (389, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (390, 5) - (390, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                string text = global::Sharpy.Builtins.Str(s);
#line (391, 5) - (391, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("socket", text);
#line (392, 5) - (392, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("family=", text);
#line (393, 5) - (393, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Contains("type=", text);
#line (394, 5) - (394, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetpeernameAfterConnectReturnsRemoteAddr()
            {
#line (400, 5) - (400, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var server = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (401, 5) - (401, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1);
#line (402, 5) - (402, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Bind(("127.0.0.1", 0));
#line (403, 5) - (403, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Listen(1);
#line (404, 5) - (404, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var port = server.Getsockname().Item2;
#line (406, 5) - (406, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var client = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (407, 5) - (407, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Connect(("127.0.0.1", port));
#line (409, 5) - (409, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var peer = client.Getpeername();
#line (410, 5) - (410, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (411, 5) - (411, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(port, peer.Item2);
#line (413, 5) - (413, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (conn, _) = server.Accept();
#line (414, 5) - (414, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                conn.Close();
#line (415, 5) - (415, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Close();
#line (416, 5) - (416, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
            }

            [Xunit.FactAttribute]
            public void TestSetblockingFalseSetsNonBlocking()
            {
#line (422, 5) - (422, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (423, 5) - (423, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(false);
#line (424, 5) - (424, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.False(s.Getblocking());
#line (425, 5) - (425, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestSetblockingTrueSetsBlocking()
            {
#line (429, 5) - (429, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(socket.AF_INET, socket.SOCK_STREAM);
#line (430, 5) - (430, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(false);
#line (431, 5) - (431, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Setblocking(true);
#line (432, 5) - (432, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(s.Getblocking());
#line (433, 5) - (433, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
            }

            [Xunit.FactAttribute]
            public void TestGetnameinfoLocalhostReturnsHostAndService()
            {
#line (439, 5) - (439, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (440, 5) - (440, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.NotEqual("", host);
#line (441, 5) - (441, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal("80", service);
            }
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class SocketModuleTests
    {
        // ---- Constants Tests ----

        [Fact]
        public void AF_INET_HasCorrectValue()
        {
            SocketModule.AF_INET.Should().Be((int)AddressFamily.InterNetwork);
        }

        [Fact]
        public void AF_INET6_HasCorrectValue()
        {
            SocketModule.AF_INET6.Should().Be((int)AddressFamily.InterNetworkV6);
        }

        [Fact]
        public void SOCK_STREAM_HasCorrectValue()
        {
            SocketModule.SOCK_STREAM.Should().Be((int)SocketType.Stream);
        }

        [Fact]
        public void SOCK_DGRAM_HasCorrectValue()
        {
            SocketModule.SOCK_DGRAM.Should().Be((int)SocketType.Dgram);
        }

        [Fact]
        public void SOCK_RAW_HasCorrectValue()
        {
            SocketModule.SOCK_RAW.Should().Be((int)SocketType.Raw);
        }

        [Fact]
        public void IPPROTO_TCP_HasCorrectValue()
        {
            SocketModule.IPPROTO_TCP.Should().Be((int)ProtocolType.Tcp);
        }

        [Fact]
        public void IPPROTO_UDP_HasCorrectValue()
        {
            SocketModule.IPPROTO_UDP.Should().Be((int)ProtocolType.Udp);
        }

        [Fact]
        public void SHUT_RD_HasCorrectValue()
        {
            SocketModule.SHUT_RD.Should().Be((int)SocketShutdown.Receive);
        }

        [Fact]
        public void SHUT_WR_HasCorrectValue()
        {
            SocketModule.SHUT_WR.Should().Be((int)SocketShutdown.Send);
        }

        [Fact]
        public void SHUT_RDWR_HasCorrectValue()
        {
            SocketModule.SHUT_RDWR.Should().Be((int)SocketShutdown.Both);
        }

        [Fact]
        public void TCP_NODELAY_HasCorrectValue()
        {
            SocketModule.TCP_NODELAY.Should().Be((int)SocketOptionName.NoDelay);
        }

        [Fact]
        public void SO_RCVBUF_HasCorrectValue()
        {
            SocketModule.SO_RCVBUF.Should().Be((int)SocketOptionName.ReceiveBuffer);
        }

        [Fact]
        public void SO_SNDBUF_HasCorrectValue()
        {
            SocketModule.SO_SNDBUF.Should().Be((int)SocketOptionName.SendBuffer);
        }

        [Fact]
        public void SOMAXCONN_Is128()
        {
            SocketModule.SOMAXCONN.Should().Be(128);
        }

        // ---- Socket creation ----

        [Fact]
        public void Socket_CreatesStreamSocket()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Family.Should().Be(SocketModule.AF_INET);
            s.Type.Should().Be(SocketModule.SOCK_STREAM);
        }

        [Fact]
        public void Socket_CreatesDatagramSocket()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_DGRAM);
            s.Family.Should().Be(SocketModule.AF_INET);
            s.Type.Should().Be(SocketModule.SOCK_DGRAM);
        }

        [Fact]
        public void Socket_DefaultParams_CreatesIPv4Stream()
        {
            using var s = SocketModule.Socket();
            s.Family.Should().Be(SocketModule.AF_INET);
            s.Type.Should().Be(SocketModule.SOCK_STREAM);
        }

        // ---- TCP Server/Client Tests ----

        [Fact]
        public void TcpServer_BindListenAccept_Works()
        {
            using var server = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            server.Setsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR, 1);
            server.Bind(("127.0.0.1", 0));
            server.Listen(5);

            var serverAddr = server.Getsockname();
            serverAddr.host.Should().Be("127.0.0.1");
            serverAddr.port.Should().BeGreaterThan(0);

            // Connect a client
            using var client = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            client.Connect(("127.0.0.1", serverAddr.port));

            // Accept the connection
            var (conn, addr) = server.Accept();
            using (conn)
            {
                addr.host.Should().Be("127.0.0.1");
                addr.port.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void TcpSendRecv_Works()
        {
            using var server = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            server.Setsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR, 1);
            server.Bind(("127.0.0.1", 0));
            server.Listen(1);
            var port = server.Getsockname().port;

            using var client = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            client.Connect(("127.0.0.1", port));

            var (conn, _) = server.Accept();
            using (conn)
            {
                var message = new Bytes(Encoding.UTF8.GetBytes("hello"));
                client.Sendall(message);

                var received = conn.Recv(1024);
                Encoding.UTF8.GetString(received.ToArray()).Should().Be("hello");
            }
        }

        // ---- UDP Tests ----

        [Fact]
        public void UdpSendtoRecvfrom_Works()
        {
            using var receiver = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_DGRAM);
            receiver.Bind(("127.0.0.1", 0));
            var port = receiver.Getsockname().port;

            using var sender = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_DGRAM);
            var message = new Bytes(Encoding.UTF8.GetBytes("udp test"));
            sender.Sendto(message, ("127.0.0.1", port));

            var (data, addr) = receiver.Recvfrom(1024);
            Encoding.UTF8.GetString(data.ToArray()).Should().Be("udp test");
            addr.host.Should().Be("127.0.0.1");
        }

        // ---- DNS Tests ----

        [Fact]
        public void Gethostbyname_Localhost_ReturnsLoopback()
        {
            var ip = SocketModule.Gethostbyname("localhost");
            // localhost should resolve to 127.0.0.1 or ::1
            (ip == "127.0.0.1" || ip == "::1").Should().BeTrue();
        }

        [Fact]
        public void Gethostname_ReturnsNonEmpty()
        {
            var hostname = SocketModule.Gethostname();
            hostname.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Gethostbyname_InvalidHost_ThrowsGaiError()
        {
            Action act = () => SocketModule.Gethostbyname("this.host.does.not.exist.invalid");
            act.Should().Throw<SharpySocketGaiError>();
        }

        // ---- Socket Options Tests ----

        [Fact]
        public void Setsockopt_Getsockopt_ReuseAddr()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Setsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR, 1);
            var val = s.Getsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR);
            val.Should().NotBe(0);
        }

        // ---- Timeout Tests ----

        [Fact]
        public void Settimeout_Gettimeout_Works()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Settimeout(5.0);
            s.Gettimeout().Should().Be(5.0);

            s.Settimeout(null);
            s.Gettimeout().Should().BeNull();
        }

        // ---- Default timeout Tests ----

        [Fact]
        public void Getdefaulttimeout_ReturnsNull_ByDefault()
        {
            // Reset to ensure clean state
            SocketModule.Setdefaulttimeout(null);
            SocketModule.Getdefaulttimeout().Should().BeNull();
        }

        [Fact]
        public void Setdefaulttimeout_SetsAndGets()
        {
            try
            {
                SocketModule.Setdefaulttimeout(10.0);
                SocketModule.Getdefaulttimeout().Should().Be(10.0);
            }
            finally
            {
                SocketModule.Setdefaulttimeout(null);
            }
        }

        [Fact]
        public void Setdefaulttimeout_AppliedToNewSockets()
        {
            try
            {
                SocketModule.Setdefaulttimeout(3.0);
                using var s = SocketModule.Socket();
                s.Gettimeout().Should().Be(3.0);
            }
            finally
            {
                SocketModule.Setdefaulttimeout(null);
            }
        }

        // ---- Byte Order Tests ----

        [Fact]
        public void Htons_Ntohs_RoundTrips()
        {
            var original = 8080;
            var network = SocketModule.Htons(original);
            var host = SocketModule.Ntohs(network);
            host.Should().Be(original);
        }

        [Fact]
        public void Htonl_Ntohl_RoundTrips()
        {
            var original = 0x12345678;
            var network = SocketModule.Htonl(original);
            var host = SocketModule.Ntohl(network);
            host.Should().Be(original);
        }

        // ---- Inet conversion Tests ----

        [Fact]
        public void Inet_aton_ntoa_RoundTrips()
        {
            var ip = "192.168.1.1";
            var packed = SocketModule.Inet_aton(ip);
            var result = SocketModule.Inet_ntoa(packed);
            result.Should().Be(ip);
        }

        [Fact]
        public void Inet_pton_ntop_IPv4_RoundTrips()
        {
            var ip = "10.0.0.1";
            var packed = SocketModule.Inet_pton(SocketModule.AF_INET, ip);
            packed.ToArray().Length.Should().Be(4); // IPv4 = 4 bytes
            var result = SocketModule.Inet_ntop(SocketModule.AF_INET, packed);
            result.Should().Be(ip);
        }

        [Fact]
        public void Inet_pton_ntop_IPv6_RoundTrips()
        {
            var ip = "::1";
            var packed = SocketModule.Inet_pton(SocketModule.AF_INET6, ip);
            packed.ToArray().Length.Should().Be(16); // IPv6 = 16 bytes
            var result = SocketModule.Inet_ntop(SocketModule.AF_INET6, packed);
            result.Should().Be(ip);
        }

        // ---- IPv6 Tests ----

        [Fact]
        public void Socket_IPv6_CanCreate()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET6, SocketModule.SOCK_STREAM);
            s.Family.Should().Be(SocketModule.AF_INET6);
        }

        // ---- Error Handling ----

        [Fact]
        public void Connect_InvalidAddress_ThrowsSharpySocketError()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Settimeout(1.0);
            Action act = () => s.Connect(("192.0.2.1", 1)); // TEST-NET, should fail
            act.Should().Throw<SharpySocketError>();
        }

        // ---- Error type hierarchy ----

        [Fact]
        public void SharpySocketTimeout_IsSharpySocketError()
        {
            var ex = new SharpySocketTimeout("timed out");
            ex.Should().BeAssignableTo<SharpySocketError>();
        }

        [Fact]
        public void SharpySocketGaiError_IsSharpySocketError()
        {
            var ex = new SharpySocketGaiError("name resolution failed");
            ex.Should().BeAssignableTo<SharpySocketError>();
        }

        [Fact]
        public void SharpySocketHError_IsSharpySocketError()
        {
            var ex = new SharpySocketHError("host error");
            ex.Should().BeAssignableTo<SharpySocketError>();
        }

        [Fact]
        public void SharpySocketError_FromSocketException_PreservesErrno()
        {
            var inner = new SocketException((int)SocketError.ConnectionRefused);
            var ex = SharpySocketError.FromSocketException(inner);
            ex.Errno.Should().Be((int)SocketError.ConnectionRefused);
            ex.InnerException.Should().BeSameAs(inner);
        }

        // ---- CreateConnection Tests ----

        [Fact]
        public void CreateConnection_ConnectsToLocalServer()
        {
            using var server = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            server.Setsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR, 1);
            server.Bind(("127.0.0.1", 0));
            server.Listen(1);
            var port = server.Getsockname().port;

            using var client = SocketModule.Create_connection(("127.0.0.1", port));
            client.Should().NotBeNull();

            var peer = client.Getpeername();
            peer.host.Should().Be("127.0.0.1");
            peer.port.Should().Be(port);

            var (conn, _) = server.Accept();
            conn.Dispose();
        }

        [Fact]
        public void CreateConnection_InvalidAddress_ThrowsAndDisposesSocket()
        {
            Action act = () => SocketModule.Create_connection(("192.0.2.1", 1), timeout: 1.0);
            act.Should().Throw<SharpySocketError>();
        }

        // ---- ToString ----

        [Fact]
        public void ToString_ContainsSocketInfo()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            var str = s.ToString();
            str.Should().Contain("socket");
            str.Should().Contain("family=");
            str.Should().Contain("type=");
        }

        // ---- Getpeername ----

        [Fact]
        public void Getpeername_AfterConnect_ReturnsRemoteAddr()
        {
            using var server = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            server.Setsockopt(SocketModule.SOL_SOCKET, SocketModule.SO_REUSEADDR, 1);
            server.Bind(("127.0.0.1", 0));
            server.Listen(1);
            var port = server.Getsockname().port;

            using var client = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            client.Connect(("127.0.0.1", port));

            var peer = client.Getpeername();
            peer.host.Should().Be("127.0.0.1");
            peer.port.Should().Be(port);

            var (conn, _) = server.Accept();
            conn.Dispose();
        }

        // ---- Setblocking / Getblocking ----

        [Fact]
        public void Setblocking_False_SetsNonBlocking()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Setblocking(false);
            s.Getblocking().Should().BeFalse();
        }

        [Fact]
        public void Setblocking_True_SetsBlocking()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Setblocking(false);
            s.Setblocking(true);
            s.Getblocking().Should().BeTrue();
        }

        // ---- Getnameinfo ----

        [Fact]
        public void Getnameinfo_Localhost_ReturnsHostAndService()
        {
            var (host, service) = SocketModule.Getnameinfo(("127.0.0.1", 80));
            host.Should().NotBeNullOrEmpty();
            service.Should().Be("80");
        }
    }
}

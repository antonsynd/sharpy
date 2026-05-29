using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
        public void IPPROTO_TCP_HasCorrectValue()
        {
            SocketModule.IPPROTO_TCP.Should().Be((int)ProtocolType.Tcp);
        }

        [Fact]
        public void IPPROTO_UDP_HasCorrectValue()
        {
            SocketModule.IPPROTO_UDP.Should().Be((int)ProtocolType.Udp);
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
                var message = new Bytes(System.Text.Encoding.UTF8.GetBytes("hello"));
                client.Sendall(message);

                var received = conn.Recv(1024);
                System.Text.Encoding.UTF8.GetString(received.ToArray()).Should().Be("hello");
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
            var message = new Bytes(System.Text.Encoding.UTF8.GetBytes("udp test"));
            sender.Sendto(message, ("127.0.0.1", port));

            var (data, addr) = receiver.Recvfrom(1024);
            System.Text.Encoding.UTF8.GetString(data.ToArray()).Should().Be("udp test");
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

        // ---- IPv6 Tests ----

        [Fact]
        public void Socket_IPv6_CanCreate()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET6, SocketModule.SOCK_STREAM);
            s.Family.Should().Be(SocketModule.AF_INET6);
        }

        // ---- Error Handling ----

        [Fact]
        public void Connect_InvalidAddress_ThrowsSocketException()
        {
            using var s = SocketModule.Socket(SocketModule.AF_INET, SocketModule.SOCK_STREAM);
            s.Settimeout(1.0);
            Action act = () => s.Connect(("192.0.2.1", 1)); // TEST-NET, should fail
            act.Should().Throw<SocketException>();
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
    }
}

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Low-level networking interface, similar to Python's socket module.
    /// <para>
    /// The bulk of this module (constants, the <c>socket</c> wrapper class, the
    /// exception hierarchy, DNS helpers, and <c>create_connection</c>) is generated
    /// from <c>src/Sharpy.Stdlib/spy/socket_module.spy</c> into <c>SocketModule.cs</c>.
    /// The byte-order, inet, and <c>getaddrinfo</c> helpers below stay hand-written
    /// because they involve <c>short</c>/<c>byte[]</c> interop and runtime-constructed
    /// tuple lists that are cleaner to express directly in C#.
    /// </para>
    /// </summary>
    [SharpyModule("socket")]
    public static partial class SocketModule
    {
        // ---- Byte-order conversions ----

        /// <summary>
        /// Convert a 16-bit integer from host byte order to network byte order (big-endian),
        /// similar to Python's <c>socket.htons()</c>.
        /// </summary>
        public static int Htons(int x)
        {
            return (int)IPAddress.HostToNetworkOrder((short)x);
        }

        /// <summary>
        /// Convert a 32-bit integer from host byte order to network byte order (big-endian),
        /// similar to Python's <c>socket.htonl()</c>.
        /// </summary>
        public static int Htonl(int x)
        {
            return IPAddress.HostToNetworkOrder(x);
        }

        /// <summary>
        /// Convert a 16-bit integer from network byte order to host byte order,
        /// similar to Python's <c>socket.ntohs()</c>.
        /// </summary>
        public static int Ntohs(int x)
        {
            return (int)IPAddress.NetworkToHostOrder((short)x);
        }

        /// <summary>
        /// Convert a 32-bit integer from network byte order to host byte order,
        /// similar to Python's <c>socket.ntohl()</c>.
        /// </summary>
        public static int Ntohl(int x)
        {
            return IPAddress.NetworkToHostOrder(x);
        }

        // ---- Inet address conversions ----

        /// <summary>
        /// Convert an IPv4 address string to a 32-bit packed binary format,
        /// similar to Python's <c>socket.inet_aton()</c>.
        /// </summary>
        public static Bytes Inet_aton(string ip_string)
        {
            var addr = IPAddress.Parse(ip_string);
            return new Bytes(addr.GetAddressBytes());
        }

        /// <summary>
        /// Convert a 32-bit packed binary IPv4 address to a string,
        /// similar to Python's <c>socket.inet_ntoa()</c>.
        /// </summary>
        public static string Inet_ntoa(Bytes packed_ip)
        {
            var addr = new IPAddress(packed_ip.ToArray());
            return addr.ToString();
        }

        /// <summary>
        /// Convert an IP address string to packed binary format for the given address family,
        /// similar to Python's <c>socket.inet_pton()</c>.
        /// </summary>
        public static Bytes Inet_pton(int af, string ip_string)
        {
            var addr = IPAddress.Parse(ip_string);
            if ((int)addr.AddressFamily != af)
                throw new Error("illegal IP address string passed to inet_pton");
            return new Bytes(addr.GetAddressBytes());
        }

        /// <summary>
        /// Convert a packed binary IP address to string form for the given address family,
        /// similar to Python's <c>socket.inet_ntop()</c>.
        /// </summary>
        public static string Inet_ntop(int af, Bytes packed_ip)
        {
            var addr = new IPAddress(packed_ip.ToArray());
            if ((int)addr.AddressFamily != af)
                throw new Error("illegal IP address passed to inet_ntop");
            return addr.ToString();
        }

        // ---- Address resolution ----

        /// <summary>
        /// Resolve a hostname to a list of address info tuples, similar to Python's
        /// <c>socket.getaddrinfo()</c>. Returns a list of tuples
        /// (family, type, proto, canonname, sockaddr).
        /// </summary>
        public static List<(int family, int type, int proto, string canonname, (string host, int port) sockaddr)> Getaddrinfo(
            string host, int port, int family = 0, int type = 0, int proto = 0)
        {
            try
            {
                var results = new List<(int, int, int, string, (string, int))>();
                var addresses = Dns.GetHostAddresses(host);

                foreach (var addr in addresses)
                {
                    var addrFamily = (int)addr.AddressFamily;
                    if (family != 0 && addrFamily != family)
                        continue;

                    // For each address, provide both STREAM and DGRAM if type not specified.
                    var types = type != 0
                        ? new[] { type }
                        : new[] { (int)SocketType.Stream, (int)SocketType.Dgram };

                    foreach (var t in types)
                    {
                        var p = proto;
                        if (p == 0)
                        {
                            p = t == (int)SocketType.Stream
                                ? (int)ProtocolType.Tcp
                                : (int)ProtocolType.Udp;
                        }

                        results.Add((addrFamily, t, p, "", (addr.ToString(), port)));
                    }
                }

                return results;
            }
            catch (SocketException ex)
            {
                throw new Gaierror(ex.Message, ex, (int)ex.SocketErrorCode);
            }
        }
    }
}

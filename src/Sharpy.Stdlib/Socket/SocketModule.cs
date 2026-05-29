using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Low-level networking interface, similar to Python's <c>socket</c> module.
    /// Wraps <see cref="System.Net.Sockets.Socket"/> and related .NET types.
    /// </summary>
    public static partial class SocketModule
    {
        // ---- Address family constants ----

        /// <summary>IPv4 address family.</summary>
        public static int AF_INET => (int)AddressFamily.InterNetwork;

        /// <summary>IPv6 address family.</summary>
        public static int AF_INET6 => (int)AddressFamily.InterNetworkV6;

        /// <summary>Unix domain socket address family.</summary>
        public static int AF_UNIX => (int)AddressFamily.Unix;

        // ---- Socket type constants ----

        /// <summary>Stream socket (TCP).</summary>
        public static int SOCK_STREAM => (int)SocketType.Stream;

        /// <summary>Datagram socket (UDP).</summary>
        public static int SOCK_DGRAM => (int)SocketType.Dgram;

        // ---- Protocol constants ----

        /// <summary>IP protocol (default for most sockets).</summary>
        public static int IPPROTO_IP => (int)ProtocolType.IP;

        /// <summary>TCP protocol.</summary>
        public static int IPPROTO_TCP => (int)ProtocolType.Tcp;

        /// <summary>UDP protocol.</summary>
        public static int IPPROTO_UDP => (int)ProtocolType.Udp;

        // ---- Socket option constants ----

        /// <summary>Socket-level options.</summary>
        public static int SOL_SOCKET => (int)SocketOptionLevel.Socket;

        /// <summary>Reuse address option.</summary>
        public static int SO_REUSEADDR => (int)SocketOptionName.ReuseAddress;

        /// <summary>Keep-alive option.</summary>
        public static int SO_KEEPALIVE => (int)SocketOptionName.KeepAlive;

        /// <summary>Broadcast option.</summary>
        public static int SO_BROADCAST => (int)SocketOptionName.Broadcast;

        // ---- Module-level functions ----

        /// <summary>
        /// Create a new socket object, similar to Python's <c>socket.socket()</c>.
        /// </summary>
        /// <param name="family">Address family (default: AF_INET).</param>
        /// <param name="type">Socket type (default: SOCK_STREAM).</param>
        /// <param name="proto">Protocol number (default: 0, auto-detect).</param>
        /// <returns>A new <see cref="SocketWrapper"/> instance.</returns>
        /// <example>
        /// <code>
        /// s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        /// </code>
        /// </example>
        public static SocketWrapper Socket(
            int family = (int)System.Net.Sockets.AddressFamily.InterNetwork,
            int type = (int)System.Net.Sockets.SocketType.Stream,
            int proto = 0)
        {
            var addressFamily = (AddressFamily)family;
            var socketType = (SocketType)type;
            var protocolType = (ProtocolType)proto;
            return new SocketWrapper(addressFamily, socketType, protocolType);
        }

        /// <summary>
        /// Resolve a hostname to an IPv4 address string, similar to Python's
        /// <c>socket.gethostbyname()</c>.
        /// </summary>
        /// <param name="hostname">The hostname to resolve.</param>
        /// <returns>A string containing the IPv4 address.</returns>
        /// <example>
        /// <code>
        /// ip = socket.gethostbyname("example.com")
        /// </code>
        /// </example>
        public static string Gethostbyname(string hostname)
        {
            var addresses = Dns.GetHostAddresses(hostname);
            foreach (var addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return addr.ToString();
            }
            if (addresses.Length > 0)
                return addresses[0].ToString();
            throw new SocketException((int)SocketError.HostNotFound);
        }

        /// <summary>
        /// Resolve a hostname to a list of address info tuples, similar to Python's
        /// <c>socket.getaddrinfo()</c>.
        /// </summary>
        /// <param name="host">The hostname to resolve.</param>
        /// <param name="port">The port number.</param>
        /// <param name="family">Address family filter (default: 0, any).</param>
        /// <param name="type">Socket type filter (default: 0, any).</param>
        /// <param name="proto">Protocol filter (default: 0, any).</param>
        /// <returns>A list of tuples (family, type, proto, canonname, sockaddr).</returns>
        /// <example>
        /// <code>
        /// infos = socket.getaddrinfo("example.com", 80)
        /// </code>
        /// </example>
        public static List<(int family, int type, int proto, string canonname, (string host, int port) sockaddr)> Getaddrinfo(
            string host, int port, int family = 0, int type = 0, int proto = 0)
        {
            var results = new List<(int, int, int, string, (string, int))>();
            var addresses = Dns.GetHostAddresses(host);

            foreach (var addr in addresses)
            {
                var addrFamily = (int)addr.AddressFamily;
                if (family != 0 && addrFamily != family)
                    continue;

                // For each address, provide both STREAM and DGRAM if type not specified
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

        /// <summary>
        /// Get the fully qualified domain name for the local host, similar to Python's
        /// <c>socket.getfqdn()</c>.
        /// </summary>
        /// <returns>The FQDN of the local host.</returns>
        public static string Getfqdn()
        {
            try
            {
                var hostName = Dns.GetHostName();
                var entry = Dns.GetHostEntry(hostName);
                return entry.HostName;
            }
            catch
            {
                return "localhost";
            }
        }

        /// <summary>
        /// Get the hostname of the current machine, similar to Python's
        /// <c>socket.gethostname()</c>.
        /// </summary>
        /// <returns>The hostname string.</returns>
        public static string Gethostname()
        {
            return Dns.GetHostName();
        }

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
    }
}

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

        /// <summary>Raw socket.</summary>
        public static int SOCK_RAW => (int)SocketType.Raw;

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

        /// <summary>Receive buffer size option.</summary>
        public static int SO_RCVBUF => (int)SocketOptionName.ReceiveBuffer;

        /// <summary>Send buffer size option.</summary>
        public static int SO_SNDBUF => (int)SocketOptionName.SendBuffer;

        /// <summary>TCP no-delay option (disable Nagle's algorithm).</summary>
        public static int TCP_NODELAY => (int)SocketOptionName.NoDelay;

        // ---- Shutdown constants ----

        /// <summary>Shut down the reading side of the socket.</summary>
        public static int SHUT_RD => (int)SocketShutdown.Receive;

        /// <summary>Shut down the writing side of the socket.</summary>
        public static int SHUT_WR => (int)SocketShutdown.Send;

        /// <summary>Shut down both reading and writing.</summary>
        public static int SHUT_RDWR => (int)SocketShutdown.Both;

        // ---- Other constants ----

        /// <summary>Maximum length of the queue of pending connections.</summary>
        public static int SOMAXCONN => 128;

        // ---- Default timeout ----

        private static double? _defaultTimeout;

        /// <summary>
        /// Get the default timeout value for new socket objects, or null if no
        /// default timeout has been set.
        /// </summary>
        public static double? Getdefaulttimeout()
        {
            return _defaultTimeout;
        }

        /// <summary>
        /// Set the default timeout value for new socket objects.
        /// Pass null to reset to blocking mode (no timeout).
        /// </summary>
        public static void Setdefaulttimeout(double? timeout)
        {
            _defaultTimeout = timeout;
        }

        // ---- Module-level functions ----

        /// <summary>
        /// Create a new socket object, similar to Python's <c>socket.socket()</c>.
        /// </summary>
        /// <param name="family">Address family (default: AF_INET).</param>
        /// <param name="type">Socket type (default: SOCK_STREAM).</param>
        /// <param name="proto">Protocol number (default: 0, auto-detect).</param>
        /// <returns>A new <see cref="SocketWrapper"/> instance.</returns>
        public static SocketWrapper Socket(
            int family = (int)System.Net.Sockets.AddressFamily.InterNetwork,
            int type = (int)System.Net.Sockets.SocketType.Stream,
            int proto = 0)
        {
            var addressFamily = (AddressFamily)family;
            var socketType = (SocketType)type;
            var protocolType = (ProtocolType)proto;
            var wrapper = new SocketWrapper(addressFamily, socketType, protocolType);
            if (_defaultTimeout != null)
            {
                wrapper.Settimeout(_defaultTimeout);
            }
            return wrapper;
        }

        /// <summary>
        /// Resolve a hostname to an IPv4 address string, similar to Python's
        /// <c>socket.gethostbyname()</c>.
        /// </summary>
        /// <param name="hostname">The hostname to resolve.</param>
        /// <returns>A string containing the IPv4 address.</returns>
        public static string Gethostbyname(string hostname)
        {
            try
            {
                var addresses = Dns.GetHostAddresses(hostname);
                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                        return addr.ToString();
                }
                if (addresses.Length > 0)
                    return addresses[0].ToString();
                throw new SharpySocketGaiError("Name or service not known", (int)SocketError.HostNotFound);
            }
            catch (SocketException ex)
            {
                throw new SharpySocketGaiError(ex.Message, ex, (int)ex.SocketErrorCode);
            }
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
            catch (SocketException ex)
            {
                throw new SharpySocketGaiError(ex.Message, ex, (int)ex.SocketErrorCode);
            }
        }

        /// <summary>
        /// Resolve a socket address to a host name and service name,
        /// similar to Python's <c>socket.getnameinfo()</c>.
        /// </summary>
        public static (string host, string service) Getnameinfo((string host, int port) sockaddr, int flags = 0)
        {
            try
            {
                var entry = Dns.GetHostEntry(sockaddr.host);
                string host = entry.HostName;
                string service = sockaddr.port.ToString();
                return (host, service);
            }
            catch (SocketException ex)
            {
                throw new SharpySocketGaiError(ex.Message, ex, (int)ex.SocketErrorCode);
            }
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

        /// <summary>
        /// Convert an IP address string to packed binary format for the given address family,
        /// similar to Python's <c>socket.inet_pton()</c>.
        /// </summary>
        /// <param name="af">Address family (AF_INET or AF_INET6).</param>
        /// <param name="ip_string">IP address string to convert.</param>
        /// <returns>Packed binary representation of the address.</returns>
        public static Bytes Inet_pton(int af, string ip_string)
        {
            var addr = IPAddress.Parse(ip_string);
            if ((int)addr.AddressFamily != af)
                throw new SharpySocketError($"illegal IP address string passed to inet_pton");
            return new Bytes(addr.GetAddressBytes());
        }

        /// <summary>
        /// Convert a packed binary IP address to string form for the given address family,
        /// similar to Python's <c>socket.inet_ntop()</c>.
        /// </summary>
        /// <param name="af">Address family (AF_INET or AF_INET6).</param>
        /// <param name="packed_ip">Packed binary IP address.</param>
        /// <returns>String representation of the IP address.</returns>
        public static string Inet_ntop(int af, Bytes packed_ip)
        {
            var addr = new IPAddress(packed_ip.ToArray());
            if ((int)addr.AddressFamily != af)
                throw new SharpySocketError($"illegal IP address passed to inet_ntop");
            return addr.ToString();
        }

        /// <summary>
        /// Create a TCP connection to a remote address, similar to Python's
        /// <c>socket.create_connection()</c>.
        /// </summary>
        /// <param name="address">A tuple of (host, port) to connect to.</param>
        /// <param name="timeout">Optional connection timeout in seconds.</param>
        /// <param name="sourceAddress">Optional source address to bind to before connecting.</param>
        /// <returns>A connected <see cref="SocketWrapper"/>.</returns>
        public static SocketWrapper Create_connection(
            (string host, int port) address,
            double? timeout = null,
            (string host, int port)? sourceAddress = null)
        {
            var sock = Socket(AF_INET, SOCK_STREAM, 0);
            try
            {
                if (timeout != null)
                    sock.Settimeout(timeout);
                if (sourceAddress != null)
                    sock.Bind(sourceAddress.Value);
                sock.Connect(address);
                return sock;
            }
            catch
            {
                sock.Dispose();
                throw;
            }
        }
    }
}

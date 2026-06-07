// Generated from src/Sharpy.Stdlib/spy/socket_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/socket_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Low-level networking interface (TCP, UDP, DNS).
    /// </summary>
    public static partial class SocketModule
    {
        public static int AF_INET = (int)global::System.Net.Sockets.AddressFamily.InterNetwork;
        public static int AF_INET6 = (int)global::System.Net.Sockets.AddressFamily.InterNetworkV6;
        public static int AF_UNIX = (int)global::System.Net.Sockets.AddressFamily.Unix;
        public static int SOCK_STREAM = (int)global::System.Net.Sockets.SocketType.Stream;
        public static int SOCK_DGRAM = (int)global::System.Net.Sockets.SocketType.Dgram;
        public static int SOCK_RAW = (int)global::System.Net.Sockets.SocketType.Raw;
        public static int IPPROTO_IP = (int)global::System.Net.Sockets.ProtocolType.IP;
        public static int IPPROTO_TCP = (int)global::System.Net.Sockets.ProtocolType.Tcp;
        public static int IPPROTO_UDP = (int)global::System.Net.Sockets.ProtocolType.Udp;
        public static int SOL_SOCKET = (int)global::System.Net.Sockets.SocketOptionLevel.Socket;
        public static int SO_REUSEADDR = (int)global::System.Net.Sockets.SocketOptionName.ReuseAddress;
        public static int SO_KEEPALIVE = (int)global::System.Net.Sockets.SocketOptionName.KeepAlive;
        public static int SO_BROADCAST = (int)global::System.Net.Sockets.SocketOptionName.Broadcast;
        public static int SO_RCVBUF = (int)global::System.Net.Sockets.SocketOptionName.ReceiveBuffer;
        public static int SO_SNDBUF = (int)global::System.Net.Sockets.SocketOptionName.SendBuffer;
        public static int TCP_NODELAY = (int)global::System.Net.Sockets.SocketOptionName.NoDelay;
        public static int SHUT_RD = (int)global::System.Net.Sockets.SocketShutdown.Receive;
        public static int SHUT_WR = (int)global::System.Net.Sockets.SocketShutdown.Send;
        public static int SHUT_RDWR = (int)global::System.Net.Sockets.SocketShutdown.Both;
        public static int SOMAXCONN = 128;
        public static double? _DefaultTimeout = default;
        /// <summary>
        /// Base exception for socket-related errors. Corresponds to Python's socket.error.
        /// </summary>
        public class Error : Exception
        {
            public int Errno;
            /// <summary>
            /// Create a socket error from a .NET SocketException.
            /// </summary>
            public static Error FromSocketException(global::System.Net.Sockets.SocketException ex)
            {
                return new Error(ex.Message, ex, ((int)ex.SocketErrorCode));
            }

            /// <summary>
            /// Create a socket error with the specified message and optional errno.
            /// </summary>
            public Error(string message, int errno = 0) : base(message)
            {
                this.Errno = errno;
            }

            /// <summary>
            /// Create a socket error wrapping an inner exception.
            /// </summary>
            public Error(string message, Exception inner, int errno = 0) : base(message, inner)
            {
                this.Errno = errno;
            }
        }

        /// <summary>
        /// Raised when a socket operation times out. Corresponds to Python's socket.timeout.
        /// </summary>
        public class Timeout : Error
        {
            /// <summary>
            /// Create a socket timeout error with the specified message.
            /// </summary>
            public Timeout(string message, int errno = 0) : base(message, errno)
            {
            }

            /// <summary>
            /// Create a socket timeout error wrapping an inner exception.
            /// </summary>
            public Timeout(string message, Exception inner, int errno = 0) : base(message, inner, errno)
            {
            }
        }

        /// <summary>
        /// Raised for address-related errors (e.g., DNS failures). Python's socket.gaierror.
        /// </summary>
        public class Gaierror : Error
        {
            /// <summary>
            /// Create a GAI error with the specified message.
            /// </summary>
            public Gaierror(string message, int errno = 0) : base(message, errno)
            {
            }

            /// <summary>
            /// Create a GAI error wrapping an inner exception.
            /// </summary>
            public Gaierror(string message, Exception inner, int errno = 0) : base(message, inner, errno)
            {
            }
        }

        /// <summary>
        /// Raised for legacy address-related errors. Corresponds to Python's socket.herror.
        /// </summary>
        public class Herror : Error
        {
            /// <summary>
            /// Create an herror with the specified message.
            /// </summary>
            public Herror(string message, int errno = 0) : base(message, errno)
            {
            }

            /// <summary>
            /// Create an herror wrapping an inner exception.
            /// </summary>
            public Herror(string message, Exception inner, int errno = 0) : base(message, inner, errno)
            {
            }
        }

        /// <summary>
        /// Wraps System.Net.Sockets.Socket to provide a Python-like socket API.
        /// Supports TCP and UDP communication, socket options, and timeout handling.
        /// </summary>
        public sealed class Socket : global::System.IDisposable
        {
            private global::System.Net.Sockets.Socket _Socket;
            private double? _Timeout;
            /// <summary>
            /// Connect to a remote (host, port) address.
            /// </summary>
            public void Connect((string host, int port) address)
            {
                try
                {
                    var ipAddresses = global::System.Net.Dns.GetHostAddresses(address.Item1);
                    if (global::Sharpy.Builtins.Len(ipAddresses) == 0)
                    {
                        throw new Gaierror("Name or service not known", ((int)global::System.Net.Sockets.SocketError.HostNotFound));
                    }

                    global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(ipAddresses[0], address.Item2);
                    this._Socket.Connect(endpoint);
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Bind the socket to a local (host, port) address.
            /// </summary>
            public void Bind((string host, int port) address)
            {
                try
                {
                    global::System.Net.IPAddress ipAddr = global::System.Net.IPAddress.Any;
                    string host = address.Item1;
                    if (host == "" || host == "0.0.0.0")
                    {
                        ipAddr = global::System.Net.IPAddress.Any;
                    }
                    else if (host == "::")
                    {
                        ipAddr = global::System.Net.IPAddress.IPv6Any;
                    }
                    else
                    {
                        ipAddr = global::System.Net.IPAddress.Parse(host);
                    }

                    global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(ipAddr, address.Item2);
                    this._Socket.Bind(endpoint);
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Enable a server to accept connections with the given backlog.
            /// </summary>
            public void Listen(int backlog = 5)
            {
                try
                {
                    this._Socket.Listen(backlog);
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Accept a connection, returning (new socket, (remote_host, remote_port)).
            /// </summary>
            public (Socket conn, (string host, int port) addr) Accept()
            {
                try
                {
                    var accepted = this._Socket.Accept();
                    var remote = accepted.RemoteEndPoint;
                    if (remote == null)
                    {
                        throw new Error("Accepted connection has no remote endpoint.");
                    }

                    global::System.Net.IPEndPoint remoteEp = (global::System.Net.IPEndPoint)remote;
                    Socket conn = new Socket(accepted);
                    return (conn, (remoteEp.Address.ToString(), remoteEp.Port));
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Send data to the socket, returning the number of bytes sent.
            /// </summary>
            public int Send(Sharpy.Bytes data)
            {
                try
                {
                    return this._Socket.Send(data.ToArray());
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Send all data to the socket, continuing until every byte is sent.
            /// </summary>
            public void Sendall(Sharpy.Bytes data)
            {
                try
                {
                    var buffer = data.ToArray();
                    int total = global::Sharpy.Builtins.Len(data);
                    int totalSent = 0;
                    while (totalSent < total)
                    {
                        int sent = this._Socket.Send(buffer, totalSent, total - totalSent, global::System.Net.Sockets.SocketFlags.None);
                        if (sent == 0)
                        {
                            throw new Error("Connection reset by peer", ((int)global::System.Net.Sockets.SocketError.ConnectionReset));
                        }

                        totalSent = totalSent + sent;
                    }
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Receive up to bufsize bytes from the socket.
            /// </summary>
            public Sharpy.Bytes Recv(int bufsize)
            {
                try
                {
                    var buffer = new byte[bufsize];
                    int received = this._Socket.Receive(buffer);
                    var result = new byte[received];
                    global::System.Array.Copy(buffer, result, received);
                    return new global::Sharpy.Bytes(result);
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Send data to a specific (host, port) address (UDP).
            /// </summary>
            public int Sendto(Sharpy.Bytes data, (string host, int port) address)
            {
                try
                {
                    global::System.Net.IPAddress ipAddr = global::System.Net.IPAddress.Parse(address.Item1);
                    global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(ipAddr, address.Item2);
                    return this._Socket.SendTo(data.ToArray(), endpoint);
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Receive data and the sender's address (UDP).
            /// </summary>
            public (Sharpy.Bytes data, (string host, int port) addr) Recvfrom(int bufsize)
            {
                try
                {
                    var buffer = new byte[bufsize];
                    global::System.Net.EndPoint remote = new global::System.Net.IPEndPoint(global::System.Net.IPAddress.Any, 0);
                    int received = this._Socket.ReceiveFrom(buffer, ref remote);
                    var result = new byte[received];
                    global::System.Array.Copy(buffer, result, received);
                    global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)remote;
                    return (new global::Sharpy.Bytes(result), (ep.Address.ToString(), ep.Port));
                }
                catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
                {
                    throw new Timeout("timed out", ex, ((int)ex.SocketErrorCode));
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Set a socket option (e.g., SOL_SOCKET, SO_REUSEADDR).
            /// </summary>
            public void Setsockopt(int level, int optname, int value)
            {
                try
                {
                    this._Socket.SetSocketOption((global::System.Net.Sockets.SocketOptionLevel)level, (global::System.Net.Sockets.SocketOptionName)optname, value);
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Get a socket option value.
            /// </summary>
            public int Getsockopt(int level, int optname)
            {
                try
                {
                    var opt = this._Socket.GetSocketOption((global::System.Net.Sockets.SocketOptionLevel)level, (global::System.Net.Sockets.SocketOptionName)optname);
                    if (opt == null)
                    {
                        return 0;
                    }

                    return (int)opt;
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Set the timeout in seconds for blocking operations, or None for blocking mode.
            /// </summary>
            public void Settimeout(double? timeout)
            {
                this._Timeout = timeout;
                if (timeout == null)
                {
                    this._Socket.Blocking = true;
                    this._Socket.ReceiveTimeout = 0;
                    this._Socket.SendTimeout = 0;
                }
                else
                {
                    double value = timeout.Value;
                    if (value == 0.0d)
                    {
                        this._Socket.Blocking = false;
                    }
                    else
                    {
                        this._Socket.Blocking = true;
                        int ms = (int)(value * 1000.0d);
                        this._Socket.ReceiveTimeout = ms;
                        this._Socket.SendTimeout = ms;
                    }
                }
            }

            /// <summary>
            /// Return the timeout in seconds, or None if in blocking mode.
            /// </summary>
            public double? Gettimeout()
            {
                return this._Timeout;
            }

            /// <summary>
            /// Set blocking (True) or non-blocking (False) mode.
            /// </summary>
            public void Setblocking(bool flag)
            {
                if (flag)
                {
                    this.Settimeout(default);
                }
                else
                {
                    this.Settimeout(0.0d);
                }
            }

            /// <summary>
            /// Return whether the socket is in blocking mode.
            /// </summary>
            public bool Getblocking()
            {
                return this._Socket.Blocking;
            }

            /// <summary>
            /// Shut down one or both halves of the connection (SHUT_RD/WR/RDWR).
            /// </summary>
            public void Shutdown(int how)
            {
                try
                {
                    this._Socket.Shutdown((global::System.Net.Sockets.SocketShutdown)how);
                }
                catch (global::System.Net.Sockets.SocketException ex)
                {
                    throw Error.FromSocketException(ex);
                }
            }

            /// <summary>
            /// Close the socket.
            /// </summary>
            public void Close()
            {
                this._Socket.Close();
            }

            /// <summary>
            /// Return the local (host, port) address the socket is bound to.
            /// </summary>
            public (string host, int port) Getsockname()
            {
                var endpoint = this._Socket.LocalEndPoint;
                if (endpoint == null)
                {
                    throw new Error("Socket is not bound to an address.");
                }

                global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)endpoint;
                return (ep.Address.ToString(), ep.Port);
            }

            /// <summary>
            /// Return the remote (host, port) address the socket is connected to.
            /// </summary>
            public (string host, int port) Getpeername()
            {
                var endpoint = this._Socket.RemoteEndPoint;
                if (endpoint == null)
                {
                    throw new Error("Socket is not connected.");
                }

                global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)endpoint;
                return (ep.Address.ToString(), ep.Port);
            }

            /// <summary>
            /// Return the socket handle (file descriptor) as an integer.
            /// </summary>
            public int Fileno()
            {
                return (int)this._Socket.Handle.ToInt64();
            }

            /// <summary>
            /// Dispose the underlying socket resources.
            /// </summary>
            public void Dispose()
            {
                this._Socket.Dispose();
            }

            public Socket Enter()
            {
                return this;
            }

            public void Exit()
            {
                this.Dispose();
            }

            public override string ToString()
            {
                int fd = this.Fileno();
                int fam = this.Family;
                int typ = this.type;
                int pr = this.Proto;
                return FormattableString.Invariant($"<socket fd={(fd)}, family={(fam)}, type={(typ)}, proto={(pr)}>");
            }

            public int Family
            {
                get
                {
                    _ = "The address family of the socket.";
                    return (int)this._Socket.AddressFamily;
                }
            }

            public int type
            {
                get
                {
                    _ = "The socket type.";
                    return (int)this._Socket.SocketType;
                }
            }

            public int Proto
            {
                get
                {
                    _ = "The protocol type.";
                    return (int)this._Socket.ProtocolType;
                }
            }

            /// <summary>
            /// Create a new socket with the given address family, type, and protocol.
            /// </summary>
            public Socket(int family = 2, int sockType = 1, int proto = 0)
            {
                this._Socket = new global::System.Net.Sockets.Socket((global::System.Net.Sockets.AddressFamily)family, (global::System.Net.Sockets.SocketType)sockType, (global::System.Net.Sockets.ProtocolType)proto);
                this._Timeout = default;
                double? @default = _DefaultTimeout;
                if (@default != null)
                {
                    this.Settimeout(@default.Value);
                }
            }

            /// <summary>
            /// Wrap an existing .NET socket (used for accepted connections).
            /// </summary>
            public Socket(global::System.Net.Sockets.Socket existing)
            {
                this._Socket = existing;
                this._Timeout = default;
            }
        }

        /// <summary>
        /// Return the default timeout in seconds for new sockets, or None.
        /// </summary>
        public static double? Getdefaulttimeout()
        {
            return _DefaultTimeout;
        }

        /// <summary>
        /// Set the default timeout for new sockets. None means blocking mode.
        /// </summary>
        public static void Setdefaulttimeout(double? timeout)
        {
            _DefaultTimeout = timeout;
        }

        /// <summary>
        /// Connect to a TCP (host, port) address and return the connected socket.
        /// </summary>
        public static Socket CreateConnection((string host, int port) address, double? timeout = default)
        {
            Socket sock = new Socket(AF_INET, SOCK_STREAM, 0);
            try
            {
                if (timeout != null)
                {
                    sock.Settimeout(timeout.Value);
                }

                sock.Connect(address);
                return sock;
            }
            catch (Exception)
            {
                sock.Close();
                throw;
            }
        }

        /// <summary>
        /// Return the hostname of the current machine.
        /// </summary>
        public static string Gethostname()
        {
            return global::System.Net.Dns.GetHostName();
        }

        /// <summary>
        /// Resolve a hostname to an IPv4 address string.
        /// </summary>
        public static string Gethostbyname(string hostname)
        {
            try
            {
                var addresses = global::System.Net.Dns.GetHostAddresses(hostname);
                int i = 0;
                while (i < global::Sharpy.Builtins.Len(addresses))
                {
                    if (((int)addresses[i].AddressFamily) == AF_INET)
                    {
                        return addresses[i].ToString();
                    }

                    i = i + 1;
                }

                if (global::Sharpy.Builtins.Len(addresses) > 0)
                {
                    return addresses[0].ToString();
                }

                throw new Gaierror("Name or service not known", ((int)global::System.Net.Sockets.SocketError.HostNotFound));
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw new Gaierror(ex.Message, ex, ((int)ex.SocketErrorCode));
            }
        }

        /// <summary>
        /// Return the fully qualified domain name of the local host.
        /// </summary>
        public static string Getfqdn()
        {
            try
            {
                string hostName = global::System.Net.Dns.GetHostName();
                var entry = global::System.Net.Dns.GetHostEntry(hostName);
                return entry.HostName;
            }
            catch (Exception)
            {
                return "localhost";
            }
        }

        /// <summary>
        /// Resolve a socket address to a (host, service) tuple.
        /// </summary>
        public static (string host, string service) Getnameinfo((string host, int port) sockaddr, int flags = 0)
        {
            try
            {
                var entry = global::System.Net.Dns.GetHostEntry(sockaddr.Item1);
                string host = entry.HostName;
                string service = global::Sharpy.Builtins.Str(sockaddr.Item2);
                return (host, service);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw new Gaierror(ex.Message, ex, ((int)ex.SocketErrorCode));
            }
        }
    }
}

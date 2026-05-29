using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Wraps <see cref="System.Net.Sockets.Socket"/> to provide a Python-like socket API.
    /// Supports TCP and UDP communication, socket options, and timeout handling.
    /// </summary>
    [SharpyModuleType("socket", "socket")]
    public sealed class SocketWrapper : IDisposable
    {
        private readonly System.Net.Sockets.Socket _socket;
        private double? _timeout;

        /// <summary>
        /// Creates a new socket with the specified address family, socket type, and protocol.
        /// </summary>
        internal SocketWrapper(AddressFamily family, SocketType type, ProtocolType proto)
        {
            _socket = new System.Net.Sockets.Socket(family, type, proto);
        }

        /// <summary>
        /// Wraps an existing .NET socket (used for accepted connections).
        /// </summary>
        internal SocketWrapper(System.Net.Sockets.Socket socket)
        {
            _socket = socket;
        }

        // ---- Properties ----

        /// <summary>
        /// The address family of the socket.
        /// </summary>
        public int Family => (int)_socket.AddressFamily;

        /// <summary>
        /// The socket type.
        /// </summary>
        public int Type => (int)_socket.SocketType;

        /// <summary>
        /// The protocol type.
        /// </summary>
        public int Proto => (int)_socket.ProtocolType;

        /// <summary>
        /// Get or set the socket timeout in seconds.
        /// A value of <c>None</c> means blocking mode (no timeout).
        /// A value of 0 means non-blocking mode.
        /// </summary>
        public double? Timeout
        {
            get => _timeout;
            set
            {
                _timeout = value;
                if (value == null)
                {
                    _socket.Blocking = true;
                    _socket.ReceiveTimeout = 0;
                    _socket.SendTimeout = 0;
                }
                else if (value == 0)
                {
                    _socket.Blocking = false;
                }
                else
                {
                    _socket.Blocking = true;
                    var ms = (int)(value.Value * 1000);
                    _socket.ReceiveTimeout = ms;
                    _socket.SendTimeout = ms;
                }
            }
        }

        // ---- Connection methods ----

        /// <summary>
        /// Connect to a remote address, similar to Python's <c>socket.connect()</c>.
        /// </summary>
        /// <param name="address">A tuple of (host, port).</param>
        /// <example>
        /// <code>
        /// s.connect(("example.com", 80))
        /// </code>
        /// </example>
        public void Connect((string host, int port) address)
        {
            var ipAddresses = Dns.GetHostAddresses(address.host);
            var endpoint = new IPEndPoint(ipAddresses[0], address.port);
            _socket.Connect(endpoint);
        }

        /// <summary>
        /// Bind the socket to an address, similar to Python's <c>socket.bind()</c>.
        /// </summary>
        /// <param name="address">A tuple of (host, port).</param>
        /// <example>
        /// <code>
        /// s.bind(("0.0.0.0", 8080))
        /// </code>
        /// </example>
        public void Bind((string host, int port) address)
        {
            IPAddress ipAddr;
            if (string.IsNullOrEmpty(address.host) || address.host == "0.0.0.0")
                ipAddr = IPAddress.Any;
            else if (address.host == "::")
                ipAddr = IPAddress.IPv6Any;
            else
                ipAddr = IPAddress.Parse(address.host);

            var endpoint = new IPEndPoint(ipAddr, address.port);
            _socket.Bind(endpoint);
        }

        /// <summary>
        /// Enable a server to accept connections, similar to Python's <c>socket.listen()</c>.
        /// </summary>
        /// <param name="backlog">Maximum number of queued connections (default: 5).</param>
        public void Listen(int backlog = 5)
        {
            _socket.Listen(backlog);
        }

        /// <summary>
        /// Accept a connection, similar to Python's <c>socket.accept()</c>.
        /// Returns a tuple of (new socket, (remote_host, remote_port)).
        /// </summary>
        /// <returns>A tuple of (connected socket, address tuple).</returns>
        /// <example>
        /// <code>
        /// conn, addr = srv.accept()
        /// </code>
        /// </example>
        public (SocketWrapper conn, (string host, int port) addr) Accept()
        {
            var accepted = _socket.Accept();
            var remoteEp = (IPEndPoint)accepted.RemoteEndPoint!;
            var wrapper = new SocketWrapper(accepted);
            return (wrapper, (remoteEp.Address.ToString(), remoteEp.Port));
        }

        // ---- Data transfer methods ----

        /// <summary>
        /// Send data to the socket, similar to Python's <c>socket.send()</c>.
        /// Returns the number of bytes sent.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>The number of bytes sent.</returns>
        public int Send(Bytes data)
        {
            return _socket.Send(data.ToArray());
        }

        /// <summary>
        /// Send all data to the socket, similar to Python's <c>socket.sendall()</c>.
        /// Unlike <c>send()</c>, this method continues sending until all data has been sent.
        /// </summary>
        /// <param name="data">The data to send.</param>
        public void Sendall(Bytes data)
        {
            var buffer = data.ToArray();
            int totalSent = 0;
            while (totalSent < buffer.Length)
            {
                int sent = _socket.Send(buffer, totalSent, buffer.Length - totalSent, SocketFlags.None);
                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                totalSent += sent;
            }
        }

        /// <summary>
        /// Receive data from the socket, similar to Python's <c>socket.recv()</c>.
        /// </summary>
        /// <param name="bufsize">Maximum number of bytes to receive.</param>
        /// <returns>The received data as bytes.</returns>
        /// <example>
        /// <code>
        /// data = s.recv(4096)
        /// </code>
        /// </example>
        public Bytes Recv(int bufsize)
        {
            var buffer = new byte[bufsize];
            int received = _socket.Receive(buffer);
            var result = new byte[received];
            Array.Copy(buffer, result, received);
            return new Bytes(result);
        }

        /// <summary>
        /// Send data to a specific address (UDP), similar to Python's <c>socket.sendto()</c>.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="address">A tuple of (host, port).</param>
        /// <returns>The number of bytes sent.</returns>
        public int Sendto(Bytes data, (string host, int port) address)
        {
            var ipAddr = IPAddress.Parse(address.host);
            var endpoint = new IPEndPoint(ipAddr, address.port);
            return _socket.SendTo(data.ToArray(), endpoint);
        }

        /// <summary>
        /// Receive data and sender's address (UDP), similar to Python's <c>socket.recvfrom()</c>.
        /// </summary>
        /// <param name="bufsize">Maximum number of bytes to receive.</param>
        /// <returns>A tuple of (data, (host, port)).</returns>
        public (Bytes data, (string host, int port) addr) Recvfrom(int bufsize)
        {
            var buffer = new byte[bufsize];
            EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
            int received = _socket.ReceiveFrom(buffer, ref remoteEp);
            var result = new byte[received];
            Array.Copy(buffer, result, received);
            var ep = (IPEndPoint)remoteEp;
            return (new Bytes(result), (ep.Address.ToString(), ep.Port));
        }

        // ---- Socket options ----

        /// <summary>
        /// Set a socket option, similar to Python's <c>socket.setsockopt()</c>.
        /// </summary>
        /// <param name="level">Option level (e.g., SOL_SOCKET).</param>
        /// <param name="optname">Option name (e.g., SO_REUSEADDR).</param>
        /// <param name="value">Option value (1 to enable, 0 to disable).</param>
        public void Setsockopt(int level, int optname, int value)
        {
            _socket.SetSocketOption(
                (SocketOptionLevel)level,
                (SocketOptionName)optname,
                value);
        }

        /// <summary>
        /// Get a socket option, similar to Python's <c>socket.getsockopt()</c>.
        /// </summary>
        /// <param name="level">Option level (e.g., SOL_SOCKET).</param>
        /// <param name="optname">Option name (e.g., SO_REUSEADDR).</param>
        /// <returns>The option value.</returns>
        public int Getsockopt(int level, int optname)
        {
            return (int)_socket.GetSocketOption(
                (SocketOptionLevel)level,
                (SocketOptionName)optname)!;
        }

        // ---- Lifecycle methods ----

        /// <summary>
        /// Set the timeout for blocking operations, similar to Python's
        /// <c>socket.settimeout()</c>.
        /// </summary>
        /// <param name="timeout">Timeout in seconds, or null for blocking mode.</param>
        public void Settimeout(double? timeout)
        {
            Timeout = timeout;
        }

        /// <summary>
        /// Get the timeout for blocking operations, similar to Python's
        /// <c>socket.gettimeout()</c>.
        /// </summary>
        /// <returns>Timeout in seconds, or null if blocking.</returns>
        public double? Gettimeout()
        {
            return _timeout;
        }

        /// <summary>
        /// Shut down one or both halves of the connection, similar to Python's
        /// <c>socket.shutdown()</c>.
        /// </summary>
        /// <param name="how">0=SHUT_RD, 1=SHUT_WR, 2=SHUT_RDWR.</param>
        public void Shutdown(int how)
        {
            _socket.Shutdown((SocketShutdown)how);
        }

        /// <summary>
        /// Close the socket, similar to Python's <c>socket.close()</c>.
        /// </summary>
        public void Close()
        {
            _socket.Close();
        }

        /// <summary>
        /// Get the local address the socket is bound to, similar to Python's
        /// <c>socket.getsockname()</c>.
        /// </summary>
        /// <returns>A tuple of (host, port).</returns>
        public (string host, int port) Getsockname()
        {
            var ep = (IPEndPoint)_socket.LocalEndPoint!;
            return (ep.Address.ToString(), ep.Port);
        }

        /// <summary>
        /// Get the remote address the socket is connected to, similar to Python's
        /// <c>socket.getpeername()</c>.
        /// </summary>
        /// <returns>A tuple of (host, port).</returns>
        public (string host, int port) Getpeername()
        {
            var ep = (IPEndPoint)_socket.RemoteEndPoint!;
            return (ep.Address.ToString(), ep.Port);
        }

        /// <summary>
        /// Return the file descriptor (handle) of the socket, similar to Python's
        /// <c>socket.fileno()</c>.
        /// </summary>
        /// <returns>The socket handle as an integer.</returns>
        public long Fileno()
        {
            return _socket.Handle.ToInt64();
        }

        /// <summary>Dispose the socket resources.</summary>
        public void Dispose()
        {
            _socket.Dispose();
        }

        /// <summary>Returns a string representation of the socket.</summary>
        public override string ToString()
        {
            return $"<socket fd={Fileno()}, family={Family}, type={Type}, proto={Proto}>";
        }
    }
}

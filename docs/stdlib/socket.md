# socket

Low-level networking interface, similar to Python's socket module.

```python
import socket
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `af_inet` | `int` | IPv4 address family. |
| `af_inet6` | `int` | IPv6 address family. |
| `af_unix` | `int` | Unix domain socket address family. |
| `sock_stream` | `int` | Stream socket (TCP). |
| `sock_dgram` | `int` | Datagram socket (UDP). |
| `sock_raw` | `int` | Raw socket. |
| `ipproto_ip` | `int` | IP protocol (default for most sockets). |
| `ipproto_tcp` | `int` | TCP protocol. |
| `ipproto_udp` | `int` | UDP protocol. |
| `sol_socket` | `int` | Socket-level options. |
| `so_reuseaddr` | `int` | Reuse address option. |
| `so_keepalive` | `int` | Keep-alive option. |
| `so_broadcast` | `int` | Broadcast option. |
| `so_rcvbuf` | `int` | Receive buffer size option. |
| `so_sndbuf` | `int` | Send buffer size option. |
| `tcp_nodelay` | `int` | TCP no-delay option (disable Nagle's algorithm). |
| `shut_rd` | `int` | Shut down the reading side of the socket. |
| `shut_wr` | `int` | Shut down the writing side of the socket. |
| `shut_rdwr` | `int` | Shut down both reading and writing. |
| `somaxconn` | `int` | Maximum length of the queue of pending connections. |

## Functions

### `socket.getdefaulttimeout() -> float | None`

Get the default timeout value for new socket objects, or None if no
default timeout has been set.

### `socket.setdefaulttimeout(timeout: float | None)`

Set the default timeout value for new socket objects.
Pass None to reset to blocking mode (no timeout).

### `socket.socket(family: int = (int)System.Net.Sockets.AddressFamily.InterNetwork, type: int = (int)System.Net.Sockets.SocketType.Stream, proto: int = 0) -> SocketWrapper`

Create a new socket object, similar to Python's `socket.socket()`.

**Parameters:**

- `family` (int) -- Address family (default: AF_INET).
- `type` (int) -- Socket type (default: SOCK_STREAM).
- `proto` (int) -- Protocol number (default: 0, auto-detect).

**Returns:** A new `SocketWrapper` instance.

### `socket.gethostbyname(hostname: str) -> str`

Resolve a hostname to an IPv4 address string, similar to Python's
`socket.gethostbyname()`.

**Parameters:**

- `hostname` (str) -- The hostname to resolve.

**Returns:** A string containing the IPv4 address.

### `socket.getfqdn() -> str`

Get the fully qualified domain name for the local host, similar to Python's
`socket.getfqdn()`.

**Returns:** The FQDN of the local host.

### `socket.gethostname() -> str`

Get the hostname of the current machine, similar to Python's
`socket.gethostname()`.

**Returns:** The hostname string.

### `socket.htons(x: int) -> int`

Convert a 16-bit integer from host byte order to network byte order (big-endian),
similar to Python's `socket.htons()`.

### `socket.htonl(x: int) -> int`

Convert a 32-bit integer from host byte order to network byte order (big-endian),
similar to Python's `socket.htonl()`.

### `socket.ntohs(x: int) -> int`

Convert a 16-bit integer from network byte order to host byte order,
similar to Python's `socket.ntohs()`.

### `socket.ntohl(x: int) -> int`

Convert a 32-bit integer from network byte order to host byte order,
similar to Python's `socket.ntohl()`.

### `socket.inet_aton(ip_string: str) -> Bytes`

Convert an IPv4 address string to a 32-bit packed binary format,
similar to Python's `socket.inet_aton()`.

### `socket.inet_ntoa(packed_ip: Bytes) -> str`

Convert a 32-bit packed binary IPv4 address to a string,
similar to Python's `socket.inet_ntoa()`.

### `socket.inet_pton(af: int, ip_string: str) -> Bytes`

Convert an IP address string to packed binary format for the given address family,
similar to Python's `socket.inet_pton()`.

**Parameters:**

- `af` (int) -- Address family (AF_INET or AF_INET6).
- `ip_string` (str) -- IP address string to convert.

**Returns:** Packed binary representation of the address.

### `socket.inet_ntop(af: int, packed_ip: Bytes) -> str`

Convert a packed binary IP address to string form for the given address family,
similar to Python's `socket.inet_ntop()`.

**Parameters:**

- `af` (int) -- Address family (AF_INET or AF_INET6).
- `packed_ip` (Bytes) -- Packed binary IP address.

**Returns:** String representation of the IP address.

### `socket.create_connection(address: tuple[string host, int port], timeout: float | None = None, source_address: tuple[string host, int port] | None = None) -> SocketWrapper`

Create a TCP connection to a remote address, similar to Python's
`socket.create_connection()`.

**Parameters:**

- `address` (tuple[string host, int port]) -- A tuple of (host, port) to connect to.
- `timeout` (float | None) -- Optional connection timeout in seconds.
- `source_address` (tuple[string host, int port] | None)

**Returns:** A connected `SocketWrapper`.

## error

Base exception for socket-related errors.
Corresponds to Python's `socket.error`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `errno` | `int` | The system error number associated with this socket error. |

### `from_socket_exception(ex: SocketException) -> SharpySocketError`

Create a `SharpySocketError` from a .NET `SocketException`.

## timeout

Base exception for socket-related errors.
Corresponds to Python's `socket.error`.

## gaierror

Base exception for socket-related errors.
Corresponds to Python's `socket.error`.

## herror

Base exception for socket-related errors.
Corresponds to Python's `socket.error`.

## socket

Wraps `System.Net.Sockets.Socket` to provide a Python-like socket API.
Supports TCP and UDP communication, socket options, and timeout handling.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `family` | `int` | The address family of the socket. |
| `type` | `int` | The socket type. |
| `proto` | `int` | The protocol type. |

### `connect(address: tuple[string host, int port])`

Connect to a remote address, similar to Python's `socket.connect()`.

**Parameters:**

- `address` (tuple[string host, int port]) -- A tuple of (host, port).

### `bind(address: tuple[string host, int port])`

Bind the socket to an address, similar to Python's `socket.bind()`.

**Parameters:**

- `address` (tuple[string host, int port]) -- A tuple of (host, port).

### `listen(backlog: int = 5)`

Enable a server to accept connections, similar to Python's `socket.listen()`.

**Parameters:**

- `backlog` (int) -- Maximum number of queued connections (default: 5).

### `send(data: Bytes) -> int`

Send data to the socket, similar to Python's `socket.send()`.
Returns the number of bytes sent.

**Parameters:**

- `data` (Bytes) -- The data to send.

**Returns:** The number of bytes sent.

### `sendall(data: Bytes)`

Send all data to the socket, similar to Python's `socket.sendall()`.
Unlike `send()`, this method continues sending until all data has been sent.

**Parameters:**

- `data` (Bytes) -- The data to send.

### `recv(bufsize: int) -> Bytes`

Receive data from the socket, similar to Python's `socket.recv()`.

**Parameters:**

- `bufsize` (int) -- Maximum number of bytes to receive.

**Returns:** The received data as bytes.

### `sendto(data: Bytes, address: tuple[string host, int port]) -> int`

Send data to a specific address (UDP), similar to Python's `socket.sendto()`.

**Parameters:**

- `data` (Bytes) -- The data to send.
- `address` (tuple[string host, int port]) -- A tuple of (host, port).

**Returns:** The number of bytes sent.

### `setsockopt(level: int, optname: int, value: int)`

Set a socket option, similar to Python's `socket.setsockopt()`.

**Parameters:**

- `level` (int) -- Option level (e.g., SOL_SOCKET).
- `optname` (int) -- Option name (e.g., SO_REUSEADDR).
- `value` (int) -- Option value (1 to enable, 0 to disable).

### `getsockopt(level: int, optname: int) -> int`

Get a socket option, similar to Python's `socket.getsockopt()`.

**Parameters:**

- `level` (int) -- Option level (e.g., SOL_SOCKET).
- `optname` (int) -- Option name (e.g., SO_REUSEADDR).

**Returns:** The option value.

### `settimeout(timeout: float | None)`

Set the timeout for blocking operations, similar to Python's
`socket.settimeout()`.

**Parameters:**

- `timeout` (float | None) -- Timeout in seconds, or None for blocking mode.

### `gettimeout() -> float | None`

Get the timeout for blocking operations, similar to Python's
`socket.gettimeout()`.

**Returns:** Timeout in seconds, or None if blocking.

### `setblocking(flag: bool)`

### `getblocking() -> bool`

### `shutdown(how: int)`

Shut down one or both halves of the connection, similar to Python's
`socket.shutdown()`.

**Parameters:**

- `how` (int) -- 0=SHUT_RD, 1=SHUT_WR, 2=SHUT_RDWR.

### `close()`

Close the socket, similar to Python's `socket.close()`.

### `fileno() -> long`

Return the file descriptor (handle) of the socket, similar to Python's
`socket.fileno()`.

**Returns:** The socket handle as an integer.

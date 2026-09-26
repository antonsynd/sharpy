# socket

Low-level networking interface, similar to Python's socket module.

The bulk of this module (constants, the `socket` wrapper class, the
exception hierarchy, DNS helpers, and `create_connection`) is generated
from `src/Sharpy.Stdlib/spy/socket_module.spy` into `SocketModule.cs`.
The byte-order, inet, and `getaddrinfo` helpers below stay hand-written
because they involve `short`/`byte[]` interop and runtime-constructed
tuple lists that are cleaner to express directly in C#.

```python
import socket
```

## Functions

### `socket.getdefaulttimeout() -> float | None`

Return the default timeout in seconds for new sockets, or None.

### `socket.setdefaulttimeout(timeout: float | None)`

Set the default timeout for new sockets. None means blocking mode.

### `socket.create_connection(address: tuple[str, int], timeout: float | None = None) -> Socket`

Connect to a TCP (host, port) address and return the connected socket.

### `socket.gethostname() -> str`

Return the hostname of the current machine.

### `socket.gethostbyname(hostname: str) -> str`

Resolve a hostname to an IPv4 address string.

### `socket.getfqdn() -> str`

Return the fully qualified domain name of the local host.

### `socket.getnameinfo(sockaddr: tuple[str, int], flags: int = 0) -> tuple[str, str]`

Resolve a socket address to a (host, service) tuple.

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

### `socket.inet_ntop(af: int, packed_ip: Bytes) -> str`

Convert a packed binary IP address to string form for the given address family,
similar to Python's `socket.inet_ntop()`.

### `socket.getaddrinfo(host: str, port: int, family: int = 0, type: int = 0, proto: int = 0) -> list[tuple[int, int, int, str, tuple[str, int]]]`

Resolve a hostname to a list of address info tuples, similar to Python's
`socket.getaddrinfo()`. Returns a list of tuples
(family, type, proto, canonname, sockaddr).

## error

Base exception for socket-related errors. Corresponds to Python's socket.error.

### `from_socket_exception(ex: Net.Sockets.SocketException) -> Error`

Create a socket error from a .NET SocketException.

## timeout

Raised when a socket operation times out. Corresponds to Python's socket.timeout.

## gaierror

Raised for address-related errors (e.g., DNS failures). Python's socket.gaierror.

## herror

Raised for legacy address-related errors. Corresponds to Python's socket.herror.

## socket

Wraps System.Net.Sockets.Socket to provide a Python-like socket API.
Supports TCP and UDP communication, socket options, and timeout handling.

### `connect(address: tuple[str, int])`

Connect to a remote (host, port) address.

### `bind(address: tuple[str, int])`

Bind the socket to a local (host, port) address.

### `listen(backlog: int = 5)`

Enable a server to accept connections with the given backlog.

### `accept() -> tuple[Socket, tuple[str, int]]`

Accept a connection, returning (new socket, (remote_host, remote_port)).

### `send(data: Sharpy.Bytes) -> int`

Send data to the socket, returning the number of bytes sent.

### `sendall(data: Sharpy.Bytes)`

Send all data to the socket, continuing until every byte is sent.

### `recv(bufsize: int) -> Sharpy.Bytes`

Receive up to bufsize bytes from the socket.

### `sendto(data: Sharpy.Bytes, address: tuple[str, int]) -> int`

Send data to a specific (host, port) address (UDP).

### `recvfrom(bufsize: int) -> tuple[Sharpy.Bytes, tuple[str, int]]`

Receive data and the sender's address (UDP).

### `setsockopt(level: int, optname: int, value: int)`

Set a socket option (e.g., SOL_SOCKET, SO_REUSEADDR).

### `getsockopt(level: int, optname: int) -> int`

Get a socket option value.

### `settimeout(timeout: float | None)`

Set the timeout in seconds for blocking operations, or None for blocking mode.

### `gettimeout() -> float | None`

Return the timeout in seconds, or None if in blocking mode.

### `setblocking(flag: bool)`

Set blocking (True) or non-blocking (False) mode.

### `getblocking() -> bool`

Return whether the socket is in blocking mode.

### `shutdown(how: int)`

Shut down one or both halves of the connection (SHUT_RD/WR/RDWR).

### `close()`

Close the socket.

### `getsockname() -> tuple[str, int]`

Return the local (host, port) address the socket is bound to.

### `getpeername() -> tuple[str, int]`

Return the remote (host, port) address the socket is connected to.

### `fileno() -> int`

Return the socket handle (file descriptor) as an integer.

### `enter() -> Socket`

### `exit()`

### `__str__() -> str`

`repr()` uses the same method.

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

### `socket.from_socket_exception(ex: Net.Sockets.SocketException) -> Error`

Create a socket error from a .NET SocketException.

### `socket.connect(address: tuple[string host, int port])`

Connect to a remote (host, port) address.

### `socket.bind(address: tuple[string host, int port])`

Bind the socket to a local (host, port) address.

### `socket.listen(backlog: int = 5)`

Enable a server to accept connections with the given backlog.

### `socket.send(data: Sharpy.Bytes) -> int`

Send data to the socket, returning the number of bytes sent.

### `socket.sendall(data: Sharpy.Bytes)`

Send all data to the socket, continuing until every byte is sent.

### `socket.recv(bufsize: int) -> Sharpy.Bytes`

Receive up to bufsize bytes from the socket.

### `socket.sendto(data: Sharpy.Bytes, address: tuple[string host, int port]) -> int`

Send data to a specific (host, port) address (UDP).

### `socket.setsockopt(level: int, optname: int, value: int)`

Set a socket option (e.g., SOL_SOCKET, SO_REUSEADDR).

### `socket.getsockopt(level: int, optname: int) -> int`

Get a socket option value.

### `socket.settimeout(timeout: float | None)`

Set the timeout in seconds for blocking operations, or None for blocking mode.

### `socket.gettimeout() -> float | None`

Return the timeout in seconds, or None if in blocking mode.

### `socket.setblocking(flag: bool)`

Set blocking (True) or non-blocking (False) mode.

### `socket.getblocking() -> bool`

Return whether the socket is in blocking mode.

### `socket.shutdown(how: int)`

Shut down one or both halves of the connection (SHUT_RD/WR/RDWR).

### `socket.close()`

Close the socket.

### `socket.fileno() -> int`

Return the socket handle (file descriptor) as an integer.

### `socket.enter() -> Socket`

### `socket.exit()`

### `socket.getdefaulttimeout() -> float | None`

Return the default timeout in seconds for new sockets, or None.

### `socket.setdefaulttimeout(timeout: float | None)`

Set the default timeout for new sockets. None means blocking mode.

### `socket.create_connection(address: tuple[string host, int port], timeout: float | None = default) -> Socket`

Connect to a TCP (host, port) address and return the connected socket.

### `socket.gethostname() -> str`

Return the hostname of the current machine.

### `socket.gethostbyname(hostname: str) -> str`

Resolve a hostname to an IPv4 address string.

### `socket.getfqdn() -> str`

Return the fully qualified domain name of the local host.

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

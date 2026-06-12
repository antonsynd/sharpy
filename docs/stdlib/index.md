# Standard Library Reference

Sharpy's standard library provides Python-familiar APIs backed by .NET implementations.

## Built-in Functions

[Built-in functions](builtins.md) available without any import: `abs()`, `all()`, `any()`, `ascii()`, `bin()`, `bool()`, `breakpoint()`, `checked_int_pow()`, `chr()`, `double()`, `enumerate()`, `filter()`, `float()`, `format()`, `format_align()`, and 32 more.

## Core Types

| Type | Description |
|------|-------------|
| [`list`](list.md) | A mutable sequence of elements, similar to Python's \`list\`. Supports negative indexing, slicing, and Python-style methods. |
| [`dict`](dict.md) | A mutable mapping of keys to values, similar to Python's dict. Supports Python-style methods like get(), pop(), items(), keys(), and values(). |
| [`set`](set.md) | A mutable set of unique elements, similar to Python's \`set\`. Supports set operations: union, intersection, difference, and symmetric difference. |
| [`complex`](complex.md) | A complex number type, similar to Python's complex. |

## Modules

| Module | Description |
|--------|-------------|
| [`argparse`](argparse.md) | Command-line argument parsing. |
| [`base64`](base64.md) | RFC 4648 base16, base32, base64, and base85 data encodings. |
| [`bisect`](bisect.md) | Array bisection algorithm for maintaining sorted lists. |
| [`calendar`](calendar.md) |  |
| [`collections`](collections.md) | Specialized container datatypes: ChainMap, Counter, Deque, DefaultDict, OrderedDict. |
| [`colorsys`](colorsys.md) |  |
| [`configparser`](configparser.md) | Configuration file parser similar to Python's configparser module. |
| [`copy`](copy.md) |  |
| [`csv`](csv.md) | CSV file reading and writing. |
| [`datetime`](datetime.md) | Classes for working with dates and times. |
| [`difflib`](difflib.md) |  |
| [`email`](email.md) | Email message creation and parsing. |
| [`fnmatch`](fnmatch.md) | Unix shell-style filename pattern matching. |
| [`fractions`](fractions.md) |  |
| [`functools`](functools.md) | Higher-order functions and operations on callable objects. |
| [`glob`](glob.md) | Unix shell-style pathname pattern expansion. |
| [`grapheme`](grapheme.md) | Unicode grapheme cluster iteration and string width calculation. |
| [`gzip`](gzip.md) | Support for gzip compressed files. |
| [`hashlib`](hashlib.md) | Secure hash and message digest algorithms. |
| [`heapq`](heapq.md) | Heap queue (priority queue) algorithm. |
| [`hmac`](hmac.md) | Keyed-hashing for message authentication (HMAC). |
| [`html`](html.md) | HTML processing module. |
| [`http`](http.md) | HTTP modules — status codes, connections, and responses. |
| [`io`](io.md) | Core tools for working with streams and file-like objects. |
| [`ipaddress`](ipaddress.md) | Functions to create and manipulate IPv4 and IPv6 addresses and networks. |
| [`itertools`](itertools.md) | Functions creating iterators for efficient looping. |
| [`json`](json.md) | JSON encoder and decoder. |
| [`logging`](logging.md) | Flexible event logging system for applications. |
| [`math`](math.md) | Mathematical functions. |
| [`numpy`](numpy.md) | Numerical computing with multi-dimensional arrays and mathematical operations. |
| [`operator`](operator.md) |  |
| [`os`](os.md) | Miscellaneous operating system interfaces. |
| [`pathlib`](pathlib.md) | Object-oriented filesystem paths. |
| [`platform`](platform.md) | Access to underlying platform's identifying data. |
| [`pprint`](pprint.md) |  |
| [`random`](random.md) | Generate pseudo-random numbers with various distributions. |
| [`re`](re.md) | Regular expression operations. |
| [`requests`](requests.md) | HTTP library for making requests (GET, POST, PUT, DELETE, etc.). |
| [`secrets`](secrets.md) | Generate cryptographically strong random numbers suitable for managing secrets. |
| [`shlex`](shlex.md) | Simple lexical analysis of shell-style syntaxes. |
| [`shutil`](shutil.md) | Utility functions for copying and removal of files and directory trees. |
| [`socket`](socket.md) | Low-level networking interface, similar to Python's socket module. The bulk of this module (constants, the \`socket\` wrapper class, the exception hierarchy, DNS helpers, and \`create_connection\`) is generated from \`src/Sharpy.Stdlib/spy/socket_module.spy\` into \`SocketModule.cs\`. The byte-order, inet, and \`getaddrinfo\` helpers below stay hand-written because they involve \`short\`/\`byte[]\` interop and runtime-constructed tuple lists that are cleaner to express directly in C#. |
| [`sqlite3`](sqlite3.md) | DB-API interface for SQLite databases. |
| [`statistics`](statistics.md) | Mathematical statistics functions. |
| [`string`](string.md) | Common string constants and operations. |
| [`struct`](struct.md) | Interpret bytes as packed binary data. |
| [`subprocess`](subprocess.md) | Subprocess management: spawn new processes, connect to their pipes, and obtain return codes. |
| [`sys`](sys.md) | System-specific parameters and functions. |
| [`tarfile`](tarfile.md) | Read and write tar archive files. |
| [`tempfile`](tempfile.md) | Generate temporary files and directories. |
| [`textwrap`](textwrap.md) | Text wrapping and filling. |
| [`threading`](threading.md) | Thread-based concurrency primitives. |
| [`time`](time.md) | Time access and conversions. |
| [`toml`](toml.md) | TOML configuration file parser and encoder. |
| [`unittest`](unittest.md) | The unittest module provides a Pythonic testing API that the Sharpy compiler transforms into xUnit test infrastructure during code generation. |
| [`urllib`](urllib.md) | URL parsing, quoting, and query string manipulation utilities. |
| [`uuid`](uuid.md) | UUID objects (universally unique identifiers) according to RFC 4122. |
| [`xml`](xml.md) | XML processing module (ElementTree API). |
| [`yaml`](yaml.md) | YAML parser and emitter. |
| [`zipfile`](zipfile.md) | Read and write ZIP archive files. |
| [`zlib`](zlib.md) | Compression and decompression using zlib. |
| [`zoneinfo`](zoneinfo.md) |  |

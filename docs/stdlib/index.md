# Standard Library Reference

Sharpy's standard library provides Python-familiar APIs backed by .NET implementations.

## Built-in Functions

[Built-in functions](builtins.md) available without any import: `abs()`, `all()`, `any()`, `ascii()`, `bin()`, `bool()`, `breakpoint()`, `chr()`, `double()`, `enumerate()`, `filter()`, `float()`, `format()`, `format_align()`, `format_float()`, and 31 more.

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
| [`argparse`](argparse.md) | Module exports for the argparse module. |
| [`base64`](base64.md) | Module exports for the base64 module. |
| [`bisect`](bisect.md) | Array bisection algorithm for maintaining sorted lists. |
| [`collections`](collections.md) | Specialized container datatypes: ChainMap, Counter, Deque, DefaultDict, OrderedDict. |
| [`configparser`](configparser.md) | Configuration file parser similar to Python's configparser module. |
| [`copy`](copy.md) |  |
| [`csv`](csv.md) | Module exports for the csv module. |
| [`datetime`](datetime.md) | Classes for working with dates and times. |
| [`fnmatch`](fnmatch.md) | Unix shell-style filename pattern matching. |
| [`functools`](functools.md) | Higher-order functions and operations on callable objects. |
| [`glob`](glob.md) | Unix shell-style pathname pattern expansion. |
| [`grapheme`](grapheme.md) | Unicode grapheme cluster iteration and string width calculation. |
| [`gzip`](gzip.md) | Support for gzip compressed files. |
| [`hashlib`](hashlib.md) | Module exports for the hashlib module. |
| [`heapq`](heapq.md) | Heap queue (priority queue) algorithm. |
| [`hmac`](hmac.md) | Module exports for the hmac module. |
| [`io`](io.md) | Module exports for the io module. |
| [`ipaddress`](ipaddress.md) |  |
| [`itertools`](itertools.md) | Functions creating iterators for efficient looping. |
| [`json`](json.md) | JSON encoder and decoder. |
| [`logging`](logging.md) | Module exports for the logging module. |
| [`math`](math.md) | Mathematical functions. |
| [`numpy`](numpy.md) |  |
| [`operator`](operator.md) |  |
| [`os`](os.md) | Miscellaneous operating system interfaces. |
| [`pathlib`](pathlib.md) | Module exports for the pathlib module. |
| [`platform`](platform.md) | Module exports for the platform module. |
| [`random`](random.md) | Generate pseudo-random numbers with various distributions. |
| [`re`](re.md) | Regular expression operations. |
| [`requests`](requests.md) |  |
| [`secrets`](secrets.md) | Module exports for the secrets module. |
| [`shlex`](shlex.md) | Module exports for the shlex module. |
| [`shutil`](shutil.md) | Utility functions for copying and removal of files and directory trees. |
| [`sqlite3`](sqlite3.md) | DB-API interface for SQLite databases. |
| [`statistics`](statistics.md) | Mathematical statistics functions. |
| [`string`](string.md) | Common string constants and operations. |
| [`struct`](struct.md) | Module exports for the struct module. |
| [`subprocess`](subprocess.md) | Module exports for the subprocess module. |
| [`sys`](sys.md) | System-specific parameters and functions. |
| [`tempfile`](tempfile.md) | Generate temporary files and directories. |
| [`textwrap`](textwrap.md) | Text wrapping and filling. |
| [`time`](time.md) | Time access and conversions. |
| [`toml`](toml.md) | TOML configuration file parser and encoder. |
| [`unittest`](unittest.md) | The unittest module provides a Pythonic testing API that the Sharpy compiler transforms into xUnit test infrastructure during code generation. |
| [`urllib`](urllib.md) | Module exports for the urllib module. |
| [`uuid`](uuid.md) | Module exports for the uuid module. |
| [`yaml`](yaml.md) | YAML parser and emitter. |
| [`zipfile`](zipfile.md) | Read and write ZIP archive files. |
| [`zlib`](zlib.md) | Compression and decompression using zlib. |

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
| [`argparse`](argparse.md) | A named group of arguments for organization in help text. Arguments still belong to the parent parser; groups are for help formatting. |
| [`base64`](base64.md) |  |
| [`bisect`](bisect.md) |  |
| [`collections`](collections.md) | A ChainMap groups multiple dictionaries together to create a single, updateable view. Like Python's collections.ChainMap. |
| [`configparser`](configparser.md) |  |
| [`copy`](copy.md) | Shallow and deep copy operations, similar to Python's \`copy\` module. |
| [`csv`](csv.md) | Reads CSV data and maps each row to a dictionary keyed by field names, similar to Python's \`csv.DictReader\`. |
| [`datetime`](datetime.md) | Represents a date (year, month, day). |
| [`fnmatch`](fnmatch.md) |  |
| [`functools`](functools.md) | Thread-safe memoization cache backing the \`@functools.lru_cache\` and \`@functools.cache\` decorators. |
| [`glob`](glob.md) | Unix-style pathname pattern expansion, similar to Python's glob module. Supports \`*\`, \`?\`, \`[seq]\`, and \`**\` patterns. |
| [`grapheme`](grapheme.md) | Grapheme cluster (user-perceived character) operations. Wraps \`System.Globalization.StringInfo\` for working with text at the level of what users perceive as a single character — including combining marks, emoji sequences, and ZWJ (zero-width joiner) sequences. |
| [`gzip`](gzip.md) |  |
| [`hashlib`](hashlib.md) | Represents a hash object that accumulates data and computes cryptographic hashes. Mirrors Python's hashlib hash object API. |
| [`heapq`](heapq.md) |  |
| [`hmac`](hmac.md) |  |
| [`io`](io.md) | In-memory text stream using a string buffer, similar to Python's io.StringIO. Extends TextWriter so it can be used anywhere a TextWriter is expected (e.g., csv module). |
| [`ipaddress`](ipaddress.md) |  |
| [`itertools`](itertools.md) | Itertools module — tools for creating iterators. |
| [`json`](json.md) | Python-compatible json module. Provides dumps/loads for string serialization and dump/load for file I/O. |
| [`logging`](logging.md) | A named logger that outputs messages at or above a configured level. Output format: LEVEL:name:message (written to stderr). |
| [`math`](math.md) |  |
| [`numpy`](numpy.md) | Interface implementations for \`NdArray{T}\` — \`IEnumerable&lt;T&gt;\`, \`ISized\`, structural equality, and conversion helpers. |
| [`operator`](operator.md) | Operator module — functions corresponding to the intrinsic operators of Python. |
| [`os`](os.md) | Common operations on pathnames. |
| [`pathlib`](pathlib.md) | Object-oriented filesystem path, similar to Python's pathlib.Path. Immutable — all mutation methods return new Path instances. |
| [`platform`](platform.md) | Represents the result of a platform.uname() call. Mirrors Python's platform.uname_result named tuple. |
| [`random`](random.md) |  |
| [`re`](re.md) | Wraps a .NET \`System.Text.RegularExpressions.Match\` with Python-compatible API. |
| [`requests`](requests.md) | Base class for all requests-related errors. Equivalent to Python's \`requests.RequestException\`. |
| [`secrets`](secrets.md) |  |
| [`shlex`](shlex.md) |  |
| [`shutil`](shutil.md) |  |
| [`sqlite3`](sqlite3.md) | Represents a connection to an SQLite database. |
| [`statistics`](statistics.md) | Exception raised for statistics-related errors, similar to Python's \`statistics.StatisticsError\`. |
| [`string`](string.md) |  |
| [`struct`](struct.md) | Pre-compiled struct format for repeated packing/unpacking operations. Corresponds to Python's struct.Struct class. |
| [`subprocess`](subprocess.md) |  |
| [`sys`](sys.md) | Provides access to system-specific parameters and functions, similar to Python's sys module. |
| [`tempfile`](tempfile.md) |  |
| [`textwrap`](textwrap.md) |  |
| [`time`](time.md) | Represents a time value as a named tuple of components, similar to Python's \`time.struct_time\`. |
| [`toml`](toml.md) |  |
| [`unittest`](unittest.md) | Marker type returned by unittest.assert_raises(). Implements IDisposable so the with-statement type checking passes. The compiler replaces the entire with-block with Xunit.Assert.Throws during codegen. |
| [`urllib`](urllib.md) | Result of \`UrllibModule.Urlparse\`. Contains the six components of a parsed URL: scheme, netloc, path, params, query, and fragment. |
| [`uuid`](uuid.md) |  |
| [`yaml`](yaml.md) | Holds the comments associated with a single key (in a mapping) or item (in a sequence) for YAML roundtrip preservation, mirroring ruamel.yaml's comment model. |
| [`zipfile`](zipfile.md) |  |
| [`zlib`](zlib.md) |  |

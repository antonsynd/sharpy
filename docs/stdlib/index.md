# Standard Library Reference

Sharpy's standard library provides Python-familiar APIs backed by .NET implementations.

## Built-in Functions

[Built-in functions](builtins.md) available without any import: `abs()`, `all()`, `any()`, `ascii()`, `bin()`, `bool()`, `breakpoint()`, `chr()`, `double()`, `enumerate()`, `filter()`, `float()`, `format()`, `format_align()`, `format_float()`, and 32 more.

## Core Types

| Type | Description |
|------|-------------|
| [`list`](list.md) | A mutable sequence of elements, similar to Python's `list`. Supports negative indexing, slicing, and Python-style methods. |
| [`dict`](dict.md) |  |
| [`set`](set.md) | A mutable set of unique elements, similar to Python's `set`. Supports set operations: union, intersection, difference, and symmetric difference. |
| [`str`](str.md) | Extension methods on `string` that provide Python string method equivalents under PascalCase names. The emitter's NameMangler converts `upper` to `Upper`, `lower` to `Lower`, etc. Generated code includes `using global::Sharpy;` which brings these extensions into scope so that `name.Upper()` compiles against C# `string`. |
| [`complex`](complex.md) | A complex number type, similar to Python's complex. |

## Modules

| Module | Description |
|--------|-------------|
| [`argparse`](argparse.md) | Python-compatible command-line argument parser. |
| [`bisect`](bisect.md) | Array bisection algorithm, similar to Python's bisect module. Provides functions to maintain a list in sorted order without having to sort the list after each insertion. |
| [`collections`](collections.md) | A ChainMap groups multiple dictionaries together to create a single, updateable view. Like Python's collections.ChainMap. |
| [`copy`](copy.md) | Shallow and deep copy operations, similar to Python's `copy` module. |
| [`csv`](csv.md) | Reads CSV data and maps each row to a dictionary keyed by field names, similar to Python's `csv.DictReader`. |
| [`datetime`](datetime.md) | Represents a date (year, month, day). |
| [`fnmatch`](fnmatch.md) | Unix filename pattern matching, matching Python's fnmatch module. |
| [`functools`](functools.md) | Higher-order functions and operations on callable objects, similar to Python's functools module. |
| [`glob`](glob.md) | Unix-style pathname pattern expansion, similar to Python's glob module. Supports `*`, `?`, `[seq]`, and `**` patterns. |
| [`hashlib`](hashlib.md) | Represents a hash object that accumulates data and computes cryptographic hashes. Mirrors Python's hashlib hash object API. |
| [`heapq`](heapq.md) | Heap queue algorithm (priority queue), similar to Python's heapq module. Implements a min-heap using a list as the underlying storage. |
| [`io`](io.md) | In-memory text stream using a string buffer, similar to Python's io.StringIO. Extends TextWriter so it can be used anywhere a TextWriter is expected (e.g., csv module). |
| [`itertools`](itertools.md) | Itertools module — tools for creating iterators. |
| [`json`](json.md) | Python-compatible json module. Provides dumps/loads for string serialization and dump/load for file I/O. |
| [`logging`](logging.md) | A named logger that outputs messages at or above a configured level. Output format: LEVEL:name:message (written to stderr). |
| [`math`](math.md) | Mathematical functions, similar to Python's math module. This module provides access to mathematical functions defined by the C standard. |
| [`operator`](operator.md) | Operator module — functions corresponding to the intrinsic operators of Python. |
| [`os`](os.md) | OS-level operations, similar to Python's os module. Wraps System.IO and System.Environment for file, directory, and environment operations. |
| [`pathlib`](pathlib.md) | Object-oriented filesystem path, similar to Python's pathlib.Path. Immutable — all mutation methods return new Path instances. |
| [`random`](random.md) | Pseudo-random number generators for various distributions, similar to Python's random module. |
| [`re`](re.md) | Wraps a .NET `System.Text.RegularExpressions.Match` with Python-compatible API. |
| [`shutil`](shutil.md) | High-level file operations, similar to Python's shutil module. Provides functions for copying, moving, and removing files and directory trees. |
| [`statistics`](statistics.md) | Mathematical statistics functions, similar to Python's `statistics` module. |
| [`string`](string.md) | String constants matching Python's string module. Provides character classification constants for ASCII characters. |
| [`sys`](sys.md) | Provides access to system-specific parameters and functions, similar to Python's sys module. |
| [`tempfile`](tempfile.md) | Temporary file and directory creation, similar to Python's tempfile module. |
| [`textwrap`](textwrap.md) | Text wrapping and filling, matching Python's textwrap module. |
| [`time`](time.md) | Represents a time value as a named tuple of components, similar to Python's `time.struct_time`. |

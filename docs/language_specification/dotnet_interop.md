# .NET Interop

## Importing .NET Types

```python
from system.collections.generic import List, Dictionary
from system.io import File, Path

# Use .NET types directly
# As of right now, this example is redundant because Sharpy
# uses the .NET collection types directly, e.g `list[T]`, so
# no explicit import is required.
items = List[int]()
items.add(42)

content = File.read_all_text("data.txt")
```

## .NET Properties

.NET properties accessed like Sharpy properties:

```python
from system.io import FileInfo

file = FileInfo("data.txt")
size = file.length
name = file.name
```

## Name Mapping (snake_case to PascalCase)

Sharpy uses Python-style `snake_case` naming, while .NET uses `PascalCase`. The compiler automatically maps between these conventions when accessing .NET members:

```python
from system import Console

# Sharpy snake_case maps to .NET PascalCase
Console.write_line("Hello")       # Calls System.Console.WriteLine("Hello")
Console.read_line()               # Calls System.Console.ReadLine()

from system.io import File
content = File.read_all_text("data.txt")  # Calls System.IO.File.ReadAllText(...)
```

This mapping applies to method names, property names, and static members. The compiler resolves `snake_case` identifiers to their `PascalCase` .NET equivalents at compile time.

## Extension Methods

.NET extension methods work naturally:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
```

## Overloaded Method Imports

When a .NET type has overloaded methods (multiple methods with the same name but different parameter signatures), importing the type makes all overloads available. The compiler resolves the correct overload at each call site based on the argument types:

```python
from system import Convert

# Convert.ToInt32 has many overloads; compiler picks the right one
n1 = Convert.to_int32("42")        # ToInt32(string)
n2 = Convert.to_int32(3.14)        # ToInt32(double)
n3 = Convert.to_int32(True)        # ToInt32(bool)
```

If the compiler cannot unambiguously resolve an overload, it reports a compile-time error listing the candidate overloads.

## IDisposable Pattern

.NET's `IDisposable` integrates with `with`:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
```

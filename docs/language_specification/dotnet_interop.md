# .NET Interop

## Importing .NET Types

```python
from system.collections.generic import List, Dictionary
from system.io import File, Path

# Use .NET types directly
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

## Extension Methods

.NET extension methods work naturally:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
```

## IDisposable Pattern

.NET's `IDisposable` integrates with `with`:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
```

Where a type implements both `IDisposable` from .NET and Sharpy's own `IContextManager`, the `IContextManager` is used to dictate the behavior within the `with`-block, e.g. calling the methods `Enter()` and `Exit()` (corresponding to the dunder methods `__enter__` and `__exit__`), rather than `Dispose()`.

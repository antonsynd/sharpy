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

`System.Linq.Enumerable`'s extension methods are available on any sequence, under their
`snake_case` names. No import is needed — `using System.Linq;` is always emitted:

```python
def main():
    numbers = [1, 2, 3, 4, 5]
    print(list(numbers.where(lambda x: x % 2 == 0)))   # [2, 4]
    print(list(numbers.select(lambda x: x * 2)))       # [2, 4, 6, 8, 10]
```

### Type arguments are inferred

The type arguments are inferred from the receiver and the arguments, so the call has a Sharpy type
and can be used wherever that type is expected — wrapped, annotated, or chained:

```python
from system.collections.generic import List


def main():
    lst: List[int] = List[int]()
    lst.add(3)
    lst.add(4)

    print(list(lst.select(lambda x: str(x))))                        # ['3', '4']
    print(list(lst.select(lambda x: x * 2).where(lambda y: y > 6)))   # [8]
```

Inference proceeds in stages: the receiver fixes what it determines, each lambda is then checked
with those types in place, and its return type fixes the rest. Three stages is the deepest any
method on the surface needs.

Write the type arguments explicitly when inference cannot reach them — `cast` and `of_type` are the
two whose result is determined by nothing else:

```python
from system.collections.generic import List


def main():
    lst: List[int] = List[int]()
    lst.add(3)
    print(list(lst.cast[int]()))   # [3]
```

### An instance member always wins

A name that exists as an instance member on the receiver resolves to that member, never to the
extension method — C#'s own rule. Several names are on both surfaces, and they mean different
things:

```python
def main():
    xs: list[int] = [1, 2, 3]
    xs.reverse()          # list.reverse() — in place, returns None
    print(xs)             # [3, 2, 1]
    print(xs.count(2))    # list.count(value) — occurrences, not length
    print("-".join(["a", "b"]))   # str.join, not Enumerable.Join
```

`reverse`, `count`, `index`, `contains`, `append`, `to_list`, `to_dictionary`, `to_hash_set`,
`union` and `join` all fall in this class. Which one binds depends on the receiver: a Sharpy `list`
has `reverse`, a raw `system.collections.generic.List` has `Reverse`, and both keep their own
meaning.

### Where inference cannot close, nothing changes

A call whose type arguments cannot be determined is left exactly as it was: no type is recorded, no
diagnostic is reported, and the emitted C# infers the vector itself. This is the normal outcome for
an ambiguous overload, for a result type Sharpy cannot represent (`order_by` returns
`IOrderedEnumerable<T>`), and for a receiver that is not a sequence:

```python
from system.collections.generic import List


def main():
    lst: List[int] = List[int]()
    lst.add(3)
    lst.add(1)
    for x in lst.order_by(lambda v: v):   # no Sharpy type, still iterates
        print(x)
```

Such a call cannot be wrapped or annotated, because there is no type to wrap. Only the explicit
spelling recovers that.

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

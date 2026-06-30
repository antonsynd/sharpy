# builtins

Functions available without any import.

## Functions

### `abs(x: int) -> int`

Return the absolute value of a number.
Python: `abs(x)`

**Parameters:**

- `x` (int) -- The number

**Returns:** The absolute value

```python
abs(-5)      # 5
abs(3)       # 3
abs(-2.5)    # 2.5
```

### `abs(x: long) -> long`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: float) -> float`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: float32) -> float32`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: decimal) -> decimal`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: short) -> short`

Return the absolute value of a number.
Python: `abs(x)`

### `abs(x: sbyte) -> sbyte`

Return the absolute value of a number.
Python: `abs(x)`

### `all(iterable: Iterable[T]) -> bool`

Return True if all elements of the iterable are True (or if the iterable is empty).

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to check

**Returns:** True if all elements are truthy, False otherwise

```python
all([True, True, True])    # True
all([True, False, True])   # False
all([])                    # True
```

### `any(iterable: Iterable[T]) -> bool`

Return True if any element of the iterable is True. If the iterable is empty, return False.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to check

**Returns:** True if any element is truthy, False otherwise

```python
any([False, False, True])    # True
any([0, 0, 0])              # False
any([])                      # False
```

### `ascii(obj: object) -> str`

Return a string with non-ASCII characters escaped.
Calls repr() first, then escapes non-ASCII characters with \xNN, \uNNNN, or \UNNNNNNNN.

```python
ascii("hello")      # "'hello'"
ascii("héllo")      # "'h\\xe9llo'"
```

### `bin(x: int) -> str`

Return a binary string prefixed with "0b".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** A binary string representation

```python
bin(10)     # "0b1010"
bin(-10)    # "-0b1010"
bin(0)      # "0b0"
```

### `bin(x: long) -> str`

Return a binary string prefixed with "0b" for long integers.

**Parameters:**

- `x` (long) -- The long integer to convert

**Returns:** A binary string representation

### `bool(b: bool) -> bool`

Convert a bool to bool (identity).

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** The same bool value

### `bool(d: decimal) -> bool`

Convert a decimal to bool. Returns False if zero, True otherwise.

**Parameters:**

- `d` (decimal) -- The decimal value

**Returns:** False if zero, True otherwise

### `bool(f: float32) -> bool`

Convert a float to bool. Returns False if zero, True otherwise.

**Parameters:**

- `f` (float32) -- The float value

**Returns:** False if zero, True otherwise

### `bool(d: float) -> bool`

Convert a double to bool. Returns False if zero, True otherwise.

**Parameters:**

- `d` (float) -- The double value

**Returns:** False if zero, True otherwise

### `bool(i: int) -> bool`

Convert an int to bool. Returns False if zero, True otherwise.

**Parameters:**

- `i` (int) -- The int value

**Returns:** False if zero, True otherwise

### `bool(u: uint) -> bool`

Convert a uint to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (uint) -- The uint value

**Returns:** False if zero, True otherwise

### `bool(s: short) -> bool`

Convert a short to bool. Returns False if zero, True otherwise.

**Parameters:**

- `s` (short) -- The short value

**Returns:** False if zero, True otherwise

### `bool(u: ushort) -> bool`

Convert a ushort to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (ushort) -- The ushort value

**Returns:** False if zero, True otherwise

### `bool(l: long) -> bool`

Convert a long to bool. Returns False if zero, True otherwise.

**Parameters:**

- `l` (long) -- The long value

**Returns:** False if zero, True otherwise

### `bool(u: ulong) -> bool`

Convert a ulong to bool. Returns False if zero, True otherwise.

**Parameters:**

- `u` (ulong) -- The ulong value

**Returns:** False if zero, True otherwise

### `bool(b: byte) -> bool`

Convert a byte to bool. Returns False if zero, True otherwise.

**Parameters:**

- `b` (byte) -- The byte value

**Returns:** False if zero, True otherwise

### `bool(s: sbyte) -> bool`

Convert an sbyte to bool. Returns False if zero, True otherwise.

**Parameters:**

- `s` (sbyte) -- The sbyte value

**Returns:** False if zero, True otherwise

### `bool(s: str) -> bool`

Convert a string to bool. Returns False if the string is None or empty, True otherwise.

**Parameters:**

- `s` (str) -- The string value

**Returns:** False if None or empty, True otherwise

### `bool(obj: object | None) -> bool`

Convert an arbitrary object to bool using Python's truth testing protocol.
Checks __bool__ (IBoolConvertible), then __len__ (ISized), then collection emptiness.
Non-None objects without these protocols are truthy.

**Parameters:**

- `obj` (object | None) -- The object to test for truthiness

**Returns:** The truth value of the object

```python
bool(0)        # False
bool(1)        # True
bool("")       # False
bool("hello")  # True
bool([])       # False
bool([1, 2])   # True
bool(None)     # False
```

### `breakpoint()`

Drop into the debugger. No-op when no debugger is attached.

!!! note
    Maps to `System.Diagnostics.Debugger.Break()`.
    When no debugger is attached, this method does nothing.

### `chr(i: int) -> str`

Return a string of one character whose Unicode code point is the integer i.
This is the inverse of ord().

**Parameters:**

- `i` (int) -- A Unicode code point (0 to 0x10FFFF)

**Returns:** A string of one character

```python
chr(65)     # "A"
chr(8364)   # "€"
chr(97)     # "a"
```

**Raises:**

- `ValueError` -- Thrown when i is out of range

### `double(b: bool) -> float`

Convert bool to double. True becomes 1.0, False becomes 0.0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1.0 for True, 0.0 for False

### `double(i: int) -> float`

Convert int to double

### `double(l: long) -> float`

Convert long to double

### `double(f: float32) -> float`

Convert float to double

### `double(d: float) -> float`

Convert double to double (identity)

### `double(m: decimal) -> float`

Convert decimal to double

### `double(s: str) -> float`

Parse string to double

### `double(b: byte) -> float`

Convert byte to double

### `double(sb: sbyte) -> float`

Convert sbyte to double

### `double(s: short) -> float`

Convert short to double

### `double(us: ushort) -> float`

Convert ushort to double

### `double(u: uint) -> float`

Convert uint to double

### `double(ul: ulong) -> float`

Convert ulong to double

### `enumerate(iterable: Iterable[T], start: int = 0) -> EnumerateIterator[T]`

Return an enumerate object. The iterable must be a sequence, an iterator,
or some other object which supports iteration. The elements produced by
enumerate are tuples containing a count (from start which defaults to 0)
and the values obtained from iterating over iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to enumerate
- `start` (int) -- The starting index (default 0)

**Returns:** An enumerate iterator

```python
for i, val in enumerate(["a", "b", "c"]):
    print(i, val)
# 0 a
# 1 b
# 2 c
```

### `filter(predicate: (T) -> bool, iterable: Iterable[T]) -> FilterIterator[T]`

Construct an iterator from those elements of iterable for which predicate is True.
If predicate is None, return the elements that are True.

**Parameters:**

- `predicate` ((T) -> bool) -- The predicate function to test each element
- `iterable` (Iterable[T]) -- The iterable to filter

**Returns:** A filter iterator

```python
list(filter(lambda x: x > 0, [-1, 0, 1, 2]))    # [1, 2]
list(filter(lambda s: len(s) > 3, ["hi", "hello"]))  # ["hello"]
```

### `float(b: bool) -> float`

Convert a bool to float. True becomes 1.0, False becomes 0.0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1.0 for True, 0.0 for False

### `float(i: int) -> float`

Convert an int to float.

**Parameters:**

- `i` (int) -- The int value

**Returns:** The value as a double

### `float(l: long) -> float`

Convert a long to float.

**Parameters:**

- `l` (long) -- The long value

**Returns:** The value as a double

### `float(f: float32) -> float`

Convert a float to double (widening).

**Parameters:**

- `f` (float32) -- The float value

**Returns:** The value as a double

### `float(d: float) -> float`

Convert a double to float (identity, since Python float maps to .NET double).

**Parameters:**

- `d` (float) -- The double value

**Returns:** The same double value

### `float(m: decimal) -> float`

Convert a decimal to float.

**Parameters:**

- `m` (decimal) -- The decimal value

**Returns:** The value as a double

### `float(s: str) -> float`

Parse a string to float.

**Parameters:**

- `s` (str) -- The string to parse

**Returns:** The parsed double value

```python
float("3.14")    # 3.14
float("42")      # 42.0
float("-1.5")    # -1.5
```

**Raises:**

- `ValueError` -- Thrown when the string cannot be parsed

### `format(value: object | None, format_spec: str = "") -> str`

Convert a value to a "formatted" representation, as controlled by format_spec.
The interpretation of format_spec will depend on the type of the value argument.

**Parameters:**

- `value` (object | None) -- The value to format
- `format_spec` (str)

**Returns:** The formatted string representation

```python
format(42, "d")        # "42"
format(3.14, ".1f")    # "3.1"
format(255, "x")       # "ff"
```

### `frozen_set(items): Iterable[T] = > new(items) -> FrozenSet[T]`

Return a new frozenset object, optionally with elements taken from iterable.

### `frozen_set()) -> FrozenSet[T]`

Return a new empty frozenset object.

### `hash(obj: object) -> int`

Return the hash value of an object.
Calls `object.GetHashCode()` on the given object.

**Parameters:**

- `obj` (object) -- The object to hash

**Returns:** The hash value as an integer

```python
hash("hello")    # integer hash value
hash(42)         # 42
```

**Raises:**

- `TypeError` -- Thrown when *obj* is null

### `hex(x: int) -> str`

Return a lowercase hexadecimal string prefixed with "0x".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** A hexadecimal string representation

```python
hex(255)    # "0xff"
hex(-42)    # "-0x2a"
hex(0)      # "0x0"
```

### `hex(x: long) -> str`

Return a lowercase hexadecimal string prefixed with "0x" for long integers.

**Parameters:**

- `x` (long) -- The long integer to convert

**Returns:** A hexadecimal string representation

### `id(obj: object) -> int`

Return the identity of an object.
This is an integer which is guaranteed to be unique and constant
for this object during its lifetime.
Maps to RuntimeHelpers.GetHashCode() which returns the sync block index.

**Parameters:**

- `obj` (object) -- The object to get the identity of

**Returns:** An integer uniquely identifying the object during its lifetime

```python
x = [1, 2, 3]
id(x)    # unique integer identity
```

**Raises:**

- `TypeError` -- Thrown when *obj* is null

### `input() -> str`

Read a line from standard input.

**Returns:** The input string (without trailing newline)

```python
name = input("Enter your name: ")
print("Hello, " + name)
```

### `input(prompt: str) -> str`

Read a line from standard input after printing a prompt.

**Parameters:**

- `prompt` (str) -- The prompt to display

**Returns:** The input string (without trailing newline)

### `int(b: bool) -> int`

Convert bool to int. True becomes 1, False becomes 0.

**Parameters:**

- `b` (bool) -- The bool value

**Returns:** 1 for True, 0 for False

```python
int(True)      # 1
int(False)     # 0
int(3.9)       # 3 (truncates)
int("42")      # 42
```

### `int(i: int) -> int`

Convert int to int (identity)

### `int(l: long) -> int`

Convert long to int

### `int(f: float32) -> int`

Convert float to int (truncates)

### `int(d: float) -> int`

Convert double to int (truncates)

### `int(m: decimal) -> int`

Convert decimal to int (truncates)

### `int(s: str) -> int`

Parse string to int

### `int(b: byte) -> int`

Convert byte to int

### `int(sb: sbyte) -> int`

Convert sbyte to int

### `int(s: short) -> int`

Convert short to int

### `int(us: ushort) -> int`

Convert ushort to int

### `int(u: uint) -> int`

Convert uint to int

### `int(ul: ulong) -> int`

Convert ulong to int

### `isinstance(obj: object | None) -> bool`

Return True if the object argument is an instance of the classinfo argument.

**Parameters:**

- `obj` (object | None) -- The object to check

**Returns:** True if obj is an instance of T, False otherwise

```python
isinstance(42, int)           # True
isinstance("hello", str)      # True
isinstance(42, str)           # False
```

### `isinstance(obj: object | None, class_info: Type) -> bool`

Return True if the object argument is an instance of the classinfo argument.
This overload accepts the type as a parameter for runtime type checking.

**Parameters:**

- `obj` (object | None) -- The object to check
- `class_info` (Type)

**Returns:** True if obj is an instance of classInfo, False otherwise

### `isinstance(obj: object | None, class_info: list[Type]) -> bool`

Return True if the object argument is an instance of any of the types in classInfo.

**Parameters:**

- `obj` (object | None) -- The object to check
- `class_info` (list[Type])

**Returns:** True if obj is an instance of any type in classInfo, False otherwise

### `issubclass(cls: Type, class_info: Type) -> bool`

Return True if class is a subclass of classinfo. A class is considered
a subclass of itself.

**Parameters:**

- `cls` (Type) -- The class to check
- `class_info` (Type)

**Returns:** True if cls is a subclass of classInfo, False otherwise

```python
issubclass(bool, int)    # True
issubclass(int, str)     # False
```

### `issubclass(cls: Type, class_info: list[Type]) -> bool`

Return True if class is a subclass of any of the types in classInfo.

**Parameters:**

- `cls` (Type) -- The class to check
- `class_info` (list[Type])

**Returns:** True if cls is a subclass of any type in classInfo, False otherwise

### `iter(enumerable: Iterable[T]) -> Iterator[T]`

Return an iterator object from any C# enumerable.

**Parameters:**

- `enumerable` (Iterable[T]) -- The C# enumerable to get an iterator from.

**Returns:** An iterator for the enumerable.

```python
it = iter([1, 2, 3])
next(it)    # 1
next(it)    # 2
```

!!! note
    Wraps the enumerator using EnumeratorIterator.
    This allows any C# IEnumerable to work seamlessly with Sharpy's iterator protocol.

**Raises:**

- `TypeError` -- Thrown when enumerable is null.

### `len(c: ICollection) -> int`

Return the length (the number of items) of a collection.

```python
len([1, 2, 3])    # 3
len("hello")      # 5
len({})           # 0
```

!!! note
    Uses the non-generic `ICollection` interface
    which is implemented by arrays, List{T}, Dictionary{K,V}, etc.
    This avoids overload ambiguity when a type implements both
    `ICollection{T}` and `IReadOnlyCollection{T}`.

**Raises:**

- `TypeError` -- Thrown when *c* is null

### `len(sized: ISized) -> int`

Return the length of an ISized type (user-defined types with __len__).

**Parameters:**

- `sized` (ISized) -- An object implementing `ISized`

**Returns:** The number of elements

**Raises:**

- `TypeError` -- Thrown when *sized* is null

### `len(list: list[T]) -> int`

Return the length of a Sharpy list.

!!! note
    This concrete overload disambiguates between the
    `ICollection` and `ISized`
    overloads, both of which `Sharpy.List{T}` now satisfies (it
    implements the non-generic `IList`).
    An identity conversion to the concrete parameter type is preferred
    over the interface conversions, so this overload wins.

### `len(dict: dict[K, V]) -> int`

Return the length of a Sharpy dictionary.

!!! note
    This concrete overload disambiguates between the
    `ICollection` and `ISized`
    overloads, both of which `Dict{K, V}` now satisfies (it
    implements the non-generic `IDictionary`).

### `len(s: str) -> int`

Return the length of a string.

### `len(tuple: Runtime.CompilerServices.ITuple) -> int`

Return the number of elements in a tuple.

!!! note
    Tuples are emitted as `System.ValueTuple` instances, which
    implement `System.Runtime.CompilerServices.ITuple` but
    neither `ICollection` nor
    `ISized`. This overload routes `len(tuple)` to
    `System.Runtime.CompilerServices.ITuple.Length`.

### `list(enumerable: Iterable[T]) -> list[T]`

Convert IEnumerable to list

### `list() -> list[T]`

Create empty list

### `list(other: list[T]) -> list[T]`

Convert list to list (copy)

### `long(b: bool) -> long`

Convert bool to long. True becomes 1, False becomes 0.

### `long(i: int) -> long`

Convert int to long (widening)

### `long(l: long) -> long`

Convert long to long (identity)

### `long(f: float32) -> long`

Convert float to long (truncates)

### `long(d: float) -> long`

Convert double to long (truncates)

### `long(m: decimal) -> long`

Convert decimal to long (truncates)

### `long(s: str) -> long`

Parse string to long

### `long(b: byte) -> long`

Convert byte to long

### `long(sb: sbyte) -> long`

Convert sbyte to long

### `long(s: short) -> long`

Convert short to long

### `long(us: ushort) -> long`

Convert ushort to long

### `long(u: uint) -> long`

Convert uint to long

### `long(ul: ulong) -> long`

Convert ulong to long

### `map(function: (TIn) -> TOut, iterable: Iterable[TIn]) -> MapIterator[TIn, TOut]`

Return an iterator that applies function to every item of iterable, yielding the results.

**Parameters:**

- `function` ((TIn) -> TOut) -- The function to apply to each element
- `iterable` (Iterable[TIn]) -- The iterable to map over

**Returns:** A map iterator

```python
list(map(lambda x: x * 2, [1, 2, 3]))    # [2, 4, 6]
list(map(str, [1, 2, 3]))                 # ["1", "2", "3"]
```

### `map(function: (T1, T2) -> TOut, iterable1: Iterable[T1], iterable2: Iterable[T2], strict: bool = False) -> MapIterator[T1, T2, TOut]`

Return an iterator that applies a two-argument function to corresponding items of two
iterables. With *strict* True, raises ValueError if the iterables have
different lengths (Python 3.14 behaviour); otherwise stops at the shortest.

```python
list(map(lambda a, b: a + b, [1, 2], [10, 20]))             # [11, 22]
list(map(lambda a, b: a + b, [1, 2], [10], strict=True))    # ValueError
```

### `map(function: (T1, T2, T3) -> TOut, iterable1: Iterable[T1], iterable2: Iterable[T2], iterable3: Iterable[T3], strict: bool = False) -> MapIterator[T1, T2, T3, TOut]`

Return an iterator that applies a three-argument function to corresponding items of three
iterables. With *strict* True, raises ValueError if the iterables have
different lengths; otherwise stops at the shortest.

### `max(iterable: Iterable[T]) -> T`

Return the largest item in an iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to search

**Returns:** The largest item

```python
max([1, 5, 3])       # 5
max("abc")           # "c"
```

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `max(iterable: Iterable[T], key: (T) -> TKey) -> T`

Return the largest item in an iterable, using a key function for comparison.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to search
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** The largest item according to the key function

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `max(iterable: Iterable[T], @default: T) -> T`

Return the largest item in an iterable, or default if the iterable is empty.

### `max(iterable: Iterable[T], key: (T) -> TKey, @default: T) -> T`

Return the largest item in an iterable using a key function,
or default if the iterable is empty.

### `max(first: T, second: T, rest: list[T]) -> T`

Return the largest of two or more values (the variadic value form).

**Parameters:**

- `first` (T) -- The first value
- `second` (T) -- The second value
- `rest` (list[T]) -- Any additional values

**Returns:** The largest value (the first encountered on ties, matching Python)

```python
max(2, 3, 1)     # 3
max(5, 2, 8, 1)  # 8
```

!!! note
    The `key=` form of this variadic value call (e.g. `max(a, b, key=f)`) is
    supported: the compiler lowers it to the iterable+key overload
    `Max&lt;T, TKey&gt;(IEnumerable&lt;T&gt;, Func&lt;T, TKey&gt;)` by wrapping the
    positional values in an array, because a C# `params` parameter must come last and
    cannot coexist with a by-keyword `key` (#1012).

### `min(iterable: Iterable[T]) -> T`

Return the smallest item in an iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to search

**Returns:** The smallest item

```python
min([1, 5, 3])       # 1
min("abc")           # "a"
```

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `min(iterable: Iterable[T], key: (T) -> TKey) -> T`

Return the smallest item in an iterable, using a key function for comparison.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to search
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** The smallest item according to the key function

**Raises:**

- `ValueError` -- Thrown when the iterable is empty

### `min(iterable: Iterable[T], @default: T) -> T`

Return the smallest item in an iterable, or default if the iterable is empty.

### `min(iterable: Iterable[T], key: (T) -> TKey, @default: T) -> T`

Return the smallest item in an iterable using a key function,
or default if the iterable is empty.

### `min(first: T, second: T, rest: list[T]) -> T`

Return the smallest of two or more values (the variadic value form).

**Parameters:**

- `first` (T) -- The first value
- `second` (T) -- The second value
- `rest` (list[T]) -- Any additional values

**Returns:** The smallest value (the first encountered on ties, matching Python)

```python
min(2, 3)        # 2
min(5, 2, 8, 1)  # 1
```

!!! note
    The `key=` form of this variadic value call (e.g. `min(a, b, key=f)`) is
    supported: the compiler lowers it to the iterable+key overload
    `Min&lt;T, TKey&gt;(IEnumerable&lt;T&gt;, Func&lt;T, TKey&gt;)` by wrapping the
    positional values in an array, because a C# `params` parameter must come last and
    cannot coexist with a by-keyword `key` (#1012).

### `next(iterator: Iterator[T]) -> T`

Retrieve the next item from the iterator by calling its Next() method.
If the iterator is exhausted, a StopIteration exception is raised.

**Parameters:**

- `iterator` (Iterator[T]) -- The iterator to advance

**Returns:** The next item from the iterator

```python
it = iter([1, 2, 3])
next(it)    # 1
next(it)    # 2
next(it)    # 3
```

**Raises:**

- `StopIteration` -- Thrown when the iterator is exhausted

### `next(iterator: Iterator[T], @default: T) -> T`

Retrieve the next item from the iterator, or return default if exhausted.

**Parameters:**

- `iterator` (Iterator[T]) -- The iterator to advance
- `@default` (T)

**Returns:** The next item, or default if exhausted

```python
it = iter([1])
next(it)          # 1
next(it, "done")  # "done"
```

### `oct(x: int) -> str`

Return an octal string prefixed with "0o".

**Parameters:**

- `x` (int) -- The integer to convert

**Returns:** An octal string representation

```python
oct(8)      # "0o10"
oct(-8)     # "-0o10"
oct(0)      # "0o0"
```

### `oct(x: long) -> str`

Return an octal string prefixed with "0o" for long integers.

**Parameters:**

- `x` (long) -- The long integer to convert

**Returns:** An octal string representation

### `open(path: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file

**Returns:** A TextFile in read mode with UTF-8 encoding

```python
f = open("file.txt")
f = open("output.txt", "w")
f = open("data.txt", "r", "utf-8")
```

### `open(path: str, mode: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file
- `mode` (str) -- File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)

**Returns:** A TextFile with UTF-8 encoding

### `open(path: str, mode: str, encoding: str) -> TextFile`

Open a file and return a file object.

**Parameters:**

- `path` (str) -- Path to the file
- `mode` (str) -- File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)
- `encoding` (str) -- Text encoding name (e.g., "utf-8", "ascii")

**Returns:** A TextFile with the specified mode and encoding

### `ord(s: str) -> int`

Return the Unicode code point for a one-character string.
This is the inverse of chr().

**Parameters:**

- `s` (str) -- A one-character string

**Returns:** The Unicode code point of the character

```python
ord("A")    # 65
ord("€")    # 8364
ord("a")    # 97
```

**Raises:**

- `TypeError` -- Thrown when the string is not exactly one character

### `pow(x: float, y: float) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (float) -- The base
- `y` (float) -- The exponent

**Returns:** x raised to the power y

```python
pow(2, 3)      # 8.0
pow(4, 0.5)    # 2.0
pow(10, -1)    # 0.1
```

### `pow(x: int, y: int) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (int) -- The base
- `y` (int) -- The exponent

**Returns:** x raised to the power y

### `pow(x: long, y: long) -> float`

Return x raised to the power y.

**Parameters:**

- `x` (long) -- The base
- `y` (long) -- The exponent

**Returns:** x raised to the power y

### `pow(x: float32, y: float32) -> float32`

Return x raised to the power y.

**Parameters:**

- `x` (float32) -- The base
- `y` (float32) -- The exponent

**Returns:** x raised to the power y

### `checked_int_pow(x: int, y: int) -> int`

Return x raised to the power y as an exact `int` using
checked exponentiation-by-squaring. Unlike `Pow(int, int)`,
this does not route through floating-point and therefore never silently
loses precision or saturates: an out-of-range result raises
`OverflowError`, matching Python's "diagnose, don't saturate"
semantics for fixed-width integers.

**Parameters:**

- `x` (int) -- The base.
- `y` (int) -- The exponent. Must be non-negative; negative exponents
are handled by the caller's floating-point path (e.g. `2 ** -1 == 0.5`).

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in an `int`.
- `System.ArgumentOutOfRangeException` -- y is negative.

### `checked_int_pow(x: long, y: long) -> long`

Return x raised to the power y as an exact `long` using
checked exponentiation-by-squaring. See `CheckedIntPow(int, int)`
for semantics; an out-of-range result raises `OverflowError`.

**Parameters:**

- `x` (long) -- The base.
- `y` (long) -- The exponent. Must be non-negative.

**Returns:** x raised to the power y.

**Raises:**

- `OverflowError` -- The result does not fit in a `long`.
- `System.ArgumentOutOfRangeException` -- y is negative.

### `range(stop: int) -> RangeIterator`

Return an iterator that produces integers from 0 up to (but not including) stop.

**Parameters:**

- `stop` (int) -- The stopping value (exclusive)

**Returns:** A range iterator

```python
list(range(5))         # [0, 1, 2, 3, 4]
list(range(2, 5))      # [2, 3, 4]
list(range(0, 10, 2))  # [0, 2, 4, 6, 8]
```

### `range(start: int, stop: int) -> RangeIterator`

Return an iterator that produces integers from start up to (but not including) stop.

**Parameters:**

- `start` (int) -- The starting value
- `stop` (int) -- The stopping value (exclusive)

**Returns:** A range iterator

### `range(start: int, stop: int, step: int) -> RangeIterator`

Return an iterator that produces integers from start up to (but not including) stop,
incrementing by step.

**Parameters:**

- `start` (int) -- The starting value
- `stop` (int) -- The stopping value (exclusive)
- `step` (int) -- The step value

**Returns:** A range iterator

**Raises:**

- `ValueError` -- Thrown when *step* is zero

### `repr(obj: object | None) -> str`

Return a string containing a printable representation of an object.

**Parameters:**

- `obj` (object | None) -- The object to get the representation of

**Returns:** A printable string representation

```python
repr("hello")      # "'hello'"
repr([1, 2, 3])    # "[1, 2, 3]"
repr(None)         # "None"
```

!!! note
    Uses `object.ToString()` to get the representation.
    Sharpy types (List, Set, Dict) override ToString() to produce
    Python-compatible repr output (e.g., "[1, 2, 3]", "{1, 2}", etc.).
    Strings are wrapped in single quotes, matching Python's repr().

### `reversed(sequence: Iterable[T]) -> Iterator[T]`

Return a reverse iterator over the values of the given sequence.

**Parameters:**

- `sequence` (Iterable[T]) -- The sequence to reverse

**Returns:** An iterator that yields elements in reverse order

```python
list(reversed([1, 2, 3]))    # [3, 2, 1]
list(reversed("abc"))        # ["c", "b", "a"]
```

!!! note
    For `IList{T}` implementations, iterates backwards efficiently.
    For other sequences, materializes the sequence and reverses using LINQ.

**Raises:**

- `TypeError` -- Thrown when *sequence* is null

### `reversed(reversible: IReverseEnumerable[T]) -> Iterator[T]`

Return a reverse iterator for types that implement `IReverseEnumerable{T}`
but not `IEnumerable{T}` (i.e., types with __reversed__ but no __iter__).

### `round(x: float) -> int`

Round a number to the nearest integer.

**Parameters:**

- `x` (float) -- The number to round

**Returns:** The rounded value

```python
round(3.7)       # 4
round(2.5)       # 2 (banker's rounding)
round(3.14159, 2) # 3.14
```

!!! note
    Uses .NET's banker's rounding (round half to even). For example, Round(2.5) returns 2, not 3.

### `round(x: float, n: int) -> float`

Round a number to n decimal places.

**Parameters:**

- `x` (float) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: float32) -> int`

Round a float to the nearest integer.

**Parameters:**

- `x` (float32) -- The number to round

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: float32, n: int) -> float32`

Round a float to n decimal places.

**Parameters:**

- `x` (float32) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: decimal) -> int`

Round a decimal to the nearest integer.

**Parameters:**

- `x` (decimal) -- The number to round

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `round(x: decimal, n: int) -> decimal`

Round a decimal to n decimal places.

**Parameters:**

- `x` (decimal) -- The number to round
- `n` (int) -- The number of decimal places

**Returns:** The rounded value

!!! note
    Uses .NET's banker's rounding (round half to even).

### `set(enumerable: Iterable[T]) -> set[T]`

Convert IEnumerable to set

### `set() -> set[T]`

Create empty set

### `set(other: set[T]) -> set[T]`

Convert set to set (copy)

### `sorted(iterable: Iterable[T]) -> list[T]`

Return a new sorted list from the items in iterable.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to sort

**Returns:** A new sorted list

```python
sorted([3, 1, 2])              # [1, 2, 3]
sorted("cab")                  # ["a", "b", "c"]
sorted([3, 1, 2], reverse=True) # [3, 2, 1]
```

### `sorted(iterable: Iterable[T], key: (T) -> TKey) -> list[T]`

Return a new sorted list using a key function for comparison.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to sort
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element

**Returns:** A new sorted list

### `sorted(iterable: Iterable[T], reverse: bool) -> list[T]`

Return a new sorted list, optionally in reverse order.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to sort
- `reverse` (bool) -- If True, sort in descending order

**Returns:** A new sorted list

### `sorted(iterable: Iterable[T], key: (T) -> TKey, reverse: bool) -> list[T]`

Return a new sorted list using a key function, optionally in reverse order.

**Parameters:**

- `iterable` (Iterable[T]) -- The iterable to sort
- `key` ((T) -> TKey) -- A function to extract a comparison key from each element
- `reverse` (bool) -- If True, sort in descending order

**Returns:** A new sorted list

### `str(x: object) -> str`

Convert an arbitrary object to its string representation.
Returns `"None"` for None, Python-style `"True"`/`"False"`
for booleans, and `object.ToString` for everything else.

**Parameters:**

- `x` (object) -- The object to convert

**Returns:** The string representation

```python
str(42)        # "42"
str(3.14)      # "3.14"
str(True)      # "True"
str(None)      # "None"
```

### `str(s: str) -> str`

Return the string unchanged.

### `str(c: char) -> str`

Convert a `char` to string without boxing.

### `str(i: int) -> str`

Convert an `int` to string without boxing.

### `str(l: long) -> str`

Convert a `long` to string without boxing.

### `str(d: float) -> str`

Convert a `double` to string without boxing.
Formats with Python-compatible trailing `.0` for whole numbers.

### `str(f: float32) -> str`

Convert a `float` to string without boxing.
Formats with Python-compatible trailing `.0` for whole numbers.

### `format_float(value: float) -> str`

Format a floating-point value with Python-compatible representation.
NaN, Infinity, and -Infinity use Python's lowercase forms.
Whole-number values get a trailing `.0`.
NOTE: Keep in sync with `FormatFloat(float)` overload.

### `format_float(value: float32) -> str`

Format a `float` value with Python-compatible representation.
Overload to avoid float→double widening precision issues.
NOTE: Keep in sync with `FormatFloat(double)` overload.

### `str(b: bool) -> str`

Convert a `bool` to string.
Returns Python-style `"True"` or `"False"`.

### `sum(iterable: Iterable[int]) -> int`

Sums a sequence of integers.

**Parameters:**

- `iterable` (Iterable[int]) -- The sequence to sum

**Returns:** The total sum

```python
sum([1, 2, 3])       # 6
sum(range(10))       # 45
sum([])              # 0
```

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: Iterable[long]) -> long`

Sums a sequence of longs.

**Parameters:**

- `iterable` (Iterable[long]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: Iterable[float32]) -> float32`

Sums a sequence of floats.

**Parameters:**

- `iterable` (Iterable[float32]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: Iterable[float]) -> float`

Sums a sequence of doubles.

**Parameters:**

- `iterable` (Iterable[float]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: Iterable[decimal]) -> decimal`

Sums a sequence of decimals.

**Parameters:**

- `iterable` (Iterable[decimal]) -- The sequence to sum

**Returns:** The total sum

**Raises:**

- `TypeError` -- Thrown when *iterable* is null

### `sum(iterable: Iterable[int], start: int) -> int`

Sums a sequence of integers with a start value.

**Parameters:**

- `iterable` (Iterable[int]) -- The sequence to sum
- `start` (int) -- The initial accumulator value

**Returns:** The total sum plus start

### `sum(iterable: Iterable[long], start: long) -> long`

Sums a sequence of longs with a start value.

### `sum(iterable: Iterable[float32], start: float32) -> float32`

Sums a sequence of floats with a start value.

### `sum(iterable: Iterable[float], start: float) -> float`

Sums a sequence of doubles with a start value.

### `sum(iterable: Iterable[decimal], start: decimal) -> decimal`

Sums a sequence of decimals with a start value.

### `type(obj: object | None) -> Type`

Return the type of an object.

**Parameters:**

- `obj` (object | None) -- The object to get the type of

**Returns:** The type of the object

```python
type(42)        # <class 'int'>
type("hello")   # <class 'str'>
type([1, 2])    # <class 'list'>
```

### `zip(iterable1: Iterable[T1], iterable2: Iterable[T2]) -> ZipIterator[T1, T2]`

Make an iterator that aggregates elements from two iterables.
Returns an iterator of tuples, where the i-th tuple contains the i-th element
from each of the argument sequences. The iterator stops when the shortest
input iterable is exhausted.

**Parameters:**

- `iterable1` (Iterable[T1]) -- The first iterable
- `iterable2` (Iterable[T2]) -- The second iterable

**Returns:** A zip iterator

```python
list(zip([1, 2, 3], ["a", "b", "c"]))    # [(1, "a"), (2, "b"), (3, "c")]
list(zip([1, 2], [10, 20, 30]))           # [(1, 10), (2, 20)]
```

### `zip(iterable1: Iterable[T1], iterable2: Iterable[T2], strict: bool) -> ZipIterator[T1, T2]`

Make an iterator that aggregates elements from two iterables.
When strict is True, raises ValueError if iterables have different lengths.

**Parameters:**

- `iterable1` (Iterable[T1]) -- The first iterable
- `iterable2` (Iterable[T2]) -- The second iterable
- `strict` (bool) -- If True, raises ValueError when iterables have different lengths

**Returns:** A zip iterator

### `zip(iterable1: Iterable[T1], iterable2: Iterable[T2], iterable3: Iterable[T3]) -> ZipIterator[T1, T2, T3]`

Make an iterator that aggregates elements from three iterables.
Returns an iterator of tuples, where the i-th tuple contains the i-th element
from each of the argument sequences. The iterator stops when the shortest
input iterable is exhausted.

**Parameters:**

- `iterable1` (Iterable[T1]) -- The first iterable
- `iterable2` (Iterable[T2]) -- The second iterable
- `iterable3` (Iterable[T3]) -- The third iterable

**Returns:** A zip iterator

### `zip(iterable1: Iterable[T1], iterable2: Iterable[T2], iterable3: Iterable[T3], strict: bool) -> ZipIterator[T1, T2, T3]`

Make an iterator that aggregates elements from three iterables.
When strict is True, raises ValueError if iterables have different lengths.

**Parameters:**

- `iterable1` (Iterable[T1]) -- The first iterable
- `iterable2` (Iterable[T2]) -- The second iterable
- `iterable3` (Iterable[T3]) -- The third iterable
- `strict` (bool) -- If True, raises ValueError when iterables have different lengths

**Returns:** A zip iterator

### `len(obj: object) -> int`

Get the length of a collection or string.
This is the fallback overload for dynamically-typed scenarios.

**Parameters:**

- `obj` (object) -- The object to measure

**Returns:** The number of elements

**Raises:**

- `TypeError` -- Thrown when *obj* is null or has no len()

### `format_align(value: str, width: int, fill: char, alignment: char) -> str`

Aligns a string within a field of given width using the specified fill character
and alignment mode. Used by f-string format spec codegen for custom fill characters
and center-alignment.

**Parameters:**

- `value` (str) -- The string to align
- `width` (int) -- The total field width
- `fill` (char) -- The fill character for padding
- `alignment` (char) -- Alignment mode: '<' left, '>' right, '^' center, '=' numeric sign-aware

**Returns:** The aligned string, or *value* unchanged if already wider than *width*

### `print(values: list[object | None])`

Print values to standard output, matching Python's print() behavior.
Values are converted to strings using ToString() and separated by the separator.

**Parameters:**

- `values` (list[object | None]) -- Values to print

```python
print("hello")           # hello
print(1, 2, 3)           # 1 2 3
print("a", "b", sep=",") # a,b
```

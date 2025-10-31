# Object Model

> **⚠️ Documentation Reorganization Notice**
>
> This document has been reorganized into three focused documents:
> - **[Language Reference](language_reference.md)** - Syntax and usage
> - **[Type System](type_system.md)** - Type semantics and protocols
> - **[Compiler Design](compiler_design.md)** - Implementation details
>
> This file is retained for reference but may not be kept up to date.
> See [README_MIGRATION.md](README_MIGRATION.md) for the new structure.

## Overview

Sharpy's object model is a hybrid that bridges Python's
dynamic object semantics with .NET's static type system.
All Sharpy types ultimately derive from the .NET type
hierarchy, with `System.Object` at the root. This allows
seamless interoperability with .NET libraries while
maintaining Pythonic syntax and semantics.

The object model is designed with the following principles:
1. **Static typing by default**: All types are statically
known at compile time.
2. **Protocol-oriented design**: Interfaces (protocols)
over inheritance.
3. **Zero-cost abstractions**: Python semantics should
compile to efficient .NET IL.
4. **Automatic conversions at boundaries**: Seamless
interop at .NET/Sharpy boundaries.
5. **Dunder method synthesis**: Automatic generation of
.NET operator overloads and other interface-oriented
behavior from Python-style dunder methods.

## Objects

The .NET top-level object `System.Object` is the base class
of all Sharpy builtin types, with `ValueType` as an
intermediary for `struct` (a.k.a. value types) in Sharpy.

Member operators that are defined and have a C# static
equivalent, e.g. `__add__` and `operator +`, will cause the
C# static equivalent to be automatically synthesized. This
allows virtual dispatch at runtime to subclass overrides of
an operator. It is possible for the user to manually define
the corresponding static operator.

No user-defined object members or methods can take the
form of `__[A-Za-z0-9_]*__`, i.e. no user-defined member
or method on a object can look like a dunder method. The
exception to this is if it is used as an override or
overload of the corresponding dunder method, since
most dunder methods are virtual (e.g. the operators) and
can be overloaded.

All Sharpy objects have an automatically synthesized
static `==` operator delegating to the `equals()` method
from the .NET object model. If there are any overloads,
their static equivalents are also synthesized.

This creates one subtle difference for .NET programmers
where even though a Sharpy type doesn't explicitly define
the static `==` operator, using `==` with it will invoke
equality checking (assuming `equals()` is overridden with
equality checking behavior), rather than reference checking.

For Python developers, this difference does not exist
because it is exactly how it works in Python.

| Concept | C#/.NET | Python | Sharpy | Notes |
| - | - | - | - | - |
| Constructor | `Foo(...)` | `def __init__(self, ...)` | `def __init__(self, ...)` | Unlike Python, Sharpy constructors can have overloads. It is a compile-time error to provide a return type as it is implied. |
| Destructor/finalizer | `~Foo()` | `__del__(self)` | `__del__(self)` | TODO |
| String representation | `string ToString()` | `def __str__(self) -> str` | `def __str__(self) -> str` | This is a compiler intrinsic alias to `to_string()`. |
| Representation | N/A | `def __repr__(self) -> str` | N/A | Not supported in .NET. This method in Python is designed for use in code generation for REPLs. |
| Equality check | `bool Equals(object?)` | `def __eq__(self, other: object \| None) -> bool` | `def __eq__(self, other: object?) -> bool` | Must override both `__eq__` and `__hash__`. This is a compiler intrinsic alias to `equals()`. This automatically synthesizes the equivalent static `==` operator. |
| Inequality check | N/A | `def __ne__(self, other: object \| None) -> bool` | N/A | Not supported in .NET. |
| Equality check with `T` | `bool Equals(T)` | N/A | `def __eq__(self, other: T) -> bool` | Automatically synthesizes the equivalent static `==` operator. |
| Reference equality | `static bool System.Object.ReferenceEquals(T a, T b)` | `a is b` | `a is b` | - |
| Hashing | `int GetHashCode()` | `def __hash__(self) -> int` | `def __hash__(self) -> int` | Must override both `__eq__` and `__hash__`. This is a compiler intrinsic alias to `get_hash_code()`. |
| Less than | `static bool operator <(object lhs, object rhs)` | `def __lt__(self, other: object) -> bool` | `def __lt__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `<` operator. |
| Less than or equal | `static bool operator <=(object lhs, object rhs)` | `__le__(self)` | `def __le__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `<=` operator. |
| Greater than or equal | `static bool operator >(object lhs, object rhs)` | `__gt__(self)` | `def __gt__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `>` operator. |
| Greater than or equal | `static bool operator >=(object lhs, object rhs)` | `__ge__(self)` | `def __ge__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `>=` operator. |
| Addition | `static T operator +(T lhs, T rhs)` | `__add__(self)` | `def __add__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `+` operator. |
| In-place addition | N/A | `__iadd__(self)` | N/A | Not supported in .NET. |
| Subtraction | `static T operator -(T lhs, T rhs)` | `__sub__(self)` | `def __sub__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `-` operator. |
| In-place subtraction | N/A | `__isub__(self)` | N/A | Not supported in .NET. |
| Multiplication | `static T operator *(T lhs, T rhs)` | `__mul__(self)` | `def __mul__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `*` operator. |
| In-place multiplication | N/A | `__imul__(self)` | N/A | Not supported in .NET. |
| True division | `static T operator /(T lhs, T rhs)` | `__div__(self)` | `def __div__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `/` operator. |
| In-place true division | N/A | `__idiv__(self)` | N/A | Not supported in .NET. |
| Negation | `static T operator -(T value)` | `__neg__(self)` | `def __neg__(self) -> T` | Automatically synthesizes the equivalent static `-` operator. |
| Inversion | `static T operator !(T value)` | `__invert__(self)` | `def __invert__(self) -> T` | Automatically synthesizes the equivalent static `!` operator. |
| Boolean conversion | `static operator bool(T value)` | `__bool__(self)` | `def __bool__(self)` | Automatically synthesizes `__true__` and `__false__`. |
| True conversion | `static bool operator true(T value)` | - | `def __true__(self) -> bool` | Automatically synthesizes `__false__`. |
| False conversion | `static bool operator false(T value)` | - | `def __false__(self) -> bool` | Automatically synthesizes `__true__`. |
| Get indexing | `T this[K] { get; }` | `def __getitem__(self, key: K) -> T` | `def __getitem__(key: K) -> T` | - |
| Set indexing | `T this[K] { set; }` | `def __setitem__(self, key: K, value: T)` | `def __setitem__(key: K, value: T)` | - |
| Delete indexing | N/A | `def __delitem__(self, key: K)` | TODO | - |
| Get slice indexing | N/A | `def __getitem__(self, key: K) -> T` | `def __getitem__(slice: slice) -> S[T]` | - |
| Set slice indexing | N/A | `def __setitem__(self, slice: slice, value: T)` | `def __setitem__(slice: slice, value: S[T])` | - |
| Delete slice indexing | N/A | `def __delitem__(self, slice: slice)` | TODO | - |
| Callable objects | `Invoke(...)` | `def __call__(self, ...)` | TODO | - |
| Enumerable | `System.Collections.Generic.IEnumerator<T> GetEnumerator()` implementing `System.Collections.Generic.IEnumerable<T>` | `def __iter__(self) -> Iterable[T]` | `def __iter__(self) -> Iterator[T]` implementing `Iterable[T]` | - |
| Enumerator | `System.Collections.Generic.IEnumerator<T> GetEnumerator()` implementing `System.Collections.Generic.IEnumerable<T>` | `def __next__(self) -> T` | `def __next__(self) -> T` implementing `Iterator[T]` | ??? |
| Context manager | `IDisposable` | `def __enter__(self) -> T` | `def __enter__(self) -> T` implementing `ContextManager[T]` | ??? |
| Context manager | `IDisposable` | `def __exit__(self) -> T` | `def __exit__(self)` implementing `ContextManager[T]` | ??? |
| Cloning | `ICloneable.Clone()` | `def __copy__(self) -> T` | ??? | ??? |
| Deep Cloning | `ICloneable.Clone()` | `def __deepcopy__(self) -> T` | ??? | ??? |

## Dunder methods

Sharpy inherits dunder methods from Python. Dunder methods
are a closed set of members that are compiler-defined.
Only those supported by the compiler can be implemented,
overridden, and/or overloaded by the user.

Dunder methods typically are associated with a Sharpy
protocol (C# interface), though some are compiler
intrinsic aliases to existing methods on `System.Object`,
e.g. when a type has `__hash__()` defined in Sharpy,
then `GetHashCode()` is overridden to delegate to that
method in the generated C# code.

| Sharpy dunder method | Sharpy user invocation | C#/.NET | Notes |
| - | - | - | - |
| `__abs__()` | `abs(x)` | `Sharpy.Abs(x)` | The implementation of `Sharpy.Abs()` should be overloaded on the `Sharpy.IAbsoluteValue<TSelf>` and the `System.Numerics.INumberBase<TSelf>` interfaces, invoking the dunder method in the former, and `TSelf.Abs(x)` in the latter. |
| `__add__()` | `x + y` | `operator +()` | - |
| `__aenter__()` | `async with x:` | `IAsyncContextManager<T>.AsyncEnterContext()` | Only used in Sharpy syntax. |
| `__aexit__()` | End of `async with`-block | `IAsyncContextManager<T>.AsyncExitContext()` | Only used in Sharpy syntax. |
| `__aiter__()` | `async iter(x)` | TODO | - |
| `__and__()` | `x & y` | `operator &()` | - |
| `__bool__()` | `bool(x)`, `if x` | `operator bool()` | If defined, auto-generates `__true__()` and `__false__()` if both are not defined. |
| `__call__()` | `x()` | `operator ()()` | - |
| `__concat__()` | - | TODO | - |
| `__contains__()` | `y in x` | `ICollection<T>.Contains()` | - |
| `__copy__()` | - | TODO | - |
| `__deepcopy__()` | - | TODO | - |
| `__del__()` | - | `~Foobar()` | - |
| `__delitem__()` | `del x[i]` | TODO | - |
| `__enter__()` | `with x:` | `IContextManager<T>.EnterContext()` | - |
| `__eq__()` | `==` | `Equals()`, `operator ==()`, `operator !=()` | - |
| `__exit__()` | End of `with`-block | `IContextManager<T>.ExitContext()` | - |
| `__false__()` | `if not x` | `operator false()` | - |
| `__floordiv__()` | `x // y` | N/A | Only used in Sharpy syntax. |
| `__ge__()` | `x >= y` | `operator >=()` | - |
| `__getitem__()` | `x[i]` | `this[]() { get; }` | - |
| `__gt__()` | `x > y` | `operator >()` | - |
| `__hash__()` | `hash(x)` | `GetHashCode()` | - |
| `__index__()` | - | TODO | - |
| `__init__()` | `Foobar()` | `Foobar()` | - |
| `__iter__()` | `iter(x)` | `IEnumerable<T>.GetEnumerator()` | - |
| `__invert__()` | `~x` | `operator ~()` | - |
| `__le__()` | `x <= y` | `operator <=()` | - |
| `__len__()` | `len(x)` | `Count { get; }` | - |
| `__lshift__()` | `x << y` | `operator <<()` | - |
| `__lt__()` | `x < y` | `operator <()` | - |
| `__matmul__()` | `x @ y` | TODO | - |
| `__mul__()` | `x * y` | `operator *()` | - |
| `__mod__()` | `x % y` | `operator %()` | - |
| `__ne__()` | `x != y` | `operator !=()` | - |
| `__neg__()` | `-x` | `operator -()` | Cannot have an argument. |
| `__new__()` | TODO | TODO | TODO |
| `__next__()` | `next(x)` | `IEnumerator<T>.Current { get; }` and `IEnumerator<T>.MoveNext()` | `MoveNext()` is auto-generated to invoke `__next__()` and store the result in an auto-generated private member that is the source of the `Current` property. |
| `__or__()` | `x \| y` | `operator \|()` | - |
| `__pos__()` | TODO | TODO | TODO |
| `__post_init__()` | TODO | TODO | TODO |
| `__pow__()` | `x ** y` | N/A | Only used in Sharpy syntax. |
| `__radd__()` | `y + x` | `operator +()` | - |
| `__rand__()` | `y & x` | `operator &()` | - |
| `__rdivmod__()` | TODO | TODO | - |
| `__repr__()` | `repr(x)` | TODO | - |
| `__reversed__()` | `reversed(x)` | TODO | - |
| `__rfloordiv__()` | `y // x` | N/A | Only used in Sharpy syntax. |
| `__rlshift__()` | `y << x` | `operator <<()` | - |
| `__rmatmul__()` | `y @ x` | Only used in Sharpy syntax. | - |
| `__rmul__()` | `y * x` | `operator *()` | - |
| `__rmod__()` | `y % x` | `operator %()` | - |
| `__round__()` | `round(x)` | N/A | Only used in Sharpy syntax. |
| `__ror__()` | `y \| x` | `operator \|()` | - |
| `__rpow__()` | `y ** x` | N/A | Only used in Sharpy syntax. |
| `__rrshift__()` | `y >> x` | `operator >>()` | - |
| `__rshift__()` | `x >> y` | `operator >>()` | - |
| `__rsub__()` | `y - x` | `operator -()` | - |
| `__rtruediv__()` | `y / x` | `operator /()` | - |
| `__rxor__()` | `y ^ x` | `operator ^()` | - |
| `__setitem__()` | `x[i] = y` | `this[]() { set; }` | - |
| `__str__()` | `str(x)` | `ToString()` | - |
| `__sub__()` | `-` | `operator -()` | Requires one argument. |
| `__sum__()` | `sum(x)` | TODO | - |
| `__true__()` | `if x` | `operator true()` | - |
| `__truediv__()` | `x / y` | `operator /()` | - |
| `__xor__()` | `x ^ y` | `operator ^()` | - |

## Context managers

Sharpy inherits context managers from Python. An object
is a context manager if it implements the
`ContextManager` or the `ContextManager[T]` protocol,
which declares the `__enter__()` and `__exit__()` dunder
methods. The context manager protocols are underlyingly the
`Sharpy.IContextManager` and `Sharpy.IContextManager<T>`
interfaces in generated C# code.

For C# code generation, when a context manager object
enters a `with`-block, its `__enter__()` method is
invoked. If it is an untyped context manager, it should
return nothing. If it a typed context manager with type
parameter `T`, then this method returns an instance of type
`T` which is an object whose scope is bound inside
the `with`-block. The `as`-clause provides the scope-local
name for this object. If no such name is provided, then
the compiler autogenerates a random name. The object
returned here is assigned to its name (either the
user-provided one or the autogenerated name from the
compiler) with a `using`-assignment. This means that if
the object implements the C# interface `IDisposable`, it
will have its `Dispose()` method invoked when the
`with`-block ends.

When the `with`-block ends, as mentioned above, if the object
originally returned by `__enter__()` implements the C#
`IDisposable` or `IAsyncDisposable` interface, that
object's `Dispose()` method will also be invoked by way
of being automatically assigned via a `using var` or
`async using var` statement during code generation.
Afterwards, the `__exit__()` method is invoked on the
context manager object.

In the case of multiple context managers declared at
the start of a `with`-block, the `__enter__()` and
`__exit__()` methods are called in FIFO order for the
`__enter__()` methods and then LIFO-order for the
`__exit__()` methods.

An example C# implementation of the interfaces is shown
below. Note that if the user defines the Sharpy dunder
methods `__enter__()` and/or `__exit__()`, then the C#
interface methods `EnterContext()` and/or `ExitContext()`
should automatically be generated to delegate to them.
It is also possible for the user to define `enter_context()`
and `exit_context()` themselves (prior to name mangling)
to obviate the need for the compiler to synthesize the
delegation.

```csharp
namespace Sharpy;

public interface IContextManager {
  void EnterContext();
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e);
}

public interface IContextManager<T> {
  T EnterContext();
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e);
}
```

The following Sharpy code:

```python
class Foobar(ContextManager[int]):
    def __enter__() -> int:
        return 5
    def __exit__(e: Tuple[Exception, StackTrace]?) -> bool:
        return False

with Foobar() as i:
    print(i)

f = Foobar()

with f as i:
    print(i)
```

Will generate this C# code:

```csharp
class Foobar : IContextManager<int> {
  int EnterContext() {
    return 5;
  }
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e) {
    return false;
  }
}

// Block for visual purposes and source mapping in code
// generation. There is no semantic purpose.
{
  var temp_foobar = new Foobar();
  Sharpy.Tuple<Exception, StackTrace>? temp_e = null;
  try {
    // If the object returned by `EnterContext` implements
    // IDisposable or IAsyncDisposable, it is assigned in
    // a `using var` assignment (not shown here).
    var i = temp_foobar.EnterContext();
    Sharpy.Print(i);
  }
  catch (Exception e) {
    temp_e = (e, StackTrace(true));
  }
  finally {
    // Note, implicit conversion of
    // `Sharpy.Tuple<Exception, StackTrace>?` to
    // `Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>>`
    if (!temp_foobar.ExitContext(temp_e)) {
      throw temp_e.Item0;
    }
  }
}

var f = new Foobar();

{
  Sharpy.Tuple<Exception, StackTrace>? temp_e = null;
  try {
    var i = f.EnterContext();
    Sharpy.Print(i);
  }
  catch (Exception e) {
    temp_e = (e, StackTrace(true));
  }
  finally {
    if (!f.ExitContext(temp_e)) {
      throw temp_e.Item0;
    }
  }
}
```

## Tuples

Tuples represent fixed-size, ordered collections of
elements that can have different types. Sharpy tuples are
implemented as custom wrapper types around .NET's
`ValueTuple<...>` family, providing Python-style iteration
and indexing support.

```csharp
namespace Sharpy.Core;

// Tuple base interface for runtime polymorphism
public interface ITuple : IEnumerable<object>
{
    int Length { get; }
    object this[int index] { get; }
}

// Concrete tuple types
public readonly struct Tuple<T1, T2> : ITuple
{
    private readonly (T1, T2) _value;

    public Tuple(T1 item1, T2 item2) => _value = (item1, item2);

    public T1 Item1 => _value.Item1;
    public T2 Item2 => _value.Item2;

    public int Length => 2;

    public object? this[int index] => index switch
    {
        0 => Item1,
        1 => Item2,
        _ => throw new IndexError($"tuple index out of range: {index}")
    };

    public IEnumerator<object> GetEnumerator()
    {
        yield return Item1;
        yield return Item2;
    }

    // Implicit conversion to/from ValueTuple
    public static implicit operator (T1, T2)(Tuple<T1, T2> tuple) => tuple._value;
    public static implicit operator Tuple<T1, T2>((T1, T2) tuple) => new(tuple.Item1, tuple.Item2);
}
```

**Sharpy syntax:**
```python
# Tuple creation
point = (10, 20)
triple = (1, "hello", True)

# Unpacking
x, y = point
a, b, c = triple

# Indexing
first = triple[0]  # first as int == 1
```

**Generated C#:**
```csharp
var point = new Tuple<int, int>(10, 20);
var triple = new Tuple<int, string, bool>(1, "hello", true);

// Unpacking (destructuring)
var (x, y) = point;
var (a, b, c) = triple;

// Indexing
var first = triple[0] as int;  // Returns object, requires cast
```

## Modules and Imports

Sharpy's module system maps Python-style imports to C#
namespaces and static classes. Each Sharpy source file
becomes a static class in a namespace hierarchy.

### Module Mapping

| Sharpy File | C# Namespace | C# Class |
|------------|--------------|----------|
| `foo.spy` | `Sharpy.Modules` | `Foo` |
| `foo/bar.spy` | `Sharpy.Modules.Foo` | `Bar` |
| `foo/bar/baz.spy` | `Sharpy.Modules.Foo.Bar` | `Baz` |

### Import Statements

| Sharpy Import | C# Equivalent | Effect |
|--------------|---------------|--------|
| `import foo` | `using Foo = Sharpy.Modules.Foo;` | Access via `Foo.member` |
| `import foo.bar` | `using Bar = Sharpy.Modules.Foo.Bar;` | Access via `Bar.member` |
| `import foo.bar as fb` | `using fb = Sharpy.Modules.Foo.Bar;` | Access via `fb.member` |
| `from foo import bar` | `using static Sharpy.Modules.Foo;`<br/>`// Reference bar directly` | Access via `bar` (function/class) |
| `from foo import *` | `using static Sharpy.Modules.Foo;` | All public members in scope |

### Module Implementation

**Sharpy module (`math.spy`):**
```python
"""Math utilities module."""

PI = 3.14159

def square(x: int) -> int:
    """Returns the square of x."""
    return x * x

def circle_area(radius: float) -> float:
    """Calculate area of circle."""
    return PI * radius * radius
```

**Generated C# (`Sharpy.Modules/Math.cs`):**
```csharp
namespace Sharpy.Modules;

/// <summary>Math utilities module.</summary>
public static class Math
{
    // Module-level constants
    public static const double PI = 3.14159;

    /// <summary>Returns the square of x.</summary>
    public static int square(int x)
    {
        return x * x;
    }

    /// <summary>Calculate area of circle.</summary>
    public static double circle_area(double radius)
    {
        return PI * radius * radius;
    }
}
```

## Type System

Sharpy uses a static type system with full type inference.
All types are known at compile time, with optional runtime
type checking for boundary cases.

### Builtin Types

| Sharpy Type | .NET Type | Python Equivalent | Notes |
|------------|-----------|-------------------|-------|
| `int` | `System.Int32` | `int` | 32-bit signed integer (default) |
| `uint` | `System.Int32` | `int` | 32-bit unsigned integer |
| `short` | `System.Int16` | `int` | 16-bit signed integer |
| `ushort` | `System.Int16` | `int` | 16-bit unsigned integer |
| `long` | `System.Int64` | `int` | 64-bit signed integer |
| `ulong` | `System.Int64` | `int` | 64-bit unsigned integer |
| `sbyte` | `System.SByte` | `int` | 8-bit signed integer |
| `byte` | `System.Byte` | `int` | 8-bit unsigned integer |
| `float` | `System.Single` | `float` | 32-bit floating point |
| `double` | `System.Double` | `float` | 64-bit floating point |
| `decimal` | `System.Decimal` | `float` | 128-bit floating point |
| `bool` | `System.Boolean` | `bool` | Boolean value |
| `str` | `System.String` | `str` | Immutable string |
| `bytes` | `System.Byte[]` | `bytes` | Immutable byte array |
| `bytearray` | `Sharpy.ByteArray` | `bytearray` | Mutable byte array |
| `list[T]` | `Sharpy.List<T>` | `list` | Generic list |
| `dict[K, V]` | `Sharpy.Dict<K, V>` | `dict` | Generic dictionary |
| `set[T]` | `Sharpy.Set<T>` | `set` | Generic set |
| `tuple[T1, T2, ...]` | `Sharpy.Tuple<T1, T2, ...>` | `tuple` | Fixed-size tuple |
| `None` | `null` | `None` | Null reference |

### Type Annotations

Sharpy supports full type annotations with inference when
omitted:

```python
# Explicit annotations
x: int = 42
name: str = "Alice"
items: list[str] = ["a", "b", "c"]

# Type inference
y = 42              # Inferred as int
pi = 3.14159        # Inferred as float
flag = True         # Inferred as bool

# Function annotations
def greet(name: str) -> str:
    return f"Hello, {name}!"

# Generic types
def first[T](items: list[T]) -> T:
    return items[0]
```

### Nullable and Optional types

Sharpy code does not allow nullable types. Every variable,
argument, and member must hold a value. To express the
absence of a value, Sharpy uses an algebraic optional
type. The type name for an optional of type `T` is `T?`
(similar to Rust and Swift). Underlyingly, this is a
wrapper struct `Sharpy.Optional<T>`.

Sharpy functions exposed at .NET boundaries (e.g. for
direct use in C# code) and other .NET functions (e.g.
direct use of a C# function) can only allow the use of
`Sharpy.Optional<T>`. For this reason, the wrapper struct
has implicit bidirectional conversion to the nullable of
the corresponding type, so interop with .NET is seamless.

**Stub C# implementation of `Sharpy.Optional<T>`**:
```csharp
namespace Sharpy;

public struct Optional<T>
{
    private T _value;
    private bool _hasValue;

    public Optional() { _value = default!; _hasValue = false; }

    public Optional(T value)
    {
        SetValue(value);
    }

    public readonly T ValueOrDefault() => ValueOr(default!);

    public readonly T ValueOr(T defaultValue)
    {
        return _hasValue ? _value : defaultValue;
    }

    public void SetValue(T value)
    {
        _value = value ?? default!;
        _hasValue = value is not null;
    }

    public readonly bool HasValue()
    {
        return _hasValue;
    }

    public void Clear()
    {
        _value = default!;
        _hasValue = false;
    }

    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }

    public static implicit operator T?(Optional<T> optional)
    {
        return optional.ValueOrDefault();
    }
}
```

```python
maybe_name: str? = None

match result:
    case Some(value):
        print(value)
    case None:
        pass
```

**Generated C#:**
```csharp
// Nullable reference types enabled
Sharpy.Optional<string> maybe_name = null;

match (maybe_name)
{
  case Some(value) {
    Print(value)
  }
  case None {
    //
  }
}
```

### User-Defined Types

#### Classes

```python
class Point:
    """A 2D point."""
    _x: float
    _y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance(self) -> float:
        """Distance from origin."""
        return (self.x ** 2 + self.y ** 2) ** 0.5

    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"
```

**Generated C#:**
```csharp
/// <summary>A 2D point.</summary>
public class Point
{
    protected float _x { get; set; }
    protected float _y { get; set; }

    public Point(float x, float y)
    {
        this._x = x;
        this._y = y;
    }

    /// <summary>Distance from origin.</summary>
    public float Distance()
    {
        return Math.Pow(Math.Pow(this._x, 2) + Math.Pow(this._y, 2), 0.5);
    }

    public string ToString()
    {
        return $"Point({this._x}, {this._y})";
    }
}
```

#### Structs (Value Types)

```python
struct Vector2:
    """A 2D vector value type."""
    x: float
    y: float

    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Generated C#:**
```csharp
namespace Sharpy.UserTypes;

/// <summary>A 2D vector value type.</summary>
public struct Vector2
{
    public float x;
    public float y;

    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public double Magnitude()
    {
        return Math.Pow(Math.Pow(this.x, 2) + Math.Pow(this.y, 2), 0.5);
    }
}
```

## Protocols and Interfaces

Sharpy supports both structural (protocols) and nominal (interfaces) typing. Protocols define contracts based on method signatures, while interfaces define explicit inheritance relationships.

### Protocol Definition

```python
from typing import Protocol

class Drawable(Protocol):
    """Protocol for drawable objects."""

    def draw(self) -> None:
        """Draw the object."""
        ...

    def get_bounds(self) -> tuple[float, float, float, float]:
        """Get bounding box (x, y, width, height)."""
        ...
```

**Generated C# Interface:**
```csharp
namespace Sharpy.Protocols;

/// <summary>Protocol for drawable objects.</summary>
public interface IDrawable
{
    /// <summary>Draw the object.</summary>
    void draw();

    /// <summary>Get bounding box (x, y, width, height).</summary>
    Tuple<double, double, double, double> get_bounds();
}
```

### Protocol Implementation

```python
class Circle:
    """A circle that can be drawn."""

    def __init__(self, x: float, y: float, radius: float):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self) -> None:
        print(f"Drawing circle at ({self.x}, {self.y}) with radius {self.radius}")

    def get_bounds(self) -> tuple[float, float, float, float]:
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)

# Type checking: Circle satisfies Drawable protocol
def render(obj: Drawable) -> None:
    bounds = obj.get_bounds()
    print(f"Rendering object with bounds: {bounds}")
    obj.draw()
```

**Generated C#:**
```csharp
public class Circle : IDrawable
{
    public double x { get; set; }
    public double y { get; set; }
    public double radius { get; set; }

    public Circle(double x, double y, double radius)
    {
        this.x = x;
        this.y = y;
        this.radius = radius;
    }

    public void draw()
    {
        Print($"Drawing circle at ({this.x}, {this.y}) with radius {this.radius}");
    }

    public Tuple<double, double, double, double> get_bounds()
    {
        return new(
            this.x - this.radius,
            this.y - this.radius,
            this.radius * 2,
            this.radius * 2
        );
    }
}

public static void render(IDrawable obj)
{
    var bounds = obj.get_bounds();
    Print($"Rendering object with bounds: {bounds}");
    obj.draw();
}
```

### Common Protocols

| Protocol | Methods | .NET Interface | Purpose |
|----------|---------|----------------|---------|
| `Iterable[T]` | `__iter__() -> Iterator[T]` | `IEnumerable<T>` | Can be iterated |
| `Iterator[T]` | `__next__() -> T` | `IEnumerator<T>` | Produces values |
| `Sized` | `__len__() -> int` | - | Has length |
| `Container[T]` | `__contains__(T) -> bool` | - | Membership test |
| `Callable[[Args], Ret]` | `__call__(Args) -> Ret` | `Func<Args, Ret>` | Can be called |
| `Comparable[T]` | `__lt__`, `__le__`, etc. | `IComparable<T>` | Can be ordered |
| `Hashable` | `__hash__() -> int` | - | Can be hashed |

## Properties and Descriptors

Sharpy supports Python's `@property` decorator for computed properties, which map to C# properties.

### Basic Properties

```python
class Temperature:
    """Temperature with Celsius/Fahrenheit conversion."""

    def __init__(self, celsius: float):
        self._celsius = celsius

    @property
    def celsius(self) -> float:
        """Temperature in Celsius."""
        return self._celsius

    @celsius.setter
    def celsius(self, value: float) -> None:
        self._celsius = value

    @property
    def fahrenheit(self) -> float:
        """Temperature in Fahrenheit."""
        return self._celsius * 9/5 + 32

    @fahrenheit.setter
    def fahrenheit(self, value: float) -> None:
        self._celsius = (value - 32) * 5/9
```

**Generated C#:**
```csharp
public class Temperature : Sharpy.Object
{
    private double _celsius;

    public Temperature(double celsius)
    {
        this._celsius = celsius;
    }

    /// <summary>Temperature in Celsius.</summary>
    public double celsius
    {
        get => this._celsius;
        set => this._celsius = value;
    }

    /// <summary>Temperature in Fahrenheit.</summary>
    public double fahrenheit
    {
        get => this._celsius * 9.0 / 5.0 + 32.0;
        set => this._celsius = (value - 32.0) * 5.0 / 9.0;
    }
}
```

### Read-Only Properties

```python
class Circle:
    def __init__(self, radius: float):
        self._radius = radius

    @property
    def radius(self) -> float:
        return self._radius

    @property
    def area(self) -> float:
        """Computed property (read-only)."""
        return 3.14159 * self._radius ** 2
```

**Generated C#:**
```csharp
public class Circle : Sharpy.Object
{
    private readonly double _radius;

    public Circle(double radius)
    {
        this._radius = radius;
    }

    public double radius => this._radius;

    /// <summary>Computed property (read-only).</summary>
    public double area => 3.14159 * Math.Pow(this._radius, 2);
}
```

## Decorators

Sharpy supports decorators for functions and classes, which are implemented using C# attributes combined with code generation.

### Function Decorators

```python
from typing import Callable

def trace(func: Callable) -> Callable:
    """Decorator that traces function calls."""
    def wrapper(*args, **kwargs):
        print(f"Calling {func.__name__} with {args}")
        result = func(*args, **kwargs)
        print(f"{func.__name__} returned {result}")
        return result
    return wrapper

@trace
def add(a: int, b: int) -> int:
    return a + b
```

**Generated C# (simplified):**
```csharp
public static int add(int a, int b)
{
    return add_impl(a, b);
}

private static int add_impl(int a, int b)
{
    return a + b;
}

// Wrapper setup in static constructor
static ModuleName()
{
    // Rewire add to use trace wrapper
    add = trace(add_impl);
}
```

### Built-in Decorators

| Decorator | Purpose | C# Equivalent |
|-----------|---------|---------------|
| `@property` | Getter property | `{ get => ... }` |
| `@<prop>.setter` | Setter property | `{ set => ... }` |
| `@staticmethod` | Static method | `static` modifier |
| `@classmethod` | Class method | `static` with `Type` param |
| `@abstractmethod` | Abstract method | `abstract` modifier |
| `@override` | Override marker | `override` modifier |
| `@dataclass` | Data class | Record or class with props |

### Class Decorators

```python
from dataclasses import dataclass

@dataclass
class Person:
    """A person with auto-generated methods."""
    name: str
    age: int
```

**Generated C#:**
```csharp
public record Person(string name, int age);

// Or as a class:
public class Person : Sharpy.Object, IEquatable<Person>
{
    public string name { get; init; }
    public int age { get; init; }

    public Person(string name, int age)
    {
        this.name = name;
        this.age = age;
    }

    // Auto-generated equality, hash, string representation
    public override bool __eq__(object? other) =>
        other is Person p && name == p.name && age == p.age;

    public override int __hash__() =>
        HashCode.Combine(name, age);

    public override string __str__() =>
        $"Person(name={name}, age={age})";
}
```

## Classes and Inheritance

Sharpy supports single inheritance with Python-style syntax, mapping to C# class hierarchies.

### Basic Inheritance

```python
class Animal:
    """Base class for animals."""

    def __init__(self, name: str):
        self.name = name

    def speak(self) -> str:
        raise NotImplementedError("Subclass must implement speak()")

class Dog(Animal):
    """A dog."""

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    def speak(self) -> str:
        return "Woof!"

class Cat(Animal):
    """A cat."""

    def speak(self) -> str:
        return "Meow!"
```

**Generated C#:**
```csharp
/// <summary>Base class for animals.</summary>
public abstract class Animal : Sharpy.Object
{
    public string name { get; set; }

    public Animal(string name)
    {
        this.name = name;
    }

    public virtual string speak()
    {
        throw new NotImplementedError("Subclass must implement speak()");
    }
}

/// <summary>A dog.</summary>
public class Dog : Animal
{
    public string breed { get; set; }

    public Dog(string name, string breed) : base(name)
    {
        this.breed = breed;
    }

    public override string speak()
    {
        return "Woof!";
    }
}

/// <summary>A cat.</summary>
public class Cat : Animal
{
    public Cat(string name) : base(name) { }

    public override string speak()
    {
        return "Meow!";
    }
}
```

### Abstract Classes

```python
from abc import ABC, abstractmethod

class Shape(ABC):
    """Abstract shape class."""

    @abstractmethod
    def area(self) -> float:
        """Calculate area."""
        pass

    @abstractmethod
    def perimeter(self) -> float:
        """Calculate perimeter."""
        pass

class Rectangle(Shape):
    def __init__(self, width: float, height: float):
        self.width = width
        self.height = height

    def area(self) -> float:
        return self.width * self.height

    def perimeter(self) -> float:
        return 2 * (self.width + self.height)
```

**Generated C#:**
```csharp
/// <summary>Abstract shape class.</summary>
public abstract class Shape : Sharpy.Object
{
    /// <summary>Calculate area.</summary>
    public abstract double area();

    /// <summary>Calculate perimeter.</summary>
    public abstract double perimeter();
}

public class Rectangle : Shape
{
    public double width { get; set; }
    public double height { get; set; }

    public Rectangle(double width, double height)
    {
        this.width = width;
        this.height = height;
    }

    public override double area()
    {
        return this.width * this.height;
    }

    public override double perimeter()
    {
        return 2 * (this.width + this.height);
    }
}
```

### Method Resolution Order (MRO)

Sharpy uses C3 linearization (same as Python) for method resolution with multiple interfaces:

```python
class A:
    def method(self) -> str:
        return "A"

class B(A):
    def method(self) -> str:
        return "B"

class C(A):
    def method(self) -> str:
        return "C"

class D(B, C):  # Multiple inheritance via interfaces
    pass

# MRO: D -> B -> C -> A -> Object
```

Since C# only supports single inheritance, multiple inheritance is implemented via interfaces when all but one parent are protocols/abstract.

## Generic Types

Sharpy supports generic type parameters with constraints, mapping to C# generics.

### Generic Functions

```python
from typing import TypeVar

T = TypeVar('T')

def identity[T](value: T) -> T:
    """Returns the input value unchanged."""
    return value

def first[T](items: list[T]) -> T:
    """Returns the first item in a list."""
    if len(items) == 0:
        raise ValueError("List is empty")
    return items[0]
```

**Generated C#:**
```csharp
/// <summary>Returns the input value unchanged.</summary>
public static T identity<T>(T value)
{
    return value;
}

/// <summary>Returns the first item in a list.</summary>
public static T first<T>(List<T> items)
{
    if (Len(items) == 0)
        throw new ValueError("List is empty");
    return items[0];
}
```

### Generic Classes

```python
from typing import Generic, TypeVar

T = TypeVar('T')

class Box[T]:
    """A container for a single value."""

    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value

    def set(self, value: T) -> None:
        self._value = value

# Usage
int_box = Box[int](42)
str_box = Box[str]("hello")
```

**Generated C#:**
```csharp
/// <summary>A container for a single value.</summary>
public class Box<T> : Sharpy.Object
{
    private T _value;

    public Box(T value)
    {
        this._value = value;
    }

    public T get()
    {
        return this._value;
    }

    public void set(T value)
    {
        this._value = value;
    }
}

// Usage
var int_box = new Box<int>(42);
var str_box = new Box<string>("hello");
```

### Type Constraints

```python
from typing import Protocol

class Comparable(Protocol):
    def __lt__(self, other) -> bool: ...

def find_max[T: Comparable](items: list[T]) -> T:
    """Find the maximum item (must be comparable)."""
    if len(items) == 0:
        raise ValueError("List is empty")

    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item
```

**Generated C#:**
```csharp
/// <summary>Find the maximum item (must be comparable).</summary>
public static T find_max<T>(List<T> items) where T : IComparable<T>
{
    if (Len(items) == 0)
        throw new ValueError("List is empty");

    T max_item = items[0];
    foreach (var item in items)
    {
        if (max_item.CompareTo(item) < 0)
            max_item = item;
    }
    return max_item;
}
```

### Variance

Sharpy supports variance annotations for generic types:

```python
from typing import TypeVar

T_co = TypeVar('T_co', covariant=True)      # Covariant (out)
T_contra = TypeVar('T_contra', contravariant=True)  # Contravariant (in)

class Producer[T_co]:
    """Can produce T (covariant)."""
    def get(self) -> T_co: ...

class Consumer[T_contra]:
    """Can consume T (contravariant)."""
    def accept(self, value: T_contra) -> None: ...
```

**Generated C#:**
```csharp
/// <summary>Can produce T (covariant).</summary>
public interface IProducer<out T>
{
    T get();
}

/// <summary>Can consume T (contravariant).</summary>
public interface IConsumer<in T>
{
    void accept(T value);
}
```

## Async Programming

Sharpy supports Python's async/await syntax, mapping to C#'s Task-based asynchronous pattern.

### Async Functions

```python
async def fetch_data(url: str) -> str:
    """Fetch data from URL asynchronously."""
    # Simulated async operation
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main() -> None:
    result = await fetch_data("https://example.com")
    print(result)
```

**Generated C#:**
```csharp
public static async Task<string> fetch_data(string url)
{
    // Simulated async operation
    await Task.Delay(TimeSpan.FromSeconds(1.0));
    return $"Data from {url}";
}

public static async Task main()
{
    string result = await fetch_data("https://example.com");
    Print(result);
}
```

### Async Iteration

```python
from typing import AsyncIterator

async def count_up(n: int) -> AsyncIterator[int]:
    """Async generator counting from 0 to n."""
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process() -> None:
    async for num in count_up(5):
        print(f"Number: {num}")
```

**Generated C#:**
```csharp
public static async IAsyncEnumerable<int> count_up(int n)
{
    for (int i = 0; i < n; i++)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        yield return i;
    }
}

public static async Task process()
{
    await foreach (var num in count_up(5))
    {
        Print($"Number: {num}");
    }
}
```

### Async Context Managers

```python
from typing import AsyncContextManager

class AsyncResource:
    """An async resource with lifecycle."""

    async def __aenter__(self):
        print("Acquiring resource...")
        await asyncio.sleep(0.1)
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        print("Releasing resource...")
        await asyncio.sleep(0.1)
        return False

async def use_resource() -> None:
    async with AsyncResource() as resource:
        print("Using resource")
```

**Generated C#:**
```csharp
public interface IAsyncContextManager<T>
{
    ValueTask<T> __aenter__();
    ValueTask<bool> __aexit__(Type? exc_type, Exception? exc_val, object? exc_tb);
}

public class AsyncResource : IAsyncContextManager<AsyncResource>
{
    public async ValueTask<AsyncResource> __aenter__()
    {
        Print("Acquiring resource...");
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        return this;
    }

    public async ValueTask<bool> __aexit__(Type? exc_type, Exception? exc_val, object? exc_tb)
    {
        Print("Releasing resource...");
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        return false;
    }
}

public static async Task use_resource()
{
    await using (var resource = await new AsyncResource().__aenter__())
    {
        Print("Using resource");
    }
}
```

### Parallel Execution

```python
import asyncio

async def task1() -> int:
    await asyncio.sleep(1.0)
    return 42

async def task2() -> str:
    await asyncio.sleep(0.5)
    return "hello"

async def run_parallel() -> None:
    # Run tasks concurrently
    results = await asyncio.gather(task1(), task2())
    num, text = results
    print(f"Got {num} and {text}")
```

**Generated C#:**
```csharp
public static async Task<int> task1()
{
    await Task.Delay(TimeSpan.FromSeconds(1.0));
    return 42;
}

public static async Task<string> task2()
{
    await Task.Delay(TimeSpan.FromSeconds(0.5));
    return "hello";
}

public static async Task run_parallel()
{
    // Run tasks concurrently
    var results = await Task.WhenAll(task1(), task2());
    int num = results.Item1;
    string text = results.Item2;
    Print($"Got {num} and {text}");
}
```

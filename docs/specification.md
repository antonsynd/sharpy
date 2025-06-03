# Sharpy Language Specification

# Goals

* Provide a statically-typed Pythonic language for the .NET CLI
* Be ABI compatible with C# and the rest of the CLI/CLR.

# Types

This is a table of top-level Sharpy types and their Python and
C#/CLI equivalents. This does not necessarily mean that the Sharpy
type is the indicated CLI type at runtime (i.e. the
implementation). For the actual runtime implementation, see the
table below this one.

| Sharpy | Python equivalent | C# equivalent | CLI equivalent | Notes |
| - | - | - | - | - |
| `array[T]` | `list[T]` | `T[]` | `System.Array<T>` | - |
| `bool` | `bool` | `bool` | `System.Boolean` | - |
| `byte` | `int` | `byte` | `System.Byte` | - |
| `bytearray` | `bytearray` | `List<byte>` | `System.Collections.Generic.List<byte>` | - |
| `bytes` | `bytes` | `byte[]` | `System.Array<byte>` | - |
| `complex` | `complex` | `Complex` | `System.Numerics.Complex` | - |
| `char` | `int` | `char` | `System.Char` | - |
| `decimal` | `float` | `decimal` | `System.Decimal` | - |
| `dict[K, V]` | `dict[K, V]` | `OrderedDictionary<K, V>` | `Systems.Collections.Generic.OrderedDictionary<K, V>` | - |
| `double` | `float` | `double` | `System.Double` | - |
| `Ellipsis` | `Ellipsis` | - | - | - |
| `enum` | `enum.Enum` | `enum` | `System.Enum` | - |
| `Exception` | `Exception` | `Exception` | `System.Exception` | - |
| `float` | `float` | `float` | `System.Single` | - |
| `frozenset[T]` | `frozenset[T]` | `FrozenSet<T>` | `System.Collections.Frozen.FrozenSet<T>` | - |
| `Func[(...), R]` | `Callable[(...), R]` | `Func<..., R>` or `Action<...>` | `System.Delegate` | `Action` used when `R` would be `void` in C# which in Sharpy is a lack of a return type. A type alias for a function type in Sharpy becomes a C# `delegate` type |
| `int` | `int` | `int` | `System.Int32` | - |
| `list[T]` | `list[T]` | `List<T>` | `System.Collections.Generic.List<T>` | - |
| `long` | `int` | `long` | `System.Int64` | - |
| `memoryview` | `memoryview` | - | - | - |
| `None` | `None` | `void` | `System.Void` | As a type, only indicates the lack of a return value, rather than an untyped parameter or the `None` (`null`) literal |
| `object` | `object` | `object` | `System.Object` | - |
| `T?` or `Optional[T]` | `T` | `T?` | `System.Nullable<T>` | Both Sharpy syntaxes are accepted, but `T?` is preferred |
| `sbyte` | `int` | `sbyte` | `System.SByte` | - |
| `set[T]` | `set[T]` | `HashSet<T>` | `System.Collections.Generic.HashSet<T>` | - |
| `short` | `int` | `short` | `System.Int16` | - |
| `slice` | `slice` | `Slice` | `System.Slice` | - |
| `str` | `str` | `string` | `System.String` | - |
| `tuple[...]` | `tuple[...]` | `(...)` | `System.Tuple<...>` | - |
| `uint` | `int` | `uint` | `System.UInt32` | - |
| `ulong` | `int` | `ulong` | `System.UInt64` | - |
| `ushort` | `int` | `ushort` | `System.UInt16` | - |

The actual implementation of a Sharpy type in terms of C# is as
follows:

| Sharpy | C# implementation | Notes |
| - | - | - |
| `array[T]` | `T[]` | - |
| `bool` | `bool` | - |
| `byte` | `byte` | - |
| `bytearray` | `Sharpy.ByteArray` | - |
| `bytes` | `Sharpy.Bytes` | - |
| `char` | `char` | - |
| `complex` | `Sharpy.Complex` | - |
| `decimal` | `decimal` | - |
| `dict[K, V]` | `Sharpy.Dict<K, V>` | - |
| `double` | `double` | - |
| `Ellipsis` | `Sharpy.Ellipsis` | - |
| `enum` | `enum` | - |
| `Exception` | `Sharpy.Exception` | - |
| `float` | `float` | - |
| `frozenset[T]` | `Sharpy.FrozenSet<T>` | - |
| `Func[(...), R]` | See note | Module-level functions are implemented as static member functions of a hidden static class called `__Exports__`. As expected, member functions are implemented as members of the class they belong to |
| `int` | `int` | - |
| `list[T]` | `Sharpy.List<T>` | - |
| `long` | `long` | - |
| `memoryview` | `Sharpy.MemoryView` | - |
| `None` | `void` | As a type, only indicates a lack of a return value, rather than an untyped parameter or the `None` (`null`) literal |
| `object` | `Sharpy.Object` | - |
| `T?` | `T?` | - |
| `sbyte` | `sbyte` | - |
| `set[T]` | `Sharpy.Set<T>` | - |
| `short` | `short` | - |
| `slice` | `Sharpy.Slice` | - |
| `str` | `Sharpy.Str` | - |
| `tuple[...]` | `(...)` | - |
| `uint` | `uint` | - |
| `ulong` | `ulong` | - |
| `ushort` | `ushort` | - |

# Special literals

| Sharpy | Python equivalent | C# equivalent | Notes |
| - | - | - | - |
| `...` | `...` | - | Ellipsis literal in slices |
| `False` | `False` | `false` | - |
| `None` | `None` | `null` | - |
| `True` | `True` | `true` | - |

# Classes and structs

Sharpy has support for classes, but unlike Python, there is no
multiple inheritance. Multiple inheritance is achieved via
protocols (see below).

```Python
class Foo(Bar):
    pass
```

Unlike C#, only Sharpy-specific reference types derive from the
Sharpy `object` base class (i.e. `Sharpy.Object`). The C# concept
of a sealed class is done in Sharpy via the `@final` decorator.
There are no static nor partial classes in Sharpy.

Static functions are indicated with the `@static` decorator. There
are also static members, like in C#, indicated in the same way.
However, unlike Python, there are no class methods with `@cls`.

Sharpy also adds structs from C#, which are always value types.

```Python
struct Val:
    pass
```

Sharpy structs do not inherit from `Sharpy.Object` and do not
participate in inheritance, but they can implement protocols
(see below).

Classes and structs in Sharpy can have member variables, as well as
functional properties (getters and setters).

```Python
class Foo:
    _value: int

    @property
    def value(self) -> int:
        return self._value

    @value.setter
    def value(self, v: int) -> None:
        self._value = v
```

There is also a shorthand notation using the new keywords `get` and `set`:

```Python
class Foo:
    _value: int

    get value(self) -> int:
        return self._value

    set value(self, v: int) -> None:
        self._value = v
```

Unlike Python, Sharpy allows overloading as in C#.

Constructor methods are indicated with the dunder method `__init__`.
Note that `__new__` is not used in Sharpy. It can still be invoked
manually, however.

```Python
class Foo:
    # Constructors do not return anything and have no return type
    def __init__(self):
        pass
```

# Protocols

Sharpy implements protocols which are the equivalent to C#'s
protocols.

```Python
protocol Encodable:
    pass
```

In a class or struct that implements protocols and derives from
a base class, the protocols must follow the base class.

```Python
class Foo(Bar, Encodable):
    pass
```

Protocols declare methods that a conforming must implement.

```Python
protocol Encodable:
    decl def encode(self) -> str

    def encode_as_json(self) -> str:
        return json.dumps(self.encode())
```

Methods with no implementation are introduced via a new keyword
`decl`, without a closing colon. Those that have an implementation
are introduced with `def` as is usually done for normal methods.

Protocols can also declare/define getters and setters.

```Python
protocol Encodable:
    decl get value(self) -> int
    decl set value(self, v: int) -> None
```

# Access modifiers

Access modifiers in Sharpy are indicated by the naming of the
variable with a special prefix. This prefix is purely cosmetic and
is stripped from the variable in the ABI. This applies to classes,
structs, protocols, and members.

Sharpy only supports public, private, and protected access
modifiers. The C# modifiers `internal`, `protected internal`, `private protected`, and `file` do not exist in Sharpy.

As an exception, Sharpy-recognized dunder methods are always
public, however the compiler will issue a warning preferring the
user to use the equivalent global function or operators.

| Sharpy | Example | C# equivalent | Access | Notes |
| - | - | - | - | - |
| Public | `foobar` | `public` | Accessible to everyone. | Sharpy-recognized dunder methods are always public |
| Protected | `_foobar` | `protected` | Accessible to only the actual class and its derived classes, irrespective of whether it is project internal or external | - |
| Private | `__foobar` | `private` | Accessible to only the actual class | This does not apply to dunder methods. Sharpy-recognized dunder methods are always public |
| N/A | - | `internal` | Accessible to anything in the project |
| N/A | - | `protected internal` | Accessible to anything in the project or the actual class and its derived classes, irrespective of whether it is project internal or external | - |
| N/A | - | `private protected` | Accessible to the class and its derived classes within the project | - |
| N/A | - | `file` | Accessible to only symbols in the current file | - |

```Python
# In the ABI, this class is "public Foo"
class Foo:
    # In the ABI, this method is "public void PublicMethod()"
    def public_method(self) -> None:
        pass

    # In the ABI, this method is "protected void ProtectedMethod()"
    def _protected_method(self) -> None:
        pass

    # In the ABI, this method is "private void PrivateMethod()"
    def __private_method(self) -> None:
        pass
```

# Delegates

TODO

# Naming conventions

Sharpy follows Python's snake case for most identifiers (see table
below), e.g. `add_something()` rather than C#'s Pascal case,
`AddSomething()`. Such symbols are exported as Pascal case
symbols in the resulting binaries.

The following is enforced for user-provided code.

| Identifier type | Sharpy case convention | Compile-time conversion? |
| - | - | - |
| Module | `snake_case` | Yes, to `PascalCase` |
| Class | `PascalCase` | No |
| Struct | `PascalCase` | No |
| Members (variables, properties, methods) | `snake_case` | Yes, to `PascalCase` |
| Enum | `PascalCase` | No |
| Enum values | `CAPS_SNAKE_CASE` | Yes, to `PascalCase` |
| Function | `snake_case` | Yes, to `PascalCase` |
| Function parameters | `snake_case` | Yes, to `camelCase` |
| Function body variables | `snake_case` | No |
| Constants | `CAPS_SNAKE_CASE` | No |

To allow Sharpy to access C# and CLI facets in an idiomatic way,
the Sharpy compiler converts all symbols requiring compile-time
conversion to the desired case. If there is an existing case variant,
the compiler throws an exception telling the user to resolve the
ambiguity.

Any symbol in Sharpy can be prefixed with `$` to tell the compiler
to not transform the name during resolution. This is equivalent to
C#'s `@`, which indicates that a symbol name should be resolved as it
is, even if it is a keyword. Sharpy `$` can also be used for the
purpose of using keywords as identifiers.

```Python
# Exposed in ABI as AddSomething()
def add_something():
    pass
```

```Python
import foo_bar.abc
import foo_bar.$abc

new_hash_set()  # resolves to FooBar.Abc.NewHashSet()

$new_hash_set()  # resolves to FooBar.abc.new_hash_set()
```

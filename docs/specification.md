# Sharpy Language Specification

# Goals

* Provide a statically-typed Pythonic language for the .NET CLI
* Be ABI compatible with C# and the rest of the CLI/CLR.

# Types

This is a table of top-level Sharpy types and their Python and
C#/CLI equivalents. This does not necessarily mean that the Sharpy
type is the indicated CLI type at runtime (i.e. the
implementation). For that, see the table below this one.

| Sharpy | Python | C# | CLI | Notes |
| - | - | - | - | - |
| `array[T]` | `list[T]` | `T[]` | `System.Array` | - |
| `bool` | `bool` | `bool` | `System.Boolean` | - |
| `byte` | `int` | `byte` | `System.Byte` | - |
| `bytearray` | `bytearray` | - | - | - |
| `bytes` | `bytes` | `byte[]` | `System.Array<byte>` | - |
| `Func[(...), R]` | `Callable[(...), R]` | `Func<..., R>` or `Action<...>` | `System.Delegate` | `Action` used when `R` would be `void` in C# which in Sharpy is a lack of a return type. A type alias for a function type in Sharpy becomes a C# `delegate` type |
| `char` | - | `char` | `System.Char` | - |
| `decimal` | `float` | `decimal` | `System.Decimal` | - |
| `dict[K, V]` | `dict[K, V]` | `OrderedDictionary<K, V>` | `Systems.Collections.Generic.OrderedDictionary<K, V>` | - |
| `double` | `float` | `double` | `System.Double` | - |
| `enum` | `enum.Enum` | `enum` | `System.Enum` | - |
| `Exception` | `Exception` | `Exception` | `System.Exception` | - |
| `float` | `float` | `float` | `System.Single` | - |
| `int` | `int` | `int` | `System.Int32` | - |
| `list[T]` | `list[T]` | `List<T>` | `System.Collections.Generic.List<T>` | - |
| `long` | `int` | `long` | `System.Int64` | - |
| `object` | `object` | `object` | `System.Object` | - |
| `T?` or `Optional[T]` | `T` | `T?` | `System.Nullable<T>` | Both Sharpy syntaxes are accepted, but `T?` is preferred |
| `sbyte` | `int` | `sbyte` | `System.SByte` | - |
| `set[T]` | `set[T]` | `HashSet<T>` | `System.Collections.Generic.HashSet<T>` | - |
| `short` | `int` | `short` | `System.Int16` | - |
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
| `Func[(...), R]` | See note | Module-level functions are implemented as static member functions of a hidden static class called `__Exports__`. As expected, member functions are implemented as members of the class they belong to. |
| `char` | `char` | - |
| `decimal` | `decimal` | - |
| `dict[K, V]` | `Sharpy.Dict<K, V>` | - |
| `double` | `double` | - |
| `enum` | `enum` | - |
| `Exception` | `Sharpy.Exception` | - |
| `float` | `float` | - |
| `int` | `int` | - |
| `list[T]` | `Sharpy.List<T>` | - |
| `long` | `long` | - |
| `object` | `Sharpy.Object` | - |
| `T?` | `T?` | - |
| `sbyte` | `sbyte` | - |
| `set[T]` | `Sharpy.Set<T>` | - |
| `short` | `short` | - |
| `str` | `Sharpy.Str` | - |
| `tuple[...]` | `(...)` | - |
| `uint` | `uint` | - |
| `ulong` | `ulong` | - |
| `ushort` | `ushort` | - |

# Classes and structs

Sharpy has support for classes, but unlike Python, there is no
multiple inheritance. Multiple inheritance is achieved via
traits (see below).

```Python
class Foo(Bar):
    pass
```

Unlike C#, only Sharpy-specific reference types derive from the
Sharpy `object` base class (i.e. `Sharpy.Object`). Also, there are
no static, sealed, or partial classes.

Static functions are indicated with the `@static` decorator. There
are also static members, like in C#, indicated in the same way.
However, unlike Python, there are no class methods with `@cls`.

Sharpy also adds structs from C#, which are always value types.

```Python
struct Val:
    pass
```

# Traits

Sharpy implements traits which are the equivalent to C#'s interfaces.

```Python
trait Encodable:
    pass
```

In a class or struct that implements traits and derives from
a base class, the traits must follow the base class.

```Python
class Foo(Bar, Encodable):
    pass
```

# Access modifiers

Access modifiers in Sharpy are indicated by the naming of the
variable with a special prefix. This prefix is purely cosmetic and
is stripped from the variable in the ABI. This applies to classes,
structs, traits, and members.

Sharpy only supports public, private, and protected access
modifiers.

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

# Delegates

TODO

# Naming conventions

Sharpy follows Python's snake case for most identifiers (see table
below), e.g. `add_something()` rather than C#'s Pascal case,
`AddSomething()`. Such symbols are exported as Pascal case
symbols in the resulting binaries.

The following is enforced for user-provided code.

| Identifier type | Case | Compile-time conversion? |
| - | - | - |
| Module | `snake_case` | Yes |
| Class | `PascalCase` | No |
| Struct | `PascalCase` | No |
| Enum | `PascalCase` | No |
| Variable | `snake_case` or `camelCase` | Yes |
| Function | `snake_case` or `PascalCase` | Yes |
| Constants | `CAPS_SNAKE_CASE` | No |

To allow Sharpy to access C# and CLI facets in an idiomatic way,
the Sharpy compiler converts all snake case symbols to
Pascal case. If there is an existing snake case symbol, the
compiler throws an exception telling the user to resolve the
ambiguity.

Any symbol in Sharpy can be prefixed with `@` to tell the compiler
to not transform the name during resolution. This extends C#'s
`@`, which indicates that a symbol name should be resolved as it
is, even if it is a keyword.

```
# Exposed in binary as AddSomething()
def add_something():
    pass
```

```
import system.collections.abc

new_hash_set()  # resolves to System.Collections.Abc.NewHashSet()

$new_hash_set()  # resolves to a local new_hash_set() function
```

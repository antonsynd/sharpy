# Sharpy Language Specification

# Goals

* Provide a statically-typed Pythonic language for the .NET CLI
* Be ABI compatible with C# and the rest of the CLI/CLR.

# Types

| Sharpy | Python | C# | CLI | Notes |
| - | - | - | - | - |
| `array[T]` | `list[T]` | `T[]` | `System.Array` | - |
| `bool` | `bool` | `Bool` | `System.Boolean` | - |
| `byte` | `int` | `Byte` | `System.Byte` | - |
| `Func[(...), R]` | `Callable[(...), R]` | `Func<..., R>` or `Action<...>` | `System.Delegate` | `Action` used when `R` would be `void` in C# which in Sharpy is a lack of a return type. A type alias becomes a `delegate` |
| `char` | `bytes` | `Char` | `System.Char` | - |
| `decimal` | `float` | `Decimal` | `System.Decimal` | - |
| `dict[K, V]` | `dict[K, V]` | `OrderedDictionary<K, V>` | `Systems.Collections.Generic.OrderedDictionary<K, V>` | - |
| `double` | `float` | `Double` | `System.Double` | - |
| `Enum[T]` | `enum.Enum` | `List<T>` | `System.Enum` | - |
| `Exception` | `Exception` | `List<T>` | `System.Exception` | - |
| `float` | `float` | `Float` | `System.Single` | - |
| `int` | `int` | `Int` | `System.Int32` | - |
| `list[T]` | `list[T]` | `List<T>` | `System.Collections.Generic.List<T>` | - |
| `long` | `int` | `Long` | `System.Int64` | - |
| `object` | `object` | `object` | `System.Object` | - |
| `Optional[T]` | N/A | `T?` | `System.Nullable<T>` | - |
| `sbyte` | `int` | `SByte` | `System.SByte` | - |
| `set[T]` | `set[T]` | `HashSet<T>` | `System.Collections.Generic.HashSet<T>` | - |
| `short` | `int` | `Short` | `System.Int16` | - |
| `str` | `str` | `string` | `System.String` | - |
| `tuple[...]` | `tuple[...]` | - | - | - |
| `uint` | `int` | `UInt` | `System.UInt32` | - |
| `ulong` | `int` | `ULong` | `System.UInt64` | - |
| `ushort` | `int` | `UShort` | `System.UInt16` | - |

# Classes and structs

Sharpy has support for classes, but unlike Python, there is no multiple
inheritance. Unlike C#, only reference types derive from the `object` base
class.

```Python
class Foo(Bar):
    pass
```

Sharpy also adds structs from C#, which are value types.

```Python
struct Foo(Bar):
    pass
```

Static members are indicated with the `@static` decorator. There are no class methods with `@cls`.

# Traits

Sharpy implements traits which are the equivalent to C#'s interfaces.

```Python
trait IEncodable:
    pass
```

# Access modifiers

Access modifiers in Sharpy are partly indicated by the naming
of the variable with a special prefix. This prefix is purely
cosmetic and is stripped from the variable in the ABI.

| Sharpy | C# | Notes |
| - | - | - |
| Default | `public` | Accessible to everyone |
| `@private` | `private` | Accessible to only the actual class |
| `@protected` | `protected` | Accessible to the actual class and its derived classes, irrespective of whether it is project internal or external |
| `@internal` | `internal` | Accessible to anything in the project |
| TODO | `protected internal` | Accessible to anything in the project or the actual class and its derived classes, irrespective of whether it is project internal or external |
| TODO | `private protected` | Accessible to the class and its derived classes within the project |
| `@file` | `file` | Accessible to only symbols in the current file |

# Delegates



# Naming conventions

Sharpy follows Python's snake case for most identifiers (see table below), e.g.
`add_something()` rather than C#'s Pascal case, `AddSomething()`. Such symbols
are exported as Pascal case symbols in the resulting binaries.

The following is enforced for user-provided code.

| Identifier type | Case | Compile-time conversion? |
| - | - | - |
| Module | snake_case | Yes |
| Class | PascalCase | No |
| Struct | PascalCase | No |
| Enum | PascalCase | No |
| Variable | snake_case or camelCase | Yes |
| Function | snake_case or PascalCase | Yes |
| Constants | CAPS_SNAKE_CASE | No |

To allow Sharpy to access C# and CLI facets in an idiomatic way, the Sharpy
compiler converts all snake case symbols to Pascal case. If there is an existing snake
case symbol, the compiler throws an exception telling the user to resolve the
ambiguity. Any symbol in Sharpy can be prefixed with `$` to tell the compiler
to not transform the name during resolution.

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

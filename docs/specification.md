# Sharpy Language Specification

# Brief

Sharpy is a modern and statically-typed Pythonic language targeting .NET.
While Python code will not run in Sharpy without modifications, the
additions and changes in Sharpy over Python will be welcomed by all Python
developers.

# Goals

* Provide a statically-typed and modern Pythonic language for the .NET CLI
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
| `T?` | `T` | `T?` | `System.Nullable<T>` | - |
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
| `T?` | `Sharpy.Optional<T>` | - |
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

Unlike Python, Sharpy allows overloading as in C#.

Constructor methods are indicated with the special dunder method
`__init__` (equivalent to Python `__init__`). Constructors are always public.

```Python
class Foo:
    # Constructors do not return anything and have no return type
    def __init__(self):
        pass
```

Classes and structs in Sharpy can have member variables, as well as
functional properties (getters and setters). Properties in Sharpy offer
flexible syntax for auto-generation, explicit implementation, and fine-grained
access control.

## Property Syntax

### Auto Properties

Auto properties generate both getter and setter with the same access modifier,
along with a compiler-managed private backing field:

```Python
class Foo:
    # Auto-generated getter/setter with internal access, initialized to 5
    # Corresponds to C#: internal int Value { get; set; } = 5;
    property $value: int = 5

    # Read-only public auto-property (only getter generated)
    get property length: int

    # Write-only private auto-property with initial value
    set property _size: int = 0
```

It is a compiler error to provide an initial value to both an auto-generated getter
and an auto-generated setter.

### Explicit Properties

Explicit properties allow custom getter/setter implementations without
auto-generated backing fields. Access modifiers are applied individually
to each getter/setter:

```Python
class Foo:
    # Manual backing field
    __some_backing_field: int = 0

    # Read-only explicit property (protected access)
    property _dimensions(self) -> int:
        return self.__some_backing_field

    # Write-only explicit property (private access)
    property __dimensions(self, v: int):
        self.__some_backing_field = v
```

### Mixed Auto/Explicit Properties

You can combine auto and explicit property syntax for the same property. In this case,
you can access the backing field via the soft keyword `field`.

```Python
class Foo:
    # Auto getter with backing field
    get property value: int = 5

    # Explicit setter using the same backing field
    property value(self, v: int):
        if v < 0:
            raise ValueError("Value must be positive")
        field = v
```

### Type Inference

When both getter and setter are explicit, type annotations can be inferred:

```Python
class Foo:
    property value(self) -> int:        # Getter with return type
        return self.__backing

    property value(self, v):            # Setter type inferred from getter
        self.__backing = v
```

### Abstract Properties (Protocols)

In protocols and abstract classes, properties can be declared as abstract:

```Python
protocol Encodable:
    # Abstract property requiring both getter and setter
    property num_characters: int

    # Abstract property requiring only getter
    get property num_words: int

    # Abstract property requiring only setter
    set property num_sentences: int

    # Abstract explicit properties with ellipsis
    property num_tokens(self) -> int: ...
    property num_tokens(self, v: int): ...
```

## Property Notes

- Auto properties have completely compiler-managed backing fields that cannot be directly accessed
- Access modifiers apply individually to getters and setters using naming prefixes
- Properties can be read-only (getter only) or write-only (setter only)
- Type inference works between getter and setter when both are explicit
- For auto properties, the backing field name is mangled by the compiler to avoid conflicts
- For mixed auto and explicit properties, access to the compiler-managed backing
field can be done via the `field` soft keyword.

# Protocols

Sharpy implements protocols which are the equivalent to C#'s interfaces.

```Python
protocol Encodable:
    pass
```

Protocols can inherit from other protocols.

```Python
protocol JSONEncodable(Encodable):
    pass
```

In a class or struct that implements protocols and derives from a base
class, the protocols must follow the base class.

```Python
class Foo(Bar, Encodable):
    pass
```

Protocols declare methods that a conforming must implement.

```Python
protocol Encodable:
    @static
    def foobar() -> bool: ...

    def encode(self) -> str: ...

    def encode_as_json(self) -> str:
        return json.dumps(self.encode())
```

Methods with no implementation have an ellipsis. If an actual
implementation is provided, it is inherited by all inheriting protocols and
implementers. If a body with a single `pass` statement (comments optional) is
provided, it is treated as being implemented. This is a crucial difference
between having an ellipsis vs. a `pass` statement.

Protocols can also declare/define properties.

```Python
protocol Encodable:
    property value: int: ...
    # This property implicitly has both a getter and setter

    property name: str:
        get(self): ...
        # This property has no public setter
```

# Access modifiers

Access modifiers in Sharpy are indicated by the naming of the
variable with a special prefix. This prefix is purely cosmetic and
is stripped from the variable in the ABI. This applies to classes,
structs, protocols, and members.

Sharpy only supports public, private, protected, internal, and file access
modifiers. The C# modifiers `protected internal`, and `private protected`,
do not exist in Sharpy.

As an exception, Sharpy-recognized dunder methods are always
public, however the compiler will issue a warning preferring the
user to use the equivalent global function or operators.

| Sharpy | Example | C# equivalent | Access | Notes |
| - | - | - | - | - |
| Public | `foobar` | `public` | Accessible to everyone. | Sharpy-recognized dunder methods are always public |
| Protected | `_foobar` | `protected` | Accessible to only the actual class and its derived classes, irrespective of whether it is project internal or external | - |
| Private | `__foobar` | `private` | Accessible to only the actual class | This does not apply to dunder methods. Sharpy-recognized dunder methods are always public |
| Internal | `$foobar` | `internal` | Accessible to anything in the project |
| N/A | - | `protected internal` | Accessible to anything in the project or the actual class and its derived classes, irrespective of whether it is project internal or external | - |
| N/A | - | `private protected` | Accessible to the class and its derived classes within the project | - |
| File | `$$foobar` | `file` | Accessible to only symbols in the current file | - |

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

    # In the ABI, this method is "internal void InternalMethod()"
    def $internal_method(self) -> None:
        pass

    # In the ABI, this method is "file void FileMethod()"
    def $$internal_method(self) -> None:
        pass
```

# Attributes

`@override`: methods

`@final`: classes/protocols

`@static`: methods, properties, members, classes

TODO

# Signals

Signals are Sharpy's equivalent of C# delegates.

```Python
class Foo:
    some_signal: Signal[[int, int], str]

f = Foo()
f.some_signal += lambda x, y: return x + y
f.some_signal(1, 3)
```

# Events

Events in Sharpy work as they do in C#.

```Python
class Foo:
    event on_click(x: int, y: int) -> int: ...
```

Events are signals that can be publicly added to, but only privately invoked.

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
| Protocol | `PascalCase` | Yes, prefixed with `I` |
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

Any symbol in Sharpy can be surrounded with `\`` to tell the compiler
to not transform the name during resolution. This is equivalent to
C#'s `@`, which indicates that a symbol name should be resolved as it
is, even if it is a keyword. Sharpy's paired `\`` can also be used for the
purpose of using keywords as identifiers.

```Python
# Exposed in ABI as AddSomething()
def add_something():
    pass
```

```Python
from foo_bar.abc import *

# resolves to FooBar.Abc.NewHashSet()
new_hash_set()
```

```Python
from foo_bar.`abc` import *

# resolves to FooBar.abc.new_hash_set()
new_hash_set()
```

# Generics

Classes, structs, and protocols can be made generic, accepting
types as parameters. This makes them incompatible with other
instantiations of that type, as in C#.

```Python
class Foo[T](Bar):
    pass
```

Constraints on generics are specified as follows:

```Python
class Dict[K, V](Bar) where K: Object + Hashable, where V: Object = list:
    pass
```

TODO

# Operators

`?.` optional chaining:

```Python
f: str = None
f?.lower()  # returns None
```

TODO

# True-optional/None-able types

None-able types are indicated in the type with `?` appended to the type:

```Python
i: int? = None
```

Value types are not None-able by default, but reference types are.

# Match assignment

```Python
i: int? = match get_user_input():
    case "none": None
    case _: int(i)
```

TODO

# Try expressions

Sharpy introduces `try` expressions (inspired by Swift), where an assignment
expression that may raise an exception, instead returns a true optional `T?`.
The optional is empty if an exception was raised.

```Python
i: int? = try something_that_may_throw()
```

# Call pipelining

```Python
def foo(x: int) -> str: ...

def bar(y: str, z: bool) -> str: ...

foo(5) -> bar(_, True) -> print
```

The use of the wildcard `_` is to indicate where the return value should be
received as an argument in the following function.

In the event of a tuple return value, it can be destructured:

```Python
def foo(x: int) -> tuple[bool, str, int]: ...

def bar(y: str, z: bool) -> str: ...

foo(5) -> bar(_.1, _.0) -> print
```

Note that in this case, `_.2` is discarded.

# Modules

Sharpy has modules which are the equivalent of namespaces in C#. Modules
can contain module-level functions, classes, protocols, structs, and
constants.

Because Sharpy transpiles to C# as of version 1.0, there are constraints
on the representation.

Module-level functions and constants are automatically placed in a
public static class named `__Module__` as static members. This
static class is internal to Sharpy and is automatically inferred by
the compiler in Sharpy code, but not in C# code interfacing with Sharpy code.

```Python
# some_module.spy
SOME_CONSTANT: int = 3
```

```C#
// Equivalent C# code
namespace SomeModule;

public static class __Module__
{
    public static const int SOME_CONSTANT = 3;
}
```

```Python
# some_other_file.spy
import some_module

print(some_module.SOME_CONSTANT)
```

```C#
// Equivalent C# code (C# 9+ with top-level statements)
using Sharpy;
using static Sharpy.__Module__;

using SomeModule;

Print(SomeModule.__Module__.SOME_CONSTANT);
```

# Program structure

The entry point of a Sharpy program is either a file with top-level
statements, or a single file with a top-level `main()` method.
The `main()` method is transpiled into a static method `Main()`
in an internally generated static class called `__Main__`.

This takes advantage of C# 9+'s top-level statements file as an entry
point.

There is no idiom of checking for `__name__ == "__main__"` as there is
in Python and in fact, the dunder variable `__name__` does not exist
in Sharpy. Attempts to reference it will cause a compilation error.

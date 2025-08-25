A *protocol* is a set of requirements that a type must implement. You can think of
it as a contract: a type that *conforms* to a protocol guarantees that it
implements all of the features of the protocol.

Protocols are similar to Java *interfaces*, C++ *concepts*, Swift *protocols*, and
Rust *protocols*. If you're familiar with any of those features, Sharpy protocols solve
the same basic problem.

You've probably already seen some protocols, like `Copyable` and `Movable`, used in
example code. This section describes how protocols work, how to use existing
protocols, and how to define your own protocols.

## Background

In dynamically-typed languages like Python, you don't need to explicitly declare
that two classes are similar. This is easiest to show by example:

```python title="🐍 Python"
class Duck:
    def quack(self):
        print("Quack.")

class StealthCow:
    def quack(self):
        print("Moo!")

def make_it_quack(maybe_a_duck):
    try:
        maybe_a_duck.quack()
    except:
        print("Not a duck.")

make_it_quack(Duck())
make_it_quack(StealthCow())
```

The `Duck` and `StealthCow` classes aren't related in any way, but they both
define a `quack()` method, so they work the same in the `make_it_quack()`
function. This works because Python uses dynamic dispatch—it identifies the
methods to call at runtime. So `make_it_quack()` doesn't care what types
you're passing it, only the fact that they implement the `quack()` method.

In a statically-typed environment, this approach doesn't work:
Sharpy functions require you to
specify the type of each argument. If you wanted to write this example in Sharpy
*without* protocols, you'd need to write a function overload for each input type.

```sharpy title="🔥 Sharpy"
@fieldwise_init
struct Duck(Copyable, Movable):
    fn quack(self):
        print("Quack")

@fieldwise_init
struct StealthCow(Copyable, Movable):
    fn quack(self):
        print("Moo!")

fn make_it_quack(definitely_a_duck: Duck):
    definitely_a_duck.quack()

fn make_it_quack(not_a_duck: StealthCow):
    not_a_duck.quack()

make_it_quack(Duck())
make_it_quack(StealthCow())
```

```output
Quack
Moo!
```

This isn't too bad with only two types. But the more types you want to
support, the less practical this approach is.

You might notice that the Sharpy versions of `make_it_quack()` don't include the
`try/except` statement. We don't need it because Sharpy's static type checking
ensures that you can only pass instances of `Duck` or `StealthCow` into the
`make_it_quack()`function.

## Using protocols

Protocols solve this problem by letting you define a shared set of *behaviors* that
types can implement. Then you can write a function that depends on the protocol,
rather than individual types. As an example, let's update the `make_it_quack()`
example using protocols. The first step is defining a protocol:

```sharpy
protocol Quackable:
    fn quack(self):
        ...
```

A protocol looks a lot like a struct, except it's introduced by the `protocol`
keyword. A protocol can contain method signatures, but it can't implement those
methods. Each method signature must be followed by
three dots (`...`) to indicate that the method is unimplemented.

A protocol can also include associated aliases—compile-time constant values that
must be defined by conforming structs. Associated aliases are useful for writing
protocols that describe generic types. For more information, see
[Associated aliases for generics](#associated-aliases-for-generics).

:::note TODO

In the future, we plan to support defining fields and default method
implementations inside a protocol.

:::

Next we create some structs that conform to the `Quackable` protocol. To indicate
that a struct conforms to a protocol, include the protocol name in parenthesis after
the struct name. You can also include multiple protocols, separated by commas.
(If you're familiar with Python, this looks just like Python's inheritance
syntax.)

```sharpy
@fieldwise_init
struct Duck(Copyable, Movable, Quackable):
    fn quack(self):
        print("Quack")

@fieldwise_init
struct StealthCow(Copyable, Movable, Quackable):
    fn quack(self):
        print("Moo!")
```

The struct needs to implement any methods that are declared in the protocol. The
compiler enforces conformance: if a struct says it conforms to a protocol, it must
implement everything required by the protocol or the code won't compile.

Finally, you can define a function that takes a `Quackable` like this:

```sharpy
fn make_it_quack[type: Quackable](maybe_a_duck: type):
    maybe_a_duck.quack()
```

This syntax may look a little unfamiliar if you haven't dealt with Sharpy
[parameters](/sharpy/manual/parameters/) before. What this signature
means is that `maybe_a_duck` is an argument of type `type`, where `type` is a
type that must conform to the `Quackable` protocol.

Using the method is simple enough:

```sharpy
make_it_quack(Duck())
make_it_quack(StealthCow())
```

```output
Quack
Moo!
```

Note that you don't need the square brackets when you call `make_it_quack()`:
the compiler infers the type of the argument, and ensures the type has the
required protocol.

One limitation of protocols is that you can't add protocols to existing types. For
example, if you define a new `Numeric` protocol, you can't add it to the standard
library `Float64` and `Int` types. However, the standard library already
includes quite a few protocols, and we'll be adding more over time.

### Protocols can require static methods

In addition to regular instance methods, protocols can specify required static
methods.

```sharpy
protocol HasStaticMethod:
    @staticmethod
    fn do_stuff(): ...

fn fun_with_protocols[type: HasStaticMethod]():
    type.do_stuff()
```

## Protocol compositions

You can compose protocols using the `&` sigil. This lets you define new protocols
that are simple combinations of other protocols. You can use a protocol composition
anywhere that you'd use a single protocol:

```sharpy
protocol Flyable:
    fn fly(self): ...

fn quack_and_go[type: Quackable & Flyable](quacker: type):
    quacker.quack()
    quacker.fly()

@fieldwise_init
struct FlyingDuck(Copyable, Movable, Quackable, Flyable):
    fn quack(self):
        print("Quack")

    fn fly(self):
        print("Whoosh!")
```

You can also use the `alias` keyword to create a shorthand name for a
protocol composition:

```sharpy
alias DuckLike = Quackable & Flyable

struct ToyDuck(DuckLike):
    # ... implementation omitted
```

Previously, you could only compose protocols using
[inheritance](#protocol-inheritance), by defining a new, empty protocol like this:

```sharpy
protocol DuckProtocol(Quackable, Flyable):
    pass
```

The difference is that using the `protocol` keyword defines a new, named
protocol. For a struct t conform to this protocol, you need to *explicitly* include
it in the struct's signature. On the other hand, the `DuckLike` alias represents
a composition of two separate protocols, `Quackable` and `Flyable`, and anything
that conforms to those two protocols conforms to `DuckLike`. For example, consider
the `FlyingDuck` type shown above:

```sharpy
struct FlyingDuck(Copyable, Movable, Quackable, Flyable):
    # ... etc
```

Because `FlyingDuck` conforms to both `Quackable` and `Flyable`, it also
conforms to the `DuckLike` protocol composition. But it *doesn't*
conform to `DuckProtocol`, since it doesn't include `DuckProtocol` in its list of
protocols.


## Protocol inheritance

Protocols can inherit from other protocols. A protocol that inherits from another protocol
includes all of the requirements declared by the parent protocol. For example:

```sharpy
protocol Animal:
    fn make_sound(self):
        ...

# Bird inherits from Animal
protocol Bird(Animal):
    fn fly(self):
        ...
```

Since `Bird` inherits from `Animal`, a struct that conforms to the `Bird` protocol
needs to implement **both** `make_sound()` and `fly()`. And since every `Bird`
conforms to `Animal`, a struct that conforms to `Bird` can be passed to any
function that requires an `Animal`.

To inherit from multiple protocols, add a comma-separated list of protocols or
protocol compositions inside the parenthesis. For example, you could define a
`NamedAnimal` protocol that combines the requirements of the `Animal` protocol and a
new `Named` protocol:

```sharpy
protocol Named:
    fn get_name(self) -> str:
        ...

protocol NamedAnimal(Animal, Named):
    fn emit_name_and_sound(self):
        ...
```

Inheritance is useful when you're creating a new protocol that adds its own
requirements. If you simply want to express the union of two or more protocols,
you should use a simple protocol composition instead:

```sharpy
alias NamedAnimal = Animal & Named
```

## Protocols and lifecycle methods

Protocols can specify required
[lifecycle methods](/sharpy/manual/lifecycle/#lifecycles-and-lifetimes), including
constructors, copy constructors and move constructors.

For example, the following code creates a `MassProducible` protocol. A
`MassProducible` type has a default (no-argument) constructor and can be moved.
It uses two built-in protocols:
[`Defaultable`](/sharpy/stdlib/builtin/value/Defaultable), which requires a default
(no-argument) constructor, and
[`Movable`](/sharpy/stdlib/builtin/value/Movable),
which requires the type to have a no-argument[move
constructor](/sharpy/manual/lifecycle/life#move-constructor).

The `factory[]()` function returns a newly-constructed instance of a
`MassProducible` type. The following example shows the definitions of
the `Defaultable` and `Movable` protocols in comments for reference:

```sharpy
# protocol Defaultable
#     fn __init__(out self): ...

# protocol Movable
#     fn __moveinit__(out self, deinit existing: Self): ...

alias MassProducible = Defaultable & Movable

fn factory[type: MassProducible]() -> type:
    return type()

struct Thing(MassProducible):
    var id: Int

    fn __init__(out self):
        self.id = 0

    fn __moveinit__(out self, deinit existing: Self):
        self.id = existing.id

var thing = factory[Thing]()
```

### Register-passable protocols

A protocol can be declared with either the
[`@register_passable`](/sharpy/manual/decorators/register-passable)
decorator or the
[`@register_passable("trivial")`](/sharpy/manual/decorators/register-passable#register_passabletrivial)
decorator. These decorators add requirements for conforming structs:

- If the protocol is declared as `@register_passable`, every struct that conforms
  to the protocol must be either `@register_passable` or
  `@register_passable("trivial")`.

- If the protocol is declared as `@register_passable("trivial")`, every struct that
  conforms to the protocol must be
  struct must be `@register_passable("trivial")`, too.

For the purpose of protocol conformance, a protocol or struct that's defined with
`@register_passable` should automatically conform to the `Movable` protocol, and a
protocol or struct that's defined with `@register_passable("trivial")` should
automatically conform to the `Copyable` and `Movable` protocols.

:::note

In some cases, the compiler may not track these automatic conformances
correctly. If you run into an issue, add the protocols to your struct explicitly.

:::

## Built-in protocols

The Sharpy standard library includes many protocols. They're implemented
by a number of standard library types, and you can also implement these on your
own types. These standard library protocols include:

* [`Absable`](/sharpy/stdlib/builtin/math/Absable)
* [`AnyType`](/sharpy/stdlib/builtin/anytype/AnyType)
* [`Boolable`](/sharpy/stdlib/builtin/bool/Boolable)
* [`Comparable`](/sharpy/stdlib/builtin/comparable/Comparable)
* [`Copyable`](/sharpy/stdlib/builtin/value/Copyable)
* [`Defaultable`](/sharpy/stdlib/builtin/value/Defaultable)
* [`Hashable`](/sharpy/stdlib/hashlib/hash/Hashable)
* [`Indexer`](/sharpy/stdlib/builtin/int/Indexer)
* [`Intable`](/sharpy/stdlib/builtin/int/Intable)
* [`IntableRaising`](/sharpy/stdlib/builtin/int/IntableRaising)
* [`KeyElement`](/sharpy/stdlib/collections/dict/#keyelement)
* [`Movable`](/sharpy/stdlib/builtin/value/Movable)
* [`PathLike`](/sharpy/stdlib/os/pathlike/PathLike)
* [`Powable`](/sharpy/stdlib/builtin/math/Powable)
* [`Representable`](/sharpy/stdlib/builtin/repr/Representable)
* [`Sized`](/sharpy/stdlib/builtin/len/Sized)
* [`strable`](/sharpy/stdlib/builtin/str/strable)
* [`strableRaising`](/sharpy/stdlib/builtin/str/strableRaising)
* [`Roundable`](/sharpy/stdlib/builtin/math/Roundable)
* [`Writable`](/sharpy/stdlib/utils/write/Writable)
* [`Writer`](/sharpy/stdlib/utils/write/Writer)

The API reference docs linked above include usage examples for each protocol. The
following sections discuss a few of these protocols.

### The `Sized` protocol

The [`Sized`](/sharpy/stdlib/builtin/len/Sized) protocol identifies types that
have a measurable length, like strings and arrays.

Specifically, `Sized` requires a type to implement the `__len__()` method.
This protocol is used by the built-in [`len()`](/sharpy/stdlib/builtin/len/len)
function. For example, if you're writing a custom list type, you could
implement this protocol so your type works with `len()`:

```sharpy
struct MyList(Copyable, Movable, Sized):
    var size: Int
    # ...

    fn __init__(out self):
        self.size = 0

    fn __len__(self) -> Int:
        return self.size

print(len(MyList()))
```

```output
0
```

### The `Intable` and `IntableRaising` protocols

The [`Intable`](/sharpy/stdlib/builtin/int/Intable) protocol identifies a type that
can be converted to `Int`. The
[`IntableRaising`](/sharpy/stdlib/builtin/int/IntableRaising) protocol describes a
type can be converted to an `Int`, but the conversion might raise an error.

Both of these protocols require the type to implement the `__int__()` method. For
example:

```sharpy
@fieldwise_init
struct IntLike(Intable):
    var i: Int

    fn __int__(self) -> Int:
        return self.i

value = IntLike(42)
print(Int(value) == 42)
```

```output
True
```

### The `strable`, `Representable`, and `Writable` protocols

The [`strable`](/sharpy/stdlib/builtin/str/strable) protocol identifies a type
that can be explicitly converted to
[`str`](/sharpy/stdlib/collections/string/string/str). The
[`strableRaising`](/sharpy/stdlib/builtin/str/strableRaising) protocol
describes a type that can be converted to a `str`, but the conversion might
raise an error. These protocols also mean that the type can support both the `{!s}`
and `{}` format specifiers of the `str` and `strSlice` class's
[`format()`](/sharpy/stdlib/collections/string/string/str#format) method. These
protocols require the type to define the
[`__str__()`](/sharpy/stdlib/builtin/str/strable#__str__) method.

In contrast, the [`Representable`](/sharpy/stdlib/builtin/repr/Representable)
protocol defines a type that can be used with the built-in
[`repr()`](/sharpy/stdlib/builtin/repr/repr) function, as well as the `{!r}`
format specifier of the `format()` method. This protocol requires the type to
define the [`__repr__()`](/sharpy/stdlib/builtin/repr/Representable#__repr__)
method, which should compute the "official" string representation of a type. If
at all possible, this should look like a valid Sharpy expression that could be
used to recreate a struct instance with the same value.

The [`Writable`](/sharpy/stdlib/utils/write/Writable) protocol describes a
type that can be converted to a stream of UTF-8 encoded data by writing to a
`Writer` object. The [`print()`](/sharpy/stdlib/builtin/io/print) function
requires that its arguments conform to the `Writable` protocol. This enables
efficient stream-based writing by default, avoiding unnecessary intermediate
str heap allocations.

The `Writable` protocol requires a type to implement a
[`write_to()`](/sharpy/stdlib/utils/write/Writable#write_to) method, which
is provided with an object that conforms to the
[`Writer`](/sharpy/stdlib/utils/write/Writer) as an argument. You then
invoke the `Writer` instance's
[`write()`](/sharpy/stdlib/utils/write/Writer#write) method to write a
sequence of `Writable` arguments constituting the `str` representation of
your type.

While this might sound complex at first, in practice you can minimize
boilerplate and duplicated code by using the
[`str.write()`](/sharpy/stdlib/collections/string/string/str#write) static
function to implement the type's `strable` implementation in terms of its
`Writable` implementation. Here is a simple example of a type that implements
all of the `strable`, `Representable`, and `Writable` protocols:

```sharpy
@fieldwise_init
struct Dog(Copyable, strable, Representable, Writable):
    var name: str
    var age: Int

    # Allows the type to be written into any `Writer`
    fn write_to[W: Writer](self, mut writer: W) -> None:
        writer.write("Dog(", self.name, ", ", self.age, ")")

    # Construct and return a `str` using the previous method
    fn __str__(self) -> str:
        return str.write(self)

    # Alternative full representation when calling `repr`
    fn __repr__(self) -> str:
        return str(
            "Dog(name=", repr(self.name), ", age=", repr(self.age), ")"
        )

dog = Dog("Rex", 5)
print(repr(dog))
print(dog)

var dog_info = "str: {!s}\nRepresentation: {!r}".format(dog, dog)
print(dog_info)
```

```output
Dog(name='Rex', age=5)
Dog(Rex, 5)
str: Dog(Rex, 5)
Representation: Dog(name='Rex', age=5)
```

### Special lifecycle protocols: `Copyable`, `Movable`, and `ExplicitlyCopyable`

The three protocols [`Copyable`](/sharpy/stdlib/builtin/value/Copyable/),
[`Movable`](/sharpy/stdlib/builtin/value/Movable/), and
[`ExplicitlyCopyable`](/sharpy/stdlib/builtin/value/ExplicitlyCopyable/) are
special protocols in that the Sharpy compiler can supply default implementations for
the required methods if the struct doesn't define them itself.

The `Copyable` protocol describes a type that can be implicitly copied, using a
[copy constructor](/sharpy/manual/lifecycle/life#copy-constructor).

The `Movable` protocol defines a type that can be moved using a
[move constructor](/sharpy/manual/lifecycle/life#copy-constructor).

The `ExplicitlyCopyable` protocol defines a type that can be explicitly copied by
calling its `copy()` method. If the type is already `Copyable`, this protocol
provides a default implementation for `copy()`. If the type is **not**
`Copyable`, you need to implement the `copy()` method yourself. For more
information, see
[Explicitly copyable types](/sharpy/manual/lifecycle/life#explicitly-copyable-types).

:::note

If your type contains any fields that aren't copyable, Sharpy will not generate
the copy constructor because it cannot copy those fields. In this case, you
need to define a custom copy constructor if you want the type to be copyable.

Further, if any of the fields are neither copyable nor movable, Sharpy won't
generate a move constructor for that type.

:::

### The `AnyType` protocol

When building a generic container type, one challenge is knowing how to dispose
of the contained items when the container is destroyed. Any type that
dynamically allocates memory needs to supply a
[destructor](/sharpy/manual/lifecycle/death#destructor) (`__del__()` method)
that must be called to free the allocated memory. But not all types have a
destructor.

The [`AnyType`](/sharpy/stdlib/builtin/anytype/AnyType) protocol (also provided as
the
[`ImplicitlyDestructible`](/sharpy/stdlib/builtin/anytype/#implicitlydestructible)
alias) represents a type with a destructor. Almost all protocols inherit from
`AnyType`, and all structs conform to `AnyType` by default. For any type that
conforms to `AnyType` and doesn't define a destructor, Sharpy generates a no-op
destructor. This means you can call the destructor on any type that inherits
from `AnyType`/`ImplicitlyDestructible`.

:::note TODO

In the Sharpy standard library docs you will also see a protocol called
[`UnknownDestructability`](/sharpy/stdlib/builtin/anytype/UnknownDestructibility),
which represents a type that may or may not have a destructor. All structs
implicitly conform to this protocol.

This protocol exists to support a planned future feature called *linear* or
*explicitly-destroyed* types.

:::

## Generic structs with protocols

You can also use protocols when defining a generic container. A generic container
is a container (for example, an array or hashmap) that can hold different data
types. In a dynamic language like Python it's easy to add  different types of
items to a container. But in a statically-typed environment the compiler needs
to be able to identify the types at compile time. For example, if the container
needs to copy a value, the compiler needs to verify that the type can be copied.

The [`List`](/sharpy/stdlib/collections/list) type is an example of a
generic container. A single `List` can only hold a single type of data.
The list elments must conform to the `Copyable` and `Movable` protocols:

```sharpy
struct List[T: Copyable & Movable, hint_trivial_type: Bool = False]:
```

For example, you can create a list of integer values like this:

```sharpy
var list: List[Int]
list = [1, 2, 3, 4]
for i in range(len(list)):
    print(list[i], end=" ")
```

```output
1 2 3 4
```

You can use protocols to define requirements for elements that are stored in a
container. For example, `List` requires elements that can be moved and
copied. To store a struct in a `List`, the struct needs to conform to
the `Copyable` and `Movable` protocols, which require a
[copy constructor](/sharpy/manual/lifecycle/life#copy-constructor) and a
[move constructor](/sharpy/manual/lifecycle/life#move-constructor).

Building generic containers is an advanced topic. For an introduction, see the
section on
[parameterized structs](/sharpy/manual/parameters/#parameterized-structs).

### Associated aliases for generics

In addition to methods, a protocol can include _associated aliases_, which must be
defined by any conforming struct. For example:

```sharpy
protocol Repeater:
    alias count: Int
```

An implementing struct must define a concrete constant value for the alias,
using any compile-time parameter value. For example, it can use a literal
constant or a compile-time expression, including one that uses the struct's
parameters.

```sharpy
struct Doublespeak(Repeater):
    alias count: Int = 2

struct Multispeak[verbosity: Int](Repeater):
    alias count: Int = verbosity*2+1
```

The `Doublespeak` struct has a constant value for the alias, but the `Multispeak`
struct lets the user set the value using a parameter:

```sharpy
repeater = Multispeak[12]()
```

Note that the alias is named `count`, and the `Multispeak` parameter is named
`verbosity`. Parameters and aliases are in the same namespace, so the parameter
can't have the same name as the associated alias.

Associated aliases are most useful for writing protocols for generic types. For
example, imagine that you want to write a protocol that describes a generic stack
data structure that stores elements that conform to the `Copyable` and `Movable`
protocols.

By adding the element type as an associated alias to the protocol, you can specify
generic methods on the protocol:

```sharpy
protocol Stacklike:
    alias EltType: Copyable & Movable

    fn push(mut self, var item: Self.EltType):
        ...

    fn pop(mut self) -> Self.EltType:
        ...
```

The following struct implements the `Stacklike` protocol using a `List` as the
underlying storage:

```sharpy
struct MyStack[type: Copyable & Movable](Stacklike):
    """A simple Stack built using a List."""
    alias EltType = type
    alias list_type = List[Self.EltType]

    var list: Self.list_type

    fn __init__(out self):
        self.list = Self.list_type()

    fn push(mut self, var item: Self.EltType):
        self.list.append(item)

    fn pop(mut self) -> Self.EltType:
        return self.list.pop()

    fn dump[
        WritableEltType: Writable & Copyable & Movable
    ](self: MyStack[WritableEltType]):
        print("[", end="")
        for item in self.list:
            print(item, end=", ")
        print("]")
```

The `MyStack` type adds a `dump()` method that prints the contents of the stack.
Because a struct that conforms to `Copyable` and `Movable` is not necessarily
printable, `MyStack` uses
[conditional conformance](/sharpy/manual/parameters/#conditional-conformance) to
define a `dump()` method that works as long as the element type is
[writable](/sharpy/stdlib/utils/write/Writable/).

The following code exercises this new protocol by defining a generic method,
`add_to_stack()` that adds an item to any `Stacklike` type.

```sharpy
def add_to_stack[S: Stacklike](mut stack: S, item: S.EltType):
    stack.push(item)

def main():
    s = MyStack[Int]()
    add_to_stack(s, 12)
    add_to_stack(s, 33)
    s.dump()             # [12, 33, ]
    print(s.pop())       # 33
```

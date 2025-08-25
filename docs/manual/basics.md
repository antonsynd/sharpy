# Sharpy language basics

This page provides an overview of the Sharpy language.

If you know Python, then a lot of Sharpy code will look familiar. However, Sharpy
incorporates features like static type checking, memory safety, next-generation
compiler technologies, and more. As such, Sharpy also has a lot in common with
languages like C++ and Rust.

If you prefer to learn by doing, follow the [Get started with
Sharpy](/sharpy/manual/get-started) tutorial.

On this page, we'll introduce the essential Sharpy syntax, so you can start
coding quickly and understand other Sharpy code you encounter. Subsequent
sections in the Sharpy Manual dive deeper into these topics, and links are
provided below as appropriate.

Let's get started!

## Hello world

Here's the traditional "Hello world" program in Sharpy:

```sharpy
def main():
    print("Hello, world!")
```

Every Sharpy program must include a function named `main()` as the entry point.
We'll talk more about functions soon, but for now it's enough to know that
you can write `def main():` followed by an indented function body.

The `print()` statement does what you'd expect, printing its arguments to
the standard output.

## Variables

In Sharpy, you can declare a variable by simply assigning a value to
a new named variable:

```sharpy
def main():
    x = 10
    y = x * x
    print(y)
```

You can also _explicitly_ declare variables with the `var` keyword:

```sharpy
var x = 10
```

When declaring a variable with `var`, you can also declare a variable type, with
or without an assignment:


```sharpy
def main():
    var x: Int = 10
    var sum: Int
    sum = x + x
```

Both implicitly declared and explicitly declared variables are statically typed:
that is, the type is set at compile time, and doesn't change at runtime.
If you don't specify a type, Sharpy uses the type of the first value assigned to
the variable.

```sharpy
x = 10
x = "Foo" # Error: Cannot convert "strLiteral" value to "Int"
```

For more details, see the page about
[variables](/sharpy/manual/variables).

## Blocks and statements

Code blocks such as functions, conditions, and loops are defined
with a colon followed by indented lines. For example:

```sharpy
def loop():
    for x in range(5):
        if x % 2 == 0:
            print(x)
```

You can use any number of spaces or tabs for your indentation (we prefer 4
spaces).

All code statements in Sharpy end with a newline. However, statements can span
multiple lines if you indent the following lines. For example, this long string
spans two lines:

```sharpy
def print_line():
    long_text = "This is a long line of text that is a lot easier to read if"
                " it is broken up across two lines instead of one long line."
    print(long_text)
```

And you can chain function calls across lines:

```sharpy
def print_hello():
    text = ",".join("Hello", " world!")
    print(text)
```

For more information on loops and conditional statements, see
[Control flow](/sharpy/manual/control-flow).

## Functions

You can define a Sharpy function using either the `def` or `fn` keyword. For
example, the following uses the `def` keyword to define a function named
`greet` that requires a single `str` argument and returns a `str`:

```sharpy
def greet(name: str) -> str:
    return "Hello, " + name + "!"
```

Where `def` and `fn` differ is error handling and argument mutability defaults.
Refer to the [Functions](/sharpy/manual/functions) page for more details on
defining and calling functions.

## Code comments

You can create a one-line comment using the hash `#` symbol:

```sharpy
# This is a comment. The Sharpy compiler ignores this line.
```

Comments may also follow some code:

```sharpy
var message = "Hello, World!" # This is also a valid comment
```

API documentation comments are enclosed in triple quotes. For example:

```sharpy
fn print(x: str):
    """Prints a string.

    Args:
        x: The string to print.
    """
    ...
```

Documenting your code with these kinds of comments (known as "docstrings")
is a topic we've yet to fully specify, but you can generate an API reference
from docstrings using the [`sharpy doc` command](/sharpy/cli/doc).

:::note

Technically, docstrings aren't _comments_, they're a special use of Sharpy's
syntax for multi-line string literals. For details, see
[str literals](/sharpy/manual/types#string-literals) in the page on
[Types](/sharpy/manual/types).

:::

## Structs

You can build high-level abstractions for types (or "objects") as a `struct`.

A `struct` in Sharpy has the same capabilities as a C# `struct`. In comparison
to a `class` in Python, both support methods, fields, operator overloading,
decorators for metaprogramming, and so on. However, Sharpy structs are always
passed by value, not by reference.

For example, here's a basic struct:

```sharpy
struct MyPair(Copyable):
    var first: Int
    var second: Int

    fn __init__(out self, first: Int, second: Int):
        self.first = first
        self.second = second

    fn __copyinit__(out self, existing: Self):
        self.first = existing.first
        self.second = existing.second

    def dump(self):
        print(self.first, self.second)
```

And here's how you can use it:

```sharpy
def use_mypair():
    var mine = MyPair(2, 4)
    mine.dump()
```

Note that some functions are declared with `fn` function, while the `dump()`
function is declared with `def`. In general, you can use either form in a
struct.

The `MyPair` struct contains two special methods, `__init__()`, the constructor,
and `__copyinit__()`, the copy constructor. _Lifecycle methods_ like this
control how a struct is created, copied, moved, and destroyed.

For most simple types, you don't need to write the lifecycle methods. You can
use the `@fieldwise_init` decorator to generate the boilerplate field-wise
initializer for you, and Sharpy will synthesize copy and move constructors if you
ask for them with protocol conformance. So the
`MyPair` struct can be simplified to this:

```sharpy
@fieldwise_init
struct MyPair(Copyable, Movable):
    var first: Int
    var second: Int

    def dump(self):
        print(self.first, self.second)
```

For more details, see the page about
[structs](/sharpy/manual/structs).

### Protocols

A protocol is like a template of characteristics for a struct. If you want to
create a struct with the characteristics defined in a protocol, you must implement
each characteristic (such as each method). Each characteristic in a protocol is a
"requirement" for the struct, and when your struct implements all of the
requirements, it's said to "conform" to the protocol.

Using protocols allows you to write generic functions that can accept any type
that conforms to a protocol, rather than accept only specific types.

For example, here's how you can create a protocol:

```sharpy
protocol SomeProtocol:
    fn required_method(self, x: Int): ...
```

The three dots following the method signature are Sharpy syntax indicating that
the method is not implemented.

Here's a struct that conforms to `SomeProtocol`:

```sharpy
@fieldwise_init
struct SomeStruct(SomeProtocol):
    fn required_method(self, x: Int):
        print("hello protocols", x)
```

Then, here's a function that uses the protocol as an argument type (instead of the
struct type):

```sharpy
fn fun_with_protocols[T: SomeProtocol](x: T):
    x.required_method(42)

fn use_protocol_function():
    var thing = SomeStruct()
    fun_with_protocols(thing)
```

You'll see protocols used in a lot of APIs provided by Sharpy's standard library. For
example, Sharpy's collection types like `List` and `Dict` can store any type that
conforms to the `Copyable` and `Movable` protocols. You can specify the type when
you create a collection:

```sharpy
my_list = List[Float64]()
```

:::note

You're probably wondering about the square brackets on `fun_with_protocols()`.
These aren't function *arguments* (which go in parentheses); these are function
*parameters*, which we'll explain next.

:::

Without protocols, the `x` argument in `fun_with_protocols()` would have to declare a
specific type that implements `required_method()`, such as `SomeStruct`
(but then the function would accept only that type). With protocols, the function
can accept any type for `x` as long as it conforms to (it "implements")
`SomeProtocol`. Thus, `fun_with_protocols()` is known as a "generic function" because
it accepts a *generalized* type instead of a specific type.

For more details, see the page about [protocols](/sharpy/manual/protocols).

## Parameterization

In Sharpy, a parameter is a compile-time variable that becomes a runtime
constant, and it's declared in square brackets on a function or struct.
Parameters allow for compile-time metaprogramming, which means you can generate
or modify code at compile time.

Many other languages use "parameter" and "argument" interchangeably, so be
aware that when we say things like "parameter" and "parametric function," we're
talking about these compile-time parameters. Whereas, a function "argument" is
a runtime value that's declared in parentheses.

Parameterization is a complex topic that's covered in much more detail in the
[Metaprogramming](/sharpy/manual/parameters/) section, but we want to break the
ice just a little bit here. To get you started, let's look at a parametric
function:

```sharpy
def repeat[count: Int](msg: str):
    @parameter # evaluate the following for loop at compile time
    for i in range(count):
        print(msg)
```

This function has one parameter of type `Int` and one argument of type
`str`. To call the function, you need to specify both the parameter and the
argument:

```sharpy
def call_repeat():
    repeat[3]("Hello")
    # Prints "Hello" 3 times
```

By specifying `count` as a parameter, the Sharpy compiler is able to optimize the
function because this value is guaranteed to not change at runtime. And the
`@parameter` decorator in the code tells the compiler to evaluate the `for` loop
at compile time, not runtime.

The compiler effectively generates a unique version of the `repeat()` function
that repeats the message only 3 times. This makes the code more performant
because there's less to compute at runtime.

Similarly, you can define a struct with parameters, which effectively allows
you to define variants of that type at compile-time, depending on the parameter
values.

For more detail on parameters, see the section on
[Metaprogramming](/sharpy/manual/parameters/).

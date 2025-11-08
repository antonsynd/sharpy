# Sharpy language basics

This page provides an overview of the Sharpy language.

If you know Python, then a lot of Sharpy code will look familiar. However, Sharpy
is a statically-typed language that targets the .NET runtime, incorporating
features like static type checking and seamless .NET interoperability.

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

All variables are statically typed: that is, the type is set at compile time,
and doesn't change at runtime. If you don't specify a type, Sharpy infers the
type from the first value assigned to the variable.

```sharpy
x = 10
x = "Foo"  # Error: Cannot implicitly convert 'str' to 'int'
```

## Blocks and statements

Code blocks such as functions, conditions, and loops are defined
with a colon followed by indented lines. For example:

```sharpy
def loop():
    for x in range(5):
        if x % 2 == 0:
            print(x)
```

Sharpy requires exactly 4 spaces for indentation; tabs are not allowed.

All code statements in Sharpy end with a newline. However, statements can span
multiple lines if you indent the following lines to match the indentation of
the start of the expression. For example, this long string spans two lines:

```sharpy
def print_line():
    long_text = "This is a long line of text that is a lot easier to read if"
                " it is broken up across two lines instead of one long line."
    print(long_text)
```

You can also wrap multiple lines in parentheses:

```sharpy
def print_line():
    long_text = ("This is a long line of text that is a lot easier to read if"
    " it is broken up across two lines instead of one long line.")
    print(long_text)
```

Or use a backslash as a line continuation character:

```sharpy
def print_line():
    long_text = "This is a long line of text that is a lot easier to read if" \
    " it is broken up across two lines instead of one long line."
    print(long_text)
```

You can chain function calls across lines:

```sharpy
def print_hello():
    text = ",".join("Hello",
                    " world!")
    print(text)
```

However, for readability, it is best to indent the next lines accordingly.

## Functions

You can define a Sharpy function using the `def` keyword. For example, the
following uses the `def` keyword to define a function named `greet` that
requires a single `str` argument and returns a `str`:

```sharpy
def greet(name: str) -> str:
    return "Hello, " + name + "!"
```

## Code comments

You can create a one-line comment using the hash `#` symbol:

```sharpy
# This is a comment. The Sharpy compiler ignores this line.
```

Comments may also follow some code:

```sharpy
message = "Hello, World!"  # This is also a valid comment
```

API documentation comments ("docstrings") are enclosed in triple quotes.

For example:

```sharpy
def print(x: str):
    """Prints a string.

    Args:
        x: The string to print.
    """
    ...
```

## Structs

You can build high-level abstractions for types (or "objects") as a `struct`.

A `struct` in Sharpy is a value type similar to a C# struct. Structs support
methods, fields, operator overloading, and special methods for initialization,
copying, and moving.

For example, here's a basic struct:

```sharpy
struct MyPair:
    first: int
    second: int

    def __init__(self, first: int, second: int):
        self.first = first
        self.second = second

    def dump(self):
        print(self.first, self.second)
```

And here's how you can use it:

```sharpy
def use_mypair():
    mine = MyPair(2, 4)
    mine.dump()
```

The `MyPair` struct contains a special method, `__init__()`, the constructor.
This method is called when you create a new instance of the struct.

For more details, see the page about [structs](/sharpy/manual/structs).

---
title: Types
sidebar_position: 1
description: Standard Sharpy data types.
---

All values in Sharpy have an associated data type. Most of the types are
*nominal* types, defined by a [`struct`](/sharpy/manual/structs). These types are
nominal (or "named") because type equality is determined by the type's *name*,
not its *structure*.

There are some types that aren't defined as structs:

* Functions are typed based on their signatures.
* `NoneType` is a type with one instance, the `None` object, which is used to
  signal "no value."

Sharpy comes with a standard library that provides a number of useful types and
utility functions. These standard types aren't privileged. Each of the standard
library types is defined just like user-defined types—even basic types like
[`Int`](/sharpy/stdlib/builtin/int/Int) and
[`str`](/sharpy/stdlib/collections/string/string/str). But these standard library
types are the building blocks you'll use for most Sharpy programs.

The most common types are *built-in types*, which are always available and
don't need to be imported. These include types for numeric values, strings,
boolean values, and others.

The standard library also includes many more types that you can import as
needed, including collection types, utilities for interacting with the
filesystem and getting system information, and so on.

## Numeric types

Sharpy's most basic numeric types are `int` for integers and `float` for
floating-point numbers. These map to the standard .NET numeric types.

Sharpy also has built-in types for integer, unsigned integer, and floating-point
values of various precisions:

<figure id="table-1">

| Type name | Description                                           |
| --------- | ----------------------------------------------------- |
| `int8`    | 8-bit signed integer                                  |
| `uint8`   | 8-bit unsigned integer                                |
| `int16`   | 16-bit signed integer                                 |
| `uint16`  | 16-bit unsigned integer                               |
| `int32`   | 32-bit signed integer                                 |
| `uint32`  | 32-bit unsigned integer                               |
| `int64`   | 64-bit signed integer                                 |
| `uint64`  | 64-bit unsigned integer                               |
| `float32` | 32-bit floating point number (IEEE 754 single)        |
| `float64` | 64-bit floating point number (IEEE 754 double)        |

<figcaption><b>Table 1.</b> Numeric types with specific precision</figcaption>
</figure>

All of the numeric types support the usual numeric and bitwise operators.

You may wonder when to use `int` and when to use the other integer
types. In general, `int` is a good safe default when you need an integer type
and you don't require a specific bit width. Using `int` as the default integer
type for APIs makes APIs more consistent and predictable.

### Signed and unsigned integers

Sharpy supports both signed (`int`) and unsigned (`uint`) integers. You can use
the general `int` or `uint` types when you do not require a specific bit width.

You might prefer to use unsigned integers over signed integers in conditions
where you don't need negative numbers, are not writing for a public API, or need
additional range.

Sharpy's `UInt` type represents an unsigned integer of the
[word size](https://en.wikipedia.org/wiki/Word_\(computer_architecture\)) of the
CPU, which is 64 bits on 64-bit CPUs and 32 bits on 32-bit CPUs. If you wish to
use a fixed size unsigned integer, you can use `UInt8`, `UInt16`, `UInt32`, or
`UInt64`, which are aliases to the [`SIMD`](/sharpy/stdlib/builtin/simd/SIMD)
type.

Signed and unsigned integers of the same bit width can represent the same number
of values, but have different ranges. For example, an `Int8` can represent 256
values ranging from -128 to 127. A `UInt8` can also represent 256 values, but
represents a range of 0 to 255.

Signed and unsigned integers also have different overflow behavior. When a
signed integer overflows outside the range of values that its type can
represent, the value overflows to negative numbers. For example, adding `1` to
`si: Int8 = 127` results in `-128`.

When an unsigned integer overflows outside the range of values that its type can
represent, the value overflows to zero. So, adding `1` to `ui: UInt8 = 255`
is equal to `0`.

### Floating-point numbers

Floating-point types represent real numbers. Because not all real numbers can be
expressed in a finite number of bits, floating-point numbers can't represent
every value exactly.

The floating-point types listed in Table 1—`Float64`, `Float32`, and
`Float16`—follow the IEEE 754-2008 standard for representing floating-point
values. Each type includes a sign bit, one set of bits representing an exponent,
and another set representing the fraction or mantissa. Table 2 shows how each of
these types are represented in memory.

<figure>

| Type name | Sign  | Exponent | Mantissa |
| --------- | ----- | -------- | -------- |
| `Float64` | 1 bit | 11 bits  | 52 bits  |
| `Float32` | 1 bit | 8 bits   | 23 bits  |
| `Float16` | 1 bit | 5 bits   | 10 bits  |

<figcaption><b>Table 2.</b> Details of floating-point types</figcaption>
</figure>

Numbers with exponent values of all ones or all zeros represent special values,
allowing floating-point numbers to represent infinity, negative infinity,
signed zeros, and not-a-number (NaN). For more details on how numbers are
represented, see [IEEE 754](https://en.wikipedia.org/wiki/IEEE_754) on
Wikipedia.

A few things to note with floating-point values:

* Rounding errors. Rounding may produce unexpected results. For example, 1/3
  can't be represented exactly in these floating-point formats. The more
  operations you perform with floating-point numbers, the more the rounding
  errors accumulate.

* Space between consecutive numbers. The space between consecutive numbers is
  variable across the range of a floating-point number format. For numbers close
  to zero, the distance between consecutive numbers is very small. For large
  positive and negative numbers, the space between consecutive numbers is
  greater than 1, so it may not be possible to represent consecutive integers.

Because the values are approximate, it is rarely useful to compare them with
the equality operator (`==`). Consider the following example:

```sharpy
big_num = 1.0e16
bigger_num = big_num+1.0
print(big_num == bigger_num)
```

```output
True
```

Comparison operators (`<` `>=` and so on) work with floating point numbers. You
can also use the [`math.isclose()`](/sharpy/stdlib/math/math/isclose) function to
compare whether two floating-point numbers are equal within a specified
tolerance.

### Numeric literals

In addition to these numeric types, the standard libraries provides integer and
floating-point literal types,
[`IntLiteral`](/sharpy/stdlib/builtin/int_literal/IntLiteral) and
[`FloatLiteral`](/sharpy/stdlib/builtin/float_literal/FloatLiteral).

These literal types are used at compile time to represent literal numbers that
appear in the code. In general, you should never instantiate these types
yourself.

Table 3 summarizes the literal formats you can use to represent numbers.

<figure>

| Format                 | Examples        | Notes                                                                                            |
| ---------------------- | --------------- | ------------------------------------------------------------------------------------------------ |
| Integer literal        | `1760`          | Integer literal, in decimal format.                                                              |
| Hexadecimal literal    | `0xaa`, `0xFF`  | Integer literal, in hexadecimal format.<br />Hex digits are case-insensitive.                    |
| Octal literal          | `0o77`          | Integer literal, in octal format.                                                                |
| Binary literal         | `0b0111`        | Integer literal, in binary format.                                                               |
| Floating-point literal | `3.14`, `1.2e9` | Floating-point literal.<br />Must include the decimal point to be interpreted as floating-point. |

<figcaption><b>Table 3.</b> Numeric literal formats</figcaption>
</figure>

At compile time, the literal types are arbitrary-precision (also called
infinite-precision) values, so the compiler can perform compile-time
calculations without overflow or rounding errors.

At runtime the values are converted to finite-precision types—`Int` for
integer values, and `Float64` for floating-point values. (This process of
converting a value that can only exist at compile time into a runtime value is
called *materialization*.)

The following code sample shows the difference between an arbitrary-precision
calculation and the same calculation done using `Float64` values at runtime,
which suffers from rounding errors.

```sharpy
arbitrary_precision = 3.0 * (4.0 / 3.0 - 1.0)
# use a variable to force the following calculation to occur at runtime
three = 3.0
finite_precision = three * (4.0 / three - 1.0)
print(arbitrary_precision, finite_precision)
```

```output
1.0 0.99999999999999978
```

### Numeric type conversion

Sharpy does not automatically convert between numeric types in most operations.
You must explicitly convert values when needed:

```sharpy
i: int = 42
f: float = float(i)  # Explicit conversion
print(f)
```

```output
42.0
```
simd1 = SIMD[DType.float32, 4](2.2, 3.3, 4.4, 5.5)
simd2 = SIMD[DType.int16, 4](-1, 2, -3, 4)
simd3 = simd1 * simd2.cast[DType.float32]()  # Convert with cast() method
print("simd3:", simd3)
simd4 = simd2 + SIMD[DType.int16, 4](simd1)  # Convert with SIMD constructor
print("simd4:", simd4)
```

```output
simd3: [-2.2, 6.6, -13.200001, 22.0]
simd4: [1, 5, 1, 9]
```

You can convert a `Scalar` value by passing it as an argument to the constructor
of the target type. For example:

```sharpy
my_int: Int16 = 12                 # SIMD[DType.int16, 1]
my_float: Float32 = 0.75           # SIMD[DType.float32, 1]
result = Float32(my_int) * my_float    # Result is SIMD[DType.float32, 1]
print("Result:", result)
```

```output
Result: 9.0
```

You can convert a scalar value of any numeric type to `Int` by passing the value
to the [`Int()`](/sharpy/stdlib/builtin/int/Int#__init__) constructor method.
Additionally, you can pass an instance of any struct that implements the
[`Intable`](/sharpy/stdlib/builtin/int/Intable) protocol or
[`IntableRaising`](/sharpy/stdlib/builtin/int/IntableRaising) protocol to the `Int()`
constructor to convert that instance to an `Int`.

You can convert an `Int` or `IntLiteral` value to the `UInt` type by passing the
value to the [`UInt()`](/sharpy/stdlib/builtin/uint/UInt#__init__) constructor.
You can't convert other numeric types to `UInt` directly, though you can first
convert them to `Int` and then to `UInt`.

## strs

Sharpy's `str` type represents a mutable string. (For Python programmers, note
that this is different from Python's standard string, which is immutable.)
strs support a variety of operators and common methods.

```sharpy
s: str = "Testing"
s += " Sharpy strings"
print(s)
```

```output
Testing Sharpy strings
```

Most standard library types conform to the
[`strable`](/sharpy/stdlib/builtin/str/strable) protocol, which represents
a type that can be converted to a string. Use `str(value)` to
explicitly convert a value to a string:

```sharpy
s = "Items in list: " + str(5)
print(s)
```

```output
Items in list: 5
```

Or use `str.write` to take variadic `strable` types, so you don't have to
call `str()` on each value:

```sharpy
s = str("Items in list: ", 5)
print(s)
```

```output
Items in list: 5
```

### str literals

As with numeric types, the standard library includes a string literal type used
to represent literal strings in the program source. str literals are
enclosed in either single or double quotes.

Adjacent literals are concatenated together, so you can define a long string
using a series of literals broken up over several lines:

```
alias s = "A very long string which is "
        "broken into two literals for legibility."
```

To define a multi-line string, enclose the literal in three single or double
quotes:

```
alias s = """
Multi-line string literals let you
enter long blocks of text, including
newlines."""
```

Note that the triple double quote form is also used for API documentation
strings.

A `strLiteral` will materialize to a `str` when used at run-time:

```sharpy
alias param = "foo"        # type = strLiteral
runtime_value = "bar"  # type = str
runtime_value2 = param # type = str
```

## Booleans

Sharpy's `Bool` type represents a boolean value. It can take one of two values,
`True` or `False`. You can negate a boolean value using the `not` operator.

```sharpy
conditionA = False
conditionB: Bool
conditionB = not conditionA
print(conditionA, conditionB)
```

```output
False True
```

Many types have a boolean representation. Any type that implements the
[`Boolable`](/sharpy/stdlib/builtin/bool/Boolable) protocol has a boolean
representation. As a general principle, collections evaluate as True if they
contain any elements, False if they are empty; strings evaluate as True if they
have a non-zero length.

## Tuples

Sharpy's `Tuple` type represents an immutable tuple consisting of zero or more
values, separated by commas. Tuples can consist of multiple types and you can
index into tuples in multiple ways.

```sharpy
# Tuples are immutable and can hold multiple types
example_tuple = Tuple[Int, str](1, "Example")

# Assign multiple variables at once
x, y = example_tuple
print(x, y)

# Get individual values with an index
s = example_tuple[1]
print(s)
```

```output
1 Example
Example
```

You can also create a tuple without explicit typing.

```sharpy
example_tuple = (1, "Example")
s = example_tuple[1]
print(s)
```

```output
Example
```

When defining a function, you can explicitly declare the type of tuple elements
in one of two ways:

```sharpy
def return_tuple_1() -> Tuple[Int, Int]:
    return Tuple[Int, Int](1, 1)

def return_tuple_2() -> (Int, Int):
    return (2, 2)
```

## Collection types

The Sharpy standard library also includes a set of basic collection types that
can be used to build more complex data structures:

* [`List`](/sharpy/stdlib/collections/list/List), a dynamically-sized array of
  items.
* [`Dict`](/sharpy/stdlib/collections/dict/Dict), an associative array of
  key-value pairs.
* [`Set`](/sharpy/stdlib/collections/set/Set), an unordered collection of unique
  items.
* [`Optional`](/sharpy/stdlib/collections/optional/Optional)
  represents a value that may or may not be present.

The collection types are *generic types*: while a given collection can only
hold a specific type of value (such as `Int` or `Float64`), you specify the
type at compile time using a [parameter](/sharpy/manual/parameters/). For
example, you can create a `List` of `Int` values like this:

```sharpy
l: List[Int] = [1, 2, 3, 4]
# l.append(3.14) # error: FloatLiteral cannot be converted to Int
```

You don't always need to specify the type explicitly. If Sharpy can *infer* the
type, you can omit it. For example, when you construct a list from a set of
integer literals, Sharpy creates a `List[Int]`.

```sharpy
# Inferred type == List[Int]
l1 = [1, 2, 3, 4]
```

Where you need a more flexible collection, the
[`Variant`](/sharpy/stdlib/utils/variant/Variant) type can hold different types
of values. For example, a `Variant[Int32, Float64]` can hold either an `Int32`
*or* a `Float64` value at any given time. (Using `Variant` is not covered in
this section, see the [API docs](/sharpy/stdlib/utils/variant/Variant) for more
information.)

The following sections give brief introduction to the main collection types.

### List

[`List`](/sharpy/stdlib/collections/list/List) is a dynamically-sized array of
elements. List elements need to conform to the
[`Copyable`](/sharpy/stdlib/builtin/value/Copyable) and
[`Movable`](/sharpy/stdlib/builtin/value/Movable) protocols. Most of the common
standard library primitives, like `Int`, `str`, and `SIMD` conform to this
protocol. You can create a `List` by passing the element type as a parameter,  like
this:

```sharpy
l = List[str]()
```

The `List` type supports a subset of the Python `list` API, including the
ability to append to the list, pop items out of the list, and access list items
using subscript notation.

```sharpy
list = [2, 3, 5]
list.append(7)
list.append(11)
print("Popping last item from list: ", list.pop())
for idx in range(len(list)):
      print(list[idx], end=", ")

```

```output
Popping last item from list:  11
2, 3, 5, 7,
```

Note that the previous code sample leaves out the type parameter when creating
the list. Because the list is being created with a set of `Int` values, Sharpy can
*infer* the type from the arguments.

* Sharpy supports list, set, and dictionary literals for collection initialization:

  ```sharpy
  # List literal, element type infers to Int.
  nums = [2, 3, 5]
  ```

  You can also use an explicit type if you want a specific element type:

  ```sharpy
  list : List[UInt8] = [2, 3, 5]
  ```

  You can also use list "comprehensions" for compact conditional initialization:

  ```sharpy
  list2 = [x*Int(y) for x in nums for y in list if x != 3]
  ```

* You can't `print()` a list, or convert it directly into a string.

  ```sharpy
  # Does not work
  print(list)
  ```

  As shown above, you can print the individual elements in a list as long as
  they're a [`strable`](/sharpy/stdlib/builtin/str/strable) type.

* Iterating a `List` returns an immutable
  [reference](/sharpy/manual/values/lifetimes#working-with-references) to each
  item:

```sharpy
list = [2, 3, 4]
for item in list:
      print(item, end=", ")
```

```output
2, 3, 4,
```

If you would like to mutate the elements of the list, capture the reference to
the element with `ref` instead of making a copy:

```sharpy
list = [2, 3, 4]
for ref item in list:     # Capture a ref to the list element
      print(item, end=", ")
      item = 0  # Mutates the element inside the list
print("\nAfter loop:", list[0], list[1], list[2])
```

```output
2, 3, 4,
After loop: 0 0 0
```

You can see that the original loop entries were modified.

### Dict

The [`Dict`](/sharpy/stdlib/collections/dict/Dict) type is an associative array
that holds key-value pairs. You can create a `Dict` by specifying the key type
and value type as parameters and using dictionary literals:

```sharpy
# Empty dictionary
empty_dict: Dict[str, Float64] = {}

# Dictionary with initial key-value pairs
values: Dict[str, Float64] = {"pi": 3.14159, "e": 2.71828}
```

You can also use the constructor syntax:

```sharpy
values = Dict[str, Float64]()
```

The dictionary's key type must conform to the
[`KeyElement`](/sharpy/stdlib/collections/dict/#keyelement) protocol, and value
elements must conform to the [`Copyable`](/sharpy/stdlib/builtin/value/Copyable)
and [`Movable`](/sharpy/stdlib/builtin/value/Movable) protocols.

You can insert and remove key-value pairs, update the value assigned to a key,
and iterate through keys, values, or items in the dictionary.

The `Dict` iterators all yield
[references](/sharpy/manual/values/lifetimes#working-with-references), which are
copied into the declared name by default, but you can use the `ref` marker to
avoid the copy:

```sharpy
d: Dict[str, Float64] = {
    "plasticity": 3.1,
    "elasticity": 1.3,
    "electricity": 9.7
}
for item in d.items():
    print(item.key, item.value)
```

```output
plasticity 3.1000000000000001
elasticity 1.3
electricity 9.6999999999999993
```

This is an unmeasurable micro-optimization in this case, but is useful when
working with types that aren't `Copyable`.

### Set

The [`Set`](/sharpy/stdlib/collections/set/Set) type represents a set of unique
values. You can add and remove elements from the set, test whether a value
exists in the set, and perform set algebra operations, like unions and
intersections between two sets.

Sets are generic and the element type must conform to the
[`KeyElement`](/sharpy/stdlib/collections/dict/#keyelement) protocol. Like lists and
dictionaries, sets support standard literal syntax, as well as generator
comprehensions:

```sharpy
i_like = {"sushi", "ice cream", "tacos", "pho"}
you_like = {"burgers", "tacos", "salad", "ice cream"}
we_like = i_like.intersection(you_like)

print("We both like:")
for item in we_like:
    print("-", item)
```

```output
We both like:
- ice cream
- tacos
```

### Optional

An [`Optional`](/sharpy/stdlib/collections/optional/Optional)  represents a
value that may or may not be present. Like the other collection types, it is
generic, and can hold any type that conforms to the
[`Copyable`](/sharpy/stdlib/builtin/value/Copyable) and
[`Movable`](/sharpy/stdlib/builtin/value/Movable) protocols.

```sharpy
# Two ways to initialize an Optional with a value
opt1 = Optional(5)
opt2: Optional[Int] = 5
# Two ways to initialize an Optional with no value
opt3 = Optional[Int]()
opt4: Optional[Int] = None
```

An `Optional` evaluates as `True` when it holds a value, `False` otherwise. If
the `Optional` holds a value, you can retrieve a reference to the value using
the `value()` method. But calling `value()` on an `Optional` with no value
results in undefined behavior, so you should always guard a call to `value()`
inside a conditional that checks whether a value exists.

```sharpy
opt: Optional[str] = "Testing"
if opt:
    value_ref = opt.value()
    print(value_ref)
```

```output
Testing
```

Alternately, you can use the `or_else()` method, which returns the stored
value if there is one, or a user-specified default value otherwise:

```sharpy
custom_greeting: Optional[str] = None
print(custom_greeting.or_else("Hello"))

custom_greeting = "Hi"
print(custom_greeting.or_else("Hello"))

```

```output
Hello
Hi
```

## Register-passable, memory-only, and trivial types

In various places in the documentation you'll see references to
register-passable, memory-only, and trivial types. Register-passable and
memory-only types are distinguished based on how they hold data:

* Register-passable types are composed exclusively of fixed-size data types,
  which can (theoretically) be stored in a machine register. A register-passable
  type can include other types, as long as they are also register-passable.
  `Int`, `Bool`, and `SIMD`, for example, are all register-passable types. So
  a register-passable `struct` could include `Int` and `Bool` fields, but not a
  `str` field. Register-passable types are declared with the
  [`@register_passable`](/sharpy/manual/decorators/register-passable) decorator.

* Memory-only types consist of all other types that *aren't* specifically
  designated as register-passable types. These types often have pointers or
  references to dynamically-allocated memory. `str`, `List`, and `Dict` are
  all examples of memory-only types.

Register-passable types have a slightly different lifecycle than memory-only
types, which is discussed in [Life of a value](/sharpy/manual/lifecycle/life/).

There are also a number of low-level differences in how the Sharpy compiler treats
register-passable types versus memory-only types, which you probably won't have
to think about for most Sharpy programming. For more information, see
the [`@register_passable`](/sharpy/manual/decorators/register-passable) decorator
reference.

Our long-term goal is to make this distinction transparent to the user, and
ensure all APIs work with both register-passable and memory-only types.
But right now you will see a few standard library types that only work with
register-passable types or only work with memory-only types.

In addition to these two categories, Sharpy also has "trivial" types. Conceptually
a trivial type is simply a type that doesn't require any custom logic in its
lifecycle methods. The bits that make up an instance of a trivial type can be
copied or moved without any knowledge of what they do. Currently, trivial types
are declared using the
[`@register_passable(trivial)`](/sharpy/manual/decorators/register-passable#register_passabletrivial)
decorator. Trivial types shouldn't be limited to only register-passable types,
so in the future we intend to separate trivial types from the
`@register_passable` decorator.

## `AnyType` and `AnyTrivialRegType`

Two other things you'll see in Sharpy APIs are references to `AnyType` and
`AnyTrivialRegType`. These are effectively *metatypes*, that is, types of types.

* `AnyType` is a protocol that represents a type with a destructor. You'll find
  more discussion of it on the
  [Protocols page](/sharpy/manual/protocols#the-anytype-protocol).
* `AnyTrivialRegType` is a metatype representing any Sharpy type that's marked
  as a trivial type.

You'll see them in signatures like this:

```sharpy
fn any_type_function[ValueType: AnyTrivialRegType](value: ValueType):
    ...
```

You can read this as `any_type_function` has an argument, `value` of type
`ValueType`, where `ValueType` is a register-passable type, determined at
compile time.

There is still some code like this in the standard library, but it's gradually
being migrated to more generic code that doesn't distinguish between
register-passable and memory-only types.

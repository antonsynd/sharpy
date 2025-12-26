Sharpy includes a variety of operators for manipulating values of different types.
Generally, the operators are equivalent to those found in Python, working with
basic .NET types. Additionally, Sharpy allows you to define the behavior of most
of these operators for your own custom types by implementing special *dunder*
(double underscore) methods.

This document contains the following three sections:

- [Operators and expressions](#operators-and-expressions) discusses Sharpy's
  built-in operators and how they work with commonly used Sharpy types.
- [Implement operators for custom types](#implement-operators-for-custom-types)
  describes the dunder methods that you can implement to support using operators
  with custom structs that you create.
- [An example of implementing operators for a custom
  type](#an-example-of-implementing-operators-for-a-custom-type) shows a
  progressive example of writing a custom struct with support for several
  operators.

## Operators and expressions

This section lists the operators that Sharpy supports, their order or precedence
and associativity, and describes how these operators behave with several
commonly used built-in types.

### Operator precedence and associativity

The table below lists the various Sharpy operators, along with their order of
precedence and associativity (also referred to as grouping). This table lists
operators from the highest precedence to the lowest precedence.

| **Operators** | **Description** | **Associativity (Grouping)** |
| ------------- | --------------- | ----------------- |
| `()` | Parenthesized expression | Left to right |
| `x[index]`, `x[index:index]` | Subscripting, slicing | Left to right |
| `**` | Exponentiation | Right to left |
| `+x`, `-x`, `~x` | Positive, negative, bitwise NOT | Right to left |
| `*`, `@`, `/`, `//`, `%` | Multiplication, matrix, division, floor division, remainder | Left to right |
| `+`, `–` | Addition and subtraction | Left to right |
| `<<`, `>>` | Shifts | Left to right |
| `&` | Bitwise AND | Left to right |
| `^` | Bitwise XOR | Left to right |
| `\|` | Bitwise OR | Left to right |
| `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons, membership tests, identity tests | Left to Right |
| `not x` | Boolean NOT | Right to left |
| `x and y` | Boolean AND | Left to right |
| `x or y` | Boolean OR | Left to right |
| `if-else` | Conditional expression | Right to left |

Sharpy supports the same operators as Python (plus a few extensions), and they
have the same precedence levels. For example, the following arithmetic
expression evaluates to 40:

```sharpy
5 + 4 * 3 ** 2 - 1
```

It is equivalent to the following parenthesized expression to explicitly control
the order of evaluation:

```sharpy
(5 + (4 * (3 ** 2))) - 1
```

Associativity defines how operators of the same precedence level are grouped
into expressions. The table indicates whether operators of a given level are
left- or right-associative. For example, multiplication and division are left
associative, so the following expression results in a value of 3:

```sharpy
3 * 4 / 2 / 2
```

It is equivalent to the following parenthesized expression to explicitly control
the order of evaluation:

```sharpy
((3 * 4) / 2) / 2
```

Whereas in the following, exponentiation operators are right associative
resulting in a value of 264,144:

```sharpy
4 ** 3 ** 2
```

It is equivalent to the following parenthesized expression to explicitly control
the order of evaluation:

```sharpy
4 ** (3 ** 2)
```

:::note

Sharpy also uses the caret (`^`) as the [*transfer
sigil*](/sharpy/manual/values/ownership#transfer-arguments-var-and-). In
expressions where its use might be ambiguous, Sharpy treats the character as the
bitwise XOR operator. For example, `x^+1` is treated as `(x)^(+1)`.

:::

### Arithmetic and bitwise operators

[Numeric types](/sharpy/manual/types#numeric-types) describes the different
numeric types provided by the Sharpy standard library. The arithmetic and bitwise
operators have slightly different behavior depending on the types of values
provided.

#### `Int` and `UInt` values

The [`Int`](/sharpy/stdlib/builtin/int/Int) and
[`UInt`](/sharpy/stdlib/builtin/uint/UInt) types represent signed and unsigned
integers of the [word
size](https://en.wikipedia.org/wiki/Word_(computer_architecture)) of the CPU,
typically 64 bits or 32 bits.

The `Int` and `UInt` types support all arithmetic operators except matrix
multiplication (`@`), as well as all bitwise and shift operators. If both
operands to a binary operator are `Int` values the result is an `Int`, if both
operands are `UInt` values the result is a `UInt`, and if one operand is `Int`
and the other `UInt` the result is an `Int`. The one exception for these types
is true division, `/`, which always returns a `Float64` type value.

```sharpy
a_int: Int = -7
b_int: Int = 4
sum_int = a_int + b_int  # Result is type Int
print("Int sum:", sum_int)

i_uint: UInt = 9
j_uint: UInt = 8
sum_uint = i_uint + j_uint  # Result is type UInt
print("UInt sum:", sum_uint)

sum_mixed = a_int + i_uint  # Result is type Int
print("Mixed sum:", sum_mixed)

quotient_int = a_int / b_int  # Result is type Float64
print("Int quotient:", quotient_int)
quotient_uint = i_uint / j_uint  # Result is type Float64
print("UInt quotient:", quotient_uint)
```

```output
Int sum: -3
UInt sum: 17
Mixed sum: 2
Int quotient: -1.75
UInt quotient: 1.125
```

#### `int` and `float` values

The `int` and `float` types represent the standard .NET integer and floating-point
numeric types.

The `int` type supports all arithmetic operators except matrix multiplication (`@`),
as well as all bitwise and shift operators. The `float` type supports arithmetic
operators but not bitwise operations.

```sharpy
a_int: int = -7
b_int: int = 4
sum_int = a_int + b_int  # Result is type int
print("int sum:", sum_int)

x_float: float = 3.5
y_float: float = 2.0
sum_float = x_float + y_float  # Result is type float
print("float sum:", sum_float)

quotient_int = a_int / b_int  # Result is type float
print("int quotient:", quotient_int)
```

```output
int sum: -3
float sum: 5.5
int quotient: -1.75
```

There are three operators related to division:

- `/`, the "true division" operator, performs division and returns a floating-point
  result. For example:

    ```sharpy
    num_float = 7.0
    denom_float = 2.0
    num_int = 7
    denom_int = 2

    # Result is float
    true_quotient_float = num_float / denom_float
    print("True float division:", true_quotient_float)

    # Result is float (converts from int)
    true_quotient_int = num_int / denom_int
    print("True int division:", true_quotient_int)
    ```

    ```output
    True float division: 3.5
    True int division: 3.5
    ```

- `//`, the "floor division" operator, performs division and *rounds down* the
  result to the nearest integer. For example:

    ```sharpy
    # Result is float
    floor_quotient_float = num_float // denom_float
    print("Floor float division:", floor_quotient_float)

    # Result is int
    floor_quotient_int = num_int // denom_int
    print("Floor int division:", floor_quotient_int)
    ```

    ```output
    Floor float division: 3.0
    Floor int division: 3
    ```

- `%`, the modulo operator, returns the remainder after dividing the numerator
  by the denominator an integral number of times. For example:

    ```sharpy
    remainder_int = num_int % denom_int
    print("Modulo int:", remainder_int)

    # Relationship: num == denom * (num // denom) + (num % denom)
    result_int = denom_int * floor_quotient_int + remainder_int
    print("Result int:", result_int)
    ```

    ```output
    Modulo int: 1
    Result int: 7
    ```


#### Literal values

Literal numeric values in Sharpy are implicitly typed as `int` or `float` based on
context. When used directly, integer literals become `int` values and decimal
literals become `float` values.

```sharpy
x = 42      # int
y = 3.14    # float
```

Literal values support all standard arithmetic operators. Integer literals support
bitwise and shift operators as well.

### Comparison operators

Sharpy supports a standard set of comparison operators: `==`, `!=`, `<`, `<=`,
`>`, and `>=`. These operators perform standard numerical comparison and return a
`bool` result.

```sharpy
a = 5
b = 10
result = a < b  # true
```

For custom types, comparison operators can be overloaded by implementing the
appropriate dunder methods (see [Comparison operator dunder methods](#comparison-operator-dunder-methods)).

### String operators

As discussed in [Strings](/sharpy/manual/types#strings), the
[`String`](/sharpy/stdlib/collections/string/string/String) type represents a
mutable string value. In contrast, the
[`StringLiteral`](/sharpy/stdlib/builtin/string_literal/StringLiteral) type
represents a literal string that is embedded into your compiled program, but
at run-time it materializes to a `String`, allowing you to mutate it:

```sharpy
message = "Hello"       # type = String
alias name = " Pat"       # type = StringLiteral
greeting = " good Day!"  # type = String

# Mutate the original `message` String
message += name
message += greeting
print(message)
```

```output
Hello Pat good day!
```

This means that `StringLiteral` values can be intermixed with `String` values in
any runtime expression without having to convert between types.

#### String concatenation

The `+` operator performs string concatenation. The `StringLiteral` type
supports compile-time string concatenation.

```sharpy
alias last_name = "Curie"

# Compile-time StringLiteral alias
alias marie = "Marie " + last_name
print(marie)

# Compile-time concatenation before materializing to a run-time `String`
pierre = "Pierre " + last_name
print(pierre)
```

:::tip
When concatenating multiple values together to form a `String`, using the
multi-argument `String()` constructor is more performant than using multiple
`+` concatenation operators and can improve code readability. For example,
instead of writing this:

```sharpy
result = "The point at (" + String(x) + ", " + String(y) + ")"
```

you can write:

```sharpy
result = String("The point at (", x, ", ", y, ")")
```

This will write the underlying data using a stack buffer, and will only allocate
and memcpy to the heap once.

:::

#### String replication

The `*` operator replicates a `String` a specified number of times. For example:

```sharpy
str1: String = "la"
str2 = str1 * 5
print(str2)
```

```output
lalalalala
```

`StringLiteral` supports the `*` operator for both compile-time and run-time
string replication. The following examples perform compile-time string
replication resulting in `StringLiteral` values:

```sharpy
alias divider1 = "=" * 40
alias symbol = "#"
alias divider2 = symbol * 40

# You must define the following function using `fn` because an alias
# initializer cannot call a function that can potentially raise an error.
fn generate_divider(char: String, repeat: Int) -> String:
    return char * repeat

alias divider3 = generate_divider("~", 40)  # Evaluated at compile-time

print(divider1)
print(divider2)
print(divider3)
```

```output
========================================
########################################
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
```

In contrast, the following examples perform run-time string replication
resulting in `String` values:

```sharpy
repeat = 40
div1 = "^" * repeat
print(div1)
print("_" * repeat)
```

```output
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
________________________________________
```

#### String comparison

`String` and `StringLiteral` values can be compared using standard
lexicographical ordering, producing a `Bool`. For example, "Zebra" is treated as
less than "ant" because upper case letters occur before lower case letters in
the character encoding.

```sharpy
animal: String = "bird"

is_cat_eq = "cat" == animal
print('Is "cat" equal to "{}"?'.format(animal), is_cat_eq)

is_cat_ne = "cat" != animal
print('Is "cat" not equal to "{}"?'.format(animal), is_cat_ne)

is_bird_eq = "bird" == animal
print('Is "bird" equal to "{}"?'.format(animal), is_bird_eq)

is_cat_gt = "CAT" > animal
print('Is "CAT" greater than "{}"?'.format(animal), is_cat_gt)

is_ge_cat = animal >= "CAT"
print('Is "{}" greater than or equal to "CAT"?'.format(animal), is_ge_cat)
```

```output
Is "cat" equal to "bird"? False
Is "cat" not equal to "bird"? True
Is "bird" equal to "bird"? True
Is "CAT" greater than "bird"? False
Is "bird" greater than or equal to "CAT"? True
```

#### Substring testing

`String`, `StringLiteral`, and `StringSlice` support using the `in` operator to
produce a `Bool` result indicating whether a given substring appears within
another string. The operator is overloaded so that you can use any combination
of `String` and `StringLiteral` for both the substring and the string to test.

```sharpy
food: String = "peanut butter"

if "nut" in food:
    print("It contains a nut")
else:
    print("It doesn't contain a nut")
```

```output
It contains a nut
```

#### String indexing and slicing

`String`, `StringLiteral`, and `StringSlice` allow you to use indexing to return
a single character. Character positions are identified with a zero-based index
starting from the first character. You can also specify a negative index to
count backwards from the end of the string, with the last character identified
by index -1. Specifying an index beyond the bounds of the string results in a
run-time error.

```sharpy
alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"  # String type value
print(alphabet[0], alphabet[-1])

# The following would produce a run-time error
# print(alphabet[45])
```

```output
A Z
```

The `String` and `StringSlice` types—but *not* the `StringLiteral` type—also
support slices to return a substring from the original `String`. Providing a
slice in the form `[start:end]` returns a substring starting with the character
index specified by `start` and continuing up to but not including the character
at index `end`. You can use positive or negative indexing for both the start and
end values. Omitting `start` is the same as specifying `0`, and omitting `end`
is the same as specifying 1 plus the length of the string.

```sharpy
alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" # String type value
print(alphabet[1:4])  # The 2nd through 4th characters
print(alphabet[:6])   # The first 6 characters
print(alphabet[-6:])  # The last 6 characters
```

```output
BCD
ABCDEF
UVWXYZ
```

You can also specify a slice with a `step` value, as in `[start:end:step]`
indicating the increment between subsequent indices of the slide. (This is also
sometimes referred to as a "stride.") If you provide a negative value for
`step`, characters are selected in reverse order starting with `start` but then
with *decreasing* index values up to but not including `end`.

```sharpy
print(alphabet[1:6:2])     # The 2nd, 4th, and 6th characters
print(alphabet[-1:-4:-1])  # The last 3 characters in reverse order
print(alphabet[::-1])      # The entire string reversed
```

```output
BDF
ZYX
ZYXWVUTSRQPONMLKJIHGFEDCBA
```

### In-place assignment operators

Mutable types that support binary arithmetic, bitwise, and shift operators
typically support equivalent in-place assignment operators. That means that for
a type that supports the `+` operator, the following two statements are
essentially equivalent:

```sharpy
a = a + b
a += b
```

However there is a subtle difference between the two. In the first example, the
expression `a + b` produces a new value, which is then assigned to `a`. In
contrast, the second example does an in-place modification of the value
currently assigned to `a`. For register-passable types, the compiled results
might be equivalent at run-time. But for a memory-only type, the first example
allocates storage for the result of `a + b` and then assigns the value to the
variable, whereas the second example can do an in-place modification of the
existing value.

:::note

A type must explicitly implement in-place assignment methods, so you might
encounter some types where in-place equivalents are not supported.

:::

### Type merging

When an expression involves values of different types,
Sharpy needs to statically determine the return type of the expression. This
process is called *type merging*. By default, Sharpy determines type merging
based on implicit conversions. Individual structs can also define custom type
merging behavior.

The following code demonstrates type merging based on implicit conversions:

```sharpy
list = [0.5, 1, 2]
for value in list:
    print(value)
```

```output
0.5
1.0
2.0
```

Here, the list literal includes both float and integer literals, which
materialize as `Float64` and `Int`, respectively. Since `Int` can be
implicitly converted to `Float64`, the result is a `List[Float64]`.

Here's an example of where type merging fails:

```sharpy
a: Int = 0
b: String = "Hello"
c = a if a > 0 else b   # Error: value of type 'Int' is not compatible with
                        # value of type 'String'sharpy
```

In this case, `Int` can't be implicitly converted to a `String`, and
`String` can't be implicitly converted to an `Int`, so type merging fails. This
is the correct result: there's no way for Sharpy to know what type you want `c` to
take. You can fix this by adding an explicit conversion:

```sharpy
c = String(a) if a > 0 else b
```

Individual structs can define custom type merging logic by defining a
`__merge_with__()` dunder method. For example:

```sharpy
@fieldwise_init
struct MyType(Movable, Copyable):
    val: Int

    def __bool__(self) -> Bool:
        return self.val > 0

    def __merge_with__[other_type: __type_of(Int)](self) -> Int:
        return Int(self.val)

def main():
    i = 0
    m = MyType(9)
    print(i if i > 0 else m)  # prints "9"
```

If either type in the expression defines a custom `__merge_with__()` dunder
for merging with the other type, this type takes precedence over any implicit
conversions. (Note that the result type doesn't have to be either of the input
types, it could be a third type.)

A type can declare multiple `__merge_with__()` overrides for different types.

At a high level, the logic for merging two types goes like this:

- Does either type define a `__merge_with__()` method for the other type? If so,
  the returned value determines the target type.
  - If **both** types define a `__merge_with__()` method for the other type,
    the two methods must both return the same type, or the conversion fails.
  - Both types must be implicitly convertible to the target type (a type is
    always implicitly convertible to itself).
- Is either type implicitly convertible to the other type?
  - If only one type is implicitly convertible to the other type, convert it.
  - If both types are convertible to the other type, the conversion is
    ambiguous, and it fails.

For more background on type merging and the `__merge_with__()` dunder, see the
proposal,
[Customizable Type Merging in Sharpy](https://github.com/modular/modular/blob/main/sharpy/proposals/custom-type-merging.md).

## Implement operators for custom types

When you create a custom struct, Sharpy allows you to define the behavior of many
of the built-in operators for that type by implementing special *dunder* (double
underscore) methods. This section lists the dunder methods associated with the
operators and briefly describes the requirements for implementing them.

:::note

Currently, Sharpy doesn't support defining arbitrary custom operators (for
example, `-^-`). You can define behaviors for only the operators listed in the
following subsections.

:::

### Unary operator dunder methods

A unary operator invokes an associated dunder method on the value to which it
applies. The supported unary operators and their corresponding methods are shown
in the table below.

| **Operator**    | **Dunder method** |
| --------------- | ----------------- |
| `+` positive    | `__pos__()` |
| `-` negative    | `__neg__()` |
| `~` bitwise NOT | `__invert__()`  |

For each of these methods that you decide to implement, you should return either
the original value if unchanged, or a new value representing the result of the
operator. For example, you could implement the `-` negative operator for a
`MyInt` struct like this:

```sharpy
@fieldwise_init
struct MyInt:
    value: Int

    def __neg__(self) -> Self:
        return Self(-self.value)
```

### Binary arithmetic, shift, and bitwise operator dunder methods

When you have a binary expression like `a + b`, there are two possible dunder
methods that could be invoked.

Sharpy first determines whether the left-hand side value (`a` in this example) has
a "normal" version of the `+` operator's dunder method defined that accepts a
value of the right-hand side's type. If so, it then invokes that method on the
left-hand side value and passes the right-hand side value as an argument.

If Sharpy doesn't find a matching "normal" dunder method on the left-hand side
value, it then checks whether the right-hand side value has a "reflected"
(sometimes referred to as "reversed") version of the `+` operator's dunder
method defined that accepts a value of the left-hand side's type. If so, it then
invokes that method on the right-hand side value and passes the left-hand side
value as an argument.

For both the normal and the reflected versions, the dunder method should return
a new value representing the result of the operator.

Additionally, there are dunder methods corresponding to the in-place assignment
versions of the operators. These methods receive the right-hand side value as an
argument and the methods should modify the existing left-hand side value to
reflect the result of the operator.

The table below lists the various binary arithmetic, shift, and bitwise
operators and their corresponding normal, reflected, and in-place dunder
methods.

| **Operator** | **Normal** | **Reflected** | **In-place** |
| ------------ | ---------- | ------------- | ------------ |
| `+` addition | `__add__()` | `__radd__()` | `__iadd__()` |
| `-` subtraction | `__sub__()`  | `__rsub__()`  | `__isub__()` |
| `*` multiplication | `__mul__()`  | `__rmul__()`  | `__imul__()` |
| `/` division | `__truediv__()`  | `__rtruediv__()`  | `__itruediv__()` |
| `//` floor division | `__floordiv__()` | `__rfloordiv__()` | `__ifloordiv__()` |
| `%` modulus/remainder | `__mod__()`  | `__rmod__()`  | `__imod__()` |
| `**` exponentiation | `__pow__()`  | `__rpow__()`  | `__ipow__()` |
| `@` matrix multiplication | `__matmul__()` | `__rmatmul__()` | `__imatmul__()` |
| `<<` left shift | `__lshift__()` | `__rlshift__()` | `__ilshift__()` |
| `>>` right shift | `__rshift__()` | `__rrshift__()` | `__irshift__()` |
| `&` bitwise AND | `__and__()`  | `__rand__()`  | `__iand__()` |
| `\|` bitwise OR | `__or__()`  | `__ror__()`  | `__ior__()`  |
| `^` bitwise XOR | `__xor__()`  | `__rxor__()`  | `__ixor__()`  |

As an example, consider implementing support for all of the `+` operator dunder
methods for a custom `MyInt` struct. This shows supporting adding two `MyInt`
instances as well as adding a `MyInt` and an `Int`. We can support the case of
having the `Int` as the right-hand side argument by overloaded the definition of
`__add__()`. But to support the case of having the `Int` as the left-hand side
argument, we need to implement an `__radd__()` method, because the built-in
`Int` type doesn't have an `__add__()` method that supports our custom `MyInt`
type.

```sharpy
@fieldwise_init
struct MyInt:
    value: Int

    def __add__(self, rhs: MyInt) -> Self:
        return MyInt(self.value + rhs.value)

    def __add__(self, rhs: Int) -> Self:
        return MyInt(self.value + rhs)

    def __radd__(self, lhs: Int) -> Self:
        return MyInt(self.value + lhs)

    def __iadd__(mut self, rhs: MyInt) -> None:
        self.value += rhs.value

    def __iadd__(mut self, rhs: Int) -> None:
        self.value += rhs
```

### Comparison operator dunder methods

When you have a comparison expression like `a < b`, Sharpy invokes as associated
dunder method on the left-hand side value and passes the right-hand side value
as an argument. Sharpy doesn't support "reflected" versions of these dunder
methods because you should only compare values of the same type. The comparison
dunder methods must return a `Bool` result representing the result of the
comparison.

There are two traits associated with the comparison dunder methods. A type that
implements the [`Comparable`](/sharpy/stdlib/builtin/comparable/Comparable) trait
must define all of the comparison methods. However, some types don't have a
natural ordering (for example, complex numbers). For those types you can decide
to implement the
[`EqualityComparable`](/sharpy/stdlib/builtin/equality_comparable/EqualityComparable)
trait, which requires defining only the equality and inequality comparison
methods.

The supported comparison operators and their corresponding methods are shown in
the table below.

| **Operator** | **Dunder method** |
| ------------ | ----------------- |
| `==` equality | `__eq__()` |
| `!=` inequality | `__ne__()` |
| `<` less than | `__lt__()` |
| `<=` less than or equal | `__le__()` |
| `>` greater than | `__gt__()` |
| `>=` greater than or equal | `__ge__()` |

:::note

The `Comparable` and `EqualityComparable` traits don't allow the comparison
dunder methods to raise errors. Because using `def` to define a method implies
that it can raise an error, you must use `fn` to implement the comparison
methods declared by these traits. See [Functions](/sharpy/manual/functions) for
more information on the differences between defining functions with `def` and
`fn`.

:::

As an example, consider implementing support for all of the comparison operator
dunder methods for a custom `MyInt` struct.

```sharpy
@fieldwise_init
struct MyInt(
    Comparable
):
    value: Int

    fn __eq__(self, rhs: MyInt) -> Bool:
        return self.value == rhs.value

    fn __ne__(self, rhs: MyInt) -> Bool:
        return self.value != rhs.value

    fn __lt__(self, rhs: MyInt) -> Bool:
        return self.value < rhs.value

    fn __le__(self, rhs: MyInt) -> Bool:
        return self.value <= rhs.value

    fn __gt__(self, rhs: MyInt) -> Bool:
        return self.value > rhs.value

    fn __ge__(self, rhs: MyInt) -> Bool:
        return self.value >= rhs.value
```

### Membership operator dunder methods

The `in` and `not in` operators depend on a type implementing the
`__contains__()` dunder method. Typically only collection types (such as `List`,
`Dict`, and `Set`) implement this method. It should accept the right-hand side
value as an argument and return a `Bool` indicating whether the value is present
in the collection or not.

### Subscript and slicing dunder methods

Subscripting and slicing typically apply only to sequential collection types,
like `List` and `String`. Subscripting references a single element of a
collection or a dimension of a multi-dimensional container, whereas slicing
refers to a range of values. A type supports both subscripting and slicing by
implementing the `__getitem__()` method for retrieving values and the
`__setitem__()` method for setting values.

#### Subscripting

In the simple case of a one-dimensional sequence, the `__getitem__()` and
`__setitem__()` methods should have signatures similar to this:

```sharpy
struct MySeq[type: Copyable & Movable]:
    fn __getitem__(self, idx: Int) -> type:
        # Return element at the given index
        ...
    fn __setitem__(mut self, idx: Int, value: type):
        # Assign the element at the given index the provided value
```

It's also possible to support multi-dimensional collections, in which case you
can implement both `__getitem__()` and `__setitem__()` methods to accept
multiple index arguments—or even variadic index arguments for
arbitrary—dimension collections.

```sharpy
struct MySeq[type: Copyable & Movable]:
    # 2-dimension support
    fn __getitem__(self, x_idx: Int, y_idx: Int) -> type:
        ...
    # Arbitrary-dimension support
    fn __getitem__(self, *indices: Int) -> type:
        ...
```

#### Slicing

You provide slicing support for a collection type also by implementing
`__getitem__()` and `__setitem__()` methods. But for slicing, instead of
accepting an `Int` index (or indices, in the case of a multi-dimensional
collection) you implement to methods to accept a
[`Slice`](/sharpy/stdlib/builtin/builtin_slice/Slice) (or multiple `Slice`s in
the case of a multi-dimensional collection).

```sharpy
struct MySeq[type: Copyable & Movable]:
    # Return a new MySeq with a subset of elements
    fn __getitem__(self, span: Slice) -> Self:
        ...

```

A `Slice` contains three fields:

- `start` (`Optional[Int]`): The starting index of the slice
- `end` (`Optional[Int]`): The ending index of the slice
- `step` (`Optional[Int]`): The step increment value of the slice.

Because the start, end, and step values are all optional when using slice
syntax, they are represented as `Optional[Int]` values in the `Slice`. And if
present, the index values might be negative representing a relative position
from the end of the sequence. As a convenience, `Slice` provides an `indices()`
method that accepts a `length` value and returns a 3-tuple of "normalized"
start, end, and step values for the given length, all represented as
non-negative values. You can then use these normalized values to determine the
corresponding elements of your collection being referenced.

```sharpy
struct MySeq[type: Copyable & Movable]:
    size: Int

    # Return a new MySeq with a subset of elements
    fn __getitem__(self, span: Slice) -> Self:
        start: Int
        end: Int
        step: Int
        start, end, step = span.indices(self.size)
        ...

```

## An example of implementing operators for a custom type

As an example of implementing operators for a custom Sharpy type, let's create a
`Complex` struct to represent a single complex number, with both the real and
imaginary components stored as `Float64` values. We'll implement most of the
arithmetic operators, the associated in-place assignment operators, the equality
comparison operators, and a few additional convenience methods to support
operations like printing complex values. We'll also allow mixing `Complex` and
`Float64` values in arithmetic expressions to produce a `Complex` result.

This example builds our `Complex` struct incrementally. You can also find the
[complete example in the public GitHub
repo](https://github.com/modular/modular/tree/main/examples/sharpy/operators).

:::note

Note that the Sharpy standard library implements a parameterized
[`ComplexSIMD`](/sharpy/stdlib/complex/complex/ComplexSIMD) struct that provides
support for a basic set of arithmetic operators. However, our `Complex` type
will not be based on the `ComplexSIMD` struct or be compatible with it.

:::

### Implement lifecycle methods

Our `Complex` struct is an example of a simple value type consisting of trivial
numeric fields and requiring no special constructor or destructor behaviors.
This means we can use the
[`@register_passable("trivial")`](/sharpy/manual/decorators/register-passable/#register_passabletrivial)
decorator, which declares that the type can be trivially copied, moved, and
destroyed—and doesn't need a copy constructor, move constructor, or destructor.

For the time being, we'll also use the
[`@fieldwise_init`](/sharpy/manual/decorators/fieldwise-init) decorator to
automatically implement a field-wise initializer (a constructor with arguments
for each field).

```sharpy
@fieldwise_init
@register_passable("trivial")
struct Complex:
    re: Float64
    im: Float64
```

This definition is enough for us to create `Complex` instances and access their
real and imaginary fields.

```sharpy
c1 = Complex(-1.2, 6.5)
print("c1: Real: {}; Imaginary: {}".format(c1.re, c1.im))
```

```output
c1: Real: -1.2; Imaginary: 6.5
```

As a convenience, let's add an explicit constructor to handle the case of
creating a `Complex` instance with an imaginary component of 0.

```sharpy
@register_passable("trivial")
struct Complex():
    re: Float64
    im: Float64

    fn __init__(out self, re: Float64, im: Float64 = 0.0):
        self.re = re
        self.im = im
```

Since this constructor also handles creating a `Complex` instance with both real
and imaginary components, we don't need the `@fieldwise_init` decorator anymore.

Now we can create a `Complex` instance and provide just a real component.

```sharpy
c2 = Complex(3.14159)
print("c2: Real: {}; Imaginary: {}".format(c2.re, c2.im))
```

```output
c2: Real: 3.1415899999999999; Imaginary: 0.0
```

### Implement the `Writable` and `Stringable` traits

To make it simpler to print `Complex` values, let's implement the
[Writable](/sharpy/stdlib/utils/write/Writable) trait. While we're at it, let's
also implement the [`Stringable`](/sharpy/stdlib/builtin/str/Stringable) trait so
that we can use the `String()` constructor to generate a `String` representation of a
`Complex` value. You can find out more about these traits and their associated
methods in [The `Stringable`, `Representable`, and `Writable`
traits](/sharpy/manual/traits#the-stringable-representable-and-writable-traits).

```sharpy
@register_passable("trivial")
struct Complex(
    Writable,
    Stringable,
):
    # ...

    fn __str__(self) -> String:
        return String.write(self)

    fn write_to[W: Writer](self, mut writer: W):
        writer.write("(", self.re)
        if self.im < 0:
            writer.write(" - ", -self.im)
        else:
            writer.write(" + ", self.im)
        writer.write("i)")
```

:::note

The `Writable` trait doesn't allow the `write_to()` method to raise an error and
the `Stringable` trait doesn't allow the `__str__()` method to raise an error.
Because defining a method with `def` implies that it can raise an error, we
instead have to define these methods with `fn`. See
[Functions](/sharpy/manual/functions) for more information on the differences
between defining functions with `def` and `fn`.

:::

Now we can print a `Complex` value directly, and we can explicitly generate a
`String` representation by passing a `Complex` value to `String()` which
constructs a new `String` from all the arguments passed to it.

```sharpy
c3 = Complex(3.14159, -2.71828)
print("c3 =", c3)

msg = String("The value is: ", c3)
print(msg)
```

```output
c3 = (3.1415899999999999 - 2.71828i)
The value is: (3.1415899999999999 - 2.71828i)
```

### Implement basic indexing

Indexing usually is supported only by collection types. But as an example, let's
implement support for accessing the real component as index 0 and the imaginary
component as index 1. We'll not implement slicing or variadic assignment for
this example.

```sharpy
    # ...
    def __getitem__(self, idx: Int) -> Float64:
        if idx == 0:
            return self.re
        elif idx == 1:
            return self.im
        else:
            raise "index out of bounds"

    def __setitem__(mut self, idx: Int, value: Float64) -> None:
        if idx == 0:
            self.re = value
        elif idx == 1:
            self.im = value
        else:
            raise "index out of bounds"
```

Now let's try getting and setting the real and imaginary components of a
`Complex` value using indexing.

```sharpy
c2 = Complex(3.14159)
print("c2[0]: {}; c2[1]: {}".format(c2[0], c2[1]))
c2[0] = 2.71828
c2[1] = 42
print("c2[0] = 2.71828; c2[1] = 42; c2:", c2)
```

```output
c2[0]: 3.1415899999999999; c2[1]: 0.0
c2[0] = 2.71828; c2[1] = 42; c2: (2.71828 + 42.0i)
```

### Implement arithmetic operators

Now let's implement the dunder methods that allow us to perform arithmetic
operations on `Complex` values. (Refer to the [Wikipedia
page](https://en.wikipedia.org/wiki/Complex_number) on complex numbers for a
more in-depth explanation of the formulas for these operators.)

#### Implement basic operators for `Complex` values

The unary `+` operator simply returns the original value, whereas the unary `-`
operator returns a new `Complex` value with the real and imaginary components
negated.

```sharpy
    # ...
    def __pos__(self) -> Self:
        return self

    def __neg__(self) -> Self:
        return Self(-self.re, -self.im)
```

Let's test these out by printing the result of applying each operator.

```sharpy
c1 = Complex(-1.2, 6.5)
print("+c1:", +c1)
print("-c1:", -c1)
```

```output
+c1: (-1.2 + 6.5i)
-c1: (1.2 - 6.5i)
```

Next we'll implement the basic binary operators: `+`, `-`, `*`, and `/`.
Dividing complex numbers is a bit tricky, so we'll also define a helper method
called `norm()` to calculate the [Euclidean
norm](https://en.wikipedia.org/wiki/Norm_(mathematics)#Euclidean_norm_of_complex_numbers)
of a `Complex` instance, which can also be useful for other types of analysis
with complex numbers.

For all of these dunder methods, the left-hand side operand is `self` and the
right-hand side operand is passed as an argument. We return a new `Complex`
value representing the result.

```sharpy
from math import sqrt

# ...

    def __add__(self, rhs: Self) -> Self:
        return Self(self.re + rhs.re, self.im + rhs.im)

    def __sub__(self, rhs: Self) -> Self:
        return Self(self.re - rhs.re, self.im - rhs.im)

    def __mul__(self, rhs: Self) -> Self:
        return Self(
            self.re * rhs.re - self.im * rhs.im,
            self.re * rhs.im + self.im * rhs.re
        )

    def __truediv__(self, rhs: Self) -> Self:
        denom = rhs.squared_norm()
        return Self(
            (self.re * rhs.re + self.im * rhs.im) / denom,
            (self.im * rhs.re - self.re * rhs.im) / denom
        )

    def squared_norm(self) -> Float64:
        return self.re * self.re + self.im * self.im

    def norm(self) -> Float64:
        return sqrt(self.squared_norm())
```

Now we can try them out.

```sharpy
c1 = Complex(-1.2, 6.5)
c3 = Complex(3.14159, -2.71828)
print("c1 + c3 =", c1 + c3)
print("c1 - c3 =", c1 - c3)
print("c1 * c3 =", c1 * c3)
print("c1 / c3 =", c1 / c3)
```

```output
c1 + c3 = (1.9415899999999999 + 3.78172i)
c1 - c3 = (-4.3415900000000001 + 9.21828i)
c1 * c3 = (13.898912000000001 + 23.682270999999997i)
c1 / c3 = (-1.2422030701265261 + 0.99419218883955773i)
```

#### Implement overloaded arithmetic operators for `Float64` values

Our initial set of binary arithmetic operators work fine if both operands are
`Complex` instances. But if we have a `Float64` value representing just a real
value, we'd first need to use it to create a `Complex` value before we could
add, subtract, multiply, or divide it with another `Complex` value. If we think
that this will be a common use case, it makes sense to overload our arithmetic
methods to accept a `Float64` as the second operand.

For the case where we have `complex1 + float1`, we can just create an overloaded
definition of `__add__()`. But what about the case of `float1 + complex1`? By
default, when Sharpy encounters a `+` operator it tries to invoke the `__add__()`
method of the left-hand operand, but the built-in `Float64` type doesn't
implement support for addition with a `Complex` value. This is an example where
we need to implement the `__radd__()` method on the `Complex` type. When Sharpy
can't find an `__add__(self, rhs: Complex) -> Complex` method defined on
`Float64`, it uses the `__radd__(self, lhs: Float64) -> Complex` method defined
on `Complex`.

So we can support arithmetic operations on `Complex` and `Float64` values by
implementing the following eight methods.

```sharpy
    # ...
    def __add__(self, rhs: Float64) -> Self:
        return Self(self.re + rhs, self.im)

    def __radd__(self, lhs: Float64) -> Self:
        return Self(self.re + lhs, self.im)

    def __sub__(self, rhs: Float64) -> Self:
        return Self(self.re - rhs, self.im)

    def __rsub__(self, lhs: Float64) -> Self:
        return Self(lhs - self.re, -self.im)

    def __mul__(self, rhs: Float64) -> Self:
        return Self(self.re * rhs, self.im * rhs)

    def __rmul__(self, lhs: Float64) -> Self:
        return Self(lhs * self.re, lhs * self.im)

    def __truediv__(self, rhs: Float64) -> Self:
        return Self(self.re / rhs, self.im / rhs)

    def __rtruediv__(self, lhs: Float64) -> Self:
        denom = self.squared_norm()
        return Self(
            (lhs * self.re) / denom,
            (-lhs * self.im) / denom
        )
```

Let's see them in action.

```sharpy
c1 = Complex(-1.2, 6.5)
f1 = 2.5
print("c1 + f1 =", c1 + f1)
print("f1 + c1 =", f1 + c1)
print("c1 - f1 =", c1 - f1)
print("f1 - c1 =", f1 - c1)
print("c1 * f1 =", c1 * f1)
print("f1 * c1 =", f1 * c1)
print("c1 / f1 =", c1 / f1)
print("f1 / c1 =", f1 / c1)
```

```output
c1 + f1 = (1.3 + 6.5i)
f1 + c1 = (1.3 + 6.5i)
c1 - f1 = (-3.7000000000000002 + 6.5i)
f1 - c1 = (3.7000000000000002 - 6.5i)
c1 * f1 = (-3.0 + 16.25i)
f1 * c1 = (-3.0 + 16.25i)
c1 / f1 = (-0.47999999999999998 + 2.6000000000000001i)
f1 / c1 = (-0.068665598535133904 - 0.37193865873197529i)
```

#### Implement in-place assignment operators

Now let's implement support for the in-place assignment operators: `+=`, `-=`,
`*=`, and `/=`. These modify the original value, so we need to mark `self` as
being an `mut` argument and update the `re` and `im` fields instead of
returning a new `Complex` instance. And once again, we'll overload the
definitions to support both a `Complex` and a `Float64` operand.

```sharpy
    # ...
    def __iadd__(mut self, rhs: Self) -> None:
        self.re += rhs.re
        self.im += rhs.im

    def __iadd__(mut self, rhs: Float64) -> None:
        self.re += rhs

    def __isub__(mut self, rhs: Self) -> None:
        self.re -= rhs.re
        self.im -= rhs.im

    def __isub__(mut self, rhs: Float64) -> None:
        self.re -= rhs

    def __imul__(mut self, rhs: Self) -> None:
        new_re = self.re * rhs.re - self.im * rhs.im
        new_im = self.re * rhs.im + self.im * rhs.re
        self.re = new_re
        self.im = new_im

    def __imul__(mut self, rhs: Float64) -> None:
        self.re *= rhs
        self.im *= rhs

    def __itruediv__(mut self, rhs: Self) -> None:
        denom = rhs.squared_norm()
        new_re = (self.re * rhs.re + self.im * rhs.im) / denom
        new_im = (self.im * rhs.re - self.re * rhs.im) / denom
        self.re = new_re
        self.im = new_im

    def __itruediv__(mut self, rhs: Float64) -> None:
        self.re /= rhs
        self.im /= rhs
```

And now to try them out.

```sharpy
c4 = Complex(-1, -1)
print("c4 =", c4)
c4 += Complex(0.5, -0.5)
print("c4 += Complex(0.5, -0.5) =>", c4)
c4 += 2.75
print("c4 += 2.75 =>", c4)
c4 -= Complex(0.25, 1.5)
print("c4 -= Complex(0.25, 1.5) =>", c4)
c4 -= 3
print("c4 -= 3 =>", c4)
c4 *= Complex(-3.0, 2.0)
print("c4 *= Complex(-3.0, 2.0) =>", c4)
c4 *= 0.75
print("c4 *= 0.75 =>", c4)
c4 /= Complex(1.25, 2.0)
print("c4 /= Complex(1.25, 2.0) =>", c4)
c4 /= 2.0
print("c4 /= 2.0 =>", c4)
```

```output
c4 = (-1.0 - 1.0i)
c4 += Complex(0.5, -0.5) => (-0.5 - 1.5i)
c4 += 2.75 => (2.25 - 1.5i)
c4 -= Complex(0.25, 1.5) => (2.0 - 3.0i)
c4 -= 3 => (-1.0 - 3.0i)
c4 *= Complex(-3.0, 2.0) => (9.0 + 7.0i)
c4 *= 0.75 => (6.75 + 5.25i)
c4 /= Complex(1.25, 2.0) => (3.404494382022472 - 1.247191011235955i)
c4 /= 2.0 => (1.702247191011236 - 0.6235955056179775i)
```

### Implement equality operators

The field of complex numbers is not an ordered field, so it doesn't make sense
for us to implement the `Comparable` trait and the `>`, `>=`, `<`, and `<=`
operators. However, we can implement the `EqualityComparable` trait and the `==`
and `!=` operators. (Of course, this suffers the same limitation of comparing
floating point numbers for equality because of the limited precision of
representing floating point numbers when performing arithmetic operations. But
we'll go ahead and implement the operators for completeness.)

```sharpy
@value
struct Complex(
    EqualityComparable,
    Formattable,
    Stringable,
):
    # ...
    fn __eq__(self, other: Self) -> Bool:
        return self.re == other.re and self.im == other.im

    fn __ne__(self, other: Self) -> Bool:
        return self.re != other.re or self.im != other.im
```

:::note

The `EqualityComparable` trait doesn't allow the `__eq__()` and `__ne__()`
methods to raise errors. Because defining a method with `def` implies that it
can raise an error, we instead have to define these methods with `fn`. See
[Functions](/sharpy/manual/functions) for more information on the differences
between defining functions with `def` and `fn`.

:::

And now to try them out.

```sharpy
c1 = Complex(-1.2, 6.5)
c3 = Complex(3.14159, -2.71828)
c5 = Complex(-1.2, 6.5)

if c1 == c5:
    print("c1 is equal to c5")
else:
    print("c1 is not equal to c5")

if c1 != c3:
    print("c1 is not equal to c3")
else:
    print("c1 is equal to c3")
```

```output
c1 is equal to c5
c1 is not equal to c3
```

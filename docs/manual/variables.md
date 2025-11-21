A variable is a name that holds a value or object. All variables in Sharpy are
mutable—their value can be changed. (If you want to define a constant value that
can't change at runtime, see the
[`alias` keyword](/sharpy/manual/parameters/#alias-named-parameter-expressions).)

Sharpy has two kinds of variables:

* Explicitly-declared variables are created with the `var` keyword.

  ```sharpy {doctest="foo"}
  a = 5
  b: Float64 = 3.14
  c: str
  ```

* Implicitly-declared variables are created the first time the variable is used,
  either with an assignment statement, or with a type annotation:

  ```sharpy
  a = 5
  b: Float64 = 3.14
  c: str
  ```

Both types of variables are strongly typed—the type is either set explicitly
with a [type annotation](#type-annotations) or implicitly when the variable is
first initialized with a value.

Either way, the variable receives a type when it's created, and the type never
changes. So you can't assign a variable a value of a different type:

```sharpy
count = 8 # count is type Int
count = "Nine?" # Error: can't implicitly convert 'strLiteral' to 'Int'
```

Some types support [*implicit conversions*](#implicit-type-conversion) from
other types. For example, an integer value can implicitly convert to a
floating-point value:

```sharpy
temperature: Float64 = 99
print(temperature)
```

```output
99.0
```

In this example, the `temperature` variable is explicitly typed as `Float64`,
but assigned an integer value, so the  value is implicitly converted to a
`Float64`.

## Implicitly-declared variables

You can create a variable with just a name and a value. For example:

```sharpy
name = "Sam"
user_id = 0
```

Implicitly-declared variables are strongly typed: they take the type from the
first value assigned to them. For example, the `user_id` variable above is type
`Int`, while the `name` variable is type `str`. You can't assign a string to
`user_id` or an integer to `name`.

You can also use a type annotation with an implicitly-declared variable, either
as part of an assignment statement, or on its own:

```sharpy
name: str = "Sam"
user_id: Int
```

Here the `user_id` variable has a type, but is uninitialized.

Implicitly-declared variables are scoped at the function level. You create an
implicitly-declared variable the first time you assign a value to a given name
inside a function. Any subsequent references to that name inside the function
refer to the same variable. For more information, see [Variable
scopes](#variable-scopes), which describes how variable scoping differs between
explicitly- and implicitly-declared variables.

## Explicitly-declared variables

You can declare a variable with the `var` keyword. For example:

```sharpy
name = "Sam"
user_id: Int
```

The `name` variable is initialized to the string "Sam". The `user_id` variable
is uninitialized, but it has a declared type, `Int` for an integer value.

Since variables are strongly typed, you can't assign a variable a
value of a different type, unless those types can be
[implicitly converted](#implicit-type-conversion). For example, this code will
not compile:

```sharpy
user_id: Int = "Sam"
```

Explicitly-declared variables follow [lexical scoping](#variable-scopes), unlike
implicitly-declared variables.

## Type annotations

Although Sharpy can infer a variable type from the first value assigned to a
variable, it also supports static type annotations on variables. Type
annotations provide a more explicit way of specifying the variable's type.

To specify the type for a variable, add a colon followed by the type name:

```sharpy
name: str = get_name()
# Or
name: str = get_name()
```

This makes it clear that `name` is type `str`, without knowing what the
`get_name()` function returns. The `get_name()` function may return a `str`,
or a value that's implicitly convertible to a `str`.

If a type has a constructor with just one argument, you can initialize it in
two ways:

```sharpy
name1: str = "Sam"
name2 = str("Sam")
name3 = "Sam"
```

All of these lines invoke the same constructor to create a `str` from a
`strLiteral`.

### Late initialization

Using type annotations allows for late initialization. For example, notice here
that the `z` variable is first declared with just a type, and the value is
assigned later:

```sharpy
fn my_function(x: Int):
    z: Float32
    if x != 0:
        z = 1.0
    else:
        z = foo()
    print(z)

fn foo() -> Float32:
    return 3.14
```

If you try to pass an uninitialized variable to a function or use
it on the right-hand side of an assignment statement, compilation fails.

```sharpy
z: Float32
y = z # Error: use of uninitialized value 'z'
```

:::note

Late initialization works only if the variable is declared with a
type.

:::

### Implicit type conversion

Some types include built-in type conversion (type casting) from one type into
its own type. For example, if you assign an integer to a variable that has a
floating-point type, it converts the value instead of giving a compiler error:

```sharpy
number: Float64 = Int(1)
print(number)
```

```output
1.0
```

As shown above, value assignment can be converted into a constructor call if the
target type has a constructor that meets the following criteria:

- It's decorated with the `@implicit` decorator.

- It takes a single required argument that matches the value being assigned.

So, this code uses the `Float64` constructor that takes an
integer: `__init__(out self, value: Int)`.

In general, implicit conversions should only be supported where the conversion
is lossless.

Implicit conversion follows the logic of [overloaded
functions](/sharpy/manual/functions#overloaded-functions). If the destination
type has a viable implicit conversion constructor for the source
type, it can be invoked for implicit conversion.

So assigning an integer to a `Float64` variable is exactly the same as this:

```sharpy
number = Float64(1)
```

Similarly, if you call a function that requires an argument of a certain type
(such as `Float64`), you can pass in any value as long as that value type can
implicitly convert to the required type (using one of the type's overloaded
constructors).

For example, you can pass an `Int` to a function that expects a `Float64`,
because `Float64` includes an implicit conversion constructor that takes an
`Int`:

```sharpy
fn take_float(value: Float64):
    print(value)

fn pass_integer():
    value: Int = 1
    take_float(value)
```

For more details on implicit conversion, see
[Constructors and implicit
conversion](/sharpy/manual/lifecycle/life/#constructors-and-implicit-conversion).

## Variable scopes

Sharpy uses **C#-style block scoping** for variables. This means:

1. Variables declared with type annotations (`x: int = 5`) are scoped to the block in which they are declared
2. Bare assignments (`x = 5`) to undefined variables create new variables in the current scope
3. Bare assignments to existing variables modify the variable in the nearest enclosing scope
4. Variables defined in control flow blocks (if, while, for) are not accessible outside those blocks

### Block Scoping Example

Control flow blocks create new scopes. Variables declared inside these blocks are not accessible outside:

```sharpy
def block_scopes():
    x: int = 1
    
    if x > 0:
        # Declare a new variable inside the if block
        category: str = "positive"
        print(category)  # OK: category is in scope
    
    # ERROR: category is not accessible here
    # print(category)
```

For loops have their own scope, and loop variables are confined to the loop:

```sharpy
def loop_scopes():
    for i in range(3):
        temp: int = i * 2
        print(f"i={i}, temp={temp}")
    
    # ERROR: i and temp are not accessible here
    # print(i)
    # print(temp)
    
    # Loop variables can be reused in consecutive loops
    for i in range(5, 8):
        print(f"second loop: {i}")
```

### Assignment vs. Declaration

Bare assignments (without type annotations) behave differently from declarations:

```sharpy
def assignment_vs_declaration():
    x: int = 1  # Declaration: creates new variable in current scope
    
    if True:
        x = 2      # Bare assignment: modifies the outer 'x'
        y: int = 3 # Declaration: creates new variable in if-block scope
    
    print(x)  # Prints 2 (modified by inner scope)
    # print(y)  # ERROR: y is not accessible here
```

### Shadowing Limitations

Unlike some languages, C# (and therefore Sharpy) does **not** allow shadowing variables within nested blocks in the same function:

```sharpy
def no_nested_shadowing():
    x: int = 1
    
    if True:
        # ERROR in C#: Cannot declare 'x' in nested scope
        # x: int = 2
        
        # Use bare assignment to modify outer variable instead
        x = 2
```

However, shadowing **is** allowed across different functions:

```sharpy
x: int = 1  # Global variable

def shadow_global():
    x: int = 2  # OK: Shadows global x within function
    print(x)    # Prints 2

shadow_global()
print(x)  # Prints 1 (global x unchanged)
```

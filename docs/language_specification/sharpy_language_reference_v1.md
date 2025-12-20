# Sharpy Language Reference

See [introduction.md](introduction.md) for goals, principles, and philosophy.

See [version_guide.md](version_guide.md) for version features and target compatibility.

## Lexical Structure **[v0.1.0]**

See [source_files.md](source_files.md) for file format, line structure, and continuation rules.

See [identifiers.md](identifiers.md) for identifier syntax, naming conventions, and backtick escaping.

See [keywords.md](keywords.md) for reserved keywords.

See [indentation.md](indentation.md) for indentation rules.

See [comments.md](comments.md) for comment syntax.

---

## Literals **[v0.1.0]**

See [integer_literals.md](integer_literals.md) for integer literals and suffixes.

See [float_literals.md](float_literals.md) for float literals and suffixes.

See [extended_numeric_literals.md](extended_numeric_literals.md) for binary, hex, octal, and scientific notation.

See [string_literals.md](string_literals.md) for string syntax, escape sequences, and raw strings.

See [fstrings.md](fstrings.md) for formatted string literals (f-strings).

See [boolean_literals.md](boolean_literals.md) for `True` and `False`.

See [none_literal.md](none_literal.md) for `None` literal and its semantics.

See [ellipsis_literal.md](ellipsis_literal.md) for the `...` placeholder.

See [empty_set_literal.md](empty_set_literal.md) for the `{/}` empty set literal.

---

## Types **[v0.1.0]**

See [primitive_types.md](primitive_types.md) for built-in primitive types and arrays.

See [string_type.md](string_type.md) for string type and UTF-16 semantics.

See [type_annotations.md](type_annotations.md) for type annotation syntax.

See [type_hierarchy.md](type_hierarchy.md) for the type hierarchy and object model.

See [nullable_types.md](nullable_types.md) for nullable type semantics.

---

## Function Types **[v0.1.3]**

See [function_types.md](function_types.md) for function type syntax, compatibility, and usage.

---

## Operators **[v0.1.1]**

See [null_coalescing_operator.md](null_coalescing_operator.md) for the `??` operator.

See [null_conditional_access.md](null_conditional_access.md) for the `?.` operator.

See [type_narrowing.md](type_narrowing.md) for type narrowing rules with `is not None` and `isinstance()`.

---

## Collection Types **[v0.1.1]**

See [collection_types.md](collection_types.md) for collection types, methods, and .NET interop.

### Collection Literals **[v0.1.1]**

See [del_statement.md](del_statement.md) for the `del` statement **[v0.2.0]**.

---

## Operators **[v0.1.0]**

See [arithmetic_operators.md](arithmetic_operators.md) for arithmetic operators and numeric type promotion.

See [comparison_operators.md](comparison_operators.md) for comparison operators.

See [comparison_chaining.md](comparison_chaining.md) for chained comparisons.

See [logical_operators.md](logical_operators.md) for `and`, `or`, `not`.

See [bitwise_operators.md](bitwise_operators.md) for bitwise operations.

See [string_operators.md](string_operators.md) for string concatenation and repetition.

See [membership_operators.md](membership_operators.md) for `in` and `not in`.

See [identity_operators.md](identity_operators.md) for `is` and `is not`.

See [assignment_operators.md](assignment_operators.md) for assignment operators.

See [operator_precedence.md](operator_precedence.md) for operator precedence table.

---

## Expressions **[v0.1.0]**

### Primary Expressions

```python
# Literals
42                  # Integer
3.14                # Float
"hello"             # String
True                # Boolean
None                # None

# Identifiers
x
my_variable

# Parenthesized
(x + y)
(2 + 3) * 4
```

### Member Access

```python
# Standard access
obj.field
obj.method()

# Null-conditional [v0.1.1]
obj?.field
obj?.method()
```

### Index Access

```python
arr[0]              # First element
arr[-1]             # Last element
arr[i]              # Element at index i
matrix[i, j]        # Multi-dimensional
```

### Function Calls

```python
print("Hello")
calculate_total(100, 0.08)
obj.method(arg1, arg2)

# Generic instantiation [v0.1.3]
container = ListContainer[str]()
```

### Type Casting (The `to` Operator)

The `to` operator performs type casting, converting a value from one type to another at runtime.

```python
result = expression to TargetType
```

**Two Forms:**

| Syntax | Behavior on Failure | Result Type |
|--------|---------------------|-------------|
| `value to T` | Throws `InvalidCastException` | `T` |
| `value to T?` | Returns `None` | `T?` |

**Examples:**

```python
# Reference type downcasting
animal: Animal = get_animal()
dog = animal to Dog              # Throws if not a Dog
dog = animal to Dog?             # None if not a Dog

# Interface casting
obj: object = get_object()
drawable = obj to IDrawable      # Throws if doesn't implement IDrawable
drawable = obj to IDrawable?     # None if doesn't implement IDrawable

# Unboxing
boxed: object = 42
value = boxed to int             # Throws if not an int
value = boxed to int?            # None if not an int

# Numeric conversions
big: long = 1_000_000
small = big to int               # Throws on overflow
small = big to int?              # None on overflow

precise: double = 3.14159
rounded = precise to int         # Truncates toward zero (3), throws if out of range
rounded = precise to int?        # None if out of range
```

**Safe Casting Pattern:**

The nullable form integrates naturally with type narrowing:

```python
animal: Animal = get_animal()

if (dog := animal to Dog?) is not None:
    # dog is narrowed to Dog in this block
    print(dog.bark())

# Or with simple None check
result = animal to Dog?
if result is not None:
    use_dog(result)
```

**Upcasting:**

Upcasts (derived → base) are always safe and can be implicit through assignment:

```python
dog: Dog = Dog("Buddy")

# Explicit upcast (allowed but unnecessary)
animal = dog to Animal

# Implicit upcast (preferred)
animal: Animal = dog
```

The compiler may emit a warning when `to` is used for compile-time-safe upcasts, since they're implicit anyway.

**Numeric Conversions:**

The `to` operator handles numeric type conversions including narrowing conversions:

| Conversion | Behavior |
|------------|----------|
| Widening (e.g., `int` → `long`) | Always succeeds |
| Narrowing (e.g., `long` → `int`) | Throws/None on overflow |
| Float → Integer | Truncates toward zero, throws/None if out of range |
| Integer → Float | May lose precision (no failure) |

```python
# Widening - always safe
x: int = 42
y = x to long                    # Always succeeds

# Narrowing - may fail
big: long = 10_000_000_000
small = big to int               # Throws: value too large for int
small = big to int?              # None: value too large for int

# Float to integer truncation
pi: double = 3.99
n = pi to int                    # 3 (truncates toward zero)
neg: double = -3.99
m = neg to int                   # -3 (truncates toward zero)

# Out of range
huge: double = 1e100
n = huge to int?                 # None: out of int range
```

**Relationship to Conversion Functions:**

The built-in conversion functions (`int()`, `str()`, `float()`, etc.) remain available and are equivalent to the throwing form of `to` for their respective types:

```python
# These are equivalent
x = int(value)
x = value to int

# These are equivalent
s = str(value)
s = value to str

# But only `to` provides the safe nullable form
x = value to int?                # No equivalent with int()
```

The conversion functions are retained for Pythonic familiarity, but `to` is the general-purpose casting mechanism that works with any type:

```python
# Only `to` works for arbitrary types
dog = animal to Dog?
point = data to Point
processor = obj to IProcessor?
```

**Operator Precedence:**

The `to` operator binds looser than member access and function calls, but tighter than comparison and logical operators:

| Precedence | Operators |
|------------|-----------|
| (higher) | `()`, `[]`, `.`, `?.` |
| | `to` |
| | `**` |
| | `+x`, `-x`, `~x` |
| | ... |
| (lower) | `in`, `is`, `<`, `>`, `==`, etc. |

This means:

```python
# Parentheses needed for member access on cast result
name = (animal to Dog).name
result = (obj to IProcessor).process(data)

# No parentheses needed for comparisons
if animal to Dog? is not None:
    pass

# Chained with None check
if (dog := animal to Dog?) is not None and dog.age > 5:
    pass
```

**Invalid Casts:**

The compiler rejects casts that are statically known to be impossible:

```python
x: int = 42
s = x to str                     # ERROR: int cannot be cast to str (use str(x))

dog: Dog = Dog("Buddy")
cat = dog to Cat                 # ERROR: Dog cannot be cast to Cat (no inheritance relationship)
```

**Casting `None`:**

Casting `None` always fails:

```python
x: Dog? = None
dog = x to Dog                   # Throws InvalidCastException
dog = x to Dog?                  # None
```

*Implementation: 🔄 Lowered*
- *`value to T` → `(T)value` (C# cast expression)*
- *`value to T?` → `value as T` for reference types, try-pattern for value types*

```csharp
// value to Dog (throwing)
(Dog)value

// value to Dog? (safe, reference type)
value as Dog

// value to int? (safe, value type - requires pattern)
value is int _temp ? (int?)_temp : null
```

---

### Conditional Expression (Ternary)

```python
result = x if x > 0 else -x         # Absolute value
status = "even" if n % 2 == 0 else "odd"
```

*Implementation: ✅ Native - Maps to `condition ? trueVal : falseVal`.*

### Lambda Expressions **[v0.1.3]**

```python
# Single expression lambda
square = lambda x: x ** 2
add = lambda x, y: x + y

# As function argument
result = apply(10, lambda x: x ** 2)
```

**Lambda Rules:**
- Single expression only (no statements)
- Parameter types inferred from context
- Expression result is automatically returned

**Lambda Expression Scope:**

Lambdas can contain any expression, including:
- Conditional expressions: `lambda x: x if x > 0 else -x`
- Walrus operator: `lambda x: (y := x * 2, y + 1)[-1]`
- Function calls, arithmetic, member access, etc.

What lambdas cannot contain (these are statements, not expressions):
- Assignments without walrus (`x = 5`)
- Control flow blocks (`if`/`for`/`while` blocks)
- Multiple statements

```python
# Valid lambda expressions
absolute = lambda x: x if x >= 0 else -x
complex_calc = lambda a, b: (temp := a * b) + temp ** 2
method_call = lambda obj: obj.process().result

# Invalid - these require statements, not expressions
# lambda x: x = 5          # ERROR: assignment is a statement
# lambda x: if x > 0: x    # ERROR: if block is a statement
```

**Closure Semantics:**

Lambdas can capture variables from enclosing scopes. Following C# semantics, variables are captured **by reference**, not by value:

```python
# Captured variables are by reference
counter = 0
increment = lambda: counter + 1
counter = 10
print(increment())  # 11, not 1

# Classic loop capture gotcha (same as C#)
funcs: list[() -> int] = []
for i in range(3):
    funcs.append(lambda: i)  # All capture the same 'i'

# After loop, i is 2 (last value)
print([f() for f in funcs])  # [2, 2, 2], not [0, 1, 2]

# To capture current value, use default parameter
funcs_fixed: list[() -> int] = []
for i in range(3):
    funcs_fixed.append(lambda captured=i: captured)  # Each captures different value
print([f() for f in funcs_fixed])  # [0, 1, 2]
```

*Implementation: ✅ Native - Maps to `(x, y) => expr`.*

### Expression Evaluation Order

Expressions are evaluated left-to-right:

```python
# Left-to-right evaluation
result = f1() + f2() * f3()
# Order: f1(), f2(), f3(), then operators by precedence

# Short-circuit evaluation
result = cheap() and expensive()
# If cheap() is False, expensive() is never called

# Argument evaluation
func(first(), second(), third())
# Order: first(), second(), third(), then func() called
```

**Rules:**
1. Expressions evaluated left-to-right
2. Operator precedence determines grouping, not evaluation order
3. Short-circuit operators (`and`, `or`, `??`, `?.`) stop early when possible
4. Function arguments evaluated left-to-right before call

---

## Statements **[v0.1.0]**

### Expression Statement

Any expression can be a statement:

```python
print("Hello")
obj.method()
list.append(item)
```

### Variable Declaration and Assignment

Variables in Sharpy must be declared and assigned in a single statement. There are three syntactic forms:

| Form | Syntax | Type Determination |
|------|--------|-------------------|
| Explicit type | `x: int = 5` | Type annotation specifies type |
| Inferred type | `x = 5` | Type inferred from initializer |
| Explicit inference | `x: auto = 5` | Type inferred from initializer (explicit) |

**Form 1: Explicit Type Annotation**

The type is explicitly specified:

```python
count: int = 0
name: str = "Alice"
items: list[int] = [1, 2, 3]
user: User? = None
```

**Form 2: Type Inference (Implicit)**

The type is inferred from the initializer expression:

```python
count = 0              # Inferred as int
name = "Alice"         # Inferred as str
items = [1, 2, 3]      # Inferred as list[int]
pi = 3.14159           # Inferred as double
```

**Form 3: Type Inference (Explicit with `auto`)**

The `auto` keyword explicitly requests type inference. This is functionally equivalent to Form 2 but makes the inference explicit:

```python
count: auto = 0        # Inferred as int
name: auto = "Alice"   # Inferred as str
items: auto = [1, 2, 3]  # Inferred as list[int]
```

**When to Use `auto`:**

The `auto` keyword is primarily useful for variable shadowing (v0.1.7), where you want to redeclare a variable with a different type:

```python
x: int = 5
x = 10                 # Assignment to existing int variable
x: str = "hello"       # Shadowing: new variable of type str
x: auto = [1, 2, 3]    # Shadowing: new variable, type inferred as list[int]
```

### No Declaration Without Assignment

Unlike some languages, Sharpy does not allow variable declarations without initialization:

```python
# ❌ Invalid - no declaration without assignment
x: int                 # ERROR: variable declaration requires initializer
name: str              # ERROR: variable declaration requires initializer

# ✅ Valid - always provide initializer
x: int = 0
name: str = ""
items: list[int] = []
user: User? = None
```

**Exception: Class Instance Fields**

Class and struct fields can be declared without initialization if they are assigned in `__init__`:

```python
class Person:
    # Field declarations (no initializer required)
    name: str
    age: int

    # Optional: fields with default values
    active: bool = True

    def __init__(self, name: str, age: int):
        # All fields without defaults must be assigned in __init__
        self.name = name
        self.age = age
```

### No `let` or `var` Keywords

Sharpy does not use `let`, `var`, or similar keywords for variable declaration. The three forms above are the only ways to declare variables:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
let x = 5              # ERROR: unexpected 'let'
var y = 10             # ERROR: unexpected 'var'
val z = 15             # ERROR: unexpected 'val'

# ✅ Valid
x = 5                  # Type inferred
y: int = 10            # Type explicit
z: auto = 15           # Type inferred (explicit)
```

### Constants

Constants are declared with `const` and must have a compile-time constant initializer:

```python
# Module-level constants
const PI: double = 3.14159
const MAX_SIZE: int = 1000
const APP_NAME = "MyApp"       # Type inferred as str
const DEBUG: bool = True
```

**Class-Level Constants:**

Constants can also be declared within classes. Class-level constants are implicitly `@static` (matching C# semantics where class constants are always static):

```python
class Math:
    const PI: double = 3.14159265358979
    const E: double = 2.71828182845904
    const TAU: double = 6.28318530717958

    @static
    def circle_area(radius: double) -> double:
        return Math.PI * radius ** 2

class HttpStatus:
    const OK: int = 200
    const NOT_FOUND: int = 404
    const INTERNAL_ERROR: int = 500

# Access via class name (constants are implicitly static)
print(Math.PI)           # 3.14159265358979
print(HttpStatus.OK)     # 200

# Cannot access via instance (they're static, not per-instance)
m = Math()
print(m.PI)              # Works but discouraged; prefer Math.PI
```

**Note:** There is no such thing as a per-instance constant. Use a read-only property (`property get`) with a backing field or `@final` field (if added in a future version) for per-instance immutability.

Constants cannot be reassigned:

```python
const X: int = 5
X = 10                 # ERROR: cannot assign to constant
```

*Implementation: ✅ Native - Direct mapping to C# variable declarations and `const`.*

## Variable Scoping Rules [v0.1.0]

**No `global` or `nonlocal` Keywords:**

Sharpy does not support Python's `global` or `nonlocal` keywords. This aligns with C# scoping semantics:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
global x       # ERROR: unexpected 'global'
nonlocal y     # ERROR: unexpected 'nonlocal'
```

To modify outer scope variables, use explicit assignment to a mutable container or return values from functions.

**Block-Scoped Constructs** (variable doesn't leak):
- For loop variables
- Comprehension variables
- Exception binding (`except E as e`)

**Containing-Scope Constructs** (variable persists):
- Regular declarations (`x = value`, `x: type = value`)
- Walrus operator (`x := value`)

### Example:

```python
x = "outer"

for x in range(5):      # New 'x' shadows outer, block-scoped
    print(x)            # Prints 0, 1, 2, 3, 4

print(x)                # Prints "outer", 'x' was shadowed only
                        # in the for-loop, and not modified.
```

### To modify outer variable:

```python
x = 0
for i in range(5):      # 'i' is block-scoped
    x += i              # Modifies outer 'x'
print(x)                # 10
print(i)                # ERROR: 'i' is block-scoped
```

### Assignment Statement

```python
# Simple assignment
x = 10

# Multiple assignment (unpacking)
x, y = 10, 20

# Augmented assignment
x += 5
count *= 2
```

### Variable Shadowing **[v0.1.7]**

Variables can be redeclared in the same scope with a different type using explicit type annotation:

```python
x: int = 5              # Initial declaration
x = 10                  # Assignment (same type)
x: str = "hello"        # Shadowing (new type, requires annotation)

# With auto keyword for type inference
x: int = 5
x: auto = "hello"       # Shadowing with inferred type
```

*Implementation:*
- 🔄 Lowered - Generates variable names (`x`, `x_1_...`, `x_2_...`). The versioned
variable names are appended with UUIDs to prevent the user from predicting the
internal names and referencing them inadvertently.

### Pass Statement

See [pass_statement.md](pass_statement.md) for the pass statement.

### Break and Continue

See [break_continue.md](break_continue.md) for break and continue statements.

### Return Statement

See [return_statement.md](return_statement.md) for the return statement.

### Assert Statement

See [assert_statement.md](assert_statement.md) for the assert statement.

---

## Control Flow **[v0.1.0]**

See [if_statement.md](if_statement.md) for if/elif/else statements.

See [while_statement.md](while_statement.md) for while loops.

See [for_statement.md](for_statement.md) for for loops.

See [loop_else.md](loop_else.md) for else clauses on loops.

---

## Exception Handling **[v0.1.0]**

See [exception_handling.md](exception_handling.md) for exception types, try/except/finally, and raise statements.

---

## Functions **[v0.1.0]**

### Function Definition

```python
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

def print_message(message: str) -> None:
    print(message)

# With default parameters
def power(base: double, exponent: double = 2.0) -> double:
    return base ** exponent

# Multiple return values via tuple
def min_max(values: list[int]) -> tuple[int, int]:
    return (min(values), max(values))  # Note that the parentheses are optional
                                       # as the tuple is implied by the comma
```

**Rules:**
- All parameters must have type annotations
- Return type annotation required if function returns a value
- Return type can be omitted for `-> None` functions
- Parameters with defaults must come after required parameters

### Default Parameters

Functions can specify default values for parameters. Parameters with defaults must come after required parameters.

```python
def greet(name: str, greeting: str = "Hello") -> str:
    return f"{greeting}, {name}!"

def connect(host: str, port: int = 8080, timeout: double = 30.0) -> Connection:
    # ...
```

**Compile-Time Constant Requirement:**

Default parameter values must be compile-time constants, matching C# semantics. This eliminates the "mutable default argument" pitfall from Python; the pattern simply isn't expressible in Sharpy.

**Allowed default values:**

| Type | Examples | Notes |
|------|----------|-------|
| Numeric literals | `42`, `3.14`, `0xFF`, `1_000_000` | Any numeric literal with optional suffix |
| String literals | `"hello"`, `'world'`, `r"path\to\file"` | Including raw strings |
| Boolean literals | `True`, `False` | |
| `None` | `None` | Only for nullable parameter types |
| Enum values | `Color.RED`, `HttpMethod.GET` | |
| Constant references | `MAX_SIZE`, `DEFAULT_NAME` | Must reference a `const` declaration |

**Examples:**

```python
# ✅ Valid default parameters
def process(
    name: str = "default",
    count: int = 0,
    factor: double = 1.0,
    enabled: bool = True,
    mode: Mode = Mode.NORMAL,
    callback: Callable? = None
) -> None:
    pass

# ✅ Using None for optional parameters (recommended pattern)
def search(query: str, limit: int? = None, offset: int? = None) -> list[Result]:
    actual_limit = limit ?? 100
    actual_offset = offset ?? 0
    # ...

# ✅ Referencing constants
const DEFAULT_TIMEOUT: double = 30.0
const DEFAULT_RETRIES: int = 3

def fetch(url: str, timeout: double = DEFAULT_TIMEOUT, retries: int = DEFAULT_RETRIES) -> Response:
    # ...

# ❌ Invalid: mutable default values
def broken(items: list[int] = []) -> int:              # ERROR: [] is not a compile-time constant
    return sum(items)

def also_broken(config: dict[str, str] = {}) -> None:  # ERROR: {} is not a compile-time constant
    pass

def still_broken(point: Point = Point(0, 0)) -> None:  # ERROR: constructor call is not constant
    pass
```

**Pattern for Optional Mutable Arguments:**

Use `None` as the default and create the mutable object inside the function:

```python
def append_to(item: int, target: list[int]? = None) -> list[int]:
    if target is None:
        target = []
    target.append(item)
    return target

# Each call gets a fresh list
list1 = append_to(1)  # [1]
list2 = append_to(2)  # [2] - separate list, not [1, 2]
```

*Implementation: ✅ Native - Direct mapping to C# optional parameters.*

### Named (Keyword) Arguments **[v0.1.0]**

Sharpy supports calling functions with named arguments, allowing callers to specify parameter values by name rather than position:

```python
def create_user(name: str, age: int, active: bool = True) -> User:
    pass

# Positional arguments
user1 = create_user("Alice", 30, False)

# Named arguments
user2 = create_user(name="Bob", age=25)
user3 = create_user(age=25, name="Bob")  # Order doesn't matter for named args

# Mixed: positional first, then named
user4 = create_user("Charlie", age=35, active=False)

# ❌ Invalid: named before positional
user5 = create_user(name="Dave", 40)  # ERROR: positional argument follows keyword argument
```

**Named Argument Rules:**
- Named arguments must follow all positional arguments
- Once a named argument is used, all subsequent arguments must be named
- A parameter cannot be specified both positionally and by name

*Implementation: ✅ Native - Direct mapping to C# named arguments.*

### Variadic Arguments (`*args`) **[v0.1.0]**

Sharpy supports a limited form of variadic arguments using the `*args` syntax. Unlike Python's fully dynamic `*args`, Sharpy's variadic arguments are **homogeneously typed**—all arguments must be of the same type `T`.

#### Syntax

```python
def function_name(*args: T) -> ReturnType:
    # args is a tuple[T, ...] inside the function
    pass
```

**Examples:**

```python
# Sum any number of integers
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result

# Call with any number of arguments
total = sum_all(1, 2, 3)           # 6
total = sum_all(1, 2, 3, 4, 5)     # 15
total = sum_all()                   # 0 (empty tuple)

# Print multiple messages
def log_all(*messages: str) -> None:
    for msg in messages:
        print(msg)

log_all("Starting", "Processing", "Done")
```

#### Rules and Restrictions

**Homogeneous typing:**

All variadic arguments must be of the same declared type `T`:

```python
def process(*items: int) -> int:
    return sum(items)

process(1, 2, 3)              # OK: all ints
process(1, "two", 3)          # ERROR: "two" is str, not int
```

**Position requirement:**

The `*args` parameter must be the last parameter in the function signature:

```python
# ✅ Valid - *args at the end
def greet(prefix: str, *names: str) -> None:
    for name in names:
        print(f"{prefix} {name}")

greet("Hello", "Alice", "Bob", "Charlie")

# ❌ Invalid - *args not at the end
def broken(*items: int, suffix: str) -> None:  # ERROR
    pass
```

**Only one `*args` per function:**

```python
# ❌ Invalid - multiple *args
def broken(*a: int, *b: str) -> None:  # ERROR
    pass
```

**No `**kwargs`:**

Sharpy does not support `**kwargs` (variadic keyword arguments). Use named parameters with defaults or a configuration class instead:

```python
# ❌ Not supported
def configure(**options: str) -> None:  # ERROR: **kwargs not supported
    pass

# ✅ Use named parameters instead
def configure(host: str = "localhost", port: int = 8080) -> None:
    pass

# ✅ Or use a configuration class
class Config:
    host: str = "localhost"
    port: int = 8080

def configure(config: Config) -> None:
    pass
```

#### Type of `*args` Inside the Function

Inside the function body, the `*args` parameter has type `list[T]}`. This is a Sharpy abstraction over C#'s `params T[]` array:

```python
def analyze(*values: double) -> tuple[double, double]:
    # values: list[double]
    if len(values) == 0:
        return (0.0, 0.0)
    return (min(values), max(values))
```

**Note:** While C#'s `params` uses raw arrays (`T[]`), Sharpy wraps this in `list[T]` for consistency with the rest of the language. The overhead of this wrapper is minimal since the underlying storage is the same array passed by the caller.

**C# Interop:** When calling Sharpy variadic functions from C#, the `params` behavior is preserved:

```csharp
// Individual arguments (compiler creates array, Sharpy wraps in list)
var result = Analyze(1.0, 2.0, 3.0);

// Explicit array (Sharpy wraps in list)
var values = new double[] { 1.0, 2.0, 3.0 };
var result = Analyze(values);
```

#### Unpacking Iterables with `*`

When calling a function with `*args`, you can unpack an iterable using the `*` operator:

```python
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result

# Direct arguments
sum_all(1, 2, 3)              # 6

# Unpack a list
nums = [1, 2, 3, 4, 5]
sum_all(*nums)                # 15

# Unpack a homogenously-typed tuple
t = (10, 20, 30)
sum_all(*t)                   # OK: 60

# Mixed: direct args and unpacking
sum_all(1, 2, *[3, 4], 5)     # 15
```

**Type checking for unpacking:**

The unpacked iterable must contain elements of the correct type:

```python
def process(*items: int) -> int:
    return sum(items)

int_list: list[int] = [1, 2, 3]
str_list: list[str] = ["a", "b", "c"]
int_tuple = (10, 20, 30)
mixed_tuple = (10, "str", 30)

process(*int_list)            # OK
process(*str_list)            # ERROR: list[str] cannot unpack to *args: int
process(*int_tuple)           # OK
process(*mixed_tuple)         # ERROR: tuple[int, str, int] cannot unpack to *args: int
```

#### C# Interop: `params` Arrays

Sharpy's `*args` maps directly to C#'s `params` arrays, enabling seamless interop:

**Sharpy:**
```python
def format_message(template: str, *args: object) -> str:
    return template.format(*args)
```

**Generated C#:**
```csharp
public static string FormatMessage(string template, params object[] args) {
    return string.Format(template, args);
}
```

**Calling C# `params` methods from Sharpy:**

When calling .NET methods that use `params`, you can pass arguments naturally:

```python
from system import String

# String.Format has params signature: Format(string format, params object[] args)
result = String.format("Hello {0}, you have {1} messages", "Alice", 42)

# Or unpack from a collection
args = ["Bob", 10]
result = String.format("Hello {0}, you have {1} messages", *args)
```

**Calling Sharpy `*args` functions from C#:**

C# code can call Sharpy variadic functions using either individual arguments or an array:

```csharp
// Individual arguments (compiler creates array)
var total = SumAll(1, 2, 3, 4, 5);

// Explicit array
var numbers = new int[] { 1, 2, 3, 4, 5 };
var total = SumAll(numbers);
```

#### Function Type Compatibility

Function types cannot express variadic parameters. When you need a function type for a variadic function, use the non-variadic equivalent:

```python
def sum_all(*numbers: int) -> int:
    return sum(numbers)

# Cannot directly use sum_all as (int, int, int) -> int
# Instead, wrap it:
fixed_sum: (int, int, int) -> int = lambda a, b, c: sum_all(a, b, c)
```

*Implementation: ✅ Native - Maps to C# `params T[]` arrays.*

### No `**kwargs` Support

Sharpy does not support `**kwargs` (variadic keyword arguments). This aligns with .NET's type system which has no direct equivalent.

**Alternatives:**

```python
# Instead of: def configure(**kwargs) -> None

# Option 1: Use a typed class
class Config:
    host: str = "localhost"
    port: int = 8080
    debug: bool = False

def configure(config: Config) -> None:
    pass

# Option 2: Use named parameters with defaults
def configure(host: str = "localhost", port: int = 8080, debug: bool = False) -> None:
    pass

# Option 3: Use a dictionary parameter (loses type safety on values)
def configure(options: dict[str, object]) -> None:
    host = options.get("host") ?? "localhost"
    port = options.get("port") to int? ?? 8080
    # ...
```

### Positional-Only and Keyword-Only Parameters

Sharpy does not support Python's positional-only (`/`) or keyword-only (`*`) parameter markers. All parameters can be passed either positionally or by name.

```python
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: str) -> str:
    return f"String: {value}"

def process(value: int, multiplier: int) -> str:
    return f"Result: {value * multiplier}"
```

**Rules:**
- Overloads resolved by parameter count and types
- Parameter names do not affect resolution

*Implementation: ✅ Native - C# supports method overloading.*

### Empty and Placeholder Function Bodies

A function body can be empty or serve as a placeholder using several forms:

**Valid Empty/Placeholder Bodies:**

```python
# Ellipsis (preferred for abstract/interface methods)
def abstract_method(self) -> int:
    ...

# Pass statement
def not_yet_implemented(self) -> None:
    pass

# Docstring only
def documented_placeholder(self) -> str:
    """This method will return a greeting."""

# Comment only
def minimal_placeholder(self) -> None:
    # TODO: implement this

# Docstring and pass
def explicit_placeholder(self) -> int:
    """Returns the computed value."""
    pass
```

**Semantics of Each Form:**

| Body Content | Valid | Compiled Behavior |
|--------------|-------|-------------------|
| `...` (ellipsis) | ✅ | `throw new NotImplementedException()` |
| `pass` | ✅ | Empty body (no-op for `-> None`, undefined return otherwise) |
| Docstring only | ✅ | Empty body (docstring extracted for documentation) |
| Comment only | ✅ | Empty body |
| Docstring + `pass` | ✅ | Empty body |
| Docstring + `...` | ✅ | `throw new NotImplementedException()` |

**Usage Guidelines:**

- **Abstract methods and interface methods**: Use `...` (ellipsis)
- **Intentionally empty methods**: Use `pass`
- **Placeholder during development**: Use `...` or `pass` with a descriptive docstring
- **Documentation-only**: Docstring alone is valid but consider adding `pass` or `...` for clarity

```python
interface IProcessor:
    def process(self, data: bytes) -> bytes:
        """Process the input data and return the result."""
        ...  # Abstract - must be implemented

class BaseHandler:
    @virtual
    def on_event(self, event: Event) -> None:
        """Called when an event occurs. Override to handle events."""
        pass  # Default: do nothing

    @virtual
    def validate(self, input: str) -> bool:
        """Validate the input. Override to customize validation."""
        # Base implementation accepts everything
        return True
```

*Implementation: ✅ Native - Empty bodies compile to empty C# method bodies; ellipsis compiles to `throw new NotImplementedException()`.*

---

## Classes **[v0.1.0]**

### Basic Class Definition

```python
class Person:
    """A person with a name and age."""

    # Field declarations (required)
    name: str
    age: int

    # Constructor
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    # Instance method
    def greet(self) -> str:
        return f"Hello, I'm {self.name}"

    def celebrate_birthday(self) -> None:
        self.age += 1
```

**Rules:**
- All instance fields must be declared at class level with type annotations
- The `self` parameter is required for instance methods
- The `self` parameter is not type-annotated and cannot be annotated
- There is no `Self` type in Sharpy v1.0 (C# 9.0 has no equivalent; C# 11+ adds `TSelf` generic constraint patterns which may be supported in v2.0+)
- For fluent APIs returning the same type, you must name the concrete type explicitly:

```python
class Builder:
    name: str = ""

    def with_name(self, name: str) -> Builder:  # Must name the type explicitly
        self.name = name
        return self
```

- `__init__` return type is implicitly `None` and can be omitted or explicitly declared

```python
class Person:
    name: str

    # Both forms are valid and equivalent:
    def __init__(self, name: str):           # Implicit None return
        self.name = name

    def __init__(self, name: str) -> None:   # Explicit None return
        self.name = name
```

**Note:** The rule "return type can be omitted for `-> None` functions" applies universally to all functions and methods, not just `__init__`. This is consistent with C# where `void` methods simply have no return type in the signature:

```python
class Counter:
    value: int = 0

    def increment(self):        # Implicit -> None
        self.value += 1

    def reset(self) -> None:    # Explicit -> None (both valid)
        self.value = 0
```

*Implementation: ✅ Native - Direct mapping to C# class.*

### Constructor Overloading **[v0.1.2]**

```python
class Point:
    x: double
    y: double

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        self.x = other.x
        self.y = other.y
```

**Constructor Chaining:**

One constructor can delegate to another using `self.__init__(...)` as the first statement. This maps to C#'s `: this(...)` syntax:

```python
class Point:
    x: double
    y: double

    def __init__(self):
        self.__init__(0.0, 0.0)  # Chains to the two-parameter constructor

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __init__(self, xy: double):
        self.__init__(xy, xy)    # Chains to the two-parameter constructor
```

**Rules for Constructor Chaining:**

- `self.__init__(...)` must be the **first statement** in the constructor body
- Only one `self.__init__()` call is allowed per constructor
- The compiler detects this pattern and transforms it to C#'s `: this(...)` syntax
- After the chained constructor returns, execution continues with the rest of the body (if any)

```python
class Rectangle:
    x: double
    y: double
    width: double
    height: double

    def __init__(self, width: double, height: double):
        self.__init__(0.0, 0.0, width, height)  # Chain first
        print("Created rectangle")               # Then other statements

    def __init__(self, x: double, y: double, width: double, height: double):
        self.x = x
        self.y = y
        self.width = width
        self.height = height
```

*Implementation: 🔄 Lowered - `self.__init__(...)` as first statement transforms to `: this(...)` in C#.*

---

## Imports **[v0.1.0]**

### Import Statement

```python
# Import entire module
import math
result = math.sqrt(16.0)

# Import with alias
import math as m
result = m.sqrt(16.0)
```

*Implementation: ✅ Native - `using Namespace;` or `using Alias = Namespace;`*

### From-Import Statement

```python
# Import specific names
from math import sqrt, pi
result = sqrt(16.0)

# Import with alias
from math import sqrt as square_root

# Import all (use sparingly)
from math import *
```

*Implementation: ✅ Native - `using static` or direct reference.*

### Module Resolution

- Module names converted: `snake_case` → `PascalCase`
- Common acronyms uppercased: `io`, `ui`, `xml`, `http`, `api`, `sql`
- Example: `system.collections.generic` → `System.Collections.Generic`

### Package Structure

Packages are directories containing an optional `__init__.spy` file:

```
project/
    utils/
        __init__.spy      # Optional, can be empty
        helpers.spy
        math/
            __init__.spy
            vectors.spy
```

The `__init__.spy` file can re-export symbols for convenient imports:

```python
# utils/__init__.spy
from utils.helpers import format_string, parse_input
from utils.math.vectors import Vector2, Vector3
```

### Circular Import Handling

Circular imports are resolved through forward references in type annotations:

```python
# module_a.spy
from module_b import ClassB  # Forward reference for type annotation

class ClassA:
    other: ClassB  # OK - used only as type annotation

    def use_b(self, b: ClassB) -> None:
        b.method()
```

**How Forward References Work:**

Sharpy resolves imports in two phases:
1. **Type declaration phase**: Type names are registered (forward references allowed)
2. **Type resolution phase**: Full type information is resolved

When an import is used only in type annotations (not at runtime during `import` because runtime imports do not exist in Sharpy), circular references work automatically. No special syntax is needed.

```python
# file: parent.spy
from child import Child  # Works because Child only used in type annotations

class Parent:
    children: list[Child]  # Type annotation - resolved later

    def add_child(self, c: Child) -> None:  # Type annotation
        self.children.append(c)

# file: child.spy
from parent import Parent  # Works because Parent only used in type annotations

class Child:
    parent: Parent?  # Type annotation - resolved later
```

**Rules:**
- Circular references are allowed for type annotations
- Circular references for base classes are **not** allowed
- Import order matters: import for type hints processed before code execution
- If you get circular import errors, restructure to avoid runtime circular dependencies

---

## Structs **[v0.1.2]**

Structs are value types that do not support inheritance but can implement interfaces.

```python
struct Vector2:
    """A 2D vector value type."""

    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def magnitude(self) -> double:
        return (self.x ** 2 + self.y ** 2) ** 0.5

    def __add__(self, other: Vector2) -> Vector2:
        return Vector2(self.x + other.x, self.y + other.y)
```

**Struct Rules:**
- All fields must be declared at struct level
- If no constructor is defined, fields are zero-initialized (matching C# 9.0 struct semantics)
- Users can define additional constructors that initialize all or some fields
- When a constructor is defined, it must initialize all fields (C# requirement)
- Cannot inherit from other structs or classes
- Can implement interfaces (including interfaces with default methods)
- Value semantics: copied when assigned or passed

**Default Initialization:**

C# structs always have an implicit parameterless constructor that zero-initializes all fields. Sharpy structs inherit this behavior:

```python
struct Point:
    x: int
    y: int

# Using implicit parameterless constructor (zero-initialized)
p1 = Point()           # x = 0, y = 0

# Using explicit constructor
struct Vector:
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

**Structs and Interface Default Methods:**

Structs can implement interfaces that have default method implementations. However, be aware of boxing implications:

```python
interface IDescribable:
    def describe(self) -> str:
        return "An object"  # Default implementation

struct Point(IDescribable):
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    # Can override default, or use it as-is
    def describe(self) -> str:
        return f"Point({self.x}, {self.y})"

# Direct call - no boxing
p = Point(10, 20)
print(p.describe())  # "Point(10, 20)" - efficient

# Interface call - requires boxing (allocates)
d: IDescribable = p  # Boxing occurs here
print(d.describe())  # "Point(10, 20)" - works but allocates
```

**Performance Note:** When a struct is assigned to an interface variable or passed as an interface parameter, the struct is boxed (copied to the heap). For performance-critical code, prefer calling struct methods directly rather than through interface references.

**When to Use Structs:**
- Small data structures (typically < 16 bytes)
- Immutable value types (Vector2, Point, Color)
- Types that benefit from value semantics

*Implementation: ✅ Native - Direct mapping to C# `struct`.*

---

## Interfaces **[v0.1.2]**

Interfaces define contracts that types must satisfy.

```python
interface IDrawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        ...

    def get_bounds(self) -> tuple[double, double, double, double]:
        ...

# Implementation
class Circle(IDrawable):
    radius: double
    x: double
    y: double

    def __init__(self, x: double, y: double, radius: double):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self) -> None:
        print(f"Drawing circle at ({self.x}, {self.y})")

    def get_bounds(self) -> tuple[double, double, double, double]:
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)
```

**Interface Rules:**
- All methods are implicitly abstract unless they have a body that is not `...` (ellipsis), excluding docstrings, whitespace, and comments.
- Methods with an actual body (even just a `pass` statement) become the default implementation for that method.
- Implementing types must provide all methods that don't have a default implementation
and can override the implementation of those that do have a default implementation.

```python
interface ISomeInterface:
    # This method is abstract (the use of the ellipsis literal signals this)
    def method(self):
        ...

    # This method has a default implementation with an empty body
    def method2(self):
        pass
```

**Non-Void Methods and Empty Bodies:**

For non-void methods in interfaces (or anywhere), using `pass` alone is a compile error because the method must return a value:

```python
interface IFoo:
    # ✅ OK - abstract method (no implementation required)
    def get_value(self) -> int:
        ...

    # ❌ ERROR - non-void method body must return a value
    def get_other(self) -> int:
        pass  # Compile error: missing return statement

    # ✅ OK - provides a default return value
    def get_default(self) -> int:
        return 0
```

The distinction is:
- `...` (ellipsis) → abstract, no implementation
- `pass` → empty body, valid only for `-> None` methods as a default implementation
- For non-void methods, either use `...` (abstract) or provide a return statement

*Implementation: ✅ Native - Direct mapping to C# `interface`.*

### Generic Interfaces **[v0.1.3]**

```python
interface IContainer[T]:
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...
```

### Interface Inheritance **[v0.1.2]**

Interfaces can extend other interfaces:

```python
interface ISerializable:
    """Base interface for serialization."""
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends ISerializable with JSON-specific methods."""
    def to_json(self) -> str: ...
    def from_json(self, json: str) -> None: ...

class User(IJSONSerializable):
    """Must implement all methods from both interfaces."""
    username: str

    def serialize(self) -> str:
        return self.username

    def to_json(self) -> str:
        return f'{{"username": "{self.username}"}}'

    def from_json(self, json: str) -> None:
        pass  # Parse and update
```

### Default Method Implementations

Interfaces can provide default implementations for methods. Implementing types inherit the default unless they provide their own implementation:

```python
interface ILogger:
    def log(self, message: str) -> None:
        """Log a message. Must be implemented."""
        ...

    def log_info(self, message: str) -> None:
        """Log an info message. Has default implementation."""
        self.log(f"[INFO] {message}")

    def log_error(self, message: str) -> None:
        """Log an error message. Has default implementation."""
        self.log(f"[ERROR] {message}")

class ConsoleLogger(ILogger):
    # Must implement abstract method
    def log(self, message: str) -> None:
        print(message)

    # Inherits log_info and log_error defaults
    # Can optionally override them

class FileLogger(ILogger):
    path: str

    def __init__(self, path: str):
        self.path = path

    def log(self, message: str) -> None:
        # Write to file
        pass

    # Override default to add timestamp
    def log_error(self, message: str) -> None:
        self.log(f"[ERROR {datetime.now()}] {message}")
```

**Calling Other Interface Methods:**

Default implementations can call other methods defined in the same interface (including methods inherited by the interface through parent interfaces, without using `super()`):

```python
interface IValidator:
    def validate(self, value: str) -> bool:
        """Core validation logic. Must be implemented."""
        ...

    def is_valid(self, value: str) -> bool:
        """Check validity, returning boolean."""
        return self.validate(value)

    def validate_or_raise(self, value: str) -> None:
        """Validate and raise if invalid."""
        if not self.validate(value):
            raise ValueError(f"Invalid value: {value}")

    def validate_all(self, values: list[str]) -> bool:
        """Validate multiple values."""
        for value in values:
            if not self.validate(value):
                return False
        return True
```

### Conflict Resolution: Base Class vs Interface

When a class inherits the same method signature from both a base class and an interface, Sharpy follows C# resolution rules:

**Rule: Base class takes precedence over interface default implementations.**

```python
interface IGreeter:
    def greet(self) -> str:
        return "Hello from interface"

class BaseGreeter:
    def greet(self) -> str:
        return "Hello from base class"

class MyGreeter(BaseGreeter, IGreeter):
    # No override needed - inherits from BaseGreeter
    pass

g = MyGreeter()
print(g.greet())  # "Hello from base class"
```

**Accessing Interface Implementation via Casting:**

To explicitly call the interface's default implementation, cast to the interface type:

```python
g = MyGreeter()

# Base class method
print(g.greet())                    # "Hello from base class"

# Attempt to call interface default (via cast)
greeter: IGreeter = g
print(greeter.greet())              # "Hello from base class" - still base class!

# To truly access interface default, must use explicit interface implementation
```

**Explicit Interface Implementation:**

When you need different behavior when accessed through the interface versus directly:

```python
class MyGreeter(BaseGreeter, IGreeter):
    # Regular method (used when called on MyGreeter)
    def greet(self) -> str:
        return "Hello from MyGreeter"

    # Explicit interface implementation (used when called through IGreeter)
    def IGreeter.greet(self) -> str:
        return "Hello from IGreeter implementation"

g = MyGreeter()
print(g.greet())                    # "Hello from MyGreeter"

igreeter: IGreeter = g
print(igreeter.greet())             # "Hello from IGreeter implementation"
```

### When to Use Interfaces vs Abstract Classes

With default implementations available in interfaces, the choice between interfaces and abstract classes may seem unclear. Here are the key distinctions:

| Feature | Interface | Abstract Class |
|---------|-----------|----------------|
| Fields (state) | ❌ Cannot have fields | ✅ Can have fields |
| Multiple inheritance | ✅ A class can implement multiple interfaces | ❌ A class can only extend one class |
| Constructors | ❌ No constructors | ✅ Can have constructors |
| Access modifiers on members | ❌ All members implicitly public | ✅ Can have protected/private members |
| Default implementations | ✅ Supported (C# 8.0+) | ✅ Supported |

**Guidelines:**

- **Use interfaces** when defining a contract ("what can this do?") without requiring shared state
- **Use abstract classes** when you need shared state (fields) or protected members across a family of related types
- **Use interfaces** when a type needs to satisfy multiple contracts
- **Use abstract classes** for "is-a" relationships with shared implementation

```python
# Interface: defines capability without state
interface ISerializable:
    def serialize(self) -> str: ...

# Abstract class: shared state and partial implementation
class Entity:
    id: int                    # Shared field
    created_at: datetime       # Shared field

    def __init__(self, id: int):
        self.id = id
        self.created_at = datetime.now()

    @abstract
    def validate(self) -> bool:
        ...                    # Subclasses must implement
```

**Multiple Interface Conflicts:**

When multiple interfaces provide defaults for the same method, the implementing class must provide its own implementation:

```python
interface IA:
    def method(self) -> str:
        return "A"

interface IB:
    def method(self) -> str:
        return "B"

class C(IA, IB):
    # ❌ ERROR if omitted: ambiguous default implementations
    # ✅ Must provide explicit implementation
    def method(self) -> str:
        return "C"
```

*Implementation: ✅ Native - Direct mapping to C# default interface methods (C# 8.0+) and explicit interface implementation.*

### Dunder Methods in Interfaces

**Standard Library Only:**

Only interfaces defined in the Sharpy standard library can declare dunder methods. User-defined interfaces cannot declare dunders.

```python
# ✅ Standard library interface (Sharpy.Core)
interface IContextManager:
    def __enter__(self) -> object:
        ...

    def __exit__(self, exc_type: Type?, exc_val: Exception?, exc_tb: object?) -> bool:
        ...

# ✅ Standard library interface
interface IHashable:
    def __hash__(self) -> int:
        ...

    def __eq__(self, other: object) -> bool:
        ...

# ❌ ERROR: User-defined interface cannot declare dunders
interface IMyProtocol:
    def __custom__(self) -> int:    # ERROR: dunder methods not allowed
        ...

    def __len__(self) -> int:       # ERROR: dunder methods not allowed
        ...
```

**Rationale:**

1. **Controlled semantics**: Dunder methods have special meaning and compiler integration. Restricting them to the standard library ensures consistent behavior.

2. **Operator dispatch**: The compiler needs to know exactly which dunders exist and what they do. User-defined dunders would break this model.

3. **.NET interop**: Standard library interfaces map to well-known .NET interfaces (e.g., `IEnumerable`).

**Implementing Standard Library Dunder Interfaces:**

User code can implement standard library interfaces that contain dunders:

```python
from sharpy.core import IContextManager

class ManagedResource(IContextManager):
    _handle: int

    def __init__(self):
        self._handle = acquire_resource()

    def __enter__(self) -> ManagedResource:
        return self

    def __exit__(self, exc_type: Type?, exc_val: Exception?, exc_tb: object?) -> bool:
        release_resource(self._handle)
        return False  # Don't suppress exceptions

# Usage
with ManagedResource() as resource:
    use(resource)
```

**Standard Library Dunder Interfaces:**

| Interface | Dunders | Purpose |
|-----------|---------|---------|
| `IContextManager` | `__enter__`, `__exit__` | Context manager protocol |
| `IIterable[T]` | `__iter__` | Iteration protocol |
| `IIterator[T]` | `__next__` | Iterator protocol |
| `ISized` | `__len__` | Length protocol |
| `IContainer[T]` | `__contains__` | Membership protocol |
| `IHashable` | `__hash__`, `__eq__` | Hashable protocol |
| `IIndexable[K, V]` | `__getitem__`, `__setitem__` | Indexing protocol |
| `IComparable[T]` | `__lt__`, `__le__`, `__gt__`, `__ge__` | Ordering protocol |

*Implementation: Compiler validates that dunder declarations only appear in whitelisted standard library interfaces.*

---

## Inheritance **[v0.1.2]**

### Single Class Inheritance

Sharpy supports single class inheritance only. A class can extend at most one base class but may implement multiple interfaces.

```python
class Employee(Person):
    employee_id: str

    def __init__(self, name: str, age: int, employee_id: str):
        super().__init__(name, age)
        self.employee_id = employee_id

    def greet(self) -> str:
        return f"Hello, I'm {self.name}, employee #{self.employee_id}"
```

**Multiple Class Inheritance is Not Supported:**

```python
class A:
    pass

class B:
    pass

# ❌ ERROR: Multiple class inheritance not allowed
class C(A, B):  # ERROR: A class can only extend one base class
    pass

# ✅ OK: Single class + multiple interfaces
class C(A, ISerializable, IComparable):
    pass
```

*Implementation: ✅ Native - `: BaseClass`; `super().__init__()` → `: base()` or `base.Method()`*

### Multiple Interface Implementation

```python
class JSONEmployee(Employee, ISerializable, IComparable):
    def serialize(self) -> str:
        # Implementation
        pass

    def compare_to(self, other: object) -> int:
        # Implementation
        pass
```

**Rules:**
- Single class inheritance allowed
- Multiple interface implementation allowed
- Base class (if present) must come first

*Implementation: ✅ Native - `: BaseClass, IInterface1, IInterface2`*

### The `super()` Function

`super()` provides access to methods from a parent class. It is only valid in specific contexts.

**Valid Contexts for `super()`:**

| Context | Example | Purpose |
|---------|---------|---------|
| Inside `__init__` | `super().__init__(args)` | Call parent constructor |
| Inside dunder methods | `super().__eq__(other)` | Call parent dunder implementation |
| Inside overriding methods | `super().method()` | Call parent method being overridden |

**Constructor Chaining:**

Use `super().__init__()` to call the parent class constructor:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)  # ✅ Call parent constructor
        self.breed = breed
```

**Calling Overridden Methods:**

When a method overrides a parent method, `super()` can call the parent implementation:

```python
class Shape:
    @virtual
    def describe(self) -> str:
        return "A shape"

class Circle(Shape):
    radius: double

    def __init__(self, radius: double):
        self.radius = radius

    @override
    def describe(self) -> str:
        base_description = super().describe()  # ✅ Calls Shape.describe()
        return f"{base_description}: circle with radius {self.radius}"
```

**Inside Dunder Methods:**

Within a dunder method, `super()` can call the parent's dunder implementation. This includes calling the same dunder on the parent, or other dunders for cross-dunder synthesis:

```python
class Child(Parent):
    @override
    def __repr__(self) -> str:
        return super().__repr__() + " (child)"  # ✅ Same dunder on parent

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ Same dunder on parent
            return False
        # Additional comparison logic...
        return True

    def __le__(self, other: Child) -> bool:
        # Cross-dunder synthesis (also allowed on self, see Dunder Invocation Rules)
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK
```

**No chained `super()` for Multi-Level Inheritance:**

In deep inheritance hierarchies, you cannot chain `super()` calls to access methods from ancestors further up the chain:

```python
class A:
    @virtual
    def process(self) -> str:
        return "A"

class B(A):
    @override
    def process(self) -> str:
        return "B+" + super().process()  # Returns "B+A"

class C(B):
    @override
    def process(self) -> str:
        # Access immediate parent (B)
        b_result = super().process()              # "B+A"

        # Access grandparent (A) by chaining
        a_result = super().super().process()      # ERROR: Not allowed

        return f"C({b_result}, {a_result})"
```

**Invalid Contexts:**

`super()` is a compile-time error in any other context:

```python
class Example:
    value: int

    def __init__(self, value: int):
        self.value = value

    def regular_method(self) -> None:
        super().something()     # ❌ ERROR: regular_method does not override a parent method

    def another_method(self) -> int:
        return super().value    # ❌ ERROR: cannot access parent fields via super()

class Standalone:
    def method(self) -> None:
        super().__init__()      # ❌ ERROR: Standalone has no parent class

def free_function():
    super().something()         # ❌ ERROR: super() not valid outside class methods
```

**Compiler Error Message:**

When `super()` is used in an invalid context, the compiler provides a helpful error:

```
error: `super()` is only valid in:
  - `__init__()` (to call parent constructor)
  - dunder methods (to call parent dunders)
  - methods decorated with @override (to call overridden methods up the inheritance chain)
```

**Rules Summary:**

1. `super()` requires the class to have a parent class (compile error otherwise)
2. In `__init__`: can call `super().__init__(...)`
3. In `@override` methods: can call `super().method_name(...)` for overridden methods up the chain
4. In dunder methods: can call `super().__dunder__(...)` for parent dunders, and `super().__other_dunder__(...)` for cross-dunder synthesis
5. `super()` cannot be chained to access ancestors further up the inheritance hierarchy
6. Cannot use `super()` to access parent fields
7. Cannot use `super()` in non-overriding regular methods
8. Cannot use `super()` in free functions or static methods

*Implementation: 🔄 Lowered - `super()` maps to `base.Method()`*

---

## Decorators **[v0.1.2]**

Decorators modify the behavior of functions, methods, and classes.

**Decorator Ordering:**

When multiple decorators are applied, they are processed bottom-up (closest to the definition first), matching Python semantics:

```python
@A
@B
def foo():
    ...
# Equivalent to: foo = A(B(foo))
```

For Sharpy's built-in decorators (`@static`, `@virtual`, `@override`, `@abstract`, `@final`, etc.), the order typically doesn't matter since they're metadata flags rather than transforming decorators. However, it's conventional to place them in a consistent order:

```python
# Recommended ordering (when applicable)
@static          # Binding (static vs instance)
@virtual         # Inheritance behavior
@override
@final
@protected       # Access modifiers last
@private
@internal
```

### Access Modifiers

| Decorator | C# Equivalent | Visibility |
|-----------|---------------|------------|
| (default) | `public` | Everyone |
| `@protected` or `_name` | `protected` | Class and derived |
| `@private` or `__name` | `private` | Declaring class only |
| `@internal` | `internal` | Same assembly |

**Assembly Boundaries for `@internal`:**

In Sharpy, an assembly corresponds to a compiled project. Assembly boundaries are defined by:

- A `.spyproj` project file defines a single assembly
- All `.spy` files in the same project compile to the same assembly
- Each referenced project becomes a separate assembly

`@internal` members are accessible from any file within the same project but not from other projects that reference it.

```python
# In mylib/internal_utils.spy (part of mylib.spyproj)
@internal
def helper_function() -> None:
    pass

# In mylib/public_api.spy (same project) - OK
from mylib.internal_utils import helper_function  # ✅ Same assembly

# In app/main.spy (different project referencing mylib) - ERROR
from mylib.internal_utils import helper_function  # ❌ Different assembly
```

```python
class Example:
    @private
    def internal_method(self) -> None:
        pass

    # Naming convention also works
    def _protected_method(self) -> None:
        pass

    def __private_method(self) -> None:
        pass
```

*Implementation: ✅ Native - Direct mapping to C# access modifiers.*

### Method Modifiers

| Decorator | C# Equivalent | Notes |
|-----------|---------------|-------|
| `@static` | `static` | Class-level method, no `self` parameter |
| `@override` | `override` | Override virtual/abstract base method |
| `@virtual` | `virtual` | Method can be overridden by subclasses |
| `@abstract` | `abstract` | Must be overridden, no implementation |
| `@final` (method) | `sealed override` | Prevents further overriding |
| `@final` (class) | `sealed class` | Prevents inheritance |
| `@abstract` (class) | `abstract class` | Cannot be instantiated, may contain abstract members |

```python
class Calculator:
    @static
    def add(x: int, y: int) -> int:
        return x + y

    @virtual
    def compute(self, x: int) -> int:
        return x * 2

    @override
    def __str__(self) -> str:
        return "Calculator"

class ScientificCalculator(Calculator):
    @override
    def compute(self, x: int) -> int:
        return x ** 2

    @final
    @override
    def __str__(self) -> str:
        return "ScientificCalculator"

@final
class CannotBeExtended:
    """This class cannot be subclassed."""
    pass

# Usage
result = Calculator.add(5, 3)        # Static method call
calc = ScientificCalculator()
calc.compute(4)                      # Returns 16 (overridden method)
```

**Note:** Sharpy uses `@final` rather than C#'s `sealed` keyword to align with Python's `typing.final` decorator and Java's `final` keyword. The compiled output uses C#'s `sealed` keyword.

**Abstract Classes:**

Classes can be marked `@abstract` to indicate they cannot be instantiated directly and may contain abstract members. A class with any abstract members must be marked `@abstract`:

```python
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def area(self) -> double:
        ...  # Must be implemented by subclasses

    @abstract
    def perimeter(self) -> double:
        ...  # Must be implemented by subclasses

    # Non-abstract methods are allowed
    def describe(self) -> str:
        return f"{self.name} with area {self.area()}"

class Circle(Shape):
    radius: double

    def __init__(self, radius: double):
        super().__init__("Circle")
        self.radius = radius

    @override
    def area(self) -> double:
        return 3.14159 * self.radius ** 2

    @override
    def perimeter(self) -> double:
        return 2 * 3.14159 * self.radius

# Usage
# shape = Shape("test")    # ERROR: Cannot instantiate abstract class
circle = Circle(5.0)       # OK
print(circle.describe())   # "Circle with area 78.53975"
```

*Implementation: ✅ Native - Direct mapping to C# keywords.*

---

## Generics **[v0.1.3]**

### Generic Classes

```python
class Box[T]:
    """A container for a single value."""
    _value: T

    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value

    def set(self, value: T) -> None:
        self._value = value

# Usage
int_box = Box[int](42)
str_box = Box[str]("hello")
```

*Implementation: ✅ Native - `class Box<T>`*

### Generic Functions

```python
def identity[T](value: T) -> T:
    return value

def first[T](items: list[T]) -> T:
    return items[0]
```

*Implementation: ✅ Native - `T Identity<T>(T value)`*

### Type Constraints **[v0.1.3]**

```python
interface IComparable[T]:
    def __lt__(self, other: T) -> bool: ...

def find_max[T: IComparable[T]](items: list[T]) -> T:
    """Find the maximum item (must be comparable)."""
    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item
```

| Constraint | C# Equivalent |
|------------|---------------|
| `T: Interface` | `where T : Interface` |
| `T: class` | `where T : class` |
| `T: struct` | `where T : struct` |

*Implementation: ✅ Native - Direct mapping to C# generic constraints.*

---

## Enumerations **[v0.1.4]**

### Simple Enums

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"

# Usage
favorite = Color.RED
if favorite == Color.RED:
    print("Red is your favorite")

# Access underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"
```

**Rules:**
- All cases must have explicit constant values
- All values must be of the same type, either an integer type or the `str` type.
- Enums must have at least one case

**Enum Iteration and Methods:**

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# Iterate over all enum values
for color in Color:
    print(f"{color.name} = {color.value}")
# Output:
# RED = 1
# GREEN = 2
# BLUE = 3

# Get all values as a list
all_colors: list[Color] = list(Color)

# Get all names
names: list[str] = [c.name for c in Color]  # ["RED", "GREEN", "BLUE"]

# Get all values
values: list[int] = [c.value for c in Color]  # [1, 2, 3]
```

**Note:** Simple enums (non-tagged unions) cannot have custom methods. For enums with methods, use tagged unions (v0.2.0+).

*Implementation:*
- *Integer enums: ✅ Native - C# `enum`*
- *String enums: 🔄 Lowered - Static class with string constants*
- *`.name` property: 🔄 Lowered - `Enum.GetName()` or lookup*

---

## Operator Overloading **[v0.1.4]**

Classes can define dunder methods (double-underscore methods like `__add__`, `__eq__`) to customize how operators and built-in functions behave with their instances. **Dunder methods are a definition mechanism only**—they specify *how* a type behaves, but users invoke that behavior through operators and built-in functions, not by calling dunders directly.

### Dunder Invocation Rules **[v0.1.0]**

#### Dunders Are Definition-Only

Dunder methods exist to **define** how a type behaves with operators and built-in functions. **Explicit dunder invocation by user code is a compile error:**

```python
x = 5
x.__eq__(3)         # ERROR: Cannot invoke dunder methods directly
x.__repr__()        # ERROR: Cannot invoke dunder methods directly

my_list = [1, 2, 3]
my_list.__len__()   # ERROR: Cannot invoke dunder methods directly

obj = MyClass()
obj.__str__()       # ERROR: Cannot invoke dunder methods directly
```

#### Correct Usage

Use operators for operator dunders:

```python
x == y              # ✅ Correct — compiler uses __eq__ internally
x + y               # ✅ Correct — compiler uses __add__ internally
-x                  # ✅ Correct — compiler uses __neg__ internally
x < y               # ✅ Correct — compiler uses __lt__ internally
x[0]                # ✅ Correct — compiler uses __getitem__ internally
```

Use built-in functions for protocol dunders:

```python
repr(x)             # ✅ Correct — uses __repr__ internally
len(x)              # ✅ Correct — uses __len__ internally
hash(x)             # ✅ Correct — uses __hash__ internally
str(x)              # ✅ Correct — uses __str__ internally
```

#### Rationale

- **Uniform syntax**: `repr(x)` and `x == y` work on any type, whether primitive or Sharpy-defined
- **.NET interop**: Primitives from .NET (`int`, `str`, `bool`) don't have dunder methods—the compiler handles dispatch
- **Zero overhead**: No wrapper types or boxing required for polymorphic dispatch
- **Consistency**: Same syntax works whether the type defines a dunder or uses native behavior

> **Summary: Dunder Call Permissions**
>
> | Context | Allowed? |
> |---------|----------|
> | User code calling `x.__dunder__()` | ❌ Compile error |
> | Inside dunder method, calling `self.__other_dunder__()` | ✅ Allowed |
> | Inside dunder method, calling `super().__dunder__()` | ✅ Allowed |
> | Inside dunder method, calling `other_obj.__dunder__()` | ❌ Use operator/built-in |
> | Inside regular method, calling `self.__dunder__()` | ❌ Use built-in function |

*Implementation: The compiler emits different code based on static type:*
- *For primitives: direct C# operator or method call*
- *For Sharpy types with dunder: call to the generated method*
- *For built-in functions: type-appropriate dispatch (e.g., `len()` calls `.Count` or `__len__`)*

### Dunder Method Signatures

Dunder methods have compiler-enforced return types. The compiler validates that dunder method signatures match the expected protocol:

**Arithmetic Operators:**

| Dunder | Required Return Type | Notes |
|--------|----------------------|-------|
| `__add__(self, other: T)` | Same type as `self` or compatible | Binary `+` |
| `__sub__(self, other: T)` | Same type as `self` or compatible | Binary `-` |
| `__mul__(self, other: T)` | Same type as `self` or compatible | Binary `*` |
| `__truediv__(self, other: T)` | Same type as `self` or compatible | Binary `/` |
| `__floordiv__(self, other: T)` | `long` or float type | Binary `//` |
| `__mod__(self, other: T)` | Same type as `self` or compatible | Binary `%` |
| `__pow__(self, other: T)` | Same type as `self` or compatible | Binary `**` |
| `__neg__(self)` | Same type as `self` | Unary `-` |
| `__pos__(self)` | Same type as `self` | Unary `+` |

**Comparison Operators:**

| Dunder | Required Return Type |
|--------|----------------------|
| `__eq__(self, other: object)` | `bool` |
| `__eq__(self, other: T)` | `bool` |
| `__ne__(self, other: object)` | `bool` |
| `__ne__(self, other: T)` | `bool` |
| `__lt__(self, other: T)` | `bool` |
| `__le__(self, other: T)` | `bool` |
| `__gt__(self, other: T)` | `bool` |
| `__ge__(self, other: T)` | `bool` |

**Special Methods:**

| Dunder | Required Return Type | Notes |
|--------|----------------------|-------|
| `__str__(self)` | `str` | Human-readable string |
| `__repr__(self)` | `str` | Debug representation |
| `__hash__(self)` | `int` | Hash code |
| `__len__(self)` | `int` | Length/count |
| `__bool__(self)` | `bool` | Truthiness (for `if`, `while`, `and`, `or`, `not`) |
| `__true__()` | N/A | C# `operator true` (advanced, rarely needed) |
| `__false__()` | N/A | C# `operator false` (advanced, rarely needed) |
| `__contains__(self, item: T)` | `bool` | Membership test |
| `__iter__(self)` | `Iterator[T]` | Iteration |
| `__getitem__(self, key: K)` | `V` | Index access |
| `__setitem__(self, key: K, value: V)` | `None` | Index assignment |

**Compiler Enforcement:**

```python
class MyNumber:
    value: int

    def __init__(self, value: int):
        self.value = value

    # ✅ Correct return type
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, MyNumber):
            return False
        return self.value == other.value

    # ❌ ERROR: __eq__ must return bool
    def __eq__(self, other: object) -> int:
        return self.value

    # ✅ Correct return type
    def __str__(self) -> str:
        return f"MyNumber({self.value})"

    # ❌ ERROR: __str__ must return str
    def __str__(self) -> int:
        return self.value

    # ✅ Correct return type
    def __hash__(self) -> int:
        return hash(self.value)

    # ❌ ERROR: __hash__ must return int
    def __hash__(self) -> str:
        return str(self.value)
```

**Parameter Types:**

While return types are strictly enforced, parameter types for `other` in binary operations can vary based on what operations the type supports:

```python
class Vector:
    x: double
    y: double

    # Vector + Vector
    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    # Vector * scalar (different parameter type)
    def __mul__(self, other: double) -> Vector:
        return Vector(self.x * other, self.y * other)
```

This also applies to comparison operators like `__lt__()`. For `__eq__()` and `__ne__()` specifically, at least one overload must accept `object` (`System.Object`) as its argument. Additional overloads can be made for other types. This is actually satisfied by default for Sharpy reference types in Sharpy because they all derive from `Sharpy.Core.Object` which implements these dunder methods.

### Dunder Inheritance and Internal Calls **[v0.1.0]**

While user code cannot call dunders directly, there are specific contexts where dunder calls are permitted.

#### Dunder Inheritance

Dunder methods are inherited like any other method:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __repr__(self) -> str:
        return f"Animal({self.name})"

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    # Inherits __repr__ from Animal

dog = Dog("Buddy")
print(repr(dog))  # Output: Animal(Buddy)
```

#### Overriding Dunders

Dunder methods can be overridden using `@override`:

```python
class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def __repr__(self) -> str:
        return f"Dog({self.name})"

dog = Dog("Buddy")
print(repr(dog))  # Output: Dog(Buddy)
```

**Note:** The `@override` decorator is **required** when overriding inherited dunder methods, just like any other virtual method. All inheritable dunder methods from base classes are implicitly `@virtual`.

**Overriding Dunders from `Sharpy.Core.Object`:**

Since all Sharpy classes inherit from `Sharpy.Core.Object`, which provides default implementations for `__str__`, `__repr__`, `__eq__`, `__ne__`, and `__hash__`, overriding these dunders requires `@override`:

```python
class MyClass:
    value: int

    def __init__(self, value: int):
        self.value = value

    # Must use @override since __str__ is inherited from Sharpy.Core.Object
    @override
    def __str__(self) -> str:
        return f"MyClass({self.value})"

    # Same for __eq__, __hash__, etc.
    @override
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, MyClass):
            return False
        return self.value == other.value

    @override
    def __hash__(self) -> int:
        return hash(self.value)
```

This matches C# where overriding `ToString()`, `Equals()`, and `GetHashCode()` from `System.Object` requires the `override` keyword.

#### Base Class Dunder Calls

Within a dunder method, you may call the base class implementation via `super()`:

```python
class Child(Parent):
    @override
    def __repr__(self) -> str:
        return super().__repr__() + " (child)"  # ✅ OK

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ OK
            return False
        # Additional checks...
        return True
```

#### Cross-Dunder Calls for Synthesis

Within a dunder method, you may call other dunders on `self` for synthesizing related operations:

```python
class Ordered:
    value: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Ordered):
            return False
        return self.value == other.value

    def __lt__(self, other: Ordered) -> bool:
        return self.value < other.value

    def __le__(self, other: Ordered) -> bool:
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK

    def __ge__(self, other: Ordered) -> bool:
        return not self.__lt__(other)                    # ✅ OK

    def __ne__(self, other: object) -> bool:
        return not self.__eq__(other)                    # ✅ OK

    def __gt__(self, other: Ordered) -> bool:
        return not self.__le__(other)                    # ✅ OK
```

#### Restrictions

Dunder calls on `self` or `super()` are **only** permitted:
- Within a dunder method body
- As immediate call expressions (cannot be captured or passed)

```python
class Example:
    def __repr__(self) -> str:
        func = self.__eq__              # ❌ ERROR: Cannot capture dunder
        return str(self.__hash__())     # ✅ OK: Immediate call, cross-dunder

    def regular_method(self):
        self.__repr__()                 # ❌ ERROR: Not inside a dunder
        print(repr(self))               # ✅ OK: Use built-in function

    def __eq__(self, other: object) -> bool:
        return other.__eq__(self)       # ❌ ERROR: Not self or super()
```

#### Child Objects Use Built-in Functions

For calling dunder-like behavior on other objects (including fields), use operators or built-in functions:

```python
class Node:
    left: Node?
    right: Node?
    value: int

    def __repr__(self) -> str:
        left_repr = repr(self.left) if self.left is not None else "None"
        right_repr = repr(self.right) if self.right is not None else "None"
        return f"Node({self.value}, {left_repr}, {right_repr})"
        # NOT: self.left.__repr__()  # ❌ Would be error anyway

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Node):
            return False
        return self.value == other.value  # ✅ Use == operator
        # NOT: self.value.__eq__(other.value)  # ❌ Error
```

#### Summary Table

| Call Site | `self.__dunder__()` | `super().__dunder__()` | `other.__dunder__()` |
|-----------|--------------------|-----------------------|---------------------|
| Inside dunder method | ✅ Immediate only | ✅ Immediate only | ❌ Use operator/built-in |
| Outside dunder method | ❌ Error | ❌ Error | ❌ Use operator/built-in |

### Arithmetic Operators

```python
class Vector:
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __sub__(self, other: Vector) -> Vector:
        return Vector(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar: double) -> Vector:
        return Vector(self.x * scalar, self.y * scalar)

    def __neg__(self) -> Vector:
        return Vector(-self.x, -self.y)
```

| Operator | Dunder Method | C# Operator |
|----------|---------------|-------------|
| `+` | `__add__` | `operator +` |
| `-` | `__sub__` | `operator -` |
| `*` | `__mul__` | `operator *` |
| `/` | `__truediv__` | `operator /` |
| `//` | `__floordiv__` | (method call) |
| `%` | `__mod__` | `operator %` |
| `**` | `__pow__` | (method call) |
| `-x` | `__neg__` | `operator -` (unary) |
| `+x` | `__pos__` | `operator +` (unary) |

*Implementation: ✅ Native - Generates both dunder method and C# operator overload.*

### Comparison Operators

```python
class Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Point):
            return False
        return self.x == other.x and self.y == other.y

    def __lt__(self, other: Point) -> bool:
        return (self.x ** 2 + self.y ** 2) < (other.x ** 2 + other.y ** 2)
```

| Operator | Dunder Method | C# Method |
|----------|---------------|-----------|
| `==` | `__eq__` | `operator ==` + `Equals()` |
| `!=` | `__ne__` | `operator !=` |
| `<` | `__lt__` | `operator <` |
| `<=` | `__le__` | `operator <=` |
| `>` | `__gt__` | `operator >` |
| `>=` | `__ge__` | `operator >=` |

### Special Methods

| Method | Purpose | C# Mapping | Invoked Via |
|--------|---------|------------|-------------|
| `__str__` | String representation | `ToString()` override | `str(x)` |
| `__repr__` | Debug representation | Custom method | `repr(x)` |
| `__hash__` | Hash value | `GetHashCode()` override | `hash(x)` |
| `__len__` | Length | `Count` property | `len(x)` |
| `__contains__` | Membership test | `Contains()` method | `x in collection` |
| `__iter__` | Iteration | `GetEnumerator()` | `for x in obj` |
| `__getitem__` | Index access | Indexer `this[...]` | `obj[key]` |
| `__setitem__` | Index assignment | Indexer `this[...]` | `obj[key] = value` |
| `__delitem__` | Index deletion | (method call) | `del obj[key]` |

**`__contains__` Return Type:**

The `__contains__` dunder method must return `bool`. The `in` operator's result type is always `bool`, regardless of the implementation:

```python
class MyContainer:
    items: list[int]

    # Must return bool
    def __contains__(self, item: int) -> bool:
        return item in self.items

    # ❌ ERROR: __contains__ must return bool
    # def __contains__(self, item: int) -> int:
    #     return self.items.index(item)

c = MyContainer()
result: bool = 5 in c  # Always bool
```

**Note:** Users invoke these behaviors through the "Invoked Via" syntax, not by calling the dunder methods directly. See [Dunder Invocation Rules](#dunder-invocation-rules-v01) for details.

### Hashable Objects

For objects to be used as dictionary keys or in sets, they must implement `__hash__` and `__eq__`:

```python
class Coordinate:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Coordinate):
            return False
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return hash((self.x, self.y))

# Now usable as dict key or in sets
locations: dict[Coordinate, str] = {}
coord = Coordinate(10, 20)
locations[coord] = "Home"  # Works because __hash__ and __eq__ defined
```

**Rules for Hashable Objects:**
- If `__eq__` is defined, `__hash__` must also be defined, and vice versa
- If `a == b`, then `hash(a) == hash(b)` must be true
- Hash value should not change during object lifetime
- Mutable objects should not implement `__hash__`

*Implementation: ✅ Native or 🔄 Lowered depending on the method.*

---

## Pattern Matching **[v0.1.6]**

### Match Statement

```python
def describe(value: object) -> str:
    match value:
        case 0:
            return "zero"
        case 1:
            return "one"
        case int() as n if n > 0:
            return "positive integer"
        case int() as n:
            return "negative integer"
        case str() as s:
            return f"string: {s}"
        case _:
            return "unknown"
```

*Implementation: ✅ Native - Maps to C# `switch` expression/statement (C# 8+).*

### Match Statement vs Match Expression

Sharpy supports both statement and expression forms of `match`, corresponding to C#'s switch statement and switch expression:

**Statement Form:**

Used when you need to execute statements for each case:

```python
match value:
    case 1:
        do_something()
        log("did something")
    case 2:
        do_other()
    case _:
        handle_default()
```

**Expression Form:**

Used when you want to produce a value:

```python
result = match value:
    case 1: "one"
    case 2: "two"
    case _: "other"

# Can be used anywhere an expression is expected
print(match x:
    case True: "yes"
    case False: "no"
)

# In a return statement
def categorize(n: int) -> str:
    return match n:
        case 0: "zero"
        case _ if n > 0: "positive"
        case _: "negative"
```

**Expression Form Rules:**
- Each case must be a single expression (not statements)
- All cases must produce values of compatible types
- Must be exhaustive (all possible values handled)
- Cases use `:` followed by an expression, not a block

*Implementation: 🔄 Lowered*
- *Statement form: C# `switch` statement*
- *Expression form: C# `switch` expression*

### Supported Patterns

| Pattern | Syntax | C# 9.0 Mapping |
|---------|--------|----------------|
| Literal | `case 0:` | `case 0:` |
| Type | `case int():` | `case int:` |
| Type with binding | `case int() as n:` | `case int n:` |
| Wildcard | `case _:` | `default:` or `_` |
| Guard | `case int() as n if n > 0:` | `case int n when n > 0:` |
| OR | `case "a" \| "b":` | `case "a" or "b":` |
| Tuple | `case (0, 0):` | Direct support |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` |
| Relational | `case > 0:` | Direct support (C# 9) |

*Implementation: ✅ Native - All patterns map to C# 9.0 pattern matching.*

### Tuple Patterns

```python
match point:
    case (0, 0):
        print("Origin")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (x, y):
        print(f"Point at ({x}, {y})")
```

### Property Patterns

```python
match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
```

### Positional Patterns

For types with a `Deconstruct` method (like records or types with explicit deconstruction), positional patterns extract values in order:

```python
# Assuming Point has Deconstruct(out double x, out double y)
match point:
    case Point(0, 0):              # Positional - matches x=0, y=0
        print("Origin")
    case Point(x, 0):              # Positional with binding
        print(f"On X-axis at {x}")
    case Point(0, y):              # Positional with binding
        print(f"On Y-axis at {y}")
    case Point(x, y):              # Positional with both bound
        print(f"Point at ({x}, {y})")

# Type pattern with binding (no Deconstruct needed)
match value:
    case int() as n:               # Type check and bind
        print(f"Integer: {n}")
    case str() as s if len(s) > 0: # Type, bind, and guard
        print(f"Non-empty string: {s}")
```

**Pattern Forms:**

| Pattern | Syntax | Use Case |
|---------|--------|----------|
| Property | `Point(x=0, y=y)` | Extract by property name |
| Positional | `Point(0, y)` | Extract by position (requires `Deconstruct`) |
| Type with binding | `int() as n` | Check type and bind entire value |

### Exhaustiveness Checking

The compiler checks that `match` statements cover all possible cases for certain types:

**Checked Types:**

| Type | Requirement |
|------|-------------|
| Enums | All enum values must be covered |
| `bool` | Must cover `True` and `False` |
| Tagged unions | All cases must be covered |
| Other types | Wildcard `_` or explicit default required |

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# ERROR: Non-exhaustive match (missing BLUE)
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")

# OK: Exhaustive with wildcard
match color:
    case Color.RED:
        print("Red")
    case _:
        print("Other color")

# OK: Fully exhaustive
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")
    case Color.BLUE:
        print("Blue")

# Boolean exhaustiveness
match flag:
    case True:
        print("Yes")
    # ERROR: missing False case
```

---

## Type Aliases **[v0.1.7]**

Type aliases create readable names for complex types:

```python
# Module-level aliases
type UserId = int
type Coordinate = tuple[double, double]
type Matrix = list[list[double]]

# Generic aliases
type Callback[T] = (T) -> None
type Res[T, E] = Result[T, E]

# Class-level aliases
class Geometry:
    type Point3D = tuple[double, double, double]

    def distance(self, p1: Point3D, p2: Point3D) -> double:
        dx, dy, dz = p1[0] - p2[0], p1[1] - p2[1], p1[2] - p2[2]
        return (dx**2 + dy**2 + dz**2) ** 0.5

# Function-level aliases
def process_data[T, E](items: dict[str, list[Result[T, E]]]) -> dict[str, list[Result[T, E]]]:
    type DataMap = dict[str, list[Result[T, E]]]
    result: DataMap = {}
    # ...
    return result
```

*Implementation: 🔄 Lowered - Inline expansion at use sites; `using` directive where possible.*

---

## Tagged Unions (Algebraic Data Types) **[v0.2.0]**

Tagged unions allow cases to carry associated data:

```python
# Generic Result type (like Rust's Result)
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Optional type (like Rust's Option)
enum Optional[T]:
    case Some(value: T)
    case Nothing()

# Tree structure
enum BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])
```

**Unit Cases (No Data):**

Cases that carry no associated data can be defined with or without parentheses:

```python
enum Option[T]:
    case Some(value: T)
    case Nothing           # No parentheses needed for unit case
    # case Nothing()       # Also valid, but parentheses are optional

enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

enum LoadState:
    case NotStarted        # Unit case
    case Loading           # Unit case
    case Loaded(data: str) # Data case
    case Failed(error: str) # Data case
```

**Pattern Matching Unit Cases:**

When pattern matching, unit cases also don't require parentheses:

```python
match opt:
    case Option.Some(v): print(v)
    case Option.Nothing: print("none")  # No parens in pattern

match state:
    case LoadState.NotStarted: start_loading()
    case LoadState.Loading: show_spinner()
    case LoadState.Loaded(data): display(data)
    case LoadState.Failed(err): show_error(err)
```

### Creating Values

Tagged union cases are created using the enum type name followed by the case name:

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Create values using Type.Case() syntax
success: Result[int, str] = Result.Ok(42)
failure: Result[int, str] = Result.Err("Something went wrong")
```

**Note:** Case names follow the same casing as defined in the enum declaration (typically `PascalCase`). The syntax `Result.Ok(42)` is a constructor call that creates an instance of the `Ok` case. This of course is just a convention and is not enforced by the compiler.

### Pattern Matching

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.Err("Division by zero")
    return Result.Ok(a / b)

result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")
```

### Methods on Tagged Unions

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Result.Ok():
                return True
            case Result.Err():
                return False

    def unwrap(self) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err():
                return default
```

*Implementation: 🔄 Lowered - Abstract base class + sealed nested case classes:*

```csharp
public abstract class Result<T, E> {
    private Result() { }

    public sealed class Ok : Result<T, E> {
        public T Value { get; }
        public Ok(T value) => Value = value;
        public void Deconstruct(out T value) => value = Value;
    }

    public sealed class Err : Result<T, E> {
        public E Error { get; }
        public Err(E error) => Error = error;
        public void Deconstruct(out E error) => error = Error;
    }
}
```

---

## Try expressions **[v0.2.0]**

The `Result[T, E]` type can be implicitly created via
`try` expressions. A `try` expression wraps the value of
the expression in `Result[T, E]` where `E`, if not
specified, is always the base `Exception` type, and `T` is
the type of the expression. If the expression raises an
exception, then the result holds its `Err` case.

```python
x = try int("some string")  # x is of type Result[int, Exception]
```

A `try` expression can be specified for a specific type
where if the expression throws that type, then it is caught
inside `Err` case. Other types become uncaught exceptions
that must be handled by other means, e.g. `try/except/finally`.

```python
x = try[ValueError] int("some string")  # x is of type Result[int, ValueError]
```

It is not an error if the expression would never raise an
exception. In such cases, the result type is always `Result[T, Exception]` where `T` is the expression's type.

**Precedence Rules:**

The `try` expression has low precedence, binding only to the immediately following primary expression and its arguments:

```python
# try binds to the function call only
x = try int("abc") + 5       # Parsed as: (try int("abc")) + 5
                             # If int() succeeds: Result.Ok + 5 = ERROR (can't add)
                             # Typically you'd unwrap first

# Use parentheses for clarity or different grouping
x = try (int("abc") + 5)     # Parsed as: try (int("abc") + 5)
                             # Exception in either int() or + is caught

# With conditional
y = try foo() if cond else bar()   # Parsed as: (try foo()) if cond else bar()
                                   # try only applies to foo(), not bar()

# Parentheses make intent clear
y = try (foo() if cond else bar())  # try applies to entire conditional
```

*Implementation: 🔄 Lowered - `try`/`catch` pattern wrapping the expression.*

---

## Maybe expressions **[v0.2.0]**

Optionals can be implicitly created via `maybe` expressions.
A `maybe` expression wraps the value of the expression in
`Optional[T]` where `T` is the type of the expression.
If the expression is `None`, then the result
holds its `Nothing` case.

```python
d: dict[str, int] = {"y": 5}
x = maybe d.get("x")  # x is of type Optional[int]
```

It is a type-checking error if the expression does not return
a nullable type (`T?`).

```python
# ✅ Valid - dict.get() returns T?
d: dict[str, int] = {}
x = maybe d.get("key")       # OK: get() returns int?

# ✅ Valid - explicitly nullable
value: int? = get_optional_value()
y = maybe value              # OK: value is int?

# ❌ Invalid - expression is not nullable
s: str = "hello"
z = maybe s.upper()          # ERROR: upper() returns str, not str?

n: int = 42
w = maybe n                  # ERROR: n is int, not int?
```

**Precedence Rules:**

Like `try`, the `maybe` expression has low precedence:

```python
x = maybe d.get("key") ?? 0    # Parsed as: (maybe d.get("key")) ?? 0
                               # ERROR: Optional[int] ?? int doesn't work directly

# Use the Optional's methods instead
x = (maybe d.get("key")).unwrap_or(0)
```

---

## Comprehensions **[v0.1.8]**

Comprehensions provide concise syntax for creating collections by transforming and filtering iterables.

### List Comprehensions

```python
# Basic transformation
squares = [x ** 2 for x in range(10)]
# [0, 1, 4, 9, 16, 25, 36, 49, 64, 81]

# With filter condition
evens = [x for x in range(10) if x % 2 == 0]
# [0, 2, 4, 6, 8]

# Transformation and filter
doubled_evens = [x * 2 for x in range(10) if x % 2 == 0]
# [0, 4, 8, 12, 16]

# Nested comprehension (comprehension inside comprehension)
matrix = [[i * j for j in range(3)] for i in range(3)]
# [[0, 0, 0], [0, 1, 2], [0, 2, 4]]
```

*Implementation: 🔄 Lowered - LINQ expressions:*
- `[expr for x in iter]` → `.Select(x => expr).ToList()`
- `[expr for x in iter if cond]` → `.Where(x => cond).Select(x => expr).ToList()`

**Filter and Transform Order:**

The filter (`if` clause) is applied **before** the transformation, matching Python semantics exactly:

```python
[x * 2 for x in items if x > 0]
# Equivalent to:
# result = []
# for x in items:
#     if x > 0:           # Filter first
#         result.append(x * 2)  # Then transform
```

This maps to LINQ's `.Where(...).Select(...)` ordering:

```csharp
// C# equivalent
items.Where(x => x > 0).Select(x => x * 2).ToList();
```

### Multiple For Clauses

Comprehensions can have multiple `for` clauses, which are evaluated left-to-right like nested loops:

```python
# Multiple for clauses
pairs = [(x, y) for x in range(3) for y in range(3)]
# Equivalent to:
# result = []
# for x in range(3):
#     for y in range(3):
#         result.append((x, y))
# [(0,0), (0,1), (0,2), (1,0), (1,1), (1,2), (2,0), (2,1), (2,2)]

# With filter on inner loop
pairs_filtered = [(x, y) for x in range(3) for y in range(3) if x != y]
# [(0,1), (0,2), (1,0), (1,2), (2,0), (2,1)]

# Later clauses can reference earlier variables
triangular = [(x, y) for x in range(4) for y in range(x)]
# [(1,0), (2,0), (2,1), (3,0), (3,1), (3,2)]
```

*Implementation: 🔄 Lowered - LINQ `SelectMany`:*
```csharp
// [(x, y) for x in range(3) for y in range(3)]
Enumerable.Range(0, 3)
    .SelectMany(x => Enumerable.Range(0, 3), (x, y) => (x, y))
    .ToList();
```

### Dict Comprehensions

```python
# Basic dict comprehension
square_dict = {x: x ** 2 for x in range(5)}
# {0: 0, 1: 1, 2: 4, 3: 9, 4: 16}

# From existing collection
names = ["alice", "bob", "charlie"]
name_lengths = {name: len(name) for name in names}
# {"alice": 5, "bob": 3, "charlie": 7}

# With filter
long_names = {name: len(name) for name in names if len(name) > 3}
# {"alice": 5, "charlie": 7}
```

*Implementation: 🔄 Lowered - `.ToDictionary(x => key, x => value)`*

### Set Comprehensions

```python
# Basic set comprehension
unique_lengths = {len(word) for word in ["apple", "banana", "cherry"]}
# {5, 6}

# With filter
short_lengths = {len(word) for word in ["apple", "banana", "cherry"] if len(word) < 7}
# {5, 6}
```

*Implementation: 🔄 Lowered - `.Select(...).ToHashSet()`*

### Comprehension Variable Scoping

Variables declared in comprehensions are scoped to that comprehension and do not leak into the enclosing scope:

```python
# Variables don't leak
squares = [i ** 2 for i in range(10)]
print(i)  # ERROR: 'i' does not exist in this scope

# Dict comprehension variables don't leak
ages = {name: age for name, age in pairs}
print(name)  # ERROR: 'name' does not exist in this scope
```

**Shadowing Outer Variables:**

Comprehension variables may shadow variables from the enclosing scope. The outer variable is not modified:

```python
x = 100
squares = [x ** 2 for x in range(5)]  # This 'x' shadows outer 'x'
print(x)  # 100 - outer 'x' unchanged
print(squares)  # [0, 1, 4, 9, 16]

name = "outer"
lengths = {name: len(name) for name in ["a", "bb", "ccc"]}
print(name)  # "outer" - unchanged
```

**Unique Variable Names Required:**

Within a single comprehension, each `for` clause must use a unique variable name. Reusing a variable name across multiple `for` clauses is a compile-time error:

```python
# ✅ OK - different variable names in each for clause
pairs = [(x, y) for x in range(3) for y in range(3)]

# ✅ OK - shadows outer scope (different from reuse within comprehension)
x = 100
result = [(x, y) for x in range(3) for y in range(3)]

# ❌ ERROR - same variable name in multiple for clauses
bad = [x for x in range(3) for x in range(3)]
# Compile error: Variable 'x' already declared in this comprehension

# ❌ ERROR - even with different structure
also_bad = [(x, x) for x in range(3) for x in range(3)]
# Compile error: Variable 'x' already declared in this comprehension
```

**Rationale:** Allowing the same variable name in multiple `for` clauses creates confusing code where the inner loop shadows the outer loop variable. This is almost always a bug rather than intentional behavior. Sharpy prohibits this pattern at compile time.

**Filter Clause Scope:**

Filter conditions (`if` clauses) can reference any variable declared in preceding `for` clauses:

```python
# Filter can use variables from any preceding for clause
result = [(x, y) for x in range(5) for y in range(5) if x + y < 4]
# [(0,0), (0,1), (0,2), (0,3), (1,0), (1,1), (1,2), (2,0), (2,1), (3,0)]

# Multiple filters
filtered = [x for x in range(20) if x % 2 == 0 if x % 3 == 0]
# [0, 6, 12, 18]
```

---

## Walrus Operator **[v0.1.8]**

The walrus operator `:=` allows assignment within expressions:

```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found match at {match.start()}")

# Reuse computed value
results = [y for x in data if (y := transform(x)) is not None]

# Avoid repeated calls
while (line := file.read_line()) is not None:
    process(line)
```

**Type Inference Only:**

The walrus operator always infers the type from the right-hand side expression. Type annotations are not supported with `:=` (matching Python 3.8+ behavior):

```python
# ✅ Valid - type inferred from get_value()
if (x := get_value()) > 0:
    pass

# ❌ Invalid - cannot annotate with walrus
if (x: int := get_value()) > 0:  # ERROR: type annotation not supported with :=
    pass
```

Since Sharpy has full static type information, the type of `get_value()` is known at compile time, making explicit annotation unnecessary.

**Walrus Operator in Comprehensions:**

Variables assigned with `:=` inside a comprehension follow special scoping rules:

```python
# Variable assigned in comprehension filter DOES leak to outer scope
results = [y for x in data if (y := transform(x)) is not None]
print(y)  # OK: y is defined (holds the last assigned value)

# This is because := creates in "containing scope", not comprehension scope
# The comprehension iteration variable (x) does NOT leak
print(x)  # ERROR: x is not defined

# Be cautious: y's final value is the last successful transform
# This may not be the value you expect
```

**Contrast with Comprehension Variables:**

| Variable Type | Scope | Leaks? |
|--------------|-------|--------|
| Iteration variable (`for x in`) | Comprehension | ❌ No |
| Walrus assignment (`y :=`) | Containing | ✅ Yes |

*Implementation: 🔄 Lowered - Hoisted variable declaration:*

```python
# Sharpy
if (match := pattern.search(text)) is not None:
    print(match.group())
```
```csharp
// C# 9.0
var match = pattern.Match(text);
if (match.Success) {
    Console.WriteLine(match.Value);
}
```

---
## Properties **[v0.1.2]**

Properties provide controlled access to object state with support for computed values, validation, and fine-grained access control. Sharpy properties map cleanly to C# properties while maintaining Pythonic readability.

### Property Forms

Sharpy supports two property forms:

| Form | Use Case | Syntax Pattern |
|------|----------|----------------|
| Auto-property | Simple storage with compiler-generated backing field | `property [get\|set\|init]? name: T [= value]` |
| Function-style property | Custom logic, user-provided backing field | `property (get\|set) name(self, ...) -> T:` |

**Key Distinction:**
- **Auto-properties** generate a backing field automatically (opaque to the user)
- **Function-style properties** require the user to provide their own backing field (or compute the value)

### Auto-Properties

Auto-properties generate a backing field and accessors automatically:

```python
class Person:
    # Read-write (default, has both get and set)
    property name: str = "Unknown"
    property age: int              # Zero-initialized (value type)

    # Read-only getter (must have default value OR be set in constructor)
    property get id: int = 0
    property get uuid: str         # Must be set in __init__

    # Init-only (readable, but can only be set at declaration or in constructor)
    property init created_at: datetime   # Must be set in __init__
    property init email: str = "unknown@example.com"

    # Write-only (rare, typically combined with public getter)
    property set password_hash: str

    def __init__(self, name: str, age: int, id: int, uuid: str, email: str, password: str):
        self.name = name
        self.age = age
        self.id = id             # OK: can set read-only in constructor
        self.uuid = uuid         # Required: no default value
        self.created_at = datetime.now()  # Required: init property, no default
        self.email = email       # OK: overrides default
        self.password_hash = hash_password(password)

# After construction:
p = Person("Alice", 30, 1, "abc-123", "alice@example.com", "secret")
p.name = "Bob"           # OK: read-write
p.id = 2                 # ERROR: read-only property (no setter)
p.email = "new@test.com" # ERROR: init-only, cannot set after construction
print(p.password_hash)   # ERROR: write-only property (no getter)
```

**Auto-Property Modifiers:**

The auto-property modifiers (or lack thereof) are mutually exclusive; for a given property
named X, only one of the following are possible within a given class/struct/interface.

| Syntax | Accessors | Readable | Settable in `__init__` | Settable after |
|--------|-----------|----------|------------------------|----------------|
| `property name: T` | get + set | ✅ | ✅ | ✅ |
| `property get name: T` | get only | ✅ | ✅ | ❌ |
| `property set name: T` | set only | ❌ | ✅ | ✅ |
| `property init name: T` | get + init | ✅ | ✅ | ❌ |

**Difference between `property get` and `property init`:**
- `property get name: T` — getter-only; can have a default value or be set in constructor, then immutable
- `property init name: T` — getter + init-only setter; **must** be set at declaration or in every constructor (no zero-initialization); immutable after construction

**Auto-Property Initialization Rules:**

| Modifier | Default Value | Zero-Init (value types) | Must set in `__init__` |
|----------|---------------|-------------------------|------------------------|
| `property` | Optional | ✅ Yes | If no default (ref types) |
| `property get` | Optional | ✅ Yes | If no default (ref types) |
| `property set` | Optional | ✅ Yes | No |
| `property init` | Optional | ❌ No | If no default |

```python
class Example:
    property name: str           # Must be assigned in __init__ (reference type)
    property count: int          # Zero-initialized to 0 (value type)
    property label: str = ""     # Default value provided
    property get id: int = 0     # Read-only with default
    property init token: str     # MUST be set in __init__ (no zero-init allowed)

    def __init__(self, name: str, token: str):
        self.name = name         # Required: no default for reference type
        self.token = token       # Required: init property without default
        # self.count not assigned - will be 0 (value type default)
```

*Implementation: ✅ Native*
```csharp
public string Name { get; set; }
public int Count { get; set; }
public string Label { get; set; } = "";
public int Id { get; } = 0;
public string Token { get; init; }
```

### Function-Style Properties

For properties requiring custom logic (validation, transformation, computation), use function-style syntax. The user must provide their own backing field or compute the value. You cannot combine get/set/init auto-properties with custom logic get/set/init, since the backing field for the auto-property cannot be accessed from the custom logic.

#### Function-Style Getter

```python
class Rectangle:
    width: double
    height: double

    def __init__(self, width: double, height: double):
        self.width = width
        self.height = height

    # Computed property (no backing field needed)
    property get area(self) -> double:
        return self.width * self.height

    property get perimeter(self) -> double:
        return 2 * (self.width + self.height)

    property get is_square(self) -> bool:
        return self.width == self.height

    # Multi-statement bodies work naturally
    property get diagonal(self) -> double:
        w_sq = self.width ** 2
        h_sq = self.height ** 2
        return (w_sq + h_sq) ** 0.5

    # Can reference other properties
    property get description(self) -> str:
        shape = "square" if self.is_square else "rectangle"
        return f"A {shape} with area {self.area}"
```

*Implementation: ✅ Native*
```csharp
public double Area => Width * Height;
public double Perimeter => 2 * (Width + Height);
public bool IsSquare => Width == Height;

public double Diagonal {
    get {
        var wSq = Width * Width;
        var hSq = Height * Height;
        return Math.Sqrt(wSq + hSq);
    }
}
```

#### Function-Style Setter

```python
class Temperature:
    _celsius: double = 0.0

    # Function-style setter with validation
    property set celsius(self, value: double):
        if value < -273.15:
            raise ValueError("Temperature below absolute zero")
        self._celsius = value

    # Cannot combine with auto getter
    property get celsius: double # ERROR: no auto backing field with function-style!
```

It is possible to have both a function-style getter and setter. However, function-style getter/setters cannot coexist with an auto-property for the same property name since there is no way to retrieve the backing field.

**Important:** Function-style accessors do **not** generate a backing field. You must provide your own storage.

**Type Consistency:** The type must be the same across all accessors (get/set/init) for a property.

**No Function-Style `init`:** There is no `property init name(self, value: T):` form because init-only semantics require compiler support for constructor-only assignment, which doesn't compose well with user-defined logic.

### Mixed Access Modifiers

Getters and setters can have different visibility:

```python
class Counter:
    _value: int = 0

    # Public getter
    property get value(self) -> int:
        return self._value

    # Private setter (only accessible within the class)
    @private
    property set value(self, v: int):
        self._value = v

    # Public methods can use the private setter
    def increment(self):
        self.value += 1

    def reset(self):
        self.value = 0

# Usage
c = Counter()
print(c.value)    # OK: public getter
c.increment()     # OK: internal modification via public method
c.value = 10      # ERROR: setter is private
```

**With Auto-Properties:**

```python
class User:
    property get name: str           # Public getter
    @private
    property set name: str           # Private setter

    def __init__(self, name: str):
        self.name = name             # OK: inside class
```

**Common Access Patterns:**

| Pattern | Getter | Setter | Use Case |
|---------|--------|--------|----------|
| Read-write | (default) | (default) | Mutable public state |
| Read-only | (default) | (none) | Computed or immutable |
| Observable | (default) | `@private` | External read, internal write |
| Protected write | (default) | `@protected` | Subclass modification |
| Internal write | (default) | `@internal` | Assembly-internal modification |

*Implementation: ✅ Native*
```csharp
public int Value {
    get => _value;
    private set => _value = value;
}
```

### Static Properties

Use `@static` decorator for class-level properties. Static properties take no `self` parameter:

```python
class AppConfig:
    _debug_mode: bool = False
    _instance_count: int = 0

    # Static auto-properties
    @static
    property version: str = "1.0.0"

    @static
    property get build_number: int = 42

    # Static function-style getter (no self parameter)
    @static
    property get is_debug_enabled() -> bool:
        return AppConfig._debug_mode

    # Static function-style setter (no self parameter)
    @static
    property set debug_mode(value: bool):
        AppConfig._debug_mode = value

    @static
    property get debug_mode() -> bool:
        return AppConfig._debug_mode

# Usage
print(AppConfig.version)           # "1.0.0"
AppConfig.debug_mode = True
print(AppConfig.is_debug_enabled)  # True
```

**Static Property Rules:**
- Auto: Use `@static` decorator with `property [get|set|init] name: T`
- Function-style: Use `@static` decorator with `property get name() -> T:` or `property set name(value: T):` (no `self`)
- Access the class by name within the body

*Implementation: ✅ Native*
```csharp
public static string Version { get; set; } = "1.0.0";
public static int BuildNumber { get; } = 42;
public static bool IsDebugEnabled => _debugMode;
public static bool DebugMode {
    get => _debugMode;
    set => _debugMode = value;
}
```

### Virtual, Abstract, and Override Properties

Properties participate in inheritance using the standard decorators:

```python
class Shape:
    # Abstract property (must be overridden)
    @abstract
    property get area(self) -> double:
        ...

    # Virtual property (can be overridden)
    @virtual
    property get name(self) -> str:
        return "Shape"

class Circle(Shape):
    property get radius: double

    def __init__(self, radius: double):
        self.radius = radius

    # Override abstract property
    @override
    property get area(self) -> double:
        return 3.14159 * self.radius ** 2

    # Override virtual property
    @override
    property get name(self) -> str:
        return "Circle"

@final
class UnitCircle(Circle):
    def __init__(self):
        super().__init__(1.0)

    # Sealed override - cannot be overridden in further subclasses
    @final
    @override
    property get name(self) -> str:
        return "Unit Circle"
```

**Inheritance Rules:**
- `@abstract` properties must use `...` as the body and must be overridden
- `@virtual` properties can optionally be overridden by subclasses
- `@override` is required when overriding a base class property
- `@final` prevents further overriding in subclasses
- A subclass can override any accessor it has visibility to
- The overriding accessor's visibility cannot be more restrictive than the base

**Covariant Return Types:**

C# 9.0 supports covariant return types for method overrides. Since properties are essentially methods, property return types can be covariant on override:

```python
class Animal:
    @virtual
    property get friend(self) -> Animal:
        return self._friend

class Dog(Animal):
    @override
    property get friend(self) -> Dog:  # Valid - Dog is subtype of Animal
        return self._dog_friend

class Cat(Animal):
    @override
    property get friend(self) -> Cat:  # Valid - Cat is subtype of Animal
        return self._cat_friend
```

This allows subclasses to return more specific types without requiring unsafe casts at call sites.

*Implementation: ✅ Native*
```csharp
public abstract double Area { get; }
public virtual string Name => "Shape";

public override double Area => 3.14159 * Radius * Radius;
public override string Name => "Circle";

public sealed override string Name => "Unit Circle";
```

### Interface Properties

Interfaces declare property requirements using the same syntax:

```python
interface IIdentifiable:
    # Read-only property requirement (getter only)
    property get id: int

interface INamed:
    # Read-write property requirement (getter + setter)
    property name: str

interface ITimestamped:
    # Function-style requirement (read-only computed)
    property get created_at(self) -> datetime: ...
    property get updated_at(self) -> datetime: ...

class Entity(IIdentifiable, INamed, ITimestamped):
    property get id: int
    property name: str = "Unnamed"
    _created: datetime
    _updated: datetime

    def __init__(self, id: int):
        self.id = id
        self._created = datetime.now()
        self._updated = self._created

    property get created_at(self) -> datetime:
        return self._created

    property get updated_at(self) -> datetime:
        return self._updated
```

**Interface Property Requirements:**

| Interface Declares | Implementer Must Provide |
|--------------------|--------------------------|
| `property get x: T` | At least a getter |
| `property set x: T` | At least a setter |
| `property x: T` | Both getter and setter |
| `property get x(self) -> T: ...` | A getter (auto or function-style) |
| `property set x(self, value: T): ...` | A setter (auto or function-style) |

**Auto-Properties in Interfaces:**

For interface auto-properties, no body means abstract (must be implemented). A default value makes it optional:

```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter

interface IConfigurable:
    property name: str = ""    # Default value - implementer can override or use default
    property enabled: bool = True
```

This matches C# interface property semantics where properties without a body are abstract requirements.

**Explicit Interface Implementation:**

When a class needs to provide different behavior when accessed through an interface versus directly:

```python
interface ISecret:
    property get value: str

class SecretHolder(ISecret):
    _secret: str

    def __init__(self, secret: str):
        self._secret = secret

    # Regular property (always accessible)
    property get hint(self) -> str:
        return self._secret[0] + "***"

    # Explicit interface implementation
    # Only accessible when referenced through the interface type
    property get ISecret.value(self) -> str:
        return self._secret

# Usage
holder = SecretHolder("password123")
print(holder.hint)        # "p***"
print(holder.value)       # ERROR: 'value' not accessible on SecretHolder

secret: ISecret = holder
print(secret.value)       # "password123" - accessible via interface
```

*Implementation: ✅ Native*
```csharp
public string Hint => _secret[0] + "***";
string ISecret.Value => _secret;
```

### Property and Method Name Conflicts

A property and a method cannot share the same name within a class:

```python
class Example:
    _value: int = 0

    # ✅ OK - property
    property get value(self) -> int:
        return self._value

    # ❌ ERROR - method cannot have same name as property
    def value(self) -> int:
        return self._value
```

**Compiler Error:**

```
error: 'value' is already defined as a property in this class
  --> example.spy:10:5
   |
10 |     def value(self) -> int:
   |         ^^^^^ method name conflicts with property on line 6
```

### Property Syntax Summary

**Auto-properties (compiler-generated backing field):**

| Syntax | Accessors | C# Equivalent |
|--------|-----------|---------------|
| `property name: T` | get + set | `T Name { get; set; }` |
| `property name: T = val` | get + set | `T Name { get; set; } = val` |
| `property get name: T` | get | `T Name { get; }` |
| `property get name: T = val` | get | `T Name { get; } = val` |
| `property set name: T` | set | `T Name { set; }` |
| `property init name: T` | get + init | `T Name { get; init; }` |
| `property init name: T = val` | get + init | `T Name { get; init; } = val` |

**Function-style properties (user-provided backing field or computed):**

| Syntax | C# Equivalent |
|--------|---------------|
| `property get name(self) -> T:` | `T Name { get { ... } }` |
| `property set name(self, value: T):` | `T Name { set { ... } }` |
| `@static property get name() -> T:` | `static T Name { get { ... } }` |
| `@static property set name(value: T):` | `static T Name { set { ... } }` |
| `property get IFace.name(self) -> T:` | `T IFace.Name { get { ... } }` |

**Valid accessor combinations:**

| Accessors | Result | Readable | Writable in `__init__` | Writable after |
|-----------|--------|----------|------------------------|----------------|
| get | Read-only | ✅ | ✅ (auto) / ❌ (func) | ❌ |
| set | Write-only | ❌ | ✅ | ✅ |
| get + set | Read-write | ✅ | ✅ | ✅ |
| init | Init-only (auto only) | ✅ | ✅ | ❌ |
| get + init | Read + init (auto only) | ✅ | ✅ | ❌ |

**Decorator placement:**

```python
@static
@virtual
property get name(self) -> str:
    return "value"

@override
property get name(self) -> str:
    return self._name

@private
property set name(self, value: str):
    self._name = value
```

---

## Context Managers **[v0.2.0]**

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

**Protocol:**
- Object passed to `with` should implement either `IContextManager` or `IDisposable`
  - For `IContextManager`:
    - `__enter__()` called on entry (returns object for `as` binding)
    - `__exit__()` called on exit
      - If the object returned in the `as` binding implements `IDisposable`, then its `Dispose()` method is also invoked (before `__exit__()`)
  - For `IDisposable`:
    - `Dispose()` called on exit
- If an object implements both, then `__exit__()` is called before `Dispose()`

*Implementation:*
- For `IContextManager`, ✅ Lowered - `try { var asBinding = contextManager; } catch(Exception e) { ... } finally { contextManager.__Exit__(...); }`
- For `IDisposable`, ✅ Native - `using (var r = resource) { ... }`

---

## Events **[v0.2.0]**

Events provide a publish-subscribe pattern:

```python
class Button:
    # Event declaration
    event clicked: (object, EventArgs) -> None

    def click(self):
        if self.clicked is not None:
            self.clicked(self, EventArgs())

# Subscription
button = Button()

def on_clicked(sender: object, args: EventArgs):
    print("Button clicked!")

button.clicked += on_clicked  # Subscribe
button.click()                 # Triggers event
button.clicked -= on_clicked  # Unsubscribe
```

**Thread-Safe Event Invocation:**

For thread-safe event invocation that avoids race conditions, use the null-conditional call pattern:

```python
class Button:
    event clicked: (object, EventArgs) -> None

    def click(self):
        # Thread-safe pattern using ?.
        self.clicked?.invoke(self, EventArgs())
```

This maps to C#'s `clicked?.Invoke(...)` pattern, which atomically checks for null and invokes, preventing race conditions where a subscriber unsubscribes between the null check and invocation.

```python
# These are equivalent:

# Explicit null check (not thread-safe)
if self.clicked is not None:
    self.clicked(self, EventArgs())  # Race condition possible here

# Null-conditional invoke (thread-safe)
self.clicked?.invoke(self, EventArgs())  # Atomic check-and-invoke
```

### Custom EventArgs

```python
class ValueChangedArgs(EventArgs):
    old_value: int
    new_value: int

    def __init__(self, old_value: int, new_value: int):
        self.old_value = old_value
        self.new_value = new_value

class Counter:
    event value_changed: (object, ValueChangedArgs) -> None
    _value: int = 0

    property get value(self) -> int:
        return self._value

    property set value(self, new_value: int):
        old = self._value
        self._value = new_value
        if self.value_changed is not None:
            self.value_changed(self, ValueChangedArgs(old, new_value))
```

**Event Rules:**
- Events can only be invoked from the declaring class
- `+=` subscribes, `-=` unsubscribes
- Multiple subscribers are called in subscription order

*Implementation: ✅ Native - `event EventHandler Name;`*

---

## Async Programming **[v0.2.0+]**

### Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ✅ Native - `async` method returning `Task<T>`.*

### Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    tasks = [fetch_data(url) for url in urls]
    results = await asyncio.gather(*tasks)
    return results
```

*Implementation: ✅ Native - `Task.WhenAll()`*

### Async Iteration

```python
async def count_up(n: int) -> AsyncIterator[int]:
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

**No Async Comprehensions:**

Sharpy does not support async comprehensions (`async for` inside comprehensions). C# 9.0's LINQ doesn't natively support `IAsyncEnumerable` in query syntax, making this feature complex to implement.

```python
# ❌ Not supported - async comprehension
results = [x async for x in async_iterator()]
results = [x async for x in async_iterator() if await predicate(x)]

# ✅ Use explicit async loop instead
results: list[T] = []
async for x in async_iterator():
    results.append(x)

# ✅ Or with condition
results: list[T] = []
async for x in async_iterator():
    if await predicate(x):
        results.append(x)
```

Async comprehensions may be added in a future version (v2.0+) when better runtime support is available.

**Generator Return Types:**

Functions using `yield` have special return type annotations:

| Pattern | Return Type | Notes |
|---------|-------------|-------|
| `yield` in function | `Iterator[T]` | Synchronous generator |
| `yield` in `async def` | `AsyncIterator[T]` | Asynchronous generator |
| `yield from` | Same as yielded iterator | Delegation |

```python
# Synchronous generator
def fibonacci(n: int) -> Iterator[int]:
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

# Async generator
async def stream_data(url: str) -> AsyncIterator[bytes]:
    async with http_client.stream(url) as response:
        async for chunk in response:
            yield chunk
```

*Implementation: ✅ Native - `IAsyncEnumerable<T>` (C# 8+)*

### Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

*Implementation: 🔄 Lowered - `await using (var r = resource) { ... }`*

---

## Built-in Functions **[v0.1.0+]**

Built-in functions provide polymorphic access to type behavior. They work uniformly on all types—primitives, .NET types, and Sharpy-defined types—by internally dispatching to the appropriate implementation:

- **For Sharpy types**: If the type defines the corresponding dunder method, the built-in function calls it
- **For primitives and .NET types**: The built-in function uses the native .NET operation
- **Fallback behavior**: Some functions provide sensible defaults when no custom implementation exists

This design allows code like `len(x)`, `str(x)`, and `repr(x)` to work consistently regardless of whether `x` is a list, a string, or a custom class.

### Type Conversion [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `int(x)` | Convert to integer | `(int)x` or `Convert.ToInt32(x)` |
| `double(x)` | Convert to double | `(double)x` |
| `str(x)` | Convert to string | Calls `__str__` if defined, else `.ToString()` |
| `bool(x)` | Convert to boolean | Truthiness check |

**`str(x)`** returns a human-readable string representation:
- For Sharpy types with `__str__`: calls `__str__`
- For all types: falls back to `.ToString()`

### Type Checking [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `isinstance(x, T)` | Check if `x` is an instance of type `T` | `x is T` |
| `type(x)` | Get runtime type of `x` | `x.GetType()` |

**`type(x)` Return Type:**

The `type()` function returns `System.Type`, the .NET reflection type:

```python
from system import Type

x = 42
t: Type = type(x)        # Returns System.Int32 type
print(t.name)            # "Int32"
print(t.full_name)       # "System.Int32"

# Type comparison
if type(x) == type(0):
    print("x is an integer")

# Prefer isinstance() for type checks
if isinstance(x, int):   # More idiomatic
    print("x is an integer")
```

**Note:** Unlike Python where `type(None)` returns `NoneType`, Sharpy's `type(None)` is a compile-time error because `None` is not a value with a type.

**`type()` on Primitive Literals:**

Unlike `type(None)`, calling `type()` on primitive literals is valid and returns the corresponding `System.Type`:

```python
# All of these are valid
t1 = type(42)        # System.Int32
t2 = type(3.14)      # System.Double
t3 = type("hello")   # System.String
t4 = type(True)      # System.Boolean
t5 = type([1, 2, 3]) # Sharpy.Core.List`1[System.Int32]

# Only type(None) is an error
t6 = type(None)      # ERROR: type(None) is not valid
```

This is because primitive literals are values with concrete runtime types, whereas `None` represents the absence of a value.

**`isinstance(x, T)`**

Checks whether `x` is an instance of type `T` at runtime. Returns `True` if `x` is an instance of `T` or any subclass of `T`.

```python
value: object = get_value()

if isinstance(value, str):
    # value is narrowed to str in this block
    print(value.upper())

if isinstance(value, MyClass):
    # value is narrowed to MyClass
    value.my_method()

# Works with interfaces too
if isinstance(value, IDrawable):
    # value is narrowed to IDrawable
    value.draw()
```

**Single Type Only:**

Unlike Python's `isinstance()` which accepts a tuple of types, Sharpy's `isinstance()` only accepts a single type argument. Sharpy does not have union types.

```python
# ✅ Valid - single type
if isinstance(x, int):
    pass

if isinstance(x, IDrawable):
    pass

# ❌ Invalid - multiple types not supported
if isinstance(x, (int, str)):      # ERROR: isinstance() takes exactly one type argument
    pass

if isinstance(x, int | str):       # ERROR: union types not supported
    pass
```

**To check multiple types**, use explicit `or`:

```python
if isinstance(x, int) or isinstance(x, str):
    # x could be int or str here
    # Note: no automatic type narrowing in this case
    pass
```

**Generic Type Limitation:**

Due to .NET type erasure for generics at runtime, `isinstance()` cannot check generic type arguments:

```python
# ✅ Valid - checks if x is any List<T>
if isinstance(x, list):
    pass  # x could be list[int], list[str], etc.

# ❌ Compile error - cannot check generic type arguments at runtime
if isinstance(x, list[int]):       # ERROR: Cannot check generic type arguments at runtime
    pass

if isinstance(x, dict[str, int]):  # ERROR: Cannot check generic type arguments at runtime
    pass
```

This limitation matches C#'s `is` operator behavior. At runtime, `List<int>` and `List<str>` are both just `List<T>`—the generic type argument is erased.

**Type Narrowing:**

When `isinstance()` is used in a conditional, the variable's type is narrowed within that branch:

```python
def process(value: object) -> str:
    if isinstance(value, str):
        return value.upper()      # OK: value is str
    if isinstance(value, int):
        return str(value * 2)     # OK: value is int
    return "unknown"
```

*Implementation: ✅ Native - Maps to C# `is` pattern matching with type narrowing.*

### Collection Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `len(x)` | Get length | Calls `__len__` if defined, else `.Count` or `.Length` |
| `min(iter)` | Minimum value | `.Min()` or `Math.Min()` |
| `max(iter)` | Maximum value | `.Max()` or `Math.Max()` |
| `sum(iter)` | Sum values | `.Sum()` |
| `sorted(iter)` | Sort collection | `.OrderBy()` |
| `reversed(iter)` | Reverse | `.Reverse()` |
| `enumerate(iter)` | Index + value | `.Select((x, i) => (i, x))` |

**`enumerate()` Signature:**

The `enumerate()` function matches Python's signature:

```python
enumerate(iterable, start=0)
```

| Form | Description |
|------|-------------|
| `enumerate(items)` | Indices start at 0 |
| `enumerate(items, start=1)` | Indices start at 1 |
| `enumerate(items, start=n)` | Indices start at n |

```python
names = ["Alice", "Bob", "Charlie"]

# Default: start at 0
for i, name in enumerate(names):
    print(f"{i}: {name}")  # 0: Alice, 1: Bob, 2: Charlie

# Start at 1 (useful for 1-based numbering)
for i, name in enumerate(names, start=1):
    print(f"{i}. {name}")  # 1. Alice, 2. Bob, 3. Charlie
```

*Implementation: 🔄 Lowered - `.Select((x, i) => (i + start, x))`.*

| `zip(a, b)` | Combine iterables | `.Zip()` |
| `range(n)` | Number sequence | `Enumerable.Range()` |

**`range()` Signature:**

The `range()` function matches Python's signature exactly:

| Form | Description | Example |
|------|-------------|---------|
| `range(stop)` | 0 to stop-1 | `range(5)` → 0, 1, 2, 3, 4 |
| `range(start, stop)` | start to stop-1 | `range(2, 5)` → 2, 3, 4 |
| `range(start, stop, step)` | start to stop-1, by step | `range(0, 10, 2)` → 0, 2, 4, 6, 8 |

```python
# Single argument: 0 to n-1
for i in range(5):
    print(i)  # 0, 1, 2, 3, 4

# Two arguments: start to stop-1
for i in range(2, 7):
    print(i)  # 2, 3, 4, 5, 6

# Three arguments: start to stop-1, stepping by step
for i in range(0, 10, 2):
    print(i)  # 0, 2, 4, 6, 8

# Negative step for countdown
for i in range(10, 0, -1):
    print(i)  # 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
```

*Implementation: 🔄 Lowered - Simple forms use `for (int i = start; i < stop; i += step)`, complex forms use `Enumerable.Range()` or generator.*

| `filter(pred, iter)` | Filter | `.Where()` |
| `map(func, iter)` | Transform | `.Select()` |
| `all(iter)` | All truthy | `.All()` |
| `any(iter)` | Any truthy | `.Any()` |

**`len(x)`** returns the number of items in a container:
- For Sharpy types with `__len__`: calls `__len__`
- For collections: uses `.Count` property
- For strings/arrays: uses `.Length` property

### I/O Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `print(x)` | Print to console | `Console.WriteLine()` |
| `input(prompt)` | Read from console | `Console.ReadLine()` |

### Mathematical Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `abs(x)` | Absolute value | `Math.Abs()` |
| `pow(x, y)` | Power | `Math.Pow()` |
| `round(x, n)` | Round | `Math.Round()` |
| `divmod(a, b)` | Quotient + remainder | `(a / b, a % b)` |

**`divmod()` Return Types:**

The `divmod()` function returns a tuple containing the quotient and remainder. The return type depends on the operand types, following the same numeric promotion rules as `/` and `//`:

| Operand Types | Return Type | Notes |
|---------------|-------------|-------|
| Both `int` | `tuple[int, int]` | Integer division and modulo |
| Any `long` | `tuple[long, long]` | Promoted to long |
| Any `float`/`double` | `tuple[double, double]` | Float division |
| Any `decimal` | `tuple[decimal, decimal]` | Decimal division |

```python
divmod(17, 5)       # (3, 2) - tuple[int, int]
divmod(17L, 5)      # (3L, 2L) - tuple[long, long]
divmod(17.0, 5.0)   # (3.0, 2.0) - tuple[double, double]
divmod(17.0m, 5.0m) # (3.0m, 2.0m) - tuple[decimal, decimal]
```

### Object Functions [v0.1.0]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `repr(x)` | Debug representation | Calls `__repr__` if defined, else `__str__`, else `.ToString()` |
| `hash(x)` | Hash code | Calls `__hash__` if defined, else `.GetHashCode()` |
| `id(x)` | Object identity | `RuntimeHelpers.GetHashCode()` |

**`repr(x)`** returns a string representation suitable for debugging:
- For Sharpy types with `__repr__`: calls `__repr__`
- Fallback: tries `__str__`, then `.ToString()`
- Typically includes type name and distinguishing attributes

**`hash(x)`** returns the hash code for use in dictionaries and sets:
- For Sharpy types with `__hash__`: calls `__hash__`
- For all types: falls back to `.GetHashCode()`
- If `__eq__` is defined, `__hash__` must also be defined (and vice versa)

**Hashing Tuples:**

Tuples are automatically hashable if all their elements are hashable:

```python
# Tuples of hashable types can be hashed
point = (10, 20)
h = hash(point)          # OK: both int elements are hashable

# Use tuples to create composite hash keys
coord_to_name: dict[tuple[int, int], str] = {}
coord_to_name[(0, 0)] = "origin"
coord_to_name[(10, 20)] = "point A"

# Nested tuples work if all elements hashable
nested = ((1, 2), (3, 4))
h = hash(nested)         # OK

# Tuples containing unhashable types cannot be hashed
bad = ([1, 2], [3, 4])   # Tuple containing lists
h = hash(bad)            # ERROR: list is not hashable
```

*Implementation: 🔄 Lowered - Generated as method calls or type-appropriate dispatch.*

---

## .NET Interop **[v0.1.0]**

### Importing .NET Types

```python
from system.collections.generic import List, Dictionary
from system.io import File, Path

# Use .NET types directly
items = List[int]()
items.add(42)

content = File.read_all_text("data.txt")
```

### .NET Properties

.NET properties accessed like Sharpy properties:

```python
from system.io import FileInfo

file = FileInfo("data.txt")
size = file.length
name = file.name
```

### Extension Methods

.NET extension methods work naturally:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
```

### IDisposable Pattern

.NET's `IDisposable` integrates with `with`:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
```

---

## Naming Conventions Summary **[v0.1.0]**

| Identifier Type | Sharpy Convention | Compiled C# Form |
|-----------------|-------------------|------------------|
| Module | `snake_case` | `PascalCase` namespace |
| Class | `PascalCase` | (unchanged) |
| Struct | `PascalCase` | (unchanged) |
| Interface | `IPascalCase` | (unchanged) |
| Method/Function | `snake_case` | `PascalCase` |
| Parameter | `snake_case` | `camelCase` |
| Local variable | `snake_case` | (unchanged) |
| Constant | `CAPS_SNAKE_CASE` | (unchanged) |
| Enum type | `PascalCase` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `PascalCase` |

---

## Program Entry Point **[v0.1.0]**

The entry point is either a file with top-level statements or a `main()` function:

```python
# Option 1: Top-level statements
print("Hello, World!")

# Option 2: main() function
def main():
    print("Hello, World!")
```

**Note:** The Python idiom `if __name__ == "__main__":` does not exist in Sharpy.

*Implementation: 🔄 Lowered*
- *Top-level statements wrapped in generated `Main()` method*
- *Module code wrapped in `public static class Exports`*

---

## Features Deferred to v2.0+

The following features require .NET 7+ runtime or C# 11+ and cannot be supported when targeting Unity or .NET 5/6:

| Feature | Required C# | Required .NET | Reason |
|---------|-------------|---------------|--------|
| `@file` access modifier | C# 11 | .NET 6+ | File-scoped types |
| List patterns `case [a, b]:` | C# 11 | Any | Compiler feature |
| Static abstract interface members | C# 11 | .NET 7 | Runtime support |
| Generic math constraints | C# 11 | .NET 7 | BCL interfaces |
| `required` members | C# 11 | .NET 7 | Attribute + compiler |
| Record structs | C# 10 | Any | Compiler feature |
| `field` keyword in properties | C# 13 | Any | Compiler feature |
| Extension properties/operators | C# 14 | Any | Compiler feature |
| User-defined `+=` operators | C# 14 | Any | Compiler feature |

---

## Version Summary

| Version | Key Additions |
|---------|---------------|
| **v0.1.0** | Core syntax, primitives, functions, classes, exceptions, imports, type hierarchy (`object` base), dunder invocation rules |
| **v0.1.1** | Nullable types (`T?`), `?.`, `??`, collections, slicing |
| **v0.1.2** | Structs, interfaces, inheritance, decorators, access modifiers, function overloading, properties |
| **v0.1.3** | Generics, type constraints, lambdas |
| **v0.1.4** | Enums, operator overloading via dunders |
| **v0.1.5** | F-strings, extended literals, comparison chaining, loop else |
| **v0.1.6** | Pattern matching (`match`/`case`), guards, all pattern types |
| **v0.1.7** | Type aliases, variable shadowing |
| **v0.1.8** | Comprehensions, walrus operator |
| **v0.2.0+** | Context managers (`with`), async/await, generators (`yield`), tagged unions (ADTs), `maybe`/`try` expressions, events, `del` statement |
| **v1.0** | Stable release |
| **v2.0+** | Features requiring C# 11+ / .NET 7+ |

---

## See Also

- **Type System** - Detailed type semantics, interfaces, and generics
- **Compiler Design** - Implementation details and code generation
- **C# 9.0 Compatibility Matrix** - Full transpilation reference

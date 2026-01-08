# Partial Application

Partial application creates new functions by fixing some arguments of an existing function using the underscore `_` placeholder.

## Syntax

```python
# Using _ as placeholder for arguments to be supplied later
partially_applied = function(arg1, _, arg3)
```

The `_` represents an argument that will be provided when the partially applied function is called.

## Disambiguation: `_` Placeholder vs Pattern Wildcard

The underscore `_` serves two different purposes in Sharpy:

1. **Partial application placeholder** - in function call argument positions
2. **Pattern matching wildcard** - in `case` pattern positions

**Disambiguation rule:** The parser determines `_` meaning based on syntactic context:

| Context | `_` Meaning | Example |
|---------|-------------|---------|
| Function call argument | Partial application placeholder | `f(_, x)` creates `(a) => f(a, x)` |
| Inside parenthesized operator expression | Operator section placeholder | `(_ * 2)` creates `(x) => x * 2` |
| `case` pattern position | Wildcard pattern | `case _:` matches anything |
| Type pattern argument | Wildcard pattern | `case Point(_, y):` matches any x |
| Assignment target | Regular identifier | `_ = compute()` (discards result) |

**Key disambiguation scenarios:**

```python
# Partial application - _ in call arguments
f(_)                    # Partial: creates lambda taking 1 arg
f(_, x)                 # Partial: creates lambda taking 1 arg
obj.method(_, y)        # Partial: creates lambda taking 1 arg

# Pattern matching - _ in case patterns
match value:
    case _:             # Wildcard: matches anything
        pass
    case Point(_, y):   # Wildcard: ignores first component
        pass
    case (_, _, z):     # Wildcards: ignores first two tuple elements
        pass

# Nested function calls in patterns - NOT partial application
match value:
    case Foo(f(_)):     # The f(_) is a positional pattern, _ is wildcard
        pass            # NOT: partial application of f

# Explicit partial in pattern context requires assignment
match value:
    case x if (partial := g(_, x)):  # Walrus creates partial, then tests
        use(partial)
```

**Rule summary:**
- Inside `case` clause patterns: `_` is **always** a wildcard
- In function call arguments outside patterns: `_` is **always** partial application placeholder
- When ambiguous, the pattern context wins

## Basic Usage

```python
# Define a function
def add(x: int, y: int) -> int:
    return x + y

# Partially apply - fix first argument
add_five = add(5, _)

# Call with remaining argument
result = add_five(3)  # 8 (equivalent to add(5, 3))
```

## Multiple Placeholders

```python
def format_name(first: str, middle: str, last: str) -> str:
    return f"{first} {middle} {last}"

# Fix only the last name
format_smith = format_name(_, _, "Smith")

# Provide the remaining arguments
name = format_smith("John", "Paul")  # "John Paul Smith"

# Fix first and last, middle as placeholder
format_j_smith = format_name("John", _, "Smith")
name = format_j_smith("Q")  # "John Q Smith"
```

## Operator Sections

Haskell-style operator sections using `_` for partial application of operators:

```python
# Unary operator sections
doubler = (_ * 2)        # Multiply by 2
is_positive = (_ > 0)   # Check if positive
negate = (-_)           # Negate number

# Usage
[1, 2, 3].map(float)           # [2, 4, 6]
[-1, 0, 1].filter(is_positive)  # [1]
[5, -3, 8].map(negate)          # [-5, 3, -8]

# Binary operator sections
add_ten = (_ + 10)      # Add 10 to argument
half = (_ / 2)          # Divide by 2
starts_with_a = (_ startswith "a")
```

## With Lambda Syntax

Partial application is syntactic sugar for lambda expressions:

```python
# These are equivalent:
add_five = add(5, _)
add_five = lambda x: add(5, x)

# Multiple placeholders
format_smith = format_name(_, _, "Smith")
format_smith = lambda a, b: format_name(a, b, "Smith")
```

## Type Inference

The type of a partially applied function is inferred from the original function:

```python
def compute(x: int, y: float, z: str) -> str:
    return f"{x}, {y}, {z}"

# Partial application infers parameter and return types
partial: (float, str) -> str = compute(42, _, _)
result = partial(3.14, "test")  # "42, 3.14, test"
```

## Argument Order

Placeholders preserve the original argument order:

```python
def divide(numerator: float, denominator: float) -> float:
    return numerator / denominator

# Fix denominator (second argument)
halve = divide(_, 2.0)
halve(10.0)  # 5.0 (10.0 / 2.0)

# Fix numerator (first argument)
ten_divided_by = divide(10.0, _)
ten_divided_by(2.0)  # 5.0 (10.0 / 2.0)
```

## With Collections

Partial application works well with collection methods:

```python
# Create specialized mappers
items = [1, 2, 3, 4, 5]

# Partial application for map
items.map((_ * 2))           # [2, 4, 6, 8, 10]
items.map(str.format("{}", _))  # ["1", "2", "3", "4", "5"]

# Partial application for filter
items.filter((_ > 2))        # [3, 4, 5]
items.filter((_ % 2 == 0))   # [2, 4]
```

## With Methods

Partial application works with both static and instance methods:

```python
# Instance method
names = ["alice", "bob", "charlie"]
uppercase = names.map(str.upper(_))  # ["ALICE", "BOB", "CHARLIE"]

# Static method
values = ["42", "100", "256"]
numbers = values.map(int.parse(_, _))
```

## Currying Pattern

Partial application enables currying-style programming:

```python
def multiply(x: int, y: int, z: int) -> int:
    return x * y * z

# Progressively fix arguments
times_two = multiply(2, _, _)
times_two_three = times_two(3, _)
result = times_two_three(4)  # 24 (2 * 3 * 4)
```

## Common Patterns

**Configuration Functions:**
```python
def fetch_url(url: str, timeout: int, retry: bool) -> str:
    # ... implementation

# Create configured versions
quick_fetch = fetch_url(_, 5, False)
robust_fetch = fetch_url(_, 30, True)

quick_fetch("https://api.example.com")
```

**Validation Functions:**
```python
def validate_range(value: int, min: int, max: int) -> bool:
    return min <= value <= max

# Create specialized validators
validate_age = validate_range(_, 0, 120)
validate_percentage = validate_range(_, 0, 100)

validate_age(25)         # True
validate_percentage(150) # False
```

**Functional Pipelines:**
```python
# Combine partial application with pipe operator
result = data
    |> parse(_)
    |> filter((_ > 0), _)
    |> map((_ * 2), _)
    |> sorted(_)
```

## Limitations

- Cannot use `_` for keyword arguments (positional only)
- Cannot mix `_` with `*args` or `**kwargs`
- Placeholders must be in valid argument positions

```python
# ❌ Cannot use with keyword arguments
func(x=_, y=5)  # ERROR

# ❌ Cannot use with *args
func(_, *rest)  # ERROR

# ✅ Only positional arguments
func(_, 5, _)   # OK
```

## C# Mapping

Partial application is lowered to lambda expressions:

```python
# Sharpy
add_five = add(5, _)
result = add_five(3)
```
```csharp
// C# 9.0
Func<int, int> addFive = (x) => add(5, x);
var result = addFive(3);
```

**Operator Sections:**
```python
# Sharpy
doubler = (_ * 2)
is_positive = (_ > 0)
```
```csharp
// C# 9.0
Func<int, int> doubler = (x) => x * 2;
Func<int, bool> isPositive = (x) => x > 0;
```

## Performance Considerations

Partial application creates a closure (lambda), which has a small runtime cost:

```python
# Each call creates a new closure
for i in range(1000):
    func = operation(i, _)  # Creates 1000 closures

# Better: create once if possible
func = operation(value, _)
for i in range(1000):
    result = func(i)
```

## Type Annotations

Explicitly annotate partially applied functions for clarity:

```python
def compute(a: int, b: float, c: str) -> str:
    return f"{a}, {b}, {c}"

# Explicit type annotation
partial: (float, str) -> str = compute(42, _, _)

# Type is inferred if omitted
partial = compute(42, _, _)  # Same as above
```

*Implementation*
- *🔄 Lowered - Transformed to lambda expression at compile time:*
  - Simple function calls: `f(a, _, c)` → `(b) => f(a, b, c)`
  - Operator sections: `(_ * 2)` → `(x) => x * 2`
  - Multiple placeholders maintain order: `f(_, x, _)` → `(a, c) => f(a, x, c)`

# Type Casting (The `to` Operator)

The `to` operator performs type casting, converting a value from one type to another at runtime.

```python
result = expression to TargetType
```

## Two Forms

| Syntax | Behavior on Failure | Result Type |
|--------|---------------------|-------------|
| `value to T` | Throws `InvalidCastException` | `T` |
| `value to T?` | Returns `None` | `T?` |

## Examples

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

## Using Result[T, E] or Optional[T]

Casting can be chained with `try` and `maybe` expressions to wrap
the cast behavior in safe tagged unions:

```python
my_dog: object = Dog()
some_result = try my_dog as Cat  # some_result = Result[Cat, InvalidCastException].Err
some_result = try my_dog as Cat?  # some_result = Result[Cat?, Exception].Ok(None). Compiler will warn user to use a `maybe` expression instead

some_optional = maybe my_dog as Cat  # Throws. Compiler will warn user to use a `try` expression instead
some_optional = maybe my_dog as Cat?  # some_optional = Optional[str].Nothing
```

## Safe Casting Pattern

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

## Upcasting

Upcasts (derived → base) are always safe and can be implicit through assignment:

```python
dog: Dog = Dog("Buddy")

# Explicit upcast (allowed but unnecessary)
animal = dog to Animal

# Implicit upcast (preferred)
animal: Animal = dog
```

The compiler may emit a warning when `to` is used for compile-time-safe upcasts, since they're implicit anyway.

## Numeric Conversions

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

## Relationship to Conversion Functions

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

## Operator Precedence

The `to` operator binds looser than member access, function calls, and arithmetic operators, but tighter than comparison and logical operators:

| Precedence | Operators |
|------------|-----------|
| (higher) | `()`, `[]`, `.`, `?.` |
| | `**` |
| | `+x`, `-x`, `~x` |
| | `*`, `/`, `//`, `%` |
| | `+`, `-` |
| | `<<`, `>>`, `&`, `^`, `\|` |
| | `to` |
| | `in`, `is`, `<`, `>`, `==`, etc. |
| | `not`, `and`, `or`, `??` |
| | `try`, `maybe` |
| (lower) | `x if c else y`, `lambda` |

This means:

```python
# Parentheses needed for member access on cast result
name = (animal to Dog).name
result = (obj to IProcessor).process(data)

# Arithmetic binds tighter than `to`
x = value + 1 to long          # Parsed as: (value + 1) to long

# No parentheses needed for comparisons
if animal to Dog? is not None:
    pass

# Chained with None check
if (dog := animal to Dog?) is not None and dog.age > 5:
    pass

# `try` and `maybe` capture the entire cast expression
result = try animal to Dog     # Parsed as: try (animal to Dog)
opt = maybe obj to Widget?     # Parsed as: maybe (obj to Widget?)
```

## Invalid Casts

The compiler rejects casts that are statically known to be impossible:

```python
x: int = 42
s = x to str                     # ERROR: int cannot be cast to str (use str(x))

dog: Dog = Dog("Buddy")
cat = dog to Cat                 # ERROR: Dog cannot be cast to Cat (no inheritance relationship)
```

## Casting `None`

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

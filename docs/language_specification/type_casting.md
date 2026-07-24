# Type Casting (the `as!` / `as?` Operators)

The `as!` and `as?` operators perform type casting, converting a value from one type to another at
runtime. The failure mode lives on the **operator**: `as!` throws on a failed cast, `as?` yields
`None`. Because the operator owns the failure mode, the target type is always written **non-nullable**.

```python
result = expression as! TargetType   # throwing
result = expression as? TargetType   # safe (yields TargetType?)
```

> The legacy `to` operator (`value to T` / `value to T?`) was **removed in 0.8.0**
> ([#1127](https://github.com/antonsynd/sharpy/issues/1127)); `to` is now an ordinary identifier.
> Migrate `x to T` → `x as! T` and `x to T?` → `x as? T` (the lowering is identical).

## Two Forms

| Syntax | Behavior on Failure | Result Type |
|--------|---------------------|-------------|
| `value as! T` | Throws `InvalidCastException` | `T` |
| `value as? T` | Returns `None` | `T?` |

The `as?`/`as!` forms are **lexically distinct** from bare `as` — the `?`/`!` must be immediately
adjacent to `as` (no intervening whitespace), so `x as ? T` and `x as ! T` are not casts.

## Examples

```python
# Reference type downcasting
animal: Animal = get_animal()
dog = animal as! Dog             # Throws if not a Dog
dog = animal as? Dog             # None if not a Dog

# Interface casting
obj: object = get_object()
drawable = obj as! IDrawable     # Throws if doesn't implement IDrawable
drawable = obj as? IDrawable     # None if doesn't implement IDrawable

# Unboxing
boxed: object = 42
value = boxed as! int            # Throws if not an int
value = boxed as? int            # None if not an int

# Numeric conversions
big: int64 = 1_000_000
small = big as! int              # Throws on overflow
small = big as? int              # None on overflow

precise: float = 3.14159
rounded = precise as! int        # Truncates toward zero (3), throws if out of range
rounded = precise as? int        # None if out of range
```

## Using Result[T, E] or Optional[T]

Casting can be chained with `try` and `maybe` expressions to wrap
the cast behavior in safe tagged unions:

```python
my_dog: object = Dog()
some_result = try my_dog as! Cat     # Result[Cat, InvalidCastException] — try wraps the throwing cast

# `as?` already yields an Optional, so use it directly (no `maybe` needed):
some_optional = my_dog as? Cat       # Optional[Cat] — None if my_dog is not a Cat
```

Wrapping a safe `as?` cast in `maybe` is redundant and **rejected** (`SPY0243`): the cast already
yields an `Optional`. Reserve `maybe` for loose `T | None` values (e.g. a nullable returned from .NET
interop).

## Safe Casting Pattern

The `as?` form integrates naturally with type narrowing:

```python
animal: Animal = get_animal()

if (dog := animal as? Dog) is not None:
    # dog is narrowed to Dog in this block
    print(dog.bark())

# Or with simple None check
result = animal as? Dog
if result is not None:
    use_dog(result)
```

## Upcasting

Upcasts (derived → base) are always safe and can be implicit through assignment:

```python
dog: Dog = Dog("Buddy")

# Explicit upcast (allowed but unnecessary)
animal = dog as! Animal

# Implicit upcast (preferred)
animal: Animal = dog
```

The compiler may emit a warning when an explicit cast is used for a compile-time-safe upcast, since
it's implicit anyway.

## Numeric Conversions

The `as!`/`as?` operators handle numeric type conversions including narrowing conversions:

| Conversion | Behavior |
|------------|----------|
| Widening (e.g., `int32` → `int64`) | Always succeeds |
| Narrowing (e.g., `int64` → `int32`) | Throws (`as!`) / `None` (`as?`) on overflow |
| Float → Integer | Truncates toward zero, throws/None if out of range |
| Integer → Float | May lose precision (no failure) |

```python
# Widening - always safe
x: int = 42
y = x as! int64                  # Always succeeds

# Narrowing - may fail
big: int64 = 10_000_000_000
small = big as! int              # Throws: value too large for int
small = big as? int              # None: value too large for int

# Float to integer truncation
pi: float = 3.99
n = pi as! int                   # 3 (truncates toward zero)
neg: float = -3.99
m = neg as! int                  # -3 (truncates toward zero)

# Out of range
huge: float = 1e100
n = huge as? int                 # None: out of int range
```

### Edge-case semantics of the safe numeric form

For the safe form (`value as? T`) with a concrete numeric source, the boundary
cases are defined as follows:

| Case | Result |
|------|--------|
| In-range float → `int`/`long` | `Some(truncated)` — truncation is toward zero, so `3.9 as? int` is `Some(3)` and `-3.9 as? int` is `Some(-3)` (matching Python's `int(x)`) |
| Out-of-range float → `int`/`long` | `None` |
| `NaN` → `int`/`long` | `None` — `NaN` is not an integer, so a safe cast to any integral target yields `None` (the throwing form raises) |
| `±inf` → `int`/`long` | `None` |
| Integer → `float32`/`float` | Always `Some` — precision may be lost for large integers (as in Python's `float(big_int)`), but overflow is impossible because the floating range exceeds the integer range |
| `float` (64-bit) → `float32` | **Always `Some`** — a value outside `float32`'s finite range maps to `±inf`, and `NaN` is preserved; both are representable in `float32`, so IEEE semantics apply and there is no `None` case |

The narrowing checks compare the source value against the target's representable range *before*
truncating, so no cast ever overflows. (The exact boundary predicates live in
`Sharpy.NumericSafeCast`; note that `long.MaxValue` is not exactly representable as a `double`, so the
upper `long` guard is a strict `< 2^63` rather than `<= long.MaxValue`.)

## Relationship to Conversion Functions

The built-in conversion functions (`int()`, `str()`, `float()`, etc.) remain available and are
equivalent to the throwing form `as!` for their respective types:

```python
# These are equivalent
x = int(value)
x = value as! int

# These are equivalent
s = str(value)
s = value as! str

# But only the cast operators provide the safe nullable form
x = value as? int                # No equivalent with int()
```

The conversion functions are retained for Pythonic familiarity, but `as!`/`as?` are the
general-purpose casting mechanism that works with any type:

```python
# Only the cast operators work for arbitrary types
dog = animal as? Dog
point = data as! Point
processor = obj as? IProcessor
```

## Nullable-target rule

Because the operator owns the failure mode, the target must be non-nullable. `x as? T?` / `x as! T?`
is a **hard error** (`SPY0334`): *"drop the `?` on the target type."* The `as?` form already yields
`T?`, and `as!` already determines the throwing failure mode, so a `?` on the target is redundant.

## Operator Precedence

The cast operators bind looser than member access, function calls, and arithmetic operators, but
tighter than comparison and logical operators:

| Precedence | Operators |
|------------|-----------|
| (higher) | `()`, `[]`, `.`, `?.` |
| | `**` |
| | `+x`, `-x`, `~x` |
| | `*`, `/`, `//`, `%` |
| | `+`, `-` |
| | `<<`, `>>` |
| | `&` |
| | `^` |
| | `\|` |
| | `\|>` |
| | `as!`, `as?` |
| | `in`, `is`, `<`, `>`, `==`, etc. |
| | `not`, `and`, `or`, `??` |
| | `try`, `maybe` |
| (lower) | `x if c else y`, `lambda` |

This means:

```python
# Parentheses needed for member access on cast result
name = (animal as! Dog).name
result = (obj as! IProcessor).process(data)

# Arithmetic binds tighter than the cast operators
x = value + 1 as! int64          # Parsed as: (value + 1) as! int64

# No parentheses needed for comparisons
if animal as? Dog is not None:
    pass

# Chained with None check
if (dog := animal as? Dog) is not None and dog.age > 5:
    pass

# `try` captures the entire cast expression (`maybe` binds at the same low precedence)
result = try animal as! Dog      # Parsed as: try (animal as! Dog)
```

## Invalid Casts

The compiler rejects casts that are statically known to be impossible:

```python
x: int = 42
s = x as! str                    # ERROR: int cannot be cast to str (use str(x))

dog: Dog = Dog("Buddy")
cat = dog as! Cat                # ERROR: Dog cannot be cast to Cat (no inheritance relationship)
```

## Casting `None`

Casting `None` always fails:

```python
x: Dog | None = None
dog = x as! Dog                  # Throws InvalidCastException
dog = x as? Dog                  # None
```

*Implementation: Lowered*
- *`value as! T` → `(T)value` (C# cast expression)*
- *`value as? T` → `value is T _temp ? Optional<T>.Some(_temp) : default` (wraps in `Optional<T>`)*

```csharp
// value as! Dog (throwing)
(Dog)value

// value as? Dog (safe, any type - uses Optional<T>)
value is Dog _temp ? Optional<Dog>.Some(_temp) : default

// value as? int (safe, value type - also uses Optional<T>)
value is int _temp ? Optional<int>.Some(_temp) : default
```

`as!` invokes any user-defined `__explicit__` conversion.

## Note on `as`

Bare `as` is **not** a casting operator in Sharpy — the keyword stays reserved for aliasing and capture
(exception binding, context managers, import aliases, and match/case pattern binding). `x as int`
(bare `as`, no `?`/`!`) remains a parse error. SRP-0005 originally rejected an `as` cast over binder
ambiguity; the two-token `as?`/`as!` operators sidestep that ambiguity entirely because they are
lexically distinct from bare `as`, which is why #1029 supersedes that rejection. See
[SRP-0005](../rejected_proposals/SRP-0005-as-casting-operator.md).

## See Also

- [Conversion Operators](conversion_operators.md) — User-defined conversions
- [Type Narrowing](type_narrowing.md) — Narrowing types with `is not None` and `isinstance()`
- [Nullable Types](nullable_types.md) — Nullable type semantics

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
big: int64 = 1_000_000
small = big to int               # Throws on overflow
small = big to int?              # None on overflow

precise: float = 3.14159
rounded = precise to int         # Truncates toward zero (3), throws if out of range
rounded = precise to int?        # None if out of range
```

## Using Result[T, E] or Optional[T]

Casting can be chained with `try` and `maybe` expressions to wrap
the cast behavior in safe tagged unions:

```python
my_dog: object = Dog()
some_result = try my_dog to Cat  # some_result = Result[Cat, InvalidCastException].Err
some_result = try my_dog to Cat?  # some_result = Result[Cat?, Exception].Ok(None). Compiler will warn user to use a `maybe` expression instead

some_optional = maybe my_dog to Cat  # Throws. Compiler will warn user to use a `try` expression instead
some_optional = maybe my_dog to Cat?  # some_optional = Optional[str].None()
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
| Widening (e.g., `int32` → `int64`) | Always succeeds |
| Narrowing (e.g., `int64` → `int32`) | Throws/None on overflow |
| Float → Integer | Truncates toward zero, throws/None if out of range |
| Integer → Float | May lose precision (no failure) |

```python
# Widening - always safe
x: int = 42
y = x to int64                    # Always succeeds

# Narrowing - may fail
big: int64 = 10_000_000_000
small = big to int               # Throws: value too large for int
small = big to int?              # None: value too large for int

# Float to integer truncation
pi: float = 3.99
n = pi to int                    # 3 (truncates toward zero)
neg: float = -3.99
m = neg to int                   # -3 (truncates toward zero)

# Out of range
huge: float = 1e100
n = huge to int?                 # None: out of int range
```

### Edge-case semantics of the safe numeric form

For the safe form (`value to T?` / `value as? T`) with a concrete numeric source, the boundary
cases are defined as follows:

| Case | Result |
|------|--------|
| In-range float → `int`/`long` | `Some(truncated)` — truncation is toward zero, so `3.9 to int?` is `Some(3)` and `-3.9 to int?` is `Some(-3)` (matching Python's `int(x)`) |
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
| | `<<`, `>>` |
| | `&` |
| | `^` |
| | `\|` |
| | `\|>` |
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
x = value + 1 to int64          # Parsed as: (value + 1) to int64

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
x: Dog | None = None
dog = x to Dog                   # Throws InvalidCastException
dog = x to Dog?                  # None
```

*Implementation: Lowered*
- *`value to T` → `(T)value` (C# cast expression)*
- *`value to T?` → `value is T _temp ? Optional<T>.Some(_temp) : default` (wraps in `Optional<T>`)*

```csharp
// value to Dog (throwing)
(Dog)value

// value to Dog? (safe, any type - uses Optional<T>)
value is Dog _temp ? Optional<Dog>.Some(_temp) : default

// value to int? (safe, value type - also uses Optional<T>)
value is int _temp ? Optional<int>.Some(_temp) : default
```

## Experimental: `as?` / `as!` failable casts

> **Status:** Experimental — behind the `failable_cast` feature flag. Off by default; carries no
> stability promise while experimental (see [feature lifecycle](../design/feature-lifecycle.md)).
> Tracking issue: [#1029](https://github.com/antonsynd/sharpy/issues/1029). Graduation (making `as?`/`as!`
> the primary spelling and retiring `to`) is tracked separately in
> [#1096](https://github.com/antonsynd/sharpy/issues/1096) — see the migration note below.

The `as?` / `as!` operators are a piloted alternative spelling for the two forms of `to`. The failure
mode moves from the target type's nullability onto the **operator**, so the target is always written
**non-nullable**:

| `to` form (primary) | `as` form (experimental) | Result type | On failure |
|---------------------|--------------------------|-------------|------------|
| `value to T`  | `value as! T` | `T`  | throws `InvalidCastException` |
| `value to T?` | `value as? T` | `T?` | evaluates to `None` |

```python
# Enable per compilation: --enable-feature=failable_cast, or <Features>failable_cast</Features>
# in a .spyproj. (Parser-scoped: a `from __future__ import` cannot unlock it.)

animal: Animal = get_animal()
dog = animal as! Dog          # throws if not a Dog        (== animal to Dog)
dog = animal as? Dog          # None if not a Dog          (== animal to Dog?)

boxed: object = 42
value = boxed as! int         # throws if not an int
value = boxed as? int         # None if not an int

some_result   = try   my_dog as! Cat   # Result[Cat, InvalidCastException]
some_optional = maybe my_dog as? Cat   # Optional[Cat]
```

The `as?`/`as!` forms are **lexically distinct** from bare `as` — the `?`/`!` must be immediately
adjacent to `as` (no intervening whitespace), so `x as ? T` and `x as ! T` are not casts. They lower
**identically** to the equivalent `to`/`to?` form (snapshot parity), and `as!` invokes any user-defined
`__explicit__` conversion exactly as `to` does.

**Nullable-target rule.** Because the operator owns the failure mode, the target must be non-nullable.
`x as? T?` / `x as! T?` is a **hard error** (`SPY0334`): *"drop the `?` on the target type."*

**Migration hint.** When `failable_cast` is enabled, a `to`/`to?` cast emits an advisory hint
(`SPY0479`) suggesting the `as!`/`as?` spelling. The hint fires *only* under the flag — it never advises
syntax the build cannot accept.

## Note on `as`

Bare `as` is **not** a casting operator in Sharpy — the keyword stays reserved for aliasing and capture
(exception binding, context managers, import aliases, and match/case pattern binding). `x as int`
(bare `as`, no `?`/`!`) remains a parse error. SRP-0005 originally rejected an `as` cast over binder
ambiguity; the two-token `as?`/`as!` operators (above) sidestep that ambiguity entirely because they are
lexically distinct from bare `as`, which is why #1029 supersedes that rejection. See
[SRP-0005](../rejected_proposals/SRP-0005-as-casting-operator.md).

## See Also

- [Conversion Operators](conversion_operators.md) — User-defined conversions
- [Type Narrowing](type_narrowing.md) — Narrowing types with `is not None` and `isinstance()`
- [Nullable Types](nullable_types.md) — Nullable type semantics

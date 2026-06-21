# Question-Mark Operator (`?`)

The postfix `?` operator is the **early-return** (error-propagation) operator. It
unwraps a [`Result[T, E]`](tagged_unions_result.md) or
[`Optional[T]`](tagged_unions_optional.md) value, and — if the value represents
failure (`Err`) or absence (`None`) — it short-circuits the enclosing function by
returning that failure/absence to the caller. This is analogous to Rust's `?`
operator.

```python
def parse_pair(a: str, b: str) -> tuple[int, int] !ValueError:
    x: int = int_parse(a)?   # unwrap, or return Err(...) from parse_pair
    y: int = int_parse(b)?   # unwrap, or return Err(...) from parse_pair
    return Ok((x, y))
```

## Overview

`expr?` does one of two things at runtime:

| Operand          | On success           | On failure (early return)        |
|------------------|----------------------|----------------------------------|
| `Result[T, E]`   | evaluates to `T`     | `return Err(e)` from the function |
| `Optional[T]`    | evaluates to `T`     | `return None` from the function   |

It replaces verbose `match` / unwrap boilerplate for the common "propagate the
error upward" pattern, while keeping control flow explicit and visible at the
call site (the trailing `?`).

## Syntax

`?` is a **postfix** operator written immediately after the expression it
operates on:

```python
value = compute()?
value = obj.method()?
value = items[0]?
value = (a + b)?
```

The operand must statically have type `Result[T, E]` or `Optional[T]`. Using `?`
on any other type is a compile-time error (see [Restrictions](#restrictions)).

## Precedence

`?` is parsed in the **postfix** position, at the same level as member access
(`.`), indexing (`[]`), and calls (`()`). It binds tighter than every binary
operator. As a postfix operator it associates with the immediately-preceding
primary expression:

```python
compute()?        # (compute())?      — the call result is unwrapped
a.b.c?            # ((a.b).c)?        — the whole member chain is unwrapped
x? + 1            # (x?) + 1          — x is unwrapped, then 1 is added
-x?               # -(x?)             — x is unwrapped, then negated
```

Because it is consumed in the postfix loop alongside `.`, `[]`, and `()`, you can
freely mix it into a chain:

```python
config.load()?.value?     # load() unwrapped, then .value unwrapped
```

## Supported Types

### `Result[T, E]`

`result?` yields `T` when the value is `Ok(T)`. When the value is `Err(e)`, the
enclosing function returns `Err(e)` immediately.

```python
def total(parts: list[str]) -> int !ValueError:
    sum: int = 0
    for p in parts:
        sum += int_parse(p)?    # any Err short-circuits the whole function
    return Ok(sum)
```

### `Optional[T]`

`optional?` yields `T` when the value is `Some(T)`. When the value is `None`, the
enclosing function returns `None` immediately.

```python
def first_upper(items: list[str]?) -> str?:
    items_val = items?          # if items is None, return None
    return Some(items_val[0].upper())
```

## Return-Type Requirements

`?` only makes sense when the enclosing function can itself propagate the
failure. The compiler enforces this:

- **`?` on `Result`** requires the enclosing function to return a `Result[_, E2]`.
  The operand's error type `E1` must be **assignable** to the function's error
  type `E2` (`E1` is convertible to `E2`). Otherwise the compiler reports
  `SPY0461`.
- **`?` on `Optional`** requires the enclosing function to return an
  `Optional[_]`. Otherwise the compiler reports `SPY0461`.

```python
# ✅ E1 (ValueError) assignable to E2 (Exception)
def f(s: str) -> int !Exception:
    return Ok(int_parse(s)?)        # int_parse returns int !ValueError

# ❌ SPY0461 — function returns Optional, not Result
def g(s: str) -> int?:
    return Some(int_parse(s)?)      # int_parse returns int !ValueError
```

The early-return value is constructed against the *function's* declared types:
`?` on a `Result[T, E1]` inside a function returning `Result[R, E2]` emits a
`return Result<R, E2>.Err(e)`.

## Interaction with `??` (Null-Coalescing)

At the lexer level, [`??`](null_coalescing_operator.md) is now **two `?`
tokens**, so a run of consecutive `?` characters has to be disambiguated between
early-return (`?`) and null-coalescing (`??`). The parser uses the following
**N-count rule**:

> Count the run of `N` consecutive `?` tokens. If they are immediately followed
> by the start of an expression (so a right-hand operand exists) **and** `N >= 2`,
> then the **last two** `?` tokens form a `??` (null-coalesce) and the remaining
> `N - 2` are early-return `?`. Otherwise, **all** `N` are early-return `?`.

This yields the following intuitive cases:

| Source     | `N` | RHS expr? | Early-return `?` | Coalesce `??` | Meaning                                                       |
|------------|-----|-----------|------------------|---------------|--------------------------------------------------------------|
| `x?`       | 1   | no        | 1                | 0             | single early-return                                          |
| `x??`      | 2   | no        | 2                | 0             | double early-return — unwrap a nested `Result`/`Optional` twice |
| `x ?? y`   | 2   | yes       | 0                | 1             | null-coalesce (`x` or default `y`)                           |
| `x???y`    | 3   | yes       | 1                | 1             | early-return once, then `?? y` coalesce                      |
| `x????y`   | 4   | yes       | 2                | 1             | early-return twice, then `?? y` coalesce                     |

```python
# x?? — x is a doubly-wrapped value (e.g. Optional[Optional[int]]); unwrap twice,
# early-returning at whichever level is empty.
inner: int = x??

# x???y — early-return once, then coalesce the result with y
value: int = compute()??? fallback
#                     └┬┘└──┬──┘
#         early-return ?    ?? y coalesce
```

> The disambiguation is purely syntactic (token counting). Each early-return `?`
> and the final `??` are then type-checked independently against the operand and
> RHS types.

## Restrictions

| Condition                                   | Diagnostic | Message (abbreviated)                                          |
|---------------------------------------------|------------|---------------------------------------------------------------|
| Used at module level / outside any function | `SPY0462`  | `'?' operator can only be used inside a function`              |
| Used inside a `finally:` block              | `SPY0211`  | `'?' operator cannot be used inside a 'finally' block`         |
| Operand is not `Result` or `Optional`       | `SPY0460`  | `'?' operator requires Result or Optional type, got '...'`    |
| Return type incompatible / error mismatch   | `SPY0461`  | `'?' ... is not assignable to function return error type ...` |

`?` is disallowed in `finally` blocks because an early `return` from `finally`
would silently discard a pending exception or return value, which is a footgun
rather than error propagation.

## Interaction with `try` / `maybe`

`?`, [`try`](try_expressions.md), and [`maybe`](maybe_expressions.md) are
complementary:

- **`?`** *propagates* an existing `Result`/`Optional` failure upward by
  early-returning it. It does not change representation; it consumes one.
- **`try expr`** *converts* a throwing expression (exceptions / `T | None`) into
  a `Result`, so its failures can then be propagated with `?`.
- **`maybe expr`** *converts* a `T | None` (.NET nullable) into an `Optional[T]`,
  so its absence can then be propagated with `?`.

Because `?` binds **tighter** than the prefix `try`/`maybe` keywords, parenthesize
the conversion so that `?` applies to the produced `Result`/`Optional` rather than
to the inner (un-converted) value:

```python
def load(path: str) -> Config !IOError:
    # (try ...) wraps a throwing call into a Result; ? then propagates any Err upward.
    # Parentheses are required: `try read_file(path)?` would apply ? to the raw
    # return value (not a Result) and fail to type-check.
    content: str = (try read_file(path))?
    return Ok(parse(content))
```

## Desugaring

`expr?` is lowered by hoisting statements before the use site and replacing the
expression with an `Unwrap()` call. For an operand of type `Result[T, E1]` inside
a function returning `Result[R, E2]`:

```python
value = compute()?
```

desugars to (conceptually):

```csharp
var __qm_0 = compute();
if (__qm_0.IsErr)
    return Result<R, E2>.Err(__qm_0.UnwrapErr());
var value = __qm_0.Unwrap();
```

For an operand of type `Optional[T]` inside a function returning `Optional[R]`:

```python
value = compute()?
```

desugars to:

```csharp
var __qm_0 = compute();
if (__qm_0.IsNone)
    return Optional<R>.None;
var value = __qm_0.Unwrap();
```

Nested `?` (e.g. `x??`) desugars depth-first: the inner `?` hoists its guard
first, then the outer `?` operates on the already-unwrapped temporary.

## Examples

### Chained parsing with early return

```python
def parse_point(s: str) -> Point !ValueError:
    parts: list[str] = s.split(",")
    if len(parts) != 2:
        return Err(ValueError("expected 'x,y'"))
    x: int = int_parse(parts[0])?     # propagate ValueError on bad x
    y: int = int_parse(parts[1])?     # propagate ValueError on bad y
    return Ok(Point(x, y))
```

### Optional propagation

```python
def head_len(items: list[str]?) -> int?:
    xs = items?                       # None in -> None out
    if len(xs) == 0:
        return None
    return Some(len(xs[0]))
```

### Mixing with `try`

```python
def read_int(path: str) -> int !Exception:
    text: str = (try read_file(path))?       # exception -> Err -> propagated
    return Ok((try int_parse(text))?)        # parse failure -> Err -> propagated
```

*Implementation*
- *✅ Implemented — the postfix `?` operator is supported end-to-end (lexer,
  parser, semantic analysis, code generation, and LSP). It lowers to an
  `IsErr`/`IsNone` guard plus `Unwrap()`/`UnwrapErr()`, with zero heap
  allocation (operates on the `Result`/`Optional` structs).*

## See Also

- [Result Type](tagged_unions_result.md) — `Result[T, E]` / `T !E`
- [Optional Type](tagged_unions_optional.md) — `Optional[T]` / `T?`
- [Null-Coalescing Operator](null_coalescing_operator.md) — `??` (now two `?` tokens)
- [Try Expressions](try_expressions.md) — convert throwing code into `Result`
- [Maybe Expressions](maybe_expressions.md) — convert `T | None` into `Optional[T]`
- [Operator Precedence](operator_precedence.md) — full precedence table

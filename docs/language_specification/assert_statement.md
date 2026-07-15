# Assert Statement

```python
assert condition
assert x > 0, "Value must be positive"
```

## Semantics

`assert` is a **real runtime check**, matching Python. When `condition` is falsy the assert
raises `AssertionError`; when a message is supplied it becomes the exception message:

```python
assert x > 0, "Value must be positive"   # raises AssertionError("Value must be positive") when x <= 0
assert cond                              # raises AssertionError() when cond is falsy
```

A passing assert is a no-op.

## Implementation

- Outside `@test` functions, `assert cond, msg` lowers to
  `if (!cond) throw new global::Sharpy.AssertionError(msg)`. The condition is evaluated with the
  same truthiness rules as `if`.
  - Earlier versions lowered to `System.Diagnostics.Debug.Assert(...)`, which was stripped in every
    configuration (the assembly is compiled with no `DEBUG` preprocessor symbol), so asserts never
    ran. That behavior was a bug (#1070).
- Inside `@test` functions, `assert` lowers to the corresponding xUnit assertion for richer failure
  messages (see [unittest.md](unittest.md)). `assert x == approx(y)` lowers to a tolerance
  comparison in **both** contexts — the xUnit flavor inside `@test`, the `AssertionError` flavor
  elsewhere (#1074).

## Future work

A future `-O`-analogue optimization flag may strip asserts (mirroring CPython's `python -O`, which
disables `assert`). This flag is **not yet implemented**; asserts always run today.

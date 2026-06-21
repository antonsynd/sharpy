# Null-Coalescing Operator

The `??` operator provides a default value when the left operand is absent. It works with both `T?` (`Optional[T]`) and `T | None` (C# nullable):

```python
# With T? (Optional)
name: str? = get_name()
display = name ?? "Anonymous"

# With T | None (C# nullable)
raw: str | None = dotnet_api()
display = raw ?? "Anonymous"

# Chaining
result = first ?? second ?? default_value
```

This contrasts with the `or` operator which tests for truthiness (via `__bool__()`) rather than absence.

> **Lexer note:** As of the `?` early-return operator, `??` is no longer a single
> token — it is **two `?` tokens** at the lexer level. The semantics of `??` are
> unchanged; the parser distinguishes `??` (null-coalesce) from the postfix `?`
> (early-return) using a token-counting rule. See the
> [Question-Mark Operator](question_mark_operator.md#interaction-with--null-coalescing)
> for the disambiguation details.

> **Note:** Unlike Python, Sharpy's `or` operator always returns `bool`, not the operand value. `"" or "Anonymous"` returns `True` (bool), not `"Anonymous"` (str). This is because Sharpy's `or` maps to C#'s `||` operator, which produces a boolean result.

```python
name = "" ?? "Anonymous"    # name = "" (not None, so left side kept)
name = None ?? "Anonymous"  # name = "Anonymous" (None, so right side used)

# or returns bool, not the operand value (unlike Python)
result = "" or "Anonymous"    # result = True (bool, not "Anonymous")
result = None or "Anonymous"  # result = True (bool, not "Anonymous")
```

*Implementation*
- *✅ Native - For `T | None`, maps to C# `??` operator.*
- *🔄 Lowered - For `T?` (`Optional[T]`), compiler generates `match` on `Some`/`None()`.*

## Optional (Tagged Union)

The `Optional[T]` tagged union (written as `T?`) works with null coalescing, with its empty case (`None()`) being treated similarly to bare `None`:

```python
maybe_int: int? = Some(5)
val = maybe_int ?? 0  # val = 5

maybe_int = None()
val = maybe_int ?? 0  # val = 0
```

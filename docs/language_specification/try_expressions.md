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


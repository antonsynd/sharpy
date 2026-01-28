# Try expressions

The `Result[T, E]` type can be implicitly created via
`try` expressions. A `try` expression wraps the value of
the expression in `Result[T, E]` where `E`, if not
specified, is almost always the base `Exception` type, and `T` is
the type of the expression. If the expression raises an
exception, then the result holds its `Err` case.


```python
x = try int("some string")  # x is of type Result[int, Exception]

# Using T !E shorthand in type annotations:
x: int !Exception = try int("some string")
```

The only place where `E` is not automatically made to be the
base `Exception` type is in type casting of the form `to` where
it is `InvalidCastException`:

```python
x = try my_dog to Cat  # x is of type Result[Cat, InvalidCastException]

# Equivalent with T !E shorthand:
x: Cat !InvalidCastException = try my_dog to Cat
```

A `try` expression can be specified for a specific type
where if the expression throws that type, then it is caught
inside `Err` case. Other types become uncaught exceptions
that must be handled by other means, e.g. `try/except/finally`.

```python
x = try[ValueError] int("some string")  # x is of type Result[int, ValueError]

# Equivalent with T !E shorthand:
x: int !ValueError = try[ValueError] int("some string")
```

It is not an error if the expression would never raise an
exception. In such cases, the result type is always `Result[T, Exception]` where `T` is the expression's type.

**Precedence Rules:**

The `try` expression has very low precedence (lower than `to`, arithmetic, comparisons, and logical operators), meaning it captures the entire following expression:

```python
# try captures the full expression including arithmetic
x = try some_func(4) + 5     # Parsed as: try (some_func(4) + 5)
                             # Result[int, Exception] wrapping the sum

# try captures through comparisons and logical operators
y = try parse_int(s) > 0 and validate(s)  # Parsed as: try (parse_int(s) > 0 and validate(s))
                                          # Result[bool, Exception]

# try is lower precedence than `to`, so it wraps casts
z = try animal to Dog        # Parsed as: try (animal to Dog)
                             # Result[Dog, InvalidCastException]

# try does NOT capture conditional expressions (which have lower precedence)
y = try foo() if cond else bar()   # Parsed as: (try foo()) if cond else bar()
                                   # try only applies to foo(), not bar()

# Parentheses make intent clear
y = try (foo() if cond else bar())  # try applies to entire conditional
```

Use parentheses when you need to limit what `try` captures:

```python
# Only wrap the function call
x = (try int("abc")).unwrap_or(0) + 5  # Unwrap first, then add
```

*Implementation*
- *🔄 Lowered - `try`/`catch` pattern wrapping the expression.*

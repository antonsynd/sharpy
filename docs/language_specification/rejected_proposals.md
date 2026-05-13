# Rejected Proposals

Features that were considered during Sharpy's design and explicitly rejected. Documenting these prevents re-litigating settled decisions and explains the reasoning for future contributors.

## Expression Blocks (`do:`)

**Proposed:** Block expressions that group multiple statements into a single expression, with the last value as the result. Inspired by Rust's everything-is-an-expression philosophy.

```python
result = do:
    x = compute()
    if x > 0:
        x * 2
    else:
        0
```

**Rejected because:** Lambdas and helper functions already cover every use case that `do:` blocks would address. The feature adds syntax complexity without enabling anything new — it's syntactic sugar with a poor complexity-to-value ratio. Helper functions are more readable, testable, and reusable than inline expression blocks.

**Alternatives:** Use a helper function or an immediately-called lambda:
```python
result = (lambda: (x := compute(), x * 2 if x > 0 else 0)[-1])()

# Better: just use a function
def compute_result() -> int:
    x = compute()
    return x * 2 if x > 0 else 0

result = compute_result()
```

**History:** Full design document preserved in [expression_blocks.md](expression_blocks.md) for reference. The `do` keyword remains reserved to prevent its use as an identifier.

---

## `@dynamic_kwargs` and `**kwargs` Call-Site Spreading

**Proposed:** A `@dynamic_kwargs` decorator (Phase 11) that would enable `**kwargs` dictionary spreading in function calls, allowing patterns like:

```python
# Proposed syntax (rejected)
@dynamic_kwargs
def wrapper(*args, **kwargs):
    return underlying_function(*args, **kwargs)

opts = {"prefix": ">>", "suffix": "<<"}
format_text(**opts)  # Spread dict as named arguments
```

**Rejected because:** This fundamentally conflicts with Sharpy's static type system (Axiom 3). Dictionary spreading in function calls erases compile-time parameter checking — the compiler cannot verify that the dictionary keys match the function's parameter names or that the value types are correct. Promoting this pattern would undermine the type safety guarantees that distinguish Sharpy from Python.

The `@dynamic_kwargs` decorator would require runtime dispatch to resolve parameter bindings, which violates Axiom 1 (.NET first) by introducing overhead that doesn't exist in equivalent C# code.

**Alternatives:**
- Use explicit named arguments: `format_text(prefix=">>", suffix="<<")`
- Use a typed configuration object: `format_text(config)` where `config` is a dataclass/struct
- Use method overloading for variant call signatures

**What remains supported:**
- `*args` positional spreading in function calls: `f(*args)` — this is type-safe because the compiler knows the element type
- `**` spreading in dict literals: `{**d1, **d2}` — this is type-safe because both sides have known key/value types
- Object copy with named arguments: `original.copy(age=31)` — statically checked

**Note:** The "object copy with overrides" syntax `original.copy(**{"age": 31})` using dict spreading is also rejected. The named-argument form `original.copy(age=31)` remains the intended pattern.

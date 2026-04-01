# Expression Blocks

> **Implementation status:** Deferred post-v0.2.x — lambdas and helper functions suffice for most use cases.

Expression blocks (`do:`) allow multiple statements to be grouped into a single expression that evaluates to the last value, inspired by Rust's everything-is-an-expression philosophy.

## Syntax

```python
result = do:
    statement1
    statement2
    expression  # This value is returned
```

The last line of the block must be an expression (not a statement), and its value becomes the value of the entire `do:` block.

## Allowed and Disallowed Constructs

| Construct | Allowed | Notes |
|-----------|---------|-------|
| Variable declarations | ✅ | `x = 5`, `x: int = 5` |
| `if`/`elif`/`else` | ✅ | All branches must end with an expression of compatible type |
| `for` loops | ❌ | Loops don't produce values; use comprehensions or explicit accumulation outside `do:` |
| `while` loops | ❌ | Loops don't produce values |
| `match` (expression form) | ✅ | All cases must produce compatible types |
| `try`/`except` | ✅ | All branches must end with an expression of compatible type |
| Nested `do:` blocks | ✅ | `do:` inside `do:` |
| Function calls | ✅ | Including side-effecting calls |
| `return` | ❌ | Not allowed; every branch must end with an expression |
| `break` | ❌ | Not allowed; every branch must end with an expression |
| `continue` | ❌ | Not allowed |
| `yield` | ❌ | Generators cannot be defined inline |
| `def` (function definition) | ❌ | Define functions outside `do:` block |
| `class` definition | ❌ | Define classes outside `do:` block |
| `import` | ❌ | Imports must be at module level |

## Core Rule: Every Terminal Branch Must Be an Expression

The fundamental rule for `do:` blocks is simple: **every possible code path must end with an expression that produces a value**. There is no early exit mechanism.

```python
# ✅ Valid - both branches end with expressions
result = do:
    x = compute()
    if x > 0:
        x * 2
    else:
        0

# ❌ Invalid - missing else branch
result = do:
    x = compute()
    if x > 0:
        x * 2
    # ERROR: missing else branch - what value if x <= 0?

# ❌ Invalid - branch ends with statement
result = do:
    x = compute()
    if x > 0:
        x * 2
    else:
        print("negative")  # ERROR: print() doesn't return a value
```

## Basic Usage

```python
# Simple computation
result = do:
    x = 5
    y = 10
    x + y  # Evaluates to 15

# Complex initialization
config = do:
    base = load_base_config()
    overrides = load_overrides()
    merged = merge(base, overrides)
    validate(merged)
    merged  # Return the validated config
```

## Conditional Initialization

```python
# Complex conditional assignment
status = do:
    if error:
        log_error()
        "failed"
    else:
        log_success()
        "success"

# With pattern matching
result = do:
    match value:
        case 0: "zero"
        case 1: "one"
        case _: "many"
```

## Type Inference

The type of a `do:` block is inferred from the final expression. All branches must produce compatible types:

```python
# Type is int
x: int = do:
    a = 5
    b = 3
    a + b

# Type is str
message: str = do:
    name = get_name()
    f"Hello, {name}!"

# Type must match all branches
value: int = do:
    if condition:
        42
    else:
        100  # Both branches must be int

# ❌ Invalid - inconsistent types
value = do:
    if flag:
        42      # int
    else:
        "text"  # str - ERROR: type mismatch
```

## Scope

Variables declared in a `do:` block are local to that block:

```python
result = do:
    temp = 42  # Local to this block
    temp * 2

print(temp)  # ERROR: temp not in scope

# Outer variables are accessible
outer = 10
result = do:
    inner = 5
    outer + inner  # Can access outer
```

## Nested Blocks

```python
result = do:
    x = do:
        a = 5
        b = 3
        a + b  # x = 8

    y = do:
        c = 2
        d = 4
        c * d  # y = 8

    x + y  # result = 16
```

## With Try/Except

All branches of exception handling must produce a value:

```python
result = do:
    try:
        data = risky_operation()
        process(data)
    except ValueError:
        default_value
    except IOError:
        fallback_value
```

## Common Patterns

**Complex Initialization:**
```python
user = do:
    raw_data = fetch_user_data(id)
    parsed = parse_json(raw_data)
    validated = validate_user(parsed)
    User.from_dict(validated)
```

**Conditional Configuration:**
```python
settings = do:
    base = default_settings()
    if is_production:
        Settings(
            log_level=LogLevel.ERROR,
            cache_enabled=True,
            base_config=base
        )
    else:
        Settings(
            log_level=LogLevel.DEBUG,
            cache_enabled=False,
            base_config=base
        )
```

**With Match:**
```python
description = do:
    category = classify(item)
    match category:
        case Category.BOOK:
            f"Book: {item.title}"
        case Category.MOVIE:
            f"Movie: {item.title} ({item.year})"
        case _:
            f"Item: {item.name}"
```

## Expressions vs Statements

The last item in every branch of a `do:` block must be an expression:

```python
# ✅ Valid - last item is expression
x = do:
    y = 5
    y + 1  # Expression

# ❌ Invalid - last item is statement
x = do:
    y = 5
    print(y)  # Statement, not expression - ERROR

# ✅ Fixed - add expression after statement
x = do:
    y = 5
    print(y)
    y  # Return y
```

## Comparison with Other Constructs

| Construct | Purpose | Returns Value | Multi-statement |
|-----------|---------|---------------|-----------------|
| `do:` block | Group statements into expression | ✅ | ✅ |
| `if/else` | Conditional | ✅ (when used as expression) | ❌ (single expression per branch) |
| `match` | Pattern matching | ✅ | ❌ (expression form) |
| Function | Reusable logic | ✅ | ✅ |
| Lambda | Anonymous function | ✅ | ❌ (single expression) |

## When to Use

**Use `do:` when:**
- Complex initialization requires multiple steps
- Conditional logic determines initialization value
- Want to keep temporary variables scoped

**Avoid `do:` when:**
- Simple expressions suffice: `x = a + b`
- Logic should be extracted to a named function
- You need loops (use comprehensions or regular code instead)

*Implementation*
- *❌ Deferred post-v0.2.x. When implemented, will be lowered to immediately-invoked lambda expression (IILE).*

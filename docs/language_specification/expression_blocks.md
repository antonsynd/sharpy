# Expression Blocks

Expression blocks (`do:`) allow multiple statements to be grouped into a single expression that evaluates to the last value, inspired by Rust's everything-is-an-expression philosophy.

## Syntax

```python
result = do:
    statement1
    statement2
    expression  # This value is returned
```

The last line of the block must be an expression (not a statement), and its value becomes the value of the entire `do:` block.

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

The type of a `do:` block is inferred from the final expression:

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

## Early Returns

Use `break` to exit early from a `do:` block with a value:

```python
result = do:
    if error_condition:
        break "error"  # Return "error" immediately
    
    # Continue processing
    data = process()
    if validation_fails(data):
        break "invalid"
    
    data  # Normal return
```

Note: `break` with a value only works in `do:` blocks, not in loops.

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
        base.log_level = LogLevel.ERROR
        base.cache_enabled = True
    else:
        base.log_level = LogLevel.DEBUG
        base.cache_enabled = False
    
    base  # Return configured settings
```

**Error Handling:**
```python
value = do:
    result = try_parse(input)
    if result is None:
        log_error("Parse failed")
        break default_value
    
    transformed = transform(result)
    if not validate(transformed):
        log_error("Validation failed")
        break default_value
    
    transformed
```

**Resource Management:**
```python
result = do:
    connection = open_connection()
    try:
        data = connection.fetch()
        processed = process(data)
        processed
    finally:
        connection.close()
```

## With Function Calls

`do:` blocks can be passed as arguments (wrapped in lambda):

```python
# As argument to function expecting () -> T
result = compute(lambda: do:
    x = expensive_computation()
    y = another_computation()
    x + y
)
```

## Expressions vs Statements

The last item in a `do:` block must be an expression:

```python
# ✅ Valid - last item is expression
x = do:
    y = 5
    y + 1  # Expression

# ❌ Invalid - last item is statement
x = do:
    y = 5
    print(y)  # Statement, not expression

# ✅ Fixed - use expression
x = do:
    y = 5
    print(y)
    y  # Return y
```

## Type Compatibility

All code paths must produce compatible types:

```python
# ✅ Valid - all paths return int
value: int = do:
    if x > 0:
        10
    elif x < 0:
        -10
    else:
        0

# ❌ Invalid - inconsistent types
value = do:
    if flag:
        42      # int
    else:
        "text"  # str - ERROR: type mismatch
```

## Comparison with Other Constructs

| Construct | Purpose | Returns Value | Multi-statement |
|-----------|---------|---------------|-----------------|
| `do:` block | Group statements into expression | ✅ | ✅ |
| `if/else` | Conditional | ✅ (when used as expression) | ❌ (single expression per branch) |
| `match` | Pattern matching | ✅ | ❌ (expression form) |
| Function | Reusable logic | ✅ | ✅ |
| Lambda | Anonymous function | ✅ | ❌ (single expression) |

## With Try/Except

`do:` blocks support exception handling:

```python
result = do:
    try:
        data = risky_operation()
        process(data)
    except ValueError as e:
        log_error(e)
        default_value
```

## C# Mapping

Expression blocks are lowered to immediately-invoked lambda expressions (IILEs):

```python
# Sharpy
result = do:
    x = 5
    y = 10
    x + y
```
```csharp
// C# 9.0
var result = (() => {
    var x = 5;
    var y = 10;
    return x + y;
})();
```

**With Early Exit:**
```python
# Sharpy
result = do:
    if error:
        break "error"
    "success"
```
```csharp
// C# 9.0
var result = (() => {
    if (error) {
        return "error";
    }
    return "success";
})();
```

## Performance Considerations

- Creates a closure, small runtime overhead
- Optimizer may inline in simple cases
- Use for readability, not performance-critical paths

```python
# Good: complex initialization
config = do:
    # Many lines of setup
    final_value

# Overkill: simple expression
x = do:
    5 + 3  # Just use: x = 5 + 3
```

## Limitations

- Cannot use `return` inside `do:` block (use `break` for early exit)
- Cannot declare functions inside `do:` block
- Last item must be an expression that produces a value

```python
# ❌ Cannot use return
result = do:
    return 42  # ERROR: return not allowed in do: block

# ✅ Use break instead
result = do:
    break 42

# ❌ Cannot end with statement
result = do:
    x = 5
    print(x)  # ERROR: last item must be expression

# ✅ Add expression at end
result = do:
    x = 5
    print(x)
    x  # OK: returns x
```

## When to Use

**Use `do:` when:**
- Complex initialization requires multiple steps
- Conditional logic determines initialization value
- Want to keep temporary variables scoped

**Avoid `do:` when:**
- Simple expressions suffice: `x = a + b`
- Logic should be extracted to a named function
- Performance is critical (adds closure overhead)

*Implementation: 🔄 Lowered - Transformed to immediately-invoked lambda expression (IILE):*
- Statements become lambda body
- Last expression becomes return value
- `break value` becomes `return value`
- Entire construct is called immediately: `(() => { ... })()`

---

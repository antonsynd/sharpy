# Pipe Operator

The pipe operator `|>` enables left-to-right data transformation chains, improving readability over nested function calls.

## Syntax

```python
value |> function
value |> function(arg1, arg2)
```

The pipe operator passes the left-hand value as the **first argument** to the right-hand function.

## Basic Usage

```python
# Instead of nested calls
result = sorted(filter(lambda x: x > 0, map(lambda x: x * 2, items)))

# With pipes - reads left to right
result = items |> map(lambda x: x * 2) |> filter(lambda x: x > 0) |> sorted()

# Single transformation
text = "hello" |> str.upper()  # "HELLO"

# With additional arguments
numbers = [1, 2, 3] |> map(lambda x: x * 2) |> list()
```

## Pipeline Chains

```python
# Data processing pipeline
data = load_data()
result = data |> parse_json() |> validate() |> transform() |> save()

# Equivalent to:
# result = save(transform(validate(parse_json(load_data()))))

# With method chains
user = get_user(id) |> update_email(new_email) |> save_to_db()
```

## Argument Position

The piped value becomes the **first argument** to the function:

```python
# Piping to a function with multiple parameters
def format_with_prefix(text: str, prefix: str) -> str:
    return f"{prefix}: {text}"

result = "hello" |> format_with_prefix("MESSAGE")
# Equivalent to: format_with_prefix("hello", "MESSAGE")
# Result: "MESSAGE: hello"

# Combining with partial application (see partial_application.md)
add_prefix = format_with_prefix(_, ">>>")
result = "test" |> add_prefix  # ">>>: test"
```

## Precedence

The pipe operator has low precedence, binding looser than most operators but tighter than assignment:

```python
# Arithmetic before pipe
result = 5 + 3 |> str()  # Equivalent to: (5 + 3) |> str() = "8"

# Comparison before pipe
flag = value > 0 |> not()  # Equivalent to: (value > 0) |> not()

# Use parentheses for clarity
result = (x + y) |> str() |> print()
```

## Method Chaining

The pipe operator works naturally with methods:

```python
# Chaining methods and functions
result = "  hello  " |> str.strip() |> str.upper() |> print()

# Mixed method calls and free functions
data |> parse() |> data.validate() |> transform() |> data.save()
```

## Type Safety

The pipe operator is fully type-checked at compile time:

```python
# Type mismatch caught at compile time
result = 42 |> str.upper()  # ERROR: str.upper() expects str, got int

# Correct types
result = 42 |> str() |> str.upper()  # OK: "42"
```

## Common Patterns

**Data Transformation:**
```python
# Transform collection
results = items |> map(transform) |> filter(is_valid) |> list()

# Process text
output = text |> str.strip() |> str.lower() |> parse_words()
```

**Option/Result Chaining:**
```python
# Chaining operations on Optional/Result types
value = get_user(id) |> user.get_profile() |> profile.get_email()
```

**Validation Chains:**
```python
# Sequential validation
user_data |> validate_email() |> validate_age() |> validate_address() |> save()
```

## Comparison with Other Operators

| Syntax | Meaning |
|--------|---------|
| `a \|> f()` | Pipe `a` as first arg to `f()` |
| `f(a)` | Regular function call |
| `a.method()` | Method call |
| `a ?? b` | Null coalescing |

*Implementation: 🔄 Lowered - Compiler transformation to function call:*

```python
# Sharpy
result = value |> function(arg1, arg2)
```
```csharp
// C# 9.0
var result = function(value, arg1, arg2);
```

**With Method Calls:**
```python
# Sharpy
result = value |> obj.method(arg)
```
```csharp
// C# 9.0
var result = obj.method(value, arg);
```

## Limitations

- Cannot pipe to constructors directly (use factory function instead)
- Cannot pipe to operators (use lambda or function wrapper)
- Piped value always becomes first argument (use partial application to reorder)

```python
# ❌ Cannot pipe to constructor
result = data |> MyClass()  # ERROR

# ✅ Use factory function instead
result = data |> MyClass.create()

# ❌ Cannot pipe to operator
result = 5 |> + 3  # ERROR

# ✅ Use lambda instead
result = 5 |> (lambda x: x + 3)()  # OK
```

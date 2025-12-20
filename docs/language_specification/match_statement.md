## Pattern Matching **[v0.1.6]**

### Match Statement

```python
def describe(value: object) -> str:
    match value:
        case 0:
            return "zero"
        case 1:
            return "one"
        case int() as n if n > 0:
            return "positive integer"
        case int() as n:
            return "negative integer"
        case str() as s:
            return f"string: {s}"
        case _:
            return "unknown"
```

*Implementation: ✅ Native - Maps to C# `switch` expression/statement (C# 8+).*

### Match Statement vs Match Expression

Sharpy supports both statement and expression forms of `match`, corresponding to C#'s switch statement and switch expression:

**Statement Form:**

Used when you need to execute statements for each case:

```python
match value:
    case 1:
        do_something()
        log("did something")
    case 2:
        do_other()
    case _:
        handle_default()
```

**Expression Form:**

Used when you want to produce a value:

```python
result = match value:
    case 1: "one"
    case 2: "two"
    case _: "other"

# Can be used anywhere an expression is expected
print(match x:
    case True: "yes"
    case False: "no"
)

# In a return statement
def categorize(n: int) -> str:
    return match n:
        case 0: "zero"
        case _ if n > 0: "positive"
        case _: "negative"
```

**Expression Form Rules:**
- Each case must be a single expression (not statements)
- All cases must produce values of compatible types
- Must be exhaustive (all possible values handled)
- Cases use `:` followed by an expression, not a block

*Implementation: 🔄 Lowered*
- *Statement form: C# `switch` statement*
- *Expression form: C# `switch` expression*

### Supported Patterns

| Pattern | Syntax | C# 9.0 Mapping |
|---------|--------|----------------|
| Literal | `case 0:` | `case 0:` |
| Type | `case int():` | `case int:` |
| Type with binding | `case int() as n:` | `case int n:` |
| Wildcard | `case _:` | `default:` or `_` |
| Guard | `case int() as n if n > 0:` | `case int n when n > 0:` |
| OR | `case "a" \| "b":` | `case "a" or "b":` |
| Tuple | `case (0, 0):` | Direct support |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` |
| Relational | `case > 0:` | Direct support (C# 9) |

*Implementation: ✅ Native - All patterns map to C# 9.0 pattern matching.*

### Tuple Patterns

```python
match point:
    case (0, 0):
        print("Origin")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (x, y):
        print(f"Point at ({x}, {y})")
```

### Property Patterns

```python
match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
```

### Positional Patterns

For types with a `Deconstruct` method (like records or types with explicit deconstruction), positional patterns extract values in order:

```python
# Assuming Point has Deconstruct(out double x, out double y)
match point:
    case Point(0, 0):              # Positional - matches x=0, y=0
        print("Origin")
    case Point(x, 0):              # Positional with binding
        print(f"On X-axis at {x}")
    case Point(0, y):              # Positional with binding
        print(f"On Y-axis at {y}")
    case Point(x, y):              # Positional with both bound
        print(f"Point at ({x}, {y})")

# Type pattern with binding (no Deconstruct needed)
match value:
    case int() as n:               # Type check and bind
        print(f"Integer: {n}")
    case str() as s if len(s) > 0: # Type, bind, and guard
        print(f"Non-empty string: {s}")
```

**Pattern Forms:**

| Pattern | Syntax | Use Case |
|---------|--------|----------|
| Property | `Point(x=0, y=y)` | Extract by property name |
| Positional | `Point(0, y)` | Extract by position (requires `Deconstruct`) |
| Type with binding | `int() as n` | Check type and bind entire value |

### Exhaustiveness Checking

The compiler checks that `match` statements cover all possible cases for certain types:

**Checked Types:**

| Type | Requirement |
|------|-------------|
| Enums | All enum values must be covered |
| `bool` | Must cover `True` and `False` |
| Tagged unions | All cases must be covered |
| Other types | Wildcard `_` or explicit default required |

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# ERROR: Non-exhaustive match (missing BLUE)
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")

# OK: Exhaustive with wildcard
match color:
    case Color.RED:
        print("Red")
    case _:
        print("Other color")

# OK: Fully exhaustive
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")
    case Color.BLUE:
        print("Blue")

# Boolean exhaustiveness
match flag:
    case True:
        print("Yes")
    # ERROR: missing False case
```

---


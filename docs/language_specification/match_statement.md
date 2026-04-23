# Pattern Matching

## Match Statement

```python
match value:
    case 0:
        print("zero")
    case 1:
        print("one")
    case (x, y):
        print(f"tuple: ({x}, {y})")
    case Color.RED:
        print("red")
    case "a" | "b":
        print("a or b")
    case > 100:
        print("large")
    case int() as n if n > 0:
        print(f"positive: {n}")
    case str() as s:
        print(f"string: {s}")
    case _:
        print("other")
```

*Implementation*
- *✅ Match statement maps to C# `switch` statement. Supports literal, wildcard, binding, tuple, member access, or-pattern, relational, type, property, and positional patterns + guard clauses (see [Supported Patterns](#supported-patterns) below).*

## Match Statement vs Match Expression

> **Implementation status:** Both match **statement** and match **expression** forms are implemented.

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

## Disambiguation: Expression vs Statement Context

The parser determines whether `match` is an expression or statement based on syntactic context:

**Expression contexts** (match produces a value):
```python
# Assignment RHS
x = match value:
    case 1: "one"
    case _: "other"

# Return statement
return match value:
    case True: "yes"
    case False: "no"

# Function argument
f(match value:
    case 1: "a"
    case _: "b"
)

# Inside larger expression
result = prefix + match value:
    case 1: "one"
    case _: "other"

# List/dict literal element
items = [match x:
    case 1: "one"
    case _: "other"
]

# Conditional expression
y = (match x: case 1: "a" case _: "b") if flag else default
```

**Statement contexts** (match is standalone):
```python
# At statement level (not part of larger expression)
match value:
    case 1:
        do_something()
        log_result()
    case _:
        handle_default()

# After if/elif/else at statement level
if condition:
    match value:
        case 1:
            action1()
        case _:
            action2()
```

**Syntactic distinction:**

| Feature | Expression Form | Statement Form |
|---------|-----------------|----------------|
| Case body | Single expression after `:` | Indented block |
| Used in | Assignment, return, arguments | Standalone statement |
| Newline after `case X:` | Expression on same line | Block on next line |
| Produces value | Yes | No |

**Parser hint:** If `case pattern:` is followed by `NEWLINE INDENT`, it's statement form. If followed by an expression on the same line, it's expression form.

*Implementation*
- *Statement form: ✅ Implemented — C# `switch` statement*
- *Expression form: ✅ Implemented — C# `switch` expression*

## Supported Patterns

| Pattern | Syntax | C# 9.0 Mapping | Status |
|---------|--------|----------------|--------|
| Literal | `case 0:` | `case 0:` | ✅ Implemented |
| Wildcard | `case _:` | `default:` or `_` | ✅ Implemented |
| Binding | `case x:` | `var x` | ✅ Implemented |
| Tuple | `case (0, 0):` | Direct support | ✅ Implemented |
| Member access | `case Color.RED:` | `case Color.RED:` | ✅ Implemented |
| Guard clause | `case x if x > 0:` | `when` clause | ✅ Implemented |
| Type with binding | `case int() as n:` | `case int n:` | ✅ Implemented |
| Or | `case "a" \| "b":` | `case "a" or "b":` | ✅ Implemented |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` | ✅ Implemented |
| Positional | `case Point(0, y):` | `case Point { X: 0 }:` (mapped via fields) | ✅ Implemented |
| Relational | `case > 0:` | Direct support (C# 9) | ✅ Implemented |

*Implementation*
- *All pattern types map to C# 9.0 pattern matching. Guard clauses (`if expr`) are supported on any pattern via C# `when` clauses.*
- *Or-patterns use C# `or` pattern (`BinaryPattern`). Binding patterns inside or-patterns are rejected (SPY0359) — use wildcard `_` instead.*
- *Relational patterns use C# `RelationalPattern` and require numeric scrutinee types.*
- *Positional patterns are mapped to property patterns using field declaration order (no `Deconstruct` required).*

## Tuple Patterns

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

## Property Patterns

```python
match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
```

*Implementation: ✅ Implemented — maps to C# recursive pattern with property clause (`case Point { X: 0, Y: 0 }`). Property names are mangled to PascalCase.*

## Positional Patterns

Positional patterns match fields by declaration order (no `Deconstruct` method required):

```python
match point:
    case Point(0, 0):              # Positional - matches x=0, y=0
        print("Origin")
    case Point(x, 0):              # Positional with binding
        print(f"On X-axis at {x}")
    case Point(0, y):              # Positional with binding
        print(f"On Y-axis at {y}")
    case Point(x, y):              # Positional with both bound
        print(f"Point at ({x}, {y})")
```

*Implementation: ✅ Implemented — positional patterns are mapped to C# property patterns using field declaration order. The element count must match the number of fields on the type (SPY0363).*

## Type Patterns with Binding

```python
match value:
    case int() as n:               # Type check and bind
        print(f"Integer: {n}")
    case str() as s if len(s) > 0: # Type, bind, and guard
        print(f"Non-empty string: {s}")
    case int():                    # Type check only (no binding)
        print("Some integer")
```

*Implementation: ✅ Implemented — maps to C# declaration pattern (`case int n:`) with binding, or type pattern with discard designation when no binding is needed.*

**Pattern Forms:**

| Pattern | Syntax | Use Case | Status |
|---------|--------|----------|--------|
| Property | `Point(x=0, y=y)` | Extract by property name | ✅ Implemented |
| Positional | `Point(0, y)` | Extract by position (field order) | ✅ Implemented |
| Type with binding | `int() as n` | Check type and bind entire value | ✅ Implemented |

## Guard Patterns

Guard clauses add conditions to any pattern using `if`. The `GuardPattern` AST node wraps an inner pattern with a boolean guard expression.

### Basic Guard

```python
match value:
    case x if x > 0:
        print(f"positive: {x}")
    case x if x < 0:
        print(f"negative: {x}")
    case _:
        print("zero")
```

### Guards on Type Patterns

```python
match value:
    case int() as n if n > 0:
        print(f"positive int: {n}")
    case str() as s if len(s) > 0:
        print(f"non-empty string: {s}")
```

### Per-Alternative Guards

Guards can be applied to individual alternatives within an or-pattern:

```python
match value:
    case Foo(x) if x > 0 | Bar(y) if y < 0:
        print("matched")
```

Each alternative in the or-pattern can have its own guard condition. This maps to C# `when` clauses on individual arms of a disjunctive pattern.

### Guard vs Exhaustiveness

Guards make patterns conditional, so a guarded pattern is **not** considered exhaustive — even `case _ if condition:` does not cover all values. For exhaustive matching, include an unguarded wildcard or binding pattern:

```python
match value:
    case _ if value > 0:
        print("positive")
    case _:              # Unguarded wildcard ensures exhaustiveness
        print("non-positive")
```

*Implementation*
- *✅ Implemented — `GuardPattern` AST node with `Inner` (pattern) and `Guard` (expression) properties*
- *Maps to C# `when` clause on `case` arms*
- *Guards do not affect exhaustiveness analysis*

## Exhaustiveness Checking

The `ExhaustivenessValidator` checks that `match` statements and expressions cover all possible cases.

**Checked Types (Finite):**

| Type | Requirement | Diagnostic |
|------|-------------|------------|
| `bool` | Must cover `True` and `False` | SPY0463 (warning) |
| Tagged unions | All cases must be covered | SPY0463 (warning) |
| Enums | All enum values must be covered | SPY0463 (warning) |

**Non-Finite Types (int, str, etc.):**

| Form | Requirement | Diagnostic |
|------|-------------|------------|
| Match expression | Must have at least one unconditionally exhaustive arm (wildcard `_` or binding pattern without guard) | SPY0416 (error) |
| Match statement | Should have a wildcard or binding arm for safety | SPY0463 (warning) |

> **Note:** Match expressions produce SPY0416 errors (not warnings) because a missing arm results in a runtime `SwitchExpressionException`. Match statements produce SPY0463 warnings since the code simply falls through.

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

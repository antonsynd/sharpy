# Variable Scoping Rules

## No `global` or `nonlocal` Keywords

Sharpy does not support Python's `global` or `nonlocal` keywords. This aligns with C# scoping semantics:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
global x       # ERROR: unexpected 'global'
nonlocal y     # ERROR: unexpected 'nonlocal'
```

To modify outer scope variables, use explicit assignment to a mutable container or return values from functions.

## Block Scoping

Sharpy uses C#-style block scoping: **all compound statement bodies introduce a new scope**. Variables declared inside a block are not visible outside it. This is a deliberate departure from Python, where variables leak out of most blocks.

**Block-Scoped Compound Statements** (variables declared inside don't leak):
- `if` / `elif` / `else` bodies
- `while` body
- `for` body (including the loop variable itself)
- `try` body
- `except` body (including the `as` binding)
- `else` body (in `try`/`except`/`else`)
- `finally` body
- `with` body
- Comprehensions (including walrus assignments inside comprehensions)

**Note on `try`/`except`/`else`/`finally`**: Variables declared in the `try` body are **not** visible in `except`, `else`, or `finally` handlers. If a variable must be accessible across all clauses, declare it before the `try` statement:

```python
result: int = 0
try:
    result = risky_operation()
except ValueError as e:
    print(f"Failed: {e}")
finally:
    print(f"Result was: {result}")
```

**Containing-Scope Constructs** (variable persists):
- Declarations in a function body or module top-level (outside any compound statement)
- Walrus operator (`x := value`) in non-block contexts - see [Walrus Operator](walrus_operator.md)

**Walrus Operator Scoping:**

The walrus operator (`:=`) assigns to the *containing scope*. In most cases this is the enclosing function or module. However, inside block-scoped constructs like comprehensions, the walrus variable is scoped to that block:

```python
# Walrus in if-statement: variable persists in containing scope
if (match := pattern.search(text)) is not None:
    print(match)  # OK
print(match)      # OK - walrus assigned in containing scope

# Walrus in comprehension: variable is comprehension-local
results = [y * 2 for x in items if (y := transform(x)) > 0]
print(y)          # ERROR: 'y' does not exist in this scope
```

**Note:** This differs from Python 3.8+, where walrus in comprehensions leaks to the outer scope. In Sharpy, the syntactic boundary equals the semantic boundary—comprehension delimiters (`[...]`, `{...}`) which fully contain all variables declared within.

### Example

```python
x = "outer"

for x in range(5):      # New 'x' shadows outer, block-scoped
    print(x)            # Prints 0, 1, 2, 3, 4

print(x)                # Prints "outer", 'x' was shadowed only
                        # in the for-loop, and not modified.
```

### To modify outer variable

```python
x = 0
for i in range(5):      # 'i' is block-scoped
    x += i              # Modifies outer 'x'
print(x)                # 10
print(i)                # ERROR: 'i' is block-scoped
```

## Assignment Statement

```python
# Simple assignment
x = 10

# Multiple assignment (unpacking)
x, y = 10, 20

# Augmented assignment
x += 5
count *= 2
```

## Variable Shadowing

Variables can be redeclared in the same scope with a different type using explicit type annotation:

```python
x: int = 5              # Initial declaration
x = 10                  # Assignment (same type)
x: str = "hello"        # Shadowing (new type, requires annotation)

# With auto keyword for type inference
x: int = 5
x: auto = "hello"       # Shadowing with inferred type
```

*Implementation:*
- *🔄 Lowered - Generates variable names using simple integer versioning (`x`, `x_1`, `x_2`, etc.) via the `_variableVersions` dictionary in RoslynEmitter.*

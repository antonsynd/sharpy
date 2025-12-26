# Variable Scoping Rules

## No `global` or `nonlocal` Keywords

Sharpy does not support Python's `global` or `nonlocal` keywords. This aligns with C# scoping semantics:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
global x       # ERROR: unexpected 'global'
nonlocal y     # ERROR: unexpected 'nonlocal'
```

To modify outer scope variables, use explicit assignment to a mutable container or return values from functions.

## Block-Scoped vs Containing-Scope Constructs

**Block-Scoped Constructs** (variable doesn't leak):
- For loop variables
- Comprehension variables
- Exception binding (`except E as e`)

**Containing-Scope Constructs** (variable persists):
- Regular declarations (`x = value`, `x: type = value`)

Note that if Sharpy supported the walrus operator `:=`, it would fall under the containing-scope constructs category. Right now, Sharpy does not and there are no plans to include it.

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
- *🔄 Lowered - Generates variable names (`x`, `x_1_...`, `x_2_...`). The versioned variable names are appended with UUIDs to prevent the user from predicting the internal names and referencing them inadvertently.*

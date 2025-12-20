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
- Walrus operator (`x := value`)

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
- 🔄 Lowered - Generates variable names (`x`, `x_1_...`, `x_2_...`). The versioned
variable names are appended with UUIDs to prevent the user from predicting the
internal names and referencing them inadvertently.

## Variable Declaration and Assignment

Variables in Sharpy must be declared and assigned in a single statement. There are three syntactic forms:

| Form | Syntax | Type Determination |
|------|--------|-------------------|
| Explicit type | `x: int = 5` | Type annotation specifies type |
| Inferred type | `x = 5` | Type inferred from initializer |
| Explicit inference | `x: auto = 5` | Type inferred from initializer (explicit) |

**Form 1: Explicit Type Annotation**

The type is explicitly specified:

```python
count: int = 0
name: str = "Alice"
items: list[int] = [1, 2, 3]
user: User? = None
```

**Form 2: Type Inference (Implicit)**

The type is inferred from the initializer expression:

```python
count = 0              # Inferred as int
name = "Alice"         # Inferred as str
items = [1, 2, 3]      # Inferred as list[int]
pi = 3.14159           # Inferred as double
```

**Form 3: Type Inference (Explicit with `auto`)**

The `auto` keyword explicitly requests type inference. This is functionally equivalent to Form 2 but makes the inference explicit:

```python
count: auto = 0        # Inferred as int
name: auto = "Alice"   # Inferred as str
items: auto = [1, 2, 3]  # Inferred as list[int]
```

**When to Use `auto`:**

The `auto` keyword is primarily useful for variable shadowing, where you want to redeclare a variable with a different type:

```python
x: int = 5
x = 10                 # Assignment to existing int variable
x: str = "hello"       # Shadowing: new variable of type str
x: auto = [1, 2, 3]    # Shadowing: new variable, type inferred as list[int]
```

## No Declaration Without Assignment

Unlike some languages, Sharpy does not allow variable declarations without initialization:

```python
# ❌ Invalid - no declaration without assignment
x: int                 # ERROR: variable declaration requires initializer
name: str              # ERROR: variable declaration requires initializer

# ✅ Valid - always provide initializer
x: int = 0
name: str = ""
items: list[int] = []
user: User? = None
```

**Exception: Class Instance Fields**

Class and struct fields can be declared without initialization if they are assigned in `__init__`.

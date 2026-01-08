# Variable Declaration and Assignment

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
pi = 3.14159           # Inferred as float
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

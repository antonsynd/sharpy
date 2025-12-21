# Statements

## Expression Statement

Any expression can be a statement:

```python
print("Hello")
obj.method()
list.append(item)
```

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

Class and struct fields can be declared without initialization if they are assigned in `__init__`:

```python
class Person:
    # Field declarations (no initializer required)
    name: str
    age: int

    # Optional: fields with default values
    active: bool = True

    def __init__(self, name: str, age: int):
        # All fields without defaults must be assigned in __init__
        self.name = name
        self.age = age
```

## No `let` or `var` Keywords

Sharpy does not use `let`, `var`, or similar keywords for variable declaration. The three forms above are the only ways to declare variables:

```python
# ❌ Invalid - these keywords don't exist in Sharpy
let x = 5              # ERROR: unexpected 'let'
var y = 10             # ERROR: unexpected 'var'
val z = 15             # ERROR: unexpected 'val'

# ✅ Valid
x = 5                  # Type inferred
y: int = 10            # Type explicit
z: auto = 15           # Type inferred (explicit)
```

## Constants

Constants are declared with `const` and must have a compile-time constant initializer:

```python
# Module-level constants
const PI: double = 3.14159
const MAX_SIZE: int = 1000
const APP_NAME = "MyApp"       # Type inferred as str
const DEBUG: bool = True
```

**Class-Level Constants:**

Constants can also be declared within classes. Class-level constants are implicitly `@static` (matching C# semantics where class constants are always static):

```python
class Math:
    const PI: double = 3.14159265358979
    const E: double = 2.71828182845904
    const TAU: double = 6.28318530717958

    @static
    def circle_area(radius: double) -> double:
        return Math.PI * radius ** 2

class HttpStatus:
    const OK: int = 200
    const NOT_FOUND: int = 404
    const INTERNAL_ERROR: int = 500

# Access via class name (constants are implicitly static)
print(Math.PI)           # 3.14159265358979
print(HttpStatus.OK)     # 200

# Cannot access via instance (they're static, not per-instance)
m = Math()
print(m.PI)              # Works but discouraged; prefer Math.PI
```

**Note:** There is no such thing as a per-instance constant. Use a read-only property (`property get`) with a backing field or `@final` field (if added in a future version) for per-instance immutability.

Constants cannot be reassigned:

```python
const X: int = 5
X = 10                 # ERROR: cannot assign to constant
```

*Implementation*
- *✅ Native - Direct mapping to C# variable declarations and `const`.*

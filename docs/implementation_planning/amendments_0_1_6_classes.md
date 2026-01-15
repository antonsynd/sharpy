# Amendments for Phase 0.1.6: Classes

**Review Date:** 2026-01-14  
**Reviewed Against:** Language Specification (classes.md, constructors.md, dunder_methods.md, name_mangling.md, static_methods.md)  
**Axiom Priority:** Axiom 1 (.NET Runtime) > Axiom 2 (Python Surface) > Axiom 3 (Static & Null-Safe)

---

## Amendment 1: Constructor Overloading Support

**Section:** Task 0.1.6.4 (`__init__` Code Generation)

**Issue:** The task list does not mention constructor overloading, but the spec explicitly supports it.

**Spec Reference (constructors.md):**
```python
class Point:
    x: float
    y: float

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        self.x = other.x
        self.y = other.y
```

**Required Addition to Task 0.1.6.4:**
- Add support for multiple `__init__` methods (constructor overloading)
- Each overload generates a separate C# constructor
- This is a native C# feature (Axiom 1 alignment)

---

## Amendment 2: Constructor Chaining (`self.__init__()`)

**Section:** Task 0.1.6.4 (`__init__` Code Generation)

**Issue:** The task list does not cover constructor chaining via `self.__init__(...)`.

**Spec Reference (constructors.md):**
```python
class Point:
    x: float
    y: float

    def __init__(self):
        self.__init__(0.0, 0.0)  # Chains to two-parameter constructor

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
```

**Generated C#:**
```csharp
public Point() : this(0.0, 0.0) { }

public Point(double x, double y)
{
    X = x;
    Y = y;
}
```

**Required Addition:**
- Detect `self.__init__(...)` as **first statement** in `__init__`
- Transform to C# constructor initializer (`: this(...)`) 
- Not an in-body call; must be in constructor declaration syntax
- Only one `self.__init__()` call allowed per constructor

---

## Amendment 3: `__init__` Direct Call Restriction

**Section:** Task 0.1.6.3 (Semantic Analysis)

**Issue:** The task list doesn't mention that `__init__` cannot be called directly by users.

**Spec Reference (dunder_methods.md):**
```python
a = Foobar()  # OK: Allowed, implicitly invokes __init__
a.__init__()  # ERROR: Not allowed in Sharpy (but allowed in Python)
```

**Required Addition:**
- Add semantic validation rule: `__init__` cannot be called directly on instances
- Exception: Within `__init__` for constructor chaining (`self.__init__(...)` or `super().__init__(...)`)
- Generate error: "Constructor `__init__` cannot be called directly; use class instantiation syntax"

---

## Amendment 4: Static Method Detection Clarification

**Section:** Task 0.1.6.7 (Static Method Detection)

**Issue:** There's a contradiction in the specification regarding `@static` decorator.

**Specification Analysis:**
1. `static_methods.md` says: "Static methods... do not have a `self` parameter. This is how the compiler distinguishes between static methods and instance methods."
2. `decorators.md` shows `@static` as a valid decorator: `| @static | static | Class-level method, no self parameter |`
3. `phases.md` (0.1.7) says: "There is no `@static` decorator."

**Decision (Per 3 Axioms):**
- **Primary mechanism:** Absence of `self` parameter makes method static (Axiom 2 - Pythonic)
- **`@static` decorator:** Valid for properties (`@static property get name...`), but NOT required for methods
- Methods without `self` ARE static; `@static` decorator is optional/redundant for methods

**Required Clarification in Task 0.1.6.7:**
- Methods without `self` → automatically generate as `static`
- `@static` decorator on methods → same effect, but optional
- **Validation:** If method has both `self` AND `@static`, produce error

**Example:**
```python
class Math:
    # Both are static methods:
    def square(x: int) -> int:        # Static (no self)
        return x * x
    
    @static
    def cube(x: int) -> int:          # Also static (explicit)
        return x * x * x
    
    @static
    def broken(self, x: int) -> int:  # ERROR: Cannot have both
        return x
```

---

## Amendment 5: Field Name Mangling Correction

**Section:** Task 0.1.6.8 (Name Mangling)

**Issue:** The task list shows inconsistent field naming rules.

**Task List States:**
| Sharpy | C# |
|--------|-----|
| `snake_case` fields | `PascalCase` |
| `_private_field` | `_privateField` (private) |

**Spec Reference (name_mangling.md):**
| Identifier Type | Convention | Example |
|-----------------|------------|---------|
| Private field | _camelCase | `_data_store` → `_dataStore` |
| Public property | PascalCase | `property user_name` → `UserName` |

**Correction:**
- **Public fields:** `snake_case` → `PascalCase` (e.g., `user_count` → `UserCount`) ✓
- **Private fields** (single underscore): `_snake_case` → `_camelCase` (e.g., `_user_count` → `_userCount`) ✓

The task list is correct. No change needed, but ensure implementation follows this pattern.

---

## Amendment 6: Code Example Fix - Distance Method

**Section:** Task 0.1.6.6 (Instance Method Code Generation)

**Issue:** The generated C# code uses `Math.Pow` incorrectly for exponentiation.

**Task List Example:**
```python
def distance_from_origin(self) -> float:
    return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Task List Generated C# (Incorrect):**
```csharp
public double DistanceFromOrigin()
{
    return Math.Pow(X * X + Y * Y, 0.5);
}
```

**Correct Generated C#:**
```csharp
public double DistanceFromOrigin()
{
    return Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));
    // Or more efficiently:
    return Math.Sqrt(X * X + Y * Y);
}
```

**Note:** `x ** 2` should use `x * x` for simple integer powers, not `Math.Pow(X * X + Y * Y, 0.5)`. The `** 0.5` operation should map to `Math.Sqrt()`.

---

## Amendment 7: `__init__` Return Type Clarification

**Section:** Task 0.1.6.2 (Parsing), Task 0.1.6.4 (Code Generation)

**Issue:** The task list doesn't specify that `__init__` return type is implicitly `None`.

**Spec Reference (classes.md):**
```python
class Person:
    name: str

    # Both forms are valid and equivalent:
    def __init__(self, name: str):           # Implicit None return
        self.name = name

    def __init__(self, name: str) -> None:   # Explicit None return
        self.name = name
```

**Required Addition:**
- Parser should accept `__init__` with or without `-> None` return type
- Both forms are semantically equivalent
- Generated C# constructor has no return type (standard C# behavior)

---

## Amendment 8: Self Parameter Type Annotation Prohibition

**Section:** Task 0.1.6.2 (Parsing), Task 0.1.6.3 (Semantic Analysis)

**Issue:** The task list doesn't mention that `self` cannot have a type annotation.

**Spec Reference (classes.md):**
- "The `self` parameter is not type-annotated and cannot be annotated"
- "There is no `Self` type in Sharpy currently (C# 9.0 has no equivalent)"

**Required Addition:**
- Semantic validation: If `self` parameter has type annotation, produce error
- Error message: "`self` parameter cannot have a type annotation"

**Example:**
```python
class Person:
    def greet(self: Person) -> str:  # ERROR: self cannot be annotated
        return "Hello"
```

---

## Amendment 9: Field Declaration Requirement

**Section:** Task 0.1.6.5 (Field Code Generation)

**Issue:** The task list doesn't emphasize that all fields must be declared at class level.

**Spec Reference (classes.md):**
- "All instance fields must be declared at class level with type annotations or an assignment of a default value where the type can be inferred"

**Required Addition to Semantic Validation:**
- Validate that fields accessed via `self.x` are declared at class level
- If `self.new_field = value` in method and `new_field` not declared at class level → Error
- Error message: "Field 'new_field' must be declared at class level before use"

**Example:**
```python
class Bad:
    def __init__(self):
        self.undeclared = 5  # ERROR: 'undeclared' not declared at class level

class Good:
    undeclared: int  # Declaration at class level
    
    def __init__(self):
        self.undeclared = 5  # OK
```

---

## Amendment 10: Integration Test - SimpleClass Missing Constructor

**Section:** Task 0.1.6.10 (Integration Tests)

**Issue:** The `SimpleClass_CompilesAndRuns` test uses `Point()` but Point has no `__init__`.

**Task List Test:**
```python
class Point:
    x: int
    y: int

p = Point()  # Works due to default parameterless constructor
p.x = 10
p.y = 20
```

**Clarification:** This is actually correct behavior. Classes without explicit `__init__` get an implicit parameterless constructor that initializes fields to their default values (0 for int). This matches C# behavior. No change needed, but add comment explaining this.

---

## Summary of Required Changes

| Amendment | Type | Priority | Effort |
|-----------|------|----------|--------|
| 1. Constructor Overloading | Feature | High | 2h |
| 2. Constructor Chaining | Feature | High | 2h |
| 3. `__init__` Direct Call Restriction | Validation | Medium | 1h |
| 4. Static Method Detection | Clarification | High | 0.5h |
| 5. Field Name Mangling | No Change | - | - |
| 6. Code Example Fix | Doc Fix | Low | 0.25h |
| 7. `__init__` Return Type | Clarification | Low | 0.5h |
| 8. Self Parameter Validation | Validation | Medium | 0.5h |
| 9. Field Declaration Requirement | Validation | Medium | 1h |
| 10. Test Comment | Doc Fix | Low | 0.25h |

**Total Additional Effort Estimate:** ~8 hours

# Amendments for Phase 0.1.7: Inheritance & Interfaces

**Review Date:** 2026-01-14  
**Reviewed Against:** Language Specification (inheritance.md, decorators.md, interfaces.md, properties_inheritance.md)  
**Axiom Priority:** Axiom 1 (.NET Runtime) > Axiom 2 (Python Surface) > Axiom 3 (Static & Null-Safe)

---

## Amendment 1: `super()` in Dunder Methods - Extended Rules

**Section:** Task 0.1.7.3 (super() Semantic Validation)

**Issue:** The validation table is incomplete regarding `super()` in dunder methods.

**Task List States:**
| Context | Allowed `super()` Calls |
|---------|------------------------|
| Dunder method | `super().__same_dunder__(...)` |

**Spec Reference (inheritance.md):** `super()` in dunder methods can call:
1. The **same** dunder on parent: `super().__eq__(other)` in `__eq__`
2. **Other** dunders for cross-dunder synthesis: `self.__lt__(other)` or `super().__lt__(other)`

**Required Correction:**
```python
class Child(Parent):
    @override
    def __le__(self, other: Child) -> bool:
        # Both of these are valid:
        return self.__lt__(other) or self.__eq__(other)   # OK: cross-dunder on self
        return super().__lt__(other) or super().__eq__(other)  # OK: cross-dunder via super
```

**Updated Validation Table:**
| Context | Allowed `super()` Calls |
|---------|------------------------|
| `__init__` | `super().__init__(...)` only |
| `@override` method | `super().same_method_name(...)` |
| Dunder method | `super().__any_dunder__(...)` (same or cross-dunder) |
| Regular method | ❌ ERROR |
| Free function | ❌ ERROR |

---

## Amendment 2: `super()` Chaining Prohibition

**Section:** Task 0.1.7.3 (super() Semantic Validation)

**Issue:** The task list doesn't mention that `super()` cannot be chained.

**Spec Reference (inheritance.md):**
```python
class C(B):
    @override
    def process(self) -> str:
        b_result = super().process()         # OK: immediate parent (B)
        a_result = super().super().process() # ERROR: Cannot chain super()
```

**Required Addition:**
- Validate: `super()` cannot be followed by another `super()`
- `super()` only accesses immediate parent, not grandparents
- Error message: "`super()` cannot be chained to access ancestor classes further up the inheritance hierarchy"

---

## Amendment 3: `@public` Decorator Clarification

**Section:** Task 0.1.7.6 (Decorator Code Generation)

**Issue:** The task list shows `@public` handling in the code example, but spec says there's no `@public`.

**Task List Code (lines 476-478):**
```csharp
case "public":
    tokens.Add(Token(SyntaxKind.PublicKeyword));
    hasAccessModifier = true;
```

**Spec Reference (decorators.md):**
- "(default) | `public` | Everyone"
- "There is no `@public` decorator; public is the default visibility."

**Decision (Per Axiom 2 - Python Surface):**
- `@public` decorator is NOT recognized
- Public is the default when no access modifier is present
- If a user writes `@public`, treat it as unknown decorator (warning or error)

**Required Change:**
Remove `@public` case from decorator handling. Only valid access modifiers are:
- `@private`
- `@protected` 
- `@internal`

---

## Amendment 4: `@final` Method Requires `@override`

**Section:** Task 0.1.7.6 (Decorator Code Generation)

**Issue:** The code example shows `@final` generating `sealed override`, but doesn't validate requirements.

**Task List Code (lines 502-508):**
```csharp
case "final":
    if (isClass)
        tokens.Add(Token(SyntaxKind.SealedKeyword));
    else
        tokens.AddRange(new[] {
            Token(SyntaxKind.SealedKeyword),
            Token(SyntaxKind.OverrideKeyword)
        });
```

**Spec Behavior:**
- `@final` on a class → `sealed class`
- `@final` on a method → `sealed override` (method must already be overriding something)

**Required Addition:**
- Semantic validation: `@final` on a method requires the method to be overriding a virtual/abstract method
- If `@final` on method but method doesn't override anything → Error
- Error message: "`@final` on a method requires the method to override a virtual or abstract base method"

**Correct Usage:**
```python
class Parent:
    @virtual
    def speak(self) -> str:
        return "Parent"

class Child(Parent):
    @final
    @override
    def speak(self) -> str:  # OK: overrides virtual method
        return "Child"
```

---

## Amendment 5: Interface Syntax Clarification

**Section:** Task 0.1.7.8 (Interface Definition Parsing)

**Issue:** Task list shows interface methods with `...` body, but doesn't clarify relationship to abstract.

**Task List Example:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...
```

**Spec Reference:**
- Interface methods are implicitly abstract
- `...` body indicates abstract/no implementation
- No `@abstract` decorator needed on interface methods

**Required Clarification:**
- Interface methods MUST have `...` body (no implementation)
- Interface methods do NOT require/allow `@abstract` decorator (they're abstract by definition)
- Semantic validation: Interface methods with actual body → Error

**Example:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...  # OK: abstract (implicit)
    
    def invalid(self) -> None:
        print("test")  # ERROR: Interface methods cannot have implementation
```

---

## Amendment 6: Interface Default Methods (C# 8+)

**Section:** Task 0.1.7.8, Task 0.1.7.9 (Interface Parsing and Code Generation)

**Issue:** The task list doesn't address C# 8+ default interface implementations.

**Spec Reference (properties_inheritance.md):**
```python
interface IConfigurable:
    property name: str = ""    # Default value - implementer can override or use default
    property enabled: bool = True
```

**Decision (Per Axiom 1 - .NET Runtime C# 9.0 Compatibility):**
- C# 9.0 supports default interface implementations
- Sharpy can support interface methods with default implementations
- **However:** For Phase 0.1.7, limit to abstract interface methods only
- Default interface implementations can be added in a later phase

**Required Note:**
Add task note: "Default interface method implementations are deferred to a future phase. All interface methods in 0.1.7 must be abstract (use `...` body)."

---

## Amendment 7: Interface Property Support

**Section:** Task 0.1.7.8, Task 0.1.7.9 (Interface Definition)

**Issue:** Task list only shows method signatures in interfaces, but spec supports properties.

**Spec Reference (properties_inheritance.md):**
```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter

interface IConfigurable:
    property name: str = ""    # With default value
```

**Required Addition:**
- Interfaces can declare property requirements
- Properties in interfaces follow same rules as interface methods
- Auto-properties with no default → must be implemented
- Auto-properties with default → can be overridden

**Phase 0.1.7 Scope Decision:**
- Include abstract interface properties: `property get name: T`
- Defer properties with default values to later phase

---

## Amendment 8: Explicit Interface Implementation

**Section:** Task 0.1.7.10 (Interface Implementation Code Generation)

**Issue:** Task list doesn't cover explicit interface implementation.

**Spec Reference (properties_inheritance.md):**
```python
interface ISecret:
    property get value: str

class SecretHolder(ISecret):
    # Explicit interface implementation
    property get ISecret.value(self) -> str:
        return self._secret
```

**Generated C#:**
```csharp
string ISecret.Value => _secret;
```

**Phase Decision:**
- Explicit interface implementation is an advanced feature
- Defer to a later phase (0.1.7 focuses on basic implementation)
- Add note: "Explicit interface implementation (`IFace.method_name`) deferred to future phase"

---

## Amendment 9: `@virtual` Requirement for Override

**Section:** Task 0.1.7.6 (Decorator Code Generation), Task 0.1.7.7 (Abstract Validation)

**Issue:** Task list doesn't validate that methods being overridden must be `@virtual` or `@abstract`.

**Required Validation:**
1. Method with `@override` must have a corresponding `@virtual` or `@abstract` method in base class
2. Overriding a non-virtual method → Error

**Error Cases:**
```python
class Parent:
    def not_virtual(self) -> str:  # No @virtual
        return "Parent"

class Child(Parent):
    @override
    def not_virtual(self) -> str:  # ERROR: Cannot override non-virtual method
        return "Child"
```

**Error Message:** "Method 'not_virtual' cannot be overridden because it is not marked @virtual or @abstract in the base class"

---

## Amendment 10: Test Fix - Missing `__init__` in Dog Class

**Section:** Task 0.1.7.11 (Integration Tests)

**Issue:** Test `VirtualOverride_Works` creates `Dog()` without constructor, but Dog has no parameterless `__init__`.

**Task List Test (line 941-949):**
```python
class Animal:
    @virtual
    def speak(self) -> str:
        return '...'

class Dog(Animal):
    @override
    def speak(self) -> str:
        return 'Woof!'

d = Dog()  # Works if Dog inherits Animal's implicit constructor
```

**Clarification:** This is valid IF:
1. Animal has no explicit `__init__` (uses implicit parameterless)
2. Dog doesn't need to initialize any fields

The test is correct for basic inheritance. However, the first inheritance test (`SingleInheritance_CompilesAndRuns`) shows a more complete example with `__init__`. No change needed, but ensure test reflects intended behavior.

---

## Amendment 11: Dunder Method Override Rules

**Section:** Task 0.1.7.6 (Decorator Code Generation)

**Issue:** The task list shows `__str__` with `@override` but doesn't explain dunder method override rules.

**Spec Behavior:**
- Dunder methods like `__str__`, `__eq__`, etc. that override base class methods need `@override`
- `super()` can be called within dunder methods to access parent implementation

**Example from spec:**
```python
class Calculator:
    @override
    def __str__(self) -> str:
        return "Calculator"

class ScientificCalculator(Calculator):
    @final
    @override
    def __str__(self) -> str:
        return "ScientificCalculator"
```

**Required Clarification:**
- Dunder methods overriding Object methods (`__str__`, `__eq__`, `__hash__`, etc.) require `@override`
- This is consistent with C#'s requirement to use `override` for `ToString()`, `Equals()`, `GetHashCode()`

---

## Amendment 12: Super in Constructor - Code Generation Detail

**Section:** Task 0.1.7.4 (Inheritance Code Generation)

**Issue:** The code generation example doesn't show the correct C# syntax for `super().__init__()` as first statement.

**Task List Shows (lines 319-329):**
```python
def __init__(self, name: str, breed: str):
    super().__init__(name)
    self.breed = breed
```

**Task List Generated C# (lines 325-328):**
```csharp
public Dog(string name, string breed) : base(name)
{
    Breed = breed;
}
```

**Clarification:** The task list is **correct**. When `super().__init__(...)` is the **first statement**, it becomes a constructor initializer (`: base(...)`), not an in-body call. This is important and correctly documented.

**Edge Case Not Covered:**
```python
def __init__(self, name: str, breed: str):
    if condition:
        super().__init__(name)  # ERROR: Must be first statement unconditionally
```

**Required Addition:**
- Validate: `super().__init__()` must be unconditionally the first statement
- Cannot be inside `if`, `try`, or other control flow
- Error message: "`super().__init__()` must be the first statement in the constructor, not inside control flow"

---

## Summary of Required Changes

| Amendment | Type | Priority | Effort |
|-----------|------|----------|--------|
| 1. Dunder Cross-Super Rules | Clarification | Medium | 0.5h |
| 2. Super Chaining Prohibition | Validation | High | 0.5h |
| 3. Remove @public | Bug Fix | High | 0.25h |
| 4. @final Requires Override | Validation | High | 0.5h |
| 5. Interface Method Body | Validation | Medium | 0.5h |
| 6. Default Interface Methods | Scope Note | Low | - |
| 7. Interface Properties | Feature Note | Medium | - |
| 8. Explicit Interface Impl | Scope Note | Low | - |
| 9. Virtual Required for Override | Validation | High | 1h |
| 10. Test Comment | No Change | - | - |
| 11. Dunder Override Rules | Clarification | Medium | 0.25h |
| 12. Super First Statement | Validation | High | 1h |

**Total Additional Effort Estimate:** ~4.5 hours

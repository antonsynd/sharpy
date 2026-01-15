# Amendments for Phase 0.1.9: Type System Enhancements

**Review Date:** 2026-01-14  
**Reviewed Against:** Language Specification (types.md, nullable_types.md, generics.md, type_aliases.md, grammar.ebnf.txt)  
**Axiom Priority:** Axiom 1 (.NET Runtime) > Axiom 2 (Python Surface) > Axiom 3 (Static & Null-Safe)

---

## Amendment 1: Nullable Reference Type Semantics

**Section:** Task 0.1.9.1 (Nullable Type Parsing), Task 0.1.9.2 (Code Generation)

**Issue:** The task list correctly distinguishes value vs reference nullable types but doesn't explain C# nullable reference type implications.

**C# 9.0 Nullable Reference Types:**
- With `#nullable enable`, `string?` indicates nullable reference
- Without it, `string` and `string?` are equivalent at runtime
- Sharpy should enable nullable reference types by default

**Required Addition:**
Generated C# should include:
```csharp
#nullable enable

// For nullable reference types
string? name = null;  // Nullable reference (C# 8+)

// For nullable value types
int? count = null;    // Nullable<int>
```

**Axiom 3 Decision:**
- Enable `#nullable enable` in all generated C# files
- Non-nullable types are the default (per Sharpy's "null-safe by default" principle)
- `T?` explicitly opts into nullability

---

## Amendment 2: Null Conditional Result Type

**Section:** Task 0.1.9.4 (Null Conditional Operator)

**Issue:** The task list notes (line 1179) say result is nullable but doesn't explain type inference.

**Spec Behavior:**
```python
name: str? = "hello"
upper = name?.upper()  # upper is str? (not str!)
```

**Type Inference Rules:**
- `T?.member` where `member` has type `U` → result type is `U?`
- `T?.method()` where `method()` returns `U` → result type is `U?`
- Chained: `T?.a?.b` → result type is `typeof(b)?`

**Required Addition to Task 0.1.9.4:**
- Type checker must wrap result type in nullable
- Even if the method returns non-nullable, null conditional makes it nullable

**Example:**
```python
class Person:
    def get_name(self) -> str:  # Returns non-nullable str
        return self.name

p: Person? = None
n = p?.get_name()  # n is str? (nullable) because p might be None
```

---

## Amendment 3: Generic Syntax Uses Square Brackets

**Section:** Task 0.1.9.9 (Generic Parsing)

**Issue:** Task list correctly uses `[T]` but should emphasize this differs from C#.

**Sharpy Syntax:**
```python
class Box[T]:
    value: T

box = Box[int](42)
```

**C# Syntax:**
```csharp
public class Box<T>
{
    public T Value;
}

var box = new Box<int>(42);
```

**Required Transformation:**
- Parser: `[T]` → AST TypeParameters
- CodeGen: AST TypeParameters → C# `<T>`

**Grammar Reference:**
```ebnf
type_params ::= '[' type_param { ',' type_param } [ ',' ] ']'
```

---

## Amendment 4: Type Constraint Syntax

**Section:** Task 0.1.9.12 (Type Constraints)

**Issue:** Task list shows `:` syntax but doesn't cover all constraint types.

**Sharpy Constraint Syntax:**
```python
def find_max[T: IComparable](a: T, b: T) -> T:  # Interface constraint
    ...

class Container[T: class](value: T):  # Reference type constraint
    ...

struct Wrapper[T: struct](value: T):  # Value type constraint
    ...
```

**C# Generated:**
```csharp
public T FindMax<T>(T a, T b) where T : IComparable
public class Container<T> where T : class
public struct Wrapper<T> where T : struct
```

**Constraint Types (per grammar.ebnf.txt):**
```ebnf
type_constraint ::= qualified_name [ '[' type_args ']' ]  # Interface/class constraint
                  | 'class'                                # Reference type constraint
                  | 'struct'                               # Value type constraint
```

**Required Addition:**
- Support `class` keyword as constraint
- Support `struct` keyword as constraint
- Support interface/class name as constraint
- **Not supported yet:** Multiple constraints (`T: IFoo & IBar`)

---

## Amendment 5: Type Alias Expansion Strategy

**Section:** Task 0.1.9.6, Task 0.1.9.7, Task 0.1.9.8 (Type Aliases)

**Issue:** The task list correctly notes aliases are compile-time, but strategy needs clarification.

**Task List Note (line 1171):**
> "Type aliases are compile-time: No C# declaration generated; aliases are expanded at every usage."

**Expansion Strategy:**
```python
type UserId = int
type StringList = list[str]

def get_user(id: UserId) -> StringList:  # Uses aliases
    ...
```

**Before Expansion (AST):**
```
FunctionDef:
  parameters: [{name: "id", type: "UserId"}]
  return_type: "StringList"
```

**After Expansion:**
```
FunctionDef:
  parameters: [{name: "id", type: "int"}]
  return_type: "List<string>"
```

**Required Implementation:**
1. Register aliases in symbol table during first pass
2. Expand aliases during type resolution (before code generation)
3. Generated C# never contains alias names, only expanded types

**Error Cases:**
```python
type Recursive = list[Recursive]  # ERROR: Recursive type alias
type Unknown = NonExistent        # ERROR: Undefined type in alias
```

---

## Amendment 6: Function Type Alias

**Section:** Task 0.1.9.6 (Type Alias Parsing)

**Issue:** Task list shows function type alias but syntax needs clarification.

**Task List Test (lines 987-994):**
```python
type Predicate = (int) -> bool

def is_positive(x: int) -> bool:
    return x > 0

check: Predicate = is_positive
```

**Generated C#:**
```csharp
// Type alias expanded to:
Func<int, bool> check = IsPositive;
```

**Function Type Syntax:**
```python
type UnaryOp = (int) -> int              # Func<int, int>
type BinaryOp = (int, int) -> int        # Func<int, int, int>
type Consumer = (str) -> None            # Action<string>
type Supplier = () -> int                # Func<int>
type Effect = () -> None                 # Action
```

**Mapping Rules:**
- `(T1, T2, ...) -> R` where R is not None → `Func<T1, T2, ..., R>`
- `(T1, T2, ...) -> None` → `Action<T1, T2, ...>`
- `() -> R` where R is not None → `Func<R>`
- `() -> None` → `Action`

---

## Amendment 7: Type Narrowing Scope

**Section:** Task 0.1.9.5 (Type Narrowing)

**Issue:** Task list mentions narrowing in if blocks but doesn't cover all narrowing patterns.

**Spec Patterns for Type Narrowing:**

1. **`is not None` check:**
   ```python
   if x is not None:
       # x is narrowed from T? to T
   ```

2. **`is None` check (inverse narrowing):**
   ```python
   if x is None:
       return
   # After return, x is narrowed to T
   ```

3. **`isinstance` check:**
   ```python
   if isinstance(obj, Dog):
       # obj is narrowed from Animal to Dog
   ```

4. **Pattern matching (future):**
   ```python
   match value:
       case Some(x):
           # x is narrowed to unwrapped type
   ```

**Scope Rules:**
- Narrowing applies only within the narrowing block
- Narrowing does NOT persist after the block ends
- Narrowing in `elif` is independent of previous `if`

**Example:**
```python
def process(value: str?) -> str:
    if value is not None:
        return value.upper()  # value is str here
    # value is str? here (narrowing ended)
    return "empty"
```

---

## Amendment 8: `??=` Null Coalescing Assignment

**Section:** Task 0.1.9.3 (Null Coalescing)

**Issue:** Task list covers `??` but not `??=` operator.

**Sharpy Syntax:**
```python
name: str? = None
name ??= "default"  # Assign only if name is None
```

**Generated C#:**
```csharp
string? name = null;
name ??= "default";
```

**Phase Scope Decision:**
- `??` (null coalescing) → Include in 0.1.9
- `??=` (null coalescing assignment) → Defer to future phase OR include as part of compound assignment operators

**Recommendation:** Include `??=` since it's a simple extension of `??`.

---

## Amendment 9: Generic Variance (Covariance/Contravariance)

**Section:** Task 0.1.9.9, Task 0.1.9.10 (Generics)

**Issue:** Task list doesn't mention variance annotations.

**C# Variance:**
```csharp
interface IReadable<out T> { }   // Covariant
interface IWritable<in T> { }    // Contravariant
```

**Sharpy Support (Future):**
Currently, Sharpy doesn't have syntax for variance. Generics are invariant by default.

**Phase Scope Decision:**
- Phase 0.1.9: Invariant generics only
- Variance annotations (`out`, `in`) deferred to future phase
- Add note: "Generic variance (covariance/contravariance) not supported in 0.1.9"

---

## Amendment 10: Test Fix - Nullable Struct Syntax

**Section:** Task 0.1.9.13 (Integration Tests)

**Issue:** Test uses `Point?` but Point definition isn't shown.

**Context for Test (implied):**
```python
struct Point:
    x: int
    y: int

p: Point? = None  # Nullable struct → Nullable<Point>
```

**Generated C#:**
```csharp
Point? p = null;  // Nullable<Point>
```

**Clarification:** Tests should include struct definition or clarify that `Point` is a class. The nullable behavior differs:
- Struct: `Point?` → `Nullable<Point>` (value type wrapped)
- Class: `Point?` → `Point?` (already reference type, just nullable annotation)

No change needed, but test could be clearer about whether `Point` is struct or class.

---

## Amendment 11: `maybe` Expression

**Section:** Task 0.1.9.4 (Null Conditional)

**Issue:** Spec includes `maybe` keyword for nullable expressions, not mentioned in task list.

**Token Reference (Token.cs line 77):**
```csharp
Maybe,          // Optional from nullable expressions
```

**Spec Behavior (if applicable):**
The `maybe` keyword might be used for nullable handling, but current spec doesn't clearly define its usage.

**Phase Scope Decision:**
- Research `maybe` keyword usage in spec
- If not clearly defined, defer to future phase
- Current focus: `?`, `??`, `?.` operators

---

## Amendment 12: Generic Constraint Validation

**Section:** Task 0.1.9.12 (Type Constraints)

**Issue:** Task list shows constraint syntax but doesn't detail validation rules.

**Required Validations:**

1. **Constraint must exist:**
   ```python
   def foo[T: NonExistent](x: T):  # ERROR: NonExistent is not defined
   ```

2. **Constraint must be interface or class:**
   ```python
   def foo[T: int](x: T):  # ERROR: int is not a valid constraint
   ```

3. **`struct` constraint incompatible with `class`:**
   ```python
   def foo[T: struct & class](x: T):  # ERROR: Cannot be both
   ```

4. **Type argument must satisfy constraint:**
   ```python
   def foo[T: IComparable](x: T):
       ...
   
   foo[int](42)     # OK: int implements IComparable
   foo[object](x)   # ERROR: object doesn't implement IComparable
   ```

**Error Messages:**
- "Type 'NonExistent' used as constraint is not defined"
- "Type 'int' cannot be used as a type constraint. Use an interface or class."
- "Type parameter cannot have both 'struct' and 'class' constraints"
- "Type 'object' does not satisfy constraint 'IComparable'"

---

## Amendment 13: Nested Nullable Types

**Section:** Task 0.1.9.1 (Nullable Type Parsing)

**Issue:** Task list doesn't clarify nested nullable semantics.

**Valid Patterns:**
```python
x: int?                # Nullable int
y: list[int?]          # List of nullable ints
z: list[int]?          # Nullable list of ints
w: list[int?]?         # Nullable list of nullable ints
```

**Invalid Patterns:**
```python
x: int??               # ERROR: Double nullable not meaningful
```

**C# Equivalents:**
```csharp
int? x;
List<int?> y;
List<int>? z;
List<int?>? w;
```

**Required Addition:**
- Parser should accept nested nullable patterns
- Semantic validation: `T??` is an error (double nullable)

---

## Summary of Required Changes

| Amendment | Type | Priority | Effort |
|-----------|------|----------|--------|
| 1. C# Nullable Reference Types | Feature | High | 1h |
| 2. Null Conditional Result Type | Clarification | High | 0.5h |
| 3. Generic Square Bracket Syntax | Clarification | Medium | 0.25h |
| 4. Type Constraint Syntax | Feature | High | 2h |
| 5. Type Alias Expansion | Clarification | High | 1h |
| 6. Function Type Alias | Feature | Medium | 1.5h |
| 7. Type Narrowing Patterns | Clarification | Medium | 0.5h |
| 8. ??= Operator | Feature | Medium | 0.5h |
| 9. Generic Variance | Scope Note | Low | - |
| 10. Test Clarification | Doc Fix | Low | - |
| 11. Maybe Keyword | Research | Low | 0.5h |
| 12. Constraint Validation | Validation | High | 1.5h |
| 13. Nested Nullable | Clarification | Medium | 0.5h |

**Total Additional Effort Estimate:** ~9.75 hours

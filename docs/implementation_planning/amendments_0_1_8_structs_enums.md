# Amendments for Phase 0.1.8: Structs & Enums

**Review Date:** 2026-01-14  
**Reviewed Against:** Language Specification (structs.md, enums.md, name_mangling.md)  
**Axiom Priority:** Axiom 1 (.NET Runtime) > Axiom 2 (Python Surface) > Axiom 3 (Static & Null-Safe)

---

## Amendment 1: Enum Name Mangling Correction

**Section:** Task 0.1.8.6 (Enum Code Generation)

**Issue:** The task list states enum members use `CAPS_SNAKE_CASE` → `PascalCase`, but implementation may use different context.

**Task List States (line 515-522):**
| Sharpy | C# |
|--------|-----|
| `CAPS_SNAKE_CASE` | `PascalCase` |
| `RED` | `Red` |
| `DARK_BLUE` | `DarkBlue` |

**Spec Reference (name_mangling.md):**
| Identifier Type | Convention | Example |
|-----------------|------------|---------|
| Enum value | PascalCase | `RED_COLOR` → `RedColor` |

**Implementation Check (RoslynEmitter.cs):**
```csharp
var memberName = NameMangler.Transform(member.Name, NameContext.Constant);
```

**Issue:** `NameContext.Constant` preserves `MAX_SIZE` as `MAX_SIZE` (based on tests showing `CAPS_SNAKE_CASE` → `CAPS_SNAKE_CASE`).

**Required Correction:**
The spec says enum values should be transformed to `PascalCase`. The implementation should use:
```csharp
var memberName = NameMangler.EnumMemberToPascalCase(member.Name);
// Or create a NameContext.EnumMember that maps to PascalCase
```

**Decision (Per Axiom 1 - .NET Conventions):**
- Enum members in C# typically use `PascalCase`
- `RED` → `Red`
- `DARK_BLUE` → `DarkBlue`
- Align with spec, NOT with current `NameContext.Constant` behavior

---

## Amendment 2: Struct Interface Boxing Warning

**Section:** Task 0.1.8.2 (Struct Code Generation), Task 0.1.8.3 (Validation)

**Issue:** The task list mentions boxing in notes but doesn't add compiler warnings.

**Spec Reference (structs.md):**
> "When a struct is assigned to an interface variable or passed as an interface parameter, the struct is boxed (copied to the heap). For performance-critical code, prefer calling struct methods directly."

**Required Addition:**
Consider adding a compiler **warning** (not error) when:
1. Struct is assigned to interface variable
2. Struct is passed to parameter typed as interface

**Example:**
```python
struct Point(IDescribable):
    x: int
    y: int

p = Point(10, 20)
d: IDescribable = p  # WARNING: Assigning struct to interface causes boxing
```

**Warning Message:** "Assigning struct 'Point' to interface 'IDescribable' causes boxing (heap allocation). Consider using direct struct method calls for performance-critical code."

**Note:** This is a warning, not an error. User can suppress if intentional.

---

## Amendment 3: Struct Field Initialization Requirement

**Section:** Task 0.1.8.3 (Struct Semantic Validation)

**Issue:** The task list mentions constructor must initialize all fields, but doesn't clarify C# 9.0 specific behavior.

**C# 9.0 Struct Behavior:**
- Structs always have an implicit parameterless constructor that zero-initializes
- User-defined constructors must initialize ALL fields before returning
- Cannot partially initialize in C# 9.0

**Spec Reference (structs.md):**
> "When a constructor is defined, it must initialize all fields (C# requirement)"

**Required Validation Enhancement:**
```python
struct Point:
    x: int
    y: int
    z: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
        # ERROR: Field 'z' is not initialized in constructor
```

**Error Message:** "Struct constructor must initialize all fields. Field 'z' is not assigned before the constructor returns."

---

## Amendment 4: Enum String Values - Implementation Note

**Section:** Task 0.1.8.6 (Enum Code Generation)

**Issue:** The spec supports string enums, but implementation differs from integer enums.

**Spec Reference (enums.md):**
```python
enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"
```

**Spec Implementation Note:**
> "String enums: 🔄 Lowered - Static class with string constants"

**Required Clarification:**
String enums cannot use C# `enum` (which only supports integral types). They must be lowered to:

**Generated C#:**
```csharp
public static class HttpMethod
{
    public const string Get = "GET";
    public const string Post = "POST";
    public const string Put = "PUT";
    public const string Delete = "DELETE";
}
```

**Required Addition to Task 0.1.8.6:**
- Detect enum value type (integer vs string)
- Integer enum → C# `enum`
- String enum → C# static class with const string fields
- Mixed types → Error (per spec: "All values must be of the same type")

---

## Amendment 5: Enum Iteration and Properties

**Section:** Task 0.1.8.8 (Enum Usage Code Generation)

**Issue:** The task list doesn't cover `.name` and `.value` properties or iteration.

**Spec Reference (enums.md):**
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# Access underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"

# Iterate over all enum values
for color in Color:
    print(f"{color.name} = {color.value}")
```

**Required Additions:**

1. **`.value` property:** Returns underlying value
   - For int enums: Returns int value
   - For string enums: Returns string value

2. **`.name` property:** Returns enum member name as string
   - Generates `Enum.GetName()` call or lookup

3. **Iteration support:** `for x in EnumType:`
   - Generates `foreach (var x in Enum.GetValues<EnumType>())`

**Generated C# for `.name`:**
```csharp
// favorite.name
Enum.GetName(typeof(Color), favorite)
// Or: favorite.ToString() for the value name
```

**Phase Scope Decision:**
- Include `.value` access (simple cast)
- Include `.name` access (Enum.GetName)
- **Defer** iteration support to later phase (requires collection semantics)

---

## Amendment 6: Enum Explicit Values Requirement - Validation

**Section:** Task 0.1.8.7 (Enum Semantic Validation)

**Issue:** The task list correctly states all values must be explicit, but test shows confusing error.

**Task List Test (lines 1023-1033):**
```python
enum Bad:
    A
    B = 1
```

Expected error: "value" in error message.

**Spec Clarification:**
This should produce a clear error: "Enum member 'A' requires an explicit value. All enum members must have explicit constant values."

**NOT:** Auto-numbering like Python. Sharpy requires explicit values for clarity and to match C# conventions.

---

## Amendment 7: Struct with No Constructor - Default Behavior

**Section:** Task 0.1.8.9 (Struct Default Initialization)

**Issue:** The task list correctly describes behavior but example needs clarification.

**Task List Example (lines 726-744):**
```python
struct Point:
    x: int
    y: int

p1 = Point()  # x = 0, y = 0

struct Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

**C# 9.0 Behavior:**
In C# 9.0, structs **always** have a parameterless constructor that zero-initializes all fields. Even if you define constructors, the parameterless one still exists.

**Verification:** The task list is correct. No change needed.

---

## Amendment 8: Test Syntax - Interface Implementation

**Section:** Task 0.1.8.10 (Integration Tests)

**Issue:** The test uses `struct Point(IDescribable):` syntax which may not be clear.

**Task List Test (lines 860-881):**
```python
interface IDescribable:
    def describe(self) -> str:
        ...

struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'
```

**Clarification:** This syntax is correct. Structs implement interfaces using the same parenthetical syntax as classes. The first item in parentheses for a struct should always be an interface (structs cannot inherit from classes/structs).

**Grammar Reference:**
```ebnf
struct_def ::= 'struct' identifier [ type_params ] [ '(' interface_list ')' ] ':' struct_body
```

No change needed, but add comment clarifying this is interface implementation, not inheritance.

---

## Amendment 9: Struct Cannot Have `@abstract` or `@virtual` Methods

**Section:** Task 0.1.8.3 (Struct Semantic Validation)

**Issue:** The task list doesn't mention that struct methods cannot be virtual/abstract.

**C# Behavior:**
- Structs cannot have virtual methods (they're value types, no polymorphism)
- Structs cannot be abstract
- Structs can implement interfaces (methods become sealed implementations)

**Required Validation:**
```python
struct Bad:
    @virtual
    def method(self) -> int:  # ERROR: Struct methods cannot be virtual
        return 0
    
    @abstract
    def other(self) -> int:   # ERROR: Struct methods cannot be abstract
        ...
```

**Error Messages:**
- "Struct methods cannot be marked @virtual"
- "Struct methods cannot be marked @abstract"
- "Structs cannot be marked @abstract"

---

## Amendment 10: Enum Cannot Have Methods

**Section:** Task 0.1.8.7 (Enum Semantic Validation)

**Issue:** The spec says simple enums cannot have methods, but this validation isn't in the task list.

**Spec Reference (enums.md):**
> "Simple enums (non-tagged unions) cannot have custom methods. For enums with methods, use tagged unions."

**Required Validation:**
```python
enum Color:
    RED = 1
    GREEN = 2
    
    def is_warm(self) -> bool:  # ERROR: Simple enums cannot have methods
        return self == Color.RED
```

**Error Message:** "Simple enums cannot have methods. Use a tagged union for enums with associated methods."

---

## Amendment 11: Code Example Fix - Struct Type Mapping

**Section:** Task 0.1.8.2 (Struct Code Generation)

**Issue:** Example uses `float` type but generated C# shows `double`.

**Task List Example (lines 171-192):**
```python
struct Vector2:
    x: float
    y: float
```

**Generated C#:**
```csharp
public struct Vector2
{
    public double X;  // float → double
    public double Y;
}
```

**Clarification:** This is correct! In Sharpy:
- `float` maps to `double` (C# double, 64-bit)
- `float32` maps to `float` (C# float, 32-bit)

This follows Python convention where `float` is 64-bit. No change needed, but task list could clarify this mapping.

---

## Amendment 12: Struct `ref` and `in` Parameter Semantics

**Section:** Task 0.1.8.4 (Struct Value Semantics)

**Issue:** The task list doesn't mention `ref`/`in` parameters for avoiding struct copies.

**Spec Reference (parameter_modifiers.md, structs.md):**
For large structs, passing by value copies the entire struct. Use:
- `in[T]` for read-only pass by reference (no copy, no mutation)
- `ref[T]` for pass by reference with mutation

**Example:**
```python
def process(data: in[LargeStruct]) -> double:  # No copy, read-only
    return data.value

def modify(data: ref[LargeStruct]):  # No copy, allows mutation
    data.value = 100
```

**Phase Scope Decision:**
- This is covered by parameter modifiers (separate feature)
- Not required for Phase 0.1.8
- Add note: "For performance with large structs, use `in[T]` or `ref[T]` parameters (covered in parameter modifiers)"

---

## Summary of Required Changes

| Amendment | Type | Priority | Effort |
|-----------|------|----------|--------|
| 1. Enum Name Mangling | Bug Fix | High | 1h |
| 2. Struct Boxing Warning | Enhancement | Low | 1h |
| 3. Struct Field Init | Clarification | Medium | 0.25h |
| 4. String Enum Lowering | Feature | High | 2h |
| 5. Enum .name/.value | Feature | Medium | 1.5h |
| 6. Explicit Value Error | Clarification | Low | 0.25h |
| 7. Default Behavior | No Change | - | - |
| 8. Interface Syntax | Clarification | Low | - |
| 9. No Virtual in Struct | Validation | Medium | 0.5h |
| 10. No Methods in Enum | Validation | Medium | 0.5h |
| 11. Type Mapping Note | Clarification | Low | - |
| 12. Ref Parameters | Scope Note | Low | - |

**Total Additional Effort Estimate:** ~7 hours

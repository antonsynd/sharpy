# Phase 0.1.8: Structs & Enums - Detailed Task List

**Goal:** Value types (structs) and enumerations.

**Prerequisites:** Phases 0.1.6-0.1.7 (Classes, Inheritance, Interfaces) must be complete.

**Exit Criteria (from spec):**
- Structs compile to C# `struct`
- Value semantics enforced (copy on assign)
- Enums compile to C# `enum`
- Enum values accessible via dot notation
- Enum comparison works

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for struct-related parsing
grep -rn "struct\|StructDef" src/Sharpy.Compiler/Parser/

# Check for enum-related parsing  
grep -rn "enum\|EnumDef" src/Sharpy.Compiler/Parser/

# Check for struct/enum code generation
grep -rn "StructDeclaration\|EnumDeclaration" src/Sharpy.Compiler/CodeGen/

# Check for existing tests
dotnet test --list-tests | grep -i "struct\|enum"
```

---

## Task 0.1.8.1: Audit/Verify Struct Definition AST and Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

### Objective
Verify that `StructDef` AST node exists and struct definitions are parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Struct Syntax
```python
struct Point:
    x: int
    y: int

struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

### Actions

1. **Verify `StructDef` AST node exists:**
   ```csharp
   public record StructDef : Statement
   {
       public string Name { get; init; } = "";
       public List<TypeAnnotation> Interfaces { get; init; } = new();  // Can implement interfaces
       public List<Statement> Body { get; init; } = new();
       // Source location...
   }
   ```

2. **Verify struct parsing:**
   - [ ] `struct` keyword recognized
   - [ ] Struct name captured
   - [ ] Fields parsed as `VariableDeclaration`
   - [ ] Methods (including `__init__`) parsed as `FunctionDef`

3. **Verify struct cannot inherit from class/struct:**
   ```python
   struct Bad(Point):  # ERROR: Structs cannot inherit
       z: int
   ```
   
4. **Verify struct can implement interfaces:**
   ```python
   interface IDescribable:
       def describe(self) -> str:
           ...
   
   struct Point(IDescribable):
       x: int
       y: int
       
       def describe(self) -> str:
           return f"Point({self.x}, {self.y})"
   ```

### Verification Commands
```bash
# Check StructDef definition
grep -A 20 "record StructDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs

# Run struct parsing tests
dotnet test --filter "Struct" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.8.2: Implement Struct Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Generate C# `struct` declarations from Sharpy struct definitions.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Rules

1. **Basic struct:**
   ```python
   struct Point:
       x: int
       y: int
   ```
   Generates:
   ```csharp
   public struct Point
   {
       public int X;
       public int Y;
   }
   ```

2. **Struct with constructor:**
   ```python
   struct Point:
       x: int
       y: int
       
       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
   ```
   Generates:
   ```csharp
   public struct Point
   {
       public int X;
       public int Y;
       
       public Point(int x, int y)
       {
           X = x;
           Y = y;
       }
   }
   ```

3. **Struct with methods:**
   ```python
   struct Vector2:
       x: float
       y: float
       
       def magnitude(self) -> float:
           return (self.x ** 2 + self.y ** 2) ** 0.5
   ```
   Generates:
   ```csharp
   public struct Vector2
   {
       public double X;
       public double Y;
       
       public double Magnitude()
       {
           return Math.Sqrt(X * X + Y * Y);
       }
   }
   ```

### Implementation Hints

```csharp
private StructDeclarationSyntax GenerateStruct(StructDef structDef)
{
    var structDecl = StructDeclaration(
        Identifier(NameMangler.ToPascalCase(structDef.Name)))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add interface implementations if present
    if (structDef.Interfaces.Count > 0)
    {
        var baseTypes = structDef.Interfaces
            .Select(i => SimpleBaseType(IdentifierName(i.Name)));
        structDecl = structDecl.WithBaseList(
            BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
    }
    
    // Generate members
    var members = new List<MemberDeclarationSyntax>();
    
    foreach (var member in structDef.Body)
    {
        if (member is VariableDeclaration field)
            members.Add(GenerateField(field));
        else if (member is FunctionDef method)
            members.Add(GenerateStructMethod(method, structDef.Name));
    }
    
    return structDecl.WithMembers(List(members));
}
```

---

## Task 0.1.8.3: Implement Struct Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Validate struct-specific rules during semantic analysis.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Validation Rules

1. **All fields must be declared at struct level:**
   ```python
   struct Good:
       x: int  # OK: at struct level
   
   struct Bad:
       def __init__(self):
           self.x = 0  # ERROR: field must be declared at struct level
   ```

2. **Structs cannot inherit from classes or other structs:**
   ```python
   struct Bad(SomeClass):  # ERROR
       pass
   ```

3. **Structs can implement interfaces:**
   ```python
   struct Good(IInterface):  # OK
       pass
   ```

4. **Constructor must initialize all fields:**
   ```python
   struct Point:
       x: int
       y: int
       
       def __init__(self, x: int):
           self.x = x
           # ERROR: field 'y' is not initialized
   ```
   
   **Note:** If no constructor is provided, fields are zero-initialized.

5. **Cannot use `None` for struct fields (use nullable type instead):**
   ```python
   struct Bad:
       p: Point = None  # ERROR: struct cannot be None
   
   struct Good:
       p: Point? = None  # OK: nullable struct
   ```

### Implementation Hints

```csharp
private void ValidateStruct(StructDef structDef)
{
    // Check inheritance - only interfaces allowed
    foreach (var baseType in structDef.Interfaces)
    {
        var resolved = ResolveType(baseType);
        if (resolved?.Kind != TypeKind.Interface)
        {
            Error($"Struct '{structDef.Name}' can only implement interfaces, not inherit from '{baseType.Name}'");
        }
    }
    
    // Collect declared fields
    var declaredFields = structDef.Body
        .OfType<VariableDeclaration>()
        .Select(f => f.Name)
        .ToHashSet();
    
    // Check __init__ initializes all fields
    var initMethod = structDef.Body
        .OfType<FunctionDef>()
        .FirstOrDefault(f => f.Name == "__init__");
    
    if (initMethod != null)
    {
        var initializedFields = GetAssignedFields(initMethod.Body);
        var uninitialized = declaredFields.Except(initializedFields);
        
        foreach (var field in uninitialized)
        {
            Error($"Field '{field}' is not initialized in constructor");
        }
    }
}
```

---

## Task 0.1.8.4: Verify Struct Value Semantics in Generated Code

**Type:** 🔍 Verification  
**Priority:** High  
**Estimated Time:** 1 hour

### Objective
Verify that structs exhibit value semantics (copy on assign, pass by value).

### Key Behaviors

1. **Copy on assignment:**
   ```python
   struct Point:
       x: int
       y: int
   
   p1 = Point(1, 2)
   p2 = p1           # p2 is a COPY
   p2.x = 10         # Modifies p2 only
   # p1.x is still 1
   ```

2. **Pass by value to functions:**
   ```python
   def modify(p: Point) -> None:
       p.x = 100  # Modifies local copy only
   
   original = Point(1, 2)
   modify(original)
   # original.x is still 1
   ```

3. **Struct cannot be `None` (non-nullable by default):**
   ```python
   p: Point = None  # ERROR: Cannot assign None to struct
   p: Point? = None  # OK: Nullable struct
   ```

### Verification Tests

These behaviors are inherent to C# structs, so we mainly need to verify:
- Struct generates as `struct` not `class`
- No `?` in type unless nullable

```csharp
[Fact]
public void StructValueSemantics_CopyOnAssign()
{
    var source = @"
struct Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p1 = Point(1, 2)
p2 = p1
p2.x = 10
# p1.x should still be 1
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Verify p1.x == 1 (not 10)
}
```

---

## Task 0.1.8.5: Audit/Verify Enum Definition AST and Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

### Objective
Verify that `EnumDef` AST node exists and enum definitions are parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Enum Syntax
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2
```

**Key Rule from Spec:**
> All enum cases must have explicit constant values (no auto-numbering). Values must all be the same type (integer or `str`).

### Actions

1. **Verify `EnumDef` AST node exists:**
   ```csharp
   public record EnumDef : Statement
   {
       public string Name { get; init; } = "";
       public List<EnumMember> Members { get; init; } = new();
       // Source location...
   }
   
   public record EnumMember : Node
   {
       public string Name { get; init; } = "";
       public Expression? Value { get; init; }  // Must be constant
       // Source location...
   }
   ```

2. **Verify enum parsing:**
   - [ ] `enum` keyword recognized
   - [ ] Enum name captured
   - [ ] Each member with name and value captured
   - [ ] Value expressions must be constants

3. **Verify explicit values required:**
   ```python
   enum Bad:
       A  # ERROR: Explicit value required
       B = 1
   ```

### Verification Commands
```bash
# Check EnumDef definition
grep -A 20 "record EnumDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs

# Run enum parsing tests
dotnet test --filter "Enum" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.8.6: Implement Enum Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 1-2 hours

### Objective
Generate C# `enum` declarations from Sharpy enum definitions.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Rules

**Input:**
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2
```

**Output:**
```csharp
public enum Color
{
    Red = 1,
    Green = 2,
    Blue = 3
}

public enum Status
{
    Pending = 0,
    Active = 1,
    Completed = 2
}
```

### Name Mangling for Enum Members

| Sharpy | C# |
|--------|-----|
| `CAPS_SNAKE_CASE` | `PascalCase` |
| `RED` | `Red` |
| `DARK_BLUE` | `DarkBlue` |

### Implementation Hints

```csharp
private EnumDeclarationSyntax GenerateEnum(EnumDef enumDef)
{
    var members = enumDef.Members.Select(m =>
        EnumMemberDeclaration(
            Identifier(NameMangler.EnumMemberToPascalCase(m.Name)))
            .WithEqualsValue(EqualsValueClause(
                GenerateExpression(m.Value!))));
    
    return EnumDeclaration(Identifier(enumDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithMembers(SeparatedList(members));
}
```

---

## Task 0.1.8.7: Implement Enum Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Validate enum-specific rules during semantic analysis.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Validation Rules

1. **All members must have explicit values:**
   ```python
   enum Bad:
       A      # ERROR: Missing value
       B = 1  # OK
   ```

2. **All values must be the same type:**
   ```python
   enum Bad:
       A = 1     # int
       B = "x"   # ERROR: Mixed types
   
   enum StringEnum:
       A = "one"
       B = "two"  # OK: All strings (if supported)
   ```

3. **Values must be compile-time constants:**
   ```python
   x = 5
   enum Bad:
       A = x  # ERROR: Not a constant
   ```

4. **No duplicate values (warning or error):**
   ```python
   enum Duplicate:
       A = 1
       B = 1  # WARNING: Duplicate value
   ```

5. **No duplicate names:**
   ```python
   enum Bad:
       A = 1
       A = 2  # ERROR: Duplicate member name
   ```

### Implementation Hints

```csharp
private void ValidateEnum(EnumDef enumDef)
{
    Type? expectedType = null;
    var usedValues = new HashSet<object>();
    var usedNames = new HashSet<string>();
    
    foreach (var member in enumDef.Members)
    {
        // Check name uniqueness
        if (!usedNames.Add(member.Name))
        {
            Error($"Duplicate enum member name: {member.Name}");
        }
        
        // Check value exists
        if (member.Value == null)
        {
            Error($"Enum member '{member.Name}' requires an explicit value");
            continue;
        }
        
        // Check value is constant
        if (!IsConstantExpression(member.Value))
        {
            Error($"Enum value for '{member.Name}' must be a compile-time constant");
            continue;
        }
        
        // Check type consistency
        var valueType = GetConstantType(member.Value);
        if (expectedType == null)
            expectedType = valueType;
        else if (valueType != expectedType)
            Error($"All enum values must be the same type. Expected {expectedType}, got {valueType}");
        
        // Check for duplicate values
        var value = EvaluateConstant(member.Value);
        if (!usedValues.Add(value))
            Warning($"Duplicate value {value} in enum {enumDef.Name}");
    }
}
```

---

## Task 0.1.8.8: Implement Enum Usage Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Generate correct C# for enum value access and comparison.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Code Generation Rules

1. **Enum value access:**
   ```python
   d = Direction.NORTH
   ```
   Generates:
   ```csharp
   var d = Direction.North;
   ```

2. **Enum comparison:**
   ```python
   if d == Direction.NORTH:
       pass
   ```
   Generates:
   ```csharp
   if (d == Direction.North)
   {
   }
   ```

3. **Type annotation with enum:**
   ```python
   current: Direction = Direction.NORTH
   ```
   Generates:
   ```csharp
   Direction current = Direction.North;
   ```

### Implementation Hints

```csharp
private ExpressionSyntax GenerateMemberAccess(MemberAccess access)
{
    // Check if this is enum member access
    if (access.Object is Identifier id && IsEnumType(id.Name))
    {
        var enumName = NameMangler.ToPascalCase(id.Name);
        var memberName = NameMangler.EnumMemberToPascalCase(access.Member);
        
        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName(enumName),
            IdentifierName(memberName));
    }
    
    // Regular member access
    return GenerateRegularMemberAccess(access);
}
```

---

## Task 0.1.8.9: Implement Struct Default Initialization

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 1 hour

### Objective
Support default (parameterless) struct instantiation.

### Spec Behavior

C# structs always have an implicit parameterless constructor that zero-initializes all fields:

```python
struct Point:
    x: int
    y: int

# Using implicit parameterless constructor (zero-initialized)
p1 = Point()           # x = 0, y = 0

# Using explicit constructor
struct Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

### Actions

1. **Handle parameterless struct instantiation:**
   - [ ] `Point()` should generate `new Point()` in C#
   - [ ] Works even without explicit `__init__`

2. **Zero-initialization defaults:**
   | Type | Default |
   |------|---------|
   | `int`, `int32`, etc. | `0` |
   | `float`, `float64` | `0.0` |
   | `bool` | `false` |
   | `str` (in struct) | `null` (requires `str?` or default) |

---

## Task 0.1.8.10: Create Phase 0.1.8 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Create comprehensive end-to-end tests for structs and enums.

### File to Create
`src/Sharpy.Compiler.Tests/Integration/Phase018IntegrationTests.cs`

### Test Cases

```csharp
#region Struct Tests

[Fact]
public void SimpleStruct_CompilesAndRuns()
{
    var source = @"
struct Point:
    x: int
    y: int

p = Point()
p.x = 10
p.y = 20
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructWithConstructor_Works()
{
    var source = @"
struct Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(3, 4)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructWithMethod_Works()
{
    var source = @"
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5

v = Vector2(3.0, 4.0)
m = v.magnitude()  # Should be 5.0
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructValueSemantics_CopyOnAssign()
{
    var source = @"
struct Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p1 = Point(1, 2)
p2 = p1           # Copy
p2.x = 10         # Modify copy only
result = p1.x     # Should still be 1
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Verify result == 1
}

[Fact]
public void StructImplementsInterface_Works()
{
    var source = @"
interface IDescribable:
    def describe(self) -> str:
        ...

struct Point(IDescribable):
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
    
    def describe(self) -> str:
        return 'Point'

p = Point(1, 2)
desc = p.describe()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructCannotInheritFromClass_Error()
{
    var source = @"
class Base:
    pass

struct Bad(Base):  # ERROR
    x: int
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

[Fact]
public void StructDefaultInitialization_ZeroValues()
{
    var source = @"
struct Point:
    x: int
    y: int

p = Point()  # Zero-initialized
# p.x should be 0
# p.y should be 0
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Enum Tests

[Fact]
public void SimpleEnum_CompilesAndRuns()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

c = Color.RED
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumComparison_Works()
{
    var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2

s = Status.ACTIVE
is_active = s == Status.ACTIVE  # Should be True
is_done = s == Status.DONE      # Should be False
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumInIfStatement_Works()
{
    var source = @"
enum Direction:
    NORTH = 0
    EAST = 1
    SOUTH = 2
    WEST = 3

d = Direction.NORTH

if d == Direction.NORTH:
    result = 'north'
elif d == Direction.SOUTH:
    result = 'south'
else:
    result = 'other'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumAsParameter_Works()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

def describe_color(c: Color) -> str:
    if c == Color.RED:
        return 'red'
    elif c == Color.GREEN:
        return 'green'
    else:
        return 'blue'

desc = describe_color(Color.GREEN)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumAsFieldType_Works()
{
    var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2

class Task:
    name: str
    status: Status
    
    def __init__(self, name: str):
        self.name = name
        self.status = Status.PENDING
    
    def start(self) -> None:
        self.status = Status.ACTIVE

t = Task('MyTask')
t.start()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumMissingValue_Error()
{
    var source = @"
enum Bad:
    A
    B = 1
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.Contains("value", result.CompilationErrors.First().ToLower());
}

[Fact]
public void EnumMixedTypes_Error()
{
    var source = @"
enum Bad:
    A = 1
    B = 'two'
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

#endregion

#region Combined Tests

[Fact]
public void StructWithEnumField_Works()
{
    var source = @"
enum Priority:
    LOW = 0
    MEDIUM = 1
    HIGH = 2

struct Task:
    name: str
    priority: Priority
    
    def __init__(self, name: str, priority: Priority):
        self.name = name
        self.priority = priority

t = Task('Important', Priority.HIGH)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion
```

---

## Task 0.1.8.11: Document Phase 0.1.8 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Structs compile to C# `struct` | `SimpleStruct_CompilesAndRuns` | [ ] |
| Value semantics enforced | `StructValueSemantics_CopyOnAssign` | [ ] |
| Enums compile to C# `enum` | `SimpleEnum_CompilesAndRuns` | [ ] |
| Enum values accessible via dot notation | `EnumComparison_Works` | [ ] |
| Enum comparison works | `EnumInIfStatement_Works` | [ ] |

### Verification Process

```bash
# Run all Phase 0.1.8 tests
dotnet test --filter "Phase018" --logger "console;verbosity=detailed"

# Verify generated C# code
dotnet run --project src/Sharpy.Compiler -- compile test_struct.spy --emit-csharp
dotnet run --project src/Sharpy.Compiler -- compile test_enum.spy --emit-csharp
```

---

## Summary: Task Dependencies

```
0.1.8.1 (Struct AST/Parsing) ────┐
                                 │
0.1.8.2 (Struct CodeGen) ────────┤
                                 │
0.1.8.3 (Struct Validation) ─────┼──► 0.1.8.4 (Value Semantics)
                                 │           │
0.1.8.5 (Enum AST/Parsing) ──────┤           │
                                 │           ▼
0.1.8.6 (Enum CodeGen) ──────────┤    0.1.8.9 (Default Init)
                                 │           │
0.1.8.7 (Enum Validation) ───────┤           │
                                 │           ▼
0.1.8.8 (Enum Usage CodeGen) ────┤    0.1.8.10 (Integration Tests)
                                 │           │
                                 ▼           ▼
                          0.1.8.11 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 2-3 hours
- **Implementation tasks:** 8-12 hours
- **Testing and documentation:** 3-4 hours
- **Total:** 13-19 hours

## Notes for Agent/Engineer

1. **Struct vs Class:** Structs are value types (copied on assignment), classes are reference types.

2. **Enum values required:** Unlike Python, Sharpy enums require explicit values.

3. **Name mangling for enums:** `CAPS_SNAKE_CASE` → `PascalCase` for C# enum members.

4. **Struct interfaces:** Structs can implement interfaces but not inherit from classes/structs.

5. **Boxing warning:** When a struct is assigned to an interface variable, boxing occurs (heap allocation).

6. **Default initialization:** Parameterless struct constructor always exists and zero-initializes.

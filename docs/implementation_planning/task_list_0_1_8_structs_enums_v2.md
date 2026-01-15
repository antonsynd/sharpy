# Phase 0.1.8: Structs & Enums - Comprehensive Task List (v2)

**Goal:** Value types (structs) and enumeration types with explicit values.

**Prerequisites:** Phase 0.1.7 (Inheritance & Interfaces) must be complete.

**Exit Criteria:**
- Structs compile to C# structs with value semantics
- Struct fields initialized correctly
- Structs can implement interfaces (with boxing awareness)
- Enums compile to C# enums (integer-based)
- String enums lower to static classes with const strings
- All enum values must be explicit (no auto-numbering)
- Enum `.name` and `.value` properties work
- Enum members use PascalCase in C# (`RED` → `Red`)
- Structs cannot have `@virtual` or `@abstract` methods
- Simple enums cannot have methods

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for struct-related code
grep -rn "struct\|StructDef" src/Sharpy.Compiler/

# Check for enum-related code
grep -rn "enum\|EnumDef" src/Sharpy.Compiler/

# Check for existing tests
find src -name "*.cs" -exec grep -l "Struct\|Enum" {} \;
```

---

## Task 0.1.8.1: Implement Struct Definition AST and Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Parse struct definitions and create AST nodes.

### Syntax
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
    
    def length(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

### Grammar
```ebnf
struct_def ::= 'struct' identifier [ type_params ] [ '(' interface_list ')' ] ':' struct_body
struct_body ::= NEWLINE INDENT { struct_member } DEDENT
struct_member ::= field_decl | method_def
```

### Actions

1. **Create `StructDef` AST node:**
   ```csharp
   public record StructDef : Statement
   {
       public string Name { get; init; } = "";
       public List<TypeAnnotation> Interfaces { get; init; } = new();  // Not base classes!
       public List<Statement> Body { get; init; } = new();
       public List<Decorator> Decorators { get; init; } = new();
   }
   ```

2. **Parse struct syntax:**
   - Recognize `struct` keyword
   - Parse optional interface list in parentheses
   - Parse struct body (fields and methods)

3. **Key difference from classes:**
   - Structs can ONLY implement interfaces (no inheritance)
   - First item in parentheses must be an interface, not a class

### Test Cases

```python
# Basic struct
struct Point:
    x: int
    y: int

# Struct with constructor
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

# Struct implementing interface
struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'
```

---

## Task 0.1.8.2: Implement Struct Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# structs from Sharpy struct definitions.

### Type Mapping Note

Sharpy `float` → C# `double` (64-bit, like Python)
Sharpy `float32` → C# `float` (32-bit)

### Code Generation Example

**Input:**
```python
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    def length(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Output:**
```csharp
public struct Vector2
{
    public double X;
    public double Y;
    
    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }
    
    public double Length()
    {
        return Math.Sqrt(X * X + Y * Y);
    }
}
```

### Interface Implementation

**Input:**
```python
struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'
```

**Output:**
```csharp
public struct Point : IDescribable
{
    public int X;
    public int Y;
    
    public string Describe()
    {
        return "Point";
    }
}
```

### Implementation Hints

```csharp
private StructDeclarationSyntax GenerateStruct(StructDef structDef)
{
    var structDecl = StructDeclaration(NameMangler.ToPascalCase(structDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add interfaces (structs can only implement interfaces, not inherit)
    if (structDef.Interfaces.Any())
    {
        var baseTypes = structDef.Interfaces
            .Select(i => SimpleBaseType(IdentifierName(i.Name)));
        
        structDecl = structDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
    }
    
    // Generate fields and methods
    var members = new List<MemberDeclarationSyntax>();
    
    foreach (var member in structDef.Body)
    {
        if (member is VariableDeclaration field)
            members.Add(GenerateStructField(field));
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
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Validate struct-specific rules.

### Validation Rules

1. **⚠️ Constructor must initialize ALL fields:**
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
   - Error message: "Struct constructor must initialize all fields. Field 'z' is not assigned before the constructor returns."

2. **⚠️ Structs cannot have `@virtual` or `@abstract` methods:**
   ```python
   struct Bad:
       @virtual
       def method(self) -> int:  # ERROR: Struct methods cannot be virtual
           return 0
       
       @abstract
       def other(self) -> int:   # ERROR: Struct methods cannot be abstract
           ...
   ```
   - Error messages:
     - "Struct methods cannot be marked @virtual"
     - "Struct methods cannot be marked @abstract"
     - "Structs cannot be marked @abstract"

3. **Structs can only implement interfaces (no inheritance):**
   ```python
   struct BadStruct(SomeClass):  # ERROR: Structs cannot inherit from classes
       pass
   ```

4. **⚠️ Optional: Boxing warning when assigning to interface:**
   ```python
   struct Point(IDescribable):
       x: int
       y: int

   p = Point(10, 20)
   d: IDescribable = p  # WARNING: Assigning struct to interface causes boxing
   ```
   - Warning message: "Assigning struct 'Point' to interface 'IDescribable' causes boxing (heap allocation). Consider using direct struct method calls for performance-critical code."

### Default Struct Behavior (C# 9.0)

- Structs **always** have an implicit parameterless constructor that zero-initializes all fields
- Even if you define constructors, the parameterless one still exists
- `Point()` → all fields initialized to default values (0 for int, etc.)

```python
struct Point:
    x: int
    y: int

p1 = Point()  # x = 0, y = 0 (implicit parameterless constructor)

struct Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

---

## Task 0.1.8.4: Document Struct Value Semantics

**Type:** 📝 Documentation / Awareness  
**Priority:** Medium  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/language_specification/structs.md`

### Objective
Document value type semantics for engineers/users.

### Key Points

1. **Structs are value types:**
   - Copied on assignment
   - Passed by value to functions
   - Stored inline (no heap allocation for the struct itself)

2. **For large structs, use parameter modifiers to avoid copies:**
   - `in[T]` — pass by reference, read-only (no copy, no mutation)
   - `ref[T]` — pass by reference, allows mutation

   ```python
   def process(data: in[LargeStruct]) -> double:  # No copy, read-only
       return data.value

   def modify(data: ref[LargeStruct]):  # No copy, allows mutation
       data.value = 100
   ```

   Note: This is covered by parameter modifiers (separate feature), not required for Phase 0.1.8.

3. **Boxing occurs when:**
   - Struct assigned to interface variable
   - Struct passed to interface-typed parameter
   - Struct used with `object` type

---

## Task 0.1.8.5: Implement Enum Definition AST and Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Parse enum definitions and create AST nodes.

### Syntax
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"
```

### Grammar
```ebnf
enum_def ::= 'enum' identifier ':' NEWLINE INDENT { enum_member } DEDENT
enum_member ::= identifier '=' constant_expr NEWLINE
```

### Actions

1. **Create `EnumDef` AST node:**
   ```csharp
   public record EnumDef : Statement
   {
       public string Name { get; init; } = "";
       public List<EnumMember> Members { get; init; } = new();
   }

   public record EnumMember
   {
       public string Name { get; init; } = "";
       public Expression Value { get; init; } = null!;  // Must have explicit value
   }
   ```

2. **Parse enum syntax:**
   - Recognize `enum` keyword
   - Parse members with explicit values
   - Determine value type (int or string)

---

## Task 0.1.8.6: Implement Enum Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# code for enums.

### ⚠️ Two Code Generation Strategies

1. **Integer enums → C# `enum`:**
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       BLUE = 3
   ```
   
   **Generated C#:**
   ```csharp
   public enum Color
   {
       Red = 1,
       Green = 2,
       Blue = 3
   }
   ```

2. **⚠️ String enums → Static class with const strings:**
   ```python
   enum HttpMethod:
       GET = "GET"
       POST = "POST"
       PUT = "PUT"
       DELETE = "DELETE"
   ```
   
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
   
   C# `enum` only supports integral types, so string enums must be lowered to static classes.

### ⚠️ Enum Member Name Mangling

Enum members should be transformed to PascalCase:
- `RED` → `Red`
- `DARK_BLUE` → `DarkBlue`
- `HTTP_NOT_FOUND` → `HttpNotFound`

**Important:** Use `NameContext.EnumMember` or create a specific method for this, NOT `NameContext.Constant` (which preserves CAPS_SNAKE_CASE).

### Implementation Hints

```csharp
private MemberDeclarationSyntax GenerateEnum(EnumDef enumDef)
{
    // Determine if this is integer or string enum
    var firstValue = enumDef.Members.FirstOrDefault()?.Value;
    bool isStringEnum = firstValue is StringLiteral;
    
    if (isStringEnum)
    {
        return GenerateStringEnumAsStaticClass(enumDef);
    }
    else
    {
        return GenerateIntegerEnum(enumDef);
    }
}

private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
{
    var members = enumDef.Members.Select(m =>
        EnumMemberDeclaration(NameMangler.EnumMemberToPascalCase(m.Name))
            .WithEqualsValue(EqualsValueClause(GenerateExpression(m.Value))));
    
    return EnumDeclaration(enumDef.Name)
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithMembers(SeparatedList(members));
}

private ClassDeclarationSyntax GenerateStringEnumAsStaticClass(EnumDef enumDef)
{
    var members = enumDef.Members.Select(m =>
        FieldDeclaration(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(NameMangler.EnumMemberToPascalCase(m.Name))
                        .WithInitializer(EqualsValueClause(GenerateExpression(m.Value))))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.ConstKeyword))));
    
    return ClassDeclaration(enumDef.Name)
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword)))
        .WithMembers(List<MemberDeclarationSyntax>(members));
}
```

---

## Task 0.1.8.7: Implement Enum Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Validate enum-specific rules.

### Validation Rules

1. **All enum values must be explicit:**
   ```python
   enum Bad:
       A        # ERROR: requires explicit value
       B = 1
   ```
   - Error message: "Enum member 'A' requires an explicit value. All enum members must have explicit constant values."

2. **All values must be of the same type:**
   ```python
   enum Mixed:
       A = 1
       B = "str"  # ERROR: mixed types
   ```
   - Error message: "Enum 'Mixed' has mixed value types. All enum members must have values of the same type (all integers or all strings)."

3. **⚠️ Simple enums cannot have methods:**
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       
       def is_warm(self) -> bool:  # ERROR: Simple enums cannot have methods
           return self == Color.RED
   ```
   - Error message: "Simple enums cannot have methods. Use a tagged union for enums with associated methods."

4. **Enum values must be compile-time constants:**
   - Integer literals
   - String literals
   - Other constant expressions

---

## Task 0.1.8.8: Implement Enum Usage Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate code for enum member access and properties.

### Actions

1. **Enum member access:**
   ```python
   favorite = Color.RED
   ```
   
   **Generated C#:**
   ```csharp
   var favorite = Color.Red;  // Note: PascalCase
   ```

2. **⚠️ `.value` property:**
   ```python
   value = favorite.value  # Returns underlying value
   ```
   
   **Generated C# (integer enum):**
   ```csharp
   var value = (int)favorite;
   ```
   
   **Generated C# (string enum):**
   ```csharp
   // String enums are already const strings, so .value just returns the string
   var value = HttpMethod.Get;  // Already "GET"
   ```

3. **⚠️ `.name` property:**
   ```python
   name = favorite.name  # Returns enum member name as string
   ```
   
   **Generated C#:**
   ```csharp
   var name = Enum.GetName(typeof(Color), favorite);
   // Or: var name = favorite.ToString();
   ```

### Phase Scope Decision

- Include `.value` access (simple cast for int enums)
- Include `.name` access (`Enum.GetName` or `ToString()`)
- **Defer** iteration support (`for x in EnumType:`) to later phase (requires collection semantics)

---

## Task 0.1.8.9: Implement Struct Instantiation and Assignment

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate correct code for struct instantiation and value semantics.

### Actions

1. **Struct instantiation:**
   ```python
   p = Point(10, 20)
   ```
   
   **Generated C#:**
   ```csharp
   var p = new Point(10, 20);
   ```

2. **Default instantiation:**
   ```python
   p = Point()  # Uses implicit parameterless constructor
   ```
   
   **Generated C#:**
   ```csharp
   var p = new Point();  // All fields zero-initialized
   // Or: var p = default(Point);
   ```

3. **Struct assignment (value copy):**
   ```python
   p1 = Point(1, 2)
   p2 = p1  # Value copy, not reference
   p2.x = 100  # Does not affect p1
   ```
   
   **Generated C#:**
   ```csharp
   var p1 = new Point(1, 2);
   var p2 = p1;  // Value copy
   p2.X = 100;   // Does not affect p1
   ```

---

## Task 0.1.8.10: Create Phase 0.1.8 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase018IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void BasicStruct_CompilesAndRuns()
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
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v = Vector2(3.0, 4.0)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructConstructorMustInitializeAllFields()
{
    var source = @"
struct Point:
    x: int
    y: int
    z: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("z", result.Error);
    Assert.Contains("not initialized", result.Error.ToLower());
}

[Fact]
public void StructWithInterface_Works()
{
    var source = @"
interface IDescribable:
    def describe(self) -> str:
        ...

struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'

p = Point()
p.x = 10
p.y = 20
desc = p.describe()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructVirtualMethod_ProducesError()
{
    var source = @"
struct Bad:
    @virtual
    def method(self) -> int:
        return 0
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("virtual", result.Error.ToLower());
}

[Fact]
public void StructAbstractMethod_ProducesError()
{
    var source = @"
struct Bad:
    @abstract
    def method(self) -> int:
        ...
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("abstract", result.Error.ToLower());
}

[Fact]
public void IntegerEnum_CompilesAndRuns()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StringEnum_CompilesAndRuns()
{
    var source = @"
enum HttpMethod:
    GET = 'GET'
    POST = 'POST'
    PUT = 'PUT'

method = HttpMethod.GET
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumWithoutExplicitValue_ProducesError()
{
    var source = @"
enum Bad:
    A
    B = 1
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("explicit value", result.Error.ToLower());
}

[Fact]
public void EnumMixedTypes_ProducesError()
{
    var source = @"
enum Mixed:
    A = 1
    B = 'string'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("mixed", result.Error.ToLower());
}

[Fact]
public void EnumWithMethods_ProducesError()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    
    def is_warm(self) -> bool:
        return True
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot have methods", result.Error.ToLower());
}

[Fact]
public void EnumValue_Property()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
value = favorite.value
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // value should be 1
}

[Fact]
public void EnumName_Property()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
name = favorite.name
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // name should be 'Red' (PascalCase in C#)
}

[Fact]
public void EnumMemberNameMangling_CorrectPascalCase()
{
    var source = @"
enum Status:
    NOT_FOUND = 404
    INTERNAL_SERVER_ERROR = 500

s = Status.NOT_FOUND
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Generated C# should have NotFound, InternalServerError
}

[Fact]
public void StructValueSemantics_CopyOnAssignment()
{
    var source = @"
struct Point:
    x: int
    y: int

p1 = Point()
p1.x = 10
p2 = p1
p2.x = 100
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // p1.x should still be 10 (value copy)
}
```

---

## Task 0.1.8.11: Document Phase 0.1.8 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_8_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Basic struct compiles | `BasicStruct_CompilesAndRuns` | [ ] |
| Struct with constructor | `StructWithConstructor_Works` | [ ] |
| Struct field init validation | `StructConstructorMustInitializeAllFields` | [ ] |
| Struct implements interface | `StructWithInterface_Works` | [ ] |
| No @virtual in structs | `StructVirtualMethod_ProducesError` | [ ] |
| No @abstract in structs | `StructAbstractMethod_ProducesError` | [ ] |
| Integer enum compiles | `IntegerEnum_CompilesAndRuns` | [ ] |
| String enum compiles | `StringEnum_CompilesAndRuns` | [ ] |
| Enum requires explicit values | `EnumWithoutExplicitValue_ProducesError` | [ ] |
| Enum type consistency | `EnumMixedTypes_ProducesError` | [ ] |
| No methods in simple enums | `EnumWithMethods_ProducesError` | [ ] |
| Enum .value property | `EnumValue_Property` | [ ] |
| Enum .name property | `EnumName_Property` | [ ] |
| Enum member name mangling | `EnumMemberNameMangling_CorrectPascalCase` | [ ] |
| Struct value semantics | `StructValueSemantics_CopyOnAssignment` | [ ] |

---

## Summary: Task Dependencies

```
0.1.8.1 (Struct AST/Parsing) ───────────────────────────┐
                                                        │
0.1.8.5 (Enum AST/Parsing) ─────────────────────────────┼──► 0.1.8.2 (Struct CodeGen)
                                                        │    0.1.8.3 (Struct Validation)
                                                        │    0.1.8.4 (Value Semantics Doc)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.8.6 (Enum CodeGen)
                                                        │    0.1.8.7 (Enum Validation)
                                                        │    0.1.8.8 (Enum Usage)
                                                        │    0.1.8.9 (Struct Instantiation)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.8.10 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.8.11 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing/AST tasks:** 4-5 hours
- **Code generation tasks:** 6-8 hours
- **Validation tasks:** 3-5 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 17-23 hours

## Notes for Agent/Engineer

1. **Structs are value types** — They're copied on assignment and passed by value.

2. **String enums → static classes** — C# `enum` only supports integral types.

3. **Enum member name mangling** — Use PascalCase (`RED` → `Red`), NOT constant case.

4. **All enum values explicit** — No auto-numbering like Python's `auto()`.

5. **Struct constructors must init all fields** — C# 9.0 requirement for value types.

6. **No @virtual/@abstract on struct methods** — Structs have no polymorphism.

7. **Simple enums have no methods** — Tagged unions (future feature) support methods.

8. **Type mapping:** `float` → `double`, `float32` → `float`.

# Phase 0.1.9: Type System Enhancements - Detailed Task List

**Goal:** Nullable types, type aliases, and basic generics.

**Prerequisites:** Phases 0.1.6-0.1.8 (Classes, Inheritance, Structs, Enums) must be complete.

**Exit Criteria (from spec):**
- `T?` compiles to `Nullable<T>` or reference `T?`
- Null coalescing `??` works
- Null conditional `?.` works
- Type aliases expand correctly
- Generic collection types instantiate
- User-defined generic classes/functions compile
- Type constraints validated

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for nullable type handling
grep -rn "Nullable\|NullCoalesce\|NullConditional" src/Sharpy.Compiler/

# Check for type alias support
grep -rn "TypeAlias\|type\s*=" src/Sharpy.Compiler/Parser/

# Check for generic type handling
grep -rn "Generic\|TypeParam\|TypeArg" src/Sharpy.Compiler/

# Check for existing tests
dotnet test --list-tests | grep -i "nullable\|generic\|alias"
```

---

## Task 0.1.9.1: Audit/Verify Nullable Type Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

### Objective
Verify that nullable type syntax (`T?`) is parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Types.cs` (TypeAnnotation)
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Nullable Syntax
```python
name: str? = None
count: int? = 42
point: Point? = None

def find(items: list[str]) -> str?:
    return None
```

### Actions

1. **Verify `TypeAnnotation` supports nullable:**
   ```csharp
   public record TypeAnnotation : Node
   {
       public string Name { get; init; } = "";
       public bool IsNullable { get; init; }  // T?
       public List<TypeAnnotation> TypeArguments { get; init; } = new();
       // ...
   }
   ```

2. **Verify parsing of nullable types:**
   - [ ] `str?` → TypeAnnotation { Name = "str", IsNullable = true }
   - [ ] `int?` → TypeAnnotation { Name = "int", IsNullable = true }
   - [ ] `list[str]?` → TypeAnnotation { Name = "list", TypeArguments = [str], IsNullable = true }

3. **Test nullable type parsing:**
   ```python
   x: int? = None
   y: str? = "hello"
   z: list[int]? = None
   ```

### Verification Commands
```bash
# Check TypeAnnotation definition
grep -A 20 "record TypeAnnotation" src/Sharpy.Compiler/Parser/Ast/

# Run nullable type tests
dotnet test --filter "Nullable" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.9.2: Implement Nullable Type Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Generate correct C# for nullable types.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Rules

1. **Nullable value types:**
   ```python
   x: int? = None
   ```
   Generates:
   ```csharp
   int? x = null;  // Or: Nullable<int> x = null;
   ```

2. **Nullable reference types:**
   ```python
   name: str? = None
   ```
   Generates:
   ```csharp
   string? name = null;  // C# 8+ nullable reference types
   ```

3. **Nullable custom types:**
   ```python
   point: Point? = None  # Point is a class
   ```
   Generates:
   ```csharp
   Point? point = null;
   ```

4. **Nullable struct types:**
   ```python
   coord: Vector2? = None  # Vector2 is a struct
   ```
   Generates:
   ```csharp
   Vector2? coord = null;  // Nullable<Vector2>
   ```

### Implementation Hints

```csharp
public TypeSyntax MapType(TypeAnnotation typeAnnotation)
{
    var baseType = MapBaseType(typeAnnotation.Name);
    
    // Handle generic type arguments
    if (typeAnnotation.TypeArguments.Count > 0)
    {
        var typeArgs = typeAnnotation.TypeArguments.Select(MapType);
        baseType = GenericName(Identifier(baseType.ToString()))
            .WithTypeArgumentList(TypeArgumentList(
                SeparatedList(typeArgs)));
    }
    
    // Handle nullable
    if (typeAnnotation.IsNullable)
    {
        return NullableType(baseType);
    }
    
    return baseType;
}
```

---

## Task 0.1.9.3: Implement Null Coalescing Operator (`??`)

**Type:** 🔍 Status Check / Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Ensure null coalescing operator is parsed and generates correct C#.

### Files to Check/Modify
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Sharpy Syntax
```python
value = name ?? "default"
result = get_value() ?? fallback_value()
```

### Code Generation

**Input:**
```python
name: str? = None
value = name ?? "default"
```

**Output:**
```csharp
string? name = null;
var value = name ?? "default";
```

### Actions

1. **Verify token exists:** `TokenType.NullCoalesce` (or similar)

2. **Verify parsing:**
   - [ ] `??` recognized as binary operator
   - [ ] Correct precedence (low, right-associative)

3. **Verify code generation:**
   - [ ] Maps to C# `??` operator

### Implementation Hints

```csharp
private ExpressionSyntax GenerateBinaryExpression(BinaryExpression expr)
{
    var left = GenerateExpression(expr.Left);
    var right = GenerateExpression(expr.Right);
    
    var opKind = expr.Operator switch
    {
        "??" => SyntaxKind.CoalesceExpression,
        // ... other operators
    };
    
    return BinaryExpression(opKind, left, right);
}
```

---

## Task 0.1.9.4: Implement Null Conditional Operator (`?.`)

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2-3 hours

### Objective
Implement null conditional access (`?.`) for safe member access.

### Files to Modify
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Sharpy Syntax
```python
length = name?.upper()  # None if name is None, else name.upper()
value = obj?.nested?.property  # Chained null conditional

# Method call with null conditional
result = items?.first()
```

### Code Generation

**Input:**
```python
name: str? = None
length = name?.upper()
```

**Output:**
```csharp
string? name = null;
var length = name?.ToUpper();  // Returns string? (or null)
```

### Actions

1. **Verify token exists:** `TokenType.QuestionDot` (or similar)

2. **Implement parsing:**
   - [ ] Recognize `?.` as special member access
   - [ ] Create `NullConditionalAccess` AST node or flag on `MemberAccess`

3. **Implement code generation:**
   ```csharp
   private ExpressionSyntax GenerateNullConditionalAccess(NullConditionalAccess access)
   {
       var obj = GenerateExpression(access.Object);
       var member = IdentifierName(NameMangler.ToPascalCase(access.Member));
       
       return ConditionalAccessExpression(
           obj,
           MemberBindingExpression(member));
   }
   ```

4. **Handle null conditional method calls:**
   ```python
   result = obj?.method()
   ```
   Generates:
   ```csharp
   var result = obj?.Method();
   ```

### Implementation Hints

```csharp
// AST option 1: Dedicated node
public record NullConditionalAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsMethodCall { get; init; }
    public List<Expression> Arguments { get; init; } = new();
}

// AST option 2: Flag on MemberAccess
public record MemberAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsNullConditional { get; init; }  // True for ?.
}
```

---

## Task 0.1.9.5: Implement Type Narrowing for Null Checks

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 2-3 hours

### Objective
Implement type narrowing when null is checked.

### Spec Behavior
```python
name: str? = get_name()

if name is not None:
    # name is str here (not str?)
    length = len(name)  # OK, no null check needed

# Outside the if, name is still str?
```

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`
- `src/Sharpy.Compiler/Semantic/TypeNarrowing.cs` (may need to create)

### Actions

1. **Track narrowing contexts:**
   - When inside `if x is not None:`, narrow `x` from `T?` to `T`
   - When inside `if x is None:`, the else branch narrows `x` to `T`

2. **Implement narrowing analysis:**
   ```csharp
   private void AnalyzeIfStatement(IfStatement ifStmt)
   {
       // Check if condition is null check
       if (IsNullCheck(ifStmt.Condition, out var variable, out var isNotNull))
       {
           if (isNotNull)
           {
               // Narrow in the then branch
               _narrowedTypes[variable] = GetNonNullableType(GetType(variable));
               AnalyzeBlock(ifStmt.ThenBranch);
               _narrowedTypes.Remove(variable);
           }
           else
           {
               // Narrow in the else branch
               AnalyzeBlock(ifStmt.ThenBranch);
               if (ifStmt.ElseBranch != null)
               {
                   _narrowedTypes[variable] = GetNonNullableType(GetType(variable));
                   AnalyzeBlock(ifStmt.ElseBranch);
                   _narrowedTypes.Remove(variable);
               }
           }
       }
       else
       {
           // Regular analysis
           AnalyzeBlock(ifStmt.ThenBranch);
           if (ifStmt.ElseBranch != null)
               AnalyzeBlock(ifStmt.ElseBranch);
       }
   }
   ```

3. **Test narrowing:**
   ```python
   def process(value: str?) -> int:
       if value is not None:
           return len(value)  # OK: value is str
       return 0
   ```

---

## Task 0.1.9.6: Implement Type Alias Parsing

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Parse type alias definitions (`type Name = ...`).

### Files to Create/Modify
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` — Add `TypeAliasStatement`
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Type Alias Syntax
```python
type UserId = int
type Point2D = tuple[float, float]
type Handler = (int) -> None
type StringList = list[str]
```

### Actions

1. **Create `TypeAliasStatement` AST node:**
   ```csharp
   public record TypeAliasStatement : Statement
   {
       public string Name { get; init; } = "";
       public TypeAnnotation AliasedType { get; init; } = null!;
       // Source location...
   }
   ```

2. **Implement parsing:**
   - [ ] Recognize `type` keyword followed by identifier
   - [ ] Parse `=` and type expression
   - [ ] Capture full type (including generics, function types)

3. **Handle function types:**
   ```python
   type Handler = (int) -> None
   type Predicate = (str) -> bool
   type BinaryOp = (int, int) -> int
   ```

### Verification Tests
```python
type UserId = int
type Point2D = tuple[float, float]
type Callback = (str) -> None

x: UserId = 42
```

---

## Task 0.1.9.7: Implement Type Alias Semantic Analysis

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Register type aliases in the symbol table and expand them during type checking.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

### Actions

1. **Register type aliases:**
   ```csharp
   private void AnalyzeTypeAlias(TypeAliasStatement alias)
   {
       var aliasedType = ResolveType(alias.AliasedType);
       _symbolTable.DefineTypeAlias(alias.Name, aliasedType);
   }
   ```

2. **Expand aliases during type resolution:**
   ```csharp
   private SemanticType ResolveType(TypeAnnotation annotation)
   {
       // Check if it's a type alias
       if (_symbolTable.TryGetTypeAlias(annotation.Name, out var aliasedType))
       {
           return aliasedType;
       }
       
       // Regular type resolution...
   }
   ```

3. **Handle recursive aliases (error):**
   ```python
   type A = B
   type B = A  # ERROR: Circular type alias
   ```

---

## Task 0.1.9.8: Implement Type Alias Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Generate C# code that uses expanded types (not aliases).

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Rules

Type aliases are **compile-time only** — they expand to their underlying types in generated C#.

**Input:**
```python
type UserId = int
type StringList = list[str]

user_id: UserId = 42
items: StringList = ["a", "b", "c"]
```

**Output:**
```csharp
// No type alias declarations in C# (aliases are expanded)
int userId = 42;
List<string> items = new List<string> { "a", "b", "c" };
```

### Actions

1. **Expand type aliases in type mapper:**
   ```csharp
   public TypeSyntax MapType(TypeAnnotation annotation)
   {
       // Expand any aliases first
       var resolvedType = _semanticAnalyzer.ResolveType(annotation);
       
       // Then generate C# type
       return MapResolvedType(resolvedType);
   }
   ```

2. **Don't generate type alias declarations:**
   - Skip `TypeAliasStatement` during code generation
   - All usages already expanded to underlying types

---

## Task 0.1.9.9: Audit/Verify Generic Type Parsing

**Type:** 🔍 Status Check  
**Priority:** High  
**Estimated Time:** 1 hour

### Objective
Verify that generic type syntax is parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Types.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Generic Syntax
```python
# Built-in generic types
items: list[int] = []
pairs: dict[str, int] = {}
numbers: set[int] = set()

# User-defined generic classes
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value

# User-defined generic functions
def identity[T](value: T) -> T:
    return value
```

### Actions

1. **Verify `TypeAnnotation.TypeArguments` parsing:**
   - [ ] `list[int]` → TypeAnnotation { Name = "list", TypeArguments = [int] }
   - [ ] `dict[str, int]` → TypeAnnotation { Name = "dict", TypeArguments = [str, int] }

2. **Verify class type parameter parsing:**
   - [ ] `class Box[T]:` captures type parameter `T`

3. **Verify function type parameter parsing:**
   - [ ] `def identity[T](value: T) -> T:` captures type parameter `T`

### Verification Commands
```bash
# Check for generic type handling
grep -rn "TypeArg\|TypeParam\|Generic" src/Sharpy.Compiler/Parser/

# Run generic type tests
dotnet test --filter "Generic" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.9.10: Implement User-Defined Generic Class Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2-3 hours

### Objective
Generate C# generic class declarations.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Example

**Input:**
```python
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value
```

**Output:**
```csharp
public class Box<T>
{
    public T Value;
    
    public Box(T value)
    {
        Value = value;
    }
    
    public T Get()
    {
        return Value;
    }
}
```

### Implementation Hints

```csharp
private ClassDeclarationSyntax GenerateClass(ClassDef classDef)
{
    var classDecl = ClassDeclaration(
        Identifier(NameMangler.ToPascalCase(classDef.Name)));
    
    // Add type parameters if present
    if (classDef.TypeParameters?.Count > 0)
    {
        var typeParams = classDef.TypeParameters
            .Select(p => TypeParameter(Identifier(p.Name)));
        classDecl = classDecl.WithTypeParameterList(
            TypeParameterList(SeparatedList(typeParams)));
    }
    
    // Add members...
    return classDecl;
}
```

---

## Task 0.1.9.11: Implement Generic Function Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Generate C# generic method declarations.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Example

**Input:**
```python
def identity[T](value: T) -> T:
    return value

def swap[T, U](pair: tuple[T, U]) -> tuple[U, T]:
    return (pair[1], pair[0])
```

**Output:**
```csharp
public static T Identity<T>(T value)
{
    return value;
}

public static (U, T) Swap<T, U>((T, U) pair)
{
    return (pair.Item2, pair.Item1);
}
```

### Implementation Hints

```csharp
private MethodDeclarationSyntax GenerateFunction(FunctionDef func)
{
    var methodDecl = MethodDeclaration(
        MapReturnType(func.ReturnType),
        Identifier(NameMangler.ToPascalCase(func.Name)));
    
    // Add type parameters if present
    if (func.TypeParameters?.Count > 0)
    {
        var typeParams = func.TypeParameters
            .Select(p => TypeParameter(Identifier(p.Name)));
        methodDecl = methodDecl.WithTypeParameterList(
            TypeParameterList(SeparatedList(typeParams)));
    }
    
    // Add parameters and body...
    return methodDecl;
}
```

---

## Task 0.1.9.12: Implement Type Constraint Parsing and Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 2-3 hours

### Objective
Parse and generate code for type constraints.

### Sharpy Constraint Syntax
```python
def find_max[T: IComparable[T]](items: list[T]) -> T:
    ...

class SortedCollection[T: IComparable[T]]:
    ...
```

### C# Output
```csharp
public T FindMax<T>(List<T> items) where T : IComparable<T>
{
    ...
}

public class SortedCollection<T> where T : IComparable<T>
{
    ...
}
```

### Actions

1. **Parse type constraints:**
   ```csharp
   public record TypeParameter : Node
   {
       public string Name { get; init; } = "";
       public TypeAnnotation? Constraint { get; init; }  // T: IComparable[T]
   }
   ```

2. **Generate constraint clauses:**
   ```csharp
   private TypeParameterConstraintClauseSyntax? GenerateConstraint(TypeParameter typeParam)
   {
       if (typeParam.Constraint == null)
           return null;
       
       var constraintType = MapType(typeParam.Constraint);
       
       return TypeParameterConstraintClause(IdentifierName(typeParam.Name))
           .WithConstraints(SingletonSeparatedList<TypeParameterConstraintSyntax>(
               TypeConstraint(constraintType)));
   }
   ```

---

## Task 0.1.9.13: Create Phase 0.1.9 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

### Objective
Create comprehensive end-to-end tests for type system enhancements.

### File to Create
`src/Sharpy.Compiler.Tests/Integration/Phase019IntegrationTests.cs`

### Test Cases

```csharp
#region Nullable Types

[Fact]
public void NullableValueType_Works()
{
    var source = @"
count: int? = None
count = 42
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullableReferenceType_Works()
{
    var source = @"
name: str? = None
name = 'Alice'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullableParameter_Works()
{
    var source = @"
def greet(name: str? = None) -> str:
    if name is not None:
        return 'Hello, ' + name
    return 'Hello, stranger'

g1 = greet()
g2 = greet('Alice')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullableReturnType_Works()
{
    var source = @"
def find_first(items: list[int]) -> int?:
    if len(items) > 0:
        return items[0]
    return None

result = find_first([])
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Null Coalescing

[Fact]
public void NullCoalescing_Works()
{
    var source = @"
name: str? = None
value = name ?? 'default'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullCoalescing_ChainedWorks()
{
    var source = @"
a: str? = None
b: str? = None
c: str = 'fallback'

result = a ?? b ?? c
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Null Conditional

[Fact]
public void NullConditional_MemberAccess()
{
    var source = @"
class Person:
    name: str
    
    def __init__(self, name: str):
        self.name = name

p: Person? = None
length = p?.name
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_MethodCall()
{
    var source = @"
name: str? = 'hello'
upper = name?.upper()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_Chained()
{
    var source = @"
class Inner:
    value: str
    
    def __init__(self, value: str):
        self.value = value

class Outer:
    inner: Inner?
    
    def __init__(self, inner: Inner? = None):
        self.inner = inner

outer: Outer? = None
result = outer?.inner?.value
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Type Aliases

[Fact]
public void TypeAlias_Simple_Works()
{
    var source = @"
type UserId = int

user_id: UserId = 42
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeAlias_Generic_Works()
{
    var source = @"
type StringList = list[str]

items: StringList = ['a', 'b', 'c']
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeAlias_Function_Works()
{
    var source = @"
type Predicate = (int) -> bool

def is_positive(x: int) -> bool:
    return x > 0

check: Predicate = is_positive
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Generics

[Fact]
public void GenericClass_Simple_Works()
{
    var source = @"
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value

int_box = Box[int](42)
str_box = Box[str]('hello')

i = int_box.get()
s = str_box.get()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericFunction_Works()
{
    var source = @"
def identity[T](value: T) -> T:
    return value

i = identity[int](42)
s = identity[str]('hello')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericClass_MultipleTypeParams_Works()
{
    var source = @"
class Pair[T, U]:
    first: T
    second: U
    
    def __init__(self, first: T, second: U):
        self.first = first
        self.second = second

pair = Pair[int, str](42, 'hello')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericWithConstraint_Works()
{
    var source = @"
interface IComparable:
    def compare_to(self, other: object) -> int:
        ...

def find_max[T: IComparable](a: T, b: T) -> T:
    if a.compare_to(b) > 0:
        return a
    return b
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Type Narrowing

[Fact]
public void TypeNarrowing_IsNotNone_Works()
{
    var source = @"
def process(value: str?) -> str:
    if value is not None:
        return value.upper()  # value is str here
    return 'empty'

result = process('hello')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion
```

---

## Task 0.1.9.14: Document Phase 0.1.9 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| `T?` compiles to `Nullable<T>` or reference `T?` | `NullableValueType_Works` | [ ] |
| Null coalescing `??` works | `NullCoalescing_Works` | [ ] |
| Null conditional `?.` works | `NullConditional_MemberAccess` | [ ] |
| Type aliases expand correctly | `TypeAlias_Simple_Works` | [ ] |
| Generic collection types instantiate | `GenericClass_Simple_Works` | [ ] |
| User-defined generic classes/functions compile | `GenericFunction_Works` | [ ] |
| Type constraints validated | `GenericWithConstraint_Works` | [ ] |

### Verification Process

```bash
# Run all Phase 0.1.9 tests
dotnet test --filter "Phase019" --logger "console;verbosity=detailed"

# Verify generated C# code
dotnet run --project src/Sharpy.Compiler -- compile test_nullable.spy --emit-csharp
dotnet run --project src/Sharpy.Compiler -- compile test_generics.spy --emit-csharp
```

---

## Summary: Task Dependencies

```
0.1.9.1 (Nullable Parsing) ──────┐
                                 │
0.1.9.2 (Nullable CodeGen) ──────┼──► 0.1.9.5 (Type Narrowing)
                                 │
0.1.9.3 (Null Coalescing) ───────┤
                                 │
0.1.9.4 (Null Conditional) ──────┤
                                 │
0.1.9.6 (Type Alias Parsing) ────┼──► 0.1.9.7 (Alias Semantic)
                                 │           │
                                 │           ▼
                                 │    0.1.9.8 (Alias CodeGen)
                                 │
0.1.9.9 (Generic Parsing) ───────┼──► 0.1.9.10 (Generic Class CodeGen)
                                 │           │
                                 │           ▼
                                 │    0.1.9.11 (Generic Func CodeGen)
                                 │           │
                                 │           ▼
                                 │    0.1.9.12 (Type Constraints)
                                 │           │
                                 ▼           ▼
                          0.1.9.13 (Integration Tests)
                                 │
                                 ▼
                          0.1.9.14 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 3-4 hours
- **Implementation tasks:** 16-22 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 23-31 hours

## Notes for Agent/Engineer

1. **Nullable value vs reference types:** `int?` is `Nullable<int>`, but `str?` is just `string?` (nullable reference).

2. **Type aliases are compile-time:** No C# declaration generated; aliases are expanded at every usage.

3. **Type narrowing scope:** Narrowed types only apply within the conditional block.

4. **Generic syntax uses `[T]`:** Unlike C#'s `<T>`, Sharpy uses square brackets for generics.

5. **Constraints use `:` syntax:** `T: IComparable` in Sharpy, becomes `where T : IComparable` in C#.

6. **Null conditional result is nullable:** `name?.upper()` returns `str?` even if `upper()` returns `str`.

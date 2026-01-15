# Phase 0.1.9: Type System Enhancements - Comprehensive Task List (v2)

**Goal:** Nullable types, generics, type aliases, type constraints, and null-handling operators.

**Prerequisites:** Phase 0.1.8 (Structs & Enums) must be complete.

**Exit Criteria:**
- Nullable types (`T?`) compile correctly for value and reference types
- `#nullable enable` in all generated C# (non-nullable by default)
- Null coalescing (`??`) works
- Null coalescing assignment (`??=`) works
- Null conditional (`?.`) works with correct result type inference
- Type narrowing in `if x is not None:` blocks
- Generic classes/methods with `[T]` syntax
- Type constraints (`: IComparable`, `: class`, `: struct`)
- Type aliases are compile-time expanded (not emitted to C#)
- Function type aliases map to `Func<>` / `Action<>`
- Nested nullable validation (`T??` is error)

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for nullable-related code
grep -rn "Nullable\|nullable\|\?" src/Sharpy.Compiler/

# Check for generic-related code
grep -rn "Generic\|TypeParam\|type_param" src/Sharpy.Compiler/

# Check for type alias handling
grep -rn "TypeAlias\|type.*=" src/Sharpy.Compiler/

# Check existing tests
find src -name "*.cs" -exec grep -l "Nullable\|Generic\|TypeAlias" {} \;
```

---

## Task 0.1.9.1: Implement Nullable Type Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Type.cs`

### Objective
Parse nullable type syntax (`T?`).

### Syntax
```python
x: int?             # Nullable int (value type)
name: str?          # Nullable string (reference type)
points: list[int?]  # List of nullable ints
data: list[int]?    # Nullable list of ints
both: list[int?]?   # Nullable list of nullable ints
```

### Grammar
```ebnf
type_annotation ::= base_type '?'?
                  | base_type '[' type_args ']' '?'?
```

### Actions

1. **Parse nullable marker:**
   - `?` suffix on type indicates nullable
   - Can apply to any type (value or reference)

2. **⚠️ Validate nested nullable:**
   ```python
   x: int??  # ERROR: Double nullable not meaningful
   ```
   - Error message: "Type 'int' cannot be made nullable twice"

3. **Handle nullable in generics:**
   ```python
   list[int?]    # List of nullable ints
   list[int]?    # Nullable list
   dict[str, int?]?  # Nullable dict with nullable int values
   ```

### AST Representation

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsNullable { get; init; }
}
```

---

## Task 0.1.9.2: Implement Nullable Type Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate correct C# nullable types.

### ⚠️ Key Requirement: `#nullable enable`

All generated C# files should include `#nullable enable` at the top. This enables C# nullable reference types (C# 8+) and aligns with Sharpy's "null-safe by default" principle (Axiom 3).

```csharp
#nullable enable

namespace MyProject
{
    // ...
}
```

### Type Mapping

| Sharpy | C# | Notes |
|--------|-----|-------|
| `int?` | `int?` | `Nullable<int>` |
| `str?` | `string?` | Nullable reference |
| `list[int]?` | `List<int>?` | Nullable list |
| `list[int?]` | `List<int?>` | List of nullable |
| `MyClass?` | `MyClass?` | Nullable reference |
| `MyStruct?` | `MyStruct?` | `Nullable<MyStruct>` |

### Implementation Hints

```csharp
private TypeSyntax MapType(TypeAnnotation type)
{
    var baseType = MapBaseType(type.Name, type.TypeArguments);
    
    if (type.IsNullable)
    {
        return NullableType(baseType);
    }
    
    return baseType;
}

// Add to file generation:
private CompilationUnitSyntax GenerateCompilationUnit(...)
{
    var unit = CompilationUnit()
        .WithUsings(...)
        .WithMembers(...);
    
    // Add #nullable enable directive
    unit = unit.WithLeadingTrivia(
        Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)));
    
    return unit;
}
```

---

## Task 0.1.9.3: Implement Null Coalescing Operator (`??`)

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null coalescing operator.

### Syntax
```python
result = value ?? default_value
```

### Actions

1. **Parse `??` operator:**
   - Binary operator with left associativity
   - Lower precedence than comparison operators

2. **Generate C# `??`:**
   ```python
   name = user_name ?? "Guest"
   ```
   
   **Generated C#:**
   ```csharp
   var name = userName ?? "Guest";
   ```

3. **Type inference:**
   - If left is `T?` and right is `T`, result is `T`
   - If both are `T?`, result is `T?`

---

## Task 0.1.9.4: Implement Null Coalescing Assignment (`??=`)

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null coalescing assignment operator.

### Syntax
```python
name: str? = None
name ??= "default"  # Assign only if name is None
```

### Generated C#
```csharp
string? name = null;
name ??= "default";
```

### Actions

1. **Parse `??=` as compound assignment:**
   - Target must be assignable (variable, field, property)
   - Left side must be nullable type

2. **Generate C# `??=`:**
   - Direct mapping to C# operator

---

## Task 0.1.9.5: Implement Null Conditional Operator (`?.`)

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null conditional operator with correct type inference.

### Syntax
```python
name: str? = "hello"
upper = name?.upper()  # upper is str? (not str!)
length = name?.length  # length is int?
```

### ⚠️ Result Type Inference

The result type of a null conditional expression is **always nullable**, even if the member returns non-nullable:

```python
class Person:
    def get_name(self) -> str:  # Returns non-nullable str
        return self.name

p: Person? = None
n = p?.get_name()  # n is str? (nullable) because p might be None
```

### Type Inference Rules

- `T?.member` where `member` has type `U` → result type is `U?`
- `T?.method()` where `method()` returns `U` → result type is `U?`
- Chained: `T?.a?.b` → result type is `typeof(b)?`

### Implementation Hints

```csharp
private ExpressionSyntax GenerateNullConditional(NullConditionalExpression expr)
{
    var target = GenerateExpression(expr.Target);
    var member = expr.Member;
    
    // Generate: target?.Member
    return ConditionalAccessExpression(
        target,
        MemberBindingExpression(IdentifierName(NameMangler.ToPascalCase(member))));
}

// In type checker:
private TypeInfo InferNullConditionalType(NullConditionalExpression expr)
{
    var targetType = InferType(expr.Target);
    // Must be nullable
    if (!targetType.IsNullable)
    {
        ReportWarning("Null conditional on non-nullable type has no effect");
    }
    
    var memberType = ResolveMemberType(targetType.UnwrapNullable(), expr.Member);
    
    // Result is ALWAYS nullable
    return memberType.MakeNullable();
}
```

---

## Task 0.1.9.6: Implement Type Narrowing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

### Objective
Narrow types within conditional blocks.

### Type Narrowing Patterns

1. **`is not None` check:**
   ```python
   def process(value: str?) -> str:
       if value is not None:
           # value is narrowed from str? to str
           return value.upper()
       return "empty"
   ```

2. **`is None` check (inverse narrowing):**
   ```python
   def process(value: str?) -> str:
       if value is None:
           return "empty"
       # After return, value is narrowed to str
       return value.upper()
   ```

3. **`isinstance` check:**
   ```python
   def handle(obj: Animal) -> str:
       if isinstance(obj, Dog):
           # obj is narrowed from Animal to Dog
           return obj.bark()
       return "unknown"
   ```

### Scope Rules

- Narrowing applies ONLY within the narrowing block
- Narrowing does NOT persist after the block ends
- Narrowing in `elif` is independent of previous `if`

### Implementation Hints

```csharp
private class TypeNarrowingScope
{
    public Dictionary<string, TypeInfo> NarrowedTypes { get; } = new();
}

private Stack<TypeNarrowingScope> _narrowingScopes = new();

private void AnalyzeIfStatement(IfStatement ifStmt)
{
    // Check condition for narrowing patterns
    if (IsNotNoneCheck(ifStmt.Condition, out var variable))
    {
        _narrowingScopes.Push(new TypeNarrowingScope());
        var currentType = GetVariableType(variable);
        _narrowingScopes.Peek().NarrowedTypes[variable] = currentType.UnwrapNullable();
        
        AnalyzeBody(ifStmt.ThenBody);
        
        _narrowingScopes.Pop();
    }
    else
    {
        AnalyzeBody(ifStmt.ThenBody);
    }
    
    // Handle else branch with inverse narrowing
    if (ifStmt.ElseBody != null)
    {
        // In else, the variable is still nullable (original type)
        AnalyzeBody(ifStmt.ElseBody);
    }
}

private TypeInfo GetVariableType(string name)
{
    // Check narrowing scopes first
    foreach (var scope in _narrowingScopes)
    {
        if (scope.NarrowedTypes.TryGetValue(name, out var narrowed))
            return narrowed;
    }
    
    return _symbolTable.GetVariable(name).Type;
}
```

---

## Task 0.1.9.7: Implement Type Alias Parsing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse type alias declarations.

### Syntax
```python
type UserId = int
type StringList = list[str]
type Callback = (int, str) -> bool
type Effect = () -> None
```

### Grammar
```ebnf
type_alias ::= 'type' identifier '=' type_expression
type_expression ::= type_annotation
                  | function_type
function_type ::= '(' [ type_list ] ')' '->' type_annotation
```

### Actions

1. **Create `TypeAlias` AST node:**
   ```csharp
   public record TypeAlias : Statement
   {
       public string Name { get; init; } = "";
       public TypeExpression Type { get; init; } = null!;
   }
   ```

2. **Parse type alias syntax:**
   - `type` keyword
   - Identifier (alias name)
   - `=` 
   - Type expression (including function types)

---

## Task 0.1.9.8: Implement Type Alias Expansion

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

### Objective
Expand type aliases at compile time (no C# output for aliases).

### Key Principle

Type aliases are **compile-time only**. They are expanded at every usage point, and NO C# declaration is generated for the alias itself.

### Example

```python
type UserId = int
type StringList = list[str]

def get_user(id: UserId) -> StringList:
    ...
```

**Generated C# (NO type alias declarations):**
```csharp
public List<string> GetUser(int id)
{
    ...
}
```

### Expansion Strategy

1. **First pass:** Register all type aliases in symbol table
2. **Type resolution:** Replace alias names with their expanded types
3. **Code generation:** Work only with expanded types

### Error Cases

```python
type Recursive = list[Recursive]  # ERROR: Recursive type alias
type Unknown = NonExistent        # ERROR: Undefined type in alias
```

### Function Type Alias Mapping

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

## Task 0.1.9.9: Implement Generic Type Parsing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse generic class/method definitions with `[T]` syntax.

### ⚠️ Key Syntax Difference from C#

Sharpy uses **square brackets** `[T]` for generics, not angle brackets `<T>`:

```python
class Box[T]:
    value: T

def find_max[T](a: T, b: T) -> T:
    ...

box = Box[int](42)  # Usage
```

### Grammar
```ebnf
type_params ::= '[' type_param { ',' type_param } [ ',' ] ']'
type_param ::= identifier [ ':' type_constraint ]
type_constraint ::= qualified_name [ '[' type_args ']' ]
                  | 'class'
                  | 'struct'
```

### Actions

1. **Parse type parameters:**
   - `[T]` → single type parameter
   - `[K, V]` → multiple type parameters
   - `[T: IComparable]` → type parameter with constraint

2. **Parse generic usage:**
   - `Box[int]` → generic instantiation
   - `list[str]` → built-in generic

---

## Task 0.1.9.10: Implement Generic Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Transform Sharpy `[T]` syntax to C# `<T>` syntax.

### Code Generation Examples

**Input:**
```python
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value

box = Box[int](42)
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

var box = new Box<int>(42);
```

### Implementation Hints

```csharp
private ClassDeclarationSyntax GenerateGenericClass(ClassDef classDef)
{
    var classDecl = ClassDeclaration(NameMangler.ToPascalCase(classDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add type parameters
    if (classDef.TypeParameters.Any())
    {
        var typeParams = classDef.TypeParameters
            .Select(tp => TypeParameter(tp.Name));
        
        classDecl = classDecl.WithTypeParameterList(
            TypeParameterList(SeparatedList(typeParams)));
    }
    
    // ... generate members
    
    return classDecl;
}
```

---

## Task 0.1.9.11: Implement Type Constraints

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate type constraints.

### Syntax
```python
def find_max[T: IComparable](a: T, b: T) -> T:  # Interface constraint
    ...

class Container[T: class](value: T):  # Reference type constraint
    ...

struct Wrapper[T: struct](value: T):  # Value type constraint
    ...
```

### Constraint Types

| Sharpy | C# |
|--------|-----|
| `T: IComparable` | `where T : IComparable` |
| `T: class` | `where T : class` |
| `T: struct` | `where T : struct` |
| `T: BaseClass` | `where T : BaseClass` |

### Generated C#

```csharp
public T FindMax<T>(T a, T b) where T : IComparable
{
    ...
}

public class Container<T> where T : class
{
    ...
}

public struct Wrapper<T> where T : struct
{
    ...
}
```

### ⚠️ Constraint Validation

1. **Constraint must exist:**
   ```python
   def foo[T: NonExistent](x: T):  # ERROR: NonExistent is not defined
   ```

2. **Constraint must be interface or class:**
   ```python
   def foo[T: int](x: T):  # ERROR: int is not a valid constraint
   ```

3. **`struct` and `class` are mutually exclusive:**
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

### Scope Decision

- **Phase 0.1.9:** Single constraints only
- **Deferred:** Multiple constraints (`T: IFoo & IBar`)
- **Deferred:** Generic variance (covariance/contravariance with `out`/`in`)

---

## Task 0.1.9.12: Create Phase 0.1.9 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase019IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void NullableValueType_CompilesCorrectly()
{
    var source = @"
x: int? = None
y: int? = 42

if x is not None:
    z = x + 1
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullableReferenceType_CompilesCorrectly()
{
    var source = @"
name: str? = None
name = 'hello'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullCoalescing_Works()
{
    var source = @"
value: str? = None
result = value ?? 'default'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullCoalescingAssignment_Works()
{
    var source = @"
value: str? = None
value ??= 'default'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_Works()
{
    var source = @"
class Person:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    def get_name(self) -> str:
        return self.name

p: Person? = None
name = p?.get_name()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_ResultTypeIsNullable()
{
    var source = @"
class Foo:
    def bar(self) -> int:
        return 42

f: Foo? = None
result = f?.bar()
result2: int? = result
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeNarrowing_IsNotNone()
{
    var source = @"
def process(value: str?) -> str:
    if value is not None:
        return value.upper()
    return 'empty'

result = process('hello')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void DoubleNullable_ProducesError()
{
    var source = @"
x: int?? = None
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("nullable twice", result.Error.ToLower());
}

[Fact]
public void TypeAlias_ExpandsCorrectly()
{
    var source = @"
type UserId = int

def get_user(id: UserId) -> str:
    return 'user'

result = get_user(42)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void FunctionTypeAlias_Works()
{
    var source = @"
type Predicate = (int) -> bool

def is_positive(x: int) -> bool:
    return x > 0

check: Predicate = is_positive
result = check(5)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericClass_CompilesAndRuns()
{
    var source = @"
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value

box = Box[int](42)
result = box.get()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericMethod_CompilesAndRuns()
{
    var source = @"
def identity[T](value: T) -> T:
    return value

result = identity[int](42)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeConstraint_Interface()
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

[Fact]
public void TypeConstraint_Class()
{
    var source = @"
class RefContainer[T: class]:
    value: T?
    
    def __init__(self):
        self.value = None

c = RefContainer[str]()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeConstraint_Struct()
{
    var source = @"
struct ValueWrapper[T: struct]:
    value: T

w = ValueWrapper[int]()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NestedNullable_InGenerics()
{
    var source = @"
values: list[int?] = [1, None, 3]
data: list[int]? = None
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void RecursiveTypeAlias_ProducesError()
{
    var source = @"
type Recursive = list[Recursive]
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("recursive", result.Error.ToLower());
}
```

---

## Task 0.1.9.13: Document Phase 0.1.9 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_9_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Nullable value type | `NullableValueType_CompilesCorrectly` | [ ] |
| Nullable reference type | `NullableReferenceType_CompilesCorrectly` | [ ] |
| `??` operator | `NullCoalescing_Works` | [ ] |
| `??=` operator | `NullCoalescingAssignment_Works` | [ ] |
| `?.` operator | `NullConditional_Works` | [ ] |
| `?.` result type nullable | `NullConditional_ResultTypeIsNullable` | [ ] |
| Type narrowing | `TypeNarrowing_IsNotNone` | [ ] |
| Double nullable error | `DoubleNullable_ProducesError` | [ ] |
| Type alias expansion | `TypeAlias_ExpandsCorrectly` | [ ] |
| Function type alias | `FunctionTypeAlias_Works` | [ ] |
| Generic class | `GenericClass_CompilesAndRuns` | [ ] |
| Generic method | `GenericMethod_CompilesAndRuns` | [ ] |
| Interface constraint | `TypeConstraint_Interface` | [ ] |
| Class constraint | `TypeConstraint_Class` | [ ] |
| Struct constraint | `TypeConstraint_Struct` | [ ] |
| Nested nullable generics | `NestedNullable_InGenerics` | [ ] |
| Recursive alias error | `RecursiveTypeAlias_ProducesError` | [ ] |

---

## Summary: Task Dependencies

```
0.1.9.1 (Nullable Parsing) ─────────────────────────────┐
                                                        │
0.1.9.7 (Type Alias Parsing) ───────────────────────────┼──► 0.1.9.2 (Nullable CodeGen)
                                                        │    0.1.9.3 (?? Operator)
0.1.9.9 (Generic Parsing) ──────────────────────────────┤    0.1.9.4 (??= Operator)
                                                        │    0.1.9.5 (?. Operator)
                                                        │    0.1.9.6 (Type Narrowing)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.9.8 (Alias Expansion)
                                                        │    0.1.9.10 (Generic CodeGen)
                                                        │    0.1.9.11 (Type Constraints)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.9.12 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.9.13 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing tasks:** 6-8 hours
- **Code generation tasks:** 8-12 hours
- **Semantic analysis tasks:** 4-6 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 22-31 hours

## Notes for Agent/Engineer

1. **`#nullable enable` is critical** — All generated C# must have this directive for proper null safety.

2. **`?.` result is ALWAYS nullable** — Even if the member returns non-nullable, the result is nullable.

3. **Type aliases are NOT emitted** — They expand at compile time; no C# declaration generated.

4. **Sharpy uses `[T]` not `<T>`** — Transform square brackets to angle brackets in code generation.

5. **Type narrowing is flow-sensitive** — Only applies within the narrowing block.

6. **`T??` is an error** — Double nullable is not meaningful.

7. **Function types → Func/Action** — `(T) -> R` → `Func<T, R>`, `(T) -> None` → `Action<T>`.

8. **Generic variance deferred** — No `out`/`in` modifiers in Phase 0.1.9.

# Walkthrough: SemanticInfo.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`

---

## 1. Overview

`SemanticInfo.cs` defines a **critical data structure** that acts as a bridge between the Abstract Syntax Tree (AST) and the semantic analysis results. Think of it as an "annotation layer" that enriches the immutable AST with type information, symbol bindings, and resolved references without modifying the AST nodes themselves.

**Key Purpose**: Store and retrieve semantic information computed during type checking and name resolution, associating this data with specific AST nodes.

**Role in the Compiler Pipeline**:
```
Parser → AST (immutable)
           ↓
Semantic Analyzer → SemanticInfo (mutable annotations)
           ↓
Code Generator → Uses SemanticInfo to emit correct C# code
```

The `SemanticInfo` class maintains several dictionaries that map AST nodes to their semantic meaning, allowing later compilation stages to query "What type does this expression have?" or "Which function does this call target?"

---

## 2. Class Structure

### `SemanticInfo` Class

```csharp
public class SemanticInfo
```

This is a **simple, focused class** with four private dictionaries and eight public methods (getters/setters). It follows a clean separation of concerns pattern where:
- **Private fields**: Store the mappings internally
- **Public methods**: Provide controlled access to set and retrieve semantic information

### Four Core Dictionaries

The class maintains four distinct mappings, each serving a specific purpose:

#### 1. Expression Type Mapping
```csharp
private readonly Dictionary<Expression, SemanticType> _expressionTypes = new();
```
**Purpose**: Maps any expression in the AST to its computed type.

**Examples**:
- `x + 5` → `SemanticType.Int`
- `"hello"` → `SemanticType.Str`
- `myFunction()` → Whatever the function returns
- `myList[0]` → Element type of the list

#### 2. Identifier Symbol Mapping
```csharp
private readonly Dictionary<Identifier, Symbol> _identifierSymbols = new();
```
**Purpose**: Links identifier nodes to their symbol table entries.

**Examples**:
- Variable reference `x` → `VariableSymbol` (includes type, scope, declaration location)
- Function name `greet` → `FunctionSymbol` (includes parameters, return type)
- Class name `MyClass` → `TypeSymbol`

#### 3. Function Call Target Mapping
```csharp
private readonly Dictionary<FunctionCall, FunctionSymbol> _callTargets = new();
```
**Purpose**: Associates function call expressions with the specific function being called (after overload resolution).

**Why separate from identifier mapping?**: A function call like `obj.method()` needs to know:
- Which method on which type
- Overload resolution results
- Whether it's virtual/static/instance

#### 4. Type Annotation Mapping
```csharp
private readonly Dictionary<TypeAnnotation, SemanticType> _typeAnnotations = new();
```
**Purpose**: Resolves type annotations from syntax to semantic types.

**Examples**:
- Type annotation `int` → `SemanticType.Int`
- Type annotation `list[str]` → `GenericType(List, [SemanticType.Str])`
- Type annotation `Optional[int]` → `NullableType(SemanticType.Int)`

---

## 3. Key Methods

All methods follow a consistent **setter/getter pattern** with nullable return types for getters.

### Expression Type Methods

#### `SetExpressionType(Expression expr, SemanticType type)`
```csharp
public void SetExpressionType(Expression expr, SemanticType type)
{
    _expressionTypes[expr] = type;
}
```

**When called**: During type checking, after the type checker determines an expression's type.

**Example usage in TypeChecker**:
```csharp
// After checking x + y, we know the result type
var leftType = CheckExpression(binaryOp.Left);
var rightType = CheckExpression(binaryOp.Right);
var resultType = InferBinaryOpType(leftType, rightType, binaryOp.Op);
_semanticInfo.SetExpressionType(binaryOp, resultType);
```

**Important**: Uses AST node object identity as the key, so the same expression node always maps to the same type.

#### `GetExpressionType(Expression expr)`
```csharp
public SemanticType? GetExpressionType(Expression expr)
{
    return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
}
```

**When called**: 
1. **Caching during type checking** - TypeChecker checks this first to avoid re-analyzing expressions
2. **Code generation** - RoslynEmitter queries this to emit correctly typed C# code

**Returns**: `null` if the expression hasn't been analyzed yet (shouldn't happen in correct compilation flow).

**Example from TypeChecker.cs**:
```csharp
public SemanticType CheckExpression(Expression expr)
{
    // Check cache first - avoid redundant analysis
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
        return cached;
    
    // ... perform type checking ...
    var type = /* computed type */;
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

### Identifier Symbol Methods

#### `SetIdentifierSymbol(Identifier id, Symbol symbol)`
```csharp
public void SetIdentifierSymbol(Identifier id, Symbol symbol)
{
    _identifierSymbols[id] = symbol;
}
```

**When called**: After name resolution determines which declaration an identifier refers to.

**Example scenarios**:
```python
# In Sharpy code:
x = 5        # SetIdentifierSymbol(x, VariableSymbol for x)
print(x)     # SetIdentifierSymbol(x, same VariableSymbol)
```

**Critical for**:
- Variable shadowing (different scopes, different symbols)
- Distinguishing between local vs parameter vs field
- Tracking declaration locations for error messages

#### `GetIdentifierSymbol(Identifier id)`
```csharp
public Symbol? GetIdentifierSymbol(Identifier id)
{
    return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
}
```

**When called**: Code generation needs to know what an identifier refers to.

**Example use case**:
```csharp
// When generating C# code for an identifier
var symbol = _semanticInfo.GetIdentifierSymbol(identifier);
if (symbol is VariableSymbol varSymbol)
{
    // Generate local variable access
    return varSymbol.Name;
}
else if (symbol is FunctionSymbol funcSymbol)
{
    // Generate method reference
    return funcSymbol.Name;
}
```

### Function Call Target Methods

#### `SetCallTarget(FunctionCall call, FunctionSymbol target)`
```csharp
public void SetCallTarget(FunctionCall call, FunctionSymbol target)
{
    _callTargets[call] = target;
}
```

**When called**: After the type checker resolves which specific function a call expression invokes.

**Why this matters**:
- **Overload resolution**: `print(5)` vs `print("hello")` may call different overloads
- **Virtual dispatch**: Need to know if calling base or derived method
- **Static vs instance**: `MyClass.static_method()` vs `obj.instance_method()`

**Example**:
```python
# Sharpy code
def greet(name: str) -> None:
    print(f"Hello, {name}")

greet("World")  # SetCallTarget(this_call, greet's FunctionSymbol)
```

#### `GetCallTarget(FunctionCall call)`
```csharp
public FunctionSymbol? GetCallTarget(FunctionCall call)
{
    return _callTargets.TryGetValue(call, out var target) ? target : null;
}
```

**When called**: Code generation needs to emit the correct C# method call.

**Example in code generator**:
```csharp
var target = _semanticInfo.GetCallTarget(functionCall);
if (target?.ClrMethod != null)
{
    // Calling a .NET method - use its CLR name
    EmitDotNetMethodCall(target.ClrMethod);
}
else
{
    // Calling a Sharpy function - use mangled name
    EmitSharpyFunctionCall(target.Name);
}
```

### Type Annotation Methods

#### `SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)`
```csharp
public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
{
    _typeAnnotations[annotation] = type;
}
```

**When called**: TypeResolver processes type annotations and resolves them to semantic types.

**Example**:
```python
# Sharpy code
x: int = 5              # SetTypeAnnotation("int" annotation, SemanticType.Int)
names: list[str] = []   # SetTypeAnnotation("list[str]", GenericType(...))
```

#### `GetTypeAnnotation(TypeAnnotation annotation)`
```csharp
public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
{
    return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
}
```

**When called**: Type checker needs the resolved type from an annotation to validate assignments.

**Example**:
```csharp
// Checking: x: int = "hello"  (type error!)
var annotationType = _semanticInfo.GetTypeAnnotation(varDecl.TypeAnnotation);
var valueType = CheckExpression(varDecl.Value);
if (!valueType.IsAssignableTo(annotationType))
{
    ReportError($"Cannot assign {valueType} to {annotationType}");
}
```

---

## 4. Dependencies

### Input Dependencies (AST Types)

From `Sharpy.Compiler.Parser.Ast`:
- **`Expression`** - Base class for all expression AST nodes
- **`Identifier`** - Specific expression type for variable/function names
- **`FunctionCall`** - Specific expression type for function calls
- **`TypeAnnotation`** - AST node representing type syntax (e.g., `int`, `list[str]`)

### Output Dependencies (Semantic Types)

From `Sharpy.Compiler.Semantic`:
- **`SemanticType`** - Abstract base class for all semantic types (`Int`, `Str`, `UserDefinedType`, etc.)
- **`Symbol`** - Base class for symbol table entries (`VariableSymbol`, `FunctionSymbol`, `TypeSymbol`)
- **`FunctionSymbol`** - Specific symbol type for functions/methods

### Used By

- **`TypeChecker`** - Primary writer, sets most of the semantic info
- **`TypeResolver`** - Sets type annotation mappings
- **`RoslynEmitter` (CodeGen)** - Primary reader, queries info to generate C# code
- **`AccessValidator`** - May query symbol information for access checks

---

## 5. Design Patterns and Decisions

### Annotation Pattern (Separated Concerns)

**Design Decision**: Keep AST immutable, store semantic information separately.

**Rationale**:
- ✅ **AST reusability**: Same AST can be analyzed multiple times (incremental compilation)
- ✅ **Thread safety**: Immutable AST can be shared across threads
- ✅ **Clear separation**: Parsing vs semantic analysis are distinct phases
- ✅ **Easier testing**: Test AST generation without requiring semantic analysis

**Alternative rejected**: Storing semantic info directly on AST nodes would couple parsing and analysis.

### Dictionary-Based Indexing

**Why dictionaries?**
- O(1) lookup by AST node reference
- Natural mapping from AST nodes to semantic data
- Simple, well-understood data structure

**Key requirement**: AST nodes must have **reference equality** (same object = same node). This is why AST nodes are classes, not structs.

### Nullable Return Types

All getter methods return nullable types (`SemanticType?`, `Symbol?`, `FunctionSymbol?`).

**Why?**
- **Graceful handling** of unanalyzed code
- **Error recovery**: Compiler can continue after errors
- **Partial compilation**: Some expressions may not be fully analyzed yet

**Pattern in code**:
```csharp
var type = _semanticInfo.GetExpressionType(expr);
if (type == null)
{
    // Handle missing type (shouldn't happen in correct flow)
    return SemanticType.Unknown; // Error recovery
}
```

### No Removal Methods

Notice there are **no methods to remove or clear entries**. This is intentional:
- Semantic info is **write-once**
- No need to invalidate cached data during a single compilation
- Simplifies reasoning about data consistency

### Separate Call Target Mapping

**Question**: Why not just use `GetIdentifierSymbol` for function calls?

**Answer**: Function calls are complex:
```python
# These all look up different things:
greet                   # Identifier → FunctionSymbol
greet("World")          # FunctionCall → requires overload resolution
obj.method()            # FunctionCall → requires method resolution on type
super().method()        # FunctionCall → requires base class lookup
```

Separating `_callTargets` allows storing the **resolved, specific function** after considering:
- Overload resolution
- Generic type arguments
- Virtual dispatch
- Extension methods (future)

---

## 6. Debugging Tips

### Common Issues

#### Issue 1: Null Type Returned
**Symptom**: `GetExpressionType` returns `null` during code generation.

**Likely causes**:
1. Type checker didn't visit this expression
2. Type checker encountered an error and skipped analysis
3. AST node instance mismatch (different object, same content)

**How to debug**:
```csharp
// Add logging in TypeChecker
public SemanticType CheckExpression(Expression expr)
{
    var cached = _semanticInfo.GetExpressionType(expr);
    Console.WriteLine($"Cached type for {expr.GetType().Name}: {cached?.GetDisplayName() ?? "null"}");
    // ...
}
```

#### Issue 2: Wrong Symbol Retrieved
**Symptom**: `GetIdentifierSymbol` returns a different symbol than expected.

**Likely causes**:
1. Variable shadowing - multiple symbols with same name in different scopes
2. Name resolution bug - wrong symbol added
3. AST node reused when it shouldn't be

**How to debug**:
```csharp
// Check all symbols with same name
var allSymbols = _symbolTable.LookupAll(identifier.Name);
Console.WriteLine($"Found {allSymbols.Count} symbols named '{identifier.Name}'");
foreach (var sym in allSymbols)
{
    Console.WriteLine($"  - {sym.Kind} at line {sym.DeclarationLine}");
}
```

#### Issue 3: Duplicate Entries
**Symptom**: Same expression is being annotated multiple times with different types.

**Likely causes**:
1. Expression visited multiple times during type checking
2. Type inference changed midway (shouldn't happen)
3. AST node mutation (violated immutability)

**How to debug**:
```csharp
public void SetExpressionType(Expression expr, SemanticType type)
{
    if (_expressionTypes.TryGetValue(expr, out var existing))
    {
        if (!existing.Equals(type))
        {
            Console.WriteLine($"WARNING: Changing type of {expr} from {existing.GetDisplayName()} to {type.GetDisplayName()}");
            // Set breakpoint here
        }
    }
    _expressionTypes[expr] = type;
}
```

### Inspection Tools

#### Dump All Semantic Info
```csharp
public void DumpSemanticInfo(SemanticInfo info)
{
    Console.WriteLine("=== Expression Types ===");
    foreach (var (expr, type) in info._expressionTypes)
    {
        Console.WriteLine($"{expr.GetType().Name} -> {type.GetDisplayName()}");
    }
    
    Console.WriteLine("\n=== Identifier Symbols ===");
    foreach (var (id, symbol) in info._identifierSymbols)
    {
        Console.WriteLine($"{id.Name} -> {symbol.Kind} ({symbol.Name})");
    }
    
    // Similar for call targets and type annotations
}
```

#### Validate Consistency
```csharp
public void ValidateSemanticInfo()
{
    // Check all call targets have corresponding expression types
    foreach (var (call, target) in _callTargets)
    {
        var exprType = _expressionTypes.GetValueOrDefault(call);
        if (exprType == null)
        {
            Console.WriteLine($"WARNING: Call to {target.Name} missing expression type");
        }
    }
}
```

---

## 7. Contribution Guidelines

### When to Modify This File

#### ✅ Add New Mapping Types
If you're adding a new semantic analysis feature that needs to associate AST nodes with semantic data:

**Example**: Adding lambda capture information
```csharp
// Add new dictionary
private readonly Dictionary<Lambda, CaptureInfo> _lambdaCaptures = new();

// Add getter/setter
public void SetLambdaCapture(Lambda lambda, CaptureInfo capture)
{
    _lambdaCaptures[lambda] = capture;
}

public CaptureInfo? GetLambdaCapture(Lambda lambda)
{
    return _lambdaCaptures.TryGetValue(lambda, out var info) ? info : null;
}
```

#### ✅ Add Utility Methods
If you need common queries that combine multiple mappings:

**Example**: Get the full type of a call expression
```csharp
public SemanticType? GetCallReturnType(FunctionCall call)
{
    var target = GetCallTarget(call);
    return target?.ReturnType;
}
```

#### ✅ Add Validation Methods
For debugging and testing:

```csharp
public bool HasCompleteInfo(Expression expr)
{
    return GetExpressionType(expr) != null;
}

public IEnumerable<Expression> GetUntypedExpressions()
{
    // Return expressions that should have types but don't
}
```

### What NOT to Change

#### ❌ Don't Add Complex Logic
This class should remain a **simple data store**. Complex analysis logic belongs in:
- `TypeChecker` - Type analysis
- `NameResolver` - Symbol resolution  
- `TypeResolver` - Type annotation processing

#### ❌ Don't Break Immutability Contract
The AST should remain immutable. Don't add methods that modify AST nodes.

#### ❌ Don't Add Caching Logic
The dictionaries already provide O(1) lookup. Don't add additional caching layers.

### Testing Guidelines

When adding new mappings:

1. **Test setter/getter**:
```csharp
[Fact]
public void SetAndGetLambdaCapture()
{
    var semanticInfo = new SemanticInfo();
    var lambda = new Lambda { /* ... */ };
    var capture = new CaptureInfo { /* ... */ };
    
    semanticInfo.SetLambdaCapture(lambda, capture);
    var retrieved = semanticInfo.GetLambdaCapture(lambda);
    
    Assert.Equal(capture, retrieved);
}
```

2. **Test null handling**:
```csharp
[Fact]
public void GetLambdaCapture_ReturnsNull_WhenNotSet()
{
    var semanticInfo = new SemanticInfo();
    var lambda = new Lambda { /* ... */ };
    
    var result = semanticInfo.GetLambdaCapture(lambda);
    
    Assert.Null(result);
}
```

3. **Test in integration**:
Ensure TypeChecker and CodeGen use the new mapping correctly.

### Documentation Standards

When adding new mappings, update:
1. XML doc comments on methods
2. This walkthrough document (add to "Four Core Dictionaries" section)
3. Any architecture docs that describe semantic analysis

### Performance Considerations

This class is used heavily during compilation. When adding new features:

- ✅ Use `Dictionary` for O(1) lookup
- ✅ Use `TryGetValue` to avoid double lookup
- ❌ Don't iterate over entire dictionaries in hot paths
- ❌ Don't add synchronization (not thread-safe by design - single-threaded compilation)

### Example: Adding Support for Decorator Metadata

Let's say you need to track which decorators are applied to functions:

```csharp
// 1. Add dictionary
private readonly Dictionary<FunctionDef, List<DecoratorInfo>> _functionDecorators = new();

// 2. Add setter (append to list)
public void AddFunctionDecorator(FunctionDef func, DecoratorInfo decorator)
{
    if (!_functionDecorators.ContainsKey(func))
        _functionDecorators[func] = new List<DecoratorInfo>();
    _functionDecorators[func].Add(decorator);
}

// 3. Add getter
public List<DecoratorInfo>? GetFunctionDecorators(FunctionDef func)
{
    return _functionDecorators.TryGetValue(func, out var decorators) ? decorators : null;
}

// 4. Test it
[Fact]
public void FunctionDecorators_CanBeSetAndRetrieved()
{
    var semanticInfo = new SemanticInfo();
    var funcDef = new FunctionDef { Name = "test" };
    var decorator = new DecoratorInfo { Name = "@property" };
    
    semanticInfo.AddFunctionDecorator(funcDef, decorator);
    var decorators = semanticInfo.GetFunctionDecorators(funcDef);
    
    Assert.NotNull(decorators);
    Assert.Single(decorators);
    Assert.Equal("@property", decorators[0].Name);
}

// 5. Use in TypeChecker
public void CheckFunctionDef(FunctionDef func)
{
    foreach (var decorator in func.Decorators)
    {
        var decoratorInfo = AnalyzeDecorator(decorator);
        _semanticInfo.AddFunctionDecorator(func, decoratorInfo);
    }
}

// 6. Use in CodeGen
public void EmitFunction(FunctionDef func)
{
    var decorators = _semanticInfo.GetFunctionDecorators(func);
    if (decorators?.Any(d => d.Name == "@property") == true)
    {
        // Emit as C# property
    }
}
```

---

## Summary

`SemanticInfo.cs` is the **semantic annotation layer** for the Sharpy compiler. It:

- 🎯 **Stores** type information, symbol bindings, and resolved references
- 🔄 **Bridges** the immutable AST with mutable semantic analysis results  
- 📊 **Enables** the code generator to emit correctly typed C# code
- 🏗️ **Follows** the separation of concerns principle (parsing ≠ analysis ≠ generation)

**Key takeaway**: This is a simple but critical infrastructure component. It should remain simple, focused, and easy to understand. Complex logic belongs in the analysis phases that **use** this class, not in the class itself.

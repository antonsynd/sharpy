# Walkthrough: SemanticInfo.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`

---

## Overview

`SemanticInfo` is a central data structure in the Sharpy compiler's semantic analysis phase that serves as an **annotation layer** for the Abstract Syntax Tree (AST). Instead of modifying AST nodes directly (which are immutable records), this class maintains separate dictionaries that map AST nodes to their semantic information—types, symbols, and resolved references.

**Key Design Philosophy**: The Sharpy compiler follows the principle of **immutable AST + external semantic storage**. This means:
- AST nodes stay clean and represent only syntactic structure
- Semantic information (types, symbol bindings) is stored separately in `SemanticInfo`
- Multiple compiler passes can query and update semantic information without AST modifications

**Role in the Compilation Pipeline**:
```
Lexer → Parser (AST) → NameResolver → TypeResolver → TypeChecker → CodeGen
                                ↓           ↓            ↓
                              SemanticInfo (stores all type/symbol mappings)
```

Think of `SemanticInfo` as a **side-by-side annotation database** that answers questions like:
- "What type does this expression have?"
- "What symbol does this identifier refer to?"
- "Which function does this call resolve to?"
- "What semantic type does this type annotation represent?"

---

## Class Structure

### Main Class: `SemanticInfo`

```csharp
public class SemanticInfo
```

A simple class with four private dictionaries that map AST nodes to their semantic information. It uses the **dictionary pattern** for O(1) lookups during code generation and later compiler phases.

### Internal State (Four Core Dictionaries)

#### 1. Expression Type Map
```csharp
private readonly Dictionary<Expression, SemanticType> _expressionTypes = new();
```

**Purpose**: Maps every expression in the AST to its resolved type.

**Examples**:
- `IntegerLiteral(42)` → `SemanticType.Int`
- `BinaryOp(x, "+", y)` → `SemanticType.Int` (if x and y are ints)
- `FunctionCall("len", [my_list])` → `SemanticType.Int`
- `Identifier("my_var")` → `SemanticType.Str` (if my_var is declared as string)

**When it's populated**: During the `TypeChecker` phase, after name resolution and type resolution.

#### 2. Identifier Symbol Map
```csharp
private readonly Dictionary<Identifier, Symbol> _identifierSymbols = new();
```

**Purpose**: Maps identifier AST nodes to the symbols they reference in the symbol table.

**Examples**:
- `Identifier("counter")` → `VariableSymbol { Name = "counter", Type = SemanticType.Int }`
- `Identifier("print")` → `FunctionSymbol { Name = "print", Parameters = [...], ReturnType = Void }`
- `Identifier("MyClass")` → `TypeSymbol { Name = "MyClass", TypeKind = Class }`

**When it's populated**: During `TypeChecker` when identifiers are resolved.

**Why it's needed**: The same identifier name can refer to different symbols in different scopes. This map connects each specific identifier AST node to the exact symbol it refers to.

#### 3. Function Call Target Map
```csharp
private readonly Dictionary<FunctionCall, FunctionSymbol> _callTargets = new();
```

**Purpose**: Maps function call AST nodes to the specific function symbol being invoked.

**Examples**:
- `FunctionCall("max", [a, b])` → `FunctionSymbol { Name = "max", ... }`
- `FunctionCall("obj.method", [])` → `FunctionSymbol { Name = "method", IsStatic = false, ... }`

**When it's populated**: During `TypeChecker` after overload resolution (selecting the right function when multiple overloads exist).

**Why it's separate from identifier map**: 
- Function calls can be complex (method calls, property accesses, etc.)
- Overload resolution may select a different function than the identifier alone suggests
- Helps the code generator emit correct C# method invocations

#### 4. Type Annotation Map
```csharp
private readonly Dictionary<TypeAnnotation, SemanticType> _typeAnnotations = new();
```

**Purpose**: Maps type annotations in the source code to their resolved semantic types.

**Examples**:
- `TypeAnnotation("int")` → `SemanticType.Int`
- `TypeAnnotation("list[str]")` → `GenericType { BaseType = List, TypeArgs = [SemanticType.Str] }`
- `TypeAnnotation("Optional[User]")` → `OptionalType { InnerType = UserDefinedType("User") }`

**When it's populated**: During `TypeResolver` phase, before type checking.

**Why it's needed**: Type annotations in source code are just syntax. This map stores the actual semantic types they represent, handling generics, imports, and type aliases.

---

## Key Methods

### Expression Type Management

#### `SetExpressionType(Expression expr, SemanticType type)`
```csharp
public void SetExpressionType(Expression expr, SemanticType type)
{
    _expressionTypes[expr] = type;
}
```

**Purpose**: Associate a semantic type with an expression node.

**Parameters**:
- `expr`: The expression AST node (e.g., `BinaryOp`, `FunctionCall`, `Identifier`)
- `type`: The resolved semantic type (e.g., `SemanticType.Int`, `UserDefinedType`)

**Usage Pattern**: Called by `TypeChecker` after determining an expression's type:
```csharp
// In TypeChecker.cs
var leftType = CheckExpression(binaryOp.Left);
var rightType = CheckExpression(binaryOp.Right);
var resultType = ResolveOperatorType(binaryOp.Op, leftType, rightType);
_semanticInfo.SetExpressionType(binaryOp, resultType);  // Store the result
```

**Important**: Always set the type **after** fully type-checking an expression, including all subexpressions.

#### `GetExpressionType(Expression expr)`
```csharp
public SemanticType? GetExpressionType(Expression expr)
{
    return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
}
```

**Purpose**: Retrieve the semantic type for an expression.

**Returns**: 
- The `SemanticType` if the expression has been type-checked
- `null` if not yet resolved (caller should handle this)

**Usage Pattern**: Used during later type checking and code generation:
```csharp
// In TypeChecker.cs - checking assignment
var valueType = _semanticInfo.GetExpressionType(assignStmt.Value);
if (valueType == null)
{
    // Expression hasn't been checked yet - error recovery
    valueType = SemanticType.Unknown;
}

// In RoslynEmitter.cs - generating C# code
var exprType = _semanticInfo.GetExpressionType(expr);
if (exprType is GenericType genericType)
{
    // Generate appropriate generic C# code
}
```

**Best Practice**: Always check for `null` and handle missing type information gracefully.

---

### Identifier Symbol Management

#### `SetIdentifierSymbol(Identifier id, Symbol symbol)`
```csharp
public void SetIdentifierSymbol(Identifier id, Symbol symbol)
{
    _identifierSymbols[id] = symbol;
}
```

**Purpose**: Link an identifier AST node to its symbol in the symbol table.

**Parameters**:
- `id`: The identifier AST node
- `symbol`: The symbol this identifier refers to (could be `VariableSymbol`, `FunctionSymbol`, or `TypeSymbol`)

**Usage Pattern**: Called during type checking when identifiers are resolved:
```csharp
// In TypeChecker.cs
var symbol = _symbolTable.Lookup(identifier.Name);
if (symbol == null)
{
    Error($"Undefined identifier: {identifier.Name}");
    return SemanticType.Unknown;
}
_semanticInfo.SetIdentifierSymbol(identifier, symbol);
```

**Design Note**: This binding happens at the **use site**, not the declaration site. Each occurrence of an identifier gets its own mapping.

#### `GetIdentifierSymbol(Identifier id)`
```csharp
public Symbol? GetIdentifierSymbol(Identifier id)
{
    return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
}
```

**Purpose**: Retrieve the symbol an identifier refers to.

**Returns**: The `Symbol` or `null` if not yet resolved.

**Usage Pattern**: Used during code generation to emit correct C# references:
```csharp
// In RoslynEmitter.cs
var symbol = _semanticInfo.GetIdentifierSymbol(identifier);
if (symbol is VariableSymbol varSym)
{
    return SyntaxFactory.IdentifierName(varSym.Name);
}
else if (symbol is FunctionSymbol funcSym)
{
    // Handle function reference differently
}
```

---

### Function Call Target Management

#### `SetCallTarget(FunctionCall call, FunctionSymbol target)`
```csharp
public void SetCallTarget(FunctionCall call, FunctionSymbol target)
{
    _callTargets[call] = target;
}
```

**Purpose**: Record which specific function a call resolves to (important for overload resolution).

**Parameters**:
- `call`: The function call AST node
- `target`: The resolved function symbol (after overload selection)

**Usage Pattern**: Called after overload resolution:
```csharp
// In TypeChecker.cs
var candidates = _symbolTable.LookupOverloads(call.FunctionName);
var bestMatch = SelectBestOverload(candidates, argumentTypes);
_semanticInfo.SetCallTarget(call, bestMatch);  // Store the winner
```

**Why this matters**: 
- Python allows function overloading (via decorators or .NET interop)
- The code generator needs to know **exactly** which overload was selected
- This enables accurate C# method invocation generation

#### `GetCallTarget(FunctionCall call)`
```csharp
public FunctionSymbol? GetCallTarget(FunctionCall call)
{
    return _callTargets.TryGetValue(call, out var target) ? target : null;
}
```

**Purpose**: Retrieve the resolved function for a call site.

**Usage Pattern**: Used in code generation:
```csharp
// In RoslynEmitter.cs
var funcSymbol = _semanticInfo.GetCallTarget(functionCall);
if (funcSymbol?.ClrMethod != null)
{
    // Generate call to .NET method
    GenerateClrMethodCall(funcSymbol.ClrMethod, arguments);
}
```

---

### Type Annotation Management

#### `SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)`
```csharp
public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
{
    _typeAnnotations[annotation] = type;
}
```

**Purpose**: Store the resolved semantic type for a type annotation.

**Parameters**:
- `annotation`: The type annotation AST node (e.g., from `x: int` or `def foo() -> str:`)
- `type`: The resolved semantic type

**Usage Pattern**: Called during type resolution:
```csharp
// In TypeResolver.cs
public SemanticType ResolveTypeAnnotation(TypeAnnotation annotation)
{
    // Check cache first
    var cached = _semanticInfo.GetTypeAnnotation(annotation);
    if (cached != null) return cached;
    
    // Resolve the type
    var resolved = ResolveTypeFromAnnotation(annotation);
    _semanticInfo.SetTypeAnnotation(annotation, resolved);
    return resolved;
}
```

**Performance Note**: This acts as a **memoization cache** to avoid resolving the same type annotation multiple times.

#### `GetTypeAnnotation(TypeAnnotation annotation)`
```csharp
public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
{
    return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
}
```

**Purpose**: Retrieve the resolved semantic type for a type annotation.

**Usage Pattern**: Used for cache lookup before resolving:
```csharp
// In TypeResolver.cs - avoid duplicate work
var cached = _semanticInfo.GetTypeAnnotation(annotation);
if (cached != null)
{
    return cached;  // Already resolved
}
```

---

## Dependencies

### Direct Dependencies

1. **`Sharpy.Compiler.Parser.Ast`** - All AST node types used as dictionary keys
   - `Expression` - Base class for all expressions
   - `Identifier` - Specific expression for names
   - `FunctionCall` - Function invocation nodes
   - `TypeAnnotation` - Type syntax nodes

2. **`Sharpy.Compiler.Semantic` namespace** (implicit)
   - `SemanticType` - Type system representation
   - `Symbol` - Symbol table entries (`VariableSymbol`, `FunctionSymbol`, `TypeSymbol`)

### Consumers (Who Uses SemanticInfo)

1. **`TypeChecker`** - Primary writer
   - Populates all four dictionaries during type checking
   - Queries expression types for type inference
   - Uses caching to avoid redundant checks

2. **`TypeResolver`** - Annotation resolver
   - Populates and queries type annotation mappings
   - Uses it as a cache for resolved types

3. **`RoslynEmitter` (CodeGen)** - Primary reader
   - Queries expression types to generate correct C# types
   - Looks up identifier symbols for name mangling
   - Checks call targets for method invocation generation

4. **`AccessValidator`** - Access control checker
   - Uses semantic info to validate member access
   - Checks visibility rules

---

## Design Patterns and Decisions

### Pattern 1: External Annotation (Separation of Concerns)

**Design**: AST nodes are kept immutable; semantic info is stored externally.

**Benefits**:
- **Immutability**: AST can be shared safely across compiler phases
- **Clean separation**: Syntax vs. semantics
- **Flexibility**: Multiple semantic analyses can annotate the same AST differently

**Alternative considered**: Storing types/symbols directly on AST nodes (rejected because it violates immutability and mixes concerns)

### Pattern 2: Dictionary-Based Lookup

**Design**: Use `Dictionary<TNode, TInfo>` for O(1) lookup.

**Benefits**:
- Fast queries during code generation (no tree traversal)
- Simple API (Set/Get pattern)
- Memory overhead is acceptable for typical programs

**Trade-off**: AST nodes must have proper `Equals`/`GetHashCode` (records provide this automatically)

### Pattern 3: Nullable Returns

**Design**: All `Get*` methods return nullable types (`SemanticType?`, `Symbol?`, `FunctionSymbol?`)

**Rationale**:
- Not all nodes are guaranteed to be annotated (error recovery)
- Caller must handle missing information explicitly
- Prevents accidental null reference exceptions

**Best Practice**: Always check for null:
```csharp
var type = _semanticInfo.GetExpressionType(expr);
if (type == null)
{
    // Handle missing type info - error recovery
    type = SemanticType.Unknown;
}
```

### Pattern 4: No Validation in SemanticInfo

**Design**: `SemanticInfo` is a **dumb storage container**—it doesn't validate inputs.

**Rationale**:
- Validation happens in `TypeChecker`, `TypeResolver`, etc.
- `SemanticInfo` focuses solely on storage/retrieval
- Keeps the class simple and testable

**Implication**: Callers are responsible for ensuring they set correct information.

---

## Common Usage Scenarios

### Scenario 1: Type Checking an Expression

```csharp
// In TypeChecker.cs
public SemanticType CheckExpression(Expression expr)
{
    // Check if already computed (caching)
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
        return cached;
    
    // Type check based on expression kind
    SemanticType type = expr switch
    {
        IntegerLiteral => SemanticType.Int,
        BinaryOp binOp => CheckBinaryOp(binOp),
        FunctionCall call => CheckFunctionCall(call),
        // ... other cases
        _ => SemanticType.Unknown
    };
    
    // Store the result
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

### Scenario 2: Resolving an Identifier

```csharp
// In TypeChecker.cs
public SemanticType CheckIdentifier(Identifier id)
{
    // Look up symbol in symbol table
    var symbol = _symbolTable.Lookup(id.Name);
    if (symbol == null)
    {
        Error($"Undefined identifier: {id.Name}");
        return SemanticType.Unknown;
    }
    
    // Store the symbol binding
    _semanticInfo.SetIdentifierSymbol(id, symbol);
    
    // Get the type from the symbol
    var type = symbol switch
    {
        VariableSymbol varSym => varSym.Type,
        FunctionSymbol funcSym => funcSym.ReturnType,
        TypeSymbol typeSym => SemanticType.Type,
        _ => SemanticType.Unknown
    };
    
    // Store and return the type
    _semanticInfo.SetExpressionType(id, type);
    return type;
}
```

### Scenario 3: Code Generation

```csharp
// In RoslynEmitter.cs
private CSharpSyntaxNode EmitExpression(Expression expr)
{
    // Get the type info
    var exprType = _semanticInfo.GetExpressionType(expr);
    
    return expr switch
    {
        Identifier id => EmitIdentifier(id),
        FunctionCall call => EmitFunctionCall(call),
        BinaryOp binOp => EmitBinaryOp(binOp, exprType),
        // ... other cases
    };
}

private CSharpSyntaxNode EmitIdentifier(Identifier id)
{
    // Get the symbol this identifier refers to
    var symbol = _semanticInfo.GetIdentifierSymbol(id);
    
    // Generate appropriate C# name
    var csharpName = NameMangler.MangleName(symbol.Name);
    return SyntaxFactory.IdentifierName(csharpName);
}
```

---

## Debugging Tips

### Tip 1: Visualize Mappings

When debugging semantic analysis issues, dump the contents of `SemanticInfo`:

```csharp
// Add to SemanticInfo.cs for debugging
public void DebugDump(TextWriter writer)
{
    writer.WriteLine("=== Expression Types ===");
    foreach (var (expr, type) in _expressionTypes)
    {
        writer.WriteLine($"{expr.GetType().Name} at {expr.Location} → {type.GetDisplayName()}");
    }
    
    writer.WriteLine("\n=== Identifier Symbols ===");
    foreach (var (id, symbol) in _identifierSymbols)
    {
        writer.WriteLine($"{id.Name} at {id.Location} → {symbol.GetType().Name} '{symbol.Name}'");
    }
    
    // ... similar for other maps
}
```

### Tip 2: Check for Missing Mappings

If code generation fails, often it's because a node wasn't properly annotated:

```csharp
// In RoslynEmitter.cs - add defensive checks
var type = _semanticInfo.GetExpressionType(expr);
if (type == null)
{
    throw new InvalidOperationException(
        $"Expression at {expr.Location} has no type information. " +
        $"Type checking may have failed.");
}
```

### Tip 3: Trace Type Resolution

Add logging to track when types are set:

```csharp
public void SetExpressionType(Expression expr, SemanticType type)
{
    _logger?.LogDebug($"Setting type for {expr.GetType().Name} at {expr.Location} to {type.GetDisplayName()}");
    _expressionTypes[expr] = type;
}
```

### Tip 4: Watch for Reference Equality Issues

Remember: Dictionary keys use reference equality for records. The **same AST node instance** must be used for Set and Get:

```csharp
// ✅ CORRECT - same instance
var expr = new IntegerLiteral { Value = "42" };
_semanticInfo.SetExpressionType(expr, SemanticType.Int);
var type = _semanticInfo.GetExpressionType(expr);  // Returns SemanticType.Int

// ❌ WRONG - different instance (even if structurally equal)
var expr1 = new IntegerLiteral { Value = "42" };
var expr2 = new IntegerLiteral { Value = "42" };
_semanticInfo.SetExpressionType(expr1, SemanticType.Int);
var type = _semanticInfo.GetExpressionType(expr2);  // Returns null!
```

### Tip 5: Test with Error Recovery

Ensure code handles missing semantic info gracefully (for programs with type errors):

```csharp
[Fact]
public void TestMissingTypeInfo()
{
    var semanticInfo = new SemanticInfo();
    var expr = new IntegerLiteral { Value = "42" };
    
    // Get without Set should return null
    var type = semanticInfo.GetExpressionType(expr);
    Assert.Null(type);
}
```

---

## Contribution Guidelines

### When to Modify SemanticInfo

**Add new mappings when**:
- Introducing new semantic information types (e.g., lifetime tracking, borrow checking)
- Adding new AST node types that need semantic annotation
- Implementing new language features requiring additional metadata

**Example**: Adding support for tracking whether a variable is captured by a closure:
```csharp
// New dictionary
private readonly Dictionary<VariableSymbol, bool> _capturedVariables = new();

// New methods
public void SetVariableCaptured(VariableSymbol var, bool captured)
{
    _capturedVariables[var] = captured;
}

public bool IsVariableCaptured(VariableSymbol var)
{
    return _capturedVariables.TryGetValue(var, out var captured) && captured;
}
```

### Don't Modify SemanticInfo For

- **Validation logic** - Keep in `TypeChecker`, `AccessValidator`, etc.
- **Complex queries** - Create separate analyzer classes that consume `SemanticInfo`
- **Transformation logic** - Keep in code generation or separate passes

### Testing Guidelines

Always test both Set and Get operations:

```csharp
[Fact]
public void TestExpressionTypeMapping()
{
    var semanticInfo = new SemanticInfo();
    var expr = new IntegerLiteral { Value = "42" };
    
    // Should return null before setting
    Assert.Null(semanticInfo.GetExpressionType(expr));
    
    // Set the type
    semanticInfo.SetExpressionType(expr, SemanticType.Int);
    
    // Should return the set type
    Assert.Equal(SemanticType.Int, semanticInfo.GetExpressionType(expr));
}
```

### Performance Considerations

**Current design is optimized for**:
- Fast lookups during code generation (O(1) dictionary access)
- Moderate memory usage (stores references to existing objects)

**Potential future optimizations**:
- If memory becomes an issue, consider using `ConditionalWeakTable<TKey, TValue>` to allow GC of AST nodes
- For very large programs, consider hierarchical storage (per-function dictionaries)

### Integration Points

When working with `SemanticInfo`, coordinate with:

1. **TypeChecker team**: Ensure new mappings are populated during type checking
2. **CodeGen team**: Ensure code generator queries new mappings correctly
3. **AST team**: Changes to AST node types may require new mapping types

---

## Example: Complete Flow Through Compilation

Let's trace a simple Sharpy program through the compiler to see how `SemanticInfo` is used:

### Source Code
```python
x: int = 42
print(x + 1)
```

### Step 1: Parser creates AST
```
Module
├─ VarDecl
│  ├─ target: Identifier("x")
│  ├─ annotation: TypeAnnotation("int")
│  └─ value: IntegerLiteral("42")
└─ ExpressionStmt
   └─ FunctionCall("print")
      └─ arg: BinaryOp
         ├─ left: Identifier("x")
         ├─ op: "+"
         └─ right: IntegerLiteral("1")
```

### Step 2: TypeResolver resolves type annotations
```csharp
// Process TypeAnnotation("int")
var intType = PrimitiveCatalog.GetType("int");  // Returns SemanticType.Int
_semanticInfo.SetTypeAnnotation(annotationNode, intType);
```

**SemanticInfo state**:
```
_typeAnnotations: { TypeAnnotation("int") → SemanticType.Int }
```

### Step 3: NameResolver creates symbol table entries
```csharp
var xSymbol = new VariableSymbol 
{ 
    Name = "x", 
    Type = SemanticType.Int  // from type annotation
};
_symbolTable.Define(xSymbol);
```

### Step 4: TypeChecker annotates expressions
```csharp
// Check IntegerLiteral("42")
_semanticInfo.SetExpressionType(literal42, SemanticType.Int);

// Check Identifier("x") - first occurrence
var xSymbol = _symbolTable.Lookup("x");
_semanticInfo.SetIdentifierSymbol(identifierX_first, xSymbol);
_semanticInfo.SetExpressionType(identifierX_first, SemanticType.Int);

// Check IntegerLiteral("1")
_semanticInfo.SetExpressionType(literal1, SemanticType.Int);

// Check Identifier("x") - in expression
_semanticInfo.SetIdentifierSymbol(identifierX_second, xSymbol);
_semanticInfo.SetExpressionType(identifierX_second, SemanticType.Int);

// Check BinaryOp(x + 1)
var leftType = GetExpressionType(identifierX_second);   // SemanticType.Int
var rightType = GetExpressionType(literal1);            // SemanticType.Int
var resultType = ResolveAddOperator(leftType, rightType); // SemanticType.Int
_semanticInfo.SetExpressionType(binaryOpNode, resultType);

// Check FunctionCall("print")
var printSymbol = _symbolTable.Lookup("print");  // FunctionSymbol
_semanticInfo.SetCallTarget(printCall, printSymbol);
_semanticInfo.SetExpressionType(printCall, SemanticType.Void);
```

**Final SemanticInfo state**:
```
_expressionTypes:
  IntegerLiteral("42") → SemanticType.Int
  Identifier("x") [first] → SemanticType.Int
  Identifier("x") [second] → SemanticType.Int
  IntegerLiteral("1") → SemanticType.Int
  BinaryOp(+) → SemanticType.Int
  FunctionCall("print") → SemanticType.Void

_identifierSymbols:
  Identifier("x") [first] → VariableSymbol { Name="x", Type=Int }
  Identifier("x") [second] → VariableSymbol { Name="x", Type=Int }

_callTargets:
  FunctionCall("print") → FunctionSymbol { Name="print", ReturnType=Void }

_typeAnnotations:
  TypeAnnotation("int") → SemanticType.Int
```

### Step 5: RoslynEmitter generates C#
```csharp
// Generate variable declaration
var varType = GetTypeAnnotation(intAnnotation);  // SemanticType.Int
var csharpType = TypeMapper.MapType(varType);    // "int"
var initializer = EmitExpression(literal42);

// Generate print call
var target = GetCallTarget(printCall);           // FunctionSymbol for print
var argType = GetExpressionType(binaryOpNode);   // SemanticType.Int
var argExpr = EmitExpression(binaryOpNode);
```

**Generated C#**:
```csharp
int x = 42;
Sharpy.Core.Exports.Print(x + 1);
```

---

## Cross-References

### Core Semantic Analysis Files

**Note**: `SemanticInfo` is a standalone class (not a partial class), but it works closely with several other components in the semantic analysis pipeline.

#### 1. **Type System** (`SemanticType.cs`)
- Defines the `SemanticType` hierarchy stored in `_expressionTypes` and `_typeAnnotations`
- Immutable record hierarchy: `BuiltinType`, `GenericType`, `UserDefinedType`, `NullableType`, etc.
- **Key relationship**: `SemanticInfo` stores these types, doesn't create them

#### 2. **Symbol Table** (`Symbol.cs`, `SymbolTable.cs`)
- `Symbol.cs`: Defines the symbol hierarchy stored in `_identifierSymbols` and `_callTargets`
  - Record hierarchy: `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, `ParameterSymbol`, etc.
- `SymbolTable.cs`: Manages symbol scopes and lookups
- **Key relationship**: `SymbolTable` creates symbols, `SemanticInfo` links them to AST nodes

#### 3. **Type Checking** (`TypeChecker.cs` - **Partial Class**)
Split across multiple files:
- `TypeChecker.cs` - Main class, module-level entry point, field declarations
- `TypeChecker.Definitions.cs` - Type checking for function/class/method definitions
- `TypeChecker.Expressions.cs` - Type checking and inference for all expression types
- `TypeChecker.Statements.cs` - Type checking for statements (assignments, returns, etc.)
- `TypeChecker.Utilities.cs` - Helper methods, type narrowing, generic instantiation

**Primary populator** of `SemanticInfo`:
- Populates `_expressionTypes` during expression type checking
- Populates `_callTargets` during function call resolution
- Queries `_identifierSymbols` and `_typeAnnotations` populated by earlier passes

#### 4. **Name Resolution** (`NameResolver.cs`)
- **First pass** of semantic analysis
- Populates `_identifierSymbols` during name resolution
- Creates and registers symbols in `SymbolTable`
- **Flow**: Identifier in code → look up in SymbolTable → store mapping in SemanticInfo

#### 5. **Type Resolution** (`TypeResolver.cs`)
- **Second pass** of semantic analysis
- Populates `_typeAnnotations` by resolving type syntax to semantic types
- Handles generic type instantiation, nullable types, etc.
- **Flow**: `TypeAnnotation` (AST) → resolve names → create `SemanticType` → store in SemanticInfo

### Code Generation Files

#### 6. **Roslyn Emitter** (`CodeGen/RoslynEmitter*.cs` - **Partial Class**)
Split across multiple files for C# code generation:
- `RoslynEmitter.cs` - Main class, module/class structure
- `RoslynEmitter.Expressions.cs` - Expression code generation
- `RoslynEmitter.Statements.cs` - Statement code generation
- Other emission-related files

**Primary consumer** of `SemanticInfo`:
- Queries `GetExpressionType()` for every expression to generate correct C# types
- Queries `GetIdentifierSymbol()` for name resolution and mangling
- Queries `GetCallTarget()` for method invocation generation
- Queries `GetTypeAnnotation()` for type declarations

#### 7. **Type Mapping** (`CodeGen/TypeMapper.cs`)
- Maps `SemanticType` → C# type syntax (Roslyn `TypeSyntax`)
- Examples: `SemanticType.Int` → `int`, `List[str]` → `List<string>`
- **Indirect use**: Uses types retrieved from `SemanticInfo`

### Validation and Analysis Files

#### 8. **Validation Pipeline** (`Semantic/Validation/`)
- `ValidationPipeline.cs` - Orchestrates validators
- `SemanticContext.cs` - Wraps `SemanticInfo` + `SymbolTable` for validators
- Individual validators:
  - `OperatorValidator.cs` - Validates operator usage
  - `ProtocolValidator.cs` - Validates protocol implementations
  - `AccessValidator.cs` - Validates access levels
  - `ControlFlowValidator.cs` - Validates control flow (returns, unreachable code)

**Usage**: Validators query `SemanticInfo` through `SemanticContext` to check semantic correctness

#### 9. **Other Semantic Utilities**
- `TypeUtils.cs` - Type compatibility checks, uses types from SemanticInfo
- `TypeInferenceService.cs` - Helper for type inference
- `CodeGenInfo.cs` / `CodeGenInfoComputer.cs` - Computes additional codegen metadata
- `BuiltinRegistry.cs` / `PrimitiveCatalog.cs` - Built-in type definitions

### AST Definitions (Upstream)

#### 10. **Parser AST** (`Parser/Ast/`)
- `Expression.cs` - Base `Expression` class and expression node types
  - Includes: `Identifier`, `FunctionCall`, `BinaryOp`, `Literal`, etc.
- `Types.cs` - `TypeAnnotation` AST node definition
- `Statement.cs` - Statement nodes (indirect, via expressions they contain)
- `Node.cs` - Base AST node with source location tracking

**Key relationship**: These are the dictionary keys in `SemanticInfo`

### Pipeline Integration

#### 11. **Compiler Entry Points**
- `Compiler.cs` - Single-file compilation, creates `SemanticInfo` instance
- `AssemblyCompiler.cs` - Multi-file project compilation
- `Project/ProjectCompiler.cs` - Project-level compilation orchestration

**Pattern**: Create `SemanticInfo()` → pass through pipeline → query in codegen

#### 12. **Services and Configuration**
- `Services/CompilerServices.cs` - Dependency injection container
- `Services/CompilerServicesBuilder.cs` - Builder for compiler services
- Allows sharing `SemanticInfo` across compilation units in multi-file projects

### Related Documentation

If walkthrough documents exist for these files, they provide deeper context:
- **Semantic Analysis Overview**: Understanding the multi-pass architecture
- **TypeChecker Deep Dive**: How type inference and checking populate SemanticInfo
- **Code Generation Guide**: How RoslynEmitter queries SemanticInfo
- **Symbol Tables and Scopes**: How symbols are created and managed

---

## Summary

`SemanticInfo` is a **simple but essential** component of the Sharpy compiler:

- **What it does**: Stores mappings from AST nodes to semantic information (types, symbols, resolved calls)
- **Why it exists**: Keeps AST immutable while allowing semantic annotation
- **How it works**: Four dictionaries with simple Set/Get APIs
- **When it's used**: Populated during semantic analysis, queried during code generation

**Key Takeaways**:
1. Think of it as a **side database** for the AST
2. Always check for `null` when querying (error recovery)
3. The same AST node instance must be used for Set and Get
4. It's a storage container—validation happens elsewhere

**For new contributors**: Start by tracing how existing mappings are used (search for `SetExpressionType` and `GetExpressionType` in `TypeChecker.cs` and `RoslynEmitter.cs`) to understand the pattern before adding new mappings.

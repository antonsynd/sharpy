# Walkthrough: TypeChecker.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

---

## Overview

The `TypeChecker` is the heart of Sharpy's semantic analysis phase. After the parser builds an Abstract Syntax Tree (AST) and the `NameResolver` and `TypeResolver` populate the symbol table, the `TypeChecker` performs **type validation** across the entire module.

**Key Responsibilities:**
- Validate that all expressions have compatible types
- Check function signatures match their call sites
- Enforce type safety for assignments, operators, and control flow
- Track **type narrowing** (e.g., `if x is not None` narrows `T?` to `T`)
- Validate protocol compliance (`__iter__`, `__getitem__`, `__len__`, etc.)
- Detect access level violations (private/public members)

**Position in the Pipeline:**
```
Source → Lexer → Parser → NameResolver → TypeResolver → TypeChecker → RoslynEmitter → C#
```

The `TypeChecker` doesn't modify the AST. Instead, it stores type information in the `SemanticInfo` class and reports errors through the `ICompilerLogger`.

---

## Class Structure

### Main Class: `TypeChecker`

```csharp
public class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;
    private readonly ControlFlowValidator _controlFlowValidator;
    private readonly AccessValidator _accessValidator;
    private readonly OperatorValidator _operatorValidator;
    private readonly ProtocolValidator _protocolValidator;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    
    // Context tracking
    private SemanticType? _currentFunctionReturnType;
    private TypeSymbol? _currentClass;
    private Dictionary<string, SemanticType> _narrowedTypes;
    private bool _inExceptBlock;
}
```

### Key Dependencies

| Dependency | Purpose |
|------------|---------|
| **SymbolTable** | Lookup variables, functions, types |
| **SemanticInfo** | Cache expression types and symbol bindings |
| **TypeResolver** | Resolve type annotations to `SemanticType` |
| **ControlFlowValidator** | Ensure functions return on all code paths |
| **AccessValidator** | Check public/private member access |
| **OperatorValidator** | Validate binary/unary operators and augmented assignments |
| **ProtocolValidator** | Check protocol compliance (`__iter__`, `__getitem__`, `__len__`) |

### Context Tracking Fields

The type checker maintains several pieces of context as it traverses the AST:

- **`_currentFunctionReturnType`**: Tracks the expected return type of the function being checked (for validating `return` statements)
- **`_currentClass`**: Tracks the class being checked (for validating `self` parameters and member access)
- **`_narrowedTypes`**: Maps variable names to narrowed types in conditional branches
- **`_inExceptBlock`**: Tracks whether we're inside an exception handler (bare `raise` is only valid here)

---

## Key Methods

### Entry Point: `CheckModule()`

```csharp
public void CheckModule(Module module)
{
    foreach (var statement in module.Body)
    {
        CheckStatement(statement);
    }
}
```

This is the entry point for type checking. It iterates through all top-level statements in the module.

---

### Statement Checking: `CheckStatement()`

Dispatches to specific handlers based on statement type:

```csharp
private void CheckStatement(Statement statement)
{
    switch (statement)
    {
        case FunctionDef functionDef:
            CheckFunction(functionDef);
            break;
        case ClassDef classDef:
            CheckClass(classDef);
            break;
        case Assignment assignment:
            CheckAssignment(assignment);
            break;
        // ... more cases
    }
}
```

---

### Function Checking: `CheckFunction()`

**What it does:**
1. Resolves the return type (defaults to `void` if not specified)
2. Validates `self` parameter for instance methods
3. Checks parameter ordering (non-default params can't follow default params)
4. Registers parameters in the function's scope
5. Type-checks default parameter values
6. Validates function body statements
7. Ensures all code paths return appropriate values

**Key Implementation Details:**

```csharp
private void CheckFunction(FunctionDef functionDef)
{
    // Special case: __init__ always returns void
    if (functionDef.Name == "__init__")
    {
        returnType = SemanticType.Void;
    }
    
    _currentFunctionReturnType = returnType;
    _symbolTable.EnterScope($"function:{functionDef.Name}");
    
    // Validate self parameter for instance methods
    if (_currentClass != null && functionDef.Parameters.Count > 0)
    {
        if (functionDef.Parameters[0].Name != "self")
        {
            AddError("Instance method must have 'self' as first parameter");
        }
    }
    
    // Check function body
    foreach (var statement in functionDef.Body)
    {
        CheckStatement(statement);
    }
    
    // Validate control flow (all paths return)
    _controlFlowValidator.ValidateFunction(functionDef, returnType);
    
    _symbolTable.ExitScope();
}
```

**Important:** The type checker validates parameter ordering (default params must come after required params) but doesn't enforce return statement presence—that's delegated to `ControlFlowValidator`.

---

### Class Checking: `CheckClass()`

**What it does:**
1. Resolves field types
2. Sets `_currentClass` context for method validation
3. Notifies `AccessValidator` of class scope
4. Type-checks all class members

```csharp
private void CheckClass(ClassDef classDef)
{
    _symbolTable.EnterScope($"class:{classDef.Name}");
    
    // Resolve field types first
    for (int i = 0; i < classSymbol.Fields.Count; i++)
    {
        var fieldSymbol = classSymbol.Fields[i];
        if (fieldSymbol.Type == SemanticType.Unknown)
        {
            // Find corresponding VariableDeclaration and resolve
            var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
            classSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
        }
    }
    
    // Set current class for method checking
    var previousClass = _currentClass;
    _currentClass = classSymbol;
    _accessValidator.EnterClass(classSymbol);
    
    // Check all members
    foreach (var statement in classDef.Body)
    {
        CheckStatement(statement);
    }
    
    _currentClass = previousClass;
    _accessValidator.ExitClass();
    _symbolTable.ExitScope();
}
```

**Design Note:** Fields are resolved before methods so that method bodies can reference field types correctly.

---

### Assignment Checking: `CheckAssignment()`

This is one of the most complex methods because Sharpy supports:
- Simple assignment with type inference: `x = 5`
- Tuple unpacking: `x, y = (1, 2)`
- Augmented assignment: `x += 5`
- Constant validation

**Key Behaviors:**

1. **Tuple Unpacking:**
   ```csharp
   if (assignment.Target is TupleLiteral targetTuple)
   {
       // Value must be a tuple type
       // Element count must match
       // Type-check each unpacking element
   }
   ```

2. **Type Inference for Simple Assignments:**
   ```csharp
   if (assignment.Target is Identifier targetId)
   {
       // Check if trying to reassign a constant
       if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
       {
           AddError("Cannot reassign constant variable");
       }
       
       // Infer type from value and create new variable
       var inferredType = CheckExpression(assignment.Value);
       var newSymbol = new VariableSymbol { Type = inferredType, ... };
       _symbolTable.Define(newSymbol);
   }
   ```

3. **Augmented Assignments:**
   ```csharp
   if (assignment.Operator != AssignmentOperator.Assign)
   {
       // Delegate to OperatorValidator which checks:
       // - In-place dunder methods (__iadd__, __isub__, etc.)
       // - Falls back to binary operators (__add__, __sub__, etc.)
       _operatorValidator.ValidateAugmentedAssignment(...);
   }
   ```

**Important:** Simple assignments allow **variable redefinition** with different types (Python-like behavior), but constants cannot be reassigned.

---

### Expression Checking: `CheckExpression()`

**Central dispatch method for all expressions:**

```csharp
public SemanticType CheckExpression(Expression expr)
{
    // Check cache first
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null) return cached;
    
    SemanticType type = expr switch
    {
        IntegerLiteral => SemanticType.Int,
        StringLiteral => SemanticType.Str,
        Identifier id => CheckIdentifier(id),
        BinaryOp binOp => CheckBinaryOp(binOp),
        FunctionCall call => CheckFunctionCall(call),
        // ... more cases
        _ => SemanticType.Unknown
    };
    
    // Cache the result
    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

**Optimization:** Results are cached in `SemanticInfo` to avoid redundant computation.

---

### Identifier Checking: `CheckIdentifier()`

```csharp
private SemanticType CheckIdentifier(Identifier id)
{
    var symbol = _symbolTable.Lookup(id.Name);
    if (symbol == null)
    {
        AddError($"Undefined identifier '{id.Name}'");
        return SemanticType.Unknown;
    }
    
    _semanticInfo.SetIdentifierSymbol(id, symbol);
    
    // Check for narrowed type in current context
    if (_narrowedTypes.TryGetValue(id.Name, out var narrowedType))
    {
        return narrowedType;
    }
    
    return symbol switch
    {
        VariableSymbol varSymbol => varSymbol.Type,
        FunctionSymbol funcSymbol => new FunctionType { ... },
        TypeSymbol => SemanticType.Unknown, // Type names need special handling
        _ => SemanticType.Unknown
    };
}
```

**Type Narrowing Integration:** If the identifier has a narrowed type (from `if x is not None:`), that type takes precedence.

---

### Binary Operator Checking: `CheckBinaryOp()`

```csharp
private SemanticType CheckBinaryOp(BinaryOp binOp)
{
    var leftType = CheckExpression(binOp.Left);
    var rightType = CheckExpression(binOp.Right);
    
    // Avoid cascading errors
    if (leftType is UnknownType || rightType is UnknownType)
    {
        return SemanticType.Unknown;
    }
    
    // Delegate to OperatorValidator
    return _operatorValidator.ValidateBinaryOp(
        binOp.Operator,
        leftType,
        rightType,
        binOp.LineStart,
        binOp.ColumnStart);
}
```

**Delegation Pattern:** The actual operator validation (checking for `__add__`, `__sub__`, etc.) is handled by `OperatorValidator`. This keeps concerns separated.

---

### Function Call Checking: `CheckFunctionCall()`

**Complex logic handles:**
- Builtin function overload resolution
- Constructor calls (calling a type)
- Default parameter support
- Special handling for `len()` (protocol validation)

```csharp
private SemanticType CheckFunctionCall(FunctionCall call)
{
    var calleeType = CheckExpression(call.Function);
    var argTypes = call.Arguments.Select(CheckExpression).ToList();
    
    if (call.Function is Identifier id)
    {
        // Special handling for len()
        if (id.Name == "len" && argTypes.Count == 1)
        {
            return _protocolValidator.ValidateLen(argTypes[0], ...);
        }
        
        // Constructor call?
        if (symbol is TypeSymbol typeSymbol)
        {
            return new UserDefinedType { Symbol = typeSymbol };
        }
        
        // Builtin overloads?
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
        if (overloads != null && overloads.Count > 1)
        {
            // Find matching overload by argument count and types
            // ...
        }
    }
    
    // Validate argument count and types
    if (funcSymbol != null)
    {
        // Check required vs total parameters (for defaults)
        // Validate each argument type
    }
    
    return funcSymbol.ReturnType;
}
```

**Overload Resolution:** For builtins like `range()` (which has multiple signatures), the checker finds the matching overload by:
1. Filtering by argument count (considering defaults)
2. Checking type compatibility

---

### Control Flow Statements

#### If Statement: `CheckIf()`

**Key Feature: Type Narrowing**

```csharp
private void CheckIf(IfStatement ifStmt)
{
    var condType = CheckExpression(ifStmt.Test);
    
    // Extract narrowed types for branches
    var narrowedTypesInThen = ExtractNarrowedTypes(ifStmt.Test, true);
    var narrowedTypesInElse = ExtractNarrowedTypes(ifStmt.Test, false);
    
    // Apply narrowed types in then branch
    var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
    foreach (var kvp in narrowedTypesInThen)
    {
        _narrowedTypes[kvp.Key] = kvp.Value;
    }
    
    _symbolTable.EnterScope("if-then");
    foreach (var stmt in ifStmt.ThenBody)
        CheckStatement(stmt);
    _symbolTable.ExitScope();
    
    // Restore original narrowed types
    _narrowedTypes = savedNarrowedTypes;
}
```

**Type Narrowing Example:**
```python
x: int? = get_value()
if x is not None:
    # Inside this block, x is narrowed from int? to int
    print(x + 1)  # Valid: x is now treated as int
```

#### For Loop: `CheckFor()`

**Key Features:**
- Validates iterator is iterable (has `__iter__` protocol)
- Infers loop variable type from iterator element type
- Supports tuple unpacking: `for x, y in items:`
- Loop variables are scoped to the loop body

```csharp
private void CheckFor(ForStatement forStmt)
{
    var iterType = CheckExpression(forStmt.Iterator);
    
    // Validate iterator protocol and get element type
    var elementType = _protocolValidator.ValidateIteration(
        iterType,
        forStmt.Iterator.LineStart,
        forStmt.Iterator.ColumnStart);
    
    // Enter scope FIRST (loop vars scoped to loop)
    _symbolTable.EnterScope("for-body");
    
    // Handle tuple unpacking
    if (forStmt.Target is TupleLiteral targetTuple)
    {
        // Element type must be a tuple
        // Define each loop variable with inferred type
    }
    else if (forStmt.Target is Identifier id)
    {
        var loopVarSymbol = new VariableSymbol { Type = elementType, ... };
        _symbolTable.Define(loopVarSymbol);
    }
    
    foreach (var stmt in forStmt.Body)
        CheckStatement(stmt);
    
    _symbolTable.ExitScope();
}
```

---

### Collection Literals

#### List Literal: `CheckListLiteral()`

```csharp
private SemanticType CheckListLiteral(ListLiteral list)
{
    if (list.Elements.Count == 0)
    {
        return new GenericType { Name = "list", TypeArguments = [SemanticType.Unknown] };
    }
    
    var elementTypes = list.Elements.Select(CheckExpression).ToList();
    var commonType = elementTypes[0];
    
    // Try to find common type
    foreach (var elemType in elementTypes.Skip(1))
    {
        if (!IsAssignable(elemType, commonType))
        {
            commonType = SemanticType.Unknown;
            break;
        }
    }
    
    return new GenericType { Name = "list", TypeArguments = [commonType] };
}
```

**Type Inference:** The element type is inferred from the elements. If all elements have compatible types, the list is typed accordingly. Empty lists get `list[Unknown]`.

#### Tuple Literal: `CheckTupleLiteral()`

```csharp
private SemanticType CheckTupleLiteral(TupleLiteral tuple)
{
    var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
    return new TupleType { ElementTypes = elementTypes };
}
```

**Heterogeneous Types:** Unlike lists, tuples preserve the type of each element: `(1, "hello", True)` has type `tuple[int, str, bool]`.

---

### Comprehensions

All three comprehension types (list, set, dict) follow the same pattern:

```csharp
private SemanticType CheckListComprehension(ListComprehension listComp)
{
    // Enter scope (variables don't leak)
    _symbolTable.EnterScope("list-comprehension");
    
    // Process clauses in order
    foreach (var clause in listComp.Clauses)
    {
        if (clause is ForClause forClause)
        {
            // Validate iterator and define loop variable
            var iterType = CheckExpression(forClause.Iterator);
            var elemType = _protocolValidator.ValidateIteration(iterType, ...);
            
            if (forClause.Target is Identifier id)
            {
                var loopVarSymbol = new VariableSymbol { Type = elemType, ... };
                _symbolTable.Define(loopVarSymbol);
            }
        }
        else if (clause is IfClause ifClause)
        {
            // Check condition is boolean
            var condType = CheckExpression(ifClause.Condition);
        }
    }
    
    // Check element expression
    var elementType = CheckExpression(listComp.Element);
    
    _symbolTable.ExitScope();
    
    return new GenericType { Name = "list", TypeArguments = [elementType] };
}
```

**Scope Isolation:** Variables defined in comprehensions don't leak to the outer scope.

---

## Type Narrowing

### What is Type Narrowing?

Type narrowing refines the type of a variable within a specific code path based on conditional checks. This allows treating a `T?` as `T` after checking it's not `None`.

### Supported Patterns

1. **`x is not None`** (positive branch narrows to non-nullable):
   ```python
   x: int? = get_value()
   if x is not None:
       # x is narrowed from int? to int
       result = x + 1  # Valid
   ```

2. **`x is None`** (negative branch narrows to non-nullable):
   ```python
   x: int? = get_value()
   if x is None:
       return
   # After this point, x is narrowed to int
   result = x + 1  # Valid
   ```

3. **`isinstance(x, Type)`** (positive branch narrows to Type):
   ```python
   def process(obj: Animal):
       if isinstance(obj, Dog):
           # obj is narrowed to Dog
           obj.bark()  # Valid if Dog has bark()
   ```

4. **`A and B`** (combines narrowings from both sides):
   ```python
   if x is not None and y is not None:
       # Both x and y are narrowed
   ```

### Implementation: `ExtractNarrowedTypes()`

```csharp
private Dictionary<string, SemanticType> ExtractNarrowedTypes(
    Expression condition, 
    bool isPositiveBranch)
{
    var narrowedTypes = new Dictionary<string, SemanticType>();
    
    // Handle 'A and B' pattern
    if (condition is BinaryOp { Operator: BinaryOperator.And } andOp && isPositiveBranch)
    {
        var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);
        var rightNarrowed = ExtractNarrowedTypes(andOp.Right, true);
        // Merge dictionaries
    }
    
    // Handle 'x is not None'
    if (condition is BinaryOp { Operator: BinaryOperator.IsNot } binOp)
    {
        if (binOp.Left is Identifier id && binOp.Right is NoneLiteral)
        {
            if (isPositiveBranch)
            {
                // Narrow nullable to non-nullable
                if (varSymbol.Type is NullableType nullable)
                {
                    narrowedTypes[id.Name] = nullable.UnderlyingType;
                }
            }
        }
    }
    
    // Handle isinstance()
    // ...
    
    return narrowedTypes;
}
```

**Narrowing Key:** For simple identifiers, the key is the name. For subscripts like `arr[i]`, the key is `"arr[i]"` to support narrowing array elements.

---

## Dependencies and Delegation

The `TypeChecker` delegates specialized validation to separate classes:

### ControlFlowValidator
- **Purpose:** Ensures all code paths in functions return appropriate values
- **Called:** After checking function body in `CheckFunction()`
- **Example:** Detects missing return in non-void functions

### AccessValidator
- **Purpose:** Validates public/private member access
- **Called:** When checking member access (`obj.field`, `obj.method()`)
- **Context:** Needs to know current class for `self` vs external access

### OperatorValidator
- **Purpose:** Validates binary/unary operators and augmented assignments
- **Called:** In `CheckBinaryOp()`, `CheckUnaryOp()`, `CheckAssignment()`
- **Features:** 
  - Checks for dunder methods (`__add__`, `__sub__`, etc.)
  - Supports CLR operator overloading
  - Prefers in-place operators for augmented assignments

### ProtocolValidator
- **Purpose:** Validates protocol compliance
- **Called:** For iteration (`for`), indexing (`[]`), membership (`in`), and `len()`
- **Protocols:**
  - `__iter__` for iteration
  - `__getitem__` for indexing
  - `__contains__` for membership testing
  - `__len__` for `len()` calls

### Shared Resource: ClrMemberCache
```csharp
var sharedClrCache = new ClrMemberCache();
_protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
_operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
```

**Performance:** The `ClrMemberCache` is shared between validators to cache reflection results for CLR types (like `System.Collections.Generic.List<T>`).

---

## Patterns and Design Decisions

### 1. Immutable AST + Separate Semantic Info

**Decision:** Type information is NOT stored on AST nodes. Instead, it's cached in `SemanticInfo`.

**Rationale:** 
- AST nodes are immutable records (C# 9+)
- Allows semantic analysis to be rerun without rebuilding AST
- Separates parsing from type checking concerns

```csharp
// NOT: expr.Type = SemanticType.Int
// YES:
_semanticInfo.SetExpressionType(expr, SemanticType.Int);
```

### 2. Error Recovery via `UnknownType`

**Decision:** When a type error occurs, return `SemanticType.Unknown` instead of throwing.

**Rationale:** Prevents cascading errors. If `x` is undefined, don't report errors for every expression using `x`.

```csharp
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown; // Avoid cascading errors
}
```

### 3. Context Tracking with Fields

**Decision:** Use private fields to track context (current function, class, narrowed types).

**Alternatives Considered:**
- Passing context through parameters (too verbose)
- Using a context stack (overkill for current needs)

**Benefit:** Simple and direct. The checker always knows "where" it is in the code.

### 4. Delegation to Specialized Validators

**Decision:** Operator and protocol validation are separate classes.

**Rationale:**
- Single Responsibility Principle
- `TypeChecker` is already large (~1850 lines)
- Validators can be tested independently
- Validators can share resources (like `ClrMemberCache`)

### 5. Scope Management

**Decision:** Explicitly enter/exit scopes with descriptive names.

```csharp
_symbolTable.EnterScope("if-then");
// ... check statements
_symbolTable.ExitScope();
```

**Rationale:** Clear scope boundaries prevent variable leakage and enable proper shadowing.

### 6. Assignment as Redefinition

**Decision:** Simple assignments (`x = value`) redefine the variable in the current scope.

**Rationale:** Matches Python behavior where variables can change types:
```python
x = 5        # x is int
x = "hello"  # x is now str
```

**Limitation:** Constants cannot be redefined (enforced by checking `IsConstant` flag).

---

## Debugging Tips

### 1. Enable Debug Logging

The type checker logs its progress:
```csharp
_logger.LogDebug($"Type checking function: {functionDef.Name}");
```

To see these messages, ensure your `ICompilerLogger` implementation outputs debug-level logs.

### 2. Inspect SemanticInfo

After type checking, you can inspect cached types:
```csharp
var exprType = _semanticInfo.GetExpressionType(someExpression);
var symbol = _semanticInfo.GetIdentifierSymbol(someIdentifier);
```

### 3. Check Error Collection

Errors from all validators are combined:
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_controlFlowValidator.Errors);
        allErrors.AddRange(_accessValidator.Errors);
        allErrors.AddRange(_operatorValidator.Errors);
        allErrors.AddRange(_protocolValidator.Errors);
        return allErrors;
    }
}
```

If you see an error but can't find it in `_errors`, check the delegated validators.

### 4. Type Narrowing Issues

If narrowing isn't working:
- Add breakpoints in `ExtractNarrowedTypes()` to see what's extracted
- Check `_narrowedTypes` dictionary in `CheckIdentifier()`
- Verify the condition pattern matches supported patterns

### 5. Unknown Type Propagation

If you see `Unknown` types in unexpected places:
- Check for unresolved identifiers (typos)
- Verify type annotations are present on parameters
- Ensure types are registered in `SymbolTable` before use

### 6. Tracing Overload Resolution

For builtin function calls with multiple overloads:
```csharp
var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
// Set a breakpoint here to see all candidates
```

---

## Contribution Guidelines

### Adding Support for New Statements

1. **Add a case to `CheckStatement()`:**
   ```csharp
   case MyNewStatement myStmt:
       CheckMyNewStatement(myStmt);
       break;
   ```

2. **Implement the handler:**
   ```csharp
   private void CheckMyNewStatement(MyNewStatement stmt)
   {
       // Type check components
       // Update symbol table if needed
       // Report errors
   }
   ```

3. **Add tests** in `Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

### Adding Support for New Expressions

1. **Add a case to `CheckExpression()`:**
   ```csharp
   MyNewExpression myExpr => CheckMyNewExpression(myExpr),
   ```

2. **Implement the handler:**
   ```csharp
   private SemanticType CheckMyNewExpression(MyNewExpression expr)
   {
       // Type check components
       // Return the result type
   }
   ```

3. **Cache the result** (already handled by `CheckExpression()`)

### Extending Type Narrowing

To support new narrowing patterns:

1. **Add pattern detection in `ExtractNarrowedTypes()`:**
   ```csharp
   // Handle 'len(x) > 0' pattern
   if (condition is ComparisonChain { ... })
   {
       // Extract narrowing for non-empty collections
   }
   ```

2. **Update `CheckIdentifier()` if needed** (usually not required)

3. **Add tests** for the new pattern

### Performance Considerations

- **Caching:** Expression types are cached in `SemanticInfo`. Don't bypass the cache.
- **Error Limits:** The checker stops after `MaxErrors` (default 100) to prevent runaway compilation.
- **CLR Member Cache:** Share `ClrMemberCache` instances when creating validators.

### Common Pitfalls

1. **Forgetting to Enter/Exit Scopes**
   - Always pair `EnterScope()` with `ExitScope()`
   - Use descriptive scope names for debugging

2. **Not Restoring Context**
   - Save and restore `_currentClass`, `_narrowedTypes`, etc.
   - Example:
     ```csharp
     var savedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
     // ... modify _narrowedTypes
     _narrowedTypes = savedTypes; // Restore
     ```

3. **Cascading Errors**
   - Always check for `UnknownType` before reporting errors
   - Return `SemanticType.Unknown` after reporting an error

4. **Modifying AST**
   - **DON'T** modify AST nodes in the type checker
   - Use `SemanticInfo` to store type information

### Testing Strategy

1. **Unit Tests:** Test individual methods with synthetic AST nodes
2. **Integration Tests:** Compile Sharpy code and verify error messages
3. **Regression Tests:** Add tests for fixed bugs

Example:
```csharp
[Fact]
public void TypeChecker_RejectsNoneAssignmentToNonNullable()
{
    var source = @"
def test() -> None:
    x: int = None  # Should fail
";
    var errors = CompileAndGetErrors(source);
    Assert.Contains("Cannot assign 'None' to non-nullable type 'int'", 
        errors[0].Message);
}
```

---

## Summary

The `TypeChecker` is a comprehensive validator that ensures type safety across Sharpy programs. It leverages:
- **Context tracking** for function returns, class scope, and type narrowing
- **Delegation** to specialized validators for operators, protocols, and access control
- **Caching** for performance (via `SemanticInfo`)
- **Error recovery** to report multiple issues without cascading failures

When extending the type checker:
- Follow the existing delegation pattern for new features
- Always handle `UnknownType` to avoid cascading errors
- Test both success and failure cases
- Update documentation for new narrowing patterns or validation rules

For questions or clarifications, consult:
- `docs/specs/type_system.md` for type system design
- `docs/architecture/semantic-analyzer-architecture.md` for overall semantic analysis flow
- Existing tests in `Sharpy.Compiler.Tests/Semantic/` for examples

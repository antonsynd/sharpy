# Walkthrough: TypeChecker.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

---

## Overview

The `TypeChecker` is the heart of the Sharpy compiler's semantic analysis phase. It performs comprehensive type checking, type inference, and semantic validation on the Abstract Syntax Tree (AST) produced by the Parser. This component ensures that all type rules are followed, expressions are well-typed, and the code adheres to Sharpy's language semantics before code generation.

**Role in the Compiler Pipeline:**
- **Input**: AST from Parser (expressions, statements, definitions)
- **Output**: Populated `SemanticInfo` with expression types and errors list
- **Downstream**: RoslynEmitter uses the validated AST and type information to generate C#

**Key Responsibilities:**
1. Type checking all expressions and statements
2. Type inference for variables and function returns
3. Validating type compatibility and assignments
4. Type narrowing in conditional contexts
5. Protocol validation (iteration, indexing, operators)
6. Access level validation (private/public/protected)
7. Control flow analysis (return paths, unreachable code)
8. Super() validation and inheritance rules
9. Generic type instantiation and substitution

---

## Architecture: Partial Classes

The `TypeChecker` is split across **five partial class files** for better maintainability:

1. **TypeChecker.cs** - Main class definition, dependencies, configuration
2. **TypeChecker.Definitions.cs** - Type definitions (functions, classes, structs, interfaces, enums)
3. **TypeChecker.Expressions.cs** - Expression type checking (operators, calls, literals, collections)
4. **TypeChecker.Statements.cs** - Statement checking (assignments, control flow, try/catch)
5. **TypeChecker.Utilities.cs** - Helper methods and validation utilities

This document covers the main file and provides cross-references to the other parts.

---

## Class Structure

### Main Class: `TypeChecker`

```csharp
public partial class TypeChecker
{
    // Core dependencies
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;

    // Specialized validators
    private readonly ControlFlowValidator _controlFlowValidator;
    private readonly AccessValidator _accessValidator;
    private readonly OperatorValidator _operatorValidator;
    private readonly ProtocolValidator _protocolValidator;
    private readonly DefaultParameterValidator _defaultParameterValidator;

    // Context tracking
    private SemanticType? _currentFunctionReturnType;
    private TypeSymbol? _currentClass;
    private Dictionary<string, SemanticType> _narrowedTypes;
    private bool _inExceptBlock;
    private string? _currentMethodName;
    private bool _currentMethodIsOverride;
    private bool _currentMethodIsDunder;
    private int _controlFlowDepth;
    private bool _superInitCalled;

    // Configuration
    public bool ContinueAfterError { get; set; } = true;
    public int MaxErrors { get; set; } = 100;
}
```

---

## Key Dependencies

### Internal Validators

The TypeChecker delegates specialized validation to focused components:

1. **ControlFlowValidator** - Validates return paths, unreachable code, break/continue
2. **AccessValidator** - Validates field/method access levels (private/public/protected)
3. **OperatorValidator** - Validates operators (binary, unary, augmented assignment) with CLR reflection
4. **ProtocolValidator** - Validates protocol support (iteration, indexing, len, membership)
5. **DefaultParameterValidator** - Validates default parameter values are compile-time constants

**Design Pattern**: The TypeChecker uses **composition over inheritance** - instead of one massive class, it delegates domain-specific validation to specialized validators. This follows the Single Responsibility Principle.

### Shared CLR Member Cache

```csharp
var sharedClrCache = new ClrMemberCache();
_protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
_operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
```

The `ClrMemberCache` is shared between validators to avoid redundant reflection calls when looking up C# runtime methods. This is a **performance optimization** since reflection is expensive.

---

## Entry Points

### CheckModule

```csharp
public void CheckModule(Module module)
{
    foreach (var statement in module.Body)
    {
        CheckStatement(statement);
    }
}
```

The main entry point for type checking. Simply iterates through top-level statements in the module and dispatches to `CheckStatement`.

### CheckStatement (Dispatcher)

```csharp
private void CheckStatement(Statement statement)
{
    switch (statement)
    {
        case FunctionDef functionDef: CheckFunction(functionDef); break;
        case ClassDef classDef: CheckClass(classDef); break;
        case Assignment assignment: CheckAssignment(assignment); break;
        case ReturnStatement returnStmt: CheckReturn(returnStmt); break;
        case IfStatement ifStmt: CheckIf(ifStmt); break;
        // ... more cases
    }
}
```

This is the **central dispatcher** for statement type checking. It uses pattern matching to route each statement type to its specialized handler.

**Design Decision**: The switch-based dispatcher is simple and efficient. Adding new statement types requires updating this switch (open-closed principle trade-off for simplicity).

---

## Context Tracking

The TypeChecker maintains several pieces of mutable state to track context during traversal:

### Function Context

```csharp
private SemanticType? _currentFunctionReturnType = null;
```

Tracks the expected return type of the current function. Used to validate return statements. Set when entering a function, cleared when exiting.

### Class Context

```csharp
private TypeSymbol? _currentClass = null;
```

Tracks the current class being checked. Used for:
- Typing the `self` parameter
- Validating access to private members
- Validating super() calls
- Resolving field/method references

### Method Context (for super() validation)

```csharp
private string? _currentMethodName = null;
private bool _currentMethodIsOverride = false;
private bool _currentMethodIsDunder = false;
private int _controlFlowDepth = 0;
private bool _superInitCalled = false;
```

Sharpy has strict rules for `super()` usage:
- `super().__init__()` must be first statement in constructors
- `super()` in `@override` methods must call the same method
- Dunder methods can cross-call dunders via super()
- Regular methods cannot use super()

These flags track where we are to enforce these rules.

### Type Narrowing

```csharp
private Dictionary<string, SemanticType> _narrowedTypes = new();
```

Implements **type narrowing** for conditional contexts. For example:

```python
x: int? = None
if x is not None:
    # Inside this block, x is narrowed to int (non-nullable)
    print(x + 1)  # Valid - x is int here
```

The `_narrowedTypes` dictionary maps variable names to their narrowed types within the current conditional scope.

### Exception Handling Context

```csharp
private bool _inExceptBlock = false;
```

Tracks whether we're inside an `except` block. Required for validating bare `raise` statements (which can only appear in exception handlers).

---

## Error Handling

### Error Collection

```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);
        allErrors.AddRange(_controlFlowValidator.Errors);
        allErrors.AddRange(_accessValidator.Errors);
        allErrors.AddRange(_operatorValidator.Errors);
        allErrors.AddRange(_protocolValidator.Errors);
        allErrors.AddRange(_defaultParameterValidator.Errors);
        return allErrors;
    }
}
```

The TypeChecker aggregates errors from all validators. This allows consumers to get all semantic errors in one place.

### Error Limits

```csharp
public bool ContinueAfterError { get; set; } = true;
public int MaxErrors { get; set; } = 100;
```

Configurable error handling:
- `ContinueAfterError`: Whether to keep checking after finding errors (default: true)
- `MaxErrors`: Stop after this many errors to avoid cascading failures (default: 100)

**Design Decision**: By default, the type checker continues after errors to report as many issues as possible in one pass. This improves developer experience (fix multiple errors at once).

---

## Key Methods by Component

### Type Definitions (TypeChecker.Definitions.cs)

See [TypeChecker.Definitions.md](./TypeChecker.Definitions.md) for detailed walkthrough.

**Summary**:
- `CheckFunction` - Validates function signatures, parameters, decorators, control flow
- `CheckClass` - Validates class definitions, fields, methods, inheritance
- `CheckStruct` - Validates struct definitions with special rules (all fields must be initialized)
- `CheckInterface` - Validates interface method signatures
- `CheckEnum` - Validates enum members have explicit values of consistent types

**Key Pattern**: All definition checkers follow the same structure:
1. Enter a new scope
2. Register type parameters (for generics)
3. Resolve field/parameter types
4. Check members/body
5. Run specialized validation rules
6. Exit scope

### Expression Checking (TypeChecker.Expressions.cs)

See [TypeChecker.Expressions.md](./TypeChecker.Expressions.md) for detailed walkthrough.

**Summary**:
- `CheckExpression` - Main dispatcher with result caching
- `CheckIdentifier` - Symbol lookup and type narrowing
- `CheckBinaryOp` - Binary operators (delegates to OperatorValidator)
- `CheckUnaryOp` - Unary operators (delegates to OperatorValidator)
- `CheckFunctionCall` - Function calls with overload resolution
- `CheckMemberAccess` - Attribute/method access with inheritance
- `CheckIndexAccess` - Subscript operations and generic type references
- `CheckListLiteral`, `CheckDictLiteral`, etc. - Collection literals
- `CheckListComprehension` - Comprehensions with scope isolation
- `CheckPipeForward` - Special handling for `|>` operator

**Key Pattern**: Expression checking uses **memoization** - results are cached in `SemanticInfo` to avoid redundant work:

```csharp
public SemanticType CheckExpression(Expression expr)
{
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
        return cached;

    SemanticType type = expr switch { /* ... */ };

    _semanticInfo.SetExpressionType(expr, type);
    return type;
}
```

### Statement Checking (TypeChecker.Statements.cs)

See [TypeChecker.Statements.md](./TypeChecker.Statements.md) for detailed walkthrough.

**Summary**:
- `CheckAssignment` - Assignment validation with tuple unpacking, type inference, const checking
- `CheckVariableDeclaration` - Variable declarations with type inference for `auto`
- `CheckReturn` - Return statement validation against function signature
- `CheckIf` - If statements with type narrowing in branches
- `CheckWhile` - While loops with type narrowing
- `CheckFor` - For loops with iteration protocol validation
- `CheckTry` - Try/except/finally with scope isolation
- `CheckRaise` - Raise statements with bare raise validation
- `CheckAssert` - Assert statements

**Key Pattern**: Control flow statements create **new scopes** for their bodies:

```csharp
_symbolTable.EnterScope("if-then");
_controlFlowDepth++;
foreach (var stmt in ifStmt.ThenBody)
    CheckStatement(stmt);
_controlFlowDepth--;
_symbolTable.ExitScope();
```

The `_controlFlowDepth` counter tracks nesting depth for super() validation.

### Utilities (TypeChecker.Utilities.cs)

See [TypeChecker.Utilities.md](./TypeChecker.Utilities.md) for detailed walkthrough.

**Summary**:
- `IsAssignable` - Type compatibility checking with nullable and generic variance
- `ExtractNarrowedTypes` - Extracts type narrowing from conditionals
- `SubstituteTypeParameters` - Generic type argument substitution
- `FindFieldInHierarchy`, `FindMethodInHierarchy` - Inheritance traversal
- `ValidateSuperMemberAccess` - super() validation with strict rules
- `ValidateConstructorOverloads` - Ensures unique constructor signatures
- `ValidateStructRules` - Struct-specific validation
- `ValidateEnumRules` - Enum-specific validation

---

## Type Narrowing Deep Dive

Type narrowing allows the type checker to refine types based on runtime checks:

```python
x: int? = get_nullable_value()

# Before check: x is int?
if x is not None:
    # Inside: x is narrowed to int
    print(x + 1)  # Valid
else:
    # Inside: x remains int?
    pass
```

### Implementation

```csharp
private Dictionary<string, SemanticType> ExtractNarrowedTypes(
    Expression condition,
    bool isPositiveBranch)
{
    // Handle 'x is not None' pattern
    if (condition is BinaryOp { Operator: BinaryOperator.IsNot } binOp)
    {
        if (binOp.Left is Identifier id && binOp.Right is NoneLiteral)
        {
            if (isPositiveBranch)
            {
                var symbol = _symbolTable.Lookup(id.Name);
                if (symbol is VariableSymbol { Type: NullableType nullable })
                {
                    narrowedTypes[id.Name] = nullable.UnderlyingType;
                }
            }
        }
    }

    // Also handles: 'x is None', 'isinstance(x, Type)', 'A and B'
}
```

Narrowed types are applied when entering conditional branches and restored when exiting:

```csharp
var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
foreach (var kvp in narrowedTypesInThen)
    _narrowedTypes[kvp.Key] = kvp.Value;

// Check then branch with narrowed types...

// Restore original types
_narrowedTypes = savedNarrowedTypes;
```

---

## Generic Type Handling

### Generic Type Instantiation

Sharpy supports generic types like `Box[int]` which are parsed as `IndexAccess` expressions:

```csharp
// Box[int] is parsed as IndexAccess(Object: "Box", Index: "int")
if (indexAccess.Object is Identifier typeId)
{
    var symbol = _symbolTable.Lookup(typeId.Name);

    if (symbol is TypeSymbol { IsGeneric: true } genericTypeSymbol)
    {
        var typeArgs = TryResolveTypeArguments(indexAccess.Index);
        if (typeArgs != null)
        {
            return new GenericType
            {
                Name = genericTypeSymbol.Name,
                TypeArguments = typeArgs,
                GenericDefinition = genericTypeSymbol
            };
        }
    }
}
```

### Generic Function Calls

Generic functions like `identity[int](42)` are similarly handled:

```csharp
if (symbol is FunctionSymbol { IsGeneric: true } genericFuncSymbol)
{
    var typeArgs = TryResolveTypeArguments(indexAccess.Index);
    if (typeArgs != null)
    {
        _semanticInfo.SetExpressionType(indexAccess, new GenericFunctionType
        {
            FunctionSymbol = genericFuncSymbol,
            TypeArguments = typeArgs
        });
        return _semanticInfo.GetExpressionType(indexAccess)!;
    }
}
```

### Type Parameter Substitution

When checking generic function calls, type parameters are substituted:

```csharp
private SemanticType SubstituteTypeParameters(
    SemanticType type,
    List<TypeParameterDef> typeParams,
    List<SemanticType> typeArgs)
{
    var substitutions = new Dictionary<string, SemanticType>();
    for (int i = 0; i < typeParams.Count; i++)
    {
        substitutions[typeParams[i].Name] = typeArgs[i];
    }

    return SubstituteTypeParametersInType(type, substitutions);
}
```

This recursively replaces `TypeParameterType` nodes with their concrete type arguments.

---

## Super() Validation

Sharpy has strict rules for `super()` usage to ensure correct inheritance semantics:

### Rule 1: super().__init__() in Constructors

```python
class Child(Parent):
    def __init__(self):
        super().__init__()  # Must be first statement
        self.x = 10
```

Validation:

```csharp
if (_currentMethodName == "__init__")
{
    if (calledMethodName != "__init__")
    {
        AddError("super() in __init__ can only call super().__init__(...)");
    }
    else if (_controlFlowDepth > 0)
    {
        AddError("super().__init__() must be the first statement in the constructor");
    }
    else if (_superInitCalled)
    {
        AddError("super().__init__() can only be called once");
    }
}
```

### Rule 2: super() in @override Methods

```python
class Child(Parent):
    @override
    def compute(self) -> int:
        return super().compute() + 1  # Must call same method
```

### Rule 3: Cross-Dunder Calls

Dunder methods can call other dunders via super():

```python
class Point:
    @override
    def __eq__(self, other) -> bool:
        return super().__eq__(other)  # Valid

    @override
    def __str__(self) -> str:
        return super().__repr__()  # Valid - cross-dunder
```

---

## Operator Validation

The TypeChecker delegates all operator validation to `OperatorValidator`, which uses reflection to check for dunder method support:

```csharp
var resultType = _operatorValidator.ValidateBinaryOp(
    binOp.Operator,
    leftType,
    rightType,
    binOp.LineStart,
    binOp.ColumnStart);
```

**Why delegation?** Operator validation is complex:
- Different operators map to different dunders (`+` → `__add__`, `*` → `__mul__`)
- Operators can be overloaded on user types via dunders
- Operators may have fallback behavior (e.g., `a + b` tries `a.__add__(b)` then `b.__radd__(a)`)
- Augmented assignments prefer in-place operators (`+=` → `__iadd__` or `__add__`)

The `OperatorValidator` encapsulates all this complexity.

---

## Protocol Validation

Sharpy validates that types support required protocols (iteration, indexing, len):

### Iteration Protocol

```python
for x in items:  # items must support __iter__
    print(x)
```

Validation:

```csharp
var elementType = _protocolValidator.ValidateIteration(
    iterType,
    forStmt.Iterator.LineStart,
    forStmt.Iterator.ColumnStart);
```

### Indexing Protocol

```python
x = items[0]  # items must support __getitem__
```

Validation:

```csharp
return _protocolValidator.ValidateIndexAccess(
    objectType,
    indexType,
    indexAccess.LineStart,
    indexAccess.ColumnStart);
```

### Len Protocol

```python
n = len(items)  # items must support __len__
```

Validation:

```csharp
return _protocolValidator.ValidateLen(
    argTypes[0],
    call.LineStart,
    call.ColumnStart);
```

---

## Patterns and Design Decisions

### 1. Visitor Pattern via Switch Expressions

The TypeChecker uses **switch expressions** instead of the traditional Visitor pattern:

```csharp
SemanticType type = expr switch
{
    IntegerLiteral => SemanticType.Int,
    BinaryOp binOp => CheckBinaryOp(binOp),
    FunctionCall call => CheckFunctionCall(call),
    _ => SemanticType.Unknown
};
```

**Why?** Modern C# pattern matching provides the benefits of the Visitor pattern (exhaustiveness, type safety) with less boilerplate.

### 2. Memoization for Performance

Expression types are cached to avoid redundant checking:

```csharp
var cached = _semanticInfo.GetExpressionType(expr);
if (cached != null)
    return cached;
```

**Why?** The same expression may be checked multiple times (e.g., in different branches). Caching provides significant performance improvement for large ASTs.

### 3. Context Stack via Fields

Instead of passing context through method parameters, the TypeChecker uses mutable fields:

```csharp
private SemanticType? _currentFunctionReturnType;
private TypeSymbol? _currentClass;
```

**Trade-off**:
- ✅ Simpler method signatures
- ✅ Easier to add new context
- ❌ Less functional, harder to parallelize
- ❌ Must carefully save/restore context

This is acceptable because semantic analysis is inherently sequential.

### 4. Fail-Fast with Unknown Types

When encountering errors, the TypeChecker often returns `SemanticType.Unknown`:

```csharp
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown;
}
```

**Why?** This prevents cascading errors. If we already reported an error for `leftType`, we don't want to report 10 more errors about expressions that use it.

### 5. Delegation to Specialized Validators

The TypeChecker is a **coordinator**, not a monolith. It delegates:
- Operator validation → `OperatorValidator`
- Protocol checking → `ProtocolValidator`
- Access control → `AccessValidator`
- Control flow → `ControlFlowValidator`
- Default parameters → `DefaultParameterValidator`

**Why?** Each validator encapsulates domain knowledge and can be tested independently. This follows the **Single Responsibility Principle**.

---

## Debugging Tips

### 1. Enable Detailed Logging

Pass a logger with debug level enabled:

```csharp
var logger = new ConsoleLogger { Level = LogLevel.Debug };
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
```

This will log each function/class being checked.

### 2. Inspect SemanticInfo

After type checking, inspect `_semanticInfo`:

```csharp
var exprType = semanticInfo.GetExpressionType(someExpression);
var identifierSymbol = semanticInfo.GetIdentifierSymbol(someIdentifier);
```

This shows what types were inferred for each expression.

### 3. Check Error Aggregation

Remember that errors come from multiple validators:

```csharp
var allErrors = typeChecker.Errors;  // Aggregates from all validators
```

If an error is missing, check all validator error lists.

### 4. Trace Context Changes

Add breakpoints when context is set/cleared:

```csharp
_currentClass = classSymbol;  // ← Breakpoint here
_currentFunctionReturnType = returnType;  // ← And here
```

Many bugs stem from incorrect context management.

### 5. Reproduce with Minimal AST

If type checking fails on a large file, create a minimal AST by hand that reproduces the issue:

```csharp
var module = new Module
{
    Body = new List<Statement>
    {
        new FunctionDef
        {
            Name = "test",
            Parameters = new List<Parameter>(),
            Body = new List<Statement> { /* minimal body */ }
        }
    }
};
typeChecker.CheckModule(module);
```

This isolates the problem from parser/lexer issues.

---

## Common Validation Scenarios

### Adding a New Expression Type

1. Add case to `CheckExpression` switch in `TypeChecker.Expressions.cs`
2. Implement validation method (e.g., `CheckMyNewExpr`)
3. Return appropriate `SemanticType`
4. Cache result in `SemanticInfo`

### Adding a New Statement Type

1. Add case to `CheckStatement` switch in `TypeChecker.cs`
2. Implement validation method in `TypeChecker.Statements.cs`
3. Handle scope creation if needed
4. Update `_controlFlowDepth` for control flow statements

### Adding a New Type Rule

1. Identify which validator owns the rule
2. Add validation logic to that validator
3. Ensure errors are collected in validator's error list
4. Test with edge cases

---

## Contribution Guidelines

### When to Modify TypeChecker

- Adding support for new language features
- Improving type inference accuracy
- Adding new type narrowing patterns
- Fixing type compatibility bugs
- Improving error messages

### When NOT to Modify TypeChecker

- To change how types are represented → Modify `SemanticType` hierarchy
- To add new symbols → Modify `Symbol` hierarchy and `NameResolver`
- To change scoping rules → Modify `SymbolTable`
- To change how types are resolved → Modify `TypeResolver`

### Testing Expectations

New type checking features should include:
1. **Unit tests** for the specific validation logic
2. **Integration tests** with full AST → semantic analysis → errors
3. **Negative tests** ensuring invalid code is rejected
4. **Edge case tests** (generics, inheritance, nullable, etc.)

### Code Style

- Keep methods focused (< 100 lines when possible)
- Add XML doc comments for public methods
- Use descriptive error messages with line/column info
- Maintain the partial class organization:
  - Definitions → `TypeChecker.Definitions.cs`
  - Expressions → `TypeChecker.Expressions.cs`
  - Statements → `TypeChecker.Statements.cs`
  - Utilities → `TypeChecker.Utilities.cs`

---

## Cross-References

### Related Partial Class Files

- [TypeChecker.Definitions.md](./TypeChecker.Definitions.md) - Type definition checking
- [TypeChecker.Expressions.md](./TypeChecker.Expressions.md) - Expression type checking
- [TypeChecker.Statements.md](./TypeChecker.Statements.md) - Statement validation
- [TypeChecker.Utilities.md](./TypeChecker.Utilities.md) - Helper methods

### Related Components

- [SymbolTable.md](./SymbolTable.md) - Symbol storage and scope management
- [TypeResolver.md](./TypeResolver.md) - Type annotation resolution
- [NameResolver.md](./NameResolver.md) - Symbol registration (phase 1)
- [SemanticInfo.md](./SemanticInfo.md) - Type information storage
- [ControlFlowValidator.md](./ControlFlowValidator.md) - Control flow analysis
- [OperatorValidator.md](./OperatorValidator.md) - Operator validation
- [ProtocolValidator.md](./ProtocolValidator.md) - Protocol checking

### Specification Documents

- `docs/language_specification/type_annotations.md` - Type annotation syntax
- `docs/language_specification/type_casting.md` - Type casting rules
- `docs/language_specification/type_hierarchy.md` - Type compatibility
- `docs/language_specification/type_narrowing.md` - Type narrowing rules

---

## Summary

The TypeChecker is the **semantic validation orchestrator** for the Sharpy compiler. It:

1. **Dispatches** statements and expressions to specialized handlers
2. **Delegates** domain-specific validation to focused validators
3. **Tracks context** (current function, class, method, narrowed types)
4. **Infers types** for expressions and variables
5. **Validates compatibility** for assignments and calls
6. **Enforces rules** for super(), inheritance, protocols, access control
7. **Collects errors** from all validators for reporting

**Key Insight**: The TypeChecker is not a monolith - it's a coordinator that leverages composition and delegation to manage complexity. Understanding how it orchestrates the various validators is key to working with this component effectively.

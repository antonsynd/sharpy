# Walkthrough: TypeChecker.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

---

## Overview

The `TypeChecker` is the heart of the Sharpy compiler's semantic analysis phase. It performs comprehensive type checking, type inference, and semantic validation on the Abstract Syntax Tree (AST) produced by the Parser. This component ensures that all type rules are followed, expressions are well-typed, and the code adheres to Sharpy's language semantics before code generation.

**Role in the Compiler Pipeline:**
- **Input**: AST from Parser (expressions, statements, definitions), SymbolTable from NameResolver
- **Output**: Populated `SemanticInfo` with expression types and errors list
- **Downstream**: RoslynEmitter uses the validated AST and type information to generate C#

**Key Responsibilities:**
1. Type checking all expressions and statements
2. Type inference for variables and function returns
3. Validating type compatibility and assignments
4. Type narrowing in conditional contexts
5. Protocol validation (iteration, indexing, operators) via ValidationPipeline
6. Access level validation (private/public/protected) via ValidationPipeline
7. Control flow analysis (return paths, unreachable code) via ValidationPipeline
8. Super() validation and inheritance rules
9. Generic type instantiation and substitution

---

## Architecture: Partial Classes

The `TypeChecker` is split across **four partial class files** for better maintainability:

1. **TypeChecker.cs** - Main class definition, module checking, error aggregation, dependencies
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
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    // Validation pipeline (always enabled)
    private readonly ValidationPipeline _validationPipeline;

    // Type inference service - extracted for clean separation
    private readonly TypeInferenceService _typeInference;

    // Optional centralized service access (preferred for new code)
    private readonly CompilerServices? _services;

    // Context tracking
    private SemanticType? _currentFunctionReturnType;
    private TypeSymbol? _currentClass;
    private Dictionary<string, SemanticType> _narrowedTypes = new();
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

**Key Evolution (2026)**: The TypeChecker has migrated from individual validator fields to:
- **ValidationPipeline**: Orchestrates validators (control flow, operators, protocols, access, unused warnings)
- **TypeInferenceService**: Centralized type inference logic (was distributed across validators)
- **CompilerServices**: Centralized service container (preferred constructor pattern)

---

## Key Dependencies

### Validation Architecture

The TypeChecker uses a **two-phase validation** approach:

**Phase 1: Direct Type Checking (TypeChecker itself)**
- Expression type computation
- Statement validation
- Assignment compatibility
- Function call argument checking
- Type narrowing extraction

**Phase 2: Specialized Validators (ValidationPipeline)**
- **ControlFlowValidator** - Return paths, unreachable code, break/continue
- **AccessValidator** - Field/method access level enforcement
- **OperatorValidator** - Operator support via protocol methods
- **ProtocolValidator** - Iteration, indexing, len protocols
- **DefaultParameterValidator** - Default parameter constant validation

**Design Pattern**: The TypeChecker is a **coordinator**, not a monolith. It delegates specialized validation to the ValidationPipeline, which runs all validators in sequence.

### Type Inference Service

```csharp
var sharedClrCache = new ClrMemberCache();
_typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);
```

The `TypeInferenceService` provides:
- `InferBinaryOpType()` - Binary operator result types
- `InferUnaryOpType()` - Unary operator result types
- `InferIndexAccessType()` - Subscript operation result types
- `InferIterableElementType()` - For loop element type inference
- `InferAugmentedAssignmentType()` - Augmented assignment (`+=`) result types

**Why separate?** Type inference is complex and reusable. Extracting it into a service makes it independently testable and available to other components.

### Constructor Patterns

The TypeChecker supports **two constructor patterns**:

```csharp
// Legacy constructor (individual services)
public TypeChecker(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null,
    ValidationPipeline? validationPipeline = null)

// Preferred constructor (centralized services)
public TypeChecker(
    CompilerServices services,
    ValidationPipeline? validationPipeline = null)
```

**New code should use the `CompilerServices` constructor** for centralized service management and to ensure all components share the same service instances.

---

## Entry Points

### CheckModule

**Location**: `TypeChecker.cs:123`

```csharp
public void CheckModule(Module module, bool computeCodeGenInfo = false)
{
    _logger.LogInfo("Type checking module");

    // Phase 1: Direct type checking
    foreach (var statement in module.Body)
    {
        CheckStatement(statement);
    }

    // Phase 2: Validation pipeline
    var context = CreateSemanticContext();
    context.MergeFromLegacyErrors(_errors);
    context.MergeFromLegacyErrors(_typeResolver.Errors);

    _validationPipeline.Validate(module, context);

    // Merge errors back
    foreach (var error in _typeResolver.Errors)
    {
        bool isDuplicate = _errors.Any(e =>
            e.Line == error.Line && e.Message == error.Message);
        if (!isDuplicate)
        {
            _errors.Add(error);
        }
    }

    foreach (var error in context.Diagnostics.GetErrors())
    {
        // Deduplication with special handling for operator errors...
        if (!isDuplicate)
        {
            _errors.Add(new SemanticError(error.Message, error.Line, error.Column));
        }
    }

    // Optional: Compute CodeGenInfo for RoslynEmitter
    if (computeCodeGenInfo)
    {
        var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable);
        codeGenInfoComputer.ComputeForModule(module);
    }
}
```

**Important Details**:
1. **Two-Phase Validation**: Direct checking first, then ValidationPipeline
2. **Error Deduplication**: Prevents duplicate errors from different validation phases
3. **CodeGenInfo Computation**: Optional step that calculates mangled names, access modifiers, and other metadata needed by RoslynEmitter

**Why `computeCodeGenInfo` parameter?** Code generation metadata is only needed when compiling, not when just checking syntax or running analysis tools.

### CheckStatement (Dispatcher)

**Location**: `TypeChecker.cs:177`

```csharp
private void CheckStatement(Statement statement)
{
    switch (statement)
    {
        case FunctionDef functionDef: CheckFunction(functionDef); break;
        case ClassDef classDef: CheckClass(classDef); break;
        case StructDef structDef: CheckStruct(structDef); break;
        case InterfaceDef interfaceDef: CheckInterface(interfaceDef); break;
        case EnumDef enumDef: CheckEnum(enumDef); break;
        case Assignment assignment: CheckAssignment(assignment); break;
        case VariableDeclaration varDecl: CheckVariableDeclaration(varDecl); break;
        case ReturnStatement returnStmt: CheckReturn(returnStmt); break;
        case IfStatement ifStmt: CheckIf(ifStmt); break;
        case WhileStatement whileStmt: CheckWhile(whileStmt); break;
        case ForStatement forStmt: CheckFor(forStmt); break;
        case RaiseStatement raiseStmt: CheckRaise(raiseStmt); break;
        case TryStatement tryStmt: CheckTry(tryStmt); break;
        case AssertStatement assertStmt: CheckAssert(assertStmt); break;
        case ExpressionStatement exprStmt: CheckExpression(exprStmt.Expression); break;
        case PassStatement:
        case BreakStatement:
        case ContinueStatement:
            // No type checking needed (control flow validation in pipeline)
            break;
        case ImportStatement:
        case FromImportStatement:
            // Import validation handled by ImportResolver
            break;
        default:
            _logger.LogWarning($"Unhandled statement type: {statement.GetType().Name}", 0, 0);
            break;
    }
}
```

This is the **central dispatcher** for statement type checking. It uses pattern matching to route each statement type to its specialized handler.

**Design Decision**: The switch-based dispatcher is simple and efficient. The compiler will warn about missing cases if new statement types are added.

---

## Error Handling

### Error Collection

**Location**: `TypeChecker.cs:113`

```csharp
/// <summary>
/// Gets all semantic errors from type checking and validation.
/// </summary>
/// <remarks>
/// Errors come from:
/// 1. Direct TypeChecker errors (_errors) - includes type mismatch, undefined symbols
/// 2. TypeResolver errors (unresolved types) - merged in CheckModule
/// 3. validators via ValidationPipeline (control flow, access, operators, protocols) - merged in CheckModule
///
/// Legacy validators are still instantiated but their errors are no longer collected here.
/// They remain for backward compatibility with code that calls their validation methods directly.
/// Error reporting is now handled by validators and direct TypeChecker error reporting.
/// </remarks>
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Important**: The `Errors` property returns only the merged errors from `_errors`. The actual merging happens in `CheckModule()`, which combines:
1. Direct TypeChecker errors
2. TypeResolver errors (unresolved type annotations)
3. ValidationPipeline errors (from validators)

**Migration Note**: Legacy validators (individual `ControlFlowValidator`, `OperatorValidator`, etc.) are no longer used for error collection. All validation now flows through the ValidationPipeline.

### Error Limits

```csharp
public bool ContinueAfterError { get; set; } = true;
public int MaxErrors { get; set; } = 100;
```

```csharp
private void AddError(string message, int? line = null, int? column = null)
{
    if (_errors.Count >= MaxErrors)
    {
        if (_errors.Count == MaxErrors)
        {
            _logger.LogError("Maximum error count reached, stopping type checking", 0, 0);
        }
        if (!ContinueAfterError)
        {
            throw new SemanticAnalysisException("Type checking failed with too many errors");
        }
        return;
    }

    var error = new SemanticError(message, line, column);
    _errors.Add(error);
    _logger.LogError(error.Message, line ?? 0, column ?? 0);
}
```

**Design Decision**: By default, the type checker continues after errors to report as many issues as possible in one pass. This improves developer experience (fix multiple errors at once) but may produce misleading secondary errors.

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
- Validating access to private members (via ValidationPipeline)
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
- `super().__init__()` must be first statement in constructors (not inside control flow)
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

The `_narrowedTypes` dictionary maps variable names (or narrowing keys like `arr[i]`) to their narrowed types within the current conditional scope.

### Exception Handling Context

```csharp
private bool _inExceptBlock = false;
```

Tracks whether we're inside an `except` block. Required for validating bare `raise` statements (which can only appear in exception handlers).

---

## Key Methods by Component

### Type Definitions (TypeChecker.Definitions.cs)

See [TypeChecker.Definitions.md](./TypeChecker.Definitions.md) for detailed walkthrough.

**Summary**:
- `CheckFunction` - Validates function signatures, parameters, decorators, override rules
- `CheckClass` - Validates class definitions, fields, methods, inheritance, interfaces
- `CheckStruct` - Validates struct definitions with special rules (all fields must be initialized)
- `CheckInterface` - Validates interface method signatures
- `CheckEnum` - Validates enum members have explicit values of consistent types

**Key Pattern**: All definition checkers follow the same structure:
1. Enter a new scope
2. Register type parameters (for generics)
3. Resolve field/parameter types using `_typeResolver`
4. Check members/body
5. Run specialized validation rules
6. Exit scope

### Expression Checking (TypeChecker.Expressions.cs)

See [TypeChecker.Expressions.md](./TypeChecker.Expressions.md) for detailed walkthrough.

**Summary**:
- `CheckExpression` - Main dispatcher with result caching
- `CheckIdentifier` - Symbol lookup and type narrowing
- `CheckBinaryOp` - Binary operators (uses TypeInferenceService)
- `CheckUnaryOp` - Unary operators (uses TypeInferenceService)
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
    // Check cache
    var cached = _semanticInfo.GetExpressionType(expr);
    if (cached != null)
        return cached;

    SemanticType type = expr switch { /* ... */ };

    // Cache result
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

The `_controlFlowDepth` counter tracks nesting depth for super() validation (ensuring `super().__init__()` is not inside control flow).

### Utilities (TypeChecker.Utilities.cs)

See [TypeChecker.Utilities.md](./TypeChecker.Utilities.md) for detailed walkthrough.

**Summary**:
- `IsAssignable` - Type compatibility checking with nullable and generic variance
- `ExtractNarrowedTypes` - Extracts type narrowing from conditionals
- `SubstituteTypeParameters` - Generic type argument substitution
- `FindFieldInHierarchy`, `FindMethodInHierarchy` - Inheritance traversal
- `ValidateSuperMemberAccess` - super() validation with strict rules
- `ValidateConstructorOverloads` - Ensures unique constructor signatures
- `ValidateStructRules` - Struct-specific validation (field initialization)
- `ValidateEnumRules` - Enum-specific validation (explicit values, consistent types)
- `ValidateInterfaceImplementations` - Interface contract checking

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

### Supported Narrowing Patterns

**Location**: `TypeChecker.Utilities.cs:11`

```csharp
private Dictionary<string, SemanticType> ExtractNarrowedTypes(
    Expression condition,
    bool isPositiveBranch)
{
    // 1. Handle 'A and B' pattern - combine narrowings from both sides
    if (condition is BinaryOp { Operator: BinaryOperator.And } andOp && isPositiveBranch)
    {
        var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);
        var rightNarrowed = ExtractNarrowedTypes(andOp.Right, true);
        // Merge dictionaries...
    }

    // 2. Handle 'x is not None' pattern
    if (condition is BinaryOp { Operator: BinaryOperator.IsNot } binOp)
    {
        if (binOp.Left is Identifier id && binOp.Right is NoneLiteral)
        {
            if (isPositiveBranch)
            {
                // Narrow nullable to non-nullable
                var symbol = _symbolTable.Lookup(id.Name);
                if (symbol is VariableSymbol varSymbol && varSymbol.Type is NullableType nullable)
                {
                    narrowedTypes[id.Name] = nullable.UnderlyingType;
                }
            }
        }
    }

    // 3. Handle 'x is None' pattern (negative branch narrowing)
    else if (condition is BinaryOp { Operator: BinaryOperator.Is } isOp)
    {
        if (isOp.Left is Identifier id && isOp.Right is NoneLiteral)
        {
            if (!isPositiveBranch)  // In else branch
            {
                // Narrow to non-nullable
                var symbol = _symbolTable.Lookup(id.Name);
                if (symbol is VariableSymbol varSymbol && varSymbol.Type is NullableType nullable)
                {
                    narrowedTypes[id.Name] = nullable.UnderlyingType;
                }
            }
        }
    }

    // 4. Handle 'isinstance(x, Type)' pattern
    else if (condition is FunctionCall { Function: Identifier { Name: "isinstance" } } call)
    {
        if (call.Arguments.Length >= 2 && isPositiveBranch)
        {
            string? narrowingKey = ExtractNarrowingKey(call.Arguments[0]);
            if (narrowingKey != null && call.Arguments[1] is Identifier typeId)
            {
                var typeSymbol = _symbolTable.Lookup(typeId.Name) as TypeSymbol;
                if (typeSymbol != null)
                {
                    narrowedTypes[narrowingKey] = new UserDefinedType { Symbol = typeSymbol };
                }
            }
        }
    }

    return narrowedTypes;
}
```

### Narrowing Key Extraction

Supports narrowing not just simple variables but also subscript expressions:

```csharp
private string? ExtractNarrowingKey(Expression expr)
{
    return expr switch
    {
        Identifier id => id.Name,
        IndexAccess indexAccess => $"{ExtractNarrowingKey(indexAccess.Object)}[{ExtractNarrowingKey(indexAccess.Index)}]",
        _ => null
    };
}
```

This allows patterns like:
```python
items: list[int?] = [1, None, 3]
if items[0] is not None:
    print(items[0] + 5)  # items[0] narrowed to int
```

### Application in Conditionals

Narrowed types are applied when entering conditional branches and restored when exiting:

```csharp
// Extract narrowings for then and else branches
var narrowedTypesInThen = ExtractNarrowedTypes(ifStmt.Test, true);
var narrowedTypesInElse = ExtractNarrowedTypes(ifStmt.Test, false);

// Save current narrowed types
var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);

// Apply then-branch narrowings
foreach (var kvp in narrowedTypesInThen)
{
    _narrowedTypes[kvp.Key] = kvp.Value;
}

// Check then branch statements...
_symbolTable.EnterScope("if-then");
foreach (var stmt in ifStmt.ThenBody)
    CheckStatement(stmt);
_symbolTable.ExitScope();

// Apply else-branch narrowings
_narrowedTypes = new Dictionary<string, SemanticType>(savedNarrowedTypes);
foreach (var kvp in narrowedTypesInElse)
{
    _narrowedTypes[kvp.Key] = kvp.Value;
}

// Check else branch statements...

// Restore original types
_narrowedTypes = savedNarrowedTypes;
```

---

## Generic Type Handling

### Generic Type Instantiation

Sharpy supports generic types like `Box[int]` which are parsed as `IndexAccess` expressions. The TypeChecker recognizes these patterns and creates `GenericType` instances:

**Location**: `TypeChecker.Expressions.cs:631`

```csharp
// Special handling for generic type reference: Box[int] or Pair[int, str]
if (indexAccess.Object is Identifier typeId)
{
    var symbol = _symbolTable.Lookup(typeId.Name);

    // Handle generic type reference (e.g., Box[int])
    if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
    {
        var typeArgs = TryResolveTypeArguments(indexAccess.Index);
        if (typeArgs != null)
        {
            // Return a GenericType representing the instantiated type
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
// Handle generic function reference (e.g., identity[int])
if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
{
    var typeArgs = TryResolveTypeArguments(indexAccess.Index);
    if (typeArgs != null)
    {
        // Store the type arguments in SemanticInfo for use in CheckFunctionCall
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

When checking generic function calls, type parameters are substituted with their concrete type arguments:

**Location**: `TypeChecker.Utilities.cs:153`

```csharp
private SemanticType SubstituteTypeParameters(
    SemanticType type,
    List<TypeParameterDef> typeParams,
    List<SemanticType> typeArgs)
{
    if (typeParams.Count != typeArgs.Count)
        return type;

    // Create a mapping from type parameter name to type argument
    var substitutions = new Dictionary<string, SemanticType>();
    for (int i = 0; i < typeParams.Count; i++)
    {
        substitutions[typeParams[i].Name] = typeArgs[i];
    }

    return SubstituteTypeParametersInType(type, substitutions);
}

private SemanticType SubstituteTypeParametersInType(
    SemanticType type,
    Dictionary<string, SemanticType> substitutions)
{
    return type switch
    {
        TypeParameterType tpt when substitutions.TryGetValue(tpt.Name, out var subst) => subst,
        GenericType gt => new GenericType
        {
            Name = gt.Name,
            TypeArguments = gt.TypeArguments.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList(),
            GenericDefinition = gt.GenericDefinition
        },
        NullableType nt => new NullableType
        {
            UnderlyingType = SubstituteTypeParametersInType(nt.UnderlyingType, substitutions)
        },
        FunctionType ft => new FunctionType
        {
            ParameterTypes = ft.ParameterTypes.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList(),
            ReturnType = SubstituteTypeParametersInType(ft.ReturnType, substitutions)
        },
        TupleType tt => new TupleType
        {
            ElementTypes = tt.ElementTypes.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList()
        },
        _ => type // For types that don't contain type parameters, return as-is
    };
}
```

This recursively walks the type structure and replaces `TypeParameterType` nodes with their concrete type arguments.

---

## Super() Validation

Sharpy has strict rules for `super()` usage to ensure correct inheritance semantics. These rules are enforced in `ValidateSuperMemberAccess()` and `ValidateSuperContextRules()`.

**Location**: `TypeChecker.Utilities.cs:484` and `TypeChecker.Utilities.cs:576`

### Rule 1: super().__init__() in Constructors

```python
class Child(Parent):
    def __init__(self):
        super().__init__()  # Must be first statement, not in control flow
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
        AddError("super().__init__() must be the first statement in the constructor, not inside control flow");
    }
    else if (_superInitCalled)
    {
        AddError("super().__init__() can only be called once");
    }
    return;
}
```

### Rule 2: super() in @override Methods

```python
class Child(Parent):
    @override
    def compute(self) -> int:
        return super().compute() + 1  # Must call same method
```

Validation:

```csharp
if (_currentMethodIsOverride)
{
    // In @override methods, can call same method name
    // OR if it's a dunder override, can call other dunders (cross-dunder)
    if (calledMethodName != _currentMethodName)
    {
        if (!(_currentMethodIsDunder && IsDunderMethod(calledMethodName)))
        {
            AddError($"super() in @override method must call super().{_currentMethodName}(...)");
        }
    }
    return;
}
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

Validation:

```csharp
// Case 3: Inside dunder method (not __init__, not @override)
if (_currentMethodIsDunder)
{
    // Dunder methods can call any dunder via super()
    if (!IsDunderMethod(calledMethodName))
    {
        AddError("super() in dunder method must call a dunder method (e.g., super().__eq__(...))");
    }
    return;
}
```

### Rule 4: Cannot Access Parent Fields

```csharp
// Check 3: Cannot access fields via super()
// Check the entire inheritance chain for fields
var currentType = _currentClass.BaseType;
while (currentType != null)
{
    var field = currentType.Fields.FirstOrDefault(f => f.Name == memberName);
    if (field != null)
    {
        AddError("Cannot access parent fields via super(); only methods are allowed");
        return SemanticType.Unknown;
    }
    currentType = currentType.BaseType;
}
```

**Rationale**: These strict rules ensure:
1. Parent constructors are always called before child initialization
2. Method overrides are explicit and intentional
3. Inheritance hierarchies are well-formed and predictable

---

## Type Inference Service Integration

The TypeChecker delegates type inference to `TypeInferenceService` instead of handling it directly.

**Location**: `TypeChecker.cs:69`

```csharp
// Create shared CLR member cache for efficient reflection caching
var sharedClrCache = new ClrMemberCache();

// Initialize type inference service for inferring result types during type checking
_typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);
```

### Binary Operators

**Location**: `TypeChecker.Expressions.cs:98`

```csharp
private SemanticType CheckBinaryOp(BinaryOp binOp)
{
    // Handle pipe forward operator specially
    if (binOp.Operator == BinaryOperator.PipeForward)
    {
        return CheckPipeForward(binOp);
    }

    var leftType = CheckExpression(binOp.Left);
    var rightType = CheckExpression(binOp.Right);

    // If either operand is Unknown, return Unknown to avoid cascading errors
    if (leftType is UnknownType || rightType is UnknownType)
    {
        return SemanticType.Unknown;
    }

    // Use TypeInferenceService for type inference
    var resultType = _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);

    // If type inference fails, report the error directly
    // (validators may not catch all type incompatibilities)
    if (resultType == null)
    {
        AddError(
            $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binOp.Operator)}' with operand of type '{rightType.GetDisplayName()}'",
            binOp.LineStart,
            binOp.ColumnStart);
        return SemanticType.Unknown;
    }

    return resultType;
}
```

### Augmented Assignments

**Location**: `TypeChecker.Statements.cs:152`

```csharp
// For augmented assignments, use TypeInferenceService
// This handles:
// - Preferring in-place dunder methods (e.g., __iadd__) when available
// - Falling back to binary operators (e.g., __add__) otherwise
var resultType = _typeInference.InferAugmentedAssignmentType(
    assignment.Operator,
    targetType,
    valueType);

// Verify result type is assignable to target type (if inference succeeded)
if (resultType != null && !resultType.IsAssignableTo(targetType))
{
    AddError(
        $"Result type '{resultType.GetDisplayName()}' of augmented assignment is not assignable to target type '{targetType.GetDisplayName()}'",
        assignment.LineStart,
        assignment.ColumnStart);
}
```

### Iteration Element Type

**Location**: `TypeChecker.Statements.cs:434`

```csharp
var iterType = CheckExpression(forStmt.Iterator);

// Infer element type from the iterator (errors reported by validator in pipeline)
var elementType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;
```

**Design Rationale**: Extracting type inference into a service:
1. Makes inference logic testable independently
2. Enables reuse across TypeChecker and validators
3. Centralizes complex logic (protocol methods, CLR reflection)
4. Improves performance (shared `ClrMemberCache`)

---

## Patterns and Design Decisions

### 1. Two-Phase Validation Architecture

**Pattern**: Separate direct type checking from specialized validation.

**Implementation**:
- **Phase 1**: TypeChecker performs core type checking (expressions, assignments, calls)
- **Phase 2**: ValidationPipeline runs specialized validators (control flow, protocols, access)

**Benefits**:
- Validators are independently testable
- Clean separation of concerns
- Easier to add new validators without modifying TypeChecker
- Validators can share context via `SemanticContext`

### 2. Visitor Pattern via Switch Expressions

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

**Why?** Modern C# pattern matching provides the benefits of the Visitor pattern (exhaustiveness, type safety) with less boilerplate. The compiler warns about non-exhaustive switches.

### 3. Memoization for Performance

Expression types are cached to avoid redundant checking:

```csharp
var cached = _semanticInfo.GetExpressionType(expr);
if (cached != null)
    return cached;

// Compute type...

_semanticInfo.SetExpressionType(expr, type);
return type;
```

**Why?** The same expression may be checked multiple times (e.g., in validators). Caching provides significant performance improvement for large ASTs.

### 4. Context Stack via Fields

Instead of passing context through method parameters, the TypeChecker uses mutable fields:

```csharp
private SemanticType? _currentFunctionReturnType;
private TypeSymbol? _currentClass;
private Dictionary<string, SemanticType> _narrowedTypes;
```

**Trade-off**:
- ✅ Simpler method signatures
- ✅ Easier to add new context
- ❌ Less functional, harder to parallelize
- ❌ Must carefully save/restore context

This is acceptable because semantic analysis is inherently sequential.

### 5. Fail-Fast with Unknown Types

When encountering errors, the TypeChecker often returns `SemanticType.Unknown`:

```csharp
if (leftType is UnknownType || rightType is UnknownType)
{
    return SemanticType.Unknown;
}
```

**Why?** This prevents cascading errors. If we already reported an error for `leftType`, we don't want to report 10 more errors about expressions that use it.

### 6. Delegation to Specialized Services

The TypeChecker is a **coordinator**, not a monolith. It delegates:
- Type inference → `TypeInferenceService`
- Specialized validation → `ValidationPipeline` (which runs individual validators)
- Type resolution → `TypeResolver`
- Symbol lookup → `SymbolTable`

**Why?** Each service encapsulates domain knowledge and can be tested independently. This follows the **Single Responsibility Principle**.

### 7. Python-like Variable Semantics

**Pattern**: Simple assignments allow type redefinition (unlike C#).

**Implementation**: `x = 5` followed by `x = "hello"` is valid. The TypeChecker creates a new `VariableSymbol` with the new type in `CheckAssignment()`.

**Rationale**: Sharpy aims to preserve Python's ergonomics while adding static typing. Variables can change types (like Python), but operations are still type-checked (unlike Python).

**Constraints**: Constants (`const x = 5`) cannot be redefined.

---

## Debugging Tips

### 1. Enable Detailed Logging

Pass a logger with debug level enabled:

```csharp
var logger = new ConsoleLogger { Level = LogLevel.Debug };
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
```

This will log each function/class being checked:
- `"Type checking module"`
- `"Type checking function: {name}"`
- `"Type checking class: {name}"`
- `"Validating {N} constructor overloads for '{type}'"`

### 2. Inspect SemanticInfo

After type checking, inspect `_semanticInfo`:

```csharp
typeChecker.CheckModule(module);
var exprType = semanticInfo.GetExpressionType(someExpression);
var identifierSymbol = semanticInfo.GetIdentifierSymbol(someIdentifier);
```

This shows what types were inferred for each expression.

### 3. Check Error Sources

Errors come from three sources:

```csharp
var allErrors = typeChecker.Errors;  // Merged in CheckModule()
// Includes:
// 1. Direct TypeChecker errors (_errors)
// 2. TypeResolver errors
// 3. ValidationPipeline errors (from validators)
```

If an error is missing, check:
- Direct TypeChecker error reporting (`AddError()` calls)
- TypeResolver error list
- Individual validator error reporting

### 4. Trace Context Changes

Add breakpoints when context is set/cleared:

```csharp
_currentClass = classSymbol;  // ← Breakpoint here
_currentFunctionReturnType = returnType;  // ← And here
_narrowedTypes[varName] = narrowedType;  // ← For type narrowing
```

Many bugs stem from incorrect context management.

### 5. Test Type Inference Separately

If type inference seems wrong, test `TypeInferenceService` directly:

```csharp
var inferenceService = new TypeInferenceService(symbolTable, new ClrMemberCache());
var resultType = inferenceService.InferBinaryOpType(BinaryOperator.Add, intType, intType);
```

This isolates inference logic from validation logic.

### 6. Reproduce with Minimal AST

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

Example:
```csharp
// In CheckExpression():
SliceExpression slice => CheckSliceExpression(slice),

// New method:
private SemanticType CheckSliceExpression(SliceExpression slice)
{
    CheckExpression(slice.Start);
    CheckExpression(slice.Stop);
    if (slice.Step != null) CheckExpression(slice.Step);
    return new SliceType();  // Hypothetical
}
```

### Adding a New Statement Type

1. Add case to `CheckStatement` switch in `TypeChecker.cs`
2. Implement validation method in `TypeChecker.Statements.cs`
3. Handle scoping if the statement introduces a new scope
4. Update `_controlFlowDepth` for control flow statements
5. Add tests

### Adding a New Type Narrowing Pattern

1. Extend `ExtractNarrowedTypes()` in `TypeChecker.Utilities.cs`
2. Handle the pattern in the `if/elif/while` conditional logic
3. Add tests for the narrowing behavior

Example:
```csharp
// Support 'len(x) > 0' narrowing empty collections
if (condition is BinaryOp { Operator: BinaryOperator.GreaterThan } gt &&
    gt.Left is FunctionCall { Function: Identifier { Name: "len" } } &&
    gt.Right is IntegerLiteral { Value: 0 })
{
    // Narrow collection to non-empty
}
```

### Adding a New Validator

For complex validation logic:

1. Create a new validator implementing `IModuleValidator`
2. Add to `ValidationPipelineFactory.CreateDefault()`
3. Ensure errors use `context.ReportError()` or `context.ReportWarning()`
4. TypeChecker will automatically merge validator errors

**Do NOT** add complex validation logic directly to TypeChecker—use the ValidationPipeline.

### Modifying Type Inference

For new operations or improved inference:

1. Modify `TypeInferenceService` (in `src/Sharpy.Compiler/Services/`)
2. Add cases to `InferBinaryOpType()`, `InferUnaryOpType()`, etc.
3. Update tests in `TypeInferenceServiceTests.cs`

**Do NOT** embed type inference logic in TypeChecker—delegate to the service.

---

## Contribution Guidelines

### When to Modify TypeChecker

- Adding support for new language features (expressions, statements)
- Improving type narrowing patterns
- Fixing type compatibility bugs
- Improving direct error messages
- Adding new definition types (classes, functions, etc.)

### When NOT to Modify TypeChecker

- To change how types are represented → Modify `SemanticType` hierarchy
- To add new symbols → Modify `Symbol` hierarchy and `NameResolver`
- To change scoping rules → Modify `SymbolTable`
- To change how types are resolved → Modify `TypeResolver`
- To add specialized validation → Create a new validator
- To change type inference → Modify `TypeInferenceService`

### Testing Expectations

New type checking features should include:
1. **Unit tests** for the specific validation logic
2. **Integration tests** with full AST → semantic analysis → errors
3. **Negative tests** ensuring invalid code is rejected
4. **Edge case tests** (generics, inheritance, nullable, type narrowing, etc.)

### Code Style

- Keep methods focused (< 100 lines when possible)
- Add XML doc comments for public methods
- Use descriptive error messages with line/column info
- Maintain the partial class organization:
  - Core orchestration → `TypeChecker.cs`
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
- [TypeInferenceService.md](./TypeInferenceService.md) - Type inference logic
- [Validation/ValidationPipeline.md](./Validation/ValidationPipeline.md) - validator orchestration
- [CodeGenInfoComputer.md](./CodeGenInfoComputer.md) - Code generation metadata

### Specification Documents

- `docs/language_specification/type_annotations.md` - Type annotation syntax
- `docs/language_specification/type_casting.md` - Type casting rules
- `docs/language_specification/type_hierarchy.md` - Type compatibility
- `docs/language_specification/type_narrowing.md` - Type narrowing rules

---

## Summary

The TypeChecker is the **semantic validation orchestrator** for the Sharpy compiler. It:

1. **Dispatches** statements and expressions to specialized handlers
2. **Delegates** type inference to `TypeInferenceService`
3. **Coordinates** with `ValidationPipeline` for specialized validation
4. **Tracks context** (current function, class, method, narrowed types)
5. **Infers types** for expressions and variables
6. **Validates compatibility** for assignments and calls
7. **Enforces rules** for super(), inheritance, generics
8. **Collects errors** from all sources and merges them for reporting

**Key Evolution (2026)**: The TypeChecker has migrated from individual validator composition to a **two-phase validation architecture** using `ValidationPipeline` and `TypeInferenceService`. This separation of concerns makes the codebase more maintainable and testable.

**Key Insight**: The TypeChecker is not a monolith—it's a coordinator that leverages the ValidationPipeline and TypeInferenceService to manage complexity. Understanding how it orchestrates these services is key to working with this component effectively.

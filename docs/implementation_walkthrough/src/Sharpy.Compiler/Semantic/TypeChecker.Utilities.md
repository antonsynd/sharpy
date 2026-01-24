# Walkthrough: TypeChecker.Utilities.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs`

---

## Overview

This file contains the utility and validation methods for the `TypeChecker` partial class. While other TypeChecker partial files handle specific AST node types (Expressions, Statements, Definitions), this file provides the "toolbox" of helper functions that support type checking throughout the compiler.

**Key Responsibilities:**
- Type narrowing extraction (for control flow analysis)
- Type assignability checking (with variance support)
- Type parameter substitution (for generics)
- Assignment target validation
- Constructor/struct/enum validation rules
- Interface implementation validation
- super() expression validation
- Error reporting utilities

This is one of five partial class files that make up the complete TypeChecker:
- `TypeChecker.cs` - Main entry point and orchestration
- `TypeChecker.Definitions.cs` - Type checking for class/function/enum/struct definitions
- `TypeChecker.Expressions.cs` - Type checking for all expression nodes
- `TypeChecker.Statements.cs` - Type checking for all statement nodes
- **`TypeChecker.Utilities.cs`** - Helper methods and validation utilities (this file)

---

## Class/Type Structure

### Partial Class: TypeChecker

This file extends the `TypeChecker` partial class with utility methods. The main class (defined in `TypeChecker.cs`) maintains important state:

```csharp
private readonly SymbolTable _symbolTable;        // Current scope and symbols
private readonly SemanticInfo _semanticInfo;      // Type info for AST nodes
private TypeSymbol? _currentClass;                // Current class context
private string? _currentMethodName;               // Current method name
private bool _currentMethodIsOverride;            // Is current method @override?
private bool _currentMethodIsDunder;              // Is current method a dunder (e.g., __init__)?
private int _controlFlowDepth;                    // Nesting level (for super() validation)
private bool _superInitCalled;                    // Has super().__init__() been called?
private Dictionary<string, SemanticType> _narrowedTypes; // Type narrowing in conditionals
```

### Exception Type: SemanticAnalysisException

A simple exception class for signaling fatal semantic analysis errors when `ContinueAfterError` is false or when `MaxErrors` is exceeded.

---

## Key Functions/Methods

### 1. Type Narrowing

#### `ExtractNarrowedTypes(Expression condition, bool isPositiveBranch)`

**Purpose**: Analyzes conditional expressions to extract type narrowing information for flow-sensitive typing.

**How it works:**
- For `x is not None` in the true branch: narrows `x` from `T?` to `T`
- For `x is None` in the false branch (else): narrows `x` from `T?` to `T`
- For `isinstance(x, Type)` in the true branch: narrows `x` to `Type`
- For `A and B` patterns: combines narrowings from both sides

**Parameters:**
- `condition` - The conditional expression to analyze
- `isPositiveBranch` - true for the "if" branch, false for the "else" branch

**Returns**: Dictionary mapping variable names to their narrowed types

**Example usage in type checker:**
```csharp
// In an if statement:
var narrowed = ExtractNarrowedTypes(ifStmt.Condition, true);
// Apply narrowed types when checking the if body
// Then extract negated narrowing for else branch
var elseNarrowed = ExtractNarrowedTypes(ifStmt.Condition, false);
```

**Design note**: This enables Python-style type narrowing where the type system "remembers" that a nullable check succeeded.

#### `ExtractNarrowingKey(Expression expr)`

**Purpose**: Converts an expression into a string key for type narrowing tracking.

**Supported patterns:**
- `Identifier` → returns the variable name
- `IndexAccess` → returns `"arr[i]"` format (recursively)
- Other expressions → returns `null` (not supported for narrowing)

**Why it exists**: Allows narrowing not just simple variables but also indexed expressions like `arr[0] is not None`.

---

### 2. Type Compatibility

#### `IsAssignable(SemanticType source, SemanticType target)`

**Purpose**: Enhanced assignability checking beyond the basic `IsAssignableTo()` method.

**Extra checks:**
1. **UnknownType escape hatch**: Always returns true if target is `UnknownType` (prevents cascading errors)
2. **Nullable coercion**: Non-nullable `T` can be assigned to `T?`
3. **Generic variance**: Supports covariant assignment for `list` and `set` (e.g., `list[Dog]` → `list[Animal]`)

**Why the variance check?** In Python/Sharpy, lists are mutable but the type system allows covariant assignment for convenience, even though it's technically unsafe in some edge cases.

```csharp
// Example: list[int] can be assigned to list[object]
IsAssignable(list<int>, list<object>) // → true
```

---

### 3. Generic Type Substitution

#### `SubstituteTypeParameters(SemanticType type, List<TypeParameterDef> typeParams, List<SemanticType> typeArgs)`

**Purpose**: Replaces generic type parameters with concrete type arguments.

**Example transformation:**
```csharp
// Given: T -> int mapping
// Transform: list[T] -> list[int]
// Transform: Tuple[T, T] -> Tuple[int, int]
```

**Upstream connection**: Called when instantiating generic functions or classes with specific type arguments.

**Downstream connection**: The substituted types flow into the code generator, which emits C# generic instantiations.

#### `SubstituteTypeParametersInType(SemanticType type, Dictionary<string, SemanticType> substitutions)`

**Purpose**: Recursive worker method that handles all type variants.

**Supported type substitution:**
- `TypeParameterType` - Direct replacement (`T` → `int`)
- `GenericType` - Recursive substitution of type arguments
- `NullableType` - Substitute the underlying type
- `FunctionType` - Substitute parameter and return types
- `TupleType` - Substitute element types
- Other types - Return as-is (already concrete)

---

### 4. Assignment Target Validation

#### `IsValidAssignmentTarget(Expression target)`

**Purpose**: Validates that an expression can appear on the left side of an assignment.

**Valid targets:**
- `Identifier` - Simple variable: `x = 5`
- `MemberAccess` - Attribute: `obj.field = 5`
- `IndexAccess` - Subscript: `arr[0] = 5`
- `TupleLiteral` - Tuple unpacking: `a, b = (1, 2)` (recursively validated)

**Invalid targets:**
- Literals: `5 = x` ❌
- Function calls: `foo() = x` ❌
- Binary expressions: `x + y = z` ❌

**Design decision**: Tuple unpacking is recursively validated - each element must itself be a valid assignment target.

#### `GetAssignmentTargetDescription(Expression target)`

**Purpose**: Generates human-readable error messages for invalid assignment targets.

**Example outputs:**
- `FunctionCall` → "function call 'foo()'"
- `IntegerLiteral` → "integer literal"
- `BinaryOp` → "expression result"

**Why it's needed**: Provides clear, user-friendly error messages instead of generic "invalid assignment" errors.

---

### 5. Collection Element Type Extraction

#### `ExtractElementType(SemanticType iterType)`

**Purpose**: Determines the element type from an iterable type for `for` loop type checking.

**Type mappings:**
- `list[T]` → `T`
- `set[T]` → `T`
- `dict[K, V]` → `K` (iteration yields keys by default, matching Python semantics)
- `Tuple[T1, T2, ...]` → `T1` (simplified; real Python would need better heterogeneous handling)
- `str` → `str` (iterating over a string yields strings)
- Unknown → `Unknown` (prevents cascading errors)

**Upstream connection**: Called when type-checking `for x in iterable:` statements.

**Limitation**: Tuple handling is simplified - a production compiler would need union types or better heterogeneous collection support.

---

### 6. Constructor Validation

#### `ValidateConstructorOverloads(TypeSymbol type)`

**Purpose**: Ensures that multiple `__init__` methods have different signatures.

**Why this exists**: Unlike Python (which only allows one `__init__`), Sharpy supports constructor overloading by mapping to C# constructor overloads. This validation prevents ambiguous overloads.

**Algorithm:**
1. Build signature string for each constructor (comma-separated type names)
2. Exclude `self` parameter from signature
3. Detect duplicates using a HashSet

**Example:**
```python
class Point:
    def __init__(self, x: int, y: int): ...
    def __init__(self, x: int, y: int): ...  # ❌ Duplicate signature error
    def __init__(self, x: float, y: float): ... # ✅ Different signature, OK
```

---

### 7. Struct-Specific Validation

#### `ValidateStructRules(TypeSymbol structSymbol, StructDef structDef)`

**Purpose**: Enforces struct-specific semantic rules (structs have stricter requirements than classes).

**Main rule**: If a struct has a constructor, it **must initialize all fields**.

**Why this matters**: Structs in Sharpy map to C# structs, which require all fields to be initialized to avoid undefined behavior.

#### `ValidateStructConstructorInitializesAllFields(...)`

**Purpose**: Verifies that a struct constructor initializes every field.

**Algorithm:**
1. Find the constructor's FunctionDef in the AST
2. Analyze the constructor body to find `self.field = ...` assignments
3. Track initialized fields in a HashSet
4. Check against all declared fields
5. Report error if any fields are missing

**Important limitation**: Only tracks **unconditional** initializations. Fields initialized inside `if` statements or loops are not counted (they must be initialized unconditionally).

#### `AnalyzeConstructorForFieldInitialization(statements, initializedFields)`

**Purpose**: Recursively walks constructor statements to find field assignments.

**Logic:**
- `Assignment` with `self.field` → Add to initialized set
- `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement` → Ignored (conditional initialization not allowed)

**Design decision**: Conservative approach - only unconditional assignments count. This prevents hard-to-debug cases where a field might be uninitialized.

---

### 8. Enum Validation

#### `ValidateEnumRules(EnumDef enumDef)`

**Purpose**: Enforces Sharpy's enum semantics.

**Rules:**
1. **All enum values must be explicit** (no auto-numbering like Python's `auto()`)
2. **All values must be `int` or `str`** (no other types allowed)
3. **All values must be the same type** (can't mix int and str)

**Why these restrictions?** Sharpy enums map to C# enums, which have stricter requirements than Python's Enum class.

**Example validation:**
```python
enum Status:
    PENDING = 1      # ✅ int
    ACTIVE = 2       # ✅ int
    COMPLETE = "ok"  # ❌ Type mismatch (was int, now str)
```

---

### 9. super() Expression Validation

#### `CheckSuperExpression(SuperExpression superExpr)`

**Purpose**: Validates standalone `super()` expressions.

**Rule**: Standalone `super()` is always invalid - it must be followed by a method call like `super().method()`.

**Returns**: `SemanticType.Unknown` (prevents cascading errors)

#### `ValidateSuperMemberAccess(MemberAccess memberAccess, SuperExpression superExpr)`

**Purpose**: Validates `super().method()` member access and returns the parent method's type.

**Validation checks:**
1. Must be inside a class
2. Class must have a parent (base type)
3. Cannot access fields via `super()` (only methods allowed)
4. Context-specific rules (delegates to `ValidateSuperContextRules`)

**Type resolution**: Looks up the method in the parent class and returns its function type (parameter types + return type).

**Special handling for `__init__`**: Constructors are stored separately in `Constructors` list, so there's special logic to find them.

#### `ValidateSuperContextRules(string calledMethodName, SuperExpression superExpr, MemberAccess memberAccess)`

**Purpose**: Enforces context-sensitive rules for `super()` usage.

**Rule matrix:**

| Current Method Context | Allowed super() calls | Additional constraints |
|------------------------|----------------------|------------------------|
| `__init__` | Only `super().__init__()` | Must be first statement, not in control flow, called only once |
| `@override` method | `super().same_method_name()` | OR cross-dunder if current method is dunder |
| Dunder method (not `__init__`, not `@override`) | Any dunder method | Can call different dunders |
| Regular method | ❌ Not allowed | Error: super() not allowed |

**Why these rules?**
- `__init__` restriction: Ensures proper initialization order (parent before child)
- `@override` restriction: Prevents calling unrelated parent methods
- Dunder flexibility: Allows dunders to compose (e.g., `__eq__` calling `__hash__`)
- Regular method prohibition: Prevents confusing inheritance patterns

**Cross-dunder example:**
```python
class Child(Parent):
    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):  # ✅ Same dunder
            return False
        return super().__hash__() == other.__hash__()  # ✅ Cross-dunder allowed
```

---

### 10. Interface Implementation Validation

#### `ValidateInterfaceImplementations(TypeSymbol typeSymbol, int? declarationLine, int? declarationColumn)`

**Purpose**: Ensures that a class or struct implements all methods required by its interfaces (including inherited interfaces).

**Validation checks:**
1. Collects all interfaces (direct + base interfaces + interfaces from base classes)
2. Collects all methods implemented by the type and its base classes
3. For each interface method:
   - Verifies a method with the same name exists
   - Verifies parameter count matches (excluding `self`)

**Why it exists**: Enforces the interface contract - classes/structs must implement all methods they promise to implement.

**Example error:**
```python
interface Drawable:
    def draw(self) -> None: ...

class Circle(Drawable):  # ❌ Error: missing draw() implementation
    pass
```

#### `CollectAllInterfaces(TypeSymbol type)`

**Purpose**: Gathers all interfaces that a type must implement.

**Collection strategy:**
1. Start with directly implemented interfaces
2. Add interfaces from base class hierarchy (with cycle detection)
3. BFS through interface inheritance to get base interfaces
4. Return deduplicated set

**Why breadth-first search?** Ensures all interface hierarchy levels are covered without getting stuck in cycles.

**Design note**: Uses `HashSet` to prevent duplicates and a `visited` set to prevent infinite loops in case of circular dependencies.

#### `CollectImplementedMethodsByName(TypeSymbol type)`

**Purpose**: Builds a name-to-method dictionary of all methods available in a type's hierarchy.

**Algorithm:**
1. Walk up the inheritance chain (with cycle detection)
2. For each class, add methods to dictionary (if not already present)
3. Prefer most-derived implementations (child methods shadow parent methods)

**Returns**: `Dictionary<string, FunctionSymbol>` - Maps method name to its symbol

**Why name-based?** Interface validation matches by name first, then validates signature compatibility.

---

### 11. Type Checking Helpers

#### `IsDunderMethod(string name)`

**Purpose**: Checks if a method name is a "dunder" method (double underscore).

**Logic**: Must start with `__`, end with `__`, and have content in between (length > 4).

**Examples:**
- `__init__` → true ✅
- `__eq__` → true ✅
- `__` → false ❌ (too short)
- `_private` → false ❌ (single underscore)

#### `IsIntType(SemanticType type)`

**Purpose**: Checks if a type is an integer type.

**Recognized types**: `SemanticType.Int` or `SemanticType.Long`

**Used by**: Enum validation, operator validation

#### `IsStrType(SemanticType type)`

**Purpose**: Checks if a type is a string type.

**Recognized types**: `SemanticType.Str`

**Used by**: Enum validation, string operations

---

### 12. Error Reporting

#### `AddError(string message, int? line = null, int? column = null)`

**Purpose**: Central error reporting with error limiting.

**Features:**
1. **Error limit**: Stops adding errors after `MaxErrors` (default: 100)
2. **Fatal error handling**: Throws `SemanticAnalysisException` if `ContinueAfterError` is false
3. **Logging integration**: Logs errors via `ICompilerLogger`
4. **Error tracking**: Adds to `_errors` list

**Why error limiting?** Prevents overwhelming output when a single mistake causes hundreds of cascading errors.

---

## Dependencies

### Internal Dependencies

**Symbol System** (`Sharpy.Compiler.Semantic`):
- `SymbolTable` - Used extensively to lookup variables, types, and methods
- `TypeSymbol`, `VariableSymbol`, `FunctionSymbol` - Symbol types used in validation
- `SemanticType` and subtypes - Type system foundation

**AST Nodes** (`Sharpy.Compiler.Parser.Ast`):
- `Expression` hierarchy - For narrowing extraction and assignment validation
- `Statement` hierarchy - For constructor body analysis
- `FunctionDef`, `StructDef`, `EnumDef` - For definition validation

**Logging** (`Sharpy.Compiler.Logging`):
- `ICompilerLogger` - For debug and error messages

### Dependencies on Other TypeChecker Partials

This utilities file is used **by** the other partial files:
- `TypeChecker.Statements.cs` - Uses `ExtractNarrowedTypes` for if/while statements
- `TypeChecker.Expressions.cs` - Uses `IsAssignable` and `SubstituteTypeParameters`
- `TypeChecker.Definitions.cs` - Uses validation methods for classes, structs, enums

---

## Patterns and Design Decisions

### 1. **Partial Class Organization**

The TypeChecker is split across multiple files by responsibility:
- **Utilities** (this file) - Pure helper functions, no direct AST traversal
- **Expressions/Statements/Definitions** - AST node type checking

**Benefit**: Keeps files manageable (~650 lines vs. 2000+ monolithic file)

### 2. **Conservative Error Handling**

Philosophy: **Better to error conservatively than allow unsafe code**

Examples:
- Struct field initialization: Only unconditional assignments count
- super() usage: Strict context rules prevent confusing inheritance
- Assignment targets: Only well-defined lvalues allowed

### 3. **Type Narrowing with Dictionary Tracking**

The `_narrowedTypes` dictionary (from main TypeChecker) stores active type narrowings.

**Pattern:**
1. Extract narrowings at control flow boundaries
2. Temporarily apply narrowings in scoped contexts
3. Restore previous narrowings when exiting scope

**Implementation note**: This is **flow-sensitive** typing - the type of a variable can change based on control flow position.

### 4. **Variance in Generics**

Decision: Allow **covariance** for `list` and `set` despite mutability concerns.

**Rationale**: Matches Python's flexibility. Users can pass `list[Dog]` where `list[Animal]` is expected.

**Trade-off**: Technically unsound (could add a Cat to list[Dog]), but pragmatic for usability.

### 5. **Error Recovery with UnknownType**

When a type error occurs, methods return `SemanticType.Unknown` rather than throwing exceptions.

**Benefit**: Allows type checking to continue and find more errors in a single pass.

**Implementation**: `IsAssignable` always returns true for `UnknownType` target to prevent cascading errors.

### 6. **Signature-Based Overload Resolution**

Constructor overloads use **simple type name signatures** rather than full type equivalence.

**Why**: Mirrors C#'s overload resolution rules (Sharpy compiles to C#)

**Example signature**: `"int,int"` for `__init__(self, x: int, y: int)`

---

## Debugging Tips

### Tracking Type Narrowing Issues

If type narrowing isn't working:

1. **Set a breakpoint** in `ExtractNarrowedTypes` with condition on variable name
2. **Check** `_narrowedTypes` dictionary state before/after if/while statements
3. **Log** extracted narrowings: add `_logger.LogDebug($"Narrowed {key} to {type}")` in ExtractNarrowedTypes

**Common issue**: Narrowing gets lost when exiting scope - ensure the calling code saves/restores `_narrowedTypes` properly.

### Debugging super() Validation Errors

If `super()` validation is rejecting valid code:

1. **Check** `_currentMethodName`, `_currentMethodIsOverride`, `_currentMethodIsDunder` state
2. **Verify** that `_controlFlowDepth` is correctly incremented/decremented in statements
3. **Add logging** in `ValidateSuperContextRules` to see which case is triggering

**Common issue**: `_superInitCalled` not being reset between methods - check that the flag is cleared when entering a new method.

### Struct Validation Debugging

If struct constructor validation is incorrectly reporting missing initializations:

1. **Set breakpoint** in `AnalyzeConstructorForFieldInitialization`
2. **Watch** the `initializedFields` HashSet as statements are processed
3. **Check** if assignments are being skipped due to control flow (if/while/for/try blocks)

**Common issue**: Field initialized inside an `if` block - move initialization to top level of constructor.

### Interface Implementation Debugging

If interface validation is reporting missing methods that are actually implemented:

1. **Check** that method names match exactly (case-sensitive)
2. **Verify** the method is in the type's `Methods` collection (not just in AST)
3. **Set breakpoint** in `CollectImplementedMethodsByName` to see what methods are found
4. **Check** if the method might be inherited from a base class

**Common issue**: Method implemented but not registered in `TypeSymbol.Methods` during name resolution phase.

### Generic Type Substitution Issues

If generic types aren't being substituted correctly:

1. **Add logging** in `SubstituteTypeParametersInType` for each case
2. **Verify** the `substitutions` dictionary has correct mappings
3. **Check** that type arguments and type parameters have matching counts

**Common issue**: Nested generics (e.g., `list[list[T]]`) - ensure recursive substitution works correctly.

---

## Contribution Guidelines

### When to Modify This File

**Add new utilities here when:**
- Creating helpers that are used across multiple TypeChecker partial files
- Adding new validation rules (e.g., for a new language feature)
- Implementing new type system features (e.g., union types, intersection types)

**Examples of changes:**
- Adding support for `or` patterns in type narrowing (complement to `and` patterns)
- Implementing contravariance checks for function types
- Adding tuple unpacking validation for match statements
- Supporting type guards beyond `isinstance` and `is not None`
- Enhancing interface validation to check method signatures (not just names)

### Code Style Conventions

**Naming:**
- Private methods use `_PascalCase` (e.g., `_ExtractNarrowedTypes`)
- Validation methods start with `Validate` (e.g., `ValidateEnumRules`)
- Type checking helpers start with `Is` or `Extract` (e.g., `IsValidAssignmentTarget`)

**Documentation:**
- All public/private methods should have XML doc comments (`/// <summary>`)
- Complex algorithms should have inline comments explaining the logic
- Error messages should be clear and suggest fixes when possible

**Error Handling:**
- Use `AddError()` for semantic errors (don't throw exceptions)
- Return `SemanticType.Unknown` on error to enable error recovery
- Include line/column information in all errors

**Testing:**
- Add test cases in `test/Sharpy.Tests/Semantic/TypeCheckerTests.cs`
- Test both positive cases (valid code) and negative cases (should error)
- For narrowing logic, test complex nested patterns

---

## Cross-References

### Related TypeChecker Files

This file is part of the `TypeChecker` partial class split:

- **[TypeChecker.cs](TypeChecker.md)** - Main orchestration and entry point
- **[TypeChecker.Definitions.cs](TypeChecker.Definitions.md)** - Handles class, function, struct, enum definitions
- **[TypeChecker.Expressions.cs](TypeChecker.Expressions.md)** - Handles all expression node types
- **[TypeChecker.Statements.cs](TypeChecker.Statements.md)** - Handles all statement node types
- **TypeChecker.Utilities.cs** (this file) - Utility methods and validation

All files share the same private fields and state defined in the main TypeChecker.cs file.

### Related Semantic Components

- **[SymbolTable.md](SymbolTable.md)** - Symbol lookup and scope management
- **[Symbol.md](Symbol.md)** - Symbol type definitions (TypeSymbol, FunctionSymbol, etc.)
- **Type system files** - SemanticType hierarchy and type operations

### Language Specification References

Relevant specification documents:
- **`docs/language_specification/type_annotations.md`** - Type annotation syntax and semantics
- **`docs/language_specification/type_narrowing.md`** - Type narrowing rules and patterns
- **`docs/language_specification/type_casting.md`** - Type conversion and casting rules
- **`docs/language_specification/type_hierarchy.md`** - Type system hierarchy and relationships

---

## Summary

`TypeChecker.Utilities.cs` is the **toolbox** for the Sharpy type checker. It provides:

✅ **Type narrowing** - Flow-sensitive typing for conditionals  
✅ **Type compatibility** - Advanced assignability with variance  
✅ **Generic support** - Type parameter substitution  
✅ **Validation** - Constructor overloading, struct initialization, enum rules  
✅ **Interface contracts** - Ensures classes implement all required methods  
✅ **super() semantics** - Complex context-sensitive validation  
✅ **Error management** - Centralized reporting with recovery

**Key insight**: This file implements the **policy** of the type system (what's allowed, what isn't), while other TypeChecker files implement the **mechanism** (traversing AST and applying rules).

When debugging type-related issues, start here to understand the validation logic before diving into the expression/statement checking code.

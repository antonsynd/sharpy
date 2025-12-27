# Walkthrough: AccessValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/AccessValidator.cs`

---

## Overview

`AccessValidator` is a semantic analysis component responsible for enforcing Python-style access control rules in Sharpy code. It validates whether member accesses (fields and methods) are permitted based on naming conventions:

- **Private members** (`__name`): Only accessible within the same class
- **Protected members** (`_name`): Only accessible within the class hierarchy (class, subclasses, and superclasses)
- **Public members** (no underscore prefix): Accessible everywhere

This validator is called during type checking when the compiler encounters member access expressions like `obj.field` or `obj.method()`. It ensures that access violations are caught at compile-time, providing clear error messages about what went wrong and where.

**Key Insight**: Unlike C# which uses explicit `private`, `protected`, and `public` keywords, Sharpy follows Python's convention of using underscore prefixes to indicate access levels. This validator bridges that semantic gap by analyzing naming patterns.

---

## Class Structure

### AccessValidator

The main class contains:

```csharp
public class AccessValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    
    private TypeSymbol? _currentClass = null;
}
```

**Field Breakdown**:
- `_symbolTable`: Reference to the global symbol table (currently not heavily used, but available for future enhancements)
- `_semanticInfo`: Semantic information about the program (also available for extension)
- `_logger`: For diagnostic logging during validation
- `_errors`: Accumulates all access violation errors found during validation
- `_currentClass`: Tracks which class context we're currently validating within (crucial for determining if access is legal)

**Why track current class?** Access rules depend on *where* the access is happening. For example, `self.__private_field` is legal inside `MyClass`, but `obj.__private_field` from outside is illegal.

---

## Key Methods

### Constructor

```csharp
public AccessValidator(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
```

**Purpose**: Initialize the validator with necessary dependencies.

**Parameters**:
- `symbolTable`: The program's symbol table
- `semanticInfo`: Semantic information store
- `logger`: Optional logger (defaults to `NullLogger.Instance` if not provided)

**Design Note**: The nullable logger with default is a common pattern for optional logging without forcing callers to pass a logger.

---

### EnterClass / ExitClass

```csharp
public void EnterClass(TypeSymbol classSymbol)
public void ExitClass()
```

**Purpose**: Manage the current class context during traversal.

**Usage Pattern**:
```csharp
// In TypeChecker when visiting a class definition:
_accessValidator.EnterClass(classSymbol);
// ... validate members ...
_accessValidator.ExitClass();
```

**Why this matters**: The validator needs to know what class context code is executing in. When checking `obj.__private_member`, it needs to know if we're inside that same class (legal) or outside (illegal).

**Implementation Detail**: These are simple setters, but they establish the context for all subsequent `ValidateMemberAccess` calls.

---

### ValidateMemberAccess (Core Method)

```csharp
public void ValidateMemberAccess(string memberName, TypeSymbol owningType, int? lineStart, int? columnStart)
```

**Purpose**: The workhorse method that validates a single member access.

**Algorithm**:

1. **Determine access level** from the member name using naming conventions
2. **Apply access rules**:
   - **Private** (`__name`): Must be accessed from the same class (`_currentClass == owningType`)
   - **Protected** (`_name`): Must be accessed from within the class hierarchy
   - **Public**: Always allowed

**Parameters**:
- `memberName`: The name of the field/method being accessed (e.g., `"__private_field"`)
- `owningType`: The class that owns/defines this member
- `lineStart`/`columnStart`: Source location for error reporting

**Example Scenarios**:

```python
# Scenario 1: Private access from outside (ILLEGAL)
class MyClass:
    def __init__(self):
        self.__secret = 42

obj = MyClass()
print(obj.__secret)  # ERROR: Cannot access private member

# Scenario 2: Protected access from subclass (LEGAL)
class Base:
    def __init__(self):
        self._protected = 10

class Derived(Base):
    def show(self):
        print(self._protected)  # OK: In class hierarchy

# Scenario 3: Public access from anywhere (LEGAL)
class MyClass:
    def __init__(self):
        self.public = 100

obj = MyClass()
print(obj.public)  # OK: Public member
```

---

### ValidateFieldAccess / ValidateMethodAccess

```csharp
public void ValidateFieldAccess(VariableSymbol field, TypeSymbol owningType, int? lineStart, int? columnStart)
public void ValidateMethodAccess(FunctionSymbol method, TypeSymbol owningType, int? lineStart, int? columnStart)
```

**Purpose**: Type-safe convenience wrappers around `ValidateMemberAccess`.

**Why separate methods?** These provide a cleaner API for callers who already have symbol objects. They extract the name and delegate to the core validation logic.

**Usage in TypeChecker**:
```csharp
// When checking a field access:
_accessValidator.ValidateFieldAccess(field, udt.Symbol, memberAccess.LineStart, memberAccess.ColumnStart);

// When checking a method call:
_accessValidator.ValidateMethodAccess(method, udt.Symbol, memberAccess.LineStart, memberAccess.ColumnStart);
```

---

### DetermineAccessLevel (Private Helper)

```csharp
private AccessLevel DetermineAccessLevel(string name)
{
    if (name.StartsWith("__") && !name.EndsWith("__"))
        return AccessLevel.Private;
    
    if (name.StartsWith("_") && !name.StartsWith("__"))
        return AccessLevel.Protected;
    
    return AccessLevel.Public;
}
```

**Purpose**: Infer access level from Python naming conventions.

**Rules**:
1. **Private**: Starts with `__` but doesn't end with `__` (to avoid matching dunder methods like `__init__`)
2. **Protected**: Starts with single `_` (but not `__`)
3. **Public**: Everything else

**Critical Edge Case**: `__init__` and other dunder (double underscore) methods are **public**, not private! The `!name.EndsWith("__")` check handles this.

**Examples**:
- `__private_field` → Private
- `_protected_field` → Protected
- `public_field` → Public
- `__init__` → Public (dunder method)
- `__str__` → Public (dunder method)

---

### IsInHierarchy (Private Helper)

```csharp
private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
```

**Purpose**: Determine if two classes are related in the inheritance hierarchy.

**Algorithm**:

1. **Check if same class**: `currentClass == targetClass` → `true`
2. **Check if currentClass is a subclass of targetClass**:
   - Walk up `currentClass`'s base types
   - If we find `targetClass`, return `true`
3. **Check if currentClass is a superclass of targetClass**:
   - Walk up `targetClass`'s base types
   - If we find `currentClass`, return `true`
4. Otherwise, return `false`

**Why bidirectional?** Protected members are accessible from both subclasses AND superclasses. This is consistent with Python's behavior where inheritance creates a family relationship.

**Example**:
```python
class Animal:
    def __init__(self):
        self._species = "unknown"

class Dog(Animal):
    def show_species(self):
        print(self._species)  # OK: Dog is in Animal's hierarchy

class Cat(Animal):
    def try_access_dog(self, dog: Dog):
        # Both Cat and Dog derive from Animal
        # But they're not in each other's direct hierarchy
        print(dog._species)  # This depends on implementation details
```

**Implementation Note**: The current implementation checks both directions, allowing access between any classes in the same hierarchy tree. This is more permissive than some languages but matches Python's semantics.

---

### AddError (Private Helper)

```csharp
private void AddError(string message, int? line, int? column)
{
    _errors.Add(new SemanticError(message, line, column));
}
```

**Purpose**: Record an access violation error with source location.

Errors are accumulated in the `_errors` list and can be retrieved via the `Errors` property. The caller (typically `TypeChecker`) will check for errors after validation and report them appropriately.

---

## Dependencies

### Internal Dependencies

- **`SymbolTable`**: Symbol table for looking up type information
- **`SemanticInfo`**: Stores semantic analysis results
- **`SemanticError`**: Error representation with source location
- **`TypeSymbol`**: Represents class types with inheritance information (`BaseType` property)
- **`VariableSymbol`**: Represents field symbols
- **`FunctionSymbol`**: Represents method symbols
- **`AccessLevel` enum**: Defines `Public`, `Protected`, `Private`

### External Dependencies

- **`ICompilerLogger`**: Logging interface from `Sharpy.Compiler.Logging`
- **`Sharpy.Compiler.Parser.Ast`**: AST types (imported but not heavily used in this file)

### Integration Points

**Primary caller**: `TypeChecker` (in `TypeChecker.cs`)
- Calls `EnterClass` when entering a class definition
- Calls `ValidateFieldAccess` / `ValidateMethodAccess` when checking member access expressions
- Calls `ExitClass` when leaving a class definition
- Retrieves errors from `Errors` property

---

## Patterns and Design Decisions

### 1. **Visitor-like Context Tracking**

The `EnterClass` / `ExitClass` pattern is reminiscent of the Visitor pattern. It maintains context during tree traversal without requiring the validator to know about AST traversal.

**Why this works**: The `TypeChecker` already traverses the AST. The `AccessValidator` just needs to know "what class am I in right now?" This separation of concerns keeps the code modular.

### 2. **Convention over Keywords**

Sharpy inherits Python's approach of using naming conventions (`_`, `__`) rather than explicit keywords. This validator encodes those conventions as business logic.

**Trade-off**: More flexible (conventions can evolve) but requires runtime checking. C#'s explicit keywords are checked at parse time.

### 3. **Error Accumulation**

Rather than throwing exceptions on the first error, the validator accumulates all errors. This provides better user experience (see all problems at once).

**Pattern**:
```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

The `IReadOnlyList` return type prevents callers from modifying the error list.

### 4. **Nullable Logger with Default**

```csharp
public AccessValidator(..., ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}
```

This pattern allows optional logging without forcing every caller to provide a logger. The Null Object pattern (`NullLogger`) avoids null checks throughout the code.

### 5. **Symbol-based Validation**

The validator works with symbols (`TypeSymbol`, `VariableSymbol`, `FunctionSymbol`) rather than AST nodes. This keeps it decoupled from syntax and focused on semantic meaning.

---

## Debugging Tips

### Common Issues

1. **False positives on dunder methods**
   - **Symptom**: `__init__` or `__str__` reported as private
   - **Check**: Verify `DetermineAccessLevel` properly excludes names ending with `__`
   - **Debug**: Add logging in `DetermineAccessLevel` to see what access level is computed

2. **Incorrect hierarchy checks**
   - **Symptom**: Protected access fails between related classes
   - **Check**: Verify `IsInHierarchy` logic - should be bidirectional
   - **Debug**: Add logging to trace base type walks: `Console.WriteLine($"Checking {currentClass.Name} vs {targetClass.Name}")`

3. **Missing context (NullReferenceException)**
   - **Symptom**: Crash when `_currentClass` is null
   - **Check**: Ensure `EnterClass` was called before validation
   - **Debug**: Add assertion: `Debug.Assert(_currentClass != null, "Must call EnterClass before ValidateMemberAccess")`

4. **Wrong error locations**
   - **Symptom**: Errors reported at wrong line/column
   - **Check**: Ensure caller passes correct `lineStart`/`columnStart` from AST node
   - **Debug**: Print error locations and compare with source file

### Debugging Workflow

**Step 1**: Enable verbose logging
```csharp
var logger = new ConsoleLogger(LogLevel.Trace);
var validator = new AccessValidator(symbolTable, semanticInfo, logger);
```

**Step 2**: Add strategic breakpoints
- In `ValidateMemberAccess` to see each access being checked
- In `IsInHierarchy` to verify hierarchy relationships
- In `AddError` to catch when violations are detected

**Step 3**: Inspect state
- Check `_currentClass` - is it what you expect?
- Check `owningType` - does it match the actual class defining the member?
- Check `memberName` - is the naming convention correct?

**Step 4**: Trace inheritance
```csharp
// Add temporary code in IsInHierarchy:
Console.WriteLine($"Checking hierarchy: {currentClass.Name} vs {targetClass.Name}");
var temp = currentClass.BaseType;
while (temp != null)
{
    Console.WriteLine($"  {currentClass.Name} -> {temp.Name}");
    temp = temp.BaseType;
}
```

---

## Contribution Guidelines

### Types of Contributions

#### 1. **Adding New Access Levels**

If Sharpy needs additional access levels (e.g., package-private):

1. Add to `AccessLevel` enum in `Symbol.cs`
2. Update `DetermineAccessLevel` with new naming convention
3. Add validation logic in `ValidateMemberAccess`
4. Add comprehensive tests

**Example**: Module-level access (visible only within same module)
```csharp
// In DetermineAccessLevel:
if (name.StartsWith("__m_"))
    return AccessLevel.ModulePrivate;

// In ValidateMemberAccess:
case AccessLevel.ModulePrivate:
    if (_currentModule != GetOwningModule(owningType))
        AddError($"Cannot access module-private member...");
    break;
```

#### 2. **Improving Error Messages**

Current errors are functional but could be more helpful:

```csharp
// Current:
"Cannot access private member '__secret' of 'MyClass' from outside the class"

// Enhanced:
"Cannot access private member '__secret' of 'MyClass' from 'OtherClass'. 
Private members are only accessible within the defining class. 
Did you mean to make this protected (rename to '_secret')?"
```

#### 3. **Performance Optimization**

Current implementation does repeated inheritance walks. Potential improvements:

- **Cache hierarchy relationships**: Build a lookup table during type resolution
- **Early exit**: Stop walking inheritance chain when relationship is found
- **Benchmark first**: Use profiler to confirm this is actually a bottleneck

#### 4. **Special Method Handling**

Some methods need special access rules:

- **Properties**: Access level of getter/setter might differ
- **Operator overloads**: Should these follow access rules?
- **Class methods / static methods**: Different scoping semantics?

#### 5. **Cross-Module Access**

Currently focuses on class-based access. Future work might include:

- Module-level access control
- Package visibility
- Friend classes

### Testing Guidelines

When modifying `AccessValidator`, ensure tests cover:

1. **Basic access levels**: Private, protected, public
2. **Edge cases**: Dunder methods, single underscore at end, etc.
3. **Inheritance scenarios**: Direct parent/child, grandparent, sibling classes
4. **Error reporting**: Correct line/column numbers
5. **Context management**: Nested classes, exiting without entering, etc.

**Test structure example**:
```csharp
[Fact]
public void PrivateAccess_FromOutsideClass_ReportsError()
{
    var source = @"
class MyClass:
    def __init__(self):
        self.__private = 42

obj = MyClass()
print(obj.__private)  # Should error
";
    var errors = Compile(source).Errors;
    Assert.Contains(errors, e => e.Message.Contains("private member"));
}
```

### Code Style

- **Follow existing patterns**: Error accumulation, nullable loggers, etc.
- **Keep it simple**: This class has a clear, focused responsibility
- **Document why, not what**: Code shows what; comments explain why
- **Prefer clarity over cleverness**: Readable code > clever optimizations

### Before Submitting

1. **Run all tests**: `dotnet test --filter "FullyQualifiedName~Semantic"`
2. **Format code**: `dotnet format`
3. **Check error messages**: Are they clear and actionable?
4. **Add tests**: Cover new functionality and edge cases
5. **Update documentation**: If behavior changes, update this walkthrough

---

## Related Files

- **`Symbol.cs`**: Defines `AccessLevel` enum, `TypeSymbol`, `VariableSymbol`, `FunctionSymbol`
- **`TypeChecker.cs`**: Primary caller of `AccessValidator`
- **`SemanticError.cs`**: Error representation
- **`SymbolTable.cs`**: Symbol lookup and management

## Further Reading

- **Python's Access Control**: [PEP 8 - Naming Conventions](https://peps.python.org/pep-0008/#naming-conventions)
- **Sharpy Type System Spec**: `docs/specs/type_system.md`
- **Semantic Analysis Architecture**: `docs/architecture/semantic-analyzer-architecture.md`

---

## Quick Reference

**When to use this class**:
- During semantic analysis (after parsing, before code generation)
- When validating member access expressions in TypeChecker

**Key validation rules**:
- `__name` (not ending in `__`) → Private → Same class only
- `_name` (single underscore) → Protected → Class hierarchy only
- Everything else → Public → No restrictions
- `__dunder__` methods → Public (special case)

**Common patterns**:
```csharp
// Setup
validator.EnterClass(classSymbol);

// Validate
validator.ValidateFieldAccess(field, owningType, line, col);
validator.ValidateMethodAccess(method, owningType, line, col);

// Cleanup
validator.ExitClass();

// Check results
if (validator.Errors.Any())
    // Handle errors
```

**Remember**: This validator assumes naming conventions are followed. It doesn't enforce the conventions themselves (that's a separate concern, possibly a linter).

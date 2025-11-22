# Walkthrough: AccessValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/AccessValidator.cs`

---

## 1. Overview

`AccessValidator.cs` is a semantic analysis component responsible for enforcing **access control rules** in Sharpy code. It ensures that private and protected members of classes are only accessed from appropriate contexts, following Python's naming conventions for access levels.

### Role in the Compiler Pipeline

The AccessValidator operates during the **semantic analysis phase**, after the AST has been parsed but before code generation. It works alongside other semantic analyzers (type checker, name resolver) to validate that member access respects visibility rules.

**Key Responsibilities:**
- Validate that private members (`__name`) are only accessed within the same class
- Validate that protected members (`_name`) are only accessible within the class hierarchy
- Allow public members to be accessed from anywhere
- Track the current class context to determine what accesses are valid
- Collect and report access violation errors

### Python-Style Access Conventions

Unlike C# or Java with explicit `private`/`protected` keywords, Sharpy follows Python's naming conventions:
- `__name` (double underscore prefix, no suffix) → **Private**
- `_name` (single underscore prefix) → **Protected**
- `name` (no prefix) → **Public**
- Special methods like `__init__`, `__str__` are **public** (dunder methods with both prefix and suffix)

---

## 2. Class/Type Structure

### Main Class: `AccessValidator`

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

**Fields:**
- **`_symbolTable`**: Reference to the global symbol table containing all type and member definitions
- **`_semanticInfo`**: Semantic analysis metadata (types, bindings, etc.)
- **`_logger`**: Logger for debugging and diagnostics
- **`_errors`**: Accumulates all access violations found during validation
- **`_currentClass`**: Tracks which class context we're currently analyzing (nullable because we might be in module-level code)

### Related Types

The AccessValidator depends on several types from the `Semantic` namespace:

**`AccessLevel` enum** (defined in `Symbol.cs`):
```csharp
public enum AccessLevel
{
    Public,
    Protected,
    Private
}
```

**`TypeSymbol` record** (from `Symbol.cs`):
```csharp
public record TypeSymbol : Symbol
{
    public TypeSymbol? BaseType { get; set; }  // For inheritance hierarchy
    public List<VariableSymbol> Fields { get; init; }
    public List<FunctionSymbol> Methods { get; init; }
    // ... other members
}
```

**`SemanticError` class** (from `SemanticError.cs`):
```csharp
public class SemanticError : Exception
{
    // Represents access violations and other semantic errors
}
```

---

## 3. Key Functions/Methods

### 3.1 Constructor

```csharp
public AccessValidator(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
{
    _symbolTable = symbolTable;
    _semanticInfo = semanticInfo;
    _logger = logger ?? NullLogger.Instance;
}
```

**Purpose:** Initialize the validator with references to the symbol table and semantic information.

**Parameters:**
- `symbolTable`: The global symbol table containing all type and member definitions
- `semanticInfo`: Semantic analysis metadata
- `logger`: Optional logger (defaults to `NullLogger.Instance` if not provided)

**Design Note:** The use of `NullLogger.Instance` is a **Null Object Pattern** that avoids null checks throughout the code.

---

### 3.2 Class Context Management

#### `EnterClass(TypeSymbol classSymbol)`

```csharp
public void EnterClass(TypeSymbol classSymbol)
{
    _currentClass = classSymbol;
}
```

**Purpose:** Establish the current class context when entering a class definition or method body.

**When to call:** Before validating access within a class method or nested code.

**Example usage flow:**
```csharp
// When analyzing: class MyClass: ...
validator.EnterClass(myClassSymbol);
// ... validate member accesses within MyClass
validator.ExitClass();
```

#### `ExitClass()`

```csharp
public void ExitClass()
{
    _currentClass = null;
}
```

**Purpose:** Clear the class context when exiting a class scope.

**Important:** Always call this after finishing validation of a class to avoid context bleed into module-level code.

---

### 3.3 Core Validation Methods

#### `ValidateMemberAccess(string memberName, TypeSymbol owningType, int? lineStart, int? columnStart)`

This is the **core validation logic** that determines whether a member access is legal.

```csharp
public void ValidateMemberAccess(string memberName, TypeSymbol owningType, 
                                  int? lineStart, int? columnStart)
{
    var accessLevel = DetermineAccessLevel(memberName);

    switch (accessLevel)
    {
        case AccessLevel.Private:
            // Private members only accessible within the same class
            if (_currentClass != owningType)
            {
                AddError($"Cannot access private member '{memberName}' of '{owningType.Name}' from outside the class",
                    lineStart, columnStart);
            }
            break;

        case AccessLevel.Protected:
            // Protected members accessible within the class hierarchy
            if (_currentClass == null || !IsInHierarchy(_currentClass, owningType))
            {
                AddError($"Cannot access protected member '{memberName}' of '{owningType.Name}' from outside the class hierarchy",
                    lineStart, columnStart);
            }
            break;

        case AccessLevel.Public:
            // Public members accessible everywhere
            break;
    }
}
```

**How it works:**

1. **Determine access level** from the member name using naming conventions
2. **For private members:** Check that `_currentClass == owningType` (exact match required)
3. **For protected members:** Check that we're either in the same class or in the hierarchy
4. **For public members:** Allow unconditionally

**Key insight:** The validation uses **reference equality** (`_currentClass != owningType`) for TypeSymbol, which works because the symbol table ensures each type has a single canonical TypeSymbol instance.

**Parameters:**
- `memberName`: Name of the member being accessed (e.g., `"__privateField"`, `"_protectedMethod"`)
- `owningType`: The TypeSymbol of the class that owns this member
- `lineStart`, `columnStart`: Source location for error reporting (nullable for generated code)

---

#### `ValidateFieldAccess(VariableSymbol field, TypeSymbol owningType, int? lineStart, int? columnStart)`

```csharp
public void ValidateFieldAccess(VariableSymbol field, TypeSymbol owningType, 
                                 int? lineStart, int? columnStart)
{
    ValidateMemberAccess(field.Name, owningType, lineStart, columnStart);
}
```

**Purpose:** Convenience wrapper for field access validation.

**When called:** During semantic analysis when encountering field access like `obj.__privateField` or `self._protectedField`.

---

#### `ValidateMethodAccess(FunctionSymbol method, TypeSymbol owningType, int? lineStart, int? columnStart)`

```csharp
public void ValidateMethodAccess(FunctionSymbol method, TypeSymbol owningType, 
                                  int? lineStart, int? columnStart)
{
    ValidateMemberAccess(method.Name, owningType, lineStart, columnStart);
}
```

**Purpose:** Convenience wrapper for method access validation.

**When called:** During semantic analysis when encountering method calls like `obj.__privateMethod()` or `self._protectedMethod()`.

---

### 3.4 Helper Methods

#### `DetermineAccessLevel(string name)`

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

**Purpose:** Infer the access level from Python-style naming conventions.

**Logic breakdown:**

1. **Private:** Starts with `__` but doesn't end with `__`
   - `__private_field` → Private ✓
   - `__init__` → Public (dunder method) ✓
   
2. **Protected:** Starts with single `_` (not double `__`)
   - `_protected_method` → Protected ✓
   - `__private_field` → Not protected (it's private) ✓
   
3. **Public:** Everything else
   - `public_field` → Public ✓
   - `__str__` → Public (dunder method) ✓

**Design decision:** The `!name.EndsWith("__")` check ensures that special methods like `__init__`, `__str__`, `__eq__` are treated as public, not private, which aligns with Python semantics.

---

#### `IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)`

This method implements **bidirectional hierarchy checking** for protected member access.

```csharp
private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
{
    // Same class
    if (currentClass == targetClass)
        return true;

    // Check if currentClass is a subclass of targetClass
    var baseType = currentClass.BaseType;
    while (baseType != null)
    {
        if (baseType == targetClass)
            return true;
        baseType = baseType.BaseType;
    }

    // Check if currentClass is a superclass of targetClass
    baseType = targetClass.BaseType;
    while (baseType != null)
    {
        if (baseType == currentClass)
            return true;
        baseType = baseType.BaseType;
    }

    return false;
}
```

**Purpose:** Determine if two classes are in the same inheritance hierarchy.

**Algorithm:**

1. **Direct match:** If both are the same class, return true
2. **Subclass check:** Walk up from `currentClass` to see if `targetClass` is an ancestor
3. **Superclass check:** Walk up from `targetClass` to see if `currentClass` is an ancestor

**Why bidirectional?**

This allows protected members to be accessed both "downward" (base class accessing derived class protected members) and "upward" (derived class accessing base class protected members).

**Example:**
```python
class Animal:
    def _protected_method(self) -> None:
        pass

class Dog(Animal):
    def test(self) -> None:
        self._protected_method()  # OK: Dog is subclass of Animal
        
# Meanwhile in Animal:
class Animal:
    def interact_with_dog(self, dog: Dog) -> None:
        dog._protected_method()  # OK: Animal is superclass of Dog
```

**Potential issue:** The superclass check (third section) might be too permissive compared to typical OOP languages, where a base class can't access protected members of a derived class. This may be a deliberate design decision for Sharpy, or an area for future refinement.

---

#### `AddError(string message, int? line, int? column)`

```csharp
private void AddError(string message, int? line, int? column)
{
    _errors.Add(new SemanticError(message, line, column));
}
```

**Purpose:** Create and record an access violation error.

**Note:** Errors are accumulated in the `_errors` list and can be retrieved via the `Errors` property after validation completes.

---

### 3.5 Public API

#### `Errors` Property

```csharp
public IReadOnlyList<SemanticError> Errors => _errors;
```

**Purpose:** Expose the list of access violations found during validation.

**Design note:** Returns `IReadOnlyList` to prevent external modification while allowing iteration and querying.

---

## 4. Dependencies

### Internal Dependencies

**Direct dependencies:**
- `SymbolTable` - Global symbol table containing type and member definitions
- `SemanticInfo` - Semantic analysis metadata
- `TypeSymbol` - Represents class types with inheritance information
- `VariableSymbol` - Represents fields
- `FunctionSymbol` - Represents methods
- `SemanticError` - Error reporting

**From other namespaces:**
- `Sharpy.Compiler.Logging.ICompilerLogger` - Logging infrastructure
- `Sharpy.Compiler.Parser.Ast` - AST node types (though not directly used in this file)

### How it Fits in the Semantic Analysis Pipeline

The AccessValidator is typically called from:

1. **SemanticAnalyzer** - The main semantic analysis orchestrator
2. **Type checker** - When validating member access expressions
3. **Name resolver** - When resolving attribute accesses

**Typical call flow:**
```
SemanticAnalyzer
    ├─> NameResolver (finds the member)
    ├─> TypeChecker (checks type compatibility)
    └─> AccessValidator (checks access rights)
```

---

## 5. Patterns and Design Decisions

### 5.1 Design Patterns

#### **Visitor Pattern (Implicit)**
While not explicitly implementing the Visitor pattern, the class expects to be called as part of AST traversal, with `EnterClass`/`ExitClass` mirroring visitor entry/exit.

#### **Null Object Pattern**
```csharp
_logger = logger ?? NullLogger.Instance;
```
Avoids null checks throughout the code by using a no-op logger when none is provided.

#### **Error Accumulation**
Rather than throwing exceptions immediately, errors are collected in a list. This allows multiple violations to be reported in a single compilation run.

### 5.2 Architectural Decisions

#### **Name-Based Access Control**
Sharpy follows Python's convention of inferring access levels from naming patterns rather than explicit keywords. This is simpler but less explicit than C#/Java.

**Trade-offs:**
- ✅ More Pythonic, familiar to Python developers
- ✅ No need for additional parser support for access modifiers
- ❌ Easy to make mistakes (forgetting the underscore)
- ❌ No compile-time enforcement of naming conventions

#### **Reference Equality for TypeSymbol**
The code uses `==` to compare TypeSymbols, relying on the symbol table maintaining a single canonical instance per type.

```csharp
if (_currentClass != owningType)  // Reference equality
```

This is efficient but requires careful symbol table management.

#### **Nullable Class Context**
`_currentClass` is nullable because not all code is inside a class (module-level functions exist). This requires null checks when validating protected access.

#### **Bidirectional Hierarchy Checking**
The `IsInHierarchy` method checks both directions (is A a subclass of B? is A a superclass of B?). This is more permissive than traditional OOP and may be intentional for Sharpy's design.

### 5.3 Coding Conventions

- **Null-conditional operators:** Used throughout for optional source locations (`int? lineStart`)
- **Expression-bodied members:** Used for simple properties (`public IReadOnlyList<SemanticError> Errors => _errors;`)
- **Readonly fields:** All dependencies are `readonly` to ensure immutability after construction
- **XML documentation comments:** Comprehensive documentation for public API

---

## 6. Debugging Tips

### 6.1 Common Issues

#### **Issue: False positives for protected access violations**

**Symptom:** Getting errors when accessing protected members from a valid subclass.

**Debug approach:**
1. Add logging in `IsInHierarchy` to trace the hierarchy walk:
   ```csharp
   _logger.Debug($"Checking hierarchy: {currentClass.Name} vs {targetClass.Name}");
   ```
2. Check that `BaseType` is correctly set during type analysis
3. Verify that `_currentClass` is set correctly before validation

#### **Issue: Dunder methods incorrectly marked as private**

**Symptom:** `__init__` or `__str__` showing access violations.

**Debug approach:**
1. Check the logic in `DetermineAccessLevel` - ensure `!name.EndsWith("__")` is working
2. Verify the member name doesn't have extra characters (e.g., whitespace)

#### **Issue: Class context not set correctly**

**Symptom:** Access violations in code that should be valid.

**Debug approach:**
1. Add logging in `EnterClass`/`ExitClass`:
   ```csharp
   _logger.Debug($"Entering class: {classSymbol?.Name ?? "null"}");
   ```
2. Ensure the semantic analyzer is calling `EnterClass` before validating class members
3. Check for missing `ExitClass` calls that might leave context in wrong state

### 6.2 Debugging Workflow

**To trace a specific access validation:**

1. **Enable verbose logging:**
   ```csharp
   var validator = new AccessValidator(symbolTable, semanticInfo, verboseLogger);
   ```

2. **Add breakpoints:**
   - In `ValidateMemberAccess` to see all access checks
   - In `DetermineAccessLevel` to verify name parsing
   - In `IsInHierarchy` to trace hierarchy walks

3. **Inspect state:**
   - Check `_currentClass` - is it what you expect?
   - Check `owningType` - does it match the class you think?
   - Check the member name - any unexpected prefixes/suffixes?

4. **Verify symbol table:**
   - Are TypeSymbols correctly registered?
   - Is inheritance (`BaseType`) correctly set up?
   - Are member symbols correctly associated with their owning types?

### 6.3 Testing Tips

**Unit test structure:**
```csharp
[Fact]
public void TestPrivateMemberAccess_SameClass_ShouldSucceed()
{
    // Arrange
    var classSymbol = new TypeSymbol { Name = "MyClass" };
    var validator = new AccessValidator(symbolTable, semanticInfo);
    validator.EnterClass(classSymbol);
    
    // Act
    validator.ValidateMemberAccess("__private_field", classSymbol, 1, 1);
    
    // Assert
    Assert.Empty(validator.Errors);
}
```

**What to test:**
- Private access from same class (should succeed)
- Private access from different class (should fail)
- Protected access from subclass (should succeed)
- Protected access from unrelated class (should fail)
- Public access from anywhere (should succeed)
- Dunder methods accessibility (should be public)

---

## 7. Contribution Guidelines

### 7.1 Potential Enhancements

#### **1. Support for Friend Classes**
Allow specific classes to access private members of another class (similar to C++ `friend` or C# `InternalsVisibleTo`).

**Where to add:**
- New field in `TypeSymbol`: `List<TypeSymbol> FriendClasses`
- Modify private access check in `ValidateMemberAccess`

#### **2. Property Access Control**
Currently properties are not explicitly handled. Add support for properties with separate getter/setter access levels.

**Where to add:**
- New method: `ValidatePropertyAccess(PropertySymbol, TypeSymbol, bool isGetter)`
- Check `GetterAccess` and `SetterAccess` in `PropertySymbol`

#### **3. Module-Level Private Variables**
Python supports module-level private variables (single underscore prefix). Extend to support module-level access control.

**Where to add:**
- Track `_currentModule` alongside `_currentClass`
- New method: `ValidateModuleMemberAccess`

#### **4. Better Error Messages**
Provide suggestions for fixing access violations.

**Example:**
```
Cannot access private member '__value' of 'MyClass' from outside the class
  Suggestion: Consider making it protected (_value) or add a public getter method
```

#### **5. Access Control for Nested Classes**
Handle access between outer and inner classes.

**Considerations:**
- Should inner classes access outer class private members?
- Should outer classes access inner class private members?

### 7.2 Common Modifications

#### **Adding a new access level:**

1. Add to `AccessLevel` enum in `Symbol.cs`
2. Update `DetermineAccessLevel` to recognize the naming pattern
3. Add case in `ValidateMemberAccess` switch statement
4. Add tests for the new access level
5. Update documentation

#### **Changing naming conventions:**

To modify what patterns map to which access levels, edit `DetermineAccessLevel`:

```csharp
private AccessLevel DetermineAccessLevel(string name)
{
    // Example: Make triple underscore (___) indicate package-private
    if (name.StartsWith("___"))
        return AccessLevel.PackagePrivate;
        
    // ... existing logic
}
```

#### **Adding access violation suggestions:**

Enhance `AddError` to include fix suggestions:

```csharp
private void AddError(string message, int? line, int? column, string? suggestion = null)
{
    var fullMessage = suggestion != null 
        ? $"{message}\n  Suggestion: {suggestion}"
        : message;
    _errors.Add(new SemanticError(fullMessage, line, column));
}
```

### 7.3 Testing Requirements

**When contributing to this file:**

1. **Add tests for new access rules** in `Sharpy.Compiler.Tests/Semantic/AccessValidatorTests.cs`
2. **Test edge cases:**
   - Empty strings
   - Unicode characters in names
   - Extremely deep inheritance hierarchies
3. **Test integration** with the semantic analyzer
4. **Update documentation** if changing naming conventions or access rules

### 7.4 Code Review Checklist

When reviewing changes to `AccessValidator.cs`:

- [ ] Are all new access rules tested?
- [ ] Do naming convention changes maintain Python compatibility?
- [ ] Are error messages clear and actionable?
- [ ] Is the class context (`_currentClass`) correctly managed?
- [ ] Are nullable types handled correctly?
- [ ] Is logging added for debugging complex logic?
- [ ] Are performance implications considered (especially in `IsInHierarchy`)?
- [ ] Is the change documented in this walkthrough?

---

## 8. Related Files

**To fully understand access validation, also review:**

- **`Symbol.cs`** - TypeSymbol, AccessLevel enum, symbol hierarchy
- **`SymbolTable.cs`** - How symbols are stored and resolved
- **`SemanticAnalyzer.cs`** - How AccessValidator fits into semantic analysis
- **`SemanticError.cs`** - Error reporting infrastructure
- **`TypeChecker.cs`** - Type validation that works alongside access validation

**Test files:**
- **`Sharpy.Compiler.Tests/Semantic/AccessValidatorTests.cs`** - Unit tests for this component

**Documentation:**
- **`docs/specs/language_reference.md`** - Language specification for access modifiers
- **`docs/manual/classes.md`** - User-facing documentation on access control

---

## 9. Quick Reference

### Access Level Summary

| Pattern | Access Level | Example | Accessible From |
|---------|-------------|---------|-----------------|
| `name` | Public | `public_field` | Anywhere |
| `_name` | Protected | `_protected_method` | Same class + hierarchy |
| `__name` | Private | `__private_field` | Same class only |
| `__name__` | Public | `__init__`, `__str__` | Anywhere (special methods) |

### Method Call Flow

```
1. EnterClass(currentClass)
2. For each member access:
   - ValidateFieldAccess() or ValidateMethodAccess()
     └─> ValidateMemberAccess()
         ├─> DetermineAccessLevel()
         └─> IsInHierarchy() (if protected)
3. ExitClass()
4. Check validator.Errors
```

### Common Error Messages

```
"Cannot access private member '__field' of 'MyClass' from outside the class"
"Cannot access protected member '_method' of 'MyClass' from outside the class hierarchy"
```

---

**Last Updated:** November 21, 2024  
**Maintainer:** Sharpy Compiler Team  
**Questions?** Check `docs/specs/language_reference.md` or ask in #sharpy-dev

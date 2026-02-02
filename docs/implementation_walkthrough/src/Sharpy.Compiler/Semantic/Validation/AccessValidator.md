# Walkthrough: AccessValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/AccessValidator.cs`

---

## Overview

`AccessValidator` is a semantic validation pass that enforces Sharpy's access control rules based on Python-style naming conventions:

- **Private members** (`__name`): Only accessible within the defining class
- **Protected members** (`_name`): Accessible within the class hierarchy (class and its subclasses/superclasses)
- **Public members** (`name`): Accessible everywhere

This validator runs as part of the **ValidationPipeline** after the AST is parsed and types are resolved. It performs a **post-pass traversal** over the entire AST, making it modular and pipeline-friendly.

**Position in Pipeline**: Runs at Order 450 (after control flow validation at 400, before code generation)

---

## Class Structure

### AccessValidator

```csharp
public class AccessValidator : SemanticValidatorBase
```

Inherits from `SemanticValidatorBase` which provides:
- The `ISemanticValidator` interface implementation
- Convenience methods: `AddError()`, `AddWarning()`

**Key Properties:**

| Property | Type | Purpose |
|----------|------|---------|
| `Name` | `string` | Identifier for logging: `"AccessValidator"` |
| `Order` | `int` | Pipeline position: `450` |
| `_logger` | `ICompilerLogger` | For debug logging |
| `_context` | `SemanticContext` | Shared semantic data (symbol table, type info) |
| `_currentClass` | `TypeSymbol?` | Tracks which class context we're validating in |

---

## Key Methods

### Entry Point: Validate()

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by the ValidationPipeline

**What it does**:
1. Stores the `SemanticContext` and logger for later use
2. Iterates through all top-level statements in the module
3. Dispatches each statement to `ValidateTopLevelStatement()`

**Upstream**: Called by `ValidationPipeline` after type checking completes  
**Downstream**: Triggers recursive AST traversal

---

### AST Traversal Methods

The validator uses a **recursive descent pattern** to traverse the entire AST tree, similar to the Parser's structure.

#### ValidateTopLevelStatement()

```csharp
private void ValidateTopLevelStatement(Statement stmt)
```

Handles statements at module scope:
- **Functions**: `ValidateFunction()`
- **Classes**: `ValidateClass()` - Sets `_currentClass` context
- **Structs**: `ValidateStruct()` - Sets `_currentClass` context
- **Expressions, Assignments, Variables**: Extracts and validates expressions

**Key Pattern**: Class/struct definitions establish the "current class context" before validating their bodies.

---

#### ValidateClass() and ValidateStruct()

```csharp
private void ValidateClass(ClassDef classDef)
private void ValidateStruct(StructDef structDef)
```

**Critical behavior**:
1. **Look up the type symbol** from the symbol table
2. **Set `_currentClass`** to this symbol
3. Validate all members inside the class/struct body
4. **Restore `_currentClass` to null** after validation

This context tracking is essential for determining whether a member access is valid. Without knowing the "current class", the validator can't determine if we're inside the class hierarchy.

```csharp
var classSymbol = _context.SymbolTable.LookupType(classDef.Name);
_currentClass = classSymbol;  // ← Context tracking
// ... validate members ...
_currentClass = null;  // ← Cleanup
```

---

#### ValidateStatement()

```csharp
private void ValidateStatement(Statement stmt)
```

Handles all statement types using a switch expression:
- **Function definitions**: Recurse into function body
- **Control flow** (if/while/for/try): Validate test expressions and body statements
- **Assignments**: Validate both target and value expressions
- **Return/raise/assert**: Validate their expression arguments

**Pattern**: Every statement type that contains expressions or sub-statements gets recursively validated.

---

#### ValidateExpression()

```csharp
private void ValidateExpression(Expression expr)
```

The workhorse method that handles all expression types:

| Expression Type | What Gets Validated |
|----------------|---------------------|
| `MemberAccess` | **Main validation logic** - checks access rules |
| `FunctionCall` | Function expression + all arguments |
| `BinaryOp`/`UnaryOp` | Operands recursively |
| `IndexAccess` | Object and index expressions |
| Literals (list/dict/set/tuple) | All elements/entries |
| Comprehensions | Element expression + all clauses |
| `ConditionalExpression` | Test, then, and else branches |

**Why so comprehensive?** Access violations can occur at any expression level:

```python
# All of these need checking:
obj._protected_field              # Direct member access
func(obj._protected_field)        # In function argument
[x._private for x in items]       # In list comprehension
result if obj._protected else 0   # In conditional expression
```

---

### Core Validation Logic

#### ValidateMemberAccess() (overload 1)

```csharp
private void ValidateMemberAccess(MemberAccess memberAccess)
```

**Purpose**: Extract the type information for a member access expression

**Algorithm**:
1. Get the **type of the object** being accessed (from `SemanticInfo`)
2. If it's a `UserDefinedType`, extract its `TypeSymbol`
3. Call the validation logic with the member name and owning type

**Type Resolution**: Uses `_context.SemanticInfo.GetExpressionType()` to look up type information that was computed during type checking.

```csharp
var objectType = _context.SemanticInfo.GetExpressionType(memberAccess.Object);
// For: user.name
//      ^^^^          ← Get type of this
//           ^^^^     ← We want to check access to this member
```

---

#### ValidateMemberAccess() (overload 2)

```csharp
private void ValidateMemberAccess(string memberName, TypeSymbol owningType, 
                                   int? lineStart, int? columnStart)
```

**Purpose**: The actual access control enforcement logic

**Algorithm**:
1. **Determine access level** from the member name
2. Apply access rules based on level:
   - **Private**: Only if `_currentClass == owningType`
   - **Protected**: Only if `_currentClass` is in the hierarchy of `owningType`
   - **Public**: Always allowed
3. Add diagnostic error if access is invalid

**Key Insight**: The `_currentClass` field (set during class/struct traversal) determines the "caller's context", while `owningType` is the "member's defining type".

---

#### DetermineAccessLevel()

```csharp
private AccessLevel DetermineAccessLevel(string name)
```

**Purpose**: Infer access level from Python naming conventions

**Rules**:
```csharp
if (name.StartsWith("__") && !name.EndsWith("__"))
    return AccessLevel.Private;      // __private_member

if (name.StartsWith("_") && !name.StartsWith("__"))
    return AccessLevel.Protected;    // _protected_member

return AccessLevel.Public;           // public_member
```

**Special case**: Dunder methods (e.g., `__init__`, `__str__`) are NOT private because they end with `__`.

---

#### IsInHierarchy()

```csharp
private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
```

**Purpose**: Check if two classes are in the same inheritance hierarchy

**Algorithm**:
1. **Same class**: Return true immediately
2. **Check if current is subclass of target**: Walk up `currentClass.BaseType` chain
3. **Check if current is superclass of target**: Walk up `targetClass.BaseType` chain
4. Return false if no relationship found

**Why bidirectional?** Protected members are accessible both:
- From derived classes accessing base class protected members
- From base classes accessing derived class protected members (within base class context)

```python
class Base:
    def __init__(self):
        self._protected = 42

class Derived(Base):
    def __init__(self):
        super().__init__()
        print(self._protected)  # ✅ Derived accessing Base._protected

class Base:
    def check(self, obj: Derived):
        return obj._protected  # ✅ Base accessing Derived._protected (if Derived defines it)
```

---

## Dependencies

### Internal Dependencies

| Dependency | Usage |
|------------|-------|
| `Parser.Ast.*` | All AST node types (Module, ClassDef, MemberAccess, etc.) |
| `SemanticContext` | Access to SymbolTable, SemanticInfo, Diagnostics |
| `TypeSymbol` | Representing class/struct types for hierarchy checks |
| `AccessLevel` enum | Defined in `Semantic/Symbol.cs` |
| `SemanticValidatorBase` | Base class providing error reporting |
| `ICompilerLogger` | Debug logging |

### Related Validators

- **OperatorValidator**: Validates operator usage
- **ProtocolValidator**: Validates protocol implementations
- **ControlFlowValidatorV3**: Validates control flow (runs before this)
- **SignatureValidator**: Validates function signatures

All validators are orchestrated by `ValidationPipeline`.

---

## Patterns and Design Decisions

### 1. Visitor Pattern (Without the Interface)

The validator implements a manual visitor pattern through recursive `Validate*()` methods. This is simpler than implementing a full `IVisitor<T>` interface and avoids the double-dispatch overhead.

**Advantage**: Easy to understand and extend  
**Trade-off**: No compile-time guarantee that all node types are handled

---

### 2. Context Tracking with _currentClass

The `_currentClass` field is the **validator's state** during traversal:

```csharp
ValidateClass(ClassDef classDef) {
    _currentClass = classSymbol;  // ← Enter class context
    // ... validate members ...
    _currentClass = null;          // ← Exit class context
}
```

**Why necessary?** Access validation needs to know:
- **Where we are**: What class is the access happening in? (`_currentClass`)
- **What we're accessing**: What class owns the member? (`owningType`)

Without this, we can't distinguish:
```python
class MyClass:
    def __init__(self):
        self.__private = 42        # ✅ OK - inside MyClass
        
def external_func():
    obj = MyClass()
    print(obj.__private)           # ❌ Error - outside MyClass
```

---

### 3. Pipeline-Based Design

This validator uses a **pipeline-based validation** approach with a separate post-pass after type checking:

**Benefits**:
- **Separation of concerns**: Type checking and access validation are independent
- **Pipeline composability**: Can enable/disable validators independently
- **Future-ready**: Supports incremental compilation for LSP

---

### 4. Defensive Null Checking

```csharp
if (objectType == null) return;
if (owningType == null) return;
```

The validator gracefully handles missing type information. This is important because:
- Earlier validation passes might have failed
- Type information might be incomplete for error recovery
- We don't want to crash on malformed code

**Philosophy**: Report errors when confident, skip when uncertain.

---

## Debugging Tips

### 1. Enable Debug Logging

```csharp
_logger.LogDebug("Starting access validation");
```

Add more logging to track validator state:
```csharp
_logger.LogDebug($"Entering class: {classDef.Name}");
_logger.LogDebug($"Checking access to {memberName} from {_currentClass?.Name ?? "global"}");
```

---

### 2. Check _currentClass State

If access validation is incorrectly allowing/denying access:
1. **Verify `_currentClass` is set correctly**: Add breakpoint in `ValidateClass()`
2. **Check it's cleared properly**: Ensure `_currentClass = null` after class validation
3. **Verify nested classes work**: `_currentClass` should be the innermost class

---

### 3. Verify Type Resolution

If member access isn't being validated:
```csharp
var objectType = _context.SemanticInfo.GetExpressionType(memberAccess.Object);
// ← Add breakpoint here
```

**Possible issues**:
- Type checking failed earlier (check diagnostics)
- Expression type not recorded in SemanticInfo
- Type is not a UserDefinedType (e.g., built-in type)

---

### 4. Test Access Level Determination

```csharp
var accessLevel = DetermineAccessLevel(memberName);
// ← Verify this returns the expected level
```

**Common confusion**:
- `__init__` is PUBLIC (ends with `__`)
- `_init` is PROTECTED (starts with single `_`)
- `__private_method` is PRIVATE (starts with `__`, doesn't end with `__`)

---

### 5. Inspect Hierarchy Checking

```csharp
bool inHierarchy = IsInHierarchy(_currentClass, owningType);
// ← Check if hierarchy detection works
```

**Possible issues**:
- BaseType not set correctly during semantic analysis
- Circular inheritance (should be caught earlier, but could cause infinite loop)
- Interface inheritance not handled (current code only checks BaseType)

---

### 6. Use Integration Tests

File-based integration tests are your friend:

```
TestFixtures/access_control/
├── private_access.spy         # Test private member access
├── private_access.error       # Expected error message
├── protected_hierarchy.spy    # Test protected in inheritance
└── protected_hierarchy.expected
```

Run with:
```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

---

## Contribution Guidelines

### When to Modify This File

1. **New access level**: Add a new naming convention (e.g., `_internal` for module-level access)
2. **Enhanced hierarchy checks**: Handle interfaces, multiple inheritance, mixins
3. **Access control refinements**: Different rules for properties vs. methods
4. **Error message improvements**: More helpful diagnostics
5. **Performance optimization**: Cache hierarchy lookups

### What NOT to Change

❌ **Don't make access checking happen during type checking** - Keep the pipeline separation  
❌ **Don't change the naming conventions** - They follow Python standards  
❌ **Don't skip validation for built-in types** - The early returns are intentional  
❌ **Don't add state that persists between `Validate()` calls** - Validators should be stateless between modules

---

### Adding New Access Control Features

**Example: Module-level internal access**

1. **Add to AccessLevel enum** (in `Symbol.cs`):
```csharp
public enum AccessLevel { Public, Protected, Private, Internal }
```

2. **Update `DetermineAccessLevel()`**:
```csharp
if (name.StartsWith("_internal_"))
    return AccessLevel.Internal;
```

3. **Add validation logic**:
```csharp
case AccessLevel.Internal:
    // Check if in same module
    if (_currentModule != owningModule) {
        AddError(_context, $"Cannot access internal member...", ...);
    }
    break;
```

4. **Add tests**:
```python
# TestFixtures/access_control/internal_access.spy
# module_a.spy
class ClassA:
    def __init__(self):
        self._internal_member = 42

# module_b.spy
from module_a import ClassA
obj = ClassA()
print(obj._internal_member)  # Should error
```

---

### Testing Access Validation

**Unit test template**:
```csharp
[Fact]
public void TestPrivateAccess_SameClass_Allowed()
{
    var result = CompileAndExecute(@"
        class MyClass:
            def __init__(self):
                self.__private = 42
                print(self.__private)  # ✅ Should work
        
        obj = MyClass()
    ");
    Assert.True(result.Success);
    Assert.Equal("42\n", result.StandardOutput);
}

[Fact]
public void TestPrivateAccess_ExternalClass_Denied()
{
    var result = CompileAndExecute(@"
        class MyClass:
            def __init__(self):
                self.__private = 42
        
        obj = MyClass()
        print(obj.__private)  # ❌ Should fail
    ");
    Assert.False(result.Success);
    Assert.Contains("Cannot access private member", result.Errors);
}
```

---

## Cross-References

### Related Files in Validation Pipeline

- **`ISemanticValidator.cs`**: Interface this validator implements
- **`SemanticValidatorBase.cs`**: Base class providing error reporting (inline in `ISemanticValidator.cs`)
- **`ValidationPipeline.cs`**: Orchestrates all validators including this one
- **`OperatorValidator.cs`**: Validates operator usage (sibling validator)
- **`ProtocolValidator.cs`**: Validates protocol implementations (sibling validator)
- **`ControlFlowValidatorV3.cs`**: Runs before this (Order 400)

### Related Semantic Components

- **`Symbol.cs`**: Defines `TypeSymbol`, `AccessLevel` enum
- **`SemanticInfo.cs`**: Stores expression type information
- **`SemanticContext.cs`**: Shared context object passed to validators
- **`SymbolTable.cs`**: Lookup mechanism for types and symbols

### AST Nodes Used

- **`Module`**: Top-level container
- **`ClassDef`, `StructDef`**: User-defined types
- **`MemberAccess`**: `obj.member` expressions (primary validation target)
- **All statement/expression types**: For complete traversal

### Testing

- **`Sharpy.Compiler.Tests/Integration/`**: Integration test base classes
- **`TestFixtures/access_control/`**: File-based access control tests
- **`Sharpy.Compiler.Tests/Semantic/`**: Unit tests for semantic analysis

---

## Summary

`AccessValidator` is a focused, single-responsibility validator that enforces Sharpy's access control rules:

✅ **Clean separation**: Post-pass validation, not mixed with type checking  
✅ **Context-aware**: Tracks current class for accurate hierarchy checks  
✅ **Comprehensive**: Validates every expression type that could contain member access  
✅ **Python-idiomatic**: Uses naming conventions (`_`, `__`) to determine access levels  
✅ **Pipeline-friendly**: Integrates cleanly with the ValidationPipeline architecture  

**Key takeaway**: Access validation requires two pieces of information:
1. **Where are we?** (`_currentClass` - the calling context)
2. **What are we accessing?** (`owningType` - the member's defining type)

The validator ensures these are in a valid relationship (same class, hierarchy, or public) before allowing the access.

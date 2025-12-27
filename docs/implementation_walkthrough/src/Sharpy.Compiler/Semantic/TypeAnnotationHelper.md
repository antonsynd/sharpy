# Walkthrough: TypeAnnotationHelper.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeAnnotationHelper.cs`

---

## 1. Overview

### What This File Does

`TypeAnnotationHelper` is a **utility class** that converts AST type annotations into human-readable string representations. It's essentially a "pretty printer" for types, used primarily in **error messages** and **validation diagnostics**.

**Example transformations:**
- `int` → `"int"`
- `list[str]` → `"list[str]"`
- `dict[str, int]?` → `"dict[str, int]?"`
- `list[dict[str, list[int]]]` → `"list[dict[str, list[int]]]"`

### Role in the Compiler Pipeline

This helper sits in the **Semantic Analysis** phase, specifically used by:

1. **`OperatorSignatureValidator`** - When validating operator method signatures (e.g., `__add__`, `__eq__`)
2. **`ProtocolSignatureValidator`** - When validating protocol implementations (e.g., `IAddable`, `IEquatable`)

**Pipeline Position:**
```
Source Code (.spy)
    ↓
Lexer → Tokens
    ↓
Parser → AST (including TypeAnnotation nodes)
    ↓
Semantic Analysis
    ├─ NameResolver
    ├─ TypeResolver
    ├─ TypeChecker
    └─ Validators ← TypeAnnotationHelper is used HERE
        ├─ OperatorSignatureValidator
        └─ ProtocolSignatureValidator
    ↓
Code Generation
```

### Why It Exists

**Problem:** The AST represents types as structured data (a `TypeAnnotation` record with fields), but validation errors need to show types as strings that developers can read.

**Solution:** A single, shared utility that converts `TypeAnnotation` → `string` consistently across all validators.

**Design Decision:** This was extracted into a separate helper to avoid code duplication between `OperatorSignatureValidator` and `ProtocolSignatureValidator`, both of which need identical type-to-string conversion logic.

---

## 2. Class Structure

### Class Declaration

```csharp
public static class TypeAnnotationHelper
```

**Key characteristics:**
- **`static class`** - Cannot be instantiated; all members must be static
- **`public`** - Available throughout the compiler
- **No state** - Purely functional; no fields, no mutation

This is a classic **utility class** pattern in C#.

---

## 3. Key Method: `GetName()`

### Method Signature

```csharp
public static string GetName(TypeAnnotation? typeAnnotation)
```

**Parameters:**
- `typeAnnotation` (`TypeAnnotation?`) - The type annotation to convert, **or null** for `void` types

**Returns:**
- `string` - Human-readable representation of the type

**Thread-safety:** ✅ Yes (pure function, no shared state)

---

### Understanding TypeAnnotation Structure

Before diving into the implementation, you need to understand what a `TypeAnnotation` looks like (from `Parser/Ast/Types.cs`):

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";                    // e.g., "int", "list", "dict"
    public List<TypeAnnotation> TypeArguments { get; init; } = new();  // Recursive!
    public bool IsNullable { get; init; }                      // T? syntax
    
    // Source location (for error messages)
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Key insight:** `TypeAnnotation` is **recursive** - a generic type like `list[int]` has:
- `Name = "list"`
- `TypeArguments = [TypeAnnotation { Name = "int" }]`

For nested generics like `dict[str, list[int]]`:
- `Name = "dict"`
- `TypeArguments = [
    TypeAnnotation { Name = "str" },
    TypeAnnotation { Name = "list", TypeArguments = [TypeAnnotation { Name = "int" }] }
  ]`

---

### Implementation Walkthrough

Let's analyze the method line by line:

```csharp
public static string GetName(TypeAnnotation? typeAnnotation)
{
    // Step 1: Handle null case (void types)
    if (typeAnnotation == null)
        return "void";
```

**Why null check?**
- Function return types can be `void` (no return value)
- In the AST, `void` is represented as `null` in the `ReturnType` field
- Example: `def print(x: int) -> None:` (where `None`/`void` are equivalent)

---

```csharp
    // Step 2: Build the base name (handling generics)
    var baseName = typeAnnotation.TypeArguments.Count > 0
        ? $"{typeAnnotation.Name}[{string.Join(", ", typeAnnotation.TypeArguments.Select(GetName))}]"
        : typeAnnotation.Name;
```

**What's happening here:**

1. **Check if generic:** `typeAnnotation.TypeArguments.Count > 0`
   - If empty → simple type like `int`
   - If has arguments → generic type like `list[int]`

2. **Simple type branch:** `typeAnnotation.Name`
   - Just return the name directly
   - Example: `int` → `"int"`

3. **Generic type branch:** `$"{typeAnnotation.Name}[...]"`
   - Use string interpolation to build `"list[...]"`
   - **Recursive call:** `typeAnnotation.TypeArguments.Select(GetName)`
     - This calls `GetName()` on **each** type argument
     - For `list[int]`, calls `GetName()` on the `int` TypeAnnotation
     - For `dict[str, int]`, calls `GetName()` on both `str` and `int`
   - **Join with commas:** `string.Join(", ", ...)`
     - Produces `"str, int"` from the recursive calls
   - **Result:** `"dict[str, int]"`

**Why recursion works:**
- Base case: Simple types (no type arguments) just return their name
- Recursive case: Generic types recursively convert their arguments
- Natural fit for tree-structured data (AST nodes)

---

```csharp
    // Step 3: Add nullable suffix if needed
    return typeAnnotation.IsNullable ? $"{baseName}?" : baseName;
}
```

**Final step:**
- Check `IsNullable` flag
- If true, append `?` to the name
- If false, return name as-is

**Examples:**
- `int?` → `IsNullable = true` → `"int?"`
- `list[str]?` → `IsNullable = true` → `"list[str]?"`
- `int` → `IsNullable = false` → `"int"`

---

### Complete Examples

Let's trace through some examples to solidify understanding:

#### Example 1: Simple Type (`int`)

```csharp
var type = new TypeAnnotation { Name = "int" };
GetName(type);
```

**Execution:**
1. `typeAnnotation == null`? No, skip
2. `TypeArguments.Count > 0`? No (count is 0)
3. `baseName = "int"`
4. `IsNullable`? No
5. Return `"int"`

---

#### Example 2: Generic Type (`list[str]`)

```csharp
var type = new TypeAnnotation 
{ 
    Name = "list",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" }
    }
};
GetName(type);
```

**Execution:**
1. `typeAnnotation == null`? No
2. `TypeArguments.Count > 0`? Yes (count is 1)
3. Build generic string:
   - `typeAnnotation.Name` = `"list"`
   - Recursive call: `GetName(TypeAnnotation { Name = "str" })`
     - Returns `"str"`
   - `string.Join(", ", ["str"])` = `"str"`
   - Result: `"list[str]"`
4. `baseName = "list[str]"`
5. `IsNullable`? No
6. Return `"list[str]"`

---

#### Example 3: Nullable Generic (`dict[str, int]?`)

```csharp
var type = new TypeAnnotation 
{ 
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    },
    IsNullable = true
};
GetName(type);
```

**Execution:**
1. `typeAnnotation == null`? No
2. `TypeArguments.Count > 0`? Yes (count is 2)
3. Build generic string:
   - `typeAnnotation.Name` = `"dict"`
   - Recursive calls:
     - `GetName(TypeAnnotation { Name = "str" })` → `"str"`
     - `GetName(TypeAnnotation { Name = "int" })` → `"int"`
   - `string.Join(", ", ["str", "int"])` = `"str, int"`
   - Result: `"dict[str, int]"`
4. `baseName = "dict[str, int]"`
5. `IsNullable`? **Yes**
6. Return `"dict[str, int]?"`

---

#### Example 4: Nested Generics (`list[dict[str, int]]`)

```csharp
var type = new TypeAnnotation 
{ 
    Name = "list",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation 
        { 
            Name = "dict",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation { Name = "int" }
            }
        }
    }
};
GetName(type);
```

**Execution (with recursion depth):**

**Level 1:**
1. `TypeArguments.Count > 0`? Yes
2. Recursive call on `dict` type argument:

**Level 2 (inside recursive call):**
3. `TypeArguments.Count > 0`? Yes
4. Recursive calls on `str` and `int`:
   - **Level 3:** `GetName(str)` → `"str"`
   - **Level 3:** `GetName(int)` → `"int"`
5. Join: `"str, int"`
6. Build: `"dict[str, int]"`
7. Return to Level 1 with `"dict[str, int]"`

**Level 1 (continued):**
8. Join: `"dict[str, int]"` (only one argument)
9. Build: `"list[dict[str, int]]"`
10. Return `"list[dict[str, int]]"`

**Key takeaway:** Recursion naturally handles arbitrary nesting depth!

---

## 4. Dependencies

### Direct Dependencies

**Internal (Sharpy.Compiler):**
- `Sharpy.Compiler.Parser.Ast` - Imports the `TypeAnnotation` record definition

**External (.NET):**
- `System.Linq` - Implicit dependency for `Select()` LINQ method

### Dependents (Who Uses This)

**Direct usage in:**
1. **`OperatorSignatureValidator.cs`** (line 129)
   ```csharp
   $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'"
   ```

2. **`ProtocolSignatureValidator.cs`** (line 93)
   ```csharp
   var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);
   ```

**Indirect usage:**
- Any code that generates semantic errors involving type mismatches
- Documentation generation tools (if implemented)
- Debug logging during semantic analysis

---

## 5. Patterns and Design Decisions

### Design Pattern: Utility Class

**Characteristics:**
- Static class with static methods
- No instance state
- Pure functions (no side effects)
- Shared by multiple consumers

**Benefits:**
- **Reusability:** Single source of truth for type-to-string conversion
- **Consistency:** All error messages use the same format
- **Maintainability:** If format needs to change, update in one place
- **Testability:** Easy to unit test (pure function)

---

### Design Pattern: Recursive Descent

The `GetName()` method uses **recursive descent** to traverse the type tree:

```csharp
typeAnnotation.TypeArguments.Select(GetName)
//                             ^^^^^^^ Recursive call!
```

**Why this works:**
- AST types form a tree structure
- Recursion naturally matches tree traversal
- Base case: Simple types (no type arguments)
- Recursive case: Generic types recurse on arguments

**Alternative (iterative) would be:**
- More complex (need explicit stack)
- Less readable
- Same performance for typical type nesting (3-5 levels max)

---

### Design Pattern: Null-Safe API

```csharp
public static string GetName(TypeAnnotation? typeAnnotation)
//                                           ^ Nullable!
```

**Why nullable parameter?**
- Allows callers to pass `null` for `void` types
- Simplifies calling code (no need for null checks before calling)
- Clear semantics: `null` → `"void"`

**Example usage:**
```csharp
// Instead of:
var typeName = funcDef.ReturnType == null ? "void" : TypeAnnotationHelper.GetName(funcDef.ReturnType);

// Can write:
var typeName = TypeAnnotationHelper.GetName(funcDef.ReturnType);
```

---

### Sharpy Design Philosophy

This helper embodies key Sharpy compiler principles:

1. **Immutable AST** - Takes `TypeAnnotation` by reference, never modifies it
2. **Separation of concerns** - Type structure (AST) separated from presentation (string)
3. **Code reuse** - Extracted from duplicated code in validators
4. **Clear error messages** - Produces readable type names for user-facing errors

---

## 6. Debugging Tips

### Common Issues and Solutions

#### Issue 1: Missing Space After Comma

**Symptom:** Type appears as `dict[str,int]` instead of `dict[str, int]`

**Debug:**
```csharp
// Check the join separator
string.Join(", ", typeAnnotation.TypeArguments.Select(GetName))
//          ^^^^ This must include the space!
```

---

#### Issue 2: Nullable Marker in Wrong Place

**Symptom:** Type appears as `list?[int]` instead of `list[int]?`

**Root cause:** Applying nullable suffix before adding type arguments

**Fix:** The current code is correct:
1. Build base name (with type arguments)
2. Then add nullable marker

---

#### Issue 3: Recursive Call Doesn't Work for Nested Generics

**Symptom:** `list[dict[str, int]]` displays as `list[dict]`

**Debug approach:**
1. Check if `TypeArguments` list is populated correctly in the parser
2. Verify `Select(GetName)` is actually making recursive calls
3. Add logging:
   ```csharp
   var baseName = typeAnnotation.TypeArguments.Count > 0
       ? $"{typeAnnotation.Name}[{string.Join(", ", typeAnnotation.TypeArguments.Select(ta => {
           var result = GetName(ta);
           Console.WriteLine($"Converted {ta.Name} -> {result}");
           return result;
       }))}]"
       : typeAnnotation.Name;
   ```

---

#### Issue 4: Null Reference Exception

**Symptom:** `NullReferenceException` when calling `GetName()`

**Possible causes:**
1. `TypeArguments` list is `null` (shouldn't happen with default initialization)
2. Individual type arguments in the list are `null`
3. `Name` property is `null`

**Debug:**
```csharp
if (typeAnnotation == null)
    return "void";

if (typeAnnotation.Name == null)
    throw new InvalidOperationException("TypeAnnotation.Name cannot be null");

if (typeAnnotation.TypeArguments == null)
    throw new InvalidOperationException("TypeAnnotation.TypeArguments cannot be null");
```

---

### Debugging Workflow

When investigating type-related issues:

1. **Start with the error message** - Find where `TypeAnnotationHelper.GetName()` is called
2. **Inspect the TypeAnnotation** - Use debugger to view the full structure
3. **Trace the recursion** - Step through recursive calls for generic types
4. **Check the parser** - If type structure is wrong, bug is likely in `Parser.cs`
5. **Verify in Python** - Check how Python represents the equivalent type

**Example debugging session:**
```csharp
// Set breakpoint here
var typeName = TypeAnnotationHelper.GetName(funcDef.ReturnType);

// In debugger, inspect:
// - funcDef.ReturnType.Name
// - funcDef.ReturnType.TypeArguments (expand to see nested types)
// - funcDef.ReturnType.IsNullable
```

---

## 7. Contribution Guidelines

### Types of Changes You Might Make

#### 1. Add Support for New Type Syntax

**Example:** Sharpy adds union types (`int | str`)

**Required changes:**
1. Update `Parser/Ast/Types.cs` to add `UnionTypes` property:
   ```csharp
   public record TypeAnnotation
   {
       public string Name { get; init; } = "";
       public List<TypeAnnotation> TypeArguments { get; init; } = new();
       public List<TypeAnnotation>? UnionTypes { get; init; }  // NEW
       public bool IsNullable { get; init; }
   }
   ```

2. Update `TypeAnnotationHelper.GetName()`:
   ```csharp
   public static string GetName(TypeAnnotation? typeAnnotation)
   {
       if (typeAnnotation == null)
           return "void";

       // NEW: Handle union types
       if (typeAnnotation.UnionTypes != null && typeAnnotation.UnionTypes.Count > 0)
       {
           var unionStr = string.Join(" | ", typeAnnotation.UnionTypes.Select(GetName));
           return typeAnnotation.IsNullable ? $"({unionStr})?" : unionStr;
       }

       // Existing logic...
       var baseName = typeAnnotation.TypeArguments.Count > 0
           ? $"{typeAnnotation.Name}[{string.Join(", ", typeAnnotation.TypeArguments.Select(GetName))}]"
           : typeAnnotation.Name;

       return typeAnnotation.IsNullable ? $"{baseName}?" : baseName;
   }
   ```

3. Add tests (see section below)

---

#### 2. Improve Error Message Formatting

**Example:** Show type aliases in error messages

**Current:** `"Expected list[int], got list[str]"`
**Improved:** `"Expected MyList (alias for list[int]), got list[str]"`

**Changes needed:**
1. Pass additional context to `GetName()`:
   ```csharp
   public static string GetName(TypeAnnotation? typeAnnotation, SemanticInfo? semanticInfo = null)
   {
       // Check if this type is an alias
       if (semanticInfo != null && typeAnnotation != null)
       {
           var alias = semanticInfo.GetTypeAlias(typeAnnotation);
           if (alias != null)
               return $"{alias} (alias for {GetBaseName(typeAnnotation)})";
       }
       
       // Existing logic...
   }
   
   private static string GetBaseName(TypeAnnotation typeAnnotation) { /* ... */ }
   ```

2. Update all call sites to pass `SemanticInfo` when available

---

#### 3. Add Caching for Performance

**Motivation:** If type-to-string conversion becomes a bottleneck (unlikely but possible)

**Implementation:**
```csharp
public static class TypeAnnotationHelper
{
    private static readonly ConcurrentDictionary<TypeAnnotation, string> _cache = new();

    public static string GetName(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return "void";

        return _cache.GetOrAdd(typeAnnotation, t =>
        {
            // Existing conversion logic...
            var baseName = t.TypeArguments.Count > 0
                ? $"{t.Name}[{string.Join(", ", t.TypeArguments.Select(GetName))}]"
                : t.Name;

            return t.IsNullable ? $"{baseName}?" : baseName;
        });
    }
}
```

**Trade-offs:**
- ✅ Faster for repeated conversions
- ❌ Memory overhead (cache grows indefinitely)
- ❌ Added complexity
- ⚠️ Only worthwhile if profiling shows this is a bottleneck

---

### Testing Your Changes

When modifying `TypeAnnotationHelper.cs`, add tests in `src/Sharpy.Compiler.Tests/Semantic/`:

**Example test class:**
```csharp
// TypeAnnotationHelperTests.cs
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeAnnotationHelperTests
{
    [Fact]
    public void GetName_SimpleType_ReturnsName()
    {
        var type = new TypeAnnotation { Name = "int" };
        Assert.Equal("int", TypeAnnotationHelper.GetName(type));
    }

    [Fact]
    public void GetName_GenericType_ReturnsFormattedString()
    {
        var type = new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" }
            }
        };
        Assert.Equal("list[str]", TypeAnnotationHelper.GetName(type));
    }

    [Fact]
    public void GetName_NullableType_AppendsQuestionMark()
    {
        var type = new TypeAnnotation 
        { 
            Name = "int",
            IsNullable = true
        };
        Assert.Equal("int?", TypeAnnotationHelper.GetName(type));
    }

    [Fact]
    public void GetName_NullAnnotation_ReturnsVoid()
    {
        Assert.Equal("void", TypeAnnotationHelper.GetName(null));
    }

    [Fact]
    public void GetName_NestedGenerics_HandlesRecursion()
    {
        var type = new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation 
                { 
                    Name = "dict",
                    TypeArguments = new List<TypeAnnotation>
                    {
                        new TypeAnnotation { Name = "str" },
                        new TypeAnnotation { Name = "int" }
                    }
                }
            }
        };
        Assert.Equal("list[dict[str, int]]", TypeAnnotationHelper.GetName(type));
    }

    [Fact]
    public void GetName_NullableGeneric_HandlesCorrectly()
    {
        var type = new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            },
            IsNullable = true
        };
        Assert.Equal("list[int]?", TypeAnnotationHelper.GetName(type));
    }
}
```

**Run tests:**
```bash
dotnet test --filter "FullyQualifiedName~TypeAnnotationHelperTests"
```

---

### Code Review Checklist

When submitting changes to this file:

- [ ] Does `GetName()` handle all existing type patterns?
  - [ ] Simple types (`int`, `str`, `bool`)
  - [ ] Generic types (`list[T]`, `dict[K, V]`)
  - [ ] Nested generics (`list[dict[str, int]]`)
  - [ ] Nullable types (`T?`)
  - [ ] Null input (`void`)

- [ ] Are error messages still readable?
  - [ ] Check `OperatorSignatureValidator` error messages
  - [ ] Check `ProtocolSignatureValidator` error messages

- [ ] Have you added tests for new functionality?

- [ ] Does the implementation follow Sharpy conventions?
  - [ ] Immutable approach (no mutation)
  - [ ] Clear, self-documenting code
  - [ ] Minimal complexity

- [ ] Performance considerations:
  - [ ] Is recursion depth bounded? (Yes, by type nesting)
  - [ ] Are there unnecessary allocations? (String interpolation is acceptable here)

---

### Related Files to Study

When working on type-related features, also review:

1. **`Parser/Ast/Types.cs`** - `TypeAnnotation` record definition
2. **`Parser/Parser.cs`** - How types are parsed from source code
3. **`Semantic/TypeResolver.cs`** - Converts `TypeAnnotation` → .NET `Type`
4. **`Semantic/TypeChecker.cs`** - Type compatibility checks
5. **`Semantic/OperatorSignatureValidator.cs`** - Usage example
6. **`Semantic/ProtocolSignatureValidator.cs`** - Usage example
7. **`CodeGen/TypeMapper.cs`** - Maps Sharpy types to C# types

---

## 8. Real-World Examples

### Example 1: Operator Signature Error

**Sharpy code:**
```python
class Point:
    def __eq__(self, other: Point) -> str:  # Should return bool!
        return "equal"
```

**Error message:**
```
Comparison operator method '__eq__' on 'Point' must return 'bool', got 'str'
```

**How TypeAnnotationHelper is used:**
```csharp
// In OperatorSignatureValidator.cs
if (!IsTypeAnnotationBool(returnType))
{
    errors.Add(new SemanticError(
        $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'",
        funcDef.LineStart,
        funcDef.ColumnStart));
}
```

The call to `TypeAnnotationHelper.GetName(returnType)` converts the `TypeAnnotation { Name = "str" }` to the string `"str"` for the error message.

---

### Example 2: Protocol Implementation Error

**Sharpy code:**
```python
class MyList:
    def __len__(self) -> str:  # Should return int!
        return "10"
```

**Error message:**
```
Protocol method '__len__' must return 'int', got 'str'
```

**How TypeAnnotationHelper is used:**
```csharp
// In ProtocolSignatureValidator.cs
var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);

if (!expectedNormalized.Equals(actualNormalized, StringComparison.OrdinalIgnoreCase))
{
    errors.Add(new SemanticError(
        $"Protocol method '{protocol.MethodName}' must return '{protocol.ExpectedReturnType}', got '{actualReturnType}'",
        funcDef.LineStart,
        funcDef.ColumnStart));
}
```

---

### Example 3: Generic Type Mismatch

**Sharpy code:**
```python
class Container:
    def get_items(self) -> list[dict[str, int]]:
        return []
    
    def process(self, items: list[str]) -> None:  # Type mismatch!
        self.get_items()  # Returns list[dict[str, int]], but process expects list[str]
```

**Error message (hypothetical):**
```
Type mismatch: expected 'list[str]', got 'list[dict[str, int]]'
```

**How TypeAnnotationHelper handles this:**
```csharp
// For list[dict[str, int]]
var expectedType = TypeAnnotationHelper.GetName(expectedAnnotation);
// Returns: "list[dict[str, int]]"

var actualType = TypeAnnotationHelper.GetName(actualAnnotation);
// Returns: "list[str]"

// Both types are formatted consistently for comparison
```

---

## 9. Summary

### Key Takeaways

1. **Purpose:** Converts AST `TypeAnnotation` nodes to readable strings for error messages
2. **Pattern:** Static utility class with a single recursive method
3. **Consumers:** Validators that need to display type names in diagnostics
4. **Design:** Simple, focused, and reusable
5. **Recursion:** Naturally handles nested generic types

### When to Use This Helper

✅ **Use `TypeAnnotationHelper.GetName()` when:**
- Generating error messages about types
- Logging type information for debugging
- Displaying types in diagnostics or warnings

❌ **Don't use it for:**
- Type resolution (use `TypeResolver` instead)
- Type checking (use `TypeChecker` instead)
- Code generation (use `TypeMapper` and `RoslynEmitter` instead)

### Mental Model

Think of `TypeAnnotationHelper` as a **translator**:
- **Input:** Structured type data (AST node)
- **Output:** Human-readable string
- **Purpose:** Bridge between compiler internals and user-facing messages

It's a small but crucial piece that makes error messages understandable!

---

## 10. Further Reading

**Documentation:**
- [Type System Overview](../../../docs/internal_walkthrough/src/Sharpy.Compiler/Parser/Ast/Types.md)
- [OperatorSignatureValidator Walkthrough](OperatorSignatureValidator.md)
- [ProtocolSignatureValidator Walkthrough](ProtocolSignatureValidator.md)

**Related source files:**
- `Parser/Ast/Types.cs` - TypeAnnotation definition
- `Semantic/TypeResolver.cs` - Type resolution
- `Semantic/TypeChecker.cs` - Type checking

**External resources:**
- [Python Type Annotations (PEP 484)](https://www.python.org/dev/peps/pep-0484/)
- [C# Record Types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Recursive Descent Parsing](https://en.wikipedia.org/wiki/Recursive_descent_parser)

---

**Last Updated:** 2025-12-27  
**Maintainer:** Sharpy Compiler Team  
**Feedback:** Report issues or suggest improvements in the project repository

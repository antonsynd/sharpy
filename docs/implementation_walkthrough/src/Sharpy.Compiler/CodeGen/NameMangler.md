# Walkthrough: NameMangler.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

---

## 1. Overview

### What Does This File Do?

`NameMangler.cs` is the **naming convention bridge** between Sharpy's Pythonic naming conventions and C#'s .NET naming conventions. When the Sharpy compiler generates C# code, it needs to translate identifiers so they feel natural in both worlds:

- **Sharpy side**: `my_function`, `user_name`, `__init__`, `MAX_SIZE`
- **C# side**: `MyFunction`, `userName`, constructor, `MAX_SIZE`

This file is critical because:
1. It ensures generated C# code follows .NET conventions
2. It handles Python's special "dunder" methods (like `__init__`, `__str__`)
3. It prevents naming collisions with C# keywords
4. It preserves user intent when they use specific casing

### Role in the Compiler Pipeline

```
Sharpy Source → Lexer → Parser → Semantic Analysis → CodeGen
                                                          ↓
                                                    NameMangler ← You are here
                                                          ↓
                                                    RoslynEmitter → C# Code
```

When `RoslynEmitter` generates C# syntax trees, it uses `NameMangler` to transform every identifier appropriately based on its context (is it a type? a method? a variable?).

---

## 2. Class/Type Structure

### `NameMangler` (Static Class)

A pure utility class with no state—all methods are static. This design makes it lightweight and easy to use throughout the code generation phase without worrying about instance management.

### `NameContext` (Enum)

Defines the **context** in which a name appears, which determines how it should be transformed:

```csharp
public enum NameContext
{
    Type,       // Classes, structs, enums → preserve user casing
    Interface,  // Interfaces → preserve user casing
    Method,     // Instance and static methods → PascalCase
    Function,   // Top-level functions → PascalCase
    Variable,   // Local variables → camelCase
    Parameter,  // Function/method parameters → camelCase
    Field,      // Class/struct fields → camelCase
    Constant    // Constants → CAPS_SNAKE_CASE
}
```

**Design Insight**: The enum-based approach makes the API self-documenting and prevents misuse. Rather than having callers remember "use `ToPascalCase()` for methods", they call `Transform(name, NameContext.Method)`.

---

## 3. Key Functions/Methods

### Core Transformation Methods

#### `Transform(string name, NameContext context)` - The Main Entry Point

```csharp
public static string Transform(string name, NameContext context)
{
    return context switch
    {
        NameContext.Type => ToTypeName(name),
        NameContext.Method => ToPascalCase(name),
        NameContext.Variable => ToCamelCase(name),
        // ... etc
    };
}
```

**What it does**: This is the public API most callers use. Pass in a name and its context, get back the transformed name.

**Example usage in RoslynEmitter**:
```csharp
var methodName = NameMangler.Transform(functionDef.Name, NameContext.Method);
// "my_function" → "MyFunction"
```

---

#### `ToPascalCase(string name)` - Snake Case to PascalCase

**Used for**: Methods, functions, properties

**Key algorithm**:
1. Split on underscores: `"user_name"` → `["user", "name"]`
2. Capitalize each part: `["User", "Name"]`
3. Join: `"UserName"`

**Special cases handled**:

1. **Literal names (backtick-escaped)**: 
   ```python
   `AlreadyPascalCase`  # User wants exact name
   ```
   → Strip backticks, return as-is

2. **Dunder methods**:
   ```python
   __init__   → Constructor  (special mapping)
   __str__    → ToString     (maps to C# override)
   __add__    → __Add__      (operator, preserve dunder)
   ```

3. **Already PascalCase**:
   ```python
   MyFunction  # No underscores, starts uppercase
   ```
   → Preserved as-is (don't break it!)

4. **Private names** (single underscore prefix):
   ```python
   _internal_method → _InternalMethod
   ```
   → Preserve underscore prefix

**Why this matters**: Some Sharpy users might write C#-style code, others Python-style. This respects both.

---

#### `ToCamelCase(string name)` - Snake Case to camelCase

**Used for**: Local variables, parameters, fields

**Key algorithm**:
1. Split on underscores: `"user_name"` → `["user", "name"]`
2. Keep first part lowercase: `"user"`
3. Capitalize remaining parts: `"Name"`
4. Join: `"userName"`

**Example transformations**:
```python
user_name      → userName
total_count    → totalCount
_private_var   → _privateVar  (preserve prefix)
```

---

#### `ToTypeName(string name)` - Type Name Handling

**Used for**: Classes, structs, enums

**Key insight**: Types preserve **exact user casing**. Why?

```python
class UserAccount:  # User chose this casing
    pass

class HTTP_Client:  # User wants this for clarity
    pass
```

Both are preserved as-is (except keyword escaping). This respects domain conventions (HTTP, XML, etc.).

---

#### `ToConstantCase(string name)` - Constant Preservation

**Used for**: Constants

**Behavior**: Preserve exactly as-is

```python
MAX_SIZE = 100      → MAX_SIZE
DEFAULT_TIMEOUT = 5 → DEFAULT_TIMEOUT
```

Python and C# both use `CAPS_SNAKE_CASE` for constants, so no transformation needed!

---

### Dunder Method Handling

#### Understanding Dunder Method Mappings

Python uses "dunder" (double-underscore) methods for special behavior. Sharpy maps these to C# equivalents where possible:

```csharp
private static readonly Dictionary<string, string> _dunderMethodMap = new()
{
    { "__init__", "Constructor" },      // Becomes C# constructor
    { "__str__", "ToString" },          // Maps to Object.ToString()
    { "__eq__", "Equals" },             // Maps to Object.Equals()
    { "__hash__", "GetHashCode" },      // Maps to Object.GetHashCode()
    { "__getitem__", "GetItem" },       // For indexers: obj[key]
    { "__len__", "Length" },            // For property access
    { "__iter__", "GetEnumerator" },    // For foreach support
    // ...
};
```

**Critical distinction**: Operator dunders (`__add__`, `__sub__`, etc.) are **NOT** in this map. Why?

```csharp
// If we mapped __add__ to "Add":
class MyNumber:
    def __add__(self, other):  # Becomes Add()?
        pass
    
    def add(self, other):      # Also becomes Add()?
        pass  # COLLISION!
```

Instead, operator dunders become `__Add__`, `__Sub__`, etc. (capitalized dunder), avoiding conflicts with user methods.

---

#### `IsDunderMethod(string name)` - Detection

```csharp
public static bool IsDunderMethod(string name)
{
    return name.StartsWith("__") && name.EndsWith("__") && name.Length > 5;
}
```

Checks if a name is a dunder method. The `length > 5` prevents `"____"` from matching.

---

#### `GetDunderMethodMapping(string dunderName)` - Lookup

```csharp
public static string? GetDunderMethodMapping(string dunderName)
{
    return _dunderMethodMap.TryGetValue(dunderName, out var mapped) ? mapped : null;
}
```

Returns the C# equivalent if one exists, otherwise `null`. Callers use this to decide whether to generate a special construct (like a constructor) vs. a regular method.

---

### Keyword Escaping

#### `EscapeKeywordIfNeeded(string name)` - C# Keyword Collision Prevention

```csharp
private static string EscapeKeywordIfNeeded(string name)
{
    return _csharpKeywords.Contains(name.ToLowerInvariant())
        ? "@" + name
        : name;
}
```

**Problem**: User writes valid Sharpy code that uses C# keywords as identifiers:

```python
def class_method():  # "class" is a C# keyword!
    return_value = 42  # "return" is a C# keyword!
```

**Solution**: Prefix with `@`:

```csharp
void ClassMethod() {
    int @return = 42;  // Valid C#!
}
```

The `_csharpKeywords` set contains all 76 C# keywords.

---

### Helper Methods

#### `Capitalize(string word)` - Single Word Capitalization

```csharp
private static string Capitalize(string word)
{
    if (string.IsNullOrEmpty(word))
        return word;
    
    return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
}
```

Transforms `"hello"` → `"Hello"`, `"HELLO"` → `"Hello"`. Used internally by `ToPascalCase()` and `ToCamelCase()`.

---

## 4. Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Semantic.ProtocolRegistry`**: 
   - Used in DEBUG builds to validate that all protocol dunders with CLR mappings are in `_dunderMethodMap`
   - Ensures consistency between protocol definitions and name mangling

### External Dependencies

- **.NET BCL**: `System.Collections.Generic` (HashSet, Dictionary)
- **LINQ**: Used for `Select()`, `Skip()` in transformations

### Callers of NameMangler

- **`RoslynEmitter`**: Primary consumer—uses `Transform()` for every identifier it emits
- **`TypeMapper`**: May use it when generating type names
- Potentially other CodeGen components

---

## 5. Patterns and Design Decisions

### Design Pattern: Static Utility Class

**Why static?**
- No state to maintain
- Thread-safe by design (read-only static data)
- Easy to call from anywhere: `NameMangler.Transform(...)`

**Alternative considered**: Instance-based with configuration. Rejected because name mangling rules should be consistent across the entire compilation.

---

### Design Decision: Enum-Based Context

**Instead of**:
```csharp
ToMethodName()
ToVariableName()
ToParameterName()
ToFieldName()
// ... 10+ different methods
```

**We have**:
```csharp
Transform(name, NameContext.Method)
Transform(name, NameContext.Variable)
// Single entry point
```

**Benefits**:
- Easier to extend (add new context, handle in switch)
- Self-documenting code at call sites
- Consistent API surface

---

### Design Decision: Preserve User Intent

The code goes to great lengths to **preserve** user choices:

1. **Already PascalCase?** Don't break it
2. **Backtick-escaped?** Use exactly as-is
3. **Type names?** Keep user casing (HTTP_Client stays HTTP_Client)

This respects the principle: **Sharpy is opinionated but not dictatorial**.

---

### Design Decision: Defensive Dunder Handling

```csharp
// Unknown dunder method - preserve dunder but capitalize the middle part
// e.g., __add__ -> __Add__, __custom_method__ -> __CustomMethod__
var middle = name[2..^2];
var capitalizedMiddle = string.Join("", middle.Split('_').Select(Capitalize));
return $"__{capitalizedMiddle}__";
```

Even for unknown dunder methods, we apply a sensible transformation. This prevents compiler crashes if Python adds new dunders or users define custom ones.

---

### Design Decision: DEBUG-Time Validation

```csharp
#if DEBUG
    static NameMangler()
    {
        // Verify all protocol dunders with CLR mappings are in _dunderMethodMap
        foreach (var protocol in ProtocolRegistry.GetAllProtocols())
        {
            if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
            {
                System.Diagnostics.Debug.Assert(false, "...");
            }
        }
    }
#endif
```

In DEBUG builds, the static constructor validates consistency between `ProtocolRegistry` and `_dunderMethodMap`. This catches configuration errors at development time, not in production.

---

## 6. Debugging Tips

### Problem: Generated C# Has Wrong Casing

**Symptom**: 
```csharp
// Expected: public void MyMethod()
// Got: public void my_method()
```

**Debug approach**:
1. Check the call site in `RoslynEmitter`—is the correct `NameContext` passed?
2. Add a breakpoint in `Transform()` to see what context is used
3. Verify the name isn't backtick-escaped (which would bypass transformation)

---

### Problem: Keyword Collision in Generated C#

**Symptom**: C# compilation fails with `"class" is a keyword`

**Likely cause**: `EscapeKeywordIfNeeded()` isn't being called, or the keyword isn't in `_csharpKeywords`.

**Debug approach**:
1. Search `_csharpKeywords` for the problematic keyword
2. If missing, add it to the set
3. Verify all transformation methods call `EscapeKeywordIfNeeded()` before returning

---

### Problem: Dunder Method Not Mapping Correctly

**Symptom**: `__str__` becomes `__Str__` instead of `ToString()`

**Debug approach**:
1. Check if `__str__` is in `_dunderMethodMap`
2. Verify `RoslynEmitter` uses `GetDunderMethodMapping()` to check for special handling
3. Look at the dunder method handling logic around line 94-104

**Common mistake**: Forgetting to special-case the dunder in `RoslynEmitter`—adding to the map isn't enough; the emitter must check and handle it specially.

---

### Problem: Private Method Name Collision

**Symptom**: Two methods `_helper()` and `helper()` both become `Helper()`

**Likely cause**: Private prefix logic isn't working correctly.

**Debug approach**:
1. Check lines 106-108 in `ToPascalCase()`—is the underscore prefix being detected?
2. Verify it's restored at line 125
3. Test with a simple case: `NameMangler.ToPascalCase("_helper")` should return `"_Helper"`

---

### Useful Test Cases for Manual Verification

```csharp
// Add these to your test file or run in a C# REPL:

NameMangler.ToPascalCase("my_function")         // → "MyFunction"
NameMangler.ToPascalCase("_private_method")     // → "_PrivateMethod"
NameMangler.ToPascalCase("__init__")            // → "Constructor"
NameMangler.ToPascalCase("__add__")             // → "__Add__"
NameMangler.ToPascalCase("AlreadyPascalCase")   // → "AlreadyPascalCase"

NameMangler.ToCamelCase("user_name")            // → "userName"
NameMangler.ToCamelCase("_private_var")         // → "_privateVar"

NameMangler.ToTypeName("HTTP_Client")           // → "HTTP_Client"

NameMangler.Transform("class", NameContext.Variable) // → "@class"
```

---

## 7. Contribution Guidelines

### When to Modify This File

**You SHOULD modify `NameMangler.cs` when**:

1. **Adding a new dunder method mapping**:
   ```csharp
   // New Python protocol needs C# equivalent
   { "__enter__", "Enter" },  // For context managers
   { "__exit__", "Exit" },
   ```

2. **Fixing a naming bug**:
   - Wrong casing in generated code
   - Keyword collision not being handled
   - Edge case causing crashes

3. **Adding support for a new identifier context**:
   ```csharp
   // Add to NameContext enum:
   Property,  // For C# properties

   // Add to Transform() switch:
   NameContext.Property => ToPropertyName(name),
   ```

4. **Improving preservation of user intent**:
   - Better detection of already-correct casing
   - New escaping mechanisms

---

### When NOT to Modify This File

**You SHOULD NOT modify `NameMangler.cs` for**:

1. **Changing overall naming strategy** without team discussion—this is a foundational design decision

2. **Adding instance state**—keep it stateless and thread-safe

3. **Type-specific transformations**—those belong in `TypeMapper.cs`

4. **Code generation logic**—that belongs in `RoslynEmitter.cs`

---

### Adding a New Dunder Method Mapping

**Steps**:

1. **Add to `_dunderMethodMap`**:
   ```csharp
   { "__enter__", "Enter" },
   ```

2. **Update `RoslynEmitter.cs`** to handle the special case:
   ```csharp
   if (NameMangler.GetDunderMethodMapping(method.Name) == "Enter")
   {
       // Generate using statement support
   }
   ```

3. **Add tests** in `Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`:
   ```csharp
   [Fact]
   public void ToPascalCase_EnterDunder_ReturnsEnter()
   {
       Assert.Equal("Enter", NameMangler.ToPascalCase("__enter__"));
   }
   ```

4. **Verify DEBUG assertion passes**: Run tests in DEBUG mode to ensure `ProtocolRegistry` is updated if needed

---

### Testing Your Changes

**Unit tests location**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`

**Test categories to cover**:
- Basic transformations (snake → PascalCase, camelCase)
- Edge cases (empty strings, single characters, all underscores)
- Dunder methods (known and unknown)
- Private names (single underscore prefix)
- Literal names (backtick-escaped)
- Keyword escaping
- Already-correct casing preservation

**Integration testing**: Compile a `.spy` file and inspect the generated C# to ensure names look correct.

---

### Code Style Guidelines for This File

1. **Keep methods pure**: No side effects, no hidden state
2. **Null-safe**: Always check for `IsNullOrEmpty()`
3. **Fail gracefully**: Unknown input should produce reasonable output, not crash
4. **Comment non-obvious logic**: Why do operator dunders skip the map? Explain in comments.
5. **Consistent naming**: Helper methods like `Capitalize()` are `private static`

---

### Common Pitfalls to Avoid

1. **Don't forget keyword escaping**: Every public method should call `EscapeKeywordIfNeeded()` before returning

2. **Don't break user intent**: Test that already-correct names aren't modified

3. **Don't special-case too much**: If you find yourself writing method-specific logic here, it probably belongs in `RoslynEmitter`

4. **Don't assume ASCII**: Use `char.ToUpperInvariant()`, not `ToUpper()`, for culture-independent behavior

5. **Don't forget DEBUG validation**: If adding a protocol dunder, update both `_dunderMethodMap` and `ProtocolRegistry`

---

## Quick Reference

### Method Selection Guide

| Context | Method | Example |
|---------|--------|---------|
| Function/Method definition | `ToPascalCase` | `my_func` → `MyFunc` |
| Variable/Parameter | `ToCamelCase` | `user_name` → `userName` |
| Class/Type name | `ToTypeName` | `MyClass` → `MyClass` |
| Constant | `ToConstantCase` | `MAX_SIZE` → `MAX_SIZE` |
| Unknown context | `Transform(name, context)` | Use with appropriate `NameContext` |

### Common Transformations

```
my_function       → MyFunction     (PascalCase)
user_name         → userName       (camelCase)
_private_method   → _PrivateMethod (preserve prefix)
__init__          → Constructor    (dunder mapping)
__add__           → __Add__        (operator dunder)
MAX_SIZE          → MAX_SIZE       (constant)
class             → @class         (keyword escape)
`CustomName`      → CustomName     (literal, remove backticks)
```

---

## Summary

`NameMangler.cs` is a small but crucial file that bridges two programming language cultures. Its stateless, enum-driven design makes it reliable and easy to use. When modifying it:

- **Preserve user intent** first
- **Test thoroughly** with edge cases
- **Keep it pure** (no side effects)
- **Validate in DEBUG** mode
- **Coordinate with `RoslynEmitter`** for special handling

Understanding this file helps you understand a core tension in language design: respecting the source language's conventions while producing idiomatic target code. Sharpy chooses to be Pythonic in source but .NET-native in output—`NameMangler` makes that possible.

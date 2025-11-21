# Walkthrough: NameMangler.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

---

## Overview

The `NameMangler` class is a critical component in the Sharpy compiler's code generation pipeline. Its primary responsibility is **converting Sharpy naming conventions to C# naming conventions** while ensuring the generated C# code is valid and avoids name collisions.

### Why Name Mangling?

Sharpy follows Python's naming conventions (snake_case for functions/variables, dunder methods like `__init__`, etc.), while C# uses different conventions (PascalCase for methods, camelCase for parameters, etc.). The NameMangler bridges this gap by:

1. **Converting naming styles** - Transforms `my_function` → `MyFunction`, `user_name` → `userName`
2. **Handling special Python constructs** - Maps `__init__` → `Constructor`, `__str__` → `ToString`
3. **Avoiding C# keyword conflicts** - Escapes names like `class`, `return`, `for` with `@` prefix
4. **Preserving intentional naming** - Keeps names wrapped in backticks (`` `MyName` ``) as-is

### Role in the Compiler Pipeline

```
Sharpy Source Code
    ↓
Parser (AST)
    ↓
Semantic Analyzer
    ↓
Code Generator → **NameMangler** (naming transformation)
    ↓
C# Code Output
    ↓
Roslyn Compiler
```

The NameMangler is called by the `RoslynEmitter` during code generation to ensure all identifiers in the generated C# code follow C# conventions and don't conflict with C# keywords.

---

## Class/Type Structure

### Main Class: `NameMangler`

```csharp
public static class NameMangler
```

A **static utility class** with no instance state. All methods are static and operate purely on input strings, making it stateless and thread-safe.

### Supporting Type: `NameContext` Enum

```csharp
public enum NameContext
{
    Type,       // Classes, structs, enums
    Interface,  // Interfaces
    Method,     // Instance and static methods
    Function,   // Top-level functions
    Variable,   // Local variables
    Parameter,  // Function/method parameters
    Field,      // Class/struct fields
    Constant    // Constants (CAPS_SNAKE_CASE)
}
```

This enum tells the NameMangler **what kind of identifier** is being transformed, so it can apply the appropriate naming convention.

### Private Static Fields

#### `_csharpKeywords` (HashSet<string>)

Contains all C# reserved keywords that would cause compilation errors if used as identifiers:

```csharp
private static readonly HashSet<string> _csharpKeywords = new()
{
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
    "char", "checked", "class", "const", "continue", // ... 70+ keywords
};
```

**Why it matters**: If a Sharpy identifier happens to be named `class` or `return`, the NameMangler will prefix it with `@` to make it `@class` or `@return`, which is valid C#.

#### `_dunderMethodMap` (Dictionary<string, string>)

Maps Python "dunder" (double underscore) methods to their C# equivalents:

```csharp
private static readonly Dictionary<string, string> _dunderMethodMap = new()
{
    { "__init__", "Constructor" },
    { "__str__", "ToString" },
    { "__eq__", "Equals" },
    { "__hash__", "GetHashCode" },
    { "__getitem__", "GetItem" },
    // ... more mappings
};
```

**Design Decision**: Only dunder methods with **direct C# equivalents** are mapped. Operator dunder methods like `__add__`, `__sub__` are **NOT** in this map - they preserve their dunder names (capitalized to `__Add__`, `__Sub__`) to avoid conflicts with user-defined `Add()` or `Sub()` methods.

---

## Key Functions/Methods

### 1. `Transform(string name, NameContext context)` - The Entry Point

```csharp
public static string Transform(string name, NameContext context)
{
    return context switch
    {
        NameContext.Type => ToTypeName(name),
        NameContext.Interface => ToInterfaceName(name),
        NameContext.Method => ToPascalCase(name),
        NameContext.Function => ToPascalCase(name),
        NameContext.Variable => ToCamelCase(name),
        NameContext.Parameter => ToCamelCase(name),
        NameContext.Constant => ToConstantCase(name),
        NameContext.Field => ToCamelCase(name),
        _ => name
    };
}
```

**What it does**: This is the main entry point called by the code generator. Based on the context, it routes to the appropriate transformation function.

**Key insight**: This method embodies the **strategy pattern** - the same name gets transformed differently based on its role in the code.

**Example usage in code generator**:
```csharp
// In RoslynEmitter.cs
var methodName = NameMangler.Transform(sharpyMethodName, NameContext.Method);
var paramName = NameMangler.Transform(sharpyParamName, NameContext.Parameter);
```

---

### 2. `ToPascalCase(string name)` - Method & Function Names

```csharp
public static string ToPascalCase(string name)
```

**What it does**: Converts snake_case to PascalCase for methods and functions.

**Transformation rules**:
1. **Backtick-escaped names** (`` `MyName` ``) → strip backticks, return as-is
2. **Dunder methods** (`__init__`, `__str__`) → map to C# equivalents or capitalize middle part
3. **Private names** (`_my_method`) → preserve underscore prefix, convert rest to PascalCase
4. **Already PascalCase** (`MyMethod`) → preserve as-is (no transformation)
5. **snake_case** (`my_method`) → convert to PascalCase (`MyMethod`)

**Implementation details**:

```csharp
// Dunder method handling
if (name.StartsWith("__") && name.EndsWith("__"))
{
    if (_dunderMethodMap.TryGetValue(name, out var mapped))
        return mapped;  // __init__ → Constructor
    
    // Unknown dunder: __add__ → __Add__, __custom_method__ → __CustomMethod__
    var middle = name[2..^2];
    var capitalizedMiddle = string.Join("", middle.Split('_').Select(Capitalize));
    return $"__{capitalizedMiddle}__";
}
```

**Why the dunder preservation?** Operator dunder methods like `__add__` need to remain recognizable for operator overload synthesis later in the code generation pipeline.

**Examples**:
- `my_function` → `MyFunction`
- `_private_method` → `_PrivateMethod`
- `__init__` → `Constructor`
- `__add__` → `__Add__`
- `calculate_total` → `CalculateTotal`
- `HTTPServer` → `HTTPServer` (already PascalCase, preserved)

---

### 3. `ToCamelCase(string name)` - Variables, Parameters, Fields

```csharp
public static string ToCamelCase(string name)
```

**What it does**: Converts snake_case to camelCase for local variables, parameters, and fields.

**Transformation rules**:
1. **Backtick-escaped** → strip backticks
2. **Dunder methods** → return as-is (shouldn't be used for variables)
3. **Private names** → preserve underscore, convert rest
4. **First part lowercased**, rest capitalized

**Implementation**:
```csharp
var parts = cleanName.Split('_');
var result = parts[0].ToLowerInvariant();  // First part lowercase
if (parts.Length > 1)
{
    result += string.Join("", parts.Skip(1).Select(Capitalize));  // Rest capitalized
}
```

**Examples**:
- `user_name` → `userName`
- `total_count` → `totalCount`
- `_private_var` → `_privateVar`
- `x` → `x`
- `class` → `@class` (keyword escaped)

---

### 4. `ToTypeName(string name)` - Classes, Structs, Enums

```csharp
public static string ToTypeName(string name)
```

**What it does**: Preserves type names exactly as written by the user (respecting their casing choice), only handling keyword escaping and backtick literals.

**Design philosophy**: Unlike methods/variables, Sharpy respects the user's exact casing for type names. If they write `class MyClass`, it stays `MyClass`. If they write `class myclass`, it stays `myclass`.

**Why?** This gives developers full control over their public API naming, which is important for library authors.

**Examples**:
- `MyClass` → `MyClass`
- `HTTPServer` → `HTTPServer`
- `myclass` → `myclass`
- `` `class` `` → `class` (backtick-escaped)
- `struct` → `@struct` (keyword)

---

### 5. `ToInterfaceName(string name)` - Interface Names

```csharp
public static string ToInterfaceName(string name)
```

**What it does**: Same as `ToTypeName` - preserves user's exact casing, only handles keyword escaping.

**Note**: Currently no automatic `I` prefix addition. If the user wants `IMyInterface`, they write it that way in Sharpy.

---

### 6. `ToConstantCase(string name)` - Constants

```csharp
public static string ToConstantCase(string name)
```

**What it does**: Keeps constants in their original form (typically `CAPS_SNAKE_CASE`).

**Examples**:
- `MAX_SIZE` → `MAX_SIZE`
- `PI` → `PI`
- `default` → `@default` (keyword)

---

### 7. `IsDunderMethod(string name)` - Utility

```csharp
public static bool IsDunderMethod(string name)
{
    return name.StartsWith("__") && name.EndsWith("__") && name.Length > 5;
}
```

**What it does**: Checks if a name is a dunder method (double underscore prefix and suffix, with content in between).

**Length check**: `name.Length > 5` ensures we have at least one character in the middle (`__x__` is shortest valid dunder).

---

### 8. `GetDunderMethodMapping(string dunderName)` - Utility

```csharp
public static string? GetDunderMethodMapping(string dunderName)
{
    return _dunderMethodMap.TryGetValue(dunderName, out var mapped) ? mapped : null;
}
```

**What it does**: Returns the C# equivalent for a dunder method, or `null` if no mapping exists.

**Usage**: Allows other compiler components to query whether a dunder method should be specially handled.

---

### 9. `Capitalize(string word)` - Private Helper

```csharp
private static string Capitalize(string word)
{
    if (string.IsNullOrEmpty(word))
        return word;
    
    return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
}
```

**What it does**: Capitalizes the first letter and lowercases the rest of a word.

**Example**: `http` → `Http`, `API` → `Api`

---

### 10. `EscapeKeywordIfNeeded(string name)` - Private Helper

```csharp
private static string EscapeKeywordIfNeeded(string name)
{
    return _csharpKeywords.Contains(name.ToLowerInvariant())
        ? "@" + name
        : name;
}
```

**What it does**: Prefixes C# keywords with `@` to make them valid identifiers.

**Case-insensitive**: Uses `ToLowerInvariant()` because C# keywords are lowercase, but user might have written `Class` or `CLASS`.

**Examples**:
- `class` → `@class`
- `return` → `@return`
- `myVar` → `myVar` (not a keyword)

---

## Dependencies

### Internal Dependencies

1. **`RoslynEmitter.cs`** - The primary consumer. Calls NameMangler during C# code generation.
2. **`CodeGenContext.cs`** - May use NameMangler for tracking transformed names.
3. **AST nodes** (indirectly) - The names being transformed come from parsed AST.

### External Dependencies

- **.NET BCL** only:
  - `System.Collections.Generic` (HashSet, Dictionary)
  - `System.Linq` (for `Select`, `Skip`)
  - String manipulation methods

**No other Sharpy components** - NameMangler is completely standalone, making it easy to test in isolation.

---

## Patterns and Design Decisions

### 1. **Static Utility Class Pattern**

The class is static with no instance state, making it:
- **Thread-safe** (no shared mutable state)
- **Easy to use** (no need to instantiate)
- **Predictable** (pure functions - same input always produces same output)

### 2. **Strategy Pattern via NameContext**

The `Transform()` method uses the context enum to select the appropriate transformation strategy. This is cleaner than having the caller know which specific method to call.

### 3. **Immutability**

All transformations return **new strings**, never modifying input. This prevents accidental side effects.

### 4. **Conservative Dunder Mapping**

**Key insight**: Only map dunder methods that have **direct C# equivalents**. Operator dunder methods preserve their dunder names to avoid conflicts.

**Why?** Consider this scenario:
```python
class Vector:
    def __add__(self, other):  # Operator overload
        pass
    
    def add(self, item):  # Regular method
        pass
```

If `__add__` was mapped to `Add`, it would conflict with the user's `add` method (both become `Add` in C#). By keeping it as `__Add__`, we avoid the collision.

### 5. **Backtick Escape Hatch**

The backtick syntax (`` `name` ``) provides an escape hatch for edge cases where the user needs exact control over the C# name.

**Example use case**:
```python
# Sharpy code
def `ToString`():  # Explicitly want C# name ToString
    pass
```

This allows interop with existing C# libraries that might have specific naming requirements.

### 6. **Private Name Preservation**

Python's convention of `_private` is preserved in C# as `_Private` (PascalCase) or `_private` (camelCase), maintaining the visual signal of privacy.

---

## Debugging Tips

### Common Issues and How to Debug Them

#### Issue 1: "Generated C# code won't compile - identifier is a keyword"

**Symptoms**: Roslyn compilation error about unexpected keyword.

**Debug approach**:
1. Check if the identifier is in `_csharpKeywords`
2. Verify `EscapeKeywordIfNeeded()` is being called
3. Check if the keyword was added to the HashSet with correct casing (lowercase)

**Fix**: Add missing keyword to `_csharpKeywords`.

#### Issue 2: "Name collision in generated C#"

**Symptoms**: Two different Sharpy identifiers produce the same C# name.

**Debug approach**:
```csharp
// Add logging to Transform()
Console.WriteLine($"Transform: {name} ({context}) → {result}");
```

**Common causes**:
- `my_method` and `MyMethod` both become `MyMethod` in PascalCase
- Dunder method mapping creates collision

**Solution**: Use backticks in Sharpy code to force specific names, or enhance collision detection in NameMangler.

#### Issue 3: "Dunder method not working as expected"

**Debug approach**:
1. Check if the dunder method is in `_dunderMethodMap`
2. If not, it's getting the default capitalization (`__add__` → `__Add__`)
3. Verify this is intentional (operators should preserve dunder names)

**Logging tip**:
```csharp
// In ToPascalCase, add:
if (name.StartsWith("__") && name.EndsWith("__"))
{
    Console.WriteLine($"Dunder: {name} → {result}");
}
```

#### Issue 4: "Private method names not preserving underscore"

**Check**:
- `hasPrivatePrefix` logic in `ToPascalCase()` and `ToCamelCase()`
- Ensure the underscore is being prepended after transformation

#### Issue 5: "PascalCase not preserving already-correct names"

**Debug**:
```csharp
// In ToPascalCase
if (!cleanName.Contains('_') && char.IsUpper(cleanName[0]))
{
    Console.WriteLine($"Already PascalCase: {cleanName}");
    return EscapeKeywordIfNeeded(cleanName);
}
```

This check prevents `HTTPServer` from becoming `Httpserver`.

---

## Testing Strategy

### Unit Tests Location

Tests would be in `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`.

### Test Categories

1. **Keyword escaping tests**
   ```csharp
   Assert.Equal("@class", NameMangler.ToPascalCase("class"));
   Assert.Equal("@return", NameMangler.ToCamelCase("return"));
   ```

2. **Snake case conversion tests**
   ```csharp
   Assert.Equal("MyMethod", NameMangler.ToPascalCase("my_method"));
   Assert.Equal("userName", NameMangler.ToCamelCase("user_name"));
   ```

3. **Dunder method tests**
   ```csharp
   Assert.Equal("Constructor", NameMangler.ToPascalCase("__init__"));
   Assert.Equal("ToString", NameMangler.ToPascalCase("__str__"));
   Assert.Equal("__Add__", NameMangler.ToPascalCase("__add__"));
   ```

4. **Private name tests**
   ```csharp
   Assert.Equal("_MyMethod", NameMangler.ToPascalCase("_my_method"));
   Assert.Equal("_userName", NameMangler.ToCamelCase("_user_name"));
   ```

5. **Backtick literal tests**
   ```csharp
   Assert.Equal("MyName", NameMangler.ToPascalCase("`MyName`"));
   ```

6. **Edge cases**
   ```csharp
   Assert.Equal("", NameMangler.ToPascalCase(""));
   Assert.Equal(null, NameMangler.ToPascalCase(null));
   Assert.Equal("X", NameMangler.ToPascalCase("x"));
   ```

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new C# keywords** - If newer C# versions add keywords
2. **Adding dunder method mappings** - When supporting new Python special methods
3. **Fixing naming edge cases** - When discovering new collision scenarios
4. **Performance optimization** - If name mangling becomes a bottleneck

### What Kinds of Changes to Make

#### ✅ Good Changes

1. **Add missing C# keyword**:
   ```csharp
   // C# 12 added a new keyword
   "file", "required", "scoped"  // Add to _csharpKeywords
   ```

2. **Add new dunder mapping**:
   ```csharp
   { "__enter__", "Enter" },  // For context managers
   { "__exit__", "Exit" }
   ```

3. **Improve already-PascalCase detection**:
   ```csharp
   // Better handling for acronyms like "HTTPSServer"
   ```

4. **Add utility methods**:
   ```csharp
   public static bool IsReservedCSharpName(string name)
   {
       return _csharpKeywords.Contains(name.ToLowerInvariant());
   }
   ```

#### ❌ Changes to Avoid

1. **Don't add operator dunder mappings to `_dunderMethodMap`**
   - Keep `__add__`, `__sub__`, etc. unmapped
   - They need to preserve dunder names to avoid collisions

2. **Don't automatically add prefixes/suffixes**
   ```csharp
   // DON'T do this:
   public static string ToInterfaceName(string name)
   {
       return "I" + name;  // ❌ Forces naming convention
   }
   ```
   Let the user control their naming.

3. **Don't make transformations context-dependent beyond NameContext**
   ```csharp
   // DON'T do this:
   public static string ToPascalCase(string name, string className)
   {
       if (className == "MyClass") { /* special logic */ }  // ❌ Too complex
   }
   ```

### Testing Requirements

**For any change to NameMangler**:

1. **Add tests** for the new behavior
2. **Run all existing tests** to ensure no regressions
3. **Test integration** with RoslynEmitter
4. **Compile a real Sharpy program** to verify end-to-end

### Performance Considerations

- NameMangler is called **frequently** during code generation
- Keep transformations **O(n)** where n is name length
- Avoid expensive regex or repeated string allocations
- Cache results if the same name is transformed multiple times (future optimization)

---

## Real-World Examples

### Example 1: Method Transformation

**Sharpy code**:
```python
class Calculator:
    def calculate_total(self, items: list[int]) -> int:
        return sum(items)
```

**Name transformations**:
- `Calculator` (type) → `Calculator` (preserved)
- `calculate_total` (method) → `CalculateTotal`
- `items` (parameter) → `items`

**Generated C#**:
```csharp
public class Calculator
{
    public int CalculateTotal(List<int> items)
    {
        return items.Sum();
    }
}
```

### Example 2: Dunder Methods

**Sharpy code**:
```python
class Point:
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
    
    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"
    
    def __add__(self, other: Point) -> Point:
        return Point(self.x + other.x, self.y + other.y)
```

**Name transformations**:
- `__init__` → `Constructor` (special mapping)
- `__str__` → `ToString` (C# override)
- `__add__` → `__Add__` (preserved dunder, operator synthesis)

### Example 3: Keyword Collision

**Sharpy code**:
```python
def return(value: int) -> int:  # 'return' is a keyword in both languages
    class: str = "example"      # 'class' is a C# keyword
    return value
```

**Name transformations**:
- `return` (function) → `@Return`
- `class` (variable) → `@class`

**Generated C#**:
```csharp
public static int @Return(int value)
{
    string @class = "example";
    return value;
}
```

---

## Future Enhancements

### Potential Improvements

1. **Collision Detection**
   - Track all generated names
   - Detect when two Sharpy names map to the same C# name
   - Automatically disambiguate with suffixes

2. **Caching**
   - Cache transformed names to avoid redundant string operations
   - Especially useful for large codebases

3. **Custom Naming Conventions**
   - Allow projects to configure naming preferences
   - Support for different C# coding standards

4. **Better Acronym Handling**
   - Preserve common acronyms: `HTTP`, `API`, `XML`
   - `http_server` → `HTTPServer` instead of `HttpServer`

5. **Namespace Support**
   - Handle namespaced types: `System.Collections.Generic.List`
   - Preserve namespace structure in transformations

---

## Summary

The `NameMangler` is a **critical but focused component** that bridges the gap between Python and C# naming conventions. Its design prioritizes:

- **Simplicity** - Pure functions, no state
- **Predictability** - Same input always gives same output  
- **Safety** - Keyword escaping prevents compilation errors
- **Flexibility** - Backtick escape hatch for edge cases
- **Respect for intent** - Preserves user choices where appropriate

Understanding NameMangler is essential for anyone working on code generation, as it determines how every identifier in the Sharpy source code appears in the final C# output.

**Key takeaway**: Name mangling isn't just about converting snake_case to PascalCase - it's about preserving semantics, avoiding collisions, and generating valid, idiomatic C# code from Pythonic Sharpy code.

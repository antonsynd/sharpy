# Walkthrough: OverloadIndexBuilder.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`

---

## Overview

`OverloadIndexBuilder` is a critical component of Sharpy's assembly discovery and caching system. Its primary job is to **introspect .NET assemblies at runtime** and build a searchable index of all public static functions that Sharpy code can call.

### Why Does This Exist?

When Sharpy code imports a .NET library or the standard library (`Sharpy.Core`), the compiler needs to know:
- What functions are available?
- What are their signatures (parameter types, return types)?
- How do I call them from Sharpy code?

Rather than discovering this information repeatedly during compilation, `OverloadIndexBuilder` creates a **serializable cache** (the `OverloadIndex`) that can be saved and reused. This dramatically improves compiler performance for large projects.

### Key Responsibilities

1. **Reflection-based Discovery**: Scan assemblies for public static methods in `Exports` classes
2. **Name Mangling**: Convert C# `PascalCase` method names to Python `snake_case` conventions
3. **Type Mapping**: Translate CLR types (like `System.Int32`) to Sharpy semantic types (like `int`)
4. **Signature Extraction**: Capture parameter types, return types, default values, and variadic arguments
5. **Method Tokenization**: Create unique identifiers for methods that can be used to find them later

---

## Class/Type Structure

### Main Class: `OverloadIndexBuilder`

```csharp
public class OverloadIndexBuilder
{
    private readonly TypeMapper _typeMapper = new();
    // ...
}
```

**State**:
- `_typeMapper`: A `Discovery.TypeMapper` instance used to convert CLR types to Sharpy semantic types. It maintains a thread-safe cache of type mappings to avoid redundant conversions.

**Key Insight**: This class is stateless beyond the type mapper. You can reuse a single instance to build indexes for multiple assemblies, benefiting from the cached type mappings.

### Data Structures Created

The builder produces an `OverloadIndex` which has this structure:

```
OverloadIndex
├── Identity: AssemblyIdentity (name, version, content hash)
├── CreatedAt: DateTime
├── CacheFormatVersion: int
└── Modules: Dictionary<string, ModuleOverloads>
    └── ModuleOverloads
        ├── ModuleName: string
        └── Functions: Dictionary<string, List<FunctionSignature>>
            └── FunctionSignature
                ├── Name: string (snake_case)
                ├── Parameters: List<ParameterSignature>
                ├── ReturnType: TypeSignature
                └── MethodToken: string (for rehydration)
```

This hierarchical structure mirrors how Sharpy code accesses functions:
```python
# Sharpy code
from builtins import print, len
from some_module import some_function
```

---

## Key Functions/Methods

### 1. `BuildFromAssembly(Assembly assembly)` - Entry Point

**Purpose**: The main public API. Accepts a .NET assembly and returns a complete `OverloadIndex`.

**Flow**:
```csharp
public OverloadIndex BuildFromAssembly(Assembly assembly)
{
    // 1. Create identity for cache invalidation
    var identity = AssemblyIdentity.FromAssembly(assembly);
    
    // 2. Initialize index structure
    var index = new OverloadIndex { Identity = identity, ... };
    
    // 3. Find all "Exports" classes
    var exportTypes = assembly.GetTypes()
        .Where(t => t.Name == "Exports" && t.IsClass && 
                    t.IsPublic && t.IsAbstract && t.IsSealed)
        .ToList();
    
    // 4. Discover functions in each Exports class
    foreach (var exportType in exportTypes)
    {
        var moduleName = DeriveModuleName(exportType);
        var moduleOverloads = DiscoverModuleFunctions(exportType);
        
        if (moduleOverloads.Functions.Count > 0)
            index.Modules[moduleName] = moduleOverloads;
    }
    
    return index;
}
```

**Key Details**:
- **"Exports" Pattern**: Sharpy uses C# static classes named `Exports` as the contract for exposing functions. These classes must be:
  - `public` (accessible from other assemblies)
  - `abstract sealed` (C#'s way of marking a static class via reflection)
- **Module Discovery**: Each `Exports` class becomes a Sharpy module (namespace-based naming)
- **Empty Filtering**: Modules with no discoverable functions are excluded from the index

**Why Multiple Exports Classes?**: A large assembly like `Sharpy.Core` uses partial classes spread across files:
```
Sharpy.Core.Exports (in Partial.List/List.Core.cs)
Sharpy.Core.Exports (in Partial.Str/Str.Core.cs)
Sharpy.Core.Exports (in Builtins.cs)
```

---

### 2. `DeriveModuleName(Type exportType)` - Namespace to Module Name

**Purpose**: Converts .NET namespace conventions to Sharpy module names.

**Logic**:
```csharp
private string DeriveModuleName(Type exportType)
{
    // Special case: Sharpy.Core.Exports → "builtins"
    if (exportType.FullName == "Sharpy.Core.Exports")
        return "builtins";
    
    // General case: namespace → lowercase_with_underscores
    var ns = exportType.Namespace ?? "unknown";
    return ns.ToLowerInvariant().Replace(".", "_");
}
```

**Examples**:
- `Sharpy.Core.Exports` → `"builtins"` (hardcoded for Python compatibility)
- `MyApp.Utils.Exports` → `"myapp_utils"`
- `System.IO.Exports` → `"system_io"`

**Design Decision**: The special case for `"builtins"` ensures that Sharpy's standard library functions (like `print`, `len`, `range`) are available as builtins, matching Python's behavior.

---

### 3. `DiscoverModuleFunctions(Type exportType)` - Method Extraction

**Purpose**: The heart of the discovery process. Finds all eligible methods in an `Exports` class and creates function signatures.

**Flow**:

```csharp
private ModuleOverloads DiscoverModuleFunctions(Type exportType)
{
    var moduleOverloads = new ModuleOverloads { /* ... */ };
    
    // Step 1: Get all public static methods
    var methods = exportType.GetMethods(BindingFlags.Public | BindingFlags.Static);
    
    // Step 2: Filter methods
    var eligibleMethods = methods
        .Where(m => !m.Name.StartsWith("_"))           // Skip private/internal
        .Where(m => !m.Name.StartsWith("get_"))        // Skip property getters
        .Where(m => !m.Name.StartsWith("set_"))        // Skip property setters
        .Where(m => !m.IsSpecialName)                  // Skip operators, events
        .Where(m => !m.IsGenericMethodDefinition)      // Skip generics (for now)
        .Where(m => !IsTypeConstructor(m))             // Skip Bool(), Int(), etc.
        .ToList();
    
    // Step 3: Group by function name (handles overloads)
    var methodGroups = eligibleMethods.GroupBy(m => GetFunctionName(m));
    
    // Step 4: Create signatures for each overload
    foreach (var group in methodGroups)
    {
        var functionName = group.Key;
        var signatures = new List<FunctionSignature>();
        
        foreach (var method in group)
        {
            try
            {
                var signature = CreateFunctionSignature(method);
                signatures.Add(signature);
            }
            catch (Exception ex)  // ArgumentException, InvalidOperationException, etc.
            {
                // Log warning but continue (graceful degradation)
                Console.Error.WriteLine($"Warning: Skipping {exportType.Name}.{method.Name}: {ex.Message}");
            }
        }
        
        if (signatures.Count > 0)
            moduleOverloads.Functions[functionName] = signatures;
    }
    
    return moduleOverloads;
}
```

**Key Filters Explained**:

1. **`StartsWith("_")`**: Respects Python convention of private members
2. **`StartsWith("get_"/"set_")`**: C# property accessors aren't functions in Sharpy
3. **`IsSpecialName`**: Filters out compiler-generated methods (operators, event handlers)
4. **`IsGenericMethodDefinition`**: Generic methods like `Foo<T>()` are skipped (future work)
5. **`IsTypeConstructor`**: Functions like `Int()`, `Bool()` are type conversions, not builtins

**Error Handling Philosophy**: 
- Graceful degradation: If one method can't be mapped (e.g., uses an unsupported type), log a warning and continue
- Don't fail the entire discovery process for edge cases
- Users can still compile against the assembly; just some functions won't be available

---

### 4. `IsTypeConstructor(MethodInfo method)` - Type Constructor Detection

**Purpose**: Identifies and filters out type constructor functions that shouldn't be exposed as regular functions.

```csharp
private bool IsTypeConstructor(MethodInfo method)
{
    var typeConstructors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Bool", "Int", "Long", "Float", "Double", "Str", 
        "List", "Dict", "Set", "Tuple"
    };
    
    return typeConstructors.Contains(method.Name);
}
```

**Why?** Type constructors like `Int("123")` or `Bool(true)` are handled specially by the compiler's semantic analyzer, not as regular function calls. Including them in the index would create confusion and duplicate type conversion paths.

---

### 5. `GetFunctionName(MethodInfo method)` - Name Mangling (C# → Python)

**Purpose**: Converts C# `PascalCase` method names to Python `snake_case` conventions.

```csharp
private string GetFunctionName(MethodInfo method)
{
    var name = method.Name;
    
    // Handle acronyms: "XMLParser" → "xml_parser"
    name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
    
    // Handle camelCase: "parseXml" → "parse_xml"
    name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
    
    return name.ToLowerInvariant();
}
```

**Examples**:
- `Print` → `"print"`
- `GetUserById` → `"get_user_by_id"`
- `ParseXMLFile` → `"parse_xml_file"`
- `HTTPRequest` → `"http_request"`

**Regex Breakdown**:
1. `([A-Z]+)([A-Z][a-z])`: Matches sequences like "XML" in "XMLParser"
   - Captures "XML" and "Pa" separately
   - Replaces with "$1_$2" → "XML_Pa"
2. `([a-z])([A-Z])`: Matches lowercase-uppercase boundaries
   - "parse" and "Xml" → "parse_Xml" → "parse_xml"

**Critical Note**: This is **one-way** name mangling. The MethodToken stores the original C# name for rehydration.

---

### 6. `CreateFunctionSignature(MethodInfo method)` - Signature Extraction

**Purpose**: Converts a .NET `MethodInfo` into a serializable `FunctionSignature`.

```csharp
private FunctionSignature CreateFunctionSignature(MethodInfo method)
{
    var signature = new FunctionSignature
    {
        Name = GetFunctionName(method),                    // snake_case name
        ReturnType = CreateTypeSignature(method.ReturnType), // Map CLR → Sharpy type
        MethodToken = CreateMethodToken(method)            // Unique identifier
    };
    
    // Add parameters
    foreach (var param in method.GetParameters())
    {
        signature.Parameters.Add(CreateParameterSignature(param));
    }
    
    return signature;
}
```

**Why MethodToken?** The index is serialized to JSON/disk. When the compiler needs to actually call a method, it uses the `MethodToken` to look up the original `MethodInfo` via reflection. Think of it as a foreign key in a database.

---

### 7. `CreateParameterSignature(ParameterInfo param)` - Parameter Details

**Purpose**: Captures everything about a parameter: name, type, default value, variadic flag.

```csharp
private ParameterSignature CreateParameterSignature(ParameterInfo param)
{
    return new ParameterSignature
    {
        Name = param.Name ?? "arg",                          // Fallback for unnamed params
        Type = CreateTypeSignature(param.ParameterType),     // CLR → Sharpy type
        HasDefault = param.HasDefaultValue,                  // Optional parameter?
        DefaultValue = param.HasDefaultValue 
            ? ConvertDefaultValue(param.DefaultValue) 
            : null,
        IsVariadic = param.GetCustomAttribute<ParamArrayAttribute>() != null  // params[]?
    };
}
```

**Key Details**:

- **Default Values**: C# allows `void Foo(int x = 42)`. Sharpy needs to know about this for type checking.
- **Variadic Parameters**: C#'s `params int[] numbers` maps to Python's `*args` behavior.
- **ParamArrayAttribute**: This is the .NET metadata that marks a parameter as `params`.

**Edge Case**: If `param.Name` is null (rare but possible with dynamic code generation), it defaults to `"arg"`.

---

### 8. `CreateTypeSignature(Type clrType)` - Type Translation

**Purpose**: Translates CLR types to Sharpy's type system using the `TypeMapper`.

```csharp
private TypeSignature CreateTypeSignature(Type clrType)
{
    // Delegate to TypeMapper for actual conversion
    var semanticType = _typeMapper.MapClrTypeToSemanticType(clrType);
    
    var signature = new TypeSignature
    {
        Name = semanticType.GetDisplayName(),           // Human-readable name
        ClrTypeName = clrType.FullName ?? clrType.Name  // For reverse lookup
    };
    
    // Handle generic types (List<int>, Dict<str, int>, etc.)
    if (semanticType is GenericType genericType)
    {
        signature.IsGeneric = true;
        signature.TypeArguments = genericType.TypeArguments
            .Select(t => new TypeSignature
            {
                Name = t.GetDisplayName(),
                ClrTypeName = string.Empty  // Type arguments are logical, not CLR types
            })
            .ToList();
    }
    
    return signature;
}
```

**Type Mapping Examples**:
- `System.Int32` → `SemanticType.Int` → `TypeSignature { Name = "int", ClrTypeName = "System.Int32" }`
- `List<string>` → `GenericType { Name = "list", TypeArguments = [SemanticType.Str] }`
- `Dictionary<int, bool>` → `GenericType { Name = "dict", TypeArguments = [Int, Bool] }`

**Why Store ClrTypeName?** When loading from cache, the compiler needs to map back to CLR types for code generation (calling conventions, etc.).

---

### 9. `CreateMethodToken(MethodInfo method)` - Unique Identifier

**Purpose**: Generate a unique, stable identifier for a method that can be used to find it later via reflection.

```csharp
private string CreateMethodToken(MethodInfo method)
{
    var assemblyName = method.DeclaringType?.Assembly.GetName().Name ?? "Unknown";
    var typeName = method.DeclaringType?.FullName ?? "Unknown";
    var methodName = method.Name;
    var paramCount = method.GetParameters().Length;
    
    return $"{assemblyName}|{typeName}|{methodName}|{paramCount}";
}
```

**Format**: `AssemblyName|TypeFullName|MethodName|ParameterCount`

**Example**: 
```
"Sharpy.Core|Sharpy.Core.Exports|Print|1"
```

**Why Include Parameter Count?** Methods can be overloaded:
```csharp
public static void Print(object obj);
public static void Print(object obj, string end);
```

These produce different tokens:
- `Sharpy.Core|Sharpy.Core.Exports|Print|1`
- `Sharpy.Core|Sharpy.Core.Exports|Print|2`

**Limitation**: This doesn't distinguish overloads with the same arity but different types:
```csharp
void Foo(int x);
void Foo(string x);
```

Both would have `|Foo|1`. The current design assumes the cache system will store all overloads, and type checking resolves which one to call.

---

### 10. `ConvertDefaultValue(object? value)` - Default Value Serialization

**Purpose**: Convert .NET default parameter values into string representations suitable for caching.

```csharp
private string? ConvertDefaultValue(object? value)
{
    if (value == null || value == DBNull.Value)
        return null;
    
    return value switch
    {
        string s      => $"\"{s}\"",              // Quoted strings
        char c        => $"'{c}'",                // Quoted chars
        bool b        => b.ToString().ToLowerInvariant(),  // true/false (not True/False)
        int or long or short or byte or sbyte 
        or uint or ulong or ushort => value.ToString(),
        float f       => f.ToString("G9"),        // 9 significant digits
        double d      => d.ToString("G17"),       // 17 significant digits
        _             => null                     // Unsupported types
    };
}
```

**Format Choices**:

1. **Strings/Chars**: Include quotes so the value can be parsed back correctly
   - `"hello"` not `hello`
2. **Booleans**: Lowercase to match Python conventions
   - `true` not `True`
3. **Floats/Doubles**: Use `"G"` format for round-trip precision
   - `G9` for float: Enough precision to reconstruct original float
   - `G17` for double: Enough precision to reconstruct original double
4. **Unsupported Types**: Return `null` (parameter becomes required, not optional)

**Edge Case**: `DBNull.Value` is .NET's representation of SQL NULL, distinct from `null`. Treated as `null` here.

---

## Dependencies

### Internal Dependencies

1. **`Discovery.TypeMapper`** (not `CodeGen.TypeMapper`!)
   - Located in `src/Sharpy.Compiler/Discovery/TypeMapper.cs`
   - Handles CLR → SemanticType conversion
   - Thread-safe with internal caching

2. **`AssemblyIdentity`**
   - Uniquely identifies assemblies with version + content hash
   - Used for cache invalidation (if assembly changes, rebuild index)

3. **`SemanticType` hierarchy**
   - Base class for Sharpy's type system
   - Includes `BuiltinType`, `GenericType`, `NullableType`, etc.

### External Dependencies

1. **`System.Reflection`**
   - `Assembly`, `Type`, `MethodInfo`, `ParameterInfo`
   - Core .NET reflection API

2. **`System.Text.RegularExpressions`**
   - Used for PascalCase → snake_case conversion

### Relationship to Broader System

```
Compilation Pipeline:
┌─────────────────────────────────────────────┐
│  1. Source Code (.spy)                      │
│  2. Lexer → Tokens                          │
│  3. Parser → AST                            │
│  4. Semantic Analysis                       │
│     ├─ Name Resolution                      │
│     ├─ Type Resolution ◄──────────────┐    │
│     │   (needs function signatures)   │     │
│     └─ Type Checking                   │     │
│  5. Code Generation → C#                    │
│  6. Roslyn → IL                             │
└─────────────────────────────────────────────┘
                                          │
                          ┌───────────────┘
                          │
              ┌───────────▼───────────┐
              │  OverloadIndexCache   │ ◄─── Loads cached index
              │  (in-memory cache)    │
              └───────────┬───────────┘
                          │
                          │ Cache miss?
                          │
              ┌───────────▼──────────────┐
              │  OverloadIndexBuilder    │ ◄─── This File
              │  (builds from assembly)  │
              └──────────────────────────┘
```

**When is this used?**
- Compiler startup: Build indices for `Sharpy.Core` and imported assemblies
- Module import: `from mylib import foo` triggers discovery if not cached
- Cache invalidation: Assembly changed? Rebuild index

---

## Patterns and Design Decisions

### 1. **The "Exports" Convention**

**Pattern**: All public APIs must be in a `public static class Exports`.

**Rationale**:
- Clear boundary between public API and internal implementation
- Static methods map cleanly to Python's module-level functions
- Easy to discover via reflection (just look for classes named "Exports")

**Trade-off**: Requires discipline from library authors, but provides consistency.

---

### 2. **Graceful Degradation with Error Handling**

**Pattern**: Catch exceptions during signature creation, log warnings, continue processing.

```csharp
try
{
    var signature = CreateFunctionSignature(method);
    signatures.Add(signature);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Warning: Skipping {exportType.Name}.{method.Name}: {ex.Message}");
}
```

**Rationale**:
- Some methods might use types the compiler doesn't support yet
- Better to partially succeed than fail completely
- Allows incremental development of type system support

**Alternative**: Fail fast and force all methods to be mappable. Rejected because it's too brittle.

---

### 3. **Caching TypeMapper Instance**

**Pattern**: Single `TypeMapper` instance reused across all method discoveries.

```csharp
private readonly TypeMapper _typeMapper = new();
```

**Rationale**:
- `TypeMapper` internally caches CLR → SemanticType mappings
- Reusing it avoids redundant type mapping work
- Thread-safe (uses `ConcurrentDictionary` internally)

**Performance Impact**: For a large assembly with 1000+ methods, this can save 30-50% of discovery time.

---

### 4. **Method Tokens for Rehydration**

**Pattern**: Store a string token instead of serializing entire `MethodInfo`.

**Rationale**:
- `MethodInfo` is not serializable
- Tokens are human-readable and debuggable
- Can reconstruct `MethodInfo` via reflection when needed

**Trade-off**: Requires reflection again at load time, but reflection is already fast in .NET.

---

### 5. **Special Case for "builtins" Module**

**Pattern**: Hardcode `Sharpy.Core.Exports` → `"builtins"`.

**Rationale**:
- Python compatibility: `print`, `len`, `range` should be builtins, not `sharpy_core.print`
- User expectations: Newcomers expect builtins to work without imports
- Consistency: `from builtins import print` mirrors Python

**Alternative**: Use namespace-based naming for everything. Rejected because it's not Pythonic.

---

### 6. **Skip Generic Method Definitions**

**Pattern**: Filter out `IsGenericMethodDefinition`.

```csharp
.Where(m => !m.IsGenericMethodDefinition)
```

**Rationale**:
- Generic type inference is complex and not yet implemented
- Methods like `T Max<T>(IEnumerable<T> items)` require type argument inference
- Future work: Support generics with explicit type arguments

**Limitation**: Users can't call generic methods from Sharpy yet.

---

## Debugging Tips

### 1. **Enable Discovery Logging**

The code writes warnings to `Console.Error`. To see them during compilation:

```bash
dotnet run --project src/Sharpy.Cli -- build myfile.spy 2>&1 | grep "Warning: Skipping"
```

This shows which methods couldn't be discovered and why.

---

### 2. **Inspect Generated Index**

The `OverloadIndex` is serialized to JSON (gzipped). To inspect:

```bash
# Find cache directory (usually ~/.sharpy/cache or similar)
# Decompress and pretty-print
gzip -d < sharpy_core-*.json.gz | jq .
```

Look for:
- Missing functions you expected
- Incorrect type mappings
- Wrong module names

---

### 3. **Debug Type Mapping Issues**

If a function isn't discovered, the likely culprit is `TypeMapper.MapClrTypeToSemanticType()`. Add a breakpoint there and inspect:
- What CLR type is being passed?
- Does `PrimitiveCatalog` have an entry for it?
- Is it a generic type that's not handled?

---

### 4. **Test Specific Assembly**

Create a minimal test case:

```csharp
// TestAssembly.cs
namespace TestLib;
public static class Exports
{
    public static int Add(int a, int b) => a + b;
}

// Test discovery
var assembly = Assembly.LoadFrom("TestLib.dll");
var builder = new OverloadIndexBuilder();
var index = builder.BuildFromAssembly(assembly);

// Inspect results
Console.WriteLine($"Modules: {string.Join(", ", index.Modules.Keys)}");
foreach (var (name, overloads) in index.Modules)
{
    Console.WriteLine($"  {name}: {string.Join(", ", overloads.Functions.Keys)}");
}
```

---

### 5. **Method Token Issues**

If a cached method can't be rehydrated, the token might be wrong. Debug:

```csharp
// Print token during creation
Console.WriteLine($"Token: {CreateMethodToken(method)}");

// Try to rehydrate manually
var parts = token.Split('|');
var assembly = Assembly.Load(parts[0]);
var type = assembly.GetType(parts[1]);
var methods = type.GetMethods().Where(m => m.Name == parts[2] && m.GetParameters().Length == int.Parse(parts[3]));
```

If this fails, the token format needs adjustment.

---

### 6. **Name Mangling Edge Cases**

Test name conversion with edge cases:

```csharp
var testCases = new[]
{
    ("Print", "print"),
    ("GetUserById", "get_user_by_id"),
    ("ParseXMLFile", "parse_xml_file"),
    ("HTTPRequest", "http_request"),
    ("XMLHTTPRequest", "xmlhttp_request"),  // Multiple acronyms
    ("IOError", "io_error")
};

foreach (var (input, expected) in testCases)
{
    var actual = GetFunctionName(CreateDummyMethod(input));
    Console.WriteLine($"{input} → {actual} (expected: {expected})");
}
```

---

## Contribution Guidelines

### What Changes Might You Make?

#### 1. **Add Support for Generic Methods**

**Current Limitation**: Generic methods are skipped.

**Task**: Remove the `IsGenericMethodDefinition` filter and handle generic type parameters.

**Considerations**:
- How to represent generic constraints in `FunctionSignature`?
- How to serialize generic type parameters?
- Update `MethodToken` to include type parameter count

**Test Case**:
```csharp
public static T Max<T>(IEnumerable<T> items) where T : IComparable<T>;
```

Should produce:
```python
def max(items: list[T]) -> T where T: IComparable[T]: ...
```

---

#### 2. **Improve Type Constructor Detection**

**Current**: Hardcoded list of type names.

**Better**: Check if return type matches name (e.g., `Int()` returns `int`).

```csharp
private bool IsTypeConstructor(MethodInfo method)
{
    var typeConstructors = new HashSet<string> { "Bool", "Int", ... };
    
    // Enhanced check: Does return type match name?
    return typeConstructors.Contains(method.Name) &&
           method.ReturnType.Name.Equals(method.Name, StringComparison.OrdinalIgnoreCase);
}
```

---

#### 3. **Support Instance Methods on Sharpy Types**

**Current**: Only static methods in `Exports` classes.

**Enhancement**: Discover instance methods on types like `Sharpy.Core.List<T>`.

**Considerations**:
- How to represent `self` parameter?
- How to namespace these methods (module vs. type methods)?
- Update `DeriveModuleName` to handle type-based modules

---

#### 4. **Better Default Value Serialization**

**Current**: Unsupported types return `null`, making parameter required.

**Enhancement**: Support more types (enums, arrays, custom structs).

```csharp
private string? ConvertDefaultValue(object? value)
{
    // ... existing cases ...
    
    // Add enum support
    if (value is Enum enumValue)
        return $"{enumValue.GetType().Name}.{enumValue}";
    
    // Add array support
    if (value is Array array)
        return $"[{string.Join(", ", array.Cast<object>().Select(ConvertDefaultValue))}]";
    
    return null;
}
```

---

#### 5. **Optimize Discovery Performance**

**Current**: Sequential processing of methods.

**Enhancement**: Parallelize discovery using `Parallel.ForEach`.

```csharp
var methodGroups = eligibleMethods
    .AsParallel()
    .GroupBy(m => GetFunctionName(m));
```

**Caution**: Ensure `TypeMapper` is thread-safe (it already is via `ConcurrentDictionary`).

---

#### 6. **Add Diagnostic Metadata**

**Enhancement**: Include more information for IDE support (doc comments, attributes).

```csharp
public class FunctionSignature
{
    // Existing fields...
    public string? DocComment { get; set; }         // XML doc comment
    public List<string> Attributes { get; set; }    // [Obsolete], etc.
}

// In CreateFunctionSignature:
signature.DocComment = ExtractDocComment(method);
signature.Attributes = method.GetCustomAttributes()
    .Select(a => a.GetType().Name)
    .ToList();
```

**Use Case**: IDEs can show function documentation on hover.

---

### Testing New Features

1. **Add Unit Tests**: Test discovery logic in isolation
   ```csharp
   [Fact]
   public void TestDiscoverGenericMethod() { ... }
   ```

2. **Integration Tests**: Test with real assemblies
   ```csharp
   var assembly = typeof(Sharpy.Core.Exports).Assembly;
   var index = builder.BuildFromAssembly(assembly);
   Assert.Contains("builtins", index.Modules.Keys);
   ```

3. **Manual Testing**: Compile Sharpy code that uses the new feature
   ```python
   # test.spy
   from builtins import max
   result = max([1, 2, 3])  # Test generic method
   print(result)
   ```

---

### Code Style

- Follow existing patterns (error handling, naming conventions)
- Add XML doc comments for public methods
- Keep methods focused (single responsibility)
- Use descriptive variable names (`eligibleMethods`, not `m`)

---

### Performance Considerations

- **Reflection is expensive**: Cache results where possible
- **Regex is slow**: Consider precompiling regex patterns if performance becomes an issue
- **Memory**: Large assemblies can have 10,000+ methods. Ensure index doesn't bloat memory.

---

## Summary

`OverloadIndexBuilder` is the bridge between .NET's reflection system and Sharpy's type system. It:

1. **Discovers** functions in .NET assemblies via reflection
2. **Translates** C# conventions to Python conventions (naming, types)
3. **Caches** the results for fast subsequent compilations
4. **Handles** edge cases gracefully without breaking the entire build

**Key Takeaway**: This isn't just a reflection wrapper—it's an intentional design that enforces Sharpy's "Exports" convention while maintaining Python ergonomics.

When debugging compilation issues with imported functions, this is often the first place to look. Understanding how it works will help you diagnose type resolution errors, missing functions, and cache invalidation problems.

---

## Further Reading

- **`OverloadIndex.cs`**: The data structures this builder creates
- **`OverloadIndexCache.cs`**: How indices are cached and retrieved
- **`Discovery/TypeMapper.cs`**: CLR → Sharpy type mapping logic
- **`Semantic/TypeChecker.cs`**: How discovered functions are used during type checking
- **Architecture docs**: `docs/architecture/semantic-analyzer-architecture.md`

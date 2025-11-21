# Walkthrough: OverloadIndexBuilder.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`

---

## 1. Overview

### What This File Does

The `OverloadIndexBuilder` is responsible for **discovering and indexing all public functions** exposed by a .NET assembly, creating a searchable catalog of function signatures that the Sharpy compiler can use for type checking and code generation. Think of it as a "phone book" builder for all the functions that Sharpy programs can call.

### Role in the Overall Project

When Sharpy code imports modules (like `from builtins import len, print`), the compiler needs to:
1. Know what functions are available in the imported module
2. Understand their type signatures (parameter types, return types)
3. Resolve function calls during compilation
4. Perform type checking on arguments

The `OverloadIndexBuilder` **scans compiled .NET assemblies** (like `Sharpy.Core.dll`) and extracts this information, building an `OverloadIndex` that can be cached and reused across compilations for performance.

### Key Insight

This is part of Sharpy's **module discovery and caching system**. Instead of using slow .NET reflection every time the compiler runs, we build an index once and serialize it. This provides a **4-7x performance improvement** for repeated compilations.

---

## 2. Class/Type Structure

### Main Class: `OverloadIndexBuilder`

```csharp
public class OverloadIndexBuilder
{
    private readonly TypeMapper _typeMapper = new();
    
    // Main entry point
    public OverloadIndex BuildFromAssembly(Assembly assembly)
    
    // Helper methods (explained below)
    private string DeriveModuleName(Type exportType)
    private ModuleOverloads DiscoverModuleFunctions(Type exportType)
    private bool IsTypeConstructor(MethodInfo method)
    private string GetFunctionName(MethodInfo method)
    private FunctionSignature CreateFunctionSignature(MethodInfo method)
    private ParameterSignature CreateParameterSignature(ParameterInfo param)
    private TypeSignature CreateTypeSignature(Type clrType)
    private string CreateMethodToken(MethodInfo method)
    private string? ConvertDefaultValue(object? value)
}
```

### Dependencies (Data Structures)

The builder creates instances of these types (defined in `OverloadIndex.cs`):

- **`OverloadIndex`**: Top-level container
  - `AssemblyIdentity Identity`: Which assembly was scanned
  - `DateTime CreatedAt`: When the index was built
  - `Dictionary<string, ModuleOverloads> Modules`: Map of module names → their functions

- **`ModuleOverloads`**: Functions in a single module
  - `string ModuleName`: e.g., "builtins", "math"
  - `Dictionary<string, List<FunctionSignature>> Functions`: Map of function names → overloads

- **`FunctionSignature`**: A single function overload
  - `string Name`: Function name (snake_case)
  - `List<ParameterSignature> Parameters`: Parameter list
  - `TypeSignature ReturnType`: What the function returns
  - `string MethodToken`: Unique identifier for rehydration

- **`ParameterSignature`**: A single parameter
  - `string Name`: Parameter name
  - `TypeSignature Type`: Parameter type
  - `bool HasDefault`: Whether parameter has a default value
  - `string? DefaultValue`: Default value (if any)
  - `bool IsVariadic`: Whether this is a `*args` parameter

- **`TypeSignature`**: Type information
  - `string Name`: Display name (e.g., `"list[int]"`)
  - `bool IsGeneric`: Whether type is generic
  - `List<TypeSignature> TypeArguments`: Generic type arguments
  - `string ClrTypeName`: Full CLR type name for mapping back

---

## 3. Key Functions/Methods

### 3.1 `BuildFromAssembly(Assembly assembly)` - Main Entry Point

**What it does**: Scans an assembly and builds a complete index of all exported functions.

**Algorithm**:
1. Create identity record (assembly name, version, etc.)
2. Find all classes named "Exports" (convention for Sharpy modules)
3. For each Exports class:
   - Derive module name from namespace
   - Discover all public static methods
   - Group methods by name (for overloading)
   - Create function signatures
4. Return complete index

**Key implementation detail**:
```csharp
var exportTypes = assembly.GetTypes()
    .Where(t => t.Name == "Exports" && t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed)
    .ToList();
```

The filters are crucial:
- `IsAbstract && IsSealed`: In C#, this means the class is **static**
- Only static classes can have the public static methods we want to discover

**How it fits into the broader codebase**: This is called by `OverloadIndexCache` when building or refreshing the cache for a .NET assembly.

---

### 3.2 `DeriveModuleName(Type exportType)` - Module Name Resolution

**What it does**: Converts a .NET namespace into a Sharpy module name.

**Examples**:
- `Sharpy.Core.Exports` → `"builtins"` (special case)
- `Sharpy.Core.Math.Exports` → `"sharpy_core_math"`
- `Sharpy.Core.Random.Exports` → `"sharpy_core_random"`

**Why this matters**: Sharpy imports use module names:
```python
from builtins import len, print
from math import sqrt, pi
```

The builder needs to map the C# namespace structure to Sharpy's module system.

---

### 3.3 `DiscoverModuleFunctions(Type exportType)` - Function Discovery

**What it does**: Extracts all public static methods from an Exports class and groups them by function name.

**Algorithm**:
1. Get all public static methods using reflection
2. Filter out unwanted methods:
   - Methods starting with `_` (internal)
   - Property accessors (`get_`, `set_`)
   - Special compiler-generated methods
   - Generic method definitions (not yet supported)
   - Type constructors (like `Int()`, `Bool()`)
3. Group methods by name (for overload resolution)
4. Create function signatures for each overload
5. Handle mapping failures gracefully (log warnings, skip problematic methods)

**Important filtering logic**:
```csharp
var eligibleMethods = methods
    .Where(m => !m.Name.StartsWith("_"))          // No private/internal
    .Where(m => !m.Name.StartsWith("get_"))       // No property getters
    .Where(m => !m.Name.StartsWith("set_"))       // No property setters
    .Where(m => !m.IsSpecialName)                 // No compiler-generated
    .Where(m => !m.IsGenericMethodDefinition)     // No generics (yet)
    .Where(m => !IsTypeConstructor(m))            // No type constructors
    .ToList();
```

**Error handling**: The method uses try-catch around signature creation to gracefully skip methods that can't be mapped (e.g., unsupported types). Warnings are printed to stderr.

---

### 3.4 `IsTypeConstructor(MethodInfo method)` - Type Constructor Detection

**What it does**: Identifies whether a method is a type constructor function (like `Bool()`, `Int()`, `Str()`).

**Why we skip these**: Type constructors are handled specially by the compiler's semantic analyzer and code generator. They're not regular functions, so they shouldn't be in the overload index.

**Implementation**:
```csharp
var typeConstructors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Bool", "Int", "Long", "Float", "Double", "Str", "List", "Dict", "Set", "Tuple"
};

return typeConstructors.Contains(method.Name);
```

---

### 3.5 `GetFunctionName(MethodInfo method)` - Name Conversion

**What it does**: Converts C# PascalCase method names to Python snake_case.

**Examples**:
- `Print` → `print`
- `GetItem` → `get_item`
- `XMLParser` → `xml_parser` (handles acronyms correctly)

**Algorithm**: Uses regular expressions:
1. Handle acronyms: `XMLParser` → `XML_Parser`
2. Handle camelCase: `getItem` → `get_Item`
3. Convert to lowercase: → `get_item`

**Implementation**:
```csharp
// Handle sequences of capitals followed by lowercase (acronyms)
name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
// Handle lowercase followed by uppercase
name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
return name.ToLowerInvariant();
```

**Why this matters**: Sharpy follows Python conventions (snake_case), but .NET uses PascalCase. This bridge is essential for the Pythonic API.

---

### 3.6 `CreateFunctionSignature(MethodInfo method)` - Signature Creation

**What it does**: Builds a complete `FunctionSignature` object from a .NET `MethodInfo`.

**Components**:
1. **Name**: Converted to snake_case
2. **Parameters**: List of `ParameterSignature` objects
3. **Return type**: `TypeSignature` for return value
4. **Method token**: Unique identifier for later lookup

**Key detail**: This uses the `TypeMapper` to convert CLR types to Sharpy semantic types:
```csharp
ReturnType = CreateTypeSignature(method.ReturnType)
```

The method token format:
```
AssemblyName|TypeName|MethodName|ParamCount
Example: "Sharpy.Core|Sharpy.Core.Exports|Print|1"
```

This token allows the compiler to **rehydrate** the actual `MethodInfo` later during compilation without re-scanning the assembly.

---

### 3.7 `CreateParameterSignature(ParameterInfo param)` - Parameter Processing

**What it does**: Extracts all relevant information about a function parameter.

**Information captured**:
- **Name**: Parameter name (or "arg" if missing)
- **Type**: Sharpy type signature
- **HasDefault**: Whether parameter is optional
- **DefaultValue**: String representation of default value
- **IsVariadic**: Whether this is a `params` parameter (`*args`)

**Variadic detection**:
```csharp
IsVariadic = param.GetCustomAttribute<ParamArrayAttribute>() != null
```

This maps C#'s `params object[] values` to Python's `*values`.

**Default value handling**: Calls `ConvertDefaultValue()` to serialize default values as strings for later parsing.

---

### 3.8 `CreateTypeSignature(Type clrType)` - Type Mapping

**What it does**: Converts a .NET `Type` to a Sharpy `TypeSignature` using the `TypeMapper`.

**Process**:
1. Use `TypeMapper` to convert CLR type → Sharpy semantic type
2. Extract display name (e.g., `"list[int]"`)
3. Store CLR type name for reverse mapping
4. If generic, recursively process type arguments

**Example**:
```csharp
// CLR: List<int>
// Semantic type: GenericType { Name = "list", TypeArguments = [SemanticType.Int] }
// TypeSignature: { Name = "list[int]", IsGeneric = true, TypeArguments = [...] }
```

**Why we store both names**:
- `Name`: For display in error messages and IDE tooltips
- `ClrTypeName`: For mapping back to the actual .NET type during code generation

---

### 3.9 `CreateMethodToken(MethodInfo method)` - Unique Method Identifier

**What it does**: Creates a unique string identifier for a method that can be used to find it again later.

**Format**:
```
{AssemblyName}|{FullTypeName}|{MethodName}|{ParameterCount}
```

**Example**:
```
"Sharpy.Core|Sharpy.Core.Builtins.Exports|Print|1"
```

**Why parameter count is included**: Handles method overloading. Multiple methods can have the same name but different parameter counts:
```csharp
Print()
Print(object value)
Print(object value1, object value2)
```

**How it's used later**: The compiler can deserialize this token and use reflection to retrieve the original `MethodInfo` without re-scanning the entire assembly.

---

### 3.10 `ConvertDefaultValue(object? value)` - Default Value Serialization

**What it does**: Converts a .NET default value into a string representation that can be serialized and later parsed.

**Supported types**:
- `null` / `DBNull.Value` → `null`
- `string` → Quoted: `"hello"`
- `char` → Single-quoted: `'x'`
- `bool` → Lowercase: `true`, `false`
- Integers → As-is: `42`
- Floats → High precision: `3.14159265` (G9 format)
- Doubles → High precision: `3.141592653589793` (G17 format)
- Other types → `null` (unsupported)

**Example**:
```csharp
// C# method: void Greet(string name = "World")
// Stored as: DefaultValue = "\"World\""
```

**Why high precision for floats**: Ensures exact round-trip conversion when the compiler parses the default value back.

---

## 4. Dependencies

### Internal Dependencies

1. **`TypeMapper`** (`Discovery/TypeMapper.cs`):
   - Maps CLR types to Sharpy semantic types
   - Handles primitives, generics, nullables, arrays
   - Thread-safe with internal caching

2. **`OverloadIndex` and related types** (`Discovery/Caching/OverloadIndex.cs`):
   - Data structures for storing discovered signatures
   - JSON-serializable for disk caching

3. **`AssemblyIdentity`** (`Discovery/Caching/AssemblyIdentity.cs`):
   - Captures assembly metadata (name, version, hash)
   - Used for cache invalidation

4. **Semantic types** (`Semantic/*.cs`):
   - `SemanticType`, `GenericType`, `NullableType`, etc.
   - Represent types in Sharpy's type system

### External Dependencies

1. **`System.Reflection`**: Assembly and type introspection
2. **`System.Text.RegularExpressions`**: PascalCase → snake_case conversion

---

## 5. Patterns and Design Decisions

### 5.1 Convention-Based Discovery

**Pattern**: Instead of attributes or configuration, Sharpy uses naming conventions:
- All modules export through a static class named `Exports`
- Functions are public static methods
- Module name derived from namespace

**Benefits**:
- No need for special attributes
- Easy to understand and implement
- Natural .NET structure

**Example**:
```csharp
// File: src/Sharpy.Core/Math/Exports.cs
namespace Sharpy.Core.Math;

public static class Exports
{
    public static double Sqrt(double x) => System.Math.Sqrt(x);
    public static double Pow(double x, double y) => System.Math.Pow(x, y);
}
```

Sharpy code:
```python
from math import sqrt, pow
```

### 5.2 Graceful Degradation

**Pattern**: If a method can't be mapped (unsupported type, etc.), log a warning and skip it rather than failing the entire build.

**Implementation**:
```csharp
try
{
    var signature = CreateFunctionSignature(method);
    signatures.Add(signature);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Warning: Skipping {method.Name}: {ex.Message}");
}
```

**Why**: Sharpy is evolving. Some .NET features aren't supported yet. Graceful degradation allows partial functionality while features are being built.

### 5.3 Separation of Concerns

**Pattern**: The builder only **discovers** and **maps** types. It doesn't:
- Load assemblies (that's `CachedModuleDiscovery`)
- Serialize/deserialize indices (that's `OverloadIndexCache`)
- Use the index for type checking (that's `SemanticAnalyzer`)

**Why**: Single responsibility principle. Each class has a clear, focused job.

### 5.4 Immutable Data Structures

**Pattern**: Once built, an `OverloadIndex` is never modified. It's a snapshot.

**Why**: 
- Thread-safe by design
- Safe to cache and share
- Prevents bugs from unexpected mutations

### 5.5 Method Tokens for Rehydration

**Pattern**: Instead of storing full `MethodInfo` objects (which aren't serializable), store a **token** that can recreate them.

**Why**:
- `MethodInfo` can't be JSON-serialized
- Tokens are small and fast to serialize
- Allows lazy loading (only rehydrate methods actually used)

---

## 6. Debugging Tips

### 6.1 Verify Assembly Loading

**Problem**: No functions discovered from an assembly.

**Debug steps**:
1. Check that assembly has classes named `Exports`
2. Verify they're `public static` (abstract + sealed in IL)
3. Check methods are `public static`

```csharp
// Add at start of BuildFromAssembly:
Console.WriteLine($"Scanning assembly: {assembly.FullName}");
Console.WriteLine($"Found {exportTypes.Count} Exports classes");
```

### 6.2 Type Mapping Failures

**Problem**: Warning messages about skipped methods.

**Debug**:
1. Check `TypeMapper.MapClrTypeToSemanticType()` to see if the CLR type is supported
2. Look at the method signature—generic methods aren't supported yet
3. Check for unusual parameter types

**Add logging**:
```csharp
// In CreateFunctionSignature:
Console.WriteLine($"Mapping method: {method.DeclaringType?.Name}.{method.Name}");
Console.WriteLine($"  Return type: {method.ReturnType}");
foreach (var p in method.GetParameters())
    Console.WriteLine($"  Param: {p.Name} : {p.ParameterType}");
```

### 6.3 Method Token Mismatches

**Problem**: Can't rehydrate method from token later.

**Debug**:
1. Verify token format is correct: `Assembly|Type|Method|ParamCount`
2. Check parameter count matches (overload resolution)
3. Ensure assembly version hasn't changed (invalidates tokens)

**Verify token**:
```csharp
var token = CreateMethodToken(method);
Console.WriteLine($"Token: {token}");

// Later, try to parse it:
var parts = token.Split('|');
Console.WriteLine($"Assembly: {parts[0]}, Type: {parts[1]}, Method: {parts[2]}, Params: {parts[3]}");
```

### 6.4 Name Conversion Issues

**Problem**: Function names don't match expectations (e.g., `get_item` vs `getitem`).

**Debug**:
1. Test `GetFunctionName()` with various inputs
2. Check regex patterns for edge cases
3. Verify Sharpy code uses correct name

```csharp
// Test harness:
var testNames = new[] { "Print", "GetItem", "XMLParser", "IsValid" };
foreach (var name in testNames)
{
    Console.WriteLine($"{name} → {GetFunctionName(new MethodInfo with name)}");
}
```

### 6.5 Cache Invalidation

**Problem**: Changes to Exports class not reflected.

**Cause**: Cached index is stale.

**Solution**:
1. Delete cache file (usually in `.sharpy/cache/`)
2. Or force rebuild by changing assembly version
3. Check `AssemblyIdentity.FromAssembly()` for cache key logic

---

## 7. Contribution Guidelines

### 7.1 Adding Support for New Type Mappings

**When**: You want to support a new CLR type in Sharpy.

**Steps**:
1. Add mapping in `TypeMapper.MapClrTypeToSemanticType()`
2. Test with a method that uses the type
3. Verify signature is created correctly
4. Update tests in `Sharpy.Compiler.Tests`

**Example**: Adding `decimal` support:
```csharp
// In TypeMapper:
if (clrType == typeof(decimal)) return SemanticType.Decimal;
```

### 7.2 Filtering Out Unwanted Methods

**When**: Some methods shouldn't be exposed to Sharpy (internal helpers, etc.).

**Where**: Modify the filter chain in `DiscoverModuleFunctions()`:
```csharp
var eligibleMethods = methods
    .Where(m => !m.Name.StartsWith("_"))
    .Where(m => /* your new filter */)
    .ToList();
```

**Example**: Skip methods with certain attributes:
```csharp
.Where(m => m.GetCustomAttribute<InternalOnlyAttribute>() == null)
```

### 7.3 Improving Name Conversion

**When**: Edge cases in PascalCase → snake_case conversion.

**Where**: Modify regex in `GetFunctionName()`.

**Testing**: Add test cases covering:
- Single words: `Print` → `print`
- Camel case: `GetItem` → `get_item`
- Acronyms: `XMLParser` → `xml_parser`
- Numbers: `GetBase64` → `get_base64`

### 7.4 Supporting Generic Methods

**Status**: Currently skipped (`.Where(m => !m.IsGenericMethodDefinition)`).

**To implement**:
1. Remove the filter
2. Handle generic parameters in `CreateFunctionSignature()`
3. Map generic constraints
4. Test with generic methods like `Enumerate<T>()`

**Challenge**: Need to map generic type parameters correctly.

### 7.5 Enhancing Default Value Support

**When**: Need to support more complex default values (enums, arrays, etc.).

**Where**: Extend `ConvertDefaultValue()` with new cases:
```csharp
return value switch
{
    string s => $"\"{s}\"",
    Enum e => /* format enum value */,
    Array a => /* format array literal */,
    // ...
};
```

### 7.6 Performance Optimization

**Current performance**: Reflection is slow, but we cache the results.

**Potential improvements**:
1. **Parallel processing**: Use `Parallel.ForEach` for scanning multiple Exports classes
2. **Lazy loading**: Build index incrementally as modules are imported
3. **Incremental updates**: Only re-scan changed assemblies

**Measure first**:
```csharp
var stopwatch = Stopwatch.StartNew();
var index = builder.BuildFromAssembly(assembly);
stopwatch.Stop();
Console.WriteLine($"Built index in {stopwatch.ElapsedMilliseconds}ms");
```

### 7.7 Testing Best Practices

**When adding features**:
1. Add unit tests in `Sharpy.Compiler.Tests/Discovery/`
2. Test with real assemblies (like `Sharpy.Core.dll`)
3. Verify cache serialization/deserialization works
4. Test error cases (invalid assemblies, missing types)

**Example test**:
```csharp
[Fact]
public void BuildFromAssembly_DiscoversPrintFunction()
{
    var assembly = typeof(Sharpy.Core.Exports).Assembly;
    var builder = new OverloadIndexBuilder();
    
    var index = builder.BuildFromAssembly(assembly);
    
    Assert.True(index.Modules.ContainsKey("builtins"));
    var builtins = index.Modules["builtins"];
    Assert.True(builtins.Functions.ContainsKey("print"));
}
```

---

## Summary

The `OverloadIndexBuilder` is a **critical piece of Sharpy's module system**, bridging .NET assemblies and Sharpy's type system. It:

- **Discovers** all public functions in .NET assemblies using reflection
- **Maps** CLR types to Sharpy semantic types
- **Converts** naming conventions (PascalCase → snake_case)
- **Serializes** function signatures for caching
- **Enables** fast module imports through caching

Understanding this file is essential for:
- Adding new builtin functions to Sharpy
- Supporting new type mappings
- Debugging import resolution issues
- Optimizing compilation performance

**Next steps for learning**:
1. Read `OverloadIndexCache.cs` to see how indices are cached
2. Read `CachedModuleDiscovery.cs` to see how indices are used
3. Read `TypeMapper.cs` to understand type mapping in depth
4. Experiment by adding a new function to `Sharpy.Core` and watching it get discovered

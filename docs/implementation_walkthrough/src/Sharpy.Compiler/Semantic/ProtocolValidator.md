# Walkthrough: ProtocolValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`

---

## Overview

`ProtocolValidator` is a key component of Sharpy's semantic analysis phase that validates **protocol usage** in Sharpy code. In Python (and Sharpy), protocols define how objects behave with certain operations through "dunder methods" (double-underscore methods like `__len__`, `__iter__`, `__getitem__`).

### What Are Protocols?

Protocols in Sharpy/Python are informal interfaces that define expected behaviors:
- `__len__` → object can be used with `len()`
- `__iter__` → object is iterable (usable in `for` loops)
- `__getitem__` → object supports indexing (`obj[0]`)
- `__contains__` → object supports membership testing (`x in obj`)

### The Bridge Between Python and .NET

This validator is particularly interesting because it bridges **two worlds**:
1. **Sharpy's Python-style protocols** (dunder methods)
2. **.NET's CLR interfaces** (`IEnumerable<T>`, `IList<T>`, etc.)

This dual support enables seamless interop: you can use .NET collections in Sharpy code with Python-style syntax, and vice versa.

### Role in Compilation Pipeline

```
Source (.spy) → Lexer → Parser → Semantic Analysis → CodeGen
                                        ↓
                              ProtocolValidator validates:
                              • len(obj) works
                              • for x in obj works
                              • obj[i] works
```

---

## Class Structure

```csharp
public class ProtocolValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    private readonly ClrMemberCache _clrMemberCache;
    private readonly Dictionary<Type, HashSet<string>> _clrProtocolCache;
    
    public IReadOnlyList<SemanticError> Errors => _errors;
}
```

### Fields Explained

- **`_symbolTable`**: Contains all symbols (variables, functions, types) discovered during name resolution. Used to look up user-defined types and their methods.

- **`_logger`**: Logs errors/warnings during compilation. Uses `NullLogger.Instance` if none provided (for testing).

- **`_errors`**: Accumulates semantic errors found during validation. Consumers can inspect these after validation completes.

- **`_clrMemberCache`**: Shared cache for CLR type metadata discovered via reflection. Avoids repeated expensive reflection calls.

- **`_clrProtocolCache`**: **Per-type cache** mapping CLR types to the set of protocols they support. Built lazily on first access.

### Thread Safety Note

**CRITICAL**: This class is **NOT thread-safe**. This is intentional and consistent with other semantic analyzers (`OperatorValidator`, `TypeChecker`). Each compilation runs in a single thread with its own validator instances.

---

## Key Methods

### 1. `HasProtocol(SemanticType, string)` - The Core Check

```csharp
public bool HasProtocol(SemanticType type, string dunderName)
```

**Purpose**: Determines if a type supports a specific protocol (dunder method).

**Algorithm**: This method implements a **priority cascade**:

```
1. Check Sharpy built-in types (str, tuples)
   ↓
2. Check generic container types (list[T], dict[K,V], set[T])
   ↓
3. Check user-defined Sharpy types (class methods)
   ↓
4. Check CLR types via reflection (IEnumerable<T>, IList<T>, etc.)
```

**Why This Order?** 
- Built-in checks are fastest (simple type comparisons)
- User-defined checks use cached symbol table data
- CLR reflection is slowest, so it's last

#### Example Flow

When checking if `list[int]` supports `__getitem__`:

```csharp
// Step 1: Not str or tuple → continue
// Step 2: Generic type check
if (type is GenericType generic)
{
    return generic.Name switch
    {
        "list" => dunderName is "__getitem__" or "__setitem__" ... ✓
    }
}
```

When checking if `System.Collections.Generic.List<int>` supports `__iter__`:

```csharp
// Steps 1-3: No match
// Step 4: CLR type check
var clrType = GetClrType(type);  // Gets System.Collections.Generic.List<int>
return HasClrProtocol(clrType, "__iter__");  // Checks IEnumerable<T>
```

---

### 2. `HasClrProtocol(Type, string)` - .NET Interop Magic

```csharp
private bool HasClrProtocol(Type clrType, string dunderName)
```

**Purpose**: Determines protocol support for CLR types by examining their interfaces.

**Caching Strategy**: Uses `_clrProtocolCache` to avoid repeated reflection:

```csharp
if (!_clrProtocolCache.TryGetValue(clrType, out var protocols))
{
    protocols = DiscoverClrProtocols(clrType);  // Expensive reflection
    _clrProtocolCache[clrType] = protocols;      // Cache for reuse
}
return protocols.Contains(dunderName);
```

**Performance Consideration**: The first check for a CLR type is slow (reflection), but subsequent checks are O(1) hash lookups.

---

### 3. `DiscoverClrProtocols(Type)` - Reflection Deep Dive

```csharp
private HashSet<string> DiscoverClrProtocols(Type clrType)
```

**Purpose**: Uses reflection to discover which protocols a CLR type supports based on its interfaces.

**Interface → Protocol Mapping**:

| .NET Interface | Sharpy Protocols Added |
|----------------|------------------------|
| `IIterable<T>` (Sharpy.Core) | `__iter__` |
| `IEnumerable<T>` or `IEnumerable` | `__iter__` |
| `Iterator<T>` (Sharpy.Core base class) | `__iter__` |
| `ICollection<T>` or `ICollection` | `__len__`, `__contains__` |
| `IList<T>` or `IList` | `__getitem__`, `__setitem__` |
| `IDictionary<K,V>` or `IDictionary` | `__getitem__`, `__setitem__`, `__contains__`, `__len__` |
| Any type (base Object) | `__str__`, `__hash__` |

#### Special Case: Iterator Detection

The code checks not just interfaces but also **base classes**:

```csharp
var currentType = clrType;
while (currentType != null)
{
    if (currentType.IsGenericType &&
        currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
    {
        protocols.Add("__iter__");
        break;
    }
    currentType = currentType.BaseType;
}
```

This handles cases where a class **extends** `Iterator<T>` rather than implementing `IIterable<T>`.

**Hardcoded Type Names**: Note the use of string comparisons (`"Sharpy.Core.Iterator`1"`). This is pragmatic but fragile - type names must match exactly. Consider using `typeof()` comparisons when possible.

---

### 4. `ValidateLen(SemanticType, int, int)` - len() Builtin

```csharp
public SemanticType ValidateLen(SemanticType containerType, int line, int column)
```

**Purpose**: Validates that a type can be used with `len()` builtin.

**Example Usage in Compiler**:
```python
# Sharpy code
x = len([1, 2, 3])
```

During semantic analysis:
```csharp
var containerType = GetType(expression);  // list[int]
var resultType = validator.ValidateLen(containerType, line, column);  // Returns int
```

**Error Handling**: If the type lacks `__len__`, adds a helpful error suggesting `ISized` interface:

```
Type 'MyClass' does not support len() (missing '__len__' method). 
Consider implementing ISized interface.
```

**Return Type**: Always returns `SemanticType.Int` (or `Unknown` on error). In Python, `len()` always returns an integer.

---

### 5. `ValidateIteration(SemanticType, int, int)` - For Loops

```csharp
public SemanticType ValidateIteration(SemanticType iterableType, int line, int column)
```

**Purpose**: Validates that a type is iterable and infers the element type.

**Why Element Type Matters**:
```python
# Sharpy code
for x in [1, 2, 3]:  # x should be inferred as int
    print(x)
```

**Type Inference Logic**:

1. **Generic types**: Extract first type argument
   ```csharp
   if (iterableType is GenericType generic && generic.TypeArguments.Count > 0)
   {
       return generic.TypeArguments[0];  // list[int] → int
   }
   ```

2. **Tuples**: Return first element type (simplified)
   ```csharp
   if (iterableType is TupleType tuple && tuple.ElementTypes.Count > 0)
   {
       return tuple.ElementTypes[0];
   }
   ```

3. **Strings**: Characters are strings
   ```csharp
   if (iterableType == SemanticType.Str)
   {
       return SemanticType.Str;  // Iterating "hello" yields strings
   }
   ```

4. **Iterator types**: Extract `T` from `Iterator<T>`
   ```csharp
   var elementType = GetIteratorElementType(builtin.ClrType);
   if (elementType != null)
   {
       return MapClrTypeToSemanticType(elementType);
   }
   ```

**Special Case: Dictionaries**:
```python
# Iterating a dict yields keys
for key in {"a": 1, "b": 2}:  # key is str, not int
    print(key)
```

The code correctly returns the **first** type argument for dicts (the key type).

---

### 6. `ValidateIndexAccess(SemanticType, SemanticType, int, int)` - Subscripting

```csharp
public SemanticType ValidateIndexAccess(
    SemanticType containerType,
    SemanticType indexType,
    int line,
    int column)
```

**Purpose**: Validates `obj[index]` syntax and infers the result type.

**Example Scenarios**:

```python
x = [1, 2, 3][0]        # list[int] indexed by int → int
y = {"a": 1}["a"]       # dict[str, int] indexed by str → int
z = (1, "hello", 3.14)[1]  # tuple indexed by int → str (ideally)
```

**Type Inference Logic**:

- **Dictionaries**: Return the **value type** (second type argument)
  ```csharp
  if (generic.Name == "dict" && generic.TypeArguments.Count > 1)
  {
      return generic.TypeArguments[1];  // dict[str, int] → int
  }
  ```

- **Lists/Sets**: Return the element type (first type argument)
  ```csharp
  if (generic.TypeArguments.Count > 0)
  {
      return generic.TypeArguments[0];  // list[int] → int
  }
  ```

- **Tuples**: Returns first element type (simplified)
  ```csharp
  // TODO: This is imprecise!
  if (containerType is TupleType tuple && tuple.ElementTypes.Count > 0)
  {
      return tuple.ElementTypes[0];  // Should track exact index
  }
  ```

**Known Limitation**: For heterogeneous tuples like `(int, str, bool)`, indexing should ideally return the **exact type** based on the index. Current implementation returns the first type, which is a **simplification**. This is noted in the code with a comment.

**TODO Comment**: The code notes that index type validation is missing:
```csharp
// TODO: Validate index type matches container expectations:
// - list/tuple: index should be int (or slice)
// - dict: index should be compatible with key type
```

This means currently you could write `[1,2,3]["invalid"]` and it wouldn't be caught here.

---

### 7. `ValidateMembership(SemanticType, SemanticType, int, int)` - 'in' Operator

```csharp
public SemanticType ValidateMembership(
    SemanticType containerType,
    SemanticType itemType,
    int line,
    int column)
```

**Purpose**: Validates the `in` operator (membership testing).

**Example**:
```python
if 5 in [1, 2, 3, 4, 5]:  # Valid
    print("found")

if "x" in 42:  # Error: int doesn't support __contains__
    print("won't work")
```

**Return Type**: Always returns `bool` (membership tests are boolean).

**TODO Note**: The code mentions validating that `itemType` is compatible with the container's element type. Currently skipped for simplicity.

---

### 8. `ValidateBoolConversion(SemanticType, int, int)` - Truthiness

```csharp
public SemanticType ValidateBoolConversion(SemanticType type, int line, int column)
```

**Purpose**: Validates that a type can be used in boolean contexts (`if`, `while` conditions).

**Python's Truthiness Rules**:
1. If type has `__bool__`, use it
2. If type has `__len__`, truthy if `len > 0`
3. Otherwise, all objects are truthy (except `None`)

**Current Implementation**: Always returns `bool`, never errors. This is because **all types can be used in boolean context** in Python/Sharpy.

**Design Decision**: This method is "informational" - it documents the behavior but doesn't actually enforce anything. Future enhancements might add warnings for suspicious boolean conversions.

---

### 9. Helper Methods

#### `GetClrType(SemanticType)`

Maps Sharpy's semantic types to underlying CLR types:

```csharp
return type switch
{
    BuiltinType builtin => builtin.ClrType,
    UserDefinedType udt => udt.Symbol?.ClrType,
    GenericType generic => generic.GenericDefinition?.ClrType,
    _ => null
};
```

**Why It's Needed**: To perform reflection on .NET types.

#### `GetIteratorElementType(Type)`

Extracts the element type `T` from `Iterator<T>` or its subclasses:

```csharp
while (currentType != null)
{
    if (currentType.IsGenericType &&
        currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
    {
        return currentType.GetGenericArguments()[0];
    }
    currentType = currentType.BaseType;
}
```

**TODO Note**: This is duplicated in `Discovery/TypeMapper.cs`. Consolidation is planned.

#### `MapClrTypeToSemanticType(Type)`

Basic mapping from CLR types to Sharpy semantic types:

```csharp
if (clrType == typeof(int)) return SemanticType.Int;
if (clrType == typeof(string)) return SemanticType.Str;
// etc.
```

**Known Limitation**: Missing coverage for `char`, `byte`, `short`, `uint`, `ulong`, `decimal`, etc. Marked with a TODO comment.

---

## Dependencies

### Internal Dependencies

1. **`SymbolTable`** (`Semantic/SymbolTable.cs`)
   - Stores all symbols discovered during name resolution
   - Used to look up user-defined types and their methods

2. **`ClrMemberCache`** (`Semantic/ClrMemberCache.cs`)
   - Caches CLR type metadata discovered via reflection
   - Shared across validators to avoid redundant reflection calls

3. **`SemanticType`** hierarchy (`Semantic/SemanticType.cs`)
   - `BuiltinType`, `GenericType`, `UserDefinedType`, `TupleType`, etc.
   - Represents types during semantic analysis

4. **`SemanticError`** (`Semantic/SemanticError.cs`)
   - Records error messages with location information

5. **`ICompilerLogger`** (`Logging/ICompilerLogger.cs`)
   - Logs errors and warnings during compilation

### External Dependencies

- **`System.Reflection`**: For CLR type introspection
- **.NET BCL interfaces**: `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IDictionary<K,V>`
- **Sharpy.Core**: References `Iterator<T>` and `IIterable<T>` from the standard library

---

## Patterns and Design Decisions

### 1. Lazy Caching Strategy

The validator uses **lazy initialization** for CLR protocol discovery:

```csharp
if (!_clrProtocolCache.TryGetValue(clrType, out var protocols))
{
    protocols = DiscoverClrProtocols(clrType);  // Expensive
    _clrProtocolCache[clrType] = protocols;      // Cache
}
```

**Why?** 
- Reflection is slow
- Not all types are checked during compilation
- Pay the cost only when needed

### 2. Priority Cascade Pattern

`HasProtocol` checks types in order of **specificity and speed**:

```
Fast, specific checks (built-ins) → Slower, general checks (reflection)
```

This is a common pattern in compilers: handle special cases first, fall back to general mechanisms.

### 3. Dual-World Support

The validator bridges Python protocols and .NET interfaces seamlessly. Users don't need to know whether they're using Sharpy collections or .NET collections - they work the same way.

**Example**:
```python
# Both work identically
sharpy_list = [1, 2, 3]          # Sharpy list[int]
dotnet_list = List[int]([1,2,3]) # System.Collections.Generic.List<int>

for x in sharpy_list: ...  # Uses Sharpy's __iter__
for x in dotnet_list: ...  # Uses IEnumerable<T> via protocol mapping
```

### 4. Error Recovery

When validation fails, the validator:
1. Records an error with location info
2. Returns `SemanticType.Unknown`
3. **Does NOT throw exceptions**

This allows compilation to continue and find multiple errors in one pass (better UX).

### 5. Hardcoded Type Names

The code uses string comparisons for some type checks:

```csharp
if (i.GetGenericTypeDefinition().FullName == "Sharpy.Core.Collections.Interfaces.IIterable`1")
```

**Why?** 
- Can't use `typeof(IIterable<>)` if the type isn't directly referenced
- Avoids circular dependencies between compiler and runtime

**Drawback**: Fragile - type renames break silently.

### 6. TODO Comments as Architecture Documentation

The code includes several TODO comments that document:
- Known limitations
- Planned refactorings
- Design debt

Example:
```csharp
// TODO: This method is duplicated in Discovery/TypeMapper.cs. 
// Consider consolidating in Phase 7 (ClrMemberCache extraction).
```

These are **valuable** for understanding the codebase's evolution and planned improvements.

---

## Debugging Tips

### 1. Enable Logging

When instantiating `ProtocolValidator`, pass a logger to see validation decisions:

```csharp
var logger = new ConsoleLogger();  // Or your logger
var validator = new ProtocolValidator(symbolTable, logger);
```

Errors will be logged with line/column information as they're discovered.

### 2. Inspect the Cache

After validation, examine `_clrProtocolCache` to see what protocols were discovered:

```csharp
// In debugger
var protocols = validator._clrProtocolCache[typeof(List<int>)];
// Should contain: __iter__, __len__, __contains__, __getitem__, __setitem__, __str__, __hash__
```

### 3. Check Error Collection

After calling validation methods, inspect `validator.Errors`:

```csharp
var validator = new ProtocolValidator(symbolTable);
validator.ValidateLen(someType, line, col);

if (validator.Errors.Count > 0)
{
    foreach (var error in validator.Errors)
    {
        Console.WriteLine($"{error.Line}:{error.Column}: {error.Message}");
    }
}
```

### 4. Test Protocol Detection Independently

You can test protocol detection without full compilation:

```csharp
var validator = new ProtocolValidator(symbolTable);
bool canIterate = validator.HasProtocol(listType, "__iter__");
bool canIndex = validator.HasProtocol(listType, "__getitem__");
```

### 5. Watch for Reflection Performance

If protocol validation is slow, check if `DiscoverClrProtocols` is being called repeatedly for the same type. The cache should prevent this.

**Debugging Strategy**:
```csharp
private HashSet<string> DiscoverClrProtocols(Type clrType)
{
    Console.WriteLine($"Cache miss for {clrType.FullName}");  // Should see each type once
    // ... rest of method
}
```

### 6. Common Pitfalls

**Pitfall 1**: Forgetting to check `Errors` after validation
```csharp
var resultType = validator.ValidateLen(type, line, col);
// resultType might be Unknown! Check validator.Errors
```

**Pitfall 2**: Assuming thread safety
```csharp
// DON'T do this:
Parallel.ForEach(types, type => validator.HasProtocol(type, "__iter__"));
// Each thread needs its own validator instance
```

**Pitfall 3**: Not handling `SemanticType.Unknown`
```csharp
var elementType = validator.ValidateIteration(iterableType, line, col);
// elementType might be Unknown - handle it gracefully
if (elementType == SemanticType.Unknown)
{
    // Don't try to use it for further type inference
}
```

---

## Contribution Guidelines

### Adding New Protocol Support

**Example**: Adding `__reversed__` protocol for reverse iteration

1. **Update built-in type checks** (if applicable):
   ```csharp
   if (type == SemanticType.Str)
   {
       return dunderName is "__len__" or "__iter__" or "__reversed__";
   }
   ```

2. **Update generic type checks**:
   ```csharp
   "list" => dunderName is "__len__" or "__iter__" or "__reversed__",
   ```

3. **Add CLR interface mapping** (if applicable):
   ```csharp
   // In DiscoverClrProtocols
   if (_clrMemberCache.ImplementsInterface(clrType, typeof(IReversible<>)))
   {
       protocols.Add("__reversed__");
   }
   ```

4. **Add validation method**:
   ```csharp
   public SemanticType ValidateReversed(SemanticType type, int line, int column)
   {
       if (!HasProtocol(type, "__reversed__"))
       {
           AddError($"Type '{type.GetDisplayName()}' does not support reversed()", 
                    line, column);
           return SemanticType.Unknown;
       }
       // Return an iterator of the same element type
       return type; // Or construct appropriate iterator type
   }
   ```

5. **Add tests** (in `Sharpy.Compiler.Tests/Semantic/ProtocolValidatorTests.cs`):
   ```csharp
   [Fact]
   public void ValidateReversed_ListType_Succeeds()
   {
       var validator = new ProtocolValidator(symbolTable);
       var listType = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
       
       var result = validator.ValidateReversed(listType, 1, 1);
       
       Assert.Equal(0, validator.Errors.Count);
   }
   ```

### Improving Type Inference

**Current Limitation**: Tuple indexing returns first element type

**Improvement Path**:
1. Track constant index values in `ValidateIndexAccess`
2. For `TupleType`, check if index is a compile-time constant
3. Return the exact element type at that index

```csharp
// Enhanced version
if (containerType is TupleType tuple)
{
    if (indexType is ConstantIntType constIndex)
    {
        var index = constIndex.Value;
        if (index >= 0 && index < tuple.ElementTypes.Count)
        {
            return tuple.ElementTypes[index];  // Precise!
        }
    }
    // Fall back to first element type
    return tuple.ElementTypes[0];
}
```

### Adding Index Type Validation

Currently, index types aren't validated. To fix:

```csharp
// In ValidateIndexAccess, before checking __getitem__
if (containerType is GenericType generic && generic.Name == "list")
{
    if (indexType != SemanticType.Int)
    {
        AddError($"List indices must be integers, not {indexType.GetDisplayName()}", 
                 line, column);
    }
}
```

### Consolidating Duplicate Code

**TODOs mention duplicated code** in `TypeMapper.cs`. To consolidate:

1. Extract `GetIteratorElementType` and `MapClrTypeToSemanticType` to a shared utility class
2. Consider putting them in `ClrMemberCache` since they're reflection-related
3. Update both `ProtocolValidator` and `TypeMapper` to use the shared implementation

### Performance Optimization

**If protocol validation becomes a bottleneck**:

1. **Pre-populate cache for common types**:
   ```csharp
   public ProtocolValidator(SymbolTable symbolTable, ...)
   {
       // Pre-cache common CLR types
       _clrProtocolCache[typeof(List<>)] = DiscoverClrProtocols(typeof(List<>));
       _clrProtocolCache[typeof(Dictionary<,>)] = DiscoverClrProtocols(typeof(Dictionary<,>));
   }
   ```

2. **Use a shared global cache** (requires thread safety):
   ```csharp
   private static readonly ConcurrentDictionary<Type, HashSet<string>> 
       GlobalProtocolCache = new();
   ```

3. **Profile reflection calls** to identify slow interfaces checks

### Testing Recommendations

**Always test**:
- ✅ Built-in Sharpy types (`list`, `dict`, `str`, etc.)
- ✅ User-defined types with protocol methods
- ✅ CLR types from .NET BCL
- ✅ Edge cases (unknown types, missing protocols)
- ✅ Error messages are helpful and actionable

**Example test structure**:
```csharp
[Fact]
public void HasProtocol_ClrListType_SupportsIteration()
{
    var validator = new ProtocolValidator(symbolTable);
    var clrListType = new BuiltinType 
    { 
        Name = "List", 
        ClrType = typeof(List<int>) 
    };
    
    Assert.True(validator.HasProtocol(clrListType, "__iter__"));
}
```

### Code Style Guidelines

When contributing to this file:

1. **Maintain the priority cascade pattern** in `HasProtocol`
2. **Cache expensive operations** (reflection, symbol lookups)
3. **Add helpful error messages** that suggest fixes
4. **Document with TODO comments** for known limitations
5. **Return `SemanticType.Unknown` on errors**, don't throw
6. **Add XML doc comments** for public methods

---

## Integration with Broader Codebase

### How TypeChecker Uses ProtocolValidator

The `TypeChecker` (the main semantic analyzer) calls `ProtocolValidator` methods when analyzing expressions:

```csharp
// In TypeChecker.AnalyzeCallExpression for len()
if (functionName == "len")
{
    var argType = AnalyzeExpression(args[0]);
    return _protocolValidator.ValidateLen(argType, line, col);
}

// In TypeChecker.AnalyzeForStatement
var iterableType = AnalyzeExpression(forStmt.Iterable);
var elementType = _protocolValidator.ValidateIteration(iterableType, line, col);
// Use elementType for the loop variable
```

### Relationship with OperatorValidator

`OperatorValidator` validates **operators** (`+`, `-`, `*`, etc.), while `ProtocolValidator` validates **protocols** (dunder methods, iteration, indexing).

**Division of Responsibilities**:
- `OperatorValidator`: Binary operators, unary operators, comparison
- `ProtocolValidator`: Built-in functions, iteration, membership, indexing

**Shared Infrastructure**: Both use `ClrMemberCache` for reflection caching.

### Code Generation Impact

Protocol validation affects code generation:

```python
# Sharpy code
for x in my_list:  # Protocol validator checks __iter__
    print(x)
```

If `my_list` is a Sharpy `list[T]`, generates:
```csharp
foreach (var x in my_list) { ... }  // Uses IEnumerable<T>
```

If `my_list` is a .NET `List<T>`, generates the same code due to protocol mapping!

---

## Conclusion

`ProtocolValidator` is a sophisticated component that:

1. **Validates protocol usage** ensuring operations like `len()`, `for` loops, and indexing are type-safe
2. **Bridges Python and .NET** by mapping CLR interfaces to Python protocols
3. **Optimizes with caching** to minimize expensive reflection calls
4. **Provides helpful errors** to guide users toward correct protocol implementations

Understanding this file is key to:
- Working on Sharpy's semantic analysis phase
- Adding new protocol support
- Improving .NET interoperability
- Debugging type system issues

**Key Takeaways for Newcomers**:
- Protocols are Sharpy's way of defining object capabilities
- The validator checks both Sharpy dunder methods and .NET interfaces
- Caching is critical for performance with reflection
- Always check `validator.Errors` after validation
- This class is NOT thread-safe by design

When in doubt, look at the tests in `Sharpy.Compiler.Tests/Semantic/ProtocolValidatorTests.cs` for concrete usage examples!

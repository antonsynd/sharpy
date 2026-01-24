# Walkthrough: AstExtensions.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/AstExtensions.cs`

---

## 1. Overview

`AstExtensions.cs` is a small but critical utility file that provides extension methods to facilitate the migration of AST nodes from mutable collections (`List<T>`) to immutable collections (`ImmutableArray<T>`). This file exists to smooth the transition as the Sharpy compiler evolves toward fully immutable AST nodes.

### Role in the Compiler Pipeline

```
Parser creates AST nodes → Uses AstExtensions → Produces immutable AST → Semantic Analysis
```

When the parser constructs AST nodes (expressions, statements, patterns), it often builds lists of child nodes during parsing. These extensions convert those mutable lists into immutable arrays before storing them in AST record types.

**Key Insight**: This file is a migration aid. As the codebase stabilizes, direct use of `ImmutableArray<T>` builders will likely replace calls to `ToImmutableArraySafe`.

---

## 2. Class/Type Structure

The file defines a single static class with extension methods:

```csharp
public static class AstExtensions
{
    // Three extension methods for immutable array handling
}
```

### Why Static?

Extension methods must be defined in static classes. This allows the methods to be called as if they were instance methods on the types they extend (`List<T>`, `IEnumerable<T>`).

---

## 3. Key Methods

### 3.1 `ToImmutableArraySafe<T>(List<T>?)`

```csharp
public static ImmutableArray<T> ToImmutableArraySafe<T>(this List<T>? list)
    => list?.ToImmutableArray() ?? ImmutableArray<T>.Empty;
```

**Purpose**: Safely converts a nullable `List<T>` to an `ImmutableArray<T>`.

**Parameters**:
- `list` (nullable): The mutable list to convert, or `null`

**Returns**: 
- The list contents as an immutable array if `list` is not null
- An empty immutable array if `list` is null

**Key Implementation Details**:
- Uses null-conditional operator (`?.`) to avoid null reference exceptions
- Uses null-coalescing operator (`??`) to provide a safe default
- Returns `ImmutableArray<T>.Empty` rather than a null, preventing downstream null checks

**Usage Example** (from parser context):
```csharp
// During parsing, we build a list of statements
var statements = new List<Statement>();
statements.Add(new ExpressionStatement { ... });
statements.Add(new ReturnStatement { ... });

// Convert to immutable array for the AST node
return new FunctionDef 
{
    Body = statements.ToImmutableArraySafe()
};
```

**Why This Matters**:
- **Immutability**: Once AST nodes are created, they should never change. This prevents bugs where downstream phases accidentally modify the tree.
- **Thread Safety**: Immutable structures can be safely shared across threads without locks.
- **Null Safety**: Guarantees a valid (possibly empty) array rather than null.

---

### 3.2 `ToImmutableArraySafe<T>(IEnumerable<T>?)`

```csharp
public static ImmutableArray<T> ToImmutableArraySafe<T>(this IEnumerable<T>? items)
    => items?.ToImmutableArray() ?? ImmutableArray<T>.Empty;
```

**Purpose**: Safely converts any nullable `IEnumerable<T>` to an `ImmutableArray<T>`.

**Parameters**:
- `items` (nullable): Any enumerable sequence (array, LINQ query, collection), or `null`

**Returns**: 
- The enumerable contents as an immutable array if `items` is not null
- An empty immutable array if `items` is null

**When to Use This vs. List Version**:
- Use this when working with LINQ queries or other `IEnumerable<T>` sources
- Use the `List<T>` version when you've explicitly built a list during parsing

**Usage Example**:
```csharp
// Using LINQ to filter and convert
var publicMethods = allMembers
    .OfType<FunctionDef>()
    .Where(f => !f.Name.StartsWith("_"))
    .ToImmutableArraySafe();
```

---

### 3.3 `CreateBuilder<T>(int)`

```csharp
public static ImmutableArray<T>.Builder CreateBuilder<T>(int initialCapacity = 4)
    => ImmutableArray.CreateBuilder<T>(initialCapacity);
```

**Purpose**: Creates an `ImmutableArray<T>.Builder` for efficient incremental construction.

**Parameters**:
- `initialCapacity` (default 4): Initial capacity hint to avoid reallocations

**Returns**: 
- A builder that can efficiently accumulate items before producing an immutable array

**Why Use a Builder?**:
- **Performance**: If you're adding many items one at a time, a builder is more efficient than creating intermediate arrays
- **API Convenience**: Provides a familiar `Add()` method interface
- **Memory Efficiency**: Pre-allocates based on expected size

**Usage Example**:
```csharp
// Efficient way to build an immutable array
var builder = AstExtensions.CreateBuilder<Statement>(expectedCount: 10);

while (/* parsing statements */)
{
    var stmt = ParseStatement();
    builder.Add(stmt);
}

return new Module 
{
    Body = builder.ToImmutable()  // Convert builder to ImmutableArray
};
```

**Performance Note**: The default capacity of 4 is chosen as a reasonable small default. For larger collections, pass an explicit capacity hint.

---

## 4. Dependencies

### Internal Dependencies

**AST Node Types** (`Node.cs`, `Statement.cs`, `Expression.cs`):
- All AST record types use `ImmutableArray<T>` for child node collections
- Example from `Node.cs`:
  ```csharp
  public record Module : Node
  {
      public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
  }
  ```

**Parser** (`Parser/Parser.cs`):
- Primary consumer of these extension methods
- Builds lists during parsing, converts to immutable arrays when constructing AST nodes

### External Dependencies

**System.Collections.Immutable** (NuGet package):
- Provides the `ImmutableArray<T>` type and related infrastructure
- Part of the .NET BCL but requires explicit reference

---

## 5. Patterns and Design Decisions

### 5.1 Immutability Pattern

**Decision**: All AST nodes are implemented as C# 9.0 `record` types with `init` properties.

**Rationale**:
- **Correctness**: Prevents accidental mutation during semantic analysis or code generation
- **Debugging**: Immutable objects are easier to reason about (state doesn't change unexpectedly)
- **Functional Style**: Aligns with functional programming principles

**Migration Strategy**:
```csharp
// Old approach (mutable)
public class FunctionDef : Statement
{
    public List<Statement> Body { get; set; }  // ❌ Can be modified
}

// New approach (immutable)
public record FunctionDef : Statement
{
    public ImmutableArray<Statement> Body { get; init; }  // ✅ Cannot be modified after construction
}
```

### 5.2 Null Safety Pattern

**Decision**: Return empty collections instead of null.

**Before (problematic)**:
```csharp
ImmutableArray<T>? nodes = list?.ToImmutableArray();  // Could be null
foreach (var node in nodes)  // ❌ NullReferenceException if null
{
    // ...
}
```

**After (safe)**:
```csharp
ImmutableArray<T> nodes = list.ToImmutableArraySafe();  // Never null
foreach (var node in nodes)  // ✅ Safe, iterates zero times if empty
{
    // ...
}
```

**Rationale**: Eliminates entire classes of null reference bugs in downstream code.

### 5.3 Extension Method Pattern

**Decision**: Implement as extension methods rather than instance methods on `List<T>`.

**Rationale**:
- Cannot modify built-in .NET types (`List<T>` is sealed)
- Extensions provide natural call syntax: `list.ToImmutableArraySafe()`
- Keeps conversion logic localized to the AST namespace

---

## 6. Debugging Tips

### 6.1 Empty vs. Null Arrays

**Common Bug**: Treating `ImmutableArray<T>` default value as empty.

```csharp
ImmutableArray<Statement> body;  // ⚠️ Default value is NOT empty, it's invalid!

// This throws!
foreach (var stmt in body)  // InvalidOperationException: array not initialized
{
    // ...
}
```

**Solution**: Always initialize with `.Empty` or use `ToImmutableArraySafe()`.

```csharp
ImmutableArray<Statement> body = ImmutableArray<Statement>.Empty;  // ✅ Safe
ImmutableArray<Statement> body = list.ToImmutableArraySafe();       // ✅ Safe
```

**Debugging Check**:
```csharp
if (body.IsDefault)  // Check if uninitialized
{
    Console.WriteLine("Warning: ImmutableArray not initialized!");
}
```

### 6.2 Performance Debugging

**Issue**: Excessive allocations during parsing.

**How to Investigate**:
1. Profile with dotnet-trace or Visual Studio profiler
2. Look for repeated calls to `ToImmutableArraySafe()` in tight loops
3. Consider using `ImmutableArray<T>.Builder` for incremental construction

**Example Fix**:
```csharp
// Before (inefficient)
var statements = new List<Statement>();
for (int i = 0; i < 1000; i++)
{
    statements.Add(ParseStatement());
    // Don't convert on every iteration!
}

// After (efficient)
var builder = AstExtensions.CreateBuilder<Statement>(1000);
for (int i = 0; i < 1000; i++)
{
    builder.Add(ParseStatement());
}
var statements = builder.ToImmutable();
```

### 6.3 Visualizing AST in Debugger

**Tip**: `ImmutableArray<T>` displays poorly in some debuggers. Use `.ToArray()` for visualization:

```csharp
// In debugger watch window or immediate window
body.ToArray()  // Easier to inspect than ImmutableArray
```

---

## 7. Contribution Guidelines

### When to Modify This File

**Add a new method if**:
- You need a common conversion pattern across multiple parser files
- The method aids immutability migration
- It reduces boilerplate in AST construction

**Don't add a method if**:
- It's specific to one parser use case (keep it local)
- It doesn't relate to immutability or collection conversion
- It's better suited to a different utility class

### Making Changes

**Before modifying**:
1. Check if similar functionality already exists in `System.Collections.Immutable`
2. Grep for existing usages: `rg "ToImmutableArraySafe" --type cs`
3. Consider backward compatibility (are existing call sites affected?)

**After adding a method**:
1. Update this documentation
2. Add XML doc comments with clear examples
3. Consider adding tests in `Sharpy.Compiler.Tests/Parser/`
4. Run full test suite: `dotnet test`

### Example: Adding a New Helper

```csharp
/// <summary>
/// Converts a single item to an ImmutableArray with one element.
/// </summary>
public static ImmutableArray<T> ToSingletonArray<T>(this T item)
    => ImmutableArray.Create(item);
```

**Checklist**:
- ✅ Clear XML documentation
- ✅ Follows existing naming conventions
- ✅ Returns non-null `ImmutableArray<T>`
- ✅ Generic and reusable
- ✅ No side effects

---

## 8. Cross-References

### Related AST Files

This file is part of the AST infrastructure. To fully understand AST construction, see:

- **[Node.cs](./Node.md)** *(if documentation exists)* - Base `Node` type, `Module` root node
- **[Statement.cs](./Statement.md)** *(if documentation exists)* - All statement node types (if/while/class/function)
- **[Expression.cs](./Expression.md)** *(if documentation exists)* - All expression node types (literals/operations)
- **[Types.cs](./Types.md)** *(if documentation exists)* - Type annotation nodes
- **[Pattern.cs](./Pattern.md)** *(if documentation exists)* - Pattern matching nodes

### Parser Context

- **`Parser/Parser.cs`** - Main parser that constructs these AST nodes
- **`Lexer/Lexer.cs`** - Upstream: produces tokens consumed by parser

### Downstream Consumers

- **`Semantic/NameResolver.cs`** - Walks immutable AST to resolve names
- **`Semantic/TypeChecker.cs`** - Type-checks immutable AST nodes
- **`CodeGen/RoslynEmitter.cs`** - Generates C# from immutable AST

### Related Documentation

- **Main README**: `/README.md` - Compiler architecture overview
- **Compiler Guide**: `/.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
- **Testing Guide**: `/.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`

---

## Quick Reference Card

| Method | Input | Output | Use When |
|--------|-------|--------|----------|
| `ToImmutableArraySafe<T>(List<T>?)` | Nullable list | Non-null immutable array | Converting parser-built lists |
| `ToImmutableArraySafe<T>(IEnumerable<T>?)` | Nullable enumerable | Non-null immutable array | Converting LINQ results |
| `CreateBuilder<T>(int)` | Initial capacity | Array builder | Building arrays incrementally |

**Key Principles**:
- Always use these methods to ensure null safety
- Prefer builders for large collections
- Never mutate AST nodes after creation
- Return empty arrays, not null

---

## Further Reading

- **C# Records**: [Microsoft Docs - Records](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-9#record-types)
- **ImmutableArray**: [Microsoft Docs - ImmutableArray&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablearray-1)
- **AST Design Patterns**: "Compilers: Principles, Techniques, and Tools" (Dragon Book), Chapter 5

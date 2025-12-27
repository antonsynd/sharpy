# Walkthrough: AssemblyIdentity.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/AssemblyIdentity.cs`

---

## 1. Overview

`AssemblyIdentity` is a key component in Sharpy's module discovery and caching system. Its primary purpose is to **uniquely identify .NET assemblies** that the Sharpy compiler interacts with during compilation.

### What Problem Does It Solve?

When compiling Sharpy code that imports .NET libraries, the compiler needs to:
1. **Cache type information** from referenced assemblies to speed up subsequent compilations
2. **Detect when assemblies change** so stale caches can be invalidated
3. **Generate unique cache file names** for each assembly version

`AssemblyIdentity` captures the essential characteristics of an assembly (name, version, and content hash) to make these operations possible.

### Where It Fits in the Architecture

```
Sharpy Source (.spy)
    ↓
Parser & Semantic Analyzer
    ↓
Discovery System ← AssemblyIdentity (YOU ARE HERE)
    ↓
Cache Manager (uses AssemblyIdentity to generate cache keys)
    ↓
Type Information Cache
```

The Discovery system uses `AssemblyIdentity` to track which assemblies have been loaded and to generate consistent cache keys for storing/retrieving metadata.

---

## 2. Class Structure

`AssemblyIdentity` is a simple **data class** with four properties and several factory/utility methods.

### Properties

```csharp
public class AssemblyIdentity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
```

| Property | Purpose | Example Value |
|----------|---------|---------------|
| **Name** | Assembly name (e.g., "System.Core") | `"System.Collections"` |
| **Version** | Assembly version from metadata | `"8.0.0.0"` |
| **ContentHash** | SHA-256 hash of the assembly file | `"a3f2b1c..."` (64 hex chars) |
| **FilePath** | Full path to the assembly file | `"/usr/share/.../System.Collections.dll"` |

**Design Note**: The class uses auto-properties with default initializers (`= string.Empty`) to ensure properties are never null, following modern C# null-safety conventions.

---

## 3. Key Methods

### 3.1 `FromAssemblyPath(string assemblyPath)` - Factory Method

**Purpose**: Creates an `AssemblyIdentity` from a file path to a .NET assembly (`.dll` or `.exe`).

```csharp
public static AssemblyIdentity FromAssemblyPath(string assemblyPath)
{
    var assembly = Assembly.LoadFrom(assemblyPath);
    var assemblyName = assembly.GetName();

    return new AssemblyIdentity
    {
        Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
        Version = assemblyName.Version?.ToString() ?? "1.0.0",
        ContentHash = ComputeFileHash(assemblyPath),
        FilePath = assemblyPath
    };
}
```

**Key Points**:
- **Loads the assembly**: Uses `Assembly.LoadFrom()` which physically loads the assembly into the current AppDomain
- **Extracts metadata**: Gets the `AssemblyName` which contains name and version information
- **Fallback handling**: If `Name` is null, falls back to the filename; if `Version` is null, uses `"1.0.0"`
- **Content hashing**: Computes SHA-256 hash of the file to detect changes

**Use Case**: Called when the compiler encounters a reference to an external assembly by path:
```csharp
// Example usage in Discovery system:
var identity = AssemblyIdentity.FromAssemblyPath("/path/to/MyLibrary.dll");
```

**⚠️ Important**: This method **loads the assembly into memory**. Be aware of AppDomain pollution if called repeatedly.

---

### 3.2 `FromAssembly(Assembly assembly)` - Factory Method

**Purpose**: Creates an `AssemblyIdentity` from an already-loaded `Assembly` object.

```csharp
public static AssemblyIdentity FromAssembly(Assembly assembly)
{
    var assemblyName = assembly.GetName();
    var location = assembly.Location;

    return new AssemblyIdentity
    {
        Name = assemblyName.Name ?? "Unknown",
        Version = assemblyName.Version?.ToString() ?? "1.0.0",
        ContentHash = string.IsNullOrEmpty(location) ? string.Empty : ComputeFileHash(location),
        FilePath = location
    };
}
```

**Key Differences from `FromAssemblyPath`**:
- **No loading required**: Operates on an already-loaded assembly
- **Handles in-memory assemblies**: If `assembly.Location` is empty (e.g., dynamically generated assemblies), `ContentHash` becomes empty string
- **Fallback to "Unknown"**: Uses `"Unknown"` for the name if not available

**Use Case**: Called when working with assemblies already in the current AppDomain:
```csharp
// Example: Getting identity of the current assembly
var identity = AssemblyIdentity.FromAssembly(typeof(Compiler).Assembly);
```

---

### 3.3 `ToCacheKey()` - Cache Key Generation

**Purpose**: Generates a unique, filesystem-safe string to use as a cache filename.

```csharp
public string ToCacheKey()
{
    var hash = string.IsNullOrEmpty(ContentHash) 
        ? "no-hash" 
        : (ContentHash.Length > 12 ? ContentHash[..12] : ContentHash);
    return $"{Name.ToLowerInvariant()}-{Version}-{hash}.json.gz";
}
```

**Key Points**:
- **Format**: `{name}-{version}-{hash}.json.gz`
- **Hash truncation**: Only uses the first 12 characters of the hash for brevity
- **Lowercase normalization**: Converts name to lowercase for case-insensitive filesystems
- **Extension**: Assumes cache files are gzipped JSON (`.json.gz`)

**Examples**:
```csharp
// Full hash scenario
Name = "System.Collections"
Version = "8.0.0.0"
ContentHash = "a3f2b1c5d7e9..."
// Result: "system.collections-8.0.0.0-a3f2b1c5d7e9.json.gz"

// No hash scenario (in-memory assembly)
Name = "DynamicAssembly"
Version = "1.0.0"
ContentHash = ""
// Result: "dynamicassembly-1.0.0-no-hash.json.gz"
```

**Why 12 characters?** Provides a good balance between:
- **Uniqueness**: 12 hex chars = 48 bits, extremely unlikely to collide
- **Readability**: Keeps filenames manageable
- **Performance**: Shorter strings to compare

---

### 3.4 `ComputeFileHash(string filePath)` - Private Helper

**Purpose**: Computes the SHA-256 hash of an assembly file to detect content changes.

```csharp
private static string ComputeFileHash(string filePath)
{
    if (!File.Exists(filePath))
        return string.Empty;

    using var stream = File.OpenRead(filePath);
    using var sha256 = SHA256.Create();
    var hashBytes = sha256.ComputeHash(stream);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}
```

**Key Points**:
- **File existence check**: Returns empty string if file doesn't exist (defensive programming)
- **Streaming**: Reads file as a stream to handle large assemblies efficiently
- **SHA-256**: Cryptographically strong hash algorithm (though not used for security here)
- **Lowercase hex**: Ensures consistent formatting

**Why SHA-256?**
- **Change detection**: Even a single byte change results in a completely different hash
- **Collision resistance**: Virtually impossible for two different files to have the same hash
- **Standard library support**: Built into .NET, no external dependencies

**Performance Note**: Hashing can be slow for large assemblies (100+ MB). Consider caching these identities themselves if performance becomes an issue.

---

### 3.5 `Equals()` and `GetHashCode()` - Identity Comparison

**Purpose**: Enable comparing two `AssemblyIdentity` instances and using them in hash-based collections.

```csharp
public override bool Equals(object? obj)
{
    if (obj is not AssemblyIdentity other)
        return false;

    return Name == other.Name &&
           Version == other.Version &&
           ContentHash == other.ContentHash;
}

public override int GetHashCode()
{
    return HashCode.Combine(Name, Version, ContentHash);
}
```

**Key Points**:
- **Three-way equality**: Two identities are equal if name, version, AND hash match
- **FilePath excluded**: Path is not considered for equality (same assembly can exist at different paths)
- **Modern C# patterns**: Uses pattern matching (`is not`) and `HashCode.Combine()`

**Use Cases**:
```csharp
// Check if assembly has changed
var oldIdentity = cache.GetIdentity("System.Core");
var newIdentity = AssemblyIdentity.FromAssemblyPath("/path/to/System.Core.dll");
if (!oldIdentity.Equals(newIdentity))
{
    // Assembly changed, invalidate cache
}

// Store in HashSet or Dictionary
var processedAssemblies = new HashSet<AssemblyIdentity>();
processedAssemblies.Add(identity);
```

---

## 4. Dependencies

### External Dependencies (NuGet/Framework)

```csharp
using System.Reflection;           // Assembly, AssemblyName
using System.Security.Cryptography; // SHA256
using System.Text;                 // (Imported but not used - potential cleanup opportunity)
```

### Internal Dependencies

- **None directly**: This is a foundational class with no dependencies on other Sharpy compiler components
- **Used by**: Likely used by:
  - `Discovery/ModuleResolver.cs` (hypothetical)
  - `Discovery/Caching/CacheManager.cs` (hypothetical)
  - Any code that needs to track assembly versions

---

## 5. Patterns and Design Decisions

### 5.1 **Immutability via Convention**

The class has public setters but is designed to be **immutable after creation**:
- Factory methods create fully-initialized instances
- No methods modify properties after construction
- Consider making properties `init` only in a future refactor:

```csharp
public string Name { get; init; } = string.Empty;
```

### 5.2 **Factory Method Pattern**

Two factory methods (`FromAssemblyPath`, `FromAssembly`) provide different construction paths:
- **Advantage**: Encapsulates complex construction logic
- **Advantage**: Clear naming indicates what input is required
- **Alternative**: Could use constructor overloads, but factory methods are more expressive

### 5.3 **Defensive Programming**

Multiple safeguards against null/missing data:
- Null coalescing operators (`??`)
- Empty string defaults
- File existence checks
- Pattern: Prefer empty strings over null to simplify consumer code

### 5.4 **Hash Truncation Strategy**

Using only the first 12 characters of the hash is a **pragmatic trade-off**:
- **Pro**: Shorter, more readable cache filenames
- **Pro**: Still provides ~48 bits of entropy (collision probability: ~1 in 281 trillion)
- **Con**: Not suitable if you need cryptographic guarantees
- **Risk**: If two assemblies have the same name, version, AND matching first 12 hash chars → cache collision (astronomically unlikely)

---

## 6. Debugging Tips

### 6.1 Common Issues

**Problem**: Cache not invalidating when assembly changes
```csharp
// Debug: Print the hash to verify it's changing
var identity = AssemblyIdentity.FromAssemblyPath(path);
Console.WriteLine($"Hash for {identity.Name}: {identity.ContentHash}");
```

**Problem**: `Assembly.LoadFrom()` throws `FileNotFoundException`
- Check that the path is absolute, not relative
- Ensure the assembly's dependencies are available
- Use Fusion Log Viewer on Windows or `MONO_LOG_LEVEL=debug` on Unix

**Problem**: Different identities for the "same" assembly
```csharp
// Debug: Check all three components
Console.WriteLine($"Name: '{id1.Name}' vs '{id2.Name}'");
Console.WriteLine($"Version: '{id1.Version}' vs '{id2.Version}'");
Console.WriteLine($"Hash: '{id1.ContentHash}' vs '{id2.ContentHash}'");
```

### 6.2 Testing Considerations

**Unit Test Example**:
```csharp
[Fact]
public void ToCacheKey_GeneratesConsistentFilename()
{
    var identity = new AssemblyIdentity
    {
        Name = "Test.Assembly",
        Version = "1.2.3",
        ContentHash = "abcdef123456789"
    };
    
    Assert.Equal("test.assembly-1.2.3-abcdef123456.json.gz", identity.ToCacheKey());
}
```

**Integration Test Tip**: Use temporary assemblies created with `AssemblyBuilder` to avoid file system dependencies.

---

## 7. Contribution Guidelines

### 7.1 Potential Improvements

#### Performance Enhancement
```csharp
// Cache file hashes to avoid recomputing on every call
private static readonly ConcurrentDictionary<string, string> _hashCache = new();

private static string ComputeFileHash(string filePath)
{
    return _hashCache.GetOrAdd(filePath, path =>
    {
        if (!File.Exists(path)) return string.Empty;
        // ... existing hash computation
    });
}
```

#### Async Support
```csharp
public static async Task<AssemblyIdentity> FromAssemblyPathAsync(string assemblyPath)
{
    // Use async file I/O for hash computation
    var hash = await ComputeFileHashAsync(assemblyPath);
    // ...
}
```

#### Validation
```csharp
public void Validate()
{
    if (string.IsNullOrWhiteSpace(Name))
        throw new InvalidOperationException("Assembly name is required");
    if (string.IsNullOrWhiteSpace(Version))
        throw new InvalidOperationException("Assembly version is required");
}
```

### 7.2 What NOT to Change

- **Don't change the cache key format** without migration logic (would invalidate all existing caches)
- **Don't change hash algorithm** (SHA-256 → SHA-512) without careful consideration of performance impact
- **Don't make `FilePath` part of equality** (same assembly at different locations should be equal)

### 7.3 Code Style Conventions

This file follows Sharpy's conventions:
- **XML documentation** on public members (good example to follow)
- **Descriptive variable names** (`assemblyName`, not `an`)
- **Modern C# features** (pattern matching, range operator `[..12]`)
- **Explicit null handling** (prefer `??` over if-null checks)

### 7.4 Testing Requirements

When modifying this class:
1. **Add tests** for any new public methods
2. **Test edge cases**: null inputs, missing files, in-memory assemblies
3. **Verify cache key stability**: Ensure format changes don't break existing caches
4. **Performance test** hash computation with large assemblies (100+ MB)

---

## 8. Related Files

To understand the full context, explore these related files:

- **`Discovery/ModuleResolver.cs`**: Likely uses `AssemblyIdentity` to resolve .NET imports
- **`Discovery/Caching/CacheManager.cs`**: Probably stores/retrieves cached data using cache keys
- **`Compiler.cs`**: Main compilation entry point that kicks off discovery

---

## 9. Quick Reference

### When to Use Each Factory Method

| Scenario | Method to Use |
|----------|---------------|
| You have a file path string | `FromAssemblyPath(path)` |
| You have a loaded `Assembly` object | `FromAssembly(assembly)` |
| You need a cache filename | Call `.ToCacheKey()` on the identity |

### Common Patterns

```csharp
// Pattern 1: Create identity and generate cache key
var identity = AssemblyIdentity.FromAssemblyPath(dllPath);
var cacheFile = identity.ToCacheKey();

// Pattern 2: Compare assemblies
if (oldIdentity.Equals(newIdentity))
{
    // Assembly unchanged, use cache
}
else
{
    // Assembly changed, recompute
}

// Pattern 3: Track processed assemblies
var seen = new HashSet<AssemblyIdentity>();
if (seen.Add(identity))
{
    // First time seeing this assembly
}
```

---

## 10. Summary

`AssemblyIdentity` is a **small but critical** class that enables Sharpy's caching system to:
1. **Uniquely identify** .NET assemblies by name, version, and content
2. **Detect changes** via SHA-256 hashing
3. **Generate consistent** cache filenames

It's a great example of:
- ✅ **Single Responsibility Principle**: Does one thing well
- ✅ **Factory Pattern**: Clear, expressive object creation
- ✅ **Defensive Programming**: Handles edge cases gracefully
- ✅ **Modern C# Idioms**: Nullable reference types, pattern matching, records-style properties

For newcomers, this is an excellent file to study to understand how Sharpy handles .NET interop and caching strategies.

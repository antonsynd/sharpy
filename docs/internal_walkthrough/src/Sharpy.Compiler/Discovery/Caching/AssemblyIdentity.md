# Walkthrough: AssemblyIdentity.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/AssemblyIdentity.cs`

---

## Overview

`AssemblyIdentity.cs` defines a class that serves as a **unique identifier** for .NET assemblies in the Sharpy compiler's caching system. Think of it as a "fingerprint" for an assembly that combines multiple pieces of information to determine whether a cached compilation result is still valid or needs to be regenerated.

### Role in the Project

When the Sharpy compiler imports .NET assemblies (e.g., `System.Collections.Generic.dll`, third-party libraries, or previously compiled Sharpy modules), it needs to cache metadata about those assemblies to avoid reprocessing them on every compilation. `AssemblyIdentity` provides:

1. **Cache Key Generation**: Creates unique file names for cached assembly metadata
2. **Change Detection**: Uses content hashing to detect when an assembly has been modified
3. **Version Tracking**: Tracks assembly versions to handle version-specific caching
4. **Equality Comparison**: Enables proper comparison of assemblies to determine cache validity

This is part of the compiler's **Discovery/Caching** subsystem, which optimizes compilation speed by avoiding redundant work.

---

## Class/Type Structure

### `AssemblyIdentity` Class

```csharp
public class AssemblyIdentity
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
```

The class is a simple data container with four key properties:

| Property | Purpose | Example Value |
|----------|---------|---------------|
| **Name** | Assembly name without extension | `"System.Collections"` |
| **Version** | Assembly version string | `"7.0.0.0"` or `"1.0.0"` |
| **ContentHash** | SHA-256 hash of assembly file | `"a3f2e1..."` (64 hex chars) |
| **FilePath** | Full path to assembly file | `"/usr/share/.../System.Collections.dll"` |

**Key Design Decision**: This is a mutable class (not a record or struct) with auto-properties. This makes it easy to deserialize from cache files (JSON) and construct incrementally, though it sacrifices immutability guarantees.

---

## Key Functions/Methods

### 1. `FromAssemblyPath(string assemblyPath)` - Static Factory Method

**Purpose**: Creates an `AssemblyIdentity` from a file path to an assembly DLL.

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

**How It Works**:
1. **Loads the assembly** using `Assembly.LoadFrom()` - this actually loads the DLL into memory
2. **Extracts metadata** using `GetName()` which reads the assembly manifest
3. **Computes content hash** by reading the entire file and running SHA-256
4. **Falls back gracefully** if assembly name is null (uses file name without extension)

**Important Implementation Details**:
- Uses `Assembly.LoadFrom()` which can have side effects (assembly stays loaded in the app domain)
- Default version is `"1.0.0"` if version info is missing
- The content hash ensures that even if version numbers don't change, file modifications are detected

**Use Case**: When the compiler discovers a .NET assembly file path from imports or project references.

---

### 2. `FromAssembly(Assembly assembly)` - Alternative Factory Method

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

**How It Works**:
1. **Extracts assembly metadata** from the loaded assembly
2. **Uses `assembly.Location`** to find the file path (may be empty for in-memory assemblies)
3. **Handles in-memory assemblies** by setting empty ContentHash when location is unavailable

**Key Difference from `FromAssemblyPath`**:
- Doesn't load the assembly (assumes already loaded)
- Can handle in-memory or dynamic assemblies (those without a file location)
- Returns empty hash if no file path is available (less reliable for caching)

**Use Case**: When working with assemblies that are already loaded by the .NET runtime (e.g., BCL assemblies like `System.Runtime`).

---

### 3. `ToCacheKey()` - Cache Key Generation

**Purpose**: Generates a unique file name for storing cached metadata about this assembly.

```csharp
public string ToCacheKey()
{
    var hash = string.IsNullOrEmpty(ContentHash) 
        ? "no-hash" 
        : (ContentHash.Length > 12 ? ContentHash[..12] : ContentHash);
    return $"{Name.ToLowerInvariant()}-{Version}-{hash}.json.gz";
}
```

**How It Works**:
1. **Truncates hash** to first 12 characters for readability (SHA-256 is 64 chars, too long for filenames)
2. **Handles missing hash** by using `"no-hash"` placeholder
3. **Normalizes name** to lowercase to avoid case-sensitivity issues
4. **Appends `.json.gz`** extension indicating compressed JSON format

**Example Output**:
```
system.collections-7.0.0.0-a3f2e1d4b5c6.json.gz
sharpy.core-1.0.0-8e7d6c5b4a39.json.gz
myassembly-1.2.3-no-hash.json.gz
```

**Design Rationale**:
- **Human-readable**: You can tell what assembly the cache file represents
- **Collision-resistant**: Combination of name + version + hash makes collisions extremely unlikely
- **File-system safe**: Lowercase prevents issues on case-insensitive file systems
- **Short enough**: 12-char hash keeps filenames reasonable while maintaining uniqueness

**Use Case**: When storing or retrieving cached assembly metadata from disk.

---

### 4. `ComputeFileHash(string filePath)` - Private Helper

**Purpose**: Computes a SHA-256 hash of the assembly file to detect content changes.

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

**How It Works**:
1. **Validates file exists** - returns empty string if not (graceful degradation)
2. **Opens file as stream** - efficient for large assemblies
3. **Computes SHA-256** - cryptographically strong hash function
4. **Converts to hex string** - readable format (e.g., `"a3f2e1d4..."`)
5. **Lowercases result** - consistent with cache key format

**Performance Considerations**:
- Uses streaming instead of loading entire file into memory
- SHA-256 is fast enough for typical assembly sizes (< 10 MB)
- `using` statements ensure proper disposal of file handles and crypto resources

**Why SHA-256?**
- **Strong collision resistance**: Nearly impossible for two different files to have the same hash
- **Sensitive to changes**: Even a 1-byte change produces a completely different hash
- **Standard in .NET**: Well-supported with `System.Security.Cryptography`

---

### 5. `Equals(object? obj)` - Equality Comparison

**Purpose**: Determines if two `AssemblyIdentity` objects represent the same assembly.

```csharp
public override bool Equals(object? obj)
{
    if (obj is not AssemblyIdentity other)
        return false;

    return Name == other.Name &&
           Version == other.Version &&
           ContentHash == other.ContentHash;
}
```

**How It Works**:
1. **Type check** using pattern matching (`is not`)
2. **Compares three fields**: Name, Version, and ContentHash
3. **Ignores FilePath** - different paths can point to the same assembly copy

**Important**: Notice that `FilePath` is **NOT** included in equality comparison. This is intentional because:
- The same assembly might be copied to different locations
- What matters is the assembly's identity (name/version) and content, not where it lives
- Allows cache hits even if assembly location changes

**Use Case**: Checking if cached metadata is still valid for a given assembly.

---

### 6. `GetHashCode()` - Hash Code Generation

**Purpose**: Generates a hash code for use in hash-based collections (dictionaries, sets).

```csharp
public override int GetHashCode()
{
    return HashCode.Combine(Name, Version, ContentHash);
}
```

**How It Works**:
- Uses `HashCode.Combine()` utility from .NET to properly combine multiple fields
- Matches the fields used in `Equals()` (must be consistent)
- Excludes `FilePath` like `Equals()` does

**Why This Matters**:
- If `Equals()` returns true, `GetHashCode()` **must** return the same value
- Allows `AssemblyIdentity` to be used as dictionary keys or in hash sets
- Enables efficient cache lookups

---

## Dependencies

### .NET Framework Dependencies
- **`System.Reflection`**: For loading assemblies and extracting metadata
  - `Assembly.LoadFrom()`, `Assembly.GetName()`, `AssemblyName`
- **`System.Security.Cryptography`**: For SHA-256 hashing
  - `SHA256.Create()`, `ComputeHash()`
- **`System.Text`**: Imported but not directly used (might be for future extensions)
- **`System.IO`**: For file operations (`File.Exists`, `File.OpenRead`)

### Sharpy Compiler Dependencies
- **Part of `Sharpy.Compiler.Discovery.Caching` namespace**
- Likely used by:
  - `ModuleCache.cs` - For storing/retrieving cached assembly metadata
  - `ModuleDiscovery.cs` - For identifying discovered assemblies
  - Any code that needs to cache information about .NET assemblies

### No External NuGet Dependencies
This class uses only BCL (Base Class Library) types, making it lightweight and dependency-free.

---

## Patterns and Design Decisions

### 1. **Static Factory Methods Pattern**
Instead of public constructors, the class provides static factory methods:
- `FromAssemblyPath()` - When you have a file path
- `FromAssembly()` - When you have a loaded assembly

**Benefits**:
- Clear intent (method names describe what you're creating from)
- Can perform validation/computation during construction
- Can return null or throw exceptions without "incomplete" objects
- Easier to read: `AssemblyIdentity.FromAssemblyPath(path)` vs `new AssemblyIdentity(path)`

### 2. **Content-Based Identification**
The class uses **content hashing** rather than just name/version:

```csharp
ContentHash = ComputeFileHash(assemblyPath)
```

**Why?**: Version numbers can be unreliable:
- Developers might forget to increment versions
- Local builds might have same version as official releases
- Debug vs. Release builds might differ despite same version

Content hash ensures that **any change** to the assembly invalidates the cache.

### 3. **Graceful Degradation**
The code handles missing or invalid data gracefully:
```csharp
Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath)
Version = assemblyName.Version?.ToString() ?? "1.0.0"
ContentHash = string.IsNullOrEmpty(location) ? string.Empty : ComputeFileHash(location)
```

Rather than throwing exceptions, it provides sensible defaults. This makes the caching system resilient to edge cases.

### 4. **Value-Like Semantics with Reference Type**
`AssemblyIdentity` is a class but behaves like a value:
- Overrides `Equals()` and `GetHashCode()`
- Compares by value, not reference
- Can be used in collections as keys

**Why not a struct or record?**
- Needs to be serializable for caching (classes work better with JSON)
- Not performance-critical (few instances created)
- Future extensibility (can add methods/inheritance)

---

## Debugging Tips

### Common Issues and How to Debug Them

#### Issue 1: "Cache Not Being Used"
**Symptoms**: Compiler recompiles assemblies every time despite caching enabled.

**Debug Steps**:
1. Check if `ToCacheKey()` is generating stable keys:
   ```csharp
   var identity = AssemblyIdentity.FromAssemblyPath(path);
   Console.WriteLine($"Cache Key: {identity.ToCacheKey()}");
   ```
2. Verify content hash is being computed:
   ```csharp
   Console.WriteLine($"Hash: {identity.ContentHash}");
   ```
3. Check if file permissions allow reading assembly files
4. Ensure assembly isn't being modified between compilations

#### Issue 2: "Assembly.LoadFrom() Side Effects"
**Symptoms**: Assemblies stay loaded in app domain, causing issues with subsequent loads.

**Understanding**:
- `Assembly.LoadFrom()` actually loads the DLL into memory
- Multiple calls with same path return the same `Assembly` instance
- Can cause issues if assembly file is locked or modified

**Workaround**:
```csharp
// Consider using Assembly.ReflectionOnlyLoad() for metadata-only access
// Or use AssemblyName.GetAssemblyName() for just reading manifest
```

#### Issue 3: "Hash Mismatch on Same File"
**Symptoms**: Different hashes for the same assembly file.

**Possible Causes**:
- File timestamp changes (hash is content-based, should be immune)
- File is being modified during hashing (rare but possible)
- Different line endings (shouldn't affect binary DLLs)

**Debug**:
```csharp
var hash1 = ComputeFileHash(path);
var hash2 = ComputeFileHash(path);
Console.WriteLine($"Match: {hash1 == hash2}");
```

#### Issue 4: "In-Memory Assemblies Not Cached"
**Symptoms**: Some assemblies have empty ContentHash.

**Understanding**:
- `FromAssembly()` returns empty hash if `assembly.Location` is empty
- This happens for dynamic assemblies or those loaded from byte arrays
- These assemblies cannot be reliably cached by file

**Solution**:
- Accept that some assemblies won't have content-based caching
- Fall back to name/version-based caching
- Consider adding a flag to `AssemblyIdentity` indicating "uncacheable"

### Useful Debugging Extensions

Add these helper methods during debugging:

```csharp
public string ToDebugString()
{
    return $"Assembly: {Name} v{Version}\n" +
           $"Path: {FilePath}\n" +
           $"Hash: {ContentHash}\n" +
           $"Cache Key: {ToCacheKey()}";
}
```

---

## Contribution Guidelines

### Types of Changes Appropriate for This File

#### ✅ Good Contributions

1. **Performance Optimizations**
   - Cache hash computation results (currently recomputed if called multiple times)
   - Use `AssemblyName.GetAssemblyName()` instead of `Assembly.LoadFrom()` to avoid loading assemblies
   - Add async versions of methods for I/O-bound operations

2. **Enhanced Equality Comparison**
   - Add `IEquatable<AssemblyIdentity>` interface for type-safe equality
   - Implement comparison operators (`==`, `!=`)

3. **Better Error Handling**
   - Add specific exceptions for invalid assembly paths
   - Log warnings when assembly metadata is missing
   - Validate assembly files before attempting to load

4. **Serialization Support**
   - Add JSON serialization attributes for cleaner cache files
   - Implement `ISerializable` for custom serialization
   - Add XML doc comments for all properties

5. **Testing Improvements**
   - Add unit tests for edge cases (empty paths, invalid assemblies, etc.)
   - Test cache key uniqueness with collision scenarios
   - Benchmark hash computation performance

#### Example Contribution: Add IEquatable

```csharp
public class AssemblyIdentity : IEquatable<AssemblyIdentity>
{
    // ... existing code ...

    public bool Equals(AssemblyIdentity? other)
    {
        if (other is null)
            return false;

        return Name == other.Name &&
               Version == other.Version &&
               ContentHash == other.ContentHash;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AssemblyIdentity);
    }

    public static bool operator ==(AssemblyIdentity? left, AssemblyIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AssemblyIdentity? left, AssemblyIdentity? right)
    {
        return !Equals(left, right);
    }
}
```

#### ❌ Changes to Avoid

1. **Breaking Changes to Cache Key Format**
   - Changing `ToCacheKey()` format will invalidate all existing caches
   - If you must change it, implement migration logic

2. **Adding Heavy Dependencies**
   - This is a lightweight utility class
   - Avoid adding NuGet packages or complex dependencies

3. **Removing FilePath Property**
   - Even though it's not used in equality, other code might depend on it
   - Check usage across codebase first

4. **Making Properties Immutable**
   - Would break JSON deserialization
   - If you want immutability, consider creating a separate `readonly` interface

### Testing Your Changes

When modifying this file, test:

1. **Basic functionality**:
   ```bash
   dotnet test --filter "FullyQualifiedName~AssemblyIdentityTests"
   ```

2. **Integration with caching**:
   ```bash
   dotnet test --filter "FullyQualifiedName~ModuleCacheTests"
   ```

3. **Real-world scenario**:
   ```bash
   # Compile a project twice and verify cache is used
   sharpyc project samples/calculator_app/calculator.spyproj
   sharpyc project samples/calculator_app/calculator.spyproj
   ```

### Documentation Updates

If you modify this file, also update:
- This walkthrough document with your changes
- XML doc comments in the source code
- Any related architecture docs in `docs/architecture/`

---

## Further Reading

- **Related Files**:
  - `ModuleCache.cs` - Uses `AssemblyIdentity` for cache storage
  - `ModuleDiscovery.cs` - Creates `AssemblyIdentity` instances during discovery
  
- **Relevant Documentation**:
  - `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md` - General compiler contribution guide
  - `docs/architecture/` - Compiler architecture documentation

- **.NET Documentation**:
  - [Assembly.LoadFrom Method](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.loadfrom)
  - [SHA256 Class](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256)
  - [HashCode.Combine](https://learn.microsoft.com/en-us/dotnet/api/system.hashcode.combine)

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-21  
**Author**: Generated for Sharpy Compiler Project

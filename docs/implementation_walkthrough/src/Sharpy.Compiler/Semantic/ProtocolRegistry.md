# Walkthrough: ProtocolRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs`

---

## 1. Overview

`ProtocolRegistry.cs` is the **central registry** for Python-style protocol dunder methods in Sharpy. It serves as a bridge between three worlds:

1. **Sharpy source code** - Python-style dunder methods like `__len__`, `__str__`, `__contains__`
2. **Sharpy.Core interfaces** - Typed interfaces like `ISized`, `IContainer`, `IHashable`
3. **.NET CLR methods** - Native .NET methods like `get_Count`, `ToString`, `GetHashCode`

Think of it as a **Rosetta Stone** that maps between these three representations of the same semantic concepts. When the compiler encounters a `__len__` method in Sharpy code, it consults this registry to understand:
- What signature is expected?
- What Sharpy.Core interface should be implemented?
- What .NET method does this correspond to for interop?

**Key Distinction**: This registry handles **non-operator** dunders (protocols like `__len__`, `__str__`). Operator dunders (`__add__`, `__eq__`, etc.) are handled separately by `OperatorSignatureValidator`.

---

## 2. Class/Type Structure

### 2.1 `ProtocolKind` Enum

```csharp
public enum ProtocolKind
{
    Lifecycle,      // __init__, __del__, __new__
    Container,      // __len__, __contains__, __getitem__, __setitem__, __delitem__
    Iterator,       // __iter__, __next__
    Representation, // __str__, __repr__, __format__
    Hashing,        // __hash__
    Conversion      // __bool__, __int__, __float__, __complex__
}
```

**Purpose**: Categorizes protocols by their semantic purpose. This helps:
- Query protocols by category (e.g., "give me all container protocols")
- Organize documentation and error messages
- Understand the protocol's intent at a glance

### 2.2 `ProtocolInfo` Record

```csharp
public record ProtocolInfo(
    string DunderName,
    ProtocolKind Kind,
    string? SharpyCoreInterface,
    string? InterfaceMethodName,
    string? ClrMethodName,
    int ExpectedParamCount,
    string? ExpectedReturnType
);
```

This is the **core data structure** that describes everything about a protocol dunder. Let's break down each field:

| Field | Purpose | Example |
|-------|---------|---------|
| `DunderName` | Sharpy source name (lowercase) | `"__len__"` |
| `Kind` | Protocol category | `ProtocolKind.Container` |
| `SharpyCoreInterface` | Interface in `Sharpy.Core` that defines this protocol | `"ISized"` |
| `InterfaceMethodName` | Method name in the interface (PascalCase with dunder preserved) | `"__Len__"` |
| `ClrMethodName` | Equivalent .NET method/property name | `"get_Count"` (for the Count property) |
| `ExpectedParamCount` | How many parameters including `self` | `1` (just self) |
| `ExpectedReturnType` | Expected return type name | `"int"` |

**Nullable Fields**: Notice that `SharpyCoreInterface`, `InterfaceMethodName`, `ClrMethodName`, and `ExpectedReturnType` are nullable. This is intentional:
- Some protocols don't map to interfaces (e.g., `__init__` maps directly to constructors)
- Some protocols don't have direct CLR equivalents (e.g., `__delitem__`)
- Generic protocols don't have fixed return types (e.g., `__getitem__` returns the element type)

### 2.3 `ProtocolRegistry` Static Class

```csharp
public static class ProtocolRegistry
{
    private static readonly FrozenDictionary<string, ProtocolInfo> _protocols;
    
    static ProtocolRegistry() { ... }
}
```

**Design Choice**: This is a static class with a frozen dictionary. Why?
- **Immutable after initialization**: Protocols are fixed at compile time, so we use `FrozenDictionary` for optimal read performance
- **Static initialization**: The `static ProtocolRegistry()` constructor runs once when the class is first accessed
- **Thread-safe**: Frozen collections are inherently thread-safe for reads

---

## 3. Key Functions/Methods

### 3.1 Registration: `RegisterAllProtocols()`

```csharp
private static void RegisterAllProtocols(Dictionary<string, ProtocolInfo> protocols)
{
    // Lifecycle protocols
    Register(protocols, new ProtocolInfo(
        DunderName: "__init__",
        Kind: ProtocolKind.Lifecycle,
        SharpyCoreInterface: null,  // Special: maps to constructor
        InterfaceMethodName: null,
        ClrMethodName: ".ctor",
        ExpectedParamCount: -1,  // Variable (1+ including self)
        ExpectedReturnType: "None"
    ));
    
    // Container protocols
    Register(protocols, new ProtocolInfo(
        DunderName: "__len__",
        Kind: ProtocolKind.Container,
        SharpyCoreInterface: "ISized",
        InterfaceMethodName: "__Len__",
        ClrMethodName: "get_Count",
        ExpectedParamCount: 1,  // Just self
        ExpectedReturnType: "int"
    ));
    
    // ... more registrations
}
```

**What it does**: This is where all protocol mappings are defined. Each `Register()` call adds one protocol to the registry.

**Key Implementation Details**:

1. **Variable parameter counts**: `ExpectedParamCount: -1` means "any count ≥ 1". Used for `__init__` which can have arbitrary parameters beyond `self`.

2. **Special CLR mappings**:
   - `".ctor"` - Constructor (not a regular method)
   - `"get_Count"` - Property getter (not a method)
   - `"set_Item"` - Indexer setter
   - `"op_Explicit"` - Explicit cast operator

3. **Important comment about `__len__`** (lines 72-74):
   ```csharp
   // NOTE: ISized.__Len__() returns uint in Sharpy.Core, but we register "int" here
   // because that's the common Sharpy/Python return type for len(). The code generator
   // handles the uint-to-int conversion when emitting calls to __Len__().
   ```
   
   This reveals a **design compromise**: Python's `len()` returns `int`, but .NET's `Count` is often `uint`. The registry records the Sharpy expectation (`int`), and the code generator (`RoslynEmitter`) handles the conversion.

4. **Operator protocols are excluded** (lines 189-191):
   ```csharp
   // Note: __eq__ and __ne__ are handled by OperatorSignatureValidator as they
   // are comparison operators that map to .NET operator overloads.
   ```

### 3.2 Basic Queries

#### `GetProtocol(string dunderName)`
```csharp
public static ProtocolInfo? GetProtocol(string dunderName)
    => _protocols.GetValueOrDefault(dunderName);
```

**What it does**: Primary lookup method. Returns full protocol info or null.

**Usage pattern**: This is the most common query. Used by:
- `ProtocolSignatureValidator` - to validate method signatures
- `RoslynEmitter` - to generate appropriate C# code
- `ProtocolValidator` - to check protocol implementations

#### `IsProtocolDunder(string name)`
```csharp
public static bool IsProtocolDunder(string name)
    => _protocols.ContainsKey(name);
```

**What it does**: Quick check if a method name is a registered protocol dunder.

**Why separate from GetProtocol?**: Sometimes you just need a boolean check without allocating the full `ProtocolInfo` object.

### 3.3 Advanced Queries

#### `GetProtocolsByKind(ProtocolKind kind)`
```csharp
public static IEnumerable<ProtocolInfo> GetProtocolsByKind(ProtocolKind kind)
    => _protocols.Values.Where(p => p.Kind == kind);
```

**Use case**: Generate documentation, list all container protocols for error messages, etc.

**Example**:
```csharp
var containerProtocols = ProtocolRegistry.GetProtocolsByKind(ProtocolKind.Container);
// Returns: __len__, __contains__, __getitem__, __setitem__, __delitem__
```

#### `GetDunderForInterface(string interfaceName)`
```csharp
public static string? GetDunderForInterface(string interfaceName)
    => _protocols.Values
        .Where(p => p.SharpyCoreInterface == interfaceName)
        .Select(p => p.DunderName)
        .FirstOrDefault();
```

**What it does**: **Reverse lookup** - given a Sharpy.Core interface name, find the corresponding dunder.

**Important caveat** (lines 249-253): If multiple protocols map to the same interface (e.g., `__setitem__` and `__delitem__` both map to `IMutableSequence`), this returns only the first match. For exhaustive lookups, use `GetAllProtocols()` and filter manually.

**Use case**: When analyzing .NET interop, the compiler might need to determine which protocol a CLR type supports based on its interfaces.

#### `IsAnyDunder(string methodName)`
```csharp
public static bool IsAnyDunder(string methodName)
    => IsProtocolDunder(methodName) || OperatorSignatureValidator.IsOperatorDunder(methodName);
```

**What it does**: Checks if a method is **any** kind of dunder (protocol or operator).

**Why it exists**: During parsing and semantic analysis, the compiler needs to distinguish:
- Regular methods: `calculate_sum()`
- Protocol dunders: `__len__()`
- Operator dunders: `__add__()`

All dunders get special handling, but protocol vs operator dunders follow different validation and code generation rules.

#### `GetExpectedSignature(string dunderName)`
```csharp
public static (int ParamCount, string? ReturnType)? GetExpectedSignature(string dunderName)
{
    var info = GetProtocol(dunderName);
    if (info == null)
        return null;
    return (info.ExpectedParamCount, info.ExpectedReturnType);
}
```

**What it does**: Convenience method that extracts just the signature constraints (parameter count and return type).

**Use case**: Quick validation during semantic analysis without needing the full `ProtocolInfo`.

---

## 4. Dependencies

### 4.1 Outbound Dependencies

```csharp
using System.Collections.Frozen;
```

**Why FrozenDictionary?** (introduced in .NET 8): Immutable collections optimized for read-heavy workloads. Once frozen, lookups are faster than regular dictionaries. Perfect for this use case since protocols never change after initialization.

### 4.2 Inbound Dependencies (Who uses ProtocolRegistry?)

1. **`ProtocolSignatureValidator`** (`Semantic/ProtocolSignatureValidator.cs`)
   - Validates that user-defined protocol methods have correct signatures
   - Calls `GetProtocol()` to get expected parameter counts and return types

2. **`RoslynEmitter`** (`CodeGen/RoslynEmitter.cs`)
   - Generates C# code from Sharpy AST
   - Uses protocol info to:
     - Map `__len__()` to `ISized.__Len__()`
     - Generate appropriate interface implementations
     - Handle special cases like constructors

3. **`NameMangler`** (`CodeGen/NameMangler.cs`)
   - Converts Sharpy names to C# names (snake_case → PascalCase)
   - Uses `GetAllProtocols()` to know which names are dunders that should preserve their format

4. **`ProtocolValidator`** (`Semantic/ProtocolValidator.cs`)
   - Checks if types support specific protocols
   - Validates protocol usage in expressions

### 4.3 Related Files

- **`OperatorSignatureValidator.cs`**: Handles operator dunders (`__add__`, `__eq__`, etc.)
- **`Sharpy.Core` interfaces**: The actual interface definitions (e.g., `ISized`, `IContainer`)
- **Test files**:
  - `ProtocolRegistryTests.cs`: Unit tests for the registry itself
  - `ProtocolSignatureValidatorTests.cs`: Tests for signature validation

---

## 5. Patterns and Design Decisions

### 5.1 Separation of Concerns: Protocols vs Operators

**Design Decision**: Protocol dunders and operator dunders are handled separately.

**Why?**
- **Different semantics**: Protocols define capabilities (e.g., "this object has a length"), while operators define computations (e.g., "add two objects")
- **Different mappings**: Protocols map to interfaces, operators map to C# operator overloads
- **Different validation rules**: Protocol validation checks signatures, operator validation checks commutativity and type compatibility

### 5.2 Frozen Collections for Performance

```csharp
_protocols = protocols.ToFrozenDictionary();
```

**Trade-off**: Spend time upfront creating the frozen dictionary, gain faster lookups throughout compilation.

**Why this works**: The registry is read thousands of times during compilation but never written to after initialization.

### 5.3 Nullable References for Flexibility

Many fields in `ProtocolInfo` are nullable:
```csharp
string? SharpyCoreInterface,
string? InterfaceMethodName,
string? ClrMethodName,
string? ExpectedReturnType
```

**Why?** Different protocols have different needs:
- `__init__` doesn't have an interface (it's a constructor)
- `__getitem__` doesn't have a fixed return type (it's generic)
- `__delitem__` doesn't have a direct CLR equivalent

This flexibility allows the registry to handle all protocols uniformly while acknowledging their differences.

### 5.4 Explicit Parameter Count: `-1` Convention

```csharp
ExpectedParamCount: -1,  // Variable (1+ including self)
```

**Why -1 instead of nullable int?** 
- `-1` explicitly signals "variable count, 1 or more"
- `null` would be ambiguous (no constraint? unknown? error?)
- Makes validation code cleaner: `if (expectedCount == -1) return;`

### 5.5 Comprehensive Documentation Comments

Every protocol registration includes comments explaining:
- What it maps to
- Special cases (e.g., `__len__` uint vs int)
- Why certain fields are null

**Why this matters**: This file is the **authoritative source** for protocol semantics. Future maintainers need to understand the rationale behind each mapping.

---

## 6. Debugging Tips

### 6.1 Tracing Protocol Lookups

Add a breakpoint in `GetProtocol()` to see when and why protocols are being queried:

```csharp
public static ProtocolInfo? GetProtocol(string dunderName)
{
    var result = _protocols.GetValueOrDefault(dunderName);  // <- Breakpoint here
    return result;
}
```

**Watch for**: Unexpected dunder names being queried (might indicate a bug elsewhere).

### 6.2 Verifying Registry Contents

In a debugger or test, inspect `_protocols` to see all registered protocols:

```csharp
// In a test or during debugging
var allProtocols = ProtocolRegistry.GetAllProtocols().ToList();
Console.WriteLine($"Registered protocols: {allProtocols.Count}");
foreach (var p in allProtocols)
    Console.WriteLine($"  {p.DunderName} -> {p.SharpyCoreInterface ?? "(no interface)"}");
```

### 6.3 Common Issues

**Issue**: "Protocol validation is failing for a valid dunder"
- **Check**: Is the dunder registered in `RegisterAllProtocols()`?
- **Check**: Does the `ExpectedParamCount` match your method signature?
- **Check**: Is it actually a protocol dunder, or an operator dunder (handled separately)?

**Issue**: "Code generation produces wrong C# method name"
- **Check**: What is `ClrMethodName` set to for this protocol?
- **Check**: Is there special-case handling in `RoslynEmitter` for this protocol?

**Issue**: "Protocol seems to work in Sharpy code but fails during .NET interop"
- **Check**: Does the protocol have a `ClrMethodName` mapping?
- **Check**: Is the .NET type expected to have this method/property?

### 6.4 Testing Protocol Changes

When modifying protocol registrations:

1. **Run registry tests**: `dotnet test --filter "FullyQualifiedName~ProtocolRegistryTests"`
2. **Run signature validator tests**: `dotnet test --filter "FullyQualifiedName~ProtocolSignatureValidatorTests"`
3. **Check consistency tests**: `dotnet test --filter "FullyQualifiedName~RegistryConsistencyTests"`
4. **Test code generation**: Look at generated C# code for a sample Sharpy file using the protocol

---

## 7. Contribution Guidelines

### 7.1 Adding a New Protocol

**When to add**: When implementing a new Python protocol that Sharpy should support.

**Steps**:

1. **Research Python behavior**: Understand exactly how Python treats this protocol
   ```bash
   python3 -c "class X: ...; x = X(); x.__yourprotocol__()"
   ```

2. **Define or identify the Sharpy.Core interface**: Does an appropriate interface exist in `src/Sharpy.Core/`? If not, create it:
   ```csharp
   // In Sharpy.Core/IYourProtocol.cs
   public interface IYourProtocol<T>
   {
       T __YourMethod__();
   }
   ```

3. **Determine CLR mapping**: What .NET method/property is equivalent?
   - Collection.Count for `__len__`
   - object.ToString() for `__str__`
   - etc.

4. **Add to `RegisterAllProtocols()`**:
   ```csharp
   Register(protocols, new ProtocolInfo(
       DunderName: "__yourprotocol__",
       Kind: ProtocolKind.YourCategory,  // Choose or add new category
       SharpyCoreInterface: "IYourProtocol",
       InterfaceMethodName: "__YourMethod__",
       ClrMethodName: "YourClrMethod",  // or null if no mapping
       ExpectedParamCount: 2,  // self + parameters
       ExpectedReturnType: "yourtype"  // or null if generic
   ));
   ```

5. **Add comments explaining**:
   - What this protocol does
   - Any special cases or type conversions
   - Why certain fields are null (if applicable)

6. **Write tests**:
   ```csharp
   [Fact]
   public void TestYourProtocol_IsRegistered()
   {
       var info = ProtocolRegistry.GetProtocol("__yourprotocol__");
       Assert.NotNull(info);
       Assert.Equal(ProtocolKind.YourCategory, info.Kind);
   }
   ```

7. **Update related components**:
   - Add validation in `ProtocolSignatureValidator` if needed
   - Add code generation in `RoslynEmitter`
   - Update documentation

### 7.2 Modifying an Existing Protocol

**Caution**: Changing existing protocols can break existing Sharpy code!

**Valid reasons to modify**:
- Bug: The protocol mapping is incorrect
- Enhancement: Adding optional fields that were previously null
- Clarification: Improving comments or documentation

**Before modifying**:
1. Check if any Sharpy code (samples, tests) uses this protocol
2. Review the Sharpy.Core interface definition
3. Check test expectations

**After modifying**:
1. Run **all** tests (not just protocol tests)
2. Compile sample programs
3. Regenerate documentation if signature changed

### 7.3 Improving Performance

Current performance is excellent (frozen dictionary), but if you need to optimize further:

**Potential improvements**:
- Add a cache for common query patterns
- Pre-compute frequently-used subsets (e.g., "all generic protocols")
- Use spans for string comparisons if profiling shows string allocation overhead

**Before optimizing**: Profile first! The current implementation is likely fast enough. Only optimize if you have evidence that `ProtocolRegistry` is a bottleneck.

### 7.4 Code Style

**Follow these conventions when contributing**:

1. **Ordering**: Group protocols by `ProtocolKind`, add comments for each section
2. **Naming**: Use exact Python names for `DunderName` (lowercase, double underscores)
3. **Comments**: Explain special cases (type conversions, null values, etc.)
4. **Consistency**: Use the same pattern for all registrations:
   ```csharp
   Register(protocols, new ProtocolInfo(
       DunderName: "...",
       Kind: ...,
       SharpyCoreInterface: "...",
       InterfaceMethodName: "...",
       ClrMethodName: "...",
       ExpectedParamCount: ...,
       ExpectedReturnType: "..."
   ));
   ```

### 7.5 Testing Checklist

Before submitting a PR that modifies `ProtocolRegistry.cs`:

- [ ] Run unit tests: `dotnet test --filter "FullyQualifiedName~ProtocolRegistry"`
- [ ] Run integration tests: `dotnet test --filter "FullyQualifiedName~Integration"`
- [ ] Compile sample programs that use protocols
- [ ] Check that generated C# code is correct
- [ ] Verify error messages are helpful
- [ ] Update documentation in `docs/` if needed
- [ ] Add/update tests for your changes

---

## 8. Real-World Examples

### 8.1 Example: How `__len__` Works End-to-End

Let's trace how `__len__` flows through the compiler:

**1. Sharpy source code**:
```python
class MyList:
    def __len__(self) -> int:
        return 42

lst = MyList()
print(len(lst))
```

**2. Parser creates AST**: `FunctionDef` node for `__len__`

**3. Semantic Analysis**:
- `TypeChecker` encounters the method
- Calls `ProtocolSignatureValidator.ValidateDunderSignature()`
- Which calls `ProtocolRegistry.GetProtocol("__len__")`
- Returns `ProtocolInfo` with `ExpectedParamCount: 1`, `ExpectedReturnType: "int"`
- Validator checks: ✓ Has 1 parameter (self), ✓ Returns int

**4. Code Generation**:
- `RoslynEmitter` generates C# class
- Calls `ProtocolRegistry.GetProtocol("__len__")` again
- Sees `SharpyCoreInterface: "ISized"`, `InterfaceMethodName: "__Len__"`
- Generates:
  ```csharp
  public class MyList : ISized
  {
      public int __Len__() => 42;
  }
  ```

**5. For the `len()` call**:
- Compiler sees it needs to call `__Len__()` on an `ISized` object
- Generates: `myList.__Len__()`

### 8.2 Example: Why `__init__` is Special

Notice that `__init__` has several special values:

```csharp
Register(protocols, new ProtocolInfo(
    DunderName: "__init__",
    Kind: ProtocolKind.Lifecycle,
    SharpyCoreInterface: null,      // No interface - it's a constructor!
    InterfaceMethodName: null,
    ClrMethodName: ".ctor",         // Maps to constructor
    ExpectedParamCount: -1,         // Variable parameters
    ExpectedReturnType: "None"      // Constructors return void
));
```

**Why these choices?**
- **No interface**: Constructors aren't part of an interface contract in C#
- **`.ctor`**: Special name that `RoslynEmitter` recognizes to generate a constructor
- **`-1` params**: `__init__(self)` is valid, but so is `__init__(self, a, b, c, ...)`
- **Returns `None`**: In Python, `__init__` implicitly returns None; in C#, constructors return void

---

## 9. Relationship to Python's Data Model

`ProtocolRegistry` implements a subset of [Python's data model](https://docs.python.org/3/reference/datamodel.html).

**Currently supported** (as of this code):
- Container protocols: `__len__`, `__contains__`, `__getitem__`, `__setitem__`, `__delitem__`
- Iterator protocols: `__iter__`, `__next__`
- Representation protocols: `__str__`, `__repr__`
- Hashing: `__hash__`
- Conversion: `__bool__`

**Not yet supported** (examples):
- Context managers: `__enter__`, `__exit__`
- Descriptors: `__get__`, `__set__`, `__delete__`
- Async protocols: `__await__`, `__aiter__`, `__anext__`
- Pickling: `__reduce__`, `__getstate__`, `__setstate__`

**Adding future protocols**: Follow the same pattern used for existing protocols. Check if Sharpy.Core has the corresponding interface, or create one.

---

## 10. Quick Reference

### Common Queries

```csharp
// Is this a protocol dunder?
bool isProtocol = ProtocolRegistry.IsProtocolDunder("__len__");

// Get full protocol info
ProtocolInfo? info = ProtocolRegistry.GetProtocol("__str__");

// What interface does this implement?
string? interfaceName = ProtocolRegistry.GetInterfaceName("__contains__");

// What's the CLR equivalent?
string? clrName = ProtocolRegistry.GetClrMethodName("__hash__");

// All container protocols
var containers = ProtocolRegistry.GetProtocolsByKind(ProtocolKind.Container);

// How many protocols are registered?
int count = ProtocolRegistry.Count;
```

### Protocol Categories at a Glance

| Kind | Protocols | Purpose |
|------|-----------|---------|
| `Lifecycle` | `__init__` | Object construction |
| `Container` | `__len__`, `__contains__`, `__getitem__`, `__setitem__`, `__delitem__` | Collection operations |
| `Iterator` | `__iter__`, `__next__` | Iteration |
| `Representation` | `__str__`, `__repr__` | String conversion |
| `Hashing` | `__hash__` | Hash code generation |
| `Conversion` | `__bool__` | Type conversion |

---

## 11. Conclusion

`ProtocolRegistry.cs` is a **foundational piece** of the Sharpy compiler. It's small (< 300 lines) but critical. Every protocol interaction in Sharpy code flows through this registry.

**Key takeaways**:
1. It's a **static, immutable lookup table** - fast and thread-safe
2. It **bridges three worlds** - Sharpy syntax, Sharpy.Core interfaces, and .NET methods
3. It's **separate from operator handling** - protocols and operators have different semantics
4. It's **well-documented** - the comments are part of the specification
5. It's **extensible** - adding new protocols follows a clear pattern

When in doubt, this file is the **source of truth** for how protocol dunders work in Sharpy.

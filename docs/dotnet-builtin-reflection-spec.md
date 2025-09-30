# Specification: Auto-Generating Builtin Symbols from .NET Standard Library

## Overview

This specification defines how the Sharpy Rust compiler toolchain should leverage reflection on the compiled .NET assembly (`Sharpy.dll`) to automatically generate builtin symbols, types, and function signatures for semantic analysis, eliminating hardcoded definitions.

## Architecture

### High-Level Flow

```
┌─────────────────────┐
│   Sharpy.dll        │
│  (.NET Assembly)    │
└──────────┬──────────┘
           │
           │ Reflection
           ▼
┌─────────────────────────────┐
│  Build-Time Tool            │
│  (Rust binary)              │
│  - Loads assembly via       │
│    .NET hosting APIs        │
│  - Extracts type metadata   │
│  - Generates Rust code      │
└──────────┬──────────────────┘
           │
           │ Code Generation
           ▼
┌─────────────────────────────┐
│  generated_builtins.rs      │
│  - BuiltinType enum         │
│  - Builtin functions        │
│  - Method signatures        │
│  - Protocol definitions     │
└──────────┬──────────────────┘
           │
           │ Include/Compile
           ▼
┌─────────────────────────────┐
│  Sharpy Compiler            │
│  - Uses generated symbols   │
│  - Semantic analysis        │
│  - Type checking            │
└─────────────────────────────┘
```

## Component Design

### 1. Build-Time Reflection Tool

**Location:** `rust/build_tools/generate_builtins/`

**Purpose:** A standalone Rust binary that uses .NET hosting APIs to reflect on `Sharpy.dll` and generate Rust code.

**Dependencies:**
- `netcorehost` crate for .NET runtime hosting
- `serde` and `serde_json` for intermediate JSON representation
- Custom C# reflection helper assembly

**Process:**
1. Load the .NET runtime
2. Load `Sharpy.dll` assembly
3. Execute C# reflection code to extract metadata
4. Receive structured JSON with type information
5. Generate Rust source code
6. Write to `rust/src/semantic/generated_builtins.rs`

### 2. C# Reflection Helper

**Location:** `dotnet/src/Sharpy.Reflection/`

**Purpose:** Provides structured type information extraction from the Sharpy standard library.

**Key Classes:**

```csharp
namespace Sharpy.Reflection;

public class TypeMetadata
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public TypeKind Kind { get; set; }  // Class, Struct, Protocol, Function, Primitive
    public bool IsGeneric { get; set; }
    public List<string> GenericParameters { get; set; }
    public List<MethodMetadata> Methods { get; set; }
    public List<PropertyMetadata> Properties { get; set; }
    public List<string> Interfaces { get; set; }
    public string? Documentation { get; set; }
}

public class MethodMetadata
{
    public string Name { get; set; }
    public bool IsStatic { get; set; }
    public bool IsPublic { get; set; }
    public List<ParameterMetadata> Parameters { get; set; }
    public TypeReference? ReturnType { get; set; }
    public string? Documentation { get; set; }
}

public class ParameterMetadata
{
    public string Name { get; set; }
    public TypeReference Type { get; set; }
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}

public class TypeReference
{
    public string Name { get; set; }
    public bool IsGeneric { get; set; }
    public List<TypeReference> GenericArguments { get; set; }
    public bool IsNullable { get; set; }
}

public enum TypeKind
{
    Primitive,      // int, float, bool, str
    Class,          // Object, List<T>, Dict<K,V>
    Struct,         // Custom value types
    Interface,      // IHashable, IEquatable, etc.
    Function,       // Static functions in Exports
    Protocol        // Sharpy protocols (interfaces)
}

public static class AssemblyReflector
{
    public static string ExtractMetadataAsJson(string assemblyPath)
    {
        // Load assembly
        // Reflect on public types
        // Extract all metadata
        // Serialize to JSON
        // Return JSON string
    }
}
```

### 3. Metadata Extraction Rules

#### Types to Extract

**From `Sharpy` namespace:**

1. **Primitive Types** (map to `BuiltinType`):
   - `Object` → `Object`
   - Built-in value types via reflection

2. **Collection Types** (generic):
   - `List<T>` → `BuiltinType::List` with generic parameter
   - `Dict<K,V>` → `BuiltinType::Dict` with key/value types
   - `Set<T>` → `BuiltinType::Set`
   - Extract generic constraints

3. **Protocol/Interface Types**:
   - All interfaces starting with `I*` (IHashable, IEquatable, etc.)
   - Map to `SemanticType::Protocol`
   - Extract method signatures

4. **Static Functions** (from `Exports` class):
   - `Print()` → builtin function
   - `Len()` → builtin function
   - Extract overload signatures
   - Extract parameter types, defaults, variadic args

5. **Special Types**:
   - `None` → `BuiltinType::None`
   - `Error` types (TypeError, ValueError, etc.)
   - `Result<T>`, `Optional<T>` types

#### Attributes for Metadata

Define custom attributes in the .NET library to guide code generation:

```csharp
namespace Sharpy;

/// <summary>
/// Marks a type as a Sharpy builtin that should be available
/// in the compiler's symbol table
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public class SharpyBuiltinAttribute : Attribute
{
    public string? SharpyName { get; set; }  // Override name in Sharpy
}

/// <summary>
/// Marks a method as a Sharpy builtin function
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SharpyFunctionAttribute : Attribute
{
    public string? SharpyName { get; set; }
    public bool IsVariadic { get; set; }
}

/// <summary>
/// Marks a parameter with additional Sharpy-specific metadata
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class SharpyParameterAttribute : Attribute
{
    public bool IsKeywordOnly { get; set; }
    public bool IsPositionalOnly { get; set; }
}

/// <summary>
/// Marks a type as a Sharpy protocol (similar to Python's Protocol)
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public class SharpyProtocolAttribute : Attribute
{
}
```

#### Example Annotated Code

```csharp
namespace Sharpy;

[SharpyBuiltin(SharpyName = "list")]
public sealed partial class List<T> : Object
{
    [SharpyFunction]
    public void Append(T item) { }

    [SharpyFunction]
    public void Extend(IEnumerable<T> items) { }
}

[SharpyProtocol]
public interface IHashable
{
    int __Hash__();
}

public static partial class Exports
{
    [SharpyFunction(IsVariadic = true)]
    public static void Print(params object?[] args) { }

    [SharpyFunction]
    public static int Len([SharpyParameter(IsPositionalOnly = true)] object obj) { }
}
```

### 4. Generated Rust Code Structure

**File:** `rust/src/semantic/generated_builtins.rs`

```rust
// This file is auto-generated by build_tools/generate_builtins
// DO NOT EDIT MANUALLY
// Generated from: Sharpy.dll version X.Y.Z
// Generation timestamp: 2025-09-29T12:00:00Z

use super::types::{SemanticType, BuiltinType};
use std::collections::HashMap;

/// Auto-generated builtin types from .NET assembly
pub fn get_builtin_types() -> Vec<(String, SemanticType)> {
    vec![
        // Primitives
        ("int".to_string(), SemanticType::Builtin(BuiltinType::Int)),
        ("float".to_string(), SemanticType::Builtin(BuiltinType::Float)),
        ("bool".to_string(), SemanticType::Builtin(BuiltinType::Bool)),
        ("str".to_string(), SemanticType::Builtin(BuiltinType::Str)),
        ("None".to_string(), SemanticType::Builtin(BuiltinType::None)),

        // Collections
        ("list".to_string(), SemanticType::Builtin(BuiltinType::List)),
        ("dict".to_string(), SemanticType::Builtin(BuiltinType::Dict)),
        ("set".to_string(), SemanticType::Builtin(BuiltinType::Set)),

        // Object
        ("object".to_string(), SemanticType::Builtin(BuiltinType::Object)),

        // Add more types...
    ]
}

/// Auto-generated builtin functions from .NET assembly
pub fn get_builtin_functions() -> Vec<(String, SemanticType)> {
    vec![
        // print function with multiple overloads
        (
            "print".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())],
                return_type: None,
            },
        ),

        // len function
        (
            "len".to_string(),
            SemanticType::Function {
                params: vec![SemanticType::Unknown("any".to_string())],
                return_type: Some(Box::new(SemanticType::Builtin(BuiltinType::Int))),
            },
        ),

        // Add more functions...
    ]
}

/// Auto-generated method signatures for builtin types
pub fn get_builtin_methods() -> HashMap<String, Vec<(String, SemanticType)>> {
    let mut methods = HashMap::new();

    // List methods
    methods.insert(
        "list".to_string(),
        vec![
            (
                "append".to_string(),
                SemanticType::Function {
                    params: vec![SemanticType::Unknown("T".to_string())],
                    return_type: None,
                },
            ),
            (
                "extend".to_string(),
                SemanticType::Function {
                    params: vec![SemanticType::Unknown("iterable".to_string())],
                    return_type: None,
                },
            ),
            (
                "pop".to_string(),
                SemanticType::Function {
                    params: vec![SemanticType::Builtin(BuiltinType::Int)],
                    return_type: Some(Box::new(SemanticType::Unknown("T".to_string()))),
                },
            ),
            // More list methods...
        ],
    );

    // Dict methods
    methods.insert(
        "dict".to_string(),
        vec![
            (
                "get".to_string(),
                SemanticType::Function {
                    params: vec![
                        SemanticType::Unknown("K".to_string()),
                        SemanticType::Unknown("default".to_string()),
                    ],
                    return_type: Some(Box::new(SemanticType::Unknown("V".to_string()))),
                },
            ),
            // More dict methods...
        ],
    );

    methods
}

/// Auto-generated protocol definitions
pub fn get_builtin_protocols() -> Vec<(String, Vec<String>)> {
    vec![
        // (protocol_name, required_methods)
        ("IHashable".to_string(), vec!["__Hash__".to_string()]),
        ("IEquatable".to_string(), vec!["__Eq__".to_string()]),
        ("IStrConvertible".to_string(), vec!["__Str__".to_string()]),
        ("IBoolConvertible".to_string(), vec!["__Bool__".to_string()]),
        // More protocols...
    ]
}

/// Metadata about the .NET assembly this was generated from
pub struct GenerationMetadata {
    pub assembly_version: &'static str,
    pub generated_at: &'static str,
    pub generator_version: &'static str,
}

pub const GENERATION_METADATA: GenerationMetadata = GenerationMetadata {
    assembly_version: "1.0.0",
    generated_at: "2025-09-29T12:00:00Z",
    generator_version: "0.1.0",
};
```

### 5. Integration with Semantic Analyzer

**Modify:** `rust/src/semantic/types.rs`

```rust
mod generated_builtins;

impl SemanticType {
    /// Initialize builtin types from generated metadata
    pub fn initialize_builtins(symbol_table: &mut SymbolTable) {
        // Load generated builtin types
        for (name, semantic_type) in generated_builtins::get_builtin_types() {
            symbol_table.add_builtin_type(name, semantic_type);
        }

        // Load generated builtin functions
        for (name, func_type) in generated_builtins::get_builtin_functions() {
            symbol_table.add_builtin_function(name, func_type);
        }

        // Load generated method signatures
        let methods = generated_builtins::get_builtin_methods();
        for (type_name, type_methods) in methods {
            for (method_name, method_type) in type_methods {
                symbol_table.add_builtin_method(&type_name, method_name, method_type);
            }
        }

        // Load protocol definitions
        for (protocol_name, required_methods) in generated_builtins::get_builtin_protocols() {
            symbol_table.add_protocol(protocol_name, required_methods);
        }
    }
}
```

## Build Process Integration

### Cargo Build Script

**File:** `rust/build.rs`

```rust
use std::env;
use std::path::PathBuf;
use std::process::Command;

fn main() {
    println!("cargo:rerun-if-changed=../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll");
    println!("cargo:rerun-if-changed=build_tools/generate_builtins");

    // Path to compiled Sharpy.dll
    let dotnet_dll = PathBuf::from("../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll");

    if !dotnet_dll.exists() {
        eprintln!("Warning: Sharpy.dll not found. Skipping builtin generation.");
        eprintln!("Run: cd ../dotnet && dotnet build");
        return;
    }

    // Path to generator tool
    let generator = PathBuf::from("build_tools/generate_builtins");

    // Run generator
    let output = Command::new(&generator)
        .arg("--assembly")
        .arg(&dotnet_dll)
        .arg("--output")
        .arg("src/semantic/generated_builtins.rs")
        .output()
        .expect("Failed to run builtin generator");

    if !output.status.success() {
        panic!(
            "Builtin generation failed:\n{}",
            String::from_utf8_lossy(&output.stderr)
        );
    }

    println!("cargo:warning=Generated builtins from Sharpy.dll");
}
```

### Manual Build Steps

For developers:

```bash
# 1. Build .NET standard library
cd dotnet
dotnet build

# 2. Generate builtins (happens automatically during cargo build)
cd ../rust
cargo build

# 3. Or manually run generator
./build_tools/generate_builtins \
    --assembly ../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll \
    --output src/semantic/generated_builtins.rs
```

## Type Mapping

### .NET Type → Sharpy Type

| .NET Type | Sharpy Type | BuiltinType |
|-----------|-------------|-------------|
| `System.Int32` | `int` | `Int` |
| `System.Int64` | `long` | `Long` |
| `System.Single` | `float` | `Float` |
| `System.Double` | `double` | `Double` |
| `System.Boolean` | `bool` | `Bool` |
| `System.String` | `str` | `Str` |
| `Sharpy.Object` | `object` | `Object` |
| `Sharpy.List<T>` | `list[T]` | `List` (Generic) |
| `Sharpy.Dict<K,V>` | `dict[K,V]` | `Dict` (Generic) |
| `Sharpy.Set<T>` | `set[T]` | `Set` (Generic) |
| `Sharpy.None` | `None` | `None` |
| `T?` (nullable) | `T?` | Optional wrapper |

### Special Method Name Mappings

| .NET Method | Sharpy Method | Purpose |
|-------------|---------------|---------|
| `__Str__()` | `__str__()` | String representation |
| `__Repr__()` | `__repr__()` | Debug representation |
| `__Hash__()` | `__hash__()` | Hash code |
| `__Eq__(other)` | `__eq__(other)` | Equality |
| `__Add__(other)` | `__add__(other)` | Addition operator |
| `__Len__()` | `__len__()` | Length/count |
| `__GetItem__(key)` | `__getitem__(key)` | Subscript access |
| `__SetItem__(key, value)` | `__setitem__(key, value)` | Subscript assignment |

## Implementation Phases

### Phase 1: Basic Infrastructure (Week 1)
- [ ] Create C# reflection helper project
- [ ] Define JSON schema for metadata
- [ ] Implement basic AssemblyReflector
- [ ] Create Rust generator skeleton

### Phase 2: Type Extraction (Week 2)
- [ ] Extract primitive types
- [ ] Extract collection types
- [ ] Handle generic types
- [ ] Map .NET types to Sharpy types

### Phase 3: Function/Method Extraction (Week 3)
- [ ] Extract static functions from Exports
- [ ] Extract instance methods
- [ ] Handle method overloads
- [ ] Extract parameter metadata

### Phase 4: Code Generation (Week 4)
- [ ] Generate BuiltinType enum
- [ ] Generate function signatures
- [ ] Generate method tables
- [ ] Generate protocol definitions

### Phase 5: Integration (Week 5)
- [ ] Integrate with semantic analyzer
- [ ] Update symbol table initialization
- [ ] Add build script
- [ ] Write tests

### Phase 6: Advanced Features (Week 6)
- [ ] Handle generic constraints
- [ ] Extract documentation comments
- [ ] Support custom attributes
- [ ] Version tracking

## Testing Strategy

### Unit Tests

```rust
#[cfg(test)]
mod tests {
    use super::generated_builtins::*;

    #[test]
    fn test_builtin_types_exist() {
        let types = get_builtin_types();
        assert!(types.iter().any(|(name, _)| name == "int"));
        assert!(types.iter().any(|(name, _)| name == "list"));
        assert!(types.iter().any(|(name, _)| name == "dict"));
    }

    #[test]
    fn test_builtin_functions_exist() {
        let funcs = get_builtin_functions();
        assert!(funcs.iter().any(|(name, _)| name == "print"));
        assert!(funcs.iter().any(|(name, _)| name == "len"));
    }

    #[test]
    fn test_list_methods_complete() {
        let methods = get_builtin_methods();
        let list_methods = methods.get("list").expect("List methods should exist");

        assert!(list_methods.iter().any(|(name, _)| name == "append"));
        assert!(list_methods.iter().any(|(name, _)| name == "extend"));
        assert!(list_methods.iter().any(|(name, _)| name == "pop"));
    }
}
```

### Integration Tests

```rust
#[test]
fn test_semantic_analysis_with_generated_builtins() {
    let source = r#"
numbers: list[int] = [1, 2, 3]
length = len(numbers)
numbers.append(4)
"#;

    let mut analyzer = SemanticAnalyzer::new();
    let result = analyzer.analyze(source);
    assert!(result.is_ok());
}
```

## Alternative Approaches Considered

### 1. Runtime Reflection (Rejected)
- **Pros:** Dynamic, no code generation
- **Cons:** Requires .NET runtime at compiler runtime, performance overhead, deployment complexity

### 2. Manual JSON Definition (Rejected)
- **Pros:** Simple, no reflection needed
- **Cons:** Maintenance burden, prone to drift from actual .NET implementation

### 3. Proc Macros (Rejected)
- **Pros:** Compile-time generation
- **Cons:** Can't cross .NET/Rust boundary easily, limited to Rust ecosystem

### 4. Build-Time Code Generation (Selected) ✓
- **Pros:** Single source of truth (.NET code), automatic updates, type safety
- **Cons:** Requires .NET SDK at build time, adds build step complexity

## Future Enhancements

1. **Incremental Generation**: Only regenerate when .NET assembly changes
2. **Multiple Target Assemblies**: Support third-party libraries
3. **Documentation Extraction**: Pull XML docs into Rust as doc comments
4. **Custom Type Mappings**: Allow user-defined type mappings via config
5. **Source Code Linking**: Include source file/line information for debugging
6. **Validation Tools**: Verify generated code matches .NET semantics

## Dependencies

### .NET Side
- .NET 9.0 SDK
- System.Reflection
- System.Reflection.Metadata
- Newtonsoft.Json or System.Text.Json

### Rust Side
- `netcorehost` for .NET hosting
- `serde_json` for JSON parsing
- `quote` for Rust code generation (optional)
- `syn` for Rust syntax tree manipulation (optional)

## Configuration File

**File:** `rust/builtin_generation.toml`

```toml
[generation]
# Path to .NET assembly
assembly_path = "../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll"

# Output path for generated Rust code
output_path = "src/semantic/generated_builtins.rs"

# Namespace to extract from
target_namespace = "Sharpy"

[type_mapping]
# Custom type name mappings
"System.Int32" = "int"
"System.String" = "str"

[extraction]
# Extract private members?
include_private = false

# Extract internal types?
include_internal = false

# Extract obsolete APIs?
include_obsolete = false

[protocols]
# Prefixes for protocol detection
interface_prefixes = ["I"]

# Specific interfaces to treat as protocols
explicit_protocols = ["IHashable", "IEquatable"]
```

## Conclusion

This specification provides a comprehensive approach to automatically generating Sharpy's builtin symbol table from the compiled .NET standard library. By using build-time code generation with reflection, we maintain a single source of truth (the .NET implementation) while providing type-safe, compile-time information to the Rust compiler toolchain.

The approach is extensible, maintainable, and ensures that the compiler's understanding of builtin types always matches the actual runtime implementation.

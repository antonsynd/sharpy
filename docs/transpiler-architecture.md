# Sharpy Transpiler Toolchain Architecture

## Overview

Sharpy is a statically-typed Pythonic language that transpiles to C# (with plans to emit CIL directly in the future). The toolchain consists of several distinct components working together to transform Sharpy source code into executable .NET assemblies while maintaining Python-like semantics and syntax.

**Current Status:** As of version 0.1.0, the frontend (lexer, parser, semantic analysis) is ~80% complete. Code generation backend is not yet implemented.

---

## 1. Component Architecture

### 1.1 High-Level Pipeline

```
┌─────────────┐     ┌──────────┐     ┌─────────┐     ┌──────────────┐     ┌────────────┐
│   Sharpy    │ --> │  Lexer   │ --> │ Parser  │ --> │   Semantic   │ --> │  Code Gen  │
│  Source     │     │  (Rust)  │     │ (Rust)  │     │   Analyzer   │     │   (Rust)   │
│  (.spy)     │     │          │     │         │     │   (Rust)     │     │            │
└─────────────┘     └──────────┘     └─────────┘     └──────────────┘     └────────────┘
                          │                │                 │                    │
                          v                v                 v                    v
                    ┌──────────┐     ┌─────────┐     ┌─────────────┐     ┌──────────────┐
                    │  Tokens  │     │   AST   │     │  Typed AST  │     │  C# Source   │
                    │          │     │         │     │  + Symbols  │     │  (.cs)       │
                    └──────────┘     └─────────┘     └─────────────┘     └──────────────┘
                                                                                  │
                                                                                  v
                                                                          ┌──────────────┐
                                                                          │   .NET CLI   │
                                                                          │   Compiler   │
                                                                          │   (csc/msbuild)│
                                                                          └──────────────┘
                                                                                  │
                                                                                  v
                                                                          ┌──────────────┐
                                                                          │   .NET DLL   │
                                                                          │   Assembly   │
                                                                          └──────────────┘
```

### 1.2 Supporting Infrastructure

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Supporting Components                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────┐         ┌──────────────────────┐         │
│  │  .NET Standard       │         │  Builtin Symbol      │         │
│  │  Library (C#)        │ ------> │  Generator (Rust)    │         │
│  │  (Sharpy.dll)        │         │  (Build Tool)        │         │
│  └─────────────────────┘         └──────────────────────┘         │
│            │                               │                       │
│            │                               v                       │
│            │                      ┌──────────────────────┐        │
│            │                      │  generated_builtins   │        │
│            │                      │  .rs (Auto-generated) │        │
│            │                      └──────────────────────┘        │
│            │                               │                       │
│            │                               v                       │
│            v                      ┌──────────────────────┐        │
│   ┌─────────────────┐             │  Semantic Analyzer   │        │
│   │  Runtime Type   │ <---------- │  (Type Checking)     │        │
│   │  System         │             └──────────────────────┘        │
│   │  (Sharpy.*)     │                                             │
│   └─────────────────┘                                             │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 2. Component Details

### 2.1 Lexical Analysis (Lexer)

**Location:** `rust/src/lexer/`

**Purpose:** Tokenize Sharpy source code into a stream of tokens.

**Key Files:**
- `lexer.rs` - Main lexer implementation
- `token.rs` - Token type definitions and data structures
- `error.rs` - Lexer error types

**Features:**
- ✅ All Python operators and keywords
- ✅ Indentation-based block structure
- ✅ String literals (regular, raw, byte, f-strings)
- ✅ Number literals (int, float, imaginary)
- ✅ Comment handling
- ✅ Default access modifier detection via naming conventions (`_name`, `__name`)
- ✅ Context-dependent soft keywords (`get`, `set`, `property`, `event`)

**Key Challenges:**
1. **Indentation Tracking**: Must maintain indentation stack for Python-style blocks
2. **F-String Parsing**: Complex multi-part tokenization (Start, Middle, End with embedded expressions)
3. **Soft Keywords**: `get`, `set`, `property`, `event` are contextual - lexer marks them but parser decides
4. **Access Modifier Hints**: Underscore prefixes stored in token metadata for semantic analysis

**Implementation Notes:**
```rust
pub struct Token {
    pub token_type: TokenType,
    pub location: SourceLocation,  // Line/column tracking
}

pub enum TokenType {
    Name(NameType),        // Includes is_literal flag for backtick names
    Number(NumberType),    // Int, Float, Imaginary
    String(StringType),    // Regular, Raw, Byte, FString parts
    Indent, Dedent,        // Block structure
    // ... operators, keywords, etc.
}
```

---

### 2.2 Syntactic Analysis (Parser)

**Location:** `rust/src/parser/`

**Purpose:** Build an Abstract Syntax Tree (AST) from token stream.

**Key Files:**
- `parse.rs` - Recursive descent parser implementation
- `error.rs` - Parse error types

**AST Structure:**
- `ast/node.rs` - AST node definitions (40+ node types)
- `ast/types.rs` - Type annotation nodes (TypeName, GenericType, OptionalType, QualifiedType)

**Features:**
- ✅ Module-level statements
- ✅ Class, struct, protocol definitions
- ✅ Function definitions with type annotations
- ✅ Property definitions (auto and explicit)
- ✅ Control flow (if/while/for/try)
- ✅ Expressions (binary ops, calls, subscripts, attributes)
- ✅ Decorators and access modifiers
- ✅ Import statements (all forms)
- ✅ Lambda expressions
- ✅ Collection literals (list, dict, set, tuple)
- ✅ F-string expression interpolation

**Key Challenges:**

1. **Assignment vs Expression Disambiguation**
   - Must lookahead to distinguish `x = 5` from standalone expression `x`
   - Handles complex patterns: `x: int = 5`, `a, b = values`, `obj[i] = val`

2. **Property Syntax Complexity**
   ```python
   # Auto property
   property name: int = 0

   # Explicit property with getter/setter
   property name(self) -> int:
       return self._name
   property name(self, value: int):
       self._name = value
   ```

3. **Type Annotation Parsing**
   - Qualified types: `collections.defaultdict[str, int]`
   - Generic types: `dict[str, list[int]]`
   - Optional types: `int?`
   - Must handle nested brackets and preserve structure

4. **Decorator Stacking**
   ```python
   @static
   @private
   def helper():
       pass
   ```

**AST Design Notes:**
- Each node has optional `NodeSource` with line/column info
- Access modifiers stored as `Option<String>` in definition nodes
- Type annotations are separate AST nodes, not strings
- Preserve all source information for error reporting

---

### 2.3 Semantic Analysis (Multi-Pass Analyzer)

**Location:** `rust/src/semantic/`

**Purpose:** Type checking, symbol resolution, scope analysis.

**Architecture:**

```
┌───────────────────────────────────────────────────────────┐
│               Multi-Pass Semantic Analyzer                │
├───────────────────────────────────────────────────────────┤
│                                                           │
│  Pass 1: Declaration Pass                                │
│  ├─ Collect all top-level symbols                        │
│  ├─ Classes, structs, protocols, functions, constants    │
│  ├─ Build initial symbol table                           │
│  └─ No type checking yet                                 │
│                                                           │
│  Pass 2: Import Pass                                     │
│  ├─ Resolve import statements                            │
│  ├─ Populate module registry                             │
│  ├─ Handle import cycles                                 │
│  └─ Build cross-module symbol references                 │
│                                                           │
│  Pass 3: Type Pass                                       │
│  ├─ Type inference for all expressions                   │
│  ├─ Function call validation                             │
│  ├─ Attribute access validation                          │
│  ├─ Type compatibility checking                          │
│  └─ Complete symbol table with type information          │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

**Key Files:**
- `multi_pass_analyzer.rs` - Orchestrates the 3 passes
- `passes/declaration_pass.rs` - Collect symbols
- `passes/import_pass.rs` - Resolve imports
- `passes/type_pass.rs` - Type checking and inference
- `symbol_table.rs` - Symbol storage and scope management
- `types.rs` - Semantic type system
- `module_registry.rs` - Cross-module symbol tracking

**Type System:**
```rust
pub enum SemanticType {
    Builtin(BuiltinType),          // int, str, bool, etc.
    Class { name: String, ... },
    Struct { name: String, ... },
    Protocol { name: String, ... },
    Function { params: Vec<...>, return_type: Box<...> },
    Generic { base: BuiltinType, args: Vec<SemanticType> },  // list[int]
    Unknown,
}

pub enum BuiltinType {
    Int, Float, Str, Bool, None,
    List, Dict, Set, Tuple,
    // ... 20+ more types
}
```

**Symbol Table:**
```rust
pub struct Symbol {
    name: String,
    kind: SymbolKind,           // Function, Method, Class, Variable, etc.
    symbol_type: SemanticType,
    access_level: AccessLevel,  // Public, Protected, Private, Internal, File
    scope_id: String,
    location: Option<SourceLocation>,
    metadata: SymbolMetadata,   // Function params, class members, etc.
}
```

**Key Challenges:**

1. **Type Inference**
   ```python
   # Must infer list[int] from context
   numbers = [1, 2, 3]

   # Mixed numeric types promote to float
   mixed = [1, 2.5, 3]  # list[float]

   # Generic subscript access
   first = numbers[0]  # int (not list!)
   ```

2. **Method Resolution on Builtins**
   ```python
   s: str = "hello"
   upper = s.upper()  # Must resolve str.upper() -> str

   nums: list[int] = [1, 2, 3]
   nums.append(4)     # Must validate append(int) on list[int]
   ```

3. **Access Level Validation**
   - Decorator takes precedence over naming convention
   - File-level access is transitive within file
   - Protected access in inheritance chains
   - Internal access across same assembly

4. **Builtin Symbol Integration**
   - Must pre-populate symbol table with 50+ builtin types
   - Each builtin type has methods (str.upper, list.append, etc.)
   - Currently hardcoded, will be auto-generated from Sharpy.dll

**Current Limitations:**
- ⚠️ No constraint validation on generics
- ⚠️ Exception type checking incomplete
- ⚠️ Protocol conformance checking partial
- ⚠️ No comprehensive operator overload resolution

---

### 2.4 Code Generation (NOT YET IMPLEMENTED)

**Planned Location:** `rust/src/codegen/`

**Purpose:** Generate C# source code from typed AST.

**Planned Architecture:**

```
┌─────────────────────────────────────────────────────────┐
│                  Code Generator                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Module Generator                                       │
│  ├─ Namespace declarations                             │
│  ├─ Using statements                                    │
│  └─ __Module__ static class for module-level code      │
│                                                         │
│  Type Generator                                         │
│  ├─ Class definitions                                   │
│  ├─ Struct definitions                                  │
│  ├─ Interface definitions (from protocols)             │
│  ├─ Generic type parameters                            │
│  └─ Inheritance and protocol implementation            │
│                                                         │
│  Member Generator                                       │
│  ├─ Methods (with name mangling)                       │
│  ├─ Properties (auto and explicit)                     │
│  ├─ Fields                                              │
│  └─ Access modifiers                                    │
│                                                         │
│  Statement Generator                                    │
│  ├─ Control flow translation                           │
│  ├─ Exception handling                                  │
│  └─ For loops (convert to foreach/for)                 │
│                                                         │
│  Expression Generator                                   │
│  ├─ Operator translation                               │
│  ├─ Method calls (with name mangling)                  │
│  ├─ Collection literals                                │
│  └─ Type conversions                                    │
│                                                         │
│  Name Mangling Engine                                   │
│  ├─ snake_case → PascalCase (types, methods)           │
│  ├─ snake_case → camelCase (parameters)                │
│  ├─ CAPS_SNAKE → PascalCase (enum values)              │
│  ├─ Backtick literal names (preserve exact casing)     │
│  └─ Collision detection and resolution                 │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Key Implementation Challenges:**

1. **Name Mangling**
   ```python
   # Sharpy input
   def add_numbers(first_num: int, second_num: int) -> int:
       return first_num + second_num

   # C# output
   public int AddNumbers(int firstNum, int secondNum)
   {
       return firstNum + secondNum;
   }
   ```

   **Collision Handling:**
   ```python
   # Sharpy - ERROR: Collision detected!
   def process_data(): pass
   def ProcessData(): pass  # Conflicts after mangling
   ```

2. **Module-Level Code**
   ```python
   # Sharpy module: math_utils.spy
   PI: float = 3.14159

   def calculate_area(radius: float) -> float:
       return PI * radius * radius
   ```

   ```csharp
   // Generated C#
   namespace MathUtils;

   public static class __Module__
   {
       public static readonly float PI = 3.14159f;

       public static float CalculateArea(float radius)
       {
           return PI * radius * radius;
       }
   }
   ```

3. **Protocol to Interface Mapping**
   ```python
   # Sharpy
   protocol Drawable:
       def draw(self, canvas: Canvas): ...

   class Circle(Drawable):
       def draw(self, canvas: Canvas):
           # implementation
   ```

   ```csharp
   // C#
   public interface IDrawable
   {
       void Draw(Canvas canvas);
   }

   public class Circle : IDrawable
   {
       public void Draw(Canvas canvas)
       {
           // implementation
       }
   }
   ```

4. **Property Generation**
   ```python
   # Auto property
   class Person:
       property name: str = ""
   ```

   ```csharp
   public class Person
   {
       public string Name { get; set; } = "";
   }
   ```

   ```python
   # Explicit property
   class Person:
       property age(self) -> int:
           return self._age
       property age(self, value: int):
           self._age = value
   ```

   ```csharp
   public class Person
   {
       private int _age;

       public int Age
       {
           get { return _age; }
           set { _age = value; }
       }
   }
   ```

5. **For Loop Translation**
   ```python
   # Sharpy
   for item in collection:
       process(item)
   ```

   ```csharp
   // C# - use IEnumerable<T>
   foreach (var item in collection)
   {
       Process(item);
   }
   ```

6. **Exception Translation**
   ```python
   # Sharpy
   try:
       risky_operation()
   except ValueError as e:
       handle_error(e)
   finally:
       cleanup()
   ```

   ```csharp
   // C#
   try
   {
       RiskyOperation();
   }
   catch (ValueError e)
   {
       HandleError(e);
   }
   finally
   {
       Cleanup();
   }
   ```

**Code Generation Strategy:**
- **Template-based approach**: Use string builders with indentation tracking
- **AST visitors**: Implement visitor pattern over typed AST
- **Incremental generation**: Generate to intermediate buffer, then format
- **Source mapping**: Emit `#line` directives for debugger support
- **Formatting**: Optional post-processing with `csharpier` or similar

**Quality Assurance:**
- Generated code should compile without warnings (when possible)
- Preserve semantic equivalence to Sharpy source
- Maintain debuggability through source maps
- Generate idiomatic C# (not literal translations)

---

### 2.5 Standard Library (.NET Runtime)

**Location:** `dotnet/src/Sharpy/`

**Purpose:** Provide Python-compatible runtime types and functions.

**Implementation Status:** ~40-50% complete

**Architecture:**
```
Sharpy.dll
├── Core Types
│   ├── Sharpy.Object (base class)
│   ├── Sharpy.Str (string with Python semantics)
│   ├── Sharpy.List<T> (95% complete)
│   ├── Sharpy.Dict<K,V> (70% - views broken)
│   ├── Sharpy.Set<T> (90% complete)
│   ├── Sharpy.Bytes
│   └── Sharpy.ByteArray
├── Protocol Interfaces (32 total)
│   ├── IHashable
│   ├── IEquatable<T>
│   ├── IIterable<T>
│   ├── ISequence<T>
│   └── ... (28 more)
├── Exceptions (5 implemented)
│   ├── StopIteration
│   ├── ValueError
│   ├── TypeError
│   ├── IndexError
│   └── KeyError
├── Operators (12 implemented)
│   ├── IAddable<T, R>
│   ├── ISubtractable<T, R>
│   └── ... (10 more)
├── Builtins (11 implemented, 35+ missing)
│   ├── Print() ✅
│   ├── Len() ✅
│   ├── Range() ❌ MISSING
│   ├── Enumerate() ❌ MISSING
│   └── Zip() ❌ MISSING
└── Itertools (3 implemented)
    ├── Count()
    ├── Cycle()
    └── Repeat()
```

**Key Implementation Details:**

1. **Partial Classes**
   - Types split across multiple files: `Partial.List/List.cs`, `List.Sequence.cs`, etc.
   - Allows organizing 50+ methods per type

2. **Protocol-Based Design**
   ```csharp
   public interface IHashable
   {
       int __Hash__();
   }

   public interface IEquatable<T>
   {
       bool __Eq__(T other);
       bool __Ne__(T other);
   }
   ```

3. **Sharpy.Exports Class**
   ```csharp
   public static partial class Exports
   {
       public static void Print(Object? obj, uint file = Stdout, bool flush = false)
       {
           var result = obj?.__Str__() ?? "None";
           _Print(result, file, flush);
       }

       public static int Len(ICollection<object> collection)
       {
           return collection.__Len__();
       }
   }
   ```

**Critical Gaps (see `docs/stdlib-implementation-status.md`):**
- ❌ Dict.Items() and Dict.Values() throw NotImplementedException
- ❌ DictKeyView has 14 unimplemented methods
- ❌ Str.Encode() returns literal "TODO" string
- ❌ 35+ essential builtins missing (range, enumerate, zip, input, etc.)
- ❌ No file I/O system at all
- ❌ No async/await support

**Integration Points:**
- All types implement C# interfaces (IEnumerable<T>, ICollection<T>)
- C# LINQ works with Sharpy collections
- Exception hierarchy compatible with System.Exception
- Implicit conversions between Sharpy.Str and string

---

### 2.6 Builtin Symbol Generator (Planned Build Tool)

**Planned Location:** `rust/build_tools/generate_builtins/`

**Purpose:** Auto-generate Rust symbol definitions from Sharpy.dll via reflection.

**Architecture (from `docs/dotnet-builtin-reflection-spec.md`):**

```
┌──────────────────────────────────────────────────────────────┐
│                  Builtin Generation Workflow                 │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Build .NET Standard Library                             │
│     cd dotnet && dotnet build                               │
│     Output: dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll  │
│                                                              │
│  2. Run Reflection Tool (Rust + .NET Hosting)               │
│     rust/build.rs triggers build_tools/generate_builtins    │
│                                                              │
│  3. .NET Reflection Helper (C# code, hosted by Rust)        │
│     ├─ Load Sharpy.dll                                      │
│     ├─ Iterate types, methods, properties                   │
│     ├─ Extract metadata (params, return types, etc.)        │
│     └─ Serialize to JSON                                    │
│                                                              │
│  4. Rust Code Generator                                     │
│     ├─ Parse JSON metadata                                  │
│     ├─ Generate Rust Symbol definitions                     │
│     └─ Write to src/semantic/generated_builtins.rs          │
│                                                              │
│  5. Integration                                             │
│     ├─ Semantic analyzer imports generated_builtins         │
│     └─ Symbol table pre-populated with builtin types        │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**Generated Output Example:**
```rust
// Auto-generated from Sharpy.dll - DO NOT EDIT MANUALLY
use crate::semantic::{Symbol, SymbolKind, SemanticType, BuiltinType};

pub fn populate_builtins(symbol_table: &mut SymbolTable) {
    // List<T> type
    symbol_table.add_builtin_class(
        "list",
        vec![],  // type params: T
        vec![
            ("append", method_list_append()),
            ("extend", method_list_extend()),
            ("pop", method_list_pop()),
            // ... 20+ more methods
        ],
    );

    // Global functions
    symbol_table.add_builtin_function("len", function_len());
    symbol_table.add_builtin_function("print", function_print());
    // ... more functions
}

fn method_list_append() -> Symbol {
    Symbol::builtin_method(
        "append",
        vec![SemanticType::Generic {
            base: BuiltinType::List,
            args: vec![SemanticType::TypeParam("T".to_string())]
        }],
        SemanticType::Builtin(BuiltinType::None),
    )
}
```

**Benefits:**
- ✅ Single source of truth (Sharpy.dll)
- ✅ Eliminates manual sync between C# and Rust
- ✅ Scales to 1000+ methods across 50+ types
- ✅ Always up-to-date with standard library changes

**Challenges:**
- Requires .NET hosting in Rust build script
- Must handle generic type parameters correctly
- Needs robust error handling (DLL not built yet, etc.)
- Build time increases (acceptable tradeoff)

---

## 3. Name Mangling System

### 3.1 Conversion Rules

| Sharpy Context | Input | Output | Rule |
|----------------|-------|--------|------|
| Module name | `math_utils` | `MathUtils` | `snake_case` → `PascalCase` |
| Class name | `MyClass` | `MyClass` | No change (`PascalCase`) |
| Protocol name | `Encodable` | `IEncodable` | Prefix with `I` |
| Method name | `add_numbers` | `AddNumbers` | `snake_case` → `PascalCase` |
| Parameter name | `first_value` | `firstValue` | `snake_case` → `camelCase` |
| Enum value | `READY_STATE` | `ReadyState` | `CAPS_SNAKE` → `PascalCase` |
| Local variable | `temp_val` | `temp_val` | No change (local scope) |
| Constant | `MAX_SIZE` | `MAX_SIZE` | No change |
| Literal name | `` `ExactName` `` | `ExactName` | Preserve exact casing |

### 3.2 Implementation Strategy

**Phase 1: Lexer**
- Detect backtick-surrounded names, set `is_literal` flag
- Store underscore hints (`_name`, `__name`) in token metadata

**Phase 2: Parser**
- Preserve original names in AST nodes
- Store naming context (module, class, parameter, etc.)

**Phase 3: Semantic Analysis**
- No mangling during analysis
- Symbol table uses Sharpy names
- Type resolution uses Sharpy naming conventions

**Phase 4: Code Generation**
- Mangle names based on context
- Check for collisions (e.g., `process_data` vs `ProcessData`)
- Generate C# with mangled names
- Preserve literal names (backtick names) exactly

### 3.3 Collision Detection

```rust
struct NameMangler {
    mangled_names: HashMap<String, String>,  // Original -> Mangled
    used_names: HashSet<String>,              // Track all C# names
}

impl NameMangler {
    fn mangle(&mut self, original: &str, context: NameContext) -> Result<String, Error> {
        let mangled = match context {
            NameContext::TypeName => to_pascal_case(original),
            NameContext::MethodName => to_pascal_case(original),
            NameContext::Parameter => to_camel_case(original),
            NameContext::Literal => original.to_string(),  // No mangling
        };

        if self.used_names.contains(&mangled) {
            // Collision detected!
            return Err(Error::NameCollision {
                original: original.to_string(),
                conflicts_with: self.find_conflicting_original(&mangled),
            });
        }

        self.used_names.insert(mangled.clone());
        self.mangled_names.insert(original.to_string(), mangled.clone());
        Ok(mangled)
    }
}
```

### 3.4 Edge Cases

1. **Ambiguous Collisions**
   ```python
   # Sharpy - these collide!
   def get_value(): pass
   def getValue(): pass
   ```
   **Solution:** Compile-time error with helpful message

2. **Reserved Words**
   ```python
   # Sharpy - 'class' is reserved in both languages
   def `class`(): pass  # Use backticks
   ```
   **C# Output:**
   ```csharp
   public static void @class() { }  // C# @-prefix
   ```

3. **Acronyms**
   ```python
   def parse_xml_file(): pass
   ```
   **Options:**
   - `ParseXmlFile` (keep acronym uppercase - chosen approach)
   - `ParseXMLFile` (all caps acronym)
   - Configurable via compiler flag

---

## 4. Integration with .NET Ecosystem

### 4.1 ABI Compatibility

**Goal:** Sharpy code is fully callable from C# and vice versa.

**Sharpy → C# Interop:**
```python
# Sharpy: math_utils.spy
class Calculator:
    @static
    def add(x: int, y: int) -> int:
        return x + y
```

```csharp
// C# consumer
using MathUtils;

int result = Calculator.Add(5, 3);  // Works seamlessly
```

**C# → Sharpy Interop:**
```csharp
// C# library
namespace ExternalLib;
public class Helper
{
    public static string FormatValue(int value) => $"Value: {value}";
}
```

```python
# Sharpy consumer
from external_lib import Helper

result = Helper.format_value(42)  # Name resolves with mangling
```

### 4.2 Namespace Mapping

| Sharpy | C# |
|--------|-----|
| `import math_utils` | `using MathUtils;` |
| `from data.models import User` | `using Data.Models; // User` |
| `import system.io as io` | `using System.IO;` (alias) |

### 4.3 Exception Compatibility

```python
# Sharpy
class CustomError(Exception):
    pass

def risky():
    raise CustomError("something went wrong")
```

```csharp
// C# - can catch Sharpy exceptions
try
{
    Risky();
}
catch (CustomError e)
{
    Console.WriteLine(e.Message);
}
```

All Sharpy exceptions inherit from `Sharpy.Exception` which inherits from `System.Exception`.

---

## 5. Build System Integration

### 5.1 Current Build Process

```bash
# 1. Build standard library
cd dotnet
dotnet build

# 2. Build Rust compiler (includes builtin generation in future)
cd ../rust
cargo build --release

# 3. Compile Sharpy source
./target/release/sharpyc input.spy --output output.cs

# 4. Compile C# output
csc output.cs -r:../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll

# 5. Run
dotnet output.dll
```

### 5.2 Planned Build Process

```bash
# One-step compilation
sharpyc build input.spy -o output.dll

# What happens internally:
# 1. Lex & parse Sharpy source
# 2. Semantic analysis with generated builtins
# 3. Generate C# source
# 4. Invoke dotnet build with Sharpy.dll reference
# 5. Output .NET assembly
```

### 5.3 Cargo Build Script Integration

**File:** `rust/build.rs`

```rust
fn main() {
    // 1. Check if Sharpy.dll exists
    let sharpy_dll = "../dotnet/src/Sharpy/bin/Debug/net9.0/Sharpy.dll";

    if !Path::new(sharpy_dll).exists() {
        eprintln!("Warning: Sharpy.dll not found. Build it first:");
        eprintln!("  cd ../dotnet && dotnet build");
        return;
    }

    // 2. Run builtin generator
    let status = Command::new("./build_tools/generate_builtins")
        .arg("--assembly").arg(sharpy_dll)
        .arg("--output").arg("src/semantic/generated_builtins.rs")
        .status()
        .expect("Failed to run builtin generator");

    assert!(status.success(), "Builtin generation failed");

    // 3. Trigger rebuild if DLL changes
    println!("cargo:rerun-if-changed={}", sharpy_dll);
}
```

---

## 6. Testing Strategy

### 6.1 Unit Tests (Rust)

**Current Coverage:** ~300 tests across lexer, parser, semantic analysis

**Test Organization:**
```
rust/tests/
├── test_lexer.rs              # Token generation
├── test_parser.rs             # AST construction
├── test_type_parsing.rs       # Type annotation parsing
├── test_semantic_analysis.rs  # Type checking
├── test_generic_types.rs      # Generic type inference
├── test_function_calls.rs     # Call validation
└── test_attribute_access.rs   # Method resolution
```

**Example Test:**
```rust
#[test]
fn test_list_method_type_inference() {
    let source = r#"
numbers: list[int] = [1, 2, 3]
numbers.append(4)
first = numbers[0]
"#;

    let mut analyzer = MultiPassAnalyzer::new();
    let result = analyzer.analyze_source(source, Some("test".to_string()));

    assert!(result.is_ok());

    // Verify 'first' has type int
    let first_symbol = analyzer.lookup_symbol("first").unwrap();
    assert_eq!(first_symbol.symbol_type, SemanticType::Builtin(BuiltinType::Int));
}
```

### 6.2 Integration Tests (C# + Sharpy)

**Planned Structure:**
```
tests/integration/
├── basic/
│   ├── hello_world.spy
│   ├── hello_world.expected.cs
│   └── test.sh
├── classes/
│   ├── simple_class.spy
│   ├── simple_class.expected.cs
│   └── test.sh
└── stdlib/
    ├── list_operations.spy
    ├── list_operations.expected.output
    └── test.sh
```

**Test Workflow:**
1. Compile `.spy` → `.cs`
2. Compare generated C# with expected (optional)
3. Build C# with Sharpy.dll reference
4. Run and verify output
5. Assert no compilation warnings

### 6.3 End-to-End Tests

**Scenario:** Full project compilation

```bash
# Test case: Multi-file Sharpy project
tests/e2e/calculator_app/
├── main.spy
├── math_utils.spy
├── models.spy
└── expected_output.txt

# Test script
sharpyc build main.spy --project
dotnet calculator.dll
diff <(dotnet calculator.dll) expected_output.txt
```

---

## 7. Important Gotchas & Challenges

### 7.1 Semantic Differences Python ↔ C#

| Feature | Python | C# | Sharpy Approach |
|---------|--------|-----|-----------------|
| Integer division | `5 / 2 = 2.5` | `5 / 2 = 2` | Use Python semantics (`/` is float, `//` is int) |
| String mutability | Immutable | Immutable | ✅ Same |
| List mutability | Mutable | Mutable (List<T>) | ✅ Same |
| None vs null | `None` | `null` | ✅ Map `None` → `null` |
| Multiple inheritance | Yes | No | ❌ Use protocols for interfaces |
| Properties | `@property` | `{ get; set; }` | ✅ Sharpy property syntax |
| Exceptions | `except` | `catch` | ✅ Map in codegen |
| Range | Lazy iterator | IEnumerable | ⚠️ Need custom Range type |

### 7.2 Type System Challenges

1. **Dynamic Typing Remnants**
   ```python
   # Python allows this
   x = 5
   x = "hello"  # Type changes

   # Sharpy should reject this
   x: int = 5
   x = "hello"  # ERROR: Cannot assign str to int
   ```

2. **None Handling**
   ```python
   # Sharpy
   def maybe_value() -> int?:
       return None

   value = maybe_value()
   # Must check before use
   if value is not None:
       print(value + 1)
   ```

   **C# Output:**
   ```csharp
   public static int? MaybeValue()
   {
       return null;
   }

   var value = MaybeValue();
   if (value != null)
   {
       Print(value.Value + 1);
   }
   ```

3. **List Covariance**
   ```python
   # Python allows
   ints: list[int] = [1, 2, 3]
   objects: list[object] = ints  # Works in Python

   # C# does NOT allow (List<T> is invariant)
   List<int> ints = new List<int> { 1, 2, 3 };
   List<object> objects = ints;  // Compile error!
   ```

   **Sharpy Solution:** Explicit conversion or type error

### 7.3 Standard Library Gaps

**Critical Missing Features:**
- ❌ `range()` - fundamental for Python-style iteration
- ❌ `enumerate()` - essential pattern
- ❌ `zip()` - common operation
- ❌ File I/O (`open()`, `File` class)
- ❌ Dict views (`.items()`, `.values()`)
- ❌ String encoding/decoding

**Impact:** Many Python patterns won't compile until implemented.

### 7.4 Performance Considerations

1. **Boxing Overhead**
   ```csharp
   // Generic List<T> avoids boxing for value types
   List<int> numbers = new List<int>();  // ✅ No boxing

   // But Sharpy.Object inheritance might cause issues
   List<Sharpy.Object> objects = new List<Sharpy.Object>();
   objects.Add(5);  // ❌ Boxes int to Object
   ```

2. **String Allocation**
   - Sharpy.Str wraps C# string
   - Each wrap allocates memory
   - Implicit conversions reduce overhead

3. **Iteration Performance**
   ```python
   # Sharpy
   for i in range(1000000):
       process(i)
   ```

   **Implementation matters:**
   - Custom `Range` struct: ✅ Zero allocation, fast
   - IEnumerable wrapper: ❌ Allocates enumerator per iteration

### 7.5 Debugging Challenges

1. **Source Mapping**
   - Must emit `#line` directives in C#
   - Maps C# line numbers back to `.spy` files
   - Essential for debugger integration

2. **Name Mangling Confusion**
   ```python
   # Sharpy stack trace
   at add_numbers() in math_utils.spy:42

   # C# stack trace (without source mapping)
   at MathUtils.__Module__.AddNumbers() in math_utils.cs:67
   ```

   **Solution:** Source maps + IDE integration

3. **Error Messages**
   - Compile errors should reference Sharpy code, not generated C#
   - Type errors should use Sharpy type names (`list[int]`, not `List<int>`)

---

## 8. Future Roadmap

### 8.1 Short-term (Next 3-6 months)

1. **Complete Code Generation**
   - Implement basic C# emitter
   - Name mangling engine
   - Module structure generation
   - Control flow translation

2. **Standard Library Critical Path**
   - Implement `range()`, `enumerate()`, `zip()`
   - Fix Dict views (`.items()`, `.values()`)
   - Fix `Str.encode()`
   - Basic file I/O

3. **Builtin Generation Tool**
   - Build reflection tool
   - Integrate into Rust build
   - Auto-populate symbol table

### 8.2 Mid-term (6-12 months)

1. **CIL Backend**
   - Replace C# codegen with direct CIL emission
   - Use `iced-x86` or similar library
   - Bypass C# compiler entirely
   - Faster compilation, more control

2. **Advanced Type System**
   - Generic constraints
   - Protocol conformance validation
   - Type inference improvements
   - Operator overload resolution

3. **Standard Library Completeness**
   - All Python builtins implemented
   - Async/await support
   - Comprehensive exception system
   - Reflection and introspection

### 8.3 Long-term (12+ months)

1. **Optimization**
   - Inline simple functions
   - Dead code elimination
   - Constant folding
   - Tail call optimization

2. **Tooling**
   - Language Server Protocol (LSP) implementation
   - IDE integration (VS Code, Visual Studio)
   - Debugger integration
   - Package manager

3. **Interop Enhancements**
   - Automatic C# library binding generation
   - NuGet package consumption
   - Attribute mapping
   - P/Invoke support

---

## 9. Key Design Decisions

### 9.1 Why Multi-Pass Semantic Analysis?

**Problem:** Forward references and circular dependencies

```python
# Function uses class defined later
def create_user(name: str) -> User:
    return User(name)

# Class defined after function
class User:
    def __init__(self, name: str):
        self.name = name
```

**Solution:** 3-pass analyzer
1. **Declaration Pass:** Collect all symbols (User class exists)
2. **Import Pass:** Resolve cross-module references
3. **Type Pass:** Validate types (User is known, check constructor)

### 9.2 Why Transpile to C# First (Not Direct CIL)?

**Rationale:**
1. **Faster Development:** C# is higher-level than CIL
2. **Debugging:** Generated C# can be inspected and debugged
3. **Validation:** C# compiler catches codegen bugs
4. **Incremental Path:** Proven intermediate representation

**Trade-offs:**
- ❌ Slower compilation (two-stage)
- ❌ Loss of control (C# compiler optimizations)
- ✅ Easier to debug
- ✅ Can leverage C# tooling

**Future:** Once stable, replace with direct CIL emission.

### 9.3 Why Rust for Compiler?

**Rationale:**
1. **Performance:** Lexer/parser/analyzer are CPU-intensive
2. **Safety:** Memory safety prevents crashes during compilation
3. **Ecosystem:** Excellent parsing libraries (logos, nom, pest)
4. **Tooling:** Cargo build system, integrated testing

**Alternatives Considered:**
- Python: Too slow for production compiler
- C++: Memory safety issues, harder to maintain
- F#: Great fit but less ecosystem support

### 9.4 Why Custom Standard Library (Not Wrapper)?

**Problem:** Python semantics ≠ .NET semantics

**Example:**
```python
# Python dict preserves insertion order (3.7+)
d = {"a": 1, "b": 2}
# Guaranteed to iterate in insertion order

# C# Dictionary<K,V> does NOT preserve order
var d = new Dictionary<string, int> { {"a", 1}, {"b", 2} };
// Order is NOT guaranteed
```

**Solution:** Custom `Sharpy.Dict<K,V>` using `OrderedDictionary` or custom implementation.

**Benefits:**
- ✅ Exact Python semantics
- ✅ Control over API surface
- ✅ Can add Sharpy-specific features

**Trade-offs:**
- ❌ Must implement and maintain 50+ types
- ❌ Reinventing some wheels
- ❌ Potential performance differences

---

## 10. Development Workflow

### 10.1 Adding a New Language Feature

**Example:** Implement optional chaining operator `?.`

1. **Lexer** (`rust/src/lexer/`)
   ```rust
   // Already lexed as QuestionDot token
   TokenType::QuestionDot => { /* exists */ }
   ```

2. **Parser** (`rust/src/parser/parse.rs`)
   ```rust
   fn parse_postfix(&mut self) -> Result<Node, ParseError> {
       // ...
       if self.match_token(&TokenType::QuestionDot) {
           self.advance();
           return self.parse_optional_chain(expr);
       }
   }
   ```

3. **AST** (`rust/src/ast/node.rs`)
   ```rust
   pub struct OptionalChain {
       pub value: Box<Node>,
       pub attr: String,
       pub source: Option<NodeSource>,
   }
   ```

4. **Semantic Analysis** (`rust/src/semantic/passes/type_pass.rs`)
   ```rust
   fn infer_optional_chain(&mut self, chain: &OptionalChain) -> SemanticType {
       let value_type = self.infer_expression_type(&chain.value)?;
       // Type must be optional
       if !is_optional_type(&value_type) {
           return Err(SemanticError::InvalidOperation { ... });
       }
       // Result type is attribute type wrapped in Optional
       let attr_type = resolve_attribute(&value_type, &chain.attr)?;
       SemanticType::Optional(Box::new(attr_type))
   }
   ```

5. **Code Generation** (`rust/src/codegen/`)
   ```rust
   fn generate_optional_chain(&mut self, chain: &OptionalChain) -> String {
       let obj = self.generate_expression(&chain.value);
       let attr = mangle_name(&chain.attr, NameContext::Member);
       format!("{}?.{}", obj, attr)  // C# also has ?.
   }
   ```

6. **Tests**
   ```rust
   #[test]
   fn test_optional_chaining() {
       let source = r#"
   user: User? = get_user()
   name = user?.name
   "#;
       // Assert type of 'name' is str?
   }
   ```

### 10.2 Adding a Builtin Function

**Example:** Implement `enumerate()`

1. **Standard Library** (`dotnet/src/Sharpy/Enumerate.cs`)
   ```csharp
   public static partial class Exports
   {
       public static EnumerateIterator<T> Enumerate<T>(
           IIterable<T> iterable,
           int start = 0)
       {
           return new EnumerateIterator<T>(iterable, start);
       }
   }

   public class EnumerateIterator<T> : Iterator<(int, T)>
   {
       // Implementation...
   }
   ```

2. **Reflection Metadata** (when generator tool is built)
   ```csharp
   [SharpyBuiltin]
   public static EnumerateIterator<T> Enumerate<T>(...)
   ```

3. **Symbol Table** (manual for now, auto-generated later)
   ```rust
   // In generated_builtins.rs (or hardcoded temporarily)
   symbol_table.add_builtin_function(
       "enumerate",
       Symbol::new(
           "enumerate",
           SymbolKind::Function,
           SemanticType::Function {
               params: vec![SemanticType::Generic { ... }],
               return_type: Box::new(SemanticType::Generic {
                   base: BuiltinType::Iterator,
                   args: vec![SemanticType::Tuple(vec![...])]
               }),
           },
           // ...
       )
   );
   ```

4. **Tests**
   ```python
   # Sharpy test
   for i, value in enumerate(["a", "b", "c"]):
       print(f"{i}: {value}")
   # Output: 0: a, 1: b, 2: c
   ```

### 10.3 Fixing a Bug

**Example:** Dict subscript returns wrong type

1. **Reproduce** - Write failing test
   ```rust
   #[test]
   fn test_dict_subscript_type() {
       let source = r#"
   data: dict[str, int] = {"a": 1}
   value = data["a"]
   "#;
       // Assert 'value' has type int, not dict[str, int]
   }
   ```

2. **Locate** - Find type inference code
   ```rust
   // In type_pass.rs::infer_subscript_type
   fn infer_subscript_type(&mut self, subscript: &Subscript) -> SemanticType {
       let value_type = self.infer_expression_type(&subscript.value)?;
       match value_type {
           SemanticType::Generic { base: BuiltinType::Dict, args } => {
               // BUG: Was returning whole dict type
               return args.get(1).cloned()  // FIX: Return value type (second arg)
                   .unwrap_or(SemanticType::Unknown);
           }
           // ...
       }
   }
   ```

3. **Fix** - Update logic

4. **Test** - Verify fix
   ```bash
   cargo test test_dict_subscript_type
   ```

5. **Regression Test** - Ensure other tests still pass
   ```bash
   cargo test
   ```

---

## 11. Documentation & Resources

### 11.1 Existing Documentation

- **`docs/specification.md`** - Sharpy language specification
- **`docs/feature_support.md`** - Implementation status
- **`docs/dotnet-builtin-reflection-spec.md`** - Builtin generation design
- **`docs/stdlib-implementation-status.md`** - Standard library gaps
- **`rust/MODULE_PARSING.md`** - Module system notes

### 11.2 Code Organization Reference

```
sharpy/
├── rust/                  # Rust compiler toolchain
│   ├── src/
│   │   ├── lexer/        # Tokenization
│   │   ├── parser/       # AST construction
│   │   ├── ast/          # AST node definitions
│   │   ├── semantic/     # Type checking & analysis
│   │   │   ├── passes/   # Multi-pass analyzer
│   │   │   └── ...
│   │   └── main.rs       # CLI entry point
│   ├── tests/            # 300+ unit tests
│   └── build.rs          # Build script (future: builtin gen)
├── dotnet/               # .NET standard library
│   └── src/Sharpy/       # 167 C# files
│       ├── Partial.List/ # List<T> implementation
│       ├── Partial.Str/  # Str implementation
│       ├── Collections/  # Interfaces & protocols
│       └── ...
├── docs/                 # Specifications & design docs
├── snippets/             # Test Sharpy programs
└── build_tools/          # Build utilities (future)
```

### 11.3 Learning Resources

**For Contributors:**
1. Read `docs/specification.md` for language design
2. Study `rust/tests/` for usage examples
3. Review `docs/dotnet-builtin-reflection-spec.md` for integration design
4. Check `docs/stdlib-implementation-status.md` for gaps

**For Users (future):**
1. Getting Started Guide (planned)
2. Standard Library Reference (planned)
3. Migration Guide from Python (planned)

---

## 12. Summary

The Sharpy transpiler toolchain is a sophisticated multi-component system designed to bring statically-typed Python semantics to the .NET ecosystem. The architecture emphasizes:

1. **Correctness First:** Multi-pass semantic analysis ensures type safety
2. **Maintainability:** Clear separation between frontend (Rust) and runtime (C#)
3. **Pythonic Feel:** Custom standard library preserves Python semantics
4. **C# Interop:** Name mangling and ABI compatibility for seamless integration
5. **Future-Proof:** Designed for eventual CIL emission backend

**Current State (v0.1.0):**
- ✅ Lexer: Complete
- ✅ Parser: 95% complete
- ✅ Semantic Analysis: 80% complete
- ❌ Code Generation: Not started
- ⚠️ Standard Library: 40-50% complete

**Critical Next Steps:**
1. Implement C# code generator
2. Build builtin symbol generator tool
3. Complete critical standard library gaps (range, enumerate, Dict views)
4. Integration testing infrastructure

**Key Challenges Ahead:**
- Name mangling collision detection
- Type system semantic differences (Python ↔ C#)
- Standard library performance optimization
- Debugger integration with source mapping

The architecture is sound and the foundation is solid. The next major milestone is completing the code generation backend to produce runnable .NET assemblies.

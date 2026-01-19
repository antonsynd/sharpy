# Sharpy Compiler Architecture Review & Recommendations

## Executive Summary

The compiler has grown organically with strong individual components, but suffers from **fragmented organization**, **inconsistent patterns**, and **an ad-hoc data model** for representing compilation artifacts. The core issues are:

1. **No unified compilation model** - Projects, modules, files, and their relationships are represented differently in different places
2. **Scattered validation logic** - Semantic checks live in 8+ different validator classes with inconsistent patterns
3. **Type system fragmentation** - Type information is stored across `Symbol`, `SemanticType`, and `TypeSymbol` with unclear ownership
4. **State-heavy code generation** - `RoslynEmitter` tracks 15+ internal sets/dictionaries for name management
5. **Missing centralized services** - Common operations like type resolution, name lookup, and error reporting are done ad-hoc

---

## Detailed Analysis

### 1. Data Model Problems

#### 1.1 No Clear Compilation Unit Abstraction

Currently, the relationship between these entities is unclear:

```
Project (.spyproj)
  └── Module (file scope)
       └── Module (import namespace)
            └── Symbol (declaration)
                 └── Type (semantic type)
```

**Problems:**
- `Module` (AST) ≠ `ModuleSymbol` (imported namespace) - confusing naming
- No single class represents "a compiled Sharpy file with its symbols, types, and errors"
- `ProjectCompiler` maintains separate dictionaries for modules, metrics, and errors
- Cross-file type references depend on `DefiningFilePath` strings, not object references

**Current code showing the problem (from `ProjectCompiler.cs`):**
```csharp
private Dictionary<string, Module> _parsedModules = new();
private Dictionary<string, CompilationMetrics> _fileMetrics = new();
private List<string> _errors = new();
```

Three separate dictionaries keyed by file path with no connection between them.

#### 1.2 Type System Has Three Overlapping Hierarchies

| Class | Purpose | Problem |
|-------|---------|---------|
| `TypeAnnotation` (AST) | Syntax representation of types | Just syntax, no semantics |
| `SemanticType` | Resolved type during type checking | Abstract, many subclasses |
| `TypeSymbol` | Declaration of a user-defined type | Contains members, inheritance |

**Example confusion:**
- `UserDefinedType.Symbol` points to `TypeSymbol`
- `TypeSymbol.ClrType` points to reflection `System.Type`
- `GenericType.GenericDefinition` points to `TypeSymbol`
- But `BuiltinType.ClrType` is a direct `System.Type`

No single source of truth for "what is this type."

#### 1.3 Symbol Ownership is Unclear

`TypeSymbol` contains:
- `Fields: List<VariableSymbol>`
- `Methods: List<FunctionSymbol>`
- `OperatorMethods: Dictionary<string, List<FunctionSymbol>>`
- `ProtocolMethods: Dictionary<string, List<FunctionSymbol>>`
- `Constructors: List<FunctionSymbol>`

But the `SymbolTable` also tracks symbols. Are `TypeSymbol.Methods` the same objects as those in `SymbolTable`? When are they updated? This coupling is implicit.

---

### 2. Validator Proliferation

The semantic phase has accumulated **8 different validator classes**:

| Validator | Location | Purpose |
|-----------|----------|---------|
| `TypeChecker` | TypeChecker.cs + 4 partials | Main type checking |
| `NameResolver` | NameResolver.cs | Declaration pass |
| `TypeResolver` | TypeResolver.cs | Type annotation resolution |
| `OperatorValidator` | OperatorValidator.cs | Binary/unary operator checking |
| `ProtocolValidator` | ProtocolValidator.cs | __len__, __iter__, etc. |
| `AccessValidator` | AccessValidator.cs | public/private/protected |
| `ControlFlowValidator` | ControlFlowValidator.cs | break/continue/return |
| `DefaultParameterValidator` | DefaultParameterValidator.cs | Default arg checking |
| `OperatorSignatureValidator` | OperatorSignatureValidator.cs | Dunder signature checking |
| `ProtocolSignatureValidator` | ProtocolSignatureValidator.cs | Protocol signature checking |

**Problems:**
- Error collection differs: some use `List<SemanticError>`, TypeChecker aggregates from all
- Some validators cache results, others don't
- `TypeChecker.Errors` combines 7 different error sources with custom getter logic
- Some checks happen in `NameResolver`, some in `TypeChecker`, creating confusion about which pass does what

---

### 3. Code Generation State Explosion

`RoslynEmitter` tracks too much mutable state:

```csharp
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
private readonly HashSet<string> _moduleConstVariables = new();
private readonly HashSet<string> _moduleVariables = new();
private readonly HashSet<string> _moduleFieldNames = new();
private HashSet<string> _variablesWithExecutionOrderIssues = new();
private readonly HashSet<string> _classNames = new();
private readonly HashSet<string> _structNames = new();
private readonly HashSet<string> _stringEnumNames = new();
private readonly HashSet<string> _fromImportSymbols = new();
private readonly Dictionary<string, string> _importAliasToOriginal = new();
private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new();
```

This is a symptom of missing abstraction - the emitter is manually tracking what should be pre-computed during semantic analysis.

---

## Recommendations

### Recommendation 1: Introduce a Compilation Unit Model

Create a clear hierarchy that represents the compilation domain:

```csharp
// New file: src/Sharpy.Compiler/Model/CompilationUnit.cs

/// <summary>
/// Represents a single Sharpy source file and all its compilation artifacts.
/// This is the fundamental unit of compilation.
/// </summary>
public class CompilationUnit
{
    public string FilePath { get; }
    public string ModulePath { get; }  // e.g., "mypackage.helpers"

    // Parsing artifacts
    public Module Ast { get; }
    public IReadOnlyList<Token> Tokens { get; }

    // Semantic artifacts
    public Scope ModuleScope { get; }
    public IReadOnlyList<TypeDeclaration> DeclaredTypes { get; }
    public IReadOnlyList<FunctionDeclaration> DeclaredFunctions { get; }
    public IReadOnlyList<ImportDeclaration> Imports { get; }

    // Dependencies
    public IReadOnlyList<CompilationUnit> Dependencies { get; }

    // Generated output
    public string? GeneratedCSharp { get; set; }

    // Diagnostics
    public DiagnosticBag Diagnostics { get; }
}

/// <summary>
/// Represents a complete Sharpy project being compiled.
/// </summary>
public class ProjectModel
{
    public ProjectConfig Config { get; }
    public IReadOnlyList<CompilationUnit> Units { get; }

    // Cross-cutting concerns
    public GlobalSymbolTable GlobalSymbols { get; }
    public IReadOnlyList<AssemblyReference> ExternalReferences { get; }

    // Build order (topologically sorted by dependencies)
    public IReadOnlyList<CompilationUnit> BuildOrder { get; }
}
```

**Benefits:**
- Single place to find everything about a file
- Dependencies are explicit object references, not string paths
- Diagnostics are localized to their source
- Build order is computed once and stored

---

### Recommendation 2: Unify the Type System

Create a single `TypeInfo` class that is the source of truth:

```csharp
// New file: src/Sharpy.Compiler/Model/TypeInfo.cs

/// <summary>
/// Unified type representation. This is the single source of truth for type information.
/// </summary>
public abstract class TypeInfo
{
    public abstract string DisplayName { get; }
    public abstract bool IsNullable { get; }
    public abstract Type? ClrType { get; }

    public virtual bool IsAssignableTo(TypeInfo other) { ... }
    public virtual TypeInfo MakeNullable() { ... }
    public virtual TypeInfo UnwrapNullable() { ... }
}

public class PrimitiveTypeInfo : TypeInfo { /* int, str, float, bool */ }
public class ClassTypeInfo : TypeInfo { /* user-defined class */ }
public class GenericTypeInfo : TypeInfo { /* list[T], dict[K,V] */ }
public class FunctionTypeInfo : TypeInfo { /* (int, str) -> bool */ }
public class TupleTypeInfo : TypeInfo { /* (int, str, bool) */ }
public class NullableTypeInfo : TypeInfo { /* wraps another TypeInfo */ }
```

Then have `Symbol` reference `TypeInfo` instead of `SemanticType`:

```csharp
public record VariableSymbol : Symbol
{
    public TypeInfo Type { get; set; }  // Single source of truth
}
```

**Benefits:**
- One place to look up type information
- Clear distinction between type declaration (`TypeSymbol`) and type usage (`TypeInfo`)
- Eliminates duplicate hierarchies

---

### Recommendation 3: Consolidate Validators into a Validation Pipeline

Create a unified validation architecture:

```csharp
// New file: src/Sharpy.Compiler/Semantic/Validation/ISemanticValidator.cs

/// <summary>
/// Interface for all semantic validation passes.
/// </summary>
public interface ISemanticValidator
{
    /// <summary>
    /// Validates the compilation unit and adds diagnostics.
    /// </summary>
    void Validate(CompilationUnit unit, SemanticContext context);
}

/// <summary>
/// Shared context for all validators.
/// </summary>
public class SemanticContext
{
    public GlobalSymbolTable Symbols { get; }
    public TypeRegistry Types { get; }
    public DiagnosticBag Diagnostics { get; }

    // Caches shared across validators
    public ClrMemberCache ClrCache { get; }
    public OperatorResolutionCache OperatorCache { get; }
}

/// <summary>
/// Runs all validators in order.
/// </summary>
public class ValidationPipeline
{
    private readonly List<ISemanticValidator> _validators;

    public ValidationPipeline()
    {
        _validators = new List<ISemanticValidator>
        {
            new NameResolutionValidator(),
            new InheritanceValidator(),
            new TypeResolutionValidator(),
            new TypeCheckingValidator(),
            new OperatorValidator(),
            new ProtocolValidator(),
            new AccessValidator(),
            new ControlFlowValidator(),
        };
    }

    public void Validate(CompilationUnit unit)
    {
        var context = new SemanticContext(...);
        foreach (var validator in _validators)
        {
            validator.Validate(unit, context);
            if (context.Diagnostics.HasErrors && !ContinueOnError)
                break;
        }
    }
}
```

**Benefits:**
- All validators follow the same pattern
- Shared context prevents duplicate caching
- Easy to add/remove/reorder validators
- Diagnostics flow to a single bag

---

### Recommendation 4: Pre-compute Code Generation Information

Move name resolution and casing decisions to semantic analysis:

```csharp
// New file: src/Sharpy.Compiler/Semantic/CodeGenInfo.cs

/// <summary>
/// Information computed during semantic analysis for use during code generation.
/// Attached to symbols during type checking.
/// </summary>
public class CodeGenInfo
{
    /// <summary>The C# name to use for this symbol.</summary>
    public string CSharpName { get; }

    /// <summary>The fully qualified C# name including namespace.</summary>
    public string FullyQualifiedName { get; }

    /// <summary>If true, this is a module-level variable (becomes a static field).</summary>
    public bool IsModuleLevel { get; }

    /// <summary>If true, use CONSTANT_CASE.</summary>
    public bool IsConstant { get; }

    /// <summary>For redeclared variables, the version number (0 for first decl).</summary>
    public int Version { get; }
}

// Attached to symbols:
public record VariableSymbol : Symbol
{
    public CodeGenInfo? CodeGenInfo { get; set; }
}
```

Then `RoslynEmitter` becomes stateless:

```csharp
public class RoslynEmitter
{
    // No more tracking sets!

    private string GetCSharpName(Symbol symbol)
    {
        return symbol.CodeGenInfo?.CSharpName ?? symbol.Name;
    }
}
```

**Benefits:**
- Code generation is pure transformation from AST + CodeGenInfo → C#
- All naming decisions are made once during semantic analysis
- Easier to test semantic analysis and code generation independently

---

### Recommendation 5: Create a Service Layer

Introduce a `CompilerServices` class that provides common operations:

```csharp
// New file: src/Sharpy.Compiler/Services/CompilerServices.cs

/// <summary>
/// Central services used throughout compilation.
/// Provides caching, logging, and common operations.
/// </summary>
public class CompilerServices
{
    public ICompilerLogger Logger { get; }
    public DiagnosticBag Diagnostics { get; }

    // Type services
    public ITypeResolver TypeResolver { get; }
    public ITypeRegistry TypeRegistry { get; }

    // Symbol services
    public ISymbolLookup SymbolLookup { get; }

    // CLR interop
    public IClrTypeMapper ClrMapper { get; }
    public IClrMemberCache ClrCache { get; }

    // Common operations
    public TypeInfo ResolveType(TypeAnnotation annotation);
    public Symbol? LookupSymbol(string name, Scope scope);
    public bool CanAssign(TypeInfo from, TypeInfo to);
    public void ReportError(string message, Node location);
}
```

Pass this to all compiler phases:

```csharp
public class TypeChecker
{
    private readonly CompilerServices _services;

    public TypeChecker(CompilerServices services)
    {
        _services = services;
    }

    private void ReportError(string msg, Node node)
    {
        _services.ReportError(msg, node);  // Centralized error handling
    }
}
```

**Benefits:**
- Centralized error reporting with consistent formatting
- Shared caches prevent duplicate work
- Easy to mock for testing
- Clear dependencies via constructor injection

---

### Recommendation 6: Directory/File Organization

Reorganize the source files to reflect the architecture:

```
src/Sharpy.Compiler/
├── Model/                    # NEW: Core data model
│   ├── CompilationUnit.cs
│   ├── ProjectModel.cs
│   ├── TypeInfo.cs
│   ├── Symbol.cs             # Moved from Semantic/
│   └── Declarations/         # Type/Function/Import declarations
│
├── Parsing/                  # Renamed from Parser/
│   ├── Lexer/
│   ├── Parser/
│   └── Ast/                  # Moved from Parser/Ast/
│
├── Semantic/
│   ├── Analysis/             # Name/Type resolution
│   │   ├── NameResolver.cs
│   │   └── TypeResolver.cs
│   ├── Validation/           # All validators
│   │   ├── ISemanticValidator.cs
│   │   ├── ValidationPipeline.cs
│   │   ├── TypeValidator.cs
│   │   ├── OperatorValidator.cs
│   │   └── ...
│   └── Types/                # Type-related utilities
│       ├── TypeRegistry.cs
│       ├── PrimitiveCatalog.cs
│       └── TypeInference.cs
│
├── CodeGen/                  # Unchanged structure
│   ├── RoslynEmitter.cs
│   └── TypeMapper.cs
│
├── Services/                 # NEW: Shared services
│   ├── CompilerServices.cs
│   └── DiagnosticBag.cs
│
├── Project/                  # Project compilation
│   ├── ProjectCompiler.cs
│   └── ModuleDiscovery.cs
│
└── Compiler.cs               # Main entry point
```

---

## Implementation Priority

| Priority | Recommendation | Effort | Impact |
|----------|---------------|--------|--------|
| 1 | Consolidate validators into pipeline | Medium | High - reduces complexity |
| 2 | Create CompilerServices layer | Medium | High - enables future work |
| 3 | Pre-compute CodeGenInfo | Medium | High - simplifies RoslynEmitter |
| 4 | Introduce CompilationUnit model | Large | Medium - cleaner data model |
| 5 | Unify type system | Large | Medium - eliminates confusion |
| 6 | Reorganize directories | Small | Low - cosmetic but helpful |

I recommend starting with **#1 (Validation Pipeline)** and **#2 (CompilerServices)** as they provide the most immediate benefit and lay groundwork for further improvements. The `CompilationUnit` model (#4) is a larger change that should be tackled after the validation and services infrastructure is solid.

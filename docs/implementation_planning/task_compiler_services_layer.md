# Task List: Create CompilerServices Layer

**Goal:** Implement Recommendation 5 from the architecture review - create a centralized `CompilerServices` class that provides common operations used throughout compilation.

**Prerequisites:** None (this can be done in parallel with other recommendations)

**Estimated Total Effort:** 2-3 days

**Related Documents:**
- `architecture_review_and_recommendations.md` - Recommendation 5
- `architecture_review_addendum_future_features.md` - Future considerations
- `phases.md` - Implementation phases for context

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Interface-based design**: All services exposed via interfaces for testability and future swapping
2. **Optional CompilerServices adoption**: Components accept `CompilerServices` but fall back to legacy parameters if null
3. **Gradual migration**: Existing code continues to work; new code prefers `CompilerServices`

### One-Way Door Decisions (Commit Now)
1. **Thread-safe by default**: All caches and shared state use concurrent collections - this is correct for future parallel compilation
2. **Immutable configuration**: `CompilerServicesConfiguration` is immutable after construction - this supports LSP scenarios
3. **DiagnosticBag as the canonical diagnostic collection**: We commit to `DiagnosticBag` over `List<SemanticError>` for new code

---

## Phase 0: Preparation (30 minutes)

### Task 0.1: Verify Test Baseline
**File:** N/A (command line)
**Description:** Run all existing tests to establish a passing baseline.

```bash
cd /Users/anton/Documents/github/sharpy/src
dotnet test Sharpy.Compiler.Tests --no-build --verbosity minimal
```

**Verification:**
- [ ] All tests pass
- [ ] Note the total test count and time

**Commit:** Not needed yet

---

### Task 0.2: Create Feature Branch
**File:** N/A (git)
**Description:** Create a feature branch for this work.

```bash
git checkout -b feature/compiler-services-layer
```

**Commit:** Not needed yet

---

## Phase 1: Define Service Interfaces (1-2 hours)

The interfaces define the contract for CompilerServices. We create interfaces first to:
1. Enable test mocking immediately
2. Document the intended API
3. Allow parallel implementation work

### Task 1.1: Create ITypeResolver Interface
**File:** `src/Sharpy.Compiler/Services/ITypeResolver.cs` (NEW)
**Description:** Extract the type resolution interface from the existing `TypeResolver`.

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for resolving type annotations to semantic types.
/// Thread-safe for parallel compilation scenarios.
/// </summary>
public interface ITypeResolver
{
    /// <summary>
    /// Resolves a type annotation to its semantic type.
    /// Results are cached for efficiency.
    /// </summary>
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);
    
    /// <summary>
    /// Gets errors that occurred during type resolution.
    /// </summary>
    IReadOnlyList<SemanticError> Errors { get; }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 1.2: Create ISymbolLookup Interface
**File:** `src/Sharpy.Compiler/Services/ISymbolLookup.cs` (NEW)
**Description:** Define interface for symbol lookup operations.

```csharp
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for looking up symbols in the symbol table.
/// Provides a simplified, read-only view for consumers that don't need
/// to modify the symbol table.
/// </summary>
public interface ISymbolLookup
{
    /// <summary>
    /// Look up a symbol by name in the current scope and parent scopes.
    /// </summary>
    Symbol? Lookup(string name);
    
    /// <summary>
    /// Look up a type symbol by name.
    /// </summary>
    TypeSymbol? LookupType(string name);
    
    /// <summary>
    /// Look up a type alias by name.
    /// </summary>
    TypeAliasSymbol? LookupTypeAlias(string name);
    
    /// <summary>
    /// Look up a function symbol by name.
    /// </summary>
    FunctionSymbol? LookupFunction(string name);
    
    /// <summary>
    /// Check if a symbol exists in the current scope (not parent scopes).
    /// </summary>
    bool ExistsInCurrentScope(string name);
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 1.3: Create IClrTypeMapper Interface
**File:** `src/Sharpy.Compiler/Services/IClrTypeMapper.cs` (NEW)
**Description:** Define interface for CLR type mapping operations.

```csharp
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for mapping between Sharpy types and CLR types.
/// Uses caching for frequently accessed CLR type information.
/// </summary>
public interface IClrTypeMapper
{
    /// <summary>
    /// Get the CLR Type for a semantic type.
    /// </summary>
    Type? GetClrType(SemanticType semanticType);
    
    /// <summary>
    /// Get the semantic type for a CLR Type.
    /// </summary>
    SemanticType GetSemanticType(Type clrType);
    
    /// <summary>
    /// Check if a CLR type has a specific member (method, property, field).
    /// Results are cached.
    /// </summary>
    bool HasMember(Type clrType, string memberName);
    
    /// <summary>
    /// Get member information from a CLR type.
    /// Results are cached.
    /// </summary>
    System.Reflection.MemberInfo? GetMember(Type clrType, string memberName);
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 1.4: Create IDiagnosticReporter Interface
**File:** `src/Sharpy.Compiler/Services/IDiagnosticReporter.cs` (NEW)
**Description:** Define interface for centralized diagnostic reporting.

```csharp
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for reporting compilation diagnostics (errors, warnings, etc.).
/// Centralizes error reporting with consistent formatting.
/// </summary>
public interface IDiagnosticReporter
{
    /// <summary>
    /// Report an error with optional source location.
    /// </summary>
    void ReportError(string message, int? line = null, int? column = null);
    
    /// <summary>
    /// Report an error at a specific AST node's location.
    /// </summary>
    void ReportError(string message, Node node);
    
    /// <summary>
    /// Report a warning with optional source location.
    /// </summary>
    void ReportWarning(string message, int? line = null, int? column = null);
    
    /// <summary>
    /// Report a warning at a specific AST node's location.
    /// </summary>
    void ReportWarning(string message, Node node);
    
    /// <summary>
    /// Get all diagnostics reported so far.
    /// </summary>
    DiagnosticBag Diagnostics { get; }
    
    /// <summary>
    /// Check if any errors have been reported.
    /// </summary>
    bool HasErrors { get; }
    
    /// <summary>
    /// Current file path being compiled (for error messages).
    /// </summary>
    string? CurrentFilePath { get; set; }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 1.5: Create Services Directory Structure
**File:** `src/Sharpy.Compiler/Services/` (NEW directory)
**Description:** Ensure the directory exists and create a README.

```markdown
# Sharpy.Compiler/Services

This directory contains the centralized compiler services layer.

## Purpose

The services layer provides common operations used throughout compilation:
- **Type Resolution**: Resolving type annotations to semantic types
- **Symbol Lookup**: Finding symbols in the symbol table
- **CLR Mapping**: Mapping between Sharpy and .NET types
- **Diagnostics**: Centralized error and warning reporting

## Design Principles

1. **Interface-based**: All services are accessed via interfaces for testability
2. **Thread-safe**: Services are designed for future parallel compilation
3. **Cacheable**: Results are cached where appropriate for incremental compilation
4. **Backwards-compatible**: Existing code continues to work during migration

## Files

- `ITypeResolver.cs` - Type resolution service interface
- `ISymbolLookup.cs` - Symbol lookup service interface  
- `IClrTypeMapper.cs` - CLR type mapping service interface
- `IDiagnosticReporter.cs` - Diagnostic reporting service interface
- `CompilerServices.cs` - Main service container
- `CompilerServicesBuilder.cs` - Builder for constructing services
```

**Verification:**
- [ ] Directory exists
- [ ] README.md created

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Services/
git commit -m "feat(services): add service interfaces for CompilerServices layer

- Add ITypeResolver interface for type annotation resolution
- Add ISymbolLookup interface for symbol table queries
- Add IClrTypeMapper interface for CLR type mapping
- Add IDiagnosticReporter interface for centralized diagnostics
- Create Services directory with documentation

Part of CompilerServices layer implementation (Rec #5)"
```

---

## Phase 2: Implement Service Adapters (2-3 hours)

Adapters wrap existing implementations to implement the new interfaces.
This allows gradual migration without breaking existing code.

### Task 2.1: Create TypeResolverAdapter
**File:** `src/Sharpy.Compiler/Services/TypeResolverAdapter.cs` (NEW)
**Description:** Wrap existing `TypeResolver` to implement `ITypeResolver`.

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that wraps the existing TypeResolver to implement ITypeResolver.
/// This enables gradual migration to the new services architecture.
/// </summary>
public class TypeResolverAdapter : ITypeResolver
{
    private readonly TypeResolver _typeResolver;
    
    public TypeResolverAdapter(TypeResolver typeResolver)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
    }
    
    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        return _typeResolver.ResolveTypeAnnotation(annotation);
    }
    
    public IReadOnlyList<SemanticError> Errors => _typeResolver.Errors;
    
    /// <summary>
    /// Get the underlying TypeResolver for cases that need direct access.
    /// Use sparingly - prefer the interface methods.
    /// </summary>
    public TypeResolver UnderlyingResolver => _typeResolver;
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 2.2: Create SymbolLookupAdapter
**File:** `src/Sharpy.Compiler/Services/SymbolLookupAdapter.cs` (NEW)
**Description:** Wrap existing `SymbolTable` to implement `ISymbolLookup`.

```csharp
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that wraps the existing SymbolTable to implement ISymbolLookup.
/// Provides a read-only view of the symbol table.
/// </summary>
public class SymbolLookupAdapter : ISymbolLookup
{
    private readonly SymbolTable _symbolTable;
    
    public SymbolLookupAdapter(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
    }
    
    public Symbol? Lookup(string name)
    {
        return _symbolTable.Lookup(name);
    }
    
    public TypeSymbol? LookupType(string name)
    {
        return _symbolTable.LookupType(name);
    }
    
    public TypeAliasSymbol? LookupTypeAlias(string name)
    {
        return _symbolTable.LookupTypeAlias(name);
    }
    
    public FunctionSymbol? LookupFunction(string name)
    {
        return _symbolTable.Lookup(name) as FunctionSymbol;
    }
    
    public bool ExistsInCurrentScope(string name)
    {
        return _symbolTable.Lookup(name, searchParents: false) != null;
    }
    
    /// <summary>
    /// Get the underlying SymbolTable for cases that need direct access.
    /// Use sparingly - prefer the interface methods.
    /// </summary>
    public SymbolTable UnderlyingTable => _symbolTable;
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 2.3: Create ClrTypeMapperAdapter
**File:** `src/Sharpy.Compiler/Services/ClrTypeMapperAdapter.cs` (NEW)
**Description:** Wrap existing `ClrMemberCache` and type mapping logic.

```csharp
using Sharpy.Compiler.Semantic;
using System.Reflection;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Adapter that provides CLR type mapping using the existing ClrMemberCache.
/// </summary>
public class ClrTypeMapperAdapter : IClrTypeMapper
{
    private readonly ClrMemberCache _clrCache;
    
    public ClrTypeMapperAdapter(ClrMemberCache clrCache)
    {
        _clrCache = clrCache ?? throw new ArgumentNullException(nameof(clrCache));
    }
    
    public Type? GetClrType(SemanticType semanticType)
    {
        // Delegate to SemanticType's built-in CLR type resolution
        return semanticType switch
        {
            BuiltinType bt => bt.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            GenericType gt => GetGenericClrType(gt),
            NullableType nt => GetNullableClrType(nt),
            _ => null
        };
    }
    
    public SemanticType GetSemanticType(Type clrType)
    {
        // Map common CLR types to semantic types
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float32;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        if (clrType == typeof(void)) return SemanticType.Void;
        if (clrType == typeof(object)) return SemanticType.Object;
        
        // For other types, return unknown (caller should handle)
        return SemanticType.Unknown;
    }
    
    public bool HasMember(Type clrType, string memberName)
    {
        return _clrCache.GetMethod(clrType, memberName) != null 
            || _clrCache.GetProperty(clrType, memberName) != null;
    }
    
    public MemberInfo? GetMember(Type clrType, string memberName)
    {
        // Try method first, then property
        var method = _clrCache.GetMethod(clrType, memberName);
        if (method != null) return method;
        
        return _clrCache.GetProperty(clrType, memberName);
    }
    
    /// <summary>
    /// Get the underlying ClrMemberCache for cases that need direct access.
    /// </summary>
    public ClrMemberCache UnderlyingCache => _clrCache;
    
    private Type? GetGenericClrType(GenericType gt)
    {
        // Handle common generic types
        if (gt.GenericDefinition?.ClrType != null && gt.TypeArguments.Count > 0)
        {
            var typeArgs = gt.TypeArguments
                .Select(ta => GetClrType(ta))
                .Where(t => t != null)
                .ToArray();
            
            if (typeArgs.Length == gt.TypeArguments.Count)
            {
                try
                {
                    return gt.GenericDefinition.ClrType.MakeGenericType(typeArgs!);
                }
                catch
                {
                    return null;
                }
            }
        }
        return gt.GenericDefinition?.ClrType;
    }
    
    private Type? GetNullableClrType(NullableType nt)
    {
        var underlyingClr = GetClrType(nt.UnderlyingType);
        if (underlyingClr == null) return null;
        
        // For value types, wrap in Nullable<T>
        if (underlyingClr.IsValueType)
        {
            return typeof(Nullable<>).MakeGenericType(underlyingClr);
        }
        
        // Reference types are already nullable
        return underlyingClr;
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 2.4: Create DiagnosticReporter Implementation
**File:** `src/Sharpy.Compiler/Services/DiagnosticReporter.cs` (NEW)
**Description:** Implement `IDiagnosticReporter` with the existing `DiagnosticBag`.

```csharp
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Implementation of IDiagnosticReporter using DiagnosticBag.
/// Provides centralized, consistent error reporting.
/// </summary>
public class DiagnosticReporter : IDiagnosticReporter
{
    private readonly DiagnosticBag _diagnostics;
    private readonly ICompilerLogger _logger;
    
    public DiagnosticReporter(ICompilerLogger? logger = null)
    {
        _diagnostics = new DiagnosticBag();
        _logger = logger ?? NullLogger.Instance;
    }
    
    public DiagnosticReporter(DiagnosticBag diagnostics, ICompilerLogger? logger = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? NullLogger.Instance;
    }
    
    public void ReportError(string message, int? line = null, int? column = null)
    {
        _diagnostics.AddError(message, line, column, CurrentFilePath);
        _logger.LogError(message, line ?? 0, column ?? 0);
    }
    
    public void ReportError(string message, Node node)
    {
        ReportError(message, node.LineStart, node.ColumnStart);
    }
    
    public void ReportWarning(string message, int? line = null, int? column = null)
    {
        _diagnostics.AddWarning(message, line, column, CurrentFilePath);
        _logger.LogWarning(message, line ?? 0, column ?? 0);
    }
    
    public void ReportWarning(string message, Node node)
    {
        ReportWarning(message, node.LineStart, node.ColumnStart);
    }
    
    public DiagnosticBag Diagnostics => _diagnostics;
    
    public bool HasErrors => _diagnostics.HasErrors;
    
    public string? CurrentFilePath { get; set; }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Services/
git commit -m "feat(services): implement service adapters

- Add TypeResolverAdapter wrapping existing TypeResolver
- Add SymbolLookupAdapter wrapping existing SymbolTable
- Add ClrTypeMapperAdapter wrapping existing ClrMemberCache
- Add DiagnosticReporter implementation

Adapters enable gradual migration to CompilerServices pattern"
```

---

## Phase 3: Create CompilerServices Container (1-2 hours)

### Task 3.1: Create CompilerServicesConfiguration
**File:** `src/Sharpy.Compiler/Services/CompilerServicesConfiguration.cs` (NEW)
**Description:** Immutable configuration for the services container.

```csharp
namespace Sharpy.Compiler.Services;

/// <summary>
/// Immutable configuration for CompilerServices.
/// Set once at construction time.
/// </summary>
public sealed class CompilerServicesConfiguration
{
    /// <summary>
    /// Maximum number of errors before stopping compilation.
    /// Default: 100
    /// </summary>
    public int MaxErrors { get; init; } = 100;
    
    /// <summary>
    /// Whether to continue compilation after encountering errors.
    /// Default: true (collect all errors)
    /// </summary>
    public bool ContinueAfterErrors { get; init; } = true;
    
    /// <summary>
    /// Enable verbose logging of service operations.
    /// Default: false
    /// </summary>
    public bool VerboseLogging { get; init; } = false;
    
    /// <summary>
    /// Current file path for error reporting.
    /// Can be updated as files are processed.
    /// </summary>
    public string? InitialFilePath { get; init; }
    
    /// <summary>
    /// Default configuration with sensible defaults.
    /// </summary>
    public static CompilerServicesConfiguration Default { get; } = new();
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 3.2: Create CompilerServices Class
**File:** `src/Sharpy.Compiler/Services/CompilerServices.cs` (NEW)
**Description:** The main service container providing all compiler services.

```csharp
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Central services container used throughout compilation.
/// Provides caching, logging, and common operations.
/// 
/// Design notes:
/// - Thread-safe for future parallel compilation
/// - Services are lazily initialized where possible
/// - Configuration is immutable after construction
/// </summary>
public class CompilerServices
{
    private readonly CompilerServicesConfiguration _config;
    
    // Logging
    public ICompilerLogger Logger { get; }
    
    // Diagnostics
    public IDiagnosticReporter DiagnosticReporter { get; }
    
    // Type services
    public ITypeResolver TypeResolver { get; }
    
    // Symbol services
    public ISymbolLookup SymbolLookup { get; }
    
    // CLR interop
    public IClrTypeMapper ClrMapper { get; }
    
    // Underlying infrastructure (for components that need direct access during migration)
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }
    
    // Configuration
    public CompilerServicesConfiguration Configuration => _config;
    
    /// <summary>
    /// Current file path being processed. Can be updated as compilation progresses.
    /// </summary>
    public string? CurrentFilePath
    {
        get => DiagnosticReporter.CurrentFilePath;
        set => DiagnosticReporter.CurrentFilePath = value;
    }
    
    internal CompilerServices(
        CompilerServicesConfiguration config,
        ICompilerLogger logger,
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        ITypeResolver typeResolver,
        ISymbolLookup symbolLookup,
        IClrTypeMapper clrMapper,
        IDiagnosticReporter diagnosticReporter)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SymbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        SemanticInfo = semanticInfo ?? throw new ArgumentNullException(nameof(semanticInfo));
        TypeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        SymbolLookup = symbolLookup ?? throw new ArgumentNullException(nameof(symbolLookup));
        ClrMapper = clrMapper ?? throw new ArgumentNullException(nameof(clrMapper));
        DiagnosticReporter = diagnosticReporter ?? throw new ArgumentNullException(nameof(diagnosticReporter));
        
        // Apply initial configuration
        if (config.InitialFilePath != null)
        {
            CurrentFilePath = config.InitialFilePath;
        }
    }
    
    // =========================================================================
    // Convenience methods for common operations
    // =========================================================================
    
    /// <summary>
    /// Resolve a type annotation to its semantic type.
    /// Convenience wrapper around TypeResolver.ResolveTypeAnnotation.
    /// </summary>
    public SemanticType ResolveType(TypeAnnotation? annotation)
    {
        return TypeResolver.ResolveTypeAnnotation(annotation);
    }
    
    /// <summary>
    /// Look up a symbol by name in the current scope and parent scopes.
    /// Convenience wrapper around SymbolLookup.Lookup.
    /// </summary>
    public Symbol? LookupSymbol(string name)
    {
        return SymbolLookup.Lookup(name);
    }
    
    /// <summary>
    /// Check if one type is assignable to another.
    /// </summary>
    public bool CanAssign(SemanticType from, SemanticType to)
    {
        if (from == null || to == null) return false;
        if (from == SemanticType.Unknown || to == SemanticType.Unknown) return true;
        if (from.Equals(to)) return true;
        
        // Handle nullable assignment (T can be assigned to T?)
        if (to is NullableType nullableTo)
        {
            return CanAssign(from, nullableTo.UnderlyingType);
        }
        
        // Handle None to nullable
        if (from == SemanticType.Void && to is NullableType)
        {
            return true;
        }
        
        // Handle inheritance
        if (from is UserDefinedType fromUdt && to is UserDefinedType toUdt)
        {
            return IsSubtypeOf(fromUdt.Symbol, toUdt.Symbol);
        }
        
        // Handle numeric widening (int -> long, float32 -> float64)
        if (IsNumericWidening(from, to))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Report an error with source location.
    /// Convenience wrapper around DiagnosticReporter.ReportError.
    /// </summary>
    public void ReportError(string message, Node location)
    {
        DiagnosticReporter.ReportError(message, location);
    }
    
    /// <summary>
    /// Report an error with optional line/column.
    /// </summary>
    public void ReportError(string message, int? line = null, int? column = null)
    {
        DiagnosticReporter.ReportError(message, line, column);
    }
    
    /// <summary>
    /// Check if we should continue processing (based on error count).
    /// </summary>
    public bool ShouldContinue()
    {
        if (!_config.ContinueAfterErrors && DiagnosticReporter.HasErrors)
            return false;
        if (DiagnosticReporter.Diagnostics.ErrorCount >= _config.MaxErrors)
            return false;
        return true;
    }
    
    /// <summary>
    /// Get all diagnostics as legacy SemanticErrors for backwards compatibility.
    /// </summary>
    public IReadOnlyList<SemanticError> GetSemanticErrors()
    {
        return DiagnosticReporter.Diagnostics.ToSemanticErrors();
    }
    
    // =========================================================================
    // Private helper methods
    // =========================================================================
    
    private bool IsSubtypeOf(TypeSymbol? derived, TypeSymbol? baseType)
    {
        if (derived == null || baseType == null) return false;
        if (derived == baseType) return true;
        
        // Check direct base type
        if (derived.BaseType != null && IsSubtypeOf(derived.BaseType, baseType))
            return true;
        
        // Check interfaces
        foreach (var iface in derived.Interfaces)
        {
            if (IsSubtypeOf(iface, baseType))
                return true;
        }
        
        return false;
    }
    
    private bool IsNumericWidening(SemanticType from, SemanticType to)
    {
        // int -> long
        if (from == SemanticType.Int && to == SemanticType.Long) return true;
        // float32 -> float64/double
        if (from == SemanticType.Float32 && (to == SemanticType.Double || to == SemanticType.Float)) return true;
        // int -> float64/double
        if (from == SemanticType.Int && (to == SemanticType.Double || to == SemanticType.Float)) return true;
        
        return false;
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 3.3: Create CompilerServicesBuilder
**File:** `src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs` (NEW)
**Description:** Builder pattern for constructing `CompilerServices`.

```csharp
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Builder for constructing CompilerServices with proper initialization.
/// Ensures all required components are available.
/// </summary>
public class CompilerServicesBuilder
{
    private CompilerServicesConfiguration _config = CompilerServicesConfiguration.Default;
    private ICompilerLogger? _logger;
    private SymbolTable? _symbolTable;
    private SemanticInfo? _semanticInfo;
    private TypeResolver? _typeResolver;
    private ClrMemberCache? _clrCache;
    
    /// <summary>
    /// Set the configuration.
    /// </summary>
    public CompilerServicesBuilder WithConfiguration(CompilerServicesConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }
    
    /// <summary>
    /// Set the logger.
    /// </summary>
    public CompilerServicesBuilder WithLogger(ICompilerLogger? logger)
    {
        _logger = logger;
        return this;
    }
    
    /// <summary>
    /// Set the symbol table (required).
    /// </summary>
    public CompilerServicesBuilder WithSymbolTable(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        return this;
    }
    
    /// <summary>
    /// Set the semantic info (required).
    /// </summary>
    public CompilerServicesBuilder WithSemanticInfo(SemanticInfo semanticInfo)
    {
        _semanticInfo = semanticInfo ?? throw new ArgumentNullException(nameof(semanticInfo));
        return this;
    }
    
    /// <summary>
    /// Set an existing TypeResolver (optional - will be created if not provided).
    /// </summary>
    public CompilerServicesBuilder WithTypeResolver(TypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
        return this;
    }
    
    /// <summary>
    /// Set an existing ClrMemberCache (optional - will be created if not provided).
    /// </summary>
    public CompilerServicesBuilder WithClrCache(ClrMemberCache clrCache)
    {
        _clrCache = clrCache;
        return this;
    }
    
    /// <summary>
    /// Build the CompilerServices instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">If required components are missing.</exception>
    public CompilerServices Build()
    {
        // Validate required components
        if (_symbolTable == null)
            throw new InvalidOperationException("SymbolTable is required. Call WithSymbolTable() before Build().");
        if (_semanticInfo == null)
            throw new InvalidOperationException("SemanticInfo is required. Call WithSemanticInfo() before Build().");
        
        // Use defaults for optional components
        var logger = _logger ?? NullLogger.Instance;
        var clrCache = _clrCache ?? new ClrMemberCache();
        var typeResolver = _typeResolver ?? new TypeResolver(_symbolTable, _semanticInfo, logger);
        
        // Create adapters
        var typeResolverAdapter = new TypeResolverAdapter(typeResolver);
        var symbolLookupAdapter = new SymbolLookupAdapter(_symbolTable);
        var clrMapperAdapter = new ClrTypeMapperAdapter(clrCache);
        var diagnosticReporter = new DiagnosticReporter(logger);
        
        return new CompilerServices(
            _config,
            logger,
            _symbolTable,
            _semanticInfo,
            typeResolverAdapter,
            symbolLookupAdapter,
            clrMapperAdapter,
            diagnosticReporter);
    }
    
    /// <summary>
    /// Create a minimal CompilerServices for testing.
    /// Creates fresh SymbolTable and SemanticInfo.
    /// </summary>
    public static CompilerServices CreateForTesting(ICompilerLogger? logger = null)
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        
        return new CompilerServicesBuilder()
            .WithLogger(logger)
            .WithSymbolTable(symbolTable)
            .WithSemanticInfo(semanticInfo)
            .Build();
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Services/
git commit -m "feat(services): implement CompilerServices container

- Add CompilerServicesConfiguration for immutable settings
- Add CompilerServices main container with convenience methods
- Add CompilerServicesBuilder for proper construction
- Include CreateForTesting() for easy test setup

CompilerServices provides centralized access to all compiler services"
```

---

## Phase 4: Add Unit Tests (1-2 hours)

### Task 4.1: Create CompilerServices Tests
**File:** `src/Sharpy.Compiler.Tests/Services/CompilerServicesTests.cs` (NEW)
**Description:** Unit tests for the CompilerServices infrastructure.

```csharp
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class CompilerServicesTests
{
    [Fact]
    public void CreateForTesting_ReturnsValidInstance()
    {
        // Act
        var services = CompilerServicesBuilder.CreateForTesting();
        
        // Assert
        Assert.NotNull(services);
        Assert.NotNull(services.TypeResolver);
        Assert.NotNull(services.SymbolLookup);
        Assert.NotNull(services.ClrMapper);
        Assert.NotNull(services.DiagnosticReporter);
        Assert.NotNull(services.Logger);
        Assert.NotNull(services.SymbolTable);
        Assert.NotNull(services.SemanticInfo);
    }
    
    [Fact]
    public void Builder_ThrowsWithoutSymbolTable()
    {
        // Arrange
        var builder = new CompilerServicesBuilder()
            .WithSemanticInfo(new SemanticInfo());
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
    
    [Fact]
    public void Builder_ThrowsWithoutSemanticInfo()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var builder = new CompilerServicesBuilder()
            .WithSymbolTable(new SymbolTable(builtinRegistry));
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
    
    [Fact]
    public void ReportError_AddsToDignostics()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        
        // Act
        services.ReportError("Test error", 10, 5);
        
        // Assert
        Assert.True(services.DiagnosticReporter.HasErrors);
        var errors = services.DiagnosticReporter.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal("Test error", errors[0].Message);
        Assert.Equal(10, errors[0].Line);
        Assert.Equal(5, errors[0].Column);
    }
    
    [Fact]
    public void CurrentFilePath_PropagatesTo DiagnosticReporter()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        
        // Act
        services.CurrentFilePath = "/path/to/file.spy";
        services.ReportError("Test error", 1, 1);
        
        // Assert
        var errors = services.DiagnosticReporter.Diagnostics.GetErrors();
        Assert.Equal("/path/to/file.spy", errors[0].FilePath);
    }
    
    [Fact]
    public void ShouldContinue_ReturnsFalseWhenMaxErrorsReached()
    {
        // Arrange
        var config = new CompilerServicesConfiguration { MaxErrors = 2 };
        var builtinRegistry = new BuiltinRegistry();
        var services = new CompilerServicesBuilder()
            .WithConfiguration(config)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();
        
        // Act
        services.ReportError("Error 1");
        Assert.True(services.ShouldContinue());
        
        services.ReportError("Error 2");
        
        // Assert
        Assert.False(services.ShouldContinue());
    }
    
    [Fact]
    public void CanAssign_SameType_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        
        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, SemanticType.Int));
        Assert.True(services.CanAssign(SemanticType.Str, SemanticType.Str));
    }
    
    [Fact]
    public void CanAssign_NumericWidening_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        
        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, SemanticType.Long));
        Assert.True(services.CanAssign(SemanticType.Float32, SemanticType.Double));
    }
    
    [Fact]
    public void CanAssign_ToNullable_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        var nullableInt = new NullableType { UnderlyingType = SemanticType.Int };
        
        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, nullableInt));
    }
    
    [Fact]
    public void GetSemanticErrors_ReturnsLegacyFormat()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        services.ReportError("Test error", 10, 5);
        
        // Act
        var errors = services.GetSemanticErrors();
        
        // Assert
        Assert.Single(errors);
        Assert.Equal(10, errors[0].Line);
    }
}
```

**Verification:**
- [ ] New tests pass
- [ ] All existing tests still pass

---

### Task 4.2: Create Service Adapter Tests
**File:** `src/Sharpy.Compiler.Tests/Services/ServiceAdapterTests.cs` (NEW)
**Description:** Tests for individual service adapters.

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class ServiceAdapterTests
{
    [Fact]
    public void SymbolLookupAdapter_Lookup_FindsDefinedSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);
        
        var symbol = new VariableSymbol { Name = "testVar", Kind = SymbolKind.Variable };
        symbolTable.Define(symbol);
        
        // Act
        var result = adapter.Lookup("testVar");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("testVar", result.Name);
    }
    
    [Fact]
    public void SymbolLookupAdapter_LookupType_FindsTypeSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);
        
        var typeSymbol = new TypeSymbol 
        { 
            Name = "MyClass", 
            Kind = SymbolKind.Type, 
            TypeKind = TypeKind.Class 
        };
        symbolTable.Define(typeSymbol);
        
        // Act
        var result = adapter.LookupType("MyClass");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("MyClass", result.Name);
        Assert.Equal(TypeKind.Class, result.TypeKind);
    }
    
    [Fact]
    public void TypeResolverAdapter_ResolveTypeAnnotation_ResolvesBuiltins()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var adapter = new TypeResolverAdapter(typeResolver);
        
        var intAnnotation = new TypeAnnotation { Name = "int", IsNullable = false };
        
        // Act
        var result = adapter.ResolveTypeAnnotation(intAnnotation);
        
        // Assert
        Assert.Equal(SemanticType.Int, result);
    }
    
    [Fact]
    public void ClrTypeMapperAdapter_GetClrType_MapsBuiltins()
    {
        // Arrange
        var clrCache = new ClrMemberCache();
        var adapter = new ClrTypeMapperAdapter(clrCache);
        
        // Act & Assert
        Assert.Equal(typeof(int), adapter.GetClrType(SemanticType.Int));
        Assert.Equal(typeof(string), adapter.GetClrType(SemanticType.Str));
        Assert.Equal(typeof(bool), adapter.GetClrType(SemanticType.Bool));
    }
    
    [Fact]
    public void ClrTypeMapperAdapter_GetSemanticType_MapsFromClr()
    {
        // Arrange
        var clrCache = new ClrMemberCache();
        var adapter = new ClrTypeMapperAdapter(clrCache);
        
        // Act & Assert
        Assert.Equal(SemanticType.Int, adapter.GetSemanticType(typeof(int)));
        Assert.Equal(SemanticType.Str, adapter.GetSemanticType(typeof(string)));
        Assert.Equal(SemanticType.Bool, adapter.GetSemanticType(typeof(bool)));
    }
    
    [Fact]
    public void DiagnosticReporter_ReportError_WithNode()
    {
        // Arrange
        var reporter = new DiagnosticReporter();
        var node = new PassStatement { LineStart = 5, ColumnStart = 10 };
        
        // Act
        reporter.ReportError("Test error at node", node);
        
        // Assert
        var errors = reporter.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal(5, errors[0].Line);
        Assert.Equal(10, errors[0].Column);
    }
}
```

**Verification:**
- [ ] New tests pass
- [ ] All existing tests still pass

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Services/
git commit -m "test(services): add unit tests for CompilerServices

- Add CompilerServicesTests for main container
- Add ServiceAdapterTests for individual adapters
- Test builder validation, error reporting, type assignment

All tests passing"
```

---

## Phase 5: Integration with Existing Code (2-3 hours)

This phase connects CompilerServices to existing components without breaking tests.
We use an incremental approach: add optional CompilerServices parameters while keeping existing parameters working.

### Task 5.1: Update SemanticContext to Use CompilerServices
**File:** `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`
**Description:** Add optional CompilerServices to SemanticContext while maintaining backward compatibility.

**Changes to make:**

1. Add optional `CompilerServices` property
2. Add alternative constructor accepting `CompilerServices`
3. Delegate to CompilerServices when available

```csharp
// Add to existing SemanticContext class:

/// <summary>
/// Optional CompilerServices for centralized service access.
/// When set, provides access to all compiler services.
/// </summary>
public CompilerServices? Services { get; }

/// <summary>
/// Create a context backed by CompilerServices.
/// Preferred constructor for new code.
/// </summary>
public SemanticContext(CompilerServices services)
{
    Services = services ?? throw new ArgumentNullException(nameof(services));
    SymbolTable = services.SymbolTable;
    SemanticInfo = services.SemanticInfo;
    TypeResolver = (services.TypeResolver as TypeResolverAdapter)?.UnderlyingResolver 
        ?? throw new InvalidOperationException("TypeResolver must be a TypeResolverAdapter");
    Logger = services.Logger;
    Diagnostics = services.DiagnosticReporter.Diagnostics;
    ClrCache = (services.ClrMapper as ClrTypeMapperAdapter)?.UnderlyingCache ?? new ClrMemberCache();
    CurrentFilePath = services.CurrentFilePath;
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass
- [ ] Both constructors work correctly

---

### Task 5.2: Add CompilerServices to TypeChecker
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Description:** Add optional CompilerServices constructor overload.

**Changes to make:**

Add an alternative constructor that accepts `CompilerServices`:

```csharp
// Add to existing TypeChecker class:

/// <summary>
/// Create TypeChecker with CompilerServices for centralized service access.
/// Preferred constructor for new code.
/// </summary>
public TypeChecker(CompilerServices services, ValidationPipeline? validationPipeline = null)
    : this(
        services.SymbolTable,
        services.SemanticInfo,
        ((TypeResolverAdapter)services.TypeResolver).UnderlyingResolver,
        services.Logger,
        validationPipeline)
{
    _services = services;
}

private readonly CompilerServices? _services;
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

---

### Task 5.3: Add CompilerServices Factory Method to Compiler
**File:** `src/Sharpy.Compiler/Compiler.cs`
**Description:** Add helper method to create CompilerServices from compilation state.

**Changes to make:**

Add a factory method and update Compile() to optionally use CompilerServices:

```csharp
// Add to existing Compiler class:

/// <summary>
/// Create CompilerServices from compilation state.
/// </summary>
private CompilerServices CreateServices(
    SymbolTable symbolTable, 
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ClrMemberCache? clrCache = null)
{
    return new CompilerServicesBuilder()
        .WithLogger(_logger)
        .WithSymbolTable(symbolTable)
        .WithSemanticInfo(semanticInfo)
        .WithTypeResolver(typeResolver)
        .WithClrCache(clrCache ?? new ClrMemberCache())
        .Build();
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit Point:**
```bash
git add src/Sharpy.Compiler/
git commit -m "feat(services): integrate CompilerServices with existing components

- Add CompilerServices property to SemanticContext
- Add CompilerServices constructor overload to TypeChecker
- Add CreateServices factory method to Compiler

Backward compatible: existing constructors still work"
```

---

## Phase 6: Add Integration Tests (1 hour)

### Task 6.1: Create Integration Tests
**File:** `src/Sharpy.Compiler.Tests/Services/CompilerServicesIntegrationTests.cs` (NEW)
**Description:** Test CompilerServices in real compilation scenarios.

```csharp
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class CompilerServicesIntegrationTests
{
    [Fact]
    public void TypeChecker_WithCompilerServices_WorksCorrectly()
    {
        // Arrange
        var source = @"
x: int = 42
y: str = ""hello""
";
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser.Parser(tokens);
        var module = parser.ParseModule();
        
        var services = CompilerServicesBuilder.CreateForTesting();
        
        var nameResolver = new NameResolver(services.SymbolTable);
        nameResolver.ResolveDeclarations(module);
        
        // Act
        var typeChecker = new TypeChecker(services);
        typeChecker.CheckModule(module);
        
        // Assert
        Assert.Empty(typeChecker.Errors);
    }
    
    [Fact]
    public void TypeChecker_WithCompilerServices_CollectsErrors()
    {
        // Arrange - intentional type error
        var source = @"
x: int = ""not an int""
";
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser.Parser(tokens);
        var module = parser.ParseModule();
        
        var services = CompilerServicesBuilder.CreateForTesting();
        
        var nameResolver = new NameResolver(services.SymbolTable);
        nameResolver.ResolveDeclarations(module);
        
        // Act
        var typeChecker = new TypeChecker(services);
        typeChecker.CheckModule(module);
        
        // Assert
        Assert.NotEmpty(typeChecker.Errors);
    }
    
    [Fact]
    public void SemanticContext_WithCompilerServices_SharesState()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        services.CurrentFilePath = "/test/file.spy";
        
        // Act
        var context = new SemanticContext(services);
        
        // Assert
        Assert.Same(services.SymbolTable, context.SymbolTable);
        Assert.Same(services.SemanticInfo, context.SemanticInfo);
        Assert.Same(services.DiagnosticReporter.Diagnostics, context.Diagnostics);
        Assert.Equal("/test/file.spy", context.CurrentFilePath);
    }
    
    [Fact]
    public void FullCompilation_WithCompilerServices_ProducesValidOutput()
    {
        // Arrange
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

result: int = add(1, 2)
";
        
        // Act
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");
        
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.GeneratedCSharpCode);
        Assert.Contains("public static int Add", result.GeneratedCSharpCode);
    }
}
```

**Verification:**
- [ ] New tests pass
- [ ] All existing tests still pass

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Services/
git commit -m "test(services): add integration tests for CompilerServices

- Test TypeChecker with CompilerServices
- Test SemanticContext with CompilerServices
- Test full compilation pipeline

All tests passing"
```

---

## Phase 7: Documentation and Final Cleanup (30 minutes)

### Task 7.1: Update Services README
**File:** `src/Sharpy.Compiler/Services/README.md`
**Description:** Update documentation with usage examples.

Add usage examples section:

```markdown
## Usage

### Basic Usage

```csharp
// Create services for a compilation
var services = new CompilerServicesBuilder()
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();

// Use services in TypeChecker
var typeChecker = new TypeChecker(services);
typeChecker.CheckModule(module);

// Check for errors
if (services.DiagnosticReporter.HasErrors)
{
    foreach (var error in services.DiagnosticReporter.Diagnostics.GetErrors())
    {
        Console.WriteLine(error);
    }
}
```

### Testing

```csharp
// Create minimal services for unit tests
var services = CompilerServicesBuilder.CreateForTesting();

// Use in test
services.ReportError("Test error", 1, 1);
Assert.True(services.DiagnosticReporter.HasErrors);
```

### Migration Path

Existing code using the old constructors continues to work:

```csharp
// Old way (still works)
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);

// New way (preferred)
var services = new CompilerServicesBuilder()
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();
var typeChecker = new TypeChecker(services);
```
```

**Verification:**
- [ ] Documentation is accurate
- [ ] Examples compile

---

### Task 7.2: Run Full Test Suite
**File:** N/A
**Description:** Final verification that all tests pass.

```bash
cd /Users/anton/Documents/github/sharpy/src
dotnet test Sharpy.Compiler.Tests --verbosity normal
```

**Verification:**
- [ ] All tests pass
- [ ] No new warnings introduced

---

### Task 7.3: Final Commit and PR Preparation
**File:** N/A
**Description:** Final commit and branch summary.

```bash
git add .
git commit -m "docs(services): add usage documentation for CompilerServices

- Add usage examples to README
- Document migration path from old constructors
- Add testing examples

CompilerServices layer implementation complete"

# Create summary for PR
git log --oneline feature/compiler-services-layer ^main
```

---

## Summary

### Files Created
```
src/Sharpy.Compiler/Services/
├── README.md
├── ITypeResolver.cs
├── ISymbolLookup.cs
├── IClrTypeMapper.cs
├── IDiagnosticReporter.cs
├── TypeResolverAdapter.cs
├── SymbolLookupAdapter.cs
├── ClrTypeMapperAdapter.cs
├── DiagnosticReporter.cs
├── CompilerServicesConfiguration.cs
├── CompilerServices.cs
└── CompilerServicesBuilder.cs

src/Sharpy.Compiler.Tests/Services/
├── CompilerServicesTests.cs
├── ServiceAdapterTests.cs
└── CompilerServicesIntegrationTests.cs
```

### Files Modified
```
src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs
src/Sharpy.Compiler/Semantic/TypeChecker.cs
src/Sharpy.Compiler/Compiler.cs
```

### Design for Future Features

The CompilerServices layer is designed to support:

1. **LSP (Language Server Protocol)**
   - Services are interface-based, allowing different implementations for IDE scenarios
   - DiagnosticBag is thread-safe for incremental compilation
   - Configuration is immutable for safe sharing

2. **Parallel Compilation**
   - All shared caches use thread-safe collections
   - Services are stateless where possible
   - CurrentFilePath is the only mutable state (localized)

3. **Incremental Compilation**
   - Service interfaces allow caching implementations
   - DiagnosticBag supports merging results
   - Builder pattern enables per-file service instances

4. **Tagged Unions / ADTs (v0.2.x)**
   - Type resolution through ITypeResolver can be extended
   - CanAssign() method extensible for new type relationships

5. **Async/Await (v0.2.x+)**
   - Services layer doesn't block async adoption
   - Interfaces can be extended with async variants if needed

### Backward Compatibility

- All existing constructors continue to work
- New code can adopt CompilerServices incrementally
- Tests verify both old and new patterns work
- No breaking changes to public API

---

## Rollback Plan

If issues are discovered after merging:

1. **Partial Rollback**: Remove CompilerServices integration from specific components while keeping the infrastructure
2. **Full Rollback**: `git revert` the merge commit
3. **The adapters wrap existing code** - removing them doesn't affect the wrapped implementations

The design specifically avoids one-way-door decisions that would make rollback difficult.

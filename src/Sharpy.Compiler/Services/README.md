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
- `TypeResolverAdapter.cs` - Adapter wrapping existing TypeResolver
- `SymbolLookupAdapter.cs` - Adapter wrapping existing SymbolTable
- `ClrTypeMapperAdapter.cs` - Adapter for CLR type mapping
- `DiagnosticReporter.cs` - Implementation of diagnostic reporting
- `CompilerServicesConfiguration.cs` - Immutable configuration
- `CompilerServices.cs` - Main service container
- `CompilerServicesBuilder.cs` - Builder for constructing services

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

### Configuration

```csharp
// Configure services with custom settings
var config = new CompilerServicesConfiguration
{
    MaxErrors = 50,           // Stop after 50 errors
    ContinueAfterErrors = false,  // Stop on first error
    VerboseLogging = true
};

var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();
```

## Architecture

```
CompilerServicesBuilder
    |
    +-- Creates --> CompilerServices
                        |
                        +-- ITypeResolver (TypeResolverAdapter)
                        +-- ISymbolLookup (SymbolLookupAdapter)
                        +-- IClrTypeMapper (ClrTypeMapperAdapter)
                        +-- IDiagnosticReporter (DiagnosticReporter)
                        +-- SymbolTable (direct access for migration)
                        +-- SemanticInfo (direct access for migration)
```

## Thread Safety

- `DiagnosticBag` uses locking for thread-safe error collection
- `ClrTypeMapperAdapter` uses `ConcurrentDictionary` for member caching
- Configuration is immutable after construction

## Future Considerations

This infrastructure supports:
- **LSP**: Services are interface-based for different implementations
- **Parallel Compilation**: Thread-safe caches and diagnostics
- **Incremental Compilation**: Service interfaces allow caching implementations

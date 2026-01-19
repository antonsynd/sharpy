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

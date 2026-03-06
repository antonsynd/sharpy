# Project Compilation

This directory contains components for multi-file project compilation.

## Key Files

- `ProjectCompiler.cs` - Orchestrates multi-file compilation pipeline (7 partial files):
  - `.cs` (main), `.Initialization.cs`, `.Parsing.cs`, `.Phases.cs`, `.CodeGen.cs`, `.IncrementalCache.cs`, `.Utilities.cs`
- `SpyProject.cs` - Project configuration and file discovery
- `DependencyGraph.cs` - Immutable dependency graph for build ordering
- `DependencyGraphBuilder.cs` - Thread-safe builder for dependency graph (moved to `Semantic/`)
- `IncrementalCompilationCache.cs` - File hash and symbol caching for incremental builds

## ProjectCompiler

Handles the full compilation pipeline for multi-file projects:

```
Phase 1: Parse all source files
Phase 2: Initialize shared symbol table
Phase 3: Collect type declarations (cross-file visibility)
Phase 4: Resolve imports, build dependency graph
Phase 5: Semantic analysis (in dependency order)
Phase 6: Code generation
Phase 7: Assembly compilation
```

Key features:
- **Two-phase type collection**: First pass collects type shells, second pass
  resolves inheritance (enables cross-module inheritance)
- **Dependency-ordered processing**: Modules processed after their dependencies
- **Shared symbol table**: Single symbol table across all files

## DependencyGraph

Immutable structure for dependency analysis:

```csharp
// Build order (dependencies first)
var order = graph.GetBuildOrder();

// Files affected by a change (for incremental compilation)
var affected = graph.GetAffectedFiles("utils.spy");

// Groups that can compile in parallel
var groups = graph.GetParallelizableGroups();

// Detect circular imports
var cycles = graph.DetectCycles();
```

## DependencyGraphBuilder

Thread-safe builder for constructing dependency graphs:

```csharp
var builder = new DependencyGraphBuilder();
builder.AddFile("main.spy");
builder.AddDependency("main.spy", "utils.spy");
builder.SetFileHash("main.spy", hash);
var graph = builder.Build();
```

## SpyProject

Handles project file parsing and source file discovery:

- Reads `.spyproj` configuration files
- Discovers `.spy` source files
- Resolves package dependencies
- Configures compilation options

## Incremental Compilation

Enable with `--incremental` to skip unchanged files:

1. **First build**: All files compiled, symbols and C# cached to `obj/{Config}/.sharpy-symbols`
2. **Subsequent builds**: Files skipped if content hash matches and dependencies unchanged
3. **Transitive deps**: If file A imports B and B changes, A is recompiled

Cache is auto-invalidated on compiler version or schema version changes.

## Design Notes

- `DependencyGraph` is immutable for thread safety
- `DependencyGraphBuilder` uses concurrent collections for parallel imports
- Import resolution populates the dependency graph automatically
- Path normalization handles cross-platform differences

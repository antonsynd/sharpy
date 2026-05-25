# Native .spy to .dll Stdlib Pipeline

**Date:** 2026-05-25
**Issue:** #695
**Status:** Implemented

## Decision

Keep checked-in C# as the primary deployment path for `Sharpy.Stdlib.dll`. The
native pipeline (`sharpyc project stdlib.spyproj`) serves as a validation
mechanism, not a replacement.

## Context

The stdlib build has two pipelines:

1. **C# emission pipeline** (deployment): `.spy` → `sharpyc emit csharp` → `.cs`
   files → MSBuild → `Sharpy.Stdlib.dll`
2. **Native pipeline** (validation): `.spy` → `sharpyc project` → ProjectCompiler
   → Roslyn `CSharpCompilation.Emit()` → `Sharpy.Stdlib.Spy.dll`

Both produce equivalent assemblies for the 15 `.spy` modules. The native pipeline
validates that the full compilation pipeline works end-to-end.

## Rationale

### Why keep checked-in C#

- Consumers of `Sharpy.Stdlib` (via NuGet or source reference) don't need the
  Sharpy compiler installed.
- MSBuild integration avoids the circular dependency problem: `Sharpy.Cli` depends
  on `Sharpy.Stdlib`, so using the CLI to build Stdlib would require Stdlib to
  already be built.
- The checked-in C# serves as a human-readable intermediate representation that
  can be debugged with standard .NET tooling.

### Why have a native pipeline at all

- Validates the ProjectCompiler's multi-file compilation against real-world code.
- Catches regressions where single-file emit works but project compilation doesn't
  (e.g., namespace resolution, cross-module references, assembly reference
  resolution).
- Enables future migration when the Sharpy compiler ships as a standalone tool.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ stdlib.spyproj                                          │
│   15 .spy modules (textwrap, bisect, statistics, ...)   │
└────────────────────────┬────────────────────────────────┘
                         │
          ┌──────────────┴──────────────┐
          │                             │
          ▼                             ▼
┌─────────────────────┐    ┌─────────────────────────────┐
│ C# Emission         │    │ Native Compilation          │
│ (deployment)        │    │ (validation)                │
│                     │    │                             │
│ regenerate_spy_     │    │ sharpyc project             │
│ stdlib.sh           │    │   stdlib.spyproj            │
│   --emit-cs-to      │    │                             │
│   + post-process    │    │ ProjectCompiler 7-phase     │
│                     │    │   → AssemblyCompiler        │
│ Output: .cs files   │    │                             │
│   → MSBuild         │    │ Output:                     │
│   → Sharpy.Stdlib   │    │   Sharpy.Stdlib.Spy.dll     │
│     .dll            │    │                             │
└─────────────────────┘    └─────────────────────────────┘
```

## No Circular Dependency

The native pipeline does NOT create a circular build:

1. `dotnet build sharpy.sln` builds everything (CLI, Core, Stdlib) using checked-in C#.
2. The built CLI is then used to validate-compile the `.spy` modules.
3. The validation artifact (`Sharpy.Stdlib.Spy.dll`) is not consumed by any build target.

The only dependency the native pipeline has on `Sharpy.Stdlib.dll` is for type
resolution — `hashlib_module.spy` references `HashObject` which lives in the
hand-written C# portion of Sharpy.Stdlib. This is resolved via the CLI's
`GetDefaultReferences()` mechanism which automatically adds `Sharpy.Stdlib.dll`
as a metadata reference.

## Future: Removing Checked-in C#

Prerequisites for removing checked-in C#:
1. Sharpy compiler ships as a standalone NuGet tool
2. MSBuild task integrates `sharpyc project` as a build step
3. Circular dependency is resolved (compiler no longer depends on Stdlib at build time)
4. All `.spy` modules compile without needing the MSBuild-produced `Sharpy.Stdlib.dll`
   (requires moving `HashObject` and `StatisticsError` into Sharpy.Core or making
   them available through another mechanism)

Timeline: not planned for v1.0.

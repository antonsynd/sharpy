<!-- Verified by /verify-plan on 2026-05-20 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Split Sharpy.Core into Core (Essentials) + Stdlib

## Context

Sharpy.Core currently bundles both runtime essentials (primitives, collections, builtins, protocol interfaces) and 35 stdlib modules (json, numpy, os, datetime, etc.) into a single assembly. This creates unnecessary coupling: the compiler references Sharpy.Core for a handful of core types but transitively depends on MathNet.Numerics, Microsoft.Data.Sqlite, and every stdlib module.

This plan splits Sharpy.Core into two assemblies with a clean dependency boundary. The compiler depends only on the essentials; the stdlib is loaded as a reference assembly at compile time, just like any other .NET library.

GitHub issues: [#676](https://github.com/antonsynd/sharpy/issues/676) (Phase 2 — rewrite stdlib in Sharpy, future work)

## Design Decisions

### 1. Keep the Name `Sharpy.Core` for Essentials

Renaming to `Sharpy.Runtime` would touch every extern alias (`SharpyRT`), every test base class, every CI workflow, and the CLI's assembly-copy logic — all for no functional gain. Instead:

- **`Sharpy.Core`** keeps its name, retains the `SharpyRT` alias, but sheds all stdlib modules
- **`Sharpy.Stdlib`** is a new assembly containing the 35 stdlib modules

This minimizes churn: the compiler's `ProjectReference` with `<Aliases>SharpyRT</Aliases>` doesn't change at all.

### 2. Compiler Has Zero Dependency on Stdlib

The compiler (`Sharpy.Compiler.csproj`) will **not** have a `ProjectReference` to `Sharpy.Stdlib`. Instead:

- `BuiltinRegistry` continues to load `Sharpy.Core` via the `SharpyRT` extern alias (builtins, core types, protocol interfaces)
- Stdlib modules are discovered through `ModuleRegistry.LoadReference()`, the same path used for arbitrary .NET assemblies. The CLI passes `Sharpy.Stdlib.dll` as a reference when invoking the compiler.

This means the compiler can build and run without Sharpy.Stdlib present — it just won't have stdlib modules available.

### 3. Stdlib Tests Move to a Separate Project

Currently ~91 of ~1,958 file-based integration tests use stdlib imports (`import json`, `import numpy`, etc.). These move to a new `Sharpy.Stdlib.Tests` project. `Sharpy.Compiler.Tests` retains the ~1,867 tests that exercise only core features.

This achieves the user's goal of zero stdlib test dependencies in the compiler test suite.

### 4. Shared Namespace

Both assemblies use the `Sharpy` root namespace. Stdlib modules already use `Sharpy` namespace (e.g., `Sharpy.Json`, `Sharpy.Os`). The `[SharpyModule]` and `[SharpyModuleType]` attributes live in Core and are referenced by Stdlib.

## Current State

| Fact | Value |
|------|-------|
| Root .cs files (Core) | ~89 files [CORRECTED: actual count is 89, not ~101] |
| Builtins/ | 5 files |
| Generators/ | 12 files |
| Partial.* directories | 9 directories |
| Stdlib module directories | 33 directories, ~152 files [CORRECTED: 33 not 35 — Generators/ is compiler infrastructure (stays in Core), not a stdlib module; file count is ~152 not ~190] |
| NuGet deps (stdlib-only) | MathNet.Numerics 5.0.0, Microsoft.Data.Sqlite 10.0.8 |
| NuGet deps (core) | System.Collections.Immutable, Microsoft.Bcl.AsyncInterfaces (netstandard2.1) |
| Cross-module dependencies | **Zero** — no stdlib module imports another |
| Compiler hard deps on Core | ~9-10 types (List, Dict, Set, FrozenDict, Optional, Result, Template, Bytes, Builtins + protocol interfaces via ProtocolRegistry) [CORRECTED: ~9-10 not ~15; ISized/IBoolConvertible/IReverseEnumerable are used via ProtocolRegistry, not direct extern alias] |
| Compiler hard deps on Stdlib | **Zero** (functools.partial is compile-time lambda generation via SyntaxFactory, collections.namedtuple is an error message only) [CORRECTED: "AST desugaring" overstated — FunctoolsPartialHelper is a detection/type-inference helper; actual code generation is lambda emission in RoslynEmitter] |
| Tests using stdlib imports | ~123 / 1,958 file-based fixtures [CORRECTED: ~123 not ~91; original grep likely missed some import patterns] |

## Dependency Graph (After Split)

```
Sharpy.Core (essentials, C#)
  ├── no Sharpy dependencies
  ├── System.Collections.Immutable
  └── Microsoft.Bcl.AsyncInterfaces (netstandard2.1 only)

Sharpy.Stdlib (modules, C#)
  ├── Sharpy.Core (ProjectReference)
  ├── MathNet.Numerics
  └── Microsoft.Data.Sqlite

Sharpy.Compiler
  ├── Sharpy.Core (ProjectReference, Aliases=SharpyRT)  ← unchanged
  └── NO reference to Sharpy.Stdlib

Sharpy.Cli
  ├── Sharpy.Compiler (ProjectReference)
  ├── Sharpy.Core (ProjectReference, Aliases=SharpyRT)  ← unchanged
  └── Sharpy.Stdlib (ProjectReference, for assembly location + copy)

Sharpy.Compiler.Tests
  ├── Sharpy.Compiler (ProjectReference)
  ├── Sharpy.Core (ProjectReference, Aliases=SharpyRT)  ← unchanged
  └── NO reference to Sharpy.Stdlib

Sharpy.Stdlib.Tests (NEW)
  ├── Sharpy.Compiler (ProjectReference)
  ├── Sharpy.Core (ProjectReference, Aliases=SharpyRT)
  └── Sharpy.Stdlib (ProjectReference)

Sharpy.Core.Tests
  ├── Sharpy.Core (ProjectReference)  ← unchanged, but loses stdlib module tests
```

---

## Implementation

### Phase 1: Create Sharpy.Stdlib Project

**Files to create:**

1. `src/Sharpy.Stdlib/Sharpy.Stdlib.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFrameworks>net10.0;netstandard2.1</TargetFrameworks>
       <LangVersion Condition="'$(TargetFramework)' == 'netstandard2.1'">9.0</LangVersion>
       <LangVersion Condition="'$(TargetFramework)' == 'net10.0'">14</LangVersion>
       <Nullable>enable</Nullable>
       <RootNamespace>Sharpy</RootNamespace>
       <GenerateDocumentationFile>true</GenerateDocumentationFile>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="../Sharpy.Core/Sharpy.Core.csproj" />
     </ItemGroup>
     <ItemGroup>
       <PackageReference Include="MathNet.Numerics" Version="5.0.0" />
       <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.8" />
     </ItemGroup>
     <ItemGroup>
       <InternalsVisibleTo Include="Sharpy.Stdlib.Tests" />
     </ItemGroup>
   </Project>
   ```

2. Move these 33 directories from `src/Sharpy.Core/` to `src/Sharpy.Stdlib/` [CORRECTED: 33 not 35; Generators/ removed — verified as compiler infrastructure used by GeneratorContextBuilder via extern alias]:
   ```
   Argparse/    Bisect/       Collections/  Copy/        Csv/
   Datetime/    Fnmatch/      Functools/    Glob/        Grapheme/
   Hashlib/     Heapq/        Io/           Itertools/   Json/
   Logging/     Math/         Numpy/        Operator/    Os/
   Pathlib/     Random/       Re/           Requests/    Shutil/
   Sqlite3/     Statistics/   String/       Sys/         Tempfile/
   Textwrap/    Time/         Unittest/
   ```

**Verification:** `dotnet build src/Sharpy.Stdlib/Sharpy.Stdlib.csproj` succeeds on both target frameworks.

### Phase 2: Trim Sharpy.Core

**Changes to `src/Sharpy.Core/Sharpy.Core.csproj`:**

1. Remove `MathNet.Numerics` and `Microsoft.Data.Sqlite` package references
2. Remove `<NoWarn>` entries that were only needed by stdlib code (verify first)
3. Keep `System.Collections.Immutable` and `Microsoft.Bcl.AsyncInterfaces`

**What stays in Sharpy.Core:**
- All ~89 root .cs files (primitives, builtins, protocol interfaces, exceptions, conversions, extensions) [CORRECTED: 89 not ~101]
- `Builtins/` directory (5 files — print, exceptions, module init)
- `Partial.Bytes/`, `Partial.Complex/`, `Partial.Dict/`, `Partial.Iterator/`, `Partial.List/`, `Partial.ListIterator/`, `Partial.ListReverseIterator/`, `Partial.Set/`, `Partial.SetIterator/`
- `Generators/` (12 files — compiler infrastructure for source generators, used by GeneratorContextBuilder via SharpyRT extern alias) [CORRECTED: verified as compiler infrastructure; stays in Core]

**Verification:** `dotnet build src/Sharpy.Core/Sharpy.Core.csproj` succeeds. No remaining references to MathNet or Sqlite namespaces.

### Phase 3: Update the CLI to Load Stdlib

**Changes to `src/Sharpy.Cli/Sharpy.Cli.csproj`:**

Add a project reference to Sharpy.Stdlib (no alias needed — CLI doesn't use stdlib types directly, just needs the assembly location):
```xml
<ProjectReference Include="..\Sharpy.Stdlib\Sharpy.Stdlib.csproj" />
```

**Changes to `src/Sharpy.Cli/Commands/RunCommand.cs`:**

Currently (lines 92-96):
```csharp
var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
var sharpyCorePath = sharpyCoreAssembly.Location;
var outputDir = Path.GetDirectoryName(outputPath)!;
var sharpyCoreDestPath = Path.Combine(outputDir, "Sharpy.Core.dll");
File.Copy(sharpyCorePath, sharpyCoreDestPath, overwrite: true);
```

Update to also copy Sharpy.Stdlib.dll and its transitive NuGet dependencies:
```csharp
// Copy Sharpy.Core.dll (essential runtime)
var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
var sharpyCorePath = sharpyCoreAssembly.Location;
var outputDir = Path.GetDirectoryName(outputPath)!;
File.Copy(sharpyCorePath, Path.Combine(outputDir, "Sharpy.Core.dll"), overwrite: true);

// Copy Sharpy.Stdlib.dll + transitive deps from same directory
var coreDir = Path.GetDirectoryName(sharpyCorePath)!;
var stdlibPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
if (File.Exists(stdlibPath))
{
    File.Copy(stdlibPath, Path.Combine(outputDir, "Sharpy.Stdlib.dll"), overwrite: true);
    // Copy transitive NuGet deps (MathNet, Sqlite, etc.)
    CopyTransitiveDependencies(coreDir, outputDir);
}
```

Note: The exact mechanism for locating Sharpy.Stdlib.dll needs validation. Since the CLI references Sharpy.Stdlib as a project reference, both DLLs will be in the CLI's output directory. Locating via `coreDir` (sibling to Sharpy.Core.dll) should work because MSBuild copies transitive dependencies to the output.

**Changes to compiler invocation:** The CLI must pass Sharpy.Stdlib.dll as a reference to the compiler so that `import json`, `import os`, etc. resolve. This likely means updating how `ModuleRegistry` is initialized — adding `moduleRegistry.LoadReference(stdlibPath)` alongside the existing Core assembly load.

**Verification:** `dotnet run --project src/Sharpy.Cli -- run <test.spy>` works for a .spy file that uses `import json`.

### Phase 4: Verify Compiler Decoupling

Confirm that `Sharpy.Compiler.csproj` has **no** reference to `Sharpy.Stdlib`:

```bash
grep -i "stdlib" src/Sharpy.Compiler/Sharpy.Compiler.csproj
# Should return nothing
```

Confirm that the compiler builds without Sharpy.Stdlib:
```bash
dotnet build src/Sharpy.Compiler/Sharpy.Compiler.csproj
# Must succeed with zero warnings
```

Confirm no source file in `src/Sharpy.Compiler/` imports Sharpy.Stdlib types:
```bash
grep -r "Sharpy\.Stdlib" src/Sharpy.Compiler/
# Should return nothing
```

**Special cases to audit:**
- `FunctoolsPartialHelper.cs` — provides detection (`IsFunctoolsPartialCall`) and type inference (`ComputeResultTypeFromFunctionType`) for `functools.partial()`. Does not reference any functools runtime types — operates purely on `ModuleSymbol` names and AST nodes. [CORRECTED: not "AST-level desugaring" — it's a semantic helper; actual code generation (lambda emission) is in RoslynEmitter.Expressions.Access.Calls.cs]
- `BuiltinRegistry.cs` — verify it only loads from `typeof(SharpyRT::Sharpy.Builtins).Assembly` and doesn't enumerate stdlib modules.
- Any `collections.namedtuple` error messages — these are just string literals, not type references.

### Phase 5: Split Tests

#### 5a. Create Sharpy.Stdlib.Tests

**Create `src/Sharpy.Stdlib.Tests/Sharpy.Stdlib.Tests.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Sharpy.Compiler/Sharpy.Compiler.csproj" />
    <ProjectReference Include="../Sharpy.Core/Sharpy.Core.csproj">
      <Aliases>SharpyRT</Aliases>
    </ProjectReference>
    <ProjectReference Include="../Sharpy.Stdlib/Sharpy.Stdlib.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- test framework packages matching Sharpy.Compiler.Tests -->
  </ItemGroup>
</Project>
```

**Move stdlib-dependent test fixtures:** Identify all .spy fixtures that use stdlib imports and move them:
```bash
# Find fixtures with stdlib imports
grep -rl "^import \(json\|numpy\|os\|datetime\|re\|collections\|csv\|argparse\|math\|sqlite3\|requests\|itertools\|heapq\|pathlib\|random\|statistics\|functools\|hashlib\|glob\|shutil\|tempfile\|textwrap\|logging\|string\|io\|bisect\|copy\|fnmatch\|grapheme\|operator\|sys\|time\|unittest\)\b" \
  src/Sharpy.Compiler.Tests/Integration/TestFixtures/
```

Move each matching `.spy` file and its companion `.expected`/`.error`/`.warning`/`.expected.cs`/`.skip` files to `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`.

**Create a FileBasedIntegrationTests runner** in `Sharpy.Stdlib.Tests` that mirrors the one in `Sharpy.Compiler.Tests` but:
- Points to its own `TestFixtures/` directory
- Loads both `Sharpy.Core.dll` and `Sharpy.Stdlib.dll` as references

**Create an IntegrationTestBase** in `Sharpy.Stdlib.Tests` (or share via a common test utilities project) that loads both assemblies in `BuildSharedReferences()`.

#### 5b. Split Sharpy.Core.Tests

Move stdlib module unit tests from `src/Sharpy.Core.Tests/` to `src/Sharpy.Stdlib.Tests/`:
- Any test file in a module directory (e.g., `JsonTests.cs`, `NumpyTests.cs`, `OsTests.cs`)
- Keep tests for core types (List, Dict, Set, Bytes, builtins, etc.)

**Verification:**
```bash
dotnet test --filter "FullyQualifiedName~Sharpy.Compiler.Tests"
# All ~1,835 core tests pass, zero stdlib imports [CORRECTED: 1958-123=~1835, not ~1867]

dotnet test --filter "FullyQualifiedName~Sharpy.Stdlib.Tests"
# All ~123 stdlib integration tests + moved Core.Tests pass [CORRECTED: ~123 not ~91]

dotnet test --filter "FullyQualifiedName~Sharpy.Core.Tests"
# All remaining core-only unit tests pass
```

### Phase 6: Update IntegrationTestBase

**Changes to `src/Sharpy.Compiler.Tests/Integration/IntegrationTestBase.cs`:**

`BuildSharedReferences()` currently searches for `Sharpy.Core.dll` at a relative path. After the split, it must **not** load Sharpy.Stdlib — this ensures compiler tests are truly decoupled. The existing code should work as-is since it only looks for `Sharpy.Core.dll`.

**Verify:** Run a compiler test that does NOT use stdlib imports — it should pass. Run one that DOES use stdlib imports — it should fail with an import resolution error (confirming decoupling). That test should have been moved to Stdlib.Tests.

### Phase 7: Update Solution and CI

**Update `sharpy.sln`:**
```bash
dotnet sln add src/Sharpy.Stdlib/Sharpy.Stdlib.csproj
dotnet sln add src/Sharpy.Stdlib.Tests/Sharpy.Stdlib.Tests.csproj
```

**Update CI workflows** (`.github/workflows/dotnet10.yml`):
- Ensure `dotnet test` runs all test projects (it should automatically if using `dotnet test` at solution level)
- Add Sharpy.Stdlib to any build matrix or artifact steps

**Update `Directory.Build.props`:** No changes needed — `SharpyVersion` already applies to all projects.

### Phase 8: Update Documentation

- Update `CLAUDE.md` architecture table to list `Sharpy.Stdlib` alongside `Sharpy.Core`
- Update the `Architecture` diagram to show the split
- Update the "Sharpy.Core Patterns" section to distinguish Core vs Stdlib

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Stdlib module has hidden dependency on Core internal type | Low | Medium | `InternalsVisibleTo` is only set for `Sharpy.Core.Tests`; stdlib modules only use public API. Grep for `internal` usage. |
| Test fixture uses stdlib import not caught by grep | Low | Low | Run full test suite after split; any missed fixture will fail with import error. |
| CLI fails to locate Sharpy.Stdlib.dll at runtime | Medium | High | Validate assembly resolution in RunCommand early. Both DLLs will be in CLI output dir. |
| Generators/ directory belongs in Core not Stdlib | **Resolved** | N/A | Verified: Generators/ is compiler infrastructure — used by GeneratorContextBuilder via SharpyRT extern alias. Stays in Core. [CORRECTED: risk resolved by verification] |
| `System.Collections.Immutable` used by a stdlib module | Low | Low | Grep for usage; if stdlib-only, move the package ref. If shared, keep in Core. |

## Validation Criteria

1. `dotnet build sharpy.sln` succeeds with zero warnings
2. `dotnet test` — all existing tests pass (in their new project locations)
3. `grep -r "Sharpy\.Stdlib" src/Sharpy.Compiler/` returns nothing
4. `Sharpy.Compiler.csproj` has no reference to `Sharpy.Stdlib`
5. `Sharpy.Compiler.Tests.csproj` has no reference to `Sharpy.Stdlib`
6. `dotnet build src/Sharpy.Compiler/Sharpy.Compiler.csproj` succeeds independently
7. A `.spy` file using `import json` compiles and runs via the CLI
8. A `.spy` file using only builtins compiles even if Sharpy.Stdlib.dll is deleted from output

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-20
**Plan file:** `.claude/plans/plan-core-stdlib-split.md`

### Corrections Made

| # | Claim | Before | After | Reason |
|---|-------|--------|-------|--------|
| 1 | Root .cs files | ~101 | 89 | Actual count from `find src/Sharpy.Core -maxdepth 1 -name '*.cs'` |
| 2 | Stdlib module directories | 35 dirs, ~190 files | 33 dirs, ~152 files | Generators/ is compiler infrastructure (verified via GeneratorContextBuilder SharpyRT usage), not a stdlib module |
| 3 | Test fixtures with stdlib imports | ~91 / 1,958 | ~123 / 1,958 | Broader grep including all 33 module names found more matches |
| 4 | Compiler hard deps on Core | ~15 types | ~9-10 types | BuiltinRegistry registers: List, Set, FrozenDict, Bytes, Result, Optional, Template, Builtins; protocol interfaces via ProtocolRegistry |
| 5 | FunctoolsPartialHelper description | "AST-level desugaring" | Detection + type-inference helper | Helper provides `IsFunctoolsPartialCall()` and `ComputeResultTypeFromFunctionType()`; actual lambda emission is in RoslynEmitter |
| 6 | Generators/ in move list | Included (with verify note) | Removed from move list | Verified: used by compiler via SharpyRT extern alias for source generator context building |
| 7 | Phase 5 test counts | ~1,867 core / ~91 stdlib | ~1,835 core / ~123 stdlib | Recalculated from corrected stdlib fixture count |

### Warnings

- **CLI Sharpy.Stdlib.dll location** (Phase 3): The plan assumes `Sharpy.Stdlib.dll` will be alongside `Sharpy.Core.dll` in the CLI output directory because MSBuild copies transitive dependencies. This is correct for a `ProjectReference`, but the `CopyTransitiveDependencies()` helper mentioned in the code sample doesn't exist yet — it will need to be implemented to copy NuGet transitive deps (MathNet, Sqlite).
- **System.Collections.Immutable**: Verified as Core-only (used by FrozenDict and FrozenSet). No stdlib module uses it, so it stays in Core's .csproj.
- **CLAUDE.md stale module count**: CLAUDE.md lists 30 stdlib modules but actual count is 33. The 3 unlisted are: Numpy, Requests, Sqlite3 (the plan also lists Unittest as a 34th, which exists). CLAUDE.md should be updated as part of Phase 8.

### Missing Steps Added

None — the plan is comprehensive. All pipeline phases and test splits are covered.

### Unchecked Claims

- **IntegrationTestBase Sharpy.Stdlib.Tests version**: The plan proposes creating a new FileBasedIntegrationTests runner and IntegrationTestBase for Sharpy.Stdlib.Tests. The exact mechanism for loading both Core and Stdlib assemblies in `BuildSharedReferences()` will need to be designed during implementation — the existing IntegrationTestBase only loads Sharpy.Core.dll.
- **CopyTransitiveDependencies implementation**: The plan sketches this helper but doesn't specify how to enumerate NuGet transitive deps at runtime. MSBuild's `deps.json` or scanning the output directory are both viable approaches.

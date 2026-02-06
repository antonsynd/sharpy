# Module-as-Static-Class: Replace `Exports` Pattern

## Context

Currently, Sharpy compiles each `.spy` module into a **namespace** containing a static class called `Exports` (for module-level functions/constants) plus sibling type declarations:

```csharp
namespace TestProject.SomeModule {
    public static class Exports { public const float PI = 3.14f; public static int Foobar() => 1; }
    public class Circle { ... }  // sibling to Exports
}
```

C# consumers need two awkward imports:
```csharp
using TestProject.SomeModule;                // for types
using static TestProject.SomeModule.Exports; // for functions/constants
```

The `Exports` name is an implementation detail that leaks into the C# consumer API.

**Goal**: Implement Module-as-Static-Class — modules become static classes, directories become nested static class containers, user-defined types are nested, and `Exports` is eliminated.

### Target architecture

```
myproject/
  utils.spy                    → MyProject.Utils (static class)
  physics/                     → MyProject.Physics (static class container)
    __init__.spy               → adds members directly to MyProject.Physics
    collision.spy              → MyProject.Physics.Collision (nested static class)
    rigidbody.spy              → MyProject.Physics.Rigidbody (nested static class)
```

```csharp
// Generated from utils.spy
namespace MyProject {
    public static class Utils {
        public static void Helper() { ... }
    }
}

// Generated from physics/__init__.spy
namespace MyProject {
    [SharpyModule("physics")]
    public static partial class Physics {
        public const float G = 9.8f;  // from __init__.spy
        // Forwarding re-export from .collision
        public static bool CheckCollision(...) => Collision.CheckCollision(...);
    }
}

// Generated from physics/collision.spy
namespace MyProject {
    public static partial class Physics {  // wrapper — partial to merge with __init__.spy
        [SharpyModule("physics.collision")]
        public static class Collision {
            public static bool CheckCollision(...) { ... }
            public class CollisionResult { ... }  // types are nested
        }
    }
}
```

**C# consumption**: `using static MyProject.Utils;` or `using static MyProject.Physics;` — one import, everything accessible.

### Advantages
- Single `using static` import for C# consumers
- Module name IS the class name (semantically honest — Python modules ARE static classes)
- Nested types exposed by `using static` (per C# spec), so classes are directly accessible
- Matches Python's mental model: `import module` → `module.thing`
- Directory hierarchy → nested class hierarchy (natural)
- `__init__.spy` is optional — directories work as packages implicitly

### Drawbacks
- Nested types show `+` in IL reflection (`Physics+Collision` vs `Physics.Collision`)
- Name collision if file defines a type matching the module class name (rare — compiler error)
- `utils.spy` alongside `utils/` directory → conflict (compiler error)
- Breaking change to all generated C# output and Sharpy.Core
- Wrapper classes must be `partial` (harmless but slightly more verbose)

---

## Design Decisions

### D1. File name → class name; directory → partial static class wrapper
- `helpers.spy` → `public static class Helpers { ... }`
- `physics/` directory → `public static partial class Physics { ... }` (container)
- `physics/collision.spy` → nested inside: `partial class Physics { static class Collision { ... } }`
- Already computed by `SimpleToPascalCase()` in `RoslynEmitter.ModuleClass.cs:32-80`

### D2. Only the project namespace is a C# namespace
Everything below the project namespace is nested `static partial class` declarations. The current `GenerateNamespaceName()` is simplified to return only the project-level namespace.

| Source file | Project namespace | Wrapper classes | Module class |
|---|---|---|---|
| `config.spy` (root) | `TestProject` | (none) | `Config` |
| `lib/helpers.spy` | `TestProject` | `[Lib]` | `Helpers` |
| `lib/math/ops.spy` | `TestProject` | `[Lib, Math]` | `Ops` |
| `pkg/__init__.spy` | `TestProject` | (none) | `Pkg` |
| `pkg/sub/__init__.spy` | `TestProject` | `[Pkg]` | `Sub` |
| `pkg/sub/utils.spy` | `TestProject` | `[Pkg, Sub]` | `Utils` |
| single-file `foo.spy` | `Sharpy` | (none) | `Foo` |

### D3. Types become nested (not namespace siblings)
`GenerateModuleMembers()` stops routing `ClassDef/StructDef/InterfaceDef/EnumDef` to `namespaceTypes`. Everything goes into the module class. The extra `using Namespace;` for types (line 350 in CompilationUnit.cs) is removed.

### D4. `__init__.spy` members go directly into the directory class
`__init__.spy` generates the directory's static class body. Other files in the same directory generate `partial` declarations that add nested classes.

- `__init__.spy` is **optional**: directories create containers implicitly through wrapper declarations
- `__init__.spy` with only imports generates an empty class body (fine)
- Empty directories are ignored

### D5. Re-exports in `__init__.spy`
- Functions/constants → forwarding methods (already supported via `GenerateReExportMembers`)
- Types → **not supported** — emit compiler error (C# has no declaration-level type aliases)

### D6. Entry points keep `Program`
No change. `GetModuleClassName()` still returns `"Program"` when `willGenerateMainMethod == true`. Wrapper classes still apply (entry point in a subdirectory gets wrapped).

### D7. Name collision detection
- File defines type whose PascalCase name matches module class name → compiler error
- `utils.spy` alongside `utils/` directory → compiler error (detected by `ProjectCompiler`)

### D8. Sharpy.Core alignment: flat `namespace Sharpy`, module-named classes

> **Updated** — original plan kept per-module sub-namespaces (`Sharpy.Core`, `Sharpy.Math`, etc.). Actual implementation flattened all sub-namespaces into a single `namespace Sharpy` to eliminate redundancy like `Sharpy.Math.Math` and `Sharpy.Operator.Operator`.

All Sharpy.Core types and module classes now live in `namespace Sharpy`:

| Before (original) | After Phase 2 | After Phase 2b (actual) |
|---------|------|------|
| `Sharpy.Core.Exports` | `Sharpy.Core.Builtins` | `Sharpy.Builtins` |
| `Sharpy.Math.Exports` | `Sharpy.Math.Math` | `Sharpy.Math` |
| `Sharpy.Datetime.Exports` | `Sharpy.Datetime.Datetime` | `Sharpy.Datetime` |
| `Sharpy.Collections.Exports` | `Sharpy.Collections.Collections` | `Sharpy.Collections` |
| `Sharpy.Random.Exports` | `Sharpy.Random.Random` | `Sharpy.Random` |
| `Sharpy.Sys.Exports` | `Sharpy.Sys.Sys` | `Sharpy.Sys` |
| `Sharpy.Operator.Exports` | `Sharpy.Operator.Operator` | `Sharpy.Operator` |
| `Sharpy.Itertools.Exports` | `Sharpy.Itertools.Itertools` | `Sharpy.Itertools` |
| `Sharpy.Core.List<T>` | `Sharpy.Core.List<T>` | `Sharpy.List<T>` |
| `Sharpy.Core.Dict<K,V>` | `Sharpy.Core.Dict<K,V>` | `Sharpy.Dict<K,V>` |
| `Sharpy.Core.Set<T>` | `Sharpy.Core.Set<T>` | `Sharpy.Set<T>` |
| `Sharpy.Datetime.Date` | `Sharpy.Datetime.Date` | `Sharpy.Date` |
| `Sharpy.Collections.Deque<T>` | `Sharpy.Collections.Deque<T>` | `Sharpy.Deque<T>` |

> **Notes**: `Sharpy.Sys` is `public sealed partial class` (not `static`). `Sharpy.Itertools` is `internal static`. `Sharpy.Random.Random()` method was renamed to `Sharpy.Random.NextDouble()` to avoid CS0542 (member name matching enclosing type).

> **Critical issue from Phase 2b**: The flat `namespace Sharpy` causes `Sharpy.List<T>` to shadow `System.Collections.Generic.List<T>` and `Sharpy.DateTime` to shadow `System.DateTime` in the compiler (because `Sharpy.Compiler` is a child namespace of `Sharpy`, C# resolution finds `Sharpy.*` types first). This produces **246 build errors** in `Sharpy.Compiler`. Phase 3 must resolve this before any further work. See Risks section.

### D9. Module discovery via `[SharpyModule]` attribute
Replace `t.Name == "Exports"` convention in `OverloadIndexBuilder` with `[SharpyModule("name")]` attribute. Decouples discovery from naming. Generated code also gets the attribute for external library discoverability.

---

## Implementation Plan

### Phase 1: Add `[SharpyModule]` attribute — DONE ✓

> Completed in `3725db52` (2026-02-05).

**New file**: `src/Sharpy.Core/SharpyModuleAttribute.cs`
```csharp
namespace Sharpy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SharpyModuleAttribute : Attribute
    {
        public string ModuleName { get; }
        public SharpyModuleAttribute(string moduleName) { ModuleName = moduleName; }
    }
}
```

C# 9.0 compatible (targets `netstandard2.0/2.1`). Originally in `namespace Sharpy.Core`, moved to `namespace Sharpy` in Phase 2b.

### Phase 2: Rename Sharpy.Core `Exports` classes — DONE ✓

> Completed in `3725db52` (2026-02-05). 79 files changed.

All `class Exports` renamed to module-named classes with `[SharpyModule]` attributes:

**Files — class rename + attribute** (all completed as planned):
- `src/Sharpy.Core/Builtins/Exports.cs` → `Builtins`, `[SharpyModule("builtins")]`
- All `partial class Exports` files at `src/Sharpy.Core/` root (33 files: Print.cs, Len.cs, Range.cs, Int.cs, Bool.cs, etc.) → `partial class Builtins`
- `src/Sharpy.Core/Partial.Str/Str.cs` → `partial class Builtins`
- `src/Sharpy.Core/Math/Exports.cs` → `Math`, `[SharpyModule("math")]`
- `src/Sharpy.Core/Datetime/Exports.cs` → `Datetime`, `[SharpyModule("datetime")]`
- `src/Sharpy.Core/Collections/Exports.cs` → `Collections`, `[SharpyModule("collections")]`
- `src/Sharpy.Core/Random/Exports.cs` → `Random`, `[SharpyModule("random")]`
- `src/Sharpy.Core/Sys/Argv.cs` + `Sys/Stdout.cs` → `Sys`, `[SharpyModule("sys")]`
- `src/Sharpy.Core/Operator/` (15 files) → `Operator`, `[SharpyModule("operator")]`
- `src/Sharpy.Core/Itertools/` (3 files) → `Itertools`, `[SharpyModule("itertools")]`

**Deviation — `Random.Random()` → `Random.NextDouble()`**: Renaming `class Exports` to `class Random` caused CS0542 (member name matching enclosing type) on the `Random()` method. Resolved by renaming the method to `NextDouble()`, matching `System.Random.NextDouble()` convention.

**Internal using updates** (completed):
- `Builtins/Exports.cs`: `using static Sharpy.Sys.Exports;` → `using static Sharpy.Sys.Sys;`
- `Print.cs`: `using static Sharpy.Sys.Exports;` → `using static Sharpy.Sys.Sys;`
- `Dict.cs`: `using static Sharpy.Core.Exports;` → `using static Sharpy.Core.Builtins;`

**Sharpy.Core.Tests** (completed — 11 files):
- `Sharpy.Core.Exports` → `Sharpy.Core.Builtins` in GlobalUsings.cs, IdentityWrapper.cs, Wrapper.cs, etc.
- `ModuleIntegrationTests.cs`: `Sharpy.Sys.Exports` → `Sharpy.Sys.Sys`, `Sharpy.Math.Exports` → `Sharpy.Math.Math`, `Sharpy.Random.Exports` → `Sharpy.Random.Random`, `Sharpy.Random.Exports.Random()` → `Sharpy.Random.Random.NextDouble()`

### Phase 2b: Flatten all sub-namespaces into `namespace Sharpy` — DONE ✓

> Completed in `47a98a45` (2026-02-05). 121 files changed. This phase was NOT in the original plan — it was added to eliminate the redundant naming pattern (`Sharpy.Math.Math`, `Sharpy.Operator.Operator`, etc.).

**All 108 Sharpy.Core source files**: `namespace Sharpy.{X}` → `namespace Sharpy`
- `Sharpy.Core` → `Sharpy` (root files, Builtins/, Partial.* directories)
- `Sharpy.Math` → `Sharpy`
- `Sharpy.Operator` → `Sharpy`
- `Sharpy.Random` → `Sharpy`
- `Sharpy.Datetime` → `Sharpy`
- `Sharpy.Collections` → `Sharpy`
- `Sharpy.Sys` → `Sharpy`
- `Sharpy.Itertools` → `Sharpy`

**Cross-reference simplifications**:
- `Operator.Operator.Eq()` → `Operator.Eq()` (no longer double-nested)
- `Sharpy.Core.List<T>` → `Sharpy.List<T>` (in Random module)
- `using static Sharpy.Sys.Sys;` → `using static Sharpy.Sys;` (in Builtins, Print)
- `using static Sharpy.Core.Builtins;` → `using static Builtins;` (in Dict — same namespace)
- `using Sharpy.Core;` → removed (no longer needed, same namespace)
- `GlobalSuppressions.cs` targets updated for new fully-qualified names

**Sharpy.Core.Tests** (13 files updated):
- `GlobalUsings.cs`: `using static Sharpy.Core.Builtins;` → `using static Sharpy.Builtins;`, `using static Sharpy.Sys.Sys;` → `using static Sharpy.Sys;`
- `ModuleIntegrationTests.cs`: All qualified references simplified (e.g., `Sharpy.Math.Math.Sqrt()` → `Sharpy.Math.Sqrt()`, `Sharpy.Datetime.Date` → `Sharpy.Date`, `Sharpy.Collections.Deque<T>` → `Sharpy.Deque<T>`)
- Additional test files: `FrozenSetTests.cs`, `OptionalTests.cs`, `ResultTests.cs` — namespace updates

**⚠️ Build breakage introduced**: The compiler (`Sharpy.Compiler`) references the `Sharpy.Core` assembly. With all types now in `namespace Sharpy`, and the compiler living in `namespace Sharpy.Compiler` (a child of `Sharpy`), C# namespace resolution finds `Sharpy.List<T>` before `System.Collections.Generic.List<T>`, and `Sharpy.DateTime` before `System.DateTime`. This produces **246 build errors** (MSBuild count; 245 unique error locations) across `TypeChecker.*.cs`, `RoslynEmitter.*.cs`, `CompilationMetrics.cs`, and other compiler files. The `Sharpy.Core` project itself builds cleanly; only the compiler is affected. Phase 3 must resolve this.

### Phase 3: Fix compiler build + update assembly references and discovery

> **Status**: Not started. This is the immediate next step. The compiler does not build.

#### 3a. Fix namespace collision in compiler — CRITICAL

The flat `namespace Sharpy` causes `Sharpy.List<T>` to shadow `System.Collections.Generic.List<T>` and `Sharpy.DateTime` to shadow `System.DateTime` wherever the compiler uses unqualified names. The root cause is that `Sharpy.Compiler` lives in `namespace Sharpy.Compiler` — a child of `Sharpy` — so C# namespace resolution checks the parent `Sharpy` namespace first. Error breakdown (246 total per MSBuild, 245 unique locations):

| Error code | Count | Cause |
|---|---|---|
| CS0019 | 123 | `.Count` resolves to `Sharpy.List<T>.Count` (method group) instead of `List<T>.Count` (property); also `-` on `Sharpy.DateTime` |
| CS0029 | 52 | `List<T>` resolves to `Sharpy.List<T>`, implicit conversion from `System.Collections.Generic.List<T>` fails |
| CS1061 | 19 | Members not found on wrong `List<T>` type (e.g., `AddRange`, `RemoveAll`, `RemoveAt`) |
| CS1503 | 16 | Argument type mismatch (BCL `List` expected, `Sharpy.List` provided) |
| CS0117 | 8 | `DateTime.UtcNow` not found on `Sharpy.DateTime` |
| Other | 28 | Cascading errors: CS8602 (7), CS0428 (5), CS0165 (4), CS0234 (3), CS8618 (2), CS1501 (2), CS0173 (2), CS1929 (1), CS1660 (1) |

**Options** (choose one):

1. **~~Add `global using` alias in compiler~~** (**not viable**): C# does not support `global using` aliases for open generic types. `global using List = System.Collections.Generic.List;` is invalid syntax. Could work for non-generic types (`DateTime`, `Math`, `Random`) but not for `List<T>`, `Dict<K,V>`, `Set<T>` — which account for the vast majority of errors.

2. **Extern alias** (Recommended): Reference `Sharpy.Core` with an extern alias in `Sharpy.Compiler.csproj` so that `Sharpy.*` types don't pollute the compiler's default namespace resolution. Add `<Aliases>SharpyRT</Aliases>` to the ProjectReference. Access Sharpy.Core types via `SharpyRT::Sharpy.Builtins` in the few places needed (`AssemblyCompiler`, `BuiltinRegistry`, `RoslynEmitter.Expressions`). Cleanly isolates the compiler from Sharpy.Core's flat namespace.

3. **Restore a wrapper namespace**: Keep Sharpy.Core types in a sub-namespace (e.g., `namespace Sharpy.Core` or `namespace Sharpy.Runtime`) that doesn't collide with BCL. The module classes stay flat (`namespace Sharpy`) but collection types etc. retain their qualified names. This is a partial rollback of Phase 2b.

> **Recommendation**: Option 2 (extern alias) is the cleanest fix — it preserves the flat `namespace Sharpy` goal while isolating the compiler. Option 1 is not viable for generic types. Option 3 is the safest fallback if extern alias proves too cumbersome.

#### 3b. Update compiler `typeof()` references

After fixing the build, update all stale `typeof(Sharpy.Core.Exports)` references. If extern alias (Option 2) is used, these become `typeof(SharpyRT::Sharpy.Builtins)` instead:

**`src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`** (line 95):
- `typeof(Sharpy.Core.Exports).Assembly` → `typeof(Sharpy.Builtins).Assembly` (or `SharpyRT::Sharpy.Builtins` with extern alias)

**`src/Sharpy.Compiler/AssemblyCompiler.cs`** (lines 175, 299):
- `typeof(Sharpy.Core.Exports)` → `typeof(Sharpy.Builtins)` (or `SharpyRT::Sharpy.Builtins` with extern alias)

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`** (lines 165, 454):
- `global::Sharpy.Core.Exports.{name}` → `global::Sharpy.Builtins.{name}` (these are string literals in generated code, not compiler type references — extern alias does NOT apply here)

#### 3c. Update `OverloadIndexBuilder` discovery

**`src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`**:
- Line 37: Replace filter `t.Name == "Exports" && t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed` → `t.IsClass && t.GetCustomAttribute<SharpyModuleAttribute>() != null`. Drop `IsAbstract && IsSealed` (only matches `static` classes, but `Sharpy.Sys` is `sealed`). Drop `IsPublic` if Itertools discovery is desired (currently `internal`).
- Line 62: `t.Name != "Exports"` → `t.GetCustomAttribute<SharpyModuleAttribute>() == null` (exclude module containers from public type discovery)
- Lines 111-120 (`DeriveModuleName`): Read from `SharpyModuleAttribute.ModuleName` instead of hardcoded `Sharpy.Core.Exports` check
- Lines 102-109 (`DeriveModuleNameFromNamespace`): **Broken by Phase 2b** — all types are now in `namespace Sharpy`, so the check `ns == "Sharpy.Core"` → `"builtins"` fails (namespace is now `"Sharpy"`), and the fallback `ns.ToLowerInvariant()` returns `"sharpy"` for ALL types regardless of module. Must be rewritten to use `[SharpyModule]` attribute on the containing class, or determine module membership by type nesting (after Phase 4, types will be nested inside module classes).
- Also discover **nested types** inside module classes (types are now nested, not namespace siblings after Phase 4)

### Phase 4: Code generation — the core change

This is the primary deliverable. Changes are in `src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.cs`.

#### 4a. `GetModuleClassName()` — `RoslynEmitter.ModuleClass.cs:304-316`

Return the file/directory name instead of `"Exports"`:

```csharp
private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
{
    if (willGenerateMainMethod)
        return "Program";

    if (!string.IsNullOrEmpty(_context.SourceFilePath))
    {
        var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);
        if (fileName == DunderNames.Init)
        {
            // __init__.spy → use directory name as class name
            var dirName = Path.GetFileName(Path.GetDirectoryName(_context.SourceFilePath));
            return SimpleToPascalCase(dirName ?? "Module");
        }
        return SimpleToPascalCase(fileName);
    }
    return "Module"; // Fallback
}
```

#### 4b. `GenerateModuleMembers()` — `RoslynEmitter.ModuleClass.cs:162-173`

Stop separating types. Change the routing at lines 164-173:

```csharp
// BEFORE: types go to namespaceTypes
if (stmt is ClassDef or StructDef or InterfaceDef or EnumDef)
    namespaceTypes.Add(memberDecl);
else
    moduleDeclarations.Add(memberDecl);

// AFTER: everything goes into the module class
moduleDeclarations.Add(memberDecl);
```

Add name collision detection (before line 285):
- If any user-defined type's PascalCase name equals `moduleClassName` → emit error

Add `[SharpyModule]` attribute to module class declaration (non-Program only):
```csharp
var moduleClass = ClassDeclaration(moduleClassName)
    .WithAttributeLists(SingletonList(
        AttributeList(SingletonSeparatedList(
            Attribute(IdentifierName("SharpyModule"),
                AttributeArgumentList(SingletonSeparatedList(
                    AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                        Literal(sharpyModuleName))))))))))
    .WithModifiers(TokenList(
        Token(SyntaxKind.PublicKeyword),
        Token(SyntaxKind.StaticKeyword)))
    .WithMembers(List(moduleDeclarations));
```

#### 4c. `GenerateCompilationUnit()` — `RoslynEmitter.CompilationUnit.cs:16-72`

Restructure to generate nested class wrappers instead of namespace-per-file.

**New logic**:
1. Compute the project namespace string (just the root: `TestProject` or `Sharpy`)
2. Compute wrapper class names from directory path
3. Generate the module class content
4. Wrap module class with `partial static class` wrappers for each directory level
5. Wrap everything in the project namespace

**Computing wrappers**: Extract from `GenerateProjectNamespace()`:
- Get relative path from project root to source file
- Extract directory parts (PascalCase each)
- For `__init__.spy`: the last directory is the MODULE, not a wrapper. Wrappers = all dirs except last.
- For regular files: all directory parts are wrappers. File name = module class.

```csharp
// For lib/math/ops.spy:
//   wrappers = ["Lib", "Math"], module = "Ops"
// For lib/math/__init__.spy:
//   wrappers = ["Lib"], module = "Math"

var (wrapperNames, moduleClassName) = ComputeModuleHierarchy();

// Build from inside out:
MemberDeclarationSyntax current = moduleClass;
for (int i = wrapperNames.Count - 1; i >= 0; i--)
{
    current = ClassDeclaration(wrapperNames[i])
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword),
            Token(SyntaxKind.PartialKeyword)))
        .WithMembers(SingletonList(current));
}

var namespaceDecl = NamespaceDeclaration(ParseName(projectNamespace))
    .WithMembers(SingletonList(current));
```

#### 4d. `GenerateNamespaceName()` — `RoslynEmitter.CompilationUnit.cs:74-118`

Simplify to return ONLY the project-level namespace:

- With project: return `_context.ProjectNamespace` (e.g., `"TestProject"`)
- Single file with project ns: return `_context.ProjectNamespace`
- Single file standalone: return `"Sharpy"`
- No info: return `"SharpyGenerated"`

Remove `GenerateProjectNamespace()` (lines 120-148) — its directory-handling logic moves to the new `ComputeModuleHierarchy()` method.

#### 4e. Import `using` generation — `RoslynEmitter.CompilationUnit.cs:189-412`

For **user project modules**, the change is simple: **drop `.Exports` suffix** from all generated paths. No special package handling needed because directory classes are the package classes.

> **Note (Phase 2b impact)**: For **stdlib module imports** (`from math import sqrt`, `import random`, etc.), the path structure also changes. Since Phase 2b flattened stdlib modules to `namespace Sharpy`, the stdlib class `Sharpy.Math` is accessed directly — no sub-namespace. The generated `using static` must resolve to `global::Sharpy.Math` (not `TestProject.Math` or `Sharpy.Math.Math`). This requires the import using generator to distinguish stdlib modules from user project modules and emit `global::Sharpy.{ModuleName}` paths for stdlib.

**`GenerateImportUsings()` (lines 189-269)**:
```csharp
// BEFORE:
const string exportsClassName = "Exports";
fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}.{exportsClassName}";

// AFTER (all 3 locations):
fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}";
// Without project namespace:
fullModuleClass = namespaceName;
```

**`GenerateFromImportUsings()` (lines 271-363)**:
```csharp
// BEFORE:
const string moduleClassName = "Exports";
fullModuleClass = $"{_context.ProjectNamespace}.{moduleNamespacePath}.{moduleClassName}";

// AFTER:
fullModuleClass = $"{_context.ProjectNamespace}.{moduleNamespacePath}";
```

**Remove lines 337-350**: The extra non-static `using` for the module namespace. No longer needed — `using static` on the class exposes nested types.

**Update `GenerateReExportedTypeNamespaceUsings()` (lines 366-412)**: For type re-exports from `__init__.spy`, add a compiler error per D5. Function/constant re-exports continue to work via forwarding methods.

#### 4f. Re-export source class — `RoslynEmitter.ModuleClass.cs:333`

`ConvertModuleNameToNamespace("mypackage.helpers")` returns `"Mypackage.Helpers"`. With the project namespace: `"TestProject.Mypackage.Helpers"`. This resolves to nested class `Helpers` inside `Mypackage` inside namespace `TestProject`. The fully qualified class path = project namespace + converted module path:

```csharp
// BEFORE:
var sourceClassName = $"{sourceModuleNamespace}.Exports";

// AFTER:
var sourceClassName = !string.IsNullOrEmpty(_context.ProjectNamespace)
    ? $"{_context.ProjectNamespace}.{sourceModuleNamespace}"
    : sourceModuleNamespace;
```

#### 4g. Type re-export validation

Add validation in `GenerateReExportMembers()` (around line 337):
```csharp
case TypeSymbol typeSymbol:
    _context.AddError($"Cannot re-export type '{localName}' from __init__.spy. Import the type from its defining module instead.");
    break;
```

### Phase 5: Update all tests

#### Auto-regenerable (15 `.expected.cs` snapshots):
```bash
UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

#### Manual updates — compiler tests:

**Total scope**: 122 occurrences of `Sharpy.Core.Exports` across 29 files (16 source test files + 13 `.expected.cs` snapshots).

| File | Refs | Changes |
|------|------|---------|
| `RoslynEmitterModuleTests.cs` | 36 | Class name `Exports` → module name, remove `.Exports` from import paths, update namespace assertions to show wrapper classes |
| `NamespaceLevelTypesTests.cs` | ~5 | **Invert**: types must be INSIDE module class. Rename to `NestedTypeTests.cs` |
| `CrossModuleNamespaceTests.cs` | — | Update `class Exports` lookups, namespace → wrapper class assertions |
| `CompilerIntegrationTests.cs` | 12 | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `RoslynEmitterVariableRedefinitionTests.cs` | 8 | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `OverloadIndexBuilderTypeTests.cs` | 7 | Update discovery assertions for `[SharpyModule]`, `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `CachedDiscoveryPerformanceTests.cs` | 7 | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `OverloadIndexBuilderTests.cs` | 5 | Update discovery assertions for `[SharpyModule]` |
| `ImportResolverNetModuleTests.cs` | 5 | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `ModuleRegistryTests.cs` | 4 | Update module discovery references |
| `ModuleDiscoveryWorkflowTests.cs` | 4 | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| Other test files (5 files) | 1-2 each | `Sharpy.Core.Exports` → `Sharpy.Builtins` |
| `.expected.cs` snapshots (13 files) | ~60 total | Auto-regenerated via `UPDATE_SNAPSHOTS=true` |

#### Sharpy.Core tests — DONE ✓
> Completed in Phases 2 and 2b. All Sharpy.Core.Tests updated across both commits.

### Phase 6: Cache invalidation and documentation

**Incremental compilation cache**: Bump `CurrentSchemaVersion` in `IncrementalCompilationCache` (`src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs:39`, currently version 1) to force full rebuild.

**Documentation**: Update `CLAUDE.md` code examples, `.github/copilot-instructions.md`, `.github/agents/` references to `Exports`.

---

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **`Sharpy.*` types shadow BCL types in compiler** | **Critical — MATERIALIZED** | Phase 2b flattened all types to `namespace Sharpy`. Since `Sharpy.Compiler` is a child of the `Sharpy` namespace, C# resolution finds `Sharpy.List<T>` before `System.Collections.Generic.List<T>` and `Sharpy.DateTime` before `System.DateTime`. **246 build errors** (123 CS0019, 52 CS0029, 19 CS1061, 16 CS1503, 8 CS0117, 28 others). Must be fixed in Phase 3a before any further work. Extern alias recommended. |
| Cross-module type resolution with nested types | High | `using static Ns.Module;` exposes nested types (C# spec). Verify with multi-file fixtures. |
| Partial class merging for directory wrappers | High | Each file in a directory generates `partial static class Wrapper`. Roslyn handles merging. Test with multi-file projects. |
| `using static` on nested class (`Ns.Outer.Inner`) | Medium | Valid C# — `using static` works with any accessible static class, including nested ones. |
| Incremental cache stale after structural change | Medium | Bump cache schema version to force rebuild. |
| `System.Math` vs `Sharpy.Math` ambiguity in generated code | Low | Generated code uses `global::` prefix for Sharpy.Core refs. |
| `System.Random` vs `Sharpy.Random` ambiguity | Low | Generated code uses `global::` prefix. Module class is in `namespace Sharpy` so `global::Sharpy.Random` is unambiguous. |
| Non-static module classes (`Sharpy.Sys`) | Medium | `Sharpy.Sys` is `sealed` not `static`. Phase 3c's `[SharpyModule]` filter must not require `IsAbstract && IsSealed`. |

---

## Verification

1. `dotnet build sharpy.sln` — everything compiles
2. `dotnet test` — all tests pass (after updates)
3. `UPDATE_SNAPSHOTS=true dotnet test --filter FileBasedIntegrationTests` — regenerate snapshots
4. Inspect generated C# for a simple module:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello_world.spy
   ```
   Confirm: no `Exports`, types nested in module class
5. Inspect a multi-file project — verify cross-module imports resolve with nested class paths
6. Verify module with types:
   ```bash
   echo -e "class Foo:\n    x: int = 0\ndef bar() -> int:\n    return 1" > /tmp/test.spy
   dotnet run --project src/Sharpy.Cli -- emit csharp /tmp/test.spy
   ```
   Confirm: `Foo` nested inside module static class
7. Test package with `__init__.spy` + submodules — verify `partial` merging works

---

## Critical files

| File | Role | Status |
|------|------|--------|
| `src/Sharpy.Core/SharpyModuleAttribute.cs` | `[SharpyModule]` attribute for discovery. | ✅ Done (Phase 1) |
| `src/Sharpy.Core/Builtins/Exports.cs` | Primary module class — renamed to `Builtins`. | ✅ Done (Phase 2) |
| `src/Sharpy.Core/Operator/` (15 files) | Operator module — renamed to `Operator`. | ✅ Done (Phase 2) |
| All 108 Sharpy.Core source files | Namespace flattened to `Sharpy`. | ✅ Done (Phase 2b) |
| `src/Sharpy.Compiler/Sharpy.Compiler.csproj` | Extern alias for Sharpy.Core reference (fixes 246 build errors). | ❌ Phase 3a |
| `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Assembly anchor (`typeof(Exports)` → `typeof(Builtins)`). | ❌ Phase 3b |
| `src/Sharpy.Compiler/AssemblyCompiler.cs` | Assembly reference paths. | ❌ Phase 3b |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Builtin function call paths (`global::Sharpy.Core.Exports` → `global::Sharpy.Builtins`). | ❌ Phase 3b |
| `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs` | Module discovery (`"Exports"` → `[SharpyModule]`). | ❌ Phase 3c |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` | Namespace + compilation unit generation. Core restructuring (nested wrappers). | ❌ Phase 4 |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` | Module class naming, member separation, re-exports. | ❌ Phase 4 |
| `src/Sharpy.Compiler.Tests/CodeGen/NamespaceLevelTypesTests.cs` | Must invert — types now nested, not at namespace level. | ❌ Phase 5 |

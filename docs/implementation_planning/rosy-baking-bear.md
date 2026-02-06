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

### D8. Sharpy.Core alignment: rename `Exports`, keep namespaces
Sharpy.Core is hand-written C#. Full restructuring to nested classes is high-risk for minimal gain. Instead:
- Rename `Exports` to the module name (e.g., `Sharpy.Core.Builtins`, `Sharpy.Math.Math`)
- Keep existing namespaces unchanged
- Add `[SharpyModule]` attribute for discovery

| Current | New |
|---------|-----|
| `Sharpy.Core.Exports` | `Sharpy.Core.Builtins` |
| `Sharpy.Math.Exports` | `Sharpy.Math.Math` |
| `Sharpy.Datetime.Exports` | `Sharpy.Datetime.Datetime` |
| `Sharpy.Collections.Exports` | `Sharpy.Collections.Collections` |
| `Sharpy.Random.Exports` | `Sharpy.Random.Random` |
| `Sharpy.Sys.Exports` | `Sharpy.Sys.Sys` |
| `Sharpy.Operator.Exports` | `Sharpy.Operator.Operator` |
| `Sharpy.Itertools.Exports` | `Sharpy.Itertools.Itertools` |

> **Notes**: `Sharpy.Sys.Exports` is `public sealed partial class` (not `static`) — it is NOT discovered by the current `OverloadIndexBuilder` filter. `Sharpy.Itertools.Exports` is `internal static` — also not discovered currently. Both will be handled by the `[SharpyModule]` attribute in Phase 3.

The redundant naming (`Sharpy.Math.Math`) is harmless — C# consumers never see it directly; the compiler generates the qualified references.

### D9. Module discovery via `[SharpyModule]` attribute
Replace `t.Name == "Exports"` convention in `OverloadIndexBuilder` with `[SharpyModule("name")]` attribute. Decouples discovery from naming. Generated code also gets the attribute for external library discoverability.

---

## Implementation Plan

### Phase 1: Add `[SharpyModule]` attribute

**New file**: `src/Sharpy.Core/SharpyModuleAttribute.cs`
```csharp
namespace Sharpy.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SharpyModuleAttribute : Attribute
    {
        public string ModuleName { get; }
        public SharpyModuleAttribute(string moduleName) { ModuleName = moduleName; }
    }
}
```

C# 9.0 compatible (targets `netstandard2.0/2.1`).

### Phase 2: Rename Sharpy.Core `Exports` classes

Rename `class Exports` to the module name in all Sharpy.Core modules. Add `[SharpyModule("name")]`.

**Files — class rename + attribute** (rename `class Exports` → new name):
- `src/Sharpy.Core/Builtins/Exports.cs` → `Builtins`, `[SharpyModule("builtins")]`
- All `partial class Exports` files at `src/Sharpy.Core/` root (33 files: Print.cs, Len.cs, Range.cs, Int.cs, Bool.cs, etc.) → `partial class Builtins`
- `src/Sharpy.Core/Partial.Str/Str.cs` → `partial class Builtins` (also `Sharpy.Core` namespace, not at root)
- `src/Sharpy.Core/Math/Exports.cs` → `Math`, `[SharpyModule("math")]`
- `src/Sharpy.Core/Datetime/Exports.cs` → `Datetime`, `[SharpyModule("datetime")]`
- `src/Sharpy.Core/Collections/Exports.cs` → `Collections`, `[SharpyModule("collections")]`
- `src/Sharpy.Core/Random/Exports.cs` → `Random`, `[SharpyModule("random")]`
- `src/Sharpy.Core/Sys/Argv.cs` + `Sys/Stdout.cs` → `Sys`, `[SharpyModule("sys")]` (currently `public sealed`, not `public static`)
- `src/Sharpy.Core/Operator/` (15 files: Add.cs, Abs.cs, Eq.cs, Ge.cs, Gt.cs, IAdd.cs, IMul.cs, Is.cs, IsNot.cs, Le.cs, Lt.cs, Mul.cs, Ne.cs, Not.cs, Truth.cs) → `Operator`, `[SharpyModule("operator")]`
- `src/Sharpy.Core/Itertools/` (3 files: Additional.cs, Cycle.cs, Repeat.cs) → `Itertools`, `[SharpyModule("itertools")]` (currently `internal static`, not `public static`)

**Internal using updates**:
- `Builtins/Exports.cs`: `using static Sharpy.Sys.Exports;` → `using static Sharpy.Sys.Sys;`
- `Print.cs`: `using static Sharpy.Sys.Exports;` → `using static Sharpy.Sys.Sys;`
- `Dict.cs`: `using static Sharpy.Core.Exports;` → `using static Sharpy.Core.Builtins;`

**Sharpy.Core.Tests**: Update all `Sharpy.Core.Exports` → `Sharpy.Core.Builtins` (GlobalUsings.cs, IdentityWrapper.cs, Wrapper.cs, etc.)

### Phase 3: Update compiler assembly references and discovery

**`src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`**:
- Line 37: Replace entire filter `t.Name == "Exports" && t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed` → `t.IsClass && t.GetCustomAttribute<SharpyModuleAttribute>() != null`. Drop `IsAbstract && IsSealed` (only matches `static` classes, but `Sharpy.Sys` is `sealed`). Drop `IsPublic` if Itertools discovery is desired (currently `internal`).
- Line 62: `t.Name != "Exports"` → `t.GetCustomAttribute<SharpyModuleAttribute>() == null` (exclude module containers from public type discovery)
- Lines 111-120 (`DeriveModuleName`): Read from `SharpyModuleAttribute.ModuleName`
- Also discover **nested types** inside module classes (types are now nested, not namespace siblings)

**`src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`** (line 95):
- `typeof(Sharpy.Core.Exports).Assembly` → `typeof(Sharpy.Core.Builtins).Assembly`

**`src/Sharpy.Compiler/AssemblyCompiler.cs`** (lines 175, 299):
- `typeof(Sharpy.Core.Exports)` → `typeof(Sharpy.Core.Builtins)`

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`** (lines 165, 454):
- `global::Sharpy.Core.Exports.{name}` → `global::Sharpy.Core.Builtins.{name}`

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

The change is simple: **drop `.Exports` suffix** from all generated paths. No special package handling needed because directory classes are the package classes.

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
| File | Changes |
|------|---------|
| `RoslynEmitterModuleTests.cs` (~36 refs) | Class name `Exports` → module name, remove `.Exports` from import paths, update namespace assertions to show wrapper classes |
| `NamespaceLevelTypesTests.cs` (~5 assertions) | **Invert**: types must be INSIDE module class. Rename to `NestedTypeTests.cs` |
| `CrossModuleNamespaceTests.cs` | Update `class Exports` lookups, namespace → wrapper class assertions |
| `CompilerIntegrationTests.cs` (~12 refs) | `Sharpy.Core.Exports` → `Sharpy.Core.Builtins` |
| `PackageResolverTests.cs` (~10 refs) | Update import path assertions |
| `OverloadIndexBuilder*Tests.cs` | Update discovery assertions for `[SharpyModule]` |
| All files with `Sharpy.Core.Exports` | Search-replace to `Sharpy.Core.Builtins` |

#### Sharpy.Core tests (11 files):
- Search-replace `Sharpy.Core.Exports` → `Sharpy.Core.Builtins` (GlobalUsings.cs, IdentityWrapper.cs, Wrapper.cs, PrintTests.cs, IntConversionTests.cs, DoubleConversionTests.cs, ListConversionTests.cs, SetConversionTests.cs, FrozenSetConversionTests.cs, TupleConversionTests.cs, ModuleIntegrationTests.cs)
- `ModuleIntegrationTests.cs` additionally: `Sharpy.Sys.Exports` → `Sharpy.Sys.Sys`, `Sharpy.Math.Exports` → `Sharpy.Math.Math`, `Sharpy.Random.Exports` → `Sharpy.Random.Random` (17+ references)

### Phase 6: Cache invalidation and documentation

**Incremental compilation cache**: Bump `CurrentSchemaVersion` in `IncrementalCompilationCache` (`src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs:39`, currently version 1) to force full rebuild.

**Documentation**: Update `CLAUDE.md` code examples, `.github/copilot-instructions.md`, `.github/agents/` references to `Exports`.

---

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Cross-module type resolution with nested types | High | `using static Ns.Module;` exposes nested types (C# spec). Verify with multi-file fixtures. |
| Partial class merging for directory wrappers | High | Each file in a directory generates `partial static class Wrapper`. Roslyn handles merging. Test with multi-file projects. |
| `using static` on nested class (`Ns.Outer.Inner`) | Medium | Valid C# — `using static` works with any accessible static class, including nested ones. |
| Incremental cache stale after structural change | Medium | Bump cache schema version to force rebuild. |
| `Sharpy.Math.Math` redundancy in Sharpy.Core | Low | Cosmetic — consumers don't see it. Can flatten later. |
| `System.Math` vs `Sharpy.Math.Math` ambiguity | Low | Generated code uses `global::` prefix for Sharpy.Core refs. |
| Non-static module classes (`Sharpy.Sys`) | Medium | `Sharpy.Sys.Exports` is `sealed` not `static`. Phase 3's `[SharpyModule]` filter must not require `IsAbstract && IsSealed`. |

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

| File | Role |
|------|------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` | Namespace + compilation unit generation. Core restructuring (nested wrappers). |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` | Module class naming, member separation, re-exports. |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Builtin function call paths (`Sharpy.Core.Exports` → `Builtins`). |
| `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs` | Module discovery (`"Exports"` → `[SharpyModule]`). |
| `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Assembly anchor (`typeof(Exports)` → `typeof(Builtins)`). |
| `src/Sharpy.Compiler/AssemblyCompiler.cs` | Assembly reference paths. |
| `src/Sharpy.Core/Builtins/Exports.cs` | Primary Exports class — rename to Builtins. |
| `src/Sharpy.Compiler.Tests/CodeGen/NamespaceLevelTypesTests.cs` | Must invert — types now nested, not at namespace level. |
| `src/Sharpy.Core/Operator/` (15 files) | Operator module `Exports` classes — must be renamed and attributed. |

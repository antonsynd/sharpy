# Reverse Name Mangling & Collision Detection — Implementation Plan

> **Design doc:** [`reverse-name-mangling-and-collision-analysis.md`](./reverse-name-mangling-and-collision-analysis.md)
>
> **Scope:** Decisions 1, 3, 4 from the design doc. Decisions 2 (keep `I` prefix) and 6 (documentation) require no compiler changes. Decision 5 (builtin shadowing) is deferred to a follow-up.
>
> **Approach:** Each phase produces a self-contained commit with tests. Earlier phases have zero dependencies on later ones. A junior engineer or Claude Sonnet should be able to pick up any phase independently.

---

## Phase 1: Add the third regex pass to reverse mangling

> **Goal:** Fix digit→word boundary splitting so `Base64Encoder` → `base64_encoder`, `SHA256Managed` → `sha256_managed`, etc.
>
> **Files touched:** 2 (1 production, 1 test)

### 1a. Add Pass 2 to `GetFunctionName()`

**File:** `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs` (line ~188)

Insert the new regex pass **between** the existing two passes:

```csharp
private string GetFunctionName(MethodInfo method)
{
    var name = method.Name;
    // Pass 1: Acronym boundaries (XMLParser → XML_Parser)
    name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
    // Pass 2: Digit→word boundaries (Base64Encoder → Base64_Encoder)
    name = Regex.Replace(name, "([0-9])([A-Z][a-z])", "$1_$2");
    // Pass 3: camelCase boundaries (getUserName → get_User_Name)
    name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
    return name.ToLowerInvariant();
}
```

- [ ] Add the `([0-9])([A-Z][a-z])` regex pass between existing passes 1 and 3
- [ ] Update the comments to label the passes 1/2/3

### 1b. Add unit tests for `GetFunctionName()`

**File:** `src/Sharpy.Compiler.Tests/Discovery/Caching/OverloadIndexBuilderTests.cs`

There are **zero** existing tests for the reverse mangling algorithm itself. Add a dedicated test class or `[Theory]` with `[InlineData]` covering the comprehensive results table from the design doc. The method is `private`, so test it indirectly by creating a simple test assembly with methods of known names, or extract the algorithm into a static helper and test it directly.

**Recommended approach:** Extract the regex logic into an `internal static` method (e.g., `ReverseNameMangler.ToSnakeCase(string name)`) in a new file `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs`, and call it from `GetFunctionName()`. This makes it directly testable and prepares for Phase 2 (symbol-aware mangling). `[InternalsVisibleTo("Sharpy.Compiler.Tests")]` is already configured in the `.csproj`.

- [ ] Extract reverse mangling logic into `ReverseNameMangler.ToSnakeCase(string name)`
- [ ] Create `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs`
- [ ] Update `GetFunctionName()` to call `ReverseNameMangler.ToSnakeCase(method.Name)`
- [ ] Create `src/Sharpy.Compiler.Tests/Discovery/ReverseNameManglerTests.cs`
- [ ] Add `[Theory]`/`[InlineData]` tests for **all 22 rows** of the comprehensive results table in the design doc appendix:
  - `GetUserName` → `get_user_name`
  - `XMLParser` → `xml_parser`
  - `HTTPSConnection` → `https_connection`
  - `Base64Encoder` → `base64_encoder`
  - `Int32Converter` → `int32_converter`
  - `SHA256Managed` → `sha256_managed`
  - `Utf8JsonReader` → `utf8_json_reader`
  - `X509Certificate` → `x509_certificate`
  - `Win32Error` → `win32_error`
  - `Log2Ceil` → `log2_ceil`
  - `H264Decoder` → `h264_decoder`
  - `Dx11Renderer` → `dx11_renderer`
  - `H2OParser` → `h2o_parser`
  - `CO2Level` → `co2_level`
  - `Vector3D` → `vector3d`
  - `Color4F` → `color4f`
  - `CRC32C` → `crc32c`
  - `Matrix4x4` → `matrix4x4`
  - `_4E` → `_4e`
  - `Base64` → `base64`
  - `Int32` → `int32`
  - `SHA256` → `sha256`
  - `ToString` → `to_string`
  - `ReadAllText` → `read_all_text`
- [ ] Run `dotnet test --filter "FullyQualifiedName~ReverseNameMangler"` — all pass
- [ ] Run `dotnet test --filter "FullyQualifiedName~OverloadIndexBuilder"` — existing tests still pass

### 1c. Commit

```
feat: Add digit→word boundary splitting to reverse name mangling

Extract reverse mangling into ReverseNameMangler with 3-pass regex
algorithm. Adds Pass 2 for digit→word boundaries (e.g., Base64Encoder
→ base64_encoder, SHA256Managed → sha256_managed).
```

---

## Phase 2: Symbol-aware reverse mangling

> **Goal:** Make `ReverseNameMangler` produce `SCREAMING_SNAKE_CASE` for enum members / constants and preserve type/interface names, instead of always producing `snake_case`.
>
> **Files touched:** ~4 (1 new enum, 1 updated `ReverseNameMangler`, 1 updated `OverloadIndexBuilder`, 1 test file)

### 2a. Define `ReverseNameContext` enum

**File:** `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs` (same file from Phase 1)

```csharp
internal enum ReverseNameContext
{
    Method,       // → snake_case
    Property,     // → snake_case
    Parameter,    // → snake_case
    EnumMember,   // → SCREAMING_SNAKE_CASE
    Constant,     // → SCREAMING_SNAKE_CASE
    Type,         // → preserved as-is
    Interface     // → preserved as-is
}
```

- [ ] Add `ReverseNameContext` enum to `ReverseNameMangler.cs`

### 2b. Add context-aware overload

**File:** `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs`

Add a second overload `ToSharpyName(string name, ReverseNameContext context)`:

```csharp
internal static string ToSharpyName(string name, ReverseNameContext context)
{
    return context switch
    {
        ReverseNameContext.Type or ReverseNameContext.Interface => name,
        ReverseNameContext.EnumMember or ReverseNameContext.Constant => ToScreamingSnakeCase(name),
        _ => ToSnakeCase(name)
    };
}
```

Where `ToScreamingSnakeCase` uses the same 3-pass word splitting but joins with `_` and calls `.ToUpperInvariant()`.

- [ ] Add `ToScreamingSnakeCase(string name)` — same word splitting, `.ToUpperInvariant()`
- [ ] Add `ToSharpyName(string name, ReverseNameContext context)` dispatcher
- [ ] Keep `ToSnakeCase` as the default (existing callers unchanged)

### 2c. Add tests for context-aware mangling

**File:** `src/Sharpy.Compiler.Tests/Discovery/ReverseNameManglerTests.cs`

- [ ] Test `ToSharpyName("DarkBlue", EnumMember)` → `"DARK_BLUE"`
- [ ] Test `ToSharpyName("MaxRetryCount", Constant)` → `"MAX_RETRY_COUNT"`
- [ ] Test `ToSharpyName("GetUserName", Method)` → `"get_user_name"`
- [ ] Test `ToSharpyName("StringBuilder", Type)` → `"StringBuilder"` (preserved)
- [ ] Test `ToSharpyName("IComparable", Interface)` → `"IComparable"` (preserved)
- [ ] Test `ToScreamingSnakeCase("Base64Encoder")` → `"BASE64_ENCODER"`
- [ ] Test `ToScreamingSnakeCase("XMLParser")` → `"XML_PARSER"`
- [ ] Test `ToScreamingSnakeCase("SHA256Managed")` → `"SHA256_MANAGED"`
- [ ] Run `dotnet test --filter "FullyQualifiedName~ReverseNameMangler"` — all pass

### 2d. Wire into `OverloadIndexBuilder`

**File:** `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`

Update `GetFunctionName()` to call `ReverseNameMangler.ToSharpyName(method.Name, ReverseNameContext.Method)`. This is a refactor with identical behavior for functions — the context-aware path only matters for future discovery of enum members and properties.

- [ ] Update `GetFunctionName()` to use `ReverseNameMangler.ToSharpyName(..., Method)`
- [ ] Run `dotnet test --filter "FullyQualifiedName~OverloadIndexBuilder"` — all pass (no behavior change)

### 2e. Commit

```
feat: Add symbol-aware reverse name mangling

ReverseNameMangler now accepts ReverseNameContext to produce
SCREAMING_SNAKE_CASE for enum members/constants and preserve
type/interface names as-is. Methods/properties/parameters still
produce snake_case.
```

---

## Phase 3: Forward collision detection in `CodeGenInfoComputer`

> **Goal:** Detect when two Sharpy symbols in the same scope mangle to the same C# name and emit `SPY0522`.
>
> **Files touched:** ~5 (1 new diagnostic code, 1 production `CodeGenInfoComputer`, 1 `DiagnosticBag` threading, 1–2 test files)
>
> **Note:** `SPY0520` (`DiagnosticCodes.CodeGen.NameCollision`) is already used for module-class name collisions (when a type name matches the module class name derived from the file name, e.g. `struct Animal` in `animal.spy`). The new member collision detection uses a separate code `SPY0522` (`MemberNameCollision`).

### 3a. Register the new diagnostic code `SPY0522`

**File:** `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`

Add a new constant for member name collisions (distinct from the existing `SPY0520` which covers module-class name collisions):

```csharp
public const string MemberNameCollision = "SPY0522";
```

**File:** `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs`

Add an explanation entry for the new code:

```csharp
Add(dict, DiagnosticCodes.CodeGen.MemberNameCollision, "Member name collision after mangling", "CodeGen",
    "Two symbols in the same scope produce the same C# name after name mangling. " +
    "For example, 'foo_bar' and 'FooBar' both compile to 'FooBar'.",
    "class Foo:\n    def foo_bar(self): ...\n    def FooBar(self): ...",
    "Rename one of the conflicting symbols or use backtick escaping.");
```

- [ ] Add `MemberNameCollision = "SPY0522"` to `DiagnosticCodes.CodeGen`
- [ ] Add explanation entry to `DiagnosticExplanations.cs`

### 3b. Thread `DiagnosticBag` into `CodeGenInfoComputer`

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

The class currently has no way to emit diagnostics. Add an **optional** `DiagnosticBag?` parameter to the constructor, mirroring the existing `SemanticBinding?` pattern. This avoids breaking existing test call sites (17+ in `CodeGenInfoComputerTests.cs`) that construct `CodeGenInfoComputer` without a `DiagnosticBag`.

- [ ] Add `private readonly DiagnosticBag _diagnostics;` field
- [ ] Add `DiagnosticBag? diagnostics = null` parameter to the constructor (after the existing `semanticBinding` parameter)
- [ ] Initialize with `_diagnostics = diagnostics ?? new DiagnosticBag();`
- [ ] Update the production call site at `TypeChecker.cs:278` to pass the TypeChecker's `_diagnostics` field
  - Only one production call site: `new CodeGenInfoComputer(_symbolTable, SemanticBinding)` → `new CodeGenInfoComputer(_symbolTable, SemanticBinding, _diagnostics)`
  - Test call sites (17+ in `CodeGenInfoComputerTests.cs`) remain unchanged — the default `null` creates a no-op bag

### 3c. Add collision detection after type member processing

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

After `ProcessTypeMembers(typeSymbol, body)` returns, collect all `CSharpName` values from the members just processed. If duplicates are found, emit `SPY0522` on the second declaration.

```csharp
private void DetectCollisions(TypeSymbol typeSymbol, IEnumerable<Statement> body)
{
    var seen = new Dictionary<string, string>(); // CSharpName → originalName

    foreach (var symbol in typeSymbol.Fields.Concat<Symbol>(typeSymbol.Methods))
    {
        var info = _semanticBinding.GetCodeGenInfo(symbol);
        if (info == null) continue;

        if (seen.TryGetValue(info.CSharpName, out var existingOriginal))
        {
            _diagnostics.AddError(
                $"Name collision: '{info.OriginalName}' and '{existingOriginal}' both compile to '{info.CSharpName}'. Rename one or use backtick escaping.",
                span, // TextSpan from the AST node for the second declaration
                code: DiagnosticCodes.CodeGen.MemberNameCollision
            );
        }
        else
        {
            seen[info.CSharpName] = info.OriginalName;
        }
    }
}
```

Implementation notes:
- To get `TextSpan`, correlate the symbol back to its AST node. The body `IEnumerable<Statement>` is available — match by name.
- In C#, ALL members of a class share the same declaration space — methods, fields, and nested types cannot have the same name. There are no separate collision domains within a single type.
- Dunder methods that map to special C# names (`__init__` → constructor, `__str__` → `ToString`) should be included — if a user also defines a method `to_string` (→ `ToString`), that's a real collision.
- Note: `ProcessTypeMembers` currently only handles `VariableDeclaration` (fields) and `FunctionDef` (methods) — Sharpy does not support nested type definitions inside a class body, so checking fields + methods is sufficient for type-level collision detection.

- [ ] Add `DetectCollisions(TypeSymbol, IEnumerable<Statement>)` method
- [ ] Call it from `ProcessClassDef`, `ProcessStructDef`, `ProcessInterfaceDef` after `ProcessTypeMembers`

### 3d. Add collision detection for module-level symbols

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

At the end of `ComputeForModule()`, check for collisions among ALL module-level symbols: functions, variables, AND types (classes, structs, interfaces, enums). All of these become members of the same module class in the generated C# — types are nested inside the module class (see `GenerateModuleMembers` docstring: "Types are nested inside the module class, enabling single 'using static' imports").

- [ ] Add `DetectModuleLevelCollisions(Module module)` method
- [ ] Call it at the end of `ComputeForModule()`
- [ ] Scope: functions + module-level variables + types (all share the same C# module class declaration space)
- [ ] Exclude symbols that already trigger `SPY0520` (module-class name collision) to avoid duplicate errors

### 3e. Add integration tests for collision detection

**File:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` — new `.spy` + `.error`/`.expected` pairs

**Test 1: method collision in class**
```python
# collision_method.spy
class Foo:
    def foo_bar(self):
        pass
    def FooBar(self):
        pass
```
```
# collision_method.error
SPY0522
```

**Test 2: field collision in class**
```python
# collision_field.spy
class Foo:
    foo_bar: int = 1
    FooBar: int = 2
```
```
# collision_field.error
SPY0522
```

**Test 3: module-level function collision**
```python
# collision_module_func.spy
def foo_bar():
    pass

def FooBar():
    pass
```
```
# collision_module_func.error
SPY0522
```

**Test 4: module-level function + type collision**

In Sharpy's generated C#, types are nested inside the module class. A nested type and a method with the same name violate C#'s single declaration space rule (CS0102). The collision detection must catch this.

```python
# collision_func_type.spy
class FooBar:
    pass

def foo_bar():
    pass
```
```
# collision_func_type.error
SPY0522
```

**Test 5: no collision — function and type with different mangled names**
```python
# no_collision_func_type.spy
class Bar:
    pass

def foo_bar():
    print("ok")

foo_bar()
```
```
# no_collision_func_type.expected
ok
```

Here `Bar` → `Bar` and `foo_bar` → `FooBar` — different C# names, no collision.

**Test 6: no collision between camelCase and snake_case (they produce different results)**
```python
# no_collision_camel_snake.spy
class Foo:
    def foo_bar(self):
        print("snake")
    def fooBar(self):
        print("camel")

f = Foo()
f.foo_bar()
f.fooBar()
```
```
# no_collision_camel_snake.expected
snake
camel
```

- [ ] Create `collision_method.spy` + `collision_method.error`
- [ ] Create `collision_field.spy` + `collision_field.error`
- [ ] Create `collision_module_func.spy` + `collision_module_func.error`
- [ ] Create `collision_func_type.spy` + `collision_func_type.error`
- [ ] Create `no_collision_func_type.spy` + `no_collision_func_type.expected`
- [ ] Create `no_collision_camel_snake.spy` + `no_collision_camel_snake.expected`
- [ ] Decide on subdirectory name (e.g., `TestFixtures/name_collision/`) or place in an existing relevant directory
- [ ] Run `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` — all pass

### 3f. Add unit tests for `CodeGenInfoComputer` collision detection

**File:** `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` (append to existing — already has 17 tests)

Use `IntegrationTestBase.CompileAndExecute()` or build the AST manually to test:

- [ ] Test: two methods with same mangled name → `SPY0522` error
- [ ] Test: method + field with same mangled name → `SPY0522` error
- [ ] Test: module-level function + type with same mangled name → `SPY0522` error (both are members of the module class)
- [ ] Test: no collision when names are different → no error
- [ ] Run `dotnet test --filter "FullyQualifiedName~CodeGenInfoComputer"` — all pass

### 3g. Commit

```
feat: Add forward name collision detection (SPY0522)

CodeGenInfoComputer now detects when two symbols in the same scope
produce the same C# name after mangling. Emits SPY0522 with both
original names and the colliding C# name. Covers class members and
module-level symbols (functions, variables, and nested types all
share the same module class declaration space).
```

---

## Phase 4: Round-trip fidelity tests

> **Goal:** Prove that `snake_case → PascalCase → snake_case` is an identity transform for common names. These are property-based regression tests, not new functionality.
>
> **Files touched:** 1 test file

### 4a. Add round-trip tests

**File:** `src/Sharpy.Compiler.Tests/Discovery/ReverseNameManglerTests.cs` (append to existing)

Test that the forward mangler (`NameMangler.ToPascalCase`) composed with the reverse mangler (`ReverseNameMangler.ToSnakeCase`) is an identity for snake_case inputs:

```csharp
[Theory]
[InlineData("get_user_name")]
[InlineData("max_size")]
[InlineData("xml_parser")]
[InlineData("read_all_text")]
[InlineData("sha256_managed")]
[InlineData("base64_encoder")]
[InlineData("simple")]
[InlineData("a")]
[InlineData("x_y_z")]
public void RoundTrip_SnakeCase_IsIdentity(string input)
{
    var forward = NameMangler.ToPascalCase(input);
    var reverse = ReverseNameMangler.ToSnakeCase(forward);
    Assert.Equal(input, reverse);
}
```

- [ ] Add `RoundTrip_SnakeCase_IsIdentity` theory with ≥9 representative snake_case inputs
- [ ] Add `RoundTrip_NonSnakeCase_MayNotBeIdentity` tests documenting known non-round-trip cases (`httpClient`, `HTTP`, `XMLParser`) — these assert the actual output, not identity, to document the behavior
- [ ] Run `dotnet test --filter "FullyQualifiedName~ReverseNameMangler"` — all pass

### 4b. Commit

```
test: Add round-trip fidelity tests for name mangling

Verify that snake_case → PascalCase → snake_case is an identity
transform. Document known non-round-trip cases for camelCase and
all-caps inputs.
```

---

## Verification Checklist

After all phases are complete:

- [ ] `dotnet build sharpy.sln` — no warnings
- [ ] `dotnet test` — all tests pass
- [ ] `dotnet format whitespace` — no formatting issues
- [ ] No changes to `.expected` files in existing test fixtures (these phases add new behavior and new tests, they don't change existing behavior)

---

## What's NOT in scope

| Item | Why deferred |
|------|-------------|
| **Decision 5: Builtin shadowing warnings** | Requires changes to import resolution and `BuiltinRegistry` interaction. More complex, separate PR. |
| **Decision 6: Documentation** | No compiler code. Can be written anytime. |
| **General .NET interop** (properties, indexers, operators, namespaces) | v0.2.x+ scope per design doc. |
| **`OverloadIndex` data model extension** (tracking properties, constants separately) | Only needed when general .NET interop lands. Phase 2 defines `ReverseNameContext` so the algorithm is ready; wiring it into the data model is a separate concern. |

---

## File Reference

| File | Phase | Action |
|------|-------|--------|
| `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs` | 1, 2 | **Create** |
| `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs` | 1, 2 | Edit |
| `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` | 3 | Edit |
| `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` | 3 | Edit — add `MemberNameCollision = "SPY0522"` |
| `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs` | 3 | Edit — add explanation for `SPY0522` |
| `src/Sharpy.Compiler.Tests/Discovery/ReverseNameManglerTests.cs` | 1, 2, 4 | **Create** |
| `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` | 3 | Edit (append — already exists with 17 tests) |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/name_collision/*.spy` | 3 | **Create** |

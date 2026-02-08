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

**Recommended approach:** Extract the regex logic into an `internal static` method (e.g., `ReverseNameMangler.ToSnakeCase(string name)`) in a new file `src/Sharpy.Compiler/Discovery/ReverseNameMangler.cs`, and call it from `GetFunctionName()`. This makes it directly testable and prepares for Phase 2 (symbol-aware mangling). Add `[InternalsVisibleTo]` if not already present.

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

> **Goal:** Detect when two Sharpy symbols in the same scope mangle to the same C# name and emit `SPY0520`.
>
> **Files touched:** ~4 (1 production `CodeGenInfoComputer`, 1 `DiagnosticBag` threading, 1–2 test files)

### 3a. Thread `DiagnosticBag` into `CodeGenInfoComputer`

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

The class currently has no way to emit diagnostics. Add a `DiagnosticBag` parameter to the constructor.

- [ ] Add `private readonly DiagnosticBag _diagnostics;` field
- [ ] Add `DiagnosticBag diagnostics` parameter to the constructor
- [ ] Update all call sites that construct `CodeGenInfoComputer` to pass a `DiagnosticBag`
  - Locate callers with `Grep` for `new CodeGenInfoComputer`

### 3b. Add collision detection after type member processing

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

After `ProcessTypeMembers(typeSymbol, body)` returns, collect all `CSharpName` values from the members just processed. If duplicates are found, emit `SPY0520` on the second declaration.

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
                DiagnosticCodes.CodeGen.NameCollision,
                $"Name collision: '{info.OriginalName}' and '{existingOriginal}' both compile to '{info.CSharpName}'. Rename one or use backtick escaping.",
                // Pass the TextSpan from the AST node for the second declaration
                span
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
- Symbols that resolve to different C# kinds and can coexist (method vs nested type) should be excluded. Group by kind category: {methods + fields} and {types} are separate collision domains.
- Dunder methods that map to special C# names (`__init__` → constructor, `__str__` → `ToString`) should be included — if a user also defines a method `to_string` (→ `ToString`), that's a real collision.

- [ ] Add `DetectCollisions(TypeSymbol, IEnumerable<Statement>)` method
- [ ] Call it from `ProcessClassDef`, `ProcessStructDef`, `ProcessInterfaceDef` after `ProcessTypeMembers`
- [ ] Group collision domains: methods+fields separate from nested types

### 3c. Add collision detection for module-level symbols

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`

At the end of `ComputeForModule()`, check for collisions among module-level functions + variables.

- [ ] Add `DetectModuleLevelCollisions()` method
- [ ] Call it at the end of `ComputeForModule()`
- [ ] Scope: functions + module-level variables (not types, which live in separate C# declarations)

### 3d. Add integration tests for collision detection

**File:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` — new `.spy` + `.error` pairs

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
SPY0520
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
SPY0520
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
SPY0520
```

**Test 4: no collision between method and type (different C# domains)**
```python
# no_collision_method_type.spy
class FooBar:
    pass

def foo_bar():
    print("ok")

foo_bar()
```
```
# no_collision_method_type.expected
ok
```

**Test 5: no collision between camelCase and snake_case (they produce different results)**
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
- [ ] Create `no_collision_method_type.spy` + `no_collision_method_type.expected`
- [ ] Create `no_collision_camel_snake.spy` + `no_collision_camel_snake.expected`
- [ ] Decide on subdirectory name (e.g., `TestFixtures/name_collision/`) or place in an existing relevant directory
- [ ] Run `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` — all pass

### 3e. Add unit tests for `CodeGenInfoComputer` collision detection

**File:** `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` (create if it doesn't exist)

Use `IntegrationTestBase.CompileAndExecute()` or build the AST manually to test:

- [ ] Test: two methods with same mangled name → `SPY0520` error
- [ ] Test: method + field with same mangled name → `SPY0520` error
- [ ] Test: method + nested type with same mangled name → no error (different domains)
- [ ] Test: no collision when names are different → no error
- [ ] Run `dotnet test --filter "FullyQualifiedName~CodeGenInfoComputer"` — all pass

### 3f. Commit

```
feat: Add forward name collision detection (SPY0520)

CodeGenInfoComputer now detects when two symbols in the same scope
produce the same C# name after mangling. Emits SPY0520 with both
original names and the colliding C# name.
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
| `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` | 3 | Verify `SPY0520` exists (it does) |
| `src/Sharpy.Compiler.Tests/Discovery/ReverseNameManglerTests.cs` | 1, 2, 4 | **Create** |
| `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` | 3 | **Create** (if needed) |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/name_collision/*.spy` | 3 | **Create** |

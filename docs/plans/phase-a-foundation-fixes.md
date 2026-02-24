<!-- Verified by /verify-plan on 2026-02-23 (rev 2) -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Phase A: Foundation Fixes — Implementation Plan

**Goal:** Complete all partially-implemented language features and correct documentation inaccuracies so that Phases 8–12 can proceed without backtracking or architectural debt.

**Branch:** `phase-a-foundation-fixes` (from `dev`)

**Commit strategy:** One commit per numbered task below; each commit must leave `dotnet build` and `dotnet test` green.

---

## Context & Rationale

A spec-completeness audit of the Sharpy compiler found three categories of issues:

1. **Features documented as broken that actually work** — nested comprehensions, tuple unpacking in comprehensions. Dead diagnostic codes (SPY0515/0516/0517) create a false impression these are blocked.
2. **Genuinely partially implemented features** — f-string format spec translator handles only 6 patterns (silent passthrough for unsupported specs), and complex function call expressions (calling return values, indexed callables, inline lambdas) hit an `EmitNotImplementedExpression` wall.
3. **Documentation inaccuracies** — the spec, roadmap, and audit doc all claim features are broken when they work.

Fixing these now prevents:
- **Phase 8** (match/tagged unions) from needing complex call expression support mid-implementation
- **Phase 10** (async) from hitting `await factory()()` limitations
- **Phase 11** (partial application) from needing `f(5, _)(rest)` support retrofitted
- Silent f-string correctness bugs from confusing users who assume Python format specs work

---

## Task 1: Remove dead diagnostic codes SPY0515, SPY0516, SPY0517

**Commit message:** `fix: remove dead diagnostic codes SPY0515/0516/0517`

### Rationale

These three codes create a false impression that nested comprehensions and tuple unpacking in comprehensions are blocked. In reality:
- **SPY0515** (`NestedComprehension`): The guard code exists at `RoslynEmitter.Expressions.Literals.cs:625-629` inside `GenerateComprehensionChain()`, but it's dead — multi-for comprehensions route to `GenerateImperativeComprehension()` at line 193, and nested comprehensions (element expression is itself a comprehension) work via recursive `GenerateExpression()` at line 204. Three test fixtures prove this works.
- **SPY0516** (`TupleUnpackingComprehension`): Defined but never referenced anywhere in CodeGen. `tuple_unpacking_comprehension_0001.spy` passes.
- **SPY0517** (`ComplexTupleUnpacking`): Defined but never referenced anywhere in CodeGen. Deep tuple unpacking works (`tuple_unpack_deep.spy`, `tuple_unpack_nested.spy`).

### Files to modify

1. **`src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`** (lines 248-250):
   - Delete the three constant declarations:
     ```csharp
     public const string NestedComprehension = "SPY0515";
     public const string TupleUnpackingComprehension = "SPY0516";
     public const string ComplexTupleUnpacking = "SPY0517";
     ```

2. **`src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs`** (lines 1034-1050):
   - Delete the three `Add()` calls for `NestedComprehension`, `TupleUnpackingComprehension`, and `ComplexTupleUnpacking`. These explanation entries reference the deleted constants and will cause compilation failure if left in place.

3. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Literals.cs`** (lines 625-629):
   - Delete the dead `case ForClause:` guard inside `GenerateComprehensionChain()`:
     ```csharp
     case ForClause:
         var forError = EmitNotImplementedExpression(
             "Nested comprehensions (multiple 'for' clauses) are not yet supported. Use a for loop instead.",
             DiagnosticCodes.CodeGen.NestedComprehension, lineStart, columnStart);
         return (null!, null!, null, forError);
     ```
   - This leaves only `case IfClause:` in the switch, which is the correct behavior — any additional `ForClause` in a single-for path would be a parser bug, not a user error.

### Verification

- `dotnet build sharpy.sln` — must compile without errors (no remaining references to deleted constants)
- `dotnet test` — all tests pass (no test references these codes)
- Grep for `SPY0515`, `SPY0516`, `SPY0517` in `src/` — zero hits in C# code
- Grep for `NestedComprehension`, `TupleUnpackingComprehension`, `ComplexTupleUnpacking` in `src/` — zero hits

---

## Task 2: Update comprehensions spec and roadmap documentation

**Commit message:** `docs: update comprehensions spec and roadmap for working features`

### Rationale

The spec (`comprehensions.md`), roadmap (`phases2.md`), and audit doc (`audit-2026-02-23.md`) all incorrectly claim nested comprehensions are blocked by SPY0515. With the dead code removed in Task 1, the documentation must reflect reality.

### Files to modify

1. **`docs/language_specification/comprehensions.md`** (lines 20-32):
   - Replace the "NOT YET SUPPORTED" block with a working example:
     ```python
     # Nested comprehension (comprehension inside comprehension)
     matrix = [[i * j for j in range(3)] for i in range(3)]
     # [[0, 0, 0], [0, 1, 2], [0, 2, 4]]
     ```
   - Remove the ❌ marker, the SPY0515 error reference, and the workaround code block.
   - Update the `> **Note:**` block at lines 29-32 to remove the claim about unsupported nested comprehensions and the "Phase 12 (v0.2.6)" reference. This note currently says nested comprehensions are unsupported; change it to just note that multiple `for` clauses and nested comprehensions are both supported.

2. **`docs/implementation_planning/phases2.md`**:
   - **Line 161**: Delete item 12.4 row entirely (`| 12.4 | Nested comprehensions | S | ... |`). Re-number remaining items if needed (12.5 → 12.4, 12.6 → 12.5).
   - **Line 163**: Update count in `12.6` (now `12.5`) item description if it references "108 spec files" to "112 spec files".
   - **Line 181**: Update Phase 12 summary from "6" items to "5" items.
   - **Line 183**: Update total from "25" to "24" remaining items.
   - **Line 273**: Remove `SPY0515, SPY0516, SPY0517` from the diagnostic codes verification row, or mark them as removed.
   - **Line 304**: Update Phase 12 audit to remove "SPY0515 still blocks nested comprehensions".
   - **Line 313**: Update the comprehensions.md accuracy issue to reflect that the spec now shows the feature as working (change "FIXED — added ❌ comment" to "FIXED — removed ❌, feature is implemented").

3. **`docs/audits/audit-2026-02-23.md`**:
   - **Line 374**: Remove the `12.4 Nested comprehensions` row from the "Doable Now" table.
   - **Line 405**: Remove the `P2 | 12.4 Nested comprehensions` row from "Recommended Action Items".

### Verification

- Grep for `SPY0515` in `docs/` — should only appear in historical context (changelog-style) if at all, not as a current limitation
- Grep for "NOT YET SUPPORTED" in `comprehensions.md` — zero hits
- Read the updated spec and verify it matches the test fixture behavior

---

## Task 3: Extend f-string format specifier translator

**Commit message:** `feat: complete f-string format spec translation (alignment, hex, binary, integer)`

### Rationale

`TranslatePythonFormatSpec()` in `RoslynEmitter.Expressions.Literals.cs:750-813` handles only 6 Python format patterns. Unrecognized specs fall through to C# passthrough, which silently produces wrong output. For example:
- `f"{x:>10}"` passes `>10` to C# which doesn't understand Python alignment syntax
- `f"{x:08b}"` passes `08b` through — C# doesn't have `b` format
- `f"{x:d}"` passes `d` through — works by coincidence (C# has `D`) but only for integers

This is a silent correctness bug. Users expect Python format specs to work; the fix is to extend the translator.

### Python-to-C# format spec mapping

Reference: Python [Format Specification Mini-Language](https://docs.python.org/3/library/string.html#format-specification-mini-language)

```
format_spec ::= [[fill]align][sign][z][#][0][width][grouping_option][.precision][type]
```

#### Type specifiers to add:

| Python | Meaning | C# Equivalent | Notes |
|--------|---------|---------------|-------|
| `d` | Integer decimal | `D` | Direct mapping; C# `D` is the same |
| `b` | Binary | Custom | No C# equivalent; use `Convert.ToString(value, 2)` — but this requires expression rewriting, not just format string change. **See note below.** |
| `o` | Octal | Custom | No C# equivalent; same situation as binary. |
| `x` | Hex lowercase | `x` | C# lowercase hex — direct passthrough works! |
| `X` | Hex uppercase | `X` | C# uppercase hex — direct passthrough works! |
| `n` | Locale-aware number | `N` | C# `N` is locale-aware number |
| `s` | String | (none) | Python `s` is a no-op for strings; translate to empty string |

**Note on `b` and `o`:** Binary and octal format specs cannot be handled by a simple format string translation because C# `$"{value:...}"` has no binary/octal format specifier. These require **expression rewriting** — the interpolation expression must be replaced with `Convert.ToString(value, 2)` or `Convert.ToString(value, 8)`. This follows the same pattern already used for the percent format special-case at lines 663-692 in `GenerateFString()`, which rewrites `{value:PN}` to `{(value * 100):FN}%`.

#### Alignment/fill to add:

| Python | Meaning | C# Equivalent |
|--------|---------|---------------|
| `>N` | Right-align in N chars | `,N` (C# alignment component, positive = right-align) |
| `<N` | Left-align in N chars | `,-N` (C# alignment component, negative = left-align) |
| `^N` | Center-align in N chars | Expression rewriting: `.PadLeft((N+len)/2).PadRight(N)` |
| `fill>N` | Right-align with fill char | Expression rewriting: `.PadLeft(N, fill)` |
| `fill<N` | Left-align with fill char | Expression rewriting: `.PadRight(N, fill)` |
| `fill^N` | Center-align with fill char | Expression rewriting: `.PadLeft((N+len)/2, fill).PadRight(N, fill)` |

**Implementation strategy for alignment:**
- **Simple `>N` / `<N` (space fill):** Use C# interpolation alignment clause `{expr,N:format}` / `{expr,-N:format}`. This is the fast path.
- **Custom fill characters and `^` (center):** Require expression rewriting. The interpolation expression must be replaced with a method-call chain. The pattern follows the existing percent format special-case (lines 663-692 in `GenerateFString()`):
  1. Generate the formatted value as a string: `expr.ToString("format")` (or just `expr.ToString()` if no type specifier)
  2. Apply padding: `.PadLeft(N, fillChar)` for right-align, `.PadRight(N, fillChar)` for left-align
  3. For center: `.PadLeft((N + str.Length) / 2, fillChar).PadRight(N, fillChar)` — but since we need the string length, use a helper or compute in two steps
- **Center-align helper:** Add a private static method `CenterAlign(string s, int width, char fill = ' ')` that can be called from the expression rewrite. Alternatively, emit the PadLeft/PadRight chain inline since the value is already converted to string.

**Center-align C# expression pattern:**
```csharp
// For f"{x:^10}" where x = "hi":
// Python result: "    hi    "
// C# equivalent: "hi".PadLeft((10 + "hi".Length) / 2).PadRight(10)
// But we need to avoid evaluating expr twice, so:
// var __t = expr.ToString(); __t.PadLeft((10 + __t.Length) / 2).PadRight(10)
// In an interpolation context, we can use a helper method instead.
```

**Decision:** For center-align and custom fill chars, generate a call to a new `Sharpy.Builtins.FormatAlign(string value, int width, char fill, string alignment)` helper method in Sharpy.Core. This avoids complex inline expression rewriting and keeps the generated C# clean. The helper handles all three alignment modes (`<`, `>`, `^`) with any fill character.

#### Combined specifiers:

| Python | Meaning | C# Equivalent |
|--------|---------|---------------|
| `>10.2f` | Right-align, 10 wide, 2 decimal fixed | `10:F2` (alignment component + format) |
| `010.2f` | Zero-pad, 10 wide, 2 decimal fixed | `010.2` is not directly expressible; use workaround |
| `,.2f` | Thousands + 2 decimal fixed | `N2` (C# `N` format includes thousands separator) |

### Implementation approach

The `TranslatePythonFormatSpec` method needs to be rewritten from a series of independent if-checks into a **proper parser** of the Python format spec mini-language. The current approach of checking `.StartsWith(".")` and `.EndsWith("f")` cannot handle combined specs like `>10.2f`.

**New implementation structure:**

```
1. Parse optional fill character (any char) + alignment (<, >, ^, =)
2. Parse optional sign (+, -, space)
3. Parse optional # (alternate form)
4. Parse optional 0 (zero-pad flag)
5. Parse optional width (integer)
6. Parse optional grouping (, or _)
7. Parse optional .precision (integer)
8. Parse optional type (b, c, d, e, E, f, F, g, G, n, o, s, x, X, %)
```

For the interpolation, C# supports `{expr,alignment:format}` where:
- `alignment` is an integer (positive = right-align, negative = left-align)
- `format` is the format string

So the method should return a **struct or tuple** `(int? alignment, string formatSpec)` instead of just a string, and the caller (`GenerateFString`) should apply alignment via `WithAlignmentClause()` on the `Interpolation` node.

**Alternatively**, to minimize the blast radius, keep the method returning a string but handle alignment separately by detecting and stripping it before calling the existing logic. Then apply alignment in the caller.

### Specific changes

1. **`RoslynEmitter.Expressions.Literals.cs`** — Replace `TranslatePythonFormatSpec()` (lines 750-813):

   **New method signature** (keep private static, but return a result struct):
   ```csharp
   private readonly record struct FormatSpecResult(
       string FormatString,          // C# format string (e.g., "F2", "X", "D5")
       int? Alignment,               // C# alignment component (positive=right, negative=left), null if none or needs expression rewrite
       bool NeedsExpressionRewrite,  // True if the format requires expression rewriting (b, o, ^, custom fill)
       char? FillChar,               // Fill character for alignment (null = space default)
       char? AlignmentMode,          // '<', '>', '^' — needed for expression rewrite path
       int? Width,                   // Width for expression rewrite alignment
       int? Base                     // 2 for binary, 8 for octal, null otherwise
   );

   private static FormatSpecResult TranslatePythonFormatSpec(string pythonSpec)
   ```

   **New parsing logic (proper format spec mini-language parser):**
   - Parse the full format spec left-to-right:
     1. Detect optional fill char + alignment (`<`, `>`, `^`). If second char is `<`/`>`/`^`, first char is fill. If first char is `<`/`>`/`^`, no fill char.
     2. Skip optional sign (`+`, `-`, ` `) — pass through to C#.
     3. Skip optional `#` (alternate form) — not applicable in C#.
     4. Detect optional `0` (zero-pad flag).
     5. Parse optional width (digits).
     6. Detect optional grouping (`,` or `_`).
     7. Parse optional `.precision` (digits).
     8. Parse optional type char (`b`, `c`, `d`, `e`, `E`, `f`, `F`, `g`, `G`, `n`, `o`, `s`, `x`, `X`, `%`).
   - **Compose result based on parsed components:**
     - If type is `b`: set `NeedsExpressionRewrite = true`, `Base = 2`
     - If type is `o`: set `NeedsExpressionRewrite = true`, `Base = 8`
     - If fill char is non-space or alignment is `^`: set `NeedsExpressionRewrite = true`, store fill/align/width
     - If simple `>` / `<` with space fill: set `Alignment` (positive for `>`, negative for `<`)
     - Build C# format string from remaining components (precision + type mapping)

2. **`RoslynEmitter.Expressions.Literals.cs`** — Update the caller in `GenerateFString()` (around line 693-734):

   The format spec handling in `GenerateFString()` currently has two branches: the percent special-case (lines 663-692) and the general case (lines 693+). Restructure to add two more special-case branches **before** the general case, following the same pattern as percent:

   **Branch order in the `else if (part.Expression != null)` block:**
   ```
   a. Percent format (.N%) — existing special case (lines 663-692)
   b. Binary format (b) — NEW: expression rewrite to Convert.ToString(value, 2)
   c. Octal format (o) — NEW: expression rewrite to Convert.ToString(value, 8)
   d. Alignment with fill/center — NEW: expression rewrite to FormatAlign()
   e. General case — existing (lines 693+), now uses FormatSpecResult for alignment
   ```

   **Binary/octal expression rewrite (new branches b, c):**
   ```csharp
   // Binary: f"{x:b}" → $"{Convert.ToString(x, 2)}"
   // Binary with width: f"{x:08b}" → $"{Convert.ToString(x, 2).PadLeft(8, '0')}"
   else if (IsBinaryOrOctalFormat(part.FormatSpec, out var numBase, out var padWidth, out var padChar))
   {
       var innerExpr = GenerateExpression(part.Expression);
       // Convert.ToString(value, base)
       var convertCall = InvocationExpression(
           MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
               IdentifierName("Convert"), IdentifierName("ToString")))
           .WithArgumentList(ArgumentList(SeparatedList(new[] {
               Argument(innerExpr),
               Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numBase)))
           })));
       ExpressionSyntax formatted = convertCall;
       if (padWidth > 0)
           formatted = InvocationExpression(
               MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                   formatted, IdentifierName("PadLeft")))
               .WithArgumentList(ArgumentList(SeparatedList(new[] {
                   Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(padWidth))),
                   Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(padChar)))
               })));
       parts.Add(Interpolation(ParenthesizedExpression(formatted)));
   }
   ```

   **Alignment with fill/center expression rewrite (new branch d):**
   ```csharp
   // f"{x:*^10}" → $"{Sharpy.Builtins.FormatAlign(x.ToString(), 10, '*', '^')}"
   // f"{x:*>10.2f}" → $"{Sharpy.Builtins.FormatAlign(x.ToString("F2"), 10, '*', '>')}"
   else if (needsAlignRewrite)  // center-align or custom fill char
   {
       var innerExpr = GenerateExpression(part.Expression);
       // If there's a type format, apply it via ToString(format)
       if (!string.IsNullOrEmpty(result.FormatString))
           innerExpr = InvocationExpression(
               MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                   innerExpr, IdentifierName("ToString")))
               .WithArgumentList(ArgumentList(SingletonSeparatedList(
                   Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                       Literal(result.FormatString))))));
       else
           innerExpr = InvocationExpression(
               MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                   innerExpr, IdentifierName("ToString")))
               .WithArgumentList(ArgumentList());
       // Sharpy.Builtins.FormatAlign(formatted, width, fill, alignment)
       var alignCall = InvocationExpression(
           MakeGlobalQualifiedName("Sharpy", "Builtins", "FormatAlign"))
           .WithArgumentList(ArgumentList(SeparatedList(new[] {
               Argument(innerExpr),
               Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(result.Width!.Value))),
               Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(result.FillChar ?? ' '))),
               Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(result.AlignmentMode!.Value)))
           })));
       parts.Add(Interpolation(ParenthesizedExpression(alignCall)));
   }
   ```

   **General case update (existing branch e):**
   ```csharp
   else  // general case
   {
       var result = TranslatePythonFormatSpec(part.FormatSpec);
       var interpolation = Interpolation(ParenthesizedExpression(innerExpr));
       if (!string.IsNullOrEmpty(result.FormatString))
           interpolation = interpolation.WithFormatClause(...result.FormatString...);
       if (result.Alignment.HasValue)
           interpolation = interpolation.WithAlignmentClause(
               InterpolationAlignmentClause(
                   Token(SyntaxKind.CommaToken),
                   LiteralExpression(SyntaxKind.NumericLiteralExpression,
                       Literal(result.Alignment.Value))));
       parts.Add(interpolation);
   }
   ```

3. **`src/Sharpy.Core/Builtins/FormatAlign.cs`** — NEW file. Add a `FormatAlign` method to the `Builtins` partial class:
   ```csharp
   namespace Sharpy;

   public static partial class Builtins
   {
       /// <summary>
       /// Aligns a string within a field of given width using the specified fill character and alignment mode.
       /// Used by f-string format spec codegen for custom fill characters and center-alignment.
       /// </summary>
       public static string FormatAlign(string value, int width, char fill, char alignment)
       {
           if (value.Length >= width) return value;
           return alignment switch
           {
               '<' => value.PadRight(width, fill),
               '>' => value.PadLeft(width, fill),
               '^' => CenterAlign(value, width, fill),
               _ => value
           };
       }

       private static string CenterAlign(string value, int width, char fill)
       {
           int totalPadding = width - value.Length;
           int leftPadding = totalPadding / 2;
           // Python puts extra padding on the right when total padding is odd
           return value.PadLeft(value.Length + leftPadding, fill).PadRight(width, fill);
       }
   }
   ```
   **Note:** This file uses C# 9.0 features only (switch expression is C# 8.0+, so fine for `netstandard2.1`).

4. **Add test fixtures** in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/expressions/`:
   - `fstring_format_alignment_0001.spy` + `.expected` — right-align `f"{x:>10}"` → `"        42"`
   - `fstring_format_alignment_0002.spy` + `.expected` — left-align `f"{x:<10}"` → `"42        "`
   - `fstring_format_alignment_0003.spy` + `.expected` — center-align `f"{x:^10}"` → `"    42    "`
   - `fstring_format_fill_0001.spy` + `.expected` — custom fill `f"{x:*>10}"` → `"********42"`
   - `fstring_format_fill_0002.spy` + `.expected` — custom fill center `f"{x:*^10}"` → `"****42****"`
   - `fstring_format_integer_0001.spy` + `.expected` — integer format `f"{x:d}"` → `"42"`
   - `fstring_format_hex_0001.spy` + `.expected` — hex `f"{x:x}"` → `"2a"`, `f"{x:X}"` → `"2A"`
   - `fstring_format_binary_0001.spy` + `.expected` — binary `f"{x:b}"` → `"101010"`
   - `fstring_format_binary_0002.spy` + `.expected` — zero-padded binary `f"{x:08b}"` → `"00101010"`
   - `fstring_format_octal_0001.spy` + `.expected` — octal `f"{x:o}"` → `"52"`
   - `fstring_format_combined_0001.spy` + `.expected` — combined `f"{x:>10.2f}"` → `"      3.14"`, `f"{x:,.2f}"` → `"1,234.50"`

### Python behavior to verify

Before implementing, verify Python behavior for each format spec:

```bash
python3 -c "x=42; print(f'{x:>10}')"       # '        42'
python3 -c "x=42; print(f'{x:<10}')"       # '42        '
python3 -c "x=42; print(f'{x:^10}')"       # '    42    '
python3 -c "x=42; print(f'{x:*>10}')"      # '********42'
python3 -c "x=42; print(f'{x:*^10}')"      # '****42****'
python3 -c "x=42; print(f'{x:d}')"         # '42'
python3 -c "x=42; print(f'{x:x}')"         # '2a'
python3 -c "x=42; print(f'{x:X}')"         # '2A'
python3 -c "x=42; print(f'{x:b}')"         # '101010'
python3 -c "x=42; print(f'{x:08b}')"       # '00101010'
python3 -c "x=42; print(f'{x:o}')"         # '52'
python3 -c "x=3.14; print(f'{x:>10.2f}')"  # '      3.14'
python3 -c "x=1234.5; print(f'{x:,.2f}')"  # '1,234.50'
```

### Verification

- `dotnet build sharpy.sln` — compiles
- `dotnet test` — all existing tests pass + new test fixtures pass
- `dotnet test --filter "DisplayName~fstring_format"` — new tests specifically

---

## Task 4: Support complex function call expressions in codegen

**Commit message:** `feat: support complex function call expressions (indirect calls, IIFE)`

### Rationale

`GenerateCall()` in `RoslynEmitter.Expressions.Access.cs:18-324` dispatches on `call.Function` being one of three types:
- `IndexAccess` → generic instantiation (lines 22-64)
- `Identifier` → simple function/builtin/type call (lines 66-179)
- `MemberAccess` → method call (lines 182-319)

Everything else falls through to `EmitNotImplementedExpression` at line 321-323. This means the following valid Python patterns fail at codegen:

```python
get_handler()("arg")       # FunctionCall(Function: FunctionCall(...))
callbacks[0]("arg")        # FunctionCall(Function: IndexAccess(...))  [non-generic]
(lambda: 42)()             # FunctionCall(Function: LambdaExpression(...))
```

### How C# handles these

In C#, you can invoke any expression that evaluates to a delegate/`Func`/`Action`:

```csharp
GetHandler()("arg");       // Invoke return value — works directly
callbacks[0]("arg");       // Invoke indexed value — works directly
((Func<int>)(() => 42))(); // Invoke lambda — needs explicit delegate type
```

The key insight: **C# already supports invoking arbitrary expressions** via `InvocationExpression(expr)`. The Roslyn `SyntaxFactory.InvocationExpression` takes any `ExpressionSyntax` as its target. We just need to generate it.

### Implementation approach

Add a **fallback path** in `GenerateCall()` after the `MemberAccess` branch. Instead of `EmitNotImplementedExpression`, generate `InvocationExpression(GenerateExpression(call.Function))`:

```csharp
// Fallback: arbitrary expression as call target
// Handles: get_handler()("arg"), callbacks[0]("arg"), (lambda x: x)(42), etc.
var callTarget = GenerateExpression(call.Function);
var positionalArgs = GeneratePositionalArguments(call.Arguments);
var keywordArgs = call.KeywordArguments.Select(kwarg =>
    Argument(GenerateExpression(kwarg.Value))
        .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

return InvocationExpression(callTarget)
    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
```

This works because:
1. `GenerateExpression(call.Function)` recursively generates the inner expression (another call, an index, a lambda, etc.)
2. C#'s `InvocationExpression` accepts any expression as target
3. Roslyn will compile this if the expression type is a delegate/Func/Action

### Semantic analysis considerations

The type checker must recognize that calling a `FunctionType`-typed expression is valid. Check `TypeChecker.Expressions.cs` to verify:
- `FunctionCall` where `Function` is not `Identifier`/`MemberAccess` still gets type-checked
- The return type of the call is correctly inferred from the `FunctionType`'s `ReturnType`

If the type checker currently rejects these patterns, it must be updated first. If it already handles them (by inferring `FunctionType` on the call target), only codegen needs the fix.

### Specific changes

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs`** (lines 321-323):
   - Replace:
     ```csharp
     return EmitNotImplementedExpression(
         "Unsupported expression type in code generation: complex function expressions are not yet supported",
         DiagnosticCodes.CodeGen.UnsupportedExpressionType, call.LineStart, call.ColumnStart);
     ```
   - With the fallback invocation code shown above.

2. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs`** — Handle the `IndexAccess` case more carefully:
   - Currently, `IndexAccess` at line 22 only handles the generic instantiation case where `indexAccess.Object is Identifier`. Non-generic indexed access like `callbacks[0]("arg")` falls through. The new fallback at the end will catch this, but verify it works correctly.

3. **Verify type checker** — Check `TypeChecker.Expressions.cs` for `FunctionCall` handling:
   - If the type checker already infers types for arbitrary call targets, no semantic changes needed.
   - If it rejects non-Identifier/MemberAccess call targets, add handling for `FunctionType`-typed expressions.

4. **Add test fixtures** in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/expressions/`:
   - `indirect_call_0001.spy` — calling a function return value:
     ```python
     def get_greeter() -> Callable[[str], str]:
         def greet(name: str) -> str:
             return f"Hello, {name}"
         return greet

     def main():
         result = get_greeter()("World")
         print(result)  # Hello, World
     ```
   - `indirect_call_0002.spy` — calling a lambda immediately (IIFE):
     ```python
     def main():
         result = (lambda x: x * 2)(21)
         print(result)  # 42
     ```
   - `indirect_call_0003.spy` — calling an indexed callable:
     ```python
     def double(x: int) -> int:
         return x * 2

     def triple(x: int) -> int:
         return x * 3

     def main():
         funcs: list[Callable[[int], int]] = [double, triple]
         print(funcs[0](5))   # 10
         print(funcs[1](5))   # 15
     ```
   - `indirect_call_0004.spy` — chained calls:
     ```python
     def make_adder(n: int) -> Callable[[int], int]:
         def add(x: int) -> int:
             return x + n
         return add

     def main():
         print(make_adder(10)(5))  # 15
     ```

### Python behavior to verify

```bash
python3 -c "
def get_greeter():
    def greet(name): return f'Hello, {name}'
    return greet
print(get_greeter()('World'))
"
# Hello, World

python3 -c "print((lambda x: x * 2)(21))"
# 42
```

### Verification

- `dotnet build sharpy.sln` — compiles
- `dotnet test` — all existing tests pass + new test fixtures pass
- `dotnet test --filter "DisplayName~indirect_call"` — new tests specifically
- Verify no regressions in existing function call tests

---

## ~~Task 5: Convert super().__init__() outside __init__ to compile-time error~~

**[CORRECTED: Task 5 is already implemented and should be removed from this plan.]**

~~**Commit message:** `fix: reject super().__init__() outside __init__ as compile-time error`~~

### Status: ALREADY IMPLEMENTED

Verification found that `ValidateSuperContextRules()` in `TypeChecker.Utilities.cs` (lines 661-736) already handles all super() context validation:

- **Case 1 (lines 672-696):** Inside `__init__` — validates super() only calls `__init__`, not inside control flow, not called multiple times
- **Case 2 (lines 699-715):** Inside `@override` method — allows same-name or cross-dunder calls
- **Case 3 (lines 717-729):** Inside dunder method (not `__init__`, not `@override`) — allows any dunder via super()
- **Case 4 (lines 731-735):** Regular method — emits `SPY0287` (`InvalidSuperUsage`): "super() cannot be used in regular methods; only in __init__, @override, or dunder methods"

The codegen guard at `RoslynEmitter.Expressions.Access.cs:214-219` uses `DiagnosticCodes.CodeGen.UnsupportedFeature` (SPY0501, not SPY0351 as previously claimed) and serves as a safety net that should never be reached.

**No changes needed. Skip this task.**

---

## Task 6: Final verification and snapshot regeneration

**Commit message:** `test: regenerate snapshots and verify all tests green`

### Rationale

After all the changes above, C# snapshot files (`.expected.cs`) may need regeneration because codegen output may have changed. Also, a full test run ensures no regressions.

### Steps

1. Run `dotnet test` — if any snapshot tests fail due to expected codegen changes (not bugs):
   - Run `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` to regenerate
   - Review the diffs to ensure they're expected
2. Run `dotnet format whitespace` — formatting compliance
3. Final `dotnet test` — all green

### Verification

- `dotnet test` — 0 failures
- `dotnet format whitespace --verify-no-changes` — no formatting issues

---

## Execution Order & Dependencies

```
Task 1 (dead diagnostics) ─→ Task 2 (docs update) ─→ Task 6 (final verification)
                                                    ↗
Task 3 (f-string formats) ──────────────────────────
                                                    ↗
Task 4 (complex calls) ────────────────────────────
```

- Tasks 1→2 must be sequential (Task 2 references removed codes)
- Tasks 3 and 4 are independent of each other and of Tasks 1-2
- ~~Task 5 removed — already implemented~~
- Task 6 must be last (final verification after all changes)

## Files Modified Summary

| Task | Files Modified |
|------|---------------|
| 1 | `DiagnosticCodes.cs`, `DiagnosticExplanations.cs`, `RoslynEmitter.Expressions.Literals.cs` |
| 2 | `comprehensions.md`, `phases2.md`, `audit-2026-02-23.md` |
| 3 | `RoslynEmitter.Expressions.Literals.cs`, new `Sharpy.Core/Builtins/FormatAlign.cs`, new test fixtures (11 files) |
| 4 | `RoslynEmitter.Expressions.Access.cs`, possibly `TypeChecker.Expressions.cs`, new test fixtures (4 files) |
| ~~5~~ | ~~REMOVED — already implemented~~ |
| 6 | Snapshot `.expected.cs` files (if needed) |

## Scope Exclusions

The following items from the audit are **intentionally excluded** from this plan:

- **2.4 `**kwargs` spread (SPY0123)**: Correctly tracked for Phase 11. No silent bugs; explicit parser error.
- **2.5 Or-pattern in match**: Phase 8 scope. Clean not-implemented boundary.
- **2.6 IEquatable CLR surface**: Low risk, correctness unaffected.
- **2.8 Generator expressions**: Not in spec, not a regression.

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-02-23 (rev 2)
**Plan file:** `docs/plans/phase-a-foundation-fixes.md`

### Corrections Made

**Rev 1 corrections (retained):**
- **Task 1**: Added missing file `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs` (lines 1034-1050) which contains `Add()` calls for all three dead diagnostic codes. Without removing these entries, `dotnet build` would fail after deleting the constants from `DiagnosticCodes.cs`. Added corresponding grep verification step.

**Rev 2 corrections:**
- **Task 5: REMOVED — already implemented.** `ValidateSuperContextRules()` in `TypeChecker.Utilities.cs` (lines 661-736) already validates super() context with 4 cases. Regular methods get `SPY0287` (InvalidSuperUsage). The codegen guard at `RoslynEmitter.Expressions.Access.cs:214-219` uses `SPY0501` (UnsupportedFeature), not SPY0351 as previously claimed. SPY0351 is `ConflictingConstructorInitializers` — unrelated. Task 5 was struck through and marked as skip.

### Warnings
- **Task 3**: The percent format special-case comment block starts at line 660, not 663 as stated. The condition itself starts at line 663, so the implementation range is correct but the description is slightly imprecise.

### Verified Claims (all tasks)

| Task | Claim | Status |
|------|-------|--------|
| 1 | DiagnosticCodes.cs lines 248-250 (3 constants) | Verified exactly |
| 1 | DiagnosticExplanations.cs lines 1034-1050 (3 Add() calls) | Verified exactly |
| 1 | RoslynEmitter.Expressions.Literals.cs lines 625-629 (dead ForClause guard) | Verified exactly |
| 1 | Multi-for routing to GenerateImperativeComprehension() at line 193 | Verified exactly |
| 1 | Recursive GenerateExpression() at line 204 | Verified exactly |
| 1 | Test fixtures tuple_unpacking_comprehension_0001, tuple_unpack_deep, tuple_unpack_nested | All exist with .expected files |
| 1 | No other references to SPY0515/0516/0517 in src/ | Verified — only 3 locations |
| 2 | comprehensions.md lines 20-32 (NOT YET SUPPORTED block) | Verified exactly |
| 2 | phases2.md line 161 (12.4 Nested comprehensions row) | Verified exactly |
| 2 | phases2.md line 163 (12.6 item) | Verified exactly |
| 2 | phases2.md line 181 (Phase 12 = 6 items) | Verified exactly |
| 2 | phases2.md line 183 (Total = 25 items) | Verified exactly |
| 2 | phases2.md line 273 (diagnostic codes verification row) | Verified exactly |
| 2 | phases2.md line 304 (Phase 12 audit) | Verified exactly |
| 2 | phases2.md line 313 (comprehensions.md accuracy) | Verified exactly |
| 2 | audit-2026-02-23.md line 374 (Doable Now table) | Verified exactly |
| 2 | audit-2026-02-23.md line 405 (Recommended Action Items) | Verified exactly |
| 3 | TranslatePythonFormatSpec() at lines 750-813 | Verified exactly |
| 3 | Handles 6 patterns | Verified exactly |
| 3 | GenerateFString() percent special-case at lines 663-692 | Verified (condition at 663, comment at 660) |
| 3 | Format spec handling at lines 693-734 | Verified exactly |
| 3 | Builtins/ directory exists | Verified (contains Builtins.cs, Exceptions.cs) |
| 3 | Sharpy.Core LangVersion 9.0, netstandard2.1;netstandard2.0 | Verified exactly |
| 4 | GenerateCall() at lines 18-324 | Verified exactly |
| 4 | Dispatch: IndexAccess → Identifier → MemberAccess | Verified exactly |
| 4 | EmitNotImplementedExpression fallback at lines 321-323 | Verified exactly |
| 4 | Error message text matches | Verified exactly |
| 4 | IndexAccess only handles `indexAccess.Object is Identifier` at line 22-23 | Verified exactly |
| 4 | TypeChecker.CheckFunctionCall calls CheckExpression(call.Function) at line 438 | Verified exactly |
| 4 | DiagnosticCodes.CodeGen.UnsupportedExpressionType = SPY0518 | Verified exactly |
| 4 | TestFixtures/expressions/ directory exists | Verified (71 files) |
| 5 | EmitNotImplementedExpression at line 216 for super().__init__() | Verified at lines 214-219 |
| 5 | SPY0351 used in codegen for this | **INCORRECT** — uses SPY0501 (UnsupportedFeature) |
| 5 | TypeChecker validation missing | **INCORRECT** — already implemented in ValidateSuperContextRules() |

### Unchecked Claims
- **Roslyn InterpolationAlignmentClause API**: Standard Roslyn API, not verified against specific NuGet version but is part of the stable API surface.
- **TypeChecker return type inference for chained calls**: `CheckFunctionCall` calls `CheckExpression(call.Function)` at line 438, so arbitrary call targets are type-checked. However, whether type inference correctly resolves the return type for chained calls (e.g., `get_handler()("arg")`) was not fully traced through the inference pipeline.
- **FormatAlign C# 9.0 compatibility**: Switch expression is C# 8.0+, compatible with LangVersion 9.0 and netstandard2.1. Not verified against netstandard2.0 target (may need if-else fallback).
- **Python center-align rounding**: Plan claims extra padding goes on the right for odd total padding. Should be verified with `python3 -c "print(f\"{'hi':^5}\")"` during implementation.
- **Convert.ToString base parameter**: Plan claims `Convert.ToString(int, int)` accepts base 2 and 8. Standard .NET API but edge cases (negative numbers, long values) should be verified during implementation.

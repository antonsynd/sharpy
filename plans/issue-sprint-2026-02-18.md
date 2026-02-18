# Sharpy Issue Sprint Plan — 2026-02-18

**Branch:** `fix/issue-sprint-feb18`
**Base:** `dev`
**Author:** Staff Compiler Engineer

---

## Executive Summary

19 open issues triaged. This plan covers the **12 most impactful issues** in dependency
order. The sprint focuses on critical multi-file compilation bugs first (#186, #190, #180),
then single-file correctness bugs (#187, #188, #183, #189), then quality-of-life
improvements (#191, #181, #182), and closes with already-resolved housekeeping (#174).

**Estimated scope:** ~800 lines of changes across 10 files + ~15 test fixtures.

---

## Priority & Dependency Order

```
Phase 1 — Cross-module bugs (blocks all multi-file programs)
  Task 1: #186  Cross-module void method calls → ICE
  Task 2: #190  Cross-module import resolution fails for class hierarchies
  Task 3: #189  Abstract class + interface stub generation gaps

Phase 2 — Single-file correctness bugs
  Task 4: #180  String indexing returns char instead of str
  Task 5: #187  Empty dict literal without annotation → ICE
  Task 6: #188  Hex integer literals in enum values → FormatException
  Task 7: #183  Float literals emit as integers

Phase 3 — Quality improvements
  Task 8: #191  Dogfood retry remediation hints
  Task 9: #181  Scientific notation float formatting tests
  Task 10: #182  Negative zero float formatting tests

Phase 4 — Housekeeping
  Task 11: #174  Close already-resolved issue (dead code was removed in 9a93adc8)
  Task 12: Close all completed issues via `gh issue close`
```

**Deferred to future sprints:** #171 (kwargs), #172 (decorator args), #173 (lambda
defaults), #175 (test density), #105 (async state machine), #108 (str.format parser),
#192 (dogfood timeout tracing), #193 (auto-generate negative fixtures).

---

## Task 1: Fix cross-module void method calls (#186)

**Priority:** P0 — most common ICE in dogfood, blocks all multi-file programs
**Risk:** Low — isolated 1-line fix with clear root cause
**Commit message:** `fix: Cross-module void methods return VoidType instead of UnknownType (#186)`

### Root Cause

`ModuleLoader.ExtractMethodSymbol()` calls `ConvertTypeAnnotationToSemanticType(method.ReturnType)`.
When `method.ReturnType == null` (no return annotation), this returns `SemanticType.Unknown`.
Python convention: unannotated functions return `None` (void). Same-file methods get correct
inference from the TypeChecker body traversal, but imported modules only go through
`ModuleLoader.ExtractMethodSymbol()` — no TypeChecker pass runs on them.

### Implementation Steps

1. **Read** `src/Sharpy.Compiler/Semantic/ModuleLoader.cs`, find `ExtractMethodSymbol()` method.
   Locate the line:
   ```csharp
   ReturnType = ConvertTypeAnnotationToSemanticType(method.ReturnType),
   ```
   (~line 474)

2. **Change** to handle null return type explicitly:
   ```csharp
   ReturnType = method.ReturnType != null
       ? ConvertTypeAnnotationToSemanticType(method.ReturnType)
       : SemanticType.Void,
   ```
   Do NOT change `ConvertTypeAnnotationToSemanticType` itself — that function is also
   used for parameter types, where `null` has different semantics.

3. **Add multi-file integration test** at
   `src/Sharpy.Compiler.Tests/Integration/TestFixtures/multifile/cross_module_void_method/`:
   - `lib.spy`:
     ```python
     class StringBuilder:
         def __init__(self):
             self.parts: list[str] = []

         def append(self, s: str):
             self.parts.append(s)

         def build(self) -> str:
             return ", ".join(self.parts)
     ```
   - `main.spy`:
     ```python
     from lib import StringBuilder

     def main():
         sb = StringBuilder()
         sb.append("hello")
         sb.append("world")
         print(sb.build())
     ```
   - `main.expected`:
     ```
     hello, world
     ```

4. **Run tests**: `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`
5. **Commit** with message above.

### Decision Guidelines
- Q: "Should I also handle methods with explicit `-> None` annotation?"
  A: These already work — `ConvertTypeAnnotationToSemanticType` converts "None" to `SemanticType.Void`.
- Q: "What about `__init__` which also has no return annotation?"
  A: Constructors are handled separately in `ExtractFullClassSymbol`, but the same fix
  would correctly give them VoidType if they flow through this path.

---

## Task 2: Fix cross-module import resolution for class hierarchies (#190)

**Priority:** P0 — blocks multi-file programs with inheritance
**Risk:** Medium — needs investigation to determine exact failure point
**Commit message:** `fix: Cross-module import resolves all class declarations including derived classes (#190)`

### Root Cause (investigation needed)

The issue description shows `from animals import Mammal` failing with SPY0301 even though
`Mammal` is defined. Four possible failure points identified:

1. `ExtractExportedSymbol()` might not process `ClassDef` when it has a base class from
   the same module (ordering issue)
2. `ModuleLoader.ExtractFullClassSymbol()` might fail during base-class resolution
3. `ImportResolver` symbol table conflict (stale symbol from previous pass)
4. `TypeResolver` creates `UserDefinedType { Symbol = null }` on lookup failure

### Investigation Steps

1. **Create reproduction test first** at
   `src/Sharpy.Compiler.Tests/Integration/TestFixtures/multifile/cross_module_inheritance/`:
   - `animals.spy` (as in issue)
   - `main.spy` importing all three classes
   - `main.expected` with expected output

2. **Run the test** and capture the exact error output.

3. **Add diagnostic logging** (temporary, for debugging only) in:
   - `ModuleLoader.ExtractExportedSymbol()` — log each symbol name as it's added
   - `ImportResolver.ResolveFromImport()` — log the ExportedSymbols keys available

4. **Identify the specific failure point** from logs.

5. **Apply the fix** based on findings:
   - If ordering: ensure all `ClassDef` statements are processed in a single pass
     regardless of base class presence
   - If base-class resolution: make `ExtractFullClassSymbol` tolerant of forward
     references within the same module
   - If symbol table conflict: add deduplication logic

6. **Remove diagnostic logging**, run all tests.
7. **Commit** with message above.

### Decision Guidelines
- Q: "The base class Animal is defined before Mammal — could this be a forward reference issue?"
  A: No. Python evaluates class bodies top-down, so `Animal` exists before `Mammal`.
  `ExtractExportedSymbol()` iterates top-level statements in order, so `Animal` should
  already be in the exports dict before `Mammal` is processed. If it's NOT, that's the bug.
- Q: "Should I fix this in ModuleLoader or ImportResolver?"
  A: Fix where the symbol gets lost. If `ExportedSymbols` is missing the key, fix
  `ModuleLoader`. If it's present but `ImportResolver` can't find it, fix `ImportResolver`.
- Q: "What about circular imports between modules?"
  A: Out of scope — `ModuleLoader` already has circular import detection. Focus only
  on the single-module export + import path.

---

## Task 3: Fix abstract class + interface stub generation (#189)

**Priority:** P1 — blocks abstract class patterns in multi-file programs
**Risk:** Medium — existing logic has a gap for specific patterns
**Commit message:** `fix: Generate abstract stubs for unimplemented interface methods in abstract classes (#189)`

### Root Cause

The `_interfaceDefinitions` dictionary in `RoslynEmitter.cs:85` is only populated for
interfaces defined in the same compilation unit. Cross-module interfaces don't get added
to this dictionary, so the abstract stub generation at `RoslynEmitter.TypeDeclarations.cs:289`
can't detect which methods need stubs.

### Architecture of the Bug

`_interfaceDefinitions` (declared `RoslynEmitter.cs:85`) is populated in
`RoslynEmitter.ModuleClass.cs:100-107` by iterating **only the current module's**
`statements` list. Cross-module interfaces are never added.

`CollectInterfaceMethodDefs()` (`TypeDeclarations.cs:339-393`) uses
`_interfaceDefinitions.ContainsKey(typeName)` as a proxy for "is this base type an
interface?" — which always returns `false` for imported interfaces. It then reads
`InterfaceDef.Body` (AST nodes) to enumerate methods — these AST nodes only exist in
the imported module's parse tree, not the current module's.

The `GenerateAbstractMethodStub()` (`TypeDeclarations.cs:505`) uses `FunctionDef.ReturnType`
and `FunctionDef.Parameters` from the AST to build stubs.

### Implementation Steps

1. **Read** `RoslynEmitter.TypeDeclarations.cs` — `CollectInterfaceMethodDefs` (line 339)
   and `GenerateAbstractMethodStub` (line 505).

2. **Read** where `_interfaceDefinitions` is populated — `RoslynEmitter.ModuleClass.cs:100-107`.

3. **Fix approach — use SymbolTable instead of AST:**
   The SymbolTable already has `TypeSymbol` entries for imported interfaces with
   `TypeKind == Interface` and populated `Methods` lists (from `ModuleLoader`). The fix
   should:

   a. In `CollectInterfaceMethodDefs`, when `_interfaceDefinitions.ContainsKey` returns
      false, fall back to checking the SymbolTable:
      ```csharp
      var typeSymbol = _context.LookupType(typeName);
      if (typeSymbol?.TypeKind == TypeKind.Interface)
      {
          // Use typeSymbol.Methods instead of InterfaceDef.Body
      }
      ```

   b. Add a new code path that builds stub info from `FunctionSymbol` (the semantic
      model) instead of `FunctionDef` (the AST). The `FunctionSymbol` has `ReturnType`,
      `Parameters`, and `Name` — everything needed to generate an abstract method stub.

   c. The existing AST-based path (`_interfaceDefinitions`) remains as an optimization
      for same-module interfaces where the AST is available.

4. **Add single-file test** first at
   `src/Sharpy.Compiler.Tests/Integration/TestFixtures/abstract_class_interface_stubs.spy`:
   ```python
   class IShape:
       @abstract
       def area(self) -> float:
           ...

   @abstract
   class ShapeBase(IShape):
       pass

   class Circle(ShapeBase):
       def __init__(self, radius: float):
           self.radius = radius

       @override
       def area(self) -> float:
           return 3.14159 * self.radius * self.radius

   def main():
       c = Circle(5.0)
       print(c.area())
   ```
   - `.expected`: `78.53975`

5. **Add multi-file test** if the single-file test passes — the issue may be
   cross-module-specific.

6. **Run tests**, **commit** with message above.

### Decision Guidelines
- Q: "Should I modify `_interfaceDefinitions` to include cross-module interfaces, or
  bypass it entirely?"
  A: Keep `_interfaceDefinitions` as a fast path for same-module interfaces (avoids
  SymbolTable lookup). Add a SymbolTable fallback for when the key is not found. This
  is the lowest-risk approach — existing behavior is preserved for same-module cases.
- Q: "The existing `GenerateAbstractMethodStub` takes `FunctionDef` AST nodes. Should I
  add an overload that takes `FunctionSymbol`?"
  A: Yes. Add a second overload `GenerateAbstractMethodStub(FunctionSymbol method)` that
  builds the stub from semantic model data (ReturnType, Parameters, Name). This cleanly
  separates the AST-based and semantic-model-based paths.
- Q: "What if the interface has a mix of abstract and concrete (default) methods?"
  A: Only generate stubs for abstract methods. Check the `FunctionSymbol.IsAbstract` flag.
- Q: "What about interfaces that extend other interfaces (interface inheritance)?"
  A: Walk the interface's `Interfaces` list recursively, same as the existing AST-based
  code does with `interfaceDef.BaseInterfaces`. The `TypeSymbol` for an interface should
  have its base interfaces populated.

---

## Task 4: Fix string indexing char vs str mismatch (#180)

**Priority:** P1 — breaks fundamental string operations
**Risk:** Low — clean, localized fix
**Commit message:** `fix: String indexing and iteration produce str instead of char (#180)`

### Root Cause

`GenerateIndexAccess()` in `RoslynEmitter.Expressions.Access.cs:648-668` generates plain
`ElementAccessExpression` for all types. For strings, C# `string[int]` returns `char`, but
Sharpy's semantic type says `str`. No `.ToString()` bridge exists.

Same issue in `GenerateForEachCore()` — `foreach (var c in s)` yields `char` values, but
the semantic type checker says the loop variable is `str`.

### Implementation Steps

1. **Fix string indexing** in `GenerateIndexAccess()` at
   `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs:648`:

   After the existing tuple indexing special case and before the final `return`, add:
   ```csharp
   // String indexing: string[int] returns char in C#, but Sharpy types it as str.
   // Wrap with .ToString() to bridge the type gap.
   var objectType = GetExpressionSemanticType(indexAccess.Object);
   if (objectType is BuiltinType { Name: "str" })
   {
       var elementAccess = ElementAccessExpression(objExpr)
           .AddArgumentListArguments(Argument(index));
       return InvocationExpression(
           MemberAccessExpression(
               SyntaxKind.SimpleMemberAccessExpression,
               elementAccess,
               IdentifierName("ToString")));
   }
   ```

2. **Fix string iteration** in `GenerateForEachCoreInner()` at
   `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs:~1286`:

   When the iterator is a string, wrap the loop variable assignment with `.ToString()`:
   - Detect string iteration by checking `GetExpressionSemanticType` on the `ForStatement.Iterator`
   - In the loop variable initialization (`loopVarInit`), add `.ToString()` to the
     `IdentifierName(tempLoopVar)` expression:
   ```csharp
   ExpressionSyntax loopVarValue = IdentifierName(tempLoopVar);

   // String iteration: foreach yields char, but Sharpy types loop var as str
   if (iteratorType is BuiltinType { Name: "str" })
   {
       loopVarValue = InvocationExpression(
           MemberAccessExpression(
               SyntaxKind.SimpleMemberAccessExpression,
               loopVarValue,
               IdentifierName("ToString")));
   }
   ```
   Note: the `ForStatement` AST node is available — the iterator type must be looked up
   from SemanticInfo for the `forStmt.Iterator` expression. Pass this from
   `GenerateFor()` into `GenerateForEachCore()`.

3. **Add test fixtures**:
   - `src/Sharpy.Compiler.Tests/Integration/TestFixtures/string_indexing.spy`:
     ```python
     def main():
         s: str = "hello"
         c: str = s[0]
         print(c)
         print(s[0] == "h")
         print(s[4].upper())
     ```
   - `.expected`:
     ```
     h
     True
     O
     ```
   - `src/Sharpy.Compiler.Tests/Integration/TestFixtures/string_iteration.spy`:
     ```python
     def main():
         s: str = "abc"
         for c in s:
             print(c.upper())
     ```
   - `.expected`:
     ```
     A
     B
     C
     ```

4. **Run tests**, **commit** with message above.

### Decision Guidelines
- Q: "Should I also handle string slicing (s[1:3])?"
  A: String slicing already returns `string` in C# (via `Substring`), so no fix needed there.
  Only `s[int]` returns `char`.
- Q: "What about `for c in s:` where `s` is a variable — how do I get its type?"
  A: Use `GetExpressionSemanticType(forStmt.Iterator)` which queries `_semanticInfo`.
- Q: "Should I use `Char.ToString()` or string interpolation?"
  A: `.ToString()` — it's the cleanest, zero-allocation path for a single char.

---

## Task 5: Fix empty dict literal ICE (#187)

**Priority:** P1 — triggers ICE on common pattern
**Risk:** Low — clean diagnostic addition
**Commit message:** `fix: Empty dict/list/set literal without type annotation emits diagnostic instead of ICE (#187)`

### Root Cause

`CheckDictLiteral()` in `TypeChecker.Expressions.Literals.cs:35-58` returns
`dict[unknown, unknown]` for empty dicts without consulting `_expectedType`. Same for
`CheckListLiteral` (empty `[]`) and `CheckSetLiteral` (empty `set()`).

When used in an unannotated assignment (`d = {}`), the variable gets `dict[unknown, unknown]`
and all subsequent operations on it produce `UnknownType`, eventually triggering SPY0907 ICE.

### Implementation Steps

1. **Read** `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs` fully.

2. **In `CheckDictLiteral`** (empty dict branch, ~line 37):
   - If `_expectedType` is a `GenericType` with `Name == "dict"` and has 2 type arguments,
     use those as the key/value types instead of Unknown.
   - Otherwise, emit a new diagnostic:
     ```
     SPY0220: Cannot infer type of empty dict literal; add a type annotation (e.g., d: dict[str, int] = {})
     ```
     And mark the expression as error recovery.

3. **Apply the same pattern** to `CheckListLiteral` (empty `[]`) and `CheckSetLiteral`
   (empty `set()`).

4. **Check** what diagnostic code to use. SPY0220 is `TypeMismatch` — use an existing code
   if there's one for "cannot infer type", otherwise add a new one in the SPY0200-0399 range.
   Search `DiagnosticCodes.cs` for available codes.

5. **Add test fixtures**:
   - `errors/empty_dict_no_annotation.spy`:
     ```python
     def main():
         d = {}
         d["key"] = 1
     ```
   - `errors/empty_dict_no_annotation.error`: `Cannot infer type`
   - `empty_dict_with_annotation.spy` (positive test):
     ```python
     def main():
         d: dict[str, int] = {}
         d["key"] = 42
         print(d["key"])
     ```
   - `.expected`: `42`

6. **Run tests**, **commit** with message above.

### Decision Guidelines
- Q: "Should `_expectedType` be consulted for ALL empty collection literals, or only
  in assignment context?"
  A: `_expectedType` is set by the assignment/parameter context. If it's set and matches,
  use it. If it's not set (standalone empty dict), emit the diagnostic.
- Q: "What about `d = {}` in a reassignment where d was previously typed?"
  A: If `d` already has a known type from a prior declaration, the TypeChecker's variable
  lookup gives `_expectedType` the prior type. The empty dict should adopt that type.
  Verify this works by testing `d: dict[str, int] = {1: 2}; d = {}; d["a"] = 3`.
- Q: "The issue says to also check empty set — what about `set()` vs `{}`?"
  A: In Python, `{}` is a dict, not a set. `set()` is the empty set. Sharpy follows this.
  Check `CheckSetLiteral` for the `set()` case.

---

## Task 6: Fix hex integer literals in enum values (#188)

**Priority:** P1 — crashes compiler on valid Python pattern
**Risk:** Low — straightforward parsing fix
**Commit message:** `fix: Parse hex, octal, and binary integer literals in code generation (#188)`

### Root Cause

`GenerateIntegerLiteral()` in `RoslynEmitter.Expressions.cs:140` calls
`int.Parse(literal.Value)` which fails on hex-prefixed strings like `"0xFF0000"`.
The lexer's `ReadHexNumber()` stores the full `"0x..."` prefix in the token value.

### Implementation Steps

1. **Read** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`, find
   `GenerateIntegerLiteral` (~line 130-145).

2. **Replace** the integer parsing logic with prefix-aware parsing:
   ```csharp
   private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral literal)
   {
       var text = literal.Value.Replace("_", ""); // Strip underscores first

       long value;
       if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
           value = long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
       else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
           value = Convert.ToInt64(text[2..], 8);
       else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
           value = Convert.ToInt64(text[2..], 2);
       else
           value = long.Parse(text, CultureInfo.InvariantCulture);

       // Use int if value fits, otherwise long
       if (value >= int.MinValue && value <= int.MaxValue)
           return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)value));
       else
           return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
   }
   ```
   Note: Use `long` throughout to handle large hex values like `0xFF000000` that exceed
   `int.MaxValue`.

3. **Also check** if the existing code already handles underscores in integer literals
   (e.g., `1_000_000`). The lexer strips underscores for some literal types but may
   preserve them for hex. Add `Replace("_", "")` to be safe.

4. **Add test fixtures**:
   - `enum_hex_values.spy`:
     ```python
     class Color(Enum):
         RED = 0xFF0000
         GREEN = 0x00FF00
         BLUE = 0x0000FF

     def main():
         print(Color.RED.value)
         print(Color.GREEN.value)
         print(Color.BLUE.value)
     ```
   - `.expected`:
     ```
     16711680
     65280
     255
     ```
   - `integer_literal_bases.spy` (non-enum, covers all bases):
     ```python
     def main():
         print(0xFF)
         print(0o755)
         print(0b1010)
     ```
   - `.expected`:
     ```
     255
     493
     10
     ```

5. **Run tests**, **commit** with message above.

### Decision Guidelines
- Q: "Should I also handle negative hex literals like `-0xFF`?"
  A: The parser handles unary minus separately (as `UnaryOp(Minus, IntegerLiteral("0xFF"))`).
  The integer literal itself is always positive. The unary minus is handled in
  `GenerateUnaryOp`. No special handling needed.
- Q: "What about underscores in hex literals like `0xFF_FF_FF`?"
  A: The lexer includes underscores in the token value. Strip them before parsing.
  Add `Replace("_", "")` at the top.
- Q: "What about very large hex values that overflow long?"
  A: Python supports arbitrary precision. For now, `long` (64-bit) covers all practical
  enum and bitmask values. If overflow occurs, let it throw — we can add `BigInteger`
  support later.

---

## Task 7: Fix float literals emitting as integers (#183)

**Priority:** P2 — incorrect output but has workaround
**Risk:** Low — Roslyn behavior is well-understood
**Commit message:** `fix: Float literals preserve decimal point in generated C# (#183)`

### Root Cause

`GenerateFloatLiteral()` in `RoslynEmitter.Expressions.cs:~146-148` uses
`Literal(literal.Value, value)` where Roslyn's `SyntaxFactory.Literal(string text, double value)`
normalizes the text representation of whole-number doubles. When `literal.Value` is `"5.0"`,
Roslyn may emit `5` instead of `5.0`, losing the float type information.

### Implementation Steps

1. **Read** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`, find
   `GenerateFloatLiteral` (~line 143-149).

2. **Fix:** Ensure the text always includes a `d` suffix (C# double literal suffix) to
   force Roslyn to emit a double literal:
   ```csharp
   private ExpressionSyntax GenerateFloatLiteral(FloatLiteral literal)
   {
       var value = double.Parse(literal.Value, CultureInfo.InvariantCulture);

       // Determine the appropriate suffix based on the literal type
       if (literal.Suffix != null)
       {
           // Float literal with explicit suffix (e.g., 5.0f, 5.0d)
           var text = literal.Value + literal.Suffix;
           if (literal.Suffix.Equals("f", StringComparison.OrdinalIgnoreCase))
               return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                   Literal(text, (float)value));
           return LiteralExpression(SyntaxKind.NumericLiteralExpression,
               Literal(text, value));
       }

       // Default: emit as double with 'd' suffix to prevent Roslyn from
       // normalizing whole numbers (e.g., 5.0 → 5)
       var literalText = literal.Value.Contains('.') || literal.Value.Contains('e')
           || literal.Value.Contains('E')
           ? literal.Value + "d"
           : literal.Value + ".0d";
       return LiteralExpression(SyntaxKind.NumericLiteralExpression,
           Literal(literalText, value));
   }
   ```

3. **Verify** that the `d` suffix is valid in the contexts where float literals appear
   (assignments, function arguments, comparisons). `5.0d` is a valid C# double literal.

4. **Add test fixture**:
   - `float_literal_output.spy`:
     ```python
     def main():
         print(5.0)
         print(1.0)
         print(0.0)
         print(3.14)
     ```
   - `.expected`:
     ```
     5.0
     1.0
     0.0
     3.14
     ```

5. **Run tests** — check that no existing tests break from the `d` suffix change.
6. **Commit** with message above.

### Decision Guidelines
- Q: "Will the `d` suffix break any existing C# code generation?"
  A: No. `5.0d` is equivalent to `5.0` in C# — both are `double`. The suffix just prevents
  Roslyn's text normalization from stripping the decimal.
- Q: "What about the `float32` type (`f` suffix)?"
  A: `float32` literals with the `f` suffix are already handled in the existing code.
  This fix only affects the default (unsuffixed) double path.
- Q: "What about scientific notation like `1e10`?"
  A: The `Contains('e')` check handles this — `1e10d` is valid C#.

---

## Task 8: Add dogfood retry remediation hints (#191)

**Priority:** P2 — improves dogfood success rate by ~6 iterations/run
**Risk:** Low — Python tooling change, no compiler risk
**Commit message:** `feat: Add error-pattern-specific remediation hints to dogfood retry prompts (#191)`

### Implementation Steps

1. **Read** `build_tools/sharpy_dogfood/prompts.py`, find `get_regeneration_prompt()` (~line 674)
   and `get_multifile_regeneration_prompt()` (~line 767).

2. **Add a remediation lookup table** near the top of `prompts.py`:
   ```python
   # Error-pattern-specific remediation hints for retry prompts.
   # Keys are regex patterns matched against the validation_error string.
   # Values are plain-text hints injected into the retry prompt.
   RETRY_REMEDIATION: list[tuple[str, str]] = [
       (r"SPY0456", "When defining __hash__, you MUST also define __eq__(self, other: object). "
        "Note: the parameter type must be 'object', not the class type."),
       (r"SPY0018", "Remove ALL markdown code fences (```) from your code. "
        "They are not valid Sharpy syntax."),
       (r"SPY0220.*list\[.*\?\]", "Cannot create list[T?] from mixed T and None literals. "
        "Use an empty list and .append() each value individually."),
       (r"SPY0301.*no exported symbol", "Check that the imported symbol name matches exactly "
        "(case-sensitive) and that the symbol is defined at the module's top level."),
       (r"SPY0907", "An internal compiler error occurred. Try simplifying your code — "
        "avoid deeply nested generics or complex cross-module patterns."),
       (r"FormatException.*0x", "Hex literals in enum values may not be supported yet. "
        "Use decimal integer values instead."),
   ]
   ```

3. **Add a helper function**:
   ```python
   def _get_remediation_hint(validation_error: str) -> str:
       """Match validation_error against known patterns and return remediation hints."""
       import re
       hints = []
       for pattern, hint in RETRY_REMEDIATION:
           if re.search(pattern, validation_error, re.IGNORECASE):
               hints.append(hint)
       if not hints:
           return ""
       return "\n\n## Remediation Hints\n\n" + "\n".join(f"- {h}" for h in hints) + "\n"
   ```

4. **Inject** the hint into both prompt functions, after the `## Validation Error` block
   and before `## Instructions`:
   ```python
   {_get_remediation_hint(validation_error)}
   ```

5. **Add pytest** in `build_tools/tests/test_prompts.py`:
   ```python
   def test_remediation_hint_spy0456():
       hint = _get_remediation_hint("error SPY0456: __hash__ requires __eq__")
       assert "__eq__(self, other: object)" in hint

   def test_remediation_hint_no_match():
       hint = _get_remediation_hint("some unknown error")
       assert hint == ""
   ```

6. **Run** `pytest build_tools/` to validate.
7. **Commit** with message above.

### Decision Guidelines
- Q: "Should I use exact error codes or regex patterns?"
  A: Use regex patterns — some errors have variable content after the code. The list-of-tuples
  approach allows ordered matching (first match wins if multiple apply).
- Q: "How many patterns should I add?"
  A: Start with the 6 listed above. More can be added incrementally as new dogfood runs
  reveal repeated failure patterns.

---

## Task 9: Add scientific notation float formatting tests (#181)

**Priority:** P3 — edge case test coverage
**Risk:** None — test-only change
**Commit message:** `test: Add scientific notation float formatting coverage (#181)`

### Implementation Steps

1. **Verify Python behavior first**:
   ```bash
   python3 -c "print(1e20); print(1.5e-10); print(1e308)"
   ```

2. **Add unit test** in `src/Sharpy.Core.Tests/StrTests.cs`:
   ```csharp
   [Theory]
   [InlineData(1e20)]
   [InlineData(1.5e-10)]
   public void FormatFloat_ScientificNotation_MatchesPython(double value)
   {
       // Verify the FormatFloat helper preserves scientific notation
       var result = Sharpy.Str.FormatFloat(value);
       Assert.Contains("e", result, StringComparison.OrdinalIgnoreCase);
   }
   ```

3. **Add integration test** `float_scientific_notation.spy`:
   ```python
   def main():
       print(1e20)
       print(1.5e-10)
   ```
   Set `.expected` based on actual Python output.

4. **Run tests**, **commit** with message above.

---

## Task 10: Add negative zero float formatting test (#182)

**Priority:** P3 — edge case, document Axiom 1 divergence
**Risk:** None — test-only change
**Commit message:** `test: Add negative zero float formatting test and document Axiom 1 divergence (#182)`

### Implementation Steps

1. **Verify both behaviors**:
   ```bash
   python3 -c "print(-0.0)"   # Expect: -0.0
   ```
   ```csharp
   // Check .NET: (-0.0).ToString("R", CultureInfo.InvariantCulture)
   ```

2. **Add unit test** in `src/Sharpy.Core.Tests/StrTests.cs`:
   ```csharp
   [Fact]
   public void FormatFloat_NegativeZero_DocumentsAxiom1Divergence()
   {
       // Python: str(-0.0) == "-0.0"
       // .NET may produce "0" or "0.0" (without negative sign)
       // Per Axiom 1, .NET behavior is acceptable
       var result = Sharpy.Str.FormatFloat(-0.0);
       // Document actual behavior — don't assert Python compatibility
       Assert.NotNull(result);
       // If .NET preserves negative zero, great; if not, that's Axiom 1
   }
   ```

3. **Run tests**, **commit** with message above.

---

## Task 11: Close already-resolved issue (#174)

**Priority:** Housekeeping
**Commit message:** N/A (no code change)

Issue #174 (Remove ValidateRestoredSymbols dead code) was already resolved in commit
`9a93adc8` on 2026-02-16. The commit message explicitly says: "Delete ~266 lines of
dead ValidateRestoredSymbols code (See: #174)."

### Steps

```bash
gh issue close 174 --reason completed --comment "Resolved in commit 9a93adc8 (2026-02-16). The ~266 lines of dead ValidateRestoredSymbols code were removed during the ProjectCompiler split refactor."
```

---

## Task 12: Close all completed issues

After all tasks are implemented and tests pass:

```bash
# For each issue fixed in this sprint:
gh issue close 186 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 190 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 189 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 180 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 187 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 188 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 183 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 191 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 181 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
gh issue close 182 --reason completed --comment "Fixed in branch fix/issue-sprint-feb18"
```

Update the comment with the actual PR URL once the PR is created.

---

## Global Decision Guidelines

### When uncertain about Python behavior
Always verify with `python3 -c "..."` before implementing. Python is the reference
implementation for Sharpy's semantic behavior, subject to Axiom 1 (.NET) and Axiom 3
(type safety) overrides.

### When a fix risks breaking existing tests
Run `dotnet test` after every commit. If any test breaks, investigate whether the test
was relying on incorrect behavior (update the test) or if the fix introduced a regression
(fix the fix). Never modify `.expected` files to make tests pass — fix the implementation.

### When the root cause is ambiguous
Add a failing test FIRST, then investigate. The test locks in the expected behavior and
serves as the acceptance criterion. If investigation reveals the bug is different from
what the issue describes, update the issue with findings before fixing.

### When a fix touches shared infrastructure
If changing `ModuleLoader`, `ImportResolver`, `TypeChecker`, or `RoslynEmitter` core paths:
1. Run the full test suite, not just targeted tests
2. Check for cascade effects on related issues (e.g., fixing #186 might also fix #190)
3. If two fixes interact, commit them separately to preserve bisect-ability

### Commit discipline
- One logical change per commit
- Each commit must leave the tree in a buildable, all-tests-passing state
- Use `dotnet format whitespace` before every commit
- Reference the issue number in every commit message

### Cross-module issues (#186, #190, #189) share infrastructure
These three issues all involve the `ModuleLoader` → `ImportResolver` → `TypeChecker` pipeline
for imported symbols. Fix #186 first (simplest, highest impact), then check if #190 and
#189 are also fixed or partially improved. They may share root causes.

---

## Out of Scope (Future Sprints)

| Issue | Reason for deferral |
|-------|-------------------|
| #171 (kwargs) | Major parser feature, needs design doc |
| #172 (decorator args) | Major parser+semantic feature |
| #173 (lambda defaults) | Parser feature, low priority |
| #175 (test density) | Ongoing quality work, no urgency |
| #105 (async state machine) | v0.2.x feature, large scope |
| #108 (str.format parser) | v0.6 feature, large scope |
| #192 (dogfood timeout tracing) | Low severity, observability |
| #193 (auto-generate fixtures) | Nice-to-have, low priority |

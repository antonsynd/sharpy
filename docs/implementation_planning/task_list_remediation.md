# Sharpy Compiler Remediation Task List

> **Purpose**: This document provides actionable remediation tasks extracted from the Phase 0.1.0 – 0.1.5 task log analysis. Tasks are organized by priority and include both verification/audit tasks and implementation fixes.

> **Source**: Tasks extracted from `docs/plans/task_log_analysis_todo.md` analysis notes, cross-referenced with `docs/implementation_planning/phases.md` and language specification documents.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| 🔧 | Fix required |
| 🔍 | Audit/Verify existing work |
| 🆕 | New implementation required |
| ⚠️ | Spec deviation to address |
| ✅ | Verification step |
| 📁 | File location |
| 🚨 | Critical priority |
| 🟠 | Medium priority |
| 🟢 | Low priority |

---

## Phase 0.1.1: Parser Foundation - Remediation

### Task R-0.1.1.1: Verify Keyword-as-Member-Name Bug Resolution

🔍 **Priority**: 🟠 Medium

**Background**: Task 0.1.1.1 identified that reserved keywords (like `property`, `event`) after `.` in member access expressions were not accepted as member names. Task 0.1.1.4 notes indicate test count increased from 453 to 456, suggesting this may have been fixed.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs` (around line 1787)

**Actions**:

1. [ ] Verify that `ParsePostfix()` now accepts keywords as member names after `.` operator
2. [ ] Run the following tests and confirm they pass:
   - `ParseChainedIndexingAndMemberAccess`
   - `ParseComplexMemberAccessChain`
   - `Position_ComplexNestedExpression_AllPositionsTracked`
3. [ ] If tests are still failing, implement fix:
   - Create a helper method `ExpectMemberName()` that accepts both `TokenType.Identifier` AND keyword tokens
   - Modify `ParsePostfix()` to use this helper when parsing member names after `.`

**Verification**:
- ✅ Run: `dotnet test --filter "ParseChainedIndexingAndMemberAccess|ParseComplexMemberAccessChain|Position_ComplexNestedExpression"`
- ✅ All 3 tests should pass

---

### Task R-0.1.1.2: Implement Walrus Operator (`:=`)

🆕 **Priority**: 🟢 Low (can defer to future phase)

**Background**: Task 0.1.1.2 identified that the walrus operator `:=` (assignment expression) at precedence level 20 is not implemented.

📁 **Files**:
- `src/Sharpy.Compiler/Lexer/Token.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

**Actions**:

1. [ ] Verify `TokenType.ColonAssign` exists in `Token.cs` for `:=`
2. [ ] Add `WalrusExpression` AST node if missing:
   ```csharp
   public record WalrusExpression(
       string Name,
       Expression Value,
       int LineStart, int ColumnStart, int LineEnd, int ColumnEnd
   ) : Expression(LineStart, ColumnStart, LineEnd, ColumnEnd);
   ```
3. [ ] Implement `ParseWalrus()` method at lowest precedence level
4. [ ] Add code generation support (C# doesn't have walrus, so lower to: `(name = value)`)

**Verification**:
- ✅ Test: `"if (x := get_value()) > 0:"` parses correctly
- ✅ Test: Walrus expressions can be used in `if`, `while`, and comprehensions

---

### Task R-0.1.1.3: Implement `try`/`maybe` Expressions

🆕 **Priority**: 🟠 Medium

**Background**: Task 0.1.1.2 identified that `try` and `maybe` expressions at precedence level 17 are not implemented (only `try` statement exists). Per `maybe_expressions.md`, these wrap expressions in `Result[T, E]` or `Optional[T]`.

📁 **Files**:
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Add `TryExpression` AST node:
   ```csharp
   public record TryExpression(
       Expression Expression,
       int LineStart, int ColumnStart, int LineEnd, int ColumnEnd
   ) : Expression(LineStart, ColumnStart, LineEnd, ColumnEnd);
   ```
2. [ ] Add `MaybeExpression` AST node:
   ```csharp
   public record MaybeExpression(
       Expression Expression,
       int LineStart, int ColumnStart, int LineEnd, int ColumnEnd
   ) : Expression(LineStart, ColumnStart, LineEnd, ColumnEnd);
   ```
3. [ ] Implement parsing at low precedence level (below conditional, above assignment)
4. [ ] Implement code generation:
   - `try expr` → wrap in `Result<T, Exception>`
   - `maybe expr` → wrap in `Optional<T>` (expr must be nullable type)
5. [ ] Add type checking to verify `maybe` only applies to nullable expressions

**Verification**:
- ✅ Test: `"x = try d.get('key')"` parses and generates correctly
- ✅ Test: `"y = maybe obj to Widget?"` wraps in `Optional[Widget]`
- ✅ Test: `"z = maybe non_nullable_expr"` produces compile error

---

### Task R-0.1.1.4: Audit Pipe Operator Code Generation

🔍 **Priority**: 🟠 Medium

**Background**: Task 0.1.1.3 implemented `|>` parsing, but code generation needs verification. Per `pipe_operator.md`, `x |> f` should pass `x` as the first argument to `f`.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify code generation handles `BinaryOperator.PipeForward`:
   - `x |> f` → `f(x)`
   - `x |> f(y)` → `f(x, y)` (prepend to argument list)
   - `x |> f |> g` → `g(f(x))`
2. [ ] Add integration tests for pipe operator code generation
3. [ ] Verify type checking validates that right-hand side is callable

**Verification**:
- ✅ Run: `dotnet test --filter "Pipe"`
- ✅ Test: `"5 |> str()"` compiles and evaluates to `"5"`

---

### Task R-0.1.1.5: Audit `to` Operator Code Generation

🔍 **Priority**: 🟠 Medium

**Background**: Task 0.1.1.4 implemented `to` operator parsing. Per `type_casting.md`, need to verify code generation handles both forms.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify `TypeCoercion` code generation:
   - `value to T` → `(T)value` with potential `InvalidCastException`
   - `value to T?` → `value as T` returning `null` on failure
2. [ ] Add tests for numeric conversions with overflow handling
3. [ ] Verify type narrowing integration works after successful `to T?`

**Verification**:
- ✅ Test: `"obj to Dog"` generates cast that throws on failure
- ✅ Test: `"obj to Dog?"` generates safe cast returning `null`

---

## Phase 0.1.2: Code Generation Bootstrap - Remediation

### Task R-0.1.2.1: Fix Floor Division Semantics

🔧 **Priority**: 🚨 Critical

**Background**: Tasks 0.1.2.3 and 0.1.3.3 identified that floor division (`//` and `//=`) uses incorrect semantics. Implementation uses `(int)(x/y)` which truncates toward zero, but Python/spec requires flooring toward negative infinity.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Spec Reference**: `docs/language_specification/arithmetic_operators.md`
> `//`: 🔄 Lowered to `(long)Math.Floor((double)a / b)` for integers, `Math.Floor(a / b)` for floats.

**Actions**:

1. [ ] Locate `//` operator generation (around line 1817-1822)
2. [ ] Change from `(int)(x / y)` to `(long)Math.Floor((double)x / y)` for integer operands
3. [ ] Locate `//=` augmented assignment generation (around line 1476-1479)
4. [ ] Apply same fix for `//=`
5. [ ] Ensure float operands use `Math.Floor(a / b)`

**Test Cases**:
```python
# Current (wrong):  -7 // 2 = -3 (truncates toward zero)
# Expected (spec):  -7 // 2 = -4 (floors toward negative infinity)
assert -7 // 2 == -4
assert -7 // 3 == -3
assert 7 // -3 == -3
```

**Verification**:
- ✅ Add test: `-7 // 2` evaluates to `-4`
- ✅ Add test: `x = -7; x //= 2; assert x == -4`
- ✅ Run: `dotnet test --filter "FloorDivision"`

---

### Task R-0.1.2.2: Audit Unity C# 9.0 Compatibility (File-Scoped Namespaces)

🔍 **Priority**: 🟠 Medium

**Background**: Tasks 0.1.2.1, 0.1.2.3, and 0.1.2.4 all flagged that `FileScopedNamespaceDeclaration` at line 92 of `RoslynEmitter.cs` is a C# 10+ feature, but the project targets C# 9.0 for Unity compatibility.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Determine if Unity C# 9.0 compatibility is still a project requirement
2. [ ] If yes: Replace `FileScopedNamespaceDeclaration()` with block-scoped `NamespaceDeclaration()`
3. [ ] If no: Document that generated code requires C# 10+ and close this issue

**Note**: This affects **generated** C# code, not the compiler source code itself.

**Verification**:
- ✅ Verify generated C# code compiles with C# 9.0 compiler
- ✅ Run integration tests targeting netstandard2.0 or Unity-compatible runtime

---

### Task R-0.1.2.3: Add Primitive Type Aliases to PrimitiveCatalog

⚠️ **Priority**: 🟢 Low

**Background**: Task 0.1.2.2 identified that `PrimitiveCatalog` only registers C# names (`sbyte`, `short`, `int`, etc.) but the spec (`primitive_types.md`) defines Sharpy-style names (`int8`, `int16`, `int32`, etc.) as primary.

📁 **Files**: `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`

**Spec Reference**: `docs/language_specification/primitive_types.md`

**Actions**:

1. [ ] Add Sharpy-style aliases to `PrimitiveCatalog.RegisterAll()`:
   ```csharp
   // Add these aliases:
   RegisterType("int8", typeof(sbyte));
   RegisterType("int16", typeof(short));
   RegisterType("int32", typeof(int));
   RegisterType("int64", typeof(long));
   RegisterType("uint8", typeof(byte));
   RegisterType("uint16", typeof(ushort));
   RegisterType("uint32", typeof(uint));
   RegisterType("uint64", typeof(ulong));
   RegisterType("float32", typeof(float));
   RegisterType("float64", typeof(double));
   ```
2. [ ] Ensure existing C# aliases (`int`, `float`, `byte`, `sbyte`) still work

**Verification**:
- ✅ Test: `"x: int32 = 42"` compiles correctly
- ✅ Test: `"y: float64 = 3.14"` compiles correctly
- ✅ Run: `dotnet test --filter "PrimitiveType"`

---

## Phase 0.1.3: Variables & Expressions - Remediation

### Task R-0.1.3.1: Fix Const Name Mangling Inconsistency

🔧 **Priority**: 🚨 Critical

**Background**: Task 0.1.3.6 identified that const declarations use `ToConstantCase()` (preserves `BASE` as `BASE`) but references use `ToCamelCase()` (converts `BASE` to `base`), causing C# compilation failures when consts are used in expressions.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Locate const declaration generation and const reference generation
2. [ ] Make naming consistent - either:
   - Option A: Both use `ToConstantCase()` (preserves ALL_CAPS)
   - Option B: Both use `ToPascalCase()` (converts to .NET convention)
3. [ ] Recommended: Use `ToPascalCase()` for consistency with C# conventions

**Test Cases**:
```python
const BASE: int = 10
result = BASE * 2  # Currently fails - declaration generates BASE, reference generates base
```

**Verification**:
- ✅ Unskip test: `Const_UsedInExpression_CompilesAndRuns`
- ✅ Unskip test: `Const_UsedInMultipleExpressions_CompilesAndRuns`
- ✅ Unskip test: `MixedDeclarations_WithOperations_CompilesAndRuns`
- ✅ Run: `dotnet test --filter "Const"`

---

### Task R-0.1.3.2: Make Const Type Annotation Optional

⚠️ **Priority**: 🟠 Medium

**Background**: Task 0.1.3.4 identified that the parser requires type annotation for const declarations (`const X: int = 1`) but the spec says type annotation should be optional (`const X = "MyApp"` with type inference).

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs` (around line 657-658)

**Spec Reference**: `docs/language_specification/statements.md`, `docs/language_specification/grammar.ebnf.txt`

**Actions**:

1. [ ] Modify `ParseConstDeclaration()` to make `:` and type annotation optional
2. [ ] When type annotation is omitted, infer type from initializer in `TypeChecker`
3. [ ] Add parser tests for const without type annotation

**Test Cases**:
```python
const NAME = "Sharpy"         # Type inferred as str
const VERSION = 1             # Type inferred as int
const PI: float = 3.14159     # Explicit type still works
```

**Verification**:
- ✅ Test: `"const NAME = 'Sharpy'"` parses successfully
- ✅ Test: `"const PI: float = 3.14"` still works
- ✅ Run: `dotnet test --filter "ParseConst"`

---

### Task R-0.1.3.3: Add Spec Deviation Tests for Division

🔍 **Priority**: 🟢 Low

**Background**: Task 0.1.3.5 noted that division `/` of two integers doesn't force `float64` result per spec.

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/`

**Spec Reference**: `docs/language_specification/arithmetic_operators.md`
> Integer types only | `float64` | Always promotes to `float64`

**Actions**:

1. [ ] Verify current behavior: `5 / 2` should equal `2.5` (float64), not `2` (int)
2. [ ] If behavior is incorrect, create fix task
3. [ ] Add tests documenting expected behavior

**Verification**:
- ✅ Test: `"result = 5 / 2"` → `result` should be `2.5` with type `float64`
- ✅ Run: `dotnet test --filter "Division"`

---

## Phase 0.1.4: Control Flow - Remediation

### Task R-0.1.4.1: Implement Loop `else` Clause

🆕 **Priority**: 🟠 Medium

**Background**: Tasks 0.1.4.2, 0.1.4.3, and 0.1.4.5 all identified that the `else` clause for loops (`for...else`, `while...else`) is NOT implemented.

📁 **Files**:
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Spec Reference**: `docs/language_specification/loop_else.md`

**Actions**:

1. [ ] Add `ElseBody` property to `WhileStatement`:
   ```csharp
   public record WhileStatement(
       Expression Test,
       List<Statement> Body,
       List<Statement>? ElseBody,  // NEW
       int LineStart, int ColumnStart, int LineEnd, int ColumnEnd
   ) : Statement(LineStart, ColumnStart, LineEnd, ColumnEnd);
   ```
2. [ ] Add `ElseBody` property to `ForStatement`:
   ```csharp
   public record ForStatement(
       Expression Target,
       Expression Iterator,
       List<Statement> Body,
       List<Statement>? ElseBody,  // NEW
       int LineStart, int ColumnStart, int LineEnd, int ColumnEnd
   ) : Statement(LineStart, ColumnStart, LineEnd, ColumnEnd);
   ```
3. [ ] Update parser to handle `else:` after loop body
4. [ ] Implement code generation using boolean flag pattern:
   ```csharp
   bool _loopCompleted = true;
   foreach (var item in items) {
       if (item == target) { _loopCompleted = false; break; }
   }
   if (_loopCompleted) { /* else body */ }
   ```

**Test Cases**:
```python
for item in items:
    if item == target:
        break
else:
    print("Not found")
```

**Verification**:
- ✅ Test: `for...else` executes else body when no break
- ✅ Test: `for...else` skips else body when break occurs
- ✅ Test: `while...else` works similarly
- ✅ Run: `dotnet test --filter "LoopElse"`

---

### Task R-0.1.4.2: Implement Try `else` Clause

🆕 **Priority**: 🟢 Low (can defer)

**Background**: Task 0.1.4.5 identified that `TryStatement` lacks `ElseBody` per `exception_handling.md` spec.

📁 **Files**:
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Add `ElseBody` property to `TryStatement` AST node
2. [ ] Update parser to handle `else:` clause in try statements
3. [ ] Implement code generation (else runs if no exception raised in try block)

**Verification**:
- ✅ Test: `try...except...else` runs else body when no exception
- ✅ Run: `dotnet test --filter "TryElse"`

---

## Phase 0.1.5: Functions - Remediation

### Task R-0.1.5.1: Fix Keyword Argument Count Validation

🔧 **Priority**: 🚨 Critical

**Background**: Task 0.1.5.4 identified that pure keyword argument calls fail semantic analysis because `TypeChecker.cs:1253` only counts positional args (`argTypes.Count`), ignoring `KeywordArguments`.

📁 **Files**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (around line 1253)

**Actions**:

1. [ ] Locate function call argument validation
2. [ ] Change argument count from `argTypes.Count` to `argTypes.Count + call.KeywordArguments.Count`
3. [ ] Add proper validation that considers keyword arguments filling parameters

**Test Cases**:
```python
def foo(x: int) -> int:
    return x * 2

result = foo(x=5)  # Currently fails: "Function expects 1 arguments but got 0"
```

**Verification**:
- ✅ Test: `"foo(x=5)"` compiles for `def foo(x: int)`
- ✅ Unskip tests in Phase015IntegrationTests:
  - `KeywordArgument_WithDefaults_SkipsMiddleParameter`
  - `KeywordArgument_MixedPositionalAndKeyword_WorksCorrectly`
  - `KeywordArgument_AllKeywords_AnyOrder`
  - `KeywordArgument_SingleKeyword_WorksCorrectly`
- ✅ Run: `dotnet test --filter "KeywordArgument"`

---

### Task R-0.1.5.2: Add Keyword Argument Validation

🆕 **Priority**: 🟠 Medium

**Background**: Task 0.1.5.4 identified several missing validations for keyword arguments.

📁 **Files**:
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Validate positional args cannot follow keyword args:
   - `foo(x=1, 2)` should produce error
2. [ ] Detect duplicate parameter specification:
   - `foo(1, x=2)` where first positional fills `x` should error
3. [ ] Consider keyword arg name mangling for C# interop

**Verification**:
- ✅ Test: `"foo(x=1, 2)"` produces "positional argument follows keyword argument" error
- ✅ Test: `"foo(1, x=2)"` produces "parameter 'x' specified multiple times" error

---

### Task R-0.1.5.3: Enhance Default Parameter Validation for Enum Values

🆕 **Priority**: 🟠 Medium

**Background**: Task 0.1.5.3 implemented `DefaultParameterValidator.cs` but noted that enum values (e.g., `Color.RED`) and const references are NOT supported as defaults.

📁 **Files**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`

**Spec Reference**: `docs/language_specification/function_default_parameters.md`

**Actions**:

1. [ ] Modify `IsCompileTimeConstant()` to accept `MemberAccess` expressions that resolve to enum members
2. [ ] Modify `IsCompileTimeConstant()` to accept `Identifier` expressions that resolve to `const` declarations
3. [ ] Add symbol table lookup to validate these cases

**Test Cases**:
```python
const DEFAULT_TIMEOUT: float = 30.0

enum Mode:
    NORMAL = 0
    DEBUG = 1

def process(timeout: float = DEFAULT_TIMEOUT, mode: Mode = Mode.NORMAL) -> None:
    pass
```

**Verification**:
- ✅ Test: `"def foo(mode: Mode = Mode.NORMAL)"` compiles successfully
- ✅ Test: `"def bar(timeout: float = DEFAULT_TIMEOUT)"` compiles successfully
- ✅ Run: `dotnet test --filter "DefaultParameter"`

---

### Task R-0.1.5.4: Implement Variadic Arguments (`*args`)

🆕 **Priority**: 🟢 Low (deferred feature)

**Background**: Tasks 0.1.5.1 and 0.1.5.2 identified that `*args` variadic parameters are not implemented.

📁 **Files**:
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Spec Reference**: `docs/language_specification/function_variadic_arguments.md`

**Actions**:

1. [ ] Add `IsVariadic` property to `Parameter` AST node
2. [ ] Update `ParseParameters()` to check for `TokenType.Star` prefix
3. [ ] Implement code generation with `params` modifier
4. [ ] Add type checking to ensure `*args` is last parameter

**Test Cases**:
```python
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result
```

**Verification**:
- ✅ Unskip tests: `ParsesFunctionWithVarArgs`, `ParsesFunctionWithKwargs`
- ✅ Test: `"sum_all(1, 2, 3)"` compiles and returns `6`

---

### Task R-0.1.5.5: Function Overloading (Deferred)

⚠️ **Priority**: 🟢 Low (intentionally deferred)

**Background**: Task 0.1.5.8 was intentionally deferred. Function overloading (same name, different signatures) is a Sharpy-specific extension not native to Python.

📁 **Files**: Would require `src/Sharpy.Compiler/Semantic/OverloadResolver.cs`

**Actions**:

1. [x] Document decision to defer function overloading
2. [x] Keep test `RegistersMultipleOverloadsOfSameOperator` skipped with message "Method overloading not yet supported in symbol table"
3. [ ] Consider implementing in v0.2.x+ phase

**Notes**: Python doesn't natively support function overloading. When implementing:
- Create `OverloadResolver.cs` to centralize resolution logic
- Implement "betterness" comparison between overloads
- Handle named argument filtering for candidate selection
- Add ambiguity detection

---

## Cross-Phase: Documentation & Spec Alignment

### Task R-DOC.1: Update collection_types.md Spec

⚠️ **Priority**: 🟢 Low

**Background**: Task 0.1.2.2 noted that `collection_types.md` states collections map to `System.Collections.Generic.*` but implementation correctly uses `Sharpy.Core.*` wrappers.

📁 **Files**: `docs/language_specification/collection_types.md`

**Actions**:

1. [ ] Update spec to reflect that `list`, `dict`, `set` map to `Sharpy.Core.*` wrappers
2. [ ] Document the wrapper behavior and Pythonic APIs

---

### Task R-DOC.2: Document C# Language Version Requirements

🔍 **Priority**: 🟢 Low

**Background**: Multiple tasks flagged file-scoped namespaces as C# 10+ feature.

📁 **Files**: `docs/` or project README

**Actions**:

1. [ ] Document the C# version requirements for generated code
2. [ ] Clarify whether Unity C# 9.0 compatibility is still a goal
3. [ ] If Unity compatibility is required, create task to fix generated code

---

## Summary: Priority Order

### 🚨 Critical (Fix Immediately)

| Task | Description | Phase |
|------|-------------|-------|
| R-0.1.2.1 | Fix floor division semantics (`//` and `//=`) | 0.1.2 |
| R-0.1.3.1 | Fix const name mangling inconsistency | 0.1.3 |
| R-0.1.5.1 | Fix keyword argument count validation | 0.1.5 |

### 🟠 Medium (Should Fix Soon)

| Task | Description | Phase |
|------|-------------|-------|
| R-0.1.1.1 | Verify keyword-as-member-name bug resolution | 0.1.1 |
| R-0.1.1.3 | Implement `try`/`maybe` expressions | 0.1.1 |
| R-0.1.1.4 | Audit pipe operator code generation | 0.1.1 |
| R-0.1.1.5 | Audit `to` operator code generation | 0.1.1 |
| R-0.1.2.2 | Audit Unity C# 9.0 compatibility | 0.1.2 |
| R-0.1.3.2 | Make const type annotation optional | 0.1.3 |
| R-0.1.4.1 | Implement loop `else` clause | 0.1.4 |
| R-0.1.5.2 | Add keyword argument validation | 0.1.5 |
| R-0.1.5.3 | Enhance default parameter validation for enums/consts | 0.1.5 |
| R-0.1.5.6 | Implement `global` keyword | 0.1.5 |

### 🟢 Low (Can Defer)

| Task | Description | Phase |
|------|-------------|-------|
| R-0.1.1.2 | Implement walrus operator (`:=`) | 0.1.1 |
| R-0.1.2.3 | Add primitive type aliases to PrimitiveCatalog | 0.1.2 |
| R-0.1.3.3 | Add spec deviation tests for division | 0.1.3 |
| R-0.1.4.2 | Implement try `else` clause | 0.1.4 |
| R-0.1.5.4 | Implement variadic arguments (`*args`) | 0.1.5 |
| R-0.1.5.5 | Function overloading (deferred) | 0.1.5 |
| R-DOC.1 | Update collection_types.md spec | - |
| R-DOC.2 | Document C# language version requirements | - |

---

## Test Suite Verification

After completing remediation tasks, run full test suite:

```bash
# Full test run
dotnet test

# Phase-specific tests
dotnet test --filter "Phase010"  # Lexer
dotnet test --filter "Phase011"  # Parser
dotnet test --filter "Phase012"  # CodeGen Bootstrap
dotnet test --filter "Phase013"  # Variables
dotnet test --filter "Phase014"  # Control Flow
dotnet test --filter "Phase015"  # Functions
```

**Expected Results After Remediation**:
- All previously skipped tests due to bugs should be unskipped and passing
- Zero test failures
- All integration tests pass end-to-end

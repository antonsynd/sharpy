# Implementation Plan: Task 0.1.2.6 - Document Phase 0.1.2 Exit Criteria

## Task Overview

**Task ID:** 0.1.2.6
**Title:** Document Phase 0.1.2 Exit Criteria
**Description:** Exit criteria validation - ensure all Phase 0.1.2 requirements are met and documented.

---

## Current State Analysis

### Test Status
- **46 integration tests** exist in `Phase012IntegrationTests.cs`
- **12 passing, 34 failing** (as of current run)
- Test failures indicate implementation gaps that need fixing before exit criteria can be validated

### Exit Criteria from Spec (phases.md:218-223)
1. ✅/❓ `pass` compiles to empty `Main()` and runs
2. ✅/❓ Primitive literals compile correctly
3. ✅/❓ Binary expressions generate correct C# operators
4. ✅/❓ Output is a valid .NET assembly

---

## Step-by-Step Implementation Approach

### Step 1: Diagnose Failing Tests
**Goal:** Understand why 34/46 tests are failing

**Actions:**
1. Run tests with verbose output to capture specific error messages
2. Categorize failures by type:
   - Lexer errors (tokenization issues)
   - Parser errors (AST generation issues)
   - Semantic errors (type checking/name resolution)
   - Code generation errors (RoslynEmitter issues)
   - C# compilation errors (invalid generated code)
3. Create a prioritized list of fixes

**Command:**
```bash
dotnet test --filter "FullyQualifiedName~Phase012" --verbosity detailed 2>&1 | head -200
```

### Step 2: Fix Blocking Issues in Order
**Goal:** Get all tests passing

Based on exploration, common issues likely include:
- Comment-only files may not generate valid code
- Unary plus (`+42`) may not be implemented
- Certain expression types may have code generation gaps

**Files to check/modify:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Code generation
- `src/Sharpy.Compiler/Parser/Parser.cs` - Parsing edge cases
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Type validation

### Step 3: Validate Each Exit Criterion

#### 3.1: `pass` compiles to empty `Main()` and runs
**Test cases:**
- `MinimalProgram_Pass_CompilesAndRuns`
- `MinimalProgram_PassWithNewline_CompilesAndRuns`
- `MinimalProgram_MultiplePassStatements_CompilesAndRuns`

**Validation:**
- Verify generated C# contains `public static void Main(string[] args) { ; }`
- Verify assembly executes without errors

#### 3.2: Primitive literals compile correctly
**Test cases:**
- `MinimalProgram_IntegerLiterals_CompilesAndRuns` (0, 42, -17, 1000000)
- `MinimalProgram_FloatLiterals_CompilesAndRuns` (3.14, -2.5, 0.0, 1.23e10)
- `MinimalProgram_BooleanLiterals_CompilesAndRuns` (True, False)
- `MinimalProgram_StringLiterals_CompilesAndRuns` ("hello", 'world', "", '')
- `MinimalProgram_NoneLiteral_CompilesAndRuns` (None → null)

**Validation:**
- Each literal type generates correct C# syntax
- Type inference produces correct types

#### 3.3: Binary expressions generate correct C# operators
**Test cases:**
- `MinimalProgram_AllArithmeticOperators_CompilesAndRuns` (+, -, *, //, %, **)
- `MinimalProgram_ComparisonOperators_CompilesAndRuns` (<, <=, >, >=, ==, !=)
- `MinimalProgram_LogicalOperators_CompilesAndRuns` (and, or, not)

**Operator mapping validation:**
| Sharpy | C# | Notes |
|--------|-----|-------|
| `+` | `+` | Direct mapping |
| `-` | `-` | Direct mapping |
| `*` | `*` | Direct mapping |
| `//` | Cast + `/` | Integer division |
| `%` | `%` | Direct mapping |
| `**` | `Math.Pow()` | Method call |
| `and` | `&&` | Short-circuit |
| `or` | `\|\|` | Short-circuit |
| `not` | `!` | Unary prefix |

#### 3.4: Output is a valid .NET assembly
**Validation:**
- All tests compile to in-memory assembly
- Assembly has valid entry point
- Can execute via reflection

### Step 4: Create Exit Criteria Documentation

**File to create:** `docs/implementation_planning/exit_criteria/phase_0.1.2_exit_criteria.md`

**Content structure:**
```markdown
# Phase 0.1.2 Exit Criteria Validation

## Summary
- Status: ✅ COMPLETE / ❌ INCOMPLETE
- Date Validated: YYYY-MM-DD
- Test Suite: Phase012IntegrationTests (46 tests)

## Exit Criteria Checklist

### 1. `pass` compiles to empty `Main()` and runs
- [ ] Single `pass` statement
- [ ] Multiple `pass` statements
- [ ] `pass` with whitespace/newlines

### 2. Primitive literals compile correctly
- [ ] Integer literals (with negation)
- [ ] Float literals (with scientific notation)
- [ ] Boolean literals (True, False)
- [ ] String literals (single/double quoted)
- [ ] None literal

### 3. Binary expressions generate correct C# operators
- [ ] Arithmetic: +, -, *, //, %, **
- [ ] Comparison: <, <=, >, >=, ==, !=
- [ ] Logical: and, or
- [ ] Unary: not, -, +

### 4. Output is a valid .NET assembly
- [ ] Compiles without errors
- [ ] Has entry point (Main method)
- [ ] Executes successfully

## Test Evidence
[Link to test run results]

## Verification Command
```bash
dotnet test --filter "FullyQualifiedName~Phase012" --logger "console;verbosity=detailed"
```
```

### Step 5: Run Full Validation

**Commands:**
```bash
# Run all Phase 0.1.2 tests
dotnet test --filter "FullyQualifiedName~Phase012" --verbosity normal

# Create sample compiled output for manual verification
echo "pass" > /tmp/test.spy
dotnet run --project src/Sharpy.Cli -- compile /tmp/test.spy -o /tmp/test.dll
dotnet /tmp/test.dll  # Should execute silently
```

---

## Key Files to Modify

| File | Purpose | Expected Changes |
|------|---------|-----------------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Code generation | Fix any gaps causing test failures |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Parsing | Handle edge cases (comments only, etc.) |
| `src/Sharpy.Compiler.Tests/Integration/Phase012IntegrationTests.cs` | Tests | Already comprehensive, may need adjustments |
| `docs/implementation_planning/exit_criteria/phase_0.1.2_exit_criteria.md` | **NEW** | Exit criteria documentation |

---

## Tests to Verify

### Must Pass (46 tests in Phase012IntegrationTests)

**Pass Statement (3 tests):**
- `MinimalProgram_Pass_CompilesAndRuns`
- `MinimalProgram_PassWithNewline_CompilesAndRuns`
- `MinimalProgram_MultiplePassStatements_CompilesAndRuns`

**Expression Statements (8 tests):**
- `MinimalProgram_IntegerLiteral_CompilesAndRuns`
- `MinimalProgram_SimpleAddition_CompilesAndRuns`
- `MinimalProgram_SimpleSubtraction_CompilesAndRuns`
- `MinimalProgram_SimpleMultiplication_CompilesAndRuns`
- `MinimalProgram_SimpleDivision_CompilesAndRuns`
- `MinimalProgram_ComplexExpression_CompilesAndRuns`
- `MinimalProgram_MultipleExpressionStatements_CompilesAndRuns`

**Binary Operators (3 tests):**
- `MinimalProgram_AllArithmeticOperators_CompilesAndRuns`
- `MinimalProgram_ComparisonOperators_CompilesAndRuns`
- `MinimalProgram_LogicalOperators_CompilesAndRuns`

**Assignments (4 tests):**
- `MinimalProgram_SimpleAssignment_CompilesAndRuns`
- `MinimalProgram_TypedAssignment_CompilesAndRuns`
- `MinimalProgram_MultipleAssignments_CompilesAndRuns`
- `MinimalProgram_AugmentedAssignment_CompilesAndRuns`

**Mixed Statements (3 tests):**
- `MinimalProgram_PassAndExpressions_CompilesAndRuns`
- `MinimalProgram_AssignmentAndExpressions_CompilesAndRuns`
- `MinimalProgram_AllStatementTypes_CompilesAndRuns`

**Comments (4 tests):**
- `MinimalProgram_OnlyComment_CompilesAndRuns`
- `MinimalProgram_PassWithComment_CompilesAndRuns`
- `MinimalProgram_ExpressionWithInlineComment_CompilesAndRuns`
- `MinimalProgram_AssignmentWithComments_CompilesAndRuns`

**Literals (7 tests):**
- `MinimalProgram_IntegerLiterals_CompilesAndRuns`
- `MinimalProgram_FloatLiterals_CompilesAndRuns`
- `MinimalProgram_BooleanLiterals_CompilesAndRuns`
- `MinimalProgram_StringLiterals_CompilesAndRuns`
- `MinimalProgram_NoneLiteral_CompilesAndRuns`
- `MinimalProgram_AllLiteralTypes_CompilesAndRuns`

**Empty/Whitespace (3 tests):**
- `MinimalProgram_EmptyFile_CompilesAndRuns`
- `MinimalProgram_OnlyWhitespace_CompilesAndRuns`
- `MinimalProgram_OnlyNewlines_CompilesAndRuns`

**Type Annotations (5 tests):**
- `MinimalProgram_IntTypeAnnotation_CompilesAndRuns`
- `MinimalProgram_FloatTypeAnnotation_CompilesAndRuns`
- `MinimalProgram_StrTypeAnnotation_CompilesAndRuns`
- `MinimalProgram_BoolTypeAnnotation_CompilesAndRuns`
- `MinimalProgram_MultipleTypedAssignments_CompilesAndRuns`

**Operator Precedence (4 tests):**
- `MinimalProgram_OperatorPrecedence_AdditionMultiplication_CompilesAndRuns`
- `MinimalProgram_OperatorPrecedence_WithParentheses_CompilesAndRuns`
- `MinimalProgram_OperatorPrecedence_Complex_CompilesAndRuns`
- `MinimalProgram_OperatorPrecedence_NestedParentheses_CompilesAndRuns`

**Unary Operators (4 tests):**
- `MinimalProgram_UnaryMinus_CompilesAndRuns`
- `MinimalProgram_UnaryPlus_CompilesAndRuns`
- `MinimalProgram_UnaryNot_CompilesAndRuns`
- `MinimalProgram_MultipleUnaryOperators_CompilesAndRuns`

---

## Potential Risks and Questions

### Risks

1. **Incomplete Implementation**
   - 34/46 tests currently failing suggests significant gaps
   - May require substantial fixes before exit criteria can be validated
   - Risk: Scope creep if fixes are complex

2. **Edge Cases**
   - Comment-only files may need special handling
   - Empty files might not generate valid entry points
   - Unary plus operator may not be implemented

3. **Test Infrastructure**
   - `IntegrationTestBase.CompileAndExecute()` relies on Sharpy.Core
   - Missing runtime library could cause false failures
   - Assembly resolution may be fragile

4. **Documentation Drift**
   - Exit criteria doc may become stale if specs change
   - Need clear ownership for keeping it updated

### Questions to Resolve

1. **Should comment-only files compile?**
   - Current test expects them to compile
   - Need to decide if this is valid Sharpy
   - May need to generate empty Main() for these

2. **Is unary plus required?**
   - `+42` is unusual but valid in Python
   - Need to confirm if this is in scope for Phase 0.1.2

3. **How should empty files be handled?**
   - Generate empty Main() that does nothing?
   - Or fail with "no statements" error?

4. **Exit criteria location?**
   - Create new `exit_criteria/` directory?
   - Or add to existing phases.md?

---

## Implementation Order

1. **Diagnose** - Run tests, understand failures (~15 min)
2. **Fix** - Address blocking issues in RoslynEmitter/Parser
3. **Validate** - Re-run tests, verify all pass
4. **Document** - Create exit criteria markdown file
5. **Final Check** - Manual verification with CLI

# Implementation Plan: Task 0.1.1.6 - Verify Pass Statement Parsing

## Summary

This task is a **verification task** - the `PassStatement` parsing is already fully implemented. The plan outlines the verification steps and confirms implementation completeness.

---

## Step-by-Step Implementation Approach

### Step 1: Verify `pass` Keyword Handling in Parser

**Location**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Verification**:
1. Check `ParseStatement()` method (line 98) - confirm `TokenType.Pass => ParsePassStatement()`
2. Check `ParsePassStatement()` method (lines 1057-1072) - confirm it:
   - Captures start position
   - Consumes `TokenType.Pass` token
   - Calls `ExpectStatementEnd()`
   - Returns `PassStatement` with position info

**Status**: ✅ Already implemented correctly

### Step 2: Verify AST Node Definition

**Location**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs:68`

**Verification**:
- Confirm `public record PassStatement : Statement;` exists
- Confirm it inherits position tracking from `Statement` base class

**Status**: ✅ Already implemented correctly

### Step 3: Run Existing Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Pass"
```

**Expected tests to pass**:
- `ParsePassStatement` - basic `"pass"` parsing
- `Position_PassStatement_TrackedCorrectly` - position tracking
- `ParseEmptyClassWithPass` - pass in class body
- `ParseEmptyStructWithPass` - pass in struct body
- `ParseEmptyInterfaceWithPass` - pass in interface body

### Step 4: (Optional) Add Explicit Function Body Test

If desired for completeness, add a dedicated test:

```csharp
[Fact]
public void ParsePassStatementInFunctionBody()
{
    var module = Parse("def foo():\n    pass");
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.Body.Should().HaveCount(1);
    func.Body[0].Should().BeOfType<PassStatement>();
}
```

---

## Key Files

| File | Purpose | Lines |
|------|---------|-------|
| `src/Sharpy.Compiler/Parser/Parser.cs` | Parser implementation | 98, 1057-1072 |
| `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | AST node definition | 68 |
| `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs` | Parser tests | 484-488 |
| `src/Sharpy.Compiler.Tests/Parser/ParserPositionTests.cs` | Position tests | 256 |

---

## Tests to Verify

### Required Tests (Already Exist)

| Test Case | Test Name | Status |
|-----------|-----------|--------|
| `"pass"` parses to `PassStatement` | `ParsePassStatement` | ✅ Exists |
| Indented `pass` in function body | Multiple negative tests use this pattern | ✅ Exists |
| Pass in class body | `ParseEmptyClassWithPass` | ✅ Exists |
| Pass in struct body | `ParseEmptyStructWithPass` | ✅ Exists |
| Pass in interface body | `ParseEmptyInterfaceWithPass` | ✅ Exists |
| Position tracking | `Position_PassStatement_TrackedCorrectly` | ✅ Exists |

### Implementation Verification Checklist

- [x] `pass` keyword recognized by Lexer (`TokenType.Pass`)
- [x] `pass` keyword handled in `ParseStatement()` switch
- [x] `ParsePassStatement()` method produces `PassStatement` AST node
- [x] Position tracking (LineStart, ColumnStart, LineEnd, ColumnEnd)
- [x] Works at top level
- [x] Works in class/struct/interface bodies
- [x] Works in function bodies

---

## Potential Risks or Questions

### Risks
**None** - All functionality is already implemented and tested.

### Questions Resolved
1. **Is `pass` handled in `ParseStatement()` or `ParseSimpleStatement()`?**
   - Answer: `ParseStatement()` - it has a dedicated case in the switch expression

2. **Does it produce `PassStatement`?**
   - Answer: Yes, via the `ParsePassStatement()` method

3. **Are there adequate tests?**
   - Answer: Yes, 5+ tests cover various contexts

---

## Recommended Action

1. **Run tests** to confirm all pass-related tests pass
2. **Mark task as complete** - no code changes needed
3. Optionally add the explicit function body test if desired for documentation purposes

---

## Test Command

```bash
cd /Users/anton/Documents/github/sharpy
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Pass"
```

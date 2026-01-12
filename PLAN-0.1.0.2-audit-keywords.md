# Implementation Plan: Task 0.1.0.2 - Audit/Implement Missing Keywords

## Overview
Audit the `Keywords` dictionary in `Lexer.cs` against the `TokenType` enum to ensure all keywords are properly mapped, cross-referencing with Task 0.1.0.1's findings.

---

## 1. Step-by-Step Implementation Approach

### Step 1: Current State Analysis

**Keywords Dictionary in `Lexer.cs` (lines 34-100)** currently has **41 keyword mappings**:

| Category | Keywords Mapped |
|----------|----------------|
| Control Flow | `def`, `class`, `struct`, `interface`, `enum`, `if`, `else`, `elif`, `while`, `for`, `in`, `return`, `break`, `continue`, `pass`, `try`, `except`, `finally`, `raise`, `assert`, `with` |
| Import | `import`, `from`, `as` |
| Type/Value | `auto`, `const`, `lambda`, `type` |
| Pattern Matching | `match`, `case` |
| Async | `async`, `await`, `yield` |
| Members | `property`, `event` |
| Other | `del`, `to`, `maybe` |
| Future Keywords | `defer`, `do` |
| Boolean/Operators | `True`, `False`, `None`, `and`, `or`, `not`, `is` |

**TokenType enum in `Token.cs`** has these keyword-related entries:

| Category | Token Types Present |
|----------|---------------------|
| Control Flow | `Def`, `Class`, `Struct`, `Interface`, `Enum`, `If`, `Else`, `Elif`, `While`, `For`, `In`, `Return`, `Break`, `Continue`, `Pass`, `Try`, `Except`, `Finally`, `Raise`, `Assert`, `With` |
| Import | `Import`, `From`, `As` |
| Type/Value | `Auto`, `Const`, `Lambda`, `Type` |
| Pattern Matching | `Match`, `Case` |
| Async | `Async`, `Await`, `Yield` |
| Members | `Property`, `Event` |
| Other | `Del`, `To`, `Maybe` |
| Future | `Defer`, `Do` |
| Boolean/Operators | `True`, `False`, `None`, `And`, `Or`, `Not`, `Is` |

### Step 2: Cross-Reference Verification

Comparing Task 0.1.0.1's findings with current implementation:

| Task 0.1.0.1 "Missing" | Lexer.cs Status | Token.cs Status | Action |
|------------------------|-----------------|-----------------|--------|
| `case` | ✅ Present (line 72) | ✅ Present | None |
| `event` | ✅ Present (line 81) | ✅ Present | None |
| `match` | ✅ Present (line 71) | ✅ Present | None |
| `maybe` | ✅ Present (line 86) | ✅ Present | None |
| `property` | ✅ Present (line 80) | ✅ Present | None |
| `to` | ✅ Present (line 85) | ✅ Present | None |
| `type` | ✅ Present (line 68) | ✅ Present | None |
| `yield` | ✅ Present (line 77) | ✅ Present | None |
| `async` | ✅ Present (line 75) | ✅ Present | None |
| `await` | ✅ Present (line 76) | ✅ Present | None |
| `del` | ✅ Present (line 84) | ✅ Present | None |

### Step 3: Result

**FINDING: All keywords identified in Task 0.1.0.1's audit are ALREADY IMPLEMENTED in both files.**

The PLAN-0.1.0.1-audit-token-types.md file appears to be outdated or was created before recent updates. The current codebase already contains:

1. **All 41 keywords** in the `Keywords` dictionary in `Lexer.cs`
2. **All corresponding TokenType enum values** in `Token.cs`
3. **Existing tests** for the core keywords in `LexerTests.cs` (lines 57-97)

### Step 4: Verify Test Coverage

**Currently tested keywords** (from `LexerTests.cs` lines 57-97):

```
def, class, struct, interface, enum, if, else, elif, while, for, in,
return, break, continue, pass, try, except, finally, raise, assert, with,
import, from, as, auto, const, lambda, True, False, None, and, or, not, is
```

**Keywords NOT in the test suite** (but implemented):
- `type`
- `match`, `case`
- `async`, `await`, `yield`
- `property`, `event`
- `del`, `to`, `maybe`
- `defer`, `do` (future/reserved)

---

## 2. Key Files to Modify

### No modifications needed for keyword implementation!

The audit reveals all keywords are already implemented. However, **test coverage should be expanded**.

| File | Status | Action Needed |
|------|--------|---------------|
| `src/Sharpy.Compiler/Lexer/Token.cs` | ✅ Complete | None |
| `src/Sharpy.Compiler/Lexer/Lexer.cs` | ✅ Complete | None |
| `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs` | ⚠️ Incomplete | Add tests for missing keywords |

---

## 3. Tests to Add

Extend the keyword theory test in `LexerTests.cs` (around line 57) to include:

```csharp
// Keywords - Type/Value (partially covered)
[InlineData("type", TokenType.Type)]

// Keywords - Pattern Matching (not covered)
[InlineData("match", TokenType.Match)]
[InlineData("case", TokenType.Case)]

// Keywords - Async (not covered)
[InlineData("async", TokenType.Async)]
[InlineData("await", TokenType.Await)]
[InlineData("yield", TokenType.Yield)]

// Keywords - Members (not covered)
[InlineData("property", TokenType.Property)]
[InlineData("event", TokenType.Event)]

// Keywords - Other (not covered)
[InlineData("del", TokenType.Del)]
[InlineData("to", TokenType.To)]
[InlineData("maybe", TokenType.Maybe)]

// Future Keywords (reserved, not covered)
[InlineData("defer", TokenType.Defer)]
[InlineData("do", TokenType.Do)]
```

### Additional Tests to Consider

1. **Case sensitivity test**: Verify keywords are case-sensitive
   ```csharp
   [Theory]
   [InlineData("MATCH")]
   [InlineData("Match")]
   [InlineData("ASYNC")]
   public void Tokenize_UppercaseKeyword_ReturnsIdentifier(string word)
   {
       var token = SingleToken(word);
       token.Type.Should().Be(TokenType.Identifier);
   }
   ```

2. **Keyword prefix test**: Verify keyword prefixes are identifiers
   ```csharp
   [Theory]
   [InlineData("matching")]
   [InlineData("async_task")]
   [InlineData("await_result")]
   public void Tokenize_KeywordPrefix_ReturnsIdentifier(string word)
   {
       var token = SingleToken(word);
       token.Type.Should().Be(TokenType.Identifier);
   }
   ```

---

## 4. Potential Risks and Questions

### Risks

1. **None for implementation** - All keywords already exist
2. **Low risk for tests** - Adding tests is purely additive

### Questions Resolved

1. **Q: Are keywords from 0.1.0.1 missing?**
   - A: No, all are already implemented. The audit file may be stale.

2. **Q: Are there undocumented keywords in Lexer.cs not in Token.cs?**
   - A: No, all mappings in Keywords dictionary have corresponding TokenType values.

3. **Q: Are there TokenType keywords without Lexer mappings?**
   - A: No, all keyword TokenTypes have corresponding Lexer dictionary entries.

### Implementation Order

Since no implementation is needed, focus on:
1. Verify the audit finding by running existing tests
2. Add missing test cases for complete coverage
3. Update PLAN-0.1.0.1 to mark items as complete
4. Close this task

---

## 5. Summary

| Item | Status |
|------|--------|
| Keywords in Lexer.cs | ✅ All 41 keywords present |
| TokenTypes in Token.cs | ✅ All keyword types present |
| Lexer → TokenType mapping | ✅ 1:1 correspondence verified |
| Test coverage | ⚠️ 13 keywords not tested |
| Implementation needed | ❌ None |
| Tests needed | ✅ Add 13 keyword test cases |

---

## 6. Recommended Actions

1. **Verify current state**: Run `dotnet test` to confirm all existing tests pass
2. **Add missing tests**: Extend the keyword Theory test with 13 new InlineData entries
3. **Mark complete**: Update the PLAN-0.1.0.1 checklist to reflect actual state
4. **Close task**: This task can be closed as "already implemented"

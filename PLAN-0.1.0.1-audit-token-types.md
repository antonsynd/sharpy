# Implementation Plan: Task 0.1.0.1 - Audit Existing Token Types

## Overview
Audit the existing `TokenType` enum in `Token.cs` against the language specification to identify any missing or extra token types.

---

## 1. Step-by-Step Implementation Approach

### Step 1: Document Current State
The `TokenType` enum in `src/Sharpy.Compiler/Lexer/Token.cs` currently defines **82 token types** across these categories:
- Literals (11): Integer, Float, String, RawString, FString*, True, False, None
- Identifiers/Keywords (33): Identifier + 32 keyword tokens
- Operators (31): Arithmetic, Comparison, Bitwise, Assignment, Special
- Delimiters (14): Parentheses, brackets, braces, punctuation
- Special (5): Newline, Indent, Dedent, Eof, Comment

### Step 2: Compare Against Spec Keywords

**Spec Keywords (from `docs/language_specification/keywords.md`):**

| Spec Keyword | Token.cs Status | Notes |
|--------------|-----------------|-------|
| `and` | ✅ `And` | Boolean AND |
| `as` | ✅ `As` | Aliasing |
| `assert` | ✅ `Assert` | Assertion |
| `auto` | ✅ `Auto` | Type inference |
| `break` | ✅ `Break` | Loop control |
| `case` | ❌ **MISSING** | Pattern matching |
| `class` | ✅ `Class` | Class decl |
| `const` | ✅ `Const` | Constant decl |
| `continue` | ✅ `Continue` | Loop control |
| `def` | ✅ `Def` | Function def |
| `elif` | ✅ `Elif` | Else-if |
| `else` | ✅ `Else` | Else block |
| `enum` | ✅ `Enum` | Enum decl |
| `event` | ❌ **MISSING** | Event decl |
| `except` | ✅ `Except` | Exception handler |
| `False` | ✅ `False` | Boolean literal |
| `finally` | ✅ `Finally` | Finally block |
| `for` | ✅ `For` | For loop |
| `from` | ✅ `From` | Imports |
| `if` | ✅ `If` | Conditional |
| `import` | ✅ `Import` | Import |
| `in` | ✅ `In` | Membership |
| `interface` | ✅ `Interface` | Interface decl |
| `is` | ✅ `Is` | Identity |
| `lambda` | ✅ `Lambda` | Lambda expr |
| `match` | ❌ **MISSING** | Pattern matching |
| `maybe` | ❌ **MISSING** | Optional expressions |
| `None` | ✅ `None` | None literal |
| `not` | ✅ `Not` | Boolean NOT |
| `or` | ✅ `Or` | Boolean OR |
| `pass` | ✅ `Pass` | No-op |
| `property` | ❌ **MISSING** | Property decl |
| `raise` | ✅ `Raise` | Raise exception |
| `return` | ✅ `Return` | Return |
| `struct` | ✅ `Struct` | Struct decl |
| `True` | ✅ `True` | Boolean literal |
| `to` | ❌ **MISSING** | Type coercion |
| `try` | ✅ `Try` | Try block |
| `type` | ❌ **MISSING** | Type alias |
| `while` | ✅ `While` | While loop |
| `with` | ✅ `With` | Context manager |
| `yield` | ❌ **MISSING** | Generators |
| `async` | ❌ **MISSING** | Async programming |
| `await` | ❌ **MISSING** | Async programming |
| `del` | ❌ **MISSING** | Delete statement |

**Future/Reserved Keywords (not currently needed):**
| Keyword | Status | Notes |
|---------|--------|-------|
| `defer` | Not needed | Future |
| `do` | Not needed | Future |

### Step 3: Identify Gaps

**Missing Keywords (11 total):**
1. `case` - Pattern matching
2. `event` - Event declaration
3. `match` - Pattern matching
4. `maybe` - Optional expressions
5. `property` - Property declaration
6. `to` - Type coercion operator
7. `type` - Type alias declaration
8. `yield` - Generators
9. `async` - Async programming
10. `await` - Async programming
11. `del` - Delete statement

### Step 4: Verify Required Keywords from Task Description

**Task-specified keywords check:**
- [x] `def` - Present
- [x] `class` - Present
- [x] `struct` - Present
- [x] `interface` - Present
- [x] `enum` - Present
- [x] `if` - Present
- [x] `elif` - Present
- [x] `else` - Present
- [x] `while` - Present
- [x] `for` - Present
- [x] `in` - Present
- [x] `break` - Present
- [x] `continue` - Present
- [x] `return` - Present
- [x] `pass` - Present

**All task-specified keywords are already implemented!**

---

## 2. Key Files to Modify

### Primary File
- `src/Sharpy.Compiler/Lexer/Token.cs` - Add missing TokenType enum values

### Secondary Files (will need updates after Token.cs changes)
- `src/Sharpy.Compiler/Lexer/Lexer.cs` - Add keyword mappings for new tokens
- `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs` - Add tests for new keywords

---

## 3. Tests to Verify

### Existing Tests
The current test suite in `LexerTests.cs` already tests all implemented keywords via the theory test at lines 57-97:
```csharp
[Theory]
[InlineData("def", TokenType.Def)]
[InlineData("class", TokenType.Class)]
// ... all current keywords tested
```

### New Tests Required
For each missing keyword, add to the `[Theory]` test:
```csharp
[InlineData("case", TokenType.Case)]
[InlineData("event", TokenType.Event)]
[InlineData("match", TokenType.Match)]
[InlineData("maybe", TokenType.Maybe)]
[InlineData("property", TokenType.Property)]
[InlineData("to", TokenType.To)]
[InlineData("type", TokenType.Type)]
[InlineData("yield", TokenType.Yield)]
[InlineData("async", TokenType.Async)]
[InlineData("await", TokenType.Await)]
[InlineData("del", TokenType.Del)]
```

### Additional Test Scenarios
1. **Soft keywords test**: Verify `_`, `get`, `init`, `set` are NOT reserved keywords (tokenize as identifiers)
2. **Keyword in identifier context**: Ensure `classname`, `define`, etc. tokenize as identifiers not keywords
3. **Case sensitivity**: Verify `Case`, `CASE`, `cAsE` tokenize as identifiers (only lowercase is keyword)

---

## 4. Potential Risks and Questions

### Risks
1. **Parser dependencies**: Adding new token types may require parser updates if any code assumes exhaustive token matching
2. **Breaking changes**: If any external code depends on TokenType enum ordering (should not, but worth checking)
3. **Lexer keyword map**: The Lexer.cs likely has a dictionary mapping strings to TokenTypes - must be updated in sync

### Questions to Resolve
1. **Soft keywords**: Should `get`, `set`, `init` be added as tokens or handled contextually in parser?
   - Recommendation: Keep as identifiers, handle in parser (current approach is correct)

2. **Versioning**: Some keywords (async/await, yield) may be for future versions - should they be added now?
   - Recommendation: Add all spec keywords now for forward compatibility. The parser can report "not yet implemented" errors.

3. **`to` operator**: Is this a keyword or infix operator like `is`?
   - Per spec: `to` is a type coercion operator, similar to `is` - should be a keyword token

### Implementation Order
1. Add missing enum values to `Token.cs` (no code changes, just declarations)
2. Update keyword mapping in `Lexer.cs`
3. Add tests to `LexerTests.cs`
4. Run test suite to verify

---

## 5. Summary Checklist

- [x] Audit complete - 11 missing keywords identified
- [ ] Add `Case`, `Event`, `Match`, `Maybe`, `Property`, `To`, `Type`, `Yield`, `Async`, `Await`, `Del` to TokenType enum
- [ ] Update Lexer keyword dictionary
- [ ] Add unit tests for all new keywords
- [ ] Verify existing tests still pass
- [ ] Document any parser updates needed

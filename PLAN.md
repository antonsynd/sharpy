# Implementation Plan: Task 0.1.0.1 - Audit Existing Token Types

## Task Summary
Audit the existing `TokenType` enum in `Token.cs` against the Sharpy language specification to verify all required keywords are implemented.

---

## 1. Step-by-Step Implementation Approach

### Step 1: Audit Current Implementation (Complete)

**Analysis of `src/Sharpy.Compiler/Lexer/Token.cs`:**

The `TokenType` enum (lines 6-135) contains:
- **Literals:** Integer, Float, String, RawString, FString tokens, True, False, None
- **Keywords:** 34 hard keywords implemented
- **Operators:** Full arithmetic, comparison, bitwise, and assignment operators
- **Delimiters:** Parentheses, brackets, braces, punctuation
- **Special:** Newline, Indent, Dedent, Eof, Comment

**Analysis of `src/Sharpy.Compiler/Lexer/Lexer.cs`:**

The `Keywords` dictionary (lines 34-77) maps 34 keyword strings to their TokenTypes.

### Step 2: Compare Against Specification

**Source:** `docs/language_specification/keywords.md`

#### Task-Specific Requirements (All Implemented)

| Category | Required Keywords | Status |
|----------|-------------------|--------|
| Definitions | `def`, `class`, `struct`, `interface`, `enum` | ✅ All Implemented |
| Control Flow | `if`, `elif`, `else`, `while`, `for`, `in` | ✅ All Implemented |
| Statements | `break`, `continue`, `return`, `pass` | ✅ All Implemented |

#### Full Spec Hard Keywords Audit

| # | Keyword | In Spec | In Token.cs | In Lexer.cs | Status |
|---|---------|---------|-------------|-------------|--------|
| 1 | `and` | ✅ | ✅ And | ✅ | Complete |
| 2 | `as` | ✅ | ✅ As | ✅ | Complete |
| 3 | `assert` | ✅ | ✅ Assert | ✅ | Complete |
| 4 | `async` | ✅ | ❌ | ❌ | **Missing** |
| 5 | `auto` | ✅ | ✅ Auto | ✅ | Complete |
| 6 | `await` | ✅ | ❌ | ❌ | **Missing** |
| 7 | `break` | ✅ | ✅ Break | ✅ | Complete |
| 8 | `case` | ✅ | ❌ | ❌ | **Missing** |
| 9 | `class` | ✅ | ✅ Class | ✅ | Complete |
| 10 | `const` | ✅ | ✅ Const | ✅ | Complete |
| 11 | `continue` | ✅ | ✅ Continue | ✅ | Complete |
| 12 | `def` | ✅ | ✅ Def | ✅ | Complete |
| 13 | `del` | ✅ | ❌ | ❌ | **Missing** |
| 14 | `elif` | ✅ | ✅ Elif | ✅ | Complete |
| 15 | `else` | ✅ | ✅ Else | ✅ | Complete |
| 16 | `enum` | ✅ | ✅ Enum | ✅ | Complete |
| 17 | `event` | ✅ | ❌ | ❌ | **Missing** |
| 18 | `except` | ✅ | ✅ Except | ✅ | Complete |
| 19 | `False` | ✅ | ✅ False | ✅ | Complete |
| 20 | `finally` | ✅ | ✅ Finally | ✅ | Complete |
| 21 | `for` | ✅ | ✅ For | ✅ | Complete |
| 22 | `from` | ✅ | ✅ From | ✅ | Complete |
| 23 | `if` | ✅ | ✅ If | ✅ | Complete |
| 24 | `import` | ✅ | ✅ Import | ✅ | Complete |
| 25 | `in` | ✅ | ✅ In | ✅ | Complete |
| 26 | `interface` | ✅ | ✅ Interface | ✅ | Complete |
| 27 | `is` | ✅ | ✅ Is | ✅ | Complete |
| 28 | `lambda` | ✅ | ✅ Lambda | ✅ | Complete |
| 29 | `match` | ✅ | ❌ | ❌ | **Missing** |
| 30 | `maybe` | ✅ | ❌ | ❌ | **Missing** |
| 31 | `None` | ✅ | ✅ None | ✅ | Complete |
| 32 | `not` | ✅ | ✅ Not | ✅ | Complete |
| 33 | `or` | ✅ | ✅ Or | ✅ | Complete |
| 34 | `pass` | ✅ | ✅ Pass | ✅ | Complete |
| 35 | `property` | ✅ | ❌ | ❌ | **Missing** |
| 36 | `raise` | ✅ | ✅ Raise | ✅ | Complete |
| 37 | `return` | ✅ | ✅ Return | ✅ | Complete |
| 38 | `struct` | ✅ | ✅ Struct | ✅ | Complete |
| 39 | `to` | ✅ | ❌ | ❌ | **Missing** |
| 40 | `True` | ✅ | ✅ True | ✅ | Complete |
| 41 | `try` | ✅ | ✅ Try | ✅ | Complete |
| 42 | `type` | ✅ | ❌ | ❌ | **Missing** |
| 43 | `while` | ✅ | ✅ While | ✅ | Complete |
| 44 | `with` | ✅ | ✅ With | ✅ | Complete |
| 45 | `yield` | ✅ | ❌ | ❌ | **Missing** |

**Summary:** 34/45 implemented, **11 missing**

### Step 3: Implementation Changes Required

#### 3a. Add Missing TokenTypes to Token.cs

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`
**Location:** After line 47 (after `With` keyword)

```csharp
// Keywords - Pattern Matching (future: v0.3+)
Case,
Match,

// Keywords - Async/Generators (future: v0.4+)
Async,
Await,
Yield,

// Keywords - Type System (future: v0.2+)
Del,
Event,
Maybe,
Property,
To,
Type,
```

#### 3b. Update Keywords Dictionary in Lexer.cs

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`
**Location:** After line 57 (after `with` entry)

```csharp
// Pattern Matching
{ "case", TokenType.Case },
{ "match", TokenType.Match },

// Async/Generators
{ "async", TokenType.Async },
{ "await", TokenType.Await },
{ "yield", TokenType.Yield },

// Type System
{ "del", TokenType.Del },
{ "event", TokenType.Event },
{ "maybe", TokenType.Maybe },
{ "property", TokenType.Property },
{ "to", TokenType.To },
{ "type", TokenType.Type },
```

#### 3c. Add Tests for New Keywords

**File:** `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs`
**Location:** Add to `Tokenize_Keyword_ReturnsCorrectToken` theory (line 57-97)

```csharp
[InlineData("case", TokenType.Case)]
[InlineData("match", TokenType.Match)]
[InlineData("async", TokenType.Async)]
[InlineData("await", TokenType.Await)]
[InlineData("yield", TokenType.Yield)]
[InlineData("del", TokenType.Del)]
[InlineData("event", TokenType.Event)]
[InlineData("maybe", TokenType.Maybe)]
[InlineData("property", TokenType.Property)]
[InlineData("to", TokenType.To)]
[InlineData("type", TokenType.Type)]
```

---

## 2. Key Files to Modify

| File | Line Range | Changes |
|------|------------|---------|
| `src/Sharpy.Compiler/Lexer/Token.cs` | 47-48 | Add 11 new TokenType enum values |
| `src/Sharpy.Compiler/Lexer/Lexer.cs` | 57-58 | Add 11 new keyword mappings |
| `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs` | 57-97 | Add 11 new InlineData test cases |

---

## 3. Tests to Verify

### Build Verification
```bash
dotnet build src/Sharpy.Compiler
```

### Run Lexer Tests
```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~LexerTests"
```

### Specific Verifications
1. Each new keyword produces its corresponding TokenType
2. Keywords as prefixes remain identifiers (e.g., `async_func` -> Identifier)
3. Case sensitivity preserved (e.g., `Async` -> Identifier, `async` -> Async)
4. Existing tests continue to pass

---

## 4. Potential Risks or Questions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Breaking Changes** | Code using new keywords as identifiers will break | Expected behavior; document in release notes |
| **Parser Incompatibility** | Parser may not handle new token types | Parser updates are separate tasks |
| **Test Coverage** | New keywords may need additional edge case tests | Add tests in Step 3c |

### Questions for Clarification

1. **Scope:** Should all 11 missing keywords be added now, or incrementally as features are implemented?
   - **Recommendation:** Add all now to reserve them as keywords, even if parser support comes later.

2. **Future Keywords:** Should `defer` and `do` (listed as reserved in spec) be added to TokenType?
   - **Recommendation:** No, wait until they are promoted to hard keywords.

3. **Soft Keywords:** The spec lists `_`, `get`, `set`, `init` as soft (context-dependent) keywords.
   - **Confirmation needed:** These should remain as Identifiers in the lexer, with context handling in the parser.

---

## 5. Conclusion

### Task Status

| Requirement | Status |
|-------------|--------|
| v0.1 Required Keywords (per task description) | ✅ **Complete** |
| Full Spec Compliance | ⚠️ **11 Keywords Missing** |

### Recommendation

The task's specific v0.1 requirements are **already satisfied**. All keywords listed in the task description (`def`, `class`, `struct`, `interface`, `enum`, `if`, `elif`, `else`, `while`, `for`, `in`, `break`, `continue`, `return`, `pass`) are implemented and tested.

**For full spec compliance**, add the 11 missing keywords. This can be done:
- **Now:** To reserve all keywords upfront (recommended)
- **Later:** Incrementally as features are implemented

The missing keywords are for features planned in v0.2+:
- Pattern matching: `case`, `match`
- Async programming: `async`, `await`
- Generators: `yield`
- Type system: `del`, `event`, `maybe`, `property`, `to`, `type`

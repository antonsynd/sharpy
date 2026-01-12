# Implementation Plan: Task 0.1.0.1 - Audit Existing Token Types

## Task Summary
Audit the existing `TokenType` enum in `Token.cs` against the Sharpy language specification to verify all required keywords are implemented.

## Analysis Results

### Task-Specific Requirements (All Implemented)

The task explicitly requires these keywords - **all are already implemented**:

| Category | Keywords | Status |
|----------|----------|--------|
| Definitions | `def`, `class`, `struct`, `interface`, `enum` | All Implemented |
| Control Flow | `if`, `elif`, `else`, `while`, `for`, `in` | All Implemented |
| Statements | `break`, `continue`, `return`, `pass` | All Implemented |

### Full Spec Compliance Check

Comparing `Token.cs` (lines 26-57) and `Lexer.cs` (lines 34-77) against `docs/language_specification/keywords.md`:

#### Implemented (34 keywords)
`and`, `as`, `assert`, `auto`, `break`, `class`, `const`, `continue`, `def`, `elif`, `else`, `enum`, `except`, `False`, `finally`, `for`, `from`, `if`, `import`, `in`, `interface`, `is`, `lambda`, `None`, `not`, `or`, `pass`, `raise`, `return`, `struct`, `True`, `try`, `while`, `with`

#### Missing (11 keywords)
| Keyword | Purpose | Priority |
|---------|---------|----------|
| `async` | Async programming | v0.5+ |
| `await` | Async programming | v0.5+ |
| `case` | Pattern matching | v0.3+ |
| `del` | Delete statement | v0.2+ |
| `event` | Event declaration | v0.4+ |
| `match` | Pattern matching | v0.3+ |
| `maybe` | Optional expressions | v0.2+ |
| `property` | Property declaration | v0.2+ |
| `to` | Type coercion | v0.2+ |
| `type` | Type alias | v0.2+ |
| `yield` | Generators | v0.4+ |

#### Future/Reserved (not in current spec)
`defer`, `do` - Not needed yet

---

## Step-by-Step Implementation Approach

### Step 1: Add Missing TokenTypes to Token.cs

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`
**Location:** After line 47 (after `With` keyword section)

```csharp
// Keywords - Pattern Matching
Case,
Match,

// Keywords - Advanced Features
Async,
Await,
Del,
Event,
Maybe,
Property,
To,
Type,
Yield,
```

### Step 2: Update Keywords Dictionary in Lexer.cs

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`
**Location:** In the `Keywords` dictionary (around line 57)

```csharp
// Pattern Matching
{ "case", TokenType.Case },
{ "match", TokenType.Match },

// Advanced Features
{ "async", TokenType.Async },
{ "await", TokenType.Await },
{ "del", TokenType.Del },
{ "event", TokenType.Event },
{ "maybe", TokenType.Maybe },
{ "property", TokenType.Property },
{ "to", TokenType.To },
{ "type", TokenType.Type },
{ "yield", TokenType.Yield },
```

### Step 3: Add Tests for New Keywords

**File:** `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs`
**Location:** In the Keywords region, add to `Tokenize_Keyword_ReturnsCorrectToken` theory

```csharp
[InlineData("case", TokenType.Case)]
[InlineData("match", TokenType.Match)]
[InlineData("async", TokenType.Async)]
[InlineData("await", TokenType.Await)]
[InlineData("del", TokenType.Del)]
[InlineData("event", TokenType.Event)]
[InlineData("maybe", TokenType.Maybe)]
[InlineData("property", TokenType.Property)]
[InlineData("to", TokenType.To)]
[InlineData("type", TokenType.Type)]
[InlineData("yield", TokenType.Yield)]
```

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Lexer/Token.cs` | Add 11 new TokenType enum values |
| `src/Sharpy.Compiler/Lexer/Lexer.cs` | Add 11 new keyword mappings |
| `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs` | Add 11 new test cases |

---

## Tests to Verify

1. **Build the project:**
   ```bash
   dotnet build src/Sharpy.Compiler
   ```

2. **Run lexer tests:**
   ```bash
   dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~LexerTests"
   ```

3. **Verify new keywords tokenize correctly:**
   - Each new keyword should produce its corresponding TokenType
   - Keywords followed by alphanumeric characters should still be identifiers (e.g., `async_func` -> Identifier)

---

## Potential Risks

1. **Parser Compatibility**
   - Adding lexer tokens doesn't mean the parser supports them
   - Parser updates are separate tasks

2. **Breaking Changes**
   - Code using new keywords as identifiers will break (e.g., `type = 5`)
   - This is expected behavior for reserved keywords

3. **Soft Keywords Not Included**
   - `_`, `get`, `set`, `init` remain as identifiers (context-dependent)
   - Parser handles their special meaning

---

## Questions to Clarify

1. **Scope:** Should all 11 missing keywords be added now, or only as their features are implemented?

2. **Future Keywords:** Should `defer` and `do` be reserved now even though they're not in the current spec?

3. **Soft Keywords:** Confirm that `get`, `set`, `init` should remain as identifiers rather than becoming hard keywords.

---

## Conclusion

**Task Status:** The specific v0.1 requirements are **already complete**. All keywords listed in the task description are implemented and tested.

**Recommendation:** Add the 11 missing spec keywords to achieve full specification compliance, but this can be done incrementally as features are implemented.

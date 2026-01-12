# Implementation Plan: Task 0.1.0.1 - Audit Existing Token Types

## Task Summary
Audit the existing `TokenType` enum in `Token.cs` against the Sharpy language specification to verify all required keywords are implemented.

## Analysis Results

### Current State (Token.cs)
The lexer already has a comprehensive set of token types implemented:

**Implemented Keywords in TokenType enum (lines 26-57):**
- Control flow: `Def`, `Class`, `Struct`, `Interface`, `Enum`, `If`, `Else`, `Elif`, `While`, `For`, `In`, `Return`, `Break`, `Continue`, `Pass`, `Try`, `Except`, `Finally`, `Raise`, `Assert`, `With`
- Import: `Import`, `From`, `As`
- Type/Value: `Auto`, `Const`, `Lambda`
- Boolean operators: `And`, `Or`, `Not`, `Is`
- Literals: `True`, `False`, `None`

**Implemented in Lexer.cs Keywords dictionary (lines 34-77):**
All the above TokenTypes are properly mapped to their string representations.

### Specification Requirements (from `docs/language_specification/keywords.md`)

**Hard Keywords Required:**
| Keyword | Status | TokenType |
|---------|--------|-----------|
| `and` | ✅ Implemented | `And` |
| `as` | ✅ Implemented | `As` |
| `assert` | ✅ Implemented | `Assert` |
| `auto` | ✅ Implemented | `Auto` |
| `break` | ✅ Implemented | `Break` |
| `case` | ❌ **MISSING** | - |
| `class` | ✅ Implemented | `Class` |
| `const` | ✅ Implemented | `Const` |
| `continue` | ✅ Implemented | `Continue` |
| `def` | ✅ Implemented | `Def` |
| `elif` | ✅ Implemented | `Elif` |
| `else` | ✅ Implemented | `Else` |
| `enum` | ✅ Implemented | `Enum` |
| `event` | ❌ **MISSING** | - |
| `except` | ✅ Implemented | `Except` |
| `False` | ✅ Implemented | `False` |
| `finally` | ✅ Implemented | `Finally` |
| `for` | ✅ Implemented | `For` |
| `from` | ✅ Implemented | `From` |
| `if` | ✅ Implemented | `If` |
| `import` | ✅ Implemented | `Import` |
| `in` | ✅ Implemented | `In` |
| `interface` | ✅ Implemented | `Interface` |
| `is` | ✅ Implemented | `Is` |
| `lambda` | ✅ Implemented | `Lambda` |
| `match` | ❌ **MISSING** | - |
| `maybe` | ❌ **MISSING** | - |
| `None` | ✅ Implemented | `None` |
| `not` | ✅ Implemented | `Not` |
| `or` | ✅ Implemented | `Or` |
| `pass` | ✅ Implemented | `Pass` |
| `property` | ❌ **MISSING** | - |
| `raise` | ✅ Implemented | `Raise` |
| `return` | ✅ Implemented | `Return` |
| `struct` | ✅ Implemented | `Struct` |
| `True` | ✅ Implemented | `True` |
| `to` | ❌ **MISSING** | - |
| `try` | ✅ Implemented | `Try` |
| `type` | ❌ **MISSING** | - |
| `while` | ✅ Implemented | `While` |
| `with` | ✅ Implemented | `With` |
| `yield` | ❌ **MISSING** | - |
| `async` | ❌ **MISSING** | - |
| `await` | ❌ **MISSING** | - |
| `del` | ❌ **MISSING** | - |

**Future Keywords (reserved but not implemented):**
| Keyword | Status |
|---------|--------|
| `defer` | Not needed yet |
| `do` | Not needed yet |

---

## Step-by-Step Implementation Approach

### Step 1: Add Missing TokenTypes to Token.cs
Add the following missing token types to the `TokenType` enum:

```csharp
// Keywords - Pattern Matching
Case,
Match,

// Keywords - Advanced Features
Event,
Property,
Type,
Maybe,
To,
Yield,
Async,
Await,
Del,
```

**Location:** `src/Sharpy.Compiler/Lexer/Token.cs` after line 47 (after `With`)

### Step 2: Update Keywords Dictionary in Lexer.cs
Add mappings for the new keywords in the `Keywords` dictionary:

```csharp
// Pattern Matching
{ "case", TokenType.Case },
{ "match", TokenType.Match },

// Advanced Features
{ "event", TokenType.Event },
{ "property", TokenType.Property },
{ "type", TokenType.Type },
{ "maybe", TokenType.Maybe },
{ "to", TokenType.To },
{ "yield", TokenType.Yield },
{ "async", TokenType.Async },
{ "await", TokenType.Await },
{ "del", TokenType.Del },
```

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs` in the `Keywords` dictionary (around line 57)

### Step 3: Add Tests for New Keywords
Add test cases to verify the new keywords are tokenized correctly:

**Location:** `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs`

Add to the existing keyword test theory:
```csharp
[InlineData("case", TokenType.Case)]
[InlineData("match", TokenType.Match)]
[InlineData("event", TokenType.Event)]
[InlineData("property", TokenType.Property)]
[InlineData("type", TokenType.Type)]
[InlineData("maybe", TokenType.Maybe)]
[InlineData("to", TokenType.To)]
[InlineData("yield", TokenType.Yield)]
[InlineData("async", TokenType.Async)]
[InlineData("await", TokenType.Await)]
[InlineData("del", TokenType.Del)]
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

1. **Unit Tests:** Run the existing lexer test suite to ensure no regressions
   ```bash
   dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~LexerTests"
   ```

2. **New Keyword Tests:** Verify each new keyword tokenizes correctly
   - `case` -> `TokenType.Case`
   - `match` -> `TokenType.Match`
   - `event` -> `TokenType.Event`
   - `property` -> `TokenType.Property`
   - `type` -> `TokenType.Type`
   - `maybe` -> `TokenType.Maybe`
   - `to` -> `TokenType.To`
   - `yield` -> `TokenType.Yield`
   - `async` -> `TokenType.Async`
   - `await` -> `TokenType.Await`
   - `del` -> `TokenType.Del`

3. **Build Verification:** Ensure the project compiles without errors
   ```bash
   dotnet build src/Sharpy.Compiler
   ```

---

## Potential Risks and Questions

### Risks

1. **Parser Impact:** Adding new keywords to the lexer doesn't automatically mean the parser supports them. The parser will need separate updates to handle `match/case`, `async/await`, etc.

2. **Breaking Changes:** If any user code uses these keywords as identifiers (e.g., `type = 5`), it will now fail to compile. However, since these are specified as reserved keywords, this is expected behavior.

3. **Soft Keywords:** The spec mentions soft keywords (`_`, `get`, `init`, `set`) that are context-dependent. These are NOT being added as hard keywords and should remain as identifiers that the parser handles contextually.

### Questions for Clarification

1. **Priority:** Should all 11 missing keywords be added in this task, or should we prioritize based on the v0.1 milestone requirements?
   - The task description specifically mentions: `def`, `class`, `struct`, `interface`, `enum`, `if`, `elif`, `else`, `while`, `for`, `in`, `break`, `continue`, `return`, `pass`
   - All of these are **already implemented**

2. **Future Keywords:** Should `defer` and `do` (listed as "Future Keywords" in the spec) be added now as reserved but non-functional keywords?

3. **Soft Keywords:** Should `get`, `set`, `init` be handled at the lexer level or remain as identifiers for the parser to interpret contextually?

---

## Summary

**Good News:** The task's specific requirements are already satisfied. All keywords listed in the task description are already implemented:
- ✅ `def`, `class`, `struct`, `interface`, `enum`
- ✅ `if`, `elif`, `else`, `while`, `for`, `in`
- ✅ `break`, `continue`, `return`, `pass`

**Action Items:** The 11 missing keywords from the full spec should be added to ensure complete spec compliance, but this may be a separate task since the core v0.1 keywords are already present.

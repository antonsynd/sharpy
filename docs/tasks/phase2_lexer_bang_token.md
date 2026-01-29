# Phase 2: Lexer Updates — Bang Token

## Overview

This phase adds the `Bang` (`!`) token to the lexer, which is needed for the `T !E` result type syntax. Currently `!` only appears as part of `!=` (not equal), so we need to handle standalone `!`.

**Prerequisites:** None (can be done in parallel with Phase 1)

**Files to modify:**
- `src/Sharpy.Compiler/Lexer/Token.cs`
- `src/Sharpy.Compiler/Lexer/Lexer.cs`

**Files to create:**
- `src/Sharpy.Compiler.Tests/Lexer/BangTokenTests.cs`

---

## Task 2.1: Add Bang Token Type

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`

### Steps

- [x] Open `src/Sharpy.Compiler/Lexer/Token.cs`
- [x] Find the `TokenType` enum
- [x] Locate the "Operators - Comparison" section (around line 100-107)
- [x] Add `Bang` token after `NotEqual`:
  ```csharp
  // Operators - Comparison
  Equal,          // ==
  NotEqual,       // !=
  Bang,           // ! (standalone, for T !E result type syntax)
  Less,           // <
  ```
- [x] Save the file

### Verification

- [x] Build the compiler: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/Lexer/Token.cs
git commit -m "lexer: add Bang token type for T !E syntax"
```

---

## Task 2.2: Implement Bang Token Lexing

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`

### Steps

- [x] Open `src/Sharpy.Compiler/Lexer/Lexer.cs`
- [x] Find the method that handles operator characters (likely `ScanToken` or similar)
- [x] Locate the case that handles `!` (currently only produces `NotEqual` for `!=`)
- [x] Modify to handle standalone `!`:
  ```csharp
  case '!':
      if (Peek() == '=')
      {
          Advance();
          return MakeToken(TokenType.NotEqual, "!=");
      }
      return MakeToken(TokenType.Bang, "!");
  ```
- [x] Ensure `Peek()` checks the next character without consuming it
- [x] Ensure `Advance()` moves to the next character
- [x] Ensure `MakeToken()` creates a token with proper position tracking

### Important Notes

- The `!` character must NOT be consumed when checking for `!=`
- Position tracking must be correct for both `!=` and standalone `!`
- If the current implementation uses a different pattern, adapt accordingly

### Verification

- [x] Build the compiler: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/Lexer/Lexer.cs
git commit -m "lexer: implement Bang token lexing for standalone !"
```

---

## Task 2.3: Add Unit Tests for Bang Token

**File:** `src/Sharpy.Compiler.Tests/Lexer/BangTokenTests.cs`

### Steps

- [x] Create new file `src/Sharpy.Compiler.Tests/Lexer/BangTokenTests.cs`
- [x] Add test class `BangTokenTests`
- [x] Add using statements:
  ```csharp
  using Sharpy.Compiler.Lexer;
  using Xunit;
  ```
- [x] Add tests:

#### Test: Standalone Bang Token
```csharp
[Fact]
public void Lexer_StandaloneBang_ProducesBangToken()
{
    var lexer = new Lexer("!");
    var tokens = lexer.Tokenize();
    
    Assert.Equal(2, tokens.Count); // Bang + EOF
    Assert.Equal(TokenType.Bang, tokens[0].Type);
    Assert.Equal("!", tokens[0].Value);
}
```

#### Test: Bang in Type Annotation Context
```csharp
[Fact]
public void Lexer_BangInTypeAnnotation_ProducesBangToken()
{
    var lexer = new Lexer("int !ValueError");
    var tokens = lexer.Tokenize();
    
    // Should produce: Identifier(int), Bang, Identifier(ValueError), EOF
    Assert.Equal(4, tokens.Count);
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
    Assert.Equal(TokenType.Bang, tokens[1].Type);
    Assert.Equal(TokenType.Identifier, tokens[2].Type);
}
```

#### Test: NotEqual Still Works
```csharp
[Fact]
public void Lexer_NotEqual_StillProducesNotEqualToken()
{
    var lexer = new Lexer("a != b");
    var tokens = lexer.Tokenize();
    
    // Should produce: Identifier(a), NotEqual, Identifier(b), EOF
    Assert.Equal(4, tokens.Count);
    Assert.Equal(TokenType.NotEqual, tokens[1].Type);
    Assert.Equal("!=", tokens[1].Value);
}
```

#### Test: Bang Followed by Equals (Separate Tokens)
```csharp
[Fact]
public void Lexer_BangSpaceEquals_ProducesSeparateTokens()
{
    var lexer = new Lexer("! =");
    var tokens = lexer.Tokenize();
    
    // Should produce: Bang, Assign, EOF
    Assert.Equal(3, tokens.Count);
    Assert.Equal(TokenType.Bang, tokens[0].Type);
    Assert.Equal(TokenType.Assign, tokens[1].Type);
}
```

#### Test: Position Tracking for Bang
```csharp
[Fact]
public void Lexer_BangToken_HasCorrectPosition()
{
    var lexer = new Lexer("int !E", trackPositions: true);
    var tokens = lexer.Tokenize();
    
    var bangToken = tokens[1];
    Assert.Equal(TokenType.Bang, bangToken.Type);
    Assert.Equal(4, bangToken.Position); // 0-indexed, after "int "
    Assert.Equal(1, bangToken.Length);
}
```

#### Test: Complex Type with Bang
```csharp
[Fact]
public void Lexer_ComplexTypeWithBang_TokenizesCorrectly()
{
    var lexer = new Lexer("Result[int, str] !IOError | None");
    var tokens = lexer.Tokenize();
    
    // Find the Bang token
    var bangIndex = tokens.FindIndex(t => t.Type == TokenType.Bang);
    Assert.True(bangIndex > 0, "Bang token should be present");
    Assert.Equal(TokenType.Bang, tokens[bangIndex].Type);
}
```

### Verification

- [x] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter BangTokenTests`
- [x] All tests pass

```
git add src/Sharpy.Compiler.Tests/Lexer/BangTokenTests.cs
git commit -m "test: add unit tests for Bang token lexing"
```

---

## Task 2.4: Verify Existing Tests Still Pass

### Steps

- [x] Run all lexer tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Lexer"`
- [x] Verify no regressions in existing tests
- [x] If any tests fail, investigate and fix

### Common Issues to Watch For

1. **Tests that explicitly count tokens** — adding a new token type shouldn't break these, but verify
2. **Tests for `!=` operator** — ensure these still pass
3. **Position tracking tests** — verify positions are still correct

### Verification

- [x] All lexer tests pass
- [x] No regressions introduced

```
# No commit needed if no fixes required
# If fixes were needed:
git add -A
git commit -m "fix: resolve test regressions from Bang token addition"
```

---

## Final Verification

- [x] Build entire compiler: `dotnet build src/Sharpy.Compiler`
- [x] Run all lexer tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Lexer"`
- [x] All tests pass
- [x] Review all commits in this phase

```
git log --oneline -3
```

Expected commits:
1. `lexer: add Bang token type for T !E syntax`
2. `lexer: implement Bang token lexing for standalone !`
3. `test: add unit tests for Bang token lexing`

---

## Notes for Implementer

- The `!` character is only used in two contexts in Sharpy:
  1. `!=` (not equal comparison operator)
  2. `T !E` (result type syntax in type annotations)
  
- The lexer doesn't need to understand context — it just produces tokens. The parser will determine whether `Bang` is valid in a given position.

- Make sure to check how the existing lexer handles multi-character operators. The pattern for `!=` should be similar to how `==`, `<=`, `>=`, etc. are handled.

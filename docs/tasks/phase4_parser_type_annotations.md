# Phase 4: Parser Updates for Type Annotations

## Overview

This phase updates the parser to handle the new type annotation syntax:
- `T?` → sets `IsOptional = true`
- `T | None` → sets `IsCSharpNullable = true`
- `T !E` → sets `ErrorType` to the parsed error type

**Prerequisites:** 
- Phase 2 (Bang token in lexer)
- Phase 3 (AST changes)

**Files to modify:**
- `src/Sharpy.Compiler/Parser/Parser.Types.cs`

**Files to create:**
- `src/Sharpy.Compiler.Tests/Parser/TypeAnnotationParserTests.cs`

---

## Task 4.1: Update ParseTypeAnnotation for T? Syntax

**File:** `src/Sharpy.Compiler/Parser/Parser.Types.cs`

### Context

The current parser already handles `T?` by setting `IsNullable = true`. After Phase 3, this was renamed to `IsOptional`. Verify this is working correctly.

### Steps

- [ ] Open `src/Sharpy.Compiler/Parser/Parser.Types.cs`
- [ ] Find the `ParseTypeAnnotation` method
- [ ] Locate where `T?` is handled (search for `TokenType.Question`)
- [ ] Verify it sets `IsOptional = true` (not `IsNullable`):
  ```csharp
  // Nullable/Optional type suffix T?
  if (Current.Type == TokenType.Question)
  {
      Advance();
      var endToken = Previous;
      // ...
      baseType = baseType with
      {
          IsOptional = true,  // Should be IsOptional after Phase 3
          // ... other fields ...
      };
  }
  ```
- [ ] If still using `IsNullable`, update to `IsOptional`

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] Run existing nullable tests to ensure `T?` still works

```
git add src/Sharpy.Compiler/Parser/Parser.Types.cs
git commit -m "parser: verify T? sets IsOptional correctly"
```

---

## Task 4.2: Add T !E Parsing (Result Type Syntax)

**File:** `src/Sharpy.Compiler/Parser/Parser.Types.cs`

### Steps

- [ ] Open `src/Sharpy.Compiler/Parser/Parser.Types.cs`
- [ ] Find the `ParseTypeAnnotation` method
- [ ] Locate the section that handles `T?` (after base type parsing, before return)
- [ ] Add handling for `T !E` **BEFORE** the `T?` check (because `!E` binds tighter):
  ```csharp
  // After parsing base type and array suffix...
  
  // Result type suffix T !E (binds tighter than ? and | None)
  if (Current.Type == TokenType.Bang)
  {
      var bangToken = Current;
      Advance(); // consume '!'
      
      // Parse the error type
      var errorType = ParseTypeAnnotation();
      
      var endToken = Previous;
      var endLine = endToken.Line;
      var endColumn = endToken.Column + endToken.Value.Length;
      
      baseType = baseType with
      {
          ErrorType = errorType,
          LineEnd = endLine,
          ColumnEnd = endColumn,
          Span = GetSpanFromTokens(startToken, endToken)
      };
  }
  
  // Optional type suffix T? (existing code)
  if (Current.Type == TokenType.Question)
  {
      // ... existing code ...
  }
  ```

### Precedence Note

The order of operations should be:
1. Parse base type (including generic args, array suffix)
2. Parse `!E` if present (result type)
3. Parse `?` if present (optional)
4. Parse `| None` if present (C# nullable)

This means:
- `int !ValueError?` → `Optional[Result[int, ValueError]]` (probably not desired, but parseable)
- `int !ValueError | None` → `Result[int, ValueError] | None` (nullable result)

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Parser/Parser.Types.cs
git commit -m "parser: add T !E result type syntax parsing"
```

---

## Task 4.3: Add T | None Parsing (C# Nullable Syntax)

**File:** `src/Sharpy.Compiler/Parser/Parser.Types.cs`

### Steps

- [ ] In `ParseTypeAnnotation`, after the `T?` handling, add `| None` handling:
  ```csharp
  // C# nullable suffix T | None
  if (Current.Type == TokenType.Pipe)
  {
      var pipeToken = Current;
      Advance(); // consume '|'
      
      // Must be followed by 'None'
      if (Current.Type != TokenType.None)
      {
          throw new ParserError(
              "Only '| None' is allowed for nullable types. " +
              "Free unions like 'int | str' are not supported. " +
              "Use 'union' declarations for custom sum types.",
              Current.Line, 
              Current.Column
          );
      }
      
      Advance(); // consume 'None'
      
      var endToken = Previous;
      var endLine = endToken.Line;
      var endColumn = endToken.Column + endToken.Value.Length;
      
      baseType = baseType with
      {
          IsCSharpNullable = true,
          LineEnd = endLine,
          ColumnEnd = endColumn,
          Span = GetSpanFromTokens(startToken, endToken)
      };
  }
  ```

### Error Message Quality

The error message should guide users:
- Explain that free unions aren't supported
- Point to `union` declarations for custom sum types
- Be clear that `| None` is the only valid `|` usage in type annotations

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Parser/Parser.Types.cs
git commit -m "parser: add T | None C# nullable syntax parsing"
```

---

## Task 4.4: Add Parser Unit Tests

**File:** `src/Sharpy.Compiler.Tests/Parser/TypeAnnotationParserTests.cs`

### Steps

- [ ] Create new file `src/Sharpy.Compiler.Tests/Parser/TypeAnnotationParserTests.cs`
- [ ] Add comprehensive tests:

```csharp
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

public class TypeAnnotationParserTests
{
    private TypeAnnotation ParseType(string source)
    {
        // Parse a variable declaration to get the type annotation
        var code = $"x: {source} = None";
        var lexer = new Lexer(code);
        var parser = new Parser(lexer.Tokenize());
        var module = parser.Parse();
        var stmt = module.Statements[0] as VariableDeclaration;
        return stmt!.TypeAnnotation!;
    }
    
    #region Basic Types
    
    [Fact]
    public void Parse_SimpleType_NoModifiers()
    {
        var type = ParseType("int");
        
        Assert.Equal("int", type.Name);
        Assert.False(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }
    
    [Fact]
    public void Parse_GenericType_NoModifiers()
    {
        var type = ParseType("list[int]");
        
        Assert.Equal("list", type.Name);
        Assert.Single(type.TypeArguments);
        Assert.Equal("int", type.TypeArguments[0].Name);
    }
    
    #endregion
    
    #region Optional (T?) Syntax
    
    [Fact]
    public void Parse_OptionalType_SetsIsOptional()
    {
        var type = ParseType("int?");
        
        Assert.Equal("int", type.Name);
        Assert.True(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }
    
    [Fact]
    public void Parse_OptionalGenericType_Works()
    {
        var type = ParseType("list[int]?");
        
        Assert.Equal("list", type.Name);
        Assert.True(type.IsOptional);
        Assert.Single(type.TypeArguments);
    }
    
    #endregion
    
    #region C# Nullable (T | None) Syntax
    
    [Fact]
    public void Parse_CSharpNullable_SetsIsCSharpNullable()
    {
        var type = ParseType("str | None");
        
        Assert.Equal("str", type.Name);
        Assert.False(type.IsOptional);
        Assert.True(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }
    
    [Fact]
    public void Parse_CSharpNullableGeneric_Works()
    {
        var type = ParseType("list[str] | None");
        
        Assert.Equal("list", type.Name);
        Assert.True(type.IsCSharpNullable);
    }
    
    [Fact]
    public void Parse_FreeUnion_ThrowsError()
    {
        Assert.Throws<ParserError>(() => ParseType("int | str"));
    }
    
    [Fact]
    public void Parse_FreeUnionWithNone_ThrowsError()
    {
        // int | str | None should fail at "int | str"
        Assert.Throws<ParserError>(() => ParseType("int | str | None"));
    }
    
    #endregion
    
    #region Result (T !E) Syntax
    
    [Fact]
    public void Parse_ResultType_SetsErrorType()
    {
        var type = ParseType("int !ValueError");
        
        Assert.Equal("int", type.Name);
        Assert.True(type.IsResult);
        Assert.NotNull(type.ErrorType);
        Assert.Equal("ValueError", type.ErrorType!.Name);
    }
    
    [Fact]
    public void Parse_ResultTypeGenericError_Works()
    {
        var type = ParseType("int !IOError[str]");
        
        Assert.True(type.IsResult);
        Assert.Equal("IOError", type.ErrorType!.Name);
        Assert.Single(type.ErrorType.TypeArguments);
    }
    
    [Fact]
    public void Parse_GenericResultType_Works()
    {
        var type = ParseType("list[int] !ParseError");
        
        Assert.Equal("list", type.Name);
        Assert.True(type.IsResult);
        Assert.Equal("ParseError", type.ErrorType!.Name);
    }
    
    #endregion
    
    #region Combined Modifiers
    
    [Fact]
    public void Parse_ResultWithCSharpNullable_Works()
    {
        // int !ValueError | None → Result[int, ValueError] | None
        var type = ParseType("int !ValueError | None");
        
        Assert.Equal("int", type.Name);
        Assert.True(type.IsResult);
        Assert.True(type.IsCSharpNullable);
        Assert.Equal("ValueError", type.ErrorType!.Name);
    }
    
    [Fact]
    public void Parse_Precedence_BangBindsTighterThanPipe()
    {
        // int !E | None should be (int !E) | None, not int !(E | None)
        var type = ParseType("int !ValueError | None");
        
        // The error type should be just "ValueError", not "ValueError | None"
        Assert.False(type.ErrorType!.IsCSharpNullable);
        Assert.True(type.IsCSharpNullable); // The outer type is nullable
    }
    
    #endregion
    
    #region Position Tracking
    
    [Fact]
    public void Parse_ResultType_TracksPosition()
    {
        var type = ParseType("int !ValueError");
        
        Assert.True(type.LineStart > 0 || type.ColumnStart > 0);
        Assert.NotNull(type.ErrorType);
    }
    
    #endregion
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter TypeAnnotationParserTests`
- [ ] All tests pass

```
git add src/Sharpy.Compiler.Tests/Parser/TypeAnnotationParserTests.cs
git commit -m "test: add parser tests for T?, T | None, and T !E syntax"
```

---

## Task 4.5: Verify Existing Parser Tests Still Pass

### Steps

- [ ] Run all parser tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Parser"`
- [ ] Investigate any failures
- [ ] Fix any regressions

### Common Issues

1. **Tests that check `IsNullable`** — should be updated to `IsOptional` or `IsCSharpNullable`
2. **Tests that parse type annotations** — verify they still work
3. **Position tracking tests** — verify spans are correct

### Verification

- [ ] All parser tests pass

```
# If fixes were needed:
git add -A
git commit -m "fix: update parser tests for new type annotation properties"
```

---

## Task 4.6: Update Type Annotation Shorthand Documentation

**File:** `docs/language_specification/type_annotation_shorthand.md`

### Steps

- [ ] Open `docs/language_specification/type_annotation_shorthand.md`
- [ ] Add a new section "Nullability and Result Syntax" after existing content:
  ```markdown
  ## Nullability and Result Syntax

  ### Optional (`T?`)

  The `T?` suffix creates an `Optional[T]` (safe tagged union):

  ```python
  name: str? = Some("Alice")
  empty: int? = Nothing
  ```

  ### C# Nullable (`T | None`)

  The `T | None` suffix marks a type as C# nullable (for .NET interop):

  ```python
  raw: str | None = dotnet_api()
  ```

  **Note:** `| None` is the only valid inline union. Free unions like `int | str` are not supported.

  ### Result Type (`T !E`)

  The `T !E` suffix creates a `Result[T, E]`:

  ```python
  def parse(s: str) -> int !ValueError:
      ...
  ```

  ### Precedence

  `!E` binds tighter than `| None`:

  ```python
  int !ValueError | None  →  Result[int, ValueError] | None
  ```

  ### Combining with Collection Shorthand

  All modifiers work with collection shorthand:

  ```python
  items: [int]?           # Optional[list[int]]
  lookup: {str: int}?     # Optional[dict[str, int]]
  data: [int] | None      # list[int] | None (C# nullable)
  parsed: [int] !Error    # Result[list[int], Error]
  ```
  ```
- [ ] Verify the new section is consistent with other documentation

### Verification

- [ ] Read through the updated document
- [ ] Check for consistency with `nullable_types.md`, `tagged_unions_optional.md`, `tagged_unions_result.md`

```
git add docs/language_specification/type_annotation_shorthand.md
git commit -m "spec: add nullability and result syntax to type annotation docs"
```

---

## Final Verification

- [ ] Build entire compiler: `dotnet build src/Sharpy.Compiler`
- [ ] Run all parser tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Parser"`
- [ ] Run all compiler tests: `dotnet test src/Sharpy.Compiler.Tests`
- [ ] All tests pass
- [ ] Review all commits in this phase

```
git log --oneline -5
```

Expected commits:
1. `parser: verify T? sets IsOptional correctly`
2. `parser: add T !E result type syntax parsing`
3. `parser: add T | None C# nullable syntax parsing`
4. `test: add parser tests for T?, T | None, and T !E syntax`
5. `spec: add nullability and result syntax to type annotation docs`

---

## Notes for Implementer

- **Precedence matters:** `!E` must bind tighter than `| None` so that `int !E | None` means `(int !E) | None`
- **Error messages:** When users try `int | str`, give a helpful error pointing to `union` declarations
- **The parser doesn't know semantics:** It just builds the AST. The semantic analyzer (Phases 5-6) will interpret these flags.
- **Recursive parsing:** `ParseTypeAnnotation()` calls itself for the error type in `T !E`, which means error types can also have modifiers (though this may be unusual)

# Phase 6: Advanced Type Safety and Quality

> **Priority:** P3 (Nice-to-have)
> **Estimated Effort:** 14-22 hours
> **Prerequisite:** Phases 1-4 recommended for stable foundation
> **Concerns Addressed:** #8, #13, #15 from remaining-hardening-concerns.md

---

## Overview

This phase contains lower-priority improvements that enhance the compiler's robustness and type safety but aren't blocking for v1.0. These can be worked on opportunistically or deferred based on team bandwidth.

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 8 | No grammar-aware fuzzing/property tests | 6-10h | Low |
| 13 | Type narrowing not persisted | 4-6h | Low |
| 15 | Missing match exhaustiveness warnings | 4-6h | Low |

---

## Task 6.1: Add Grammar-Aware Fuzzing Tests

**Files:**
- Create: `src/Sharpy.Compiler.Tests/Fuzzing/LexerFuzzTests.cs`
- Create: `src/Sharpy.Compiler.Tests/Fuzzing/ParserFuzzTests.cs`
- Create: `src/Sharpy.Compiler.Tests/Stress/LargeFileTests.cs`

### Background

The test suite has 332+ file-based tests and 317 error cases, providing strong coverage. However, there are no:
- Grammar-aware fuzz tests (structured random input)
- Property-based tests (QuickCheck-style)
- Stress tests (large files, deep nesting)

### Why Grammar-Aware Matters

Random strings are mostly garbage—they test lexer error recovery but not interesting parser code paths. Grammar-aware fuzzing generates structurally valid-ish input (correct token sequences) then mutates it to find edge cases.

### Implementation Checklist

#### 6.1.1 Setup FsCheck for Property-Based Testing

- [ ] **Add NuGet package**
  ```bash
  cd src/Sharpy.Compiler.Tests
  dotnet add package FsCheck.Xunit
  ```

- [ ] **Create base generators**
  ```csharp
  // src/Sharpy.Compiler.Tests/Fuzzing/Generators.cs
  namespace Sharpy.Compiler.Tests.Fuzzing;

  using FsCheck;

  public static class SharpyGenerators
  {
      // Generate valid identifiers
      public static Arbitrary<string> Identifier() =>
          Arb.Default.NonEmptyString()
              .Generator
              .Select(s => new string(s.Get.Where(c =>
                  char.IsLetterOrDigit(c) || c == '_').ToArray()))
              .Where(s => s.Length > 0 && !char.IsDigit(s[0]))
              .ToArbitrary();

      // Generate valid integer literals
      public static Arbitrary<string> IntLiteral() =>
          Gen.Choose(-1_000_000, 1_000_000)
              .Select(i => i.ToString())
              .ToArbitrary();

      // Generate valid string literals
      public static Arbitrary<string> StringLiteral() =>
          Arb.Default.NonEmptyString()
              .Generator
              .Select(s => $"\"{EscapeString(s.Get)}\"")
              .ToArbitrary();

      private static string EscapeString(string s) =>
          s.Replace("\\", "\\\\")
           .Replace("\"", "\\\"")
           .Replace("\n", "\\n")
           .Replace("\r", "\\r");

      // Generate simple expressions
      public static Gen<string> SimpleExpression() =>
          Gen.OneOf(
              Gen.Choose(1, 1000).Select(i => i.ToString()),
              Gen.Constant("True"),
              Gen.Constant("False"),
              Gen.Constant("None"),
              Identifier().Generator
          );

      // Generate binary operators
      public static Gen<string> BinaryOp() =>
          Gen.Elements("+", "-", "*", "/", "//", "%", "==", "!=", "<", ">", "<=", ">=", "and", "or");

      // Generate compound expressions
      public static Gen<string> Expression(int depth = 0) =>
          depth > 5
              ? SimpleExpression()
              : Gen.Frequency(
                  (3, SimpleExpression()),
                  (1, Gen.Map3(
                      (l, op, r) => $"({l} {op} {r})",
                      Expression(depth + 1),
                      BinaryOp(),
                      Expression(depth + 1))));
  }
  ```

#### 6.1.2 Lexer Fuzz Tests

- [ ] **Create lexer fuzz tests**
  ```csharp
  // src/Sharpy.Compiler.Tests/Fuzzing/LexerFuzzTests.cs
  namespace Sharpy.Compiler.Tests.Fuzzing;

  using FsCheck;
  using FsCheck.Xunit;
  using Sharpy.Compiler.Lexer;

  public class LexerFuzzTests
  {
      [Property(MaxTest = 1000)]
      public bool LexerNeverCrashesOnRandomInput(NonEmptyString input)
      {
          var lexer = new Lexer(input.Get);
          try
          {
              var tokens = lexer.TokenizeAll();
              return true; // Completed without crash
          }
          catch (LexerAbortException)
          {
              return true; // Expected for invalid input
          }
          catch (Exception ex)
          {
              // Unexpected crash
              Assert.Fail($"Lexer crashed on input: {input.Get}\nException: {ex}");
              return false;
          }
      }

      [Property(MaxTest = 500)]
      public bool LexerHandlesValidTokenSequences(
          NonEmptyArray<string> tokens)
      {
          // Join with spaces to create "valid-ish" input
          var input = string.Join(" ", tokens.Get);

          var lexer = new Lexer(input);
          try
          {
              lexer.TokenizeAll();
              return true;
          }
          catch (LexerAbortException)
          {
              return true; // Invalid input is fine
          }
      }

      [Property(Arbitrary = new[] { typeof(SharpyArbitraries) })]
      public bool LexerTokenizesValidIdentifiers(ValidIdentifier id)
      {
          var lexer = new Lexer(id.Value);
          var tokens = lexer.TokenizeAll();

          // Should produce exactly one IDENTIFIER token (plus EOF)
          var identifiers = tokens.Where(t => t.Type == TokenType.IDENTIFIER).ToList();
          return identifiers.Count == 1 && identifiers[0].Lexeme == id.Value;
      }
  }

  // Custom types for better generation
  public record ValidIdentifier(string Value);

  public class SharpyArbitraries
  {
      public static Arbitrary<ValidIdentifier> ValidIdentifier() =>
          SharpyGenerators.Identifier()
              .Generator
              .Where(s => !IsKeyword(s))
              .Select(s => new ValidIdentifier(s))
              .ToArbitrary();

      private static bool IsKeyword(string s) =>
          s is "def" or "class" or "if" or "else" or "while" or "for"
          or "return" or "True" or "False" or "None" or "and" or "or"
          or "not" or "in" or "is" or "import" or "from" or "as";
  }
  ```

#### 6.1.3 Parser Fuzz Tests

- [ ] **Create parser fuzz tests**
  ```csharp
  // src/Sharpy.Compiler.Tests/Fuzzing/ParserFuzzTests.cs
  namespace Sharpy.Compiler.Tests.Fuzzing;

  using FsCheck;
  using FsCheck.Xunit;
  using Sharpy.Compiler.Parser;

  public class ParserFuzzTests
  {
      [Property(MaxTest = 500)]
      public bool ParserNeverCrashesOnTokenizedInput(NonEmptyString source)
      {
          try
          {
              var parser = new Parser(source.Get);
              parser.Parse();
              return true;
          }
          catch (ParserException)
          {
              return true; // Expected for invalid input
          }
          catch (LexerAbortException)
          {
              return true; // Lexer rejection is fine
          }
          catch (Exception ex)
          {
              Assert.Fail($"Parser crashed: {ex}");
              return false;
          }
      }

      [Property(Arbitrary = new[] { typeof(SharpySourceArbitraries) })]
      public bool ParserHandlesGeneratedExpressions(GeneratedExpression expr)
      {
          var source = $"x = {expr.Value}";

          try
          {
              var parser = new Parser(source);
              var ast = parser.Parse();
              return ast.Body.Count >= 1;
          }
          catch (ParserException)
          {
              return true; // Some generated expressions might be invalid
          }
      }

      [Property(Arbitrary = new[] { typeof(SharpySourceArbitraries) })]
      public bool ParserHandlesGeneratedFunctions(GeneratedFunction func)
      {
          try
          {
              var parser = new Parser(func.Source);
              var ast = parser.Parse();
              return ast.Body.OfType<FunctionDef>().Any();
          }
          catch (ParserException)
          {
              return true;
          }
      }
  }

  public record GeneratedExpression(string Value);
  public record GeneratedFunction(string Source);

  public class SharpySourceArbitraries
  {
      public static Arbitrary<GeneratedExpression> GeneratedExpression() =>
          SharpyGenerators.Expression()
              .Select(e => new GeneratedExpression(e))
              .ToArbitrary();

      public static Arbitrary<GeneratedFunction> GeneratedFunction() =>
          from name in SharpyGenerators.Identifier().Generator.Where(n => !IsKeyword(n))
          from paramCount in Gen.Choose(0, 5)
          from params_ in Gen.ListOf(paramCount, SharpyGenerators.Identifier().Generator.Where(n => !IsKeyword(n)))
          from body in Gen.Elements("pass", "return 1", "return True")
          let paramList = string.Join(", ", params_.Select((p, i) => $"{p}{i}: int"))
          let source = $"def {name}({paramList}):\n    {body}"
          select new GeneratedFunction(source);

      private static bool IsKeyword(string s) =>
          s is "def" or "class" or "if" or "else" or "while" or "for"
          or "return" or "True" or "False" or "None" or "pass";
  }
  ```

#### 6.1.4 Stress Tests

- [ ] **Create stress tests for large inputs**
  ```csharp
  // src/Sharpy.Compiler.Tests/Stress/LargeFileTests.cs
  namespace Sharpy.Compiler.Tests.Stress;

  public class LargeFileTests : IntegrationTestBase
  {
      public LargeFileTests(ITestOutputHelper output) : base(output) { }

      [Fact]
      public void HandlesLargeFile_10000Lines()
      {
          var sb = new StringBuilder();
          sb.AppendLine("def main():");
          for (int i = 0; i < 10000; i++)
          {
              sb.AppendLine($"    x{i}: int = {i}");
          }
          sb.AppendLine("    print(x9999)");

          var result = CompileAndExecute(sb.ToString());
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("9999\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesDeeplyNestedExpressions_100Levels()
      {
          // (((((...1...)))))
          var expr = "1";
          for (int i = 0; i < 100; i++)
          {
              expr = $"({expr})";
          }

          var source = $@"
  def main():
      x = {expr}
      print(x)
  ";

          var result = CompileAndExecute(source);
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("1\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesDeeplyNestedBlocks_50Levels()
      {
          var sb = new StringBuilder();
          sb.AppendLine("def main():");
          var indent = "    ";

          for (int i = 0; i < 50; i++)
          {
              sb.AppendLine($"{indent}if True:");
              indent += "    ";
          }

          sb.AppendLine($"{indent}print(\"deep\")");

          var result = CompileAndExecute(sb.ToString());
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("deep\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesManyFunctions_1000()
      {
          var sb = new StringBuilder();
          for (int i = 0; i < 1000; i++)
          {
              sb.AppendLine($"def func{i}() -> int:");
              sb.AppendLine($"    return {i}");
              sb.AppendLine();
          }

          sb.AppendLine("def main():");
          sb.AppendLine("    print(func999())");

          var result = CompileAndExecute(sb.ToString());
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("999\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesManyClasses_500()
      {
          var sb = new StringBuilder();
          for (int i = 0; i < 500; i++)
          {
              sb.AppendLine($"class Class{i}:");
              sb.AppendLine($"    value: int = {i}");
              sb.AppendLine();
          }

          sb.AppendLine("def main():");
          sb.AppendLine("    obj = Class499()");
          sb.AppendLine("    print(obj.value)");

          var result = CompileAndExecute(sb.ToString());
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("499\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesManyImports_100Files()
      {
          using var helper = new ProjectCompilationHelper(_output);

          // Create 100 library files
          for (int i = 0; i < 100; i++)
          {
              helper.AddSourceFile($"lib{i}.spy", $"VALUE_{i}: int = {i}");
          }

          // Create main that imports all
          var sb = new StringBuilder();
          for (int i = 0; i < 100; i++)
          {
              sb.AppendLine($"from lib{i} import VALUE_{i}");
          }
          sb.AppendLine();
          sb.AppendLine("def main():");
          sb.AppendLine("    total = 0");
          for (int i = 0; i < 100; i++)
          {
              sb.AppendLine($"    total += VALUE_{i}");
          }
          sb.AppendLine("    print(total)");

          helper.AddSourceFile("main.spy", sb.ToString());
          helper.CreateProjectFile();

          var result = helper.Compile();
          Assert.True(result.Success, result.ErrorOutput);
          // Sum of 0..99 = 4950
          Assert.Equal("4950\n", result.StandardOutput);
      }
  }
  ```

### Verification

```bash
# Run fuzz tests (may take a while due to many iterations)
dotnet test --filter "FullyQualifiedName~Fuzzing" --timeout 300000

# Run stress tests
dotnet test --filter "FullyQualifiedName~Stress"
```

---

## Task 6.2: Persist Type Narrowing in SemanticInfo

**Files:**
- `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

### Background

Type narrowing tracks refined types after guards:
```python
def process(x: int?) -> int:
    if x is not None:
        return x  # x is narrowed to int here
    return 0
```

Currently, `_narrowedTypes` is a local dictionary in `TypeChecker` and isn't persisted to `SemanticInfo`. This means LSP hover would show `int?` instead of `int` after the null check.

### Implementation Checklist

- [ ] **6.2.1** Add narrowing storage to SemanticInfo
  ```csharp
  // SemanticInfo.cs
  public class SemanticInfo
  {
      // Existing stores...

      // Narrowed types: maps (expression, scope span) -> narrowed type
      private readonly Dictionary<(Expression, TextSpan), SemanticType> _narrowedTypes = new();

      /// <summary>
      /// Records a type narrowing for an expression within a specific scope.
      /// </summary>
      /// <param name="expr">The expression being narrowed</param>
      /// <param name="narrowedTo">The narrowed type</param>
      /// <param name="scopeSpan">The text span where this narrowing is valid</param>
      public void SetNarrowedType(Expression expr, SemanticType narrowedTo, TextSpan scopeSpan)
      {
          _narrowedTypes[(expr, scopeSpan)] = narrowedTo;
      }

      /// <summary>
      /// Gets the narrowed type for an expression at a specific position.
      /// Returns null if no narrowing applies at this position.
      /// </summary>
      public SemanticType? GetNarrowedType(Expression expr, int position)
      {
          foreach (var ((e, scope), narrowedType) in _narrowedTypes)
          {
              if (ReferenceEquals(e, expr) && scope.Contains(position))
              {
                  return narrowedType;
              }
          }
          return null;
      }

      /// <summary>
      /// Gets the effective type of an expression at a position,
      /// considering type narrowing.
      /// </summary>
      public SemanticType? GetEffectiveType(Expression expr, int position)
      {
          return GetNarrowedType(expr, position) ?? GetType(expr);
      }
  }
  ```

- [ ] **6.2.2** Update TypeChecker to persist narrowings
  ```csharp
  // TypeChecker.cs
  private void CheckIfStatement(IfStatement ifStmt)
  {
      // ... existing condition checking ...

      // Extract narrowing from condition
      var narrowings = ExtractNarrowedTypes(ifStmt.Condition);

      // Calculate scope span for the if body
      var bodySpan = new TextSpan(
          ifStmt.Body.First().Location!.Start,
          ifStmt.Body.Last().Location!.End);

      // Persist narrowings to SemanticInfo
      foreach (var (expr, narrowedType) in narrowings)
      {
          _semanticInfo.SetNarrowedType(expr, narrowedType, bodySpan);
      }

      // ... check body with narrowed context ...
  }
  ```

- [ ] **6.2.3** Add tests for persisted narrowing
  ```csharp
  [Fact]
  public void NarrowingPersistedInSemanticInfo()
  {
      var source = @"
  def process(x: int?) -> int:
      if x is not None:
          return x
      return 0
  ";

      var (ast, semanticInfo) = CompileToSemanticInfo(source);

      // Find the 'return x' statement
      var func = ast.Body.OfType<FunctionDef>().First();
      var ifStmt = func.Body.OfType<IfStatement>().First();
      var returnStmt = ifStmt.Body.OfType<ReturnStatement>().First();
      var xRef = returnStmt.Value as Identifier;

      // Get position inside the if body
      var positionInBody = ifStmt.Body.First().Location!.Start + 1;

      // Should be narrowed to int (not int?)
      var effectiveType = semanticInfo.GetEffectiveType(xRef!, positionInBody);
      Assert.Equal(SemanticType.Int, effectiveType);
  }
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~Narrowing"
```

---

## Task 6.3: Add Match Exhaustiveness Warnings

**File:** Create `src/Sharpy.Compiler/Semantic/Validation/ExhaustivenessValidator.cs`

### Background

Sharpy supports pattern matching but doesn't warn about non-exhaustive matches:
```python
match x:
    case 1: print("one")
    case 2: print("two")
    # No default - what if x is 3?
```

A statically-typed language should warn about this (Axiom 3: Type Safety).

### Implementation Checklist

- [ ] **6.3.1** Create the validator
  ```csharp
  // src/Sharpy.Compiler/Semantic/Validation/ExhaustivenessValidator.cs
  namespace Sharpy.Compiler.Semantic.Validation;

  public class ExhaustivenessValidator : ISemanticValidator
  {
      public int Order => 350;  // After type checking, before control flow

      public void Validate(Module module, SemanticContext context)
      {
          foreach (var matchStmt in FindMatchStatements(module))
          {
              ValidateExhaustiveness(matchStmt, context);
          }
      }

      private IEnumerable<MatchStatement> FindMatchStatements(Module module)
      {
          return new MatchStatementFinder().Find(module);
      }

      private void ValidateExhaustiveness(MatchStatement match, SemanticContext context)
      {
          var subjectType = context.SemanticInfo.GetType(match.Subject);

          if (subjectType == null)
              return;  // Type error already reported

          var hasWildcard = match.Cases.Any(c => c.Pattern is WildcardPattern);

          if (hasWildcard)
              return;  // Exhaustive by wildcard

          // Check based on subject type
          if (subjectType is UserDefinedType udt)
          {
              var symbol = context.SymbolTable.Lookup(udt.Name) as TypeSymbol;
              if (symbol?.TypeKind == TypeKind.Enum)
              {
                  ValidateEnumExhaustiveness(match, symbol, context);
                  return;
              }
          }

          // For other types (int, str, etc.), require wildcard
          context.Diagnostics.AddWarning(
              match.Location,
              "SHP0280",
              $"Match expression may not be exhaustive. Consider adding a " +
              $"'case _:' default branch.");
      }

      private void ValidateEnumExhaustiveness(
          MatchStatement match,
          TypeSymbol enumSymbol,
          SemanticContext context)
      {
          var allCases = enumSymbol.Fields.Select(f => f.Name).ToHashSet();
          var coveredCases = new HashSet<string>();

          foreach (var matchCase in match.Cases)
          {
              if (matchCase.Pattern is MemberAccessPattern memberPattern)
              {
                  coveredCases.Add(memberPattern.Member);
              }
              else if (matchCase.Pattern is WildcardPattern)
              {
                  return;  // Wildcard covers all
              }
          }

          var missingCases = allCases.Except(coveredCases).ToList();

          if (missingCases.Any())
          {
              var missing = string.Join(", ", missingCases.Select(c => $"'{c}'"));
              context.Diagnostics.AddWarning(
                  match.Location,
                  "SHP0281",
                  $"Match expression is not exhaustive. Missing cases: {missing}");
          }
      }
  }

  // Helper to find match statements in AST
  private class MatchStatementFinder : AstVisitor<IEnumerable<MatchStatement>>
  {
      private readonly List<MatchStatement> _found = new();

      public IEnumerable<MatchStatement> Find(Module module)
      {
          Visit(module);
          return _found;
      }

      protected override void VisitMatchStatement(MatchStatement match)
      {
          _found.Add(match);
          base.VisitMatchStatement(match);
      }
  }
  ```

- [ ] **6.3.2** Register the validator
  ```csharp
  // In ValidationPipeline or wherever validators are registered
  validators.Add(new ExhaustivenessValidator());
  ```

- [ ] **6.3.3** Add warning code to DiagnosticCodes
  ```csharp
  // DiagnosticCodes.cs
  public const string NonExhaustiveMatch = "SHP0280";
  public const string MissingEnumCases = "SHP0281";
  ```

- [ ] **6.3.4** Add tests
  ```csharp
  [Fact]
  public void WarnsOnNonExhaustiveIntMatch()
  {
      var source = @"
  def main():
      x = 1
      match x:
          case 1: print(""one"")
          case 2: print(""two"")
  ";

      var result = Compile(source);
      Assert.True(result.Success);  // Compiles, but with warning
      Assert.Contains(result.Warnings, w => w.Code == "SHP0280");
  }

  [Fact]
  public void NoWarningWithWildcard()
  {
      var source = @"
  def main():
      x = 1
      match x:
          case 1: print(""one"")
          case _: print(""other"")
  ";

      var result = Compile(source);
      Assert.True(result.Success);
      Assert.DoesNotContain(result.Warnings, w => w.Code == "SHP0280");
  }

  [Fact]
  public void WarnsOnMissingEnumCases()
  {
      var source = @"
  enum Color:
      RED = 1
      GREEN = 2
      BLUE = 3

  def main():
      c = Color.RED
      match c:
          case Color.RED: print(""red"")
          case Color.GREEN: print(""green"")
          # Missing BLUE!
  ";

      var result = Compile(source);
      Assert.True(result.Success);
      Assert.Contains(result.Warnings, w =>
          w.Code == "SHP0281" && w.Message.Contains("BLUE"));
  }

  [Fact]
  public void NoWarningOnExhaustiveEnumMatch()
  {
      var source = @"
  enum Color:
      RED = 1
      GREEN = 2

  def main():
      c = Color.RED
      match c:
          case Color.RED: print(""red"")
          case Color.GREEN: print(""green"")
  ";

      var result = Compile(source);
      Assert.True(result.Success);
      Assert.Empty(result.Warnings);
  }
  ```

- [ ] **6.3.5** Add file-based test fixtures
  ```
  # TestFixtures/match_exhaustiveness_warning/main.spy
  def main():
      x = 1
      match x:
          case 1: print("one")

  # TestFixtures/match_exhaustiveness_warning/main.expected
  one

  # TestFixtures/match_exhaustiveness_warning/main.warning
  not exhaustive
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~Exhaustiveness"
dotnet test --filter "DisplayName~match_exhaustiveness"
```

---

## Phase Completion Criteria

- [ ] Fuzz tests run without crashes (1000+ iterations)
- [ ] Stress tests pass for large inputs
- [ ] Type narrowing persisted and queryable by position
- [ ] Match exhaustiveness warnings for non-exhaustive matches
- [ ] Enum exhaustiveness correctly detects missing cases
- [ ] All tests pass
- [ ] Code review completed

---

## Notes for Implementers

### Task Priority Within Phase

If time is limited:
1. **Task 6.3 (Exhaustiveness)** — Most visible user benefit
2. **Task 6.1 (Fuzzing)** — Good for long-term quality
3. **Task 6.2 (Narrowing)** — LSP prerequisite, less immediate impact

### Property-Based Testing Tips

- Start with simple generators, make them more complex gradually
- Use `Verbose` property to see which inputs are tested
- If a test fails, FsCheck reports the shrunk failing case

```csharp
[Property(Verbose = true, MaxTest = 100)]
public bool MyProperty(int x) => x > 0;  // Will show all tested values
```

### Stress Test Thresholds

These thresholds are conservative:
- 10,000 lines — typical large file
- 100 nesting levels — beyond normal code
- 1,000 functions — large module
- 500 classes — large codebase

If tests are too slow, reduce numbers but keep proportions.

---

## Future Considerations (Beyond Phase 6)

### Concern #10: TypeChecker Refactor (Deferred)

The TypeChecker is ~4,600 lines across 5 partial files. While well-organized, it could benefit from extraction:
- `TypeNarrower` (~200 lines)
- `ExpressionTypeInference` (~500 lines)
- `CodeGenInfoComputer` (move to post-TypeChecker pass)
- `OverloadResolver` (needed for LSP completions)

**Trigger:** Start this refactor when implementing LSP or adding major type system features.

### High-Impact Structural Improvements

From the original document:
- **ErrorType sentinel** — Distinguish "unknown" from "error"
- **Source mapping for generated C#** — Covered in Phase 5
- **Typed diagnostic parameters** — Structured error data
- **Immutable symbol snapshots** — FrozenSymbol for cache

These are larger architectural improvements that could be their own phases.

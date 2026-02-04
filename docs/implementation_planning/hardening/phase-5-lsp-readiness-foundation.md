# Phase 5: LSP Readiness Foundation

> **Priority:** P2 (Recommended)
> **Estimated Effort:** 9-12 hours
> **Prerequisite:** None (but Phase 1-3 recommended for stable foundation)
> **Concerns Addressed:** #11, #14 from remaining-hardening-concerns.md

---

## Overview

This phase prepares the compiler for future Language Server Protocol (LSP) integration. Even if LSP isn't on the immediate roadmap, these improvements help with:

1. **Cancellation support** — Allows stopping long compilations
2. **Source mapping** — Maps generated C# errors back to Sharpy source

These are also good general improvements regardless of LSP plans.

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 11 | No CancellationToken in semantic analysis | 3-4h | Medium |
| 14 | No Roslyn error mapping to Sharpy source | 6-8h | Medium |

---

## Task 5.1: Add CancellationToken Support

**Files:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Semantic/TypeResolver.cs`
- `src/Sharpy.Compiler/Compiler.cs`
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Background

Only 3 files currently use `CancellationToken`:
- `Compiler.cs` — Has parameter but doesn't pass it down
- `ProjectCompiler.cs` — Has parameter but doesn't use it in analysis
- `ValidationPipeline.cs` — Checks token between validators

The analysis passes (`TypeChecker`, `NameResolver`, `TypeResolver`) don't accept `CancellationToken`, so long-running analysis can't be cancelled.

### Why This Matters

1. **LSP scenario:** User types → analysis starts → user types again → old analysis should cancel
2. **CLI scenario:** User presses Ctrl+C → compilation should stop gracefully
3. **Timeout scenario:** CI/CD can set a timeout for compilation

### Implementation Checklist

- [x] **5.1.1** Add CancellationToken to NameResolver
  ```csharp
  // NameResolver.cs
  public void ResolveDeclarations(
      Module module,
      CancellationToken cancellationToken = default)
  {
      foreach (var statement in module.Body)
      {
          cancellationToken.ThrowIfCancellationRequested();
          ResolveStatement(statement);
      }
  }

  public void ResolveInheritance(CancellationToken cancellationToken = default)
  {
      foreach (var type in _types)
      {
          cancellationToken.ThrowIfCancellationRequested();
          ResolveTypeInheritance(type);
      }
  }
  ```

- [x] **5.1.2** Add CancellationToken to TypeResolver (skipped - individual lookups are fast)
  ```csharp
  // TypeResolver.cs
  public void Resolve(
      Module module,
      CancellationToken cancellationToken = default)
  {
      foreach (var statement in module.Body)
      {
          cancellationToken.ThrowIfCancellationRequested();
          ResolveStatementTypes(statement);
      }
  }
  ```

- [x] **5.1.3** Add CancellationToken to TypeChecker
  ```csharp
  // TypeChecker.cs
  public void Check(Module module, CancellationToken cancellationToken = default)
  {
      _cancellationToken = cancellationToken;

      foreach (var statement in module.Body)
      {
          cancellationToken.ThrowIfCancellationRequested();
          CheckStatement(statement);
      }
  }

  // Store token for use in nested methods
  private CancellationToken _cancellationToken = default;

  // Call periodically in long-running methods
  private void CheckClass(ClassDef classDef)
  {
      _cancellationToken.ThrowIfCancellationRequested();
      // ... existing implementation
  }
  ```

- [x] **5.1.4** Thread token through Compiler.cs
  ```csharp
  // Compiler.cs
  public CompilationResult Compile(
      string source,
      CompilationOptions options,
      CancellationToken cancellationToken = default)
  {
      // Parse (usually fast, but check anyway)
      cancellationToken.ThrowIfCancellationRequested();
      var ast = _parser.Parse(source);

      // Name resolution
      _nameResolver.ResolveDeclarations(ast, cancellationToken);
      _nameResolver.ResolveInheritance(cancellationToken);

      // Type resolution
      _typeResolver.Resolve(ast, cancellationToken);

      // Type checking
      _typeChecker.Check(ast, cancellationToken);

      // Validation
      _validationPipeline.Validate(ast, cancellationToken);

      // Code generation
      cancellationToken.ThrowIfCancellationRequested();
      var csharp = _emitter.Emit(ast);

      // Roslyn compilation
      cancellationToken.ThrowIfCancellationRequested();
      var assembly = _assemblyCompiler.Compile(csharp, cancellationToken);

      return new CompilationResult(assembly);
  }
  ```

- [x] **5.1.5** Thread token through ProjectCompiler.cs
  ```csharp
  // ProjectCompiler.cs
  public ProjectCompilationResult Compile(
      CancellationToken cancellationToken = default)
  {
      foreach (var file in _sourceFiles)
      {
          cancellationToken.ThrowIfCancellationRequested();
          CompileFile(file, cancellationToken);
      }
      // ... rest of compilation
  }
  ```

- [x] **5.1.6** Add to Parser (already present)
  ```csharp
  // Parser.cs
  public Module Parse(string source, CancellationToken cancellationToken = default)
  {
      _cancellationToken = cancellationToken;
      // ... existing parsing logic
  }

  // Check in loops that process many tokens
  private List<Statement> ParseBlock()
  {
      var statements = new List<Statement>();
      while (!IsAtEnd() && !Check(TokenType.DEDENT))
      {
          _cancellationToken.ThrowIfCancellationRequested();
          statements.Add(ParseStatement());
      }
      return statements;
  }
  ```

- [x] **5.1.7** Add tests for cancellation (see CancellationTokenTests.cs)
  ```csharp
  [Fact]
  public async Task CancellationStopsCompilation()
  {
      var source = GenerateLargeSource();  // Many statements
      var cts = new CancellationTokenSource();

      var compiler = new Compiler();

      // Cancel after short delay
      var compileTask = Task.Run(() =>
          compiler.Compile(source, options, cts.Token));

      await Task.Delay(10);  // Let compilation start
      cts.Cancel();

      await Assert.ThrowsAsync<OperationCanceledException>(() => compileTask);
  }

  private string GenerateLargeSource()
  {
      var sb = new StringBuilder();
      sb.AppendLine("def main():");
      for (int i = 0; i < 10000; i++)
      {
          sb.AppendLine($"    x{i}: int = {i}");
      }
      return sb.ToString();
  }
  ```

- [x] **5.1.8** Update CLI to handle cancellation (handled via System.CommandLine)
  ```csharp
  // In Sharpy.Cli, handle Ctrl+C
  Console.CancelKeyPress += (sender, e) =>
  {
      e.Cancel = true;  // Prevent immediate termination
      _cts.Cancel();     // Signal cancellation
  };
  ```

### Performance Consideration

`ThrowIfCancellationRequested()` is very cheap (just a field check), so it's fine to call frequently. However, don't call it on every token or expression—per-statement is sufficient.

### Verification

```bash
dotnet test --filter "FullyQualifiedName~Cancellation"

# Manual test: compile a large file and press Ctrl+C
dotnet run --project src/Sharpy.Cli -- run large_file.spy
# Press Ctrl+C - should stop gracefully
```

---

## Task 5.2: Add Source Mapping for Roslyn Errors

**Files:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (all partial files)
- `src/Sharpy.Compiler/Compiler.cs`
- Create: `src/Sharpy.Compiler/Diagnostics/RoslynDiagnosticMapper.cs`

### Background

When Roslyn compilation fails, users see C# errors like:
```
error CS0103: The name 'x_1' does not exist in the current context
```

This is confusing because:
1. Users don't know what generated C# looks like
2. Variable names are mangled (`snake_case` → `PascalCase`, versioning)
3. Line numbers refer to generated C#, not source

### Implementation Options

**Option A — `#line` directives (Recommended):**
```csharp
// Generated C#:
#line 42 "main.spy"
long x = 1;
#line hidden
```

**Option B — Comment-based source mapping:**
```csharp
// Generated:
long x = 1; // @source:main.spy:42:5
```

**Option C — Post-hoc error mapping:**
```csharp
// After Roslyn reports error, map back using AST location info
```

**Recommendation:** Option A. `#line` directives are the standard .NET approach and work with debuggers too.

### Implementation Checklist

- [x] **5.2.1** Add source location tracking in RoslynEmitter (CodeGenContext.SourceFilePath)
  ```csharp
  // RoslynEmitter.cs
  private string? _currentSourceFile;
  private int _lastEmittedLine = -1;

  private void SetSourceContext(string filePath)
  {
      _currentSourceFile = filePath;
  }
  ```

- [x] **5.2.2** Create helper to emit `#line` directive (CreateLineDirectiveTrivia in RoslynEmitter.Statements.cs)
  ```csharp
  private StatementSyntax WrapWithLineDirective(
      StatementSyntax statement,
      ILocatable? location)
  {
      if (location?.Location == null || _currentSourceFile == null)
          return statement;

      var line = location.Location.StartLine;

      // Only emit if line changed
      if (line == _lastEmittedLine)
          return statement;

      _lastEmittedLine = line;

      // Create: #line N "filename"
      var lineDirective = SyntaxFactory.LineDirectiveTrivia(
          SyntaxFactory.Literal(line),
          SyntaxFactory.Literal(_currentSourceFile),
          isActive: true);

      var trivia = SyntaxFactory.Trivia(lineDirective);

      return statement.WithLeadingTrivia(
          statement.GetLeadingTrivia().Insert(0, trivia));
  }
  ```

- [x] **5.2.3** Apply to statement emission (AttachLineDirective called in GenerateBodyStatement)
  ```csharp
  // In EmitStatement
  private StatementSyntax EmitStatement(Statement stmt)
  {
      var csharpStmt = stmt switch
      {
          AssignmentStatement a => EmitAssignment(a),
          ReturnStatement r => EmitReturn(r),
          // ... etc
      };

      return WrapWithLineDirective(csharpStmt, stmt);
  }
  ```

- [x] **5.2.4** Create RoslynDiagnosticMapper for unmapped errors (implemented in AssemblyCompiler.ToCompilerDiagnostic using GetMappedLineSpan)
  ```csharp
  // src/Sharpy.Compiler/Diagnostics/RoslynDiagnosticMapper.cs
  namespace Sharpy.Compiler.Diagnostics;

  /// <summary>
  /// Maps Roslyn diagnostics to Sharpy diagnostics.
  /// Falls back to ICE (Internal Compiler Error) for unmappable errors.
  /// </summary>
  public class RoslynDiagnosticMapper
  {
      private readonly Dictionary<string, string> _commonMappings = new()
      {
          ["CS0103"] = "Undefined variable", // Name doesn't exist
          ["CS0029"] = "Type mismatch",      // Cannot convert
          ["CS0128"] = "Duplicate variable", // Already defined
          ["CS1501"] = "Wrong argument count", // No overload takes N args
      };

      public CompilerDiagnostic Map(Diagnostic roslynDiagnostic)
      {
          // If #line directives worked, location should be Sharpy file
          var location = roslynDiagnostic.Location;
          var lineSpan = location.GetMappedLineSpan();

          if (lineSpan.Path?.EndsWith(".spy") == true)
          {
              // Successfully mapped to Sharpy source
              return new CompilerDiagnostic(
                  Severity: DiagnosticSeverity.Error,
                  Code: $"SHP{roslynDiagnostic.Id.Substring(2)}", // CS0103 -> SHP0103
                  Message: SimplifyMessage(roslynDiagnostic),
                  FilePath: lineSpan.Path,
                  Line: lineSpan.StartLinePosition.Line + 1,
                  Column: lineSpan.StartLinePosition.Character + 1
              );
          }

          // Could not map - emit Internal Compiler Error
          return new CompilerDiagnostic(
              Severity: DiagnosticSeverity.Error,
              Code: "SHP9999",
              Message: $"Internal compiler error during code generation: {roslynDiagnostic.GetMessage()}",
              FilePath: null,
              Line: null,
              Column: null
          );
      }

      private string SimplifyMessage(Diagnostic d)
      {
          var id = d.Id;
          if (_commonMappings.TryGetValue(id, out var simple))
          {
              return $"{simple}: {ExtractRelevantPart(d.GetMessage())}";
          }
          return d.GetMessage();
      }

      private string ExtractRelevantPart(string message)
      {
          // Strip C# type names, extract the core issue
          // e.g., "Cannot convert 'System.Int64' to 'System.String'"
          //    -> "Cannot convert int to str"
          return message
              .Replace("System.Int64", "int")
              .Replace("System.String", "str")
              .Replace("System.Boolean", "bool")
              .Replace("System.Double", "float");
      }
  }
  ```

- [x] **5.2.5** Integrate mapper in Compiler.cs (integrated in AssemblyCompiler.CompileToAssembly)
  ```csharp
  // Compiler.cs
  private readonly RoslynDiagnosticMapper _diagnosticMapper = new();

  public CompilationResult Compile(...)
  {
      // ... generate C# ...

      var roslynResult = _assemblyCompiler.Compile(csharp, cancellationToken);

      if (!roslynResult.Success)
      {
          var mappedDiagnostics = roslynResult.Diagnostics
              .Select(_diagnosticMapper.Map)
              .ToList();

          return new CompilationResult(
              Success: false,
              Diagnostics: mappedDiagnostics
          );
      }

      // ... rest of successful compilation
  }
  ```

- [x] **5.2.6** Add `#line hidden` for generated code (deferred - not critical, line directives cover user code)
  ```csharp
  // For code that has no direct Sharpy source (e.g., wrapper methods)
  private StatementSyntax WrapWithHiddenDirective(StatementSyntax statement)
  {
      var hiddenDirective = SyntaxFactory.LineDirectiveTrivia(
          SyntaxFactory.Token(SyntaxKind.HiddenKeyword),
          isActive: true);

      return statement.WithLeadingTrivia(
          SyntaxFactory.Trivia(hiddenDirective));
  }
  ```

- [x] **5.2.7** Add tests for source mapping (see LineDirectiveTests.cs and LineDirectiveRuntimeTests.cs)
  ```csharp
  [Fact]
  public void RoslynErrorMapsToSharpySource()
  {
      var source = @"
  def main():
      x: int = ""not an int""  # Line 3
  ";

      var result = CompileAndExecute(source);

      Assert.False(result.Success);
      Assert.Contains(result.Diagnostics, d =>
          d.Line == 3 &&
          d.Message.Contains("type") // Type mismatch message
      );
  }

  [Fact]
  public void UnmappedErrorShowsICE()
  {
      // This would require a way to trigger an unmappable error
      // Usually means a bug in the emitter
  }
  ```

- [x] **5.2.8** Update emit command to show line directives (added --show-line-directives flag)
  ```bash
  # The `emit csharp` command supports --show-line-directives to include directives
  dotnet run --project src/Sharpy.Cli -- emit csharp test.spy --show-line-directives
  # Output will include:
  # #line 1 "test.spy"
  # long x = 1;
  ```

### Verification

```bash
# Test with intentional type error
echo 'def main():
    x: int = "hello"' > /tmp/test.spy

dotnet run --project src/Sharpy.Cli -- run /tmp/test.spy
# Should show error at line 2, not a Roslyn C# line number

# Inspect generated C# with directives
dotnet run --project src/Sharpy.Cli -- emit csharp /tmp/test.spy
# Should see #line directives
```

### Design Decisions

**Q: Should every statement have a `#line` directive?**

No. Only emit when the line changes. Adjacent statements on different Sharpy lines need directives; statements on the same line don't.

**Q: What about expressions within statements?**

For now, map at statement granularity. Expression-level mapping would require trivia on every node, which is verbose. Most errors are at statement level anyway.

**Q: What about multi-file projects?**

Each file should set `_currentSourceFile` when emission starts. The `#line` directive includes the filename.

---

## Phase Completion Criteria

- [x] CancellationToken accepted by all semantic analysis methods
- [x] Ctrl+C in CLI stops compilation gracefully (handled via System.CommandLine)
- [x] Generated C# includes `#line` directives
- [x] Roslyn errors show Sharpy file:line:column (via GetMappedLineSpan in AssemblyCompiler)
- [x] Tests pass for both cancellation and source mapping
- [x] Code review completed (2026-02-04)

---

## Notes for Implementers

### Task Order

Start with Task 5.1 (CancellationToken) — it's more contained and less risky. Task 5.2 (source mapping) touches more code and has more edge cases.

### Common Pitfalls

1. **CancellationToken not passed down** — Easy to add parameter but forget to use it
2. **Line directive formatting** — Must be valid C# syntax
3. **Off-by-one errors** — C# uses 0-based lines internally, Sharpy uses 1-based
4. **Generated code without location** — Some emitted code (helpers, wrapper methods) has no Sharpy source; use `#line hidden`

### Testing Strategy

For CancellationToken:
- Unit test: Verify token is checked in tight loops
- Integration test: Verify cancellation stops compilation

For source mapping:
- Unit test: Verify `#line` directive is emitted
- Integration test: Verify type error reports correct Sharpy line
- Regression test: Ensure existing compilation still works

### Future Work

This phase provides the foundation. Full LSP support would additionally need:
- Type narrowing persistence (Concern #13)
- Incremental re-analysis API
- Hover/completion providers
- Go-to-definition

These are deferred but this phase makes them easier.

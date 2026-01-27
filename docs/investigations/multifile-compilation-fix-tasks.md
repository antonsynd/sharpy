# Multi-File Compilation Fix - Task List

**Issue**: `sharpyc run` and `sharpyc build` fail for multi-file projects because imported modules are discovered but never compiled to C#.

**Goal**: Enhance single-file compilation to compile ALL discovered imports, not just the entry file.

**Estimated Effort**: 4-6 hours

---

## Prerequisites

Before starting, ensure you understand:
1. How `Compiler.Compile()` works (see `src/Sharpy.Compiler/Compiler.cs`)
2. How `ImportResolver` discovers modules (see `src/Sharpy.Compiler/Semantic/ImportResolver.cs`)
3. How `CompileToBinary` uses compilation results (see `src/Sharpy.Cli/Program.cs`)

Run existing tests to establish baseline:
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test
```

---

## Phase 1: Add Infrastructure for Multi-File Results

### Task 1.1: Extend CompilationResult to hold multiple C# files
- [x] Open `src/Sharpy.Compiler/Compiler.cs`
- [x] Find the `CompilationResult` class (near bottom of file, ~line 380)
- [x] Add a new property to hold all generated C# files:
  ```csharp
  /// <summary>
  /// All generated C# code files (entry point + all imported modules).
  /// Key is the source file path, value is the generated C# code.
  /// </summary>
  public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();
  ```
- [x] Run `dotnet build src/Sharpy.Compiler` to verify it compiles
- [x] **Commit**: `feat(compiler): add GeneratedCSharpFiles to CompilationResult`

### Task 1.2: Extend ImportResolver to expose loaded modules
- [x] Open `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- [x] Find the `_moduleCache` field (near top, ~line 28)
- [x] Add a public property to expose loaded .spy modules:
  ```csharp
  /// <summary>
  /// All loaded .spy modules (excludes .NET modules).
  /// Key is the full file path, value is the ModuleInfo.
  /// </summary>
  public IReadOnlyDictionary<string, ModuleInfo> LoadedSpyModules =>
      _moduleCache
          .Where(kvp => !kvp.Value.IsNetModule && kvp.Value.Module != null)
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
  ```
- [x] Run `dotnet build src/Sharpy.Compiler` to verify it compiles
- [x] **Commit**: `feat(import-resolver): expose LoadedSpyModules for code generation`

---

## Phase 2: Generate C# for All Discovered Modules

### Task 2.1: Create helper method to generate C# for a single module
- [x] Open `src/Sharpy.Compiler/Compiler.cs`
- [x] Find the `Compile` method (~line 64)
- [x] Add a new private helper method after `Compile`:
  ```csharp
  /// <summary>
  /// Generate C# code for a single module that has already been parsed and type-checked.
  /// Used for generating code for imported modules discovered during compilation.
  /// </summary>
  private string? GenerateCSharpForModule(
      ModuleInfo moduleInfo,
      SymbolTable symbolTable,
      BuiltinRegistry builtinRegistry,
      string? projectNamespace)
  {
      if (moduleInfo.Module == null || moduleInfo.IsNetModule)
          return null;

      var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
      {
          SourceFilePath = moduleInfo.Path,
          ProjectNamespace = projectNamespace,
          // Imported modules are NOT entry points - no Main method
          IsEntryPoint = false,
          Logger = _logger
      };

      var emitter = new RoslynEmitter(codeGenContext);
      var compilationUnit = emitter.GenerateCompilationUnit(moduleInfo.Module);
      
      if (codeGenContext.HasErrors)
      {
          foreach (var error in codeGenContext.Errors)
          {
              _logger.LogError($"Code generation error in {moduleInfo.Path}: {error}", 0, 0);
          }
          return null;
      }

      return compilationUnit.ToFullString();
  }
  ```
- [x] Run `dotnet build src/Sharpy.Compiler` to verify it compiles
- [x] **Commit**: `feat(compiler): add GenerateCSharpForModule helper`

### Task 2.2: Update Compile method to generate C# for all modules
- [x] Open `src/Sharpy.Compiler/Compiler.cs`
- [x] Find the code generation section in `Compile` method (~line 240-270)
- [x] After generating C# for the entry file, add code to generate C# for imports:
  ```csharp
  // After: var csharpCode = compilationUnit.ToFullString();
  // Add the following:

  // Generate C# for all imported .spy modules
  var allGeneratedFiles = new Dictionary<string, string>();
  
  // Add entry file
  allGeneratedFiles[filePath] = csharpCode;
  
  // Add all imported modules
  foreach (var (modulePath, moduleInfo) in _importResolver?.LoadedSpyModules ?? 
      Enumerable.Empty<KeyValuePair<string, ModuleInfo>>())
  {
      // Skip the entry file (already added)
      if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(filePath), 
          StringComparison.OrdinalIgnoreCase))
          continue;

      var moduleCs = GenerateCSharpForModule(
          moduleInfo, symbolTable, builtinRegistry, 
          codeGenContext.ProjectNamespace);
      
      if (moduleCs != null)
      {
          allGeneratedFiles[modulePath] = moduleCs;
          _logger.LogInfo($"Generated C# for imported module: {Path.GetFileName(modulePath)}");
      }
  }
  ```
- [x] Update the success return statement to include `GeneratedCSharpFiles`:
  ```csharp
  return new CompilationResult
  {
      Success = true,
      Module = module,
      SymbolTable = symbolTable,
      SemanticInfo = semanticInfo,
      ModuleRegistry = _moduleRegistry,
      GeneratedCSharpCode = csharpCode,  // Keep for backward compatibility
      GeneratedCSharpFiles = allGeneratedFiles,  // NEW
      Metrics = metrics
  };
  ```
- [x] Run `dotnet build src/Sharpy.Compiler` to verify it compiles
- [x] **Commit**: `feat(compiler): generate C# for all imported modules`

### Task 2.3: Store ImportResolver reference for later use
- [x] The `Compile` method creates a local `ImportResolver` but we need access to it later
- [x] ~~Find where `importResolver` is created (~line 123)~~ (Not needed - local variable is accessible within same method)
- [x] ~~Store it in a field that can be accessed by `GenerateCSharpForModule`~~ (Not needed - using local variable directly)
- [x] Update the code generation section to use `importResolver.LoadedSpyModules`
- [x] Run `dotnet build src/Sharpy.Compiler` to verify it compiles
- [x] **Note**: Merged with Task 2.2 since local variable access works without additional refactoring

---

## Phase 3: Update CLI to Use Multi-File Results

### Task 3.1: Update CompileToBinary to use GeneratedCSharpFiles
- [x] Open `src/Sharpy.Cli/Program.cs`
- [x] Find the `CompileToBinary` method (~line 870)
- [x] Find where `csharpSources` dictionary is created (~line 920):
  ```csharp
  // OLD:
  var csharpSources = new Dictionary<string, string>
  {
      { Path.ChangeExtension(inputFile.FullName, ".cs"), result.GeneratedCSharpCode! }
  };
  ```
- [x] Replace with:
  ```csharp
  // NEW: Use all generated files (entry + imports)
  var csharpSources = new Dictionary<string, string>();
  foreach (var (sourcePath, csCode) in result.GeneratedCSharpFiles)
  {
      var csFileName = Path.ChangeExtension(sourcePath, ".cs");
      csharpSources[csFileName] = csCode;
  }

  // Fallback for backward compatibility if GeneratedCSharpFiles is empty
  if (csharpSources.Count == 0 && result.GeneratedCSharpCode != null)
  {
      csharpSources[Path.ChangeExtension(inputFile.FullName, ".cs")] = result.GeneratedCSharpCode;
  }
  ```
- [x] Run `dotnet build src/Sharpy.Cli` to verify it compiles
- [x] **Commit**: `feat(cli): use GeneratedCSharpFiles for multi-file compilation`

---

## Phase 4: Testing

### Task 4.1: Create manual test case
- [x] Create test directory and files:
  ```bash
  mkdir -p /tmp/sharpy_multifile_test
  ```
- [x] Create `/tmp/sharpy_multifile_test/main.spy`:
  ```python
  from helpers import greet

  def main():
      print(greet("World"))
  ```
- [x] Create `/tmp/sharpy_multifile_test/helpers.spy`:
  ```python
  def greet(name: str) -> str:
      return f"Hello, {name}!"
  ```
- [x] Run the test:
  ```bash
  cd /Users/anton/Documents/github/sharpy
  dotnet run --project src/Sharpy.Cli -- run /tmp/sharpy_multifile_test/main.spy
  ```
- [x] Verify output is: `Hello, World!`
- [x] **Manual test passed** (no separate commit needed - verification only)

### Task 4.2: Create automated integration test
- [x] Open or create `tests/Sharpy.Compiler.Tests/Integration/MultiFileCompilationTests.cs`
- [x] Add test class:
  ```csharp
  using Xunit;
  using Sharpy.Compiler;
  using System.IO;

  namespace Sharpy.Compiler.Tests.Integration;

  public class MultiFileCompilationTests : IDisposable
  {
      private readonly string _tempDir;

      public MultiFileCompilationTests()
      {
          _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
          Directory.CreateDirectory(_tempDir);
      }

      public void Dispose()
      {
          if (Directory.Exists(_tempDir))
              Directory.Delete(_tempDir, recursive: true);
      }

      [Fact]
      public void Compile_WithImportedModule_GeneratesCSharpForBothFiles()
      {
          // Arrange
          var mainPath = Path.Combine(_tempDir, "main.spy");
          var helpersPath = Path.Combine(_tempDir, "helpers.spy");

          File.WriteAllText(mainPath, @"
  from helpers import greet

  def main():
      print(greet(""World""))
  ");

          File.WriteAllText(helpersPath, @"
  def greet(name: str) -> str:
      return f""Hello, {name}!""
  ");

          var compiler = new Compiler();

          // Act
          var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);

          // Assert
          Assert.True(result.Success, string.Join(", ", result.Errors));
          Assert.True(result.GeneratedCSharpFiles.Count >= 2, 
              $"Expected at least 2 files, got {result.GeneratedCSharpFiles.Count}");
          Assert.Contains(result.GeneratedCSharpFiles.Keys, 
              k => k.Contains("main", StringComparison.OrdinalIgnoreCase));
          Assert.Contains(result.GeneratedCSharpFiles.Keys, 
              k => k.Contains("helpers", StringComparison.OrdinalIgnoreCase));
      }
  }
  ```
- [x] Run the test:
  ```bash
  dotnet test --filter "FullyQualifiedName~MultiFileCompilationTests"
  ```
- [x] Verify test passes (5 tests pass)
- [x] **Commit**: `test: add integration tests for multi-file compilation`

### Task 4.3: Run full test suite
- [x] Run all existing tests to check for regressions:
  ```bash
  cd /Users/anton/Documents/github/sharpy
  dotnet test
  ```
- [x] ~~Fix any failing tests~~ - No failures! All 4098 tests pass.
- [x] **No commit needed** - no regressions found

---

## Phase 5: Edge Cases and Cleanup

### Task 5.1: Handle transitive imports
- [x] Create test with A imports B, B imports C:
  ```
  /tmp/transitive_test/
    main.spy      -> from utils import format_greeting
    utils.spy     -> from helpers import greet; def format_greeting...
    helpers.spy   -> def greet...
  ```
- [x] Verify all three files are compiled
- [x] ~~If not working, debug `LoadedSpyModules` to ensure transitive modules are included~~ - Works correctly
- [x] **Commit**: `test: add transitive import test for multi-file compilation`

### Task 5.2: Handle circular import edge case
- [x] Verify circular imports still produce proper error messages
- [x] Create test case:
  ```
  a.spy -> from b import func_b
  b.spy -> from a import func_a
  ```
- [x] Verify error message is clear
- [x] **Commit**: `test: add circular import error handling test`

### Task 5.3: Update EmitCSharp command (optional enhancement)
- [ ] The `emit csharp` command currently only emits the entry file
- [ ] Consider updating `EmitCSharp` in Program.cs to also use `GeneratedCSharpFiles`
- [ ] Add `--all` flag to emit all files to a directory
- [ ] **Commit**: `feat(cli): add --all flag to emit csharp for multi-file projects`

---

## Verification Checklist

Before marking complete:

- [x] Manual test passes: `sharpyc run` works with multi-file project
- [x] All existing tests pass: `dotnet test` shows no regressions (4098 tests pass)
- [x] New integration test passes (7 new tests)
- [x] Code is clean: no commented-out code, proper logging
- [x] Edge cases handled: transitive imports, circular imports show errors

---

## Files Modified

1. `src/Sharpy.Compiler/Compiler.cs` - Main changes
2. `src/Sharpy.Compiler/Semantic/ImportResolver.cs` - Expose loaded modules
3. `src/Sharpy.Cli/Program.cs` - Use multi-file results
4. `tests/Sharpy.Compiler.Tests/Integration/MultiFileCompilationTests.cs` - New test

---

## Troubleshooting

### "Namespace 'X' does not exist" errors
- Check that `GeneratedCSharpFiles` includes the imported module
- Verify `LoadedSpyModules` returns the module after import resolution
- Check that `IsNetModule` is false for .spy files

### ImportResolver.LoadedSpyModules is empty
- Ensure `_moduleCache` is populated during `ResolveFromImport`
- Check that the file path normalization matches

### Generated C# has wrong namespace
- Check `ProjectNamespace` is passed consistently
- Verify `ComputeNamespace` in RoslynEmitter handles imported modules correctly

### Transitive imports not compiled
- Check that `ResolveModuleImports` in ImportResolver recursively processes imports
- Verify `_moduleCache` accumulates all discovered modules

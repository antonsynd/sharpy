# Test Infrastructure for Multi-File Compilation

This directory contains test helpers and infrastructure for testing multi-file compilation scenarios in the Sharpy compiler.

## Files

### ProjectCompilationHelper.cs

A comprehensive test helper class for creating, compiling, and executing multi-file Sharpy projects in tests.

**Features:**
- Automatic temporary directory management with cleanup
- Fluent API for building test projects
- Support for nested directories and packages
- Project file (.spyproj) generation
- Compilation and execution support
- Assertion helpers for test validation

**Key Classes:**
- `ProjectCompilationHelper` - Main helper class (implements `IDisposable`)
- `ProjectOptions` - Configuration options for test projects
- `ExecutionResult` - Result of executing a compiled assembly

### ProjectCompilationHelperTests.cs

Comprehensive test suite demonstrating all features of `ProjectCompilationHelper`.

**Test Categories:**
1. **Basic Compilation**
   - Single file projects
   - Multi-file projects
   - Nested directory structures

2. **Package Support**
   - Package initialization (`__init__.spy`)
   - Package modules
   - Complex multi-package scenarios

3. **Configuration**
   - Custom entry points
   - Output types (exe/library)
   - Root namespaces
   - Target frameworks

4. **Execution**
   - Compile and execute
   - Capture stdout/stderr
   - Execution error handling

5. **Error Handling**
   - Compilation errors
   - Type errors
   - Assertion helpers

6. **Utilities**
   - Temporary directory management
   - File tracking
   - Bulk file addition
   - Cleanup verification

## Usage Examples

### Basic Single File Test

```csharp
[Fact]
public void Test_SingleFileCompilation()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("MyTest")
        .AddSourceFile("main.spy", @"
def main():
    print('Hello, World!')
")
        .CreateProjectFile();

    var result = helper.Compile();

    result.Success.Should().BeTrue();
    result.Errors.Should().BeEmpty();
}
```

### Multi-File Project with Nested Directories

```csharp
[Fact]
public void Test_MultiFileProject()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("MyProject")
        .AddSourceFile("main.spy", @"
def main():
    print('Main application')
")
        .AddSourceFile("utils/helpers.spy", @"
def helper_func() -> int:
    return 42
")
        .AddSourceFile("utils/string_helpers.spy", @"
def double_string(s: str) -> str:
    return s + s
")
        .CreateProjectFile();

    var result = helper.Compile();

    result.Success.Should().BeTrue();
    result.GeneratedCSharpFiles.Should().HaveCount(3);
}
```

### Package with __init__.spy

```csharp
[Fact]
public void Test_PackageStructure()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("PackageTest")
        .AddPackage("mypackage", @"
# Package initialization
def init_func() -> str:
    return 'initialized'
")
        .AddPackageFile("mypackage", "module.spy", @"
def module_func() -> int:
    return 100
")
        .AddSourceFile("main.spy", @"
def main():
    print('Package test')
")
        .CreateProjectFile();

    var result = helper.Compile();
    result.Success.Should().BeTrue();
}
```

### Compile and Execute

```csharp
[Fact]
public void Test_CompileAndExecute()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("ExecuteTest")
        .AddSourceFile("main.spy", @"
def main():
    print('Test output')
    print('Line 2')
")
        .CreateProjectFile();

    var result = helper.CompileAndExecute();

    result.Success.Should().BeTrue();
    result.StandardOutput.Should().Contain("Test output");
    result.StandardOutput.Should().Contain("Line 2");
}
```

### Custom Entry Point

```csharp
[Fact]
public void Test_CustomEntryPoint()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("CustomEntry")
        .WithEntryPoint("startup.spy")
        .AddSourceFile("startup.spy", @"
def main():
    print('Custom entry point')
")
        .AddSourceFile("helpers.spy", @"
def helper() -> str:
    return 'help'
")
        .CreateProjectFile();

    var result = helper.Compile();
    result.Success.Should().BeTrue();
}
```

### Library Output Type

```csharp
[Fact]
public void Test_LibraryProject()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("MyLibrary")
        .WithOutputType("library")
        .AddSourceFile("lib.spy", @"
def public_api() -> int:
    return 42
")
        .CreateProjectFile();

    var result = helper.Compile();

    result.Success.Should().BeTrue();
    result.OutputAssemblyPath.Should().EndWith(".dll");
}
```

### Adding Multiple Files at Once

```csharp
[Fact]
public void Test_BulkFileAddition()
{
    using var helper = new ProjectCompilationHelper(output);

    var sourceFiles = new Dictionary<string, string>
    {
        ["main.spy"] = "def main(): print('Main')",
        ["module1.spy"] = "def func1() -> int: return 1",
        ["module2.spy"] = "def func2() -> int: return 2"
    };

    helper
        .WithRootNamespace("BulkTest")
        .AddSourceFiles(sourceFiles)
        .CreateProjectFile();

    var result = helper.Compile();
    result.Success.Should().BeTrue();
    result.GeneratedCSharpFiles.Should().HaveCount(3);
}
```

### Testing Compilation Errors

```csharp
[Fact]
public void Test_CompilationError()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("ErrorTest")
        .AddSourceFile("main.spy", @"
def main():
    x: int = 'not an int'  # Type error
    print(x)
")
        .CreateProjectFile();

    var result = helper.Compile();

    result.Success.Should().BeFalse();
    result.Errors.Should().NotBeEmpty();
}
```

### Using Assertion Helpers

```csharp
[Fact]
public void Test_AssertionHelpers()
{
    using var helper = new ProjectCompilationHelper(output);

    helper
        .WithRootNamespace("AssertTest")
        .AddSourceFile("main.spy", @"
def main():
    x: int = 'string'  # Type error
")
        .CreateProjectFile();

    var result = helper.Compile();

    // This will not throw since compilation failed
    helper.AssertCompilationFailed(result);

    // Can also check for specific error patterns
    helper.AssertCompilationFailed(result, "Cannot assign");
}
```

## API Reference

### ProjectCompilationHelper

#### Constructor
```csharp
public ProjectCompilationHelper(ITestOutputHelper? output = null)
```
Creates a new helper instance with an optional xUnit output helper for debugging.

#### Properties
- `string TempDirectory` - Gets the temporary directory path
- `string ProjectDirectory` - Gets the project directory path
- `string SourceDirectory` - Gets the source directory path (default: ProjectDirectory/src)
- `IReadOnlyList<string> SourceFiles` - Gets list of added source files
- `ProjectOptions Options` - Configuration options for the project

#### Configuration Methods
- `WithSourceDirectory(string relativePath)` - Sets custom source directory
- `WithRootNamespace(string rootNamespace)` - Sets project root namespace
- `WithOutputType(string outputType)` - Sets output type ("exe" or "library")
- `WithEntryPoint(string entryPoint)` - Sets custom entry point file

#### File Management Methods
- `AddSourceFile(string relativePath, string content)` - Adds a single source file
- `AddSourceFiles(Dictionary<string, string> files)` - Adds multiple source files
- `AddPackage(string packagePath, string initContent = "")` - Creates a package with __init__.spy
- `AddPackageFile(string packagePath, string fileName, string content)` - Adds file to a package

#### Project Operations
- `CreateProjectFile()` - Generates the .spyproj file
- `Compile()` - Compiles the project and returns result
- `CompileAndExecute()` - Compiles and executes, returning execution result

#### Assertion Helpers
- `AssertCompilationSucceeded(ProjectCompilationResult result)` - Asserts success, throws if failed
- `AssertCompilationFailed(ProjectCompilationResult result, string? expectedErrorPattern = null)` - Asserts failure

#### Disposal
Implements `IDisposable` - automatically cleans up temporary directories.

### ProjectOptions

Configuration class with properties:
- `string RootNamespace` (default: "TestProject")
- `string OutputType` (default: "exe")
- `string TargetFramework` (default: "net10.0")
- `string? AssemblyName`
- `string? EntryPoint`
- `string? SourceFilePattern`

### ExecutionResult

Result of executing a compiled assembly:
- `bool Success` - Whether execution succeeded
- `string StandardOutput` - Captured stdout
- `string StandardError` - Captured stderr
- `List<string> CompilationErrors` - Any compilation errors
- `Exception? Exception` - Exception if execution failed

## Best Practices

1. **Always use `using` statements** - The helper implements `IDisposable` and cleans up temp directories.

2. **Use FluentAssertions** - All tests use FluentAssertions for readable assertions.

3. **Pass ITestOutputHelper** - When debugging, pass the test output helper to see compilation details.

4. **Keep test code simple** - Use simple, valid Sharpy code that demonstrates the feature being tested.

5. **Test both success and failure paths** - Verify both successful compilation and proper error reporting.

6. **Use descriptive test names** - Test names should clearly indicate what scenario they're testing.

## Running Tests

```bash
# Run all helper tests
dotnet test --filter "FullyQualifiedName~ProjectCompilationHelperTests"

# Run all tests in the Helpers namespace
dotnet test --filter "FullyQualifiedName~Sharpy.Compiler.Tests.Helpers"

# Run a specific test
dotnet test --filter "FullyQualifiedName~Helper_CompilesSingleFileProject"
```

## Test Results

All 16 tests in `ProjectCompilationHelperTests` are passing:
- ✅ Helper_CompilesSingleFileProject
- ✅ Helper_CompilesMultiFileProject
- ✅ Helper_CompilesAndExecutesProject
- ✅ Helper_HandlesCompilationErrors
- ✅ Helper_SupportsPackageStructure
- ✅ Helper_SupportsNestedDirectories
- ✅ Helper_SupportsCustomEntryPoint
- ✅ Helper_SupportsLibraryOutputType
- ✅ Helper_AddMultipleSourceFilesAtOnce
- ✅ Helper_AssertCompilationSucceededHelper
- ✅ Helper_AssertCompilationFailedHelper
- ✅ Helper_AssertCompilationFailedWithPattern
- ✅ Helper_CleansUpTemporaryFiles
- ✅ Helper_ProvidesAccessToTempDirectories
- ✅ Helper_TracksAddedSourceFiles
- ✅ Helper_SupportsComplexMultiFileScenario

## Notes

- The helper creates unique temporary directories using `Guid.NewGuid()` to avoid conflicts
- All temporary directories are automatically cleaned up on disposal
- The helper supports both executable and library projects
- Package support includes `__init__.spy` initialization files
- Generated C# files are available in the compilation result for inspection

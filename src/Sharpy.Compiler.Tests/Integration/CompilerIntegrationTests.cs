using System.Linq;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for the full compilation pipeline with module support
/// </summary>
public class CompilerIntegrationTests
{
    [Fact]
    public void Compiler_WithDefaultConstructor_CompilesSuccessfully()
    {
        var code = @"
def main():
    x = 5
    y = 10
    z = x + y
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.NotNull(result.Module);
    }

    [Fact]
    public void Compiler_WithCompilerOptions_LoadsBuiltins()
    {
        var code = @"
def main():
    result = range(5)
";
        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("builtins", result.ModuleRegistry.GetLoadedModules());
    }

    [Fact]
    public void Compiler_WithSampleModule_LoadsSuccessfully()
    {
        var code = @"
def main():
    x = 5
";
        // Get the path to SampleModule.dll
        var sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Skip test if SampleModule hasn't been built
        if (!File.Exists(sampleModulePath))
        {
            Assert.True(true, $"Test skipped: SampleModule not found at {sampleModulePath}");
            return;
        }

        var options = new CompilerOptions
        {
            References = new[] { sampleModulePath }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("samplemodule", result.ModuleRegistry.GetLoadedModules());

        // Verify functions were discovered
        var functions = result.ModuleRegistry.GetModuleFunctions("samplemodule");
        Assert.NotEmpty(functions);
        Assert.Contains(functions, f => f.Name == "square");
        Assert.Contains(functions, f => f.Name == "cube");
        Assert.Contains(functions, f => f.Name == "average");
        Assert.Contains(functions, f => f.Name == "is_prime");
        Assert.Contains(functions, f => f.Name == "factorial");
    }

    [Fact]
    public void Compiler_WithInvalidReference_ReportsError()
    {
        var code = @"
def main():
    x = 5
";
        var options = new CompilerOptions
        {
            References = new[] { "NonExistent.dll" }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
        Assert.Contains(result.Diagnostics.GetErrors(), d => d.Message.Contains("NonExistent.dll"));
    }

    [Fact]
    public void Compiler_WithModulePath_AddsSuccessfully()
    {
        var code = @"
def main():
    x = 5
";
        var tempPath = Path.GetTempPath();
        var options = new CompilerOptions
        {
            ModulePaths = new[] { tempPath }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
    }

    [Fact]
    public void Compiler_WithMultipleReferences_LoadsAll()
    {
        var code = @"
def main():
    x = 5
";
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        var sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Only include SampleModule if it exists
        var references = File.Exists(sampleModulePath)
            ? new[] { sharpyCoreAssembly, sampleModulePath }
            : new[] { sharpyCoreAssembly };

        var options = new CompilerOptions
        {
            References = references
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("builtins", result.ModuleRegistry.GetLoadedModules());

        if (File.Exists(sampleModulePath))
        {
            Assert.Contains("samplemodule", result.ModuleRegistry.GetLoadedModules());
        }
    }

    [Fact]
    public void Compiler_WithLogger_LogsModuleLoading()
    {
        var code = @"
def main():
    x = 5
";
        var logOutput = new StringWriter();
        var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, logOutput, logOutput);

        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };

        var compiler = new Compiler(options, logger);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));

        var log = logOutput.ToString();
        Assert.Contains("Loaded module reference", log);
    }

    #region Parser Error Recovery Pipeline Tests

    [Fact]
    public void Compiler_WithMultipleSyntaxErrors_ReportsAllAndDoesNotCrash()
    {
        // Verify that the full compiler pipeline handles source with multiple
        // syntax errors: reports all errors without crashing or hanging.
        var code = @"
def ():
    pass

def ():
    pass

def main():
    pass
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);

        // Parser recovery should report at least 2 errors (one per broken def)
        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.True(errors.Count >= 2, $"Expected at least 2 errors, got {errors.Count}: {string.Join("; ", errors.Select(e => e.Message))}");
    }

    [Fact]
    public void Compiler_WithSyntaxErrors_BailsBeforeSemanticAnalysis()
    {
        // Verify that the compiler does not attempt semantic analysis when
        // parser errors exist. The Module should not be populated.
        var code = @"
def 123():
    pass

def main():
    pass
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);

        // Semantic fields should not be populated
        Assert.Null(result.SymbolTable);
        Assert.Null(result.SemanticInfo);
    }

    [Fact]
    public void Compiler_WithMixedErrorTypes_ReportsDistinctErrors()
    {
        // Source with different kinds of syntax errors. The compiler should
        // report distinct error messages for each.
        var code = @"
def foo(x: int)
    return x + 1

def 456():
    pass

class :
    pass

def main():
    pass
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);

        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.True(errors.Count >= 2, $"Expected at least 2 distinct errors, got {errors.Count}");

        // Errors should come from the Parser phase
        Assert.All(errors, e => Assert.Equal(Sharpy.Compiler.Diagnostics.CompilerPhase.Parser, e.Phase));
    }

    [Fact]
    public void Compiler_WithBlockLevelErrors_ReportsMultipleErrors()
    {
        // Errors inside a function body should all be reported.
        var code = @"
def main():
    x: int = 10
    def
    class
    y: int = 20
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);

        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.True(errors.Count >= 2, $"Expected at least 2 errors, got {errors.Count}");
    }

    #endregion

    #region Parser Error Recovery - Semantic Skipping

    [Fact]
    public void ParserErrors_PreventSemanticAnalysis_OnlyParserDiagnosticsPresent()
    {
        // Code with multiple parser errors - semantic analysis should be skipped entirely,
        // so we should only see parser-phase diagnostics, never semantic-phase ones.
        var code = @"
def ():
    pass

def 456():
    pass

def main():
    undefined_var + 1
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success, "Code with parser errors should not compile");

        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.True(errors.Count >= 2, $"Expected at least 2 parser errors, got {errors.Count}");

        // All diagnostics should be from the Parser phase - semantic analysis should be skipped
        foreach (var error in errors)
        {
            Assert.Equal(CompilerPhase.Parser, error.Phase);
        }
    }

    [Fact]
    public void ParserRecovery_ReportsMultipleDistinctErrors()
    {
        // Code with distinct error types at different locations
        var code = @"
def ():
    pass

class :
    pass

def valid_function():
    x: int = 42
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);

        var errors = result.Diagnostics.GetErrors().ToList();

        // Parser should recover from first error and report the second one too
        Assert.True(errors.Count >= 2,
            $"Expected at least 2 errors from recovery, got {errors.Count}: {string.Join("; ", errors.Select(e => e.Message))}");

        // Errors should be on different lines (not cascading from the same problem)
        var distinctLines = errors.Select(e => e.Line).Distinct().Count();
        Assert.True(distinctLines >= 2,
            $"Expected errors on at least 2 distinct lines, got {distinctLines}");
    }

    [Fact]
    public void ParserRecovery_PartialAstDoesNotReachSemanticPhases()
    {
        // Code where the first definition fails but the second is valid.
        // The compiler should return parser errors without attempting semantic analysis.
        var code = @"
def 123(x: int) -> int:
    return x

def main():
    pass
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);

        // Verify no semantic-phase diagnostics leaked through
        var semanticErrors = result.Diagnostics.GetErrors()
            .Where(e => e.Phase != CompilerPhase.Parser && e.Phase != CompilerPhase.Lexer)
            .ToList();

        Assert.Empty(semanticErrors);
    }

    #endregion

    #region Warning Pipeline Tests

    [Fact]
    public void Compiler_Compile_IncludesWarningsOnSuccessPath()
    {
        // Compile code that succeeds but should produce an unused variable warning.
        // This verifies that warnings from the validation pipeline are included
        // in CompilationResult.Diagnostics even when compilation succeeds.
        var code = @"
def foo() -> int:
    unused_var: int = 42
    return 0

def main():
    print(foo())
";
        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };
        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));

        var warnings = result.Diagnostics.GetWarnings();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.Validation.UnusedVariable
                                       && w.Message.Contains("unused_var"));
    }

    [Fact]
    public void Compiler_Compile_IncludesUnreachableCodeWarning()
    {
        var code = @"
def foo() -> int:
    return 42
    print(""unreachable"")

def main():
    print(foo())
";
        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };
        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));

        var warnings = result.Diagnostics.GetWarnings();
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.Validation.UnreachableCodeWarning);
    }

    #endregion
}

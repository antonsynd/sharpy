using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for import error recovery, which prevents cascading errors when imports fail.
/// When a module import fails, error recovery symbols are injected to suppress
/// "undefined identifier" errors that would otherwise cascade from the import failure.
/// </summary>
public class ImportErrorRecoveryTests : IntegrationTestBase
{
    public ImportErrorRecoveryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void FromImport_ModuleNotFound_NoUndefinedIdentifierError()
    {
        // When import fails, only the import error should be reported
        // The imported symbol should be available as an error recovery symbol
        var source = @"
from nonexistent_module import helper

def main():
    helper(42)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_MultipleSymbols_AllRecovered()
    {
        // Multiple imported symbols should all be recovered
        var source = @"
from nonexistent_module import foo, bar, baz

def main():
    foo()
    bar()
    baz()
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_WithValidTypeError_BothReported()
    {
        // Type errors unrelated to the import should still be reported
        var source = @"
from nonexistent_module import helper

def main():
    x: int = ""not an int""
";

        var result = CompileAndExecute(source);

        // Should have two errors: import error and type mismatch
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.Contains(result.CompilationErrors, e => e.Contains("Cannot assign"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_AliasedImport_NoUndefinedError()
    {
        // Aliased imports should also be recovered
        var source = @"
from nonexistent_module import helper as h

def main():
    h(42)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_UsedInExpression_NoUndefinedError()
    {
        // Error recovery symbols should work in expressions
        var source = @"
from nonexistent_module import helper

def main():
    x: int = helper(1) + helper(2)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_UsedAsVariable_NoUndefinedError()
    {
        // Error recovery symbols should work when used as variables (not called)
        var source = @"
from nonexistent_module import helper

def main():
    x = helper
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void SuccessfulImport_NoErrorRecovery()
    {
        // Successful imports should not create error recovery symbols
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", @"
from lib import greet

def main():
    greet()
")
            .AddSourceFile("lib.spy", @"
def greet():
    print(""Hello"")
")
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.Empty(result.Diagnostics.GetErrors());
    }

    [Fact]
    public void ErrorRecoverySymbol_HasCorrectProperties()
    {
        // Verify error recovery symbols have the correct properties set
        var logger = NullLogger.Instance;
        var builtinRegistry = new BuiltinRegistry();
        var resolver = new ImportResolver(logger);
        var symbolTable = new SymbolTable(builtinRegistry);

        // Parse a module with a failed import
        var source = "from nonexistent import foo";
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        // Resolve imports - this should create error recovery symbols
        resolver.ResolveAllImports(module, symbolTable, null);

        // The symbol should exist and be marked as error recovery
        var symbol = symbolTable.Lookup("foo");
        Assert.NotNull(symbol);
        Assert.True(symbol.IsErrorRecovery);
        Assert.Equal("nonexistent", symbol.OriginalModule);
    }

    [Fact]
    public void RegularImport_ModuleNotFound_NoUndefinedIdentifierError()
    {
        // Regular 'import' statement should also use error recovery
        var source = @"
import nonexistent

def main():
    x = nonexistent.foo(42)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void RegularImport_ModuleNotFound_MemberAccess_NoUndefinedError()
    {
        // Member access on error recovery modules should not produce cascading errors
        var source = @"
import nonexistent

def main():
    a = nonexistent.foo
    b = nonexistent.bar.baz
    c = nonexistent.helper(1, 2, 3)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("has no member"));
    }

    [Fact]
    public void RegularImport_ModuleNotFound_AliasedImport_NoUndefinedError()
    {
        // Aliased regular imports should also use error recovery
        var source = @"
import nonexistent as ne

def main():
    x = ne.foo(42)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Undefined"));
    }

    [Fact]
    public void RegularImport_ModuleNotFound_WithValidTypeError_BothReported()
    {
        // Type errors unrelated to the import should still be reported
        var source = @"
import nonexistent

def main():
    x: int = ""not an int""
    y = nonexistent.foo
";

        var result = CompileAndExecute(source);

        // Should have two errors: import error and type mismatch
        Assert.False(result.Success);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.Contains(result.CompilationErrors, e => e.Contains("Cannot assign"));
    }

    [Fact]
    public void RegularImport_ErrorRecoveryModule_HasCorrectProperties()
    {
        // Verify error recovery modules from regular imports have the correct properties
        var logger = NullLogger.Instance;
        var builtinRegistry = new BuiltinRegistry();
        var resolver = new ImportResolver(logger);
        var symbolTable = new SymbolTable(builtinRegistry);

        // Parse a module with a failed regular import
        var source = "import nonexistent";
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        // Resolve imports - this should create error recovery module
        resolver.ResolveAllImports(module, symbolTable, null);

        // The module symbol should exist and be marked as error recovery
        var symbol = symbolTable.Lookup("nonexistent");
        Assert.NotNull(symbol);
        Assert.IsType<ModuleSymbol>(symbol);
        Assert.True(symbol.IsErrorRecovery);
    }

    [Fact]
    public void FromImport_ModuleNotFound_UsedAsTypeAnnotation_NoTypeNotFoundError()
    {
        // Error recovery symbols should suppress "type not found" errors when used in type annotations
        var source = @"
from nonexistent_module import SomeType

def main():
    x: SomeType = None
    print(x)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Type") && e.Contains("not found"));
    }

    [Fact]
    public void FromImport_ModuleNotFound_UsedAsMultipleTypeAnnotations_NoTypeNotFoundErrors()
    {
        // Multiple uses of error recovery symbol as type annotation should all be suppressed
        var source = @"
from nonexistent_module import SomeType

def process(item: SomeType) -> SomeType:
    return item

def main():
    x: SomeType = None
    y: list[SomeType] = []
    print(x)
";

        var result = CompileAndExecute(source);

        // Should have exactly one error: the import error
        Assert.False(result.Success);
        Assert.Single(result.CompilationErrors, e => e.Contains("Cannot find module"));
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("Type") && e.Contains("not found"));
    }

    [Fact]
    public void MultiFile_ImportErrorInOneFile_TypeErrorInOtherFile_BothReported()
    {
        // Multi-file scenario: import fails in lib.spy, type error in main.spy
        // Both errors should be reported - the import error doesn't mask the type error
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", @"
from lib import helper

def main():
    # Type error unrelated to import
    x: int = ""not an int""
    print(x)
")
            .AddSourceFile("lib.spy", @"
# This file has a bad import
from nonexistent import foo

def helper() -> int:
    return 42
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Should fail with both errors: import error and type error
        Assert.False(result.Success, "Compilation should fail");
        var errors = result.Diagnostics.GetErrors().Select(d => d.Message).ToList();
        Assert.Contains(errors, e => e.Contains("Cannot find module 'nonexistent'"));
        Assert.Contains(errors, e => e.Contains("Cannot assign"));
    }

    [Fact]
    public void MultiFile_TransitiveImportError_NoCascadingErrors()
    {
        // Multi-file scenario: main imports lib which imports nonexistent
        // Only the import error should be reported, not cascading "undefined" errors
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", @"
from lib import process

def main():
    result = process(42)
    print(result)
")
            .AddSourceFile("lib.spy", @"
from nonexistent import helper

def process(x: int) -> int:
    return helper(x)
")
            .CreateProjectFile();

        var result = helper.Compile();

        // Should fail with import error but no cascading "undefined" errors
        Assert.False(result.Success, "Compilation should fail");
        var errors = result.Diagnostics.GetErrors().Select(d => d.Message).ToList();
        Assert.Contains(errors, e => e.Contains("Cannot find module 'nonexistent'"));
        Assert.DoesNotContain(errors, e => e.Contains("Undefined identifier 'helper'"));
    }
}

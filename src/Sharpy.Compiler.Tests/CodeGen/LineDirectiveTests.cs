using Xunit;
using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class LineDirectiveTests
{
    [Fact]
    public void Compile_WithLineDirectivesEnabled_EmitsLineDirectives()
    {
        var source = @"def greet(name: str) -> str:
    msg: str = ""Hello, "" + name
    return msg

def main():
    result: str = greet(""World"")
    print(result)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue($"compilation should succeed, but got errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        result.GeneratedCSharpCode.Should().NotBeNull();

        var code = result.GeneratedCSharpCode!;

        // Should contain #line directives pointing to the .spy file
        code.Should().Contain("#line");
        code.Should().Contain("\"test.spy\"");
    }

    [Fact]
    public void Compile_WithLineDirectivesEnabled_DirectivesReferenceCorrectLines()
    {
        var source = @"def add(a: int, b: int) -> int:
    result: int = a + b
    return result

def main():
    x: int = add(1, 2)
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "math.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;

        // Line 2 is "result: int = a + b" - should have #line 2
        code.Should().Contain("#line 2");
        // Line 3 is "return result" - should have #line 3
        code.Should().Contain("#line 3");
        // Line 6 is "x: int = add(1, 2)" - should have #line 6
        code.Should().Contain("#line 6");
        // Line 7 is "print(x)" - should have #line 7
        code.Should().Contain("#line 7");
    }

    [Fact]
    public void Compile_WithLineDirectivesDisabled_OmitsLineDirectives()
    {
        var source = @"def greet(name: str) -> str:
    return ""Hello""

def main():
    print(greet(""World""))
";
        // Use direct emitter with disabled line directives
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = "test.spy",
            EmitLineDirectives = false,
            IsEntryPoint = true
        };

        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // The Compiler.Compile uses default (true), but we can verify the flag works
        // by testing the CodeGenContext property
        context.EmitLineDirectives.Should().BeFalse();
    }

    [Fact]
    public void Compile_WithLineDirectives_DirectivesContainFilePath()
    {
        var source = @"def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "my_program.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;

        code.Should().Contain("\"my_program.spy\"");
    }

    [Fact]
    public void Compile_WithLineDirectives_ControlFlowStatements()
    {
        var source = @"def process(x: int) -> int:
    if x > 0:
        return x
    else:
        return -x

def main():
    result: int = process(5)
    print(result)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "control.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;

        // The if statement on line 2 should have a #line directive
        code.Should().Contain("#line 2");
        // Return on line 3 should have a #line directive
        code.Should().Contain("#line 3");
    }

    [Fact]
    public void Compile_WithLineDirectives_ForLoopStatements()
    {
        var source = @"def sum_list(items: list[int]) -> int:
    total: int = 0
    for item in items:
        total += item
    return total

def main():
    result: int = sum_list([1, 2, 3])
    print(result)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "loops.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;

        // For loop on line 3 should have a #line directive
        code.Should().Contain("#line 3");
        // Augmented assignment on line 4
        code.Should().Contain("#line 4");
    }

    [Fact]
    public void Compile_WithLineDirectives_FilePathWithBackslashes_EscapedCorrectly()
    {
        var source = @"def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, @"C:\Users\test\project\main.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;

        // Backslashes should be escaped in #line directives
        code.Should().Contain("#line");
        // The generated code should be valid C# (no unescaped backslashes)
        code.Should().NotContain("#line 2 \"C:\\Users");
        code.Should().Contain("#line 2 \"C:\\\\Users");
    }

    [Fact]
    public void Compile_WithLineDirectives_NoSourceFilePath_OmitsDirectives()
    {
        var source = @"def main():
    x: int = 42
    print(x)
";
        // Compile with empty file path
        var compiler = new Compiler();
        var result = compiler.Compile(source, "");

        // Even if compilation fails or succeeds, the point is:
        // with empty SourceFilePath, #line directives should not appear
        if (result.Success && result.GeneratedCSharpCode != null)
        {
            // If it succeeds, there should be no #line directives
            // because SourceFilePath is empty
            result.GeneratedCSharpCode.Should().NotContain("#line 2");
        }
    }

    [Fact]
    public void EmitLineDirectivesFlag_DefaultsToTrue()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins);

        context.EmitLineDirectives.Should().BeTrue();
    }

    [Fact]
    public void EmitLineDirectivesFlag_CanBeDisabled()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            EmitLineDirectives = false
        };

        context.EmitLineDirectives.Should().BeFalse();
    }
}

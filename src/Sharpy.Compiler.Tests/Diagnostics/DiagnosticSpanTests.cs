using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Tests verifying that compiler diagnostics carry TextSpan information
/// when reported from updated call sites.
/// </summary>
public class DiagnosticSpanTests
{
    private TypeChecker CompileToTypeChecker(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        typeChecker.CheckModule(module, isEntryPoint: false);
        return typeChecker;
    }

    [Fact]
    public void UndefinedVariable_DiagnosticHasSpan()
    {
        var source = @"
def main():
    print(xyz)
";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        errors.Should().Contain(e =>
            e.Code == DiagnosticCodes.Semantic.UndefinedVariable &&
            e.Message.Contains("xyz"));

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.UndefinedVariable);
        error.Span.Should().NotBeNull("updated call site should pass span from AST node");
    }

    [Fact]
    public void TypeMismatch_Assignment_DiagnosticHasSpan()
    {
        var source = @"
def main():
    x: int = ""hello""
";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        errors.Should().Contain(e =>
            e.Code == DiagnosticCodes.Semantic.TypeMismatch);

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.TypeMismatch);
        error.Span.Should().NotBeNull("variable declaration type mismatch should carry span");
    }

    [Fact]
    public void InvalidBinaryOperation_DiagnosticHasSpan()
    {
        var source = @"
def main():
    x: int = 1 + ""hello""
";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        errors.Should().Contain(e =>
            e.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);
        error.Span.Should().NotBeNull("binary operation error should carry span");
    }

    [Fact]
    public void WrongArgumentCount_DiagnosticHasSpan()
    {
        var source = @"
def foo(a: int, b: int) -> int:
    return a + b

def main():
    foo(1, 2, 3)
";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        errors.Should().Contain(e =>
            e.Code == DiagnosticCodes.Semantic.WrongArgumentCount);

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.WrongArgumentCount);
        error.Span.Should().NotBeNull("wrong argument count error should carry span");
    }

    [Fact]
    public void NonBoolCondition_DiagnosticHasSpan()
    {
        var source = @"
def main():
    if 42:
        pass
";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        errors.Should().Contain(e =>
            e.Code == DiagnosticCodes.Semantic.TypeMismatch &&
            e.Message.Contains("boolean"));

        var error = errors.First(e => e.Message.Contains("boolean"));
        error.Span.Should().NotBeNull("condition type mismatch should carry span from the test expression");
    }

    [Fact]
    public void DiagnosticWithoutSpan_StillWorks()
    {
        // Verify that diagnostics from non-updated call sites still work (span is null)
        var diagnostic = new CompilerDiagnostic(
            "Some error",
            CompilerDiagnosticSeverity.Error,
            Line: 5,
            Column: 10
        );

        diagnostic.Span.Should().BeNull();
        diagnostic.Line.Should().Be(5);
        diagnostic.Column.Should().Be(10);
    }
}

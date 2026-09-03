using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests the DFS-based const-cycle detection in <c>TypeChecker.DetectConstantCycles</c>.
/// The structural walk via <c>AstHelper.ContainsDescendant</c> names every constant on a
/// cycle regardless of expression kind (#1728).
/// </summary>
public class ConstantCycleDetectionTests
{
    private readonly ITestOutputHelper _output;

    public ConstantCycleDetectionTests(ITestOutputHelper output) => _output = output;

    private static (Module, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, typeChecker);
    }

    private int CountCircularConstantErrors(string source)
    {
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        var errors = typeChecker.Diagnostics.GetErrors().ToList();
        foreach (var e in errors)
            _output.WriteLine($"  {e.Code}: {e.Message}");
        return errors.Count(e => e.Code == DiagnosticCodes.Semantic.CircularConstantReference);
    }

    [Fact]
    public void IdentifierCycle_RaisesSPY0278_OnBothConstants()
    {
        var count = CountCircularConstantErrors(
            "const A: int = B\nconst B: int = A\n\ndef main():\n    print(A)\n");
        count.Should().Be(2, "both constants in the cycle are named");
    }

    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = A + 1\n", "function-call edge")]
    [InlineData("const A: int = max(B, 1)\nconst B: int = max(A, 1)\n", "both through function-call")]
    [InlineData("const A: int = B if True else 2\nconst B: int = A\n", "conditional edge")]
    [InlineData("const A: int = (B,)[0]\nconst B: int = A\n", "index-access edge")]
    public void CycleThroughAnyEdge_RaisesSPY0278_OnBothConstants(string decls, string why)
    {
        var count = CountCircularConstantErrors(decls + "\ndef main():\n    print(A)\n");
        count.Should().Be(2, why);
    }

    [Theory]
    [InlineData("const A: float = B\nconst B: float = A\n", "float cycle")]
    [InlineData("const A: str = B\nconst B: str = A\n", "str cycle")]
    [InlineData("const A: bool = B\nconst B: bool = A\n", "bool cycle")]
    public void NonIntegerCycle_RaisesSPY0278(string decls, string why)
    {
        var count = CountCircularConstantErrors(decls + "\ndef main():\n    print(A)\n");
        count.Should().Be(2, why);
    }
}

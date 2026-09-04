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

    /// <summary>
    /// A const initializer's VALUE flows through an immediately-invoked lambda, so the dependency
    /// walk must not stop at a <c>LambdaExpression</c> the way the walrus walk does — that boundary
    /// is about where a binding takes effect, not about where a value comes from. With the stop in
    /// place this cycle compiled and printed <c>0 1</c> (#1728).
    /// </summary>
    [Fact]
    public void CycleThroughLambdaBody_RaisesSPY0278_OnBothConstants()
    {
        var count = CountCircularConstantErrors(
            "const A: int = (lambda: B)()\nconst B: int = A + 1\n\ndef main():\n    print(A, B)\n");
        count.Should().Be(2, "an immediately-invoked lambda carries the dependency out of its body");
    }

    /// <summary>
    /// Class-level consts are in the same graph, keyed by their qualified spelling — the only way
    /// to name one from an initializer. Before this the pair reached codegen and came back as
    /// SPY0908 / CS0110 (#1728).
    /// </summary>
    [Theory]
    [InlineData("class C:\n    const A: int = C.B + 1\n    const B: int = C.A + 1\n", 2,
        "both class-level constants on the cycle are named")]
    [InlineData("const X: int = C.A + 1\n\nclass C:\n    const A: int = X + 1\n", 2,
        "a module const and a class const can close a cycle between them")]
    public void ClassLevelCycle_RaisesSPY0278(string decls, int expected, string why)
    {
        CountCircularConstantErrors(decls + "\ndef main():\n    print(1)\n")
            .Should().Be(expected, why);
    }

    /// <summary>
    /// The positive controls for every arm above: acyclic programs of the SAME shapes report
    /// nothing. Without these, a walker that reported SPY0278 on every const — or on none — could
    /// still satisfy the counts above.
    /// </summary>
    [Theory]
    [InlineData("const A: int = (lambda: B)()\nconst B: int = 4\n", "forward reference through a lambda")]
    [InlineData("def ident(x: int) -> int:\n    return x\n\nconst A: int = ident(5)\nconst B: int = A + 1\n",
        "an unfoldable acyclic chain is not a cycle")]
    [InlineData("class C:\n    const A: int = 4\n    const B: int = C.A + 1\n", "acyclic class-level chain")]
    [InlineData("const A: float = 1.0\nconst B: float = A + 1.0\n", "acyclic float chain")]
    public void AcyclicProgram_ReportsNoCircularConstantError(string decls, string why)
    {
        CountCircularConstantErrors(decls + "\ndef main():\n    print(1)\n")
            .Should().Be(0, why);
    }
}

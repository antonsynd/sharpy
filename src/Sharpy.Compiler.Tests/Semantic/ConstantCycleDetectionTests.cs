using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pins <c>TypeChecker.ReferencesUnfoldedConst</c> (the SPY0278 circular-constant edge walker)
/// and executes the cyclic probes plan-950124 Phase 3c Task 10 required.
///
/// The switch's conservative default means a cycle whose edge passes through a kind outside the
/// four arms is invisible: measured @ 277f54543 (verify round 2026-09-02, #1728) —
/// <list type="bullet">
///   <item><c>const A: int = B</c> / <c>const B: int = A</c> → SPY0278 on BOTH (control, executed).</item>
///   <item><c>A = max(B, 1)</c> / <c>B = A + 1</c> → SPY0278 on B only (the BinaryOp edge) plus a
///     SPY0220 "Cannot assign type 'T'" open-generic leak on A (executed: SPY0278 present).</item>
///   <item><c>A = max(B, 1)</c> / <c>B = max(A, 1)</c> → NO SPY0278 at all, only the 'T' leak —
///     the wrong-diagnostic shape. Documented here, NOT asserted (asserting the missing
///     diagnostic would pin the bug); #1728 owns it (P2).</item>
///   <item><c>A = B if True else 2</c> / <c>B = A</c> and <c>A = (B,)[0]</c> / <c>B = A</c> →
///     SPY0278 on B only (ConditionalExpression / IndexAccess edges not walked; executed:
///     SPY0278 present).</item>
/// </list>
/// mutation (4bd021e24): Parenthesized arm deleted → <c>ReferencesUnfoldedConst_SwitchArms_MatchExpected</c>
/// red (1 failed), restored → green.
/// </summary>
public class ConstantCycleDetectionTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/TypeChecker.cs";

    private readonly ITestOutputHelper _output;

    public ConstantCycleDetectionTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> ExpectedArms = new()
    {
        "Identifier",
        "UnaryOp",
        "BinaryOp",
        "Parenthesized",
    };

    [Fact]
    public void ReferencesUnfoldedConst_SwitchArms_MatchExpected()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ReferencesUnfoldedConst");
        Assert.NotEmpty(switchArms);

        _output.WriteLine($"Switch arms found: {switchArms.Count}");
        foreach (var arm in switchArms.OrderBy(a => a, StringComparer.Ordinal))
            _output.WriteLine($"  {arm}");

        Assert.True(switchArms.SetEquals(ExpectedArms),
            $"ReferencesUnfoldedConst switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(ExpectedArms))}\n" +
            $"  Missing from switch: {string.Join(", ", ExpectedArms.Except(switchArms))}");
    }

    #region Cyclic probes (#1728)

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
        // Control: both edges are Identifier arms, so both constants are named.
        var count = CountCircularConstantErrors(
            "const A: int = B\nconst B: int = A\n\ndef main():\n    print(A)\n");
        count.Should().Be(2, "both constants in the cycle are named (control)");
    }

    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = A + 1\n", "BinaryOp edge on B")]
    [InlineData("const A: int = B if True else 2\nconst B: int = A\n", "Identifier edge on B; ConditionalExpression edge on A not walked")]
    [InlineData("const A: int = (B,)[0]\nconst B: int = A\n", "Identifier edge on B; IndexAccess edge on A not walked")]
    public void CycleThroughOneWalkedEdge_RaisesSPY0278(string decls, string why)
    {
        // The cycle is detected through the edge the switch walks; the other side is not named
        // (#1728 — a structural walk names both, as the control does).
        var count = CountCircularConstantErrors(decls + "\ndef main():\n    print(A)\n");
        count.Should().BeGreaterThanOrEqualTo(1, why);
    }

    #endregion
}

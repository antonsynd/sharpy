using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>HoverService</c> dispatch switches.
/// Each fact pins the arm set via <see cref="SwitchArmScan"/>. Kinds with no hover content
/// hit the default (null) — that is contractual; a kind that should have hover but does not
/// is a missing arm caught by this guard.
/// </summary>
public class HoverDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public HoverDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // GetHoverMarkdownForNode has a base-type `Expression` catch-all arm in addition to
    // the specific Node-kind arms. SwitchArmScan picks up the type name from the pattern.
    private static readonly HashSet<string> GetHoverMarkdownExpected = new()
    {
        nameof(Identifier),
        nameof(MemberAccess),
        nameof(FunctionCall),
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(WithStatement),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(AwaitExpression),
        nameof(YieldStatement),
        nameof(ReturnStatement),
        nameof(SuperExpression),
        nameof(BinaryOp),
        nameof(UnaryOp),
        nameof(ComparisonChain),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(LambdaExpression),
        nameof(Expression),
        "GenericType",
        "ResultType",
        "OptionalType",
        "BuiltinType",
    };

    [Fact]
    public void GetHoverMarkdownForNode_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/HoverService.cs",
            "GetHoverMarkdownForNode");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GetHoverMarkdownForNode arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(GetHoverMarkdownExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(GetHoverMarkdownExpected))}\n" +
            $"  Missing: {string.Join(", ", GetHoverMarkdownExpected.Except(arms))}");
    }

    private static readonly HashSet<string> TryNarrowHighlightExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(AwaitExpression),
        nameof(ReturnStatement),
    };

    [Fact]
    public void TryNarrowHighlight_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/HoverService.cs",
            "TryNarrowHighlight");
        Assert.NotEmpty(arms);
        _output.WriteLine($"TryNarrowHighlight arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(TryNarrowHighlightExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(TryNarrowHighlightExpected))}\n" +
            $"  Missing: {string.Join(", ", TryNarrowHighlightExpected.Except(arms))}");
    }

    private static readonly HashSet<string> TryNarrowToKeywordExpected = new()
    {
        nameof(YieldStatement),
        nameof(UnaryOp),
    };

    [Fact]
    public void TryNarrowToKeyword_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/HoverService.cs",
            "TryNarrowToKeyword");
        Assert.NotEmpty(arms);
        _output.WriteLine($"TryNarrowToKeyword arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(TryNarrowToKeywordExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(TryNarrowToKeywordExpected))}\n" +
            $"  Missing: {string.Join(", ", TryNarrowToKeywordExpected.Except(arms))}");
    }

    private static readonly HashSet<string> GetDecoratorsExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
    };

    [Fact]
    public void GetDecorators_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/HoverService.cs",
            "GetDecorators");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GetDecorators arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(GetDecoratorsExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(GetDecoratorsExpected))}\n" +
            $"  Missing: {string.Join(", ", GetDecoratorsExpected.Except(arms))}");
    }

    private static readonly HashSet<string> GetBodyExpected = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(PropertyDef),
    };

    [Fact]
    public void GetBody_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/HoverService.cs",
            "GetBody");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GetBody arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(GetBodyExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(GetBodyExpected))}\n" +
            $"  Missing: {string.Join(", ", GetBodyExpected.Except(arms))}");
    }
}

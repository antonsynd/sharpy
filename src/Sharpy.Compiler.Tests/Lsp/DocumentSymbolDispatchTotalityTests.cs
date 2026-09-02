using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>DocumentSymbolHandler</c> dispatch switches.
/// Kinds with no outline symbol hit the default (null) — that is contractual.
/// </summary>
public class DocumentSymbolDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public DocumentSymbolDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> ConvertStatementExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(TypeAlias),
    };

    [Fact]
    public void ConvertStatement_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs",
            "ConvertStatement");
        Assert.NotEmpty(arms);
        _output.WriteLine($"ConvertStatement arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(ConvertStatementExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(ConvertStatementExpected))}\n" +
            $"  Missing: {string.Join(", ", ConvertStatementExpected.Except(arms))}");
    }

    private static readonly HashSet<string> ConvertClassMemberExpected = new()
    {
        nameof(FunctionDef),
        nameof(PropertyDef),
        nameof(EventDef),
        nameof(VariableDeclaration),
    };

    [Fact]
    public void ConvertClassMember_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs",
            "ConvertClassMember");
        Assert.NotEmpty(arms);
        _output.WriteLine($"ConvertClassMember arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(ConvertClassMemberExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(ConvertClassMemberExpected))}\n" +
            $"  Missing: {string.Join(", ", ConvertClassMemberExpected.Except(arms))}");
    }
}

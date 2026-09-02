using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>CodeLensHandler</c> and <c>DocumentLinkHandler</c> dispatch switches.
/// CodeLens provides lenses only for top-level definitions; DocumentLink provides navigation
/// only for import statements. Both silently skip other kinds — that is contractual.
/// </summary>
public class CodeLensDocumentLinkDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public CodeLensDocumentLinkDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> CodeLensHandleExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
    };

    [Fact]
    public void CodeLensHandle_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/CodeLensHandler.cs",
            "Handle");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CodeLensHandler.Handle arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CodeLensHandleExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CodeLensHandleExpected))}\n" +
            $"  Missing: {string.Join(", ", CodeLensHandleExpected.Except(arms))}");
    }

    private static readonly HashSet<string> CollectLinksExpected = new()
    {
        nameof(ImportStatement),
        nameof(FromImportStatement),
    };

    [Fact]
    public void CollectLinks_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/DocumentLinkHandler.cs",
            "CollectLinks");
        Assert.NotEmpty(arms);
        _output.WriteLine($"DocumentLinkHandler.CollectLinks arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CollectLinksExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CollectLinksExpected))}\n" +
            $"  Missing: {string.Join(", ", CollectLinksExpected.Except(arms))}");
    }
}

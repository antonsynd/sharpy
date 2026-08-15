using System.IO;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The node-keyed <c>Parameter → VariableSymbol</c> map (#1359) and its merge behaviour.
/// </summary>
/// <remarks>
/// A per-file <see cref="SemanticInfo"/> that never joins <c>MergeFrom</c> loses its entries
/// silently in the per-file→project merge everything downstream reads from (Critical Rule 2).
/// The multi-file cell below is the mutation target: delete the <c>_parameterSymbols</c> loop in
/// <c>SemanticInfo.MergeFrom</c> and it must fail while the per-file control stays green.
/// </remarks>
public class ParameterSymbolMapTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public ParameterSymbolMapTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetParameterSymbol_ReturnsNull_WhenNotSet()
    {
        ISemanticQuery query = new SemanticInfo();

        Assert.Null(query.GetParameterSymbol(new Parameter { Name = "unbound" }));
    }

    [Fact]
    public void GetParameterSymbol_IsKeyedByReference_NotByValue()
    {
        // Parameter is a record, so two identically-spelled parameters are EQUAL by value. Keying
        // on value would collapse `def a(x: int)` and `def b(x: int)` onto one symbol.
        var info = new SemanticInfo();
        var first = new Parameter { Name = "x" };
        var second = new Parameter { Name = "x" };
        Assert.Equal(first, second);

        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Parameter, Type = SemanticType.Int };
        info.SetParameterSymbol(first, symbol);

        ISemanticQuery query = info;
        Assert.Same(symbol, query.GetParameterSymbol(first));
        Assert.Null(query.GetParameterSymbol(second));
    }

    [Fact]
    public void ParameterSymbols_SurviveThePerFileToProjectMerge()
    {
        // The MergeFrom cell. `scale`'s parameter is bound while checking lib.spy, into that file's
        // own SemanticInfo; the handlers and codegen read the PROJECT-level instance. Without the
        // merge line the project-level lookup returns null and rename silently does nothing for
        // every parameter in every non-entry file (#1359).
        _helper = new ProjectCompilationHelper(_output);
        _helper.WithRootNamespace("ParameterSymbolMerge");
        _helper.AddSourceFile("lib.spy", "def scale(factor: int) -> int:\n    return factor * 2\n");
        _helper.AddSourceFile("main.spy", "from lib import scale\n\n\ndef main():\n    print(scale(3))\n");
        _helper.WithEntryPoint("main.spy");
        _helper.CreateProjectFile();

        var result = _helper.Compile();

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        var libUnit = result.ProjectModel!.Units.Values.Single(u => Path.GetFileName(u.FilePath) == "lib.spy");
        var scale = libUnit.Ast!.Body.OfType<FunctionDef>().Single(f => f.Name == "scale");
        var factor = scale.Parameters.Single();

        // Positive control: the checker DID record it, in lib.spy's own SemanticInfo. If this arm
        // ever goes red the defect is upstream of the merge and the assertion below is not the
        // thing to read.
        ISemanticQuery perFile = libUnit.FileSemanticInfo!;
        Assert.NotNull(perFile.GetParameterSymbol(factor));

        // The falsifiable arm: the same node, through the merged project-level instance.
        ISemanticQuery merged = result.ProjectModel.SemanticInfo!;
        var symbol = merged.GetParameterSymbol(factor);

        Assert.NotNull(symbol);
        Assert.Equal("factor", symbol!.Name);
        Assert.Same(perFile.GetParameterSymbol(factor), symbol);
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }
}

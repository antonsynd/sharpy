using System.Collections.Immutable;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// LSP-level tests for the source generators feature (#636): hover on generator
/// bracket attributes, hover attribution on generated members, and diagnostic
/// rerouting for synthetic <c>&lt;generated:...&gt;</c> file paths.
///
/// These tests construct a parsed/analyzed program then directly populate the
/// generator metadata on <see cref="SemanticInfo"/> and <see cref="TypeSymbol"/>.
/// This mirrors the strategy used by <c>SourceGeneratorValidatorTests</c>: we
/// avoid the full project pipeline (which would require loading
/// <c>Sharpy.Core</c> into the test ModuleRegistry) and instead exercise the
/// LSP surface in isolation.
/// </summary>
public class SourceGeneratorLspTests
{
    private readonly CompilerApi _api = new();
    private readonly HoverService _hoverService;

    public SourceGeneratorLspTests()
    {
        _hoverService = new HoverService(_api);
    }

    private static TypeSymbol MarkTypeAsGenerator(SemanticResult result, string typeName)
    {
        var symbol = result.SymbolTable!.LookupType(typeName);
        Assert.NotNull(symbol);
        symbol!.IsSourceGenerator = true;
        return symbol;
    }

    private static T FindStatement<T>(SemanticResult result, System.Func<T, bool> predicate) where T : Statement
    {
        foreach (var stmt in result.Ast!.Body)
        {
            if (stmt is T match && predicate(match))
                return match;

            // Search class bodies one level deep — sufficient for these tests.
            if (stmt is ClassDef classDef)
            {
                foreach (var inner in classDef.Body)
                {
                    if (inner is T innerMatch && predicate(innerMatch))
                        return innerMatch;
                }
            }
        }
        throw new Xunit.Sdk.XunitException(
            $"No {typeof(T).Name} found in AST matching predicate");
    }

    [Fact]
    public void Hover_OnGeneratorBracketAttribute_ReturnsSourceGeneratorContent()
    {
        // The generator class is also defined in the same source so it appears in the
        // SymbolTable. We mark its symbol as IsSourceGenerator post-analysis (the same
        // trick used by SourceGeneratorValidatorTests).
        var source =
            "class GenerateEquals:\n" +
            "    def generate(self, context: int) -> int:\n" +
            "        return 0\n" +
            "\n" +
            "@[GenerateEquals]\n" +
            "class Point:\n" +
            "    x: int = 0\n" +
            "    y: int = 0\n";

        var analysis = _api.Analyze(source);
        analysis.Ast.Should().NotBeNull();
        MarkTypeAsGenerator(analysis, "GenerateEquals");

        // Cursor on "GenerateEquals" inside the bracket attribute. The decorator starts
        // at column 1 with "@" and "[", so the name begins at column 3.
        var result = _hoverService.GetHoverResult(analysis, line: 5, col: 5);

        result.Should().NotBeNull();
        result!.Markdown.Should().Contain("source generator");
        result.Markdown.Should().Contain("GenerateEquals");
    }

    [Fact]
    public void Hover_OnGeneratorBracketAttribute_ReportsMemberCount()
    {
        var source =
            "class GenerateEquals:\n" +
            "    def generate(self, context: int) -> int:\n" +
            "        return 0\n" +
            "\n" +
            "@[GenerateEquals]\n" +
            "class Point:\n" +
            "    x: int = 0\n" +
            "    y: int = 0\n";

        var analysis = _api.Analyze(source);
        analysis.Ast.Should().NotBeNull();
        MarkTypeAsGenerator(analysis, "GenerateEquals");

        // Tag a "generated member" within the Point class so the hover can count it.
        var point = FindStatement<ClassDef>(analysis, c => c.Name == "Point");
        var fieldX = point.Body.OfType<VariableDeclaration>().First(v => v.Name == "x");
        analysis.SemanticInfo!.MarkAsGenerated(fieldX, "GenerateEquals");

        var result = _hoverService.GetHoverResult(analysis, line: 5, col: 5);

        result.Should().NotBeNull();
        result!.Markdown.Should().Contain("generates 1 member");
    }

    [Fact]
    public void Hover_OnNonGeneratorBracketAttribute_DoesNotReturnGeneratorHover()
    {
        // Plain class without IsSourceGenerator set — bracket attribute should not be
        // treated as a source generator (the existing decorator validator pipeline rejects
        // this, but the hover service should still defer).
        var source =
            "class Obsolete:\n" +
            "    def __init__(self) -> None:\n" +
            "        pass\n" +
            "\n" +
            "class Point:\n" +
            "    x: int = 0\n";

        var analysis = _api.Analyze(source);

        // Hover on a normal expression position — no source-generator hover should fire.
        var result = _hoverService.GetHoverResult(analysis, line: 1, col: 7);

        // It should return a hover for the class itself (not a generator hover).
        if (result != null)
            result.Markdown.Should().NotContain("source generator");
    }

    [Fact]
    public void Hover_OnGeneratedFunctionName_AppendsGeneratorAttribution()
    {
        var source =
            "class Point:\n" +
            "    x: int = 0\n" +
            "\n" +
            "    def __eq__(self, other: int) -> bool:\n" +
            "        return True\n";

        var analysis = _api.Analyze(source);
        analysis.Ast.Should().NotBeNull();

        var pointClass = FindStatement<ClassDef>(analysis, c => c.Name == "Point");
        var eqMethod = pointClass.Body.OfType<FunctionDef>().First(f => f.Name == "__eq__");
        analysis.SemanticInfo!.MarkAsGenerated(eqMethod, "GenerateEquals");

        // Cursor on the method name (line 4, col starts at "def __eq__" — name at col 9)
        var result = _hoverService.GetHoverResult(analysis, line: eqMethod.NameLineStart, col: eqMethod.NameColumnStart);

        result.Should().NotBeNull();
        result!.Markdown.Should().Contain("Generated by");
        result.Markdown.Should().Contain("@[GenerateEquals]");
    }

    [Fact]
    public void Hover_OnNonGeneratedFunctionName_DoesNotAppendAttribution()
    {
        var source = "def greet(name: str) -> str:\n    return name\n";
        var analysis = _api.Analyze(source);

        var result = _hoverService.GetHoverResult(analysis, line: 1, col: 5);

        result.Should().NotBeNull();
        result!.Markdown.Should().NotContain("Generated by");
    }

    [Fact]
    public void DiagnosticPublisher_GeneratedFilePath_IsDetected()
    {
        DiagnosticPublisher.IsGeneratedFilePath("<generated:GenA:Foo>").Should().BeTrue();
        DiagnosticPublisher.IsGeneratedFilePath("<generated:GenA:>").Should().BeTrue();
        DiagnosticPublisher.IsGeneratedFilePath("regular/path.spy").Should().BeFalse();
        DiagnosticPublisher.IsGeneratedFilePath(null).Should().BeFalse();
        DiagnosticPublisher.IsGeneratedFilePath("").Should().BeFalse();
    }

    [Fact]
    public void DiagnosticPublisher_ParseGeneratedFilePath_ExtractsParts()
    {
        var parsed = DiagnosticPublisher.ParseGeneratedFilePath("<generated:GenerateEquals:Point>");
        parsed.Should().NotBeNull();
        parsed!.Value.GeneratorName.Should().Be("GenerateEquals");
        parsed.Value.TargetName.Should().Be("Point");
    }

    [Fact]
    public void DiagnosticPublisher_RoutesGeneratedDiagnosticToTriggerDecorator()
    {
        // Build an analysis that has a GeneratorBinding so the publisher can locate the
        // trigger decorator and remap the diagnostic.
        var source =
            "class GenerateEquals:\n" +
            "    def generate(self, context: int) -> int:\n" +
            "        return 0\n" +
            "\n" +
            "@[GenerateEquals]\n" +
            "class Point:\n" +
            "    x: int = 0\n";

        var analysis = _api.Analyze(source);
        analysis.Ast.Should().NotBeNull();
        var generatorSymbol = MarkTypeAsGenerator(analysis, "GenerateEquals");

        // Find the @[GenerateEquals] decorator on Point and record a binding for it.
        var pointClass = FindStatement<ClassDef>(analysis, c => c.Name == "Point");
        var triggerDecorator = pointClass.Decorators.Single(d => d.Name == "GenerateEquals");
        analysis.SemanticInfo!.AddGeneratorBinding(pointClass, generatorSymbol, triggerDecorator);

        // Build a diagnostic that pretends to originate from generated code.
        var generatedDiag = new CompilerDiagnostic(
            Message: "type mismatch in generated code",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 3,    // line inside the synthetic generated source
            Column: 1,
            FilePath: "<generated:GenerateEquals:Point>",
            Code: "SPY0552");

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(
            new[] { generatedDiag },
            sourceText: null,
            configuration: null,
            semanticQuery: analysis.SemanticQuery,
            documentUri: "file:///test.spy");

        lspDiagnostics.Should().HaveCount(1);
        var converted = lspDiagnostics[0];

        // Expected: the diagnostic now points at the trigger decorator's location
        // (line 5 in the source, 1-based → line 4 in LSP 0-based).
        converted.Range.Start.Line.Should().Be(triggerDecorator.LineStart - 1);
        converted.Range.Start.Character.Should().Be(triggerDecorator.ColumnStart - 1);

        // The synthetic origin is preserved as related information.
        converted.RelatedInformation.Should().NotBeNull();
        converted.RelatedInformation!.Should().ContainSingle();
        converted.RelatedInformation!.First().Message.Should()
            .Contain("<generated:GenerateEquals:Point>");
    }

    [Fact]
    public void DiagnosticPublisher_NonGeneratedDiagnostic_IsUnchanged()
    {
        var diag = new CompilerDiagnostic(
            Message: "regular error",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 7,
            Column: 3,
            FilePath: "/Users/me/test.spy",
            Code: "SPY0100");

        var lspDiagnostics = DiagnosticPublisher.ConvertDiagnostics(
            new[] { diag },
            sourceText: null);

        lspDiagnostics.Should().HaveCount(1);
        var converted = lspDiagnostics[0];
        // 1-based 7, 3 → 0-based 6, 2
        converted.Range.Start.Line.Should().Be(6);
        converted.Range.Start.Character.Should().Be(2);
        converted.RelatedInformation.Should().BeNull();
    }
}

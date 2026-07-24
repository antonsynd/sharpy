using System;
using System.IO;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Conformance guard for statement-scoped <c>@suppress</c> on imports (#1124). A suppress-decorated
/// import is wrapped in a <see cref="DecoratedStatement"/>, so every <c>module.Body</c> scan that
/// type-tests for an import must unwrap via <see cref="StatementExtensions.UnwrapDecorated"/> first.
/// These tests feed a decorated import through the Pass-1/1.5 consumers (dependency extraction,
/// invariant checks, import resolution, using-directive generation) and assert it is still seen —
/// the durable defense against a future hand-rolled scan re-introducing the raw <c>is</c> test.
///
/// LSP consumers live in a separate project and are guarded in <c>Sharpy.Lsp.Tests</c>: document
/// links by <c>DocumentLinkTests.DecoratedPlainImport_ProducesSameLinkAsUndecoratedTwin</c> /
/// <c>DecoratedFromImport_ProducesSameLinkAsUndecoratedTwin</c>, and hover by
/// <c>HoverServiceTests.GetHoverMarkdown_OverDecoratedImport_MatchesUndecoratedTwin</c> /
/// <c>GetHoverMarkdown_OverDecoratedFromImport_MatchesUndecoratedTwin</c> (#1125). Any future
/// <c>module.Body</c> import-scan sweep must cover both projects — #1124's D4 sweep stopped at the
/// compiler boundary and left these two LSP consumers type-testing the bare import type.
/// </summary>
[Collection("HeavyCompilation")]
public class DecoratedImportConformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public DecoratedImportConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose() => _helper?.Dispose();

    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void UnwrapDecorated_WrappedImport_ReturnsInnerImport()
    {
        var module = Parse("@suppress(\"SPY0452\")\nfrom foo import bar\n");
        var top = Assert.IsType<DecoratedStatement>(module.Body[0]);

        var unwrapped = ((Statement)top).UnwrapDecorated();

        Assert.IsType<FromImportStatement>(unwrapped);
    }

    [Fact]
    public void UnwrapDecorated_PlainStatement_ReturnsSameInstance()
    {
        var module = Parse("import foo\n");
        var plain = module.Body[0];

        Assert.Same(plain, plain.UnwrapDecorated());
    }

    [Fact]
    public void CompilationUnitFactory_ExtractsDependency_FromDecoratedImport()
    {
        // The compilation-unit dependency extraction (Pass-1.5 input) must see the wrapped import.
        var unit = new CompilationUnit(
            filePath: "test.spy",
            modulePath: "test",
            sourceText: "@suppress(\"SPY0452\")\nfrom foo import bar\n");

        Assert.True(CompilationUnitFactory.Lex(unit));
        Assert.True(CompilationUnitFactory.Parse(unit));

        var fromImport = Assert.Single(unit.FromImports);
        Assert.Equal("foo", fromImport.Module);
    }

    [Fact]
    public void CompilerInvariants_DecoratedImport_ProducesNoSpanViolation()
    {
        // AssertStatementsHaveSpans exempts imports; a decorated import must receive the same
        // exemption after unwrapping rather than tripping the SPY0904 missing-span invariant.
        var module = Parse("@suppress(\"SPY0452\")\nfrom foo import bar\n");
        var diagnostics = new DiagnosticBag();

        CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

        Assert.DoesNotContain(diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Infrastructure.InvariantViolation);
    }

    [Fact]
    public void DecoratedImport_ProjectMode_ResolvesDependency_And_Suppresses()
    {
        // End-to-end through the multi-file pipeline: a suppress-decorated import of a USED sibling
        // must drive dependency-graph extraction, import resolution, and using-directive generation,
        // while silencing the unused-import warning for the unused name it also imports.
        _helper = new ProjectCompilationHelper(_output);
        _helper.WithRootNamespace("DecoratedImportConformance");
        _helper.AddSourceFile("helpers.spy",
            "def add(a: int, b: int) -> int:\n    return a + b\n\n\ndef multiply(a: int, b: int) -> int:\n    return a * b\n");
        _helper.AddSourceFile("main.spy",
            "@suppress(\"SPY0452\")\nfrom helpers import add, multiply\n\n\ndef main():\n    print(add(2, 3))\n");
        _helper.WithEntryPoint("main.spy");

        var result = _helper.Compile();

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");

        // Dependency-graph extraction saw the wrapped import: main depends on helpers.
        var mainPath = result.Dependencies!.AllFiles.Single(f => Path.GetFileName(f) == "main.spy");
        var deps = result.Dependencies.GetDirectDependencies(mainPath);
        Assert.Contains(deps, d => Path.GetFileName(d) == "helpers.spy");

        // Success implies import resolution + using-directive generation fired through the wrapper:
        // `add` is called across the module boundary, so the emitted C# only compiles if the using
        // directive for helpers was generated from the decorated import.

        // The unused `multiply` import is silenced by @suppress("SPY0452").
        Assert.DoesNotContain(result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.UnusedImport);
    }

    [Fact]
    public void DecoratedImport_ProjectMode_UnrelatedCode_StillWarnsUnusedImport()
    {
        // Key regression: an @suppress with an UNRELATED code must not make the import invisible —
        // SPY0452 must still fire for the unused name.
        _helper = new ProjectCompilationHelper(_output);
        _helper.WithRootNamespace("DecoratedImportConformanceUnrelated");
        _helper.AddSourceFile("helpers.spy",
            "def add(a: int, b: int) -> int:\n    return a + b\n\n\ndef multiply(a: int, b: int) -> int:\n    return a * b\n");
        _helper.AddSourceFile("main.spy",
            "@suppress(\"SPY0480\")\nfrom helpers import add, multiply\n\n\ndef main():\n    print(add(2, 3))\n");
        _helper.WithEntryPoint("main.spy");

        var result = _helper.Compile();

        Assert.True(result.Success,
            $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.Contains(result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.UnusedImport);
    }
}

using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// Function definitions and calls from the typed generator. Every lane reads its generator through
/// <see cref="SemanticFilter"/> with a <see cref="RejectionSink"/> and asserts no unexplained
/// rejection: every raw sample analyses clean (or is a pinned known red), and the accepted programs
/// satisfy the lane's own fact. Before #2288 each lane passed with half (or a quarter) of its
/// samples refused or throwing.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class FunctionSemanticsPropertyTests
{
    private readonly ITestOutputHelper _output;

    public FunctionSemanticsPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FunctionDef_IsResolvableByName()
    {
        var sink = new RejectionSink("FunctionSemantics.FunctionDef_IsResolvableByName");
        int resolved = 0;

        SemanticFilter.WellTypedProgram(GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2), sink)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var result = new Sharpy.Compiler.Compiler().Analyze(source, "func_test.spy");
                var symbols = result.SymbolTable
                    ?? throw new Exception($"An admitted program has no symbol table:\n{source}");

                var funcNames = module.Body
                    .OfType<Sharpy.Compiler.Parser.Ast.FunctionDef>()
                    .Where(f => f.Name != "main")
                    .Select(f => f.Name)
                    .ToList();
                if (funcNames.Count == 0)
                    throw new Exception($"ModuleWithFunctions drew a module without a function besides main:\n{source}");
                var missing = funcNames.Where(name => symbols.LookupFunction(name) == null).ToList();
                if (missing.Count > 0)
                    throw new Exception($"Function(s) {string.Join(", ", missing)} do not resolve by name:\n{source}");
                Interlocked.Increment(ref resolved);
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        _output.WriteLine($"Function resolution: {resolved} admitted programs, every function resolved");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    [Fact]
    public void FunctionCall_TypeChecksCorrectly()
    {
        var sink = new RejectionSink("FunctionSemantics.FunctionCall_TypeChecksCorrectly");

        SemanticFilter.WellTypedProgram(
                Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                    GenFunctions.ModuleWithFunctions(TypeEnv.Default, type, fuel: 2)), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// <c>TypeEnv.WithFunctions</c> names <c>add</c>/<c>greet</c>/<c>is_positive</c>; every sample
    /// must define what it calls. Before #2288 the generator called them without defining them
    /// (SPY0200 on 134/200 raw samples — generator defect G2), and only the no-ICE lane below read it.
    /// </summary>
    [Fact]
    public void ModuleWithEnvFunctions_AnalyzesClean()
    {
        var sink = new RejectionSink("FunctionSemantics.ModuleWithEnvFunctions_AnalyzesClean");

        SemanticFilter.WellTypedProgram(
                Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                    GenFunctions.ModuleWithFunctions(TypeEnv.WithFunctions, type, fuel: 2)), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    [Fact]
    public void FunctionReturnType_MatchesAnnotation()
    {
        var sink = new RejectionSink("FunctionSemantics.FunctionReturnType_MatchesAnnotation");
        int matched = 0;

        SemanticFilter.WellTypedProgram(GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2), sink)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var result = new Sharpy.Compiler.Compiler().Analyze(source, "func_test.spy");
                var symbols = result.SymbolTable
                    ?? throw new Exception($"An admitted program has no symbol table:\n{source}");

                var funcDefs = module.Body
                    .OfType<Sharpy.Compiler.Parser.Ast.FunctionDef>()
                    .Where(f => f.Name != "main" && f.ReturnType != null)
                    .ToList();
                if (funcDefs.Count == 0)
                    throw new Exception($"ModuleWithFunctions drew no annotated function:\n{source}");
                var unmatched = funcDefs.Where(fd => symbols.LookupFunction(fd.Name)?.ReturnType == null)
                    .Select(fd => fd.Name)
                    .ToList();
                if (unmatched.Count > 0)
                    throw new Exception($"Function(s) {string.Join(", ", unmatched)} have no resolved return type:\n{source}");
                Interlocked.Increment(ref matched);
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        _output.WriteLine($"Return type matching: {matched} admitted programs, every return type resolved");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    [Fact]
    public void FunctionOverload_DispatchesCorrectly()
    {
        var sink = new RejectionSink("FunctionSemantics.FunctionOverload_DispatchesCorrectly");

        var source = Gen.Const("class Processor:\n    def handle(self, x: int) -> str:\n        return \"int\"\n\n    def handle(self, x: str) -> str:\n        return \"str\"\n\n    def handle(self, x: int, y: int) -> str:\n        return \"two\"\n\ndef main():\n    p = Processor()\n    a = p.handle(42)\n    b = p.handle(\"hi\")\n    c = p.handle(1, 2)\n    print(a)\n    print(b)\n    print(c)\n");

        SemanticFilter.WellTypedSource(source, sink).Sample(_ => { }, iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    [Fact]
    public void ModuleWithFunctions_NeverThrowsInternalError()
    {
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenFunctions.ModuleWithFunctions(TypeEnv.WithFunctions, type, fuel: 2))
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    compiler.Analyze(source, "func_test.spy");
                }
                catch (Sharpy.Compiler.Diagnostics.InternalCompilerErrorException ex)
                {
                    throw new Exception(
                        $"InternalCompilerErrorException on function program:\n{source}\n{ex.Message}");
                }
                catch
                {
                    // Other exceptions are acceptable
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }
}

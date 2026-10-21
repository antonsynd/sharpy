using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class ImportResolutionPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ImportResolutionPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Every raw <c>GenImports.ImportingModule</c> pair compiles clean as one project (or is a pinned known
    /// red). Before #2288 the lane passed with half its pairs refused.
    /// </summary>
    [Fact]
    public void ImportedFunction_IsCallable()
    {
        var sink = new RejectionSink("ImportResolution.ImportedFunction_IsCallable");

        SemanticFilter.CompilableProject(GenImports.ImportingModule(TypeEnv.Default, fuel: 1),
                pair => new[] { ("main.spy", pair.MainSource), ("lib.spy", pair.LibSource) }, sink)
            .Sample(_ => { }, iter: 20);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// Every raw <c>GenImports.MultiFileProgram</c> pair compiles clean as one project. Before #2288 the
    /// lane passed with half its pairs refused.
    /// </summary>
    [Fact]
    public void MultiFileProgram_CompilesClean()
    {
        var sink = new RejectionSink("ImportResolution.MultiFileProgram_CompilesClean");

        SemanticFilter.CompilableProject(GenImports.MultiFileProgram(TypeEnv.Default, fuel: 1),
                pair => new[] { ("main.spy", pair.Main), ("lib.spy", pair.Lib) }, sink)
            .Sample(_ => { }, iter: 20);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// <c>from lib import Widget</c> of a one-field class, for each field type: every pair compiles clean.
    /// Before #2288 the lane passed with half its pairs refused.
    /// </summary>
    [Fact]
    public void ImportedClass_IsInstantiable()
    {
        var sink = new RejectionSink("ImportResolution.ImportedClass_IsInstantiable");

        SemanticFilter.CompilableProject(Gen.OneOfConst("int", "str", "bool").Select(fieldType =>
                {
                    var lib = $"class Widget:\n    value: {fieldType}\n\n    def __init__(self, value: {fieldType}):\n        self.value = value\n";
                    var main = $"from lib import Widget\n\ndef main():\n    w = Widget({GenImports_DefaultLiteral(fieldType)})\n    print(w.value)\n";
                    return (Main: main, Lib: lib);
                }),
                pair => new[] { ("main.spy", pair.Main), ("lib.spy", pair.Lib) }, sink)
            .Sample(_ => { }, iter: 20);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// <c>from lib1 import f</c> beside <c>from lib2 import f</c> draws exactly SPY0204 on every
    /// sample, judged through the multi-file front-end of the refusal seam (#2288). Before #2288 the
    /// lane counted any failed compile or any <c>SPY0</c>-prefixed code, and passed at half the samples.
    /// </summary>
    [Fact]
    public void NameCollision_IsDetected()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenImports.NameCollisionPair(TypeEnv.Default, fuel: 1),
            quad => ErrorInjector.JudgeProject(
                new[] { ("main.spy", quad.Main), ("lib1.spy", quad.Lib1), ("lib2.spy", quad.Lib2) },
                "SPY0204", CompilerDiagnosticSeverity.Error, null, "name collision between two imports"),
            iter: 20);
        _output.WriteLine($"Name collision: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0204");

        Assert.True(verdicts.Tested > 0, "No name collision programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} name collision programs misjudged; the first:\n{verdicts.First}");
    }

    /// <summary>
    /// Two modules importing each other draw exactly SPY0302 on every sample (#2288). Before #2288
    /// any failed compile counted.
    /// </summary>
    [Fact]
    public void CircularImport_IsRejected()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenImports.CircularImportPair(TypeEnv.Default, fuel: 1),
            pair => ErrorInjector.JudgeProject(
                new[]
                {
                    ("file_a.spy", pair.FileA),
                    ("file_b.spy", pair.FileB),
                    ("main.spy", "from file_a import helper_a\n\ndef main():\n    print(helper_a())\n")
                },
                "SPY0302", CompilerDiagnosticSeverity.Error, null, "circular import"),
            iter: 20);
        _output.WriteLine($"Circular import: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0302");

        Assert.True(verdicts.Tested > 0, "No circular import programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} circular import programs misjudged; the first:\n{verdicts.First}");
    }

    /// <summary>
    /// An import <c>main</c> never uses draws exactly the SPY0452 warning and no error on every sample
    /// — the WARNING arm of the seam (#2288). Before #2288 the lane counted samples that drew the
    /// warning at all, beside anything else, and passed at half of them.
    /// </summary>
    [Fact]
    public void UnusedImport_ProducesWarning()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenImports.UnusedImportProgram(TypeEnv.Default, fuel: 1),
            pair => ErrorInjector.JudgeProject(
                new[] { ("main.spy", pair.Main), ("lib.spy", pair.Lib) },
                "SPY0452", CompilerDiagnosticSeverity.Warning, null, "unused import"),
            iter: 20);
        _output.WriteLine($"Unused import: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew warning SPY0452");

        Assert.True(verdicts.Tested > 0, "No unused import programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} unused import programs misjudged; the first:\n{verdicts.First}");
    }

    private static string GenImports_DefaultLiteral(string type) => type switch
    {
        "int" => "42",
        "str" => "\"hello\"",
        "bool" => "True",
        _ => "0"
    };
}

using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class ClassInheritancePropertyTests
{
    private readonly ITestOutputHelper _output;

    public ClassInheritancePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClassHierarchy_CompilesClean()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.WithInheritance, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ClassHierarchy_CompilesClean");
    }

    [Fact]
    public void DerivedClass_InheritsBaseMembers()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.WithInheritance, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "DerivedClass_InheritsBaseMembers");
    }

    [Fact]
    public void MethodOverride_PreservesSignature()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "MethodOverride_PreservesSignature");
    }

    [Fact]
    public void Constructor_InitializesFieldTypes()
    {
        var samples = PropertyCorpus.CompileAll(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "Constructor_InitializesFieldTypes");
    }

    [Fact]
    public void AbstractMethod_MustBeOverridden()
    {
        // Judged like every injection (#2288): exactly the injection's code, or — while the refusal
        // is missing — its KnownRed pin. This was `rejected > tested / 2` over any failure or throw,
        // and every sample was rejected for SPY0493 on the BASE class, never for the missing member.
        int tested = 0;
        var failures = new List<string>();

        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2)
            .Sample(module =>
            {
                var injected = ErrorInjector.InjectMissingAbstractImplementation(module);
                if (injected == null)
                    return;

                Interlocked.Increment(ref tested);
                if (ErrorInjector.Judge(injected, "missing abstract implementation") is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Abstract method enforcement: {tested - failures.Count}/{tested} judged as expected");
        Assert.True(tested > 0, "No missing-abstract-implementation injections were applicable");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} missing-abstract-implementation injections misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    /// <summary>
    /// Every raw <c>ModuleWithClasses(TypeEnv.Default)</c> sample analyses clean (the sink); every
    /// admitted one that calls <c>super().__init__</c> carries its semantic info; and the corpus calls
    /// <c>super()</c> at least once (the applicability floor — a corpus without one proves nothing).
    /// Before #2288 one resolved <c>super()</c> anywhere passed, beside any number of refusals.
    /// </summary>
    [Fact]
    public void SuperCall_ResolvesCorrectly()
    {
        var sink = new RejectionSink("ClassInheritance.SuperCall_ResolvesCorrectly");
        int superCallsTested = 0;

        SemanticFilter.WellTypedProgram(GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2), sink)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                if (!source.Contains("super().__init__"))
                    return;

                Interlocked.Increment(ref superCallsTested);
                var result = new Sharpy.Compiler.Compiler().Analyze(source, "class_test.spy");
                if (result.SemanticInfo == null)
                    throw new Exception($"An admitted super() program has no semantic info:\n{source}");
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        _output.WriteLine($"Super call resolution: {superCallsTested} admitted programs call super().__init__");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Assert.True(superCallsTested >= 1, "No sampled class program calls super().__init__ — the lane is vacuous");
    }

    /// <summary>Every RAW class program analyses without a throw or an internal compiler error
    /// (<see cref="ErrorInjector.JudgeNoInternalError"/>); a refusal is allowed — the sample is
    /// unfiltered. Before #2288 the lane swallowed every exception but one <c>Analyze</c> never throws.</summary>
    [Fact]
    public void ModuleWithClasses_NeverThrowsInternalError()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2),
            source => ErrorInjector.JudgeNoInternalError(source, "class program"), iter: 50);
        _output.WriteLine($"Class programs: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed without an internal error");
        Assert.True(verdicts.Tested > 0, "No class programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} class programs crashed the compiler; the first:\n{verdicts.First}");
    }
}

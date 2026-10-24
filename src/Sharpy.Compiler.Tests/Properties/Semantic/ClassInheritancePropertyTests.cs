using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
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
    /// admitted one that calls <c>super().__init__</c> is judged clean by
    /// <see cref="ErrorInjector.JudgeClean"/> AND each such call resolves, in <c>SemanticInfo</c>, to the
    /// BASE class's <c>__init__</c>: the callee's recorded type
    /// (<see cref="Sharpy.Compiler.Semantic.SemanticInfo.GetExpressionType"/> of the
    /// <c>super().__init__</c> member access) carries exactly the parameter types of the symbol
    /// <see cref="Sharpy.Compiler.Semantic.SemanticInfo.GetFunctionDeclarationSymbol"/> records for the
    /// base's <c>__init__</c> (<c>self</c> dropped), and a recorded call target, if any, is that very
    /// symbol. A sample whose derived <c>__init__</c> has the same parameter types cannot tell base from
    /// derived, so the lane also requires a DISCRIMINATING sample — one where the derived signature
    /// differs — beside the applicability floor (a corpus without <c>super()</c> proves nothing). Before
    /// #2288 one resolved <c>super()</c> anywhere passed, beside any number of refusals; before #2324 an
    /// admitted sample only had to carry SOME semantic info.
    /// </summary>
    [Fact]
    public void SuperCall_ResolvesCorrectly()
    {
        var sink = new RejectionSink("ClassInheritance.SuperCall_ResolvesCorrectly");
        int superCallsTested = 0;
        int discriminatingSuperCallsTested = 0;
        var misresolved = new List<string>();

        SemanticFilter.WellTypedProgram(GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2), sink)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                if (!source.Contains("super().__init__"))
                    return;

                Interlocked.Increment(ref superCallsTested);
                if ((ErrorInjector.JudgeClean(source, "super() program")
                        ?? SuperInitResolutionFailure(source, ref discriminatingSuperCallsTested)) is { } failure)
                {
                    lock (misresolved)
                        misresolved.Add(failure);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        _output.WriteLine($"Super call resolution: {superCallsTested - misresolved.Count}/{superCallsTested} admitted programs resolve super().__init__ to the base __init__ ({discriminatingSuperCallsTested} with a derived signature that differs)");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Assert.True(superCallsTested > 0, "No sampled class program calls super().__init__ — the lane is vacuous");
        Assert.True(misresolved.Count == 0,
            $"{misresolved.Count}/{superCallsTested} super().__init__ programs misresolved; the first:\n{misresolved.FirstOrDefault()}");
        Assert.True(discriminatingSuperCallsTested > 0,
            $"No super().__init__ sample had a derived __init__ signature that differs from the base's ({superCallsTested} tested) — base and derived resolution are indistinguishable");
    }

    /// <summary>Null when every <c>super().__init__(…)</c> call in <paramref name="source"/> resolves to
    /// the <c>__init__</c> of the enclosing class's base (see <see cref="SuperCall_ResolvesCorrectly"/>);
    /// otherwise why not. Counts into <paramref name="discriminating"/> each call whose derived
    /// <c>__init__</c> signature differs from the base's.</summary>
    private static string? SuperInitResolutionFailure(string source, ref int discriminating)
    {
        var result = new Sharpy.Compiler.Compiler().Analyze(source, "class_test.spy");
        if (result.SemanticInfo is not { } info || result.Module is not { } parsed)
            return $"An admitted super() program has no semantic info or AST:\n{source}";

        var classes = parsed.Body.OfType<ClassDef>().ToDictionary(c => c.Name);
        int calls = 0;
        foreach (var derived in classes.Values)
        {
            var superInits = AstDescendants.SelfAndDescendants(derived).OfType<FunctionCall>()
                .Where(c => c.Function is MemberAccess { Member: "__init__", Object: SuperExpression })
                .ToList();
            if (superInits.Count == 0)
                continue;

            if (derived.BaseClasses.Length == 0 || !classes.TryGetValue(derived.BaseClasses[0].Name, out var baseClass))
                return $"super().__init__ in '{derived.Name}', whose base is not a class of the module:\n{source}";
            var baseInit = InitSymbol(info, baseClass);
            var derivedInit = InitSymbol(info, derived);
            if (baseInit is null || derivedInit is null)
                return $"'{baseClass.Name}' or '{derived.Name}' has no recorded __init__ symbol:\n{source}";
            var expected = ParameterKeys(baseInit);

            foreach (var call in superInits)
            {
                calls++;
                if (info.GetCallTarget(call) is { } target && !ReferenceEquals(target, baseInit))
                    return $"super().__init__ in '{derived.Name}' records call target '{target.Name}' ({string.Join(", ", ParameterKeys(target))}), not the __init__ of base '{baseClass.Name}':\n{source}";
                if (info.GetExpressionType(call.Function) is not Sharpy.Compiler.Semantic.FunctionType callee)
                    return $"super().__init__ in '{derived.Name}' has no recorded function type:\n{source}";
                var actual = callee.ParameterTypes.Select(t => t.CanonicalKey).ToList();
                if (!actual.SequenceEqual(expected))
                    return $"super().__init__ in '{derived.Name}' resolved to ({string.Join(", ", actual)}), expected the __init__ of base '{baseClass.Name}' ({string.Join(", ", expected)}):\n{source}";
                if (!ParameterKeys(derivedInit).SequenceEqual(expected))
                    Interlocked.Increment(ref discriminating);
            }
        }
        return calls > 0 ? null : $"The source spells super().__init__ but the parsed AST holds no such call:\n{source}";
    }

    private static Sharpy.Compiler.Semantic.FunctionSymbol? InitSymbol(Sharpy.Compiler.Semantic.SemanticInfo info, ClassDef cls) =>
        cls.Body.OfType<FunctionDef>().SingleOrDefault(f => f.Name == "__init__") is { } def
            ? info.GetFunctionDeclarationSymbol(def)
            : null;

    /// <summary>The canonical keys of <paramref name="init"/>'s parameter types, <c>self</c> dropped.</summary>
    private static List<string> ParameterKeys(Sharpy.Compiler.Semantic.FunctionSymbol init) =>
        init.Parameters.Skip(1).Select(p => p.Type.CanonicalKey).ToList();

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

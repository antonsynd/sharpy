using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Integration;

public class SourceGeneratorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public SourceGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ProjectCompilationHelper CreateHelper()
    {
        _helper = new ProjectCompilationHelper(_output);
        return _helper;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    [Fact]
    public void Compilation_WithNoGenerators_SucceedsNormally()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("main.spy", @"
def main():
    x: int = 42
    print(x)
");

        helper.WithRootNamespace("NoGenTest").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void GeneratorClass_Compiles_WithoutErrors()
    {
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class MyGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('')
");

        helper.AddSourceFile("main.spy", @"
def main():
    print('hello')
");

        helper.WithRootNamespace("GenClassTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void GeneratorClass_ReadingReflectionTypes_Compiles()
    {
        // Exercises the Sharpy.Generators reflection types (ClassInfo/MethodInfo/ParameterInfo)
        // through codegen — the Mode-A types that #1090 fixed — from a .spy-authored generator.
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput, ClassInfo, MethodInfo

class DescribeGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        target: ClassInfo = context.target_class
        first: MethodInfo = target.methods[0]
        return GeneratorOutput('// ' + first.name)
");

        helper.AddSourceFile("main.spy", @"
def main():
    print('hello')
");

        helper.WithRootNamespace("GenReflectionTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void GeneratorClass_EmitsOverrideModifier_OnClrAbstractMethod()
    {
        // #1122: a .spy method overriding the CLR-abstract SourceGenerator.Generate must be
        // emitted with the `override` modifier (auto-detected — no @override decorator). The
        // CLR-virtual (non-abstract) override case is covered by ClrBaseOverrideDetectorTests,
        // since no abstract-free bridged base is available in the runtime-reference harness.
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class MyGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('')
");

        helper.AddSourceFile("main.spy", @"
def main():
    print('hello')
");

        helper.WithRootNamespace("GenOverrideTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        var generatorCSharp = result.GeneratedCSharpFiles
            .First(kvp => kvp.Key.Contains("gen", StringComparison.Ordinal)).Value;
        Assert.Contains("override", generatorCSharp);
        Assert.Contains("GeneratorOutput Generate(", generatorCSharp);
    }

    [Fact]
    public void GeneratorTrigger_IsConsumed_AndTheProjectCompiles()
    {
        // #1431: a generator trigger @[MyGen] applied end to end now COMPILES. Three module-scope /
        // emission bugs stacked behind the CS0616 the issue reported:
        //   1. PartitionGenerators looked up the ClassDef symbol at global scope (null) instead of the
        //      file's module scope, so it never found the generator (the log ran "Phase 4d" but never
        //      "Found N source generator(s)").
        //   2. CompileGeneratorAssembly emitted the generator file WITHOUT entering its module scope,
        //      so the base type and constructor came out unqualified (bare `SourceGenerator` /
        //      `GeneratorOutput`) — a generator assembly no reference could satisfy, SPY0550.
        //   3. The emitter wrote the trigger out as an ordinary bracket attribute, and MyGen is not a
        //      C# attribute class: CS0616 behind SPY0908. The trigger is a generator directive,
        //      consumed by the pipeline — the emitter now skips it (mirrors the validator's own
        //      IsSourceGeneratorBracketAttribute exemption).
        // Asserted end to end (not just at the validator) because all three live upstream of the
        // compile. This uses an EMPTY-output generator — the shape #1431 measured. A generator that
        // emits a NEW top-level declaration is covered by GeneratorEmittingNewDeclaration_CompilesAndRuns
        // below (#1535 — fixed by the materialize/freeze split).
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class MyGen(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('')
");

        helper.AddSourceFile("main.spy", @"
from gen import MyGen

@[MyGen]
class Point:
    x: int

def main():
    print('hello')
");

        helper.WithRootNamespace("GenTriggerTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.True(result.Success,
            "the generator trigger must compile end to end (#1431): "
            + string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // The generator ACTUALLY RAN: SPY0554 (empty output) is only emitted after the generator
        // assembly compiled and executed. This makes the two module-scope fixes (partition +
        // generator-assembly emission) load-bearing for this test — without them the generator is
        // never found or never compiles, and this diagnostic never appears (the assertion below would
        // also then pass vacuously on the trigger-skip alone).
        Assert.Contains(result.Diagnostics.GetWarnings(),
            d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorEmptyOutput);

        // The trigger must NOT reach C# as an attribute — that is exactly the CS0616 this closes.
        var mainCSharp = result.GeneratedCSharpFiles
            .First(kvp => kvp.Key.Contains("main", StringComparison.Ordinal)).Value;
        Assert.DoesNotContain("MyGen]", mainCSharp);

        // A control with an UNKNOWN trigger name is still refused (SPY0495) — the consumption above is
        // scoped to names that resolve to a source generator, and would pass vacuously if the check
        // never ran in this harness.
        using var control = new ProjectCompilationHelper(_output);
        control.AddSourceFile("main.spy", @"
@[no_such_generator]
class Point:
    x: int

def main():
    print('hello')
");
        control.WithRootNamespace("GenTriggerControl").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var controlResult = control.Compile();

        Assert.Contains(controlResult.Diagnostics.GetErrors(),
            e => e.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Validation.UnknownBracketAttribute);
    }

    [Fact]
    public void GeneratorEmittingNewDeclaration_CompilesAndRuns()
    {
        // #1535's own repro: a generator returning a NEW top-level declaration. Before the
        // materialize/freeze split (cc15802a4) this ICE'd — Phase 5b ran after the CodeGenInfo
        // freeze, so integrating `greeting` threw PhaseViolationException behind SPY0909.
        // MUTATION-VERIFIED (2026-08-20, plan-930411 Task 3.2): with FreezeTypeInfo() locally moved
        // back before ExecuteGeneratorPipeline in ProjectCompiler.cs, this test fails with the
        // SPY0909 phase violation the issue reported; with the split restored it passes. That is
        // the red-run evidence the plan asked for, taken against the mutated (pre-fix-shaped)
        // pipeline rather than a historical checkout.
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class AddGreeting(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('def greeting() -> str:\n    return ""generated!""\n')
");

        helper.AddSourceFile("main.spy", @"
from gen import AddGreeting

@[AddGreeting]
class Point:
    x: int

def main():
    print('hello')
");

        helper.WithRootNamespace("GenNewDeclTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.CompileAndExecute();

        Assert.True(result.Success,
            "a generator emitting a new top-level declaration must compile and run (#1535): "
            + string.Join("; ", result.CompilationErrors));
        Assert.Contains("hello", result.StandardOutput);

        // The generated declaration actually reached codegen: the integrated C# carries Greeting.
        var compilation = helper.LastCompilationResult!;
        Assert.Contains(compilation.GeneratedCSharpFiles,
            kvp => kvp.Value.Contains("Greeting", StringComparison.Ordinal));

        // SPY0909 unreachable via generators (the #1146 contract): no internal-compiler-error
        // diagnostic may appear on this path.
        Assert.DoesNotContain(compilation.Diagnostics.GetErrors(), e => e.Code == "SPY0909");
    }

    [Fact]
    public void GeneratorEmittingPlainClass_WithoutBases_CompilesAndRuns()
    {
        // Task 3.2(b) of plan-930411 said decide this shape by measurement: a generated CLASS with
        // no base clause works end-to-end (measured 2026-08-20), so it is pinned as supported —
        // only base clauses are refused (SPY0555, the shape inheritance-freeze cannot take).
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class AddClass(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('class Generated:\n    n: int = 5\n')
");

        helper.AddSourceFile("main.spy", @"
from gen import AddClass

@[AddClass]
class Point:
    x: int

def main():
    print('hello')
");

        helper.WithRootNamespace("GenPlainClassTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.CompileAndExecute();

        Assert.True(result.Success,
            "a generated base-less class must compile and run: "
            + string.Join("; ", result.CompilationErrors));
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public void GeneratorEmittingClassWithBaseClause_IsRefusedWithSPY0555_NotSPY0909()
    {
        // IntegrateGeneratedSource never runs ResolveInheritance and inheritance froze at Phase 4c:
        // a generated class with a base clause is structurally unsupported this round. The contract
        // (#1146): refused loudly with the named diagnostic, never the SPY0909 net. Full support is
        // #1592.
        var helper = CreateHelper();

        helper.AddSourceFile("gen.spy", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class AddDerived(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput('class GenChild(Point):\n    pass\n')
");

        helper.AddSourceFile("main.spy", @"
from gen import AddDerived

@[AddDerived]
class Point:
    x: int

def main():
    print('hello')
");

        helper.WithRootNamespace("GenBaseClauseTest").WithEntryPoint("main.spy")
            .WithRuntimeReferences().CreateProjectFile();
        var result = helper.Compile();

        Assert.False(result.Success, "a generated class with a base clause must be refused");
        Assert.Contains(result.Diagnostics.GetErrors(),
            e => e.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorUnsupportedShape);
        Assert.DoesNotContain(result.Diagnostics.GetErrors(), e => e.Code == "SPY0909");
    }

    [Fact]
    public void DiagnosticCodes_GeneratorCodesExist()
    {
        Assert.Equal("SPY0550", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorExecutionError);
        Assert.Equal("SPY0551", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorTimeout);
        Assert.Equal("SPY0552", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorInvalidSource);
        Assert.Equal("SPY0553", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorCycleDetected);
        Assert.Equal("SPY0554", Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.GeneratorEmptyOutput);
    }
}

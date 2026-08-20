using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Covers the <c>isinstance</c> TYPE-OPERAND CLASSIFIER: the single semantic-time authority on what a
/// type test's second argument denotes (#1207, #1213).
///
/// <para>
/// Before it existed, nothing classified that operand, so any shape the checker tolerated reached the
/// emitter — which re-derived a C# type from the operand's syntax and produced raw Roslyn errors
/// (SPY0908) for the two it could not spell: a bare generic name became <c>Box&lt;T&gt;</c> (CS0305)
/// and a tuple of type names became a tuple of method groups (CS1503). These tests pin the whole
/// outcome table, so a new operand shape cannot quietly rejoin the un-lowerable set.
/// </para>
/// </summary>
public class TypeTestLoweringTests
{
    // --- Accepted shapes -----------------------------------------------------------------------

    [Fact]
    public void BuiltinPrimitiveName_ClassifiesAsClosedType()
    {
        var lowering = SingleTypeTest(@"
def f(x: object) -> bool:
    return isinstance(x, int)
");

        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        lowering.TestType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void NonGenericUserClass_ClassifiesAsClosedType()
    {
        var lowering = SingleTypeTest(@"
class Animal:
    pass

def f(x: object) -> bool:
    return isinstance(x, Animal)
");

        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        lowering.TestType.Should().BeOfType<UserDefinedType>()
            .Which.Name.Should().Be("Animal");
    }

    [Fact]
    public void UnparameterizedBuiltinCollection_ClassifiesAsErased()
    {
        var lowering = SingleTypeTest(@"
def f(x: object) -> bool:
    return isinstance(x, list)
");

        // The bare name cannot know the element type, so the test erases to the non-generic protocol
        // interface (#912). The carried type still has default `object` arguments, because that is
        // what narrowing needs for member access on the narrowed value to resolve.
        lowering.Kind.Should().Be(TypeTestLoweringKind.ErasedBuiltinCollection);
        var generic = lowering.TestType.Should().BeOfType<GenericType>().Which;
        generic.Name.Should().Be("list");
        generic.TypeArguments.Should().ContainSingle();
    }

    [Fact]
    public void ParameterizedBuiltinCollection_ClassifiesAsClosedType()
    {
        var lowering = SingleTypeTest(@"
def f(x: object) -> bool:
    return isinstance(x, list[int])
");

        // The written spelling names the instantiation, and .NET reifies it, so the test can be exact
        // rather than erased. CPython rejects this form outright (parameterized generics are not
        // usable in isinstance there); Sharpy accepts it because its generics are real runtime types.
        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        var generic = lowering.TestType.Should().BeOfType<GenericType>().Which;
        generic.Name.Should().Be("list");
        generic.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void BareGenericName_FillsTypeArgumentsFromTheSubject()
    {
        var lowering = SingleTypeTest(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(b: Box[int]) -> bool:
    return isinstance(b, Box)
");

        // #1207's core case: `Box` alone names no runtime type, but the subject's own static type
        // determines the vector, so the test is against Box[int].
        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        var generic = lowering.TestType.Should().BeOfType<GenericType>().Which;
        generic.Name.Should().Be("Box");
        generic.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ClosedGenericSpelling_ClassifiesAsClosedType()
    {
        var lowering = SingleTypeTest(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(x: object) -> bool:
    return isinstance(x, Box[str])
");

        // The spelling the open-generic rejection recommends. It has to work, or that diagnostic
        // names a remedy that does not (#1207).
        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        var generic = lowering.TestType.Should().BeOfType<GenericType>().Which;
        generic.Name.Should().Be("Box");
        generic.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void ParenthesizedCallee_ClassifiesIdentically()
    {
        // `(isinstance)(x, T)` denotes the same call as `isinstance(x, T)` (#1170); a classifier that
        // dispatched on surface syntax would miss it and let the operand through unclassified.
        var lowering = SingleTypeTest(@"
def f(x: object) -> bool:
    return (isinstance)(x, str)
");

        lowering.Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        lowering.TestType.Should().Be(SemanticType.Str);
    }

    // --- Rejected shapes -----------------------------------------------------------------------

    [Fact]
    public void TupleOperand_IsClassifiedAsTupleType()
    {
        // (int, str) denotes tuple[int, str] in a type position (#1532).
        var (module, info, _) = Check(@"
def main() -> None:
    x: object = 5
    if isinstance(x, (int, str)):
        print(""tuple ok"")
");

        var lowerings = TypeTestLowerings(module, info);
        lowerings.Should().HaveCount(1);
        lowerings[0].Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        lowerings[0].TestType.Should().BeOfType<Sharpy.Compiler.Semantic.TupleType>();
        var tupleType = (Sharpy.Compiler.Semantic.TupleType)lowerings[0].TestType;
        tupleType.ElementTypes.Should().HaveCount(2);
        tupleType.ElementTypes[0].Should().Be(SemanticType.Int);
        tupleType.ElementTypes[1].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void UndeterminedGenericOperand_IsRejectedWithExactlyOneDiagnostic()
    {
        var diagnostics = Diagnose(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def main() -> None:
    x: object = Box[int](5)
    print(isinstance(x, Box))
");

        diagnostics.Select(d => d.Code).Should().Equal(DiagnosticCodes.Semantic.OpenGenericTypeTest);
        diagnostics[0].Message.Should().Contain("Box[...]",
            "the rejection must name the closed spelling, which is the remedy");
    }

    [Fact]
    public void TupleOperand_RecordsLowering()
    {
        // The tuple form now classifies as a tuple type test (#1532) and records a lowering.
        var (module, info, _) = Check(@"
def main() -> None:
    x: object = 5
    if isinstance(x, (int, str)):
        print(""tuple ok"")
");

        TypeTestLowerings(module, info).Should().HaveCount(1);
    }

    // --- Shapes the classifier deliberately leaves alone ---------------------------------------

    [Fact]
    public void ShadowedIsinstance_IsNotClassified()
    {
        // A user-defined `isinstance` is an ordinary call whose second argument is an ordinary value.
        // Classifying it would reject legal programs; this is the guard the retired SPY0475 hint had.
        var (module, info) = Analyze(@"
def isinstance(obj: object, type_name: str) -> bool:
    return type_name == ""int""

def main() -> None:
    print(isinstance(42, ""int""))
");

        TypeTestLowerings(module, info).Should().BeEmpty();
    }

    [Fact]
    public void TestAssertTupleForm_IsClassifiedAsTupleType()
    {
        // Classification is uniform in every position (#1532) — the @test exemption is deleted.
        var (module, info) = Analyze(@"
@test
def test_tuple():
    x: object = 42
    assert isinstance(x, (int, str))

@test
def test_negated_tuple():
    x: object = 3.14
    assert not isinstance(x, (int, str))

def main():
    print(""ok"")
");

        TypeTestLowerings(module, info).Should().HaveCount(2);
    }

    [Fact]
    public void TestAssertTupleForm_ProducesNoDiagnostic()
    {
        Diagnose(@"
@test
def test_tuple():
    x: object = 42
    assert isinstance(x, (int, str))

def main():
    print(""ok"")
").Should().BeEmpty();
    }

    [Fact]
    public void TupleInsideTestFunction_IsClassifiedUniformly()
    {
        // Classification is uniform — tuple form is a tuple type test everywhere (#1532).
        var (module, info) = Analyze(@"
@test
def test_tuple():
    x: object = 42
    ok: bool = isinstance(x, (int, str))
    assert ok

def main():
    print(""ok"")
");

        TypeTestLowerings(module, info).Should().HaveCount(1);
    }

    // --- Materialization -----------------------------------------------------------------------

    [Fact]
    public void TypeTestLowering_SurvivesTheProjectMerge()
    {
        // A node-keyed SemanticInfo dictionary that is missing from MergeFrom loses every entry in the
        // per-file -> project merge, silently — no error, no warning, just an emitter that sees
        // nothing and falls back. All compiles go through that merge, so this is the tripwire.
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("TypeTestMergeTest")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");

        helper.AddSourceFile("lib.spy", @"
def describe(x: object) -> str:
    if isinstance(x, int):
        return ""int""
    return ""other""
");
        helper.AddSourceFile("main.spy", @"
from lib import describe

def main() -> None:
    print(describe(5))
");
        helper.CreateProjectFile();

        var projectPath = Path.Combine(helper.ProjectDirectory, $"{helper.Options.RootNamespace}.spyproj");
        var config = ProjectFileParser.Load(projectPath);
        var result = new CompilerApi().AnalyzeProject(config);

        result.Success.Should().BeTrue("the two-file project must analyze cleanly");

        var merged = result.ProjectModel.SemanticInfo;
        merged.Should().NotBeNull("project-level SemanticInfo is populated by MergeFrom");

        var libUnit = result.ProjectModel.Units.Values
            .First(u => Path.GetFileName(u.FilePath) == "lib.spy");

        var lowerings = TypeTestLowerings(libUnit.Ast!, merged!);
        lowerings.Should().ContainSingle("the type test in lib.spy must survive the per-file -> project merge");
        lowerings[0].Kind.Should().Be(TypeTestLoweringKind.ClosedType);
        lowerings[0].TestType.Should().Be(SemanticType.Int);
    }

    // --- Harness -------------------------------------------------------------------------------

    private static TypeTestLowering SingleTypeTest(string source)
    {
        var (module, info) = Analyze(source);
        var lowerings = TypeTestLowerings(module, info);
        lowerings.Should().ContainSingle("the probe program contains exactly one type test");
        return lowerings[0];
    }

    private static List<TypeTestLowering> TypeTestLowerings(Module module, SemanticInfo info)
    {
        return Descendants(module)
            .OfType<FunctionCall>()
            .Where(c => c.Arguments.Length == 2)
            .Select(c => info.GetTypeTestLowering(c.Arguments[1]))
            .Where(l => l != null)
            .Select(l => l!)
            .ToList();
    }

    private static (Module Module, SemanticInfo Info) Analyze(string source)
    {
        var (module, info, diagnostics) = Check(source);
        diagnostics.Should().BeEmpty(
            "classification probe programs must analyze cleanly (a diagnostic would mask the outcome)");
        return (module, info);
    }

    private static List<CompilerDiagnostic> Diagnose(string source) => Check(source).Diagnostics;

    private static (Module Module, SemanticInfo Info, List<CompilerDiagnostic> Diagnostics) Check(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var pipeline = ValidationPipelineFactory.CreateDefault(NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance, pipeline)
        {
            SemanticBinding = semanticBinding
        };

        typeChecker.CheckModule(module);

        return (module, semanticInfo, typeChecker.Diagnostics.GetErrors().ToList());
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}

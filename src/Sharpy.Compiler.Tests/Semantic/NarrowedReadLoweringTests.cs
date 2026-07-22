using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
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
/// Verifies that the TypeChecker materializes a <see cref="NarrowedReadLowering"/> on each narrowed
/// read node (Identifier / MemberAccess / IndexAccess) stating the accessor codegen must apply, and
/// that match-arm reads acquire none (they narrow via redefined symbols, not context/CFG facts) —
/// the additive dual-write half of the emitter narrowing co-migration (#1081).
/// </summary>
public class NarrowedReadLoweringTests
{
    [Fact]
    public void OptionalNarrowing_RecordsUnwrapOptional()
    {
        var (module, info) = Analyze(@"
def f(x: int?) -> int:
    if x is not None:
        return x + 1
    return 0
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle("only the narrowed read of x inside the guarded branch is lowered");
        reads[0].Node.Should().BeOfType<Identifier>();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.UnwrapOptional);
        reads[0].Lowering.CastTarget.Should().BeNull();
    }

    [Fact]
    public void ValueNullableNarrowing_RecordsNullableValue()
    {
        var (module, info) = Analyze(@"
def f(x: int | None) -> int:
    if x is not None:
        return x + 1
    return 0
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.NullableValue);
    }

    [Fact]
    public void ReferenceNullableNarrowing_RecordsNullForgiving()
    {
        var (module, info) = Analyze(@"
def f(x: str | None) -> str:
    if x is not None:
        return x
    return """"
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.NullForgiving);
    }

    [Fact]
    public void IsInstanceNarrowing_RecordsCastToTargetType()
    {
        var (module, info) = Analyze(@"
class Animal:
    pass

class Dog(Animal):
    def speak(self) -> str:
        return ""woof""

def f(a: Animal) -> str:
    if isinstance(a, Dog):
        return a.speak()
    return """"
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle("the receiver identifier a is the narrowed read");
        reads[0].Node.Should().BeOfType<Identifier>();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.Cast);
        reads[0].Lowering.CastTarget.Should().BeOfType<UserDefinedType>()
            .Which.Name.Should().Be("Dog");
    }

    [Fact]
    public void AndRhsNarrowing_RecordsLoweringOnRightOperandRead()
    {
        var (module, info) = Analyze(@"
def f(x: int?) -> bool:
    return x is not None and x > 0
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle("only the RHS read of x sees the left operand's narrowing");
        reads[0].Node.Should().BeOfType<Identifier>();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.UnwrapOptional);
    }

    [Fact]
    public void IndexAccessNarrowing_RecordsLoweringOnIndexNode()
    {
        var (module, info) = Analyze(@"
def f(xs: list[int | None]) -> int:
    if xs[0] is not None:
        return xs[0] + 1
    return 0
");

        var reads = ReadsWithLowering(module, info);
        reads.Should().ContainSingle("only the narrowed element read inside the branch is lowered");
        reads[0].Node.Should().BeOfType<IndexAccess>();
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.NullableValue);
    }

    [Fact]
    public void MatchArmRead_AcquiresNoLowering()
    {
        // Match patterns narrow by redefining the bound symbol (n : int), not through the narrowing
        // context or CFG facts, so a read of the binding must NOT record an accessor lowering. The
        // program still type-checks (n * 2 requires int), proving the narrowing itself works.
        var (module, info) = Analyze(@"
def describe(x: object) -> str:
    match x:
        case int() as n:
            return ""doubled: "" + str(n * 2)
        case _:
            return ""other""
");

        ReadsWithLowering(module, info).Should().BeEmpty(
            "match-arm reads narrow via redefined symbols and need no accessor");
    }

    [Fact]
    public void NarrowedReadLowering_SurvivesProjectMergeFromImportedModule()
    {
        // End-to-end guard for the #1038 merge gotcha: a narrowed read in an imported module must be
        // present on the merged, project-level SemanticInfo (populated by SemanticInfo.MergeFrom during
        // type checking), not just the per-file instance codegen never reads.
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("MergeTest")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");

        helper.AddSourceFile("lib.spy", @"
def bump(x: int?) -> int:
    if x is not None:
        return x + 1
    return 0
");
        helper.AddSourceFile("main.spy", @"
from lib import bump

def main() -> None:
    print(bump(5))
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

        var reads = ReadsWithLowering(libUnit.Ast!, merged!);
        reads.Should().ContainSingle("the narrowed read in lib.spy must survive the per-file -> project merge");
        reads[0].Lowering.Kind.Should().Be(NarrowedReadKind.UnwrapOptional);
    }

    // --- Harness -------------------------------------------------------------------------------

    private static (Module Module, SemanticInfo Info) Analyze(string source)
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

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty(
            "narrowing probe programs must type-check cleanly (a diagnostic would mask the narrowing)");

        return (module, semanticInfo);
    }

    private static List<(Expression Node, NarrowedReadLowering Lowering)> ReadsWithLowering(
        Module module, SemanticInfo info)
    {
        return Descendants(module)
            .OfType<Expression>()
            .Select(e => (Node: e, Lowering: info.GetNarrowedReadLowering(e)))
            .Where(x => x.Lowering != null)
            .Select(x => (x.Node, x.Lowering!))
            .ToList();
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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The agreement contract between an <c>isinstance</c> test and the narrowing it implies (#1207).
///
/// <para>
/// These used to be two independent derivations of the same operand expression, with different
/// rules, and they could disagree: for a bare generic <c>Box</c> the narrowing side produced a
/// <c>UserDefinedType</c> with no type arguments while the emitter spelled <c>Box&lt;T&gt;</c> —
/// same input, two answers, neither of them spellable. Both now read the single type the
/// type-operand classifier recorded, so for every accepted shape the type narrowed to IS the type
/// tested against.
/// </para>
/// </summary>
public class TypeTestNarrowingAgreementTests
{
    /// <summary>
    /// Each case is a program with exactly one type test and one narrowed read of its subject.
    /// The names describe the operand shape being covered.
    /// </summary>
    public static TheoryData<string, string> AcceptedOperandShapes() => new()
    {
        {
            "builtin primitive name",
            @"
def f(x: object) -> int:
    if isinstance(x, int):
        return x + 1
    return 0
"
        },
        {
            "non-generic user class",
            @"
class Animal:
    pass

class Dog(Animal):
    def speak(self) -> str:
        return ""woof""

def f(a: Animal) -> str:
    if isinstance(a, Dog):
        return a.speak()
    return """"
"
        },
        {
            "unparameterized builtin collection",
            @"
def f(x: object) -> int:
    if isinstance(x, list):
        return len(x)
    return 0
"
        },
        {
            "parameterized builtin collection",
            @"
def f(x: object) -> int:
    if isinstance(x, list[int]):
        return len(x)
    return 0
"
        },
        {
            "bare generic name filled from the subject",
            @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(b: Box[int]) -> int:
    if isinstance(b, Box):
        return b.value
    return 0
"
        },
        {
            "closed generic spelling",
            @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(x: object) -> int:
    if isinstance(x, Box[int]):
        return x.value
    return 0
"
        },
    };

    [Theory]
    [MemberData(nameof(AcceptedOperandShapes))]
    public void NarrowedTypeEqualsTheTypeTheTestIsEmittedAgainst(string shape, string source)
    {
        var (module, info) = Analyze(source);

        var typeTests = Descendants(module)
            .OfType<FunctionCall>()
            .Where(c => c.Arguments.Length == 2)
            .Select(c => info.GetTypeTestLowering(c.Arguments[1]))
            .Where(l => l != null)
            .Select(l => l!)
            .ToList();

        typeTests.Should().ContainSingle($"the {shape} probe contains exactly one classified type test");

        var casts = Descendants(module)
            .OfType<Expression>()
            .Select(info.GetNarrowedReadLowering)
            .Where(l => l is { Kind: NarrowedReadKind.Cast })
            .Select(l => l!)
            .ToList();

        casts.Should().NotBeEmpty($"the {shape} probe reads the narrowed subject inside the branch");
        casts.Should().OnlyContain(c => c.CastTarget!.Equals(typeTests[0].TestType),
            $"the {shape} narrowing and its emitted type test must come from one resolved type");
    }

    /// <summary>
    /// The agreement contract on the <c>is</c>-operator path (#1235): the operator's
    /// <see cref="TypeAnnotation"/> operand and the <c>isinstance</c> spelling of the SAME test on
    /// the SAME subject must classify to the same resolved type — one classifier, two spellings,
    /// one answer. Each probe contains both spellings so the assertion compares lowerings from one
    /// analysis of one program.
    /// <para>
    /// This deliberately does NOT assert a narrowed read on the <c>is</c> branch: the CFG condition
    /// recognizer (<c>NarrowingFlowAnalysis.RecognizeLeaf</c>) has never produced a fact for a
    /// <c>TypeCheck</c> condition, so <c>if a is Dog:</c> narrows nothing today. That gap is tracked
    /// separately (#1333); when it is closed, this theory should grow the cast-agreement half its
    /// isinstance sibling has.
    /// </para>
    /// </summary>
    public static TheoryData<string, string> IsOperatorTwinShapes() => new()
    {
        {
            "non-generic user class",
            @"
class Animal:
    pass

class Dog(Animal):
    def speak(self) -> str:
        return ""woof""

def f(a: Animal) -> bool:
    if a is Dog:
        return True
    return isinstance(a, Dog)
"
        },
        {
            "bare generic name filled from the subject",
            @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(b: Box[int]) -> bool:
    if b is Box:
        return True
    return isinstance(b, Box)
"
        },
        {
            "closed generic spelling",
            @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(x: object) -> bool:
    if x is Box[int]:
        return True
    return isinstance(x, Box[int])
"
        },
    };

    [Theory]
    [MemberData(nameof(IsOperatorTwinShapes))]
    public void IsOperatorAndIsinstanceClassifyTheSameTestToTheSameType(string shape, string source)
    {
        var (module, info) = Analyze(source);

        var isOperatorTests = Descendants(module)
            .OfType<TypeCheck>()
            .Select(tc => info.GetTypeTestLowering(tc.CheckType))
            .Where(l => l != null)
            .Select(l => l!)
            .ToList();

        isOperatorTests.Should().ContainSingle(
            $"the {shape} probe contains exactly one classified is-operator test");

        var isinstanceTests = Descendants(module)
            .OfType<FunctionCall>()
            .Where(c => c.Arguments.Length == 2)
            .Select(c => info.GetTypeTestLowering(c.Arguments[1]))
            .Where(l => l != null)
            .Select(l => l!)
            .ToList();

        isinstanceTests.Should().ContainSingle(
            $"the {shape} probe contains exactly one classified isinstance test");

        isOperatorTests[0].TestType.Should().Be(
            isinstanceTests[0].TestType,
            $"the {shape} test resolves through one classifier regardless of spelling");
        isOperatorTests[0].Kind.Should().Be(
            isinstanceTests[0].Kind,
            $"the {shape} test's lowering kind must not depend on the spelling");
    }

    [Fact]
    public void EquivalentTypeTestsOnBothBranchesSurviveTheMergePoint()
    {
        // The guard for the invariant that keeps narrowing facts SYMBOLIC. A fact's identity is its
        // textual TypeKey, not the AST node it came from, so two `isinstance(a, Dog)` checks written
        // at different source locations are EQUAL facts and survive the intersection join where
        // control flow rejoins. Resolving the operand to a SemanticType inside the fact would change
        // that key and silently drop the narrowing here — with no diagnostic, and with every
        // single-branch test still passing. Resolution therefore happens at the TypeChecker's
        // resolvers, against what the classifier recorded, and never in the dataflow engine.
        var (module, info) = Analyze(@"
class Animal:
    pass

class Dog(Animal):
    def speak(self) -> str:
        return ""woof""

def f(a: Animal, flag: bool) -> str:
    if flag:
        if not isinstance(a, Dog):
            return ""not a dog""
    else:
        if not isinstance(a, Dog):
            return ""still not a dog""
    return a.speak()
");

        // `a.speak()` type-checking at all is the assertion: Animal has no speak(), so the read only
        // resolves if the fact survived the join. Analyze() already rejected any diagnostic.
        var narrowedReads = Descendants(module)
            .OfType<Expression>()
            .Select(info.GetNarrowedReadLowering)
            .Where(l => l is { Kind: NarrowedReadKind.Cast })
            .ToList();

        narrowedReads.Should().ContainSingle(
            "only the post-join read of a is narrowed; the two checks themselves read the raw value");
        narrowedReads[0]!.CastTarget.Should().BeOfType<UserDefinedType>()
            .Which.Name.Should().Be("Dog");
    }

    [Fact]
    public void EquivalentClosedGenericTestsOnBothBranchesAlsoSurviveTheMergePoint()
    {
        // Same invariant for the closed generic spelling, whose textual key had to be added for it to
        // produce a fact at all — an operand shape that lowers as a type test but narrows nothing is
        // exactly the compiles-but-cannot-narrow outcome these rules exist to prevent (#1207).
        var (_, info) = Analyze(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def f(x: object, flag: bool) -> int:
    if flag:
        if not isinstance(x, Box[int]):
            return -1
    else:
        if not isinstance(x, Box[int]):
            return -2
    return x.value
");

        info.Should().NotBeNull();
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
            "agreement probe programs must type-check cleanly (a diagnostic would mask the narrowing)");

        return (module, semanticInfo);
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

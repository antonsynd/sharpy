using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance for <see cref="IterableElementDecider"/> (#1832, Design Decision 3 iv): the two entry
/// points — <see cref="IterableElementDecider.UnpeelProducerAnnotation"/> over the AST (what
/// <see cref="SynthesisAnalyzer"/> asks, before type checking) and
/// <see cref="IterableElementDecider.UnpeelProducerType"/> over the resolved type (what
/// <c>TypeInferenceService</c> asks, after) — exist only because synthesis runs before types; they
/// must never drift apart. This is a data-level conformance test (no <see cref="IntegrationTestBase"/>
/// compile-and-run needed for the roster half); the synthesis half asserts on <c>emit csharp</c>'s
/// base list, matching <c>SynthesizedInterfaceVisibilityTests</c>' own pattern.
/// </summary>
public class IterableElementDeciderConformanceTests : IntegrationTestBase
{
    public IterableElementDeciderConformanceTests(ITestOutputHelper output) : base(output) { }

    private const int RosterCount = 3;

    [Fact]
    public void Roster_IsAnchored()
    {
        IterableElementDecider.ProducerElementRoster.Length.Should().Be(RosterCount);
        IterableElementDecider.ProducerElementRoster.Should().BeEquivalentTo(
            new[] { BuiltinNames.Iterator, BuiltinNames.IEnumerator, BuiltinNames.IEnumerable });
    }

    /// <summary>
    /// Both entry points agree for every roster name, generator and non-generator alike (Decision 3
    /// iv) — the annotation-level unpeel and the resolved-type-level unpeel must answer the SAME
    /// element for the SAME producer, or synthesis (annotation-time) and the route
    /// (resolved-type-time) would disagree about what a class implements.
    /// </summary>
    [Theory]
    [InlineData(BuiltinNames.Iterator)]
    [InlineData(BuiltinNames.IEnumerator)]
    [InlineData(BuiltinNames.IEnumerable)]
    public void RosterName_UnpeelsTheSameElement_AnnotationAndResolvedType_NonGenerator(string producerName)
    {
        var annotation = new TypeAnnotation
        {
            Name = producerName,
            TypeArguments = System.Collections.Immutable.ImmutableArray.Create(new TypeAnnotation { Name = "int" }),
        };
        var resolvedType = new GenericType
        {
            Name = producerName,
            TypeArguments = new List<SemanticType> { SemanticType.Int },
        };

        var unpeeledAnnotation = IterableElementDecider.UnpeelProducerAnnotation(annotation, isGenerator: false);
        var unpeeledType = IterableElementDecider.UnpeelProducerType(resolvedType, isGenerator: false);

        unpeeledAnnotation.Name.Should().Be("int", $"{producerName}'s non-generator annotation unpeels to its element");
        unpeeledType.Should().Be(SemanticType.Int, $"{producerName}'s non-generator resolved type unpeels to its element");
    }

    [Theory]
    [InlineData(BuiltinNames.Iterator)]
    [InlineData(BuiltinNames.IEnumerator)]
    [InlineData(BuiltinNames.IEnumerable)]
    public void RosterName_IsUnchanged_AnnotationAndResolvedType_Generator(string producerName)
    {
        // A generator's annotation/type IS the element already — unpeeling is the identity, even
        // when the spelling happens to name a roster type (a generator would not normally be
        // annotated this way, per Decision 9, but the decider must still answer consistently).
        var annotation = new TypeAnnotation
        {
            Name = producerName,
            TypeArguments = System.Collections.Immutable.ImmutableArray.Create(new TypeAnnotation { Name = "int" }),
        };
        var resolvedType = new GenericType
        {
            Name = producerName,
            TypeArguments = new List<SemanticType> { SemanticType.Int },
        };

        IterableElementDecider.UnpeelProducerAnnotation(annotation, isGenerator: true).Should().Be(annotation);
        IterableElementDecider.UnpeelProducerType(resolvedType, isGenerator: true).Should().Be(resolvedType);
    }

    [Fact]
    public void UnrecognizedName_IsReturnedUnchanged_NonGenerator()
    {
        var annotation = new TypeAnnotation { Name = "int" };
        var resolvedType = SemanticType.Int;

        IterableElementDecider.UnpeelProducerAnnotation(annotation, isGenerator: false).Should().Be(annotation);
        IterableElementDecider.UnpeelProducerType(resolvedType, isGenerator: false).Should().Be(resolvedType);
    }

    /// <summary>
    /// An UNANNOTATED generator's resolved <see cref="FunctionSymbol.ReturnType"/> is
    /// <see cref="SemanticType.Void"/> (<c>TypeChecker.ResolveReturnType</c> defaults every
    /// unannotated function to Void) — not the "object" <see cref="SynthesisAnalyzer"/> substitutes
    /// for a null AST annotation, at the AST level, before type checking ever runs. Left as Void
    /// (or Unknown, defensively — belt-and-suspenders for the marker TypeInferenceService itself
    /// uses for "nothing resolved"), a route consumer (<c>list()</c>/<c>reversed()</c>) would refuse
    /// a receiver whose interface synthesis already put
    /// <c>IEnumerable[object]</c>/<c>IReverseEnumerable[object]</c> on it — the two entry points
    /// disagreeing is exactly what this decider exists to prevent.
    /// </summary>
    [Theory]
    [InlineData(true)]  // Void: what ResolveReturnType actually produces for an unannotated function
    [InlineData(false)] // Unknown: defensive — never observed in practice, but must not slip through
    public void Generator_VoidOrUnknownResolvedType_UnpeelsToObject(bool useVoid)
    {
        var input = useVoid ? SemanticType.Void : SemanticType.Unknown;
        IterableElementDecider.UnpeelProducerType(input, isGenerator: true)
            .Should().Be(SemanticType.Object, "an unannotated generator's Void/Unknown must agree with synthesis's own object default");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonGenerator_VoidOrUnknownResolvedType_IsReturnedUnchanged(bool useVoid)
    {
        // The non-generator arm has no "object" default of its own (synthesis's null-annotation
        // fallback only ever feeds the ANNOTATION side) — an unannotated, non-generator producer
        // return is a distinct, out-of-scope gap (not one #1832's decider answers for), so Void/Unknown
        // must stay unchanged here rather than silently acquiring the generator arm's default too.
        var input = useVoid ? SemanticType.Void : SemanticType.Unknown;
        IterableElementDecider.UnpeelProducerType(input, isGenerator: false)
            .Should().Be(input);
    }

    /// <summary>The base list of the first class/struct declaration in the emitted C#.</summary>
    private static string FirstTypeBaseList(string csharp)
    {
        var match = Regex.Match(csharp, @"(?:class|struct)\s+\w+(?:<[^>]*>)?\s*:\s*([^\r\n{]+)");
        match.Success.Should().BeTrue("the emitted C# must declare the type with a base list");
        return match.Groups[1].Value;
    }

    [Fact]
    public void SynthesisAnalyzer_NonGeneratorReversed_ProducesIReverseEnumerableOfElement()
    {
        var result = CompileAndExecute(
            "class R:\n"
            + "    items: list[int]\n"
            + "    def __init__(self) -> None:\n"
            + "        self.items = [4, 5]\n"
            + "    def __reversed__(self) -> Iterator[int]:\n"
            + "        return reversed(self.items)\n\n"
            + "def main() -> None:\n"
            + "    r = R()\n");

        // c02c's emitter half is #1832's paired follow-up (P3.2) — the SYNTHESIS half is this
        // commit's, verified by the base list, independent of whether the method body compiles yet.
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Info.ImplicitInterfaceSynthesis,
            "synthesis is announced by SPY1001");
        var baseList = FirstTypeBaseList(result.GeneratedCSharp!);
        baseList.Should().Contain("Sharpy.IReverseEnumerable<int>",
            $"the element must be int (unpeeled from Iterator[int]), not Iterator<int>; got: {baseList}");
    }

    [Fact]
    public void SynthesisAnalyzer_NonGeneratorIter_ProducesIEnumerableOfElement()
    {
        var result = CompileAndExecute(
            "class Bag:\n"
            + "    items: list[int]\n"
            + "    def __init__(self) -> None:\n"
            + "        self.items = [1, 2]\n"
            + "    def __iter__(self) -> Iterator[int]:\n"
            + "        return iter(self.items)\n\n"
            + "def main() -> None:\n"
            + "    e: IEnumerable[int] = Bag()\n");

        // The ASSIGNABILITY binds at the semantic level (this commit); the emitter bridge for the
        // non-generic IEnumerable interface is #1832's paired follow-up (P3.2) — not asserted here.
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "Bag must be assignable to IEnumerable[int] once __iter__ synthesizes it (c03)");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Info.ImplicitInterfaceSynthesis,
            "synthesis is announced by SPY1001");
        var baseList = FirstTypeBaseList(result.GeneratedCSharp!);
        baseList.Should().Contain("System.Collections.Generic.IEnumerable<int>",
            $"the element must be int (unpeeled from Iterator[int]); got: {baseList}");
    }
}

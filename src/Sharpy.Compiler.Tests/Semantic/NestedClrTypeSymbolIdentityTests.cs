using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A nested .NET type (<c>Environment.SpecialFolder</c>) is ONE <see cref="TypeSymbol"/> no matter
/// which route spells it (#1864).
///
/// <para><b>Contract.</b> The value route (<c>TypeChecker.ResolveClrMember</c>'s NestedType arm) and
/// the annotation route (<c>TypeResolver.LookupNestedType</c>) both map the reflected
/// <see cref="System.Type"/> through <c>ClrTypeBridge.GetOrCreateClrDefinitionSymbol</c>, whose
/// definition-symbol cache is an INSTANCE field. The two routes therefore have to share ONE bridge
/// instance: <c>TypeChecker</c> adopts <c>TypeResolver.ClrBridge</c>. Before that, the annotation
/// route constructed <c>new ClrTypeBridge()</c> per resolution, minting a fresh symbol every time;
/// the program still compiled only because <c>TypeHierarchyService.SameTypeSymbol</c> has a
/// <c>ClrType</c> arm, so any consumer keyed on REFERENCE equality (or on a symbol-keyed dictionary,
/// which <see cref="Symbol"/> gives reference semantics) saw two different types.</para>
///
/// <para><b>Anti-vacuity.</b> Each cell first asserts that BOTH routes produced a CLR-backed symbol
/// for the expected reflected type, so a pair of nulls or a pair of <c>Unknown</c>s cannot satisfy
/// the identity assertion.</para>
/// </summary>
public class NestedClrTypeSymbolIdentityTests
{
    /// <summary>Sharpy.Core makes <c>from system import ...</c> and the CLR module walk resolvable.</summary>
    private readonly CompilerApi _api = new(null, new[] { SharpyCoreReference.Location });

    private const string Source =
        "from system import Environment\n"
        + "\n"
        + "def pick(f: Environment.SpecialFolder) -> None:\n"
        + "    print(f)\n"
        + "\n"
        + "def main() -> None:\n"
        + "    pick(Environment.SpecialFolder.Desktop)\n";

    [Fact]
    public void NestedClrType_AnnotationAndValueRoutes_ProduceTheSameSymbolInstance()
    {
        var analysis = _api.Analyze(Source);
        analysis.Success.Should().BeTrue(
            "the program spells Environment.SpecialFolder in an annotation and in a value. Errors: {0}",
            string.Join(" | ", analysis.Diagnostics.Where(d => d.IsError).Select(d => d.Code + ": " + d.Message)));

        var annotationSymbol = AnnotationRouteSymbol(analysis);
        var valueSymbol = ValueRouteSymbol(analysis);

        // Positive control: both routes really resolved the nested CLR type. Without this the
        // identity assertion below would pass on two nulls.
        annotationSymbol.Should().NotBeNull("the parameter annotation resolves the nested CLR type");
        valueSymbol.Should().NotBeNull("the member-access chain resolves the nested CLR type");
        annotationSymbol!.ClrType.Should().Be(typeof(Environment.SpecialFolder));
        valueSymbol!.ClrType.Should().Be(typeof(Environment.SpecialFolder));

        ReferenceEquals(annotationSymbol, valueSymbol).Should().BeTrue(
            "the annotation route and the value route map the reflected type through the SAME "
            + "ClrTypeBridge instance, so a reference-equality or symbol-keyed consumer cannot see "
            + "two symbols for one nested .NET type (#1864)");
    }

    /// <summary>
    /// The symbol-keyed twin of the cell above: a dictionary with the default (reference) comparer
    /// for <see cref="Symbol"/> must find the value route's symbol under the annotation route's key.
    /// This is the shape every <c>Dictionary&lt;Symbol, ...&gt;</c> in the compiler relies on.
    /// </summary>
    [Fact]
    public void NestedClrType_SymbolKeyedLookup_HitsAcrossRoutes()
    {
        var analysis = _api.Analyze(Source);
        analysis.Success.Should().BeTrue();

        var annotationSymbol = AnnotationRouteSymbol(analysis);
        var valueSymbol = ValueRouteSymbol(analysis);
        annotationSymbol.Should().NotBeNull();
        valueSymbol.Should().NotBeNull();

        var bySymbol = new Dictionary<TypeSymbol, string> { [annotationSymbol!] = "annotation" };

        bySymbol.TryGetValue(valueSymbol!, out var found).Should().BeTrue(
            "Symbol overrides record equality with REFERENCE equality, so a symbol-keyed dictionary "
            + "hits only when both routes yield the same instance (#1864)");
        found.Should().Be("annotation");
    }

    // -- Route accessors -----------------------------------------------------------

    /// <summary>The symbol the parameter ANNOTATION <c>f: Environment.SpecialFolder</c> resolved to.</summary>
    private static TypeSymbol? AnnotationRouteSymbol(SemanticResult analysis)
    {
        var pick = analysis.Ast!.Body.OfType<FunctionDef>().Single(f => f.Name == "pick");
        var annotation = pick.Parameters[0].Type;
        annotation.Should().NotBeNull("the parameter is annotated");
        return (analysis.SemanticInfo!.GetTypeAnnotation(annotation!) as UserDefinedType)?.Symbol;
    }

    /// <summary>
    /// The symbol the VALUE spelling <c>Environment.SpecialFolder</c> (the receiver of
    /// <c>.Desktop</c>) was typed with.
    /// </summary>
    private static TypeSymbol? ValueRouteSymbol(SemanticResult analysis)
    {
        var access = Descendants(analysis.Ast!)
            .OfType<MemberAccess>()
            .Single(m => m.Member == "SpecialFolder");
        return (analysis.SemanticInfo!.GetExpressionType(access) as UserDefinedType)?.Symbol;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}

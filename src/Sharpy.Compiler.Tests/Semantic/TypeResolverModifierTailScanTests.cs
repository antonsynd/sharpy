using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1781: <c>TypeResolver.ResolveTypeAnnotation</c> has ONE modifier tail
/// (<c>ApplyAnnotationModifiers</c>), and every arm that returns a resolved type goes through it.
///
/// <para><b>Why a scan.</b> The defect class is an arm that returns EARLY with its own partial copy
/// of the tail: the <c>Self</c> arm applied <c>?</c> and <c>| None</c> and never <c>!E</c>, so
/// <c>Self !str</c> resolved to a bare <c>Self</c>; the <c>LiteralString</c> and <c>Template</c>
/// arms applied none at all. A per-kind matrix catches the kinds it enumerates; this catches the
/// NEXT arm somebody adds, which is the part a matrix cannot.</para>
///
/// <para><b>Mutation.</b> Delete the <c>ApplyAnnotationModifiers</c> call from the <c>Self</c> arm
/// (or from any other) -> the counts diverge and this test goes RED, alongside the
/// <c>Self</c> x <c>!E</c> cell of <see cref="TypeAnnotationModifierMatrixTests"/>.</para>
/// </summary>
public class TypeResolverModifierTailScanTests
{
    /// <summary>
    /// The returns that legitimately do NOT apply the tail, each with the reason. Both are about
    /// there being no resolution to modify at that point — neither is an arm that resolved a name.
    /// </summary>
    private static readonly (string Return, string Reason)[] ExemptReturns =
    {
        ("return SemanticType.Unknown;",
            "the annotation is null — there is no annotation to read modifiers from"),
        ("return cached;",
            "the cached value IS a previous call's tail output; applying it again double-wraps"),
    };

    [Fact]
    public void EveryReturningArmOfResolveTypeAnnotation_AppliesTheModifierTail()
    {
        var body = ResolveTypeAnnotationBody();

        body.Should().Contain("ApplyAnnotationModifiers",
            "positive control: the scan must be looking at the method that has the tail");

        var returns = Regex.Matches(body, @"^\s*return\b", RegexOptions.Multiline).Count;
        var tailApplications = Regex.Matches(body, @"ApplyAnnotationModifiers\(").Count;

        returns.Should().Be(tailApplications + ExemptReturns.Length,
            "every return of ResolveTypeAnnotation applies the modifier tail, except "
            + string.Join("; ", ExemptReturns.Select(e => $"`{e.Return}` ({e.Reason})"))
            + $". Found {returns} returns and {tailApplications} tail applications");

        foreach (var (exempt, reason) in ExemptReturns)
        {
            body.Should().Contain(exempt,
                $"the exempt return `{exempt}` must still exist, because {reason} — "
                + "an exemption whose subject vanished silently loosens the count above");
        }
    }

    /// <summary>
    /// The source text of <c>ResolveTypeAnnotation</c>, from its signature to the closing brace of
    /// its body, located by brace depth so a nested block cannot end the scan early.
    /// </summary>
    private static string ResolveTypeAnnotationBody()
    {
        var path = FindTypeResolverPath();
        var text = File.ReadAllText(path);

        var signature = text.IndexOf("public SemanticType ResolveTypeAnnotation(", StringComparison.Ordinal);
        signature.Should().BeGreaterThan(-1, $"positive control: ResolveTypeAnnotation must exist in {path}");

        var open = text.IndexOf('{', signature);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }

        throw new InvalidOperationException("ResolveTypeAnnotation's body is unbalanced");
    }

    private static string FindTypeResolverPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Sharpy.Compiler", "Semantic", "TypeResolver.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("TypeResolver.cs not found from " + AppContext.BaseDirectory);
    }
}

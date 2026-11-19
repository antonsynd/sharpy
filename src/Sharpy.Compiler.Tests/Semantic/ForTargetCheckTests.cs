using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Infrastructure;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// plan-775c50 (P77): a member or element <c>for</c>-statement target is type-checked as the
/// comprehension for-clause target and the with-as target are, so the checker records its
/// resolution — the store site the emitter reads a Guarded <c>@static</c> value's storage fact from
/// (<see cref="SemanticInfo.GetMemberAccessResolution"/>), never re-deriving it. Before the fix
/// <c>TypeChecker.CheckFor</c> checked only an identifier or tuple target, and <c>for H.t in xs:</c>
/// recorded nothing.
/// <para><b>Cells.</b> The three target shapes the statement admits besides a name and a tuple —
/// a type-qualified static (<c>H.t</c>), an instance member (<c>o.x</c>), an element (<c>xs[0]</c>) —
/// the first recording the member it bound, the other two their receiver's type, each with the comprehension for-clause's spelling of the same target beside it as the positive
/// control (that path always checked its target).</para>
/// </summary>
public class ForTargetCheckTests
{
    private const string Source =
        "class H:\n"
        + "    @static\n"
        + "    t: int = 1\n"
        + "\n"
        + "class O:\n"
        + "    x: int = 0\n"
        + "\n"
        + "def main() -> None:\n"
        + "    o = O()\n"
        + "    xs: list[int] = [0]\n"
        + "    for H.t in [5, 6]:\n"
        + "        pass\n"
        + "    for o.x in [7, 8]:\n"
        + "        pass\n"
        + "    for xs[0] in [9, 10]:\n"
        + "        pass\n"
        + "    a = [0 for H.t in [5, 6]]\n"
        + "    b = [0 for o.x in [7, 8]]\n"
        + "    c = [0 for xs[0] in [9, 10]]\n"
        + "    print(H.t, o.x, xs[0], a, b, c)\n";

    private static (Module Ast, SemanticInfo Info) Analyze()
    {
        var result = new CompilerApi(null, new[] { SharpyCoreReference.Location }).Analyze(Source);
        result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}: {d.Message}")
            .Should().BeEmpty("the probe program type-checks");
        return (result.Ast!, result.SemanticInfo!);
    }

    private static IEnumerable<(string Site, Expression Target)> Targets(Module ast)
    {
        foreach (var node in AstDescendants.SelfAndDescendants(ast))
        {
            if (node is ForStatement f)
                yield return ("for", f.Target);
            else if (node is ForClause c)
                yield return ("comprehension", c.Target);
        }
    }

    [Fact]
    public void MemberAndElementForTargets_AreChecked_AsTheComprehensionTargetIs()
    {
        var (ast, info) = Analyze();
        var targets = Targets(ast).ToList();
        targets.Should().HaveCount(6, "three for statements and three comprehension clauses (the probe)");

        var failures = new List<string>();
        foreach (var (site, target) in targets)
        {
            var spelling = target switch
            {
                MemberAccess { Object: Identifier o } ma => $"{o.Name}.{ma.Member}",
                IndexAccess { Object: Identifier o } => $"{o.Name}[0]",
                _ => target.GetType().Name,
            };
            if (info.GetExpressionType(target) is null or UnknownType)
                failures.Add($"{site} target {spelling}: no type recorded");
            // A type-qualified member (`H.t`) records the member it bound; an instance member or an
            // element records its receiver's type (the checker visited it).
            if (target is MemberAccess { Object: Identifier { Name: "H" } } typeQualified)
            {
                if (info.GetMemberAccessResolution(typeQualified) is not { } resolution)
                    failures.Add($"{site} target {spelling}: no member resolution recorded");
                else if (resolution.Member.Name != typeQualified.Member)
                    failures.Add($"{site} target {spelling}: resolved to '{resolution.Member.Name}'");
            }
            else if (((target as MemberAccess)?.Object ?? (target as IndexAccess)?.Object) is { } receiver
                     && info.GetExpressionType(receiver) is null)
            {
                failures.Add($"{site} target {spelling}: its receiver was never checked");
            }
        }

        failures.Should().BeEmpty("a for-statement's member or element target is checked as a comprehension's is");

        // The Guarded @static value the emitter must recognise at the store (D4): the recorded member
        // carries its storage fact.
        var staticTarget = targets.First(t => t.Site == "for" && t.Target is MemberAccess { Object: Identifier { Name: "H" } }).Target;
        info.GetMemberAccessResolution((MemberAccess)staticTarget)!.Value.Member
            .Should().BeOfType<VariableSymbol>().Which.Name.Should().Be("t");
    }
}

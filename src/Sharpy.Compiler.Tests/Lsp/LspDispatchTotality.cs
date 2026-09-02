using System.Text.RegularExpressions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// One rule set for the LSP dispatch-totality guards (plan-950124 Phase 2). Each guard supplies
/// a production switch (file + method), the reflection universe of its scrutinee kind, the arms
/// it expects and a justified-default roster (kind → reason); <see cref="Verify"/> runs the four
/// assertions every guard owes:
/// <list type="number">
/// <item>arms read by <see cref="SwitchArmScan"/> == expected arms ∪ declared non-kind arms;</item>
/// <item>expected arms ∪ justified-default keys == the reflection universe (no unclassified kind);</item>
/// <item>the two rosters are disjoint;</item>
/// <item>no phantom name (every rostered name is a live concrete kind, every non-kind arm is NOT
/// one), and every justified-default reason opens with a recognised reason-class tag.</item>
/// </list>
/// Reason classes: <c>UNREACHABLE</c> (the kind never reaches this switch — the reason says where
/// it is consumed instead), <c>CONTRACTUAL</c> (nothing for this feature to emit), <c>BASE-ARM</c>
/// (a base-type arm of the same switch covers it), <c>PRE-SWITCH</c> (an if-chain before the
/// switch in the same method covers it), <c>MISS #NNN</c> (a known gap rostered against an issue).
/// Six rosters, one rule set: a kind added to the AST fails every guard that has not classified
/// it; an arm added or removed from a handler fails its guard until the roster acknowledges it.
/// </summary>
internal static class LspDispatchTotality
{
    private static readonly Regex ReasonShape = new(
        @"^(UNREACHABLE|CONTRACTUAL|BASE-ARM|PRE-SWITCH|MISS #\d+): \S.*$",
        RegexOptions.Singleline);

    /// <summary>
    /// Every public, concrete subtype of <paramref name="baseType"/> in the compiler assembly,
    /// by simple name. <c>typeof(Statement)</c> → 31, <c>typeof(Expression)</c> → 41,
    /// <c>typeof(Pattern)</c> → 16, <c>typeof(ComprehensionClause)</c> → 2, <c>typeof(Node)</c> → 94
    /// (measured @ 277f54543).
    /// </summary>
    public static IReadOnlySet<string> Universe(Type baseType)
    {
        return baseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A justified-default roster that applies <paramref name="reason"/> to every universe kind
    /// outside <paramref name="arms"/>. Callers override individual entries afterwards; the
    /// disjointness and phantom checks in <see cref="Verify"/> still apply to the result.
    /// </summary>
    public static Dictionary<string, string> UniformDefault(
        IReadOnlySet<string> universe, IReadOnlySet<string> arms, string reason)
    {
        return universe
            .Where(k => !arms.Contains(k))
            .ToDictionary(k => k, _ => reason, StringComparer.Ordinal);
    }

    /// <param name="output">Receives the classification table.</param>
    /// <param name="repoRelativePath">Production file holding the switch.</param>
    /// <param name="methodName">Method whose switch arms are read.</param>
    /// <param name="universeBase">Scrutinee base type; its concrete subtypes are the universe.</param>
    /// <param name="expectedArms">Concrete kinds the switch must name.</param>
    /// <param name="justifiedDefault">Concrete kinds the switch must NOT name, each with a tagged reason.</param>
    /// <param name="nonKindArms">
    /// Names the scanner reads that are not universe kinds — a base-type catch-all arm, or the
    /// arms of a nested switch on another scrutinee (operator enums, semantic types). Kept out of
    /// the kind universe explicitly: a non-kind arm that IS a universe kind is a roster error.
    /// </param>
    public static void Verify(
        ITestOutputHelper output,
        string repoRelativePath,
        string methodName,
        Type universeBase,
        IReadOnlySet<string> expectedArms,
        IReadOnlyDictionary<string, string> justifiedDefault,
        IReadOnlySet<string>? nonKindArms = null)
    {
        nonKindArms ??= new HashSet<string>();
        var universe = Universe(universeBase);
        var arms = SwitchArmScan.CaseTypeNames(repoRelativePath, methodName);
        Assert.NotEmpty(arms);

        output.WriteLine($"{methodName} ({repoRelativePath}) — universe {universeBase.Name}: {universe.Count} kinds, " +
            $"{expectedArms.Count} arms, {justifiedDefault.Count} justified defaults, {nonKindArms.Count} non-kind arms");
        foreach (var kind in universe.OrderBy(k => k, StringComparer.Ordinal))
        {
            var label = expectedArms.Contains(kind) ? "ARM"
                : justifiedDefault.TryGetValue(kind, out var reason) ? reason
                : "*** UNCLASSIFIED ***";
            output.WriteLine($"  {kind,-28} {label}");
        }

        // 1. The switch names exactly the expected arms (plus the declared non-kind arms).
        var expectedScan = new HashSet<string>(expectedArms, StringComparer.Ordinal);
        expectedScan.UnionWith(nonKindArms);
        Assert.True(arms.SetEquals(expectedScan),
            $"{methodName} arms differ from the roster.\n" +
            $"  Extra in switch: {string.Join(", ", arms.Except(expectedScan).OrderBy(a => a))}\n" +
            $"  Missing from switch: {string.Join(", ", expectedScan.Except(arms).OrderBy(a => a))}");

        // 2. arms ∪ justified-default == universe; 4a. no phantom.
        var classified = new HashSet<string>(expectedArms, StringComparer.Ordinal);
        classified.UnionWith(justifiedDefault.Keys);
        var unclassified = universe.Except(classified).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var phantom = classified.Except(universe).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(unclassified.Count == 0,
            $"{methodName}: unclassified {universeBase.Name} kinds (neither an arm nor a justified default): " +
            string.Join(", ", unclassified));
        Assert.True(phantom.Count == 0,
            $"{methodName}: phantom names (rostered but not a concrete {universeBase.Name} kind): " +
            string.Join(", ", phantom));

        // 3. Disjoint.
        var overlap = expectedArms.Intersect(justifiedDefault.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(overlap.Count == 0,
            $"{methodName}: kinds rostered as BOTH arm and justified default: {string.Join(", ", overlap)}");

        // 4b. Non-kind arms are not universe kinds, and every reason names its class.
        var nonKindPhantom = nonKindArms.Intersect(universe).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(nonKindPhantom.Count == 0,
            $"{methodName}: non-kind arms that ARE {universeBase.Name} kinds (classify them instead): " +
            string.Join(", ", nonKindPhantom));
        var untagged = justifiedDefault
            .Where(kv => !ReasonShape.IsMatch(kv.Value))
            .Select(kv => $"{kv.Key} => \"{kv.Value}\"")
            .ToList();
        Assert.True(untagged.Count == 0,
            $"{methodName}: justified-default reasons without a class tag " +
            "(UNREACHABLE | CONTRACTUAL | BASE-ARM | PRE-SWITCH | MISS #NNN): " +
            string.Join("; ", untagged));
    }
}

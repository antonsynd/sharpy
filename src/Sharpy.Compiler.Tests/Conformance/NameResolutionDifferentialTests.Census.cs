namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The generated pool's size per family × tag × rule, a literal. <c>coveragePercent</c> is cells run over cells generated, so a
/// generator that drops half an axis still reads 100, and an arm retagged from must-refuse to the more forgiving must-refuse-or-throw
/// moves no verdict. Both move a count here. The table reads the generator and never the compiler, so a plan draining the allowlist
/// does not touch it; one that changes the generator re-takes it in the commit that does.
/// </summary>
public partial class NameResolutionDifferentialTests
{
    private static readonly IReadOnlyDictionary<(Family Family, SharpyTag Tag, string Rule), int> GeneratedCensus =
        new Dictionary<(Family, SharpyTag, string), int>
        {
            [(Family.Local, SharpyTag.MustRefuse, "N5")] = 147,
            [(Family.Local, SharpyTag.MustRefuse, "N5-declared-later")] = 190,
            [(Family.Local, SharpyTag.MustRefuse, "N5-self")] = 3,
            [(Family.Local, SharpyTag.MustRefuse, "S2")] = 167,
            [(Family.Local, SharpyTag.MustRefuse, "S3")] = 35,
            [(Family.Local, SharpyTag.MustRefuse, "kind:SPY0204")] = 1,
            [(Family.Local, SharpyTag.MustRun, "#1974")] = 5,
            [(Family.Local, SharpyTag.MustRun, "N1")] = 45,
            [(Family.Local, SharpyTag.MustRun, "N1-innermost")] = 504,
            [(Family.Local, SharpyTag.MustRun, "N5")] = 42,
            [(Family.Local, SharpyTag.MustRun, "N5-block-scope")] = 97,
            [(Family.Local, SharpyTag.MustRun, "R-BM")] = 26,
            [(Family.Local, SharpyTag.MustRun, "R-LG")] = 60,
            [(Family.Read, SharpyTag.MayRefuse, "kind:SPY0340")] = 71,
            [(Family.Read, SharpyTag.MustRefuse, "N1-arity")] = 6,
            [(Family.Read, SharpyTag.MustRefuse, "N1-methods")] = 330,
            [(Family.Read, SharpyTag.MustRefuse, "N1-type-error")] = 592,
            [(Family.Read, SharpyTag.MustRefuse, "N2")] = 600,
            [(Family.Read, SharpyTag.MustRefuse, "N2-module")] = 11,
            [(Family.Read, SharpyTag.MustRefuse, "R-KG")] = 15,
            [(Family.Read, SharpyTag.MustRefuse, "R-KT")] = 12,
            [(Family.Read, SharpyTag.MustRefuse, "R1(b)")] = 45,
            [(Family.Read, SharpyTag.MustRefuse, "SPY0401")] = 460,
            [(Family.Read, SharpyTag.MustRefuse, "kind:SPY0290")] = 302,
            [(Family.Read, SharpyTag.MustRefuse, "kind:enum-member-constant")] = 16,
            [(Family.Read, SharpyTag.MustRefuse, "kind:instance-at-initializer")] = 803,
            [(Family.Read, SharpyTag.MustRefuse, "kind:result-wrap")] = 2,
            [(Family.Read, SharpyTag.MustRefuseOrThrow, "N3-cycle")] = 70,
            [(Family.Read, SharpyTag.MustRun, "A3")] = 32,
            [(Family.Read, SharpyTag.MustRun, "N1")] = 861,
            [(Family.Read, SharpyTag.MustRun, "N1+N3")] = 81,
            [(Family.Read, SharpyTag.MustRun, "N1-innermost")] = 189,
            [(Family.Read, SharpyTag.MustRun, "N1-nesting")] = 99,
            [(Family.Read, SharpyTag.MustRun, "N3")] = 114,
            [(Family.Read, SharpyTag.MustRun, "N7")] = 917,
            [(Family.Read, SharpyTag.MustRun, "N3-order")] = 12,
            [(Family.Read, SharpyTag.MustRun, "R-Y")] = 410,
            [(Family.Read, SharpyTag.MustRun, "construction-order")] = 4,
            [(Family.Seed, SharpyTag.MayRefuse, "seed")] = 1,
            [(Family.Seed, SharpyTag.MustRefuse, "seed")] = 62,
            [(Family.Seed, SharpyTag.MustRefuseOrThrow, "seed")] = 4,
            [(Family.Seed, SharpyTag.MustRun, "seed")] = 99,
            [(Family.Seed, SharpyTag.Unruled, "seed")] = 2,
            [(Family.Store, SharpyTag.MustRefuse, "N5")] = 31,
            [(Family.Store, SharpyTag.MustRefuse, "N6")] = 93,
            [(Family.Store, SharpyTag.MustRefuse, "N6-declaration")] = 80,
            [(Family.Store, SharpyTag.MustRefuse, "N6-type-kind")] = 63,
            [(Family.Store, SharpyTag.MustRefuse, "R-KU")] = 9,
            [(Family.Store, SharpyTag.MustRefuse, "SPY0606")] = 40,
            [(Family.Store, SharpyTag.MustRefuse, "kind:SPY0225")] = 28,
            [(Family.Store, SharpyTag.MustRefuse, "kind:SPY0290")] = 13,
            [(Family.Store, SharpyTag.MustRefuse, "kind:SPY0606")] = 10,
            [(Family.Store, SharpyTag.MustRun, "#1974")] = 128,
            [(Family.Store, SharpyTag.MustRun, "N5")] = 38,
            [(Family.Store, SharpyTag.MustRun, "N6")] = 33,
            [(Family.Store, SharpyTag.MustRun, "N6-same-function")] = 12,
            [(Family.Store, SharpyTag.MustRun, "N7")] = 18,
            [(Family.Store, SharpyTag.MustRun, "R-BM")] = 64,
            [(Family.Store, SharpyTag.MustRun, "R-Y")] = 8,
            [(Family.Store, SharpyTag.Unruled, "unruled:global-creates-binding")] = 10,
            [(Family.TypeMember, SharpyTag.MustRun, "P52")] = 440,
        };

    internal static IEnumerable<string> CensusFailures(IReadOnlyList<NameCell> pool)
    {
        var measured = pool.GroupBy(c => (c.Family, c.Tag, c.Rule)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var key in measured.Keys.Union(GeneratedCensus.Keys).OrderBy(k => k.ToString(), StringComparer.Ordinal))
        {
            int pinned = GeneratedCensus.GetValueOrDefault(key);
            int generated = measured.GetValueOrDefault(key);
            if (pinned != generated)
                yield return $"Census: {key.Family} × {key.Tag} × {key.Rule} is pinned at {pinned} and generates {generated}";
        }
    }
}

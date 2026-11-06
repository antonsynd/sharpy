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
            [(Family.Local, SharpyTag.MustRefuse, "N5")] = 50,
            [(Family.Local, SharpyTag.MustRefuse, "N5-declared-later")] = 21,
            [(Family.Local, SharpyTag.MustRefuse, "N5-self")] = 3,
            [(Family.Local, SharpyTag.MustRefuse, "S2")] = 69,
            [(Family.Local, SharpyTag.MustRefuse, "S3")] = 73,
            [(Family.Local, SharpyTag.MustRun, "#1974")] = 18,
            [(Family.Local, SharpyTag.MustRun, "N1")] = 33,
            [(Family.Local, SharpyTag.MustRun, "N1-innermost")] = 504,
            [(Family.Local, SharpyTag.MustRun, "N5")] = 42,
            [(Family.Local, SharpyTag.MustRun, "R-BM")] = 26,
            [(Family.Local, SharpyTag.Unruled, "unruled:N5-before-binding")] = 120,
            [(Family.Local, SharpyTag.Unruled, "unruled:N5-match-capture")] = 52,
            [(Family.Local, SharpyTag.Unruled, "unruled:S2-builtin")] = 40,
            [(Family.Local, SharpyTag.Unruled, "unruled:rowless-construct")] = 16,
            [(Family.Read, SharpyTag.MayRefuse, "kind:SPY0340")] = 71,
            [(Family.Read, SharpyTag.MustRefuse, "N1-arity")] = 6,
            [(Family.Read, SharpyTag.MustRefuse, "N1-methods")] = 270,
            [(Family.Read, SharpyTag.MustRefuse, "N1-type-error")] = 592,
            [(Family.Read, SharpyTag.MustRefuse, "N2")] = 600,
            [(Family.Read, SharpyTag.MustRefuse, "N2-module")] = 12,
            [(Family.Read, SharpyTag.MustRefuse, "R-KG")] = 15,
            [(Family.Read, SharpyTag.MustRefuse, "R1(b)")] = 45,
            [(Family.Read, SharpyTag.MustRefuse, "SPY0401")] = 460,
            [(Family.Read, SharpyTag.MustRefuse, "kind:SPY0212")] = 4,
            [(Family.Read, SharpyTag.MustRefuse, "kind:SPY0290")] = 274,
            [(Family.Read, SharpyTag.MustRefuse, "kind:enum-member-constant")] = 16,
            [(Family.Read, SharpyTag.MustRefuse, "kind:instance-at-initializer")] = 803,
            [(Family.Read, SharpyTag.MustRefuseOrThrow, "N3-cycle")] = 28,
            [(Family.Read, SharpyTag.MustRun, "A3")] = 32,
            [(Family.Read, SharpyTag.MustRun, "N1")] = 860,
            [(Family.Read, SharpyTag.MustRun, "N1+N3")] = 81,
            [(Family.Read, SharpyTag.MustRun, "N1-innermost")] = 189,
            [(Family.Read, SharpyTag.MustRun, "N1-nesting")] = 36,
            [(Family.Read, SharpyTag.MustRun, "N3")] = 93,
            [(Family.Read, SharpyTag.MustRun, "N7")] = 799,
            [(Family.Read, SharpyTag.MustRun, "R-Y")] = 410,
            [(Family.Seed, SharpyTag.MayRefuse, "seed")] = 1,
            [(Family.Seed, SharpyTag.MustRefuse, "seed")] = 59,
            [(Family.Seed, SharpyTag.MustRefuseOrThrow, "seed")] = 4,
            [(Family.Seed, SharpyTag.MustRun, "seed")] = 99,
            [(Family.Seed, SharpyTag.Unruled, "seed")] = 3,
            [(Family.Store, SharpyTag.MustRefuse, "N5")] = 21,
            [(Family.Store, SharpyTag.MustRefuse, "N6")] = 93,
            [(Family.Store, SharpyTag.MustRefuse, "SPY0606")] = 40,
            [(Family.Store, SharpyTag.MustRefuse, "kind:SPY0225")] = 18,
            [(Family.Store, SharpyTag.MustRun, "#1974")] = 132,
            [(Family.Store, SharpyTag.MustRun, "N5")] = 28,
            [(Family.Store, SharpyTag.MustRun, "N6")] = 33,
            [(Family.Store, SharpyTag.MustRun, "N6-same-function")] = 12,
            [(Family.Store, SharpyTag.MustRun, "R-BM")] = 66,
            [(Family.Store, SharpyTag.MustRun, "R-Y")] = 8,
            [(Family.Store, SharpyTag.Unruled, "unruled:N6-non-writable")] = 63,
            [(Family.TypeMember, SharpyTag.MustRun, "P52")] = 410,
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

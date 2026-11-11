using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A2 of plan-638e8b (#2476) — "the mapping is spelled once", over the real sources: the bound scan of
/// <see cref="MappingScan"/> over the two compilations G1 builds (<c>src/Sharpy.Core</c>, then
/// <c>src/Sharpy.Stdlib</c> with its generated spy C#), judged with G1's axis-1 reach from
/// <see cref="DoorCensus"/>. Outside <c>PathErrors.cs</c> no source creates an <c>OSError</c> subclass;
/// every plain-<c>OSError</c> creation is a row of <see cref="MappingRoster.PlainOSError"/>; every catch
/// around a door that can receive an I/O failure is <c>throw PathErrors.Translate…(ex, path)</c> or a row
/// of <see cref="MappingRoster.Swallows"/>. Rosters are compared in both directions (unlisted or stale
/// is red). Stated blind arm: a path-state failure spelled as plain <c>OSError</c> under a roster row is
/// invisible here; G1's differential sees it (mutation (h)).
/// </summary>
public class MappingSpelledOnceTests
{
    private readonly ITestOutputHelper _output;

    public MappingSpelledOnceTests(ITestOutputHelper output) => _output = output;

    private static readonly Lazy<(MappingScanResult Core, MappingScanResult Stdlib)> Passes = new(() =>
    {
        var census = DoorCensus.Instance.Value;
        var (plain, subclasses) = MappingScan.Family();
        var coreReach = new HashSet<string>(census.Core.Reaching.Keys, StringComparer.Ordinal);
        var core = MappingScan.Run(DoorScan.CoreCompilation(),
            new MappingScanContext(census.Map, DoorScanOptions.None, coreReach, plain, subclasses));
        var stdlib = MappingScan.Run(DoorScan.StdlibCompilation(),
            new MappingScanContext(census.Map, new DoorScanOptions("Sharpy.Core", coreReach),
                new HashSet<string>(census.Stdlib.Reaching.Keys, StringComparer.Ordinal), plain, subclasses));
        return (core, stdlib);
    });

    /// <summary>A degraded compilation cannot pass by finding nothing: both bind, and the scan reads catches and creations.</summary>
    [Fact]
    public void BothPasses_BindAndReadTheSources()
    {
        var (core, stdlib) = Passes.Value;
        foreach (var (name, pass) in new[] { ("Core", core), ("Stdlib", stdlib) })
        {
            Assert.True(pass.Errors.Count == 0, $"{name}: compilation errors:\n  " + string.Join("\n  ", pass.Errors.Take(20)));
            Assert.True(pass.Unbound.Count == 0, $"{name}: unbound:\n  " + string.Join("\n  ", pass.Unbound.Take(20)));
        }
        Assert.True(core.HelperFileSeen, "the Core pass must see PathErrors.cs (the one spelling) to exclude it");
        Assert.True(stdlib.TryBlocksReachingDoor >= 10, $"Stdlib: only {stdlib.TryBlocksReachingDoor} try block(s) reach a door");
        Assert.Contains(stdlib.Catches, c => c.Shape == CatchShape.Translate);
        Assert.Contains(core.Catches, c => c.Shape == CatchShape.Translate && c.Key.StartsWith("src/Sharpy.Core/Open.cs:", StringComparison.Ordinal));
        Assert.NotEmpty(core.PlainCreations.Concat(stdlib.PlainCreations));
    }

    [Fact]
    public void A2_TheMappingIsSpelledOnce()
    {
        var (core, stdlib) = Passes.Value;
        var problems = MappingScan.Judge(new[] { core, stdlib }, MappingRoster.PlainOSError, MappingRoster.Swallows);
        Assert.True(problems.Count == 0, "A2 (fix at the site: call PathErrors; a roster row is only for a site that is not path state):\n  "
                                         + string.Join("\n  ", problems));
    }

    /// <summary>For review: every judged catch and every family creation, with its witness.</summary>
    [Fact]
    public void Sites_AreListed()
    {
        var (core, stdlib) = Passes.Value;
        foreach (var pass in new[] { core, stdlib })
        {
            foreach (var c in pass.Catches.OrderBy(c => c.Site, StringComparer.Ordinal))
                _output.WriteLine($"catch {c.Shape,-9} {c.Site} {c.Key} catch ({c.Caught}) via {c.Witness}");
            foreach (var s in pass.PlainCreations.OrderBy(c => c.Site, StringComparer.Ordinal))
                _output.WriteLine($"plain OSError {s.Site} {s.Key}");
            foreach (var s in pass.SubclassCreations.OrderBy(c => c.Site, StringComparer.Ordinal))
                _output.WriteLine($"SUBCLASS {s.Type} {s.Site} {s.Key}");
            _output.WriteLine($"try blocks {pass.TryBlocks}, reaching a door {pass.TryBlocksReachingDoor}");
        }
    }
}

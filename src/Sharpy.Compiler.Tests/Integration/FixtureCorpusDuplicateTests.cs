using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2512 (plan-88736c D2): no two fixture units in <see cref="FixtureRoots.All"/> have the same
/// input. A body-equal pair runs the same program twice, so it costs a shard slot and moves every
/// corpus census without testing anything new.
///
/// <para>
/// A unit is what <see cref="FixtureDiscoveryHelper"/> discovers (skipped fixtures included): a
/// single <c>.spy</c> file, or a multi-file fixture's whole directory. Its key is SHA-256 over
/// the normalized source plus the sorted <see cref="TestFixtureInfo.Features"/>. The source of a
/// single-file unit is its body; of a multi-file unit, each <c>.spy</c> file as
/// <c>### &lt;relpath&gt;</c> then its body, ordinal by relative path. Normalizing turns CRLF into
/// LF, trims each line's trailing whitespace and the outer blank lines.
/// </para>
///
/// <para>
/// Sidecars are not in the key except <c>.features</c>: two units with one body and different
/// <c>.features</c> compile under different feature sets (an <c>experimental/*</c> fixture and its
/// ungated SPY0331 twin), so they are not duplicates. Every other sidecar (<c>.expected</c>,
/// <c>.error</c>, <c>.warning</c>, <c>.expected.cs</c>, <c>.skip</c>) states what is checked about
/// the input, not what the input is; one input with two expectations is still one input.
/// </para>
/// </summary>
public class FixtureCorpusDuplicateTests
{
    private sealed record Unit(string Name, string Source, IReadOnlyList<string> Features);

    [Fact]
    public void NoTwoFixtureUnits_HaveTheSameInput()
    {
        var groups = DuplicateGroups(Units(), withFeatures: true);

        Assert.True(
            groups.Count == 0,
            $"{groups.Count} groups / {groups.Sum(g => g.Count)} fixture units have the same input "
            + "(normalized source + .features). Keep one per group (the oldest stem, unless its name "
            + "states a superseded behaviour) and delete the others with their sidecars:\n"
            + string.Join("\n", groups.Select(g => "  " + string.Join(" == ", g))));
    }

    /// <summary>
    /// Positive control for the <c>.features</c> arm of the key: the corpus holds body-equal units
    /// that differ only by their <c>.features</c> sidecar (the experimental twins). Without this,
    /// a key that ignored <c>Features</c> and a corpus with no such pair would look the same.
    /// </summary>
    [Fact]
    public void FeaturesSidecar_SeparatesAtLeastOneBodyEqualGroup()
    {
        var units = Units();
        var bodyOnly = DuplicateGroups(units, withFeatures: false).Count;
        var withFeatures = DuplicateGroups(units, withFeatures: true).Count;

        Assert.True(
            bodyOnly > withFeatures,
            $"expected at least one body-equal group that .features splits; body-only {bodyOnly}, "
            + $"with features {withFeatures}");
    }

    private static List<Unit> Units()
    {
        var units = new List<Unit>();
        foreach (var root in FixtureRoots.All)
        {
            // DiscoverFixtures builds an unlabeled root, so the label is prefixed here (N1).
            var prefix = root.Label.Length == 0 ? "" : root.Label + "/";
            foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures(root.Path, includeSkipped: true))
            {
                var source = fixture.IsMultiFile
                    ? MultiFileSource(fixture.SpyFilePath)
                    : Normalize(File.ReadAllText(fixture.SpyFilePath));
                units.Add(new Unit(prefix + fixture.TestName, source, fixture.Features));
            }
        }

        Assert.NotEmpty(units);
        return units;
    }

    private static string MultiFileSource(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*.spy", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(directory, f))
            .Where(rel => !CrashBundleWriter.IsNonSourceSegment(rel))
            .Select(rel => rel.Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(rel => rel, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (var rel in files)
        {
            sb.Append("### ").Append(rel).Append('\n');
            sb.Append(Normalize(File.ReadAllText(Path.Combine(directory, rel)))).Append('\n');
        }

        return sb.ToString();
    }

    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0)
            lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return string.Join("\n", lines);
    }

    private static List<List<string>> DuplicateGroups(IEnumerable<Unit> units, bool withFeatures)
        => units
            .GroupBy(u => Key(u, withFeatures), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(u => u.Name).OrderBy(n => n, StringComparer.Ordinal).ToList())
            .OrderBy(g => g[0], StringComparer.Ordinal)
            .ToList();

    private static string Key(Unit unit, bool withFeatures)
    {
        var text = unit.Source;
        if (withFeatures)
        {
            text += "\n### .features\n"
                + string.Join("\n", unit.Features.OrderBy(f => f, StringComparer.Ordinal));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

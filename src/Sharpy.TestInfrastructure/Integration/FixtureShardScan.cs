using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// Reads the rows of every concrete shard class of a fixture corpus the way xUnit does — through
/// each class's own <c>GetTestFixtures</c> member — for the totality tests (#2179). The classes are
/// found by reflection rather than listed, so a shard class added or dropped is seen; the expected
/// count is the test's own literal.
/// </summary>
public static class FixtureShardScan
{
    public sealed record Shard(Type Type, IReadOnlyList<string> TestNames);

    public static IReadOnlyList<Shard> ReadShards(Type shardBase)
        => shardBase.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && shardBase.IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => new Shard(t, ReadRows(t)))
            .ToList();

    private static IReadOnlyList<string> ReadRows(Type shardClass)
    {
        var member = shardClass.GetMethod("GetTestFixtures", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException($"{shardClass.FullName} declares no public static GetTestFixtures()");
        var rows = (IEnumerable<object[]>)member.Invoke(null, null)!;
        return rows.Select(r => (string)r[0]).ToList();
    }

    /// <summary>
    /// How xUnit will RUN each shard, which <see cref="ReadShards"/> (the rows) cannot see: every
    /// concrete shard class must declare <paramref name="testMethodName"/> with exactly one
    /// <c>[Theory]</c> whose <c>Skip</c> is empty, exactly one <c>[MemberData]</c> naming that class's
    /// own <c>GetTestFixtures</c>, and no trait — on the method or the class — that the shard base
    /// does not carry. A <c>Skip</c> collapses a shard's rows to one skipped row; a
    /// <c>[Trait("Category", …)]</c> lets a CI filter (the main filter excludes GapDiscovery and
    /// Benchmark) drop one shard; either leaves the row totality green (#2179).
    /// </summary>
    /// <returns>The number of shard classes checked and the violations (empty = clean).</returns>
    public static (int Checked, IReadOnlyList<string> Violations) ShardMethodViolations(Type shardBase, string testMethodName)
    {
        var baseTraits = TraitsOf(shardBase);
        var violations = new List<string>();
        var shards = shardBase.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && shardBase.IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var shard in shards)
        {
            var method = shard.GetMethod(testMethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method == null)
            {
                violations.Add($"{shard.Name}: declares no public {testMethodName}");
                continue;
            }

            var facts = method.GetCustomAttributes<FactAttribute>(inherit: true).ToList();
            if (facts.Count != 1 || facts[0] is not TheoryAttribute)
                violations.Add($"{shard.Name}.{testMethodName}: expected exactly one [Theory], found {string.Join(", ", facts.Select(f => f.GetType().Name))}");
            foreach (var fact in facts.Where(f => !string.IsNullOrEmpty(f.Skip)))
                violations.Add($"{shard.Name}.{testMethodName}: Skip = \"{fact.Skip}\" (its rows would not run)");

            var memberData = method.GetCustomAttributes<MemberDataAttribute>(inherit: true).ToList();
            if (memberData.Count != 1
                || memberData[0].MemberName != "GetTestFixtures"
                || (memberData[0].MemberType != null && memberData[0].MemberType != shard))
            {
                violations.Add($"{shard.Name}.{testMethodName}: expected one [MemberData] naming its own GetTestFixtures, found "
                    + string.Join(", ", memberData.Select(m => $"{m.MemberType?.Name ?? shard.Name}.{m.MemberName}")));
            }

            var extra = TraitsOf(method).Concat(TraitsOf(shard))
                .Where(t => !baseTraits.Contains(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (extra.Count > 0)
                violations.Add($"{shard.Name}: trait(s) the shard base does not carry: {string.Join(", ", extra)}");
        }

        return (shards.Count, violations);
    }

    /// <summary>
    /// Every trait-bearing attribute (an <see cref="ITraitAttribute"/>, e.g. <c>[Trait]</c>) declared
    /// on a method, or on a type and each of its base types, spelled with its constructor arguments.
    /// </summary>
    private static HashSet<string> TraitsOf(MemberInfo member)
    {
        var declared = new List<CustomAttributeData>();
        if (member is Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                declared.AddRange(CustomAttributeData.GetCustomAttributes(t));
        }
        else
        {
            declared.AddRange(CustomAttributeData.GetCustomAttributes(member));
        }

        return declared
            .Where(d => typeof(ITraitAttribute).IsAssignableFrom(d.AttributeType))
            .Select(d => $"{d.AttributeType.Name}({string.Join(", ", d.ConstructorArguments.Select(c => c.Value))})")
            .ToHashSet(StringComparer.Ordinal);
    }
}

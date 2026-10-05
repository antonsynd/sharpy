using System.Reflection;

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
}

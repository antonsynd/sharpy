using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// The Cli twin of <c>StaticStateConformanceTests</c> (#1036, #2250): the compile server serves many
/// requests from one process, so a mutable static in the Cli assembly is state one request can leave
/// for the next. Every static field of <c>sharpyc</c> must be <c>readonly</c>/<c>const</c>, and a
/// <c>static readonly</c> mutable collection or cache (<c>List</c>, <c>Dictionary</c>, <c>HashSet</c>,
/// <c>ConcurrentDictionary</c>, <c>Lazy</c>, <c>ConditionalWeakTable</c>) is equally state — unless it
/// is a <see cref="Roster"/> row whose justification says why it cannot carry one request's answer
/// into another. A stale row (its field no longer flagged) is red.
/// </summary>
public class CliStaticStateConformanceTests
{
    /// <summary>(declaring type full name, field name) → why this static cannot leak across requests.</summary>
    private static readonly IReadOnlyDictionary<(string Type, string Field), string> Roster =
        new Dictionary<(string, string), string>
        {
            [("Sharpy.Cli.CliHelpers", "ShowDiagnosticProvenance")] =
                "a rendering switch set from --verbose by every command handler from its own parse result " +
                "before any rendering, so each command (and each compile-server request, which runs under " +
                "the server's _compileLock) writes it before it reads it; it never carries a compile's answer (#2250)",
        };

    [Fact]
    public void CliAssembly_HasNoUnrosteredMutableStaticState()
    {
        var violations = FlaggedStatics(typeof(CliHelpers).Assembly)
            .Where(f => !Roster.ContainsKey(f.Key))
            .Select(f => f.Description)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0,
            "Unrostered mutable static state in the Cli assembly (a compile-server request can read what an " +
            "earlier request left). Return the value instead, or add a Roster row with the reason it cannot " +
            "leak across requests:\n  " + string.Join("\n  ", violations));
    }

    [Fact]
    public void Roster_HasNoStaleRows()
    {
        var flagged = FlaggedStatics(typeof(CliHelpers).Assembly).Select(f => f.Key).ToHashSet();
        var stale = Roster.Keys.Where(k => !flagged.Contains(k))
            .Select(k => $"{k.Type}.{k.Field}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0, "Stale Roster rows (field no longer flagged) — delete them:\n  " +
            string.Join("\n  ", stale));
    }

    /// <summary>Positive control: the scan flags a mutable static and a static cache in a known type.</summary>
    [Fact]
    public void Scan_FlagsAMutableStaticAndAStaticCache_PositiveControl()
    {
        var flagged = FlaggedStatics(typeof(CliStaticStateConformanceTests).Assembly)
            .Select(f => f.Key)
            .ToHashSet();

        Assert.Contains((typeof(PositiveControl).FullName!, nameof(PositiveControl.Counter)), flagged);
        Assert.Contains((typeof(PositiveControl).FullName!, nameof(PositiveControl.Cache)), flagged);
        Assert.DoesNotContain((typeof(PositiveControl).FullName!, nameof(PositiveControl.Constant)), flagged);
    }

    private static class PositiveControl
    {
        public static int Counter = 1; // the field IS the probe: a non-readonly static the scan must flag
        public static readonly ConcurrentDictionary<string, int> Cache = new();
        public static readonly string Constant = "init-once";
    }

    private static IEnumerable<((string Type, string Field) Key, string Description)> FlaggedStatics(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (IsCompilerGenerated(type))
                continue;

            foreach (var field in type.GetFields(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (IsCompilerGenerated(field) || field.IsLiteral)
                    continue;

                var key = (type.FullName ?? type.Name, field.Name);
                if (!field.IsInitOnly)
                    yield return (key, $"[not readonly] {key.Item1}.{field.Name} : {field.FieldType.Name}");
                else if (IsMutableCollectionOrCache(field.FieldType))
                    yield return (key, $"[mutable static collection/cache] {key.Item1}.{field.Name} : {field.FieldType.Name}");
            }
        }
    }

    private static bool IsMutableCollectionOrCache(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var def = type.GetGenericTypeDefinition();
        return def == typeof(List<>)
            || def == typeof(Dictionary<,>)
            || def == typeof(HashSet<>)
            || def == typeof(ConcurrentDictionary<,>)
            || def == typeof(Lazy<>)
            || def == typeof(ConditionalWeakTable<,>);
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>() != null
        || member.Name.IndexOf('<') >= 0;
}

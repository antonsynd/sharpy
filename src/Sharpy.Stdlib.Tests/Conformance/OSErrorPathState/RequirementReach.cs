using System.Reflection;
using Microsoft.CodeAnalysis;
using Sharpy;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// R2's reach (plan-638e8b C2 axis 4): every reference to a <see cref="PathErrors"/> door outside
/// <c>PathErrors.cs</c>, its requirement read from the door's <see cref="PathRequirementAttribute"/> stamp
/// by reflection, propagated backwards over G1's bound call graph (<see cref="DoorScanResult.Edges"/>) to
/// every member that reaches it — Core first, then the Stdlib, where a reference to a Core member carries
/// the requirements the Core pass found that member reaching. A <see cref="PathErrors"/> door reaches its
/// own stamp (its probes declare it). Like axis 1's reach, an over-approximation on purpose: a member
/// that dispatches on a mode literal (<c>open</c>) reaches every requirement of every arm.
/// </summary>
internal static class RequirementReach
{
    private const string Helper = "Sharpy.PathErrors";

    /// <summary>The stamp of every public <see cref="PathErrors"/> door, by name (the names are unique).</summary>
    public static IReadOnlyDictionary<string, PathRequirement> Stamps()
    {
        var stamps = new Dictionary<string, PathRequirement>(StringComparer.Ordinal);
        foreach (var door in typeof(PathErrors).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var stamp = door.GetCustomAttribute<PathRequirementAttribute>(inherit: false)
                        ?? throw new InvalidOperationException($"{door} has no requirement stamp (R0's business)");
            if (!stamps.TryAdd(door.Name, stamp.Requirement))
                throw new InvalidOperationException($"PathErrors.{door.Name} is overloaded; R2 keys its doors by name");
        }
        return stamps;
    }

    /// <summary>Member key → each requirement it reaches → one witness chain ending at the stamped door.</summary>
    public static SortedDictionary<string, SortedDictionary<PathRequirement, string>> Of(DoorCensus census)
    {
        var stamps = Stamps();
        var all = new SortedDictionary<string, SortedDictionary<PathRequirement, string>>(StringComparer.Ordinal);

        // Core: a source edge into a PathErrors door, from a member outside PathErrors.
        var core = Propagate(census.Core, stamps, (owner, target) =>
            IsDoor(target, stamps, out var r) && !IsHelper(owner.ContainingType)
                ? new[] { (r, DoorScan.MemberKey(target)) }
                : Array.Empty<(PathRequirement, string)>(),
            _ => Array.Empty<(PathRequirement, string)>());
        foreach (var (k, v) in core)
            all[k] = v;

        // Stdlib: an external reference into a PathErrors door, or into a Core member that reaches one.
        var stdlib = Propagate(census.Stdlib, stamps, (_, _) => Array.Empty<(PathRequirement, string)>(), reference =>
        {
            var key = DoorScan.MemberKey(reference.Member);
            if (IsDoor(reference.Member, stamps, out var r))
                return new[] { (r, $"{key} at {reference.Site}") };
            return core.TryGetValue(key, out var set)
                ? set.Select(kv => (kv.Key, $"{key} at {reference.Site} → {kv.Value}")).ToArray()
                : Array.Empty<(PathRequirement, string)>();
        });
        foreach (var (k, v) in stdlib)
            all[k] = v;
        return all;
    }

    private static bool IsHelper(INamedTypeSymbol? type) => type != null && DoorScan.TypeKey(type) == Helper;

    private static bool IsDoor(ISymbol member, IReadOnlyDictionary<string, PathRequirement> stamps, out PathRequirement requirement)
    {
        requirement = default;
        return member is IMethodSymbol && IsHelper(member.ContainingType) && member.DeclaredAccessibility == Accessibility.Public
               && stamps.TryGetValue(member.Name, out requirement);
    }

    private static Dictionary<string, SortedDictionary<PathRequirement, string>> Propagate(DoorScanResult pass,
        IReadOnlyDictionary<string, PathRequirement> stamps,
        Func<ISymbol, ISymbol, IEnumerable<(PathRequirement, string)>> sourceSeed,
        Func<ExternalReference, IEnumerable<(PathRequirement, string)>> externalSeed)
    {
        var reached = new Dictionary<ISymbol, SortedDictionary<PathRequirement, string>>(SymbolEqualityComparer.Default);
        var queue = new Queue<(ISymbol, PathRequirement)>();
        void Mark(ISymbol member, PathRequirement r, string witness)
        {
            if (!reached.TryGetValue(member, out var set))
                reached[member] = set = new SortedDictionary<PathRequirement, string>();
            if (set.TryAdd(r, witness))
                queue.Enqueue((member, r));
        }

        var reverse = new Dictionary<ISymbol, System.Collections.Generic.List<ISymbol>>(SymbolEqualityComparer.Default);
        foreach (var (owner, targets) in pass.Edges)
        {
            foreach (var target in targets)
            {
                if (!reverse.TryGetValue(target, out var callers))
                    reverse[target] = callers = new System.Collections.Generic.List<ISymbol>();
                callers.Add(owner);
                foreach (var (r, witness) in sourceSeed(owner, target))
                    Mark(owner, r, witness);
            }
        }
        foreach (var reference in pass.External)
        {
            foreach (var (r, witness) in externalSeed(reference))
                Mark(reference.Owner, r, witness);
        }
        // A door reaches its own stamp (its probes declare it).
        foreach (var owner in pass.Edges.Keys.Concat(pass.External.Select(e => e.Owner)))
        {
            if (IsDoor(owner, stamps, out var own))
                Mark(owner, own, "its own stamp");
        }

        while (queue.Count > 0)
        {
            var (member, r) = queue.Dequeue();
            if (IsHelper(member.ContainingType))
                continue; // references inside PathErrors.cs are the helper's own
            if (!reverse.TryGetValue(member, out var callers))
                continue;
            foreach (var caller in callers)
            {
                if (!IsHelper(caller.ContainingType))
                    Mark(caller, r, DoorScan.MemberKey(member) + " → " + reached[member][r]);
            }
        }
        return reached.ToDictionary(kv => DoorScan.MemberKey(kv.Key), kv => kv.Value, StringComparer.Ordinal);
    }
}

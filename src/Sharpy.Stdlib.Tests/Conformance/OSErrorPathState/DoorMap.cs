namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>A type row of Map 1: <c>door</c>, or <c>not a door: &lt;reason&gt;</c>.</summary>
internal sealed record TypeRow(bool Door, string Reason);

/// <summary>
/// A member row of a door type: the path parameters it takes (a door), or
/// <c>takes no path: &lt;reason&gt;</c>.
/// </summary>
internal sealed record MemberRow(string[] PathParameters, string? NoPathReason)
{
    public bool IsDoor => PathParameters.Length > 0;
}

/// <summary>Map 1 (plan-638e8b C2 axis 1): external type → door?, with no default arm.</summary>
internal sealed class DoorMap
{
    public Dictionary<string, string> Namespaces { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, TypeRow> Types { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, MemberRow> Members { get; } = new(StringComparer.Ordinal);

    public DoorMap Namespace(string ns, string reason)
    {
        Namespaces.Add(ns, reason);
        return this;
    }

    public DoorMap NotADoor(string type, string reason)
    {
        Types.Add(type, new TypeRow(false, reason));
        return this;
    }

    public DoorMap Door(string type, string reason)
    {
        Types.Add(type, new TypeRow(true, reason));
        return this;
    }

    public DoorMap Path(string member, params string[] pathParameters)
    {
        if (pathParameters.Length == 0)
            throw new ArgumentException("a door member names its path parameters", nameof(pathParameters));
        Members.Add(member, new MemberRow(pathParameters, null));
        return this;
    }

    public DoorMap NoPath(string member, string reason)
    {
        Members.Add(member, new MemberRow(Array.Empty<string>(), reason));
        return this;
    }

    public DoorMap Clone()
    {
        var copy = new DoorMap();
        foreach (var (k, v) in Namespaces)
            copy.Namespaces.Add(k, v);
        foreach (var (k, v) in Types)
            copy.Types.Add(k, v);
        foreach (var (k, v) in Members)
            copy.Members.Add(k, v);
        return copy;
    }
}

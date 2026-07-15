using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal sealed record GenContext
{
    public int Fuel { get; init; } = 5;
    public ImmutableArray<string> InScopeNames { get; init; } = ImmutableArray<string>.Empty;
    public int Depth { get; init; }
    public bool AllowAsync { get; init; }
    public bool AllowYield { get; init; }
    public bool InLoop { get; init; }
    public bool InFunction { get; init; }
    public bool InClass { get; init; }

    /// <summary>
    /// When true, the expression generator may emit the experimental <c>as!</c>/<c>as?</c>
    /// failable-cast forms (#1029) in addition to the legacy <c>to</c>/<c>to?</c> forms. Off by
    /// default so ungated round-trip/compile property suites never generate gated syntax
    /// (which would trip SPY0331 in any compile-based property test).
    /// </summary>
    public bool AllowFailableCast { get; init; }

    public GenContext Burn(int amount = 1) =>
        this with { Fuel = Math.Max(0, Fuel - amount), Depth = Depth + 1 };

    public GenContext WithName(string name) =>
        this with { InScopeNames = InScopeNames.Add(name) };

    public GenContext WithNames(IEnumerable<string> names) =>
        this with { InScopeNames = InScopeNames.AddRange(names) };

    public bool HasFuel => Fuel > 0;

    public static GenContext Default { get; } = new()
    {
        Fuel = 5,
        InScopeNames = ImmutableArray.Create("x", "y", "z", "n", "s", "items", "result")
    };

    public static GenContext HighFuel { get; } = new()
    {
        Fuel = 10,
        InScopeNames = ImmutableArray.Create(
            "x", "y", "z", "n", "s", "items", "result",
            "a", "b", "c", "data", "value", "tmp", "acc"),
        InFunction = true,
        AllowAsync = true
    };

    public static GenContext DeepNesting { get; } = new()
    {
        Fuel = 12,
        Depth = 0,
        InScopeNames = ImmutableArray.Create(
            "x", "y", "z", "n", "s", "items", "result"),
        InFunction = true,
        InLoop = true
    };
}

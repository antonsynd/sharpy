using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal sealed record TypeEnv
{
    public ImmutableDictionary<string, string> Bindings { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    public TypeEnv WithBinding(string name, string type) =>
        this with { Bindings = Bindings.SetItem(name, type) };

    public string? Lookup(string name) =>
        Bindings.TryGetValue(name, out var t) ? t : null;

    public IReadOnlyList<string> VarsOfType(string type) =>
        Bindings.Where(kv => kv.Value == type).Select(kv => kv.Key).ToList();

    public static TypeEnv Default { get; } = new TypeEnv()
        .WithBinding("x", "int")
        .WithBinding("y", "int")
        .WithBinding("s", "str")
        .WithBinding("flag", "bool");
}

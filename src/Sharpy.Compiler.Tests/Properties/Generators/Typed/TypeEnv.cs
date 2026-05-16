using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal sealed record FunctionSignature(
    ImmutableArray<(string Name, string Type)> Parameters,
    string ReturnType);

internal sealed record ClassInfo(
    string? BaseClass,
    ImmutableArray<(string Name, string Type)> Fields,
    ImmutableArray<(string Name, FunctionSignature Signature)> Methods);

internal sealed record TypeEnv
{
    public ImmutableDictionary<string, string> Bindings { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    public ImmutableDictionary<string, FunctionSignature> Functions { get; init; } =
        ImmutableDictionary<string, FunctionSignature>.Empty;

    public ImmutableDictionary<string, ClassInfo> Classes { get; init; } =
        ImmutableDictionary<string, ClassInfo>.Empty;

    public TypeEnv WithBinding(string name, string type) =>
        this with { Bindings = Bindings.SetItem(name, type) };

    public string? Lookup(string name) =>
        Bindings.TryGetValue(name, out var t) ? t : null;

    public IReadOnlyList<string> VarsOfType(string type) =>
        Bindings.Where(kv => kv.Value == type).Select(kv => kv.Key).ToList();

    public TypeEnv WithFunction(string name, FunctionSignature sig) =>
        this with { Functions = Functions.SetItem(name, sig) };

    public IReadOnlyList<string> FunctionsReturning(string type) =>
        Functions.Where(kv => kv.Value.ReturnType == type).Select(kv => kv.Key).ToList();

    public TypeEnv WithClass(string name, ClassInfo info) =>
        this with { Classes = Classes.SetItem(name, info) };

    public TypeEnv WithGenericClass(string name, ImmutableArray<string> typeParams,
        ImmutableArray<(string Name, string Type)> fields) =>
        this with
        {
            Classes = Classes.SetItem(name, new ClassInfo(null, fields,
            ImmutableArray<(string Name, FunctionSignature Signature)>.Empty))
        };

    public static TypeEnv Default { get; } = new TypeEnv()
        .WithBinding("x", "int")
        .WithBinding("y", "int")
        .WithBinding("s", "str")
        .WithBinding("flag", "bool");

    public static TypeEnv WithCollections { get; } = Default
        .WithBinding("nums", "list[int]")
        .WithBinding("words", "list[str]");

    public static TypeEnv WithOptionals { get; } = Default
        .WithBinding("maybe_n", "int?")
        .WithBinding("maybe_s", "str?");

    public static TypeEnv WithFunctions { get; } = Default
        .WithFunction("add", new FunctionSignature(
            ImmutableArray.Create(("a", "int"), ("b", "int")), "int"))
        .WithFunction("greet", new FunctionSignature(
            ImmutableArray.Create(("name", "str")), "str"))
        .WithFunction("is_positive", new FunctionSignature(
            ImmutableArray.Create(("n", "int")), "bool"));

    public static TypeEnv WithInheritance { get; } = Default
        .WithClass("Animal", new ClassInfo(null,
            ImmutableArray.Create(("name", "str")),
            ImmutableArray.Create(("speak", new FunctionSignature(
                ImmutableArray<(string, string)>.Empty, "str")))))
        .WithClass("Dog", new ClassInfo("Animal",
            ImmutableArray.Create(("name", "str"), ("breed", "str")),
            ImmutableArray.Create(("speak", new FunctionSignature(
                ImmutableArray<(string, string)>.Empty, "str")))))
        .WithClass("Cat", new ClassInfo("Animal",
            ImmutableArray.Create(("name", "str")),
            ImmutableArray.Create(("speak", new FunctionSignature(
                ImmutableArray<(string, string)>.Empty, "str")))));

    public static TypeEnv WithGenerics { get; } = Default
        .WithGenericClass("Box", ImmutableArray.Create("T"),
            ImmutableArray.Create(("value", "T")))
        .WithGenericClass("Pair", ImmutableArray.Create("A", "B"),
            ImmutableArray.Create(("first", "A"), ("second", "B")));
}

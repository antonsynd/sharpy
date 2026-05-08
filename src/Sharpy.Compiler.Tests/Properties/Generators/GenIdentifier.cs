using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenIdentifier
{
    private static readonly string[] Keywords =
    {
        "if", "else", "elif", "while", "for", "in", "def", "class", "return",
        "pass", "break", "continue", "import", "from", "as", "with", "try",
        "except", "finally", "raise", "and", "or", "not", "is", "True",
        "False", "None", "match", "case", "struct", "interface", "yield",
        "async", "await", "lambda", "assert", "del", "global", "nonlocal",
        "union", "enum", "event", "delegate", "property", "abstract",
        "virtual", "override", "static", "sealed", "readonly", "super"
    };

    private static readonly HashSet<string> KeywordSet = new(Keywords);

    private static readonly string[] SimpleNames =
    {
        "x", "y", "z", "a", "b", "c", "n", "m", "s", "t",
        "foo", "bar", "baz", "qux", "val", "tmp", "res",
        "items", "count", "name", "data", "value", "key",
        "idx", "acc", "elem", "head", "tail", "left", "right"
    };

    public static Gen<string> Name { get; } =
        Gen.OneOfConst(SimpleNames).Where(n => !KeywordSet.Contains(n));

    public static Gen<string> FreshName { get; } =
        Gen.Select(Gen.OneOfConst(SimpleNames), Gen.Int[0, 99],
            (name, suffix) => $"{name}_{suffix}");

    public static Gen<string> ClassName { get; } =
        Gen.OneOfConst("Foo", "Bar", "Baz", "Animal", "Shape", "Node", "Item", "Point");

    public static Gen<string> FunctionName { get; } =
        Gen.OneOfConst("compute", "process", "transform", "calculate", "get_value",
            "do_work", "make_thing", "run_task", "check_it", "apply_fn");

    public static Gen<string> NameFromContext(GenContext ctx) =>
        ctx.InScopeNames.IsEmpty
            ? Name
            : Gen.Frequency(
                (3, Gen.OneOfConst(ctx.InScopeNames.ToArray())),
                (1, Name));
}

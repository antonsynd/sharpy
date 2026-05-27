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

    private static readonly string[] BacktickContents =
    {
        "class", "for", "while", "if", "return", "import", "from",
        "foo bar", "my variable", "hello world",
        "αβγ", "δεζ", "кириллица", "日本語",
        "item_1", "x y z", "123abc", "a+b",
        "with spaces", "special!chars", "@property",
        "yield", "async", "await", "match", "case",
        "interface", "struct", "enum", "abstract"
    };

    public static Gen<string> BacktickContent { get; } =
        Gen.OneOfConst(BacktickContents);

    public static Gen<string> BacktickIdentifier { get; } =
        BacktickContent.Select(content => $"`{content}`");

    // Dotted backtick contents are valid (#713): they name fully-qualified
    // .NET types/namespaces, e.g. `System.IO`.
    private static readonly string[] BacktickWithDot =
    {
        "sys.path", "os.path", "foo.bar", "a.b.c"
    };

    public static Gen<string> BacktickContentWithDot { get; } =
        Gen.OneOfConst(BacktickWithDot);

    private static readonly string[] InvalidBacktickWithNewline =
    {
        "foo\nbar", "hello\r\nworld", "line\nbreak"
    };

    public static Gen<string> BacktickContentWithNewline { get; } =
        Gen.OneOfConst(InvalidBacktickWithNewline);

    // --- Name mangling generators ---

    private static Gen<string> LowercaseSegment { get; } =
        Gen.Int[1, 6].SelectMany(len =>
            Gen.Char['a', 'z'].Array[len, len].Select(cs => new string(cs)));

    private static Gen<string> LowercaseSegmentWithDigits { get; } =
        Gen.Select(LowercaseSegment, Gen.OneOf(
            Gen.Const(""),
            Gen.Int[0, 99].Select(d => d.ToString())),
            (seg, digits) => seg + digits);

    /// <summary>
    /// Generate snake_case identifiers: 1-4 lowercase segments joined by underscores.
    /// Each segment is 1-6 lowercase alpha chars optionally followed by digits.
    /// e.g., "foo", "foo_bar", "get_item_count", "x_1"
    /// </summary>
    public static Gen<string> SnakeCaseIdentifier { get; } =
        Gen.Int[1, 4].SelectMany(count =>
            LowercaseSegmentWithDigits.Array[count, count]
                .Select(parts => string.Join("_", parts)))
        .Where(n => !KeywordSet.Contains(n));

    private static Gen<string> UppercaseSegment { get; } =
        Gen.Int[1, 6].SelectMany(len =>
            Gen.Char['A', 'Z'].Array[len, len].Select(cs => new string(cs)));

    private static Gen<string> UppercaseSegmentWithDigits { get; } =
        Gen.Select(UppercaseSegment, Gen.OneOf(
            Gen.Const(""),
            Gen.Int[0, 99].Select(d => d.ToString())),
            (seg, digits) => seg + digits);

    /// <summary>
    /// Generate SCREAMING_SNAKE_CASE identifiers: 1-4 UPPERCASE segments joined by underscores.
    /// e.g., "MAX", "MAX_SIZE", "HTTP_STATUS_CODE"
    /// </summary>
    public static Gen<string> ScreamingSnakeCaseIdentifier { get; } =
        Gen.Int[1, 4].SelectMany(count =>
            UppercaseSegmentWithDigits.Array[count, count]
                .Select(parts => string.Join("_", parts)))
        .Where(n => !KeywordSet.Contains(n));

    /// <summary>
    /// Generate mixed-form identifiers covering multiple naming conventions:
    /// snake_case, PascalCase, SCREAMING_SNAKE_CASE, single-word, _prefixed, __prefixed.
    /// </summary>
    public static Gen<string> MixedFormIdentifier { get; } =
        Gen.OneOf(
            SnakeCaseIdentifier,
            // PascalCase: capitalize first char of each snake_case segment, join without underscores
            Gen.Int[1, 3].SelectMany(count =>
                LowercaseSegment.Array[count, count]
                    .Select(parts => string.Join("", parts.Select(p =>
                        char.ToUpperInvariant(p[0]) + p[1..])))),
            ScreamingSnakeCaseIdentifier,
            // Single lowercase word
            LowercaseSegment,
            // _prefixed snake_case
            SnakeCaseIdentifier.Select(n => "_" + n),
            // __prefixed snake_case
            SnakeCaseIdentifier.Select(n => "__" + n))
        .Where(n => !KeywordSet.Contains(n));
}

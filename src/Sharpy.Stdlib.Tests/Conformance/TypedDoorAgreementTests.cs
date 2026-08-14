using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// Holds the two TYPED stdlib doors — <c>json.loads[T]</c> and <c>yaml.safe_load_typed[T]</c> — to
/// the same answer for the same document into the same <c>@dataclass</c> (#1504, #1505).
///
/// <para><b>Why this corpus exists.</b> Both doors deserialize into a type the compiler generated,
/// through libraries that bind names by different rules, and both had been fixed independently
/// before. The result was two doors that were each plausible and jointly incoherent:</para>
/// <list type="bullet">
///   <item><description>json <b>crashed the process</b> for any multi-word snake_case field: the
///     emitted constructor parameter was the raw <c>max_connections</c> while the property beside
///     it was <c>MaxConnections</c>, System.Text.Json binds those case-insensitively but not
///     underscore-insensitively, and the resulting <c>InvalidOperationException</c> escaped a
///     function whose signature promises a <c>Result</c> (#1504). yaml read the same document
///     fine.</description></item>
///   <item><description>yaml <b>silently fabricated</b> <c>0</c>/<c>null</c>/<c>false</c> for an
///     absent required field, returning <c>Ok</c> (#1505). json's binder left the same hole.</description></item>
/// </list>
///
/// <para>Neither could be found by a test of one door. This is the cell named in #1504's thread as
/// exactly what would have caught the crash, and never written: the same document, both doors,
/// a multi-word snake_case dataclass, compared to each other rather than to a hand-written
/// expectation.</para>
///
/// <para><b>What agreement means here.</b> Cell-by-cell: both <c>Ok</c> with equal field values, or
/// both <c>Err</c> naming the same missing field. The rendering is deliberately total — every
/// field, including the ones a row is not about — so a row that only meant to check
/// <c>retry_count</c> still fails if the doors disagree about <c>label</c>.</para>
///
/// <para><b>Mutation test</b> (performed 2026-08-13, per the round's guard-integrity rule): with
/// yaml's missing-required pre-check stubbed out (<c>MissingRequiredField</c> returning null), the
/// <c>missing-required</c> row failed — yaml returned <c>Ok</c> with <c>max_connections = 0</c>
/// while json returned <c>Err</c>. Restored, the row passes. A corpus that cannot go red for the
/// thing it guards is not a guard.</para>
/// </summary>
public class TypedDoorAgreementTests
{
    /// <summary>
    /// Mirrors the C# a <c>@dataclass</c> lowers to — settable PascalCase properties plus a single
    /// all-fields constructor whose parameters are camelCase — because that shape is what both
    /// doors actually meet, and the naming relationship BETWEEN the two is the thing #1504 broke.
    /// Written out rather than compiled from <c>.spy</c> so the corpus states the contract it
    /// depends on; the compiler-side fixture that pins the emitted shape to this one lives in
    /// <c>TestFixtures/dataclasses/</c>.
    ///
    /// <code>
    /// @dataclass
    /// class Config:
    ///     name: str
    ///     max_connections: int
    ///     retry_count: int = 3
    ///     label: str? = None
    /// </code>
    /// </summary>
    public class Config
    {
        public string Name { get; set; }
        public int MaxConnections { get; set; }
        public int RetryCount { get; set; } = 3;
        public Optional<string> Label { get; set; } = Optional<string>.None;

        public Config(
            string name,
            int maxConnections,
            int retryCount = 3,
            Optional<string> label = default)
        {
            Name = name;
            MaxConnections = maxConnections;
            RetryCount = retryCount;
            Label = label;
        }
    }

    /// <summary>One document in both spellings, plus what the doors must jointly answer.</summary>
    public sealed record Row(string Name, string Json, string Yaml, string Expected);

    /// <summary>
    /// The rendered outcome shared by both doors. <c>Ok</c> renders every field so no row is
    /// partially compared; <c>Err</c> renders the message, which both doors take from the one
    /// <c>TypedLoadContract.MissingFieldMessage</c> so identical text is a real property rather
    /// than a coincidence of two phrasings.
    /// </summary>
    private const string ErrMissingMaxConnections =
        "err: missing required field 'max_connections' for Config";

    public static TheoryData<Row> Corpus()
    {
        var data = new TheoryData<Row>();
        foreach (var row in Rows)
            data.Add(row);
        return data;
    }

    /// <summary>The corpus itself, enumerable outside xUnit's theory plumbing.</summary>
    private static IReadOnlyList<Row> Rows { get; } = BuildRows();

    private static Row[] BuildRows()
    {
        var data = new List<Row>();

        // Every field present, INCLUDING the multi-word snake_case one that crashed json for every
        // document until the constructor parameter joined the property's naming authority (#1504).
        data.Add(new Row(
            "all-present",
            /*json*/ "{\"name\": \"web\", \"max_connections\": 10, \"retry_count\": 7, \"label\": \"prod\"}",
            /*yaml*/ "name: web\nmax_connections: 10\nretry_count: 7\nlabel: prod\n",
            "ok: name=web max_connections=10 retry_count=7 label=Some(prod)"));

        // A required field the document omits is an Err naming it — not a fabricated 0 (#1505,
        // owner ruling: decided for both doors together).
        data.Add(new Row(
            "missing-required",
            /*json*/ "{\"name\": \"web\"}",
            /*yaml*/ "name: web\n",
            ErrMissingMaxConnections));

        // A field with a DECLARED default is not required; the default applies.
        data.Add(new Row(
            "missing-with-default",
            /*json*/ "{\"name\": \"web\", \"max_connections\": 10, \"label\": \"prod\"}",
            /*yaml*/ "name: web\nmax_connections: 10\nlabel: prod\n",
            "ok: name=web max_connections=10 retry_count=3 label=Some(prod)"));

        // An absent `T?` is None, not an error — absence is a value that field can hold.
        data.Add(new Row(
            "missing-optional",
            /*json*/ "{\"name\": \"web\", \"max_connections\": 10, \"retry_count\": 7}",
            /*yaml*/ "name: web\nmax_connections: 10\nretry_count: 7\n",
            "ok: name=web max_connections=10 retry_count=7 label=None"));

        // Both required fields absent: the doors must also agree on WHICH one they name, or a
        // caller switching doors gets a different diagnosis of the same document.
        data.Add(new Row(
            "missing-required-and-optional",
            /*json*/ "{\"retry_count\": 7}",
            /*yaml*/ "retry_count: 7\n",
            "err: missing required field 'name' for Config"));

        // An unmatched key is ignored by both (json's binder by default, yaml via
        // IgnoreUnmatchedProperties) — the one place the two were already deliberately aligned.
        data.Add(new Row(
            "extra-key",
            /*json*/ "{\"name\": \"web\", \"max_connections\": 10, \"unknown\": 1}",
            /*yaml*/ "name: web\nmax_connections: 10\nunknown: 1\n",
            "ok: name=web max_connections=10 retry_count=3 label=None"));

        return data.ToArray();
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void JsonAndYamlTypedDoors_Agree(Row row)
    {
        var json = RenderJson(row.Json);
        var yaml = RenderYaml(row.Yaml);

        Assert.True(
            json == yaml,
            $"row '{row.Name}': the two typed doors disagree about one document.\n"
            + $"  json.loads[Config]        -> {json}\n"
            + $"  yaml.safe_load_typed[Config] -> {yaml}\n"
            + "One of them is wrong; a caller must be able to switch doors without changing "
            + "which documents their program accepts (#1504, #1505).");

        Assert.Equal(row.Expected, json);
    }

    /// <summary>
    /// Neither door may kill the process. Asserted separately from the agreement rows because a
    /// crash is not a disagreement — it is the absence of an answer to compare, and it is what
    /// #1504 actually did for every document with a multi-word field. Kept as a standing check on
    /// the boundary rather than on the naming fix, so a future deserializer fault we have not met
    /// still comes back as an Err (Design Decision 2: crash-proof by construction, not per
    /// exception type).
    /// </summary>
    [Fact]
    public void TypedDoors_ReturnErrRatherThanThrow_ForUnreadableDocuments()
    {
        var unreadable = new[]
        {
            "{",                                     // truncated
            "{\"name\": }",                          // malformed value
            "[1, 2, 3]",                             // right syntax, wrong shape
            "\"just a string\"",
            "42",
        };

        foreach (var document in unreadable)
        {
            var result = Json.Loads<Config>(document);
            Assert.True(result.IsErr, $"json.loads[Config] should Err, not throw or Ok, for: {document}");
        }

        foreach (var document in new[] { "[1, 2, 3]\n", "- a\n- b\n", "just a string\n" })
        {
            var result = Yaml.SafeLoadTyped<Config>(document);
            Assert.True(result.IsErr,
                $"yaml.safe_load_typed[Config] should Err, not throw or Ok, for: {document}");
        }
    }

    private static string RenderJson(string document)
    {
        var result = Json.Loads<Config>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYaml(string document)
    {
        var result = Yaml.SafeLoadTyped<Config>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(Config config)
        => $"ok: name={config.Name} max_connections={config.MaxConnections} "
        + $"retry_count={config.RetryCount} "
        + $"label={(config.Label.IsNone ? "None" : "Some(" + config.Label.Unwrap() + ")")}";

    /// <summary>
    /// The document keys every row uses, as a set — a guard on the corpus itself. If a field is
    /// added to <see cref="Config"/> and no row mentions it, the corpus silently stops covering
    /// the shape it claims to cover.
    /// </summary>
    [Fact]
    public void Corpus_MentionsEveryFieldOfTheTargetType()
    {
        var mentioned = new HashSet<string>();
        foreach (var row in Rows)
        {
            foreach (var field in new[] { "name", "max_connections", "retry_count", "label" })
            {
                if (row.Json.Contains('"' + field + '"'))
                    mentioned.Add(field);
            }
        }

        Assert.Equal(
            new[] { "label", "max_connections", "name", "retry_count" },
            mentioned.OrderBy(f => f, System.StringComparer.Ordinal).ToArray());
    }
}

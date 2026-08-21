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

    /// <summary>
    /// Nested dataclass for the #1513 recursive walk rows.
    /// <code>
    /// @dataclass
    /// class Inner:
    ///     port: int
    ///     host: str = "localhost"
    /// </code>
    /// </summary>
    public class Inner
    {
        public int Port { get; set; }
        public string Host { get; set; } = "localhost";

        public Inner(int port, string host = "localhost")
        {
            Port = port;
            Host = host;
        }
    }

    /// <summary>
    /// Outer dataclass referencing Inner, for the #1513 nested-missing-field rows.
    /// <code>
    /// @dataclass
    /// class Outer:
    ///     name: str
    ///     inner: Inner
    /// </code>
    /// </summary>
    public class Outer
    {
        public string Name { get; set; }
        public Inner Inner { get; set; }

        public Outer(string name, Inner inner)
        {
            Name = name;
            Inner = inner;
        }
    }

    /// <summary>
    /// Outer with optional nested, for testing absent-optional-nested → Ok.
    /// <code>
    /// @dataclass
    /// class OuterOptional:
    ///     name: str
    ///     inner: Inner? = None
    /// </code>
    /// </summary>
    public class OuterOptional
    {
        public string Name { get; set; }
        public Optional<Inner> Inner { get; set; } = Optional<Inner>.None;

        public OuterOptional(string name, Optional<Inner> inner = default)
        {
            Name = name;
            Inner = inner;
        }
    }

    /// <summary>
    /// Outer with a list of Inner, for testing items[i].field paths.
    /// <code>
    /// @dataclass
    /// class OuterList:
    ///     name: str
    ///     items: list[Inner]
    /// </code>
    /// </summary>
    public class OuterList
    {
        public string Name { get; set; }
        public List<Inner> Items { get; set; }

        public OuterList(string name, List<Inner> items)
        {
            Name = name;
            Items = items;
        }
    }

    /// <summary>
    /// Outer with a dict of Inner, for testing servers[key].field paths (#1513).
    /// <code>
    /// @dataclass
    /// class OuterDict:
    ///     name: str
    ///     servers: dict[str, Inner]
    /// </code>
    /// </summary>
    public class OuterDict
    {
        public string Name { get; set; }
        public Dict<string, Inner> Servers { get; set; }

        public OuterDict(string name, Dict<string, Inner> servers)
        {
            Name = name;
            Servers = servers;
        }
    }

    /// <summary>
    /// Three-level nesting for the a.b.c dotted-path rows (#1513).
    /// <code>
    /// @dataclass
    /// class Level3:
    ///     c: int
    /// @dataclass
    /// class Level2:
    ///     b: Level3
    /// @dataclass
    /// class Level1:
    ///     name: str
    ///     a: Level2
    /// </code>
    /// </summary>
    public class Level3
    {
        public int C { get; set; }

        public Level3(int c)
        {
            C = c;
        }
    }

    public class Level2
    {
        public Level3 B { get; set; }

        public Level2(Level3 b)
        {
            B = b;
        }
    }

    public class Level1
    {
        public string Name { get; set; }
        public Level2 A { get; set; }

        public Level1(string name, Level2 a)
        {
            Name = name;
            A = a;
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

    // ========================================================================
    // Nested required-field rows (#1513)
    // ========================================================================

    public static TheoryData<Row> NestedCorpus()
    {
        var data = new TheoryData<Row>();
        foreach (var row in NestedRows)
            data.Add(row);
        return data;
    }

    private static IReadOnlyList<Row> NestedRows { get; } = BuildNestedRows();

    private static Row[] BuildNestedRows()
    {
        var data = new List<Row>();

        // The #1513 repro: required field missing one level down.
        data.Add(new Row(
            "nested-missing-required",
            /*json*/ "{\"name\": \"web\", \"inner\": {\"host\": \"example.com\"}}",
            /*yaml*/ "name: web\ninner:\n  host: example.com\n",
            "err: missing required field 'inner.port' for Outer"));

        // Nested complete → Ok.
        data.Add(new Row(
            "nested-complete",
            /*json*/ "{\"name\": \"web\", \"inner\": {\"port\": 8080}}",
            /*yaml*/ "name: web\ninner:\n  port: 8080\n",
            "ok: name=web inner.port=8080 inner.host=localhost"));

        // Nested complete with all fields.
        data.Add(new Row(
            "nested-all-present",
            /*json*/ "{\"name\": \"web\", \"inner\": {\"port\": 8080, \"host\": \"db.local\"}}",
            /*yaml*/ "name: web\ninner:\n  port: 8080\n  host: db.local\n",
            "ok: name=web inner.port=8080 inner.host=db.local"));

        // Optional-typed nested object absent → Ok (absence is a value for T?).
        data.Add(new Row(
            "optional-nested-absent",
            /*json*/ "{\"name\": \"web\"}",
            /*yaml*/ "name: web\n",
            "ok-optional: name=web inner=None"));

        // Optional-typed nested object present but incomplete → Err.
        data.Add(new Row(
            "optional-nested-present-incomplete",
            /*json*/ "{\"name\": \"web\", \"inner\": {\"host\": \"db.local\"}}",
            /*yaml*/ "name: web\ninner:\n  host: db.local\n",
            "err: missing required field 'inner.port' for OuterOptional"));

        // Missing field inside a list element → items[index].field.
        data.Add(new Row(
            "list-element-missing-required",
            /*json*/ "{\"name\": \"web\", \"items\": [{\"port\": 80}, {\"host\": \"db\"}]}",
            /*yaml*/ "name: web\nitems:\n- port: 80\n- host: db\n",
            "err: missing required field 'items[1].port' for OuterList"));

        // List all complete → Ok.
        data.Add(new Row(
            "list-all-complete",
            /*json*/ "{\"name\": \"web\", \"items\": [{\"port\": 80}, {\"port\": 443}]}",
            /*yaml*/ "name: web\nitems:\n- port: 80\n- port: 443\n",
            "ok-list: name=web items=[80, 443]"));

        // Missing field inside a dict VALUE → servers[key].field, using the document's key text.
        // The dict's own keys (web, db) are document data, not Inner's fields — a walk that
        // confuses the two either misses this or falsely refuses dict-all-complete below.
        data.Add(new Row(
            "dict-value-missing-required",
            /*json*/ "{\"name\": \"web\", \"servers\": {\"web\": {\"host\": \"h\"}, \"db\": {\"port\": 443}}}",
            /*yaml*/ "name: web\nservers:\n  web:\n    host: h\n  db:\n    port: 443\n",
            "err: missing required field 'servers[web].port' for OuterDict"));

        // Dict with every value complete → Ok. The false-refusal control: Inner's field names
        // appear nowhere among the dict's keys, so a key-confused walk reports a phantom missing.
        data.Add(new Row(
            "dict-all-complete",
            /*json*/ "{\"name\": \"web\", \"servers\": {\"web\": {\"port\": 80}, \"db\": {\"port\": 443}}}",
            /*yaml*/ "name: web\nservers:\n  web:\n    port: 80\n  db:\n    port: 443\n",
            "ok-dict: name=web servers={db:443, web:80}"));

        // Two-level nesting: the dotted path grows one segment per level.
        data.Add(new Row(
            "two-level-missing-required",
            /*json*/ "{\"name\": \"web\", \"a\": {\"b\": {}}}",
            /*yaml*/ "name: web\na:\n  b: {}\n",
            "err: missing required field 'a.b.c' for Level1"));

        data.Add(new Row(
            "two-level-complete",
            /*json*/ "{\"name\": \"web\", \"a\": {\"b\": {\"c\": 7}}}",
            /*yaml*/ "name: web\na:\n  b:\n    c: 7\n",
            "ok-deep: name=web a.b.c=7"));

        return data.ToArray();
    }

    [Theory]
    [MemberData(nameof(NestedCorpus))]
    public void JsonAndYamlTypedDoors_AgreeOnNestedFields(Row row)
    {
        string json, yaml;

        if (row.Name.StartsWith("optional-nested"))
        {
            json = RenderJsonOptional(row.Json);
            yaml = RenderYamlOptional(row.Yaml);
        }
        else if (row.Name.StartsWith("list"))
        {
            json = RenderJsonList(row.Json);
            yaml = RenderYamlList(row.Yaml);
        }
        else if (row.Name.StartsWith("dict"))
        {
            json = RenderJsonDict(row.Json);
            yaml = RenderYamlDict(row.Yaml);
        }
        else if (row.Name.StartsWith("two-level"))
        {
            json = RenderJsonDeep(row.Json);
            yaml = RenderYamlDeep(row.Yaml);
        }
        else
        {
            json = RenderJsonOuter(row.Json);
            yaml = RenderYamlOuter(row.Yaml);
        }

        Assert.True(
            json == yaml,
            $"row '{row.Name}': the two typed doors disagree about a nested document (#1513).\n"
            + $"  json  -> {json}\n"
            + $"  yaml  -> {yaml}");

        Assert.Equal(row.Expected, json);
    }

    private static string RenderJsonOuter(string document)
    {
        var result = Json.Loads<Outer>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYamlOuter(string document)
    {
        var result = Yaml.SafeLoadTyped<Outer>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(Outer outer)
        => $"ok: name={outer.Name} inner.port={outer.Inner.Port} inner.host={outer.Inner.Host}";

    private static string RenderJsonOptional(string document)
    {
        var result = Json.Loads<OuterOptional>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYamlOptional(string document)
    {
        var result = Yaml.SafeLoadTyped<OuterOptional>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(OuterOptional outer)
        => outer.Inner.IsNone
            ? $"ok-optional: name={outer.Name} inner=None"
            : $"ok-optional: name={outer.Name} inner.port={outer.Inner.Unwrap().Port} inner.host={outer.Inner.Unwrap().Host}";

    private static string RenderJsonList(string document)
    {
        var result = Json.Loads<OuterList>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYamlList(string document)
    {
        var result = Yaml.SafeLoadTyped<OuterList>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(OuterList outer)
        => $"ok-list: name={outer.Name} items=[{string.Join(", ", outer.Items.Select(i => i.Port.ToString()))}]";

    private static string RenderJsonDict(string document)
    {
        var result = Json.Loads<OuterDict>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYamlDict(string document)
    {
        var result = Yaml.SafeLoadTyped<OuterDict>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(OuterDict outer)
        => $"ok-dict: name={outer.Name} servers="
        + "{"
        + string.Join(", ", outer.Servers.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value.Port}"))
        + "}";

    private static string RenderJsonDeep(string document)
    {
        var result = Json.Loads<Level1>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Msg;
    }

    private static string RenderYamlDeep(string document)
    {
        var result = Yaml.SafeLoadTyped<Level1>(document);
        return result.IsOk ? RenderOk(result.Unwrap()) : "err: " + result.UnwrapErr().Message;
    }

    private static string RenderOk(Level1 outer)
        => $"ok-deep: name={outer.Name} a.b.c={outer.A.B.C}";
}

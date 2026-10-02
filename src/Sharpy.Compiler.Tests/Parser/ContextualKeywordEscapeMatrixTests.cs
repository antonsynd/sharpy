using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Conformance;
using Xunit;
using Xunit.Abstractions;
using Oracle = Sharpy.Compiler.Tests.Conformance.ContextualKeywordEscapeOracle;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// P40 site matrix (#2166): <b>a backtick-escaped contextual keyword is an identifier at every
/// site.</b> Each row is one (keyword, site) cell, a template with <c>{K}</c> as the hole, parsed
/// three ways:
/// <list type="bullet">
/// <item><b>bare</b> — the positive control: the keyword construct the site builds is present
/// (<see cref="WildcardPattern"/>, a placeholder lambda, <see cref="PropertyAccessor"/>,
/// <see cref="EventAccessor"/>, <see cref="ObserverKind"/>, an exception filter, a
/// <see cref="ParameterModifier"/>, <see cref="ModifiedArgument"/>, covariance,
/// <see cref="NotnullConstraint"/>, <see cref="NewConstraint"/>);</item>
/// <item><b>renamed</b> — a fresh identifier in the hole (<see cref="Oracle.RenamedSpelling"/>):
/// the ground truth that the row's <see cref="Escaped"/> outcome is what an identifier yields
/// there — the construct is absent and either an unescaped name node binds the fresh name
/// (<see cref="Escaped.IdentifierBound"/>) or the program does not parse
/// (<see cref="Escaped.ParseError"/>);</item>
/// <item><b>escaped</b> — the construct is absent, the row's outcome holds with
/// <c>{Name: K, IsNameBacktickEscaped: true}</c>, and the escaped twin's
/// <see cref="Oracle.Outcome"/> equals the renamed twin's (so a wrong-but-consistent pair of node
/// kinds cannot pass).</item>
/// </list>
/// The <c>X</c> rows are the non-contextual positions (binding target, ordinary expression) for
/// every keyword: both spellings are identifiers today and stay so.
///
/// <para><b>Ratchet.</b> The escaped cells are red until P40 Phase 2. <see cref="KnownRed"/> lists
/// them; a listed row whose escaped cell fails passes as expected, a listed row whose escaped cell
/// holds fails ("stale KnownRed entry — delete it"). Bare and renamed cells are never listed.</para>
/// </summary>
public class ContextualKeywordEscapeMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ContextualKeywordEscapeMatrixTests(ITestOutputHelper output) => _output = output;

    public enum Escaped
    {
        /// <summary>The escaped token is an identifier that parses in place: a name node carries it.</summary>
        IdentifierBound,

        /// <summary>The escaped token is an identifier the site cannot take: a parse error.</summary>
        ParseError,
    }

    public sealed record Row(string Id, string Keyword, string Template, string Construct, Func<Module, bool> HasConstruct, Escaped Escaped)
    {
        public string Source(string spelling) => Template.Replace("{K}", spelling, StringComparison.Ordinal);
    }

    private static bool Any<T>(Module module, Func<T, bool>? predicate = null)
        => Oracle.Walk(module).OfType<T>().Any(predicate ?? (_ => true));

    private static bool HasPlaceholderLambda(Module module)
        => Any<LambdaExpression>(module, l => l.Parameters.Any(p => p.Name.StartsWith("__placeholder_", StringComparison.Ordinal)));

    private const string Match = "match x:\n    case {P}:\n        pass\n";

    private static string Case(string pattern) => Match.Replace("{P}", pattern, StringComparison.Ordinal);

    /// <summary>The contextual sites of every roster keyword (Parser escape-blind sites 1–13 of plan-be89c1).</summary>
    public static readonly IReadOnlyList<Row> Rows = new Row[]
    {
        // `_`: the call placeholder (positional, keyword), the operator section, and the wildcard in
        // every pattern position (case, star capture, class positional/keyword, tuple element, guarded).
        new("U1", ContextualKeywords.Placeholder, "f(1, {K})\n", "placeholder lambda", HasPlaceholderLambda, Escaped.IdentifierBound),
        new("U2", ContextualKeywords.Placeholder, "f(1, b={K})\n", "lambda over the keyword parameter b", m => Any<LambdaExpression>(m, l => l.Parameters.Any(p => p.Name == "b")), Escaped.IdentifierBound),
        new("U3", ContextualKeywords.Placeholder, "h = ({K} + 1)\n", "placeholder lambda", HasPlaceholderLambda, Escaped.IdentifierBound),
        new("U4", ContextualKeywords.Placeholder, Case("{K}"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),
        new("U5", ContextualKeywords.Placeholder, Case("[1, *{K}]"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),
        new("U6", ContextualKeywords.Placeholder, Case("P({K}, y)"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),
        new("U7", ContextualKeywords.Placeholder, Case("P(x={K})"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),
        new("U8", ContextualKeywords.Placeholder, Case("({K}, z)"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),
        new("U9", ContextualKeywords.Placeholder, Case("{K} if c"), "WildcardPattern", m => Any<WildcardPattern>(m), Escaped.IdentifierBound),

        // Property accessors.
        new("G1", ContextualKeywords.Get, "class C:\n    property {K} p(self) -> int:\n        return 1\n", "PropertyAccessor.Get", m => Any<PropertyDef>(m, p => p.Accessor == PropertyAccessor.Get), Escaped.ParseError),
        new("G2", ContextualKeywords.Set, "class C:\n    property {K} p(self, v: int):\n        pass\n", "PropertyAccessor.Set", m => Any<PropertyDef>(m, p => p.Accessor == PropertyAccessor.Set), Escaped.ParseError),
        new("G3", ContextualKeywords.Init, "class C:\n    property {K} p(self, v: int):\n        pass\n", "PropertyAccessor.Init", m => Any<PropertyDef>(m, p => p.Accessor == PropertyAccessor.Init), Escaped.ParseError),

        // Event accessors.
        new("A1", ContextualKeywords.Add, "class C:\n    event {K} e(self, h: H):\n        pass\n", "EventAccessor.Add", m => Any<EventDef>(m, e => e.Accessor == EventAccessor.Add), Escaped.ParseError),
        new("A2", ContextualKeywords.Remove, "class C:\n    event {K} e(self, h: H):\n        pass\n", "EventAccessor.Remove", m => Any<EventDef>(m, e => e.Accessor == EventAccessor.Remove), Escaped.ParseError),

        // Property observers (parse level: the parser always parses the clause; the gate is semantic).
        new("O1", ContextualKeywords.BeforeSet, "class C:\n    property health: int = 0\n        {K}(v):\n            pass\n", "ObserverKind.BeforeSet", m => Any<PropertyObserver>(m, o => o.Kind == ObserverKind.BeforeSet), Escaped.ParseError),
        new("O2", ContextualKeywords.AfterSet, "class C:\n    property health: int = 0\n        {K}(v):\n            pass\n", "ObserverKind.AfterSet", m => Any<PropertyObserver>(m, o => o.Kind == ObserverKind.AfterSet), Escaped.ParseError),

        // Exception filters, with and without `as`.
        new("W1", ContextualKeywords.When, "try:\n    pass\nexcept E as e {K} c:\n    pass\n", "ExceptHandler.Filter", m => Any<ExceptHandler>(m, h => h.Filter != null), Escaped.ParseError),
        new("W2", ContextualKeywords.When, "try:\n    pass\nexcept E {K} c:\n    pass\n", "ExceptHandler.Filter", m => Any<ExceptHandler>(m, h => h.Filter != null), Escaped.ParseError),

        // Parameter and call-site modifiers.
        new("M1", ContextualKeywords.Out, "def f(x: {K} int):\n    pass\n", "ParameterModifier.Out", m => Any<Parameter>(m, p => p.Modifier == ParameterModifier.Out), Escaped.ParseError),
        new("M2", ContextualKeywords.Ref, "def f(x: {K} int):\n    pass\n", "ParameterModifier.Ref", m => Any<Parameter>(m, p => p.Modifier == ParameterModifier.Ref), Escaped.ParseError),
        new("M3", ContextualKeywords.Out, "f({K} y)\n", "ModifiedArgument(Out)", m => Any<ModifiedArgument>(m, a => a.Modifier == ParameterModifier.Out), Escaped.ParseError),
        new("M4", ContextualKeywords.Ref, "f({K} y)\n", "ModifiedArgument(Ref)", m => Any<ModifiedArgument>(m, a => a.Modifier == ParameterModifier.Ref), Escaped.ParseError),

        // Covariance.
        new("V1", ContextualKeywords.Out, "interface I[{K} T]:\n    pass\n", "TypeParameterVariance.Covariant", m => Any<TypeParameterDef>(m, t => t.Variance == TypeParameterVariance.Covariant), Escaped.ParseError),
        new("V2", ContextualKeywords.Out, "delegate D[{K} T]() -> T\n", "TypeParameterVariance.Covariant", m => Any<TypeParameterDef>(m, t => t.Variance == TypeParameterVariance.Covariant), Escaped.ParseError),

        // Constraints: an escaped `notnull` is a type constraint naming the type `notnull`.
        new("C1", ContextualKeywords.NotNull, "def f[T: {K}](x: T) -> T:\n    return x\n", "NotnullConstraint", m => Any<NotnullConstraint>(m), Escaped.IdentifierBound),
        new("C2", ContextualKeywords.New, "def f[T: {K}()](x: T) -> T:\n    return x\n", "NewConstraint", m => Any<NewConstraint>(m), Escaped.ParseError),
    };

    /// <summary>
    /// Escaped cells red until P40 Phase 2 (#2166) — drain on fix: a row whose escaped cell holds
    /// must be deleted from this set.
    /// </summary>
    private static readonly IReadOnlySet<string> KnownRed = new HashSet<string>(StringComparer.Ordinal)
    {
        "U1", "U2", "U3", "U4", "U5", "U6", "U7", "U8", "U9", // #2166
        "G1", "G2", "G3", // #2166
        "A1", "A2", // #2166
        "O1", "O2", // #2166
        "W1", "W2", // #2166
        "M1", "M2", "M3", "M4", // #2166
        "V1", "V2", // #2166
        "C1", "C2", // #2166
    };

    private static Row RowById(string id) => Rows.Single(r => r.Id == id);

    public static IEnumerable<object[]> RowIds() => Rows.Select(r => new object[] { r.Id });

    [Theory]
    [MemberData(nameof(RowIds))]
    public void Bare_IsTheContextualKeyword(string id)
    {
        var row = RowById(id);
        var bare = Oracle.Observe(row.Source(row.Keyword));

        Assert.False(bare.HasErrors, $"{id}: the bare spelling must parse: {bare}\n{row.Source(row.Keyword)}");
        Assert.True(row.HasConstruct(bare.Module!), $"{id}: the bare '{row.Keyword}' must build {row.Construct}");
    }

    [Theory]
    [MemberData(nameof(RowIds))]
    public void Renamed_IsAnIdentifier(string id)
    {
        var row = RowById(id);
        var name = Oracle.RenamedSpelling(row.Keyword, row.Template);
        var renamed = Oracle.Observe(row.Source(name));

        if (renamed.Module != null)
            Assert.False(row.HasConstruct(renamed.Module), $"{id}: a fresh identifier must not build {row.Construct}");
        if (row.Escaped == Escaped.IdentifierBound)
        {
            Assert.False(renamed.HasErrors, $"{id}: '{name}' must parse as an identifier: {renamed}");
            Assert.True(Oracle.HasName(renamed.Module!, name, escaped: false), $"{id}: no name node binds '{name}'");
        }
        else
        {
            Assert.True(renamed.HasErrors, $"{id}: '{name}' in the site must not parse");
        }
    }

    [Theory]
    [MemberData(nameof(RowIds))]
    public void Escaped_ParsesAsTheRenamedTwin(string id)
    {
        var row = RowById(id);
        var problems = EscapedProblems(row);
        _output.WriteLine($"CKE-MATRIX {id} {row.Keyword} {(problems.Count == 0 ? "ok" : "red")}");
        foreach (var problem in problems)
            _output.WriteLine($"  {problem}");

        if (KnownRed.Contains(id))
        {
            Assert.True(problems.Count > 0,
                $"{id}: the escaped '{row.Keyword}' is an identifier now — stale KnownRed entry — delete it (#2166 is fixed for this cell)");
            return;
        }

        Assert.True(problems.Count == 0, $"{id}: an escaped '{row.Keyword}' must be an identifier:\n  " + string.Join("\n  ", problems));
    }

    private static List<string> EscapedProblems(Row row)
    {
        var problems = new List<string>();
        var escaped = Oracle.Observe(row.Source(Oracle.EscapedSpelling(row.Keyword)));
        var renamed = Oracle.Observe(row.Source(Oracle.RenamedSpelling(row.Keyword, row.Template)));

        if (escaped.Module != null && row.HasConstruct(escaped.Module))
            problems.Add($"the escape is lost: `{row.Keyword}` still builds {row.Construct}");
        if (row.Escaped == Escaped.IdentifierBound)
        {
            if (escaped.HasErrors)
                problems.Add($"expected an identifier, got {escaped}");
            else if (!Oracle.HasName(escaped.Module!, row.Keyword, escaped: true))
                problems.Add($"no name node {{Name: {row.Keyword}, IsNameBacktickEscaped: true}}");
        }
        else if (!escaped.HasErrors)
        {
            problems.Add("expected a parse error, the escaped twin parses");
        }

        if (!escaped.SameAs(renamed))
            problems.Add("escaped ≠ renamed: " + Oracle.Difference(escaped, renamed));
        return problems;
    }

    // ================================================================
    // Non-contextual positions: binding target and ordinary expression
    // ================================================================

    public static IEnumerable<object[]> OrdinaryCells()
        => LiteralRoster.SelectMany(k => new[] { new object[] { "X1", k }, new object[] { "X2", k } });

    /// <summary>
    /// X1 binds the bare spelling and reads the escaped one; X2 the reverse. Both parse to
    /// <see cref="Identifier"/>s carrying their own flag. The read is an assignment's value, not a
    /// call argument — <c>print(_)</c> is the placeholder site U1.
    /// </summary>
    [Theory]
    [MemberData(nameof(OrdinaryCells))]
    public void Ordinary_BothSpellingsAreIdentifiers(string id, string keyword)
    {
        var source = id == "X1"
            ? $"{keyword} = 1\ny = `{keyword}`\n"
            : $"`{keyword}` = 2\ny = {keyword}\n";
        var outcome = Oracle.Observe(source);

        Assert.False(outcome.HasErrors, $"{id} {keyword}: must parse: {outcome}\n{source}");
        var names = Oracle.Walk(outcome.Module!).OfType<Identifier>().Where(i => i.Name == keyword).ToList();
        Assert.Contains(names, i => !i.IsNameBacktickEscaped);
        Assert.Contains(names, i => i.IsNameBacktickEscaped);
    }

    // ================================================================
    // Totality
    // ================================================================

    /// <summary>The roster, written out — never derived from <see cref="ContextualKeywords.All"/>.</summary>
    private static readonly string[] LiteralRoster =
    {
        "_", "get", "set", "init", "add", "remove", "before_set", "after_set", "when", "out", "ref", "notnull", "new",
    };

    [Fact]
    public void Totality_TheRosterIsTheLiteralThirteen_AndEveryMemberHasASiteRow()
    {
        Assert.Equal(13, LiteralRoster.Length);
        Assert.Equal(LiteralRoster.OrderBy(k => k, StringComparer.Ordinal), ContextualKeywords.All.OrderBy(k => k, StringComparer.Ordinal));

        var withoutRow = LiteralRoster.Where(k => !Rows.Any(r => r.Keyword == k)).ToList();
        Assert.True(withoutRow.Count == 0, "every contextual keyword needs a site row: " + string.Join(", ", withoutRow));
        Assert.All(Rows, r => Assert.Contains(r.Keyword, LiteralRoster));
        Assert.Equal(Rows.Count, Rows.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void KnownRed_NamesOnlySiteRows()
    {
        var unknown = KnownRed.Where(id => !Rows.Any(r => r.Id == id)).ToList();
        Assert.True(unknown.Count == 0, "KnownRed names rows that do not exist: " + string.Join(", ", unknown));
    }
}

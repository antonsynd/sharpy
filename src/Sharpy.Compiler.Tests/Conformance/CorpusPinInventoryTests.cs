using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The closed inventory of integer literals in corpus-reading test code (#2512 W3.5, plan-88736c D3(iv)): a new fixture
/// must move no unrelated count pin, so every integer an assertion, field or table row holds in a file that reads the
/// fixture corpus is a row of <c>corpus-pin-inventory.txt</c> with a reason from a closed set, and the sweep here is the
/// totality check.
///
/// <para><b>Sweep.</b> Files: every <c>*.cs</c> under <c>src/*Tests*/</c> (no <c>bin</c>, <c>obj</c>, <c>TestFixtures</c>,
/// <c>generated</c>) whose comment-stripped text names a corpus reader (<see cref="CorpusReader"/>), with its
/// partial-class siblings (<c>Name.Part.cs</c> beside <c>Name.cs</c>). Hits: each comment-stripped line matching one of
/// <see cref="HitPatterns"/>; plus every <c>*.txt</c> under the same roots with a bare integer token before its first
/// <c>#</c> (an integer column). One comment-stripping rule (<see cref="Strip"/>, Roslyn comment trivia blanked) serves
/// the file filter, the hit text and the per-reason checks.</para>
///
/// <para><b>Key.</b> A multiset of (file, trimmed comment-stripped text): a row's <c>×n</c> is the number of hits with
/// that text in that file. Red, each named: an unlisted hit or a surplus one (a new pin, or an edited literal), a row
/// with no hit, a multiplicity that differs, and a row whose reason fails its check.</para>
///
/// <para><b>Reasons.</b> Checked per row (<see cref="CheckReason"/>): <c>floor</c>, <c>zero-guard</c>,
/// <c>drained-empty</c>, <c>non-integer</c>, <c>code-site-roster</c>, <c>spec-file-roster</c>, <c>generator-roster</c>,
/// <c>rate-tolerance</c>, <c>set-file</c>. Unchecked, with evidence text the lead reviews in the inventory diff at each
/// gate: <c>tuning-constant</c> (the identifier), <c>fixed-input</c> (the named stems, or the member that reads no
/// corpus). <c>in</c> marked a corpus-moved count pin while plan-88736c D3 converted them to set files; none may remain.</para>
/// </summary>
public sealed class CorpusPinInventoryTests
{
    private const string InventoryFileName = "corpus-pin-inventory.txt";

    private readonly ITestOutputHelper _output;

    public CorpusPinInventoryTests(ITestOutputHelper output) => _output = output;

    // ================================================================
    // The sweep
    // ================================================================

    /// <summary>The file filter: a comment-stripped source naming one of these reads the fixture corpus.</summary>
    internal static readonly Regex CorpusReader = new(
        @"FixtureRoots|SweepCorpus|DiscoverFixtures|FixtureDiscoveryHelper|TestFixtures|TestFixturePaths|CorpusCensus", RegexOptions.Compiled);

    /// <summary>A corpus enumerator in a member's reach (narrower than <see cref="CorpusReader"/>: the repository root is a path, not a corpus).</summary>
    internal static readonly Regex CorpusEnumerator = new(
        @"FixtureRoots\.(All|CompilerTests|StdlibTests)\b|SweepCorpus|DiscoverFixtures|FixtureDiscoveryHelper|TestFixturePaths|CorpusCensus|""TestFixtures""",
        RegexOptions.Compiled);

    /// <summary>The hit forms, first match wins: (kind, pattern). <c>n</c> is the compared literal, <c>id</c> a field's name.</summary>
    internal static readonly (string Kind, Regex Pattern)[] HitPatterns =
    {
        (Assertion, new Regex(@"Should\(\)\.(?<cmp>Be|HaveCount|BeLessThanOrEqualTo|BeGreaterThanOrEqualTo|BeLessThan|BeGreaterThan|HaveCountLessThanOrEqualTo|HaveCountGreaterThanOrEqualTo|HaveCountLessThan|HaveCountGreaterThan)\((?<n>[0-9_]+)[,)]", RegexOptions.Compiled)),
        (Assertion, new Regex(@"Assert\.(?<cmp>Equal|True)\((?<n>[0-9_]+),", RegexOptions.Compiled)),
        (Assertion, new Regex(@"Assert\.True\([^,;]*?\s(?<cmp>[<>]=?)\s*(?<n>[0-9_]+)", RegexOptions.Compiled)),
        (Assertion, new Regex(@"Assert\.(?<cmp>InRange)\(", RegexOptions.Compiled)),
        (Field, new Regex(@"(const int|static readonly int|const long) (?<id>\w+) = (?<n>[0-9_]+);", RegexOptions.Compiled)),
        (Row, new Regex(@"^\s*\[[^]]+\] = ([0-9]+,|new\[\] \{)", RegexOptions.Compiled)),
    };

    internal const string Assertion = "assertion";
    internal const string Field = "field";
    internal const string Row = "row";
    internal const string Column = "column";

    /// <summary>The text of a <c>*.txt</c> hit: the file has an integer column.</summary>
    internal const string IntegerColumnText = "(integer column)";

    private static readonly Regex IntegerLiteral = new(@"(?<![\w.])\d[\d_]*(?![\w.])", RegexOptions.Compiled);
    private static readonly Regex BareInteger = new(@"^\d[\d_]*$", RegexOptions.Compiled);

    /// <summary>One source file of the sweep, comment-stripped, with its syntax tree.</summary>
    internal sealed class Source
    {
        public Source(string path, string text)
        {
            Path = path;
            Tree = CSharpSyntaxTree.ParseText(text);
            Stripped = Strip(Tree, text);
            Lines = Stripped.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Group = GroupKey(path);
        }

        public string Path { get; }
        public SyntaxTree Tree { get; }
        public string Stripped { get; }
        public string[] Lines { get; }
        public string Group { get; }

        /// <summary>The project directory under <c>src/</c> (the first path segment).</summary>
        public string Project => Path[..Path.IndexOf('/')];
    }

    /// <summary>The one comment-stripping rule: every comment trivia (line, block, doc) blanked to spaces, line breaks kept (so spans and lines stay aligned).</summary>
    internal static string Strip(SyntaxTree tree, string text)
    {
        var chars = text.ToCharArray();
        foreach (var trivia in tree.GetRoot().DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                continue;
            for (var i = trivia.Span.Start; i < trivia.Span.End; i++)
            {
                if (chars[i] != '\n' && chars[i] != '\r')
                    chars[i] = ' ';
            }
        }

        return new string(chars);
    }

    /// <summary>Partial-class siblings share a group: the directory plus the file name up to its first dot.</summary>
    internal static string GroupKey(string path)
    {
        var slash = path.LastIndexOf('/');
        var name = path[(slash + 1)..];
        return path[..(slash + 1)] + name[..name.IndexOf('.')];
    }

    /// <summary>One hit: a (file, trimmed stripped text) occurrence and what the per-reason checks read from it.</summary>
    internal sealed record Hit(string File, string Text, string Kind, string? Identifier, string? Literal, string? Comparator, Source? Source, int Line);

    /// <summary>The sweep's population and hits.</summary>
    internal sealed record SweepResult(IReadOnlyList<Source> Sources, IReadOnlyList<Source> Swept, IReadOnlyList<Hit> Hits);

    /// <summary>Sweeps <paramref name="files"/> (path relative to <c>src/</c>, text): the sources, the swept ones, the hits.</summary>
    internal static SweepResult Sweep(IEnumerable<(string Path, string Text)> files)
    {
        var all = files.ToList();
        var sources = all.Where(f => f.Path.EndsWith(".cs", StringComparison.Ordinal)).Select(f => new Source(f.Path, f.Text)).ToList();
        var readerGroups = sources.Where(s => CorpusReader.IsMatch(s.Stripped)).Select(s => s.Group).ToHashSet(StringComparer.Ordinal);
        var swept = sources.Where(s => readerGroups.Contains(s.Group)).OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
        var hits = new List<Hit>();
        foreach (var source in swept)
        {
            for (var i = 0; i < source.Lines.Length; i++)
            {
                var line = source.Lines[i];
                foreach (var (kind, pattern) in HitPatterns)
                {
                    var m = pattern.Match(line);
                    if (!m.Success)
                        continue;
                    hits.Add(new Hit(source.Path, line.Trim(), kind, m.Groups["id"].Success ? m.Groups["id"].Value : null,
                        m.Groups["n"].Success ? m.Groups["n"].Value : null, m.Groups["cmp"].Success ? m.Groups["cmp"].Value : null, source, i));
                    break;
                }
            }
        }

        foreach (var (path, text) in all.Where(f => f.Path.EndsWith(".txt", StringComparison.Ordinal)).OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var integerColumn = text.Split('\n').Any(raw =>
            {
                var hash = raw.IndexOf('#');
                var body = hash < 0 ? raw : raw[..hash];
                return body.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries).Any(t => BareInteger.IsMatch(t));
            });
            if (integerColumn)
                hits.Add(new Hit(path, IntegerColumnText, Column, null, null, null, null, -1));
        }

        return new SweepResult(sources, swept, hits);
    }

    // ================================================================
    // The inventory
    // ================================================================

    internal const string In = "in";

    /// <summary>The reasons a mechanical check backs (<see cref="CheckReason"/>).</summary>
    internal static readonly string[] CheckedReasons =
    {
        "floor", "zero-guard", "drained-empty", "non-integer", "code-site-roster", "spec-file-roster", "generator-roster", "rate-tolerance", "set-file",
    };

    /// <summary>The closed reason set: the checked ones, the two evidence-only ones and <see cref="In"/>.</summary>
    internal static readonly string[] Reasons = CheckedReasons.Concat(new[] { "tuning-constant", "fixed-input", In }).ToArray();

    /// <summary>
    /// The symbols a <c>rate-tolerance</c> row may sit in: the formatting route sweep's per-stem refusal-ratio bounds, its
    /// corpus refusal-ratio ceilings and its census measures' ceilings per corpus stem (R-DK's decision-rule instrument, R-MY):
    /// ratios, not counts, so an ordinary fixture does not move them.
    /// </summary>
    internal static readonly string[] RateToleranceSymbols = { "RefusalRatioBoundPercent", "RefusalCorpusCeilingPercent", "CensusCellsPerThousandStemsCeiling" };

    /// <summary>The D3 set files: a per-stem column there is a set row, not a count pin.</summary>
    internal static readonly string[] SetFiles = { "formatting-route-census.txt", "code-action-shape-stems.txt", "generate-expression-uncompilable.txt" };

    /// <summary>One inventory row: <c>file | text | ×n | reason | evidence</c>.</summary>
    internal sealed record InventoryRow(string File, string Text, int Count, string Reason, string Evidence)
    {
        public override string ToString() => $"{File} | {Text} | ×{Count} | {Reason} | {Evidence}";
    }

    internal static IReadOnlyList<InventoryRow> ParseInventory(IEnumerable<string> lines)
    {
        var rows = new List<InventoryRow>();
        var keys = new HashSet<(string, string)>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var fields = line.Split(" | ");
            string Refuse(string why) => $"{InventoryFileName}: '{line}' — {why}";
            if (fields.Length < 5)
                throw new InvalidOperationException(Refuse("must read `file | text | ×n | reason | evidence`."));
            var text = string.Join(" | ", fields[1..^3]);
            var count = fields[^3];
            if (!count.StartsWith('×') || !int.TryParse(count[1..], out var n) || n < 1)
                throw new InvalidOperationException(Refuse("the multiplicity must read ×n with n ≥ 1."));
            var reason = fields[^2];
            if (!Reasons.Contains(reason))
                throw new InvalidOperationException(Refuse($"reason '{reason}' is not one of {string.Join(", ", Reasons)}."));
            var evidence = fields[^1].Trim();
            if (evidence.Length == 0)
                throw new InvalidOperationException(Refuse("every row carries evidence text."));
            if (!keys.Add((fields[0], text)))
                throw new InvalidOperationException(Refuse("is listed twice (one row per (file, text); its ×n is the multiplicity)."));
            rows.Add(new InventoryRow(fields[0], text, n, reason, evidence));
        }

        return rows;
    }

    /// <summary>The totality diff and the per-reason checks: every problem, named.</summary>
    internal static List<string> Compare(SweepResult sweep, IReadOnlyList<InventoryRow> rows)
    {
        var problems = new List<string>();
        var hitsByKey = sweep.Hits.GroupBy(h => (h.File, h.Text)).ToDictionary(g => g.Key, g => g.ToList());
        var rowsByKey = rows.ToDictionary(r => (r.File, r.Text));
        foreach (var (key, hits) in hitsByKey.OrderBy(p => p.Key.File, StringComparer.Ordinal).ThenBy(p => p.Key.Text, StringComparer.Ordinal))
        {
            if (!rowsByKey.TryGetValue(key, out var row))
            {
                problems.Add($"unlisted hit: {key.File} | {key.Text} (×{hits.Count}, line {string.Join(",", hits.Select(h => h.Line + 1))}): add a row with its reason");
                continue;
            }

            if (row.Count != hits.Count)
            {
                problems.Add($"multiplicity: {key.File} | {key.Text} is listed ×{row.Count} and the sweep finds ×{hits.Count} "
                    + $"(line {string.Join(",", hits.Select(h => h.Line + 1))})" + (hits.Count > row.Count ? ": an unlisted hit with the text of a listed one" : ""));
            }

            foreach (var hit in hits)
            {
                var failure = CheckReason(hit, row.Reason, row.Evidence, sweep);
                if (failure is not null)
                    problems.Add($"reason: {key.File} | {key.Text} (line {hit.Line + 1}) is labelled {row.Reason}, and {failure}");
            }
        }

        foreach (var row in rows.Where(r => !hitsByKey.ContainsKey((r.File, r.Text))))
            problems.Add($"row has no hit: {row.File} | {row.Text} — the sweep no longer finds it: delete the row (or the literal moved: re-key it)");

        return problems;
    }

    // ================================================================
    // Per-reason checks
    // ================================================================

    private static readonly Regex LowerBoundComparator = new(@"BeGreaterThan(OrEqualTo)?\(|HaveCountGreaterThan(OrEqualTo)?\(|Assert\.True\([^,;]*?\s>=?\s*\d", RegexOptions.Compiled);
    private static readonly Regex EqualityComparator = new(@"Should\(\)\.(Be|HaveCount)\(|Assert\.Equal\(", RegexOptions.Compiled);
    private static readonly Regex CodeRoot = new(@"""src""|""src/|""\.\./Sharpy\.|""Sharpy\.(Compiler|Core|Stdlib|Lsp|Cli)""", RegexOptions.Compiled);
    private static readonly Regex SpecRoot = new(@"""language_specification""|docs/language_specification", RegexOptions.Compiled);

    /// <summary>Null when <paramref name="reason"/>'s check holds for <paramref name="hit"/>; else why not (naming the check).</summary>
    internal static string? CheckReason(Hit hit, string reason, string evidence, SweepResult sweep)
    {
        switch (reason)
        {
            case "floor":
                if (hit.Kind == Assertion)
                    return LowerBoundComparator.IsMatch(hit.Text) ? null : "the floor check fails: the assertion is not a lower bound (>=, >, BeGreaterThan[OrEqualTo])";
                if (hit.Kind == Field && hit.Identifier is not null)
                    return FloorUses(hit, sweep);
                return $"the floor check fails: a {hit.Kind} hit is neither a lower-bound assertion nor a field compared as one";
            case "zero-guard":
                return hit.Kind == Assertion && IsZero(hit.Literal) ? null : "the zero-guard check fails: the hit is not an assertion against the literal 0";
            case "drained-empty":
                return hit.Kind == Assertion && IsZero(hit.Literal) && EqualityComparator.IsMatch(hit.Text)
                    ? null
                    : "the drained-empty check fails: the hit is not an equality assertion against the literal 0";
            case "non-integer":
                return hit.Kind != Column && !IntegerLiteral.IsMatch(hit.Text) ? null : "the non-integer check fails: the hit line holds an integer literal";
            case "code-site-roster":
                return RosterReach(hit, sweep, CodeRoot, "code-site-roster", "src/");
            case "spec-file-roster":
                return RosterReach(hit, sweep, SpecRoot, "spec-file-roster", "docs/language_specification/");
            case "generator-roster":
                if (hit.Source is null)
                    return "the generator-roster check fails: a *.txt column is not a generated roster";
                var enumerating = sweep.Swept.Where(s => s.Group == hit.Source.Group && CorpusEnumerator.IsMatch(s.Stripped)).Select(s => s.Path).ToList();
                return enumerating.Count == 0 ? null : $"the generator-roster check fails: {string.Join(", ", enumerating)} reads a corpus enumerator";
            case "rate-tolerance":
                var symbol = hit.Source is null ? null : EnclosingMember(hit.Source, hit.Line) is FieldDeclarationSyntax f ? f.Declaration.Variables.First().Identifier.Text : null;
                return symbol is not null && RateToleranceSymbols.Contains(symbol)
                    ? null
                    : $"the rate-tolerance check fails: the hit is in {symbol ?? "no field"}, not one of {string.Join(", ", RateToleranceSymbols)}";
            case "set-file":
                return hit.Kind == Column && SetFiles.Contains(Path.GetFileName(hit.File))
                    ? null
                    : $"the set-file check fails: {hit.File} is not a D3 set file ({string.Join(", ", SetFiles)})";
            case "tuning-constant":
                return hit.Text.Contains(evidence, StringComparison.Ordinal) ? null : $"tuning-constant evidence '{evidence}' must be the identifier the hit names";
            default:
                return null;
        }
    }

    private static bool IsZero(string? literal) => literal is not null && literal.Replace("_", "").TrimStart('0').Length == 0;

    /// <summary>A field floor: resolved to its comparison sites in its file group, never its name — ≥ 1 lower-bound use and no pinning use.</summary>
    private static string? FloorUses(Hit hit, SweepResult sweep)
    {
        var id = Regex.Escape(hit.Identifier!);
        var lower = new Regex($@"(BeGreaterThan(OrEqualTo)?\(\s*|HaveCountGreaterThan(OrEqualTo)?\(\s*|\s>=?\s*){id}\b");
        var pinning = new Regex($@"((\bBe|HaveCount|BeLessThan(OrEqualTo)?|HaveCountLessThan(OrEqualTo)?|Equal)\(\s*|==\s*|!=\s*|\s<=?\s*){id}\b|\b{id}\s*(==|!=|>=?)");
        var group = sweep.Swept.Where(s => s.Group == hit.Source!.Group).ToList();
        var lowerUses = group.Sum(s => lower.Matches(s.Stripped).Count);
        var pinningUses = group.Sum(s => pinning.Matches(s.Stripped).Count);
        if (lowerUses > 0 && pinningUses == 0)
            return null;
        return $"the floor check fails: {hit.Identifier} has {lowerUses} lower-bound comparison site(s) and {pinningUses} pinning one(s) in its file group";
    }

    /// <summary>
    /// A roster's members: the hit's enclosing member, or (a field) the members that read it, or (a <c>*.txt</c>) the members
    /// of its project naming the file (through the field that holds the name). Each member's reach (itself and, transitively,
    /// the members of its file group it names, comment-stripped) reads no corpus enumerator, and one of them names
    /// <paramref name="root"/>.
    /// </summary>
    private static string? RosterReach(Hit hit, SweepResult sweep, Regex root, string check, string rootName)
    {
        var anchors = Anchors(hit, sweep);
        if (anchors.Count == 0)
            return $"the {check} check fails: no member reads the hit";
        var named = false;
        foreach (var (source, member) in anchors)
        {
            var reach = Reach(source, member, sweep);
            var enumerator = CorpusEnumerator.Match(reach);
            if (enumerator.Success)
                return $"the {check} check fails: {MemberName(member)}'s reach reads the corpus ({enumerator.Value})";
            named |= root.IsMatch(reach);
        }

        return named
            ? null
            : $"the {check} check fails: no reach of {string.Join(", ", anchors.Select(a => MemberName(a.Member)).Distinct())} names the {rootName} root";
    }

    private static List<(Source Source, MemberDeclarationSyntax Member)> Anchors(Hit hit, SweepResult sweep)
    {
        if (hit.Source is null)
        {
            var project = hit.File[..hit.File.IndexOf('/')];
            var literal = $"\"{Path.GetFileName(hit.File)}\"";
            var naming = sweep.Sources.Where(s => s.Project == project && s.Stripped.Contains(literal, StringComparison.Ordinal))
                .SelectMany(s => Members(s).Where(m => s.Stripped.AsSpan(m.Span.Start, m.Span.Length).IndexOf(literal.AsSpan(), StringComparison.Ordinal) >= 0)
                    .Select(m => (s, m)))
                .ToList();
            return naming.SelectMany(n => n.m is BaseFieldDeclarationSyntax field
                    ? Readers(n.s, field, field.Declaration.Variables.First().Identifier.Text, sweep)
                    : new() { n })
                .ToList();
        }

        var enclosing = EnclosingMember(hit.Source, hit.Line);
        if (hit.Kind != Field || hit.Identifier is null)
            return enclosing is null ? new() : new() { (hit.Source, enclosing) };
        return enclosing is null ? new() : Readers(hit.Source, enclosing, hit.Identifier, sweep);
    }

    /// <summary>The members of <paramref name="source"/>'s file group that name <paramref name="identifier"/>, other than its declaration.</summary>
    private static List<(Source Source, MemberDeclarationSyntax Member)> Readers(Source source, MemberDeclarationSyntax declaration, string identifier, SweepResult sweep)
        => sweep.Sources.Where(s => s.Group == source.Group)
            .SelectMany(s => Members(s).Where(m => m != declaration && m.DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.Text == identifier))
                .Select(m => (s, m)))
            .ToList();

    private static IEnumerable<MemberDeclarationSyntax> Members(Source source)
        => source.Tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().Where(m => m is not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax);

    private static MemberDeclarationSyntax? EnclosingMember(Source source, int line)
    {
        var text = source.Tree.GetText();
        if (line < 0 || line >= text.Lines.Count)
            return null;
        var span = text.Lines[line].Span;
        var offset = span.Start + (span.Length - text.Lines[line].ToString().TrimStart().Length);
        return source.Tree.GetRoot().FindToken(offset).Parent?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(m => m is not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax);
    }

    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        BaseFieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        _ => member.Kind().ToString(),
    };

    /// <summary>The comment-stripped text of <paramref name="member"/> and every member of its file group it names, transitively.</summary>
    private static string Reach(Source source, MemberDeclarationSyntax member, SweepResult sweep)
    {
        var group = sweep.Sources.Where(s => s.Group == source.Group).ToList();
        var byName = new Dictionary<string, List<(Source, MemberDeclarationSyntax)>>(StringComparer.Ordinal);
        foreach (var s in group)
        {
            foreach (var m in Members(s))
            {
                var names = m is BaseFieldDeclarationSyntax f ? f.Declaration.Variables.Select(v => v.Identifier.Text) : new[] { MemberName(m) };
                foreach (var name in names)
                {
                    if (!byName.TryGetValue(name, out var list))
                        byName[name] = list = new();
                    list.Add((s, m));
                }
            }
        }

        var seen = new HashSet<MemberDeclarationSyntax>();
        var queue = new Queue<(Source, MemberDeclarationSyntax)>();
        queue.Enqueue((source, member));
        seen.Add(member);
        var text = new System.Text.StringBuilder();
        while (queue.Count > 0)
        {
            var (s, m) = queue.Dequeue();
            text.Append(s.Stripped, m.Span.Start, m.Span.Length).Append('\n');
            foreach (var name in m.DescendantNodes().OfType<IdentifierNameSyntax>().Select(n => n.Identifier.Text).Distinct())
            {
                if (!byName.TryGetValue(name, out var targets))
                    continue;
                foreach (var target in targets.Where(t => seen.Add(t.Item2)))
                    queue.Enqueue(target);
            }
        }

        return text.ToString();
    }

    // ================================================================
    // Facts
    // ================================================================

    private static readonly string SrcRoot = Path.Combine(FixtureRoots.RepositoryRoot, "src");

    private static IEnumerable<(string Path, string Text)> RepositoryFiles()
    {
        foreach (var project in Directory.GetDirectories(SrcRoot).Where(d => Path.GetFileName(d).Contains("Tests", StringComparison.Ordinal)).OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".cs", StringComparison.Ordinal) && !file.EndsWith(".txt", StringComparison.Ordinal))
                    continue;
                var relative = Path.GetRelativePath(SrcRoot, file).Replace('\\', '/');
                var segments = relative.Split('/');
                if (segments.Any(s => s is "bin" or "obj" or "TestFixtures" or "generated"))
                    continue;
                yield return (relative, File.ReadAllText(file));
            }
        }
    }

    private static string InventoryPath => Path.Combine(SrcRoot, "Sharpy.Compiler.Tests", "Conformance", InventoryFileName);

    [Fact]
    public void EveryCorpusPin_IsAnInventoryRow_WithAReasonThatHolds()
    {
        var sweep = Sweep(RepositoryFiles());
        File.Exists(InventoryPath).Should().BeTrue($"Conformance/{InventoryFileName} is what arms the totality check");
        var rows = ParseInventory(File.ReadAllLines(InventoryPath));
        var problems = Compare(sweep, rows);

        var keys = sweep.Hits.Select(h => (h.File, h.Text)).Distinct().Count();
        _output.WriteLine($"CORPUS-PIN-INVENTORY sources={sweep.Sources.Count} swept={sweep.Swept.Count} hits={sweep.Hits.Count} keys={keys} "
            + $"rows={rows.Count} in={rows.Count(r => r.Reason == In)} problems={problems.Count}");
        foreach (var reason in Reasons)
            _output.WriteLine($"CORPUS-PIN-INVENTORY reason {reason}={rows.Where(r => r.Reason == reason).Sum(r => r.Count)}");

        var dumpDir = Path.Combine(FixtureRoots.RepositoryRoot, ".claude", "tmp", "corpus-pin-inventory");
        Directory.CreateDirectory(dumpDir);
        var kept = rows.ToDictionary(r => (r.File, r.Text));
        File.WriteAllLines(Path.Combine(dumpDir, "actual-inventory.txt"), sweep.Hits.GroupBy(h => (h.File, h.Text))
            .OrderBy(g => g.Key.File, StringComparer.Ordinal).ThenBy(g => g.Key.Text, StringComparer.Ordinal)
            .Select(g => kept.TryGetValue(g.Key, out var r)
                ? (r with { Count = g.Count() }).ToString()
                : new InventoryRow(g.Key.File, g.Key.Text, g.Count(), "TODO", "TODO").ToString()));

        problems.Should().BeEmpty($"every integer pin in corpus-reading test code is a row of Conformance/{InventoryFileName} whose reason holds "
            + $"(actual rows: {Path.Combine(dumpDir, "actual-inventory.txt")})");
        rows.Where(r => r.Reason == In).Select(r => r.ToString()).Should().BeEmpty(
            "plan-88736c D3 converted every corpus-moved count pin to a set file: a count a new fixture can move is a set of named "
            + "rows, never an integer (an `in` row is a pin to convert, not an exclusion)");
    }

    // ---- the guard's own controls, over a synthetic corpus ----

    private const string ProbeFile = "Sharpy.Compiler.Tests/Probe/ProbeSweepTests.cs";

    /// <summary>A corpus-reading probe class holding one pin (the plant of the acceptance mutations).</summary>
    private static string ProbeSource(string pinLine) => string.Join("\n",
        "namespace Probe;",
        "public class ProbeSweepTests",
        "{",
        pinLine,
        "    public void Sweep()",
        "    {",
        "        var all = FixtureRoots.All;",
        "        all.Count.Should().Be(MaxProbeFixtures);",
        "    }",
        "}");

    private static readonly string ProbePin = "    private const int " + "MaxProbeFixtures = 7;";

    [Fact]
    public void Sweep_PlantedPin_IsUnlistedUntilItsRowExists()
    {
        var sweep = Sweep(new[] { (ProbeFile, ProbeSource(ProbePin)) });
        sweep.Hits.Should().ContainSingle().Which.Kind.Should().Be(Field);
        Compare(sweep, Array.Empty<InventoryRow>()).Should().ContainSingle().Which.Should().StartWith("unlisted hit: " + ProbeFile);

        var row = new InventoryRow(ProbeFile, ProbePin.Trim(), 1, "tuning-constant", "MaxProbeFixtures");
        Compare(sweep, new[] { row }).Should().BeEmpty();
        Compare(sweep, new[] { row, row with { Text = "int gone = 3;" } }).Should().ContainSingle().Which.Should().StartWith("row has no hit");
        Compare(sweep, new[] { row with { Count = 2 } }).Should().ContainSingle().Which.Should().StartWith("multiplicity");

        var doubled = Sweep(new[] { (ProbeFile, ProbeSource(ProbePin + "\n" + ProbePin)) });
        Compare(doubled, new[] { row }).Should().ContainSingle().Which.Should().Contain("an unlisted hit with the text of a listed one");

        // Every FluentAssertions count comparator is a hit form, the HaveCount* bounds included: a ceiling spelled
        // HaveCountLessThanOrEqualTo is the same count pin as BeLessThanOrEqualTo (plan-88736c verify-implementation).
        foreach (var comparator in new[] { "HaveCountLessThanOrEqualTo", "HaveCountLessThan", "HaveCountGreaterThanOrEqualTo", "HaveCountGreaterThan" })
        {
            var bound = ProbeSource("    void B() { FixtureRoots.All.Should()." + comparator + "(" + "59, \"rows\"); }");
            Sweep(new[] { (ProbeFile, bound) }).Hits.Should().ContainSingle(comparator + " is a count comparator")
                .Which.Comparator.Should().Be(comparator);
        }

        // The one comment-stripping rule: a corpus reader named only in a comment does not put the file in the sweep.
        var commentOnly = ProbeSource(ProbePin).Replace("var all = FixtureRoots.All;", "// FixtureRoots.All").Replace("all.Count", "0");
        Sweep(new[] { (ProbeFile, commentOnly) }).Swept.Should().BeEmpty();
    }

    /// <summary>The planted pin relabelled as each checked reason: every check refuses it, naming itself.</summary>
    [Theory]
    [InlineData("floor")]
    [InlineData("zero-guard")]
    [InlineData("drained-empty")]
    [InlineData("non-integer")]
    [InlineData("code-site-roster")]
    [InlineData("spec-file-roster")]
    [InlineData("generator-roster")]
    [InlineData("rate-tolerance")]
    [InlineData("set-file")]
    public void CheckedReason_RefusesThePlantedPin(string reason)
    {
        CheckedReasons.Should().Contain(reason);
        var sweep = Sweep(new[] { (ProbeFile, ProbeSource(ProbePin)) });
        var problems = Compare(sweep, new[] { new InventoryRow(ProbeFile, ProbePin.Trim(), 1, reason, "relabelled") });
        problems.Should().ContainSingle().Which.Should().Contain($"the {reason} check fails");
    }

    /// <summary>Every checked reason that holds on a true instance (the positive controls of the relabel theory).</summary>
    [Fact]
    public void CheckedReasons_HoldOnTheirOwnShapes()
    {
        string Probe(string member) => string.Join("\n", "namespace Probe;", "public class ProbeSweepTests", "{",
            "    private static readonly object Corpus = FixtureRoots.All;", member, "}");
        string? Check(string member, string reason, string? file = null, string? extra = null)
        {
            var files = new List<(string, string)> { (file ?? ProbeFile, Probe(member)) };
            if (extra is not null)
                files.Add(("Sharpy.Compiler.Tests/Probe/Reader.cs", extra));
            var sweep = Sweep(files);
            var hit = sweep.Hits.Should().ContainSingle().Subject;
            return CheckReason(hit, reason, "x", sweep);
        }

        Check("    void A() { n.Should().BeGreaterThan(" + "3); }", "floor").Should().BeNull();
        Check("    const int " + "Floor = 3;\n    void A() { Assert.True(n > Floor - 1); }", "floor").Should().BeNull();
        Check("    void A() { n.Should().BeGreaterThan(" + "0); }", "zero-guard").Should().BeNull();
        Check("    void A() { rows.Count.Should().Be(" + "0); }", "drained-empty").Should().BeNull();
        Check("    [S1] = new[] { Full, Line },", "non-integer").Should().BeNull();
        Check("    [(S6k, Full)] = new[] { " + "0, 0 },", "non-integer").Should().NotBeNull("a KnownLimitPin-shaped row holds integers");

        // A roster member names its non-corpus root; the field is read there only.
        var roster = string.Join("\n", "namespace Probe;", "public class RosterTests", "{",
            "    private static readonly string Root = Path.Combine(FixtureRoots.RepositoryRoot, \"src\", \"Sharpy.Compiler\");",
            "    private const int " + "Expected = 4;",
            "    void A() { Assert.Equal(Expected, Directory.GetFiles(Root).Length); }", "}");
        var rosterSweep = Sweep(new[] { ("Sharpy.Compiler.Tests/Probe/RosterTests.cs", roster) });
        CheckReason(rosterSweep.Hits.Single(), "code-site-roster", "x", rosterSweep).Should().BeNull();
        var spec = roster.Replace("\"src\", \"Sharpy.Compiler\"", "\"docs\", \"language_specification\"");
        var specSweep = Sweep(new[] { ("Sharpy.Compiler.Tests/Probe/RosterTests.cs", spec) });
        CheckReason(specSweep.Hits.Single(), "spec-file-roster", "x", specSweep).Should().BeNull();
        var generator = string.Join("\n", "namespace Probe;", "public class GenTests", "{",
            "    private static readonly string R = FixtureRoots.RepositoryRoot;", "    void A() { Assert.Equal(" + "114, Cells().Count()); }", "}");
        var generatorSweep = Sweep(new[] { ("Sharpy.Compiler.Tests/Probe/GenTests.cs", generator) });
        CheckReason(generatorSweep.Hits.Single(), "generator-roster", "x", generatorSweep).Should().BeNull();
        var rate = string.Join("\n", "namespace Probe;", "public class RateTests", "{", "    static readonly object C = FixtureRoots.All;",
            "    static readonly Dictionary<string, int> RefusalRatioBoundPercent = new()", "    {", "        [\"a\"] = " + "4,", "    };", "}");
        var rateSweep = Sweep(new[] { ("Sharpy.Compiler.Tests/Probe/RateTests.cs", rate) });
        CheckReason(rateSweep.Hits.Single(), "rate-tolerance", "x", rateSweep).Should().BeNull();
        var setSweep = Sweep(new[] { ("Sharpy.Lsp.Tests/Conformance/" + SetFiles[0], "s7-parity-exempt identity full a/b 2 # #2279\n") });
        CheckReason(setSweep.Hits.Single(), "set-file", "x", setSweep).Should().BeNull();
    }

    [Fact]
    public void Inventory_Grammar_RefusesMalformedRows()
    {
        ParseInventory(new[] { "A/B.cs | int x = 3; | ×2 | tuning-constant | X" }).Should().ContainSingle()
            .Which.Should().Be(new InventoryRow("A/B.cs", "int x = 3;", 2, "tuning-constant", "X"));
        ParseInventory(new[] { "A/B.cs | a | b; | ×1 | fixed-input | e" }).Single().Text.Should().Be("a | b;");
        foreach (var bad in new[]
                 {
                     "A/B.cs | int x = 3; | ×1 | tuning-constant",       // no evidence column
                     "A/B.cs | int x = 3; | 1 | tuning-constant | X",    // multiplicity without ×
                     "A/B.cs | int x = 3; | ×0 | tuning-constant | X",   // multiplicity 0
                     "A/B.cs | int x = 3; | ×1 | tuning | X",            // reason outside the closed set
                     "A/B.cs | int x = 3; | ×1 | fixed-input |  ",       // empty evidence
                 })
        {
            FluentActions.Invoking(() => ParseInventory(new[] { bad })).Should().Throw<InvalidOperationException>(bad);
        }

        FluentActions.Invoking(() => ParseInventory(new[] { "A/B.cs | t | ×1 | in | e", "A/B.cs | t | ×1 | in | f" }))
            .Should().Throw<InvalidOperationException>().WithMessage("*twice*");
    }
}

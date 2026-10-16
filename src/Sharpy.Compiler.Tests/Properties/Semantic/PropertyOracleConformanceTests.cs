using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// Source-scan backstop for the typed-generator refusal oracles (#2288, P63 Design Decision 7):
/// <c>ErrorInjector.JudgeSource</c>/<c>Judge</c> is the ONE refusal verdict, and the weak shapes
/// that made the property lanes pass vacuously — prefix judging, thresholds, pass-counter lower
/// bounds, any-failure counting, two-bucket sums, a <c>catch</c> that counts as a pass, and a second
/// judging core comparing diagnostic codes outside the seam — must not be re-typed.
/// <para>
/// <b>Universe</b>: every <c>*.cs</c> directly in <c>Properties/Semantic</c>, and every <c>*.cs</c> under
/// <c>src/Sharpy.Compiler.Tests/Properties/**</c> whose text
/// names the typed generator namespace, computed by the scan (no hand list), minus this scanner and the files inside
/// that namespace's own directory (<c>ErrorInjector</c> and <c>SemanticFilter</c> ARE the seam). A
/// universe under <see cref="MinimumUniverse"/> files fails loud — a broken path or marker would
/// otherwise scan nothing and pass. Comments are blanked (Roslyn trivia) before matching, so a
/// comment recording the old shape is not a hit; string literals are scanned (the codes live there).
/// </para>
/// <para>
/// <b>Deliberate exclusions</b> (each a benign look-alike in the positive control):
/// <c>tested &gt; 0</c> — the applicability floor every lane keeps (an inapplicable injection is red);
/// <c>=&gt; n / 2</c> — a lambda arrow is not a comparison; <c>.Code != null</c> / <c>== null</c> — a
/// null guard compares no code; <c>a.Code != b.Code</c> — two runs of the compiler compared with each
/// other (the determinism oracle), not a verdict against an expected code; <c>{d.Code}</c> — reading
/// a code to PRINT it; <c>.ExitCode != 0</c> — not a diagnostic code; <c>catch { return; }</c> /
/// <c>catch { throw …; }</c> — a catch that counts nothing.
/// </para>
/// <para>
/// <b>Not expressed</b>: the plan's "<c>new Compiler().Analyze(</c> outside the seam" spelling — every
/// soundness lane analyses its accepted program that way, so the call alone cannot be banned without
/// banning the accepted-program lane; a second core is caught where it JUDGES, by
/// <see cref="CodeCompare"/>.
/// </para>
/// <para>
/// <b>Allowlist</b>: one literal row per (file, spelling) with the exact current count, each citing
/// an issue; a hit the rows do not cover is red, a row whose count exceeds its hits is red (it must
/// shrink as the lane is migrated), a row naming a file outside the universe is red. The P63 lanes
/// drain it to zero.
/// </para>
/// </summary>
public class PropertyOracleConformanceTests
{
    private const int MinimumUniverse = 20;

    private const string Prefix = "prefix";
    private const string Threshold = "threshold";
    private const string LowerBound = "lower-bound";
    private const string AnyFailure = "any-failure";
    private const string TwoBucket = "two-bucket";
    private const string CatchIncrement = "catch-increment";
    private const string CodeCompare = "code-compare";

    private static readonly (string Id, Regex Pattern)[] BannedPatterns =
    {
        // Prefix judging: a code family is not a code.
        (Prefix, new Regex(@"StartsWith\(\s*""SPY", RegexOptions.Compiled)),
        // Thresholds: `> total / 4`, `> tested / 2`, `>= analyzed / 2`, `> resolved / 2` … — a
        // fraction of the samples may fail. The look-behind keeps `=>` (lambda) and `->` out.
        (Threshold, new Regex(
            @"(?<![=\-])>=?\s*(?:(?:total|tested|analyzed|passed)\s*/|[A-Za-z_]\w*\s*/\s*\d)",
            RegexOptions.Compiled)),
        // Lower bounds on a pass counter: one success anywhere satisfies it. `tested > 0` is
        // deliberately NOT here — it is the applicability floor.
        (LowerBound, new Regex(@"\b(?:passed|diagnosed|rejected|caught)\s*>\s*0\b", RegexOptions.Compiled)),
        // Any-failure counting: a refusal for an unrelated reason passes for the one under test.
        (AnyFailure, new Regex(@"!\s*\w+\.Success\s*\|\|", RegexOptions.Compiled)),
        // Two-bucket sums: one accepted VALID program satisfies the refusal lane.
        (TwoBucket, new Regex(@"\b[A-Za-z_]\w*\s*\+\s*[A-Za-z_]\w*\s*>\s*0\b", RegexOptions.Compiled)),
        // A second judging core: a diagnostic code compared outside the seam. Excludes the null
        // guard and code-vs-code comparison of two runs (see the class comment).
        (CodeCompare, new Regex(
            @"\.Code\s*(?:==|!=)(?>\s*)(?!null\b)(?![\w\[\]\.]*\.Code\b)|\.Code\??\.(?:StartsWith|EndsWith|Contains|Equals)\(|\.Code\s+is\s+(?:not\s+)?""",
            RegexOptions.Compiled)),
    };

    private static readonly string[] AllIds =
        { Prefix, Threshold, LowerBound, AnyFailure, TwoBucket, CatchIncrement, CodeCompare };

    /// <summary>
    /// Every current hit, by (file relative to <c>Properties/</c>, spelling) with its exact count,
    /// seeded @ 8e0791f85 (34 rows, 64 hits) and drained by P63 Phase 2 Task 3 and Phase 3. The
    /// <see cref="AnyFailure"/> rows of the accepted-program lanes (<c>CsClean</c>,
    /// <c>SemanticInfoConsistency</c>, <c>SymbolPosition</c>) are silent skips of a refused sample —
    /// the filtered-soundness shape Phase 3's sink asserts; <c>DecoratorPropertyTests</c>' exact-code
    /// comparison is a strong oracle but a second judging core, so it moves onto the seam too.
    /// </summary>
    private static readonly (string File, string SpellingId, int Count, string Issue)[] Allowlist =
    {
        ("Semantic/ClassInheritancePropertyTests.cs", LowerBound, 1, "#2288"),
        ("Semantic/CombinedFeaturePropertyTests.cs", Threshold, 4, "#2288"),
        ("Semantic/ExceptionPropertyTests.cs", CatchIncrement, 1, "#2288"),
        ("Semantic/ExceptionPropertyTests.cs", Threshold, 1, "#2288"),
        ("Semantic/FunctionSemanticsPropertyTests.cs", Threshold, 4, "#2288"),
        ("Semantic/GenericTypePropertyTests.cs", Threshold, 5, "#2288"),
        ("Semantic/ImportResolutionPropertyTests.cs", Threshold, 3, "#2288"),
        ("Semantic/IteratorPropertyTests.cs", Threshold, 3, "#2288"),
    };

    private readonly ITestOutputHelper _output;

    public PropertyOracleConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PropertyLanes_UseNoWeakRefusalOracle_BeyondTheAllowlist()
    {
        var propertiesRoot = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler.Tests", "Properties");
        var universe = Universe(propertiesRoot);
        _output.WriteLine($"universe {universe.Count} files under {propertiesRoot}");
        Assert.True(universe.Count >= MinimumUniverse,
            $"The typed-generator consumer universe has {universe.Count} files (< {MinimumUniverse}): the scan "
            + "lost its root or its namespace marker and would pass vacuously.");

        var hits = universe
            .SelectMany(file => Scan(File.ReadAllText(Path.Combine(propertiesRoot, file)))
                .Select(hit => (File: file, hit.Id, hit.Line, hit.Text)))
            .ToList();
        foreach (var hit in hits)
            _output.WriteLine($"  hit {hit.File}:{hit.Line} [{hit.Id}] {hit.Text}");

        var actual = hits
            .GroupBy(h => (h.File, h.Id))
            .ToDictionary(g => g.Key, g => g.Count());
        var failures = new List<string>();

        foreach (var row in Allowlist)
        {
            if (!universe.Contains(row.File))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) names a file outside the universe");
            if (!AllIds.Contains(row.SpellingId))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) names an unknown spelling id");
            if (!Regex.IsMatch(row.Issue, @"^#\d+$"))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) cites '{row.Issue}', not an issue number");
            var have = actual.GetValueOrDefault((row.File, row.SpellingId));
            if (row.Count > have)
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) allows {row.Count} but the file has {have} "
                    + "— the row is stale: shrink it (delete it at 0)");
        }

        foreach (var group in Allowlist.GroupBy(r => (r.File, r.SpellingId)).Where(g => g.Count() > 1))
            failures.Add($"allowlist has {group.Count()} rows for ({group.Key.File}, {group.Key.SpellingId})");

        var allowed = Allowlist
            .GroupBy(r => (r.File, r.SpellingId))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));
        foreach (var ((file, id), count) in actual.OrderBy(kv => kv.Key.File, StringComparer.Ordinal))
        {
            var allowedCount = allowed.GetValueOrDefault((file, id));
            if (count > allowedCount)
            {
                var lines = string.Join(", ", hits.Where(h => h.File == file && h.Id == id).Select(h => h.Line));
                failures.Add($"{file} has {count} [{id}] hit(s) (lines {lines}), the allowlist covers {allowedCount} — "
                    + "judge the lane through ErrorInjector.JudgeSource/Judge instead");
            }
        }

        _output.WriteLine($"allowlist rows {Allowlist.Length}, hits {hits.Count}");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Positive control: each banned spelling once → exactly that id; each benign look-alike → nothing.
    /// A scanner whose patterns no longer match the real spellings fails here, not silently.
    /// </summary>
    [Fact]
    public void Scanner_FindsEachBannedSpelling_AndNoBenignLookAlike()
    {
        const string text = """
            class Probe
            {
                void Banned()
                {
                    ok = name.StartsWith("SPY0");
                    Assert.True(diagnosed > total / 4, "");
                    Assert.True(passed > tested / 2, "");
                    Assert.True(analyzed > analyzed / 2, "");
                    Assert.True(resolved > passed / 2, "");
                    Assert.True(matched >= tested / 2, "");
                    Assert.True(passed > 0, "");
                    Assert.True(caught > 0, "");
                    if (!result.Success || other) { }
                    Assert.True(insidePassed + outsideDiagnosed > 0, "");
                    try { Run(); } catch { Interlocked.Increment(ref diagnosedCount); }
                    try { Run(); } catch (Exception) { passedCount++; }
                    ok = d.Code == "SPY0273";
                    ok = d.Code != expectedCode;
                    ok = d.Code.Contains("02");
                    ok = d.Code?.EndsWith("20") == true;
                    ok = d.Code is "SPY0463";
                }

                void Benign()
                {
                    Assert.True(tested > 0, "No injections were applicable");
                    // Assert.True(passed > tested / 2) — a comment recording the old shape
                    /* if (!result.Success || other) { } */
                    var half = items.Select(n => n / 2);
                    ok = d.Code != null && d.Code == null;
                    ok = first[i].Code != second[i].Code;
                    output.WriteLine($"drew {d.Code}");
                    ok = proc.ExitCode != 0;
                    ok = result.Success && other;
                    ok = count + 1 > 0;
                    try { Run(); } catch { return; }
                    try { Run(); } catch (Exception e) { throw new Exception("ICE", e); }
                }
            }
            """;

        var hits = Scan(text);
        foreach (var hit in hits)
            _output.WriteLine($"  hit line {hit.Line} [{hit.Id}] {hit.Text}");

        var byId = hits.GroupBy(h => h.Id).ToDictionary(g => g.Key, g => g.Count());
        var expected = new Dictionary<string, int>
        {
            [Prefix] = 1,
            [Threshold] = 5,
            [LowerBound] = 2,
            [AnyFailure] = 1,
            [TwoBucket] = 1,
            [CatchIncrement] = 2,
            [CodeCompare] = 5,
        };
        Assert.Equal(AllIds.OrderBy(i => i), expected.Keys.OrderBy(i => i));
        Assert.Equal(
            expected.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"),
            byId.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        // Every hit is in Banned(): the benign half produced none.
        var benignStart = text.Split('\n').ToList().FindIndex(l => l.Contains("void Benign()")) + 1;
        Assert.All(hits, hit => Assert.True(hit.Line < benignStart, $"benign line {hit.Line} hit [{hit.Id}]: {hit.Text}"));
        Assert.Equal(17, hits.Count);
    }

    /// <summary>
    /// Every <c>*.cs</c> under <paramref name="propertiesRoot"/> naming the typed generator
    /// namespace, outside that namespace's own directory, plus every file of <c>Properties/Semantic</c>
    /// whether or not it uses the typed generator (<c>CombinedFeaturePropertyTests</c> builds its own
    /// programs and judged them with <c>passed &gt; total / 4</c>) — minus this scanner, whose
    /// positive-control literals ARE the banned spellings. Paths relative to the root, '/'-separated.
    /// </summary>
    private const string SelfFileName = "PropertyOracleConformanceTests.cs";

    private static HashSet<string> Universe(string propertiesRoot)
    {
        var seamDir = Path.Combine(propertiesRoot, "Generators", "Typed") + Path.DirectorySeparatorChar;
        var semanticDir = Path.Combine(propertiesRoot, "Semantic") + Path.DirectorySeparatorChar;
        var marker = new Regex(@"\bGenerators\.Typed\b");
        return Directory.EnumerateFiles(propertiesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(seamDir, StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path) != SelfFileName)
            .Where(path => path.StartsWith(semanticDir, StringComparison.Ordinal)
                || marker.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(propertiesRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static List<(string Id, int Line, string Text)> Scan(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        // Blank every comment (keep newlines) so line numbers survive and comments never hit.
        var chars = source.ToCharArray();
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                continue;
            for (int i = trivia.FullSpan.Start; i < trivia.FullSpan.End; i++)
            {
                if (chars[i] is not ('\n' or '\r'))
                    chars[i] = ' ';
            }
        }

        var lines = new string(chars).Split('\n');
        var hits = new List<(string Id, int Line, string Text)>();
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (var (id, pattern) in BannedPatterns)
            {
                foreach (Match _ in pattern.Matches(lines[i]))
                    hits.Add((id, i + 1, lines[i].Trim()));
            }
        }

        // A catch whose body counts: an exception passes for the outcome the counter records.
        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            var increments = catchClause.Block.DescendantNodes().Count(IsIncrement);
            var line = tree.GetLineSpan(catchClause.Span).StartLinePosition.Line;
            for (int k = 0; k < increments; k++)
                hits.Add((CatchIncrement, line + 1, lines[line].Trim()));
        }

        return hits;
    }

    private static bool IsIncrement(SyntaxNode node) => node switch
    {
        PostfixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PostIncrementExpression),
        PrefixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PreIncrementExpression),
        AssignmentExpressionSyntax a => a.IsKind(SyntaxKind.AddAssignmentExpression),
        InvocationExpressionSyntax inv => inv.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Increment" or "Add",
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Interlocked" },
        },
        _ => false,
    };
}

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// Source-scan backstop for the typed-generator refusal oracles (#2288, P63 Design Decision 7):
/// <c>ErrorInjector.JudgeSource</c>/<c>Judge</c> is the ONE refusal verdict, and the weak shapes
/// that made the property lanes pass vacuously — prefix judging, thresholds, pass-counter lower
/// bounds, any-failure counting, two-bucket sums, a <c>catch</c> that counts as a pass or swallows a
/// throw, a <c>HasErrors</c> verdict, and a second judging core comparing diagnostic codes outside the
/// seam — must not be re-typed.
/// <para>
/// <b>Universe</b>: every <c>*.cs</c> directly in <c>Properties/Semantic</c>, and every <c>*.cs</c> under
/// <c>src/Sharpy.Compiler.Tests/Properties/**</c> whose text
/// names the typed generator namespace, computed by the scan (no hand list), minus this scanner and the files inside
/// that namespace's own directory (<c>ErrorInjector</c> and <c>SemanticFilter</c> ARE the seam). A
/// universe under <see cref="MinimumUniverse"/> files fails loud — a broken path or marker would
/// otherwise scan nothing and pass.
/// </para>
/// <para>
/// <b>Unit</b>: the banned regexes run over the file's WHITESPACE-NORMALIZED token stream (Roslyn: every
/// run of trivia — spaces, newlines, comments — collapses to one space), so a spelling split over
/// lines (<c>!r.Success</c> / <c>|| …</c>) is one statement's text, and a comment is never a hit. A hit
/// is reported at the line of its first token with its enclosing statement. String literals are
/// scanned (the codes live there). The syntax rules (lower bounds, catches, <c>HasErrors</c>, code
/// literals) read the tree itself.
/// </para>
/// <para>
/// <b>Deliberate exclusions</b> (each a benign look-alike in the positive control):
/// <c>Assert.True(tested &gt; 0)</c> — a lower bound is banned only on a counter that is not an
/// applicability counter: the exempt names are <see cref="ApplicabilityCounters"/> (a counter, or the
/// receiver of its <c>.Count</c>/<c>.Length</c>, whose name ends in one of them counts samples that
/// REACHED the oracle, never samples that passed it); <c>=&gt; n / 2</c> — a lambda arrow is not a
/// comparison; <c>.Code != null</c> / <c>== null</c> — a null guard compares no code;
/// <c>a.Code != b.Code</c> — two runs of the compiler compared with each other (the determinism
/// oracle), not a verdict against an expected code; <c>{d.Code}</c> — reading a code to PRINT it;
/// <c>.ExitCode != 0</c> — not a diagnostic code; a code literal passed to the seam
/// (<c>JudgeSource(src, "SPY0273", …)</c>, <c>new InjectionResult(m, "SPY0220")</c>);
/// <c>Assert.False(x.HasErrors)</c> — an acceptance check; <c>if (errors.Count &gt; 0)</c> — a failure
/// check outside an assertion; a <c>catch</c> that RECORDS (a throw, an <c>Assert</c>, a call named
/// one of <see cref="RecordVerbs"/>, or a return/assignment of a message string or of the caught
/// exception); a <c>catch</c> whose <c>try</c> never reaches the pipeline under test
/// (<see cref="PipelineCalls"/>/<see cref="PipelineTypes"/> — e.g. killing the CPython oracle's
/// process).
/// </para>
/// <para>
/// <b>Not expressed</b>: the plan's "<c>new Compiler().Analyze(</c> outside the seam" spelling — every
/// soundness lane analyses its accepted program that way, so the call alone cannot be banned without
/// banning the accepted-program lane; a second core is caught where it JUDGES, by
/// <see cref="CodeCompare"/> and <see cref="CodeLiteral"/>. A filter of an UNTYPED generator's refusals
/// (<c>if (!result.Success) return;</c> over <c>GenSharpy</c>) is not a refusal verdict and is not banned.
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
    private const string CodeLiteral = "code-literal";
    private const string HasErrorsVerdict = "has-errors";
    private const string SwallowCatch = "swallow-catch";

    private static readonly (string Id, Regex Pattern)[] BannedPatterns =
    {
        // Prefix judging: a code family is not a code.
        (Prefix, new Regex(@"StartsWith\(\s*""SPY", RegexOptions.Compiled)),
        // Thresholds: `> total / 4`, `> tested / 2`, `>= analyzed / 2`, `> (total / 2)`, `passed * 2 > total`,
        // `total / 2 < passed` — a fraction of the samples may fail: a division by a sample count or a
        // multiplied counter compared with a COUNTER (`x * 2) > 2` and a relative tolerance
        // `/ av) <= RelTol` are not). The look-behind keeps `=>` (lambda), `->` and shifts out.
        (Threshold, new Regex(
            @"(?<![=\-<>])>(?!>)=?\s*\(?\s*[A-Za-z_][\w\.]*\s*/\s*[\w\.]+"
            + @"|\b[A-Za-z_][\w\.]*\s*\*\s*\d+\s*\)?\s*>(?!>)=?\s*\(?\s*[A-Za-z_]"
            + @"|\b\d+\s*\*\s*[A-Za-z_][\w\.]*\s*\)?\s*>(?!>)=?\s*\(?\s*[A-Za-z_]"
            + @"|[\w\.\)]\s*/\s*\d+\s*\)?\s*<(?![<])=?\s*\(?\s*[A-Za-z_]",
            RegexOptions.Compiled)),
        // Lower bounds on a pass counter outside an assertion (inside one, the syntax rule judges every
        // counter): one success anywhere satisfies it.
        (LowerBound, new Regex(@"\b(?:passed|diagnosed|rejected|caught)\s*>\s*0\b", RegexOptions.Compiled)),
        // Any-failure counting: a refusal for an unrelated reason passes for the one under test.
        (AnyFailure, new Regex(@"!\s*[A-Za-z_][\w\.]*\.Success\s*\|\|", RegexOptions.Compiled)),
        // Two-bucket sums: one accepted VALID program satisfies the refusal lane.
        (TwoBucket, new Regex(@"\b[A-Za-z_]\w*\s*\+\s*[A-Za-z_]\w*\s*>\s*0\b", RegexOptions.Compiled)),
        // A second judging core: a diagnostic code compared outside the seam. Excludes the null
        // guard and code-vs-code comparison of two runs (see the class comment).
        (CodeCompare, new Regex(
            @"\.Code\s*(?:==|!=)(?>\s*)(?!null\b)(?![\w\[\]\.]*\.Code\b)|\.Code\??\.(?:StartsWith|EndsWith|Contains|Equals)\(|\.Code\s+is\s+(?:not\s+)?""",
            RegexOptions.Compiled)),
    };

    private static readonly string[] AllIds =
    {
        Prefix, Threshold, LowerBound, AnyFailure, TwoBucket, CatchIncrement, CodeCompare, CodeLiteral,
        HasErrorsVerdict, SwallowCatch,
    };

    /// <summary>Counters a lower bound may floor: each counts samples that REACHED the oracle (or the
    /// corpus a sweep compares), never samples that passed it — the applicability floor every lane
    /// keeps. Matched case-insensitively as the counter's name or its suffix (<c>verdicts.Tested</c>,
    /// <c>superCallsTested</c>, <c>programs.Count</c>).</summary>
    private static readonly string[] ApplicabilityCounters =
        { "tested", "total", "admitted", "compared", "injected", "programs" };

    /// <summary>A diagnostic-code literal passed to one of these is a code compared outside the seam.</summary>
    private static readonly HashSet<string> CodeComparingCalls = new(StringComparer.Ordinal)
    {
        "Equal", "NotEqual", "Contains", "DoesNotContain", "Equals", "StartsWith", "EndsWith", "Single",
        "SingleOrDefault", "First", "FirstOrDefault", "Any", "All", "Count", "Where", "IndexOf", "SequenceEqual",
        "Remove", "Same",
    };

    /// <summary>A <c>try</c> that calls one of these (or constructs a <see cref="PipelineTypes"/>) reaches
    /// the pipeline under test, so its <c>catch</c> must record what it caught.</summary>
    private static readonly HashSet<string> PipelineCalls = new(StringComparer.Ordinal)
    {
        "Analyze", "Compile", "CompileAndExecute", "CompileToCSharp", "Unparse", "ParseModule", "TokenizeAll",
        "Validate", "Judge", "JudgeSource", "JudgeProject",
    };

    private static readonly HashSet<string> PipelineTypes = new(StringComparer.Ordinal)
    {
        "Compiler", "Parser", "Lexer", "ControlFlowGraphBuilder", "ProjectCompilationHelper",
    };

    /// <summary>A call named one of these inside a <c>catch</c> records what was caught (a failure list,
    /// a sink, a sweep's exclusion counter).</summary>
    private static readonly HashSet<string> RecordVerbs = new(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Enqueue", "TryAdd", "Reject", "Exclude", "Fail", "Record",
    };

    /// <summary>
    /// Every current hit, by (file relative to <c>Properties/</c>, spelling) with its exact count.
    /// Seeded @ 8e0791f85 (34 rows, 64 hits) and drained to ZERO by P63: Phase 2 Task 3 moved every
    /// refusal lane onto <c>ErrorInjector.JudgeSource</c>/<c>Judge</c>, Phase 3 turned every threshold
    /// soundness lane and every skip of a refused sample into a <c>RejectionSink</c> assertion (#2288).
    /// A new row needs an issue and drains on fix, like every ratchet.
    /// </summary>
    private static readonly (string File, string SpellingId, int Count, string Issue)[] Allowlist =
    {
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
    /// A scanner whose patterns no longer match the real spellings fails here, not silently. The second
    /// half of <c>Banned()</c> is the verify round's evasions (#2288 F1): a spelling split over lines,
    /// parenthesized and multiplied thresholds, lower bounds on any counter, code literals compared
    /// outside the seam, a <c>HasErrors</c> verdict, and swallowing catches.
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

                void Evasions()
                {
                    if (!analysis.Success
                        || analysis.Module is null)
                        return;
                    Assert.True(diagnosed
                        > total / 4, "");
                    Assert.True(passed > (total / 2), "");
                    Assert.True(passed * 2 > total, "");
                    Assert.True(total / 2 < passed, "");
                    Assert.True(handled > 0, "");
                    Assert.True(diagnosed >= 1, "");
                    Assert.True(diagnosed != 0, "");
                    Assert.NotEmpty(refusedSamples);
                    if (!result.Success) Interlocked.Increment(ref diagnosed);
                    Assert.Equal("SPY0273", diagnostics.First().Code);
                    ok = codes.Contains("SPY0273");
                    Assert.Contains("SPY0273", diagnostics.Select(d => d.Code));
                    ok = string.Equals(d.Code, "SPY0273");
                    ok = codes[0] == "SPY0273";
                    if (result.Diagnostics.HasErrors) diagnosed++;
                    Assert.True(result.Diagnostics.HasErrors, "");
                    try { new Sharpy.Compiler.Compiler().Analyze(source, "a.spy"); } catch { }
                    try { Analyze(source); } catch { return null; }
                    try { var r = compiler.Compile(source, "a.spy"); } catch (Exception) { }
                    try { text = Unparser.Unparse(module); } catch { return (false, -1); }
                }

                void Benign()
                {
                    Assert.True(tested > 0, "No injections were applicable");
                    Assert.True(verdicts.Tested > 0, "");
                    Assert.True(admitted > 0, "");
                    Assert.True(superCallsTested >= 1, "");
                    Assert.True(programs.Count > 0, "");
                    Assert.True(compared == total, "");
                    // Assert.True(passed > tested / 2) — a comment recording the old shape
                    /* if (!result.Success || other) { } */
                    var half = items.Select(n => n / 2);
                    ok = d.Code != null && d.Code == null;
                    ok = first[i].Code != second[i].Code;
                    output.WriteLine($"drew {d.Code}");
                    ok = proc.ExitCode != 0;
                    ok = result.Success && other;
                    ok = count + 1 > 0;
                    if (errors.Count > 0) throw new Exception("errors");
                    if (!result.Success) return;
                    verdict = ErrorInjector.JudgeSource(source, "SPY0273", CompilerDiagnosticSeverity.Error, null, "x");
                    var injection = new ErrorInjector.InjectionResult(mutated, "SPY0220");
                    Assert.False(parser.Diagnostics.HasErrors, "");
                    if (parser.Diagnostics.HasErrors) { Exclude("parse-errors"); continue; }
                    try { Run(); } catch { return; }
                    try { Run(); } catch (Exception e) { throw new Exception("ICE", e); }
                    try { proc.Kill(); } catch { }
                    try { Analyze(source); } catch (Exception e) { failures.Add($"threw {e}"); }
                    try { Analyze(source); } catch (Exception e) { return $"threw {e.GetType().Name}"; }
                    try { Analyze(source); } catch (Exception e) { failure = $"threw {e.GetType().Name}"; }
                    try { Analyze(source); } catch (CfgInvariantException) { throw; }
                    try { ParseModule(source); } catch { Exclude("parse-errors"); continue; }
                    var walrus = "def main():\n    ys = [y for x in [1, 2] if (y := x * 2) > 2]\n";
                    ok = Math.Abs((av - bv) / av) <= RelTol;
                    if (parser.Diagnostics.HasErrors) { pinHits[pin]++; throw new Exception("unpinned"); }
                    if (!result.Success) { failures++; Assert.Fail("refused"); }
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
            [Threshold] = 5 + 4,
            [LowerBound] = 2 + 4,
            [AnyFailure] = 1 + 2,
            [TwoBucket] = 1,
            [CatchIncrement] = 2,
            [CodeCompare] = 5,
            [CodeLiteral] = 5,
            [HasErrorsVerdict] = 2,
            [SwallowCatch] = 4,
        };
        Assert.Equal(AllIds.OrderBy(i => i), expected.Keys.OrderBy(i => i));
        Assert.Equal(
            expected.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"),
            byId.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

        // Every hit is in Banned() or Evasions(): the benign half produced none.
        var benignStart = text.Split('\n').ToList().FindIndex(l => l.Contains("void Benign()")) + 1;
        Assert.All(hits, hit => Assert.True(hit.Line < benignStart, $"benign line {hit.Line} hit [{hit.Id}]: {hit.Text}"));
        Assert.Equal(38, hits.Count);

        // The split spellings are seen, each reported at the line of the match's first token.
        var lines = text.Split('\n').ToList();
        var splitAnyFailure = lines.FindIndex(l => l.Contains("if (!analysis.Success")) + 1;
        var splitThreshold = lines.FindIndex(l => l.TrimStart().StartsWith("> total / 4", StringComparison.Ordinal)) + 1;
        Assert.Contains(hits, h => h.Id == AnyFailure && h.Line == splitAnyFailure);
        Assert.Contains(hits, h => h.Id == Threshold && h.Line == splitThreshold);
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
        var found = new Dictionary<(string Id, int Position), SyntaxNode?>();

        void Hit(string id, int position, SyntaxNode? node = null) => found.TryAdd((id, position), node);

        // The regexes read the whitespace-normalized token stream: every trivia run (whitespace,
        // newlines, comments) is one space, so a spelling split over lines is seen and a comment never is.
        var (normalized, origin) = Normalize(root);
        foreach (var (id, pattern) in BannedPatterns)
        {
            foreach (Match match in pattern.Matches(normalized))
                Hit(id, origin[match.Index]);
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" },
                    Name.Identifier.ValueText: var assertion,
                })
            {
                // A lower bound inside an assertion, on any counter but an applicability counter.
                foreach (var comparison in invocation.ArgumentList.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (LowerBoundOperand(comparison) is { } operand && !IsApplicabilityCounter(operand))
                        Hit(LowerBound, operand.SpanStart, comparison);
                }

                if (assertion == "NotEmpty" && invocation.ArgumentList.Arguments is [var only]
                    && !IsApplicabilityCounter(only.Expression))
                    Hit(LowerBound, only.Expression.SpanStart, invocation);

                // "Some error was reported" asserted as the verdict.
                if (assertion == "True")
                {
                    foreach (var hasErrors in HasErrorsReads(invocation.ArgumentList))
                        Hit(HasErrorsVerdict, hasErrors.SpanStart, invocation);
                }
            }

            // A diagnostic-code literal handed to a comparison outside the seam.
            if (InvokedName(invocation) is { } name && CodeComparingCalls.Contains(name))
            {
                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    if (IsCodeLiteral(argument.Expression))
                        Hit(CodeLiteral, argument.Expression.SpanStart, invocation);
                }
            }
        }

        // `codes[0] == "SPY0273"` — a code literal compared with something that is not `.Code` (that is
        // CodeCompare's spelling).
        foreach (var equality in root.DescendantNodes().OfType<BinaryExpressionSyntax>()
                     .Where(b => b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression)))
        {
            var (literal, other) = IsCodeLiteral(equality.Right) ? (equality.Right, equality.Left)
                : IsCodeLiteral(equality.Left) ? (equality.Left, equality.Right)
                : (null, null);
            if (literal is not null && other is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Code" }
                && other is not ConditionalAccessExpressionSyntax)
                Hit(CodeLiteral, literal.SpanStart, equality);
        }

        // Counting by a verdict that admits any failure: `if (!r.Success) count++`, `if (d.HasErrors) count++`.
        // A branch that also throws or asserts judges the failure itself; its counter only tallies.
        foreach (var ifStatement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            var branch = ifStatement.Statement.DescendantNodesAndSelf().ToList();
            if (!branch.Any(IsIncrement) || branch.Any(n => n is ThrowStatementSyntax or ThrowExpressionSyntax
                    || (n is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" },
                    })))
                continue;
            foreach (var not in ifStatement.Condition.DescendantNodesAndSelf().OfType<PrefixUnaryExpressionSyntax>()
                         .Where(p => p.IsKind(SyntaxKind.LogicalNotExpression)
                             && p.Operand is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Success" }))
                Hit(AnyFailure, not.SpanStart, ifStatement);
            foreach (var hasErrors in HasErrorsReads(ifStatement.Condition))
                Hit(HasErrorsVerdict, hasErrors.SpanStart, ifStatement);
        }

        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            // A catch whose body counts: an exception passes for the outcome the counter records.
            var increments = catchClause.Block.DescendantNodes().Where(IsIncrement).ToList();
            foreach (var increment in increments)
                Hit(CatchIncrement, increment.SpanStart, catchClause);

            // A catch that swallows what the pipeline under test threw: no record, no rethrow, no assert.
            if (increments.Count == 0 && catchClause.Parent is TryStatementSyntax tryStatement
                && ReachesPipeline(tryStatement.Block) && !Records(catchClause))
                Hit(SwallowCatch, catchClause.SpanStart, catchClause);
        }

        return found
            .OrderBy(kv => kv.Key.Position)
            .ThenBy(kv => kv.Key.Id, StringComparer.Ordinal)
            .Select(kv => (kv.Key.Id, tree.GetLineSpan(new TextSpan(kv.Key.Position, 0)).StartLinePosition.Line + 1,
                Describe(kv.Value ?? EnclosingStatement(root, kv.Key.Position))))
            .ToList();
    }

    /// <summary>The token stream with every trivia run collapsed to one space, and for each character
    /// of it the source position it came from.</summary>
    private static (string Text, int[] Origin) Normalize(SyntaxNode root)
    {
        var text = new StringBuilder();
        var origin = new List<int>();
        var previousHadTrailing = false;
        foreach (var token in root.DescendantTokens())
        {
            if (text.Length > 0 && (previousHadTrailing || token.HasLeadingTrivia))
            {
                text.Append(' ');
                origin.Add(token.SpanStart);
            }

            var tokenText = token.Text;
            for (int k = 0; k < tokenText.Length; k++)
            {
                text.Append(tokenText[k]);
                origin.Add(token.SpanStart + k);
            }

            previousHadTrailing = token.HasTrailingTrivia;
        }

        return (text.ToString(), origin.ToArray());
    }

    private static SyntaxNode? EnclosingStatement(SyntaxNode root, int position) =>
        root.FindToken(position).Parent?.AncestorsAndSelf().FirstOrDefault(n => n is StatementSyntax or CatchClauseSyntax);

    private static string Describe(SyntaxNode? node)
    {
        if (node is null)
            return "";
        var text = Regex.Replace(node.WithoutTrivia().ToString(), @"\s+", " ");
        return text.Length <= 160 ? text : text[..160] + "…";
    }

    /// <summary>The counter side of <c>x &gt; 0</c>, <c>x &gt;= 1</c>, <c>x != 0</c>, <c>0 &lt; x</c>,
    /// <c>1 &lt;= x</c>, <c>0 != x</c>; null when the comparison is not a lower bound.</summary>
    private static ExpressionSyntax? LowerBoundOperand(BinaryExpressionSyntax b)
    {
        static bool Is(ExpressionSyntax e, int value) =>
            e is LiteralExpressionSyntax { Token.Value: int v } && v == value;

        var operand = b.Kind() switch
        {
            SyntaxKind.GreaterThanExpression when Is(b.Right, 0) => b.Left,
            SyntaxKind.GreaterThanOrEqualExpression when Is(b.Right, 1) => b.Left,
            SyntaxKind.NotEqualsExpression when Is(b.Right, 0) => b.Left,
            SyntaxKind.LessThanExpression when Is(b.Left, 0) => b.Right,
            SyntaxKind.LessThanOrEqualExpression when Is(b.Left, 1) => b.Right,
            SyntaxKind.NotEqualsExpression when Is(b.Left, 0) => b.Right,
            _ => null,
        };
        // A sum is TwoBucket's spelling; a lower bound floors one counter.
        return operand is IdentifierNameSyntax or MemberAccessExpressionSyntax ? operand : null;
    }

    /// <summary>The counter's name — the identifier, the member name, or for <c>.Count</c>/<c>.Length</c>
    /// the receiver's — ends (case-insensitively) in one of <see cref="ApplicabilityCounters"/>.</summary>
    private static bool IsApplicabilityCounter(ExpressionSyntax operand)
    {
        var name = operand switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Count" or "Length", Expression: var receiver } =>
                receiver switch
                {
                    IdentifierNameSyntax rid => rid.Identifier.ValueText,
                    MemberAccessExpressionSyntax rma => rma.Name.Identifier.ValueText,
                    _ => "",
                },
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            _ => "",
        };
        return ApplicabilityCounters.Any(c => name.EndsWith(c, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<SyntaxNode> HasErrorsReads(SyntaxNode scope) =>
        scope.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
            .Where(m => m.Name.Identifier.ValueText == "HasErrors");

    private static bool IsCodeLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax { Token.Value: string s } && Regex.IsMatch(s, @"^SPY\d{4}$");

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText,
        _ => null,
    };

    private static bool ReachesPipeline(BlockSyntax tryBlock) =>
        tryBlock.DescendantNodes().Any(n => n switch
        {
            InvocationExpressionSyntax inv => InvokedName(inv) is { } name && PipelineCalls.Contains(name),
            ObjectCreationExpressionSyntax creation => creation.Type switch
            {
                IdentifierNameSyntax id => PipelineTypes.Contains(id.Identifier.ValueText),
                QualifiedNameSyntax q => PipelineTypes.Contains(q.Right.Identifier.ValueText),
                _ => false,
            },
            _ => false,
        });

    /// <summary>The catch body rethrows, asserts, calls a <see cref="RecordVerbs"/> member, or returns or
    /// assigns a message string or the caught exception.</summary>
    private static bool Records(CatchClauseSyntax catchClause)
    {
        var exception = catchClause.Declaration?.Identifier.ValueText;

        bool Carries(ExpressionSyntax? e) => e is not null && (
            e is InterpolatedStringExpressionSyntax
            || e is LiteralExpressionSyntax { Token.Value: string }
            || (exception is { Length: > 0 } && e.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(id => id.Identifier.ValueText == exception)));

        return catchClause.Block.DescendantNodes().Any(n => n switch
        {
            ThrowStatementSyntax or ThrowExpressionSyntax => true,
            InvocationExpressionSyntax inv =>
                (inv.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" } })
                || (InvokedName(inv) is { } name && RecordVerbs.Contains(name)),
            ReturnStatementSyntax ret => Carries(ret.Expression),
            AssignmentExpressionSyntax assignment => Carries(assignment.Right),
            _ => false,
        });
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

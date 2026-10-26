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
/// Source-scan backstop for the typed-generator oracles (#2288 P63 Design Decision 7; rewritten by P63b,
/// #2324 Decision 7): <c>ErrorInjector.JudgeSource</c>/<c>Judge</c>/<c>JudgeProject</c>/<c>JudgeClean</c>/
/// <c>JudgeNoInternalError</c> and the <c>SemanticFilter</c> sink are the ONE verdict seam, and a lane outside
/// it may only consume their verdicts. The rules ban five IDEAS, read from the Roslyn syntax tree, with no
/// name-list exemption (the verify round found 43 re-spellings the old token regexes missed and three
/// exemptions — a counter-name suffix list, a "records" heuristic, a pipeline-call name list — that were
/// their own subject):
/// <list type="bullet">
/// <item><b>R1 <see cref="NoCatch"/></b> — no <c>catch</c>: a throw is judged by the seam (which reports it),
/// or recorded by <c>Record.Exception</c>; process and file plumbing lives in a file that does not name the
/// typed generators.</item>
/// <item><b>R2 <see cref="VerdictRead"/></b> — no read of a verdict surface (<c>.Success</c>,
/// <c>.HasErrors</c>, <c>.IsError</c>, <c>.IsWarning</c>) on any receiver; by shape only, both operands of
/// one <c>==</c>/<c>!=</c> reading the same member (two runs compared — the determinism lanes) is not a
/// verdict.</item>
/// <item><b>R3 <see cref="CodeLiteralFlow"/></b> — a code literal (<c>"SPY0273"</c>, a <c>"SPY…"</c> or
/// <c>"^SPY…"</c> prefix), a <c>DiagnosticCodes</c> member, or a <c>.Code</c> read flows only INTO the seam:
/// a literal is legal as an argument of <see cref="CodeSinks"/> (or of a same-file helper that hands that
/// parameter to one), or under <c>allowedCodes:</c>; a <c>.Code</c> read is legal in an interpolation hole
/// (printing it), compared with another <c>.Code</c> (two runs) or with <c>null</c>.</item>
/// <item><b>R4 <see cref="NumericBound"/></b> — inside an assertion, the only numeric comparisons are
/// <c>x &gt; 0</c> and (in)equality of two values with no arithmetic; <c>&gt;=</c>, <c>&lt;</c>,
/// <c>&lt;=</c>, a non-zero bound, <c>!= 0</c>, arithmetic, <c>Assert.InRange</c>,
/// <c>Assert.NotEqual(0, …)</c> and <c>== 0</c> under <c>Assert.False</c> are thresholds and lower bounds
/// in disguise. <c>x &gt; 0</c> stays legal: the counter it floors cannot be a pass counter, because R5
/// forbids forming the boolean a pass counter increments on.</item>
/// <item><b>R5 <see cref="VerdictTested"/></b> — a seam verdict is consumed, never tested: returned,
/// added to a collection, asserted, or bound by <c>is { } x</c> in a branch that records it; a null test of
/// it (or of a local holding it) in an <c>if</c>/<c>while</c>/conditional, a verdict discarded
/// (<c>Judge(…);</c>, <c>_ = …</c>), and a <c>SampleVerdicts</c> local whose <c>.First</c> never reaches an
/// assertion are hits.</item>
/// </list>
/// <para><b>Universe</b>: every <c>*.cs</c> directly in <c>Properties/Semantic</c>, and every <c>*.cs</c>
/// under <c>Properties/**</c> whose text names the typed generator namespace, computed by the scan, minus
/// this scanner (its positive-control literals ARE the banned spellings), the seam's own directory
/// (<c>Generators/Typed/</c>: the injector, the filter, the mutation generator and its census) and the
/// seam's own tests (<c>JudgeSourceTests</c>). Under <see cref="MinimumUniverse"/> files fails loud.</para>
/// <para><b>Known limits</b> (syntax, not data flow — the residue on #2324): a verdict copied into a second local
/// and tested there, arithmetic hoisted into a local before an <c>x &gt; 0</c> floor, and the two shape
/// exemptions (two <c>.Success</c> reads compared, two <c>.Code</c> reads compared) when one side is a fixed
/// known-good run rather than a second run of the same program.</para>
/// <para><b>Allowlist</b>: one literal row per (file, rule) with the exact current count, each citing an
/// issue; drained to zero by P63b.</para>
/// </summary>
public class PropertyOracleConformanceTests
{
    private const int MinimumUniverse = 20;

    private const string NoCatch = "R1-no-catch";
    private const string VerdictRead = "R2-verdict-read";
    private const string CodeLiteralFlow = "R3-code-literal-flow";
    private const string NumericBound = "R4-numeric-bound";
    private const string VerdictTested = "R5-verdict-tested";

    private static readonly string[] AllIds = { NoCatch, VerdictRead, CodeLiteralFlow, NumericBound, VerdictTested };

    private static readonly HashSet<string> VerdictMembers = new(StringComparer.Ordinal) { "Success", "HasErrors", "IsError", "IsWarning" };

    /// <summary>The seam's verdicts: a call of one of these returns a judgment R5 says must be consumed.</summary>
    private static readonly HashSet<string> SeamVerdicts = new(StringComparer.Ordinal)
    {
        "Judge", "JudgeSource", "JudgeProject", "JudgeClean", "JudgeNoInternalError", "JudgeNoDiagnostic", "JudgeRun",
    };

    /// <summary>Where a code literal may flow: the seam's verdict calls and the records that carry an
    /// expected code into it (the roster's lanes and pins).</summary>
    private static readonly HashSet<string> CodeSinks = new(StringComparer.Ordinal)
    {
        "JudgeSource", "JudgeProject", "InjectionResult", "ProjectInjectionResult", "KnownRedRefusal", "KnownRedCascade", "KnownRedRow",
        "YInjected", "KnownRedSpecCode", "ProseCrossReference",
    };

    /// <summary>
    /// Every current hit, by (file relative to <c>Properties/</c>, rule) with its exact count, each citing an
    /// issue; a hit the rows do not cover is red, a row whose count exceeds its hits is red (shrink it), a
    /// row naming a file outside the universe is red. Drained to zero by P63b (#2324).
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
        Assert.True(universe.Count > MinimumUniverse - 1,
            $"The typed-generator consumer universe has {universe.Count} files (< {MinimumUniverse}): the scan "
            + "lost its root or its namespace marker and would pass vacuously.");

        var hits = universe
            .SelectMany(file => Scan(File.ReadAllText(Path.Combine(propertiesRoot, file)))
                .Select(hit => (File: file, hit.Id, hit.Line, hit.Text)))
            .ToList();
        foreach (var hit in hits)
            _output.WriteLine($"  hit {hit.File}:{hit.Line} [{hit.Id}] {hit.Text}");

        var actual = hits.GroupBy(h => (h.File, h.Id)).ToDictionary(g => g.Key, g => g.Count());
        var failures = new List<string>();

        foreach (var row in Allowlist)
        {
            if (!universe.Contains(row.File))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) names a file outside the universe");
            if (!AllIds.Contains(row.SpellingId))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) names an unknown rule id");
            if (!Regex.IsMatch(row.Issue, @"^#\d+$"))
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) cites '{row.Issue}', not an issue number");
            var have = actual.GetValueOrDefault((row.File, row.SpellingId));
            if (row.Count > have)
                failures.Add($"allowlist row ({row.File}, {row.SpellingId}) allows {row.Count} but the file has {have} "
                    + "— the row is stale: shrink it (delete it at 0)");
        }

        foreach (var group in Allowlist.GroupBy(r => (r.File, r.SpellingId)).Where(g => g.Count() > 1))
            failures.Add($"allowlist has {group.Count()} rows for ({group.Key.File}, {group.Key.SpellingId})");

        var allowed = Allowlist.GroupBy(r => (r.File, r.SpellingId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Count));
        foreach (var ((file, id), count) in actual.OrderBy(kv => kv.Key.File, StringComparer.Ordinal))
        {
            var allowedCount = allowed.GetValueOrDefault((file, id));
            if (count > allowedCount)
            {
                var lines = string.Join(", ", hits.Where(h => h.File == file && h.Id == id).Select(h => h.Line));
                failures.Add($"{file} has {count} [{id}] hit(s) (lines {lines}), the allowlist covers {allowedCount} — "
                    + "consume a seam verdict (ErrorInjector.Judge*/JudgeClean, SemanticFilter + sink) instead");
            }
        }

        _output.WriteLine($"allowlist rows {Allowlist.Length}, hits {hits.Count} "
            + $"({string.Join(", ", AllIds.Select(id => $"{id} {hits.Count(h => h.Id == id)}"))})");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Positive control: every line of <c>Banned()</c> carries the rule it must trip in a trailing
    /// <c>// R&lt;n&gt;</c> comment and draws a hit of that rule (the old scan's banned and evasion spellings,
    /// the 43 the verify round planted, the three exemptions that were their own subject, and a
    /// <c>passed &gt; 0</c> on a counter a verdict increments); every line of <c>Benign()</c> draws nothing.
    /// </summary>
    [Fact]
    public void Scanner_FindsEachBannedSpelling_AndNoBenignLookAlike()
    {
        const string text = """
            class Probe
            {
                void Banned()
                {
                    ok = name.StartsWith("SPY0"); // R3
                    if (!result.Success || other) { } // R2
                    try { Run(); } catch { Interlocked.Increment(ref diagnosedCount); } // R1
                    try { Run(); } catch (Exception) { passedCount++; } // R1
                    ok = d.Code == "SPY0273"; // R3
                    ok = d.Code != expectedCode; // R3
                    ok = d.Code.Contains("02"); // R3
                    ok = d.Code?.EndsWith("20") == true; // R3
                    ok = d.Code is "SPY0463"; // R3
                    Assert.True(diagnosed > total / 4, ""); // R4
                    Assert.True(passed > (total / 2), ""); // R4
                    Assert.True(passed * 2 > total, ""); // R4
                    Assert.True(total / 2 < passed, ""); // R4
                    Assert.True(matched >= tested / 2, ""); // R4
                    Assert.True(diagnosed >= 1, ""); // R4
                    Assert.True(diagnosed != 0, ""); // R4
                    Assert.True(insidePassed + outsideDiagnosed > 0, ""); // R4
                    Assert.Equal("SPY0273", diagnostics.First().Code); // R3
                    ok = codes.Contains("SPY0273"); // R3
                    Assert.Contains("SPY0273", diagnostics.Select(d => d.Code)); // R3
                    ok = string.Equals(d.Code, "SPY0273"); // R3
                    ok = codes[0] == "SPY0273"; // R3
                    if (result.Diagnostics.HasErrors) diagnosed++; // R2
                    Assert.True(result.Diagnostics.HasErrors, ""); // R2
                    try { new Sharpy.Compiler.Compiler().Analyze(source, "a.spy"); } catch { } // R1
                    try { Analyze(source); } catch { return null; } // R1
                    try { text = Unparser.Unparse(module); } catch { return (false, -1); } // R1
                    if (!analysis.Success) return; // R2
                    Assert.False(r.Success); // R2
                    if (!(r.Success)) n++; // R2
                    if (r.Success == false) n++; // R2
                    n += r.Success ? 0 : 1; // R2
                    if (!r.Success) n = n + 1; // R2
                    ok = r?.IsError == true; // R2
                    ok = all.Any(x => x.IsWarning); // R2
                    Assert.True(diagnosed >= tested * 0.5, ""); // R4
                    Assert.True((double)diagnosed / tested > 0.25, ""); // R4
                    Assert.True(passed > floor, ""); // R4
                    Assert.NotEqual(0, diagnosed); // R4
                    Assert.False(diagnosed == 0); // R4
                    Assert.InRange(passed, 1, total); // R4
                    Assert.True(passed < total, ""); // R4
                    Assert.True(passed <= total, ""); // R4
                    Assert.True(passed > 3, ""); // R4
                    examined.Should().BeGreaterThanOrEqualTo(10, "a floor"); // R4
                    diagnosed.Should().NotBe(0); // R4
                    passed.Should().BeGreaterThan(total / 4); // R4
                    ok = codes[0][..3] == "SPY"; // R3
                    Assert.Matches("^SPY02", d.Code); // R3
                    ok = "SPY0273".Equals(d.Code); // R3
                    var k = d.Code switch { "SPY0273" => 1, _ => 0 }; // R3
                    ok = d is { Code: "SPY0273" }; // R3
                    ok = new[] { "SPY0273" }.Contains(d.Code); // R3
                    ok = expected.Contains(d.Code); // R3
                    ok = DiagnosticCodes.Semantic.TypeMismatch == got; // R3
                    try { Run(); } catch (Exception e) { _ = e; } // R1
                    try { Helper(src); } catch { } // R1
                    try { Analyze(s); } catch (Exception e) { return $"{e}"; } // R1
                    try { Helper(source); } catch { return null; } // R1
                    try { proc.Kill(); } catch { } // R1
                    if (ErrorInjector.JudgeClean(s, "l") is null) passedTotal++; // R5
                    if (ErrorInjector.JudgeSource(s, "SPY0220", CompilerDiagnosticSeverity.Error, null, "l") is not null) refusedPrograms.Add(s); // R5
                    if (ErrorInjector.JudgeClean(s, "l") == null) passed++; // R5
                    ok = ErrorInjector.JudgeClean(s, "l") != null ? a : b; // R5
                    ErrorInjector.Judge(injection, "l"); // R5
                    var dropped = ErrorInjector.JudgeClean(s, "l") ?? "x"; // R5
                    if (dropped is null) passed++;
                    if (JudgeRun(run, s, "l") is { } ran) Console.WriteLine(ran); // R5
                    _ = ErrorInjector.JudgeClean(s, "l"); // R5
                    while (ErrorInjector.JudgeNoInternalError(s, "l") is not null) { } // R5
                    var lost = ErrorInjector.JudgeSamples(gen, s => ErrorInjector.JudgeClean(s, "l"), iter: 1); // R5
                    n += ErrorInjector.JudgeClean(s, "l") switch { null => 1, _ => 0 }; // R5
                    if (ErrorInjector.JudgeClean(s, "l") is { } f1) failures.Add(f1); else passed++; // R5
                    Console.WriteLine(ErrorInjector.JudgeSamples(gen, s => ErrorInjector.JudgeClean(s, "l"), iter: 1)); // R5
                    if (Record.Exception(() => Run()) is null) passed++; // R5
                    ok = r is { Success: false }; // R2
                    var (accepted, all) = PropertyAnalysis.AnalyseSingleFile(s); // R2
                    ok = $"{d.Code}".EndsWith("0273"); // R3
                    ok = $"{d.Code}" == expectedText; // R3
                }

                void VerdictThroughALocal()
                {
                    var verdict = ErrorInjector.JudgeClean(s, "l"); // R5
                    if (verdict is null) passed++;
                    Assert.True(passed > 0, "");
                }

                void Benign()
                {
                    Assert.True(tested > 0, "No injections were applicable");
                    Assert.True(verdicts.Tested > 0, "");
                    Assert.True(admitted > 0, "");
                    Assert.True(programs.Count > 0, "");
                    Assert.True(failures.Count == 0, "");
                    Assert.True(compared == total, "");
                    Assert.Equal(expected, actual);
                    Assert.NotEmpty(samples);
                    examined.Should().BeGreaterThan(0, "an applicability floor");
                    failures.Should().BeEmpty();
                    // Assert.True(passed > tested / 2) — a comment recording the old shape
                    /* if (!result.Success || other) { } */
                    var half = items.Select(n => n / 2);
                    for (int i = 0; i < n; i++) { }
                    ok = Math.Abs((av - bv) / av) <= RelTol;
                    ok = first[i].Code != second[i].Code;
                    ok = d.Code != null && d.Code == null;
                    output.WriteLine($"drew {d.Code}");
                    ok = proc.ExitCode != 0;
                    ok = first.Success == second.Success;
                    ok = a.HasErrors != b.HasErrors;
                    if (errors.Count > 0) throw new Exception("errors");
                    verdict = ErrorInjector.JudgeSource(source, "SPY0273", CompilerDiagnosticSeverity.Error, null, "x");
                    var injection = new ErrorInjector.InjectionResult(mutated, "SPY0220");
                    var pin = new ErrorInjector.KnownRedRefusal("#2212", "analyses clean; run is SPY0908 CS0311");
                    var row = new YInjected("ErrorInjector.InjectX", "SPY0222", CompilerDiagnosticSeverity.Error);
                    var samples = PropertyCorpus.CompileAll(gen, iter: 50);
                    PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: new[] { "SPY0451" }, _output, "x");
                    if (ErrorInjector.Judge(result, "x") is { } failure) failures.Add(failure);
                    var either = ErrorInjector.JudgeClean(s, "x") ?? Other(s);
                    Assert.Null(either);
                    if (JudgeRun(run, s, "x") is { } ran) throw new Exception(ran);
                    var verdictOf = valid is { } v ? ErrorInjector.JudgeClean(v, "x") : "inapplicable";
                    Assert.Null(verdictOf);
                    failures.Add(ErrorInjector.JudgeClean(source, "x"));
                    Assert.Null(ErrorInjector.JudgeClean(source, "x"));
                    var thrown = Record.Exception(() => Run());
                    Assert.Contains(expected, text);
                    if (Record.Exception(() => Run()) is { } thrownAgain) failures.Add(thrownAgain.Message);
                    var message = $"drew {d.Code} at {d.Line}";
                    var joined = $"A: [{a.Code}]\n" + $"B: [{b.Code}]";
                    var verdicts = ErrorInjector.JudgeSamples(gen, s => ErrorInjector.JudgeClean(s, "l"), iter: 1);
                    Assert.True(verdicts.First is null, "");
                    Assert.True(sink.Unexplained.Count == 0, sink.Report());
                }

                string? Lane(string s) => ErrorInjector.JudgeClean(s, "returned");

                string? Reparent(string s)
                {
                    if (Record.Exception(() => Run()) is { } unreachable)
                        return $"unreachable: {unreachable.Message}";
                    return null;
                }

                void Draws(Gen<string> gen) => AssertEveryDraw(gen, "SPY0417");

                void AssertEveryDraw(Gen<string> gen, string code) =>
                    ErrorInjector.JudgeSamples(gen, s => ErrorInjector.JudgeSource(s, code, CompilerDiagnosticSeverity.Error, null, "l"), iter: 1);
            }
            """;

        var hits = Scan(text);
        foreach (var hit in hits)
            _output.WriteLine($"  hit line {hit.Line} [{hit.Id}] {hit.Text}");

        var lines = text.Split('\n');
        var benignStart = Array.FindIndex(lines, l => l.Contains("void Benign()", StringComparison.Ordinal)) + 1;
        var failures = new List<string>();
        int bannedTested = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var expected = Regex.Match(lines[i], @"// (R\d)\s*$");
            if (!expected.Success)
                continue;
            bannedTested++;
            var lineHits = hits.Where(h => h.Line == i + 1).Select(h => h.Id).ToList();
            if (!lineHits.Any(id => id.StartsWith(expected.Groups[1].Value + "-", StringComparison.Ordinal)))
                failures.Add($"line {i + 1} expects {expected.Groups[1].Value}, drew [{string.Join(", ", lineHits)}]: {lines[i].Trim()}");
        }
        foreach (var hit in hits.Where(h => h.Line > benignStart))
            failures.Add($"benign line {hit.Line} hit [{hit.Id}]: {hit.Text}");

        _output.WriteLine($"{bannedTested} planted spellings");
        Assert.True(bannedTested > 0, "no planted spelling was read");
        Assert.True(failures.Count == 0, $"{failures.Count} positive-control failure(s):\n  " + string.Join("\n  ", failures));
    }

    private const string SelfFileName = "PropertyOracleConformanceTests.cs";

    /// <summary>The seam's own tests: they exercise the verdicts they are made of.</summary>
    private const string SeamTestsFileName = "JudgeSourceTests.cs";

    private static HashSet<string> Universe(string propertiesRoot)
    {
        var seamDir = Path.Combine(propertiesRoot, "Generators", "Typed") + Path.DirectorySeparatorChar;
        var semanticDir = Path.Combine(propertiesRoot, "Semantic") + Path.DirectorySeparatorChar;
        var marker = new Regex(@"\bGenerators\.Typed\b");
        return Directory.EnumerateFiles(propertiesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(seamDir, StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path) is not (SelfFileName or SeamTestsFileName))
            .Where(path => path.StartsWith(semanticDir, StringComparison.Ordinal)
                || marker.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(propertiesRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
    }

    // ---- the rules ------------------------------------------------------------------------------------

    private static List<(string Id, int Line, string Text)> Scan(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var found = new Dictionary<(string Id, int Position), SyntaxNode>();

        void Hit(string id, SyntaxNode node) => found.TryAdd((id, node.SpanStart), node);

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case CatchClauseSyntax catchClause:
                    Hit(NoCatch, catchClause);
                    break;
                case MemberAccessExpressionSyntax access:
                    if (VerdictMembers.Contains(access.Name.Identifier.ValueText) && !IsTwoRunComparison(access))
                        Hit(VerdictRead, access);
                    if (access.Name.Identifier.ValueText == "Code" && !IsLegalCodeRead(access))
                        Hit(CodeLiteralFlow, access);
                    if (access.Expression is IdentifierNameSyntax { Identifier.ValueText: "DiagnosticCodes" })
                        Hit(CodeLiteralFlow, access);
                    break;
                case MemberBindingExpressionSyntax binding:
                    if (VerdictMembers.Contains(binding.Name.Identifier.ValueText))
                        Hit(VerdictRead, binding);
                    if (binding.Name.Identifier.ValueText == "Code" && !IsLegalCodeRead(binding))
                        Hit(CodeLiteralFlow, binding);
                    break;
                case LiteralExpressionSyntax { Token.Value: string value } literal
                    when Regex.IsMatch(value, @"^\^?SPY\d{0,4}$") && !FlowsIntoTheSeam(literal, root):
                    Hit(CodeLiteralFlow, literal);
                    break;
                case SubpatternSyntax { ExpressionColon: { } colon } sub
                    when colon.Expression is IdentifierNameSyntax { Identifier.ValueText: "Code" }:
                    Hit(CodeLiteralFlow, sub);
                    break;
                case SubpatternSyntax { NameColon.Name.Identifier.ValueText: "Code" } sub:
                    Hit(CodeLiteralFlow, sub);
                    break;
                case SubpatternSyntax { NameColon.Name.Identifier.ValueText: var member } sub when VerdictMembers.Contains(member):
                    Hit(VerdictRead, sub);
                    break;
                case SubpatternSyntax { ExpressionColon.Expression: IdentifierNameSyntax { Identifier.ValueText: var member2 } } sub
                    when VerdictMembers.Contains(member2):
                    Hit(VerdictRead, sub);
                    break;
                case InvocationExpressionSyntax invocation:
                    if (IsAssertion(invocation, out var assertion))
                        NumericBounds(invocation, assertion, Hit);
                    if (IsFluentBound(invocation))
                        Hit(NumericBound, invocation);
                    if ((SeamVerdicts.Contains(Name(invocation)) || IsRecordException(invocation)) && !IsConsumed(invocation, root))
                        Hit(VerdictTested, invocation);
                    // the seam's raw analysis (success flag + diagnostics) is the verdict surface itself
                    if (invocation.Expression is MemberAccessExpressionSyntax
                        {
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "PropertyAnalysis" },
                            Name.Identifier.ValueText: "AnalyseSingleFile" or "AnalyseProject",
                        })
                        Hit(VerdictRead, invocation);
                    if (Name(invocation) == "JudgeSamples" && !SampleVerdictsAsserted(invocation))
                        Hit(VerdictTested, invocation);
                    break;
            }
        }

        return found
            .OrderBy(kv => kv.Key.Position)
            .ThenBy(kv => kv.Key.Id, StringComparer.Ordinal)
            .Select(kv => (kv.Key.Id, tree.GetLineSpan(new TextSpan(kv.Key.Position, 0)).StartLinePosition.Line + 1, Describe(kv.Value)))
            .ToList();
    }

    /// <summary>R2's one shape exemption: both operands of one <c>==</c>/<c>!=</c> read the same verdict member.</summary>
    private static bool IsTwoRunComparison(MemberAccessExpressionSyntax access) =>
        access.Parent is BinaryExpressionSyntax b
        && (b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression))
        && b.Left is MemberAccessExpressionSyntax l && b.Right is MemberAccessExpressionSyntax r
        && l.Name.Identifier.ValueText == r.Name.Identifier.ValueText;

    /// <summary>A <c>.Code</c> read that decides nothing: printed in an interpolation hole, compared with
    /// another <c>.Code</c> (two runs) or with <c>null</c>.</summary>
    private static bool IsLegalCodeRead(ExpressionSyntax access)
    {
        // printed: inside an interpolation hole whose string is not itself inspected (`$"{d.Code}".EndsWith(…)`
        // or compared is a code read in disguise)
        if (access.Ancestors().TakeWhile(a => a is not StatementSyntax).OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault() is { } text)
            return text.Parent is not (MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax)
                && !(text.Parent is BinaryExpressionSyntax b0 && (b0.IsKind(SyntaxKind.EqualsExpression) || b0.IsKind(SyntaxKind.NotEqualsExpression)));
        var outer = access.Parent is ConditionalAccessExpressionSyntax c && ReferenceEquals(c.WhenNotNull, access) ? c : (SyntaxNode)access;
        if (outer.Parent is BinaryExpressionSyntax b && (b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression)))
        {
            var other = ReferenceEquals(b.Left, outer) ? b.Right : b.Left;
            return other is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }
                || other is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Code" };
        }
        return false;
    }

    /// <summary>A code literal handed to the seam: an argument of a <see cref="CodeSinks"/> call or object
    /// creation, a value under <c>allowedCodes:</c>, or an argument of a same-file helper whose matching
    /// parameter is passed to a sink.</summary>
    private static bool FlowsIntoTheSeam(LiteralExpressionSyntax literal, SyntaxNode root)
    {
        if (literal.Ancestors().OfType<ArgumentSyntax>().Any(a => a.NameColon?.Name.Identifier.ValueText == "allowedCodes"))
            return true;
        if (literal.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax list } argument)
            return false;
        var callee = list.Parent switch
        {
            InvocationExpressionSyntax inv => Name(inv),
            BaseObjectCreationExpressionSyntax creation => creation switch
            {
                ObjectCreationExpressionSyntax { Type: var type } => LastName(type),
                _ => "",
            },
            _ => "",
        };
        if (CodeSinks.Contains(callee))
            return true;
        // one level through a same-file helper: Draws(gen, "SPY0417") -> AssertEveryDraw(gen, code) -> JudgeSource(…, code, …)
        var index = list.Arguments.IndexOf(argument);
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == callee && m.ParameterList.Parameters.Count > index)
            .Any(m =>
            {
                var parameter = m.ParameterList.Parameters[index].Identifier.ValueText;
                return ((SyntaxNode?)m.Body ?? m.ExpressionBody)?.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(i => CodeSinks.Contains(Name(i)) && i.ArgumentList.Arguments.Any(a =>
                        a.Expression is IdentifierNameSyntax { Identifier.ValueText: var v } && v == parameter)) == true;
            });
    }

    private static string LastName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax q => q.Right.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        _ => "",
    };

    private static bool IsAssertion(InvocationExpressionSyntax invocation, out string assertion)
    {
        assertion = invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" },
            Name.Identifier.ValueText: var name,
        } ? name : "";
        return assertion.Length > 0;
    }

    /// <summary>R4: the numeric comparisons inside one assertion's arguments (not inside a nested lambda's
    /// own assertions — those are judged where they are).</summary>
    private static void NumericBounds(InvocationExpressionSyntax invocation, string assertion, Action<string, SyntaxNode> hit)
    {
        var args = invocation.ArgumentList.Arguments;
        if (assertion == "InRange")
            hit(NumericBound, invocation);
        if (assertion == "NotEqual" && args.Count > 0 && args[0].Expression is LiteralExpressionSyntax { Token.Value: int })
            hit(NumericBound, invocation);

        var comparisons = args.SelectMany(a => a.Expression.DescendantNodesAndSelf()
                .Where(n => n.Ancestors().TakeWhile(x => !ReferenceEquals(x, a)).All(x => x is not LambdaExpressionSyntax))
                .OfType<BinaryExpressionSyntax>())
            .Where(IsComparison);
        foreach (var comparison in comparisons)
        {
            var arithmetic = HasArithmetic(comparison.Left) || HasArithmetic(comparison.Right);
            var bad = comparison.Kind() switch
            {
                SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression => true,
                SyntaxKind.GreaterThanExpression => arithmetic || !IsZero(comparison.Right),
                SyntaxKind.NotEqualsExpression => arithmetic || IsZero(comparison.Left) || IsZero(comparison.Right),
                SyntaxKind.EqualsExpression => arithmetic || (assertion == "False" && (IsZero(comparison.Left) || IsZero(comparison.Right))),
                _ => false,
            };
            if (bad)
                hit(NumericBound, comparison);
        }
    }

    /// <summary>R4 over FluentAssertions: <c>x.Should().BeGreaterThan(k)</c> with a non-zero bound, and every
    /// other numeric bound (<c>BeGreaterThanOrEqualTo</c>, <c>BeLessThan</c>, <c>BeLessThanOrEqualTo</c>,
    /// <c>BeInRange</c>, <c>BeCloseTo</c>, <c>NotBe(0)</c>).</summary>
    private static bool IsFluentBound(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: var bound,
                Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Should" } },
            })
            return false;
        var args = invocation.ArgumentList.Arguments;
        return bound switch
        {
            "BeGreaterThan" => args.Count == 0 || !IsZero(args[0].Expression),
            "BeGreaterThanOrEqualTo" or "BeLessThan" or "BeLessThanOrEqualTo" or "BeInRange" or "BeCloseTo" or "BePositive" => true,
            "NotBe" => args.Count > 0 && IsZero(args[0].Expression),
            _ => false,
        };
    }

    private static bool IsComparison(BinaryExpressionSyntax b) => b.Kind() is SyntaxKind.GreaterThanExpression
        or SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
        or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression;

    private static bool IsZero(ExpressionSyntax e) => e is LiteralExpressionSyntax { Token.Value: 0 };

    private static bool HasArithmetic(ExpressionSyntax e) =>
        e.DescendantNodesAndSelf().Any(n => n is BinaryExpressionSyntax b && b.Kind() is SyntaxKind.AddExpression
            or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression);

    /// <summary>R5: a seam verdict is consumed — returned, added, asserted, bound by <c>is { } x</c> in a
    /// branch that records it, or held by a local only ever asserted or returned.</summary>
    private static bool IsConsumed(ExpressionSyntax verdict, SyntaxNode root)
    {
        var parent = verdict.Parent;
        while (parent is ParenthesizedExpressionSyntax)
            parent = parent.Parent;
        // `a ?? b` and `c ? a : b` pass the verdict on: the combined value is what must be consumed.
        if (parent is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression))
            return IsConsumed(coalesce, root);
        if (parent is ConditionalExpressionSyntax conditional && !ReferenceEquals(conditional.Condition, verdict))
            return IsConsumed(conditional, root);
        switch (parent)
        {
            case ExpressionStatementSyntax:
                return false; // discarded
            case AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" } }:
                return false; // discarded
            case ReturnStatementSyntax or ArrowExpressionClauseSyntax:
                return true;
            case LambdaExpressionSyntax:
                return true; // returned from a lambda
            case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } }:
                // asserted, or recorded into a collection; printed or handed elsewhere decides nothing
                return IsAssertion(call, out _) || Name(call) is "Add" or "Enqueue" or "TryAdd" or "AddRange";
            case IsPatternExpressionSyntax pattern:
                return IsRecordingBinding(pattern);
            case BinaryExpressionSyntax:
            case PrefixUnaryExpressionSyntax:
            case ConditionalExpressionSyntax:
            case SwitchExpressionSyntax:
            case SwitchStatementSyntax:
                return false; // tested
            case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax local }:
                return !LocalIsTested(local);
            default:
                return true;
        }
    }

    /// <summary><c>Record.Exception(…)</c>: the thrown-or-not verdict of a call; consumed like a seam verdict.</summary>
    private static bool IsRecordException(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Record" },
            Name.Identifier.ValueText: "Exception",
        };

    /// <summary><c>verdict is { } x</c> (or <c>is string x</c>) whose branch adds <c>x</c> to a collection or
    /// asserts on it — a recorded verdict, not a tested one.</summary>
    private static bool IsRecordingBinding(IsPatternExpressionSyntax pattern)
    {
        var designation = pattern.Pattern switch
        {
            RecursivePatternSyntax { Designation: SingleVariableDesignationSyntax d } => d.Identifier.ValueText,
            DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax d } => d.Identifier.ValueText,
            _ => null,
        };
        if (designation is null || pattern.Parent is not IfStatementSyntax branch)
            return false;
        // an `else` that counts turns the binding back into a test (`… is { } f) record(f); else passed++;`)
        if (branch.Else?.Statement.DescendantNodesAndSelf().Any(n => n is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax
            { RawKind: (int)SyntaxKind.PreIncrementExpression } or AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression }
                || n is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Increment" } }) == true)
            return false;
        bool Mentions(SyntaxNode n) => n.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.ValueText == designation);
        return branch.Statement.DescendantNodesAndSelf().Any(n => n switch
        {
            // recorded into a collection or asserted on
            InvocationExpressionSyntax i => (Name(i) is "Add" or "Enqueue" or "TryAdd" || IsAssertion(i, out _))
                && i.ArgumentList.Arguments.Any(a => Mentions(a.Expression)),
            // thrown: the sample fails with the verdict as its message; returned: the caller receives it
            ThrowStatementSyntax { Expression: { } thrown } => Mentions(thrown),
            ReturnStatementSyntax { Expression: { } returned } => Mentions(returned),
            _ => false,
        });
    }

    /// <summary>A local holding a verdict that a branch or a conditional tests for null.</summary>
    private static bool LocalIsTested(VariableDeclaratorSyntax local)
    {
        var name = local.Identifier.ValueText;
        var scope = local.Ancestors().OfType<BlockSyntax>().FirstOrDefault() ?? (SyntaxNode?)local.Ancestors().LastOrDefault();
        if (scope is null)
            return false;
        return scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == name)
            .Any(id =>
            {
                var test = id.Parent is IsPatternExpressionSyntax or BinaryExpressionSyntax ? id.Parent : null;
                return test is not null
                    && test.Ancestors().TakeWhile(a => a is not StatementSyntax || a is IfStatementSyntax or WhileStatementSyntax)
                        .Any(a => a is IfStatementSyntax or WhileStatementSyntax or ConditionalExpressionSyntax)
                    && !test.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => IsAssertion(i, out _));
            });
    }

    /// <summary>A <c>JudgeSamples</c> call whose <c>SampleVerdicts</c> is returned, or bound to a local whose
    /// <c>.First</c> reaches an assertion.</summary>
    private static bool SampleVerdictsAsserted(InvocationExpressionSyntax judgeSamples)
    {
        if (judgeSamples.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax)
            return true;
        if (judgeSamples.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } })
            return IsAssertion(call, out _);
        if (judgeSamples.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax local })
            return false;
        var name = local.Identifier.ValueText;
        var body = local.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        return body?.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => IsAssertion(i, out _))
            .Any(a => a.ArgumentList.Arguments.Any(arg => arg.Expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
                .Any(m => m.Name.Identifier.ValueText == "First" && m.Expression is IdentifierNameSyntax { Identifier.ValueText: var v } && v == name))) == true;
    }

    private static string Name(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText,
        _ => "",
    };

    private static string Describe(SyntaxNode node)
    {
        var statement = node.AncestorsAndSelf().FirstOrDefault(n => n is StatementSyntax or CatchClauseSyntax) ?? node;
        var text = Regex.Replace(statement.WithoutTrivia().ToString(), @"\s+", " ");
        return text.Length <= 160 ? text : text[..160] + "…";
    }
}

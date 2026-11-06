using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Guards that every [Fact]/[Theory] in Sharpy.Compiler.Tests is selected by at least one CI
/// workflow step. Scoped to this assembly only — other projects' steps filter only
/// Category!=Benchmark (no substring holes); extending cross-assembly would need one guard per
/// test project (#1556). Since #2178 the Compiler.Tests steps are matrix jobs (Compiler shards,
/// one job per sweep); each step's --filter is resolved per matrix combination against the
/// workflow env before evaluation, and the shard-partition job's `--list-tests` runs are not
/// counted as coverage.
/// </summary>
public class CiFilterCoverageConformanceTests
{
    private static readonly (string Predicate, string Rationale)[] Exemptions =
    {
        ("Category=Benchmark", "benchmarks run on demand; excluded by every step by design"),
        ("FullyQualifiedName~MetamorphicCorpusInvarianceTests",
            "deliberately CI-excluded (documented on both Compiler.Tests steps in dotnet10.yml): " +
            "spawns a dotnet subprocess per (fixture, transform) pair and draws a fresh sample per " +
            "run — coverage comes from /property-stress, not a deterministic CI gate"),
    };

    [Fact]
    public void EveryCompilerTest_IsSelectedByAtLeastOneCiStep()
    {
        var (filters, tests) = LoadFiltersAndTests();

        var unselected = new List<string>();
        foreach (var test in tests)
        {
            if (IsExempt(test))
                continue;
            if (!filters.Any(f => EvaluateFilter(f, test)))
                unselected.Add(test.Fqn);
        }

        Assert.True(unselected.Count == 0,
            $"{unselected.Count} test(s) not selected by any CI Compiler.Tests step:\n" +
            string.Join("\n", unselected.Select(t => $"  {t}")) +
            "\nFix the CI filter or add to the exemption list with a rationale.");
    }

    [Fact]
    public void EveryFilterSubstring_MatchesAtLeastOneTest()
    {
        var (filters, tests) = LoadFiltersAndTests();
        var fqns = tests.Select(t => t.Fqn).ToList();

        var stale = new List<string>();
        foreach (var filter in filters)
        {
            foreach (var token in ExtractFqnSubstrings(filter))
            {
                if (!fqns.Any(f => f.Contains(token, StringComparison.Ordinal)))
                    stale.Add(token);
            }
        }

        Assert.True(stale.Count == 0,
            $"FQN filter substring(s) match no test:\n" +
            string.Join("\n", stale.Select(t => $"  \"{t}\"")) +
            "\nUpdate or remove stale tokens in dotnet10.yml.");
    }

    /// <summary>
    /// A sweep leaves the Compiler shards by a <c>FullyQualifiedName!~X</c> in <c>COMPILER_MAIN_FILTER</c> and runs in
    /// its own step, so it must be selected by exactly one step across every job (a shard is one step per matrix
    /// value). The sweeps are read from both sides of that hand-off, because reading only the main filter would let the
    /// deleted <c>!~</c> take its own sweep out of this fact.
    /// </summary>
    [Fact]
    public void EverySweep_IsSelectedByExactlyOneStep()
    {
        var (steps, tests) = LoadStepsAndTests();
        var sweepTokens = SweepTokens(steps);
        Assert.True(sweepTokens.Count >= MinimumSweeps,
            $"Found {sweepTokens.Count} sweeps ({string.Join(", ", sweepTokens)}), expected at least {MinimumSweeps}.");

        var judged = 0;
        var wrong = new List<string>();
        foreach (var test in tests)
        {
            if (IsExempt(test) || !sweepTokens.Any(t => test.Fqn.Contains(t, StringComparison.Ordinal)))
                continue;
            judged++;
            var selecting = steps.Where(s => EvaluateFilter(s.Filter, test)).Select(s => s.Name).ToList();
            if (selecting.Count != 1)
                wrong.Add($"  {test.Fqn}: {selecting.Count} step(s): {string.Join("; ", selecting)}");
        }

        Assert.True(judged >= MinimumSweepTests,
            $"Judged {judged} sweep tests, expected at least {MinimumSweepTests}: the sweep names match nothing.");
        Assert.True(wrong.Count == 0,
            $"{wrong.Count} sweep test(s) not selected by exactly one CI step:\n{string.Join("\n", wrong)}");
    }

    /// <summary>
    /// A skipped oracle guards nothing and leaves its job green, so every step that selects a class calling
    /// <see cref="PythonInterpreter.RequireOrSkip(int)"/> must resolve <see cref="PythonInterpreter.RequireEnvironmentVariable"/>
    /// to 1 from the workflow, job or step env. The three sweep classes are pinned: a scan that misses a direct caller
    /// is red.
    /// </summary>
    [Fact]
    public void EveryPythonOracleTest_RunsWithTheOracleRequired()
    {
        var (steps, tests) = LoadStepsAndTests();
        var (oracleClasses, unattributed) = ScanPythonOracleClasses(tests);

        Assert.True(unattributed.Count == 0,
            "RequireOrSkip called outside any class with tests, so no step can be judged for it:\n"
            + string.Join("\n", unattributed.Select(u => $"  {u}")));
        Assert.Contains("Sharpy.Compiler.Tests.Properties.Differential.DifferentialExecutionTests", oracleClasses);
        Assert.Contains("Sharpy.Compiler.Tests.Properties.Differential.FormatSpecDifferentialTests", oracleClasses);
        Assert.Contains("Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests", oracleClasses);

        var judged = 0;
        var wrong = new List<string>();
        foreach (var step in steps)
        {
            var selected = oracleClasses
                .Where(c => tests.Any(t => t.Fqn.StartsWith(c + ".", StringComparison.Ordinal) && EvaluateFilter(step.Filter, t)))
                .ToList();
            if (selected.Count == 0)
                continue;
            judged++;
            if (step.Env.GetValueOrDefault(PythonInterpreter.RequireEnvironmentVariable) != "1")
                wrong.Add($"  {step.Name}: selects {string.Join(", ", selected)}");
        }

        Assert.True(judged >= MinimumOracleSteps,
            $"Judged {judged} steps, expected at least {MinimumOracleSteps}: the filters select no python oracle test.");
        Assert.True(wrong.Count == 0,
            $"{wrong.Count} step(s) run a python oracle test without {PythonInterpreter.RequireEnvironmentVariable}=1, so it skips green:\n"
            + string.Join("\n", wrong));
    }

    /// <summary>
    /// The evaluator resolves the workflow's matrix/env spelling to a literal filter, and refuses
    /// (throws) what it cannot read rather than treating it as "selects nothing" or "selects all".
    /// </summary>
    [Fact]
    public void Evaluator_ResolvesMatrixAndEnv_AndRefusesWhatItCannotRead()
    {
        var env = new Dictionary<string, string>
        {
            ["MAIN"] = "Category!=Benchmark",
            ["SHARD_2"] = "(FullyQualifiedName~A.|FullyQualifiedName~B.)",
        };
        var matrix = new Dictionary<string, string> { ["n"] = "2" };
        Assert.Equal("Category!=Benchmark&(FullyQualifiedName~A.|FullyQualifiedName~B.)",
            ResolveFilter("$MAIN&$SHARD_${{ matrix.n }}", matrix, env));

        Assert.Throws<InvalidOperationException>(() => ResolveFilter("$UNDEFINED", matrix, env));
        Assert.Throws<InvalidOperationException>(() => ResolveFilter("$SHARD_${{ matrix.missing }}", matrix, env));
        Assert.Throws<InvalidOperationException>(() => ResolveFilter("${!var}", matrix, env));
        Assert.Throws<InvalidOperationException>(() => ParseFilter("FullyQualifiedName"));
        Assert.Throws<InvalidOperationException>(() => ParseFilter("(FullyQualifiedName~A"));
        Assert.Throws<InvalidOperationException>(() => ParseFilter("FullyQualifiedName~A|"));
        Assert.Throws<InvalidOperationException>(() => ParseFilter("FullyQualifiedName~A\\(b"));

        var test = new TestInfo("N.B.C.M", new Dictionary<string, List<string>>());
        Assert.True(EvaluateFilter("Category!=Benchmark&(FullyQualifiedName~A.|FullyQualifiedName~B.)", test));
        Assert.False(EvaluateFilter("Category!=Benchmark&(FullyQualifiedName~A.|FullyQualifiedName~X.)", test));
        Assert.False(EvaluateFilter("FullyQualifiedName~B.&FullyQualifiedName!~N.B", test));
    }

    private (List<string> Filters, List<TestInfo> Tests) LoadFiltersAndTests()
    {
        var (steps, tests) = LoadStepsAndTests();
        return (steps.Select(s => s.Filter).ToList(), tests);
    }

    private (List<ResolvedStep> Steps, List<TestInfo> Tests) LoadStepsAndTests()
    {
        var repoRoot = FindRepoRoot();
        var yaml = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "dotnet10.yml"));
        var steps = ExtractCompilerTestSteps(yaml);
        Assert.True(steps.Count >= 2,
            $"Expected ≥2 Compiler.Tests steps, found {steps.Count}. Workflow restructured?");
        return (steps, ReflectTests());
    }

    /// <summary>The seven sweeps <c>COMPILER_MAIN_FILTER</c> excludes today (measured). A literal, so that a shrinking
    /// universe is red rather than a smaller check.</summary>
    private const int MinimumSweeps = 7;

    /// <summary>Just below the 84 test methods measured when this fact was written, exemptions aside. A literal, so that
    /// sweep names matching nothing are red.</summary>
    private const int MinimumSweepTests = 80;

    /// <summary>The three pinned sweep steps; the shards that select a python twin come on top.</summary>
    private const int MinimumOracleSteps = 3;

    private static List<string> SweepTokens(IReadOnlyList<ResolvedStep> steps)
    {
        static IEnumerable<string> FqnTokens(string filter, FilterOp op) => Clauses(ParseFilter(filter))
            .Where(c => c.Property == "FullyQualifiedName" && c.Op == op)
            .Select(c => c.Value);

        var mainFilter = steps[0].Env["COMPILER_MAIN_FILTER"];
        var sweepSteps = steps.Where(s => Clauses(ParseFilter(s.Filter)).Any(
            c => c is { Property: "Category", Op: FilterOp.Equals, Value: "GapDiscovery" }));
        return FqnTokens(mainFilter, FilterOp.NotContains)
            .Concat(sweepSteps.SelectMany(s => FqnTokens(s.Filter, FilterOp.Contains)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The test classes that call <c>PythonInterpreter.RequireOrSkip</c>, in the shape of
    /// <c>PythonOracleSkipCensusTests.Scan</c>: a call inside a lambda runs under the lambda's caller, and a call in a
    /// helper of a test class counts for that class. <c>TestPreconditions.RequireOrSkip</c> is a different gate.
    /// <c>Unattributed</c> holds the calls in a class with no tests of its own or around it.
    /// </summary>
    private static (List<string> Classes, List<string> Unattributed) ScanPythonOracleClasses(IReadOnlyList<TestInfo> tests)
    {
        var classes = new HashSet<string>(StringComparer.Ordinal);
        var unattributed = new List<string>();
        var projectDir = Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler.Tests");
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(projectDir, path).Split(Path.DirectorySeparatorChar);
            if (parts.Contains("bin") || parts.Contains("obj") || parts.Contains("TestFixtures") || parts.Contains("TestResults"))
                continue;

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot();
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var isOracleGate = call.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "RequireOrSkip",
                    Expression: IdentifierNameSyntax { Identifier.ValueText: nameof(PythonInterpreter) },
                };
                if (!isOracleGate || call.Ancestors().Any(a => a is AnonymousFunctionExpressionSyntax))
                    continue;

                var owner = EnclosingClassNames(call)
                    .FirstOrDefault(c => tests.Any(t => t.Fqn.StartsWith(c + ".", StringComparison.Ordinal)));
                if (owner != null)
                    classes.Add(owner);
                else
                    unattributed.Add($"{path}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
            }
        }

        return (classes.OrderBy(c => c, StringComparer.Ordinal).ToList(), unattributed);
    }

    /// <summary>The qualified name of each class around <paramref name="node"/>, innermost first, spelled as
    /// <see cref="ReflectTests"/> spells them.</summary>
    private static IEnumerable<string> EnclosingClassNames(SyntaxNode node)
    {
        var classes = node.Ancestors().OfType<ClassDeclarationSyntax>().Select(c => c.Identifier.ValueText).ToList();
        var ns = string.Join('.', node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
        for (var i = 0; i < classes.Count; i++)
            yield return string.Join('.', classes.Skip(i).Reverse().Prepend(ns));
    }

    /// <summary>One <c>dotnet test</c> of this assembly as CI runs it: a step per matrix value, its filter resolved to a
    /// literal, and the env the step sees (workflow, then job, then step).</summary>
    private sealed record ResolvedStep(string Name, string Filter, IReadOnlyDictionary<string, string> Env);

    private enum FilterOp { Equals, NotEquals, Contains, NotContains }

    private record FilterClause(string Property, FilterOp Op, string Value);

    private record TestInfo(string Fqn, Dictionary<string, List<string>> Traits);

    // ---- Filter expressions -------------------------------------------------------------------
    // The vstest grammar subset the workflow uses: clauses `Property{=,!=,~,!~}Value` joined by
    // `&` and `|`, grouped by parentheses. `&` binds tighter than `|` (as in vstest). Anything
    // else — an empty clause, an operator-less clause, an unbalanced parenthesis, a `\` escape —
    // throws, so a filter this evaluator cannot read fails the guard instead of passing it.

    private abstract record FilterNode;

    private sealed record ClauseNode(FilterClause Clause) : FilterNode;

    private sealed record AndNode(IReadOnlyList<FilterNode> Operands) : FilterNode;

    private sealed record OrNode(IReadOnlyList<FilterNode> Operands) : FilterNode;

    private static FilterNode ParseFilter(string filter)
    {
        var pos = 0;
        var node = ParseOr(filter, ref pos);
        if (pos != filter.Length)
            throw Unsupported(filter, $"unexpected '{filter[pos]}' at offset {pos}");
        return node;
    }

    private static FilterNode ParseOr(string s, ref int pos)
    {
        var operands = new List<FilterNode> { ParseAnd(s, ref pos) };
        while (pos < s.Length && s[pos] == '|')
        {
            pos++;
            operands.Add(ParseAnd(s, ref pos));
        }

        return operands.Count == 1 ? operands[0] : new OrNode(operands);
    }

    private static FilterNode ParseAnd(string s, ref int pos)
    {
        var operands = new List<FilterNode> { ParsePrimary(s, ref pos) };
        while (pos < s.Length && s[pos] == '&')
        {
            pos++;
            operands.Add(ParsePrimary(s, ref pos));
        }

        return operands.Count == 1 ? operands[0] : new AndNode(operands);
    }

    private static FilterNode ParsePrimary(string s, ref int pos)
    {
        if (pos < s.Length && s[pos] == '(')
        {
            pos++;
            var inner = ParseOr(s, ref pos);
            if (pos >= s.Length || s[pos] != ')')
                throw Unsupported(s, $"unbalanced '(' before offset {pos}");
            pos++;
            return inner;
        }

        var start = pos;
        while (pos < s.Length && s[pos] is not ('&' or '|' or '(' or ')'))
        {
            if (s[pos] == '\\')
                throw Unsupported(s, "escaped characters are not supported");
            pos++;
        }

        return new ClauseNode(ParseClause(s[start..pos]));
    }

    private static InvalidOperationException Unsupported(string filter, string why)
        => new($"Unsupported filter syntax in '{filter}': {why}. Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");

    private static FilterClause ParseClause(string part)
    {
        string property;
        FilterOp op;
        string value;

        if (part.Contains("!~"))
        {
            var idx = part.IndexOf("!~", StringComparison.Ordinal);
            (property, op, value) = (part[..idx], FilterOp.NotContains, part[(idx + 2)..]);
        }
        else if (part.Contains("!="))
        {
            var idx = part.IndexOf("!=", StringComparison.Ordinal);
            (property, op, value) = (part[..idx], FilterOp.NotEquals, part[(idx + 2)..]);
        }
        else if (part.Contains('~'))
        {
            var idx = part.IndexOf('~');
            (property, op, value) = (part[..idx], FilterOp.Contains, part[(idx + 1)..]);
        }
        else if (part.Contains('='))
        {
            var idx = part.IndexOf('=');
            (property, op, value) = (part[..idx], FilterOp.Equals, part[(idx + 1)..]);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported filter syntax: '{part}'. Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
        }

        if (property is not ("Category" or "FullyQualifiedName"))
        {
            throw new InvalidOperationException(
                $"Unsupported filter property: '{property}'. Update the evaluator.");
        }

        if (value.Length == 0)
        {
            throw new InvalidOperationException(
                $"Unsupported filter syntax: '{part}' has an empty value. Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
        }

        return new FilterClause(property, op, value);
    }

    private static bool EvaluateFilter(string filter, TestInfo test)
        => Evaluate(ParseFilter(filter), test);

    private static bool Evaluate(FilterNode node, TestInfo test) => node switch
    {
        ClauseNode c => EvaluateClause(c.Clause, test),
        AndNode a => a.Operands.All(o => Evaluate(o, test)),
        OrNode o => o.Operands.Any(x => Evaluate(x, test)),
        _ => throw new InvalidOperationException($"Unknown filter node {node.GetType().Name}"),
    };

    private static IEnumerable<FilterClause> Clauses(FilterNode node) => node switch
    {
        ClauseNode c => new[] { c.Clause },
        AndNode a => a.Operands.SelectMany(Clauses),
        OrNode o => o.Operands.SelectMany(Clauses),
        _ => throw new InvalidOperationException($"Unknown filter node {node.GetType().Name}"),
    };

    private static bool EvaluateClause(FilterClause clause, TestInfo test)
    {
        if (clause.Property == "FullyQualifiedName")
        {
            return clause.Op switch
            {
                FilterOp.Equals => test.Fqn == clause.Value,
                FilterOp.NotEquals => test.Fqn != clause.Value,
                FilterOp.Contains => test.Fqn.Contains(clause.Value, StringComparison.Ordinal),
                FilterOp.NotContains => !test.Fqn.Contains(clause.Value, StringComparison.Ordinal),
                _ => throw new InvalidOperationException()
            };
        }

        var vals = test.Traits.GetValueOrDefault(clause.Property, new List<string>());
        return clause.Op switch
        {
            FilterOp.Equals => vals.Contains(clause.Value),
            FilterOp.NotEquals => !vals.Contains(clause.Value),
            FilterOp.Contains => vals.Any(v => v.Contains(clause.Value, StringComparison.Ordinal)),
            FilterOp.NotContains => !vals.Any(v => v.Contains(clause.Value, StringComparison.Ordinal)),
            _ => throw new InvalidOperationException()
        };
    }

    private static bool IsExempt(TestInfo test)
        => Exemptions.Any(e => EvaluateFilter(e.Predicate, test));

    private static List<string> ExtractFqnSubstrings(string filter)
        => Clauses(ParseFilter(filter))
            .Where(c => c.Property == "FullyQualifiedName" && c.Op is FilterOp.Contains or FilterOp.NotContains)
            .Select(c => c.Value)
            .ToList();

    // ---- Workflow resolution ----------------------------------------------------------------
    // Since #2178 the Compiler.Tests steps are matrix jobs whose --filter is spelled with
    // `${{ matrix.<key> }}` and shell references to the workflow/job/step `env:` (e.g.
    // "$COMPILER_MAIN_FILTER&$COMPILER_SHARD_${{ matrix.shard }}"). Each step is expanded once per
    // matrix combination into a concrete filter BEFORE evaluation. Parsed with YamlDotNet — the
    // parser GitHub's runner uses — so escaped-newline scalars join exactly as Actions joins them.
    // Any reference that cannot be resolved to a literal throws.

    private static readonly Regex CompilerTestCommand =
        new(@"^[^\S\n]*dotnet\s+test\s+\S*Sharpy\.Compiler\.Tests\S*(\s.*)?$", RegexOptions.Multiline);

    private static readonly Regex FilterArgument = new(@"--filter\s+""([^""]+)""");

    private static readonly Regex MatrixExpression = new(@"\$\{\{\s*matrix\.([A-Za-z0-9_-]+)\s*\}\}");

    private static readonly Regex ShellVariable = new(@"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))");

    private static List<ResolvedStep> ExtractCompilerTestSteps(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var workflowEnv = ReadEnv(root);

        var resolved = new List<ResolvedStep>();
        foreach (var (jobKey, jobNode) in Child<YamlMappingNode>(root, "jobs")!.Children)
        {
            var job = (YamlMappingNode)jobNode;
            var jobEnv = Merge(workflowEnv, ReadEnv(job));
            var combinations = MatrixCombinations(job);
            var steps = Child<YamlSequenceNode>(job, "steps");
            if (steps == null)
                continue;

            foreach (var stepNode in steps.Children)
            {
                var step = (YamlMappingNode)stepNode;
                if (Child<YamlScalarNode>(step, "run")?.Value is not { } run)
                    continue;
                var stepEnv = Merge(jobEnv, ReadEnv(step));

                foreach (Match command in CompilerTestCommand.Matches(run))
                {
                    var line = command.Value;
                    // `--list-tests` enumerates without running anything (the shard-partition job):
                    // counting it as coverage would make the guard vacuous.
                    if (line.Contains("--list-tests", StringComparison.Ordinal))
                        continue;
                    var filterArgs = FilterArgument.Matches(line);
                    if (filterArgs.Count != 1)
                        throw new InvalidOperationException(
                            $"Compiler.Tests command without exactly one quoted --filter: '{line.Trim()}'. " +
                            $"Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
                    var raw = filterArgs[0].Groups[1].Value;
                    var stepName = Child<YamlScalarNode>(step, "name")?.Value ?? "(unnamed)";
                    foreach (var matrix in combinations)
                    {
                        var label = $"{jobKey}/{stepName}" + (matrix.Count == 0 ? "" : $" [{string.Join(", ", matrix.Select(m => $"{m.Key}={m.Value}"))}]");
                        resolved.Add(new ResolvedStep(label, ResolveFilter(raw, matrix, stepEnv), stepEnv));
                    }
                }
            }
        }

        return resolved;
    }

    private static string ResolveFilter(
        string raw, IReadOnlyDictionary<string, string> matrix, IReadOnlyDictionary<string, string> env)
    {
        var withMatrix = MatrixExpression.Replace(raw, m =>
            matrix.TryGetValue(m.Groups[1].Value, out var v)
                ? v
                : throw new InvalidOperationException(
                    $"Unresolvable '${{{{ matrix.{m.Groups[1].Value} }}}}' in filter '{raw}': the job's matrix has no such key."));

        var resolved = ShellVariable.Replace(withMatrix, m =>
        {
            var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (!env.TryGetValue(name, out var v))
                throw new InvalidOperationException(
                    $"Unresolvable '${name}' in filter '{raw}': not defined in the workflow, job or step env.");
            if (v.Contains('$', StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Unresolvable '${name}' in filter '{raw}': its env value is itself an expression ('{v}').");
            return v;
        });

        if (resolved.Contains('$', StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Unresolvable reference left in filter '{raw}' after expansion: '{resolved}'. " +
                $"Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
        return resolved;
    }

    /// <summary>
    /// The job's matrix combinations: one empty combination for a non-matrix job; the cartesian
    /// product for `key: [values]` axes; one combination per entry for an `include`-only matrix.
    /// Any other matrix shape (include alongside axes, exclude, expressions) throws.
    /// </summary>
    private static List<Dictionary<string, string>> MatrixCombinations(YamlMappingNode job)
    {
        var strategy = Child<YamlMappingNode>(job, "strategy");
        var matrix = strategy == null ? null : Child<YamlNode>(strategy, "matrix");
        if (matrix == null)
            return new List<Dictionary<string, string>> { new() };
        if (matrix is not YamlMappingNode map)
            throw new InvalidOperationException($"Unsupported matrix shape: '{matrix}'. Update the evaluator.");

        var keys = map.Children.Keys.Select(k => ((YamlScalarNode)k).Value!).ToList();
        if (keys.Contains("exclude") || (keys.Contains("include") && keys.Count > 1))
            throw new InvalidOperationException(
                $"Unsupported matrix shape (keys: {string.Join(", ", keys)}). Update the evaluator.");

        if (keys.Contains("include"))
        {
            return ((YamlSequenceNode)map.Children[new YamlScalarNode("include")]).Children
                .Select(entry => ((YamlMappingNode)entry).Children.ToDictionary(
                    kv => ((YamlScalarNode)kv.Key).Value!,
                    kv => kv.Value is YamlScalarNode s
                        ? s.Value!
                        : throw new InvalidOperationException($"Unsupported non-scalar matrix value '{kv.Value}'.")))
                .ToList();
        }

        var combinations = new List<Dictionary<string, string>> { new() };
        foreach (var key in keys)
        {
            if (map.Children[new YamlScalarNode(key)] is not YamlSequenceNode axis)
                throw new InvalidOperationException($"Unsupported matrix axis '{key}' (not a list). Update the evaluator.");
            combinations = combinations
                .SelectMany(c => axis.Children.Select(v => new Dictionary<string, string>(c)
                {
                    [key] = v is YamlScalarNode s
                        ? s.Value!
                        : throw new InvalidOperationException($"Unsupported non-scalar matrix value '{v}'."),
                }))
                .ToList();
        }

        return combinations;
    }

    private static Dictionary<string, string> ReadEnv(YamlMappingNode node)
    {
        var env = Child<YamlMappingNode>(node, "env");
        return env == null
            ? new Dictionary<string, string>()
            : env.Children.ToDictionary(
                kv => ((YamlScalarNode)kv.Key).Value!,
                kv => kv.Value is YamlScalarNode s
                    ? s.Value!
                    : throw new InvalidOperationException($"Unsupported non-scalar env value '{kv.Value}'."));
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> outer, IReadOnlyDictionary<string, string> inner)
    {
        var merged = new Dictionary<string, string>(outer);
        foreach (var (k, v) in inner)
            merged[k] = v;
        return merged;
    }

    private static T? Child<T>(YamlMappingNode node, string key) where T : YamlNode
        => node.Children.TryGetValue(new YamlScalarNode(key), out var child) ? (T)child : null;

    private static List<TestInfo> ReflectTests()
    {
        var tests = new List<TestInfo>();
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            var classTraits = CollectTraits(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // Every xunit test attribute derives from FactAttribute ([Theory], and the
                // [SkippableFact]/[SkippableTheory] of the python twins, #2342) — match by
                // derivation, never by name, or a subclassed test drops out of the universe.
                if (!method.GetCustomAttributesData().Any(a =>
                        typeof(FactAttribute).IsAssignableFrom(a.AttributeType)))
                    continue;

                var merged = MergeTraits(classTraits, CollectTraits(method));
                tests.Add(new TestInfo($"{type.FullName!.Replace('+', '.')}.{method.Name}", merged));
            }
        }

        return tests;
    }

    private static Dictionary<string, List<string>> CollectTraits(MemberInfo member)
    {
        var traits = new Dictionary<string, List<string>>();
        foreach (var attr in member.GetCustomAttributesData())
        {
            if (attr.AttributeType.Name != "TraitAttribute" || attr.ConstructorArguments.Count < 2)
                continue;
            var name = attr.ConstructorArguments[0].Value?.ToString() ?? "";
            var value = attr.ConstructorArguments[1].Value?.ToString() ?? "";
            if (!traits.TryGetValue(name, out var list))
                traits[name] = list = new List<string>();
            list.Add(value);
        }

        return traits;
    }

    private static Dictionary<string, List<string>> MergeTraits(
        Dictionary<string, List<string>> a, Dictionary<string, List<string>> b)
    {
        var merged = new Dictionary<string, List<string>>(
            a.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value)));
        foreach (var (key, values) in b)
        {
            if (!merged.TryGetValue(key, out var list))
                merged[key] = list = new List<string>();
            list.AddRange(values);
        }

        return merged;
    }

    private static string FindRepoRoot()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "src")) &&
                Directory.Exists(Path.Combine(dir, ".github")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find repo root (walked up from assembly looking for src/ + .github/)");
    }
}

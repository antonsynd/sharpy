using System.Reflection;
using System.Text.RegularExpressions;
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
    /// Plan-3eba57 D4.1: a sweep runs EXACTLY once. The sweeps are the tests named by a
    /// <c>FullyQualifiedName!~X</c> token of <c>COMPILER_MAIN_FILTER</c> (excluded from the shards)
    /// together with the tests named by a <c>FullyQualifiedName~X</c> token of a step that selects
    /// <c>Category=GapDiscovery</c> (the dedicated sweep steps) — the second half keeps a sweep in
    /// the subject when its <c>!~</c> exclusion is dropped, which is exactly the defect (it would
    /// then run in a shard AND in its own step). Every non-exempt such test is selected by exactly
    /// one resolved step across ALL jobs (each matrix combination is its own step, so the seven
    /// Compiler shards count seven). Judged per TEST, not per class: <c>InProcessIsolationSweepTests</c>
    /// is two populations by design (its GapDiscovery mode in the `sweep` job, its regular slice in
    /// a shard), each run once.
    /// </summary>
    [Fact]
    public void EverySweep_IsSelectedByExactlyOneStep()
    {
        var (steps, workflowEnv, tests) = LoadStepsAndTests();

        Assert.True(workflowEnv.TryGetValue("COMPILER_MAIN_FILTER", out var mainFilter),
            "COMPILER_MAIN_FILTER is not defined in the workflow env. Workflow restructured?");
        var excluded = Clauses(ParseFilter(mainFilter!))
            .Where(c => c.Property == "FullyQualifiedName" && c.Op == FilterOp.NotContains)
            .Select(c => c.Value);
        var swept = steps
            .Select(s => Clauses(ParseFilter(s.Filter)).ToList())
            .Where(cs => cs.Any(c => c is { Property: "Category", Op: FilterOp.Equals, Value: "GapDiscovery" }))
            .SelectMany(cs => cs.Where(c => c is { Property: "FullyQualifiedName", Op: FilterOp.Contains }).Select(c => c.Value));
        var tokens = excluded.Concat(swept).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();

        var subjects = new Dictionary<string, int>();
        var wrong = new List<string>();
        foreach (var token in tokens)
        {
            subjects[token] = 0;
            foreach (var test in tests.Where(t => t.Fqn.Contains(token, StringComparison.Ordinal) && !IsExempt(t)))
            {
                subjects[token]++;
                var selecting = steps.Where(s => EvaluateFilter(s.Filter, test)).Select(s => s.Label).ToList();
                if (selecting.Count != 1)
                    wrong.Add($"  {test.Fqn} — selected by {selecting.Count} step(s)" +
                              (selecting.Count == 0 ? "" : $": {string.Join("; ", selecting)}"));
            }
        }

        // Positive control: the subject is the seven sweeps the workflow names today, each with tests.
        Assert.True(tokens.Count >= 7,
            $"only {tokens.Count} sweep token(s) found ({string.Join(", ", tokens)}): the !~ / Category=GapDiscovery reading went blind.");
        var empty = subjects.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
        Assert.True(empty.Count == 0, $"sweep token(s) naming no test: {string.Join(", ", empty)}");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} sweep test(s) not selected by exactly one CI step " +
            $"(subjects per token: {string.Join(", ", subjects.Select(kv => $"{kv.Key}={kv.Value}"))}):\n" +
            string.Join("\n", wrong.Take(50)) +
            (wrong.Count > 50 ? $"\n  … and {wrong.Count - 50} more" : "") +
            "\nA sweep excluded from COMPILER_MAIN_FILTER must have its own step, and a sweep with its own " +
            "step must be excluded from COMPILER_MAIN_FILTER.");
    }

    /// <summary>
    /// Plan-3eba57 D4.2: a python-oracle test never runs where a missing python is a skip. The
    /// oracle tests are the test classes of every <c>[Fact]</c>/<c>[Theory]</c> (skippable or
    /// not) of Sharpy.Compiler.Tests whose body calls <c>PythonInterpreter.RequireOrSkip(</c>
    /// (Roslyn scan, the shape of <see cref="PythonOracleSkipCensusTests"/>' walk; a call inside a
    /// lambda of the test body counts — it runs too; a helper method is not a site). Every resolved
    /// step whose filter selects a test of such a class must run with
    /// <see cref="PythonInterpreter.RequireEnvironmentVariable"/> = "1" in its resolved env
    /// (workflow ∪ job ∪ step): otherwise a runner that lost `setup-python` reports the oracle
    /// skipped and the job stays green — a skipped sweep guards nothing.
    /// </summary>
    [Fact]
    public void EveryPythonOracleTest_RunsWithTheOracleRequired()
    {
        var (steps, _, tests) = LoadStepsAndTests();
        var testsRoot = Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler.Tests");

        var oracleClasses = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
            if (rel.StartsWith("bin/", StringComparison.Ordinal) || rel.StartsWith("obj/", StringComparison.Ordinal)
                || rel.Contains("TestFixtures/", StringComparison.Ordinal))
                continue;
            oracleClasses.UnionWith(PythonOracleTestClasses(rel, File.ReadAllText(file)));
        }

        // Positive control: the three sweeps that reach the oracle through their own direct
        // RequireOrSkip calls — a scan that misses one of them is blind, not clean.
        var simpleNames = oracleClasses.Select(c => c[(c.LastIndexOf('.') + 1)..]).ToHashSet(StringComparer.Ordinal);
        foreach (var pinned in new[] { "DifferentialExecutionTests", "FormatSpecDifferentialTests", "NameResolutionDifferentialTests" })
            Assert.True(simpleNames.Contains(pinned),
                $"the RequireOrSkip scan did not select {pinned} (selected: {string.Join(", ", oracleClasses)}): the scan went blind.");

        static string ClassOf(TestInfo t) => t.Fqn[..t.Fqn.LastIndexOf('.')];
        var oracleTests = tests.Where(t => oracleClasses.Contains(ClassOf(t))).ToList();
        var unreflected = oracleClasses.Except(oracleTests.Select(ClassOf)).ToList();
        Assert.True(unreflected.Count == 0,
            $"scanned oracle class(es) with no reflected test — the scan's class naming diverged from reflection: {string.Join(", ", unreflected)}");

        var unguarded = new List<string>();
        var checkedSteps = 0;
        foreach (var step in steps)
        {
            var selected = oracleTests.Where(t => EvaluateFilter(step.Filter, t)).Select(ClassOf).Distinct().ToList();
            if (selected.Count == 0)
                continue;
            checkedSteps++;
            if (step.Env.GetValueOrDefault(PythonInterpreter.RequireEnvironmentVariable) != "1")
                unguarded.Add($"  {step.Label} selects {string.Join(", ", selected.Select(c => c[(c.LastIndexOf('.') + 1)..]))}");
        }

        var unselected = oracleClasses.Where(c => !oracleTests.Any(t => ClassOf(t) == c && steps.Any(s => EvaluateFilter(s.Filter, t)))).ToList();
        Assert.True(unselected.Count == 0, $"oracle class(es) selected by no step, so not judged: {string.Join(", ", unselected)}");
        Assert.True(unguarded.Count == 0,
            $"{unguarded.Count} of {checkedSteps} CI step(s) run python-oracle tests without " +
            $"{PythonInterpreter.RequireEnvironmentVariable}=\"1\" in their workflow/job/step env — without python they SKIP green:\n" +
            string.Join("\n", unguarded) +
            $"\nSet {PythonInterpreter.RequireEnvironmentVariable}: \"1\" on the job (or the workflow).");
    }

    /// <summary>
    /// The test classes (namespace-qualified, nested types joined by '.', as reflection names them
    /// here) of the test methods in <paramref name="source"/> that call <c>PythonInterpreter.RequireOrSkip(</c>.
    /// </summary>
    private static IEnumerable<string> PythonOracleTestClasses(string path, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source, path: path).GetRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            bool isTest = method.AttributeLists.SelectMany(l => l.Attributes).Any(a =>
            {
                var name = a.Name.ToString();
                var simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                return new[] { "Fact", "Theory", "SkippableFact", "SkippableTheory" }
                    .Any(n => simple == n || simple == n + "Attribute");
            });
            if (!isTest)
                continue;
            bool callsGate = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(inv =>
                inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "RequireOrSkip" } member
                && member.Expression.ToString() is var receiver
                && (receiver == nameof(PythonInterpreter) || receiver.EndsWith("." + nameof(PythonInterpreter), StringComparison.Ordinal)));
            if (!callsGate)
                continue;

            var parts = new List<string>();
            foreach (var ancestor in method.Ancestors())
            {
                if (ancestor is TypeDeclarationSyntax type)
                    parts.Insert(0, type.Identifier.ValueText);
                else if (ancestor is BaseNamespaceDeclarationSyntax ns)
                    parts.Insert(0, ns.Name.ToString());
            }
            yield return string.Join(".", parts);
        }
    }

    /// <summary>
    /// Positive control for <see cref="PythonOracleTestClasses"/>: a direct call and a lambda-nested
    /// call in a test name their (nested) class; a helper method, another gate's RequireOrSkip and a
    /// non-gating PythonInterpreter call name nothing.
    /// </summary>
    [Fact]
    public void PythonOracleScan_NamesTheClassOfEveryGatedTest_AndNoHelperOrOtherGate()
    {
        const string Source = """
            namespace N.M;

            public class Direct
            {
                [SkippableFact]
                public void A() { var p = PythonInterpreter.RequireOrSkip(minMinor: 12); }
            }

            public class Outer
            {
                public class Nested
                {
                    [SkippableTheory]
                    public void B(int x) { Run(() => Sharpy.TestInfrastructure.Oracles.PythonInterpreter.RequireOrSkip()); }
                }
            }

            public class Helper { public static void H() { PythonInterpreter.RequireOrSkip(); } }

            public class OtherGate
            {
                [SkippableFact]
                public void C() { TestPreconditions.RequireOrSkip(true, "x"); }
            }

            public class NotAGate
            {
                [Fact]
                public void D() { PythonInterpreter.TryLocate(); }
            }
            """;
        Assert.Equal(new[] { "N.M.Direct", "N.M.Outer.Nested" },
            PythonOracleTestClasses("control.cs", Source).OrderBy(c => c, StringComparer.Ordinal));
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

    /// <summary>
    /// The step resolver behind D4.1/D4.2: one step per (job, step, matrix combination) — a matrix
    /// axis and an `include` list alike — each with its own literal filter and its resolved env
    /// (workflow ∪ job ∪ step, inner wins); other test projects and `--list-tests` runs are not
    /// steps; a `dotnet test` whose project it cannot read throws rather than drop out.
    /// </summary>
    [Fact]
    public void StepResolver_YieldsOneStepPerMatrixCombination_WithItsEnv_AndRefusesAnUnreadableCommand()
    {
        const string Yaml = """
            env:
              MAIN: "Category!=Benchmark"
              SHARD_1: "FullyQualifiedName~A."
              SHARD_2: "FullyQualifiedName~B."
              LEVEL: workflow
            jobs:
              shards:
                env:
                  LEVEL: job
                strategy:
                  matrix:
                    shard: [1, 2]
                steps:
                - name: Shard ${{ matrix.shard }}
                  run: |
                    echo "filter: $MAIN"
                    dotnet test src/Sharpy.Compiler.Tests --no-build --filter "$MAIN&$SHARD_${{ matrix.shard }}"
              sweeps:
                strategy:
                  matrix:
                    include:
                      - name: x
                        filter: "Category=GapDiscovery&FullyQualifiedName~X"
                steps:
                - name: Sweep ${{ matrix.name }}
                  env:
                    LEVEL: step
                  run: dotnet test src/Sharpy.Compiler.Tests --no-build --filter "${{ matrix.filter }}"
                - name: Other project
                  run: dotnet test src/Sharpy.Lsp.Tests --no-build --filter "Category!=Benchmark"
                - name: Listing
                  run: dotnet test "$1" --no-build --list-tests --filter "$2"
              folded:
                steps:
                - run: >
                    dotnet test src/Sharpy.Compiler.Tests
                    --filter "FullyQualifiedName~F"
            """;

        var steps = ExtractCompilerTestSteps(Yaml);
        Assert.Equal(new[] { "shards/Shard 1", "shards/Shard 2", "sweeps/Sweep x", "folded/step 0" }, steps.Select(s => s.Label));
        Assert.Equal(new[]
        {
            "Category!=Benchmark&FullyQualifiedName~A.",
            "Category!=Benchmark&FullyQualifiedName~B.",
            "Category=GapDiscovery&FullyQualifiedName~X",
            "FullyQualifiedName~F",
        }, steps.Select(s => s.Filter));
        Assert.Equal(new[] { "job", "job", "step", "workflow" }, steps.Select(s => s.Env["LEVEL"]));

        Assert.Throws<InvalidOperationException>(() => ExtractCompilerTestSteps(
            Yaml.Replace("dotnet test src/Sharpy.Lsp.Tests --no-build", "dotnet test --no-build src/Sharpy.Compiler.Tests", StringComparison.Ordinal)));
    }

    private (List<string> Filters, List<TestInfo> Tests) LoadFiltersAndTests()
    {
        var repoRoot = FindRepoRoot();
        var yaml = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "dotnet10.yml"));
        var filters = ExtractCompilerTestFilters(yaml);
        Assert.True(filters.Count >= 2,
            $"Expected ≥2 Compiler.Tests steps, found {filters.Count}. Workflow restructured?");
        return (filters, ReflectTests());
    }

    private (List<ResolvedStep> Steps, Dictionary<string, string> WorkflowEnv, List<TestInfo> Tests) LoadStepsAndTests()
    {
        var yaml = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "dotnet10.yml"));
        var steps = ExtractCompilerTestSteps(yaml);
        Assert.True(steps.Count >= 2,
            $"Expected ≥2 Compiler.Tests steps, found {steps.Count}. Workflow restructured?");
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (steps, ReadEnv((YamlMappingNode)stream.Documents[0].RootNode), ReflectTests());
    }

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

    private static readonly Regex DotnetTestCommand =
        new(@"^[^\S\n]*dotnet\s+test\b.*$", RegexOptions.Multiline);

    private static readonly Regex CompilerTestCommand =
        new(@"^[^\S\n]*dotnet\s+test\s+\S*Sharpy\.Compiler\.Tests\S*(\s.*)?$");

    private static readonly Regex OtherTestProjectCommand =
        new(@"^[^\S\n]*dotnet\s+test\s+src/Sharpy\.(?!Compiler\.Tests\b)[A-Za-z]+\.Tests(\s.*)?$");

    private static readonly Regex FilterArgument = new(@"--filter\s+""([^""]+)""");

    private static readonly Regex MatrixExpression = new(@"\$\{\{\s*matrix\.([A-Za-z0-9_-]+)\s*\}\}");

    private static readonly Regex ShellVariable = new(@"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))");

    /// <summary>
    /// One resolved Compiler.Tests step: a step of one job under one matrix combination (the seven
    /// Compiler shards are seven steps), its literal filter, and the env it runs under (workflow ∪
    /// job ∪ step, inner wins — the precedence Actions applies).
    /// </summary>
    private sealed record ResolvedStep(string Label, string Filter, IReadOnlyDictionary<string, string> Env);

    private static List<string> ExtractCompilerTestFilters(string yaml)
        => ExtractCompilerTestSteps(yaml).Select(s => s.Filter).ToList();

    private static List<ResolvedStep> ExtractCompilerTestSteps(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var workflowEnv = ReadEnv(root);

        var steps = new List<ResolvedStep>();
        foreach (var (jobKey, jobNode) in Child<YamlMappingNode>(root, "jobs")!.Children)
        {
            var jobId = ((YamlScalarNode)jobKey).Value!;
            var job = (YamlMappingNode)jobNode;
            var jobEnv = Merge(workflowEnv, ReadEnv(job));
            var combinations = MatrixCombinations(job);
            var jobSteps = Child<YamlSequenceNode>(job, "steps");
            if (jobSteps == null)
                continue;

            for (var index = 0; index < jobSteps.Children.Count; index++)
            {
                var step = (YamlMappingNode)jobSteps.Children[index];
                if (Child<YamlScalarNode>(step, "run")?.Value is not { } run)
                    continue;
                var stepEnv = Merge(jobEnv, ReadEnv(step));
                var stepName = Child<YamlScalarNode>(step, "name")?.Value ?? $"step {index}";

                foreach (Match test in DotnetTestCommand.Matches(run))
                {
                    var line = test.Value;
                    // `--list-tests` enumerates without running anything (the shard-partition job):
                    // counting it as coverage would make the guard vacuous.
                    if (line.Contains("--list-tests", StringComparison.Ordinal))
                        continue;
                    if (!CompilerTestCommand.IsMatch(line))
                    {
                        // Another test project's step. Anything else — the project after an option, a
                        // shell variable for the project — would be a Compiler.Tests step this
                        // resolver cannot see, so it refuses rather than under-counting.
                        if (OtherTestProjectCommand.IsMatch(line))
                            continue;
                        throw new InvalidOperationException(
                            $"`dotnet test` command in {jobId}/{stepName} whose project this resolver cannot read: " +
                            $"'{line.Trim()}'. Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
                    }

                    var filterArgs = FilterArgument.Matches(line);
                    if (filterArgs.Count != 1)
                        throw new InvalidOperationException(
                            $"Compiler.Tests command without exactly one quoted --filter: '{line.Trim()}'. " +
                            $"Update the evaluator in {nameof(CiFilterCoverageConformanceTests)}.");
                    var raw = filterArgs[0].Groups[1].Value;
                    foreach (var matrix in combinations)
                    {
                        var label = $"{jobId}/{MatrixExpression.Replace(stepName, m => matrix.GetValueOrDefault(m.Groups[1].Value, m.Value))}";
                        if (matrix.Count > 0 && label == $"{jobId}/{stepName}")
                            label += $" [{string.Join(", ", matrix.Select(kv => $"{kv.Key}={kv.Value}"))}]";
                        steps.Add(new ResolvedStep(label, ResolveFilter(raw, matrix, stepEnv), stepEnv));
                    }
                }
            }
        }

        return steps;
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

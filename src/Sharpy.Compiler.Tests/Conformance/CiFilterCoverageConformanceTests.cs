using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Guards that every [Fact]/[Theory] in Sharpy.Compiler.Tests is selected by at least one CI
/// workflow step. Scoped to this assembly only — other projects' steps filter only
/// Category!=Benchmark (no substring holes); extending cross-assembly would need one guard per
/// test project (#1556).
/// </summary>
public class CiFilterCoverageConformanceTests
{
    private static readonly (string Predicate, string Rationale)[] Exemptions =
    {
        ("Category=Benchmark", "benchmarks run on demand; excluded by every step by design"),
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

    private (List<string> Filters, List<TestInfo> Tests) LoadFiltersAndTests()
    {
        var repoRoot = FindRepoRoot();
        var yaml = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "dotnet10.yml"));
        var filters = ExtractCompilerTestFilters(yaml);
        Assert.True(filters.Count >= 2,
            $"Expected ≥2 Compiler.Tests steps, found {filters.Count}. Workflow restructured?");
        return (filters, ReflectTests());
    }

    private enum FilterOp { Equals, NotEquals, Contains, NotContains }

    private record FilterClause(string Property, FilterOp Op, string Value);

    private record TestInfo(string Fqn, Dictionary<string, List<string>> Traits);

    private static List<FilterClause> ParseFilter(string filter)
    {
        var clauses = new List<FilterClause>();
        foreach (var part in filter.Split('&'))
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

            clauses.Add(new FilterClause(property, op, value));
        }

        return clauses;
    }

    private static bool EvaluateFilter(string filter, TestInfo test)
        => ParseFilter(filter).All(c => EvaluateClause(c, test));

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
        => ParseFilter(filter)
            .Where(c => c.Property == "FullyQualifiedName" && c.Op is FilterOp.Contains or FilterOp.NotContains)
            .Select(c => c.Value)
            .ToList();

    private static List<string> ExtractCompilerTestFilters(string yaml)
    {
        var regex = new Regex(@"dotnet\s+test\s+\S*Sharpy\.Compiler\.Tests\S*\s+.*?--filter\s+""([^""]+)""");
        return regex.Matches(yaml).Select(m => m.Groups[1].Value).ToList();
    }

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
                if (!method.GetCustomAttributesData().Any(a =>
                        a.AttributeType.Name is "FactAttribute" or "TheoryAttribute"))
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

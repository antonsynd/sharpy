using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The census behind #2342 (R-HZ; verification contract §2): a test that cannot reach its python
/// oracle reports SKIPPED, never PASSED. Two rules over every C# source of every test project
/// (<c>src/*.Tests/</c> and <c>src/Sharpy.TestInfrastructure/</c>):
/// <list type="number">
/// <item>no string (plain or interpolated) starts with <c>SKIP:</c> — the write-and-return idiom
/// that xunit counts Passed;</item>
/// <item>a <c>[Fact]</c>/<c>[Theory]</c> that calls <see cref="PythonInterpreter.RequireOrSkip(int)"/>
/// is a <c>[SkippableFact]</c>/<c>[SkippableTheory]</c> — under a plain <c>[Fact]</c> the
/// helper's <c>SkipException</c> is a FAILURE, not a skip.</item>
/// </list>
/// Syntax-only (Roslyn parse, no compilation), so the scan cannot be blinded by a build setting.
/// </summary>
public class PythonOracleSkipCensusTests
{
    /// <summary>
    /// The python-twin tests that open with <c>RequireOrSkip</c> at #2342 (the eight former
    /// <c>SKIP:</c> sites plus the helper's own located-verdict test): a floor, anchored as a
    /// literal so a scan that finds no file cannot pass vacuously.
    /// </summary>
    private const int RequireOrSkipCallerFloor = 8;

    /// <summary>Spelled in two pieces so this file's own literals do not match the rule they state.</summary>
    private const string SkipMarker = "SKIP" + ":";

    private sealed record Violation(string File, int Line, string Rule);

    private static IEnumerable<string> TestSourceFiles(string repoRoot)
    {
        var src = Path.Combine(repoRoot, "src");
        var roots = Directory.GetDirectories(src)
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return name.EndsWith(".Tests", StringComparison.Ordinal) || name == "Sharpy.TestInfrastructure";
            });
        foreach (var root in roots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                // Build output and the fixtures' generated C# snapshots are not test sources.
                if (rel.StartsWith("bin/", StringComparison.Ordinal) || rel.StartsWith("obj/", StringComparison.Ordinal)
                    || rel.Contains("/TestFixtures/", StringComparison.Ordinal) || rel.StartsWith("Integration/TestFixtures/", StringComparison.Ordinal))
                {
                    continue;
                }
                yield return file;
            }
        }
    }

    private static string StringText(SyntaxNode node) => node switch
    {
        LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression) => lit.Token.ValueText,
        // The text before the first hole: `$"SKIP: {why}"` starts with "SKIP: ".
        InterpolatedStringExpressionSyntax interp => string.Concat(
            interp.Contents.TakeWhile(c => c is InterpolatedStringTextSyntax)
                .Cast<InterpolatedStringTextSyntax>().Select(t => t.TextToken.ValueText)),
        _ => "",
    };

    private static bool HasAttribute(MethodDeclarationSyntax method, params string[] names) =>
        method.AttributeLists.SelectMany(l => l.Attributes).Any(a =>
        {
            var name = a.Name.ToString();
            var simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
            return names.Any(n => simple == n || simple == n + "Attribute");
        });

    /// <summary>Scans one source; returns the violations and how many test methods call RequireOrSkip.</summary>
    private static (List<Violation> Violations, int RequireOrSkipCallers) Scan(string path, string source)
    {
        var violations = new List<Violation>();
        var root = CSharpSyntaxTree.ParseText(source, path: path).GetRoot();

        foreach (var node in root.DescendantNodes().Where(n => n is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax))
        {
            if (StringText(node).TrimStart().StartsWith(SkipMarker, StringComparison.Ordinal))
            {
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(new Violation(path, line, "a 'SKIP:' string — write-and-return counts Passed; call PythonInterpreter.RequireOrSkip"));
            }
        }

        int callers = 0;
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // The gate is a statement of the test body; a call inside a lambda (Assert.Throws(() =>
            // RequireOrSkip(...)), the helper's own verdict tests) runs under the lambda's caller.
            bool callsHelper = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(inv =>
                (inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "RequireOrSkip" }
                    or IdentifierNameSyntax { Identifier.ValueText: "RequireOrSkip" })
                && !inv.Ancestors().TakeWhile(a => a != method).Any(a => a is AnonymousFunctionExpressionSyntax));
            if (!callsHelper)
                continue;
            if (HasAttribute(method, "SkippableFact", "SkippableTheory"))
            {
                callers++;
            }
            else if (HasAttribute(method, "Fact", "Theory"))
            {
                int line = method.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(new Violation(path, line,
                    $"{method.Identifier.ValueText} calls RequireOrSkip under [Fact]/[Theory] — its SkipException FAILS there; use [SkippableFact]/[SkippableTheory]"));
            }
        }
        return (violations, callers);
    }

    [Fact]
    public void NoTestWritesSkipAndReturns_AndEveryRequireOrSkipCallerIsSkippable()
    {
        var sln = PythonInterpreter.FindRepoFile("sharpy.sln")
            ?? throw new FileNotFoundException("sharpy.sln not found above the test binary.");
        var repoRoot = Path.GetDirectoryName(sln)!;

        var violations = new List<Violation>();
        int files = 0, callers = 0;
        foreach (var file in TestSourceFiles(repoRoot))
        {
            files++;
            var (v, c) = Scan(Path.GetRelativePath(repoRoot, file), File.ReadAllText(file));
            violations.AddRange(v);
            callers += c;
        }

        Assert.True(files > 500, $"the census scanned only {files} test source files — the repository walk is broken.");
        Assert.True(violations.Count == 0,
            $"{violations.Count} python-oracle skip violation(s) (#2342):\n"
            + string.Join("\n", violations.Select(v => $"  {v.File}:{v.Line}: {v.Rule}")));
        Assert.True(callers >= RequireOrSkipCallerFloor,
            $"only {callers} [SkippableFact]/[SkippableTheory] tests call RequireOrSkip (floor {RequireOrSkipCallerFloor}): a python twin lost its gate, or the scan went blind.");
    }

    [Fact]
    public void Census_FiresOnEachForbiddenShape()
    {
        // Positive control: each rule fires on a source built to break it, and the allowed shape passes.
        const string Broken = """
            public class T
            {
                [Fact]
                public void A()
                {
                    var python = Locate();
                    if (python is null)
                    {
                        _output.WriteLine("SKIP: no python");
                        return;
                    }
                }

                [Fact]
                public void B() { Output.WriteLine($"SKIP: {why}; nothing ran"); }

                [Theory]
                public void C(string s) { PythonInterpreter.RequireOrSkip(minMinor: 12); }

                [SkippableFact]
                public void D() { PythonInterpreter.RequireOrSkip(minMinor: 12); }

                [SkippableTheory]
                public void E(string s) { RequireOrSkip(); }

                [Fact]
                public void F() { Assert.Throws<SkipException>(() => PythonInterpreter.RequireOrSkip(none, required: false)); }
            }
            """;
        var (violations, callers) = Scan("control.cs", Broken);
        Assert.Equal(new[] { 9, 15, 18 }, violations.Select(v => v.Line).ToArray());
        Assert.Equal(2, callers);
    }
}

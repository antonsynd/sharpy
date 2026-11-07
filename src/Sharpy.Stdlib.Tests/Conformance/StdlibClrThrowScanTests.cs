using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// A Stdlib boundary raises Python's exception for an expected failure of user input, never a CLR
/// <c>ArgumentException</c> / <c>ArgumentOutOfRangeException</c> / <c>InvalidOperationException</c> /
/// <c>NotSupportedException</c> that <c>except ValueError</c> cannot catch (#2461; CLAUDE.md "Core &amp;
/// Stdlib Conventions": expected failure is a Python exception or a <c>Result</c>, a CLR throw is for
/// bugs only).
///
/// <para><b>Scan 1 (source).</b> A Roslyn syntax scan over <c>src/Sharpy.Stdlib/**/*.cs</c> (<c>bin</c>,
/// <c>obj</c> and the two directories the project file removes from compilation, <c>spy/</c> and
/// <c>modules/</c>, excluded, as <see cref="StdlibModuleGlobalRosterTests"/> does) for every
/// <c>throw new X(...)</c> statement or expression whose type is one of the four. Each is keyed
/// <c>file:Type.Member:X</c> (the enclosing type and member, never a line number) and counted; the
/// multiset must equal <c>Conformance/stdlib-clr-throw-allowlist.txt</c>, where each row cites #2461
/// and names why the throw is a bug guard. An unlisted throw is red; a listed one the scan no longer
/// finds is red (stale: delete the row).</para>
///
/// <para><b>Scan 2 (spy tests).</b> No spy test asserts a CLR exception type:
/// <c>assert_raises(&lt;name&gt;Exception)</c> or <c>except &lt;name&gt;Exception</c> over
/// <c>src/Sharpy.Stdlib.Tests/Spy/**/*.spy</c> is red, bare <c>Exception</c> aside. Python's own
/// exception names end in <c>Error</c> (argparse's <c>ArgumentError</c> included), so they never
/// match.</para>
/// </summary>
public class StdlibClrThrowScanTests
{
    private static readonly HashSet<string> ClrTypes = new(StringComparer.Ordinal)
    {
        "ArgumentException", "ArgumentOutOfRangeException", "InvalidOperationException", "NotSupportedException",
    };

    // The net10.0 view, as StdlibModuleGlobalRosterTests parses it: `#if NET10_0_OR_GREATER` blocks
    // (Toml's typed door, Tarfile) are scanned, not skipped as inactive trivia.
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview,
        preprocessorSymbols: new[] { "NET", "NET10_0", "NET10_0_OR_GREATER", "NETCOREAPP", "RELEASE" });

    private static readonly Regex AllowlistRow = new(
        @"^(?<key>\S+:\S+:\S+) x(?<n>\d+) # #2461 (?<reason>\S.*)$", RegexOptions.Compiled);

    private static readonly Regex SpyClrAssertion = new(
        @"(?:assert_raises\(\s*|except\s+\(?\s*)(?:[A-Za-z_][\w]*\.)*(?<type>[A-Za-z_]\w*Exception)\b",
        RegexOptions.Compiled);

    [Fact]
    public void EveryStdlibClrThrowIsAnAllowlistedBugGuard()
    {
        var found = ScanStdlib();
        var allowed = ReadAllowlist();

        var unlisted = found.Where(kv => !allowed.TryGetValue(kv.Key, out var n) || n != kv.Value)
            .Select(kv => $"{kv.Key} x{kv.Value}" + (allowed.TryGetValue(kv.Key, out var m) ? $" (allowlist says x{m})" : ""))
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        var stale = allowed.Where(kv => !found.ContainsKey(kv.Key))
            .Select(kv => $"{kv.Key} x{kv.Value}").OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.True(unlisted.Count == 0 && stale.Count == 0,
            "Stdlib CLR throws disagree with Conformance/stdlib-clr-throw-allowlist.txt (#2461).\n"
            + "An expected failure of user input raises Python's exception (ValueError, TypeError, IndexError, ...);\n"
            + "only an unreachable bug guard may stay, as a row citing #2461 with its reason.\n"
            + "Unlisted (convert, or allowlist with a reason):\n  " + string.Join("\n  ", unlisted)
            + "\nStale (delete the row):\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void NoSpyTestAssertsAClrExceptionType()
    {
        var root = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Stdlib.Tests", "Spy");
        var hits = new System.Collections.Generic.List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var type in SpyClrAssertionTypes(lines[i]))
                    hits.Add($"{SysPath.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/')}:{i + 1}: {type}");
            }
        }

        Assert.True(hits.Count == 0,
            "Spy tests assert a CLR exception type; assert the Python exception the Stdlib raises instead (#2461):\n  "
            + string.Join("\n  ", hits));
    }

    /// <summary>Positive control: the source scan finds and keys a throw of each of the four types.</summary>
    [Fact]
    public void PositiveControl_TheSourceScanKeysEachClrThrowShape()
    {
        const string source = """
            namespace Sharpy {
              public class Widget {
                public Widget(int n) { if (n < 0) throw new System.ArgumentException("x"); }
                public int Size { set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); } }
                public int Pick(int k) => k switch { 0 => 1, _ => throw new InvalidOperationException() };
                public void Run() { throw new NotSupportedException("y"); }
                public void Fine() { throw new ValueError("z"); }
                public void Null(object o) { if (o == null) throw new ArgumentNullException(nameof(o)); }
              }
            }
            """;
        var keys = ScanTree(CSharpSyntaxTree.ParseText(source), "f.cs");
        Assert.Equal(
            new[]
            {
                "f.cs:Widget.Pick:InvalidOperationException",
                "f.cs:Widget.Run:NotSupportedException",
                "f.cs:Widget.Size:ArgumentOutOfRangeException",
                "f.cs:Widget.Widget:ArgumentException",
            },
            keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>Positive control: the spy scan matches a CLR type and spares Python's names.</summary>
    [Fact]
    public void PositiveControl_TheSpyScanMatchesClrTypesOnly()
    {
        Assert.Equal(new[] { "ArgumentException" }, SpyClrAssertionTypes("    with assert_raises(ArgumentException):"));
        Assert.Equal(new[] { "InvalidOperationException" }, SpyClrAssertionTypes("    with assert_raises(system.InvalidOperationException):"));
        Assert.Equal(new[] { "NotSupportedException" }, SpyClrAssertionTypes("    except NotSupportedException as e:"));
        Assert.Empty(SpyClrAssertionTypes("    with assert_raises(ArgumentError):"));
        Assert.Empty(SpyClrAssertionTypes("    with assert_raises(ValueError):"));
        Assert.Empty(SpyClrAssertionTypes("    with assert_raises(Exception):"));
        Assert.Empty(SpyClrAssertionTypes("    except Exception:"));
    }

    private static IEnumerable<string> SpyClrAssertionTypes(string line)
        => SpyClrAssertion.Matches(line).Select(m => m.Groups["type"].Value)
            .Where(t => t != "Exception").ToList();

    private static Dictionary<string, int> ScanStdlib()
    {
        var root = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Stdlib");
        var excluded = new[] { "bin", "obj", "spy", "modules" }
            .Select(d => SysPath.Combine(root, d) + SysPath.DirectorySeparatorChar)
            .ToArray();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !excluded.Any(x => f.StartsWith(x, StringComparison.Ordinal)))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = SysPath.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/');
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseOptions);
            foreach (var key in ScanTree(tree, rel))
                counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        Assert.True(counts.Count > 0 || Directory.Exists(root), "the Stdlib source root was not found");
        return counts;
    }

    private static System.Collections.Generic.List<string> ScanTree(SyntaxTree tree, string rel)
    {
        var keys = new System.Collections.Generic.List<string>();
        foreach (var creation in tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Parent is not (ThrowStatementSyntax or ThrowExpressionSyntax))
                continue;

            var typeName = creation.Type switch
            {
                QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                SimpleNameSyntax s => s.Identifier.ValueText,
                _ => creation.Type.ToString(),
            };
            if (!ClrTypes.Contains(typeName))
                continue;

            keys.Add($"{rel}:{Anchor(creation)}:{typeName}");
        }

        return keys;
    }

    private static string Anchor(SyntaxNode node)
    {
        string? member = null;
        for (var n = node.Parent; n != null; n = n.Parent)
        {
            switch (n)
            {
                case BaseTypeDeclarationSyntax type:
                    return type.Identifier.ValueText + "." + (member ?? "<type>");
                case MethodDeclarationSyntax m when member == null:
                    member = m.Identifier.ValueText;
                    break;
                case ConstructorDeclarationSyntax c when member == null:
                    member = c.Identifier.ValueText;
                    break;
                case PropertyDeclarationSyntax p when member == null:
                    member = p.Identifier.ValueText;
                    break;
                case IndexerDeclarationSyntax when member == null:
                    member = "this[]";
                    break;
                case OperatorDeclarationSyntax o when member == null:
                    member = "operator" + o.OperatorToken.ValueText;
                    break;
                case LocalFunctionStatementSyntax l when member == null:
                    member = l.Identifier.ValueText;
                    break;
                case FieldDeclarationSyntax f when member == null:
                    member = f.Declaration.Variables.First().Identifier.ValueText;
                    break;
            }
        }

        return "<global>." + (member ?? "<none>");
    }

    private static Dictionary<string, int> ReadAllowlist()
    {
        var path = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Stdlib.Tests", "Conformance",
            "stdlib-clr-throw-allowlist.txt");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "Conformance/stdlib-clr-throw-allowlist.txt is missing; its presence arms the #2461 ratchet.", path);

        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var m = AllowlistRow.Match(line);
            Assert.True(m.Success, $"malformed allowlist row (want `file:Type.Member:Exception xN # #2461 reason`): {line}");
            Assert.False(rows.ContainsKey(m.Groups["key"].Value), $"duplicate allowlist row: {line}");
            rows[m.Groups["key"].Value] = int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        return rows;
    }
}

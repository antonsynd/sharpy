using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The member key of a type-body property is <c>(Name, ExplicitInterface)</c> at every consumer (P21h D5,
/// R-JH, #2390): a bare <c>area</c> and an explicit <c>property IShape.area</c> are two properties, as in C#,
/// and the explicit one is not a member of the type's own namespace. Before, eighteen by-name lookups
/// (<c>Properties.FirstOrDefault(p =&gt; p.Name == …)</c>) and two AST-side groupings keyed on the name alone,
/// so <c>q.area()</c> beside an explicit <c>IShape.area</c> was "int32 is not callable" and a bare getter
/// beside the explicit member answered the interface read (<c>6|6</c>, a silent wrong). Every by-name
/// property lookup now goes through <c>TypeSymbol.FindOwnProperty</c> (the own namespace) or
/// <c>TypeSymbol.FindProperty(name, explicitInterface)</c> (a <c>PropertyDef</c> in hand), and every grouping
/// of a body's <c>PropertyDef</c>s through <c>PropertyDef.MemberKey()</c>.
///
/// <para>This census pins the other spellings to zero in <c>src/Sharpy.Compiler/</c> and
/// <c>src/Sharpy.Lsp/</c> (comment-stripped, matched across lines): the lambda form (variable-agnostic —
/// <c>s =&gt; s.Name</c> was one of the eighteen — any method, and the name test anywhere in the body:
/// a later conjunct, reversed, negated, <c>string.Equals</c> / <c>.Name.Equals</c>), the name-KEYED form
/// (<c>Properties.ToDictionary(p =&gt; p.Name)</c> and its <c>GroupBy</c>/<c>ToLookup</c> twins), the raw
/// selector form (<c>FindMember(…, t =&gt; t.Properties, …)</c> — FindMember matches by name), the loop form (<c>foreach (… in x.Properties)</c> with a <c>.Name ==</c>/<c>!=</c> test within
/// three lines), and a <c>PropertyDef</c> family keyed by name (a <c>Dictionary&lt;string,
/// List&lt;PropertyDef&gt;&gt;</c>, an <c>OfType&lt;PropertyDef&gt;()</c> projection on <c>.Name</c>).
/// Exemptions are <c>file:line</c>, never directories, and each must still match (a stale exemption
/// fails), so the two helper definitions are the census's positive control.</para>
/// </summary>
public class ExplicitInterfaceKeyCensusTests
{
    /// <summary>A by-name property lookup through a lambda: any method on <c>Properties</c> whose lambda
    /// tests ITS parameter's <c>.Name</c> anywhere in the body before the statement ends — the name test as
    /// a later conjunct (<c>p =&gt; !p.IsStatic &amp;&amp; p.Name == n</c>), reversed (<c>n == p.Name</c>),
    /// negated (<c>!=</c>), or through <c>string.Equals</c> / <c>.Name.Equals(</c> (P21h verify, M06b: the
    /// first-conjunct-only pattern was green on those three spellings).</summary>
    private static readonly Regex LambdaLookup = new(
        @"\b[Pp]roperties\s*\.\s*\w+\(\s*(?<v>\w+)\s*=>[^;{}]*?(\k<v>\.Name\s*[!=]=|[!=]=\s*\k<v>\.Name\b|\k<v>\.Name\s*\.\s*Equals\(|string\.Equals\([^;{}]*?\b\k<v>\.Name\b)",
        RegexOptions.Compiled);

    /// <summary>A type's properties KEYED by the name alone (<c>Properties.ToDictionary(p =&gt; p.Name)</c>,
    /// <c>GroupBy</c>, <c>ToLookup</c>, <c>DistinctBy</c>, <c>ToHashSet</c>) — a bare and an explicit member
    /// collide on one key.</summary>
    private static readonly Regex NameKeyedProperties = new(
        @"\b[Pp]roperties\s*\.\s*(ToDictionary|ToLookup|GroupBy|DistinctBy|ToHashSet)\(\s*(?<v>\w+)\s*=>\s*\k<v>\.Name\s*[,)]",
        RegexOptions.Compiled);

    /// <summary>A by-name hierarchy search handed the raw property list as its selector
    /// (<c>TypeHierarchyService.FindMember(type, name, t =&gt; t.Properties, …)</c>): FindMember matches by
    /// name, so the raw list answers with an explicit-interface member. <c>FindProperty</c> passes a
    /// key-aware selector and is not a hit.</summary>
    private static readonly Regex RawPropertySelector = new(
        @"\bFindMember(<\w+>)?\([^;]*?(?<v>\w+)\s*=>\s*\k<v>\.Properties\s*[,)]",
        RegexOptions.Compiled);

    /// <summary>A loop over a type's properties; a hit is one with a name test within three lines.</summary>
    private static readonly Regex LoopHeader = new(@"\bin\s+[\w.?]+\.Properties\)", RegexOptions.Compiled);

    private static readonly Regex NameTest = new(
        @"\.Name\s*[!=]=|[!=]=\s*[\w.?]*\.Name\b|\.Name\s*\.\s*Equals\(|string\.Equals\([^;]*?\.Name\b",
        RegexOptions.Compiled);

    /// <summary>A family of <c>PropertyDef</c>s keyed by the name alone.</summary>
    private static readonly Regex NameKeyedFamily = new(
        @"Dictionary<string,\s*List<PropertyDef>>|OfType<PropertyDef>\(\)\s*\.\s*\w+\(\s*\w+\s*=>\s*\w+\.Name\b",
        RegexOptions.Compiled);

    /// <summary>
    /// <c>file:line</c> exemptions, each with its reason. Every entry must still match its pattern.
    /// </summary>
    private static readonly (string Pattern, string Site, string Reason)[] Exemptions =
    {
        ("lambda", "Sharpy.Compiler/Semantic/Symbol.cs:502", "TypeSymbol.FindOwnProperty — the own-namespace helper"),
        ("lambda", "Sharpy.Compiler/Semantic/Symbol.cs:510", "TypeSymbol.FindProperty — the full-key helper"),
        ("family", "Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs:131", "module properties: a module body has no explicit-interface member"),
        ("lambda", "Sharpy.Compiler/Semantic/TypeHierarchyService.cs:190", "key-aware: collects the EXPLICIT implementations of a name (ExplicitInterface != null), which FindProperty skips"),
        ("lambda", "Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.Calls.cs:2904", "key-aware (ExplicitInterface == null) and case-insensitive — a CLR member-name probe FindOwnProperty's ordinal match cannot answer"),
        ("selector", "Sharpy.Compiler/Semantic/Validation/InterfaceImplementationValidator.cs:125", "by design explicit members included: an explicit 'property IFoo.x' implements the interface property as a bare 'x' does, and the mixed-escape check judges either"),
    };

    [Fact]
    public void ByNamePropertyLookups_GoThroughTheMemberKeyHelpers()
    {
        var root = FindSourceRoot();
        var hits = Scan(root).ToList();
        var exempt = Exemptions.Select(e => (e.Pattern, e.Site)).ToHashSet();

        var violations = hits.Where(h => !exempt.Contains((h.Pattern, h.Site)))
            .Select(h => $"{h.Site} [{h.Pattern}]: {h.Text}").ToList();
        violations.Should().BeEmpty(
            "a property's member key is (Name, ExplicitInterface) (R-JH, #2390): look a property up with "
            + "TypeSymbol.FindOwnProperty(name) (the type's own namespace — an explicit-interface member is not in "
            + "it) or TypeSymbol.FindProperty(def.Name, def.ExplicitInterface) (a PropertyDef in hand), and group "
            + "a body's PropertyDefs by PropertyDef.MemberKey(). Violations:\n" + string.Join("\n", violations));

        var stale = Exemptions.Where(e => !hits.Any(h => h.Pattern == e.Pattern && h.Site == e.Site))
            .Select(e => $"{e.Site} [{e.Pattern}] ({e.Reason})").ToList();
        stale.Should().BeEmpty(
            "every exemption must still match its site — the helper definitions are the census's positive "
            + "control; move a drifted line number, delete a drained entry. Stale:\n" + string.Join("\n", stale));
    }

    /// <summary>The patterns themselves, on the spellings the census was built from (positive controls that do
    /// not depend on the tree's contents) and on the key-aware spellings that replace them (negative ones).</summary>
    [Theory]
    [InlineData("var p = t.Properties.FirstOrDefault(p => p.Name == member);", "lambda")]
    [InlineData("x?.Properties.FirstOrDefault(s => s.Name == p.Name) is { IsStatic: false }", "lambda")]
    [InlineData("var p = t.Properties\n    .FirstOrDefault(p => p.Name == member);", "lambda")]
    [InlineData("var i = t.Properties.FindIndex(p => p.Name == d.Name && !p.IsStatic);", "lambda")]
    [InlineData("if (t.Properties.Any(p =>\n    string.Equals(p.Name, m, StringComparison.OrdinalIgnoreCase)))", "lambda")]
    [InlineData("var e = properties.FirstOrDefault(p => p.Name == propDef.Name);", "lambda")]
    [InlineData("var p = t.Properties.FirstOrDefault(p => !p.IsStatic && p.Name == n);", "lambda")]
    [InlineData("var p = t.Properties.FirstOrDefault(p => n == p.Name);", "lambda")]
    [InlineData("var p = t.Properties.FirstOrDefault(q =>\n    q.IsStatic\n    && name == q.Name);", "lambda")]
    [InlineData("if (t.Properties.All(p => p.Name != n))", "lambda")]
    [InlineData("var p = t.Properties.Find(p => p.Name.Equals(n));", "lambda")]
    [InlineData("var byName = t.Properties.ToDictionary(p => p.Name);", "keyed")]
    [InlineData("var byName = t.Properties.GroupBy(p => p.Name, p => p.Type);", "keyed")]
    [InlineData("var (p, _) = TypeHierarchyService.FindMember(type, name, t => t.Properties, searchInterfaces: true);", "selector")]
    [InlineData("var (p, _) = FindMember<PropertySymbol>(\n    type, name, t => t.Properties);", "selector")]
    [InlineData("foreach (var property in type.Properties)\n{\n    if (name == property.Name)\n        return property;", "loop")]
    [InlineData("foreach (var property in type.Properties)\n{\n    if (property.Name != name)\n        continue;", "loop")]
    [InlineData("var g = new Dictionary<string, List<PropertyDef>>();", "family")]
    [InlineData("body.OfType<PropertyDef>().GroupBy(p => p.Name);", "family")]
    [InlineData("body\n    .OfType<PropertyDef>()\n    .Where(p => p.Name == s.Name)", "family")]
    [InlineData("var p = t.FindOwnProperty(member);", null)]
    [InlineData("var p = t.FindProperty(def.Name, def.ExplicitInterface);", null)]
    [InlineData("foreach (var property in type.Properties.Where(p => p.ExplicitInterface == null))\n    yield return property.Name;", null)]
    [InlineData("body.OfType<PropertyDef>().GroupBy(p => p.MemberKey());", null)]
    [InlineData("var names = t.Properties.Select(p => p.Name).ToList();", null)]
    [InlineData("var p = t.Properties.FirstOrDefault(p => p.IsStatic) ?? other.FirstOrDefault(q => q.Name == n);", null)]
    [InlineData("var (f, _) = TypeHierarchyService.FindMember<VariableSymbol>(type, name, t => t.Fields);", null)]
    [InlineData("var (p, _) = FindMember<PropertySymbol>(type, name, t => t.Properties.Where(p => p.ExplicitInterface == null));", null)]
    public void Patterns_MatchTheByNameSpellings(string code, string? expectedPattern)
    {
        var patterns = Match(code).Select(h => h.Pattern).Distinct().ToList();
        if (expectedPattern == null)
            patterns.Should().BeEmpty();
        else
            patterns.Should().Equal(expectedPattern);
    }

    private static IEnumerable<(string Pattern, string Site, string Text)> Scan(string root)
    {
        foreach (var project in new[] { "Sharpy.Compiler", "Sharpy.Lsp" })
        {
            var dir = Path.Combine(root, project);
            Directory.Exists(dir).Should().BeTrue($"the census scans {dir}");
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
                    continue;
                foreach (var (pattern, line, text) in Match(File.ReadAllText(file)))
                    yield return (pattern, $"{relative}:{line}", text);
            }
        }
    }

    private static IEnumerable<(string Pattern, int Line, string Text)> Match(string source)
    {
        var lines = source.Split('\n').Select(StripLineComment).ToArray();
        var code = string.Join("\n", lines);
        int LineOf(int index) => code.AsSpan(0, index).Count('\n') + 1;

        foreach (Match m in LambdaLookup.Matches(code))
            yield return ("lambda", LineOf(m.Index), lines[LineOf(m.Index) - 1].Trim());
        foreach (Match m in NameKeyedFamily.Matches(code))
            yield return ("family", LineOf(m.Index), lines[LineOf(m.Index) - 1].Trim());
        foreach (Match m in NameKeyedProperties.Matches(code))
            yield return ("keyed", LineOf(m.Index), lines[LineOf(m.Index) - 1].Trim());
        foreach (Match m in RawPropertySelector.Matches(code))
            yield return ("selector", LineOf(m.Index), lines[LineOf(m.Index) - 1].Trim());
        for (int i = 0; i < lines.Length; i++)
        {
            if (!LoopHeader.IsMatch(lines[i]))
                continue;
            for (int j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
            {
                if (NameTest.IsMatch(lines[j]))
                {
                    yield return ("loop", i + 1, lines[i].Trim());
                    break;
                }
            }
        }
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    private static string FindSourceRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "src", "Sharpy.Compiler", "Semantic")))
                return Path.Combine(current, "src");
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("repository root not found from " + AppContext.BaseDirectory);
    }
}

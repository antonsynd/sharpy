using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guards the scope-kind roster behind the one scope walk (#1786, R-Y).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Scope.ResolveName"/> decides Python's class-scope rule from the scope CHAIN, so a
/// scope name it does not recognise silently behaves like neither a function body nor a type body:
/// a new accessor family would stop refusing bare class-member uses, and a new type-body family
/// would start binding them. That is exactly how the predecessor mode flag lost the
/// property-accessor host. This scan reads every <c>EnterScope(</c> literal out of the semantic
/// pipeline's sources and asserts <see cref="SymbolTable.ClassifyScope"/> answers for each, with no
/// default arm to absorb a new one.
/// </para>
/// <para>
/// Anchored to measured literals rather than bounds: the census was counted at fb728b9be by this
/// very scan. A change to either number is a change to the pipeline's scope inventory — re-measure,
/// classify the new family, and update the literal in the same commit.
/// </para>
/// </remarks>
public class ScopeKindRosterTests
{
    /// <summary>Distinct scope-name families reachable from <c>EnterScope(</c>, measured @ fb728b9be.</summary>
    private const int ScopeNameFamilyCensus = 35;

    /// <summary><c>EnterScope(</c> call sites in the semantic pipeline, measured @ fb728b9be.</summary>
    private const int EnterScopeCallSiteCensus = 41;

    /// <summary>
    /// Families the pipeline never reaches through an <c>EnterScope(</c> literal because they are
    /// created by a different entry point, and which the walk must still classify: the global scope
    /// is built by the <c>SymbolTable</c> constructor and module scopes by <c>EnterModuleScope</c>.
    /// </summary>
    private static readonly string[] NonEnterScopeFamilies = { "global", "module:" };

    [Fact]
    public void EveryEnterScopeLiteral_IsClassified()
    {
        var unclassified = new List<string>();

        foreach (var (family, site) in ScopeNameFamilies())
        {
            if (SymbolTable.ClassifyScope(SpecimenFor(family)) == null)
                unclassified.Add($"{family}  [{site}]");
        }

        unclassified.Should().BeEmpty(
            "SymbolTable.ClassifyScope has no default arm, so an unclassified scope name is "
            + "function-like to nothing and a type body to nothing: names inside it would stop "
            + "obeying the class-scope rule (#1786, R-Y) without any test noticing. Classify each "
            + "family below as function-like, type-body, block, module or transparent.\n"
            + string.Join("\n", unclassified));
    }

    [Fact]
    public void ScopeNameFamilies_MatchTheMeasuredCensus()
    {
        var families = ScopeNameFamilies().Select(f => f.Family).Distinct(StringComparer.Ordinal).ToList();

        families.Should().HaveCount(ScopeNameFamilyCensus,
            "the roster is total over the pipeline's scope-name families and there are exactly "
            + $"{ScopeNameFamilyCensus} of them. A new family must be classified in "
            + "SymbolTable.ClassifyScope and counted here in the same commit.\nFamilies found:\n"
            + string.Join("\n", families.OrderBy(f => f, StringComparer.Ordinal)));
    }

    [Fact]
    public void EnterScopeCallSites_MatchTheMeasuredCensus()
    {
        var sites = ScopeNameFamilies().Select(f => f.Site).ToList();

        sites.Should().HaveCount(EnterScopeCallSiteCensus,
            $"there are exactly {EnterScopeCallSiteCensus} EnterScope call sites in the semantic "
            + "pipeline. Fewer means a scope stopped being pushed; more means a new one arrived and "
            + "its classification is unmeasured.\nSites found:\n"
            + string.Join("\n", sites));
    }

    [Fact]
    public void EveryClassifiedFamily_HasExactlyOneKind()
    {
        // A family that matched two arms would take whichever the implementation tests first, and
        // the answer would depend on arm order rather than on the roster. Checked by asking the two
        // derived predicates: both true is a contradiction, and both false must mean a non-null
        // classification of another kind.
        var contradictions = new List<string>();

        foreach (var family in AllFamilies())
        {
            var specimen = SpecimenFor(family);
            var kind = SymbolTable.ClassifyScope(specimen);
            bool functionLike = SymbolTable.IsFunctionLikeScope(specimen);
            bool classLike = SymbolTable.IsClassLikeScope(specimen);

            if (functionLike && classLike)
                contradictions.Add($"{family}: function-like AND type-body");
            else if (functionLike && kind != SymbolTable.ScopeKind.FunctionLike)
                contradictions.Add($"{family}: IsFunctionLikeScope true but ClassifyScope said {kind}");
            else if (classLike && kind != SymbolTable.ScopeKind.TypeBody)
                contradictions.Add($"{family}: IsClassLikeScope true but ClassifyScope said {kind}");
        }

        contradictions.Should().BeEmpty(
            "IsFunctionLikeScope and IsClassLikeScope are both derived from ClassifyScope, so no "
            + "family may answer to two kinds.\n" + string.Join("\n", contradictions));
    }

    /// <summary>
    /// Positive control for the absence assertion above: a name that is in no family classifies as
    /// null, so <see cref="EveryEnterScopeLiteral_IsClassified"/> can fail.
    /// </summary>
    [Theory]
    [InlineData("not-a-scope-kind")]
    [InlineData("classy")]
    [InlineData("functional")]
    public void AnUnknownScopeName_IsUnclassified(string scopeName)
    {
        SymbolTable.ClassifyScope(scopeName).Should().BeNull();
        SymbolTable.IsFunctionLikeScope(scopeName).Should().BeFalse();
        SymbolTable.IsClassLikeScope(scopeName).Should().BeFalse();
    }

    [Theory]
    [InlineData("function:m", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("lambda", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("property:w:Get", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("event:e:Add", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("observer:p:BeforeSet", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("pre-pass:m", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("interface-method:I.m", SymbolTable.ScopeKind.FunctionLike)]
    [InlineData("class:C", SymbolTable.ScopeKind.TypeBody)]
    [InlineData("struct:S", SymbolTable.ScopeKind.TypeBody)]
    [InlineData("union:U", SymbolTable.ScopeKind.TypeBody)]
    [InlineData("interface:I", SymbolTable.ScopeKind.TypeBody)]
    [InlineData("delegate:D", SymbolTable.ScopeKind.TypeBody)]
    [InlineData("if-then", SymbolTable.ScopeKind.Block)]
    [InlineData("for-body", SymbolTable.ScopeKind.Block)]
    [InlineData("match-case", SymbolTable.ScopeKind.Block)]
    [InlineData("list-comprehension", SymbolTable.ScopeKind.Block)]
    [InlineData("global", SymbolTable.ScopeKind.Module)]
    [InlineData("module:lib", SymbolTable.ScopeKind.Module)]
    [InlineData("type-parameter-scope", SymbolTable.ScopeKind.Transparent)]
    [InlineData("constraint-resolution", SymbolTable.ScopeKind.Transparent)]
    internal void NamedFamily_ClassifiesAsRecorded(string scopeName, SymbolTable.ScopeKind expected)
    {
        SymbolTable.ClassifyScope(scopeName).Should().Be(expected);
    }

    /// <summary>
    /// Builds a concrete scope name from a family. Prefixed families (<c>class:</c>) are pushed with
    /// an interpolated suffix at runtime, so the specimen appends one.
    /// </summary>
    private static string SpecimenFor(string family)
        => family.EndsWith(':') ? family + "X" : family;

    private static IEnumerable<string> AllFamilies()
        => ScopeNameFamilies().Select(f => f.Family)
            .Concat(NonEnterScopeFamilies)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Every <c>EnterScope(</c> argument in the semantic pipeline, reduced to its family: a plain
    /// string literal is its own family (<c>"if-then"</c>), and an interpolated string contributes
    /// the literal prefix before its first hole (<c>$"class:{name}"</c> → <c>class:</c>).
    /// </summary>
    private static List<(string Family, string Site)> ScopeNameFamilies()
    {
        var found = new List<(string, string)>();

        foreach (var (fileName, root) in SemanticSyntaxTrees())
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "EnterScope"
                    } member)
                {
                    continue;
                }

                // _narrowingContext.EnterScope() takes no argument and is a different mechanism.
                if (invocation.ArgumentList.Arguments.Count != 1)
                    continue;
                if (member.Expression.ToString() is not ("_symbolTable" or "symbolTable" or "SymbolTable"))
                    continue;

                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var site = $"{fileName}:{line}";
                var argument = invocation.ArgumentList.Arguments[0].Expression;

                switch (argument)
                {
                    case LiteralExpressionSyntax literal
                        when literal.Kind() == SyntaxKind.StringLiteralExpression:
                        found.Add((literal.Token.ValueText, site));
                        break;

                    case InterpolatedStringExpressionSyntax interpolated:
                        var prefix = interpolated.Contents
                            .TakeWhile(c => c is InterpolatedStringTextSyntax)
                            .Cast<InterpolatedStringTextSyntax>()
                            .Select(t => t.TextToken.ValueText)
                            .FirstOrDefault();
                        prefix.Should().NotBeNull(
                            $"an EnterScope interpolation with no literal prefix has no family to "
                            + $"classify ({site})");
                        found.Add((prefix!, site));
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"EnterScope at {site} takes a non-literal argument "
                            + $"('{argument}'), so its scope-name family cannot be scanned. Give it "
                            + "a literal prefix or extend this scan.");
                }
            }
        }

        found.Should().NotBeEmpty("the scan is vacuous if it finds no EnterScope call sites");
        return found;
    }

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> SemanticSyntaxTrees()
    {
        var dir = Path.Combine(FindSourceDir("Sharpy.Compiler"), "Semantic");
        Directory.Exists(dir).Should().BeTrue($"the semantic source directory must be locatable (looked in {dir})");

        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            yield return (Path.GetFileName(file), (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot());
        }
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}

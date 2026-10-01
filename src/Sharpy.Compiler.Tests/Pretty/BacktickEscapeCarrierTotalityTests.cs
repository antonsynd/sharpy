using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// Backtick-escape carrier totality (#2157, P22b Phase 3): a user-written name keeps its escape
/// through parse → unparse → compare at every position. Three checks, each guarding one half of the
/// class the formatter sweep measured (12 positions dropped the escape at 4ef844961, six because the
/// record had no flag, six because the unparser or the comparer ignored one):
/// <list type="number">
/// <item><b>Carriers</b> — every public AST record property that holds a user-written name
/// (<c>string</c>, or an array of name parts) is in <see cref="Roster"/> with its parallel escape
/// flag, and the flag exists with the right shape. The roster is literal and asserted EQUAL to the
/// reflective discovery minus the asserted <see cref="NotNames"/> set, so a new name-carrying
/// property is red until it is given a flag (a decision, not a default).</item>
/// <item><b>One writer</b> — no <c>_w.Write(…)</c> in <c>Pretty/UnparseVisitor*.cs</c> writes a name
/// property bare; names go through <c>WriteName</c>/<c>WriteDottedName</c>. The scanner's positive
/// control is the pinned pre-fix source (<see cref="PreFixBareWrites"/>, 17 hits at 011fa4068).</item>
/// <item><b>Compared</b> — every escape-flag property in the AST assembly is named in
/// <c>StructuralEqualityComparer.cs</c> (at 4ef844961 <c>IsMemberBacktickEscaped</c> had zero
/// mentions; recorded as this check's mutation in the commit body). The behavioural twin of this
/// source check is <see cref="StructuralComparerFieldTotalityTests"/>.</item>
/// </list>
/// </summary>
public class BacktickEscapeCarrierTotalityTests
{
    private readonly ITestOutputHelper _output;

    public BacktickEscapeCarrierTotalityTests(ITestOutputHelper output) => _output = output;

    private const string AstNamespace = "Sharpy.Compiler.Parser.Ast";

    /// <summary>
    /// <c>Record.Property</c> → its escape flag. A string name pairs with a <c>bool</c>; a parts
    /// array pairs with a parallel <c>ImmutableArray&lt;bool&gt;</c>. A dotted name stored both joined
    /// and split (<c>ImportAlias.Name</c> + <c>NameParts</c>) carries its flags on the parts, and the
    /// joined spelling is in <see cref="JoinedSpellings"/>.
    /// </summary>
    private static readonly Dictionary<string, string> Roster = new(StringComparer.Ordinal)
    {
        // Declarations
        ["ClassDef.Name"] = Name,
        ["StructDef.Name"] = Name,
        ["InterfaceDef.Name"] = Name,
        ["EnumDef.Name"] = Name,
        ["EnumMember.Name"] = Name,
        ["UnionDef.Name"] = Name,
        ["UnionCaseDef.Name"] = Name,
        ["UnionCaseField.Name"] = Name,
        ["DelegateDef.Name"] = Name,
        ["EventDef.Name"] = Name,
        ["FunctionDef.Name"] = Name,
        ["PropertyDef.Name"] = Name,
        ["PropertyDef.ExplicitInterface"] = "IsExplicitInterfaceBacktickEscaped",
        ["PropertyObserver.ParamName"] = "IsParamNameBacktickEscaped",
        ["Parameter.Name"] = Name,
        ["TypeParameterDef.Name"] = Name,
        ["TypeAlias.Name"] = Name,
        ["VariableDeclaration.Name"] = Name,
        ["Decorator.QualifiedParts"] = Parts,
        // Expressions and patterns
        ["Identifier.Name"] = Name,
        ["MemberAccess.Member"] = "IsMemberBacktickEscaped",
        ["KeywordArgument.Name"] = Name,
        ["ModifiedArgument.InlineName"] = Name,
        ["WalrusExpression.Target"] = Name,
        ["TupleLiteral.ElementNames"] = "ElementNamesBacktickEscaped",
        ["ExceptHandler.Name"] = Name,
        ["PropertyPatternField.Name"] = Name,
        ["MemberAccessPattern.Parts"] = Parts,
        // Types
        ["TypeAnnotation.Name"] = Name,
        ["TypeAnnotation.NameParts"] = Parts,
        ["TypeAnnotation.TupleElementNames"] = "TupleElementNamesBacktickEscaped",
        // Imports
        ["ImportAlias.NameParts"] = Parts,
        ["ImportAlias.AsName"] = "IsAsNameBacktickEscaped",
        ["FromImportStatement.ModuleParts"] = Parts,
    };

    private const string Name = "IsNameBacktickEscaped";
    private const string Parts = "BacktickEscapedParts";

    /// <summary>Joined dotted spellings whose escapes ride the parallel parts array (<c>Record.Joined</c> → <c>Record.Parts</c>).</summary>
    private static readonly Dictionary<string, string> JoinedSpellings = new(StringComparer.Ordinal)
    {
        ["ImportAlias.Name"] = "ImportAlias.NameParts",
        ["FromImportStatement.Module"] = "FromImportStatement.ModuleParts",
    };

    /// <summary>String-typed AST properties that are not user-written names, each with why.</summary>
    private static readonly Dictionary<string, string> NotNames = new(StringComparer.Ordinal)
    {
        ["StringLiteral.Value"] = Literal,
        ["BytesLiteralExpression.Value"] = Literal,
        ["IntegerLiteral.Value"] = Literal,
        ["IntegerLiteral.Suffix"] = Literal,
        ["FloatLiteral.Value"] = Literal,
        ["FloatLiteral.Suffix"] = Literal,
        ["FStringPart.Text"] = "f-string literal text",
        ["FStringPart.SourceText"] = "a replacement field's verbatim source, written back byte-for-byte (#2024)",
        ["FStringPart.ExpressionText"] = "a replacement field's verbatim source, written back byte-for-byte (#2024)",
        ["FStringPart.RawText"] = "a replacement field's verbatim source, written back byte-for-byte (#2024)",
        ["Module.DocString"] = DocString,
        ["ClassDef.DocString"] = DocString,
        ["StructDef.DocString"] = DocString,
        ["InterfaceDef.DocString"] = DocString,
        ["EnumDef.DocString"] = DocString,
        ["UnionDef.DocString"] = DocString,
        ["DelegateDef.DocString"] = DocString,
        ["FunctionDef.DocString"] = DocString,
        ["FromImportStatement.ResolvedModulePath"] = "set by semantic analysis, not parsed (a Rule-3 smell, ledgered)",
        ["NestedDeclaration.Name"] = "a query projection built by StatementExtensions over a parsed declaration, never unparsed",
    };

    private const string Literal = "literal value, not a name";
    private const string DocString = "docstring text (a string literal)";

    // ================================================================
    // (1) Carriers
    // ================================================================

    [Fact]
    public void EveryNameCarryingProperty_IsRosteredWithAnEscapeFlag()
    {
        var discovered = NameShapedProperties().Select(p => $"{p.DeclaringType!.Name}.{p.Name}").ToHashSet(StringComparer.Ordinal);
        foreach (var key in discovered.OrderBy(k => k, StringComparer.Ordinal))
            _output.WriteLine($"CARRIER {key}");

        var accounted = Roster.Keys.Concat(JoinedSpellings.Keys).Concat(NotNames.Keys).ToList();
        accounted.Should().OnlyHaveUniqueItems("a property is a name carrier, a joined spelling, or not a name — never two");
        discovered.Should().BeEquivalentTo(accounted,
            "every string/string-parts property of an AST record is either rostered with its escape flag, a joined dotted "
            + "spelling whose parts carry the flags, or asserted not a name with a reason — a new name property must not skip its flag");

        var problems = new List<string>();
        foreach (var (carrier, flag) in Roster)
        {
            var (type, property) = Resolve(carrier);
            var flagProperty = type.GetProperty(flag, BindingFlags.Public | BindingFlags.Instance);
            var expected = IsStringArray(property.PropertyType) ? typeof(ImmutableArray<bool>) : typeof(bool);
            if (flagProperty == null)
                problems.Add($"{carrier}: escape flag {flag} does not exist");
            else if (flagProperty.PropertyType != expected)
                problems.Add($"{carrier}: escape flag {flag} is {flagProperty.PropertyType.Name}, expected {expected.Name}");
        }

        foreach (var (joined, parts) in JoinedSpellings)
        {
            if (!Roster.ContainsKey(parts))
                problems.Add($"{joined}: its parts property {parts} is not a rostered carrier");
        }

        problems.Should().BeEmpty();
    }

    /// <summary>Public settable <c>string</c>/<c>string?</c>/<c>ImmutableArray&lt;string(?)&gt;</c> properties declared on AST records.</summary>
    private static IEnumerable<PropertyInfo> NameShapedProperties()
        => typeof(Node).Assembly.GetTypes()
            .Where(t => t.Namespace == AstNamespace && t.IsClass)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod is { IsPublic: true } && (p.PropertyType == typeof(string) || IsStringArray(p.PropertyType)))
            .Where(p => p.DeclaringType!.GetMethod("<Clone>$") != null);

    private static bool IsStringArray(Type t)
        => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ImmutableArray<>) && t.GetGenericArguments()[0] == typeof(string);

    private static (Type Type, PropertyInfo Property) Resolve(string key)
    {
        var dot = key.IndexOf('.');
        var type = typeof(Node).Assembly.GetTypes().Single(t => t.Namespace == AstNamespace && t.Name == key[..dot]);
        return (type, type.GetProperty(key[(dot + 1)..], BindingFlags.Public | BindingFlags.Instance)!);
    }

    // ================================================================
    // (2) One writer
    // ================================================================

    private static readonly Regex BareNameWrite = new(
        @"_w\.Write\((?<arg>[^;]*?\.(?:Name|AsName|ParamName|InlineName|Target|Member|Module|ExplicitInterface|(?:Element|TupleElement)Names\[[a-z]\]|(?:Name|Module|Qualified)?Parts\[[a-z]\]))!?\)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The bare writes that remain on purpose — the joined-spelling fallback for an alias or module
    /// with NO parts array (a dots-only relative <c>from . import x</c>, or an AST not built by the
    /// parser), where there is nothing to escape. Exact source text; each must still be present.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedBareWrites = new(StringComparer.Ordinal)
    {
        ["_w.Write(alias.Name)"] = "ImportAlias with empty NameParts: no parts, no escapes to write",
        ["_w.Write(node.Module)"] = "FromImportStatement with empty ModuleParts: `from . import x` (dots only) or a hand-built node",
    };

    /// <summary>The bare name writes of the unparser at 011fa4068 (before the WriteName fix), verbatim.</summary>
    private const string PreFixBareWrites = """
            _w.Write(dec.Name);
            _w.Write(kwarg.Name);
        _w.Write(node.Member);
                _w.Write(node.ElementNames[i]!);
            _w.Write(node.Parts[i]);
        _w.Write(node.Name);
                    _w.Write(handler.Name);
        _w.Write(node.Name);
                    _w.Write(observer.ParamName);
            _w.Write(node.Names[i].Name);
                _w.Write(node.Names[i].AsName!);
        _w.Write(node.Module);
            _w.Write(node.Names[i].Name);
                    _w.Write(node.Names[i].AsName!);
                _w.Write(caseDef.Name);
                        _w.Write(caseDef.Fields[i].Name);
                    _w.Write(type.TupleElementNames[i]!);
        """;

    [Fact]
    public void Scanner_FindsEveryBareNameWriteOfThePreFixUnparser()
    {
        Scan(PreFixBareWrites).Should().HaveCount(17, "the scanner must see each of the 17 bare name writes the unparser had at 011fa4068");
    }

    [Fact]
    public void Unparser_WritesNoNameBare_ExceptTheEmptyPartsFallbacks()
    {
        var hits = UnparserSources()
            .SelectMany(file => Scan(File.ReadAllText(file)).Select(hit => (File: Path.GetFileName(file), Hit: hit)))
            .ToList();
        foreach (var (file, hit) in hits)
            _output.WriteLine($"BAREWRITE {file}: {hit}");

        hits.Select(h => h.Hit).Where(h => !AllowedBareWrites.ContainsKey(h)).Should().BeEmpty(
            "every user-written name goes through WriteName/WriteDottedName so its escape is written back (#2157)");
        hits.Select(h => h.Hit).Should().BeEquivalentTo(AllowedBareWrites.Keys,
            "each allowed fallback write is still present — delete its entry when it goes");
    }

    private static List<string> Scan(string source)
        => BareNameWrite.Matches(source).Select(m => m.Value).ToList();

    private static IEnumerable<string> UnparserSources()
    {
        var dir = Path.Combine(RepoRoot(), "src", "Sharpy.Compiler", "Pretty");
        var files = Directory.GetFiles(dir, "UnparseVisitor*.cs").OrderBy(f => f, StringComparer.Ordinal).ToList();
        files.Should().NotBeEmpty();
        return files;
    }

    // ================================================================
    // (3) Compared
    // ================================================================

    [Fact]
    public void EveryEscapeFlag_IsNamedInTheStructuralComparer()
    {
        var flags = typeof(Node).Assembly.GetTypes()
            .Where(t => t.Namespace == AstNamespace)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(p => p.Name)
            .Where(n => n.Contains("BacktickEscaped", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        flags.Should().Contain(Roster.Values.Distinct(), "the roster's flags are a subset of the AST's escape flags");

        var comparer = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Sharpy.Compiler", "Pretty", "StructuralEqualityComparer.cs"));
        flags.Where(f => !Regex.IsMatch(comparer, $@"\.{f}\b")).Should().BeEmpty(
            "a flag the comparer never reads is invisible to the formatter sweep's O3 and the SPY0912 net");
    }

    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null && !File.Exists(Path.Combine(current, "sharpy.sln")))
            current = Directory.GetParent(current)?.FullName;
        return current ?? throw new InvalidOperationException("sharpy.sln not found above the test assembly");
    }
}

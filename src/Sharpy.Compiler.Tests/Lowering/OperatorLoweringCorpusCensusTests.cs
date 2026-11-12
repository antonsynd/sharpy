using System.Collections;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Stage 0 of P82 tranche 3 (#2443, plan-92249e): the positive control for moving
/// <c>_operatorLowerings</c> off the emitter's <c>SemanticInfo</c> reads and onto the lowering IR.
/// Before any read moves, every fact about to move must be visible in a program whose output is
/// committed — otherwise an IR carrier that silently reads <c>Native</c> for that fact changes
/// nothing a test sees.
///
/// <para><b>The census.</b> Every non-skipped fixture with an <c>.expected</c> file is compiled
/// through the production pipeline, and the checker's recorded operator lowerings are read by
/// reflection from the <c>SemanticInfo</c> each emitter was given (no product change). Each entry
/// is counted by its <see cref="OperatorLoweringKind"/> and by the AST type of its key.</para>
///
/// <para><b>Two axes, both closed against the compiler.</b>
/// <list type="bullet">
/// <item><b>Kind</b> is <c>Enum.GetValues&lt;OperatorLoweringKind&gt;()</c>. Every value has at
/// least one entry in the corpus, or is in <see cref="ExcludedKinds"/> with its reason; an excluded
/// kind that does appear fails the test until its row is deleted.</item>
/// <item><b>Key kind</b> is the set of static types of the first argument at every
/// <c>SetOperatorLowering(</c> call in <c>Semantic/</c>, read from the checker's source by a
/// syntax census (<see cref="KeyKindRoster"/>). Every roster type has at least one entry, and no
/// entry's key has a type outside the roster. A new writer with a fifth key kind therefore fails
/// here until a fixture exercises it, instead of reading <c>Native</c> through an emitter helper
/// that finds no carrier.</item>
/// </list>
/// The record's second member, <c>NarrowTo</c>, is floored for each key kind that writes one.</para>
///
/// <para>Categorised <c>GapDiscovery</c>: a standing corpus sweep, not an edit-loop test.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class OperatorLoweringCorpusCensusTests
{
    private readonly ITestOutputHelper _output;

    public OperatorLoweringCorpusCensusTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Kinds with no entry to find, each with its reason. <c>Native</c> is the absent-entry
    /// convention: the checker guards its writes with <c>!= Native</c> and readers spell a missing
    /// entry as <c>Native</c>, so the one writer that can record it (an augmented assignment that
    /// narrows its result, which stores <c>(Native, NarrowTo)</c>) is not required to appear.
    /// </summary>
    private static readonly IReadOnlyDictionary<OperatorLoweringKind, string> ExcludedKinds =
        new Dictionary<OperatorLoweringKind, string>
        {
            [OperatorLoweringKind.Native] = "the absent-entry convention; not a lowering to transport",
        };

    private static readonly FieldInfo OperatorLoweringsField =
        typeof(SemanticInfo).GetField("_operatorLowerings", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "SemanticInfo._operatorLowerings is gone: if P82 moved it, this census moves with it (#2443)");

    [Fact]
    public void EveryOperatorLoweringKindAndKeyKind_HasAnEntryInAFixtureWithCommittedOutput()
    {
        var roster = KeyKindRoster();
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsSkipped && f.ExpectedFile != null)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();
        Assert.True(fixtures.Count > 0, "no fixture with an .expected file was discovered: the census is vacuous");

        var byKind = Enum.GetValues<OperatorLoweringKind>().ToDictionary(k => k, _ => 0);
        var byKeyKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var cells = new SortedDictionary<(string Kind, string Key), int>();
        var firstFixture = new Dictionary<OperatorLoweringKind, string>();
        var narrowToByKeyKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var internalErrors = new List<string>();
        int compiled = 0, entries = 0;

        foreach (var fixture in fixtures)
        {
            LoweredCompilationCapture.Result result;
            try
            {
                result = LoweredCompilationCapture.Compile(fixture);
            }
            catch (Exception ex)
            {
                internalErrors.Add($"{fixture.TestName}: compile threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (result.InternalCompilerErrors.Count > 0)
            {
                internalErrors.Add($"{fixture.TestName}: {string.Join(" | ", result.InternalCompilerErrors)}");
                continue;
            }

            if (!result.FrontEndClean)
                continue;

            compiled++;

            // A project's units share one SemanticInfo; read each instance once.
            var seen = new HashSet<SemanticInfo>(ReferenceEqualityComparer.Instance);
            foreach (var (context, _) in result.Units)
            {
                if (context.SemanticInfo is not { } info || !seen.Add(info))
                    continue;

                foreach (DictionaryEntry entry in (IDictionary)OperatorLoweringsField.GetValue(info)!)
                {
                    var lowering = (OperatorLowering)entry.Value!;
                    var keyKind = entry.Key.GetType().Name;
                    entries++;
                    byKind[lowering.Kind]++;
                    byKeyKind[keyKind] = byKeyKind.GetValueOrDefault(keyKind) + 1;
                    cells[(lowering.Kind.ToString(), keyKind)] =
                        cells.GetValueOrDefault((lowering.Kind.ToString(), keyKind)) + 1;
                    firstFixture.TryAdd(lowering.Kind, fixture.TestName);
                    if (lowering.NarrowTo != null)
                        narrowToByKeyKind[keyKind] = narrowToByKeyKind.GetValueOrDefault(keyKind) + 1;
                }
            }
        }

        _output.WriteLine(
            $"fixtures with .expected: {fixtures.Count}, compiled clean: {compiled}, entries: {entries}, "
            + "with NarrowTo: " + (narrowToByKeyKind.Count == 0 ? "0"
                : string.Join(", ", narrowToByKeyKind.Select(p => $"{p.Key}={p.Value}"))));
        _output.WriteLine("key-kind roster (SetOperatorLowering call sites): " + string.Join(", ", roster));
        _output.WriteLine("per key kind: " + string.Join(", ", byKeyKind.Select(p => $"{p.Key}={p.Value}")));
        foreach (var kind in Enum.GetValues<OperatorLoweringKind>())
        {
            var keys = string.Join(", ", cells.Where(c => c.Key.Kind == kind.ToString())
                .Select(c => $"{c.Key.Key}={c.Value}"));
            _output.WriteLine(
                $"  {kind,-32} {byKind[kind],6}  [{keys}]  first: {firstFixture.GetValueOrDefault(kind, "-")}");
        }

        Assert.True(internalErrors.Count == 0,
            $"{internalErrors.Count} fixture(s) hit an internal compiler error; the census does not skip them:\n"
            + string.Join("\n", internalErrors.Take(25)));

        Assert.True(compiled > 0, "no fixture compiled clean: the census read nothing");

        var missingKinds = byKind.Where(p => p.Value == 0 && !ExcludedKinds.ContainsKey(p.Key))
            .Select(p => p.Key.ToString()).ToList();
        Assert.True(missingKinds.Count == 0,
            "OperatorLoweringKind value(s) with no entry in any fixture that has an .expected file. Add a "
            + "fixture whose printed output differs when the kind reads Native, or an ExcludedKinds row "
            + "with the reason it cannot be recorded (#2443):\n  " + string.Join("\n  ", missingKinds));

        var staleExclusions = ExcludedKinds.Keys
            .Where(k => k != OperatorLoweringKind.Native && byKind[k] > 0).Select(k => k.ToString()).ToList();
        Assert.True(staleExclusions.Count == 0,
            "ExcludedKinds row(s) whose kind now has an entry; delete the row: " + string.Join(", ", staleExclusions));

        var missingKeyKinds = roster.Where(k => !byKeyKind.ContainsKey(k)).ToList();
        Assert.True(missingKeyKinds.Count == 0,
            "key kind(s) a SetOperatorLowering call site writes that no fixture with an .expected file "
            + "exercises (#2443): " + string.Join(", ", missingKeyKinds));

        // NarrowTo is the record's second member and moves with the kind. Its two writers are the
        // augmented assignment that narrows its result and the super-operator call (the base type).
        foreach (var keyKind in new[] { nameof(Assignment), nameof(FunctionCall) })
        {
            Assert.True(narrowToByKeyKind.GetValueOrDefault(keyKind) > 0,
                $"no {keyKind} entry with a NarrowTo in any fixture that has an .expected file: the "
                + "member would move onto the IR with nothing observing it (#2443)");
        }

        var unrostered = byKeyKind.Keys.Where(k => !roster.Contains(k)).ToList();
        Assert.True(unrostered.Count == 0,
            "entries keyed by an AST type no SetOperatorLowering call site names; the call-site census "
            + "missed a writer: " + string.Join(", ", unrostered));
    }

    /// <summary>
    /// The call-site census resolves every writer: a call whose first argument it cannot type fails
    /// here, so the roster cannot shrink by a writer being silently skipped.
    /// </summary>
    [Fact]
    public void KeyKindRoster_ResolvesEverySetOperatorLoweringCallSite()
    {
        var (roster, sites, unresolved) = ScanCallSites();

        _output.WriteLine($"{sites} SetOperatorLowering call site(s); roster: {string.Join(", ", roster)}");
        Assert.True(sites > 0, "no SetOperatorLowering call site found under Semantic/: the scan reads nothing");
        Assert.True(unresolved.Count == 0,
            "SetOperatorLowering call site(s) whose first argument is not a parameter, local or pattern "
            + "variable of the enclosing member; extend ScanCallSites:\n  " + string.Join("\n  ", unresolved));
        Assert.All(roster, name => Assert.True(
            typeof(Node).Assembly.GetTypes().Any(t => t.Name == name && typeof(Node).IsAssignableFrom(t)),
            $"'{name}' is not an AST node type"));
    }

    private static SortedSet<string> KeyKindRoster() => ScanCallSites().Roster;

    private static (SortedSet<string> Roster, int Sites, List<string> Unresolved) ScanCallSites()
    {
        var semanticDir = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler", "Semantic");
        var roster = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        var sites = 0;

        foreach (var file in Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "SetOperatorLowering" }
                    || call.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                sites++;
                var where = $"{Path.GetFileName(file)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (call.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax { Identifier.ValueText: var name })
                {
                    unresolved.Add($"{where}: first argument is not an identifier");
                    continue;
                }

                if (DeclaredTypeOf(call, name) is { } type)
                    roster.Add(type);
                else
                    unresolved.Add($"{where}: no declaration of '{name}' found in the enclosing member");
            }
        }

        return (roster, sites, unresolved);
    }

    /// <summary>
    /// The declared type of <paramref name="name"/> as seen from <paramref name="use"/>: a parameter,
    /// a typed local, or a declaration pattern (<c>is BinaryOp b</c>, <c>case UnaryOp u</c>) of the
    /// enclosing member, innermost enclosing function first.
    /// </summary>
    private static string? DeclaredTypeOf(Microsoft.CodeAnalysis.SyntaxNode use, string name)
    {
        for (var scope = use.Parent; scope != null; scope = scope.Parent)
        {
            if (scope is not (BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax))
            {
                continue;
            }

            foreach (var node in scope.DescendantNodes())
            {
                switch (node)
                {
                    case ParameterSyntax { Type: { } parameterType } parameter
                        when parameter.Identifier.ValueText == name:
                        return SimpleName(parameterType);
                    case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax designation } pattern
                        when designation.Identifier.ValueText == name:
                        return SimpleName(pattern.Type);
                    case RecursivePatternSyntax { Type: { } recursiveType, Designation: SingleVariableDesignationSyntax designation }
                        when designation.Identifier.ValueText == name:
                        return SimpleName(recursiveType);
                    case VariableDeclarationSyntax { Type: var localType } declaration
                        when !localType.IsVar && declaration.Variables.Any(v => v.Identifier.ValueText == name):
                        return SimpleName(localType);
                }
            }

            if (scope is BaseMethodDeclarationSyntax)
                return null;
        }

        return null;
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
        _ => type.ToString(),
    };
}

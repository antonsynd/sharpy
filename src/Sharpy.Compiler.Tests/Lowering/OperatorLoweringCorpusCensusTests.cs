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
/// The record's second member, <c>NarrowTo</c>, is floored for each key kind a call site writes one
/// for (read from the same syntax census), and for the <c>(Native, NarrowTo)</c> entry.</para>
///
/// <para><b>The binary / augmented pair.</b> A kind is not one fact per key kind: the checker
/// classifies <c>a OP b</c> and <c>a OP= b</c> with the same classifiers, and the two are carried by
/// different IR nodes. So every kind seen on a <c>BinaryOp</c> whose operator has an augmented twin
/// (<see cref="AugmentedTwin"/>, total over <see cref="BinaryOperator"/>) must also be seen on an
/// <c>Assignment</c> with that twin, or be in <see cref="NotOnAugmented"/> with the reason the
/// checker cannot record it there; a row whose pair does appear fails until it is deleted.</para>
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

    /// <summary>
    /// Kinds a <c>BinaryOp</c> records that its augmented twin cannot, each with the reason (probed
    /// with the built compiler at eabb480c6).
    /// </summary>
    private static readonly IReadOnlyDictionary<OperatorLoweringKind, string> NotOnAugmented =
        new Dictionary<OperatorLoweringKind, string>
        {
            [OperatorLoweringKind.TrueDivisionCastLeft] =
                "an integer target: `i /= 2` is refused, the float64 result is not assignable (SPY0220)",
            [OperatorLoweringKind.GuardedTrueDivisionCastLeft] =
                "an integer target: `i /= j` is refused, the float64 result is not assignable (SPY0220)",
            [OperatorLoweringKind.StringRepeatStrRight] =
                "an integer target: `n *= \"ab\"` is refused, the str result is not assignable (SPY0220)",
            [OperatorLoweringKind.DecimalPow] =
                "`d **= 2` is refused: decimal ** int is float64, not assignable to decimal (SPY0220)",
            [OperatorLoweringKind.OptionalUnwrapOr] =
                "`??=` records only OptionalCoalesceBothOptional; a payload right side is a store wrap",
        };

    /// <summary>
    /// The augmented operator of a binary operator, or <c>null</c> when it has none. No default arm:
    /// a new <see cref="BinaryOperator"/> value throws here until it is classified.
    /// </summary>
    private static AssignmentOperator? AugmentedTwin(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => AssignmentOperator.PlusAssign,
        BinaryOperator.Subtract => AssignmentOperator.MinusAssign,
        BinaryOperator.Multiply => AssignmentOperator.StarAssign,
        BinaryOperator.Divide => AssignmentOperator.SlashAssign,
        BinaryOperator.FloorDivide => AssignmentOperator.DoubleSlashAssign,
        BinaryOperator.Modulo => AssignmentOperator.PercentAssign,
        BinaryOperator.Power => AssignmentOperator.PowerAssign,
        BinaryOperator.MatMul => AssignmentOperator.MatMulAssign,
        BinaryOperator.BitwiseAnd => AssignmentOperator.AndAssign,
        BinaryOperator.BitwiseOr => AssignmentOperator.OrAssign,
        BinaryOperator.BitwiseXor => AssignmentOperator.XorAssign,
        BinaryOperator.LeftShift => AssignmentOperator.LeftShiftAssign,
        BinaryOperator.RightShift => AssignmentOperator.RightShiftAssign,
        BinaryOperator.NullCoalesce => AssignmentOperator.NullCoalesceAssign,
        BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.LessThan
            or BinaryOperator.LessThanOrEqual or BinaryOperator.GreaterThan
            or BinaryOperator.GreaterThanOrEqual or BinaryOperator.And or BinaryOperator.Or
            or BinaryOperator.In or BinaryOperator.NotIn or BinaryOperator.Is or BinaryOperator.IsNot => null,
        _ => throw new InvalidOperationException(
            $"BinaryOperator.{op} has no AugmentedTwin arm: add its augmented operator, or null (#2443)"),
    };

    private static readonly FieldInfo OperatorLoweringsField =
        typeof(SemanticInfo).GetField("_operatorLowerings", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "SemanticInfo._operatorLowerings is gone: if P82 moved it, this census moves with it (#2443)");

    [Fact]
    public void EveryOperatorLoweringKindAndKeyKind_HasAnEntryInAFixtureWithCommittedOutput()
    {
        var (roster, _, narrowToRoster, _) = ScanCallSites();
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
        var onBinary = new HashSet<(OperatorLoweringKind Kind, BinaryOperator Op)>();
        var onAugmented = new HashSet<(OperatorLoweringKind Kind, AssignmentOperator Op)>();
        var nativeWithNarrowTo = 0;
        var notClean = new List<string>();
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
            {
                notClean.Add(fixture.TestName);
                continue;
            }

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
                    {
                        narrowToByKeyKind[keyKind] = narrowToByKeyKind.GetValueOrDefault(keyKind) + 1;
                        if (lowering.Kind == OperatorLoweringKind.Native)
                            nativeWithNarrowTo++;
                    }

                    if (entry.Key is BinaryOp binary)
                        onBinary.Add((lowering.Kind, binary.Operator));
                    else if (entry.Key is Assignment augmented)
                        onAugmented.Add((lowering.Kind, augmented.Operator));
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

        // A fixture with committed output that does not compile clean would be dropped unseen.
        Assert.True(notClean.Count == 0,
            "fixture(s) with an .expected file that did not compile clean, so the census read nothing "
            + "from them: " + string.Join(", ", notClean.Take(25)));

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

        // NarrowTo is the record's second member and moves with the kind. Its writers are read from
        // the call sites (today: the augmented assignment that narrows its result, and the
        // super-operator call, whose NarrowTo is the base type).
        Assert.True(narrowToRoster.Count > 0, "no SetOperatorLowering call site passes a NarrowTo: the scan reads nothing");
        foreach (var keyKind in narrowToRoster)
        {
            Assert.True(narrowToByKeyKind.GetValueOrDefault(keyKind) > 0,
                $"no {keyKind} entry with a NarrowTo in any fixture that has an .expected file: the "
                + "member would move onto the IR with nothing observing it (#2443)");
        }

        Assert.True(nativeWithNarrowTo > 0,
            "no (Native, NarrowTo) entry in any fixture that has an .expected file: an augmented "
            + "assignment that only narrows records the absent kind WITH a fact (#2443)");

        // The binary / augmented pair (class doc).
        var missingTwins = new List<string>();
        var staleTwinExclusions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (kind, op) in onBinary.OrderBy(c => c.Kind).ThenBy(c => c.Op))
        {
            if (AugmentedTwin(op) is not { } twin)
                continue;
            var seen = onAugmented.Contains((kind, twin));
            if (NotOnAugmented.ContainsKey(kind))
            {
                if (seen)
                    staleTwinExclusions.Add(kind.ToString());
            }
            else if (!seen)
            {
                missingTwins.Add($"{kind} on {op} has no entry on {twin}");
            }
        }

        Assert.True(missingTwins.Count == 0,
            "kind(s) a BinaryOp records in the corpus whose augmented twin no fixture with an .expected "
            + "file records. Add the augmented line to a fixture, or a NotOnAugmented row with the reason "
            + "the checker cannot record it (#2443):\n  " + string.Join("\n  ", missingTwins));
        Assert.True(staleTwinExclusions.Count == 0,
            "NotOnAugmented row(s) whose kind now appears on its augmented twin; delete the row: "
            + string.Join(", ", staleTwinExclusions));

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
        var (roster, sites, narrowToRoster, unresolved) = ScanCallSites();

        _output.WriteLine($"{sites} SetOperatorLowering call site(s); roster: {string.Join(", ", roster)}; "
            + $"NarrowTo writers: {string.Join(", ", narrowToRoster)}");
        Assert.True(sites > 0, "no SetOperatorLowering call site found under Semantic/: the scan reads nothing");
        Assert.True(unresolved.Count == 0,
            "SetOperatorLowering call site(s) whose first argument is not a parameter, local or pattern "
            + "variable of the enclosing member; extend ScanCallSites:\n  " + string.Join("\n  ", unresolved));
        Assert.All(roster, name => Assert.True(
            typeof(Node).Assembly.GetTypes().Any(t => t.Name == name && typeof(Node).IsAssignableFrom(t)),
            $"'{name}' is not an AST node type"));
    }

    private static (SortedSet<string> Roster, int Sites, SortedSet<string> NarrowToRoster, List<string> Unresolved)
        ScanCallSites()
    {
        var semanticDir = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler", "Semantic");
        var roster = new SortedSet<string>(StringComparer.Ordinal);
        var narrowToRoster = new SortedSet<string>(StringComparer.Ordinal);
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

                if (DeclaredTypeOf(call, name) is not { } type)
                {
                    unresolved.Add($"{where}: no declaration of '{name}' found before the call in an enclosing scope");
                    continue;
                }

                roster.Add(type);

                // A site writes a NarrowTo when its `new OperatorLowering(...)` passes a second argument.
                var passesNarrowTo = call.ArgumentList.Arguments.Skip(1)
                    .SelectMany(a => a.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>())
                    .Any(creation => creation.ArgumentList is { Arguments.Count: > 1 });
                if (passesNarrowTo)
                    narrowToRoster.Add(type);
            }
        }

        return (roster, sites, narrowToRoster, unresolved);
    }

    /// <summary>
    /// The declared type of <paramref name="name"/> as seen from <paramref name="use"/>: the nearest
    /// declaration that starts before the use, in the innermost enclosing function first. A parameter,
    /// a typed local, or a declaration pattern (<c>is BinaryOp b</c>, <c>case UnaryOp u</c>) counts;
    /// a declaration inside a nested lambda or local function that does not enclose the use does not,
    /// and neither does one that follows it.
    /// </summary>
    private static string? DeclaredTypeOf(Microsoft.CodeAnalysis.SyntaxNode use, string name)
    {
        static bool IsFunction(Microsoft.CodeAnalysis.SyntaxNode node) => node is BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax;

        for (var scope = use.Parent; scope != null; scope = scope.Parent)
        {
            if (!IsFunction(scope))
                continue;

            string? nearest = null;
            foreach (var node in scope.DescendantNodes())
            {
                if (node.SpanStart >= use.SpanStart)
                    break;

                // Visible only if every function between the declaration and this scope encloses the use.
                var visible = true;
                for (var owner = node.Parent; owner != null && owner != scope; owner = owner.Parent)
                {
                    if (IsFunction(owner) && !owner.Span.Contains(use.Span))
                    {
                        visible = false;
                        break;
                    }
                }

                if (!visible)
                    continue;

                switch (node)
                {
                    case ParameterSyntax { Type: { } parameterType } parameter
                        when parameter.Identifier.ValueText == name:
                        nearest = SimpleName(parameterType);
                        break;
                    case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax designation } pattern
                        when designation.Identifier.ValueText == name:
                        nearest = SimpleName(pattern.Type);
                        break;
                    case RecursivePatternSyntax { Type: { } recursiveType, Designation: SingleVariableDesignationSyntax designation }
                        when designation.Identifier.ValueText == name:
                        nearest = SimpleName(recursiveType);
                        break;
                    case VariableDeclarationSyntax { Type: var localType } declaration
                        when declaration.Variables.Any(v => v.Identifier.ValueText == name):
                        // A `var` local hides an outer declaration and has no spelled type: unresolved.
                        nearest = localType.IsVar ? null : SimpleName(localType);
                        break;
                }
            }

            if (nearest != null)
                return nearest;
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

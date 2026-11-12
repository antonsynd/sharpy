using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// T3 of P82 tranche 3 (#2443, plan-92249e): the transport sweep. The checker records an operator
/// lowering per AST key (<c>SemanticInfo._operatorLowerings</c>); the lowering pass copies it onto
/// the key's IR node, an <see cref="IIrOperatorSite"/> (D1). An index miss reads <c>Native</c>, which
/// is also the right value for most nodes, so a missing pass arm is silent in output; this sweep
/// compares the table with the IR, per compiled program of the fixture corpus (every fixture with an
/// <c>.expected</c> file, <see cref="OperatorLoweringCorpusCensusTests.CorpusFixtures"/>), in both
/// directions:
/// <list type="bullet">
/// <item><b>entry → site.</b> Every entry's key indexes an <see cref="IIrOperatorSite"/> (looking
/// through an <see cref="IrOptionalWrappedElement"/>, as the emitter does) whose <c>Operator</c> and
/// <c>NarrowTo</c> equal the entry's <c>Kind</c> and <c>NarrowTo</c>.</item>
/// <item><b>site → entry.</b> Every <see cref="IIrOperatorSite"/> in the IR (tree and index) has the
/// equal entry at its key, where a site reading <c>(Native, null)</c> equals an absent entry (the
/// table's own convention: most <see cref="IrEqualityComparison"/> and plain-<c>=</c>
/// <see cref="IrOptionalStoreWrap"/> nodes). A site an opt-in IR pass rebuilt is keyed by the indexed
/// site it replaced (same kind, same span); one it folded away leaves the indexed site, still checked.</item>
/// <item><b>D1's two predicates.</b> Entries keyed on a node whose IR is an <see cref="IrConstant"/> (a
/// folded power) or an <see cref="IrCollectionMutation"/> = 0, each measured over a non-empty set of
/// such keys (the positive control is in the same test).</item>
/// <item><b>Carriers.</b> Every implementer of <see cref="IIrOperatorSite"/> in the compiler assembly
/// (reflection, so a fifth carrier is red here) is seen carrying a non-<c>Native</c> operator.</item>
/// <item><b>Key kinds.</b> Every key kind a <c>SetOperatorLowering(</c> call site writes (the census's
/// own call-site roster) appears on the entry → site side, and no entry is keyed outside it.</item>
/// </list>
/// Categorised <c>GapDiscovery</c>: a standing corpus sweep, not an edit-loop test.
/// </summary>
[Trait("Category", "GapDiscovery")]
public class OperatorLoweringTransportTests
{
    private readonly ITestOutputHelper _output;

    public OperatorLoweringTransportTests(ITestOutputHelper output) => _output = output;

    private static readonly EqualityComparer<SemanticType?> TypeEquality = EqualityComparer<SemanticType?>.Default;

    [Fact]
    public void EveryOperatorLoweringEntry_IsCarriedByItsIrNode_AndEveryOperatorSite_HasItsEntry()
    {
        var (roster, _, _, _) = OperatorLoweringCorpusCensusTests.ScanCallSites();
        var implementers = typeof(IIrOperatorSite).Assembly.GetTypes()
            .Where(t => typeof(IIrOperatorSite).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.True(implementers.Count > 0, "no IIrOperatorSite implementer found by reflection: the carrier floor is vacuous");

        var fixtures = OperatorLoweringCorpusCensusTests.CorpusFixtures();
        Assert.True(fixtures.Count > 0, "no fixture with an .expected file was discovered: the sweep is vacuous");

        // entry → site: key kind × carrier (the IR node the key indexes, wrapped elements looked through).
        var cells = new SortedDictionary<(string KeyKind, string Carrier), int>();
        var entriesByKeyKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var carriedByKeyKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        // site → entry: carrier × (non-Native operator | Native with an entry | Native, absent entry).
        var sitesNonNative = implementers.ToDictionary(n => n, _ => 0);
        var sitesNativeWithEntry = implementers.ToDictionary(n => n, _ => 0);
        var sitesNativeAbsent = implementers.ToDictionary(n => n, _ => 0);
        int entries = 0, viaWrappedElement = 0, treeSites = 0, rewrittenSites = 0;
        int foldedPowerKeys = 0, collectionMutationKeys = 0;
        var entryToSiteMisses = new List<string>();
        var siteToEntryMisses = new List<string>();
        var onFoldedPower = new List<string>();
        var onCollectionMutation = new List<string>();
        var unrostered = new SortedSet<string>(StringComparer.Ordinal);
        var unkeyedTreeSites = new List<string>();
        var indexOnlySites = new List<string>();
        var internalErrors = new List<string>();
        var notClean = new List<string>();
        var compiled = 0;

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

            // A project's units share one SemanticInfo and one IR; read each instance once.
            var infos = new HashSet<SemanticInfo>(ReferenceEqualityComparer.Instance);
            var irs = new HashSet<IrCompilation>(ReferenceEqualityComparer.Instance);
            foreach (var (context, _) in result.Units)
            {
                if (context.SemanticInfo is { } info)
                    infos.Add(info);
                irs.Add(context.Ir);
            }

            var table = new Dictionary<Node, OperatorLowering>(ReferenceEqualityComparer.Instance);
            foreach (var info in infos)
            {
                foreach (var (key, lowering) in OperatorLoweringCorpusCensusTests.EntriesOf(info))
                    table[key] = lowering;
            }

            // The index, and its inverse: the AST key of each operator site (an indexed node, or the
            // single child of an indexed wrapped element).
            var index = new Dictionary<Node, IrNode>(ReferenceEqualityComparer.Instance);
            var keyOfSite = new Dictionary<IrNode, Node>(ReferenceEqualityComparer.Instance);
            foreach (var ir in irs)
            {
                foreach (var (key, node) in ir.Index)
                {
                    index[key] = node;
                    if (Unwrap(node) is IIrOperatorSite)
                        keyOfSite[Unwrap(node)] = key;
                    if (Unwrap(node) is IrConstant && key is BinaryOp { Operator: BinaryOperator.Power })
                        foldedPowerKeys++;
                    if (Unwrap(node) is IrCollectionMutation)
                        collectionMutationKeys++;
                }
            }

            // entry → site.
            foreach (var (key, lowering) in table)
            {
                entries++;
                var keyKind = key.GetType().Name;
                entriesByKeyKind[keyKind] = entriesByKeyKind.GetValueOrDefault(keyKind) + 1;
                if (!roster.Contains(keyKind))
                    unrostered.Add(keyKind);
                var where = $"{fixture.TestName}: {keyKind} @ {key.LineStart}:{key.ColumnStart} ({lowering.Kind}"
                    + (lowering.NarrowTo is { } n ? $", {n.GetDisplayName()}" : "") + ")";

                if (!index.TryGetValue(key, out var indexed))
                {
                    cells[(keyKind, "(not indexed)")] = cells.GetValueOrDefault((keyKind, "(not indexed)")) + 1;
                    entryToSiteMisses.Add($"{where}: the key is not in the IR index");
                    continue;
                }

                var carrier = Unwrap(indexed);
                if (!ReferenceEquals(carrier, indexed))
                    viaWrappedElement++;
                cells[(keyKind, carrier.GetType().Name)] = cells.GetValueOrDefault((keyKind, carrier.GetType().Name)) + 1;

                // D1's two predicates: such a key never has an entry.
                if (carrier is IrConstant)
                {
                    onFoldedPower.Add(where);
                    continue;
                }

                if (carrier is IrCollectionMutation)
                {
                    onCollectionMutation.Add(where);
                    continue;
                }

                if (carrier is not IIrOperatorSite site)
                {
                    entryToSiteMisses.Add($"{where}: indexes a {carrier.GetType().Name}, not an operator site");
                    continue;
                }

                if (site.Operator != lowering.Kind || !TypeEquality.Equals(site.NarrowTo, lowering.NarrowTo))
                {
                    entryToSiteMisses.Add(
                        $"{where}: the {carrier.GetType().Name} reads ({site.Operator}, {Show(site.NarrowTo)})");
                    continue;
                }

                carriedByKeyKind[keyKind] = carriedByKeyKind.GetValueOrDefault(keyKind) + 1;
            }

            // site → entry, over the tree and the index (one visit per node). The index is built by the
            // lowering pass and not rewritten by the opt-in IR passes, so a site a pass rebuilt (its
            // children folded) is in the tree but not the index; it is keyed by the indexed site of the
            // same kind at the same span, the node it replaced. Folded away entirely (`-5` under
            // opt_const_fold), the indexed site is index-only.
            var bySpan = new Dictionary<(Type, TextSpan), Node>();
            foreach (var (siteNode, key) in keyOfSite)
                bySpan.TryAdd((siteNode.GetType(), siteNode.Span), key);

            var keyedSites = new List<(IrNode Site, Node Key)>();
            var sites = new HashSet<IrNode>(ReferenceEqualityComparer.Instance);

            void Visit(IrNode node)
            {
                if (node is IIrOperatorSite && sites.Add(node))
                {
                    treeSites++;
                    if (keyOfSite.TryGetValue(node, out var key))
                        keyedSites.Add((node, key));
                    else if (bySpan.TryGetValue((node.GetType(), node.Span), out var rebuiltKey))
                    {
                        rewrittenSites++;
                        keyedSites.Add((node, rebuiltKey));
                    }
                    else
                        unkeyedTreeSites.Add($"{fixture.TestName}: {node.GetType().Name} @ {node.Span}");
                }

                foreach (var child in node.Children)
                    Visit(child);
            }

            foreach (var ir in irs)
            {
                foreach (var module in ir.Modules)
                {
                    foreach (var statement in module.Body)
                        Visit(statement);
                }
            }

            foreach (var (siteNode, key) in keyOfSite)
            {
                if (!sites.Add(siteNode))
                    continue;
                indexOnlySites.Add($"{fixture.TestName}: {siteNode.GetType().Name} @ {siteNode.Span} ({key.GetType().Name} key)");
                keyedSites.Add((siteNode, key));
            }

            foreach (var (siteNode, key) in keyedSites)
            {
                var site = (IIrOperatorSite)siteNode;
                var carrierName = siteNode.GetType().Name;
                var expected = table.GetValueOrDefault(key);
                var expectedKind = expected?.Kind ?? OperatorLoweringKind.Native;
                if (site.Operator != expectedKind || !TypeEquality.Equals(site.NarrowTo, expected?.NarrowTo))
                {
                    siteToEntryMisses.Add(
                        $"{fixture.TestName}: {carrierName} @ {siteNode.Span} reads ({site.Operator}, "
                        + $"{Show(site.NarrowTo)}); its {key.GetType().Name} key's entry is "
                        + (expected is null ? "absent (Native, null)" : $"({expected.Kind}, {Show(expected.NarrowTo)})"));
                    continue;
                }

                if (site.Operator != OperatorLoweringKind.Native)
                    sitesNonNative[carrierName]++;
                else if (expected is not null)
                    sitesNativeWithEntry[carrierName]++;
                else
                    sitesNativeAbsent[carrierName]++;
            }
        }

        _output.WriteLine($"fixtures with .expected: {fixtures.Count}, compiled clean: {compiled}, entries: {entries}");
        _output.WriteLine("key-kind roster (SetOperatorLowering call sites): " + string.Join(", ", roster));
        _output.WriteLine("entry -> site (key kind x carrier):");
        foreach (var keyKind in entriesByKeyKind.Keys)
        {
            var row = string.Join(", ", cells.Where(c => c.Key.KeyKind == keyKind).Select(c => $"{c.Key.Carrier}={c.Value}"));
            _output.WriteLine(
                $"  {keyKind,-14} {entriesByKeyKind[keyKind],6} entries, {carriedByKeyKind.GetValueOrDefault(keyKind),6} carried  [{row}]");
        }

        _output.WriteLine($"  via IrOptionalWrappedElement: {viaWrappedElement}");
        _output.WriteLine($"site -> entry (operator sites: {treeSites} in the tree, of which {rewrittenSites} rebuilt by an "
            + $"IR pass; {indexOnlySites.Count} index-only):");
        foreach (var name in implementers)
        {
            _output.WriteLine(
                $"  {name,-22} non-Native={sitesNonNative[name],6}  (Native, entry)={sitesNativeWithEntry[name],6}  "
                + $"(Native, absent)={sitesNativeAbsent[name],6}");
        }

        foreach (var line in indexOnlySites)
            _output.WriteLine("  index-only: " + line);
        _output.WriteLine(
            $"D1 predicates: entries on IrConstant (folded power) keys = {onFoldedPower.Count} of {foldedPowerKeys} keys; "
            + $"on IrCollectionMutation keys = {onCollectionMutation.Count} of {collectionMutationKeys} keys");

        Assert.True(internalErrors.Count == 0,
            $"{internalErrors.Count} fixture(s) hit an internal compiler error; the sweep does not skip them:\n"
            + string.Join("\n", internalErrors.Take(25)));
        Assert.True(notClean.Count == 0,
            "fixture(s) with an .expected file that did not compile clean, so the sweep read nothing from them: "
            + string.Join(", ", notClean.Take(25)));
        Assert.True(entries > 0, "no operator lowering entry in the corpus: the sweep is vacuous");

        // Every operator site in the tree is reachable by its key (the emitter reads the IR by key).
        Assert.True(unkeyedTreeSites.Count == 0,
            $"{unkeyedTreeSites.Count} operator site(s) in the IR tree that no index key reaches, so the emitter cannot read them by key:\n  "
            + string.Join("\n  ", unkeyedTreeSites.Take(40)));

        // (a) entry → site.
        Assert.True(entryToSiteMisses.Count == 0,
            $"{entryToSiteMisses.Count} operator lowering entr(y/ies) the IR does not carry at the key; the emitter "
            + "would read Native there once it reads the IR (#2443, plan-92249e D1):\n  "
            + string.Join("\n  ", entryToSiteMisses.Take(40)));

        // (b) site → entry.
        Assert.True(siteToEntryMisses.Count == 0,
            $"{siteToEntryMisses.Count} operator site(s) whose operator differs from their key's entry "
            + "((Native, null) = absent) (#2443, plan-92249e D1):\n  " + string.Join("\n  ", siteToEntryMisses.Take(40)));

        // (c) D1's two predicates, each over a non-empty set of keys (positive control).
        Assert.True(foldedPowerKeys > 0,
            "no folded-power (IrConstant) key in the corpus: 'no entry on a folded power' is measured over nothing");
        Assert.True(onFoldedPower.Count == 0,
            "operator lowering entr(y/ies) keyed on a folded constant power (IrConstant): D1's predicate "
            + "(TryFoldIntegerPower returns before every SetOperatorLowering) is false:\n  "
            + string.Join("\n  ", onFoldedPower.Take(40)));
        Assert.True(collectionMutationKeys > 0,
            "no IrCollectionMutation key in the corpus: 'no entry on a collection mutation' is measured over nothing");
        Assert.True(onCollectionMutation.Count == 0,
            "operator lowering entr(y/ies) keyed on an in-place collection mutation (IrCollectionMutation): D1's "
            + "predicate (the mutation path returns before SetOperatorLowering) is false:\n  "
            + string.Join("\n  ", onCollectionMutation.Take(40)));

        // (d) every carrier is seen carrying a non-Native operator.
        var unseenCarriers = implementers.Where(n => sitesNonNative[n] == 0).ToList();
        Assert.True(unseenCarriers.Count == 0,
            "IIrOperatorSite implementer(s) never seen carrying a non-Native operator in a fixture that has an "
            + ".expected file; add a fixture whose output differs when it reads Native (#2443): "
            + string.Join(", ", unseenCarriers));

        // (e) every key kind is carried; no entry is keyed outside the roster.
        Assert.True(roster.Count > 0, "the SetOperatorLowering call-site roster is empty: the key-kind floor is vacuous");
        var uncarriedKeyKinds = roster.Where(k => carriedByKeyKind.GetValueOrDefault(k) == 0).ToList();
        Assert.True(uncarriedKeyKinds.Count == 0,
            "key kind(s) a SetOperatorLowering call site writes with no entry carried by an IR site in the corpus: "
            + string.Join(", ", uncarriedKeyKinds));
        Assert.True(unrostered.Count == 0,
            "entries keyed by an AST type no SetOperatorLowering call site names: " + string.Join(", ", unrostered));
    }

    private static string Show(SemanticType? type) => type?.GetDisplayName() ?? "null";

    /// <summary>The node a key's operator fact rides on: a wrapped tuple element's own node (D3's look-through).</summary>
    private static IrNode Unwrap(IrNode node)
        => node is IrOptionalWrappedElement { Children: [var element] } ? element : node;
}

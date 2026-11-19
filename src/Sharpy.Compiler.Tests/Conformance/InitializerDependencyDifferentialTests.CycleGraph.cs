using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;
using ParameterModifier = Sharpy.Compiler.Parser.Ast.ParameterModifier;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;
using Validator = Sharpy.Compiler.Semantic.Validation.InitializerCycleValidator;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The cycle graph's slot map and node kinds are the generator's (plan-775c50 D8, T9). The validator
/// (<see cref="InitializerCycleValidator"/>) classifies every slot of every concrete <see cref="Expression"/> subclass, every
/// <see cref="BinaryOperator"/> and every <see cref="ParameterModifier"/>; where this generator classifies a slot, the two
/// classifications are equal, case by case, and the validator's walk (<see cref="InitializerCycleValidator.SlotsOf"/>)
/// places each entry's read in a slot of the class the entry names. The validator's node kinds are the image of
/// <see cref="NodeKinds"/>. A slot moved to the other class, or a node kind dropped, is a line here.
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    /// <summary>The generator's node kinds, as the validator names them.</summary>
    private static readonly IReadOnlyDictionary<BindingKind, Validator.NodeKind> ValidatorNodeKinds = new Dictionary<BindingKind, Validator.NodeKind>
    {
        [BindingKind.ModuleVar] = Validator.NodeKind.ModuleVariable,
        [BindingKind.ModuleComputedConst] = Validator.NodeKind.ModuleConst,
        [BindingKind.ModuleProperty] = Validator.NodeKind.ModuleProperty,
        [BindingKind.StaticField] = Validator.NodeKind.StaticField,
        [BindingKind.StaticProperty] = Validator.NodeKind.StaticProperty,
        [BindingKind.ClassComputedConst] = Validator.NodeKind.TypeConst,
    };

    private static Validator.SlotEvaluation Expected(SlotEntry entry) =>
        entry.Stores ? Validator.SlotEvaluation.Stored
        : entry.When == Evaluated.Always ? Validator.SlotEvaluation.Always
        : Validator.SlotEvaluation.Conditionally;

    /// <summary>Every way the validator's map and node kinds differ from the generator's, one line each.</summary>
    internal static List<string> CycleGraphMapFailures()
    {
        var failures = new List<string>();

        // Totality: the validator's keys are every slot of every concrete Expression subclass, the generator's excluded shapes included.
        var nodes = typeof(Expression).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(Expression).IsAssignableFrom(t)).ToList();
        var found = nodes.SelectMany(SlotKeys).ToHashSet(StringComparer.Ordinal);
        if (found.Count < 50)
            failures.Add($"the scan found {found.Count} slots: it reads nothing");
        var classified = Validator.SlotMap.Keys.Append(Validator.BinaryRightSlot).Append(Validator.ModifiedArgumentSlot).ToHashSet(StringComparer.Ordinal);
        foreach (var key in found.Where(k => !classified.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
            failures.Add($"slot {key} is not in the validator's map");
        foreach (var key in classified.Where(k => !found.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
            failures.Add($"the validator's map holds {key}, which is no slot of a concrete Expression subclass");
        foreach (var (mine, theirs) in new[] { (Validator.BinaryRightSlot, BinaryRight), (Validator.ModifiedArgumentSlot, ModifiedArgumentSlot) })
        {
            if (!string.Equals(mine, theirs, StringComparison.Ordinal))
                failures.Add($"the validator keys {theirs} as {mine}");
        }
        foreach (var op in Enum.GetValues<BinaryOperator>().Where(o => !Validator.BinaryRights.ContainsKey(o)))
            failures.Add($"BinaryOperator.{op} is not in the validator's map");
        foreach (var modifier in Enum.GetValues<ParameterModifier>().Where(m => !Validator.ModifiedArguments.ContainsKey(m)))
            failures.Add($"ParameterModifier.{modifier} is not in the validator's map");

        // Equality, case by case, wherever the generator classifies.
        foreach (var (key, entries) in Slots)
        {
            if (!Validator.SlotMap.TryGetValue(key, out var cases))
                continue;
            var generatorCases = entries.Select(e => e.Case).OrderBy(c => c, StringComparer.Ordinal).ToList();
            var validatorCases = cases.Select(c => c.Case).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (!generatorCases.SequenceEqual(validatorCases, StringComparer.Ordinal))
                failures.Add($"slot {key}: the generator's cases [{string.Join(", ", generatorCases)}] are not the validator's [{string.Join(", ", validatorCases)}]");
            foreach (var entry in entries)
            {
                var mine = cases.FirstOrDefault(c => c.Case == entry.Case);
                if (mine != null && mine.Evaluation != Expected(entry))
                    failures.Add($"slot {key}{(entry.Case == null ? "" : "/" + entry.Case)}: the generator says {Expected(entry)}, the validator {mine.Evaluation}");
            }
        }
        foreach (var (op, entry) in BinaryRights)
        {
            if (Validator.BinaryRights.TryGetValue(op, out var mine) && mine != Expected(entry))
                failures.Add($"{BinaryRight}/{op}: the generator says {Expected(entry)}, the validator {mine}");
        }
        foreach (var (modifier, entry) in ModifiedArguments)
        {
            if (Validator.ModifiedArguments.TryGetValue(modifier, out var mine) && mine != Expected(entry))
                failures.Add($"{ModifiedArgumentSlot}/{modifier}: the generator says {Expected(entry)}, the validator {mine}");
        }

        // The node kinds: the validator's are the image of the generator's, const for const.
        foreach (var kind in NodeKinds)
        {
            if (!ValidatorNodeKinds.TryGetValue(kind.Kind, out var mine))
                failures.Add($"node BindingKind.{kind.Kind} has no validator kind");
            else if (!Validator.NodeKinds.TryGetValue(mine, out var isConst))
                failures.Add($"node BindingKind.{kind.Kind} ({mine}) is no node of the validator's");
            else if (isConst != kind.IsConst)
                failures.Add($"node BindingKind.{kind.Kind} ({mine}) is {(kind.IsConst ? "" : "no ")}const here and {(isConst ? "" : "no ")}const in the validator");
        }
        foreach (var mine in Validator.NodeKinds.Keys.Where(k => !ValidatorNodeKinds.Values.Contains(k)))
            failures.Add($"the validator's node kind {mine} is no generator node kind");

        // The walk: each entry's read lies under slots whose classes combine to the entry's.
        foreach (var (id, key, entry) in SlotProduct())
        {
            var text = entry.Sharpy.Replace(Operand, SlotRead, StringComparison.Ordinal);
            var module = new SharpyParser(new SharpyLexer($"t0: int = {text}\n").TokenizeAll()).ParseModule();
            var initializer = module.Body.OfType<VariableDeclaration>().Single().InitialValue!;
            var reads = new List<(Validator.SlotEvaluation Evaluation, List<string> Keys)>();
            FindRead(initializer, Validator.SlotEvaluation.Always, new List<string>(), reads);
            if (reads.Count != 1)
            {
                failures.Add($"{id}: the validator's walk finds the read {reads.Count} times in '{text}'");
                continue;
            }
            var (evaluation, keys) = reads[0];
            if (!keys.Contains(key, StringComparer.Ordinal))
                failures.Add($"{id}: the validator's walk places the read under [{string.Join(", ", keys)}], not under {key}");
            if (evaluation != Expected(entry))
                failures.Add($"{id}: the validator's walk classifies the read {evaluation}, the generator {Expected(entry)}");
        }

        return failures;
    }

    /// <summary>The validator's walk to the family's read: a store anywhere above it stores it, a conditional slot
    /// makes it conditional.</summary>
    private static void FindRead(Node node, Validator.SlotEvaluation along, List<string> keys, List<(Validator.SlotEvaluation, List<string>)> reads)
    {
        if (node is MemberAccess { Object: Identifier { Name: SlotMarker }, Member: "q" } || node is MemberAccessPattern { Parts: [SlotMarker, "q"] })
        {
            reads.Add((along, keys.ToList()));
            return;
        }
        if (node is not Expression expression)
            return;
        foreach (var slot in Validator.SlotsOf(expression))
        {
            var next = along == Validator.SlotEvaluation.Stored || slot.Evaluation == Validator.SlotEvaluation.Stored ? Validator.SlotEvaluation.Stored
                : along == Validator.SlotEvaluation.Conditionally || slot.Evaluation == Validator.SlotEvaluation.Conditionally ? Validator.SlotEvaluation.Conditionally
                : Validator.SlotEvaluation.Always;
            keys.Add(slot.Key);
            FindRead(slot.Child, next, keys, reads);
            keys.RemoveAt(keys.Count - 1);
        }
    }

    [Fact]
    public void CycleGraph_SlotMapAndNodeKinds_AreTheGenerators()
    {
        var failures = CycleGraphMapFailures();
        Assert.True(failures.Count == 0, $"{failures.Count} difference(s):\n{string.Join("\n", failures)}");
    }
}

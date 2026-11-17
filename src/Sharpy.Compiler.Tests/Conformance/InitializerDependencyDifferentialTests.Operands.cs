using System.Globalization;
using Sharpy.Compiler.Parser.Ast;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The operand family (plan-775c50, second stage-0 commit): a <c>ref</c> / <c>out</c> / <c>in</c> operand names a
/// variable. Two axes, each total over its source: <see cref="ParameterModifier"/> and <see cref="BindingKind"/>.
/// The expectation is one rule, <see cref="OperandRefusal"/>: every modifier on a variable runs; every modifier on a
/// property is refused, in every host, with or without a default (the owner's ruling of 2026-11-16, #2422 comment
/// 6923063788, Q8); <c>ref</c> and <c>out</c> on a const are refused, because the caller passes a variable
/// (<c>parameter_modifiers.md</c>); <c>in</c> on a computed const runs, as it does today. The plain argument is the
/// control of every operand.
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    internal enum OperandClass
    {
        Variable,
        Property,
        ComputedConst,
        LiteralConst,
    }

    /// <summary>One operand: its declaration lines and its text, an int worth <see cref="Value"/>.</summary>
    internal sealed record OperandSpec(string Key, BindingKind Kind, OperandClass Class, string[] Declaration, string Text, int Value = DependencyValue);

    private static readonly string[] InstanceHolder = { "o: D0 = D0()" };

    internal static readonly IReadOnlyList<OperandSpec> Operands = new OperandSpec[]
    {
        new("ModuleVar", BindingKind.ModuleVar, OperandClass.Variable, new[] { "k: int = 3" }, "k"),
        new("ModuleLiteralConst", BindingKind.ModuleLiteralConst, OperandClass.LiteralConst, new[] { "const k: int = 3" }, "k"),
        new("ModuleComputedConst", BindingKind.ModuleComputedConst, OperandClass.ComputedConst, new[] { "const k: int = three()" }, "k"),
        new("ModuleProperty", BindingKind.ModuleProperty, OperandClass.Property, new[] { "property k: int = 3" }, "k"),
        new("ModuleProperty.NoDefault", BindingKind.ModuleProperty, OperandClass.Property, new[] { "property k: int" }, "k", StorageDefault),
        new("StaticField", BindingKind.StaticField, OperandClass.Variable, new[] { "class D0:", "    @static", "    k: int = 3" }, "D0.k"),
        new("StaticProperty", BindingKind.StaticProperty, OperandClass.Property, new[] { "class D0:", "    @static", "    property k: int = 3" }, "D0.k"),
        new("StaticProperty.NoDefault", BindingKind.StaticProperty, OperandClass.Property, new[] { "class D0:", "    @static", "    property k: int" }, "D0.k", StorageDefault),
        new("ClassLiteralConst", BindingKind.ClassLiteralConst, OperandClass.LiteralConst, new[] { "class D0:", "    const k: int = 3" }, "D0.k"),
        new("ClassComputedConst", BindingKind.ClassComputedConst, OperandClass.ComputedConst, new[] { "class D0:", "    const k: int = three()" }, "D0.k"),
        new("InstanceField", BindingKind.InstanceField, OperandClass.Variable, new[] { "class D0:", "    k: int = 3" }.Concat(InstanceHolder).ToArray(), "o.k"),
        new("InstanceProperty", BindingKind.InstanceProperty, OperandClass.Property, new[] { "class D0:", "    property k: int = 3" }.Concat(InstanceHolder).ToArray(), "o.k"),
    };

    private const string NotAnIntValue = "names a function or a type: no argument passes it by reference as an int";

    private const string PositionLocal = "position-local: it lives in a body of its own, no rule of N3 names it, and the fixtures of `parameter_modifiers.md` hold it";

    private const string OtherModule = "a value of another module: it needs the project route, which is the hand controls' axis";

    private const string LiteralOperandProgram = "def bump(a: ref int) -> int:\n    a = a + 1\n    return a\ndef main() -> None:\n    print(bump(ref 3))\n";

    private const string InLiteralProgram = "def peek(a: in int) -> int:\n    return a\ndef main() -> None:\n    print(peek(in 3))\n";

    internal static readonly IReadOnlyDictionary<BindingKind, Exclusion> ExcludedOperandKinds = new Dictionary<BindingKind, Exclusion>
    {
        [BindingKind.ModuleLet] = new("a module `let` is refused where it is declared: no program holds one", "let t = 3\n" + MainPrintingT, "SPY0340"),
        [BindingKind.ModuleDef] = new(NotAnIntValue),
        [BindingKind.ModuleClass] = new(NotAnIntValue),
        [BindingKind.ModuleStruct] = new(NotAnIntValue),
        [BindingKind.ModuleEnum] = new(NotAnIntValue),
        [BindingKind.ModuleUnion] = new(NotAnIntValue),
        [BindingKind.ModuleInterface] = new(NotAnIntValue),
        [BindingKind.ModuleTypeAlias] = new(NotAnIntValue),
        [BindingKind.StaticMethod] = new(NotAnIntValue),
        [BindingKind.NestedType] = new(NotAnIntValue),
        [BindingKind.InstanceMethod] = new(NotAnIntValue),
        [BindingKind.Constructor] = new(NotAnIntValue),
        [BindingKind.EnclosingLocal] = new(PositionLocal),
        [BindingKind.Parameter] = new(PositionLocal),
        [BindingKind.Inner] = new(PositionLocal),
        [BindingKind.LocalVar] = new(PositionLocal),
        [BindingKind.LocalLet] = new(PositionLocal),
        [BindingKind.LocalLiteralConst] = new(PositionLocal),
        [BindingKind.LocalComputedConst] = new(PositionLocal),
        [BindingKind.LocalDef] = new(PositionLocal),
        [BindingKind.LambdaTypedLocal] = new(PositionLocal),
        [BindingKind.Builtin] = new(NotAnIntValue),
        [BindingKind.ImportedName] = new(OtherModule),
        // Under `in` a literal is the literal const's internal error, the excluded pair's (OperandExclusions carries its program).
        [BindingKind.None] = new(
            "no binding: a literal names no value a call could store to, and under `ref` and `out` it is refused by its shape, before any name is resolved",
            LiteralOperandProgram,
            OperandRule),
    };

    /// <summary>The pairs of the product no rule decides. The exclusion's program draws the internal error the pair is today.</summary>
    internal static readonly IReadOnlyDictionary<(ParameterModifier Modifier, OperandClass Class), Exclusion> ExcludedOperandPairs =
        new Dictionary<(ParameterModifier, OperandClass), Exclusion>
        {
            [(ParameterModifier.In, OperandClass.LiteralConst)] = new(
                "unruled: the spec makes the `in` keyword optional at the call, the C# const has no storage to pass, and no ruling says whether the keyword is dropped or the operand refused (#2025's class)",
                "const k: int = 3\ndef peek(a: in int) -> int:\n    return a\ndef main() -> None:\n    print(peek(in k))\n",
                "SPY0908 CS8156"),
        };

    /// <summary>The refusal a call draws for an operand of a class under a modifier. Null: the call runs.</summary>
    internal const string OperandRule = "SPY0389";

    internal static string? OperandRefusal(ParameterModifier modifier, OperandClass operand) => (modifier, operand) switch
    {
        (ParameterModifier.None, OperandClass.Variable or OperandClass.Property or OperandClass.ComputedConst or OperandClass.LiteralConst) => null,
        (ParameterModifier.Ref or ParameterModifier.Out or ParameterModifier.In, OperandClass.Variable) => null,
        (ParameterModifier.Ref or ParameterModifier.Out or ParameterModifier.In, OperandClass.Property) => OperandRule,
        (ParameterModifier.Ref or ParameterModifier.Out, OperandClass.ComputedConst or OperandClass.LiteralConst) => OperandRule,
        (ParameterModifier.In, OperandClass.ComputedConst) => null,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), $"{modifier} on {operand} has no rule"),
    };

    /// <summary>The call under a modifier, then the operand again: the callee's answer and what the operand holds after it.</summary>
    private static (string Text, int Model) OperandUse(ParameterModifier modifier, string o, int v) => modifier switch
    {
        ParameterModifier.None => ($"peek0({o}) * 10 + {o}", v * 11),
        ParameterModifier.Ref => ($"bump(ref {o}) * 10 + {o}", (v + 1) * 11),
        ParameterModifier.Out => ($"fill(out {o}) * 100 + {o}", 142),
        ParameterModifier.In => ($"peek(in {o}) * 10 + {o}", v * 11),
        _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
    };

    /// <summary>An operand the checker must refuse is an internal error today: the C# compiler refuses it in the checker's place.</summary>
    private static Route OperandRoute(bool mustRefuse) => (verdict, _, _) => mustRefuse && verdict == VerdictClass.Ice ? $"{ReadinessIssue} ice" : null;

    private static string OperandId(Site site, ParameterModifier modifier, OperandSpec operand) => $"Operand/{site.Key}/{modifier}/{operand.Key}";

    /// <summary>A declaration, an expression placed at a site, and a <c>main</c> that prints it.</summary>
    private static string PlacedProgram(IEnumerable<string> declaration, Site site, string e)
    {
        var placed = Place(site.Host, site.Position!.Value, site.Nesting, e) ?? throw new InvalidOperationException($"{site.Key} has no placement");
        return WithHelpers(Harness.Join(declaration.Concat(placed.Lines)) + Harness.MainPrinting(placed.Access, placed.Setup));
    }

    private static IEnumerable<(Site Site, ParameterModifier Modifier, OperandSpec Operand)> OperandProduct() =>
        from site in SweepSites
        from modifier in Enum.GetValues<ParameterModifier>()
        from operand in Operands
        where !ExcludedOperandPairs.ContainsKey((modifier, operand.Class))
        select (site, modifier, operand);

    internal static IEnumerable<Cell> OperandCells()
    {
        foreach (var (site, modifier, operand) in OperandProduct())
        {
            var (text, model) = OperandUse(modifier, operand.Text, operand.Value);
            var refusal = OperandRefusal(modifier, operand.Class);
            // Python has no parameter modifier: the model is the rule's.
            yield return new Cell(OperandId(site, modifier, operand), Family.Operand, PlacedProgram(operand.Declaration, site, text), null, model.ToString(CultureInfo.InvariantCulture))
            {
                RefusalCode = refusal,
                IsControl = modifier == ParameterModifier.None,
                Python = PythonExpectation.NoTwin,
                Route = OperandRoute(refusal != null),
            };
        }
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> OperandExclusions() =>
        ExcludedOperandKinds.Select(e => ($"operand BindingKind.{e.Key}", e.Value))
            .Concat(ExcludedOperandPairs.Select(e => ($"operand {e.Key.Modifier} on {e.Key.Class}", e.Value)))
            .Append((
                "operand In on a literal",
                new Exclusion("unruled, as `in` on a literal const is (#2025's class): the literal has no storage to pass, and it is an internal error today", InLiteralProgram, "SPY0908 CS8156")));

    internal static List<string> OperandTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        foreach (var kind in Enum.GetValues<BindingKind>())
        {
            bool generated = Operands.Any(o => o.Kind == kind), excluded = ExcludedOperandKinds.ContainsKey(kind);
            if (generated == excluded)
                failures.Add($"operand BindingKind.{kind} is {Both(generated)}");
        }
        foreach (var dup in Operands.GroupBy(o => o.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"operand key {dup.Key} is declared twice");
        foreach (var cls in Enum.GetValues<OperandClass>().Where(c => Operands.All(o => o.Class != c)))
            failures.Add($"OperandClass.{cls} has no operand");

        // The rule is total over the product: every pair has a verdict or an exclusion, never both.
        foreach (var modifier in Enum.GetValues<ParameterModifier>())
            foreach (var cls in Enum.GetValues<OperandClass>())
            {
                var excluded = ExcludedOperandPairs.ContainsKey((modifier, cls));
                var ruled = true;
                try
                {
                    OperandRefusal(modifier, cls);
                }
                catch (ArgumentOutOfRangeException)
                {
                    ruled = false;
                }
                if (ruled == excluded)
                    failures.Add($"operand {modifier} on {cls} is {(ruled ? "both ruled and excluded" : "neither ruled nor excluded")}");
            }

        // What the axes owe, written again from the enumerations and the one exclusion map: the plain argument is the control.
        var owed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var site in SweepSites)
            foreach (var modifier in Enum.GetValues<ParameterModifier>())
                foreach (var operand in Operands.Where(o => !ExcludedOperandPairs.ContainsKey((modifier, o.Class))))
                    owed[OperandId(site, modifier, operand)] = modifier == ParameterModifier.None;
        failures.AddRange(PoolAgainstOwed("the operand axes", owed, pool.Where(c => c.Family == Family.Operand)));
        var ids = pool.Where(c => c.Family == Family.Operand).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (site, modifier, operand) in OperandProduct().Where(k => k.Modifier != ParameterModifier.None))
        {
            if (!ids.Contains(OperandId(site, ParameterModifier.None, operand)))
                failures.Add($"{OperandId(site, modifier, operand)} has no plain-argument control");
        }
        return failures;
    }
}

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.TestInfrastructure.Oracles;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The location family (plan-775c50, second to fourth stage-0 commits): a use of a module or <c>@static</c> value does
/// what it does on a C# field today, whenever it runs, except from inside the value's own initializer. Axes: the use
/// (<see cref="LocationUse"/>), the holder (<see cref="BindingKind"/>), whether the holder's type is generic
/// (<see cref="HostForm"/>), what the value is (<see cref="ValueKind"/>) and when the use runs (<see cref="When"/>).
///
/// A use is fed by one of four sources, and a use no source feeds is a totality failure:
/// <list type="bullet">
/// <item>a store site: the AST nodes that carry a <c>Target</c>, found by reflection, each with the emitter method that
/// writes it (<see cref="StoreSites"/>); that method set is the callers of the emitter's store seam, read from its source
/// (<see cref="EmitterStoreSeam"/>), as are the target arms of <c>GenerateStore</c> (<see cref="TargetArms"/>);</item>
/// <item>the whole-value store forms of the binder-site census (<see cref="StoreForm"/>);</item>
/// <item>a parameter modifier (<see cref="ParameterModifier"/>, <see cref="OperandUses"/>);</item>
/// <item>the two uses of a value that are neither: a read and a call on it (<see cref="ValueUses"/>).</item>
/// </list>
///
/// The expectation is one rule, <see cref="LocationModel"/>. A use that runs after the value is ready, an <c>await</c>
/// inside it or not, leaves what it wrote. A use that runs before the value's initializer (a function an earlier
/// initializer calls) is rule N3's: a plain store is kept until the initializer runs at its own place and overwrites
/// it, as python's module does, and an <c>out</c> operand is a plain store (the owner's ruling of 2026-11-16, #2422
/// comment 6925297897, Q12); any other use reads the value, so the value is initialized first and its initializer does
/// not run again.
///
/// The fourth stage-0 commit carries the owner's ruling of 2026-11-16 (#2422 comment 6927434861, Q14 to Q16, "go with
/// model B as described"): declaration order, never dropped, on every host. A generic host is no exception: each
/// closed generic type sweeps its own values in declaration order, so a plain store before the initializer is kept
/// and then replaced there too (<c>5 7</c>). And any use of a value, a read or a store, while its own initializer is
/// running is a cycle (Q15): a <c>NameError</c> at run time, SPY0278 where the store sits in the initializer's own text
/// in a slot the slot map classifies <see cref="Evaluated.Always"/>. Python runs the plain stores of that kind and prints
/// the initializer's value: the divergence each such cell records (<see cref="Cell.PythonModel"/>).
/// <see cref="CrossTypeCells"/> are the forward read from one type into another, on both host forms.
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    internal enum ValueKind
    {
        /// <summary>A value with no member to store through.</summary>
        Int,

        /// <summary>A value type with a field and a method that changes it: a copy and the location differ.</summary>
        Struct,

        /// <summary>A reference type of the same shape: the control of <see cref="Struct"/>.</summary>
        Class,

        /// <summary>A list: the value of the two target forms an int has none of, the index and the star.</summary>
        List,
    }

    internal enum LocationUse
    {
        Assign,
        Augmented,
        Walrus,
        RefOperand,
        MemberAssign,
        MemberAugmented,
        MutatingCall,
        MemberRefOperand,
        Read,
        TupleLeaf,
        NestedTupleLeaf,
        StarLeaf,
        ForTarget,
        WithTarget,
        ComprehensionTarget,
        IndexAssign,
        OutOperand,
        InOperand,
        MemberOutOperand,
        MemberInOperand,
    }

    internal enum When
    {
        Ready,

        /// <summary>The value is ready, and the use holds an <c>await</c> between naming the value and writing it.</summary>
        ReadyAcrossAwait,
        BeforeInitializer,

        /// <summary>The use runs inside the value's OWN initializer, in a function that initializer calls.</summary>
        DuringInitializer,

        /// <summary>The same, and the function then reads the value back: the second form of a plain store.</summary>
        DuringInitializerThenRead,

        /// <summary>The use sits in the text of the value's own initializer, where the compiler sees it.</summary>
        InInitializerText,
    }

    /// <summary>Whether the type that holds a <c>@static</c> value is generic: the plan's generic-host fact (D2), whose
    /// values are swept per closed type (D5). A closed literal set.</summary>
    internal enum HostForm
    {
        Plain,
        Generic,
    }

    // ------------------------------------------------------------------------------------------- //
    // The sources of a use
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A store site: the emitter method that writes the node's target, and the uses that reach it.</summary>
    internal sealed record StoreSite(string Emitter, LocationUse[] Uses);

    /// <summary>The AST nodes that carry a store target, each with its emitter method. The keys are the image of the AST
    /// (every node with a <c>Target</c>), the emitters the image of the callers of the emitter's store seam.</summary>
    internal static readonly IReadOnlyDictionary<Type, StoreSite> StoreSites = new Dictionary<Type, StoreSite>
    {
        [typeof(Assignment)] = new("GenerateAssignment", new[]
        {
            LocationUse.Assign, LocationUse.Augmented, LocationUse.TupleLeaf, LocationUse.NestedTupleLeaf, LocationUse.StarLeaf,
            LocationUse.MemberAssign, LocationUse.MemberAugmented, LocationUse.IndexAssign,
        }),
        [typeof(ForStatement)] = new("GenerateForEachCoreInner", new[] { LocationUse.ForTarget }),
        [typeof(WithItem)] = new("GenerateWithTargetStore", new[] { LocationUse.WithTarget }),
        [typeof(ForClause)] = new("BindComprehensionLoopTarget", new[] { LocationUse.ComprehensionTarget }),
        [typeof(WalrusExpression)] = new("GenerateWalrusExpression", new[] { LocationUse.Walrus }),
    };

    /// <summary>The target arms of the emitter's <c>GenerateStore</c>, each with the uses whose target it is.</summary>
    internal static readonly IReadOnlyDictionary<string, LocationUse[]> TargetArms = new Dictionary<string, LocationUse[]>
    {
        // A module variable's bare name, where the store does not take the assignment's own fast path.
        [nameof(Identifier)] = new[] { LocationUse.NestedTupleLeaf, LocationUse.StarLeaf },
        // A `@static` value's qualified name, and a member of any value.
        [nameof(MemberAccess)] = new[]
        {
            LocationUse.Assign, LocationUse.NestedTupleLeaf, LocationUse.ForTarget, LocationUse.WithTarget, LocationUse.ComprehensionTarget, LocationUse.MemberAssign,
        },
        [nameof(IndexAccess)] = new[] { LocationUse.IndexAssign },
    };

    internal static readonly IReadOnlyDictionary<string, Exclusion> ExcludedTargetArms = new Dictionary<string, Exclusion>
    {
        [nameof(TupleLiteral)] = new("stores nothing itself: its leaves come back to this switch through the unpacking rule, and the leaf uses are their cells"),
    };

    /// <summary>The whole-value forms are the store forms of the binder-site census: each maps to a use or says why not.</summary>
    internal static readonly IReadOnlyDictionary<StoreForm, LocationUse> StoreUses = new Dictionary<StoreForm, LocationUse>
    {
        [StoreForm.Assign] = LocationUse.Assign,
        [StoreForm.Augmented] = LocationUse.Augmented,
        [StoreForm.Walrus] = LocationUse.Walrus,
    };

    internal static readonly IReadOnlyDictionary<StoreForm, Exclusion> ExcludedStoreForms = new Dictionary<StoreForm, Exclusion>
    {
        [StoreForm.OutLet] = new("declares a new local: it stores no module or type value"),
        [StoreForm.LetOrVar] = new("declares a new local: it stores no module or type value"),
    };

    /// <summary>The operand uses are the image of <see cref="ParameterModifier"/>: the value itself, and a member of it.</summary>
    internal static readonly IReadOnlyDictionary<ParameterModifier, (LocationUse Whole, LocationUse Member)> OperandUses =
        new Dictionary<ParameterModifier, (LocationUse, LocationUse)>
        {
            [ParameterModifier.Ref] = (LocationUse.RefOperand, LocationUse.MemberRefOperand),
            [ParameterModifier.Out] = (LocationUse.OutOperand, LocationUse.MemberOutOperand),
            [ParameterModifier.In] = (LocationUse.InOperand, LocationUse.MemberInOperand),
        };

    internal static readonly IReadOnlyDictionary<ParameterModifier, Exclusion> ExcludedOperandModifiers = new Dictionary<ParameterModifier, Exclusion>
    {
        [ParameterModifier.None] = new("the plain argument passes a copy: a read, which the use `Read` is"),
    };

    /// <summary>The uses no store site and no modifier feeds: a read of the value, and a call on it.</summary>
    internal static readonly IReadOnlyList<LocationUse> ValueUses = new[] { LocationUse.Read, LocationUse.MutatingCall };

    private static readonly string[] StoreSeam = { "GenerateStore", "StoreTargetName", "GenerateUnpackingStores" };

    /// <summary>The emitter's store seam, read from its source: the methods outside the seam that call into it, and the
    /// target types <c>GenerateStore</c> switches on.</summary>
    internal static (SortedSet<string> Callers, SortedSet<string> Arms) EmitterStoreSeam()
    {
        var callers = new SortedSet<string>(StringComparer.Ordinal);
        var arms = new SortedSet<string>(StringComparer.Ordinal);
        var solution = PythonInterpreter.FindRepoFile("sharpy.sln") ?? throw new InvalidOperationException("sharpy.sln not found above the test binaries");
        var codeGen = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Sharpy.Compiler", "CodeGen");
        foreach (var file in Directory.EnumerateFiles(codeGen, "RoslynEmitter*.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var name = method.Identifier.Text;
                var callsTheSeam = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(i => i.Expression is IdentifierNameSyntax callee && StoreSeam.Contains(callee.Identifier.Text));
                if (callsTheSeam && !StoreSeam.Contains(name))
                    callers.Add(name);
                if (name != "GenerateStore")
                    continue;
                foreach (var label in method.DescendantNodes().OfType<SwitchStatementSyntax>().SelectMany(s => s.Sections).SelectMany(s => s.Labels))
                {
                    var arm = label switch
                    {
                        CasePatternSwitchLabelSyntax { Pattern: DeclarationPatternSyntax declaration } => declaration.Type.ToString(),
                        CasePatternSwitchLabelSyntax { Pattern: TypePatternSyntax type } => type.Type.ToString(),
                        CasePatternSwitchLabelSyntax { Pattern: ConstantPatternSyntax constant } => constant.Expression.ToString(),
                        CaseSwitchLabelSyntax value => value.Value.ToString(),
                        _ => null,
                    };
                    if (arm != null)
                        arms.Add(arm);
                }
            }
        }
        return (callers, arms);
    }

    /// <summary>The AST's own store sites: every class of the AST namespace with a public <c>Target</c>.</summary>
    internal static IEnumerable<Type> TargetBearingNodes() =>
        typeof(Expression).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == typeof(Expression).Namespace && t.GetProperty("Target") != null);

    // ------------------------------------------------------------------------------------------- //
    // The holders
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The holders: the two kinds of static storage a program stores to, and the two a program reads and passes by <c>in</c>.</summary>
    internal static readonly IReadOnlyList<BindingKind> LocationHolders = new[]
    {
        BindingKind.ModuleVar, BindingKind.StaticField, BindingKind.ModuleComputedConst, BindingKind.ClassComputedConst,
    };

    private const string NoStorage = "a literal const is a C# `const`: it has no storage, and `in` on it is unruled (the operand family's excluded pair carries its program)";

    /// <summary>Lazy: it starts from the operand family's exclusions, which another file of this type declares.</summary>
    private static readonly Lazy<IReadOnlyDictionary<BindingKind, Exclusion>> LazyExcludedHolders = new(() => new Dictionary<BindingKind, Exclusion>(
        ExcludedOperandKinds.Where(e => !LocationHolders.Contains(e.Key)))
    {
        [BindingKind.ModuleLiteralConst] = new(NoStorage),
        [BindingKind.ClassLiteralConst] = new(NoStorage),
        [BindingKind.ModuleProperty] = new(
            "a property hands out a copy of a struct, which is C#'s rule and not this plan's emission: its member store is an internal error today and its mutating call changes the copy (#2268's class)",
            "struct P:\n    x: int\n    def __init__(self, x: int) -> None:\n        self.x = x\nproperty t: P = P(7)\ndef main() -> None:\n    t.x = 5\n    print(t.x)\n",
            "SPY0908 CS1612"),
        [BindingKind.StaticProperty] = new(
            "a property hands out a copy of a struct, as the module property does (#2268's class)",
            "struct P:\n    x: int\n    def __init__(self, x: int) -> None:\n        self.x = x\nclass H:\n    @static\n    property t: P = P(7)\ndef main() -> None:\n    H.t.x = 5\n    print(H.t.x)\n",
            "SPY0908 CS1612"),
        [BindingKind.InstanceField] = new("its storage is the object's: not a value rule N3 initializes"),
        [BindingKind.InstanceProperty] = new("its storage is the object's: not a value rule N3 initializes"),
    });

    internal static IReadOnlyDictionary<BindingKind, Exclusion> ExcludedHolders => LazyExcludedHolders.Value;

    private static bool IsModuleHolder(BindingKind holder) => holder is BindingKind.ModuleVar or BindingKind.ModuleComputedConst;

    private static bool IsConstHolder(BindingKind holder) => holder is BindingKind.ModuleComputedConst or BindingKind.ClassComputedConst;

    /// <summary>Why a holder has no cell in a host form, or null.</summary>
    internal static string? FormExclusion(BindingKind holder, HostForm form) =>
        form == HostForm.Generic && IsModuleHolder(holder) ? "a module has no type parameter" : null;

    // ------------------------------------------------------------------------------------------- //
    // The rule
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A use reads the value before it writes, unless it is a plain store: python binds a plain store's target
    /// without reading it, and an <c>out</c> operand is one by the owner's ruling.</summary>
    internal static bool IsPlainStore(LocationUse use) => use switch
    {
        LocationUse.Assign or LocationUse.Walrus or LocationUse.TupleLeaf or LocationUse.NestedTupleLeaf or LocationUse.StarLeaf
            or LocationUse.ForTarget or LocationUse.WithTarget or LocationUse.ComprehensionTarget or LocationUse.OutOperand => true,
        LocationUse.Read or LocationUse.Augmented or LocationUse.RefOperand or LocationUse.InOperand or LocationUse.MemberAssign
            or LocationUse.MemberAugmented or LocationUse.MutatingCall or LocationUse.IndexAssign or LocationUse.MemberRefOperand
            or LocationUse.MemberOutOperand or LocationUse.MemberInOperand => false,
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    /// <summary>The value kinds a use is a statement on. Total over <see cref="LocationUse"/>.</summary>
    private static ValueKind[] KindsOf(LocationUse use) => use switch
    {
        LocationUse.Read => Enum.GetValues<ValueKind>(),
        LocationUse.Assign => new[] { ValueKind.Int, ValueKind.Struct, ValueKind.Class },
        LocationUse.Augmented or LocationUse.Walrus or LocationUse.TupleLeaf or LocationUse.NestedTupleLeaf or LocationUse.ForTarget
            or LocationUse.WithTarget or LocationUse.ComprehensionTarget or LocationUse.RefOperand or LocationUse.OutOperand
            or LocationUse.InOperand => new[] { ValueKind.Int },
        LocationUse.MemberAssign or LocationUse.MemberAugmented or LocationUse.MutatingCall or LocationUse.MemberRefOperand
            or LocationUse.MemberOutOperand or LocationUse.MemberInOperand => new[] { ValueKind.Struct, ValueKind.Class },
        LocationUse.StarLeaf or LocationUse.IndexAssign => new[] { ValueKind.List },
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    /// <summary>Why a use has no cell on a value kind or a holder, or null: it has one.</summary>
    internal static string? UseExclusion(LocationUse use, ValueKind kind, BindingKind holder)
    {
        if (!KindsOf(use).Contains(kind))
            return "the use is no statement on this kind of value: an int has no member, a struct's whole-value forms are the int's, a list rides the index and the star only";
        if (IsConstHolder(holder) && use is not (LocationUse.Read or LocationUse.InOperand))
            return "a const is not stored to: it is read, and passed by `in`";
        if (IsConstHolder(holder) && kind != ValueKind.Int)
            return "the int cell is the const's";
        if (use == LocationUse.Walrus && holder != BindingKind.ModuleVar)
            return "a walrus target is a bare name, which a `@static` value is not outside its type";
        if (use is LocationUse.ForTarget or LocationUse.WithTarget or LocationUse.ComprehensionTarget && holder == BindingKind.ModuleVar)
            return "a bare name there declares a local of the loop, the block or the comprehension: it stores no module value";
        return null;
    }

    /// <summary>A cell another class's defect or the parser takes today. Its program is the cell's own, as the generator writes it.</summary>
    internal static readonly IReadOnlyDictionary<(LocationUse Use, BindingKind Holder, HostForm Form), (string Reason, string Code)> ExcludedLocationCells =
        new Dictionary<(LocationUse, BindingKind, HostForm), (string, string)>
        {
            [(LocationUse.StarLeaf, BindingKind.StaticField, HostForm.Plain)] = ("the parser takes a bare name as a starred target, and reads a qualified one as a tuple statement", "SPY0106"),
            [(LocationUse.StarLeaf, BindingKind.StaticField, HostForm.Generic)] = ("the parser takes a bare name as a starred target, and reads a qualified one as a tuple statement", "SPY0106"),
            [(LocationUse.Augmented, BindingKind.StaticField, HostForm.Generic)] = ("a compound store through a closed generic type is an internal error wherever it is written (#2435's class): no cell isolates the value", "SPY0908 CS0119"),
            [(LocationUse.ForTarget, BindingKind.StaticField, HostForm.Generic)] = ("a `for` target through a closed generic type is an internal error wherever it is written (#2435's class): no cell isolates the value", "SPY0908 CS0305"),
        };

    private static bool IsOwnInitializer(When when) => when is When.DuringInitializer or When.DuringInitializerThenRead or When.InInitializerText;

    /// <summary>The error of a use of a value while its own initializer is running (rule N3; the owner's Q15 for a store).</summary>
    internal const string CycleError = "NameError";

    /// <summary>A plain store as an expression of the value's own initializer, or null: it is a statement there.</summary>
    private static string? TextForm(LocationUse use, string t) => use switch
    {
        LocationUse.OutOperand => $"fill2(out {t}, 1)",
        LocationUse.ComprehensionTarget => $"len([0 for {t} in [5]])",
        LocationUse.Walrus => $"({t} := 5)",
        LocationUse.Assign or LocationUse.TupleLeaf or LocationUse.NestedTupleLeaf or LocationUse.StarLeaf or LocationUse.ForTarget
            or LocationUse.WithTarget => null,
        _ => throw new ArgumentOutOfRangeException(nameof(use), "not a plain store"),
    };

    /// <summary>The slot of the slot family's map that a plain store's target sits in, where the store is an expression.</summary>
    internal static readonly IReadOnlyDictionary<LocationUse, string> TextSlots = new Dictionary<LocationUse, string>
    {
        [LocationUse.OutOperand] = "ModifiedArgument.Argument",
        [LocationUse.ComprehensionTarget] = "ListComprehension.Clauses:ForClause.Target",
    };

    /// <summary>The cycle graph's rule (D8), read from the slot map: a store in the value's own initializer text is a
    /// compile error when its slot is evaluated whenever the initializer is. A binder's slot is none of the map's: the
    /// store runs once per element, so the compiler cannot say it runs, and the cycle is the run-time error.</summary>
    internal static bool TextStoreIsRefused(LocationUse use) =>
        TextSlots.TryGetValue(use, out var slot) && Slots.TryGetValue(slot, out var entries) && entries.All(e => e.When == Evaluated.Always);

    private const string WalrusBindsALocal =
        "a walrus in a module initializer binds a local of that initializer (`a: int = (t := 5) + 1` beside `t: int = 7` prints `6 7`): it stores no module value";

    /// <summary>A holder a store in the initializer's own text has no cell on, with the program that shows why.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, Exclusion> ExcludedTextHolders = new Dictionary<BindingKind, Exclusion>
    {
        [BindingKind.ModuleVar] = new(
            "a module variable is invisible in its own initializer until rule N1 lands (#2423), which is not this plan's",
            "def fill2(a: out int, by: int) -> int:\n    a = 41 + by\n    return 1\nt: int = fill2(out t, 1)\n" + MainPrintingT,
            "SPY0200"),
    };

    /// <summary>Why a use has no cell at a time, or null.</summary>
    internal static string? WhenExclusion(LocationUse use, BindingKind holder, When when)
    {
        if (when == When.DuringInitializerThenRead && !IsPlainStore(use))
            return "the use reads the value itself: its cell without a read back is already the cycle";
        if (when != When.InInitializerText)
            return null;
        if (!IsPlainStore(use))
            return "a read in the value's own initializer text is the cycle graph's self-loop, which StaticInitializerOrderMatrixTests' self cells hold: the store is what this family adds";
        if (TextForm(use, "t") == null)
            return "a statement: an initializer's text holds an expression";
        if (use == LocationUse.Walrus)
            return WalrusBindsALocal;
        return ExcludedTextHolders.TryGetValue(holder, out var excluded) ? excluded.Reason : null;
    }

    private static string Wrap(ValueKind kind, string e) => kind switch
    {
        ValueKind.Int => e,
        ValueKind.Struct => $"P({e})",
        ValueKind.Class => $"Q({e})",
        ValueKind.List => $"[{e}]",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private const int InitialValue = 7;

    private const int CalleeStore = 42;

    private static string ReadBack(ValueKind kind, string t) => kind switch
    {
        ValueKind.Int => t,
        ValueKind.Struct or ValueKind.Class => $"{t}.x",
        ValueKind.List => $"{t}[0]",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The statement of a use on target <paramref name="t"/> with operand <paramref name="v"/> (worth 1), what the
    /// value holds after it, and what the use's function returns where that is not the value read back.</summary>
    private static (string Statement, int After, (string Text, int Value)? Returned) UseText(LocationUse use, ValueKind kind, string t, string v) => (use, kind) switch
    {
        (LocationUse.Read, _) => ($"r0 = {ReadBack(kind, t)} + {v}", InitialValue, ("r0", InitialValue + 1)),
        (LocationUse.Assign, ValueKind.Int) => ($"{t} = 5 * {v}", 5, null),
        (LocationUse.Assign, ValueKind.Struct) => ($"{t} = P(5 * {v})", 5, null),
        (LocationUse.Assign, ValueKind.Class) => ($"{t} = Q(5 * {v})", 5, null),
        (LocationUse.Augmented, _) => ($"{t} += {v}", InitialValue + 1, null),
        (LocationUse.Walrus, _) => ($"w0 = ({t} := 5 * {v})", 5, null),
        (LocationUse.TupleLeaf, _) => ($"{t}, u0 = 5 * {v}, 6", 5, null),
        (LocationUse.NestedTupleLeaf, _) => ($"({t}, u0), w0 = (5 * {v}, 6), 7", 5, null),
        (LocationUse.StarLeaf, _) => ($"u0, *{t} = [0, 5 * {v}]", 5, null),
        (LocationUse.ForTarget, _) => ($"for {t} in [5 * {v}]:\n    pass", 5, null),
        (LocationUse.WithTarget, _) => ($"with CM0(5 * {v}) as {t}:\n    pass", 5, null),
        (LocationUse.ComprehensionTarget, _) => ($"w0 = [0 for {t} in [5 * {v}]]", 5, null),
        (LocationUse.IndexAssign, _) => ($"{t}[0] = 5 * {v}", 5, null),
        (LocationUse.RefOperand, _) => ($"r0 = grow(ref {t}, {v})", InitialValue + 1, null),
        (LocationUse.OutOperand, _) => ($"r0 = fill2(out {t}, {v})", CalleeStore, null),
        (LocationUse.InOperand, _) => ($"r0 = peek2(in {t}, {v})", InitialValue, ("r0", InitialValue + 1)),
        (LocationUse.MemberAssign, _) => ($"{t}.x = 5 * {v}", 5, null),
        (LocationUse.MemberAugmented, _) => ($"{t}.x += {v}", InitialValue + 1, null),
        (LocationUse.MutatingCall, _) => ($"{t}.add({v})", InitialValue + 1, null),
        (LocationUse.MemberRefOperand, _) => ($"r0 = grow(ref {t}.x, {v})", InitialValue + 1, null),
        (LocationUse.MemberOutOperand, _) => ($"r0 = fill2(out {t}.x, {v})", CalleeStore, null),
        (LocationUse.MemberInOperand, _) => ($"r0 = peek2(in {t}.x, {v})", InitialValue, ("r0", InitialValue + 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    /// <summary>What a cell prints: what the use's function returns (the value read back, unless the use says otherwise),
    /// then the value once its host has been swept, then, on a generic host, the same value of another closed type. That
    /// type is swept when the print reaches it, last: its own initializers run then, the use's function among them, and
    /// its value is its initializer's.</summary>
    internal static string LocationModel(LocationUse use, When when, HostForm form, int after, int? returned)
    {
        var loaded = when == When.BeforeInitializer && IsPlainStore(use) ? InitialValue : after;
        return $"{returned ?? after} {loaded}{(form == HostForm.Generic ? $" {InitialValue}" : "")}";
    }

    private static string LocationId(BindingKind holder, HostForm form, ValueKind kind, LocationUse use, When when) =>
        $"Location/{holder}{(form == HostForm.Plain ? "" : "+" + form)}/{kind}/{use}/{when}";

    private static string LocationProgram(BindingKind holder, HostForm form, ValueKind kind, LocationUse use, When when, bool twin)
    {
        var module = IsModuleHolder(holder);
        var generic = form == HostForm.Generic;
        var t = module ? "t" : generic ? "H[int].t" : "H.t";
        var called = when is When.DuringInitializer or When.DuringInitializerThenRead;
        var inText = when == When.InInitializerText;
        var (type, initial) = kind switch
        {
            ValueKind.Int => ("int", IsConstHolder(holder) ? "seven()" : $"{InitialValue}"),
            ValueKind.Struct => ("P", $"P({InitialValue})"),
            ValueKind.Class => ("Q", $"Q({InitialValue})"),
            ValueKind.List => ("list[int]", $"[{InitialValue}]"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        // Inside its own initializer: the initializer calls the use's function, or holds the use itself.
        if (called)
            initial = Wrap(kind, "use()");
        else if (inText)
            initial = TextForm(use, t) ?? throw new InvalidOperationException($"{use} is no expression");
        var readBack = ReadBack(kind, t);
        var awaited = when == When.ReadyAcrossAwait;
        var (statement, _, returned) = UseText(use, kind, t, awaited ? "await one()" : "1");
        var def = awaited ? "async def" : "def";

        var function = new List<string>();
        if (!inText)
        {
            function.Add($"{def} use() -> int:");
            // Python binds a bare store's name in the function unless it is declared global: the twin says so, Sharpy has no such line.
            if (twin && module)
                function.Add("    global t");
            function.AddRange(statement.Split('\n').Select(l => "    " + l));
            function.Add($"    return {returned?.Text ?? (when == When.DuringInitializer ? "1" : readBack)}");
        }

        var isConst = IsConstHolder(holder);
        var value = $"{(isConst ? "const " : "")}t: {type} = {initial}";
        var declared = when == When.BeforeInitializer ? new[] { (Static: true, Line: "a: int = use()"), (Static: !isConst, Line: value) } : new[] { (Static: !isConst, Line: value) };
        var holderLines = module
            ? declared.Select(d => d.Line).ToList()
            : new[] { generic ? "class H[E]:" : "class H:" }
                .Concat(declared.SelectMany(d => d.Static ? new[] { "    @static", "    " + d.Line } : new[] { "    " + d.Line }))
                .ToList();

        // Sharpy sees a module variable from a function declared after it (rule N1's order, #2423's until P78), and calls a
        // later function from an initializer. Python runs top to bottom: the function is defined before the call.
        var body = new List<string>();
        body.AddRange(twin ? function.Concat(holderLines) : holderLines.Concat(function));
        var other = generic ? ", " + ReadBack(kind, "H[str].t") : "";
        if (when == When.BeforeInitializer)
        {
            body.Add("def main() -> None:");
            body.Add($"    print({(module ? "a" : generic ? "H[int].a" : "H.a")}, {readBack}{other})");
        }
        else if (called || inText)
        {
            // One closed type: the cycle is that type's value's.
            body.Add("def main() -> None:");
            body.Add($"    print({readBack})");
        }
        else
        {
            body.Add($"{def} main() -> None:");
            body.Add($"    r = {(awaited ? "await " : "")}use()");
            body.Add($"    print(r, {readBack}{other})");
        }
        return WithHelpers(Harness.Join(body));
    }

    /// <summary>What python's twin does, and whether it has one. Python has no modifier, the harness's twin no <c>await</c> chain,
    /// a closed generic type is an alias object there, and its <c>__exit__</c> takes three arguments the Sharpy spelling does not
    /// declare. Before the initializer, the module twin is python's own order: a plain store is kept and then overwritten, any
    /// read is a NameError (order-only: under N3 the value is initialized first). A class body's twin cannot name its own class
    /// yet, so a type's value has none there. Inside the value's own initializer the module twin is python's order too: a
    /// read is a NameError, as the model is, and a plain store runs and prints (<see cref="PythonPrints"/>).</summary>
    internal static PythonExpectation LocationPython(BindingKind holder, HostForm form, LocationUse use, When when)
    {
        if (OperandUses.Values.Any(o => o.Whole == use || o.Member == use) || use == LocationUse.WithTarget
            || when == When.ReadyAcrossAwait || form == HostForm.Generic)
            return PythonExpectation.NoTwin;
        if (when == When.Ready)
            return PythonExpectation.Prints;
        if (!IsModuleHolder(holder))
            return PythonExpectation.NoTwin;
        return IsPlainStore(use) ? PythonExpectation.Prints : PythonExpectation.NameError;
    }

    /// <summary>What python prints for a plain store from inside the value's own initializer, where Sharpy's model is the
    /// cycle error: the initializer's value, which is what the use's function returns (1, or the value it stored and read back).</summary>
    internal static string PythonPrints(When when, int after) => when == When.DuringInitializerThenRead ? $"{after}" : "1";

    /// <summary>A read before the initializer sees a storage default today: a wrong value, or a null that throws. A use
    /// inside the value's own initializer runs today on that default, or throws on it. A store in the initializer's own
    /// text that the compiler cannot call certain is refused today by the validator the plan retires.</summary>
    private static Route LocationRoute(LocationUse use, When when) => (verdict, _, _) =>
        when switch
        {
            When.BeforeInitializer when !IsPlainStore(use) => ValueRoute(verdict),
            When.DuringInitializer or When.DuringInitializerThenRead => ValueRoute(verdict),
            When.InInitializerText => verdict == VerdictClass.FalseRefusal && !TextStoreIsRefused(use) ? $"{ReadinessIssue} false-refusal" : null,
            _ => null,
        };

    private static string? ValueRoute(VerdictClass verdict) =>
        verdict == VerdictClass.SilentWrong ? $"{ReadinessIssue} silent-wrong"
        : verdict == VerdictClass.RuntimeThrow ? $"{ReadinessIssue} runtime-throw"
        : null;

    private static IEnumerable<(BindingKind Holder, HostForm Form, ValueKind Kind, LocationUse Use, When When)> LocationProduct() =>
        from holder in LocationHolders
        from form in Enum.GetValues<HostForm>()
        where FormExclusion(holder, form) == null
        from kind in Enum.GetValues<ValueKind>()
        from use in Enum.GetValues<LocationUse>()
        where UseExclusion(use, kind, holder) == null && !ExcludedLocationCells.ContainsKey((use, holder, form))
        from when in Enum.GetValues<When>()
        where WhenExclusion(use, holder, when) == null
        select (holder, form, kind, use, when);

    internal static IEnumerable<Cell> LocationCells()
    {
        foreach (var (holder, form, kind, use, when) in LocationProduct())
        {
            var python = LocationPython(holder, form, use, when);
            var (_, after, returned) = UseText(use, kind, "t", "1");
            var cycle = IsOwnInitializer(when);
            var refused = when == When.InInitializerText && TextStoreIsRefused(use);
            yield return new Cell(
                LocationId(holder, form, kind, use, when),
                Family.Location,
                LocationProgram(holder, form, kind, use, when, twin: false),
                python == PythonExpectation.NoTwin ? null : LocationProgram(holder, form, kind, use, when, twin: true),
                cycle ? CycleError : LocationModel(use, when, form, after, returned?.Value))
            {
                RefusalCode = refused ? SlotCycle : null,
                RetiringRefusal = refused ? DiagnosticCodes.SemanticOverflow.StaticInitializerForwardRead : null,
                Raises = cycle && !refused ? CycleError : null,
                PythonModel = cycle && python == PythonExpectation.Prints ? PythonPrints(when, after) : null,
                // What runs today and must not move: a use of a ready value, a plain store before the initializer (kept, then
                // replaced), and a store the compiler refuses in the value's own initializer text.
                IsControl = when is When.Ready or When.ReadyAcrossAwait || (when == When.BeforeInitializer && IsPlainStore(use)) || refused,
                Python = python,
                Route = LocationRoute(use, when),
            };
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The cross-type forward read
    // ------------------------------------------------------------------------------------------- //

    private const string CrossTypePrefix = "Location/CrossType/";

    private static string CrossTypeId(HostForm reader, HostForm target) => $"{CrossTypePrefix}{reader}>{target}";

    /// <summary>The reader type's first value reads the target type's first value, and the target's second value reads the
    /// reader's first: no value is on a cycle. A sweep of the target that starts inside the reader's initializer runs the
    /// target's second initializer while the reader's value is still being initialized, and raises.</summary>
    private static string CrossTypeProgram(HostForm reader, HostForm target)
    {
        static string Head(string name, HostForm form) => form == HostForm.Generic ? $"class {name}[E]:" : $"class {name}:";
        static string Closed(string name, HostForm form) => form == HostForm.Generic ? $"{name}[int]" : name;
        var (a, b) = (Closed("A", reader), Closed("B", target));
        var body = new[]
        {
            Head("A", reader), "    @static", $"    x: int = {b}.p", "    @static", "    z: int = 5",
            Head("B", target), "    @static", "    p: int = 1", "    @static", $"    q: int = {a}.x + 1",
        };
        return Harness.Join(body) + Harness.MainBlock($"print({a}.x, {a}.z, {b}.p, {b}.q)");
    }

    /// <summary>The cross-type forward read (the first verification's F3, and the owner's Q14 for a generic class), reader
    /// × target <see cref="HostForm"/>: a forward read initializes its target first, whatever hosts the two values, so each
    /// cell prints <c>1 5 1 2</c>. Python's class bodies run where they are written: the twin is an order-only NameError.</summary>
    internal static IEnumerable<Cell> CrossTypeCells() =>
        from reader in Enum.GetValues<HostForm>()
        from target in Enum.GetValues<HostForm>()
        let plain = reader == HostForm.Plain && target == HostForm.Plain
        select new Cell(CrossTypeId(reader, target), Family.Location, CrossTypeProgram(reader, target), plain ? CrossTypeProgram(reader, target) : null, "1 5 1 2")
        {
            Python = plain ? PythonExpectation.NameError : PythonExpectation.NoTwin,
            Route = static (verdict, _, _) => ValueRoute(verdict),
        };

    private static IEnumerable<(string What, Exclusion Exclusion)> LocationExclusions() =>
        ExcludedHolders.Select(e => ($"holder BindingKind.{e.Key}", e.Value))
            .Concat(ExcludedStoreForms.Select(e => ($"StoreForm.{e.Key}", e.Value)))
            .Concat(ExcludedTargetArms.Select(e => ($"target arm {e.Key}", e.Value)))
            .Concat(ExcludedOperandModifiers.Select(e => ($"operand use ParameterModifier.{e.Key}", e.Value)))
            .Concat(ExcludedTextHolders.Select(e => ($"a store in the initializer text of BindingKind.{e.Key}", e.Value)))
            .Concat(ExcludedLocationCells.Select(e => (
                $"location {e.Key.Use} on {e.Key.Holder}+{e.Key.Form}",
                new Exclusion(e.Value.Reason, LocationProgram(e.Key.Holder, e.Key.Form, KindsOf(e.Key.Use)[0], e.Key.Use, When.Ready, twin: false), e.Value.Code))));

    internal static List<string> LocationTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        foreach (var kind in Enum.GetValues<BindingKind>())
        {
            bool generated = LocationHolders.Contains(kind), excluded = ExcludedHolders.ContainsKey(kind);
            if (generated == excluded)
                failures.Add($"holder BindingKind.{kind} is {Both(generated)}");
        }
        foreach (var form in Enum.GetValues<StoreForm>())
        {
            bool generated = StoreUses.ContainsKey(form), excluded = ExcludedStoreForms.ContainsKey(form);
            if (generated == excluded)
                failures.Add($"StoreForm.{form} is {Both(generated)}");
        }
        foreach (var modifier in Enum.GetValues<ParameterModifier>())
        {
            bool generated = OperandUses.ContainsKey(modifier), excluded = ExcludedOperandModifiers.ContainsKey(modifier);
            if (generated == excluded)
                failures.Add($"operand use ParameterModifier.{modifier} is {Both(generated)}");
        }

        // The store sites against the AST, and their emitters and target arms against the emitter's source.
        var nodes = TargetBearingNodes().ToList();
        if (nodes.Count < 4)
            failures.Add($"the scan found {nodes.Count} AST nodes with a Target: it reads nothing");
        foreach (var node in nodes.Where(n => !StoreSites.ContainsKey(n)))
            failures.Add($"AST node {node.Name} carries a Target and is no store site");
        foreach (var node in StoreSites.Keys.Where(n => !nodes.Contains(n)))
            failures.Add($"store site {node.Name} is no AST node with a Target");
        var (callers, arms) = EmitterStoreSeam();
        if (callers.Count < 4 || arms.Count < 3)
            failures.Add($"the census found {callers.Count} callers of the store seam and {arms.Count} target arms: it reads nothing");
        foreach (var caller in callers.Where(c => StoreSites.Values.All(s => s.Emitter != c)))
            failures.Add($"emitter method {caller} calls the store seam and is no store site's");
        foreach (var site in StoreSites.Where(s => !callers.Contains(s.Value.Emitter)))
            failures.Add($"store site {site.Key.Name} names {site.Value.Emitter}, which does not call the store seam");
        foreach (var arm in arms)
        {
            bool generated = TargetArms.ContainsKey(arm), excluded = ExcludedTargetArms.ContainsKey(arm);
            if (generated == excluded)
                failures.Add($"target arm {arm} is {Both(generated)}");
        }
        foreach (var arm in TargetArms.Keys.Concat(ExcludedTargetArms.Keys).Where(a => !arms.Contains(a)))
            failures.Add($"{arm} is no target arm of GenerateStore");

        // Every use has a source, and every use a source names is a use with a cell.
        var cells = LocationProduct().ToList();
        var fed = StoreSites.Values.SelectMany(s => s.Uses)
            .Concat(StoreUses.Values)
            .Concat(OperandUses.Values.SelectMany(o => new[] { o.Whole, o.Member }))
            .Concat(ValueUses)
            .ToHashSet();
        foreach (var use in Enum.GetValues<LocationUse>().Where(u => !fed.Contains(u)))
            failures.Add($"LocationUse.{use} is fed by no source");
        foreach (var use in TargetArms.Values.SelectMany(u => u).Concat(fed).Distinct().Where(u => cells.All(c => c.Use != u)))
            failures.Add($"LocationUse.{use} has no cell");
        foreach (var site in StoreSites.Where(s => s.Value.Uses.Length == 0))
            failures.Add($"store site {site.Key.Name} has no use");
        foreach (var arm in TargetArms.Where(a => a.Value.Length == 0))
            failures.Add($"target arm {arm.Key} has no use");

        foreach (var kind in Enum.GetValues<ValueKind>().Where(k => cells.All(c => c.Kind != k)))
            failures.Add($"ValueKind.{kind} has no cell");
        foreach (var when in Enum.GetValues<When>().Where(w => cells.All(c => c.When != w)))
            failures.Add($"When.{when} has no cell");
        foreach (var holder in LocationHolders)
        {
            foreach (var form in Enum.GetValues<HostForm>())
            {
                if (FormExclusion(holder, form) == null && cells.All(c => c.Holder != holder || c.Form != form))
                    failures.Add($"holder BindingKind.{holder} has no cell in HostForm.{form}");
                if (FormExclusion(holder, form) != null)
                    continue;
                // A host's own falsifiers: a read before the initializer, which a missing state check answers with a default,
                // and a read inside it, which is the cycle.
                foreach (var when in new[] { When.BeforeInitializer, When.DuringInitializer }.Where(w => !cells.Contains((holder, form, ValueKind.Int, LocationUse.Read, w))))
                    failures.Add($"holder BindingKind.{holder} in HostForm.{form} has no read at When.{when}");
                if (IsConstHolder(holder))
                    continue;
                // A stored holder's: a plain store before the initializer, which an unswept host answers with the stored value
                // (the owner's Q14), and one inside it, alone and read back (Q15).
                foreach (var when in new[] { When.BeforeInitializer, When.DuringInitializer, When.DuringInitializerThenRead }
                    .Where(w => !cells.Contains((holder, form, ValueKind.Int, LocationUse.Assign, w))))
                    failures.Add($"holder BindingKind.{holder} in HostForm.{form} has no plain store at When.{when}");
            }
            if (!IsModuleHolder(holder) && FormExclusion(holder, HostForm.Generic) != null)
                failures.Add($"holder BindingKind.{holder} is a type's value and has no generic host");
        }
        foreach (var form in Enum.GetValues<HostForm>().Where(f => cells.All(c => c.Form != f)))
            failures.Add($"HostForm.{form} has no cell");
        foreach (var excluded in ExcludedLocationCells.Keys)
        {
            if (FormExclusion(excluded.Holder, excluded.Form) != null || KindsOf(excluded.Use).All(k => UseExclusion(excluded.Use, k, excluded.Holder) != null))
                failures.Add($"location {excluded.Use} on {excluded.Holder}+{excluded.Form} is excluded twice");
        }
        foreach (var use in Enum.GetValues<LocationUse>())
        {
            // Every use reads or does not read by rule, and is a statement on every kind it names.
            var plain = IsPlainStore(use);
            foreach (var kind in KindsOf(use))
                UseText(use, kind, "t", "1");
            // Inside its own initializer every use has its cell, and every plain store its second form.
            foreach (var when in new[] { When.BeforeInitializer, When.DuringInitializer }.Where(w => cells.All(c => c.Use != use || c.When != w)))
                failures.Add($"LocationUse.{use} has no cell at When.{when}");
            if (plain && cells.All(c => c.Use != use || c.When != When.DuringInitializerThenRead))
                failures.Add($"LocationUse.{use} is a plain store and has no cell that reads it back inside its own initializer");
            // A plain store that is an expression has its slot, which the slot map classifies or excludes.
            if (!plain || TextForm(use, "t") == null || use == LocationUse.Walrus)
                continue;
            if (!TextSlots.TryGetValue(use, out var slot))
                failures.Add($"LocationUse.{use} is a store an initializer's text can hold and names no slot");
            else if (!Slots.ContainsKey(slot) && !ExcludedSlots.ContainsKey(slot))
                failures.Add($"LocationUse.{use} names the slot {slot}, which the slot map neither classifies nor excludes");
            if (cells.All(c => c.Use != use || c.When != When.InInitializerText))
                failures.Add($"LocationUse.{use} has no cell in its own initializer text");
        }
        foreach (var use in TextSlots.Keys.Where(u => !IsPlainStore(u) || TextForm(u, "t") == null))
            failures.Add($"LocationUse.{use} names a slot and is no store an initializer's text can hold");
        // The text rule's two answers both have a cell.
        var inText = cells.Where(c => c.When == When.InInitializerText).Select(c => TextStoreIsRefused(c.Use)).ToHashSet();
        foreach (var answer in new[] { true, false }.Where(a => !inText.Contains(a)))
            failures.Add($"no store in an initializer's own text is {(answer ? "refused" : "left to run time")}");
        foreach (var holder in ExcludedTextHolders.Keys.Where(h => !LocationHolders.Contains(h) || IsConstHolder(h)))
            failures.Add($"BindingKind.{holder} is excluded from the initializer text and is no stored holder");

        // The cross-type read: every pair of host forms.
        var crossOwed = (from reader in Enum.GetValues<HostForm>() from target in Enum.GetValues<HostForm>() select CrossTypeId(reader, target)).ToHashSet(StringComparer.Ordinal);
        var crossHeld = pool.Where(c => c.Id.StartsWith(CrossTypePrefix, StringComparison.Ordinal)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in crossOwed.Where(id => !crossHeld.Contains(id)))
            failures.Add($"the host forms owe {id} and the pool does not hold it");
        foreach (var id in crossHeld.Where(id => !crossOwed.Contains(id)))
            failures.Add($"the pool holds {id} and the host forms do not owe it");

        var ids = pool.Where(c => c.Family == Family.Location && !c.Id.StartsWith(CrossTypePrefix, StringComparison.Ordinal)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = cells.Select(k => LocationId(k.Holder, k.Form, k.Kind, k.Use, k.When)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)).Take(20))
            failures.Add($"the location axes owe {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)).Take(20))
            failures.Add($"the pool holds {id} and the location axes do not owe it");
        return failures;
    }
}

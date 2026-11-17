using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// and then replaced there too (<c>5 7</c>).
///
/// Inside a value's OWN initializer the rule is the owner's of 2026-11-16 (#2422 comment 6930736624, Q21, which
/// supersedes Q15 where they differ): do what C# does unless N3 forbids it. A plain store gives the value a value, so
/// it is allowed, a read after it returns what was stored, and the initializer's result then replaces it: the cell
/// prints what it prints today and in python. A use that reads the value before it was ever given one, a store that
/// reads first among them, is the cycle: a <c>NameError</c>, never a default (<see cref="OwnInitializerRaises"/>).
///
/// The fifth stage-0 commit carries the rulings of 2026-11-16 that followed the third verification (#2422 comments
/// 6930063803 and 6930130729, Q17 and Q18, "Sweep at once" and "go with option 1, C# order"): every host, a module, a
/// class or a closed generic type, sweeps as a unit the first time it is required. <see cref="CrossCells"/> are a use
/// from one host's initializer of another host's value: the use (<see cref="LocationUse"/>) × the two hosts
/// (<see cref="CrossHosts"/>) × which is declared first (<see cref="CrossOrder"/>) × whether the target host reads the
/// user's value back. Their one rule is <see cref="CrossModel"/>.
///
/// The sixth stage-0 commit carries the ruling that followed the fourth verification (#2422 comment 6932647936, Q24, "C#
/// storage, as today"): an <c>out</c> operand naming a value that is ready IS the storage, as in C#, and the temporary
/// stored when the call returns stays where the value has none yet (Q12). The cell that reads the value after the call
/// prints the same under both, so <see cref="OutObservationCells"/> observe the store while the call runs and after
/// the callee raises (<see cref="OutObservation"/>), on every cell of the two products above whose use is an operand
/// that stores and whose value is ready when the use runs. Each carries what the rejected emission would print
/// (<see cref="Cell.Rejected"/>), and the sweep is red where Sharpy prints it.
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

    private const string TupleArmStoresNothing = "stores nothing itself: its leaves come back to this switch through the unpacking rule, and the leaf uses are their cells";

    internal static readonly IReadOnlyDictionary<string, Exclusion> ExcludedTargetArms = new Dictionary<string, Exclusion>
    {
        [nameof(TupleLiteral)] = new(TupleArmStoresNothing),
    };

    /// <summary>The whole-value forms are the store forms of the binder-site census: each maps to a use or says why not.</summary>
    internal static readonly IReadOnlyDictionary<StoreForm, LocationUse> StoreUses = new Dictionary<StoreForm, LocationUse>
    {
        [StoreForm.Assign] = LocationUse.Assign,
        [StoreForm.Augmented] = LocationUse.Augmented,
        [StoreForm.Walrus] = LocationUse.Walrus,
    };

    private const string DeclaresALocal = "declares a new local, beside a module value of its name too: it stores no module or type value";

    internal static readonly IReadOnlyDictionary<StoreForm, Exclusion> ExcludedStoreForms = new Dictionary<StoreForm, Exclusion>
    {
        [StoreForm.OutLet] = new(
            DeclaresALocal,
            "def fill(a: out int) -> int:\n    a = 42\n    return 1\nt: int = 7\ndef use() -> int:\n    r0 = fill(out let t)\n    return t\ndef main() -> None:\n    print(use(), t)\n")
        { Prints = "42 7" },
        [StoreForm.LetOrVar] = new(
            DeclaresALocal,
            "t: int = 7\ndef use() -> int:\n    let t = 5\n    return t\ndef use2() -> int:\n    t: int = 6\n    return t\ndef main() -> None:\n    print(use(), use2(), t)\n")
        { Prints = "5 6 7" },
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
        [ParameterModifier.None] = new(
            "the plain argument passes a copy, which the callee's store does not reach: a read, which the use `Read` is",
            "def set9(a: int) -> int:\n    a = 9\n    return a\nt: int = 7\ndef main() -> None:\n    print(set9(t), t)\n")
        { Prints = "9 7" },
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

    private const string NoStorage = "a literal const is a C# `const`: it has no storage, and `in` on it is unruled, an internal error today (the operand family's excluded pair)";

    private const string ObjectStorage = "its storage is the object's: not a value rule N3 initializes";

    /// <summary>Lazy: it starts from the operand family's exclusions, which another file of this type declares.</summary>
    private static readonly Lazy<IReadOnlyDictionary<BindingKind, Exclusion>> LazyExcludedHolders = new(() => new Dictionary<BindingKind, Exclusion>(
        ExcludedOperandKinds.Where(e => !LocationHolders.Contains(e.Key)))
    {
        [BindingKind.ModuleLiteralConst] = new(
            NoStorage,
            "const t: int = 3\ndef peek(a: in int) -> int:\n    return a\ndef main() -> None:\n    print(peek(in t))\n",
            "SPY0908 CS8156"),
        [BindingKind.ClassLiteralConst] = new(
            NoStorage,
            "class H:\n    const t: int = 3\ndef peek(a: in int) -> int:\n    return a\ndef main() -> None:\n    print(peek(in H.t))\n",
            "SPY0908 CS8156"),
        [BindingKind.ModuleProperty] = new(
            "a property hands out a copy of a struct, which is C#'s rule and not this plan's emission: its member store is an internal error today and its mutating call changes the copy (#2268's class)",
            "struct P:\n    x: int\n    def __init__(self, x: int) -> None:\n        self.x = x\nproperty t: P = P(7)\ndef main() -> None:\n    t.x = 5\n    print(t.x)\n",
            "SPY0908 CS1612"),
        [BindingKind.StaticProperty] = new(
            "a property hands out a copy of a struct, as the module property does (#2268's class)",
            "struct P:\n    x: int\n    def __init__(self, x: int) -> None:\n        self.x = x\nclass H:\n    @static\n    property t: P = P(7)\ndef main() -> None:\n    H.t.x = 5\n    print(H.t.x)\n",
            "SPY0908 CS1612"),
        [BindingKind.InstanceField] = new(ObjectStorage),
        [BindingKind.InstanceProperty] = new(ObjectStorage),
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
            return WalrusTargetIsABareName;
        if (use is LocationUse.ForTarget or LocationUse.WithTarget or LocationUse.ComprehensionTarget && holder == BindingKind.ModuleVar)
            return BareNameDeclaresALocal;
        return null;
    }

    private const string WalrusTargetIsABareName = "a walrus target is a bare name, which a `@static` value is not outside its type";

    private const string BareNameDeclaresALocal = "a bare name there declares a local of the loop, the block or the comprehension: it stores no module value";

    /// <summary>The reasons of <see cref="UseExclusion"/> that say what the compiler does with the use, each with what
    /// the excluded cell's own program does: the code it draws, or what it prints when the store misses the value
    /// (the use's function returns the value read back, then <c>main</c> reads it).</summary>
    private static readonly IReadOnlyDictionary<string, (string? Code, string? Prints)> UseExclusionProbes = new Dictionary<string, (string?, string?)>
    {
        [WalrusTargetIsABareName] = ("SPY0143", null),
        [BareNameDeclaresALocal] = (null, $"{InitialValue} {InitialValue}"),
    };

    /// <summary>Every use and holder <see cref="UseExclusion"/> leaves out for a reason with a probe, as the program the
    /// generator would write for the cell: an exclusion widened to a use that does store is red.</summary>
    private static IEnumerable<(string What, Exclusion Exclusion)> UseExclusions()
    {
        foreach (var use in Enum.GetValues<LocationUse>())
            foreach (var holder in LocationHolders)
            {
                if (UseExclusion(use, ValueKind.Int, holder) is { } reason && UseExclusionProbes.TryGetValue(reason, out var probe))
                {
                    var program = LocationProgram(holder, HostForm.Plain, ValueKind.Int, use, When.Ready, twin: false);
                    yield return ($"location {use} on {holder}", new Exclusion(reason, program, probe.Code) { Prints = probe.Prints });
                }
            }
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

    /// <summary>The error of a read of a value that has no value yet while its own initializer is running (rule N3).</summary>
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

    /// <summary>The rule inside a value's own initializer (the owner's Q21). A plain store reads nothing: the value has
    /// been given a value, and the rest of the initializer may read it. Any other use reads the value before it has one,
    /// and that is the cycle.</summary>
    internal static bool OwnInitializerRaises(LocationUse use) => !IsPlainStore(use);

    /// <summary>What a plain store from inside the value's own initializer leaves: the initializer's result, which replaces
    /// what was stored. That is what the use's function returns (1, or the stored value where it reads it back), or what
    /// the store's expression is worth in the initializer's own text (1).</summary>
    internal static string OwnInitializerModel(When when, int after) => when == When.DuringInitializerThenRead ? $"{after}" : "1";

    private const string WalrusBindsALocal =
        "a walrus in a module initializer binds a local of that initializer (`a: int = (t := 5) + 1` beside `t: int = 7` prints `6 7`): it stores no module value";

    private static string RootName(string read) => read.Split('.', '[')[0];

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

    private static string LocationProgram(BindingKind holder, HostForm form, ValueKind kind, LocationUse use, When when, bool twin, OutObservation? observed = null)
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
        var operand = awaited ? "await one()" : "1";
        var (statement, _, returned) = observed is { } how ? ObservedText(how, t, operand) : UseText(use, kind, t, operand);
        var def = awaited ? "async def" : "def";

        var function = new List<string>();
        if (observed is { } callee)
            function.AddRange(ObservedCallee(callee, readBack));
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
    /// read is a NameError and a plain store runs and prints the initializer's result, as the model has both.</summary>
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

    /// <summary>A read before the initializer sees a storage default today: a wrong value, or a null that throws. A use
    /// that reads the value inside its own initializer runs today on that default, or throws on it. A store in the
    /// initializer's own text is refused today by the validator the plan retires.</summary>
    private static Route LocationRoute(LocationUse use, When when) => (verdict, _, _) =>
        when switch
        {
            When.BeforeInitializer when !IsPlainStore(use) => ValueRoute(verdict),
            When.DuringInitializer or When.DuringInitializerThenRead => ValueRoute(verdict),
            When.InInitializerText => verdict == VerdictClass.FalseRefusal ? $"{ReadinessIssue} false-refusal" : null,
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
            var own = IsOwnInitializer(when);
            var raises = own && OwnInitializerRaises(use);
            yield return new Cell(
                LocationId(holder, form, kind, use, when),
                Family.Location,
                LocationProgram(holder, form, kind, use, when, twin: false),
                python == PythonExpectation.NoTwin ? null : LocationProgram(holder, form, kind, use, when, twin: true),
                raises ? CycleError : own ? OwnInitializerModel(when, after) : LocationModel(use, when, form, after, returned?.Value))
            {
                Raises = raises ? CycleError : null,
                // What runs today and must not move: a use of a ready value, and a plain store before the value's initializer
                // (kept, then replaced) or from a function that initializer calls (replaced by its result).
                IsControl = when is When.Ready or When.ReadyAcrossAwait
                    || (IsPlainStore(use) && (when is When.BeforeInitializer or When.DuringInitializer or When.DuringInitializerThenRead)),
                Python = python,
                // The twin's value is a module name: python reaches it before its line has run.
                NameErrorOn = python == PythonExpectation.NameError ? "t" : null,
                Route = LocationRoute(use, when),
            };
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // Two hosts: a use, from one host's initializer, of another host's value
    // ------------------------------------------------------------------------------------------- //

    private const string CrossPrefix = "Location/Cross/";

    /// <summary>Which of the two hosts is declared first. A closed literal set.</summary>
    internal enum CrossOrder
    {
        UserFirst,
        TargetFirst,
    }

    /// <summary>One host of a pair: a stored holder in a host form. A module, a plain class or a generic class.</summary>
    internal sealed record CrossHost(BindingKind Holder, HostForm Form)
    {
        public bool IsModule => IsModuleHolder(Holder);

        public bool IsGeneric => Form == HostForm.Generic;

        public string Key => IsModule ? "Module" : Form.ToString();
    }

    private const string ConstTakesNoStore =
        "a const takes no store, and its read from another host's initializer is the `Read` cell of a stored holder in the same host form: the two-host rule is which host sweeps when, and names no holder kind";

    /// <summary>A holder of the location family that is no host of a pair, with the code its store draws: the program is
    /// the location family's own, a plain store to the holder (<see cref="CrossExclusions"/>).</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, (string Reason, string Code)> ExcludedCrossHolders =
        new Dictionary<BindingKind, (string, string)>
        {
            [BindingKind.ModuleComputedConst] = (ConstTakesNoStore + "; the store is refused", "SPY0225"),
            [BindingKind.ClassComputedConst] = (
                ConstTakesNoStore + "; the store through its qualified name is an internal error today, where the checker is to refuse it (#2435's class)",
                "SPY0908 CS0198"),
        };

    /// <summary>The hosts: every holder of the location family that is not excluded, in every host form it has.</summary>
    internal static IEnumerable<CrossHost> CrossHosts() =>
        from holder in LocationHolders
        where !ExcludedCrossHolders.ContainsKey(holder)
        from form in Enum.GetValues<HostForm>()
        where FormExclusion(holder, form) == null
        select new CrossHost(holder, form);

    private const string OneHost = "one host: a module's use of its own value is the location family's cell";

    /// <summary>Why a pair of hosts has no cell, or null. Two classes are two hosts; a module is one.</summary>
    internal static string? PairExclusion(CrossHost user, CrossHost target) => user.IsModule && target.IsModule ? OneHost : null;

    private const string LaterModuleVariable =
        "the class reads back a module variable declared after it, which is invisible until rule N1 lands (#2423): not this plan's";

    /// <summary>The module's sweep reaches a module value and a plain class at its place. A closed generic type has none:
    /// its first use is the only thing that sweeps it.</summary>
    private static bool HasPosition(CrossHost host) => !host.IsGeneric;

    /// <summary>The rule's first half (D5; the owner's Q17 and Q18). The use is the first thing to require the target
    /// class, so the class sweeps there, as a unit: every initializer of it runs inside the user's. A closed generic type
    /// is first required by the use whichever is declared first (a generic user's own first use is <c>main</c>'s read of
    /// its value, which the cell prints first). A plain class is, when the user's initializer runs before the module's
    /// sweep reaches the class: the user has a place in that sweep, and is declared first.</summary>
    internal static bool UseStartsTheTargetSweep(CrossHost user, CrossHost target, CrossOrder order) =>
        !target.IsModule && (target.IsGeneric || (HasPosition(user) && order == CrossOrder.UserFirst));

    /// <summary>The rule's second half (the owner's Q14, which stands inside one host). The use runs inside the target
    /// host's OWN sweep, before the target value's place: a module's sweep reaches a plain class declared above the value.
    /// A plain store is kept and the initializer replaces it; any other use initializes the value first.</summary>
    internal static bool UseRunsInsideTheTargetSweep(CrossHost user, CrossHost target, CrossOrder order) =>
        target.IsModule && HasPosition(user) && order == CrossOrder.UserFirst;

    /// <summary>What a cell prints, or null: it raises. With a read back, the target host's second value reads the user's
    /// value: inside a sweep the use started, that value is still being initialized, and the read is the cycle error (the
    /// residue the owner accepted at Q18). Otherwise the user's value is what the use's function returns, the target value
    /// is what the use left, or its initializer's where a kept store was replaced, and the read back is one more.</summary>
    internal static string? CrossModel(CrossHost user, CrossHost target, CrossOrder order, LocationUse use, bool readBack)
    {
        if (readBack && UseStartsTheTargetSweep(user, target, order))
            return null;
        var (_, after, returned) = UseText(use, ValueKind.Int, "p", "1");
        var first = returned?.Value ?? after;
        var loaded = UseRunsInsideTheTargetSweep(user, target, order) && IsPlainStore(use) ? InitialValue : after;
        return $"{first} {loaded}{(readBack ? $" {first + 1}" : "")}";
    }

    /// <summary>Why a pair's cell does not exist, or null.</summary>
    internal static string? CrossExclusion(CrossHost user, CrossHost target, CrossOrder order, LocationUse use, bool readBack)
    {
        if (PairExclusion(user, target) is { } pair)
            return pair;
        if (UseExclusion(use, ValueKind.Int, target.Holder) is { } notAUse)
            return notAUse;
        if (ExcludedLocationCells.TryGetValue((use, target.Holder, target.Form), out var excluded))
            return excluded.Reason;
        return user.IsModule && order == CrossOrder.TargetFirst && readBack ? LaterModuleVariable : null;
    }

    private static string CrossId(CrossHost user, CrossHost target, CrossOrder order, LocationUse use, bool readBack) =>
        $"{CrossPrefix}{user.Key}>{target.Key}/{order}/{use}{(readBack ? "/ReadBack" : "")}";

    private static List<string> CrossDeclaration(string name, CrossHost host, params (string Name, string Initializer)[] values)
    {
        if (host.IsModule)
            return values.Select(v => $"{v.Name}: int = {v.Initializer}").ToList();
        var lines = new List<string> { host.IsGeneric ? $"class {name}[E]:" : $"class {name}:" };
        foreach (var (value, initializer) in values)
        {
            lines.Add("    @static");
            lines.Add($"    {value}: int = {initializer}");
        }
        return lines;
    }

    private static string CrossName(string type, CrossHost host, string value) =>
        host.IsModule ? value : host.IsGeneric ? $"{type}[int].{value}" : $"{type}.{value}";

    /// <summary>The user host's value <c>x</c> is initialized by a function that uses the target host's value <c>p</c>;
    /// with a read back, the target host's second value <c>q</c> reads <c>x</c>. No value is on a cycle.</summary>
    private static string CrossProgram(CrossHost user, CrossHost target, CrossOrder order, LocationUse use, bool readBack, bool twin, OutObservation? observed = null)
    {
        var (x, p, q) = (CrossName("A", user, "x"), CrossName("B", target, "p"), CrossName("B", target, "q"));
        var (statement, _, returned) = observed is { } how ? ObservedText(how, p, "1") : UseText(use, ValueKind.Int, p, "1");
        var userLines = CrossDeclaration("A", user, ("x", "use()"));
        var targetLines = readBack
            ? CrossDeclaration("B", target, ("p", $"{InitialValue}"), ("q", $"{x} + 1"))
            : CrossDeclaration("B", target, ("p", $"{InitialValue}"));
        var function = new List<string>();
        if (observed is { } callee)
            function.AddRange(ObservedCallee(callee, p));
        function.Add("def use() -> int:");
        // Python binds a bare store's name in the function unless it is declared global, as the location family's twin says.
        if (twin && target.IsModule)
            function.Add("    global p");
        function.AddRange(statement.Split('\n').Select(l => "    " + l));
        function.Add($"    return {returned?.Text ?? p}");

        var hosts = order == CrossOrder.UserFirst ? userLines.Concat(targetLines) : targetLines.Concat(userLines);
        var body = new List<string>(twin ? function.Concat(hosts) : hosts.Concat(function));
        return WithHelpers(Harness.Join(body) + Harness.MainBlock($"print({x}, {p}{(readBack ? ", " + q : "")})"));
    }

    /// <summary>What python's twin does, where it has one: no generic host, and a use python can spell
    /// (<see cref="LocationPython"/>). Python runs top to bottom. With the user first, its function runs before the target
    /// host exists: a plain store to a module name binds it and the module's own line then replaces it, as the model says;
    /// anything else is an order-only NameError. With the target first, a read back names a user that does not exist yet.</summary>
    internal static PythonExpectation CrossPython(CrossHost user, CrossHost target, CrossOrder order, LocationUse use, bool readBack)
    {
        if (user.IsGeneric || target.IsGeneric || LocationPython(target.Holder, HostForm.Plain, use, When.Ready) == PythonExpectation.NoTwin)
            return PythonExpectation.NoTwin;
        if (order == CrossOrder.UserFirst)
            return target.IsModule && IsPlainStore(use) ? PythonExpectation.Prints : PythonExpectation.NameError;
        return readBack ? PythonExpectation.NameError : PythonExpectation.Prints;
    }

    private static IEnumerable<(CrossHost User, CrossHost Target, CrossOrder Order, LocationUse Use, bool ReadBack)> CrossProduct() =>
        from user in CrossHosts()
        from target in CrossHosts()
        from order in Enum.GetValues<CrossOrder>()
        from use in Enum.GetValues<LocationUse>()
        from readBack in new[] { false, true }
        where CrossExclusion(user, target, order, use, readBack) == null
        select (user, target, order, use, readBack);

    internal static IEnumerable<Cell> CrossCells()
    {
        foreach (var (user, target, order, use, readBack) in CrossProduct())
        {
            var python = CrossPython(user, target, order, use, readBack);
            var model = CrossModel(user, target, order, use, readBack);
            yield return new Cell(
                CrossId(user, target, order, use, readBack),
                Family.Location,
                CrossProgram(user, target, order, use, readBack, twin: false),
                python == PythonExpectation.NoTwin ? null : CrossProgram(user, target, order, use, readBack, twin: true),
                model ?? CycleError)
            {
                Raises = model == null ? CycleError : null,
                // What runs today and must not move: a use that reads nothing back, of a value that is ready when it runs or
                // is made ready by it. A store into a later class lands on the class's initialized value, as C# has it.
                IsControl = !readBack && !UseRunsInsideTheTargetSweep(user, target, order),
                Python = python,
                // With the user first its function runs before the target host exists; with the target first, the read back names a user that does not.
                NameErrorOn = python != PythonExpectation.NameError ? null : RootName(order == CrossOrder.UserFirst ? CrossName("B", target, "p") : CrossName("A", user, "x")),
                Route = static (verdict, _, _) => ValueRoute(verdict),
            };
        }
    }

    /// <summary>The read back of a later module variable, on both class forms, and a plain store to each holder that is
    /// no host: the programs the exclusions rest on.</summary>
    private static IEnumerable<(string What, Exclusion Exclusion)> CrossExclusions() =>
        (from user in CrossHosts()
         where user.IsModule
         from target in CrossHosts()
         where !target.IsModule
         select (
             $"cross {user.Key}>{target.Key} read back, target first",
             new Exclusion(LaterModuleVariable, CrossProgram(user, target, CrossOrder.TargetFirst, LocationUse.Assign, readBack: true, twin: false), "SPY0200")))
        .Concat(ExcludedCrossHolders.Select(e => (
            $"cross host BindingKind.{e.Key}",
            new Exclusion(e.Value.Reason, LocationProgram(e.Key, HostForm.Plain, ValueKind.Int, LocationUse.Assign, When.Ready, twin: false), e.Value.Code))));

    internal static List<string> CrossTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        var hosts = CrossHosts().ToList();
        var cells = CrossProduct().ToList();

        // The hosts are the location family's holders in their host forms, each a host or excluded with its program, once
        // per holder and form; and every pair is generated or is one host.
        foreach (var (holder, form) in LocationProduct().Select(c => (c.Holder, c.Form)).Distinct())
        {
            bool generated = hosts.Contains(new CrossHost(holder, form)), excluded = ExcludedCrossHolders.ContainsKey(holder);
            if (generated == excluded)
                failures.Add($"holder BindingKind.{holder} in HostForm.{form} has location cells and as a cross host is {Both(generated)}");
        }
        foreach (var holder in ExcludedCrossHolders.Keys.Where(h => !LocationHolders.Contains(h)))
            failures.Add($"BindingKind.{holder} is excluded as a cross host and is no holder of the location family");
        foreach (var user in hosts)
            foreach (var target in hosts)
            {
                bool generated = cells.Any(c => c.User == user && c.Target == target), excluded = PairExclusion(user, target) != null;
                if (generated == excluded)
                    failures.Add($"the host pair {user.Key}>{target.Key} is {Both(generated)}");
                if (excluded)
                    continue;
                // The use axis is the location family's: every use with a cell on the target's holder and form has its cells
                // here, in both orders, with and without a read back, but for the one exclusion that carries its program.
                var uses = LocationProduct().Where(c => c.Holder == target.Holder && c.Form == target.Form && c.Kind == ValueKind.Int).Select(c => c.Use).Distinct().ToList();
                if (uses.Count(IsPlainStore) == 0 || uses.Count(u => !IsPlainStore(u)) == 0)
                    failures.Add($"the host pair {user.Key}>{target.Key} has no plain store, or nothing else");
                foreach (var use in uses)
                    foreach (var order in Enum.GetValues<CrossOrder>())
                        foreach (var readBack in new[] { false, true })
                        {
                            var owedHere = !(user.IsModule && order == CrossOrder.TargetFirst && readBack);
                            if (owedHere != cells.Contains((user, target, order, use, readBack)))
                                failures.Add($"{CrossId(user, target, order, use, readBack)} is {(owedHere ? "owed by the location family's uses and not generated" : "generated against its exclusion")}");
                        }
            }

        // The rule's answers each have a cell, on each side of the plain-store line.
        foreach (var plain in new[] { true, false })
        {
            var kind = plain ? "a plain store" : "a use that reads";
            if (!cells.Any(c => IsPlainStore(c.Use) == plain && c.ReadBack && UseStartsTheTargetSweep(c.User, c.Target, c.Order)))
                failures.Add($"no cell reads back inside a sweep that {kind} started");
            if (!cells.Any(c => IsPlainStore(c.Use) == plain && c.ReadBack && !UseStartsTheTargetSweep(c.User, c.Target, c.Order)))
                failures.Add($"no cell reads back {kind} that started no sweep");
            if (!cells.Any(c => IsPlainStore(c.Use) == plain && UseRunsInsideTheTargetSweep(c.User, c.Target, c.Order)))
                failures.Add($"no cell holds {kind} inside the target host's own sweep");
            // A store into a class declared later, and into a closed generic type declared earlier (the third verification's F10).
            foreach (var target in hosts.Where(h => !h.IsModule))
                foreach (var order in Enum.GetValues<CrossOrder>())
                {
                    if (!cells.Any(c => IsPlainStore(c.Use) == plain && c.Target == target && c.Order == order && !c.ReadBack && HasPosition(c.User)))
                        failures.Add($"no cell holds {kind} from a host with a place into a {target.Key} class, {order}");
                }
        }
        foreach (var readBack in new[] { false, true }.Where(r => cells.All(c => c.ReadBack != r)))
            failures.Add($"no cross cell {(readBack ? "reads back" : "is without a read back")}");
        foreach (var order in Enum.GetValues<CrossOrder>().Where(o => cells.All(c => c.Order != o)))
            failures.Add($"CrossOrder.{order} has no cell");

        // What the axes owe, written again from the enumerations and the exclusion predicates. A control reads nothing
        // back and uses a value that is ready or that the use makes ready; an observed operand is one always.
        var owed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var user in hosts)
            foreach (var target in hosts)
                foreach (var order in Enum.GetValues<CrossOrder>())
                    foreach (var use in Enum.GetValues<LocationUse>())
                        foreach (var readBack in new[] { false, true }.Where(r => CrossExclusion(user, target, order, use, r) == null))
                        {
                            var id = CrossId(user, target, order, use, readBack);
                            owed[id] = !readBack && !UseRunsInsideTheTargetSweep(user, target, order);
                            if (!ObservedUses().Contains(use) || ObservedCrossExclusion(user, target, order, readBack) != null)
                                continue;
                            foreach (var observed in Enum.GetValues<OutObservation>())
                                owed[ObservedId(id, observed)] = true;
                        }
        failures.AddRange(PoolAgainstOwed("the cross axes", owed, pool.Where(c => c.Id.StartsWith(CrossPrefix, StringComparison.Ordinal))));
        return failures;
    }

    // ------------------------------------------------------------------------------------------- //
    // An `out` operand naming a ready value: the store observed while the call runs, and after a throw
    // ------------------------------------------------------------------------------------------- //

    /// <summary>How an operand's store is observed, beyond the read after the call that the use's own cell is. A closed
    /// literal set.</summary>
    internal enum OutObservation
    {
        /// <summary>The callee reads the value by its own name after it has stored through the operand.</summary>
        InCallee,

        /// <summary>The callee raises after its store, the call sits in a <c>try</c>, and the value is read after it.</summary>
        AfterThrow,
    }

    /// <summary>The uses observed: the whole-value operand uses that store without reading, the image of
    /// <see cref="ParameterModifier"/> through <see cref="OperandUses"/> and <see cref="IsPlainStore"/>.</summary>
    internal static IEnumerable<LocationUse> ObservedUses() => OperandUses.Values.Select(o => o.Whole).Where(IsPlainStore);

    private const string NotReadyYet =
        "the value has none yet, so the operand is the temporary stored when the call returns (the owner's Q12), which the cell without an observation holds by its own model: what the callee reads there is a read before the initializer, the `Read` use's cell";

    /// <summary>Why an operand's store is not observed at a time, or null: the value is ready when the use runs.</summary>
    internal static string? ObservedWhenExclusion(When when) => when is When.Ready or When.ReadyAcrossAwait ? null : NotReadyYet;

    /// <summary>Why a pair's operand is not observed, or null: the target value is ready when the use runs, in a host
    /// that has swept, that the use sweeps, or that is still sweeping below the value.</summary>
    internal static string? ObservedCrossExclusion(CrossHost user, CrossHost target, CrossOrder order, bool readBack) =>
        readBack ? "the read back observes the user's value, not the operand: the cell without it is the one observed"
        : UseRunsInsideTheTargetSweep(user, target, order) ? NotReadyYet
        : null;

    /// <summary>The use runs inside the target host's OWN sweep, below the target value's place: the value is ready
    /// and its host has not swept. The cell that tells an arm picked by the value from one picked by the host.</summary>
    internal static bool UseRunsBelowTheValueInTheTargetSweep(CrossHost user, CrossHost target, CrossOrder order) =>
        target.IsModule && HasPosition(user) && order == CrossOrder.TargetFirst;

    /// <summary>The callee of an observed operand: it stores <see cref="CalleeStore"/>, then reads the value by its
    /// own name, or raises.</summary>
    private static IEnumerable<string> ObservedCallee(OutObservation observed, string read) => observed switch
    {
        OutObservation.InCallee => new[] { "def see(a: out int, by: int) -> int:", "    a = 41 + by", $"    return {read}" },
        OutObservation.AfterThrow => new[] { "def boom(a: out int, by: int) -> None:", "    a = 41 + by", "    raise ValueError(\"x\")" },
        _ => throw new ArgumentOutOfRangeException(nameof(observed)),
    };

    /// <summary>The statement of an observed operand on target <paramref name="t"/>, what the value holds after it, and
    /// what the use's function returns where that is not the value read back: what the callee read.</summary>
    private static (string Statement, int After, (string Text, int Value)? Returned) ObservedText(OutObservation observed, string t, string v) => observed switch
    {
        OutObservation.InCallee => ($"r0 = see(out {t}, {v})", CalleeStore, ("r0", CalleeStore)),
        OutObservation.AfterThrow => ($"try:\n    boom(out {t}, {v})\nexcept ValueError:\n    pass", CalleeStore, null),
        _ => throw new ArgumentOutOfRangeException(nameof(observed)),
    };

    /// <summary>What a cell prints first under the emission the owner rejected at Q24, the temporary on a ready value: the
    /// callee reads the value it was before the call, and a callee that raises leaves it. The value read after the
    /// call follows: stored on return, or never.</summary>
    internal static string RejectedModel(OutObservation observed, HostForm? form) =>
        (observed switch
        {
            OutObservation.InCallee => $"{InitialValue} {CalleeStore}",
            OutObservation.AfterThrow => $"{InitialValue} {InitialValue}",
            _ => throw new ArgumentOutOfRangeException(nameof(observed)),
        }) + (form == HostForm.Generic ? $" {InitialValue}" : "");

    private static string ObservedId(string id, OutObservation observed) => $"{id}/{observed}";

    private static IEnumerable<(BindingKind Holder, HostForm Form, ValueKind Kind, LocationUse Use, When When, OutObservation Observed)> ObservedLocationProduct() =>
        from cell in LocationProduct()
        where ObservedUses().Contains(cell.Use) && ObservedWhenExclusion(cell.When) == null
        from observed in Enum.GetValues<OutObservation>()
        select (cell.Holder, cell.Form, cell.Kind, cell.Use, cell.When, observed);

    private static IEnumerable<(CrossHost User, CrossHost Target, CrossOrder Order, LocationUse Use, OutObservation Observed)> ObservedCrossProduct() =>
        from cell in CrossProduct()
        where ObservedUses().Contains(cell.Use) && ObservedCrossExclusion(cell.User, cell.Target, cell.Order, cell.ReadBack) == null
        from observed in Enum.GetValues<OutObservation>()
        select (cell.User, cell.Target, cell.Order, cell.Use, observed);

    /// <summary>Every cell is green today and must not move: the operand is the storage, so the callee reads what it
    /// stored and a throw leaves it (the owner's Q24). Python has no modifier, so no twin.</summary>
    internal static IEnumerable<Cell> OutObservationCells()
    {
        foreach (var (holder, form, kind, use, when, observed) in ObservedLocationProduct())
        {
            var (_, after, returned) = ObservedText(observed, "t", "1");
            yield return new Cell(
                ObservedId(LocationId(holder, form, kind, use, when), observed),
                Family.Location,
                LocationProgram(holder, form, kind, use, when, twin: false, observed),
                null,
                LocationModel(use, when, form, after, returned?.Value))
            {
                IsControl = true,
                Python = PythonExpectation.NoTwin,
                Rejected = RejectedModel(observed, form),
            };
        }
        foreach (var (user, target, order, use, observed) in ObservedCrossProduct())
        {
            var (_, after, returned) = ObservedText(observed, "p", "1");
            yield return new Cell(
                ObservedId(CrossId(user, target, order, use, readBack: false), observed),
                Family.Location,
                CrossProgram(user, target, order, use, readBack: false, twin: false, observed),
                null,
                $"{returned?.Value ?? after} {after}")
            {
                IsControl = true,
                Python = PythonExpectation.NoTwin,
                Rejected = RejectedModel(observed, null),
            };
        }
    }

    internal static List<string> OutObservationTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        var uses = ObservedUses().ToList();
        if (uses.Count == 0)
        {
            failures.Add("no operand use stores without reading: nothing is observed");
            return failures;
        }
        if (ObservedWhenExclusion(When.Ready) != null)
            failures.Add("an operand naming a ready value is not observed");
        // An id listed twice is the pool's own totality line: here the first cell stands for it.
        var byId = pool.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Every cell of the two products whose use is an operand that stores is observed in every way, or says why not.
        var owed = new List<(string Id, string? Excluded)>();
        foreach (var cell in LocationProduct().Where(c => uses.Contains(c.Use)))
            foreach (var observed in Enum.GetValues<OutObservation>())
                owed.Add((ObservedId(LocationId(cell.Holder, cell.Form, cell.Kind, cell.Use, cell.When), observed), ObservedWhenExclusion(cell.When)));
        foreach (var cell in CrossProduct().Where(c => uses.Contains(c.Use)))
            foreach (var observed in Enum.GetValues<OutObservation>())
                owed.Add((
                    ObservedId(CrossId(cell.User, cell.Target, cell.Order, cell.Use, cell.ReadBack), observed),
                    ObservedCrossExclusion(cell.User, cell.Target, cell.Order, cell.ReadBack)));
        foreach (var (id, excluded) in owed)
        {
            if (byId.ContainsKey(id) == (excluded != null))
                failures.Add($"{id} is {(excluded == null ? "owed by an operand that stores into a ready value and not generated" : "generated against its exclusion")}");
        }

        var location = ObservedLocationProduct().ToList();
        var cross = ObservedCrossProduct().ToList();
        foreach (var observed in Enum.GetValues<OutObservation>())
        {
            // On every stored holder and host form, and at every time the value is ready.
            foreach (var (holder, form) in LocationProduct().Where(c => uses.Contains(c.Use)).Select(c => (c.Holder, c.Form)).Distinct())
                foreach (var when in Enum.GetValues<When>().Where(w => ObservedWhenExclusion(w) == null && !location.Contains((holder, form, ValueKind.Int, uses[0], w, observed))))
                    failures.Add($"holder BindingKind.{holder} in HostForm.{form} has no operand observed {observed} at When.{when}");
            // The value ready in a host that has not swept: the one place the value's state and its host's differ.
            if (!cross.Any(c => c.Observed == observed && UseRunsBelowTheValueInTheTargetSweep(c.User, c.Target, c.Order)))
                failures.Add($"no operand is observed {observed} on a ready value whose host is still sweeping");
            if (!cross.Any(c => c.Observed == observed && UseStartsTheTargetSweep(c.User, c.Target, c.Order)))
                failures.Add($"no operand is observed {observed} on a host the use is the first to require");
        }

        // An observed cell tells the two emissions apart, and is a control: green today.
        var generated = location.Select(c => ObservedId(LocationId(c.Holder, c.Form, c.Kind, c.Use, c.When), c.Observed))
            .Concat(cross.Select(c => ObservedId(CrossId(c.User, c.Target, c.Order, c.Use, readBack: false), c.Observed)));
        foreach (var id in generated)
        {
            if (!byId.TryGetValue(id, out var cell))
                continue;
            if (cell.Rejected == null || cell.Rejected == cell.Model)
                failures.Add($"{id}: its model '{cell.Model}' is what the rejected emission prints too ('{cell.Rejected}'): it tells nothing apart");
            if (!cell.IsControl)
                failures.Add($"{id} observes a ready value and is no control");
        }
        foreach (var cell in pool.Where(c => c.Rejected != null && !owed.Any(o => o.Id == c.Id)))
            failures.Add($"{cell.Id} carries a rejected model and is no observed operand");
        return failures;
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> LocationExclusions() =>
        ExcludedHolders.Select(e => ($"holder BindingKind.{e.Key}", e.Value))
            .Concat(ExcludedStoreForms.Select(e => ($"StoreForm.{e.Key}", e.Value)))
            .Concat(ExcludedTargetArms.Select(e => ($"target arm {e.Key}", e.Value)))
            .Concat(ExcludedOperandModifiers.Select(e => ($"operand use ParameterModifier.{e.Key}", e.Value)))
            .Concat(ExcludedTextHolders.Select(e => ($"a store in the initializer text of BindingKind.{e.Key}", e.Value)))
            .Concat(UseExclusions())
            .Append((
                "a walrus in a module initializer's text",
                new Exclusion(WalrusBindsALocal, "a: int = (t := 5) + 1\nt: int = 7\ndef main() -> None:\n    print(a, t)\n") { Prints = "6 7" }))
            .Concat(CrossExclusions())
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
                // (the owner's Q14), and one inside it, alone and read back, which runs (Q21).
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
            // A plain store that is an expression has its cell in the initializer's own text.
            if (plain && TextForm(use, "t") != null && use != LocationUse.Walrus && cells.All(c => c.Use != use || c.When != When.InInitializerText))
                failures.Add($"LocationUse.{use} has no cell in its own initializer text");
        }
        // Inside its own initializer the rule's two answers both have a cell, on every stored holder and host form.
        foreach (var (holder, form) in cells.Where(c => !IsConstHolder(c.Holder)).Select(c => (c.Holder, c.Form)).Distinct())
            foreach (var answer in new[] { true, false }.Where(a => !cells.Any(c => c.Holder == holder && c.Form == form && c.When == When.DuringInitializer && OwnInitializerRaises(c.Use) == a)))
                failures.Add($"holder BindingKind.{holder} in HostForm.{form} has no use inside its own initializer that {(answer ? "raises" : "runs")}");
        foreach (var holder in ExcludedTextHolders.Keys.Where(h => !LocationHolders.Contains(h) || IsConstHolder(h)))
            failures.Add($"BindingKind.{holder} is excluded from the initializer text and is no stored holder");

        failures.AddRange(CrossTotalityFailures(pool));
        failures.AddRange(OutObservationTotalityFailures(pool));

        // What the axes owe, written again from the enumerations and the exclusion predicates. A control uses a value
        // that is ready, or is a plain store that runs wherever it is called from; an observed operand is one always.
        var owed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var holder in LocationHolders)
            foreach (var form in Enum.GetValues<HostForm>().Where(f => FormExclusion(holder, f) == null))
                foreach (var kind in Enum.GetValues<ValueKind>())
                    foreach (var use in Enum.GetValues<LocationUse>().Where(u => UseExclusion(u, kind, holder) == null && !ExcludedLocationCells.ContainsKey((u, holder, form))))
                        foreach (var when in Enum.GetValues<When>().Where(w => WhenExclusion(use, holder, w) == null))
                        {
                            var id = LocationId(holder, form, kind, use, when);
                            owed[id] = (!IsOwnInitializer(when) && when != When.BeforeInitializer) || (IsPlainStore(use) && when != When.InInitializerText);
                            if (!ObservedUses().Contains(use) || ObservedWhenExclusion(when) != null)
                                continue;
                            foreach (var observed in Enum.GetValues<OutObservation>())
                                owed[ObservedId(id, observed)] = true;
                        }
        failures.AddRange(PoolAgainstOwed("the location axes", owed, pool.Where(c => c.Family == Family.Location && !c.Id.StartsWith(CrossPrefix, StringComparison.Ordinal))));
        return failures;
    }
}

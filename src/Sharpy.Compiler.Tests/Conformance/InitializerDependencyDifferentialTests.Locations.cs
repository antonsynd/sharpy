using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The location family (plan-775c50, second stage-0 commit): a store to a module or <c>@static</c> value, and every
/// use that needs the value's storage location and not a copy of it, does what it does on a C# field today, whenever
/// it runs. Axes: the use (<see cref="LocationUse"/>, whose whole-value forms are the image of <see cref="StoreForm"/>),
/// the holder (<see cref="BindingKind"/>), what the value is (<see cref="ValueKind"/>) and when the use runs
/// (<see cref="When"/>).
///
/// The expectation is one rule, <see cref="LocationModel"/>. A use that runs after the value is ready, an <c>await</c>
/// inside it or not, leaves what it wrote. A use that runs before the value's initializer (a function an earlier
/// initializer calls) is rule N3's: a plain store is kept until the initializer runs at its own place and overwrites
/// it, as python's module does; any other use reads the value, so the value is initialized first and its initializer
/// does not run again.
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
    }

    internal enum When
    {
        Ready,

        /// <summary>The value is ready, and the use holds an <c>await</c> between naming the value and writing it.</summary>
        ReadyAcrossAwait,
        BeforeInitializer,
    }

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

    /// <summary>The holders: the two kinds of static storage a program stores to.</summary>
    internal static readonly IReadOnlyList<BindingKind> LocationHolders = new[] { BindingKind.ModuleVar, BindingKind.StaticField };

    private const string NotStored = "a const is not stored to, and has no member store that is not its value's";

    /// <summary>Lazy: it starts from the operand family's exclusions, which another file of this type declares.</summary>
    private static readonly Lazy<IReadOnlyDictionary<BindingKind, Exclusion>> LazyExcludedHolders = new(() => new Dictionary<BindingKind, Exclusion>(
        ExcludedOperandKinds.Where(e => !LocationHolders.Contains(e.Key)))
    {
        [BindingKind.ModuleLiteralConst] = new(NotStored),
        [BindingKind.ModuleComputedConst] = new(NotStored),
        [BindingKind.ClassLiteralConst] = new(NotStored),
        [BindingKind.ClassComputedConst] = new(NotStored),
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

    /// <summary>A use reads the value before it writes, unless it is a plain store: python binds a plain store's target without reading it.</summary>
    internal static bool IsPlainStore(LocationUse use) => use switch
    {
        LocationUse.Assign or LocationUse.Walrus => true,
        LocationUse.Augmented or LocationUse.RefOperand or LocationUse.MemberAssign or LocationUse.MemberAugmented
            or LocationUse.MutatingCall or LocationUse.MemberRefOperand => false,
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    /// <summary>Why a use has no cell on a value kind or a holder, or null: it has one.</summary>
    internal static string? UseExclusion(LocationUse use, ValueKind kind, BindingKind holder)
    {
        var whole = use is LocationUse.Assign or LocationUse.Augmented or LocationUse.Walrus or LocationUse.RefOperand;
        if (kind == ValueKind.Int && !whole)
            return "an int has no member";
        if (kind != ValueKind.Int && use is LocationUse.Augmented or LocationUse.RefOperand)
            return "the int cell is the whole-value form; a struct's is its member's";
        if (kind != ValueKind.Int && use == LocationUse.Walrus)
            return "the int cell is the walrus store";
        if (use == LocationUse.Walrus && holder != BindingKind.ModuleVar)
            return "a walrus target is a bare name, which a `@static` value is not outside its type";
        return null;
    }

    private const int InitialValue = 7;

    /// <summary>The statement of a use on target <paramref name="t"/> with operand <paramref name="v"/> (worth 1), and what the value holds after it.</summary>
    private static (string Statement, int After) UseText(LocationUse use, ValueKind kind, string t, string v) => (use, kind) switch
    {
        (LocationUse.Assign, ValueKind.Int) => ($"{t} = 5 * {v}", 5),
        (LocationUse.Assign, ValueKind.Struct) => ($"{t} = P(5 * {v})", 5),
        (LocationUse.Assign, ValueKind.Class) => ($"{t} = Q(5 * {v})", 5),
        (LocationUse.Augmented, _) => ($"{t} += {v}", InitialValue + 1),
        (LocationUse.Walrus, _) => ($"w0 = ({t} := 5 * {v})", 5),
        (LocationUse.RefOperand, _) => ($"r0 = grow(ref {t}, {v})", InitialValue + 1),
        (LocationUse.MemberAssign, _) => ($"{t}.x = 5 * {v}", 5),
        (LocationUse.MemberAugmented, _) => ($"{t}.x += {v}", InitialValue + 1),
        (LocationUse.MutatingCall, _) => ($"{t}.add({v})", InitialValue + 1),
        (LocationUse.MemberRefOperand, _) => ($"r0 = grow(ref {t}.x, {v})", InitialValue + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    /// <summary>What a cell prints: the read-back inside the use's function, then the value once the module has loaded.</summary>
    internal static string LocationModel(LocationUse use, When when, int after) => when switch
    {
        When.Ready or When.ReadyAcrossAwait => $"{after} {after}",
        When.BeforeInitializer => IsPlainStore(use) ? $"{after} {InitialValue}" : $"{after} {after}",
        _ => throw new ArgumentOutOfRangeException(nameof(when)),
    };

    private static string LocationId(BindingKind holder, ValueKind kind, LocationUse use, When when) => $"Location/{holder}/{kind}/{use}/{when}";

    private static string LocationProgram(BindingKind holder, ValueKind kind, LocationUse use, When when, bool twin)
    {
        var module = holder == BindingKind.ModuleVar;
        var t = module ? "t" : "H.t";
        var (type, initial, readBack) = kind switch
        {
            ValueKind.Int => ("int", $"{InitialValue}", t),
            ValueKind.Struct => ("P", $"P({InitialValue})", $"{t}.x"),
            ValueKind.Class => ("Q", $"Q({InitialValue})", $"{t}.x"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var awaited = when == When.ReadyAcrossAwait;
        var (statement, _) = UseText(use, kind, t, awaited ? "await one()" : "1");
        var def = awaited ? "async def" : "def";

        var function = new List<string> { $"{def} use() -> int:" };
        // Python binds a bare store's name in the function unless it is declared global: the twin says so, Sharpy has no such line.
        if (twin && module)
            function.Add("    global t");
        function.Add($"    {statement}");
        function.Add($"    return {readBack}");

        var value = $"t: {type} = {initial}";
        var early = "a: int = use()";
        var declared = when == When.BeforeInitializer ? new[] { early, value } : new[] { value };
        var holderLines = module
            ? declared.ToList()
            : new[] { "class H:" }.Concat(declared.SelectMany(d => new[] { "    @static", "    " + d })).ToList();

        // Sharpy sees a module variable from a function declared after it (rule N1's order, #2423's until P78), and calls a
        // later function from an initializer. Python runs top to bottom: the function is defined before the call.
        var body = new List<string>();
        body.AddRange(twin ? function.Concat(holderLines) : holderLines.Concat(function));
        if (when == When.BeforeInitializer)
        {
            body.Add("def main() -> None:");
            body.Add($"    print({(module ? "a" : "H.a")}, {readBack})");
        }
        else
        {
            body.Add($"{def} main() -> None:");
            body.Add($"    r = {(awaited ? "await " : "")}use()");
            body.Add($"    print(r, {readBack})");
        }
        return WithHelpers(Harness.Join(body));
    }

    /// <summary>What python's twin does, and whether it has one. Python has no modifier and the harness's twin no <c>await</c> chain.
    /// Before the initializer, the module twin is python's own order: a plain store is kept and then overwritten, any read is a
    /// NameError (order-only: under N3 the value is initialized first). A class body's twin cannot name its own class yet, so
    /// the <c>@static</c> holder has none there.</summary>
    internal static PythonExpectation LocationPython(BindingKind holder, LocationUse use, When when)
    {
        if (use is LocationUse.RefOperand or LocationUse.MemberRefOperand || when == When.ReadyAcrossAwait)
            return PythonExpectation.NoTwin;
        if (when == When.Ready)
            return PythonExpectation.Prints;
        if (holder != BindingKind.ModuleVar)
            return PythonExpectation.NoTwin;
        return IsPlainStore(use) ? PythonExpectation.Prints : PythonExpectation.NameError;
    }

    /// <summary>A read before the initializer sees a storage default today: a wrong value, or a null that throws.</summary>
    private static Route LocationRoute(LocationUse use, When when) => (verdict, _, _) =>
        when != When.BeforeInitializer || IsPlainStore(use) ? null
        : verdict == VerdictClass.SilentWrong ? $"{ReadinessIssue} silent-wrong"
        : verdict == VerdictClass.RuntimeThrow ? $"{ReadinessIssue} runtime-throw"
        : null;

    private static IEnumerable<(BindingKind Holder, ValueKind Kind, LocationUse Use, When When)> LocationProduct() =>
        from holder in LocationHolders
        from kind in Enum.GetValues<ValueKind>()
        from use in Enum.GetValues<LocationUse>()
        where UseExclusion(use, kind, holder) == null
        from when in Enum.GetValues<When>()
        select (holder, kind, use, when);

    internal static IEnumerable<Cell> LocationCells()
    {
        foreach (var (holder, kind, use, when) in LocationProduct())
        {
            var python = LocationPython(holder, use, when);
            var (_, after) = UseText(use, kind, "t", "1");
            yield return new Cell(
                LocationId(holder, kind, use, when),
                Family.Location,
                LocationProgram(holder, kind, use, when, twin: false),
                python == PythonExpectation.NoTwin ? null : LocationProgram(holder, kind, use, when, twin: true),
                LocationModel(use, when, after))
            {
                // What runs today on a field and must not move.
                IsControl = when != When.BeforeInitializer,
                Python = python,
                Route = LocationRoute(use, when),
            };
        }
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> LocationExclusions() =>
        ExcludedHolders.Select(e => ($"holder BindingKind.{e.Key}", e.Value))
            .Concat(ExcludedStoreForms.Select(e => ($"StoreForm.{e.Key}", e.Value)));

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

        var cells = LocationProduct().ToList();
        foreach (var use in Enum.GetValues<LocationUse>().Where(u => cells.All(c => c.Use != u)))
            failures.Add($"LocationUse.{use} has no cell");
        foreach (var kind in Enum.GetValues<ValueKind>().Where(k => cells.All(c => c.Kind != k)))
            failures.Add($"ValueKind.{kind} has no cell");
        foreach (var when in Enum.GetValues<When>().Where(w => cells.All(c => c.When != w)))
            failures.Add($"When.{when} has no cell");
        foreach (var holder in LocationHolders)
            foreach (var kind in Enum.GetValues<ValueKind>())
                foreach (var use in Enum.GetValues<LocationUse>())
                {
                    // Every use is a statement on every kind it is not excluded from, and reads or does not read by rule.
                    IsPlainStore(use);
                    if (UseExclusion(use, kind, holder) == null)
                        UseText(use, kind, "t", "1");
                }

        var ids = pool.Where(c => c.Family == Family.Location).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = cells.Select(k => LocationId(k.Holder, k.Kind, k.Use, k.When)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)).Take(20))
            failures.Add($"the location axes owe {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)).Take(20))
            failures.Add($"the pool holds {id} and the location axes do not owe it");
        return failures;
    }
}

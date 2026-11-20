using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>Whether a declaration says its first value: R-QI's axis. Closed: a declaration has an initializer, has
/// none, or (a property) computes its value in accessor bodies.</summary>
internal enum ScopeInitializer
{
    Present,
    Absent,
    AccessorBodies,
}

/// <summary>Where a declaration's type comes from. Closed: written, or inferred from the initializer.</summary>
internal enum ScopeAnnotation
{
    Annotated,
    Inferred,
}

/// <summary>What a constant position is handed (#2432's expression-kind axis, the kinds the P77a pool has no cell
/// for; charter §3.20l calls 43 and 45). Closed by those two calls.</summary>
internal enum ScopeConstantKind
{
    /// <summary>A constant of a referenced assembly: a CLR literal field read through a Stdlib module.</summary>
    ClrLiteralField,

    /// <summary>A module variable of the <c>.spy</c> stdlib: no constant.</summary>
    StdlibModuleVariable,

    /// <summary>A comparison chain whose every link is a constant.</summary>
    ComparisonChain,

    /// <summary>A comparison chain with a link that is no constant.</summary>
    ChainWithNonConstantLink,
}

/// <summary>Whether a type alias's target exists. Closed.</summary>
internal enum ScopeAliasTarget
{
    Declared,
    Missing,
}

/// <summary>Whether anything names a type alias. Closed.</summary>
internal enum ScopeAliasUse
{
    Used,
    Unused,
}

/// <summary>Who calls a constructor overload. Closed: another constructor of the type, a method of it, an initializer
/// of it, a function outside it.</summary>
internal enum ScopeOverloadCaller
{
    InitDelegation,
    MethodBody,
    StaticInitializer,
    ModuleFunction,
}

/// <summary>
/// Stage 0 of plan-777361 (P78; verification contract §11), the owner's ruling of 2026-11-20 (#2423 comment
/// 7030074628, item 1): the cells of rules N1, N2, N4 and N7 that the P77a pool
/// (<see cref="NameResolutionDifferentialTests"/>) has no program for, as a generated test type of their own. A cell is
/// a <see cref="NameCell"/>, and its tag and rule are READ from the rule table (<c>name-resolution-rules.txt</c>, #2487):
/// the generator writes the program, what it prints when it runs and the codes a refusal may carry, and never a verdict.
///
/// Six cell sets, each over an enumeration or a set named closed, with every value generated or excluded with its reason
/// (<see cref="Axes_EveryValueIsGeneratedOrExcluded"/>):
/// <list type="bullet">
/// <item><c>no-initializer</c> (R-QI): <see cref="BindingKind"/> × <see cref="ScopeInitializer"/>;</item>
/// <item><c>typed-read</c>: <c>b: bool = &lt;read&gt;</c>, <see cref="BindingKind"/> × <see cref="ScopeAnnotation"/> × order;</item>
/// <item><c>callee</c>: a binder's name called under a module <c>def</c> of the name, <see cref="LocalTarget"/> and the parameter;</item>
/// <item><c>constant-kind</c> (#2432): the constant positions of <see cref="ReadPosition"/> × <see cref="ScopeConstantKind"/>;</item>
/// <item><c>init-overload</c>: <see cref="ScopeOverloadCaller"/> × order;</item>
/// <item><c>alias-target</c>: <see cref="ScopeAliasTarget"/> × <see cref="ScopeAliasUse"/> × order.</item>
/// </list>
/// Known-red cells are rows of <c>scope-meaning-differential-allowlist.txt</c> (a missing file reads as empty), each citing
/// the issue whose fix turns it green. A must-refuse cell refused under a code its cell does not admit is listed as
/// <see cref="VerdictClass.FalseRefusal"/> with the lane <c>wrong-code</c>: the refusal is another rule's.
/// </summary>
[Trait("Category", "GapDiscovery")]
public sealed class ScopeMeaningDifferentialTests : IntegrationTestBase
{
    private const string AllowlistFileName = "scope-meaning-differential-allowlist.txt";
    private const int SharpyExecTimeoutMs = 12_000;
    private const int PythonBatchTimeoutMs = 600_000;

    public ScopeMeaningDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    /// <summary>A generated case: the cell, and whether python runs the same program with the same meaning (no twin, no vote).</summary>
    internal sealed record Case(NameCell Cell, bool Twin);

    private const string Main = "def main() -> None:\n";

    private static CellKey Key(params object?[] parts)
    {
        var kept = parts.Where(p => p != null).Select(p => p!).ToArray();
        return new CellKey(string.Join("/", kept.Select(p => Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture))), kept);
    }

    /// <summary>A cell before the table has said what it must do. <paramref name="model"/> is what it prints if it runs;
    /// <paramref name="refusal"/> the codes a refusal of it may carry.</summary>
    private static Case Draft(CellKey key, Family family, string source, string? model, string[] refusal, bool twin,
        BindingKind target = BindingKind.None, BindingKind outer = BindingKind.None, ReadPosition? position = null,
        LocalTarget? construct = null, bool stdlib = false)
        => new(
            new NameCell
            {
                Key = key,
                Family = family,
                Source = source,
                Tag = SharpyTag.Unruled,
                Model = model,
                Python = model == null ? new PyOutcome(PyKind.OtherError, "no twin") : PyOutcome.Ran(model),
                Rule = "",
                Target = target,
                Outer = outer,
                Position = position,
                Construct = construct,
                SeedRefusalCodes = new HashSet<string>(refusal, StringComparer.Ordinal),
                NeedsStdlib = stdlib,
            },
            twin);

    // ------------------------------------------------------------------------------------------- //
    // no-initializer (R-QI)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>How a kind declares <c>x</c> with a type and no value, where the declaration sits and how <c>main</c> reads it.
    /// <see cref="Stored"/>: the statements that give it a value before the read, for the kinds R-QI leaves alone.</summary>
    internal sealed record Declared(string Text, bool InType, string Read, string? Accessor = null, string[]? Stored = null, bool Twin = false);

    internal static readonly IReadOnlyDictionary<BindingKind, Declared> Declarations = new Dictionary<BindingKind, Declared>
    {
        [BindingKind.ModuleVar] = new("x: int", false, "x", Twin: true),
        [BindingKind.ModuleProperty] = new("property x: int", false, "x", Accessor: "property get x() -> int:\n    return 5"),
        [BindingKind.StaticField] = new("@static\nx: int", true, "H.x", Twin: true),
        [BindingKind.StaticProperty] = new("@static\nproperty x: int", true, "H.x", Accessor: "@static\nproperty get x() -> int:\n    return 5"),
        [BindingKind.InstanceField] = new("x: int", true, "H().x", Stored: new[] { "def __init__(self):", "    self.x = 5" }, Twin: true),
        [BindingKind.InstanceProperty] = new("property x: int", true, "H().x", Accessor: "property get x(self) -> int:\n    return 5", Stored: new[] { "def __init__(self):", "    self.x = 5" }),
        [BindingKind.LocalVar] = new("x: int", false, "x", Stored: new[] { "x = 5" }, Twin: true),
    };

    private const string NeedsItsValue = "the declaration's grammar takes a value: there is no spelling without one";
    private const string NoValueSlot = "declares no storage with a type annotation, so it has no initializer to leave out";

    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedDeclarations = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = "a module `let` is refused at every order (SPY0340, P21b)",
        [BindingKind.ModuleLiteralConst] = NeedsItsValue,
        [BindingKind.ModuleComputedConst] = NeedsItsValue,
        [BindingKind.ClassLiteralConst] = NeedsItsValue,
        [BindingKind.ClassComputedConst] = NeedsItsValue,
        [BindingKind.LocalLet] = NeedsItsValue,
        [BindingKind.LocalLiteralConst] = NeedsItsValue,
        [BindingKind.LocalComputedConst] = NeedsItsValue,
        [BindingKind.LambdaTypedLocal] = "a local `x: T` with another type: the LocalVar cell is its shape",
        [BindingKind.ModuleDef] = NoValueSlot,
        [BindingKind.ModuleClass] = NoValueSlot,
        [BindingKind.ModuleStruct] = NoValueSlot,
        [BindingKind.ModuleEnum] = NoValueSlot,
        [BindingKind.ModuleUnion] = NoValueSlot,
        [BindingKind.ModuleInterface] = NoValueSlot,
        [BindingKind.ModuleTypeAlias] = NoValueSlot,
        [BindingKind.StaticMethod] = NoValueSlot,
        [BindingKind.NestedType] = NoValueSlot,
        [BindingKind.InstanceMethod] = NoValueSlot,
        [BindingKind.Constructor] = NoValueSlot,
        [BindingKind.LocalDef] = NoValueSlot,
        [BindingKind.EnclosingLocal] = "a local read from a nested function: the LocalVar cell's declaration",
        [BindingKind.Parameter] = "a parameter's value is its argument",
        [BindingKind.Inner] = "bound by its comprehension or lambda",
        [BindingKind.Builtin] = "not declared by the program",
        [BindingKind.ImportedName] = "not declared by the program",
        [BindingKind.None] = "no binding",
    };

    private static string Indent(string text) => string.Join("\n", text.Split('\n').Select(l => "    " + l));

    private static IEnumerable<Case> NoInitializerCases()
    {
        foreach (var (kind, d) in Declarations.OrderBy(e => e.Key))
        {
            foreach (var initializer in Enum.GetValues<ScopeInitializer>())
            {
                if (initializer == ScopeInitializer.AccessorBodies && d.Accessor == null)
                    continue;
                string decl = initializer switch
                {
                    ScopeInitializer.Present => d.Text + " = 5",
                    ScopeInitializer.Absent => d.Text,
                    _ => d.Accessor!,
                };
                var lines = new List<string>();
                if (kind == BindingKind.LocalVar)
                {
                    lines.Add(Main.TrimEnd());
                    lines.Add(Indent(decl));
                    if (initializer == ScopeInitializer.Absent)
                        lines.AddRange(d.Stored!.Select(Indent));
                    lines.Add($"    print({d.Read})");
                }
                else
                {
                    if (d.InType)
                    {
                        lines.Add("class H:");
                        lines.Add(Indent(decl));
                        if (initializer == ScopeInitializer.Absent && d.Stored != null)
                            lines.AddRange(d.Stored.Select(Indent));
                    }
                    else
                        lines.Add(decl);
                    lines.Add(Main.TrimEnd());
                    lines.Add($"    print({d.Read})");
                }
                // Read where R-QI refuses; the kinds it leaves alone store first, so every cell that runs prints 5.
                yield return Draft(Key("read", "no-initializer", kind, initializer), Family.Read, string.Join("\n", lines) + "\n", "5",
                    Array.Empty<string>(), d.Twin && (initializer == ScopeInitializer.Present || (initializer == ScopeInitializer.Absent && d.Stored != null)), target: kind);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // typed-read: `b: bool = <read>` is the type mismatch at either order
    // ------------------------------------------------------------------------------------------- //

    /// <summary>An int-valued declaration of <c>x</c> per kind, with its inferred spelling where the kind has one, and the read of it.</summary>
    internal sealed record Typed(string Annotated, string? Inferred, bool InType, string Read, bool NeedsFive = false);

    internal static readonly IReadOnlyDictionary<BindingKind, Typed> TypedReads = new Dictionary<BindingKind, Typed>
    {
        [BindingKind.ModuleVar] = new("x: int = 5", "x: auto = 5", false, "x"),
        [BindingKind.ModuleLiteralConst] = new("const x: int = 5", "const x = 5", false, "x"),
        [BindingKind.ModuleComputedConst] = new("const x: int = five()", "const x = five()", false, "x", NeedsFive: true),
        [BindingKind.ModuleProperty] = new("property x: int = 5", null, false, "x"),
        [BindingKind.ModuleDef] = new("def x() -> int:\n    return 5", null, false, "x()"),
        [BindingKind.StaticField] = new("@static\nx: int = 5", "@static\nx: auto = 5", true, "H.x"),
        [BindingKind.StaticMethod] = new("@static\ndef x() -> int:\n    return 5", null, true, "H.x()"),
        [BindingKind.ClassLiteralConst] = new("const x: int = 5", "const x = 5", true, "H.x"),
        [BindingKind.ClassComputedConst] = new("const x: int = five()", "const x = five()", true, "H.x", NeedsFive: true),
    };

    private const string NotAnIntValue = "its read is no int value at a module or type level: the mismatch against bool is not a question of order";

    internal static readonly IReadOnlyDictionary<BindingKind, string> ExcludedTypedReads = new Dictionary<BindingKind, string>
    {
        [BindingKind.ModuleLet] = "a module `let` is refused at every order (SPY0340, P21b)",
        [BindingKind.ModuleClass] = NotAnIntValue,
        [BindingKind.ModuleStruct] = NotAnIntValue,
        [BindingKind.ModuleEnum] = NotAnIntValue,
        [BindingKind.ModuleUnion] = NotAnIntValue,
        [BindingKind.ModuleInterface] = NotAnIntValue,
        [BindingKind.ModuleTypeAlias] = NotAnIntValue,
        [BindingKind.NestedType] = NotAnIntValue,
        [BindingKind.StaticProperty] = "read through its type's name it is untyped at either order: #2417's class, not a question of order (CS0029 at both, measured @ e5991c887)",
        [BindingKind.InstanceField] = "an instance member has no read from another declaration's initializer (kind:SPY0290)",
        [BindingKind.InstanceProperty] = "an instance member has no read from another declaration's initializer (kind:SPY0290)",
        [BindingKind.InstanceMethod] = "an instance member has no read from another declaration's initializer (kind:SPY0290)",
        [BindingKind.Constructor] = NotAnIntValue,
        [BindingKind.EnclosingLocal] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.Parameter] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.Inner] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LocalVar] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LocalLet] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LocalLiteralConst] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LocalComputedConst] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LocalDef] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.LambdaTypedLocal] = "a local: a read above it is R-KX's (P80)",
        [BindingKind.Builtin] = "not declared by the program",
        [BindingKind.ImportedName] = "not declared by the program",
        [BindingKind.None] = "no binding",
    };

    private static IEnumerable<Case> TypedReadCases()
    {
        foreach (var (kind, t) in TypedReads.OrderBy(e => e.Key))
        {
            foreach (var annotation in Enum.GetValues<ScopeAnnotation>())
            {
                var text = annotation == ScopeAnnotation.Annotated ? t.Annotated : t.Inferred;
                if (text == null)
                    continue;
                foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                {
                    var target = t.InType ? "class H:\n" + Indent(text) : text;
                    // The reader is a function's local: a body resolves every module-level and type-level name, whatever the order.
                    var reader = "def r() -> int:\n    b: bool = " + t.Read + "\n    return 1";
                    var lines = new List<string>();
                    if (t.NeedsFive)
                        lines.Add("def five() -> int:\n    return 5");
                    lines.AddRange(order == Harness.Order.Earlier ? new[] { target, reader } : new[] { reader, target });
                    lines.Add(Main + "    print(r())");
                    yield return Draft(Key("read", "typed-read", kind, annotation, order), Family.Read, string.Join("\n", lines) + "\n", null,
                        new[] { "SPY0220" }, twin: false, target: kind, position: ReadPosition.FunctionBody);
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // callee: the innermost binding of a name is what a call of it calls
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The statements of <c>main</c> that bind <c>x</c> to a function returning 1 and print its call, per binder.</summary>
    internal static readonly IReadOnlyDictionary<LocalTarget, string[]> CalleeBinders = new Dictionary<LocalTarget, string[]>
    {
        [LocalTarget.For] = new[] { "for x in [one]:", "    print(x())" },
        [LocalTarget.Comprehension] = new[] { "print([x() for x in [one]][0])" },
        [LocalTarget.WithAs] = new[] { "with CM() as x:", "    print(x())" },
    };

    internal static readonly IReadOnlyDictionary<LocalTarget, string> ExcludedCalleeBinders = new Dictionary<LocalTarget, string>
    {
        [LocalTarget.ExceptAs] = "binds an exception, which no call calls",
        [LocalTarget.MatchCapture] = "`match one:` / `case x:` over a function value is SPY0908 CS8119 with no module def of the name (measured @ e5991c887): no callee verdict",
        [LocalTarget.AsPatternCapture] = "a capture of the same subject: the MatchCapture exclusion's shape",
        [LocalTarget.OutLet] = "binds an out argument: a function-typed out parameter is #2154's class",
    };

    private const string One = "def one() -> int:\n    return 1";
    private const string CalleeCm = "class CM:\n    def __enter__(self) -> () -> int:\n        return one\n    def __exit__(self) -> None:\n        pass";

    private static IEnumerable<Case> CalleeCases()
    {
        foreach (var outer in new[] { BindingKind.ModuleDef, BindingKind.None })
        {
            var module = outer == BindingKind.ModuleDef ? new[] { "def x() -> int:\n    return 12", One } : new[] { One };
            yield return Draft(Key("local", "callee", BindingKind.Parameter, $"outer={outer}"), Family.Local,
                string.Join("\n", module.Append("def f(x: () -> int) -> int:\n    return x()").Append(Main + "    print(f(one))")) + "\n",
                "1", Array.Empty<string>(), twin: true, target: BindingKind.Parameter, outer: outer, position: ReadPosition.FunctionBody);
            foreach (var (binder, stmts) in CalleeBinders.OrderBy(e => e.Key))
            {
                var lines = module.ToList();
                if (binder == LocalTarget.WithAs)
                    lines.Add(CalleeCm);
                lines.Add(Main + string.Join("\n", stmts.Select(s => "    " + s)));
                yield return Draft(Key("local", "callee", binder, $"outer={outer}"), Family.Local, string.Join("\n", lines) + "\n",
                    "1", Array.Empty<string>(), twin: binder is LocalTarget.For or LocalTarget.Comprehension,
                    target: binder == LocalTarget.Comprehension ? BindingKind.Inner : BindingKind.LocalVar, outer: outer,
                    position: ReadPosition.FunctionBody, construct: binder);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // constant-kind (#2432)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The positions that take constants only: <see cref="Harness.IsConstantOnly"/>'s and the enum member's value.</summary>
    internal static IReadOnlyList<ReadPosition> ConstantPositions =>
        Enum.GetValues<ReadPosition>().Where(p => Harness.IsConstantOnly(p) || p == ReadPosition.EnumMemberValue).ToList();

    /// <summary>The expression, its type, what it prints and what it imports.</summary>
    internal sealed record ConstantExpression(string Label, string Text, string Type, string Prints, string? Import);

    internal static readonly IReadOnlyDictionary<ScopeConstantKind, ConstantExpression[]> ConstantExpressions = new Dictionary<ScopeConstantKind, ConstantExpression[]>
    {
        [ScopeConstantKind.ClrLiteralField] = new[] { new ConstantExpression("subprocess.PIPE", "subprocess.PIPE", "int", "-1", "subprocess") },
        [ScopeConstantKind.StdlibModuleVariable] = new[]
        {
            new ConstantExpression("socket.AF_INET", "socket.AF_INET", "int", "2", "socket"),
            new ConstantExpression("math.pi", "math.pi", "float", "3.141592653589793", "math"),
        },
        [ScopeConstantKind.ComparisonChain] = new[] { new ConstantExpression("ordered", "1 < 2 < 3", "bool", "True", null) },
        [ScopeConstantKind.ChainWithNonConstantLink] = new[] { new ConstantExpression("membership", "0 < 2 in [1, 2]", "bool", "True", null) },
    };

    private static IEnumerable<Case> ConstantKindCases()
    {
        foreach (var position in ConstantPositions)
        {
            foreach (var (kind, expressions) in ConstantExpressions.OrderBy(e => e.Key))
            {
                foreach (var e in expressions)
                {
                    // An enum member's value is an int: the other types have no cell there.
                    if (position == ReadPosition.EnumMemberValue && e.Type != "int")
                        continue;
                    string host = position switch
                    {
                        ReadPosition.ParameterDefault => $"def f(p: {e.Type} = {e.Text}) -> {e.Type}:\n    return p\n{Main}    print(f())",
                        ReadPosition.DataclassFieldInitializer => $"@dataclass\nclass H:\n    p: {e.Type} = {e.Text}\n{Main}    print(H().p)",
                        ReadPosition.StructFieldInitializer => $"struct H:\n    p: {e.Type} = {e.Text}\n{Main}    print(H().p)",
                        ReadPosition.EnumMemberValue => $"enum H:\n    RED = {e.Text}\n{Main}    print(H.RED.value)",
                        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "not a constant position"),
                    };
                    var source = (e.Import == null ? "" : $"import {e.Import}\n") + host + "\n";
                    yield return Draft(Key("read", "constant-kind", position, kind, e.Label), Family.Read, source, e.Prints,
                        new[] { "SPY0401" }, twin: kind is ScopeConstantKind.ClrLiteralField or ScopeConstantKind.ComparisonChain && position != ReadPosition.EnumMemberValue,
                        position: position, stdlib: e.Import != null);
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // init-overload: a constructor's overload set is the whole body's
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> InitOverloadCases()
    {
        const string Target = "def __init__(self, x: int):\n    self.x = x";
        foreach (var caller in Enum.GetValues<ScopeOverloadCaller>())
        {
            foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
            {
                (string? member, string? function, string read) = caller switch
                {
                    ScopeOverloadCaller.InitDelegation => ("def __init__(self):\n    self.__init__(7)", null, "P().x"),
                    ScopeOverloadCaller.MethodBody => ("def mk(self) -> int:\n    return P(7).x", null, "P(1).mk()"),
                    ScopeOverloadCaller.StaticInitializer => ("@static\nd: int = P(7).x", null, "P.d"),
                    _ => ((string?)null, "def mk() -> int:\n    return P(7).x", "mk()"),
                };
                var members = new List<string> { "x: int = 0" };
                if (member == null)
                    members.Add(Target);
                else
                    members.AddRange(order == Harness.Order.Earlier ? new[] { Target, member } : new[] { member, Target });
                var type = "class P:\n" + string.Join("\n", members.Select(Indent));
                var lines = function == null ? new[] { type } : order == Harness.Order.Earlier ? new[] { type, function } : new[] { function, type };
                yield return Draft(Key("read", "init-overload", caller, order), Family.Read, string.Join("\n", lines) + $"\n{Main}    print({read})\n", "7",
                    Array.Empty<string>(), twin: false, target: BindingKind.Constructor);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // alias-target: an alias names a type that exists, used or not
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> AliasTargetCases()
    {
        foreach (var target in Enum.GetValues<ScopeAliasTarget>())
        {
            foreach (var use in Enum.GetValues<ScopeAliasUse>())
            {
                foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                {
                    // The order is the alias against its target's declaration: a missing target has none.
                    if (target == ScopeAliasTarget.Missing && order == Harness.Order.Later)
                        continue;
                    var alias = target == ScopeAliasTarget.Declared ? "type K0 = C0" : "type K0 = nosuch";
                    var lines = new List<string>();
                    if (target == ScopeAliasTarget.Declared && order == Harness.Order.Earlier)
                        lines.Add("class C0:\n    v: int = 1");
                    lines.Add(alias);
                    if (target == ScopeAliasTarget.Declared && order == Harness.Order.Later)
                        lines.Add("class C0:\n    v: int = 1");
                    if (use == ScopeAliasUse.Used)
                        lines.Add("def f(k: K0) -> int:\n    return 1");
                    lines.Add(Main + "    print(1)");
                    yield return Draft(Key("read", "alias-target", target, use, order), Family.Read, string.Join("\n", lines) + "\n", "1",
                        new[] { "SPY0202" }, twin: false, target: BindingKind.ModuleTypeAlias, position: ReadPosition.TypeAliasTarget);
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The pool: every case, with the tag and rule its one row of the table gives it
    // ------------------------------------------------------------------------------------------- //

    private static readonly Lazy<IReadOnlyList<Case>> Drafts = new(() =>
        NoInitializerCases().Concat(TypedReadCases()).Concat(CalleeCases()).Concat(ConstantKindCases()).Concat(InitOverloadCases()).Concat(AliasTargetCases()).ToList());

    /// <summary>The cells as generated, before the table is read: what <see cref="Harness.RuleTablePools"/> counts a row's cells over.</summary>
    internal static IReadOnlyList<NameCell> DraftCells() => Drafts.Value.Select(c => c.Cell).ToList();

    private static readonly Lazy<IReadOnlyList<Case>> Pool = new(() => Drafts.Value.Select(c =>
    {
        var rows = Harness.RowsOf(Harness.RuleTable(), c.Cell);
        return rows.Count == 1 && rows[0].Tag is { } tag ? c with { Cell = c.Cell with { Tag = tag, Rule = rows[0].Rule } } : c;
    }).ToList());

    internal static IReadOnlyList<Case> GeneratedCases() => Pool.Value;

    /// <summary>The pool's totality: ids are unique, every cell has its one row and took its verdict from it, a cell that
    /// must run says what it prints, and a twin is a program that runs.</summary>
    internal static List<string> TotalityFailures(IReadOnlyList<Case> pool)
    {
        var failures = new List<string>();
        foreach (var dup in pool.GroupBy(c => c.Cell.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"duplicate cell id {dup.Key}");
        var rows = Harness.RuleTable();
        foreach (var c in pool)
        {
            var matched = Harness.RowsOf(rows, c.Cell);
            if (matched.Count != 1)
                failures.Add($"RuleTable: {c.Cell.Id} matches {(matched.Count == 0 ? "no row" : "rows L" + string.Join(", L", matched.Select(r => r.Line)))}");
            else if (matched[0].Tag != c.Cell.Tag || matched[0].Rule != c.Cell.Rule)
                failures.Add($"RuleTable: {c.Cell.Id} carries {c.Cell.Tag} under '{c.Cell.Rule}', and its row L{matched[0].Line} says {matched[0].Tag?.ToString() ?? "*"} under {matched[0].Rule}");
            if (c.Cell.Tag is not (SharpyTag.MustRun or SharpyTag.MustRefuse))
                failures.Add($"{c.Cell.Id}: a cell of this pool must run or must be refused, and its row says {c.Cell.Tag}");
            if (c.Cell.Tag == SharpyTag.MustRun && c.Cell.Model == null)
                failures.Add($"{c.Cell.Id}: its row says must-run and the generator gives it nothing to print");
            if (c.Twin && c.Cell.Tag != SharpyTag.MustRun)
                failures.Add($"{c.Cell.Id}: a twin is python running the same program, and the cell does not run");
        }
        failures.AddRange(Harness.RuleTableRowFailures(rows, Harness.RuleTablePools.SelectMany(p => p.Cells()).ToList()));
        return failures;
    }

    [Fact]
    public void RuleTable_EveryCaseHasOneRow_AndTakesItsVerdictFromIt()
    {
        var failures = TotalityFailures(GeneratedCases());
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Each axis against its enumeration: a value is generated or carries its reason, never both, never neither.</summary>
    [Fact]
    public void Axes_EveryValueIsGeneratedOrExcluded()
    {
        var pool = GeneratedCases();
        var problems = new List<string>();

        void Partition<T>(string axis, IEnumerable<T> generated, IEnumerable<T> excluded) where T : struct, Enum
        {
            var g = generated.ToHashSet();
            var e = excluded.ToHashSet();
            foreach (var value in Enum.GetValues<T>())
            {
                if (g.Contains(value) == e.Contains(value))
                    problems.Add($"{axis}: {value} is {(g.Contains(value) ? "generated and excluded" : "neither generated nor excluded")}");
            }
        }

        Partition("no-initializer", Declarations.Keys, ExcludedDeclarations.Keys);
        Partition("typed-read", TypedReads.Keys, ExcludedTypedReads.Keys);
        Partition("callee", CalleeBinders.Keys, ExcludedCalleeBinders.Keys);

        void Fed<T>(string route, IEnumerable<T> owed) where T : struct, Enum
        {
            var carried = pool.Where(c => c.Cell.Key!.Route == route).Select(c => c.Cell.Key!.Part<T>()).ToHashSet();
            foreach (var value in owed.Where(v => !carried.Contains(v)))
                problems.Add($"{route}: no cell carries {typeof(T).Name}.{value}");
        }

        Fed("no-initializer", Declarations.Keys);
        Fed("no-initializer", Enum.GetValues<ScopeInitializer>());
        Fed("typed-read", TypedReads.Keys);
        Fed("typed-read", Enum.GetValues<ScopeAnnotation>());
        Fed("callee", CalleeBinders.Keys);
        Fed("constant-kind", ConstantPositions);
        Fed("constant-kind", Enum.GetValues<ScopeConstantKind>());
        Fed("init-overload", Enum.GetValues<ScopeOverloadCaller>());
        Fed("alias-target", Enum.GetValues<ScopeAliasTarget>());
        Fed("alias-target", Enum.GetValues<ScopeAliasUse>());
        foreach (var kind in Enum.GetValues<ScopeConstantKind>().Where(k => !ConstantExpressions.ContainsKey(k)))
            problems.Add($"constant-kind: {kind} has no expression");

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Equal(4, ConstantPositions.Count);
    }

    // ------------------------------------------------------------------------------------------- //
    // Judge, route, ratchet
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The issue whose fix turns a red cell of a route green: #2432 for a constant position's verdict, #2423 for the rest
    /// (R-QI's cells, the untyped forward read, the callee lookup, the alias refusal: charter §3.20l).</summary>
    internal static readonly IReadOnlyDictionary<string, string> RouteIssues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["no-initializer"] = "#2423",
        ["typed-read"] = "#2423",
        ["callee"] = "#2423",
        ["constant-kind"] = "#2432",
        ["init-overload"] = "#2423",
        ["alias-target"] = "#2423",
    };

    internal sealed record Result(Case Case, Harness.SharpyRun Run, SharpyKind Kind, VerdictClass Class, bool WrongCode, string? PythonFailure);

    /// <summary>The harness's table, and one more red: a must-refuse cell refused under a code it does not admit.</summary>
    internal static (VerdictClass Class, bool WrongCode) Judge(NameCell cell, SharpyKind kind, Harness.SharpyRun run)
    {
        var verdict = Harness.Judge(cell, kind, run.Stdout);
        bool wrongCode = verdict == VerdictClass.Match && cell.Tag == SharpyTag.MustRefuse && Harness.RefusalReasonFailure(cell, run) != null;
        return (wrongCode ? VerdictClass.FalseRefusal : verdict, wrongCode);
    }

    internal static string ProposedCite(Result r)
    {
        var issue = RouteIssues.GetValueOrDefault(r.Case.Cell.Key!.Route ?? "", "UNROUTED");
        string lane = r.WrongCode ? "wrong-code" : r.Class switch
        {
            VerdictClass.Ice => "ice",
            VerdictClass.FalseRefusal => "false-refusal",
            VerdictClass.RuntimeThrow => "runtime-throw",
            VerdictClass.SilentWrong when r.Case.Cell.Tag == SharpyTag.MustRefuse => "missing-refusal",
            _ => "silent-wrong",
        };
        return $"{issue} {lane}";
    }

    private static bool IsRed(VerdictClass c) => c is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow;

    /// <summary>The ratchet: a red cell has its row, a row has its red cell of that class and cite, and python or the harness
    /// failing to judge a cell is never a row's to hold.</summary>
    internal static List<string> RatchetFailures(IReadOnlyList<Result> results, IReadOnlyDictionary<string, InitializerDependencyDifferentialTests.AllowRow> rows)
    {
        var failures = new List<string>();
        foreach (var r in results)
        {
            rows.TryGetValue(r.Case.Cell.Id, out var row);
            if (r.PythonFailure != null)
                failures.Add($"GeneratorBug {r.Case.Cell.Id}: {r.PythonFailure}{(row == null ? "" : " (and a row lists it: not allowlistable)")}");
            else if (r.Class == VerdictClass.Harness)
                failures.Add($"Harness {r.Case.Cell.Id}: Sharpy {Describe(r)}");
            else if (IsRed(r.Class) && row == null)
                failures.Add($"{r.Class} {r.Case.Cell.Id} ({r.Case.Cell.Tag}, {r.Case.Cell.Rule}): Sharpy {Describe(r)}; model '{r.Case.Cell.Model}'");
            else if (IsRed(r.Class) && row!.Class != r.Class)
                failures.Add($"{r.Case.Cell.Id}: listed as {row.Class}, now {r.Class}: MOVED, update or delete the row");
            else if (!IsRed(r.Class) && row != null)
                failures.Add($"{r.Case.Cell.Id}: listed as {row.Class}, now {r.Class}: STALE, delete the row");
            else if (IsRed(r.Class) && row!.Cite != ProposedCite(r))
                failures.Add($"{r.Case.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r)}'");
        }
        return failures;
    }

    private static string Describe(Result r)
        => r.Kind == SharpyKind.Ran
            ? $"Ran '{Harness.Normalize(r.Run.Stdout)}'"
            : $"{r.Kind} [{string.Join(",", r.Run.AllCodes.Distinct())}{(r.Run.CsCode != null ? " " + r.Run.CsCode : "")}]"
                + (r.Kind == SharpyKind.RuntimeThrow ? $" stderr '{r.Run.Stderr.TrimStart().Split('\n', 2)[0].TrimEnd()}'" : "");

    private bool _currentCellNeedsStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
    {
        if (_currentCellNeedsStdlib)
            yield return SharpyStdlibReference.Location;
    }

    private Harness.SharpyRun RunSharpy(NameCell cell, int timeoutMs)
    {
        _currentCellNeedsStdlib = cell.NeedsStdlib;
        var r = CompileAndExecute(cell.Source, "scope_meaning.spy", executionTimeoutMs: timeoutMs);
        var diagnostics = r.RawDiagnostics;
        var all = diagnostics.Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var errors = diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var cs = diagnostics.Where(d => d.Code == "SPY0908").Select(d => Regex.Match(d.Message, @"\bCS\d{4}\b")).Where(m => m.Success).Select(m => m.Value).FirstOrDefault();
        return new Harness.SharpyRun(r.Success, r.ExitCode, r.Exception != null, r.TimedOut, r.StandardOutput, r.StandardError, errors, all, cs);
    }

    [SkippableFact]
    public void ScopeMeaning_Sweep()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
        var wall = Stopwatch.StartNew();
        var pool = GeneratedCases();
        var failures = TotalityFailures(pool);

        // --- Python arm: one batch over the cells python runs with the same meaning. ---
        var requests = new List<PyRequest>();
        var twinFailures = new Dictionary<int, string>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Twin)
                continue;
            try
            {
                requests.Add(new PyRequest(i, TwinRenderer.Render(pool[i].Cell.Source)));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                twinFailures[i] = $"renderer: {ex.Message}";
            }
        }
        var pythonById = oracle.RunBatch(requests, PythonBatchTimeoutMs);

        // --- Sharpy arm: production compile + execute, `run` and never `emit`. ---
        var results = new List<Result>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            var c = pool[i];
            string? pythonFailure = twinFailures.GetValueOrDefault(i);
            if (pythonFailure == null && c.Twin)
            {
                var python = pythonById.TryGetValue(i, out var verdict) ? Harness.ClassifyPython(verdict) : new PyOutcome(PyKind.SyntaxError, "no verdict from the runner");
                if (python != c.Cell.Python)
                    pythonFailure = $"the cell is modelled to print '{c.Cell.Model}' and python {python}";
            }
            var run = RunSharpy(c.Cell, SharpyExecTimeoutMs);
            if (run.TimedOut)
                run = RunSharpy(c.Cell, SharpyExecTimeoutMs * 2);
            var kind = Harness.ClassifySharpy(run);
            var (cls, wrongCode) = Judge(c.Cell, kind, run);
            results.Add(new Result(c, run, kind, cls, wrongCode, pythonFailure));
        }

        // --- Ratchet. ---
        var universe = pool.Select(c => c.Cell.Id).ToHashSet(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        var rows = InitializerDependencyDifferentialTests.ParseAllowlist(path == null ? Array.Empty<string>() : File.ReadAllLines(path), universe, failures);
        failures.AddRange(RatchetFailures(results, rows));

        wall.Stop();
        var red = results.Where(r => IsRed(r.Class)).ToList();
        var dir = ReportDirectory();
        using (var writer = new StreamWriter(Path.Combine(dir, "scope-meaning-differential-cells.jsonl")))
        {
            foreach (var r in results)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    id = r.Case.Cell.Id,
                    tag = r.Case.Cell.Tag.ToString(),
                    rule = r.Case.Cell.Rule,
                    verdict = r.Class.ToString(),
                    wrongCode = r.WrongCode,
                    twin = r.Case.Twin,
                    python = r.PythonFailure,
                    sharpy = Describe(r),
                    source = r.Case.Cell.Source,
                }));
            }
        }
        File.WriteAllLines(
            Path.Combine(dir, "scope-meaning-differential-proposed-allowlist.txt"),
            red.OrderBy(r => r.Case.Cell.Id, StringComparer.Ordinal).Select(r => $"{r.Case.Cell.Id} {r.Class} # {ProposedCite(r)}"));

        var report = $"scope-meaning-differential: pool {pool.Count} ({pool.Count(c => c.Cell.Tag == SharpyTag.MustRun)} must-run, {pool.Count(c => c.Cell.Tag == SharpyTag.MustRefuse)} must-refuse), "
            + $"python agreed {results.Count(r => r.Case.Twin && r.PythonFailure == null)}/{pool.Count(c => c.Twin)}, red {red.Count}, allowlist {rows.Count}, wall {wall.Elapsed.TotalSeconds:F1}s. "
            + string.Join(" ", results.GroupBy(r => r.Case.Cell.Key!.Route).Select(g => $"{g.Key}={g.Count()}/red {g.Count(r => IsRed(r.Class))}"));
        Output.WriteLine(report);
        Assert.True(failures.Count == 0, $"{failures.Count} failures. {report}\n{string.Join("\n", failures.Take(120))}");
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }

    // ------------------------------------------------------------------------------------------- //
    // The ratchet's own fact: plain data in, no compile
    // ------------------------------------------------------------------------------------------- //

    [Fact]
    public void Ratchet_ARedCell_NeedsItsRow_AndARowNeedsItsRedCell()
    {
        var pool = GeneratedCases();
        var running = pool.First(c => c.Cell.Tag == SharpyTag.MustRun && c.Cell.Key!.Route == "callee");
        var refusing = pool.First(c => c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.Key!.Route == "typed-read");
        static Harness.SharpyRun Ran(string stdout) => new(true, 0, false, false, stdout, "", Array.Empty<string>(), Array.Empty<string>(), null);
        static Harness.SharpyRun Refused(string code) => new(false, null, false, false, "", "", new[] { code }, new[] { code }, null);
        Result Of(Case c, Harness.SharpyRun run, string? pythonFailure = null)
        {
            var kind = Harness.ClassifySharpy(run);
            var (cls, wrongCode) = Judge(c.Cell, kind, run);
            return new Result(c, run, kind, cls, wrongCode, pythonFailure);
        }
        var none = new Dictionary<string, InitializerDependencyDifferentialTests.AllowRow>();
        var listed = new Dictionary<string, InitializerDependencyDifferentialTests.AllowRow> { [running.Cell.Id] = new(VerdictClass.SilentWrong, "#2423 silent-wrong") };
        var wrong = new Dictionary<string, InitializerDependencyDifferentialTests.AllowRow> { [refusing.Cell.Id] = new(VerdictClass.FalseRefusal, "#2423 wrong-code") };

        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("1")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("12")) }, listed));
        Assert.Empty(RatchetFailures(new[] { Of(refusing, Refused("SPY0220")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(refusing, Refused("SPY0200")) }, wrong));

        Assert.Contains("model", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1")) }, listed)));
        Assert.Contains("MOVED", Assert.Single(RatchetFailures(new[] { Of(running, Refused("SPY0200")) }, listed)));
        Assert.Contains("FalseRefusal", Assert.Single(RatchetFailures(new[] { Of(refusing, Refused("SPY0200")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(refusing, Refused("SPY0220")) }, wrong)));
        Assert.Contains("the route says", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) },
            new Dictionary<string, InitializerDependencyDifferentialTests.AllowRow> { [running.Cell.Id] = new(VerdictClass.SilentWrong, "#2432 silent-wrong") })));
        Assert.Contains("GeneratorBug", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1"), "python 4") }, none)));
    }
}

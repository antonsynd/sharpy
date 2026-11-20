using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic.Validation;
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

/// <summary>What a constant position is handed (#2432's expression-kind axis; charter §3.20l calls 43 and 45; the
/// owner's ruling 7 of 2026-11-20, #2423 comment 7042723494): every kind of member a constant position's answer is
/// asked about, and the two chains. Each member kind is the image of what the compiler records for the member:
/// <list type="bullet">
/// <item>a value of a referenced MODULE: discovery's <c>FieldSignature.IsConst</c> × <c>IsGuarded</c>
/// (<see cref="ScopeMeaningDifferentialTests.ModuleValueShapes"/>);</item>
/// <item>a member of a referenced TYPE: the subclasses of <c>ClrMemberResolution</c>, a field refined by
/// <c>FieldInfo.IsLiteral</c> and whether its type is an enum (<see cref="ScopeMeaningDifferentialTests.ClrTypeMemberShapes"/>);</item>
/// <item>a member of an enum the program declares: <c>EnumMemberAccessKind</c> and the member itself, per enum shape
/// (<see cref="ScopeMeaningDifferentialTests.EnumReadKinds"/>);</item>
/// <item>any other name the program declares: <see cref="OwnMember"/>, refined by <see cref="BindingKind"/>
/// (<see cref="ScopeMeaningDifferentialTests.OwnReads"/>).</item>
/// </list>
/// The two chains are closed by call 45.</summary>
internal enum ScopeConstantKind
{
    /// <summary>A constant of a referenced assembly: a CLR literal field read through a Stdlib module.</summary>
    ClrLiteralField,

    /// <summary>A module variable of the <c>.spy</c> stdlib, whose storage is guarded (N3): no constant.</summary>
    StdlibModuleVariable,

    /// <summary>A static of a referenced assembly's module that is neither literal nor guarded (a static property, a
    /// static readonly field): no constant.</summary>
    ClrStaticMember,

    /// <summary>A literal field of a referenced type (<c>int.max_value</c>, <c>Math.PI</c>): a C# constant.</summary>
    ClrTypeLiteralField,

    /// <summary>A member of a referenced enum (<c>DayOfWeek.Monday</c>): a C# constant.</summary>
    ClrTypeEnumMember,

    /// <summary>A static field of a referenced type that is no literal (<c>static readonly</c>): no constant.</summary>
    ClrTypeStaticField,

    /// <summary>A static property of a referenced type: no constant.</summary>
    ClrTypeStaticProperty,

    /// <summary>A static method of a referenced type with one overload, read uncalled: a function value, no constant.</summary>
    ClrTypeMethod,

    /// <summary>A static method group of a referenced type, read uncalled: no value at all (SPY0336).</summary>
    ClrTypeMethodGroup,

    /// <summary>A member of an int enum of the program: a C# constant.</summary>
    IntEnumMember,

    /// <summary>An int enum member's <c>.value</c>: the underlying integer, a C# constant (ruling 2).</summary>
    IntEnumValue,

    /// <summary>An int enum member's <c>.name</c>: a call of the name channel, no constant.</summary>
    IntEnumName,

    /// <summary>A member of a string enum of the program: a singleton object, no C# constant.</summary>
    StringEnumMember,

    /// <summary>A string enum member's <c>.value</c>: the singleton's property, no constant.</summary>
    StringEnumValue,

    /// <summary>A string enum member's <c>.name</c>: the singleton's property, no constant.</summary>
    StringEnumName,

    /// <summary>A name the program declares, read where its kind says (bare at module level, through its type's
    /// name or a subclass's): the cell's target is the kind.</summary>
    OwnMember,

    /// <summary>A comparison chain whose every link is a constant.</summary>
    ComparisonChain,

    /// <summary>A comparison chain with a link that is no constant.</summary>
    ChainWithNonConstantLink,
}

/// <summary>What a type alias's target names: a type that exists, nothing, or the alias itself, directly or
/// through a ring of aliases. Closed.</summary>
internal enum ScopeAliasTarget
{
    Declared,
    Missing,
    Itself,
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
/// Stage 0 of plan-777361 (P78; verification contract §11), the owner's rulings of 2026-11-20 (#2423 comments
/// 7030074628 item 1, 7039834146 item 5 and 7042723494 items 7 to 9): the cells of rules N1, N2, N4 and N7 that the P77a pool
/// (<see cref="NameResolutionDifferentialTests"/>) has no program for, as a generated test type of their own. A cell is
/// a <see cref="NameCell"/>, and its tag and rule are READ from the rule table (<c>name-resolution-rules.txt</c>, #2487):
/// the generator writes the program, what it prints when it runs and the codes a refusal may carry, and never a verdict.
///
/// Six cell sets. Every axis is the image of a compiler enumeration read by reflection, a census of the compiler's own
/// output, the P77a pool, or a set named closed; every value is generated or excluded with its reason
/// (<see cref="Axes_EveryValueIsGeneratedOrExcluded"/>), and an exclusion that rests on what the compiler does today
/// has a probe that goes red when it stops (<see cref="Exclusions_RestingOnTheCompiler_GoRedWhenItChanges"/>). The
/// axes and their sources are in <c>ScopeMeaningDifferentialTests.Axes.cs</c>:
/// <list type="bullet">
/// <item><c>no-initializer</c> (R-QI): <see cref="BindingKind"/> × <see cref="ScopeInitializer"/> × host
/// (<c>TypeKind</c>) × declared type (<c>SemanticType</c>'s subclasses);</item>
/// <item><c>typed-read</c>: a read handed to a <c>bool</c> slot, <see cref="BindingKind"/> × <see cref="ScopeAnnotation"/>
/// × order × <c>Spelling</c> × <see cref="ReadPosition"/>, the last two as the P77a pool runs them;</item>
/// <item><c>callee</c>: a binder's name called under an outer binding of the name, binder (<see cref="LocalTarget"/> and
/// the parameter) × outer (<see cref="BindingKind"/>);</item>
/// <item><c>constant-kind</c> (#2432): host (the call sites of <c>ConstantDefaultClassifier.IsAdmitted</c>, each with its
/// <c>AdmissionTable</c>, and the enum member's value) × <see cref="ScopeConstantKind"/>;</item>
/// <item><c>init-overload</c>: <see cref="ScopeOverloadCaller"/> × order;</item>
/// <item><c>alias-target</c>: declaration site (<c>ScopeKind</c> × <c>TypeKind</c>) × target shape (the properties of the
/// alias's AST records) × <see cref="ScopeAliasTarget"/> × <see cref="ScopeAliasUse"/> × order. An alias that reaches
/// itself has its unused cells, which must run, and no used one (#2769).</item>
/// </list>
/// Known-red cells are rows of <c>scope-meaning-differential-allowlist.txt</c> (a missing file reads as empty), each citing
/// the issue whose fix turns it green. A must-refuse cell refused under a code its cell does not admit is listed as
/// <see cref="VerdictClass.FalseRefusal"/> with the lane <c>wrong-code</c>: the refusal is another rule's.
/// </summary>
[Trait("Category", "GapDiscovery")]
public sealed partial class ScopeMeaningDifferentialTests : IntegrationTestBase
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

    private static string Indent(string text) => string.Join("\n", text.Split('\n').Select(l => "    " + l));

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

    // ------------------------------------------------------------------------------------------- //
    // no-initializer (R-QI)
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> NoInitializerCases()
    {
        foreach (var (kind, d) in Declarations.OrderBy(e => e.Key))
        {
            foreach (var host in d.InType ? DeclarationHosts.Values.SelectMany(h => h).ToArray() : new string?[] { null })
            {
                foreach (var type in ValueTypes.OrderBy(e => e.Key.Name, StringComparer.Ordinal).SelectMany(e => e.Value))
                {
                    foreach (var initializer in Enum.GetValues<ScopeInitializer>())
                    {
                        if (initializer == ScopeInitializer.AccessorBodies && d.Accessor == null)
                            continue;
                        // A struct's instance field initializer is a constant position (the 8951 pool's StructFieldInitializer):
                        // a declared type whose value is no constant has no initializer to write there.
                        if (initializer == ScopeInitializer.Present && kind == BindingKind.InstanceField && host == "struct" && !type.Constant)
                            continue;
                        string F(string text) => text.Replace("{T}", type.Spelling, StringComparison.Ordinal).Replace("{L}", type.Literal, StringComparison.Ordinal);
                        string decl = initializer switch
                        {
                            ScopeInitializer.Present => F(d.Text) + " = " + type.Literal,
                            ScopeInitializer.Absent => F(d.Text),
                            _ => F(d.Accessor!),
                        };
                        var lines = new List<string>();
                        if (type.Prelude != null)
                            lines.Add(type.Prelude);
                        // The read lands in a local of the declared type before it is shown: a member of an untyped read is #2417's.
                        var print = $"    v: {type.Spelling} = {d.Read}\n    print({string.Format(System.Globalization.CultureInfo.InvariantCulture, type.Show, "v")})";
                        if (kind == BindingKind.LocalVar)
                        {
                            lines.Add(Main.TrimEnd());
                            lines.Add(Indent(decl));
                            if (initializer == ScopeInitializer.Absent)
                                lines.AddRange(d.Stored!.Select(s => Indent(F(s))));
                            lines.Add(print);
                        }
                        else
                        {
                            if (d.InType)
                            {
                                lines.Add($"{host} H:");
                                lines.Add(Indent(decl));
                                if (initializer == ScopeInitializer.Absent && d.Stored != null)
                                    lines.AddRange(d.Stored.Select(s => Indent(F(s))));
                            }
                            else
                                lines.Add(decl);
                            lines.Add(Main.TrimEnd());
                            lines.Add(print);
                        }
                        // Read where R-QI refuses; the kinds it leaves alone store first, so every cell that runs prints the stored value.
                        bool twin = d.Twin && type.Twin && host is null or "class"
                            && (initializer == ScopeInitializer.Present || (initializer == ScopeInitializer.Absent && d.Stored != null));
                        yield return Draft(Key("read", "no-initializer", kind, initializer, host, type.Label), Family.Read, Lines(lines), "5",
                            Array.Empty<string>(), twin, target: kind);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // typed-read: a read handed to a bool slot is the type mismatch, whatever the order, spelling and position
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The probe: a function that takes a <c>bool</c>. Handed an <c>int</c> the checker has typed, the call is
    /// SPY0220; handed a read it has not typed, it is C#'s CS1503 behind SPY0908 (measured @ 55846bb3f).</summary>
    private const string Probe = "def nb(v: bool) -> int:\n    return 1";

    private const string Five = "def five() -> int:\n    return 5";

    /// <summary>The reader of a constant-only position: its slot is declared <c>bool</c>, since a call is no constant there.</summary>
    private static string[] BoolSlot(ReadPosition p, string read) => p switch
    {
        ReadPosition.ParameterDefault => new[] { $"def r(self, p0: bool = {read}) -> int:", "    return 1" },
        ReadPosition.DataclassFieldInitializer or ReadPosition.StructFieldInitializer => new[] { $"r: bool = {read}" },
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, "not a constant-only position"),
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
                foreach (var reader in TypedReaders.Where(r => r.Kind == kind))
                {
                    foreach (var order in new[] { Harness.Order.Earlier, Harness.Order.Later })
                    {
                        string receiver = reader.Spelling switch
                        {
                            Harness.Spelling.Bare => "",
                            Harness.Spelling.SelfDot => "self.",
                            Harness.Spelling.Alias => t.Level == MemberLevel.Instance ? "A()." : "A.",
                            Harness.Spelling.Qualified => t.Level == MemberLevel.Instance ? "H()." : "H.",
                            _ => throw new ArgumentOutOfRangeException(nameof(reader), reader.Spelling, "no receiver"),
                        };
                        string read = receiver + t.Member;
                        bool constantOnly = Harness.IsConstantOnly(reader.Position);
                        string probed = constantOnly ? read : $"nb({read})";
                        var prelude = new List<string> { Probe };
                        if (t.NeedsFive)
                            prelude.Add(Five);
                        var body = new List<string>();
                        if (reader.Inside)
                        {
                            // The reader is a sibling of the member, in the member's own type.
                            var members = constantOnly ? BoolSlot(reader.Position, probed) : Harness.PlaceInClass(reader.Position, reader.Nesting, probed).Members.ToArray();
                            var member = text.Split('\n');
                            body.Add(Harness.HostHeader(reader.Position));
                            body.AddRange((order == Harness.Order.Earlier ? member.Concat(members) : members.Concat(member)).Select(l => "    " + l));
                            if (reader.Spelling == Harness.Spelling.Alias)
                                body.Add("type A = H");
                        }
                        else
                        {
                            var target = new List<string>();
                            if (t.Level == MemberLevel.Module)
                                target.Add(text);
                            else
                            {
                                target.Add("class H:\n" + Indent(text));
                                if (reader.Spelling == Harness.Spelling.Alias)
                                    target.Add("type A = H");
                            }
                            var host = new List<string>();
                            if (constantOnly)
                            {
                                host.Add(Harness.HostHeader(reader.Position).Replace("H:", "Rd:", StringComparison.Ordinal));
                                host.AddRange(BoolSlot(reader.Position, probed).Select(l => "    " + l));
                            }
                            else if (ModulePlacedPositions.Contains(reader.Position))
                                host.AddRange(Harness.PlaceAtModule(reader.Position, reader.Nesting, probed).Lines);
                            else
                            {
                                host.Add(Harness.HostHeader(reader.Position).Replace("H:", "Rd:", StringComparison.Ordinal));
                                host.AddRange(Harness.PlaceInClass(reader.Position, reader.Nesting, probed).Members.Select(l => "    " + l));
                            }
                            body.AddRange(order == Harness.Order.Earlier ? target.Concat(host) : host.Concat(target));
                        }
                        var lines = prelude.Concat(body).ToList();
                        if (string.Join("\n", body).Contains("ap(lambda", StringComparison.Ordinal))
                            lines.Insert(0, "def ap(f: () -> int) -> int:\n    return f()");
                        lines.Add(Main + "    print(1)");
                        yield return Draft(Key("read", "typed-read", kind, annotation, order, reader.Spelling, reader.Position, reader.Nesting), Family.Read, Lines(lines), null,
                            new[] { "SPY0220" }, twin: false, target: kind, position: reader.Position);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // callee: the innermost binding of a name is what a call of it calls
    // ------------------------------------------------------------------------------------------- //

    private const string One = "def one() -> int:\n    return 1";
    private const string Twelve = "def twelve() -> int:\n    return 12";
    private const string CalleeCm = "class CM:\n    def __enter__(self) -> () -> int:\n        return one\n    def __exit__(self) -> None:\n        pass";

    private static IEnumerable<Case> CalleeCases()
    {
        foreach (var (outer, o) in CalleeOuters.OrderBy(e => e.Key))
        {
            // The binders: a parameter, which is no LocalTarget, and each construct target.
            foreach (var binder in new object[] { BindingKind.Parameter }.Concat(CalleeBinders.Keys.OrderBy(k => k).Cast<object>()))
            {
                string n = o.Name;
                bool parameter = binder is BindingKind;
                // The statements that bind n to `one` and print its call; a parameter binder's function is declared below.
                string[] stmts = parameter ? new[] { "print(f(one))" } : CalleeBinders[(LocalTarget)binder].Select(s => s.Replace("{n}", n, StringComparison.Ordinal)).ToArray();
                var lines = new List<string> { One };
                if (o.Text.Contains("twelve()", StringComparison.Ordinal))
                    lines.Add(Twelve);
                if (binder is LocalTarget.WithAs)
                    lines.Add(CalleeCm);
                string call;
                switch (o.Scope)
                {
                    case OuterScope.Module:
                        if (o.Text.Length > 0)
                            lines.Insert(0, o.Text);
                        if (parameter)
                            lines.Add($"def f({n}: () -> int) -> int:\n    return {n}()");
                        lines.Add(Main + string.Join("\n", stmts.Select(s => "    " + s)));
                        break;
                    case OuterScope.Member:
                        // The outer binding is a member of H, and the binder sits in a method of H.
                        lines.Add("class H:\n" + Indent(o.Text) + "\n"
                            + (parameter
                                ? $"    def m(self, {n}: () -> int) -> int:\n        return {n}()"
                                : "    def m(self) -> None:\n" + string.Join("\n", stmts.Select(s => "        " + s))));
                        call = parameter ? "print(H().m(one))" : "H().m()";
                        lines.Add(Main + "    " + call);
                        break;
                    default:
                        // The outer binding is a local of the enclosing function, and the binder sits in a nested def.
                        string signature = outer == BindingKind.Parameter ? $"def outer0({n}: int) -> None:" : "def outer0() -> None:";
                        var enclosing = new List<string> { signature };
                        if (outer != BindingKind.Parameter)
                            enclosing.Add(Indent(o.Text));
                        enclosing.Add(parameter
                            ? $"    def g({n}: () -> int) -> int:\n        return {n}()\n    print(g(one))"
                            : "    def g() -> None:\n" + string.Join("\n", stmts.Select(s => "        " + s)) + "\n    g()");
                        lines.Add(string.Join("\n", enclosing));
                        lines.Add(Main + (outer == BindingKind.Parameter ? "    outer0(12)" : "    outer0()"));
                        break;
                }
                var construct = binder as LocalTarget?;
                yield return Draft(Key("local", "callee", binder, $"outer={outer}"), Family.Local, Lines(lines), "1", Array.Empty<string>(),
                    twin: o.Twin && binder is BindingKind or LocalTarget.For or LocalTarget.Comprehension,
                    target: parameter ? BindingKind.Parameter : construct == LocalTarget.Comprehension ? BindingKind.Inner : BindingKind.LocalVar,
                    outer: outer, position: ReadPosition.FunctionBody, construct: construct, stdlib: o.Stdlib);
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // constant-kind (#2432)
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<Case> ConstantKindCases()
    {
        foreach (var (table, host) in AllConstantHosts())
        {
            foreach (var (kind, expressions) in ConstantExpressions.OrderBy(e => e.Key))
            {
                foreach (var e in expressions)
                {
                    if (HostSkips(host, e.Type) != null)
                        continue;
                    yield return ConstantCase(Key("read", "constant-kind", table, host.Label, kind, e.Form, e.Label), table, host, kind, e.Text, e.Type, e.Prints, e.Show,
                        imports: e.Import, prelude: e.Prelude, refusals: e.Refusals, target: e.Target, stdlib: e.Stdlib,
                        twin: host.Twin && e.Target == BindingKind.None && kind is ScopeConstantKind.ClrLiteralField or ScopeConstantKind.ComparisonChain);
                }
            }
            foreach (var own in OwnReads)
            {
                if (HostSkips(host, own.Type) != null)
                    continue;
                yield return ConstantCase(Key("read", "constant-kind", table, host.Label, ScopeConstantKind.OwnMember, own.Kind, own.Annotation, own.Relation, own.Form), table, host,
                    ScopeConstantKind.OwnMember, own.Text, own.Type, "5", own.Show, imports: null, prelude: own.Prelude, refusals: own.Refusals, target: own.Kind, stdlib: false, twin: false);
            }
        }
    }

    /// <summary>One cell: the expression's imports and declarations, then the host's program around it. What <c>main</c> shows is
    /// the slot's value read into a local of the slot's type first, since a call on a function-typed property is #2770's.</summary>
    private static Case ConstantCase(CellKey key, AdmissionTable? table, ConstantHost host, ScopeConstantKind kind, string text, string type, string prints, string show,
        string? imports, string? prelude, string[]? refusals, BindingKind target, bool stdlib, bool twin)
    {
        var lines = new List<string>();
        if (host.Imports != null)
            lines.Add(host.Imports);
        if (imports != null)
            lines.Add(imports);
        if (prelude != null)
            lines.Add(prelude);
        lines.Add(host.Program.Replace("{T}", type, StringComparison.Ordinal).Replace("{e}", text, StringComparison.Ordinal)
            .Replace("{s}", string.Format(System.Globalization.CultureInfo.InvariantCulture, show, "v"), StringComparison.Ordinal));
        return Draft(key, Family.Read, Lines(lines), host.PrintsOne ? "1" : prints, new[] { host.Refusal }.Concat(refusals ?? Array.Empty<string>()).ToArray(), twin,
            target: target, position: host.Position, stdlib: stdlib);
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
    // alias-target: an alias names a type that exists, used or not, wherever it is declared and whatever its shape
    // ------------------------------------------------------------------------------------------- //

    private const string AliasTargetClass = "class C0:\n    v: int = 1";

    /// <summary>The orders an alias has against what its target names: a declared type stands above or below it, a missing one
    /// has no declaration to stand anywhere, and the alias itself is reached directly or through a ring.</summary>
    internal static Harness.Order[] AliasOrders(ScopeAliasTarget target) => target switch
    {
        ScopeAliasTarget.Declared => new[] { Harness.Order.Earlier, Harness.Order.Later },
        ScopeAliasTarget.Missing => new[] { Harness.Order.Earlier },
        _ => new[] { Harness.Order.Self, Harness.Order.Cycle },
    };

    private static IEnumerable<Case> AliasTargetCases()
    {
        foreach (var site in AllAliasSites())
        {
            foreach (var shape in AliasShapes.Values.DistinctBy(s => s.Label).OrderBy(s => s.Label, StringComparer.Ordinal))
            {
                foreach (var target in Enum.GetValues<ScopeAliasTarget>())
                {
                    // A name a module qualifies is never the alias (ExcludedSelfReachingShapes says so, and the axes fact holds the two together).
                    if (target == ScopeAliasTarget.Itself && shape.Import != null)
                        continue;
                    foreach (var use in Enum.GetValues<ScopeAliasUse>())
                    {
                        // Named by an annotation, a self-reaching alias kills the compiler's process (#2769; ExcludedAliasUses).
                        if (target == ScopeAliasTarget.Itself && use == ScopeAliasUse.Used)
                            continue;
                        foreach (var order in AliasOrders(target))
                        {
                            string generic = shape.Generic ? "[T]" : "";
                            // What the target names: the program's class, nothing, the alias itself, or the next alias of a ring of two.
                            string named = target switch
                            {
                                ScopeAliasTarget.Declared => shape.Declared,
                                ScopeAliasTarget.Missing => "nosuch",
                                _ when order == Harness.Order.Self => "K0" + generic,
                                _ => "K1",
                            };
                            var aliases = new List<string> { $"type K0{generic} = " + shape.Target.Replace("{N}", named, StringComparison.Ordinal) };
                            if (target == ScopeAliasTarget.Itself && order == Harness.Order.Cycle)
                                aliases.Add("type K1 = K0" + (shape.Generic ? "[int]" : ""));
                            var lines = new List<string>();
                            if (shape.Import != null)
                                lines.Add($"import {shape.Import}");
                            if (target == ScopeAliasTarget.Declared && order == Harness.Order.Earlier)
                                lines.Add(AliasTargetClass);
                            // Every alias of the site stands at the site's own indentation.
                            var slot = site.Program.Split('\n').First(l => l.Contains("{alias}", StringComparison.Ordinal));
                            var indent = slot[..slot.IndexOf("{alias}", StringComparison.Ordinal)];
                            lines.Add(site.Program
                                .Replace("{alias}", string.Join("\n" + indent, aliases), StringComparison.Ordinal)
                                .Replace("{use}", use == ScopeAliasUse.Used ? site.Use.Replace("{K}", shape.Generic ? "K0[int]" : "K0", StringComparison.Ordinal) : site.NoUse, StringComparison.Ordinal));
                            if (target == ScopeAliasTarget.Declared && order == Harness.Order.Later)
                                lines.Add(AliasTargetClass);
                            lines.Add(Main + "    print(1)");
                            yield return Draft(Key("read", "alias-target", target, use, order, site.Label, shape.Label), Family.Read, Lines(lines), "1",
                                new[] { "SPY0202" }, twin: false, target: BindingKind.ModuleTypeAlias, position: ReadPosition.TypeAliasTarget, stdlib: shape.Import != null);
                        }
                    }
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
            if (c.Cell.Id.Any(char.IsWhiteSpace))
                failures.Add($"'{c.Cell.Id}': an id is one word, since an allowlist row is `<id> <class> # <cite>`");
        }
        failures.AddRange(Harness.RuleTableRowFailures(rows, Harness.RuleTablePools.SelectMany(p => p.Cells()).ToList()));
        return failures.Count > 60 ? failures.Take(60).Append($"… {failures.Count} failures in all").ToList() : failures;
    }

    [Fact]
    public void RuleTable_EveryCaseHasOneRow_AndTakesItsVerdictFromIt()
    {
        var failures = TotalityFailures(GeneratedCases());
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ------------------------------------------------------------------------------------------- //
    // Judge, route, ratchet
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The issue whose fix turns a red cell of a route green: #2432 for a constant position's verdict, #2423 for the rest
    /// (R-QI's cells, the untyped forward read, the callee lookup, the alias refusal: charter §3.20l). A typed read that is
    /// red at the EARLIER order is no order defect, and it and its later twin are #2417's; an alias that reaches itself is
    /// #2769's (<see cref="ProposedCite"/>).</summary>
    internal static readonly IReadOnlyDictionary<string, string> RouteIssues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["no-initializer"] = "#2423",
        ["typed-read"] = "#2423",
        ["callee"] = "#2423",
        ["constant-kind"] = "#2432",
        ["init-overload"] = "#2423",
        ["alias-target"] = "#2423",
    };

    /// <summary>The class of an untyped member-access read (#2417): typed at neither order.</summary>
    internal const string UntypedAtEitherOrder = "#2417";

    /// <summary>The class of an alias whose target reaches the alias itself (#2769): unruled, so the unused cells keep running
    /// and a red one is that tracker's.</summary>
    internal const string SelfReachingAlias = "#2769";

    internal sealed record Result(Case Case, Harness.SharpyRun Run, SharpyKind Kind, VerdictClass Class, bool WrongCode, string? PythonFailure);

    /// <summary>The harness's table, and one more red: a must-refuse cell refused under a code it does not admit.</summary>
    internal static (VerdictClass Class, bool WrongCode) Judge(NameCell cell, SharpyKind kind, Harness.SharpyRun run)
    {
        var verdict = Harness.Judge(cell, kind, run.Stdout);
        bool wrongCode = verdict == VerdictClass.Match && cell.Tag == SharpyTag.MustRefuse && Harness.RefusalReasonFailure(cell, run) != null;
        return (wrongCode ? VerdictClass.FalseRefusal : verdict, wrongCode);
    }

    /// <summary>The id of a typed-read cell's twin at the earlier order, or null for a cell that has none.</summary>
    internal static string? EarlierTwinId(NameCell cell)
    {
        if (cell.Key is not { Route: "typed-read" } key || key.Part<Harness.Order>() is not { } order)
            return null;
        return order == Harness.Order.Earlier ? cell.Id : Key(key.Parts.Select(p => p is Harness.Order ? Harness.Order.Earlier : p).ToArray()).Text;
    }

    /// <summary>The cite a red cell's row must carry. <paramref name="redIds"/> are the ids red in the same run: a typed read
    /// whose earlier twin is red is untyped whatever the order, so declaring before reading does not cure it and it is #2417's.</summary>
    internal static string ProposedCite(Result r, IReadOnlySet<string> redIds)
    {
        var issue = RouteIssues.GetValueOrDefault(r.Case.Cell.Key!.Route ?? "", "UNROUTED");
        if (EarlierTwinId(r.Case.Cell) is { } earlier && redIds.Contains(earlier))
            issue = UntypedAtEitherOrder;
        if (r.Case.Cell.Key.Part<ScopeAliasTarget>() == ScopeAliasTarget.Itself)
            issue = SelfReachingAlias;
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
        var redIds = results.Where(r => IsRed(r.Class)).Select(r => r.Case.Cell.Id).ToHashSet(StringComparer.Ordinal);
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
            else if (IsRed(r.Class) && row!.Cite != ProposedCite(r, redIds))
                failures.Add($"{r.Case.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r, redIds)}'");
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

    private Harness.SharpyRun RunSharpy(string source, bool needsStdlib, int timeoutMs)
    {
        _currentCellNeedsStdlib = needsStdlib;
        var r = CompileAndExecute(source, "scope_meaning.spy", executionTimeoutMs: timeoutMs);
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
            var run = RunSharpy(c.Cell.Source, c.Cell.NeedsStdlib, SharpyExecTimeoutMs);
            if (run.TimedOut)
                run = RunSharpy(c.Cell.Source, c.Cell.NeedsStdlib, SharpyExecTimeoutMs * 2);
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
        var redIds = red.Select(r => r.Case.Cell.Id).ToHashSet(StringComparer.Ordinal);
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
            red.OrderBy(r => r.Case.Cell.Id, StringComparer.Ordinal).Select(r => $"{r.Case.Cell.Id} {r.Class} # {ProposedCite(r, redIds)}"));

        var report = $"scope-meaning-differential: pool {pool.Count} ({pool.Count(c => c.Cell.Tag == SharpyTag.MustRun)} must-run, {pool.Count(c => c.Cell.Tag == SharpyTag.MustRefuse)} must-refuse), "
            + $"python agreed {results.Count(r => r.Case.Twin && r.PythonFailure == null)}/{pool.Count(c => c.Twin)}, red {red.Count}, allowlist {rows.Count}, wall {wall.Elapsed.TotalSeconds:F1}s. "
            + string.Join(" ", results.GroupBy(r => r.Case.Cell.Key!.Route).Select(g => $"{g.Key}={g.Count()}/red {g.Count(r => IsRed(r.Class))}"))
            + " cites: " + string.Join(" ", rows.Values.GroupBy(r => r.Cite.Split(' ')[0]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}={g.Count()}"));
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
        var later = pool.First(c => c.Cell.Tag == SharpyTag.MustRefuse && c.Cell.Key!.Route == "typed-read" && c.Cell.Key.Part<Harness.Order>() == Harness.Order.Later);
        var earlier = pool.Single(c => c.Cell.Id == EarlierTwinId(later.Cell));
        static Harness.SharpyRun Ran(string stdout) => new(true, 0, false, false, stdout, "", Array.Empty<string>(), Array.Empty<string>(), null);
        static Harness.SharpyRun Refused(string code) => new(false, null, false, false, "", "", new[] { code }, new[] { code }, null);
        Result Of(Case c, Harness.SharpyRun run, string? pythonFailure = null)
        {
            var kind = Harness.ClassifySharpy(run);
            var (cls, wrongCode) = Judge(c.Cell, kind, run);
            return new Result(c, run, kind, cls, wrongCode, pythonFailure);
        }
        static Dictionary<string, InitializerDependencyDifferentialTests.AllowRow> Rows(params (Case Case, VerdictClass Class, string Cite)[] rows)
            => rows.ToDictionary(r => r.Case.Cell.Id, r => new InitializerDependencyDifferentialTests.AllowRow(r.Class, r.Cite), StringComparer.Ordinal);
        var none = Rows();
        var listed = Rows((running, VerdictClass.SilentWrong, "#2423 silent-wrong"));
        var wrong = Rows((later, VerdictClass.FalseRefusal, "#2423 wrong-code"));

        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("1")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(running, Ran("12")) }, listed));
        Assert.Empty(RatchetFailures(new[] { Of(later, Refused("SPY0220")) }, none));
        Assert.Empty(RatchetFailures(new[] { Of(later, Refused("SPY0200")) }, wrong));

        Assert.Contains("model", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1")) }, listed)));
        Assert.Contains("MOVED", Assert.Single(RatchetFailures(new[] { Of(running, Refused("SPY0200")) }, listed)));
        Assert.Contains("FalseRefusal", Assert.Single(RatchetFailures(new[] { Of(later, Refused("SPY0200")) }, none)));
        Assert.Contains("STALE", Assert.Single(RatchetFailures(new[] { Of(later, Refused("SPY0220")) }, wrong)));
        Assert.Contains("the route says", Assert.Single(RatchetFailures(new[] { Of(running, Ran("12")) }, Rows((running, VerdictClass.SilentWrong, "#2432 silent-wrong")))));
        Assert.Contains("GeneratorBug", Assert.Single(RatchetFailures(new[] { Of(running, Ran("1"), "python 4") }, none)));

        // By class (contract §10): a typed read red at BOTH orders is no order defect, so both rows are #2417's; red at the
        // later order alone it is #2423's, and a row citing the other issue is refused.
        var both = new[] { Of(earlier, Refused("SPY0200")), Of(later, Refused("SPY0200")) };
        Assert.Empty(RatchetFailures(both, Rows((earlier, VerdictClass.FalseRefusal, "#2417 wrong-code"), (later, VerdictClass.FalseRefusal, "#2417 wrong-code"))));
        Assert.Contains("the route says '#2417", Assert.Single(RatchetFailures(both, Rows((earlier, VerdictClass.FalseRefusal, "#2417 wrong-code"), (later, VerdictClass.FalseRefusal, "#2423 wrong-code")))));
        var laterOnly = new[] { Of(earlier, Refused("SPY0220")), Of(later, Refused("SPY0200")) };
        Assert.Contains("the route says '#2423", Assert.Single(RatchetFailures(laterOnly, Rows((later, VerdictClass.FalseRefusal, "#2417 wrong-code")))));

        // An alias whose target reaches the alias itself is unruled (#2769): its unused cell must run, and one that stops
        // running is that tracker's row, never the alias refusal's (#2423).
        var itself = pool.First(c => c.Cell.Key!.Part<ScopeAliasTarget>() == ScopeAliasTarget.Itself);
        Assert.Equal(SharpyTag.MustRun, itself.Cell.Tag);
        var refusedItself = new[] { Of(itself, Refused("SPY0202")) };
        Assert.Empty(RatchetFailures(refusedItself, Rows((itself, VerdictClass.FalseRefusal, "#2769 false-refusal"))));
        Assert.Contains("the route says '#2769", Assert.Single(RatchetFailures(refusedItself, Rows((itself, VerdictClass.FalseRefusal, "#2423 false-refusal")))));
    }
}

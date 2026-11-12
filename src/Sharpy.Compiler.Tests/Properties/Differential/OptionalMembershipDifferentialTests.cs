extern alias SharpyRT;
extern alias SharpyStdlib;

using System.Globalization;
using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for membership with an optional left operand (#2635): a needle of type <c>T | None</c> is
/// tested against every container kind exactly as the <c>T</c> needle is, and its <c>None</c> answers what
/// CPython answers — <c>False</c> for <c>in</c> (an equality scan finds no element equal to <c>None</c>),
/// <c>True</c> for <c>not in</c>, and <c>TypeError</c> at run time where CPython raises (a <c>str</c> or
/// <c>bytes</c> container) — owner rulings 2026-11-12. Never SPY0222, SPY0908 or a CLR exception. The literal
/// <c>None</c> into a non-optional slot is refused (SPY0222) by owner ruling A, 2026-11-12, and accepted into an
/// optional element type (the element-optionality axis).
/// <para><b>Generated, never listed.</b> The full product of six axes, with one structural exclusion (an optional
/// element exists only where the element type is the payload); the count is asserted from the axis sizes, not the
/// generator:</para>
/// <list type="bullet">
/// <item><b>payload</b> — the image of <see cref="PrimitiveCatalog.GetAllPrimitives"/> (one canonical name per
/// CLR type) minus <see cref="ExcludedPrimitives"/>, each excluded with its reason, plus two composite kinds a
/// primitive census cannot reach (a value-typed <c>tuple[int, int]</c>, a reference-typed user class), plus
/// <see cref="SlotPayloads"/>: every fixed needle slot of the container axis (a single-argument public
/// <c>Contains</c> parameter, read by reflection) that is not a catalog primitive — asserted both ways by
/// <see cref="AssertPayloadAxisIsTheCatalogAndTheSlots"/>;</item>
/// <item><b>container</b> — the closed set of the spec's "Container kinds" table
/// (<c>membership_operators.md</c>), read from the spec and mapped row by row (<see cref="SpecRowMap"/>); the CLR
/// row is closed against the reflection census of public single-argument <c>Contains</c> in Sharpy.Core and
/// Sharpy.Stdlib (<see cref="ClrContainerRoster"/>, <see cref="AssertClrRowIsTheContainsCensus"/>), and its
/// "any <c>IEnumerable[T]</c>" arm by one representative, <c>deque[T]</c> (<see cref="EnumerableArm"/>);</item>
/// <item><b>element optionality</b> — <c>T</c> or <c>T | None</c> for every container whose element type is the
/// payload (the container then also holds a <c>None</c> element); a fixed-slot container has no such spelling;</item>
/// <item><b>needle form</b> — the plain <c>T</c> (the control twin), <c>T | None</c>, and the literal
/// <c>None</c>;</item>
/// <item><b>operator</b> — the <see cref="BinaryOperator"/> members that are membership tests;</item>
/// <item><b>position</b> — the checker's two membership sites (a binary operator, a comparison-chain link) and
/// the two statement-level contexts that lower a binary operator differently (an <c>if</c> condition, a
/// comprehension filter).</item>
/// </list>
/// <para><b>Expected map</b> (<see cref="Expect"/>). COMPARED cells print CPython 3.15's values (the
/// <c>.python-version</c> pin). HAND-EXPECTED cells — a typed needle the slot cannot hold (<see cref="SlotHolds"/>),
/// refused by SPY0222 under the deviation <c>membership-needle-type-mismatch</c> (owner ruling B for bool, float and
/// decimal into an <c>int</c> slot), the literal <c>None</c> into a non-optional slot (SPY0222, ruling A), and chain
/// links (SPY0519, #2648) — are counted apart in the summary line. One program per payload is compiled;
/// cells refused at compile time are attributed by line, removed and the rest recompiled until it runs.</para>
/// <para><b>Known red.</b> <c>optional-membership-differential-allowlist.txt</c> (Conformance): one row per
/// <c>container/form</c>, each pinning its exact divergent cells and their observed Sharpy verdicts (<c>cells=</c>,
/// <c>pin=</c> and one <c>#= cell -&gt; verdict</c> line per cell). A cell that starts or stops diverging inside a
/// row, or whose wrong answer changes, fails the test; the problems and a regenerated allowlist are written under
/// <c>.claude/tmp/</c>, and every divergence always is.</para>
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class OptionalMembershipDifferentialTests : IntegrationTestBase
{
    public OptionalMembershipDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string AllowlistFileName = "optional-membership-differential-allowlist.txt";

    /// <summary>The CLR row's stdlib containers (collections, ipaddress, argparse, email) are imported by every program.</summary>
    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(SharpyStdlibReference.Location);

    /// <summary>The primitives the payload axis leaves out, each with why its python twin cannot be spelled.</summary>
    private static readonly Dictionary<string, string> ExcludedPrimitives = new(StringComparer.Ordinal)
    {
        ["char"] = "python has no character type",
        ["object"] = "an `object` element slot admits every needle: no needle-type decision to make",
        ["void"] = "None itself, the needle form axis",
    };

    private enum PayloadKind { Integer, Float, Decimal, Bool, Str, Tuple, Class, Slot }

    private sealed record Payload(string Name, PayloadKind Kind, string Member, string Other, string NonMember, Type? ClrType);

    /// <summary>
    /// The fixed needle slots of the container axis that are not catalog primitives, keyed by CLR full name: the
    /// Sharpy (and python) spelling, a member of every container that holds the type, a second member, and a
    /// non-member. <see cref="AssertPayloadAxisIsTheCatalogAndTheSlots"/> asserts the keys are exactly the reflected
    /// slot types the catalog does not cover.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Member, string Other, string NonMember)> SlotPayloads =
        new(StringComparer.Ordinal)
        {
            ["Sharpy.Bytes"] = ("bytes", "bytes([1])", "bytes([2])", "bytes([5])"),
            ["Sharpy.IPv4Address"] = ("IPv4Address", "IPv4Address(\"10.0.0.1\")", "IPv4Address(\"10.0.0.2\")",
                "IPv4Address(\"11.0.0.1\")"),
            ["Sharpy.IPv6Address"] = ("IPv6Address", "IPv6Address(\"::1\")", "IPv6Address(\"::2\")", "IPv6Address(\"1::1\")"),
        };

    private enum Container
    {
        Tuple, List, Set, FrozenSet, Dict, DictKeys, DictValues, ContainsDunder, Str, Bytes, Range,
        Counter, OrderedDict, ChainMap, DefaultDict, FrozenDict, IPv4Network, IPv6Network, ArgparseNamespace, EmailMessage,
        Deque,
    }

    /// <summary>
    /// The spec's "Container kinds" rows (first cell, verbatim) → the containers generated for that row, or the
    /// reason none is. Read against the spec by <see cref="AssertContainerAxisIsTheSpecTable"/>.
    /// </summary>
    private static readonly (string Row, Container[] Generated, string? Excluded)[] SpecRowMap =
    {
        ("`list[T]`, `set[T]`, `frozenset[T]`, `array[T]`", new[] { Container.List, Container.Set, Container.FrozenSet },
            "array[T]: python's array.array is a typecode-keyed stdlib class with no T | None spelling"),
        ("`dict[K, V]`", new[] { Container.Dict, Container.DictKeys, Container.DictValues }, null),
        ("`str`", new[] { Container.Str }, null),
        ("`bytes`", new[] { Container.Bytes }, null),
        ("`tuple[...]`", new[] { Container.Tuple }, null),
        ("`range(...)`", new[] { Container.Range }, null),
        ("a class with `__contains__` (declared or inherited)", new[] { Container.ContainsDunder }, null),
        ("a CLR type with a public `Contains`, or any `IEnumerable[T]`", new[]
            {
                Container.Counter, Container.OrderedDict, Container.ChainMap, Container.DefaultDict, Container.FrozenDict,
                Container.IPv4Network, Container.IPv6Network, Container.ArgparseNamespace, Container.EmailMessage,
                Container.Deque,
            },
            "the Contains arm is closed against the reflection census (ClrContainerRoster); the IEnumerable[T]-without-"
            + "Contains arm is generated by one representative (EnumerableArm)"),
    };

    /// <summary>
    /// The spec row's "any <c>IEnumerable[T]</c>" arm: a receiver with no <c>Contains</c> of its own. Its needle is
    /// decided by the checker's generic-container arm (<c>ResolveMembershipElementType</c>: a <c>GenericType</c> →
    /// <c>InferIterableElementType</c>; <c>ProtocolMembership</c> admits it through <c>IEnumerable&lt;T&gt;</c>) and
    /// emitted as LINQ's <c>Enumerable.Contains</c>. Every such receiver Sharpy spells is a <c>GenericType</c> over its
    /// element and reaches that same arm and the same emission, so one representative stands for it: <c>deque[T]</c>
    /// (an <c>Iterator[T]</c> from <c>iter(xs)</c> was probed at the base: the same SPY0222 for a <c>T | None</c>
    /// needle, the same answers otherwise). <see cref="AssertClrRowIsTheContainsCensus"/> asserts it has no
    /// <c>Contains</c> and is an <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    private static readonly (string ClrName, Container Generated, string Why) EnumerableArm =
        ("Sharpy.Deque`1", Container.Deque, "the IEnumerable[T]-without-Contains arm: one GenericType checker arm "
            + "(InferIterableElementType) and one LINQ emission for every such receiver");

    /// <summary>The element-typed containers, as a literal roster: the totality count reads its size, and
    /// <see cref="IsElementTyped"/> (which the generator uses) is asserted equal to it.</summary>
    private static readonly Container[] ElementTypedRoster =
    {
        Container.Tuple, Container.List, Container.Set, Container.FrozenSet, Container.Dict, Container.DictKeys,
        Container.DictValues, Container.ContainsDunder, Container.Counter, Container.OrderedDict, Container.ChainMap,
        Container.DefaultDict, Container.FrozenDict, Container.Deque,
    };

    /// <summary>Element-typed containers exempt from the Sharpy half of the optional-element control (a compared
    /// <c>T | None</c> needle that Sharpy answers <c>True False True</c> today), each with its reason and issue.</summary>
    private static readonly Dictionary<Container, string> OptionalElementControlExcluded = new()
    {
        [Container.Counter] = "#2651: Counter[T | None] with a None element throws ArgumentNullException at construction",
    };

    /// <summary>
    /// The CLR row's census: every public type in Sharpy.Core and Sharpy.Stdlib with a public instance single-argument
    /// <c>Contains</c> (a generic definition by its open name) → the container that generates it, or why none does.
    /// <see cref="AssertClrRowIsTheContainsCensus"/> asserts the keys are exactly the reflected set.
    /// </summary>
    private static readonly Dictionary<string, (Container? Generated, string? Excluded)> ClrContainerRoster =
        new(StringComparer.Ordinal)
        {
            ["Sharpy.List`1"] = (Container.List, null),
            ["Sharpy.Set`1"] = (Container.Set, null),
            ["Sharpy.FrozenSet`1"] = (Container.FrozenSet, null),
            ["Sharpy.Dict`2"] = (Container.Dict, null),
            ["Sharpy.DictKeyView`2"] = (Container.DictKeys, null),
            ["Sharpy.DictValuesView`2"] = (Container.DictValues, null),
            ["Sharpy.DictItemsView`2"] = (null, "its slot is the (K, V) pair, which no payload of this axis is: a pair "
                + "payload would need its own value type per key; the pair is the dict's slot, generated by Dict"),
            ["Sharpy.RangeIterator"] = (Container.Range, null),
            ["Sharpy.IDict"] = (null, ObjectSlot),
            ["Sharpy.IList"] = (null, ObjectSlot),
            ["Sharpy.ISet"] = (null, ObjectSlot),
            ["Sharpy.Bytes"] = (Container.Bytes, null),
            ["Sharpy.FrozenDict`2"] = (Container.FrozenDict, null),
            ["Sharpy.Counter`1"] = (Container.Counter, null),
            ["Sharpy.OrderedDict`2"] = (Container.OrderedDict, null),
            ["Sharpy.ChainMap`2"] = (Container.ChainMap, null),
            ["Sharpy.DefaultDict`2"] = (Container.DefaultDict, null),
            ["Sharpy.IPv4Network"] = (Container.IPv4Network, null),
            ["Sharpy.IPv6Network"] = (Container.IPv6Network, null),
            ["Sharpy.Namespace"] = (Container.ArgparseNamespace, null),
            ["Sharpy.EmailMessage"] = (Container.EmailMessage, null),
        };

    private const string ObjectSlot = "a non-generic runtime interface whose Contains(object) slot admits every needle: no "
        + "needle-type decision to make (as for the excluded `object` primitive)";

    private static IEnumerable<Type> SharpyTypes() =>
        new[] { typeof(SharpyRT::Sharpy.Builtins).Assembly, typeof(SharpyStdlib::Sharpy.EmailMessage).Assembly }
            .Distinct()
            .SelectMany(a => a.GetExportedTypes());

    /// <summary>The public instance single-argument <c>Contains</c> parameter types of a CLR type.</summary>
    private static IReadOnlyList<Type> ContainsSlots(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Contains" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Distinct()
            .ToList();

    /// <summary>The CLR type a container's membership test reaches: the roster's, <c>str</c>'s, or none (a tuple
    /// projection; a user class).</summary>
    private static Type? ClrTypeOf(Container container)
    {
        if (container == Container.Str)
            return typeof(string);
        if (container is Container.Tuple or Container.ContainsDunder)
            return null;
        var name = container == EnumerableArm.Generated
            ? EnumerableArm.ClrName
            : ClrContainerRoster.Single(r => r.Value.Generated == container).Key;
        return SharpyTypesByName.Value[name];
    }

    private static readonly Lazy<Dictionary<string, Type>> SharpyTypesByName =
        new(() => SharpyTypes().ToDictionary(t => t.FullName!, StringComparer.Ordinal));

    /// <summary>A container whose element type is the payload, so it has a <c>T | None</c> element spelling: a tuple,
    /// a user class, or a generic CLR type.</summary>
    private static bool IsElementTyped(Container container) =>
        container is Container.Tuple or Container.ContainsDunder || ClrTypeOf(container) is { IsGenericTypeDefinition: true };

    private enum Form { Plain, Optional, NoneLiteral }

    private enum Position { Expression, IfCondition, ComprehensionFilter, ChainLink }

    private sealed record Cell(int Id, Payload Payload, Container Container, bool OptionalElement, Form Form,
        BinaryOperator Operator, Position Position)
    {
        public string ContainerName => OptionalElement ? $"{Container}OfOptional" : Container.ToString();
        public string Key => $"{Payload.Name}/{ContainerName}/{Form}/{OperatorSpelling(Operator)}/{Position}";
        /// <summary>The allowlist row: the container × whether the needle can be None (<c>T | None</c> and the literal
        /// <c>None</c> share one row, as they share the rulings; an optional element has its own row, since a defect
        /// that only an optional element shows is a different class). Rows pin their exact cells, so the key's
        /// coarseness hides nothing.</summary>
        public string RowKey => $"{ContainerName}/{(Form == Form.Plain ? "Plain" : "NoneNeedle")}";
    }

    private static string OperatorSpelling(BinaryOperator op) => op == BinaryOperator.In ? "in" : "not in";

    private static readonly BinaryOperator[] Operators =
        Enum.GetValues<BinaryOperator>().Where(o => o is BinaryOperator.In or BinaryOperator.NotIn).ToArray();

    /// <summary>One canonical Sharpy name per CLR primitive (aliases dropped), ordinal-sorted.</summary>
    private static IReadOnlyList<string> CanonicalPrimitives() =>
        PrimitiveCatalog.GetAllPrimitives()
            .Where(p => PrimitiveCatalog.GetByClrType(p.Info.ClrType)?.SharpyName == p.Name)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static List<Payload> GeneratePayloads()
    {
        var payloads = new List<Payload>();
        foreach (var name in CanonicalPrimitives())
        {
            if (ExcludedPrimitives.ContainsKey(name))
                continue;
            var info = PrimitiveCatalog.GetByName(name)!;
            var clr = info.ClrType;
            var payload = info.Kind switch
            {
                PrimitiveCatalog.NumericKind.SignedInteger or PrimitiveCatalog.NumericKind.UnsignedInteger
                    => new Payload(name, PayloadKind.Integer, "1", "2", "5", clr),
                PrimitiveCatalog.NumericKind.FloatingPoint => new Payload(name, PayloadKind.Float, "1.5", "2.5", "5.5", clr),
                // The python twin spells it through the prelude's `from decimal import Decimal as decimal`.
                PrimitiveCatalog.NumericKind.Decimal
                    => new Payload(name, PayloadKind.Decimal, "decimal(1)", "decimal(2)", "decimal(5)", clr),
                _ when name == "bool" => new Payload(name, PayloadKind.Bool, "True", "True", "False", clr),
                _ when name == "str" => new Payload(name, PayloadKind.Str, "\"a\"", "\"b\"", "\"z\"", clr),
                // A primitive the generator has no literals for: the totality assertion names it.
                _ => null,
            };
            if (payload != null)
                payloads.Add(payload);
        }
        payloads.Add(new Payload("tuple[int, int]", PayloadKind.Tuple, "(1, 2)", "(3, 4)", "(5, 6)", typeof(ValueTuple<int, int>)));
        payloads.Add(new Payload("K", PayloadKind.Class, "K(1)", "K(2)", "K(5)", null));
        var types = SharpyTypesByName.Value;
        foreach (var (clrName, slot) in SlotPayloads.OrderBy(s => s.Key, StringComparer.Ordinal))
            payloads.Add(new Payload(slot.Name, PayloadKind.Slot, slot.Member, slot.Other, slot.NonMember,
                types.GetValueOrDefault(clrName)));
        return payloads;
    }

    private static Container[] Containers() => SpecRowMap.SelectMany(m => m.Generated).OrderBy(c => c).ToArray();

    /// <summary>The full product of the axes; the one structural exclusion is an optional element on a fixed-slot
    /// container, which has no spelling. A needle the container's slot cannot hold is a CELL (its expectation is the
    /// spec's refusal), never a skip.</summary>
    private static List<Cell> GenerateCells(IReadOnlyList<Payload> payloads)
    {
        var containers = Containers();
        var cells = new List<Cell>();
        foreach (var payload in payloads)
            foreach (var container in containers)
                foreach (var optionalElement in IsElementTyped(container) ? new[] { false, true } : new[] { false })
                    foreach (var form in Enum.GetValues<Form>())
                        foreach (var op in Operators)
                            foreach (var position in Enum.GetValues<Position>())
                                cells.Add(new Cell(cells.Count, payload, container, optionalElement, form, op, position));
        return cells;
    }

    /// <summary>The container expression. An optional element types <c>a</c>, <c>c</c> and <c>z</c> as
    /// <c>T | None</c> and adds <c>z</c> (always <c>None</c>) as an element, so <c>None</c> is a member.</summary>
    private static string ContainerSource(Cell cell)
    {
        var o = cell.OptionalElement;
        var items = o ? "a, c, z" : "a, c";
        return cell.Container switch
        {
            Container.Tuple => $"({items})",
            Container.List => $"[{items}]",
            Container.Set => $"{{{items}}}",
            Container.FrozenSet => $"frozenset([{items}])",
            Container.Dict => o ? "{a: 0, c: 1, z: 2}" : "{a: 0, c: 1}",
            Container.DictKeys => o ? "{a: 0, c: 1, z: 2}.keys()" : "{a: 0, c: 1}.keys()",
            Container.DictValues => o ? "{0: a, 1: c, 2: z}.values()" : "{0: a, 1: c}.values()",
            Container.ContainsDunder => o ? "OBag(a, c, z)" : "Bag(a, c)",
            Container.Str => "\"ab\"",
            Container.Bytes => "bytes([1, 2])",
            Container.Range => "range(1, 3)",
            // Built by a helper from typed locals: an explicit type argument list on a constructor call does not take
            // a `T | None` argument (SPY0342), and the helpers spell both variants the same way.
            Container.Counter or Container.OrderedDict or Container.ChainMap or Container.DefaultDict or Container.FrozenDict
                or Container.Deque
                => $"make_{cell.Container}{(o ? "_o" : "")}({items})",
            Container.IPv4Network => "IPv4Network(\"10.0.0.0/8\")",
            Container.IPv6Network => "IPv6Network(\"::/112\")",
            Container.ArgparseNamespace => "make_ns()",
            Container.EmailMessage => "make_msg()",
            _ => throw new InvalidOperationException(cell.Container.ToString()),
        };
    }

    private sealed record Program(string Source, Dictionary<int, int> CellByLine);

    /// <summary>One program per payload over the cells still active; every line of a cell's function and of its
    /// driver loop is attributed to it, so a refusal anywhere in either removes that cell alone.</summary>
    private static Program ProgramFor(Payload payload, IReadOnlyList<Cell> cells)
    {
        var lines = new List<string>();
        var cellByLine = new Dictionary<int, int>();
        void Add(string line, int? cellId = null)
        {
            lines.Add(line);
            if (cellId is { } id)
                cellByLine[lines.Count] = id;
        }

        var p = payload.Name;
        Add("from argparse import ArgumentParser, Namespace");
        Add("from collections import ChainMap, Counter, OrderedDict, defaultdict, deque");
        Add("from email import EmailMessage");
        Add("from ipaddress import IPv4Address, IPv4Network, IPv6Address, IPv6Network");
        Add("");
        Add("class K:");
        Add("    v: int");
        Add("    def __init__(self, v: int):");
        Add("        self.v = v");
        Add("");
        Add("class Bag:");
        Add($"    items: list[{p}]");
        Add($"    def __init__(self, a: {p}, c: {p}):");
        Add("        self.items = [a, c]");
        Add($"    def __contains__(self, item: {p}) -> bool:");
        Add("        return item in self.items");
        Add("");
        Add("class OBag:");
        Add($"    items: list[{p} | None]");
        Add($"    def __init__(self, a: {p} | None, c: {p} | None, z: {p} | None):");
        Add("        self.items = [a, c, z]");
        Add($"    def __contains__(self, item: {p} | None) -> bool:");
        Add("        return item in self.items");
        Add("");
        // The CLR row's generic containers, each over `a, c` (element T) and `a, c, z` (element T | None, z = None).
        foreach (var optional in new[] { false, true })
        {
            var e = optional ? $"{p} | None" : p;
            var sig = optional ? $"a: {e}, c: {e}, z: {e}" : $"a: {e}, c: {e}";
            var suffix = optional ? "_o" : "";
            var items = optional ? "a, c, z" : "a, c";
            var dict = optional ? "{a: 0, c: 1, z: 2}" : "{a: 0, c: 1}";
            Add($"def make_Counter{suffix}({sig}) -> Counter[{e}]:");
            Add($"    xs: list[{e}] = [{items}]");
            Add($"    r: Counter[{e}] = Counter(xs)");
            Add("    return r");
            Add($"def make_Deque{suffix}({sig}) -> deque[{e}]:");
            Add($"    xs: list[{e}] = [{items}]");
            Add($"    r: deque[{e}] = deque(xs)");
            Add("    return r");
            Add($"def make_OrderedDict{suffix}({sig}) -> OrderedDict[{e}, int]:");
            Add($"    d: dict[{e}, int] = {dict}");
            Add($"    r: OrderedDict[{e}, int] = OrderedDict(d)");
            Add("    return r");
            Add($"def make_FrozenDict{suffix}({sig}) -> frozendict[{e}, int]:");
            Add($"    d: dict[{e}, int] = {dict}");
            Add($"    r: frozendict[{e}, int] = frozendict(d)");
            Add("    return r");
            Add($"def make_ChainMap{suffix}({sig}) -> ChainMap[{e}, int]:");
            Add($"    first: dict[{e}, int] = {{a: 0}}");
            Add($"    rest: dict[{e}, int] = {(optional ? "{c: 1, z: 2}" : "{c: 1}")}");
            Add($"    r: ChainMap[{e}, int] = ChainMap(first, rest)");
            Add("    return r");
            Add($"def make_DefaultDict{suffix}({sig}) -> defaultdict[{e}, int]:");
            Add($"    r: defaultdict[{e}, int] = defaultdict(int)");
            Add("    r[a] = 0");
            Add("    r[c] = 1");
            if (optional)
                Add("    r[z] = 2");
            Add("    return r");
            Add("");
        }
        // A Namespace holding "a" and "b"; positionals, as Sharpy's argparse takes no `--` options here.
        Add("def make_ns() -> Namespace:");
        Add("    parser = ArgumentParser()");
        Add("    parser.add_argument(\"a\")");
        Add("    parser.add_argument(\"b\")");
        Add("    return parser.parse_args([\"1\", \"2\"])");
        Add("");
        Add("def make_msg() -> EmailMessage:");
        Add("    m = EmailMessage()");
        Add("    m.add_header(\"a\", \"1\")");
        Add("    m.add_header(\"b\", \"2\")");
        Add("    return m");
        Add("");

        foreach (var cell in cells)
        {
            var op = OperatorSpelling(cell.Operator);
            var needle = cell.Form == Form.NoneLiteral ? "None" : "x";
            var elements = cell.OptionalElement ? $"a: {p} | None, c: {p} | None, z: {p} | None" : $"a: {p}, c: {p}";
            var parameters = cell.Form switch
            {
                Form.Plain => $"x: {p}, {elements}",
                Form.Optional => $"x: {p} | None, {elements}",
                _ => elements,
            };
            var container = ContainerSource(cell);
            var test = $"{needle} {op} {container}";
            Add($"def c{cell.Id}({parameters}) -> bool:", cell.Id);
            // A located first statement: code the emitter hoists above the test (a comprehension) carries no
            // #line of its own, and a Roslyn error there maps to the last located line — this cell's, not the
            // previous function's.
            Add($"    cell: int = {cell.Id}", cell.Id);
            switch (cell.Position)
            {
                case Position.Expression:
                    Add($"    return {test}", cell.Id);
                    break;
                case Position.IfCondition:
                    Add($"    if {test}:", cell.Id);
                    Add("        return True", cell.Id);
                    Add("    return False", cell.Id);
                    break;
                case Position.ComprehensionFilter:
                    Add($"    return len([0 for _ in [0] if {test}]) == 1", cell.Id);
                    break;
                case Position.ChainLink:
                    // The first link reads the needle twice; the literal None's spelling is `None is None` (Sharpy has
                    // no `None == None`), so the cell tests the chain arm's needle decision, not `==`.
                    Add(cell.Form == Form.NoneLiteral
                        ? $"    return None is None {op} {container}"
                        : $"    return x == x {op} {container}", cell.Id);
                    break;
            }
            Add("");
        }

        Add("def main() -> None:");
        Add($"    a: {p} = {payload.Member}");
        Add($"    c: {p} = {payload.Other}");
        Add($"    n: {p} = {payload.NonMember}");
        Add($"    plain: list[{p}] = [a, n]");
        Add($"    optional: list[{p} | None] = [a, n, None]");
        foreach (var cell in cells)
        {
            var id = cell.Id.ToString(CultureInfo.InvariantCulture);
            var elements = cell.OptionalElement ? "a, c, None" : "a, c";
            var (loop, call) = cell.Form switch
            {
                Form.Plain => ("    for v in plain:", $"c{id}(v, {elements})"),
                Form.Optional => ("    for v in optional:", $"c{id}(v, {elements})"),
                _ => ("    for v in [0]:", $"c{id}({elements})"),
            };
            Add(loop, cell.Id);
            Add("        try:", cell.Id);
            Add($"            print({id}, {call})", cell.Id);
            Add("        except TypeError:", cell.Id);
            Add($"            print({id}, \"TypeError\")", cell.Id);
            Add("        except AttributeError:", cell.Id);
            Add($"            print({id}, \"AttributeError\")", cell.Id);
            Add("        except Exception:", cell.Id);
            Add($"            print({id}, \"other\")", cell.Id);
        }
        Add("    print(\"done\")");
        return new Program(string.Join("\n", lines) + "\n", cellByLine);
    }

    private static Dictionary<int, List<string>> ParseLines(string stdout)
    {
        var byId = new Dictionary<int, List<string>>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                if (!byId.TryGetValue(id, out var values))
                    byId[id] = values = new List<string>();
                values.Add(line[(space + 1)..]);
            }
        }
        return byId;
    }

    /// <summary>Compile rounds per payload: checker refusals, then codegen refusals, then Roslyn's (SPY0908).</summary>
    private const int MaxRounds = 40;

    private sealed record SharpyVerdict(IReadOnlyList<string>? Values, IReadOnlySet<string>? RefusedBy);

    /// <summary>Compiles the payload's program, removing each cell a refusal is attributed to, until it runs.</summary>
    private Dictionary<int, SharpyVerdict> RunSharpy(Payload payload, IReadOnlyList<Cell> cells, List<string> harnessFailures)
    {
        var verdicts = new Dictionary<int, SharpyVerdict>();
        var active = cells.ToList();
        for (var round = 0; round < MaxRounds; round++)
        {
            var program = ProgramFor(payload, active);
            var run = CompileAndExecute(program.Source, "optional_membership.spy", executionTimeoutMs: 120_000);
            if (RanToCompletion(run))
            {
                var byId = ParseLines(run.StandardOutput);
                foreach (var cell in active)
                    verdicts[cell.Id] = new SharpyVerdict(byId.TryGetValue(cell.Id, out var v) ? v : new List<string>(), null);
                if (!run.StandardOutput.Contains("done", StringComparison.Ordinal))
                    harnessFailures.Add($"{payload.Name}: the program did not reach its last line");
                return verdicts;
            }

            var refused = new Dictionary<int, HashSet<string>>();
            var unattributed = new List<string>();
            foreach (var d in run.RawDiagnostics.Where(d => d.IsError))
            {
                if (d.Line is { } line && program.CellByLine.TryGetValue(line, out var id))
                {
                    if (!refused.TryGetValue(id, out var codes))
                        refused[id] = codes = new HashSet<string>(StringComparer.Ordinal);
                    codes.Add(d.Code ?? "?");
                    if (d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
                        Output.WriteLine($"  {payload.Name} c{id} line {line}: {d.Message}");
                }
                else
                {
                    unattributed.Add($"line {d.Line}: [{d.Code}] {d.Message}");
                }
            }
            if (unattributed.Count > 0 || refused.Count == 0)
            {
                harnessFailures.Add($"{payload.Name}: round {round} failed with no cell to remove: "
                    + (unattributed.Count > 0 ? string.Join("; ", unattributed.Take(5)) : run.FailureSummary()));
                return verdicts;
            }
            foreach (var (id, codes) in refused)
                verdicts[id] = new SharpyVerdict(null, codes);
            active = active.Where(c => !refused.ContainsKey(c.Id)).ToList();
            Output.WriteLine($"{payload.Name}: round {round} removed {refused.Count} cell(s) ("
                + string.Join(",", refused.Values.SelectMany(c => c).Distinct().OrderBy(c => c, StringComparer.Ordinal)) + ")");
        }
        harnessFailures.Add($"{payload.Name}: still refusing after {MaxRounds} rounds");
        return verdicts;
    }

    /// <summary>What a cell may do: print CPython's values (<see cref="Values"/>: the cell is COMPARED against the
    /// oracle), or be refused by exactly one of <see cref="RefusedBy"/> (HAND-EXPECTED: the basis is in
    /// <see cref="Why"/>, never the oracle).</summary>
    private sealed record Expectation(bool Values, IReadOnlySet<string> RefusedBy, string Why)
    {
        public bool Compared => Values;

        public string Describe() => (Values ? "python's values" : "")
            + (RefusedBy.Count > 0 ? "refused by " + string.Join("|", RefusedBy.OrderBy(c => c, StringComparer.Ordinal)) : "")
            + " (" + Why + ")";
    }

    private static readonly IReadOnlySet<string> NoCodes = new HashSet<string>();
    private static readonly IReadOnlySet<string> Spy0222 = new HashSet<string> { DiagnosticCodes.Semantic.InvalidBinaryOperation };

    /// <summary>The needle-slot rule (membership_operators.md › Container kinds, as owner ruling C amends the
    /// <c>bytes</c> row): an element-typed container's slot IS the payload. A fixed-slot container's slots are its
    /// public single-argument <c>Contains</c> parameter types, read by reflection: the payload holds one when its CLR
    /// type is that type, or when it is an integer the catalog converts implicitly to it (so <c>range</c> and
    /// <c>bytes</c> take <c>int</c> and narrower, <c>bytes</c> also takes <c>bytes</c>, <c>str</c> takes <c>str</c>,
    /// an IP network its address type).</summary>
    private static bool SlotHolds(Cell cell)
    {
        if (IsElementTyped(cell.Container))
            return true;
        var payload = cell.Payload;
        if (payload.ClrType is not { } clr)
            return false;
        return ContainsSlots(ClrTypeOf(cell.Container)!).Any(slot => slot == clr
            || (payload.Kind == PayloadKind.Integer && PrimitiveCatalog.GetByClrType(slot) is { } to
                && PrimitiveCatalog.CanImplicitlyConvert(PrimitiveCatalog.GetByName(payload.Name)!, to)));
    }

    /// <summary>
    /// The case → expected map. COMPARED: a needle the slot holds, plain or <c>T | None</c>, prints CPython's values —
    /// <c>False</c> for a None needle in an equality scan (owner ruling 1, #2635), <c>TypeError</c> for a None needle
    /// in <c>str</c>/<c>bytes</c> (owner ruling 2: accepted, TypeError at run time), and whatever CPython answers for
    /// the other fixed-slot containers; so does the literal <c>None</c> into an optional element (owner ruling A).
    /// HAND-EXPECTED, by owner ruling A (2026-11-12): the literal <c>None</c> into a non-optional slot is refused by
    /// SPY0222. HAND-EXPECTED, by the deviation <c>membership-needle-type-mismatch</c> in <c>docs/deviations.yaml</c>:
    /// a typed needle the slot cannot hold (<see cref="SlotHolds"/>) is refused by SPY0222. HAND-EXPECTED, out of this
    /// contract: a chain link (#2648, below).
    /// </summary>
    private static Expectation Expect(Cell cell, IReadOnlyDictionary<string, Cell> byKey)
    {
        if (cell.Form == Form.NoneLiteral && !cell.OptionalElement && cell.Position != Position.ChainLink)
        {
            // Owner ruling A, 2026-11-12: "switch to the table, refuse literal None for non-optional containers, makes
            // the most sense". The chain link inherits it.
            return new Expectation(false, Spy0222, "literal None into a non-optional slot: owner ruling A (2026-11-12)");
        }

        if (cell.Position == Position.ChainLink)
        {
            // The checker's chain arm decides the needle as the binary arm does; codegen then refuses every `in` link
            // with SPY0519 (#2648, out of this contract), so a link whose expression twin runs expects SPY0519.
            var atExpression = Expect(byKey[cell.Key.Replace("/ChainLink", "/Expression", StringComparison.Ordinal)], byKey);
            var codes = atExpression.Values
                ? new HashSet<string> { DiagnosticCodes.CodeGen.UnsupportedOperator }
                : new HashSet<string>(atExpression.RefusedBy);
            return new Expectation(false, codes, "chain link: the expression twin's checker verdict, then SPY0519 (#2648)");
        }

        if (cell.Form == Form.NoneLiteral)
            return new Expectation(true, NoCodes, "literal None into an optional element: owner ruling A (2026-11-12)");

        if (!SlotHolds(cell))
            return new Expectation(false, Spy0222, "by deviation membership-needle-type-mismatch, not by oracle: the slot cannot hold this type");

        return new Expectation(true, NoCodes, cell.Form == Form.Plain
            ? "plain needle"
            : "T | None needle: owner rulings 1-2 (None is no member of an equality scan; TypeError for str/bytes); CPython's answer elsewhere");
    }

    /// <summary>The python twin's names for Sharpy's types and its <c>email.EmailMessage</c> (python's lives in
    /// <c>email.message</c>).</summary>
    private const string PythonPrelude =
        "from decimal import Decimal as decimal\n"
        + "int8 = int16 = int32 = int64 = uint8 = uint16 = uint32 = uint64 = int\nfloat32 = float64 = float\n"
        + "import email, email.message\nemail.EmailMessage = email.message.EmailMessage\n";

    [SkippableFact]
    public void OptionalNeedle_EveryContainerOperatorPosition_MatchesCPython()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        AssertContainerAxisIsTheSpecTable();
        AssertClrRowIsTheContainsCensus();
        AssertPayloadAxisIsTheCatalogAndTheSlots();
        AssertEveryExclusionHasAReason();
        // The generator's element-typed predicate is the literal roster, container by container.
        Assert.Equal(ElementTypedRoster.OrderBy(c => c), Containers().Where(IsElementTyped).OrderBy(c => c));

        var payloads = GeneratePayloads();
        var cells = GenerateCells(payloads);

        // Totality: the cell count is the product of the axis sizes, each from its own source (the catalog minus its
        // named exclusions plus the two composites plus the reflected slot types; the spec table's containers, doubled
        // for the literal element-typed roster; the enums). Never from the generator or its predicates, so a cell the
        // generator drops (or invents) is red.
        var payloadCount = CanonicalPrimitives().Count - ExcludedPrimitives.Count + 2 + RequiredSlotPayloadTypes().Count;
        var containerCount = SpecRowMap.Sum(m => m.Generated.Length);
        var elementTypedCount = ElementTypedRoster.Length;
        var formCount = Enum.GetValues<Form>().Length;
        var positionCount = Enum.GetValues<Position>().Length;
        var containerVariants = containerCount + elementTypedCount;
        var expectedCount = payloadCount * containerVariants * Operators.Length * formCount * positionCount;
        Assert.Equal(2, Operators.Length);
        Assert.Equal(payloadCount, payloads.Count);
        Assert.True(expectedCount == cells.Count,
            $"the generator made {cells.Count} cells; the axes give {expectedCount} ({payloadCount} payloads x "
            + $"{containerVariants} container variants ({containerCount} containers, {elementTypedCount} with an optional "
            + $"element) x {Operators.Length} operators x {formCount} forms x {positionCount} positions): "
            + "a cell was dropped or invented");

        // CPython: one program per payload × container (the runner passes a program as one argv string, which the OS
        // caps at 128 KiB), one batch.
        var chunks = cells.GroupBy(c => (c.Payload, c.Container)).Select(g => (Name: $"{g.Key.Payload.Name}/{g.Key.Container}",
            Program: ProgramFor(g.Key.Payload, g.ToList()))).ToList();
        var py = oracle.RunBatch(chunks.Select((chunk, i) => new PyRequest(i, PythonPrelude + chunk.Program.Source + "main()\n")).ToList());
        var pythonById = new Dictionary<int, List<string>>();
        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.True(py.TryGetValue(i, out var result) && result.Ok,
                $"python3 did not run the {chunks[i].Name} program: " + (result?.Stderr ?? "no verdict"));
            foreach (var (id, values) in ParseLines(result!.Stdout))
                pythonById[id] = values;
        }
        Assert.Equal(cells.Count, pythonById.Count);

        // Sharpy.
        var harnessFailures = new List<string>();
        var sharpyById = new Dictionary<int, SharpyVerdict>();
        foreach (var payload in payloads)
            foreach (var (id, verdict) in RunSharpy(payload, cells.Where(c => c.Payload == payload).ToList(), harnessFailures))
                sharpyById[id] = verdict;
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(cells.Count, sharpyById.Count);

        var byKey = cells.ToDictionary(c => c.Key, StringComparer.Ordinal);
        var expectations = cells.ToDictionary(c => c.Id, c => Expect(c, byKey));

        var divergences = new List<(Cell Cell, string Observed, string Detail)>();
        foreach (var cell in cells)
        {
            var python = pythonById[cell.Id];
            var sharpy = sharpyById[cell.Id];
            var expected = expectations[cell.Id];
            var meets = sharpy.RefusedBy is { } refusedBy
                ? refusedBy.Count == 1 && expected.RefusedBy.Contains(refusedBy.Single())
                : expected.Values && python.SequenceEqual(sharpy.Values!);
            if (meets)
                continue;
            var observed = sharpy.RefusedBy is { } by
                ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
                : "values " + string.Join(" ", sharpy.Values!);
            // The pinned verdict names both sides, so a changed oracle answer or expectation for a red cell is a change
            // to its pin, exactly as a changed Sharpy answer is.
            var expectedShort = expected.Values
                ? "python " + string.Join(" ", python)
                : "refused " + string.Join("|", expected.RefusedBy.OrderBy(c => c, StringComparer.Ordinal));
            var pinned = $"sharpy {observed} | expected {expectedShort}";
            divergences.Add((cell, pinned, $"python [{string.Join(" ", python)}] sharpy [{observed}]: expected {expected.Describe()}"));
        }

        var compared = expectations.Values.Count(e => e.Compared);
        var handExpected = cells.Count - compared;
        var reportDir = ReportDirectory();
        var divergencePath = Path.Combine(reportDir, "optional-membership-differential-divergences.txt");
        File.WriteAllLines(divergencePath, divergences.OrderBy(d => d.Cell.Key, StringComparer.Ordinal)
            .Select(d => $"{d.Cell.Key}  {d.Detail}"));

        var allowlist = LoadAllowlist();
        var actualByRow = divergences.GroupBy(d => d.Cell.RowKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Cell.Key, d => d.Observed, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var row in actualByRow.Keys.Union(allowlist.Keys).OrderBy(r => r, StringComparer.Ordinal))
        {
            var actual = actualByRow.GetValueOrDefault(row) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            if (!allowlist.TryGetValue(row, out var pinned))
            {
                problems.Add($"ROW {row}: not in the allowlist ({actual.Count} divergent cells)");
                problems.AddRange(actual.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"  added {a.Key} -> {a.Value}"));
                continue;
            }
            if (pinned.Count != pinned.Cells.Count || pinned.Pin != Digest(pinned.Cells))
                problems.Add($"ROW {row}: its header (cells={pinned.Count} pin={pinned.Pin}) does not match its "
                    + $"{pinned.Cells.Count} pin lines (pin={Digest(pinned.Cells)})");
            if (actual.Count == 0)
                problems.Add($"ROW {row}: no cell diverges any more — delete the row");
            foreach (var (key, verdict) in actual.OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                if (!pinned.Cells.TryGetValue(key, out var was))
                    problems.Add($"  {row}: added {key} -> {verdict} (a cell outside the pin now diverges)");
                else if (was != verdict)
                    problems.Add($"  {row}: changed {key}: {was} -> {verdict}");
            }
            foreach (var key in pinned.Cells.Keys.Where(k => !actual.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                problems.Add($"  {row}: removed {key} (pinned {pinned.Cells[key]}; it now meets its expectation)");
        }

        Output.WriteLine($"{payloads.Count} payloads, {cells.Count} cases: {compared} compared with CPython, {handExpected} "
            + $"hand-expected (slot mismatches by deviation membership-needle-type-mismatch; literal None by owner ruling A; "
            + "chain links by #2648); "
            + $"{divergences.Count} divergent in {actualByRow.Count} rows; allowlist {allowlist.Count} rows; "
            + $"{problems.Count} pin problem line(s). Every divergence: {divergencePath}");

        // Positive controls: the oracle column reaches every answer the expected map distinguishes.
        Assert.Contains(cells, c => pythonById[c.Id].Contains("True"));
        Assert.Contains(cells, c => pythonById[c.Id].Contains("False"));
        Assert.Contains(cells, c => expectations[c.Id].Compared && c.Form == Form.Optional && pythonById[c.Id].Contains("TypeError"));
        Assert.Contains(cells, c => c.Form == Form.Plain && c.Position != Position.ChainLink && sharpyById[c.Id].Values != null);
        // The optional column can pass: a reference payload's T | None needle already runs and prints False for None.
        Assert.Contains(cells, c => c.Form == Form.Optional && sharpyById[c.Id].Values is { } v
            && v.Contains("False") && v.SequenceEqual(pythonById[c.Id]) && divergences.All(d => d.Cell != c));
        // The element-optionality axis discriminates, container by container: a None needle into an optional element
        // is a member (CPython `True False True` over a member, a non-member and None), so a lift that answers False
        // for every None needle is red there, and a container whose optional variant lost its None element is red here.
        // The Sharpy half (the cell runs and agrees today) holds for every element-typed container but the named ones.
        var trueFalseTrue = new[] { "True", "False", "True" };
        var controlled = 0;
        foreach (var container in ElementTypedRoster)
        {
            controlled++;
            var controls = cells.Where(c => c.Container == container && c.OptionalElement && c.Form == Form.Optional
                && c.Operator == BinaryOperator.In && expectations[c.Id].Compared).ToList();
            Assert.True(controls.Any(c => pythonById[c.Id].SequenceEqual(trueFalseTrue)),
                $"{container}OfOptional: no compared T | None needle cell where CPython answers True False True "
                + "(the optional variant must hold a None element)");
            if (!OptionalElementControlExcluded.ContainsKey(container))
                Assert.True(controls.Any(c => pythonById[c.Id].SequenceEqual(trueFalseTrue)
                        && sharpyById[c.Id].Values is { } v && v.SequenceEqual(trueFalseTrue)),
                    $"{container}OfOptional: no cell where Sharpy also answers True False True (exempt it in "
                    + "OptionalElementControlExcluded with a reason and an issue, or fix it)");
            else
                Assert.False(controls.Any(c => sharpyById[c.Id].Values is { } v && v.SequenceEqual(trueFalseTrue)),
                    $"{container} is in OptionalElementControlExcluded but Sharpy now answers True False True: "
                    + "delete the exemption");
        }
        Assert.Equal(ElementTypedRoster.Length, controlled);

        // Written on every run, header included, so copying it over the allowlist is a byte-identical round trip
        // when nothing moved and a reviewable diff when something did.
        var proposedPath = Path.Combine(reportDir, "optional-membership-differential-proposed-allowlist.txt");
        File.WriteAllText(proposedPath, string.Join("\n", ProposedAllowlist(actualByRow, allowlist)) + "\n");

        if (problems.Count > 0)
        {
            var problemPath = Path.Combine(reportDir, "optional-membership-differential-pin-problems.txt");
            File.WriteAllLines(problemPath, problems);
            Assert.Fail($"{problems.Count} pin problem line(s) against {AllowlistFileName}: every line is in {problemPath}; "
                + $"a regenerated allowlist (rows' citations kept) is in {proposedPath}. First lines:\n"
                + string.Join("\n", problems.Take(30)));
        }
    }

    /// <summary>The first 16 hex digits of SHA-256 over the row's sorted <c>cell -&gt; verdict</c> lines.</summary>
    private static string Digest(IReadOnlyDictionary<string, string> cells)
    {
        var text = string.Concat(cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} -> {c.Value}\n"));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>The allowlist's header, emitted verbatim by <see cref="ProposedAllowlist"/>.</summary>
    private static readonly string[] AllowlistHeader =
    {
        "# Optional-needle membership differential allowlist (OptionalMembershipDifferentialTests).",
        "#",
        "# A row is '<container>/<needle>  cells=N  pin=HEX  # citation', needle = Plain | NoneNeedle (a T | None needle and",
        "# the literal None share a row). The '#= <cell> -> <observed Sharpy verdict>' lines under a row pin its EXACT",
        "# divergent cells: a pinned cell that starts passing, a cell of the row that starts diverging, or a pinned verdict",
        "# that changes fails the test, and the row's cells= and pin= (a SHA-256 prefix over its sorted pin lines) must match",
        "# them. On a failure the test writes every problem and a regenerated allowlist under .claude/tmp/; every",
        "# divergence (cell, CPython's answer, Sharpy's, the expectation) is written there on every run. Delete a row when",
        "# its last cell passes. Pin lines are comments, so scorecard_metrics.py counts one row per container/needle.",
    };

    private static IEnumerable<string> ProposedAllowlist(
        IReadOnlyDictionary<string, Dictionary<string, string>> actualByRow, IReadOnlyDictionary<string, PinnedRow> allowlist)
    {
        foreach (var line in AllowlistHeader)
            yield return line;
        foreach (var (row, cellsInRow) in actualByRow.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            var cite = allowlist.TryGetValue(row, out var old) ? old.Citation : "# see #<open issue>";
            yield return "";
            yield return $"{row}  cells={cellsInRow.Count}  pin={Digest(cellsInRow)}  {cite}";
            foreach (var (key, verdict) in cellsInRow.OrderBy(c => c.Key, StringComparer.Ordinal))
                yield return $"#= {key} -> {verdict}";
        }
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

    /// <summary>The fixed needle-slot types of the container axis (reflected <c>Contains</c> parameters of every
    /// non-element-typed container) that the catalog does not name: the payloads <see cref="SlotPayloads"/> must
    /// spell.</summary>
    private static IReadOnlyList<Type> RequiredSlotPayloadTypes() =>
        SpecRowMap.SelectMany(m => m.Generated)
            .Where(c => !IsElementTyped(c))
            .SelectMany(c => ContainsSlots(ClrTypeOf(c)!))
            .Where(t => PrimitiveCatalog.GetByClrType(t) == null)
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>Every canonical primitive is a payload or an exclusion with its reason — never neither, never both; and
    /// every fixed needle slot of the container axis is a payload's type (a catalog primitive, or a
    /// <see cref="SlotPayloads"/> entry), with no slot payload that is not such a slot.</summary>
    private static void AssertPayloadAxisIsTheCatalogAndTheSlots()
    {
        var canonical = CanonicalPrimitives();
        var payloads = GeneratePayloads();
        var generated = payloads.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var unaccounted = canonical.Where(n => !generated.Contains(n) && !ExcludedPrimitives.ContainsKey(n)).ToList();
        Assert.True(unaccounted.Count == 0,
            "primitives neither generated nor excluded (give them literals or an exclusion reason): " + string.Join(", ", unaccounted));
        var phantom = ExcludedPrimitives.Keys.Where(n => !canonical.Contains(n)).ToList();
        Assert.True(phantom.Count == 0, "excluded names that are not canonical primitives: " + string.Join(", ", phantom));
        Assert.DoesNotContain(generated, ExcludedPrimitives.ContainsKey);

        var required = RequiredSlotPayloadTypes().Select(t => t.FullName!).ToList();
        Assert.True(required.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(SlotPayloads.Keys.OrderBy(n => n, StringComparer.Ordinal)),
            "the container axis's fixed needle slots outside the catalog are [" + string.Join(", ", required)
            + "]; SlotPayloads spells [" + string.Join(", ", SlotPayloads.Keys.OrderBy(n => n, StringComparer.Ordinal)) + "]");
        Assert.All(payloads.Where(p => p.Kind == PayloadKind.Slot), p => Assert.NotNull(p.ClrType));
        // Every catalog-typed slot is a payload or an excluded primitive.
        var catalogSlots = SpecRowMap.SelectMany(m => m.Generated).Where(c => !IsElementTyped(c))
            .SelectMany(c => ContainsSlots(ClrTypeOf(c)!)).Select(PrimitiveCatalog.GetByClrType).OfType<PrimitiveCatalog.PrimitiveInfo>()
            .Select(i => i.SharpyName).Distinct().ToList();
        Assert.All(catalogSlots, n => Assert.True(generated.Contains(n) || ExcludedPrimitives.ContainsKey(n), $"slot primitive {n}"));
    }

    /// <summary>The CLR row is the census: the roster's keys are exactly the public types of Sharpy.Core and
    /// Sharpy.Stdlib with a public instance single-argument <c>Contains</c>; each generates one container or names why
    /// not; and every container of the CLR row comes from the roster.</summary>
    private static void AssertClrRowIsTheContainsCensus()
    {
        var census = SharpyTypes().Where(t => ContainsSlots(t).Count > 0).Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var roster = ClrContainerRoster.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(census.SequenceEqual(roster),
            "public single-argument Contains census [" + string.Join(", ", census) + "] vs ClrContainerRoster ["
            + string.Join(", ", roster) + "]: generate or exclude (with a reason) each type, and drop each phantom");
        foreach (var (name, (generated, excluded)) in ClrContainerRoster)
            Assert.True(generated.HasValue != (excluded != null), $"{name}: a container or an exclusion reason, exactly one");
        var fromRoster = ClrContainerRoster.Values.Where(v => v.Generated.HasValue).Select(v => v.Generated!.Value).ToList();
        Assert.Equal(fromRoster.Count, fromRoster.Distinct().Count());
        var clrRow = SpecRowMap.Single(m => m.Row.StartsWith("a CLR type", StringComparison.Ordinal)).Generated;
        Assert.All(clrRow, c => Assert.True(fromRoster.Contains(c) || c == EnumerableArm.Generated, $"{c}: not from the roster"));
        Assert.Contains(EnumerableArm.Generated, clrRow);
        Assert.DoesNotContain(EnumerableArm.Generated, fromRoster);
        // The representative IS of the arm it stands for: no Contains of its own, an IEnumerable<T>.
        var arm = SharpyTypesByName.Value[EnumerableArm.ClrName];
        Assert.Empty(ContainsSlots(arm));
        Assert.Contains(arm.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>Every exclusion names its reason: a primitive, a census type, a spec row's unexpanded part, the
    /// enumerable arm's representative, and an exemption from the optional-element control (which also names its
    /// issue). An empty or blank reason is red.</summary>
    private static void AssertEveryExclusionHasAReason()
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        Assert.All(ExcludedPrimitives, e => Assert.False(Blank(e.Value), $"primitive {e.Key}: no reason"));
        Assert.All(ClrContainerRoster.Where(r => r.Value.Generated == null),
            r => Assert.False(Blank(r.Value.Excluded), $"census type {r.Key}: no exclusion reason"));
        Assert.All(SpecRowMap.Where(m => m.Excluded != null || m.Generated.Length == 0),
            m => Assert.False(Blank(m.Excluded), $"spec row {m.Row}: no exclusion reason"));
        Assert.False(Blank(EnumerableArm.Why), "EnumerableArm: no reason");
        Assert.All(OptionalElementControlExcluded, e => Assert.True(
            !Blank(e.Value) && System.Text.RegularExpressions.Regex.IsMatch(e.Value, "#[0-9]+"),
            $"{e.Key}: an optional-element control exemption needs a reason and an issue"));
    }

    /// <summary>The container axis is the spec's "Container kinds" table: every row is mapped, every mapped row
    /// exists, and every <see cref="Container"/> comes from exactly one row.</summary>
    private static void AssertContainerAxisIsTheSpecTable()
    {
        var spec = PythonInterpreter.FindRepoFile("docs", "language_specification", "membership_operators.md")
            ?? throw new FileNotFoundException("docs/language_specification/membership_operators.md");
        var rows = new List<string>();
        var inTable = false;
        foreach (var line in File.ReadAllLines(spec))
        {
            if (line.StartsWith("### Container kinds", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }
            if (!inTable)
                continue;
            if (line.StartsWith("#", StringComparison.Ordinal))
                break;
            if (!line.StartsWith("|", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
                continue;
            var first = line.Split('|')[1].Trim();
            if (first != "container")
                rows.Add(first);
        }
        Assert.True(rows.Count >= 5, $"the spec's Container kinds table was not found ({rows.Count} rows)");
        var mapped = SpecRowMap.Select(m => m.Row).ToList();
        Assert.Equal(rows.OrderBy(r => r, StringComparer.Ordinal), mapped.OrderBy(r => r, StringComparer.Ordinal));
        var fromRows = SpecRowMap.SelectMany(m => m.Generated).ToList();
        Assert.Equal(fromRows.Count, fromRows.Distinct().Count());
        Assert.Equal(Enum.GetValues<Container>().OrderBy(c => c), fromRows.OrderBy(c => c));
    }

    /// <summary>A row: its key, the count and digest its header pins, its citation comment, and the pinned cells
    /// (<c>#= cell -&gt; verdict</c> comment lines under it, which <c>scorecard_metrics.py</c> does not count).</summary>
    private sealed record PinnedRow(int Count, string Pin, string Citation, Dictionary<string, string> Cells);

    private static Dictionary<string, PinnedRow> LoadAllowlist()
    {
        var rows = new Dictionary<string, PinnedRow>(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        if (path == null || !File.Exists(path))
            return rows;
        PinnedRow? current = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#=", StringComparison.Ordinal))
            {
                var arrow = line.IndexOf(" -> ", StringComparison.Ordinal);
                Assert.True(current != null && arrow > 2, $"{AllowlistFileName}: a pin line outside a row or without ' -> ': {raw}");
                current!.Cells[line[2..arrow].Trim()] = line[(arrow + 4)..].Trim();
                continue;
            }
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var citation = hash >= 0 ? line[hash..] : "";
            if (hash >= 0)
                line = line[..hash].Trim();
            if (line.Length == 0)
                continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var count = parts.FirstOrDefault(t => t.StartsWith("cells=", StringComparison.Ordinal));
            var pin = parts.FirstOrDefault(t => t.StartsWith("pin=", StringComparison.Ordinal));
            Assert.True(count != null && pin != null
                && System.Text.RegularExpressions.Regex.IsMatch(citation, @"#[0-9]+"),
                $"{AllowlistFileName}: a row needs 'cells=N pin=HEX # see #NNN' (an issue number): {raw}");
            current = new PinnedRow(int.Parse(count!["cells=".Length..], CultureInfo.InvariantCulture), pin!["pin=".Length..],
                citation, new Dictionary<string, string>(StringComparer.Ordinal));
            rows.Add(parts[0], current);
        }
        return rows;
    }
}

using Sharpy.Compiler.Parser.Ast;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using OrderMatrix = Sharpy.Compiler.Tests.Semantic.StaticInitializerOrderMatrixTests;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The cycle routes (plan-775c50's Testing Strategy, task M; #2457 cell 4, "<c>CycleThrough</c> beyond direct and def",
/// which R-LN rules to P77c and P77 generates here). The ring and slot cells read the cycle's other value in the
/// initializer's own text. A route is any other way the initializer reaches that read, and the cycle graph (D8) has one
/// rule for it: an edge is CERTAIN only where its read sits in the initializer's text under slots evaluated whenever the
/// initializer is. So a cycle closed through a route is never a compile error, and it is a run-time <c>NameError</c>:
/// the read meets a value whose initializer is still running (D3). A cycle closed through the text's own read is
/// SPY0278. Two sub-products, each the image of a source:
/// <list type="bullet">
/// <item><see cref="CycleRoutes"/>: a callee whose body holds the read, one per <see cref="BindingKind"/> that runs code
/// of the program when it is used (or excluded with its reason), plus <see cref="DirectRoute"/>, the read in the text,
/// the one certain route. Each rides every node kind of the graph (<see cref="NodeKinds"/>) in every host form a type's
/// value has (<see cref="HostForm"/>), on a class, and the <c>@static</c> field again on every other kind of host of the
/// static-initializer order matrix (<see cref="OrderMatrix.Hosts"/>), as a value that reads itself through the route and
/// as the first of two values whose second reads the first in its text (<see cref="RouteShape"/>). Each cell has its
/// control: the same program with the route's read replaced by a literal, which runs.</item>
/// <item><see cref="EvaluatedSlots"/>: every case of the D8 slot map that is not evaluated whenever its parent is, in a
/// text where it IS evaluated on this run. The slot family's own cell of the case, whose text never evaluates the read,
/// is its control, and runs.</item>
/// </list>
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    private const string RoutePrefix = "Slot/Route/";

    /// <summary>Where a route's read goes in its declaration and call.</summary>
    private const string RouteRead = "{r}";

    /// <summary>The value every control's route returns in place of the read.</summary>
    private const int RouteControlValue = 3;

    /// <summary>One way an initializer reaches a read. <see cref="Declaration"/> declares the callee after the values (a
    /// function sees a module value declared before it, rule N1); <see cref="Call"/> is the initializer's text. A route
    /// python has no twin of says why in <see cref="NoTwin"/>.</summary>
    internal sealed record CycleRoute(string Key, BindingKind? Kind, string[] Declaration, string Call, string? NoTwin = null)
    {
        /// <summary>The read sits in the initializer's own text, under a slot evaluated whenever it is: the one certain route.</summary>
        public bool IsCertain => Kind == null;
    }

    private const string SelflessProperty = "python's `@property` needs an instance: a selfless getter has no twin that reads the same";

    private const string PerObjectDefault = "python evaluates a class attribute once, where the class is written, not per object";

    private const string GenericAlias = "a closed generic type is an alias object in python";

    private const string StructDataclass = "a struct is a dataclass in python's twin, whose fields are constructor parameters";

    /// <summary>The direct route: the initializer's text reads the value, in <c>Parenthesized.Expression</c>, a slot the map
    /// evaluates whenever its parent is.</summary>
    internal static readonly CycleRoute DirectRoute = new("Direct", null, Array.Empty<string>(), $"({RouteRead})");

    /// <summary>The callee routes, keyed by the <see cref="BindingKind"/> whose use runs the callee's body. A kind with more
    /// than one form of body (a getter and a per-object default, a plain and a generic host) has one route per form.</summary>
    internal static readonly IReadOnlyList<CycleRoute> CycleRoutes = new[]
    {
        new CycleRoute("ModuleDef", BindingKind.ModuleDef, new[] { "def f0() -> int:", $"    return {RouteRead}" }, "f0()"),
        new CycleRoute("ModuleProperty.Getter", BindingKind.ModuleProperty, new[] { "property get p0() -> int:", $"    return {RouteRead}" }, "p0", SelflessProperty),
        new CycleRoute("StaticMethod.Plain", BindingKind.StaticMethod, new[] { "class R0:", "    @static", "    def f() -> int:", $"        return {RouteRead}" }, "R0.f()"),
        new CycleRoute("StaticMethod.Generic", BindingKind.StaticMethod, new[] { "class R0[E]:", "    @static", "    def f() -> int:", $"        return {RouteRead}" }, "R0[int].f()", GenericAlias),
        new CycleRoute("StaticProperty.Getter", BindingKind.StaticProperty, new[] { "class R0:", "    @static", "    property get p() -> int:", $"        return {RouteRead}" }, "R0.p", SelflessProperty),
        new CycleRoute("InstanceMethod", BindingKind.InstanceMethod, new[] { "class R0:", "    def f(self) -> int:", $"        return {RouteRead}" }, "R0().f()"),
        new CycleRoute("InstanceProperty.Getter", BindingKind.InstanceProperty, new[] { "class R0:", "    property get p(self) -> int:", $"        return {RouteRead}" }, "R0().p"),
        new CycleRoute("InstanceProperty.Default", BindingKind.InstanceProperty, new[] { "class R0:", $"    property k: int = {RouteRead}" }, "R0().k", PerObjectDefault),
        new CycleRoute("InstanceField", BindingKind.InstanceField, new[] { "class R0:", $"    k: int = {RouteRead}" }, "R0().k", PerObjectDefault),
        new CycleRoute("Constructor", BindingKind.Constructor, new[] { "class R0:", "    v: int", "    def __init__(self) -> None:", $"        self.v = {RouteRead}" }, "R0().v"),
        new CycleRoute("ModuleStruct", BindingKind.ModuleStruct, new[] { "struct S0:", "    v: int", "    def __init__(self) -> None:", $"        self.v = {RouteRead}" }, "S0().v", StructDataclass),
        new CycleRoute("NestedType", BindingKind.NestedType, new[] { "class D0:", "    class K0:", "        v: int", "        def __init__(self) -> None:", $"            self.v = {RouteRead}" }, "D0.K0().v"),
    };

    private const string RunsNoCode = "a value: reading it runs no code of the program, so its read is the text's own, which the ring and slot cells hold";

    private const string ReachedThroughConstructor = "a type is reached through its constructor, the Constructor route";

    private const string ReachedThroughSlot = "runs only a lambda it is passed or holds, whose body is the slot LambdaExpression.Body: an evaluated slot's cell";

    private const string NoCodeOfItsOwn = "its value is built by the compiler, and its use runs no code the program writes";

    private const string ReachedThroughImplementation = "an interface's code runs through an object of a class that implements it: the InstanceMethod route";

    /// <summary>The kinds no route is keyed by. A position-local kind has no body to be declared in beside an initializer;
    /// another module's value is the project route's.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, Exclusion> ExcludedRouteKinds = new Dictionary<BindingKind, Exclusion>
    {
        [BindingKind.ModuleVar] = new(RunsNoCode),
        [BindingKind.ModuleLet] = new("a module `let` is refused where it is declared: no program holds one", "let t = 3\n" + MainPrintingT, "SPY0340"),
        [BindingKind.ModuleLiteralConst] = new(RunsNoCode),
        [BindingKind.ModuleComputedConst] = new(RunsNoCode),
        [BindingKind.ModuleClass] = new(ReachedThroughConstructor),
        [BindingKind.ModuleEnum] = new(NoCodeOfItsOwn),
        [BindingKind.ModuleUnion] = new(NoCodeOfItsOwn),
        [BindingKind.ModuleInterface] = new(ReachedThroughImplementation),
        [BindingKind.ModuleTypeAlias] = new(ReachedThroughConstructor),
        [BindingKind.StaticField] = new(RunsNoCode),
        [BindingKind.ClassLiteralConst] = new(RunsNoCode),
        [BindingKind.ClassComputedConst] = new(RunsNoCode),
        [BindingKind.EnclosingLocal] = new(NoEnclosingBody),
        [BindingKind.Parameter] = new(NoEnclosingBody),
        [BindingKind.Inner] = new(NoEnclosingBody),
        [BindingKind.LocalVar] = new(NoEnclosingBody),
        [BindingKind.LocalLet] = new(NoEnclosingBody),
        [BindingKind.LocalLiteralConst] = new(NoEnclosingBody),
        [BindingKind.LocalComputedConst] = new(NoEnclosingBody),
        [BindingKind.LocalDef] = new(NoEnclosingBody),
        [BindingKind.LambdaTypedLocal] = new(ReachedThroughSlot),
        [BindingKind.Builtin] = new(ReachedThroughSlot),
        [BindingKind.ImportedName] = new(OtherModule),
        [BindingKind.None] = new(NoBinding),
    };

    /// <summary>How the cycle closes: the value reads itself through the route, or it reads a second value of its kind and
    /// host form, whose initializer reads the first in its own text (a certain edge back). A closed literal set.</summary>
    internal enum RouteShape
    {
        Self,
        Pair,
    }

    /// <summary>A value on the cycle: a node kind of the graph, whether its type is generic, and the kind of type that
    /// declares it, one of the static-initializer order matrix's hosts (<see cref="OrderMatrix.Hosts"/>).</summary>
    internal sealed record RouteEndpoint(NodeKind Node, HostForm Form, string Kind = DefaultHostKind)
    {
        public string Key => $"{Node.Kind}{(Form == HostForm.Plain ? "" : "+" + Form)}{(Kind == DefaultHostKind ? "" : "." + Kind)}";

        public bool IsModule => IsModuleHolder(Node.Kind) || Node.Kind == BindingKind.ModuleProperty;

        public string[] Declare(string name, string initializer)
        {
            var lines = Node.Declare(name, initializer).ToList();
            if (IsModule)
                return lines.ToArray();
            // The node kind's header is a class's: the order matrix's header for the kind, the type renamed, in its place.
            var type = lines[0]["class ".Length..^1];
            var header = OrderMatrix.Hosts.Single(h => h.Name == Kind).Header
                .Select(l => l.Replace("Host:", Form == HostForm.Generic ? $"{type}[E]:" : $"{type}:", StringComparison.Ordinal));
            return header.Concat(lines.Skip(1)).ToArray();
        }

        public string Read(string name) => Form == HostForm.Generic ? Node.Read(name).Replace(".", "[int].", StringComparison.Ordinal) : Node.Read(name);
    }

    /// <summary>The order matrix's class host: every node kind rides it.</summary>
    private const string DefaultHostKind = "class";

    /// <summary>Every node kind, in every host form its value has (a module has no type parameter), on a class; and the
    /// <c>@static</c> field again on each other kind of host of the order matrix.</summary>
    internal static IEnumerable<RouteEndpoint> RouteEndpoints() =>
        from node in NodeKinds
        from form in Enum.GetValues<HostForm>()
        from kind in OrderMatrix.Hosts.Select(h => h.Name)
        let endpoint = new RouteEndpoint(node, form, kind)
        where (form == HostForm.Plain || !endpoint.IsModule) && (kind == DefaultHostKind || (node.Kind == BindingKind.StaticField))
        select endpoint;

    private const string ModuleVariableReadsItself =
        "a module variable is invisible in its own initializer, and a later one in an earlier initializer, until rule N1 lands (#2423), which is not this plan's: a read in the text names neither";

    /// <summary>Why a route has no cell on an endpoint in a shape, or null. The text's own read of a module variable is
    /// rule N1's; and the direct route's pair is a ring cell (<see cref="RingCells"/>).</summary>
    internal static string? RouteExclusion(CycleRoute route, RouteEndpoint endpoint, RouteShape shape)
    {
        if (!route.IsCertain)
            return null;
        if (shape == RouteShape.Pair)
            return "the text's own read of a second value is a ring cell on the slot Parenthesized.Expression";
        return endpoint.Node.Kind == BindingKind.ModuleVar ? ModuleVariableReadsItself : null;
    }

    private static string RouteId(CycleRoute route, RouteEndpoint endpoint, RouteShape shape, bool control) =>
        $"{RoutePrefix}{route.Key}/{endpoint.Key}/{shape}{(control ? "/Control" : "")}";

    /// <summary>The cycle's program: <c>x</c>'s initializer is the route's call, the route reads <c>x</c> (or <c>y</c>, whose
    /// initializer reads <c>x</c>), and <c>main</c> prints the values. The control's route reads a literal. Python's twin
    /// runs top to bottom, so the callee comes first there.</summary>
    private static string RouteProgram(CycleRoute route, RouteEndpoint endpoint, RouteShape shape, bool control, bool twin)
    {
        var x = endpoint.Read("x");
        var read = control ? $"{RouteControlValue}" : shape == RouteShape.Self ? x : endpoint.Read("y");
        var callee = route.Declaration.Select(l => l.Replace(RouteRead, read, StringComparison.Ordinal)).ToList();
        var values = new List<string>(endpoint.Declare("x", route.Call.Replace(RouteRead, read, StringComparison.Ordinal)));
        if (shape == RouteShape.Pair)
            values.AddRange(endpoint.Declare("y", $"{x} + 1"));
        var body = twin ? callee.Concat(values) : values.Concat(callee);
        var printed = shape == RouteShape.Self ? x : $"{x}, {endpoint.Read("y")}";
        return WithHelpers(Harness.Join(body) + Harness.MainBlock($"print({printed})"));
    }

    /// <summary>The rule (D8 with D3): a cycle closed through the certain route is SPY0278; through any other it compiles,
    /// and initializing the value meets its own initializer running: <c>NameError</c>. A control prints what its route
    /// returns, and one more for the second value.</summary>
    internal static string RouteModel(RouteShape shape, bool control) =>
        !control ? CycleError : shape == RouteShape.Self ? $"{RouteControlValue}" : $"{RouteControlValue} {RouteControlValue + 1}";

    /// <summary>Python's twin, where it has one: a plain host, a route python spells the same, and the value that reads
    /// itself, whose load stops on its own name. A pair's twin stops on the forward read of the second value, which N3
    /// initializes first: an order-only error, so no twin.</summary>
    internal static PythonExpectation RoutePython(CycleRoute route, RouteEndpoint endpoint, RouteShape shape, bool control) =>
        route.NoTwin != null || endpoint.Form == HostForm.Generic || shape == RouteShape.Pair ? PythonExpectation.NoTwin
        : control ? PythonExpectation.Prints
        : PythonExpectation.NameError;

    private static IEnumerable<CycleRoute> AllRoutes() => CycleRoutes.Prepend(DirectRoute);

    private static IEnumerable<(CycleRoute Route, RouteEndpoint Endpoint, RouteShape Shape)> RouteProduct() =>
        from route in AllRoutes()
        from endpoint in RouteEndpoints()
        from shape in Enum.GetValues<RouteShape>()
        where RouteExclusion(route, endpoint, shape) == null
        select (route, endpoint, shape);

    internal static IEnumerable<Cell> RouteCells()
    {
        foreach (var (route, endpoint, shape) in RouteProduct())
        {
            foreach (var control in new[] { false, true })
            {
                var python = RoutePython(route, endpoint, shape, control);
                var refused = !control && route.IsCertain;
                yield return new Cell(
                    RouteId(route, endpoint, shape, control),
                    Family.Slot,
                    RouteProgram(route, endpoint, shape, control, twin: false),
                    python == PythonExpectation.NoTwin ? null : RouteProgram(route, endpoint, shape, control, twin: true),
                    refused ? SlotCycle : RouteModel(shape, control))
                {
                    RefusalCode = refused ? SlotCycle : null,
                    Raises = !control && !route.IsCertain ? CycleError : null,
                    // The control reads no value on the cycle: it runs today and must keep running.
                    IsControl = control,
                    Python = python,
                    NameErrorOn = python == PythonExpectation.NameError ? RootName(endpoint.Read("x")) : null,
                    Route = static (verdict, _, _) => ValueRoute(verdict),
                };
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The slots the map does not evaluate whenever their parent is, evaluated on this run
    // ------------------------------------------------------------------------------------------- //

    private const string EvaluatedPrefix = RoutePrefix + "Evaluated/";

    /// <summary>One text per conditional case of the slot map (<see cref="SlotProduct"/>'s ids), with the read in the same
    /// slot, evaluated on this run; and python's spelling where Sharpy's has none there.</summary>
    internal static readonly IReadOnlyDictionary<string, (string Sharpy, string? Twin)> EvaluatedSlots = new Dictionary<string, (string, string?)>(StringComparer.Ordinal)
    {
        ["Slot/ListComprehension.Element"] = ("len([{d} for v0 in range(1)])", null),
        ["Slot/ListComprehension.Clauses:ForClause.Iterator/Later"] = ("len([v1 for v0 in range(1) for v1 in range({d})])", null),
        ["Slot/ListComprehension.Clauses:IfClause.Condition"] = ("len([v0 for v0 in range(1) if {d} > 0])", null),
        ["Slot/GeneratorExpression.Element"] = ("sum({d} for v0 in range(1))", null),
        ["Slot/GeneratorExpression.Clauses:ForClause.Iterator/Later"] = ("sum(v1 for v0 in range(1) for v1 in range({d}))", null),
        ["Slot/GeneratorExpression.Clauses:IfClause.Condition"] = ("sum(v0 for v0 in range(1) if {d} > 0)", null),
        ["Slot/SetComprehension.Element"] = ("len({{d} for v0 in range(1)})", null),
        ["Slot/SetComprehension.Clauses:ForClause.Iterator/Later"] = ("len({v1 for v0 in range(1) for v1 in range({d})})", null),
        ["Slot/SetComprehension.Clauses:IfClause.Condition"] = ("len({v0 for v0 in range(1) if {d} > 0})", null),
        ["Slot/DictComprehension.Key"] = ("len({{d}: v0 for v0 in range(1)})", null),
        ["Slot/DictComprehension.Value"] = ("len({v0: {d} for v0 in range(1)})", null),
        ["Slot/DictComprehension.Clauses:ForClause.Iterator/Later"] = ("len({v1: 1 for v0 in range(1) for v1 in range({d})})", null),
        ["Slot/DictComprehension.Clauses:IfClause.Condition"] = ("len({v0: 1 for v0 in range(1) if {d} > 0})", null),
        ["Slot/DictSpreadComprehension.Spread"] = ("len({**{v0: {d}} for v0 in range(1)})", "len({v0: {d} for v0 in range(1)})"),
        ["Slot/DictSpreadComprehension.Clauses:ForClause.Iterator/Later"] = (
            "len({**{v1: 1} for v0 in range(1) for v1 in range({d})})", "len({v1: 1 for v0 in range(1) for v1 in range({d})})"),
        ["Slot/DictSpreadComprehension.Clauses:IfClause.Condition"] = ("len({**{v0: 1} for v0 in range(1) if {d} > 0})", "len({v0: 1 for v0 in range(1) if {d} > 0})"),
        ["Slot/ComparisonChain.Operands/Later"] = ("int(0 < 1 < {d})", null),
        ["Slot/ConditionalExpression.ThenValue"] = ("{d} if 1 > 0 else 5", null),
        ["Slot/ConditionalExpression.ElseValue"] = ("5 if 1 < 0 else {d}", null),
        ["Slot/LambdaExpression.Body"] = ("ap(lambda: {d})", null),
        ["Slot/MatchExpression.Arms.Pattern/Later"] = ("match 1:\n    case 2: 5\n    case {d}: 6\n    case _: 0", "5 if 1 == 2 else (6 if 1 == {d} else 0)"),
        ["Slot/MatchExpression.Arms.Guard"] = ("match 1:\n    case 1 if {d} > 0: 3\n    case _: 5", "3 if 1 == 1 and {d} > 0 else 5"),
        ["Slot/MatchExpression.Arms.Result"] = ("match 1:\n    case 1: {d}\n    case _: 5", "{d} if 1 == 1 else 5"),
        [$"Slot/{BinaryRight}/{BinaryOperator.And}"] = ("int(1 > 0 and {d} > 0)", null),
        [$"Slot/{BinaryRight}/{BinaryOperator.Or}"] = ("int(1 < 0 or {d} > 0)", null),
        [$"Slot/{BinaryRight}/{BinaryOperator.NullCoalesce}"] = ("none0() ?? {d}", "none0() or {d}"),
    };

    /// <summary>The conditional cases of the slot map, from its own entries: the slots whose read the map takes for no edge.</summary>
    private static IEnumerable<(string Id, string Key)> ConditionalSlotCases() =>
        SlotProduct().Where(s => !s.Entry.Stores && s.Entry.When == Evaluated.Conditionally).Select(s => (s.Id, s.Key));

    private static string EvaluatedId(string slotId) => EvaluatedPrefix + slotId["Slot/".Length..];

    /// <summary>One cell per conditional case. The read is evaluated, so the program meets the cycle at run time; it is no
    /// certain edge, so it compiles. Python's reader sits first, and its read names a class not made yet, as the slot
    /// family's twins say.</summary>
    internal static IEnumerable<Cell> EvaluatedSlotCells()
    {
        foreach (var (id, _) in ConditionalSlotCases())
        {
            if (!EvaluatedSlots.TryGetValue(id, out var text))
                continue;
            yield return new Cell(EvaluatedId(id), Family.Slot, SlotProgram(text.Sharpy, readerFirst: false), SlotProgram(text.Twin ?? text.Sharpy, readerFirst: true), CycleError)
            {
                Raises = CycleError,
                Python = PythonExpectation.NameError,
                NameErrorOn = SlotMarker,
                Route = static (verdict, _, _) => ValueRoute(verdict),
            };
        }
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> RouteExclusions() =>
        ExcludedRouteKinds.Select(e => ($"route BindingKind.{e.Key}", e.Value))
            .Append((
                "the direct route on a module variable",
                new Exclusion(ModuleVariableReadsItself, RouteProgram(DirectRoute, new RouteEndpoint(NodeKinds.Single(n => n.Kind == BindingKind.ModuleVar), HostForm.Plain), RouteShape.Self, control: false, twin: false), "SPY0200")));

    internal static List<string> RouteTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();

        // The routes are the image of BindingKind: each kind keys a route or is excluded, once.
        foreach (var kind in Enum.GetValues<BindingKind>())
        {
            bool generated = CycleRoutes.Any(r => r.Kind == kind), excluded = ExcludedRouteKinds.ContainsKey(kind);
            if (generated == excluded)
                failures.Add($"route BindingKind.{kind} is {Both(generated)}");
        }
        foreach (var dup in AllRoutes().GroupBy(r => r.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            failures.Add($"route {dup.Key} is declared twice");
        foreach (var route in AllRoutes())
        {
            if (route.Kind != null && CycleRoutes.Count(r => r.Kind == route.Kind) > 1 && !route.Key.StartsWith(route.Kind + ".", StringComparison.Ordinal))
                failures.Add($"route {route.Key} shares BindingKind.{route.Kind} and its key does not name its form");
            if (!route.Call.Contains(RouteRead, StringComparison.Ordinal) && !route.Declaration.Any(l => l.Contains(RouteRead, StringComparison.Ordinal)))
                failures.Add($"route {route.Key} holds no read");
            if (route.IsCertain != (route.Declaration.Length == 0))
                failures.Add($"route {route.Key}: the certain route reads in the text, and every other in a callee's body");
        }
        // The direct route's read sits in a slot the map evaluates whenever its parent is, and is the ring's certain slot.
        var direct = ReadSlots(DirectRoute.Call.Replace(RouteRead, SlotRead, StringComparison.Ordinal)).Slots;
        if (!direct.Contains(RingSlots[0]) || !Slots.TryGetValue(RingSlots[0], out var ring) || ring[0].When != Evaluated.Always)
            failures.Add($"the direct route's read sits in [{string.Join(", ", direct)}], which is not the ring's certain slot");

        // The endpoints are the graph's node kinds in every host form a type's value has.
        var endpoints = RouteEndpoints().ToList();
        if (OrderMatrix.Hosts.Length < 3 || OrderMatrix.Hosts.All(h => h.Name != DefaultHostKind))
            failures.Add($"the order matrix's hosts [{string.Join(", ", OrderMatrix.Hosts.Select(h => h.Name))}] hold no class, or read nothing");
        foreach (var node in NodeKinds)
        {
            foreach (var form in Enum.GetValues<HostForm>())
            {
                foreach (var kind in OrderMatrix.Hosts.Select(h => h.Name))
                {
                    var isModule = IsModuleHolder(node.Kind) || node.Kind == BindingKind.ModuleProperty;
                    var owedHere = (form == HostForm.Plain || !isModule) && (kind == DefaultHostKind || (!isModule && node.Kind == BindingKind.StaticField));
                    if (owedHere != endpoints.Any(e => e.Node == node && e.Form == form && e.Kind == kind))
                        failures.Add($"route endpoint {node.Kind} in HostForm.{form} on a {kind} is {(owedHere ? "owed and not generated" : "generated against the rule")}");
                }
            }
        }
        foreach (var answer in new[] { true, false }.Where(a => !NodeKinds.Any(n => n.IsConst == a)))
            failures.Add($"no route endpoint is {(answer ? "" : "no ")}const");

        // Each route's cells, and the rule's two answers on each endpoint.
        var product = RouteProduct().ToList();
        foreach (var endpoint in endpoints)
        {
            // A module variable's text cannot name it yet (rule N1): that exclusion carries its program.
            if (!product.Any(c => c.Endpoint == endpoint && c.Route.IsCertain) && RouteExclusion(DirectRoute, endpoint, RouteShape.Self) != ModuleVariableReadsItself)
                failures.Add($"route endpoint {endpoint.Key} has no certain route: no cell of it is refused");
            if (!product.Any(c => c.Endpoint == endpoint && !c.Route.IsCertain && c.Shape == RouteShape.Pair))
                failures.Add($"route endpoint {endpoint.Key} has no pair that closes through a callee");
        }

        // The evaluated slots are the image of the slot map's conditional cases.
        var cases = ConditionalSlotCases().ToList();
        foreach (var (id, key) in cases)
        {
            if (!EvaluatedSlots.TryGetValue(id, out var text))
            {
                failures.Add($"{id} is a conditional case of the slot map and has no evaluated text");
                continue;
            }
            foreach (var t in new[] { text.Sharpy, text.Twin ?? text.Sharpy })
            {
                if (!t.Contains(Operand, StringComparison.Ordinal))
                    failures.Add($"{EvaluatedId(id)}: '{t}' holds no read");
            }
            var (slots, rights, _) = ReadSlots(text.Sharpy.Replace(Operand, SlotRead, StringComparison.Ordinal));
            var holds = key == BinaryRight ? rights.Contains(Enum.Parse<BinaryOperator>(id[(id.LastIndexOf('/') + 1)..])) : slots.Contains(key);
            if (!holds)
                failures.Add($"{EvaluatedId(id)}: the read of '{text.Sharpy}' sits in [{string.Join(", ", slots.OrderBy(s => s, StringComparer.Ordinal))}], not in {key}");
            // Its control is the slot family's own cell of the case: the read not evaluated, which runs.
            if (pool.FirstOrDefault(c => c.Id == id) is not { IsControl: true })
                failures.Add($"{EvaluatedId(id)} has no control: the slot family's {id} is not a control in the pool");
        }
        foreach (var id in EvaluatedSlots.Keys.Where(k => cases.All(c => c.Id != k)))
            failures.Add($"{id} has an evaluated text and is no conditional case of the slot map");
        if (cases.Count < 20)
            failures.Add($"the slot map has {cases.Count} conditional cases: it reads nothing");

        // What the axes owe, written again from the enumerations and the exclusion predicate. A control reads no value on
        // the cycle; an evaluated slot is none.
        var owed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var route in CycleRoutes.Prepend(DirectRoute))
            foreach (var node in NodeKinds)
                foreach (var form in Enum.GetValues<HostForm>())
                    foreach (var kind in OrderMatrix.Hosts.Select(h => h.Name))
                        foreach (var shape in Enum.GetValues<RouteShape>())
                        {
                            var endpoint = new RouteEndpoint(node, form, kind);
                            if ((form == HostForm.Generic && endpoint.IsModule) || (kind != DefaultHostKind && node.Kind != BindingKind.StaticField)
                                || RouteExclusion(route, endpoint, shape) != null)
                                continue;
                            owed[RouteId(route, endpoint, shape, control: false)] = false;
                            owed[RouteId(route, endpoint, shape, control: true)] = true;
                        }
        foreach (var (id, _) in cases)
            owed[EvaluatedId(id)] = false;
        failures.AddRange(PoolAgainstOwed("the route axes", owed, pool.Where(c => c.Id.StartsWith(RoutePrefix, StringComparison.Ordinal))));
        return failures;
    }
}

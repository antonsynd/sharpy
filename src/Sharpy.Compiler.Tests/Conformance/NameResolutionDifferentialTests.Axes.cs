namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>P77b's axes over the families: each axis is read from its roster in <c>NameResolutionDifferentialTests.Rosters.cs</c>
/// and fed back through <c>RosterFedFailures</c>, so a roster value the generator drops is red.</summary>
public partial class NameResolutionDifferentialTests
{
    // ------------------------------------------------------------------------------------------- //
    // P77b axis 1: comprehension kind × clause × slot
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The expression text of every slot of a comprehension, and its clauses. Each kind renders the slots it has.</summary>
    private sealed record ComprehensionParts(string Element, string Key, string Value, string Spread, string Clauses);

    private static string RenderComprehension(ComprehensionKind kind, ComprehensionParts p) => kind switch
    {
        ComprehensionKind.List => $"[{p.Element} {p.Clauses}]",
        ComprehensionKind.Set => $"{{{p.Element} {p.Clauses}}}",
        ComprehensionKind.Dict => $"{{{p.Key}: {p.Value} {p.Clauses}}}",
        ComprehensionKind.DictSpread => $"{{**{p.Spread} {p.Clauses}}}",
        // A generator is consumed where it is written, so every slot of it reads at the same moment the list kind's does.
        ComprehensionKind.Generator => $"list(({p.Element} {p.Clauses}))",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ComprehensionType(ComprehensionKind kind) => kind switch
    {
        ComprehensionKind.List or ComprehensionKind.Generator => "list[int]",
        ComprehensionKind.Set => "set[int]",
        ComprehensionKind.Dict or ComprehensionKind.DictSpread => "dict[int, int]",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>How <c>main</c> reads the one int out of the comprehension's value: a set and a dict's keys by iteration, a dict's
    /// value by its constant key 0, a list by index.</summary>
    private static string ComprehensionExtract(ComprehensionKind kind, ComprehensionSlot slot, string value) => kind switch
    {
        ComprehensionKind.Set => $"[w0 for w0 in {value}][0]",
        ComprehensionKind.Dict when slot == ComprehensionSlot.Key => $"[w0 for w0 in {value}][0]",
        _ => $"{value}[0]",
    };

    /// <summary>A READ's parts: <paramref name="name"/> in the site's slot, every other slot the loop variable or a constant, so
    /// the extracted int is the name's value and a wrong binding or a storage default prints differently.</summary>
    private static ComprehensionParts ReadParts(ComprehensionSite site, string name, int value)
    {
        var p = new ComprehensionParts("v0", "0", "v0", "{0: v0}", "for v0 in [1]");
        return site.Slot switch
        {
            ComprehensionSlot.Element => p with { Element = name },
            ComprehensionSlot.Key => p with { Key = name, Value = "0" },
            ComprehensionSlot.Value => p with { Value = name },
            ComprehensionSlot.Spread => p with { Spread = $"{{0: {name}}}" },
            ComprehensionSlot.Iterable => p with
            {
                Clauses = site.Clause == ComprehensionClauseShape.SecondFor ? $"for v1 in [1] for v0 in [{name}]" : $"for v0 in [{name}]",
            },
            ComprehensionSlot.Condition => p with { Clauses = $"for v0 in [{Str(value)}, 0] if v0 == {name}" },
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "not a read slot"),
        };
    }

    /// <summary>The walrus STORE's parts: <c>(x := v0 * 2)</c> in the site's slot, python's 6.</summary>
    private static ComprehensionParts WalrusParts(ComprehensionSite site)
    {
        const string walrus = "(x := v0 * 2)";
        var p = new ComprehensionParts("v0", "0", "v0", "{0: v0}", "for v0 in [3]");
        return site.Slot switch
        {
            ComprehensionSlot.Element => p with { Element = walrus },
            ComprehensionSlot.Key => p with { Key = walrus, Value = "0" },
            ComprehensionSlot.Value => p with { Value = walrus },
            ComprehensionSlot.Spread => p with { Spread = $"{{0: {walrus}}}" },
            ComprehensionSlot.Condition => p with { Clauses = $"for v0 in [3] if {walrus} > 0" },
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "not a walrus slot"),
        };
    }

    /// <summary>The LOCAL target's parts: <c>x</c> is the clause's target, read in every element slot.</summary>
    private static ComprehensionParts TargetParts(ComprehensionSite site) =>
        new("x", "x", "x", "{x: x}", site.Clause == ComprehensionClauseShape.SecondFor ? "for v1 in [1] for x in [1, 2]" : "for x in [1, 2]");

    /// <summary>A READ of the module's <c>x</c> from a comprehension slot (the list kind's arm, unchanged per kind: python's
    /// comprehension scoping is kind-independent, p06). A function body reads at the call; a <c>@static</c> initializer reads at
    /// load, so a later module binding is python's NameError there and N3's run.</summary>
    private static NameCell ComprehensionReadCell(ComprehensionSite site, ReadPosition host, Order order)
    {
        int value = ValueOf(BindingKind.ModuleVar);
        var comprehension = RenderComprehension(site.Kind, ReadParts(site, ReadName.Plain.Name, value));
        var type = ComprehensionType(site.Kind);
        var (reader, access) = host switch
        {
            ReadPosition.FunctionBody => (new List<string> { $"def r() -> {type}:", $"    return {comprehension}" },
                ComprehensionExtract(site.Kind, site.Slot, "r()")),
            ReadPosition.StaticInitializer => (new List<string> { "class H:", "    @static", $"    r: {type} = {comprehension}" },
                ComprehensionExtract(site.Kind, site.Slot, "H.r")),
            _ => throw new ArgumentOutOfRangeException(nameof(host), host, "not a comprehension host"),
        };
        var declaration = ModuleDecl(BindingKind.ModuleVar, ReadName.Plain);
        var lines = order == Order.Earlier ? declaration.Concat(reader) : reader.Concat(declaration);
        var expected = host == ReadPosition.StaticInitializer && order == Order.Later
            ? Run(PyOutcome.Name(ReadName.Plain.Name), value, "N3")
            : Run(value, "N1-nesting");
        return new NameCell
        {
            Id = CellId("read", "comprehension", site.Kind, site.Clause, site.Slot, host, order),
            Family = Family.Read,
            Source = Join(lines) + MainBlock($"print({access})"),
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = BindingKind.ModuleVar,
            Outer = BindingKind.None,
            Position = host,
            Comprehension = site,
        };
    }

    /// <summary>The clause's target read after the comprehension: it does not leak, so the read is the outer (LocalTarget's
    /// comprehension arm, per kind).</summary>
    private static NameCell ComprehensionTargetCell(ComprehensionSite site, BindingKind outer)
    {
        var stmts = new[]
        {
            $"ys: {ComprehensionType(site.Kind)} = {RenderComprehension(site.Kind, TargetParts(site))}",
            "print(x)",
        };
        var expected = outer == BindingKind.None ? Refuse(PyOutcome.Name("x"), "N5") : Run(ValueOf(outer), "N1");
        return LocalProgram(CellId("local", "comprehension", site.Kind, site.Clause, site.Slot, $"outer={outer}"),
                BindingKind.LocalVar, outer, ReadPosition.FunctionBody, stmts, expected, null, ReadName.Plain, LocalTarget.Comprehension)
            with
        { Comprehension = site };
    }

    /// <summary>A walrus in a comprehension slot, read after it: python binds the enclosing function's <c>x</c>; Sharpy keeps it
    /// in the comprehension (<c>walrus-in-comprehension-scope</c>), so the read is the outer or refused.</summary>
    private static NameCell ComprehensionWalrusCell(ComprehensionSite site, ReadPosition host, BindingKind outer)
    {
        if (host != ReadPosition.FunctionBody)
            throw new ArgumentOutOfRangeException(nameof(host), host, "the walrus host axis generates the function body only");
        var stmts = new[]
        {
            $"ys: {ComprehensionType(site.Kind)} = {RenderComprehension(site.Kind, WalrusParts(site))}",
            "print(x)",
        };
        var (body, call) = Host(host, stmts);
        var moduleOuter = outer == BindingKind.ModuleVar;
        var lines = moduleOuter ? ModuleDecl(BindingKind.ModuleVar, ReadName.Plain).Concat(body) : body;
        return new NameCell
        {
            Id = CellId("store", "walrus-in", site.Kind, site.Clause, site.Slot, $"outer={outer}"),
            Family = Family.Store,
            Source = Join(lines) + MainBlock(call.ToArray()),
            Tag = moduleOuter ? SharpyTag.MustRun : SharpyTag.MustRefuse,
            Model = moduleOuter ? Str(ValueOf(BindingKind.ModuleVar)) : null,
            Python = PyOutcome.Ran(6),
            Rule = "N5",
            Deviation = "walrus-in-comprehension-scope",
            Target = outer,
            Outer = outer,
            Position = ReadPosition.ComprehensionElement,
            Form = StoreForm.Walrus,
            Comprehension = site,
        };
    }

    /// <summary>Axis 1's cells: every (kind, clause, slot) of the roster — a READ at each host × order, the target's LOCAL read,
    /// and a walrus STORE at each slot and host <see cref="NameResolutionRosters.ComprehensionWalrusSlots"/> generates.</summary>
    private static IEnumerable<NameCell> ComprehensionAxisCells()
    {
        var outers = new[] { BindingKind.None, BindingKind.ModuleVar };
        foreach (var site in NameResolutionRosters.ComprehensionSites())
        {
            if (site.Slot == ComprehensionSlot.Target)
            {
                foreach (var u in outers)
                    yield return ComprehensionTargetCell(site, u);
                continue;
            }

            foreach (var host in NameResolutionRosters.ComprehensionHosts)
                foreach (var o in new[] { Order.Earlier, Order.Later })
                    yield return ComprehensionReadCell(site, host, o);

            if (!NameResolutionRosters.ComprehensionWalrusSlots[site.Slot].Generated)
                continue;
            foreach (var host in NameResolutionRosters.ComprehensionWalrusHosts.Where(e => e.Value.Generated).Select(e => e.Key))
                foreach (var u in outers)
                    yield return ComprehensionWalrusCell(site, host, u);
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 2: N3 cycle × route
    // ------------------------------------------------------------------------------------------- //

    private static bool IsModuleEndpoint(BindingKind k) => NameResolutionRosters.ModuleLevelKinds.Contains(k);

    /// <summary>The initializer position a cycle endpoint's own declaration carries.</summary>
    private static ReadPosition InitializerPositionOf(BindingKind k) => k switch
    {
        BindingKind.ModuleVar or BindingKind.ModuleComputedConst => ReadPosition.ModuleInitializer,
        BindingKind.ModuleProperty => ReadPosition.ModulePropertyInitializer,
        _ => SelfPositionOf(k) ?? throw new ArgumentOutOfRangeException(nameof(k), k, "no initializer of its own"),
    };

    /// <summary>A two-endpoint N3 cycle: <c>a</c>'s initializer reads <c>b</c> (directly or through a helper <c>def</c>) and
    /// <c>b</c>'s reads <c>a</c>, <c>a</c> declared first. A type endpoint is a member of <c>H</c> (the first type) or <c>J</c> (the
    /// second), read through the route's spelling; a module endpoint is read bare. python raises NameError on the name the first
    /// read spells, which is unbound while <c>a</c> initializes (N3 bullet 4: never a default).</summary>
    private static NameCell CycleCell(CycleRoute route)
    {
        var a = new ReadName("a", "", "", null);
        var b = new ReadName("b", "", "", null);
        var firstOwner = "H";
        var secondOwner = IsModuleEndpoint(route.First) ? "H" : "J";
        var aliases = new List<string>();

        string Read(BindingKind kind, ReadName n, string owner)
        {
            if (IsModuleEndpoint(kind))
                return n.Name;
            if (route.Spelling == Spelling.Alias)
            {
                var alias = owner == "H" ? "A0" : "A1";
                if (!aliases.Contains($"type {alias} = {owner}"))
                    aliases.Add($"type {alias} = {owner}");
                return $"{alias}.{n.Name}";
            }
            return route.Spelling == Spelling.Qualified ? $"{owner}.{n.Name}"
                : throw new ArgumentOutOfRangeException(nameof(route), route, "a type endpoint is read qualified or through an alias");
        }

        var readOfSecond = Read(route.Second, b, secondOwner);
        var readOfFirst = Read(route.First, a, firstOwner);
        var firstRead = readOfSecond.Split('.')[0];

        var lines = new List<string>();
        string firstInit = readOfSecond;
        if (route.Through == CycleThrough.ThroughDef)
        {
            lines.AddRange(new[] { "def g0() -> int:", $"    return {readOfSecond}" });
            firstInit = "g0()";
        }

        List<string> Declare(BindingKind kind, ReadName n, string owner, string init) => IsModuleEndpoint(kind)
            ? ModuleDecl(kind, n, init)
            : new[] { $"class {owner}:" }.Concat(Indent(MemberDecl(kind, n, init))).ToList();

        lines.AddRange(Declare(route.First, a, firstOwner, firstInit));
        lines.AddRange(Declare(route.Second, b, secondOwner, readOfFirst));
        lines.AddRange(aliases);

        string Access(BindingKind kind, ReadName n, string owner) => IsModuleEndpoint(kind) ? n.Name : $"{owner}.{n.Name}";
        var source = Join(lines) + MainBlock($"print({Access(route.First, a, firstOwner)})", $"print({Access(route.Second, b, secondOwner)})");

        var expected = Cycle(PyOutcome.Name(firstRead), "N3-cycle");
        return new NameCell
        {
            Id = CellId("read", "cycle", route.Pair, route.Spelling, route.Through, route.First, route.Second),
            Family = Family.Read,
            Source = source,
            Tag = expected.Tag,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = route.First,
            Outer = BindingKind.None,
            Position = InitializerPositionOf(route.First),
            Cycle = route,
        };
    }

    private static IEnumerable<NameCell> CycleAxisCells() => NameResolutionRosters.CycleRouteCells().Select(CycleCell);

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 3: observation {value, order}
    // ------------------------------------------------------------------------------------------- //

    private static readonly string[] OrderHelper = { "def f(tag: str) -> int:", "    print(\"init \" + tag)", "    return 1" };

    /// <summary>The order observation (seed A11's shape, probe p14): the kind's initializer and a sibling's call a printing helper,
    /// <c>main</c> prints first. A module or <c>@static</c> kind's sibling is a module variable and python's order is load order,
    /// top to bottom, before <c>main</c> (N3 bullet 2); an instance kind's sibling is an instance field and the order is
    /// construction order, after <c>main</c>'s first line. Earlier / later is the kind's declaration above / below its sibling.</summary>
    private static NameCell OrderCell(BindingKind kind, Order order)
    {
        var a = new ReadName("a", "", "", null);
        var b = new ReadName("b", "", "", null);
        const string initA = "f(\"a\")";
        const string initB = "f(\"b\")";
        var earlier = order == Order.Earlier;
        var firstTwo = earlier ? new[] { "init a", "init b" } : new[] { "init b", "init a" };
        var lines = new List<string>(OrderHelper);
        string model;
        string rule;
        string? twin = null;
        string[] main;
        if (NameResolutionRosters.InstanceKinds.Contains(kind))
        {
            var own = MemberDecl(kind, a, initA);
            var sibling = MemberDecl(BindingKind.InstanceField, b, initB);
            lines.Add("class H:");
            lines.AddRange(Indent(earlier ? own.Concat(sibling) : sibling.Concat(own)));
            main = new[] { "print(\"main\")", "h0: H = H()", "print(h0.a + h0.b)" };
            model = JoinOut("main", firstTwo[0], firstTwo[1], 2);
            rule = "construction-order";

            // Python has no instance-field initializer: the renderer's class attribute runs once at class creation, so the twin is
            // the same initializers in `__init__`, python's spelling of a per-construction initializer, in declaration order.
            var assigns = (earlier ? new[] { "a", "b" } : new[] { "b", "a" }).Select(n => $"        self.{n} = f(\"{n}\")");
            twin = Join(OrderHelper.Concat(new[] { "class H:", "    a: int = 0", "    b: int = 0", "    def __init__(self) -> None:" }).Concat(assigns))
                + MainBlock(main);
        }
        else
        {
            var own = NameResolutionRosters.ModuleLevelKinds.Contains(kind)
                ? ModuleDecl(kind, a, initA)
                : new[] { "class H:" }.Concat(Indent(MemberDecl(kind, a, initA))).ToList();
            var sibling = ModuleDecl(BindingKind.ModuleVar, b, initB);
            lines.AddRange(earlier ? own.Concat(sibling) : sibling.Concat(own));
            var access = NameResolutionRosters.ModuleLevelKinds.Contains(kind) ? "a" : "H.a";
            main = new[] { "print(\"main\")", $"print({access} + b)" };
            model = JoinOut(firstTwo[0], firstTwo[1], "main", 2);
            rule = "N3-order";
        }

        return new NameCell
        {
            Id = CellId("read", "order", kind, order),
            Family = Family.Read,
            Source = Join(lines) + MainBlock(main),
            TwinSource = twin,
            Tag = SharpyTag.MustRun,
            Model = model,
            Python = PyOutcome.Ran(model),
            Rule = rule,
            Target = kind,
            Outer = BindingKind.None,
            Position = InitializerPositionOf(kind),
            Observed = Observation.Order,
        };
    }

    /// <summary>Axis 3's cells: every kind <see cref="NameResolutionRosters.OrderObservationKinds"/> generates × {earlier, later}.</summary>
    private static IEnumerable<NameCell> OrderAxisCells() =>
        from e in NameResolutionRosters.OrderObservationKinds.Where(e => e.Value.Generated).OrderBy(e => e.Key)
        from o in new[] { Order.Earlier, Order.Later }
        select OrderCell(e.Key, o);

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 4: LOCAL read timing
    // ------------------------------------------------------------------------------------------- //

    private static NameCell Timed(ReadTiming timing, NameCell cell) => cell with { Timing = timing };

    /// <summary>A closure taken BEFORE a construct binds the name, called after it (p11). python's construct binds the function's
    /// own <c>x</c>, so the closure reads it; Sharpy's block-scoped target leaves the closure on the outer (S2, R-KZ, #2425), or
    /// on nothing (N5). The comprehension's target does not leak in either language (N1); <c>out let</c> binds the function
    /// scope, so the closure reads a later binding of its own scope (R-LC's L28: N5-declared-later, #2425).</summary>
    private static NameCell ClosureBeforeCell(LocalTarget t, BindingKind outer)
    {
        var (construct, last) = ConstructOf(t, "x");
        var stmts = new List<string> { "g: () -> int = lambda: x" };
        stmts.AddRange(construct);
        stmts.Add("print(g())");
        var python = t switch
        {
            LocalTarget.ExceptAs => PyOutcome.Name("x"),
            // Under an enclosing function's `x`, CPython 3.12+ (PEP 709's inlined comprehension) leaves the closure's `x` unbound
            // after a comprehension whose target is `x` (3.13.18: NameError "cannot access free variable"; the generator-expression
            // spelling, which is not inlined, prints 13). The rule is N1's either way: the target does not leak.
            LocalTarget.Comprehension => outer is BindingKind.None or BindingKind.EnclosingLocal ? PyOutcome.Name("x") : PyOutcome.Ran(ValueOf(outer)),
            _ => PyOutcome.Ran(last!),
        };
        var expected = t switch
        {
            LocalTarget.Comprehension => outer == BindingKind.None ? Refuse(python, "N5") : Run(python, ValueOf(outer), "N1"),
            LocalTarget.OutLet => Refuse(python, "N5-declared-later"),
            _ => Refuse(python, outer == BindingKind.None ? "N5" : "S2"),
        };
        return LocalProgram(CellId("local", "closure-before", t, $"outer={outer}"), BindingKind.LocalVar, outer, ReadPosition.FunctionBody,
                stmts, expected, null, ReadName.Plain, t)
            with
        { Timing = ReadTiming.ClosureBeforeConstruct };
    }

    /// <summary>A read at a loop's next iteration of a binding made below it in the body (R-KX, #2425): python reads the previous
    /// iteration's value; under Sharpy's block scope the read is above its binding.</summary>
    private static NameCell BackEdgeCell(string family, BindingKind binder, BindingKind outer)
    {
        var (open, close) = family switch
        {
            "for-body" => (new[] { "for i0 in range(2):" }, Array.Empty<string>()),
            "while-body" => (new[] { "i0: int = 0", "while i0 < 2:" }, new[] { "    i0 += 1" }),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "not a loop-body family"),
        };
        var binding = binder switch
        {
            BindingKind.LocalLet => "let x = 13",
            BindingKind.LocalVar => "x: int = 13",
            _ => throw new ArgumentOutOfRangeException(nameof(binder), binder, "the back edge's binders are let and annotated"),
        };
        var stmts = new List<string>(open) { "    if i0 > 0:", "        print(x)", "    " + binding };
        stmts.AddRange(close);
        return LocalProgram(CellId("local", "back-edge", family, binder, $"outer={outer}"), binder, outer, ReadPosition.FunctionBody,
                stmts, Refuse(PyOutcome.Ran(13), "N5-declared-later"), null, ReadName.Plain)
            with
        { Timing = ReadTiming.BackEdge };
    }

    /// <summary>A capture whose guard fails, read after the match (p10): python bound the capture before the guard ran (R-KZ).</summary>
    private static NameCell FailedGuardCell(LocalTarget capture, BindingKind outer)
    {
        var pattern = capture switch
        {
            LocalTarget.MatchCapture => "case x if x > 9:",
            LocalTarget.AsPatternCapture => "case 2 as x if x > 9:",
            _ => throw new ArgumentOutOfRangeException(nameof(capture), capture, "not a pattern capture"),
        };
        var stmts = new[] { "match 2:", "    " + pattern, "        pass", "    case _:", "        pass", "print(x)" };
        var expected = Refuse(PyOutcome.Ran(2), outer == BindingKind.None ? "N5" : "S2");
        return LocalProgram(CellId("local", "failed-guard", capture, $"outer={outer}"), BindingKind.LocalVar, outer, ReadPosition.FunctionBody,
                stmts, expected, null, ReadName.Plain, capture)
            with
        { Timing = ReadTiming.FailedGuard };
    }

    /// <summary>Axis 4's new timings: closure-before × every LocalTarget × outer {None, ModuleVar, EnclosingLocal}; back edge ×
    /// loop family × binder {let, annotated} × outer {None, ModuleVar}; failed guard × capture × outer {None, ModuleVar}. The two
    /// same-block timings are LocalCells' below / above arms.</summary>
    private static IEnumerable<NameCell> ReadTimingAxisCells()
    {
        foreach (var t in Enum.GetValues<LocalTarget>())
            foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar, BindingKind.EnclosingLocal })
                yield return ClosureBeforeCell(t, u);
        foreach (var family in NameResolutionRosters.BackEdgeLoopFamilies)
            foreach (var binder in new[] { BindingKind.LocalLet, BindingKind.LocalVar })
                foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                    yield return BackEdgeCell(family, binder, u);
        foreach (var capture in NameResolutionRosters.FailedGuardCaptures)
            foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                yield return FailedGuardCell(capture, u);
    }
}

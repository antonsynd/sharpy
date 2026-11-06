using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

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

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 5: let-in binder × outer
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The let-in product's fed check (P77b axis 5): every (binder, family) pair of <see cref="LocalKinds"/> ×
    /// <see cref="BlockShapes"/> has a cell at each outer that crosses every family, every (binder, outer) pair of the
    /// <see cref="NameResolutionRosters.LetInOuters"/> image has a cell, and no let-in cell sits on a binder or an outer the
    /// rosters do not generate.</summary>
    private static IEnumerable<string> LetInProduct(IReadOnlyList<NameCell> pool)
    {
        var letIn = pool.Where(c => c.Family == Family.Local && c.Id.StartsWith("local/let-in/", StringComparison.Ordinal)).ToList();
        bool Has(BindingKind k, string family, BindingKind outer) =>
            letIn.Any(c => c.Target == k && c.Outer == LetInOuterFor(k, outer) && c.Id.StartsWith($"local/let-in/{family}/", StringComparison.Ordinal));

        var outers = NameResolutionRosters.LetInOuters.Values.SelectMany(e => e.Image).Append(NameResolutionRosters.LetInNoOuter).ToHashSet();
        foreach (var (k, _) in NameResolutionRosters.LocalKinds.Where(e => e.Value.Generated))
        {
            foreach (var b in BlockShapes)
                foreach (var u in NameResolutionRosters.LetInEveryFamilyOuters.Where(u => !Has(k, b.Family, u)))
                    yield return $"LetInProduct: binder {k} × family {b.Family} × outer {u} has no let-in cell";
            foreach (var u in outers.Where(u => !Has(k, NameResolutionRosters.StoreNestingFamily, u)))
                yield return $"LetInProduct: binder {k} × outer {u} has no let-in cell";
        }
        foreach (var c in letIn.Where(c => !(NameResolutionRosters.LocalKinds.TryGetValue(c.Target, out var m) && m.Generated)))
            yield return $"{c.Id}: a let-in cell on binder {c.Target}, which LocalKinds does not generate";
        foreach (var c in letIn.Where(c => !outers.Any(u => LetInOuterFor(c.Target, u) == c.Outer)))
            yield return $"{c.Id}: a let-in cell on outer {c.Outer}, which LetInOuters does not generate";
    }

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 6: closure over a loop-BODY binding (R-LG)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The reads of axis 6: the first closure, and every closure in order.</summary>
    private static readonly (string Name, string Text)[] LoopBodyClosureReads = { ("first", "print(fs[0]())"), ("all", "print([g0() for g0 in fs])") };

    /// <summary>The loop-body statement binding <c>y</c> from the loop counter <c>i</c>.</summary>
    private static string[] LoopBodyBinder(BindingKind binder) => binder switch
    {
        BindingKind.LocalVar => new[] { "y: int = i" },
        BindingKind.LocalLet => new[] { "let y = i" },
        BindingKind.LocalComputedConst => new[] { "const y: int = i" },
        BindingKind.LocalDef => new[] { "def y() -> int:", "    return i" },
        BindingKind.LambdaTypedLocal => new[] { "y: () -> int = lambda: i" },
        _ => throw new ArgumentOutOfRangeException(nameof(binder), binder, "not an axis-6 binder"),
    };

    /// <summary>The block a binder sits in inside the loop body, by its roster-8 family (or none: the loop body itself).</summary>
    private static (string[] Open, int Depth) LoopBodyEnclosure(string enclosure) => enclosure switch
    {
        NameResolutionRosters.LoopBodyDirectEnclosure => (Array.Empty<string>(), 0),
        "with" => (new[] { "with CM() as w0:" }, 1),
        "except" => (new[] { "try:", "    raise ValueError(\"v\")", "except ValueError:" }, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(enclosure), enclosure, "not a generated axis-6 enclosure"),
    };

    /// <summary>A closure over a binding made in a loop body, called after the loop (R-LG, p18): Sharpy's binding is fresh per
    /// iteration, so the closures read 0, 1, 2; python's is the function's one binding, read at the call (2).
    /// A callable binder's body reads the counter: a <c>for</c> counter is itself fresh per iteration (the same row), a
    /// <c>while</c> counter is the function's one variable in both languages, so there the closures agree on its last value.</summary>
    private static NameCell LoopBodyClosureCell(BindingKind binder, string loop, string enclosure, (string Name, string Text) read)
    {
        var (open, close) = loop switch
        {
            "for-body" => (new[] { "for i in range(3):" }, Array.Empty<string>()),
            "while-body" => (new[] { "i: int = 0", "while i < 3:" }, new[] { "    i += 1" }),
            _ => throw new ArgumentOutOfRangeException(nameof(loop), loop, "not a loop-body family"),
        };
        var (enclosureOpen, depth) = LoopBodyEnclosure(enclosure);
        var pad = new string(' ', 4 * (1 + depth));
        var call = IsCallableKind(binder) ? "y()" : "y";
        var stmts = new List<string> { "fs: list[() -> int] = []" };
        stmts.AddRange(open);
        stmts.AddRange(Indent(enclosureOpen));
        stmts.AddRange(LoopBodyBinder(binder).Select(l => pad + l));
        stmts.Add($"{pad}fs.append(lambda: {call})");
        stmts.AddRange(close);
        stmts.Add(read.Text);

        var shared = IsCallableKind(binder) && loop == "while-body";
        string Out(int each) => read.Name == "first" ? Str(shared ? 3 : each) : shared ? "[3, 3, 3]" : each == 0 ? "[0, 1, 2]" : $"[{each}, {each}, {each}]";
        var model = Out(0);
        var python = Out(2);
        var e = new Expected(SharpyTag.MustRun, model, PyOutcome.Ran(python), "R-LG", model == python ? null : "loop-variable-fresh-per-iteration");
        return LocalProgram(CellId("local", "closure-in-loop-body", binder, loop, enclosure, read.Name), binder, BindingKind.None,
            ReadPosition.FunctionBody, stmts, e, null, ReadName.Plain);
    }

    /// <summary>Axis 6's cells: every generated binder × loop family × enclosure × read.</summary>
    private static IEnumerable<NameCell> LoopBodyClosureAxisCells() =>
        from b in NameResolutionRosters.LoopBodyClosureBinders.Where(e => e.Value.Generated).Select(e => e.Key).OrderBy(k => k)
        from loop in NameResolutionRosters.BackEdgeLoopFamilies
        from enclosure in LoopBodyEnclosureNames()
        from read in LoopBodyClosureReads
        select LoopBodyClosureCell(b, loop, enclosure, read);

    private static IEnumerable<string> LoopBodyEnclosureNames() =>
        new[] { NameResolutionRosters.LoopBodyDirectEnclosure }
            .Concat(NameResolutionRosters.LoopBodyEnclosures.Where(e => e.Value.Generated).Select(e => e.Key).OrderBy(f => f, StringComparer.Ordinal));

    /// <summary>Axis 6's fed check (deferred finding 4): every <see cref="LocalKinds"/> binder is a row of
    /// <see cref="NameResolutionRosters.LoopBodyClosureBinders"/> or out with its reason, and every generated (binder, loop,
    /// enclosure) has a cell at each read; no axis-6 cell sits off the rosters.</summary>
    private static IEnumerable<string> LoopBodyClosureProduct(IReadOnlyList<NameCell> pool)
    {
        var cells = pool.Where(c => c.Family == Family.Local && c.Id.StartsWith("local/closure-in-loop-body/", StringComparison.Ordinal)).ToList();
        var enclosures = new[] { NameResolutionRosters.LoopBodyDirectEnclosure }
            .Concat(NameResolutionRosters.LoopBodyEnclosures.Where(e => e.Value.Generated).Select(e => e.Key)).ToList();
        foreach (var k in LocalKinds)
        {
            if (!NameResolutionRosters.LoopBodyClosureBinders.TryGetValue(k, out var m) || m.Generated == (m.Excluded != null))
            {
                yield return $"LoopBodyClosureBinders: LocalKinds binder {k} has neither a row nor an exclusion with its reason";
                continue;
            }
            if (!m.Generated)
                continue;
            foreach (var loop in NameResolutionRosters.BackEdgeLoopFamilies)
                foreach (var enclosure in enclosures)
                    foreach (var (read, _) in LoopBodyClosureReads.Where(r => !cells.Any(c => c.Id == CellId("local", "closure-in-loop-body", k, loop, enclosure, r.Name))))
                        yield return $"LoopBodyClosure: binder {k} × {loop} × {enclosure} × read {read} has no cell";
        }
        foreach (var c in cells.Where(c => !(NameResolutionRosters.LoopBodyClosureBinders.TryGetValue(c.Target, out var m) && m.Generated)))
            yield return $"{c.Id}: an axis-6 cell on binder {c.Target}, which LoopBodyClosureBinders does not generate";
        foreach (var c in cells.Where(c => !enclosures.Any(e => c.Id.Contains($"/{e}/", StringComparison.Ordinal))))
            yield return $"{c.Id}: an axis-6 cell in an enclosure LoopBodyEnclosures does not generate";
    }

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 7: the struct host (R-KT)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The spellings a struct-host body reads its member through: <c>self.</c> only where <c>self</c> is in scope.</summary>
    private static IEnumerable<Spelling> StructHostSpellings(ReadPosition p) =>
        IsSelfBearing(p) ? new[] { Spelling.SelfDot, Spelling.Bare, Spelling.Qualified } : new[] { Spelling.Bare, Spelling.Qualified };

    /// <summary>A READ class cell in a <c>struct</c> host: the class host's arm (N1, N7, the methods rule) at every position,
    /// except <c>self.</c> on an instance member captured by a C# closure — R-KT (#2434): Sharpy refuses what C# refuses
    /// (CS1673), never wider.</summary>
    private static NameCell StructHostCell(BindingKind t, ReadPosition p, Nesting nesting, Order order, Spelling s)
    {
        var cell = ClassCell(t, p, nesting, order, s, BindingKind.None, ReadName.Plain, TypeKind.Struct);
        return s == Spelling.SelfDot && NameResolutionRosters.StructSelfCaptureClosures.Contains((p, nesting))
            && NameResolutionRosters.StructSelfCaptureTargets.Contains(t)
            ? cell with { Tag = SharpyTag.MustRefuse, Model = null, Rule = "R-KT" }
            : cell;
    }

    /// <summary>Axis 7's cells: the struct host × every <c>ClassBodies</c> position × target × spelling at the earlier order, the
    /// later order on the method body, and <c>self.</c> through a stored lambda at the two lambda nestings.</summary>
    private static IEnumerable<NameCell> StructHostAxisCells()
    {
        if (!(NameResolutionRosters.HostKinds.TryGetValue(TypeKind.Struct, out var host) && host.Generated))
            yield break;
        foreach (var t in NameResolutionRosters.StructHostTargets)
        {
            foreach (var p in ClassBodies)
                foreach (var s in StructHostSpellings(p))
                {
                    yield return StructHostCell(t, p, Nesting.Direct, Order.Earlier, s);
                    if (p == ReadPosition.MethodBody)
                        yield return StructHostCell(t, p, Nesting.Direct, Order.Later, s);
                }
            foreach (var p in NameResolutionRosters.StructLambdaNestings)
                yield return StructHostCell(t, p, Nesting.Lambda, Order.Earlier, Spelling.SelfDot);
        }
    }

    /// <summary>Axis 7's fed check: every generated host kind has a cell, every struct (position, target, spelling) has its cell,
    /// R-KT is exactly the self-dot cells at the closure set, and no cell rides a host HostKinds does not generate.</summary>
    private static IEnumerable<string> HostProduct(IReadOnlyList<NameCell> pool)
    {
        var read = pool.Where(c => c.Family == Family.Read).ToList();
        foreach (var (k, _) in NameResolutionRosters.HostKinds.Where(e => e.Value.Generated))
            if (!read.Any(c => (c.Host ?? TypeKind.Class) == k))
                yield return $"HostKinds {k} is generated by the roster and has no READ cell";
        foreach (var c in read.Where(c => c.Host is TypeKind h && !(NameResolutionRosters.HostKinds.TryGetValue(h, out var m) && m.Generated)))
            yield return $"{c.Id}: a READ cell in a {c.Host} host, which HostKinds does not generate";

        var structCells = read.Where(c => c.Host == TypeKind.Struct).ToList();
        if (NameResolutionRosters.HostKinds.TryGetValue(TypeKind.Struct, out var st) && st.Generated)
            foreach (var t in NameResolutionRosters.StructHostTargets)
                foreach (var p in ClassBodies)
                    foreach (var s in StructHostSpellings(p).Where(s => !structCells.Any(c => c.Target == t && c.Position == p && c.Id.Contains($"/{p}/Earlier/{s}/", StringComparison.Ordinal))))
                        yield return $"HostKinds Struct × {p} × {t} × {s} has no READ cell";
        foreach (var c in structCells)
        {
            var closure = NameResolutionRosters.StructSelfCaptureClosures.Any(e => c.Position == e.Position
                && c.Id.Contains(e.Nesting == Nesting.Direct ? $"/{e.Position}/Earlier/" : $"/{e.Position}/{e.Nesting}/", StringComparison.Ordinal));
            var selfDot = c.Id.Contains("/SelfDot/", StringComparison.Ordinal);
            var captures = NameResolutionRosters.StructSelfCaptureTargets.Contains(c.Target);
            if ((c.Rule == "R-KT") != (closure && selfDot && captures))
                yield return $"{c.Id}: rule {c.Rule}, and R-KT is exactly struct × self. × an instance member × the C# closure positions";
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // P77b axis 8: the attribute STORE target (R-KU, R-LH)
    // ------------------------------------------------------------------------------------------- //

    private static string ReceiverText(AttributeReceiver r) => r switch
    {
        AttributeReceiver.SelfDot => "self.",
        AttributeReceiver.Type => "H.",
        AttributeReceiver.Alias => "A.",
        AttributeReceiver.Derived => "D.",
        AttributeReceiver.InstanceLocal => "h0.",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };

    /// <summary>Axis 8's product: receiver × member × <c>=</c> at the method body; form × {method, init} body × {instance,
    /// <c>@static</c>} field × {<c>self.</c>, <c>H.</c>}; form × {static method, function} body × the same fields × <c>H.</c> —
    /// the union, so a coordinate two parts share is one cell. <c>self.</c> exists only at a self-bearing body (the rest is
    /// python's NameError on <c>self</c>, a type-system agreement and no name verdict), and the instance local only at a body
    /// that can construct its own class without recursing (not <c>__init__</c>).</summary>
    private static IEnumerable<(AttributeReceiver Receiver, BindingKind Member, StoreForm Form, ReadPosition Body)> AttributeStoreProduct()
    {
        var members = NameResolutionRosters.AttributeStoreMembers.Where(e => e.Value.Generated).Select(e => e.Key).OrderBy(k => k).ToList();
        var forms = NameResolutionRosters.AttributeStoreForms.Where(e => e.Value.Generated).Select(e => e.Key).OrderBy(f => f).ToList();
        var product = new List<(AttributeReceiver, BindingKind, StoreForm, ReadPosition)>();
        foreach (var r in Enum.GetValues<AttributeReceiver>())
            foreach (var m in members)
                product.Add((r, m, StoreForm.Assign, ReadPosition.MethodBody));
        foreach (var f in forms)
            foreach (var body in new[] { ReadPosition.MethodBody, ReadPosition.InitBody })
                foreach (var m in NameResolutionRosters.AttributeStoreFormMembers)
                    foreach (var r in new[] { AttributeReceiver.SelfDot, AttributeReceiver.Type })
                        product.Add((r, m, f, body));
        foreach (var f in forms)
            foreach (var body in new[] { ReadPosition.StaticMethodBody, ReadPosition.FunctionBody })
                foreach (var m in NameResolutionRosters.AttributeStoreFormMembers)
                    product.Add((AttributeReceiver.Type, m, f, body));
        return product.Distinct().Where(e => e.Item1 != AttributeReceiver.SelfDot || IsSelfBearing(e.Item4));
    }

    /// <summary>An attribute STORE (P77b axis 8): <c>&lt;receiver&gt;x = v</c> (or <c>+=</c>) in the body, then a read that
    /// tells which binding the store wrote: a type-level member through <c>H.x</c> (python's store through an instance or a
    /// derived class makes a new attribute there and leaves <c>H.x</c> alone, p03), an instance member through the receiver, a
    /// method through a call on the receiver. The stored value has the member's type (a method takes <c>lambda: 9</c>), so the
    /// only refusal is the store's own. Verdict by (receiver class, member kind): an instance member through <c>self.</c> /
    /// <c>h0.</c> and a <c>@static</c> member through <c>H.</c> / <c>A.</c> run (N7); a <c>@static</c> member through
    /// <c>self.</c> / <c>D.</c> / <c>h0.</c> is R-KU's refusal (#2435); a const is SPY0225's, a method SPY0606's (R-LH), an
    /// instance member through a type SPY0290's, by kind.</summary>
    private static NameCell AttributeStoreCell(AttributeReceiver r, BindingKind m, StoreForm f, ReadPosition body)
    {
        var recv = ReceiverText(r);
        var callable = IsCallableKind(m);
        var stmts = new List<string>();
        if (r == AttributeReceiver.InstanceLocal)
            stmts.Add("h0: H = H()");
        stmts.Add($"{recv}x {(f == StoreForm.Augmented ? "+=" : "=")} {(callable ? "lambda: 9" : "9")}");
        var instanceRead = r is AttributeReceiver.SelfDot or AttributeReceiver.InstanceLocal;
        // An instance member stored through a type is read back through this instance where the body has one (constructing
        // H inside its own __init__ would recurse), else through a new instance.
        var instanceBack = instanceRead ? $"{recv}x" : IsSelfBearing(body) ? "self.x" : "H().x";
        stmts.Add("print(" + (callable ? $"{recv}x()" : IsInstance(m) ? instanceBack : "H.x") + ")");

        var members = MemberDecl(m, ReadName.Plain);
        var (host, call) = Host(body, stmts, members);
        var lines = new List<string>();
        if (body is ReadPosition.FunctionBody)
        {
            lines.Add("class H:");
            lines.AddRange(Indent(members));
        }
        lines.AddRange(host);
        if (r == AttributeReceiver.Derived)
            lines.AddRange(new[] { "class D(H):", "    pass" });
        if (r == AttributeReceiver.Alias)
            lines.Add("type A = H");

        int original = ValueOf(m);
        int stored = f == StoreForm.Augmented ? original + 9 : 9;
        var throughType = r is AttributeReceiver.Type or AttributeReceiver.Alias;
        Expected e;
        if (callable)
            e = Refuse(PyOutcome.Ran(9), "kind:SPY0606");
        else if (m is BindingKind.ClassLiteralConst or BindingKind.ClassComputedConst)
            e = Refuse(PyOutcome.Ran(throughType ? stored : original), "kind:SPY0225");
        else if (IsInstance(m))
            e = instanceRead ? Run(stored, "N7") : Refuse(PyOutcome.Ran(throughType ? stored : original), "kind:SPY0290");
        else
            e = throughType ? Run(stored, "N7") : Refuse(PyOutcome.Ran(original), "R-KU");

        return new NameCell
        {
            Id = CellId("store", "attribute", r, m, f, body),
            Family = Family.Store,
            Source = WithHelpers(Join(lines) + MainBlock(call.ToArray())),
            Tag = e.Tag,
            Model = e.Model,
            Python = e.Python,
            Rule = e.Rule,
            Target = m,
            Outer = BindingKind.None,
            Position = body,
            Form = f,
            TargetShape = "MemberAccess",
            Receiver = r,
        };
    }

    /// <summary>Axis 8's cells: the MemberAccess arm of <see cref="NameResolutionRosters.AssignmentTargetShapes"/>.</summary>
    private static IEnumerable<NameCell> AttributeStoreAxisCells() =>
        NameResolutionRosters.AssignmentTargetShapes.TryGetValue("MemberAccess", out var m) && m.Generated
            ? AttributeStoreProduct().Select(e => AttributeStoreCell(e.Receiver, e.Member, e.Form, e.Body))
            : Enumerable.Empty<NameCell>();

    /// <summary>Axis 8's fed check: every generated target shape has a STORE cell (Identifier: the bare forms; MemberAccess: the
    /// attribute cells), every receiver, generated member and generated form has an attribute cell, the R-KU rule is exactly
    /// the <c>@static</c> value members through a non-type receiver, and no attribute cell sits off the rosters.</summary>
    private static IEnumerable<string> AttributeStoreFed(IReadOnlyList<NameCell> pool)
    {
        var store = pool.Where(c => c.Family == Family.Store).ToList();
        var attribute = store.Where(c => c.TargetShape == "MemberAccess").ToList();
        foreach (var (shape, m) in NameResolutionRosters.AssignmentTargetShapes.Where(e => e.Value.Generated))
        {
            var has = shape == "Identifier" ? store.Any(c => c.TargetShape == null && c.Form is StoreForm.Assign or StoreForm.Augmented) : store.Any(c => c.TargetShape == shape);
            if (!has)
                yield return $"AssignmentTargetShapes {shape} is generated by the roster and has no STORE cell";
        }
        foreach (var c in store.Where(c => c.TargetShape != null && !(NameResolutionRosters.AssignmentTargetShapes.TryGetValue(c.TargetShape, out var m) && m.Generated)))
            yield return $"{c.Id}: a STORE cell on target shape {c.TargetShape}, which AssignmentTargetShapes does not generate";
        if (!(NameResolutionRosters.AssignmentTargetShapes.TryGetValue("MemberAccess", out var ma) && ma.Generated))
            yield break;
        foreach (var r in Enum.GetValues<AttributeReceiver>().Where(r => !attribute.Any(c => c.Receiver == r)))
            yield return $"AttributeReceiver {r} has no attribute STORE cell";
        foreach (var (k, _) in NameResolutionRosters.AttributeStoreMembers.Where(e => e.Value.Generated && !attribute.Any(c => c.Target == e.Key)))
            yield return $"AttributeStoreMembers {k} is generated by the roster and has no attribute STORE cell";
        foreach (var (f, _) in NameResolutionRosters.AttributeStoreForms.Where(e => e.Value.Generated && !attribute.Any(c => c.Form == e.Key)))
            yield return $"AttributeStoreForms {f} is generated by the roster and has no attribute STORE cell";
        foreach (var c in attribute)
        {
            if (!(NameResolutionRosters.AttributeStoreMembers.TryGetValue(c.Target, out var m) && m.Generated))
                yield return $"{c.Id}: an attribute store on {c.Target}, which AttributeStoreMembers does not generate";
            if (!(c.Form is StoreForm f && NameResolutionRosters.AttributeStoreForms.TryGetValue(f, out var fm) && fm.Generated))
                yield return $"{c.Id}: an attribute store in form {c.Form}, which AttributeStoreForms does not generate";
            if (c.Receiver == AttributeReceiver.SelfDot && c.Position is ReadPosition p && !IsSelfBearing(p))
                yield return $"{c.Id}: self. at {p}, where no self is in scope";
            var rku = c.Target is BindingKind.StaticField or BindingKind.StaticProperty
                && c.Receiver is AttributeReceiver.SelfDot or AttributeReceiver.Derived or AttributeReceiver.InstanceLocal;
            if ((c.Rule == "R-KU") != rku)
                yield return $"{c.Id}: rule {c.Rule}, and R-KU is exactly a @static value member stored through self. / D. / h0.";
        }
    }
}

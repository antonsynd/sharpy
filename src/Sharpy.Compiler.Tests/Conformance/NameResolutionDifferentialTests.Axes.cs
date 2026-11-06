using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>P77b's generated axes (D2's table): each reads its roster, and <see cref="AxisFedFailures"/> computes from the same roster the
/// cells the generator owes, so a value left out here is red and never a smaller pool.</summary>
public partial class NameResolutionDifferentialTests
{
    private static IEnumerable<string> AxisFedFailures(IReadOnlyList<NameCell> pool)
    {
        var axisCells = pool.Where(c => c.AxisCell != null).Select(c => c.AxisCell!).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        failures.AddRange(ComprehensionFedFailures(axisCells));
        failures.AddRange(CycleFedFailures(axisCells));
        failures.AddRange(OrderObservationFedFailures(axisCells));
        failures.AddRange(ReadTimingFedFailures(pool, axisCells));
        failures.AddRange(LetInFedFailures(axisCells));
        return failures;
    }

    // ------------------------------------------------------------------------------------------- //
    // Axis 1: comprehension kind × clause × slot
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A second <c>for</c>'s iterable: the same AST slot as the first's, and on the other side of python's scope rule.</summary>
    private const string SecondIterable = "IterableSecond";

    private static string ComprehensionAnnotation(string family) => family switch
    {
        "list-comprehension" => "list[int]",
        "set-comprehension" => "set[int]",
        "generator-expression" => "Iterator[int]",
        "dict-comprehension" or "dict-spread-comprehension" => "dict[int, int]",
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "not a comprehension family"),
    };

    /// <summary>The comprehension's text. Python refuses a dict unpacking in a comprehension (<see cref="NameResolutionRosters.DictSpreadRefusal"/>),
    /// so the twin of <c>{**{k: v} for …}</c> is the <c>{k: v for …}</c> it means.</summary>
    private static string ComprehensionText(string family, string element, string key, string value, string clauses, bool twin = false) => family switch
    {
        "list-comprehension" => $"[{element} {clauses}]",
        "set-comprehension" => $"{{{element} {clauses}}}",
        "generator-expression" => $"({element} {clauses})",
        "dict-comprehension" => $"{{{key}: {value} {clauses}}}",
        "dict-spread-comprehension" => twin ? $"{{{key}: {value} {clauses}}}" : $"{{**{{{key}: {value}}} {clauses}}}",
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "not a comprehension family"),
    };

    /// <summary>The slots of one kind × clause shape a name sits in, read from the slot roster: the node's own, the iterable, and the
    /// clause's own where the shape has that clause.</summary>
    private static IEnumerable<string> ComprehensionReadSlots(Type node, ComprehensionClauseShape shape)
    {
        foreach (var slot in NameResolutionRosters.ComprehensionSlots.Where(e => e.Key.StartsWith(node.Name + ".", StringComparison.Ordinal)).SelectMany(e => e.Value.Image))
            yield return slot.ToString();
        yield return ComprehensionSlot.Iterable.ToString();
        if (shape == ComprehensionClauseShape.SecondFor)
            yield return SecondIterable;
        if (shape == ComprehensionClauseShape.IfClause)
            yield return ComprehensionSlot.Condition.ToString();
    }

    private static string ComprehensionReadKey(string family, ComprehensionClauseShape shape, string slot, ComprehensionNesting nesting, Order order)
        => $"comprehension-read: {family} × {shape} × {slot} × {nesting} × {order}";

    private static string ComprehensionTargetKey(string family, ComprehensionClauseShape shape, BindingKind outer)
        => $"comprehension-target: {family} × {shape} × {outer}";

    private static string ComprehensionWalrusKey(string family, ComprehensionClauseShape shape, ComprehensionSlot slot, BindingKind outer)
        => $"comprehension-walrus: {family} × {shape} × {slot} × {outer}";

    /// <summary>The walrus slots of one kind × shape: its slots the roster generates at a function body. A condition exists on the if shape alone.</summary>
    private static IEnumerable<ComprehensionSlot> ComprehensionWalrusSlots(Type node, ComprehensionClauseShape shape)
    {
        var own = NameResolutionRosters.ComprehensionSlots.Where(e => e.Key.StartsWith(node.Name + ".", StringComparison.Ordinal)).SelectMany(e => e.Value.Image);
        var slots = shape == ComprehensionClauseShape.IfClause ? own.Append(ComprehensionSlot.Condition) : own;
        return slots.Where(s => NameResolutionRosters.ComprehensionWalrus[NameResolutionRosters.WalrusKey(s, ComprehensionNesting.FunctionBody)].Generated);
    }

    private static readonly BindingKind[] ComprehensionOuters = { BindingKind.None, BindingKind.ModuleVar };

    private static IEnumerable<NameCell> ComprehensionCells()
    {
        var shapes = Roster.ImageOf(NameResolutionRosters.ComprehensionClauses).ToArray();
        foreach (var (family, entry) in NameResolutionRosters.ComprehensionKinds)
        {
            var node = entry.Image[0];
            foreach (var shape in shapes)
            {
                foreach (var slot in ComprehensionReadSlots(node, shape))
                    foreach (var nesting in Enum.GetValues<ComprehensionNesting>())
                        foreach (var order in new[] { Order.Earlier, Order.Later })
                            yield return ComprehensionReadCell(family, shape, slot, nesting, order);

                foreach (var outer in ComprehensionOuters)
                {
                    yield return ComprehensionTargetCell(family, shape, outer);
                    foreach (var slot in ComprehensionWalrusSlots(node, shape))
                        yield return ComprehensionWalrusCell(family, shape, slot, outer);
                }
            }
        }
    }

    /// <summary>A read of module <c>x</c> (value 5) at one slot, in a program that prints 5 only if that slot read <c>x</c>.</summary>
    private static NameCell ComprehensionReadCell(string family, ComprehensionClauseShape shape, string slot, ComprehensionNesting nesting, Order order)
    {
        const string X = "x";
        var first = slot == nameof(ComprehensionSlot.Iterable);
        var second = slot == SecondIterable;
        var condition = slot == nameof(ComprehensionSlot.Condition);

        // A condition reads x by selecting x's value out of the iterable: any other binding selects nothing.
        var firstIterable = first ? $"[{X}]" : condition ? "[1, 5]" : "[1]";
        var clauses = shape switch
        {
            ComprehensionClauseShape.OneFor => $"for v0 in {firstIterable}",
            ComprehensionClauseShape.SecondFor => $"for v0 in {firstIterable} for v1 in {(second ? $"[{X}]" : "[2]")}",
            ComprehensionClauseShape.IfClause => $"for v0 in {firstIterable} if {(condition ? $"v0 == {X}" : "v0 > 0")}",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        var variable = second ? "v1" : "v0";

        var element = slot == nameof(ComprehensionSlot.Element) ? X : variable;
        var keySlot = slot == nameof(ComprehensionSlot.Key);
        var valueSlot = slot is nameof(ComprehensionSlot.Value) or nameof(ComprehensionSlot.Spread);
        var key = keySlot ? X : valueSlot ? "1" : variable;
        var value = valueSlot ? X : keySlot ? "1" : variable;

        string Read(string received) => family switch
        {
            "list-comprehension" => $"{received}[0]",
            _ when valueSlot => $"{received}[1]",
            _ => $"[w0 for w0 in {received}][0]",
        };

        string Program(bool twin)
        {
            var text = ComprehensionText(family, element, key, value, clauses, twin);
            var annotation = ComprehensionAnnotation(family);
            List<string> reader;
            string print;
            if (nesting == ComprehensionNesting.FunctionBody)
            {
                reader = new List<string> { "def r() -> int:", $"    c0: {annotation} = {text}", $"    return {Read("c0")}" };
                print = "r()";
            }
            else
            {
                reader = new List<string> { "class H:", "    @static", $"    c0: {annotation} = {text}" };
                print = Read("H.c0");
            }

            var decl = new[] { "x: int = 5" };
            var lines = order == Order.Earlier ? decl.Concat(reader) : reader.Concat(decl);
            return Join(lines) + MainBlock($"print({print})");
        }

        // A generator expression evaluates its first iterable where it is written and every other slot when it is iterated, here in main.
        var lazy = family == "generator-expression" && !first;
        var python = nesting == ComprehensionNesting.StaticInitializer && order == Order.Later && !lazy ? PyOutcome.Name(X) : PyOutcome.Ran(5);
        var expected = Run(python, 5, first ? "N1" : "N1-nesting");
        return new NameCell
        {
            Id = CellId("read", "comprehension", family, shape, slot, nesting, order),
            Family = Family.Read,
            Source = Program(twin: false),
            TwinSource = family == "dict-spread-comprehension" ? Program(twin: true) : null,
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = BindingKind.ModuleVar,
            Outer = BindingKind.None,
            Position = first ? ReadPosition.ComprehensionIterable : ReadPosition.ComprehensionElement,
            AxisCell = ComprehensionReadKey(family, shape, slot, nesting, order),
        };
    }

    /// <summary>The comprehension's own target read after its bracket: a fresh binding of the bracket, in both languages.</summary>
    private static NameCell ComprehensionTargetCell(string family, ComprehensionClauseShape shape, BindingKind outer)
    {
        var clauses = shape switch
        {
            ComprehensionClauseShape.OneFor => "for x in [1, 2]",
            ComprehensionClauseShape.SecondFor => "for v0 in [1] for x in [1, 2]",
            ComprehensionClauseShape.IfClause => "for x in [1, 2] if x > 0",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        var e = outer == BindingKind.None ? Refuse(PyOutcome.Name("x"), "N5") : Run(ValueOf(outer), "N1");
        NameCell Program(bool twin) => LocalProgram(
            CellId("local", "comprehension-target", family, shape, $"outer={outer}"),
            BindingKind.Inner, outer, ReadPosition.FunctionBody,
            new[] { $"c0: {ComprehensionAnnotation(family)} = {ComprehensionText(family, "x", "x", "x", clauses, twin)}", "print(x)" },
            e, null, ReadName.Plain, LocalTarget.Comprehension);
        return Program(twin: false) with
        {
            TwinSource = family == "dict-spread-comprehension" ? Program(twin: true).Source : null,
            AxisCell = ComprehensionTargetKey(family, shape, outer),
        };
    }

    /// <summary>A walrus at one slot. Sharpy binds it in the comprehension's scope and python in the function's (the ledger's
    /// <c>walrus-in-comprehension-scope</c>), so the later read names the outer, or nothing.</summary>
    private static NameCell ComprehensionWalrusCell(string family, ComprehensionClauseShape shape, ComprehensionSlot slot, BindingKind outer)
    {
        const string Walrus = "(x := v0 * 2)";
        var clauses = shape switch
        {
            ComprehensionClauseShape.OneFor => "for v0 in [3]",
            ComprehensionClauseShape.SecondFor => "for v0 in [3] for v1 in [2]",
            ComprehensionClauseShape.IfClause => $"for v0 in [3] if {(slot == ComprehensionSlot.Condition ? Walrus + " > 0" : "v0 > 0")}",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        var element = slot == ComprehensionSlot.Element ? Walrus : "v0";
        var key = slot == ComprehensionSlot.Key ? Walrus : slot == ComprehensionSlot.Value ? "1" : "v0";
        var value = slot == ComprehensionSlot.Value ? Walrus : slot == ComprehensionSlot.Key ? "1" : "v0";

        string Program(bool twin)
        {
            var stmts = new List<string> { $"c0: {ComprehensionAnnotation(family)} = {ComprehensionText(family, element, key, value, clauses, twin)}" };
            if (family == "generator-expression")
                stmts.Add("c1: list[int] = [w0 for w0 in c0]");
            stmts.Add("print(x)");
            var (host, call) = Host(ReadPosition.FunctionBody, stmts);
            var lines = outer == BindingKind.ModuleVar ? new[] { "x: int = 5" }.Concat(host) : host;
            return Join(lines) + MainBlock(call.ToArray());
        }

        var moduleOuter = outer == BindingKind.ModuleVar;
        return new NameCell
        {
            Id = CellId("store", "walrus-in-comprehension", family, shape, slot, $"outer={outer}"),
            Family = Family.Store,
            Source = Program(twin: false),
            TwinSource = family == "dict-spread-comprehension" ? Program(twin: true) : null,
            Tag = moduleOuter ? SharpyTag.MustRun : SharpyTag.MustRefuse,
            Model = moduleOuter ? "5" : null,
            Python = PyOutcome.Ran(6),
            Rule = "N5",
            Deviation = "walrus-in-comprehension-scope",
            Target = outer,
            Outer = outer,
            Position = ReadPosition.ComprehensionElement,
            Form = StoreForm.Walrus,
            AxisCell = ComprehensionWalrusKey(family, shape, slot, outer),
        };
    }

    private static IEnumerable<string> ComprehensionFedFailures(IReadOnlySet<string> axisCells)
    {
        foreach (var (family, entry) in NameResolutionRosters.ComprehensionKinds)
        {
            foreach (var shape in Roster.ImageOf(NameResolutionRosters.ComprehensionClauses))
            {
                foreach (var slot in ComprehensionReadSlots(entry.Image[0], shape))
                    foreach (var nesting in Enum.GetValues<ComprehensionNesting>())
                        foreach (var order in new[] { Order.Earlier, Order.Later })
                            if (!axisCells.Contains(ComprehensionReadKey(family, shape, slot, nesting, order)))
                                yield return $"ComprehensionKinds {family} × {shape} × {slot} × {nesting} × {order} has no READ cell";
                foreach (var outer in ComprehensionOuters)
                {
                    if (!axisCells.Contains(ComprehensionTargetKey(family, shape, outer)))
                        yield return $"ComprehensionKinds {family} × {shape} × outer {outer} has no target cell";
                    foreach (var slot in ComprehensionWalrusSlots(entry.Image[0], shape))
                        if (!axisCells.Contains(ComprehensionWalrusKey(family, shape, slot, outer)))
                            yield return $"ComprehensionWalrus {family} × {shape} × {slot} × outer {outer} has no walrus cell";
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // Axis 2: N3 cycle × route
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<BindingKind> CycleEndpointsIn(ImmutableArray<BindingKind> group)
        => group.Where(k => NameResolutionRosters.CycleEndpoints[k].Generated);

    /// <summary>The (first declared, second declared) endpoints of one route. Self: one endpoint, a module value when it is read bare and a type's otherwise.</summary>
    private static IEnumerable<(BindingKind A, BindingKind B)> CycleEndpointPairs(Spelling spelling, CyclePair pair)
    {
        var module = CycleEndpointsIn(NameResolutionRosters.ModuleLevelKinds);
        var type = CycleEndpointsIn(NameResolutionRosters.TypeLevelKinds);
        return pair switch
        {
            CyclePair.ModuleModule => module.SelectMany(a => module.Select(b => (a, b))),
            CyclePair.ModuleType => module.SelectMany(a => type.Select(b => (a, b))),
            CyclePair.TypeModule => type.SelectMany(a => module.Select(b => (a, b))),
            CyclePair.TypeOtherType => type.SelectMany(a => type.Select(b => (a, b))),
            CyclePair.SelfThroughDef => (spelling == Spelling.Bare ? module : type).Select(a => (a, a)),
            _ => throw new ArgumentOutOfRangeException(nameof(pair)),
        };
    }

    /// <summary>A direct self read is READ's <c>Order.Self</c>, and <c>self</c> at an initializer does not exist: a method is the only way to it, so both are through a def.</summary>
    private static CycleThrough[] CycleThroughFor(Spelling spelling, CyclePair pair)
        => spelling == Spelling.SelfDot || pair == CyclePair.SelfThroughDef
            ? new[] { CycleThrough.ThroughDef }
            : Enum.GetValues<CycleThrough>();

    private static string CycleKey(Spelling spelling, CyclePair pair, CycleThrough through, BindingKind a, BindingKind b)
        => $"cycle: {spelling} × {pair} × {through} × {a} × {b}";

    private static IEnumerable<NameCell> CycleCells()
    {
        foreach (var spelling in Enum.GetValues<Spelling>())
            foreach (var pair in Enum.GetValues<CyclePair>())
            {
                if (!NameResolutionRosters.CycleRoutes[NameResolutionRosters.CycleRouteKey(spelling, pair)].Generated)
                    continue;
                foreach (var through in CycleThroughFor(spelling, pair))
                    foreach (var (a, b) in CycleEndpointPairs(spelling, pair))
                        yield return CycleCell(spelling, pair, through, a, b);
            }
    }

    private static List<string> CycleEndpointDecl(BindingKind kind, string name, string init)
    {
        var n = new ReadName(name, "", "", null);
        return IsModuleLevel(kind) ? ModuleDecl(kind, n, init) : MemberDecl(kind, n, init);
    }

    private static ReadPosition CycleEndpointPosition(BindingKind kind) => kind switch
    {
        BindingKind.ModuleVar or BindingKind.ModuleComputedConst => ReadPosition.ModuleInitializer,
        BindingKind.ModuleProperty => ReadPosition.ModulePropertyInitializer,
        BindingKind.StaticField => ReadPosition.StaticInitializer,
        BindingKind.StaticProperty => ReadPosition.StaticPropertyDefault,
        BindingKind.ClassComputedConst => ReadPosition.ConstInitializer,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a cycle endpoint"),
    };

    /// <summary>The cycle's program: <c>a</c>, declared first, reads <c>b</c>, and <c>b</c> reads <c>a</c>. Through a def, <c>a</c>'s read goes through a
    /// helper above both. Python stops at the first name its top-to-bottom load cannot read, and N3 says Sharpy never prints a default for it.</summary>
    private static NameCell CycleCell(Spelling spelling, CyclePair pair, CycleThrough through, BindingKind a, BindingKind b)
    {
        // The read of a type's member under this route, and the python name it stops on.
        (string Read, string PythonName, string[] Method) TypeRead(string type, string alias, string member) => spelling switch
        {
            Spelling.Qualified => ($"{type}.{member}", type, Array.Empty<string>()),
            Spelling.Alias => ($"{alias}.{member}", alias, Array.Empty<string>()),
            Spelling.SelfDot => ($"{type}().get0()", type, new[] { "def get0(self) -> int:", $"    return self.{member}" }),
            _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, "no type read"),
        };

        // SelfDot's read is already through a method: the helper would be a second hop of the same thing.
        var helperNeeded = through == CycleThrough.ThroughDef && spelling != Spelling.SelfDot;
        var lines = new List<string>();
        void Helper(string read)
        {
            if (helperNeeded)
                lines.AddRange(new[] { "def get() -> int:", $"    return {read}" });
        }
        string AInit(string read) => helperNeeded ? "get()" : read;

        string print;
        string python;
        switch (pair)
        {
            case CyclePair.ModuleModule:
                Helper("b");
                lines.AddRange(CycleEndpointDecl(a, "a", AInit("b")));
                lines.AddRange(CycleEndpointDecl(b, "b", "a"));
                print = "a";
                python = "b";
                break;
            case CyclePair.ModuleType:
                {
                    var (read, name, method) = TypeRead("H", "A", "b");
                    Helper(read);
                    lines.AddRange(CycleEndpointDecl(a, "a", AInit(read)));
                    lines.Add("class H:");
                    lines.AddRange(Indent(CycleEndpointDecl(b, "b", "a").Concat(method)));
                    if (spelling == Spelling.Alias)
                        lines.Add("type A = H");
                    print = "a";
                    python = name;
                    break;
                }
            case CyclePair.TypeModule:
                {
                    var (read, _, method) = TypeRead("H", "A", "a");
                    Helper("b");
                    lines.Add("class H:");
                    lines.AddRange(Indent(CycleEndpointDecl(a, "a", AInit("b")).Concat(method)));
                    lines.AddRange(CycleEndpointDecl(b, "b", read));
                    if (spelling == Spelling.Alias)
                        lines.Add("type A = H");
                    print = "H.a";
                    python = "b";
                    break;
                }
            case CyclePair.TypeOtherType:
                {
                    var (readB, name, methodB) = TypeRead("J", "B", "b");
                    var readA = spelling == Spelling.Alias ? "A.a" : "H.a";
                    Helper(readB);
                    lines.Add("class H:");
                    lines.AddRange(Indent(CycleEndpointDecl(a, "a", AInit(readB))));
                    lines.Add("class J:");
                    lines.AddRange(Indent(CycleEndpointDecl(b, "b", readA).Concat(methodB)));
                    if (spelling == Spelling.Alias)
                        lines.AddRange(new[] { "type A = H", "type B = J" });
                    print = "H.a";
                    python = name;
                    break;
                }
            case CyclePair.SelfThroughDef when spelling == Spelling.Bare:
                Helper("a");
                lines.AddRange(CycleEndpointDecl(a, "a", "get()"));
                print = "a";
                python = "a";
                break;
            case CyclePair.SelfThroughDef:
                {
                    var (read, name, method) = TypeRead("H", "A", "a");
                    Helper(read);
                    lines.Add("class H:");
                    lines.AddRange(Indent(CycleEndpointDecl(a, "a", helperNeeded ? "get()" : read).Concat(method)));
                    if (spelling == Spelling.Alias)
                        lines.Add("type A = H");
                    print = "H.a";
                    python = name;
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(pair));
        }

        var expected = Cycle(PyOutcome.Name(python), "N3-cycle");
        return new NameCell
        {
            Id = CellId("read", "cycle", spelling, pair, through, a, b),
            Family = Family.Read,
            Source = Join(lines) + MainBlock($"print({print})"),
            Tag = expected.Tag,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = a,
            Outer = BindingKind.None,
            Position = CycleEndpointPosition(a),
            AxisCell = CycleKey(spelling, pair, through, a, b),
        };
    }

    private static IEnumerable<string> CycleFedFailures(IReadOnlySet<string> axisCells)
    {
        foreach (var (spelling, pair) in Enum.GetValues<Spelling>().SelectMany(s => Enum.GetValues<CyclePair>().Select(p => (s, p))))
        {
            if (!NameResolutionRosters.CycleRoutes.TryGetValue(NameResolutionRosters.CycleRouteKey(spelling, pair), out var member) || !member.Generated)
                continue;
            foreach (var through in CycleThroughFor(spelling, pair))
                foreach (var (a, b) in CycleEndpointPairs(spelling, pair))
                    if (!axisCells.Contains(CycleKey(spelling, pair, through, a, b)))
                        yield return $"CycleRoutes {spelling} × {pair} × {through} × {a} × {b} has no cell";
        }

        foreach (var kind in NameResolutionRosters.CycleEndpoints.Where(e => e.Value.Generated).Select(e => e.Key))
            if (!axisCells.Any(c => c.StartsWith("cycle: ", StringComparison.Ordinal) && c.Contains($" × {kind}", StringComparison.Ordinal)))
                yield return $"CycleEndpoints {kind} is generated by the roster and has no cycle cell";
    }

    // ------------------------------------------------------------------------------------------- //
    // Axis 3: the order observation
    // ------------------------------------------------------------------------------------------- //

    private static string OrderObservationKey(BindingKind kind, Order order) => $"order-observation: {kind} × {order}";

    private static IEnumerable<NameCell> OrderObservationCells()
    {
        foreach (var kind in NameResolutionRosters.OrderObservationKinds.Where(e => e.Value.Generated).Select(e => e.Key))
            foreach (var order in new[] { Order.Earlier, Order.Later })
                yield return OrderObservationCell(kind, order);
    }

    /// <summary>What a program observes beside a value: which print comes first. Module and <c>@static</c> initializers run top to bottom before
    /// <c>main</c> (N3); an instance's run when it is constructed, in both languages, which is the arm's green control. Python's twin of an
    /// instance member is the <c>__init__</c> that means it: its class body would run the initializer at load.</summary>
    private static NameCell OrderObservationCell(BindingKind kind, Order order)
    {
        var plain = ReadName.Plain;
        var value = ValueOf(kind);
        var instance = IsInstance(kind);
        const string Init = "init0()";
        var helper = new[] { "def init0() -> int:", $"    print(\"init {kind}\")", $"    return {value}" };
        var read = IsModuleLevel(kind) ? "x" : TypeLevelRead(kind, plain);
        var main = MainBlock("print(\"main\")", $"print({read})");

        string Program(IEnumerable<string> decl) => Join(helper) + (order == Order.Earlier ? Join(decl) + main : main + Join(decl));

        var decl = IsModuleLevel(kind)
            ? ModuleDecl(kind, plain, Init)
            : new[] { "class H:" }.Concat(Indent(MemberDecl(kind, plain, Init))).ToList();
        var twinDecl = new[] { "class H:", "    x: int = 0", "    def __init__(self) -> None:", $"        self.x = {Init}" };
        var model = instance ? JoinOut("main", $"init {kind}", value) : JoinOut($"init {kind}", "main", value);
        return new NameCell
        {
            Id = CellId("read", "order-observation", kind, order),
            Family = Family.Read,
            Source = Program(decl),
            TwinSource = instance ? Program(twinDecl) : null,
            Tag = SharpyTag.MustRun,
            Model = model,
            Python = PyOutcome.Ran(model),
            Rule = instance ? "N1" : "N3",
            Target = kind,
            Outer = BindingKind.None,
            Position = ReadPosition.FunctionBody,
            AxisCell = OrderObservationKey(kind, order),
        };
    }

    private static IEnumerable<string> OrderObservationFedFailures(IReadOnlySet<string> axisCells)
    {
        foreach (var kind in NameResolutionRosters.OrderObservationKinds.Where(e => e.Value.Generated).Select(e => e.Key))
            foreach (var order in new[] { Order.Earlier, Order.Later })
                if (!axisCells.Contains(OrderObservationKey(kind, order)))
                    yield return $"OrderObservationKinds {kind} × {order} has no cell";
    }

    // ------------------------------------------------------------------------------------------- //
    // Axis 4: LOCAL read timing
    // ------------------------------------------------------------------------------------------- //

    private static readonly BindingKind[] ClosureBeforeOuters = { BindingKind.None, BindingKind.ModuleVar, BindingKind.EnclosingLocal };

    private static readonly BindingKind[] FailedGuardOuters = { BindingKind.None, BindingKind.ModuleVar };

    private static readonly BindingKind[] BackEdgeBinders = { BindingKind.LocalLet, BindingKind.LocalVar };

    private static string ClosureBeforeKey(LocalTarget target, BindingKind outer) => $"timing-closure-before: {target} × {outer}";

    private static string BackEdgeKey(string loop, BindingKind binder) => $"timing-back-edge: {loop} × {binder}";

    private static string FailedGuardKey(LocalTarget capture, BindingKind outer) => $"timing-failed-guard: {capture} × {outer}";

    private static IEnumerable<NameCell> ReadTimingCells()
    {
        foreach (var target in Enum.GetValues<LocalTarget>())
            foreach (var outer in ClosureBeforeOuters)
                yield return ClosureBeforeCell(target, outer);
        foreach (var loop in NameResolutionRosters.ReadTimingLoops)
            foreach (var binder in BackEdgeBinders)
                yield return BackEdgeCell(loop, binder);
        foreach (var capture in NameResolutionRosters.ReadTimingCaptures)
            foreach (var outer in FailedGuardOuters)
                yield return FailedGuardCell(capture, outer);
    }

    /// <summary>A closure taken before the construct reads the target's name after it. Python's closure sees the leak (p11), and Sharpy's fresh target
    /// cannot print that, so it is S2's refusal. No outer is N5's name check. A comprehension's target leaks nothing in either language, so its closure reads the outer.</summary>
    private static NameCell ClosureBeforeCell(LocalTarget target, BindingKind outer)
    {
        var (construct, last) = ConstructOf(target, "x");
        var stmts = new List<string> { "g: () -> int = lambda: x" };
        stmts.AddRange(construct);
        stmts.Add("print(g())");

        Expected e;
        if (target == LocalTarget.Comprehension)
        {
            // CPython 3.12's inlined comprehension (PEP 709) makes the enclosing function's `x` a cell of its own, so the closure finds no
            // value there (measured on 3.12.13). The program means the same thing, and python's NameError is the one D2 lets a must-run carry.
            var python = outer == BindingKind.EnclosingLocal ? PyOutcome.Name("x") : PyOutcome.Ran(outer == BindingKind.None ? 0 : ValueOf(outer));
            e = outer == BindingKind.None ? Refuse(PyOutcome.Name("x"), "N5") : Run(python, ValueOf(outer), "N1");
        }
        else
        {
            // `except … as x` deletes the name, so python's closure finds a free variable with no value.
            var python = target == LocalTarget.ExceptAs ? PyOutcome.Name("x") : PyOutcome.Ran(last!);
            e = Refuse(python, outer == BindingKind.None ? "N5" : "S2");
        }

        return LocalProgram(CellId("local", "timing", "closure-before", target, $"outer={outer}"), BindingKind.LocalVar, outer, ReadPosition.FunctionBody, stmts, e, null, ReadName.Plain, target)
            with
        { Timing = ReadTimings.ClosureBeforeConstruct, AxisCell = ClosureBeforeKey(target, outer) };
    }

    /// <summary>The read above a body binding, at the next iteration: python reads the last iteration's (`13 0`), and the read is R-KX's "declared later".</summary>
    private static NameCell BackEdgeCell(string loop, BindingKind binder)
    {
        var open = loop == "for-body"
            ? new[] { "x: int = 13", "for w0 in range(2):" }
            : new[] { "x: int = 13", "w0: int = 0", "while w0 < 2:" };
        var stmts = new List<string>(open) { "    print(x)", binder == BindingKind.LocalLet ? "    let x = w0" : "    x: int = w0" };
        if (loop == "while-body")
            stmts.Add("    w0 += 1");
        return LocalProgram(CellId("local", "timing", "back-edge", loop, binder), binder, BindingKind.None, ReadPosition.FunctionBody, stmts,
                Refuse(PyOutcome.Ran(JoinOut(13, 0)), "N5-declared-later"), null, ReadName.Plain)
            with
        { Outer = BindingKind.LocalVar, Timing = ReadTimings.BackEdge, AxisCell = BackEdgeKey(loop, binder) };
    }

    /// <summary>R-KZ: a capture is fresh per arm, failed guard included. Python has bound it by the time the guard fails (p10).</summary>
    private static NameCell FailedGuardCell(LocalTarget capture, BindingKind outer)
    {
        var pattern = capture == LocalTarget.MatchCapture ? "x" : "2 as x";
        var stmts = new[] { "match 2:", $"    case {pattern} if x > 9:", "        pass", "    case _:", "        pass", "print(x)" };
        return LocalProgram(CellId("local", "timing", "failed-guard", capture, $"outer={outer}"), BindingKind.LocalVar, outer, ReadPosition.FunctionBody, stmts,
                Refuse(PyOutcome.Ran(2), outer == BindingKind.None ? "N5" : "S2"), null, ReadName.Plain, capture)
            with
        { Timing = ReadTimings.FailedGuard, AxisCell = FailedGuardKey(capture, outer) };
    }

    private static IEnumerable<string> ReadTimingFedFailures(IReadOnlyList<NameCell> pool, IReadOnlySet<string> axisCells)
    {
        var timings = pool.Where(c => c.Timing != null).Select(c => c.Timing!.Value).ToHashSet();
        foreach (var timing in Enum.GetValues<ReadTimings>().Where(t => !timings.Contains(t)))
            yield return $"ReadTimings {timing} has no generated cell";

        foreach (var target in Enum.GetValues<LocalTarget>())
            foreach (var outer in ClosureBeforeOuters)
                if (!axisCells.Contains(ClosureBeforeKey(target, outer)))
                    yield return $"ReadTimings closure-before × {target} × {outer} has no cell";
        foreach (var loop in NameResolutionRosters.ReadTimingLoops)
            foreach (var binder in BackEdgeBinders)
                if (!axisCells.Contains(BackEdgeKey(loop, binder)))
                    yield return $"ReadTimings back-edge × {loop} × {binder} has no cell";
        foreach (var capture in NameResolutionRosters.ReadTimingCaptures)
            foreach (var outer in FailedGuardOuters)
                if (!axisCells.Contains(FailedGuardKey(capture, outer)))
                    yield return $"ReadTimings failed-guard × {capture} × {outer} has no cell";
    }

    // ------------------------------------------------------------------------------------------- //
    // Axis 5: the let-in binder × outer
    // ------------------------------------------------------------------------------------------- //

    private static string LetInKey(BindingKind binder, string family, LetInOuter outer) => $"let-in: {binder} × {family} × {outer}";

    private static IEnumerable<NameCell> LetInBinderCells()
    {
        foreach (var binder in LocalKinds)
            foreach (var b in BlockShapes)
                foreach (var outer in Enum.GetValues<LetInOuter>())
                {
                    // `let` × {no outer, module} is LetInConstructCells' body control, which carries this key at the function body.
                    if (binder == BindingKind.LocalLet && outer is LetInOuter.None or LetInOuter.Module)
                        continue;
                    if (NameResolutionRosters.LetInProduct[NameResolutionRosters.LetInKey(b.Family, outer)].Generated)
                        yield return LetInBinderCell(binder, b, outer);
                }
    }

    /// <summary>A binder in a block body is block-scoped (R-LB, R-LF). No outer is the name check after the block, an outer reads the outer, and python prints
    /// the inner value through the family's row. A callable binder is read through the call form, so its outer is callable too: <c>x()</c> over an int is a type verdict.</summary>
    private static NameCell LetInBinderCell(BindingKind binder, BlockShape b, LetInOuter outer)
    {
        var plain = ReadName.Plain;
        var callable = IsCallableKind(binder);
        var value = ValueOf(binder);
        var block = new List<string>(b.Open);
        block.AddRange(Indent(LocalDecl(binder, plain), 4 * b.Depth));
        block.AddRange(b.Close);
        block.Add($"print({ReadText(FormOf(binder), "", plain)})");

        // The enclosing and same-function outer of the name, in the binder's own form.
        var nearOuter = binder == BindingKind.LocalDef
            ? new[] { "def x() -> int:", "    return 13" }
            : binder == BindingKind.LambdaTypedLocal
                ? new[] { "x: () -> int = lambda: 13" }
                : new[] { "x: int = 13" };

        var id = CellId("local", "let-in", b.Family, binder, $"outer={outer}", ReadPosition.FunctionBody);
        NameCell Program(BindingKind programOuter, IReadOnlyList<string> stmts, Expected e) =>
            LocalProgram(id, binder, programOuter, ReadPosition.FunctionBody, stmts, e, null, plain) with { AxisCell = LetInKey(binder, b.Family, outer) };
        Expected Outer(int model, string rule) => new(SharpyTag.MustRun, Str(model), PyOutcome.Ran(value), rule, b.Row);

        switch (outer)
        {
            case LetInOuter.None:
                return Program(BindingKind.None, block, new Expected(SharpyTag.MustRefuse, null, PyOutcome.Ran(value), "N5", b.Row));
            case LetInOuter.Module:
                var moduleOuter = callable ? BindingKind.ModuleDef : BindingKind.ModuleVar;
                return Program(moduleOuter, block, Outer(ValueOf(moduleOuter), "N5-block-scope"));
            case LetInOuter.Enclosing:
                var enclosing = new List<string>(nearOuter) { "def g1() -> None:" };
                enclosing.AddRange(Indent(block));
                enclosing.Add("g1()");
                return Program(BindingKind.None, enclosing, Outer(13, "N5-block-scope")) with { Outer = BindingKind.EnclosingLocal };
            case LetInOuter.SameFunction:
                return Program(BindingKind.None, nearOuter.Concat(block).ToList(), Outer(13, "#1974")) with { Outer = BindingKind.LocalVar };
            default:
                throw new ArgumentOutOfRangeException(nameof(outer));
        }
    }

    private static IEnumerable<string> LetInFedFailures(IReadOnlySet<string> axisCells)
    {
        foreach (var binder in NameResolutionRosters.LocalKinds.Where(e => e.Value.Generated).Select(e => e.Key))
            foreach (var (key, member) in NameResolutionRosters.LetInProduct.Where(e => e.Value.Generated))
            {
                var parts = key.Split(" × ");
                if (!axisCells.Contains(LetInKey(binder, parts[0], Enum.Parse<LetInOuter>(parts[1]))))
                    yield return $"LetInProduct {binder} × {key} has no cell";
            }
    }
}

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
}

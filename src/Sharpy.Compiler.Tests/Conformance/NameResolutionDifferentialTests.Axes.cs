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
}

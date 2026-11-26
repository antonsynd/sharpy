using System.Text.Json;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>A cell's id with the values the generator built it from, each still carrying its type. The rule table's key
/// axes read these (<see cref="NameResolutionDifferentialTests.RuleTableAxes"/>): an order or a spelling is the enum
/// value the generator passed, never a token found in the id's text.</summary>
internal sealed record CellKey(string Text, IReadOnlyList<object> Parts)
{
    /// <summary>The first part of this type, or null when the key spells none.</summary>
    public T? Part<T>() where T : struct, Enum
    {
        foreach (var part in Parts)
        {
            if (part is T value)
                return value;
        }
        return null;
    }

    /// <summary>The sub-generator's own label: the part after the family where it is text, not an axis value.</summary>
    public string? Route => Parts.Count > 1 && Parts[1] is string s ? s : null;

    /// <summary>The text parts after the route: the generator's labels with no enumeration behind them. An
    /// <c>outer=</c> part repeats <see cref="NameCell.Outer"/> and is not one.</summary>
    public IEnumerable<string> Marks => Parts.Skip(2).OfType<string>().Where(s => !s.StartsWith("outer=", StringComparison.Ordinal));

    public override string ToString() => Text;
}

/// <summary>
/// The binding and scoping rule table (#2487; contract §11's case → expected map): <c>name-resolution-rules.txt</c>,
/// data beside the allowlist. A row is a tag, a rule id and a conjunction of axis constraints. Every generated cell
/// of every pool that reads the table matches exactly one row, and that row's tag and rule are the cell's: a verdict
/// is a function of the axes, so an arm retagged to a rule whose codes also fit is red here, which a per-rule
/// predicate could not see (#2457 cell 1: 2733 of the pool's cells fit a second rule's predicate). Every row has a cell.
/// </summary>
public partial class NameResolutionDifferentialTests
{
    private const string RuleTableFileName = "name-resolution-rules.txt";

    /// <summary>Edit-loop only: a path that receives every cell's axis values, one JSON line each after a header line of
    /// the axes' universes and classes. It is what a row is written against; no test reads it.</summary>
    internal const string RuleTableDumpEnv = "NAME_RES_RULE_TABLE_DUMP";

    /// <summary>The value of an axis a cell does not carry.</summary>
    internal const string Unset = "-";

    /// <summary>An axis of the table. <see cref="Universe"/> is the enumeration's names, or null for an axis closed by
    /// the pools: a value the table names there must be carried by a cell. <see cref="SetValued"/>: a cell carries any
    /// number of values, and a constraint asks whether it carries one of the listed.</summary>
    internal sealed record RuleAxis(
        string Name,
        Func<NameCell, IEnumerable<string>> Read,
        IReadOnlyCollection<string>? Universe,
        IReadOnlyDictionary<string, IReadOnlySet<string>> Classes,
        bool SetValued = false);

    internal sealed record RuleConstraint(RuleAxis Axis, bool Negated, IReadOnlySet<string> Values, string Text)
    {
        public bool Holds(NameCell cell)
        {
            bool any = false;
            foreach (var value in Axis.Read(cell))
            {
                if (Values.Contains(value))
                {
                    any = true;
                    break;
                }
            }
            return any != Negated;
        }
    }

    /// <summary>A row. <see cref="Tag"/> null is <c>*</c>: the cell's own tag, which only the seed row may say (a seed's tag
    /// is its line in the seed table).</summary>
    internal sealed record RuleRow(int Line, SharpyTag? Tag, string Rule, IReadOnlyList<RuleConstraint> Constraints, string Text)
    {
        public bool Matches(NameCell cell)
        {
            foreach (var constraint in Constraints)
            {
                if (!constraint.Holds(cell))
                    return false;
            }
            return true;
        }
    }

    private static IReadOnlySet<string> NamesWhere<T>(Func<T, bool> holds) where T : struct, Enum
        => Enum.GetValues<T>().Where(holds).Select(v => v.ToString()).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> One(object? value) => new[] { value?.ToString() ?? Unset };

    private static RuleAxis EnumAxis<T>(string name, Func<NameCell, T?> read, IReadOnlyDictionary<string, IReadOnlySet<string>>? classes = null) where T : struct, Enum
        => new(name, c => One(read(c)), Enum.GetNames<T>(), classes ?? new Dictionary<string, IReadOnlySet<string>>());

    private static RuleAxis KeyAxis<T>(string name) where T : struct, Enum
        => EnumAxis(name, c => c.Key?.Part<T>());

    /// <summary>The classes of a binding kind, each the image of the roster or predicate the generator itself branches on.</summary>
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BindingKindClasses() => new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["Module"] = NamesWhere<BindingKind>(NameResolutionRosters.ModuleLevelKinds.Contains),
        ["TypeLevel"] = NamesWhere<BindingKind>(NameResolutionRosters.TypeLevelKinds.Contains),
        ["Instance"] = NamesWhere<BindingKind>(NameResolutionRosters.InstanceKinds.Contains),
        ["PositionLocal"] = NamesWhere<BindingKind>(NameResolutionRosters.PositionLocalKinds.Contains),
        ["Named"] = NamesWhere<BindingKind>(IsNamedOuter),
        ["LocalOuter"] = NamesWhere<BindingKind>(IsLocalOuter),
        ["NonWritableByMeaning"] = NamesWhere<BindingKind>(IsNonWritableByMeaning),
        ["TypeLevelMember"] = NamesWhere<BindingKind>(IsTypeLevelMember),
        ["CallForm"] = NamesWhere<BindingKind>(k => FormOf(k) == Form.Call),
        ["CtorForm"] = NamesWhere<BindingKind>(k => FormOf(k) == Form.Ctor),
        ["ValueForm"] = NamesWhere<BindingKind>(k => FormOf(k) == Form.Value),
    };

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadPositionClasses() => new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["Initializer"] = NamesWhere<ReadPosition>(IsInitializer),
        ["ConstantOnly"] = NamesWhere<ReadPosition>(IsConstantOnly),
        ["StructClosureBody"] = NamesWhere<ReadPosition>(NameResolutionRosters.StructClosureBodies.Contains),
    };

    /// <summary>The table's axes. Each is a typed property of the cell or a typed part of its key, except the three the
    /// generator spells as text with no enumeration behind them: <c>route</c> (a sub-generator's label), <c>mark</c> (the
    /// other labels of a key) and <c>shape</c> / <c>deviation</c> (an unruled question's name, a <c>docs/deviations.yaml</c>
    /// row id). Those are closed by the pools.</summary>
    internal static IReadOnlyList<RuleAxis> RuleTableAxes => LazyRuleTableAxes.Value;

    /// <summary>Lazy: the classes read rosters other files of this class initialize.</summary>
    private static readonly Lazy<IReadOnlyList<RuleAxis>> LazyRuleTableAxes = new(() => new[]
    {
        EnumAxis<Family>("family", c => c.Family),
        EnumAxis<BindingKind>("target", c => c.Target, BindingKindClasses()),
        EnumAxis<BindingKind>("outer", c => c.Outer, BindingKindClasses()),
        EnumAxis<ReadPosition>("position", c => c.Position, ReadPositionClasses()),
        EnumAxis<DeclaringRelation>("relation", c => c.Relation),
        EnumAxis<StoreForm>("form", c => c.Form),
        EnumAxis<LocalTarget>("construct", c => c.Construct),
        EnumAxis<ReadTimings>("timing", c => c.Timing),
        EnumAxis<DeclarationShapes>("declaration", c => c.Declaration),
        EnumAxis<TypePosition>("typeAt", c => c.TypeAt),
        KeyAxis<Order>("order"),
        KeyAxis<Spelling>("spelling"),
        KeyAxis<Nesting>("nesting"),
        KeyAxis<Form>("readForm"),
        KeyAxis<AttributeReceiver>("receiver"),
        KeyAxis<ResultValueForm>("resultForm"),
        KeyAxis<StoreSpelling>("storeSpelling"),
        KeyAxis<ScopeInitializer>("initializer"),
        KeyAxis<ScopeAnnotation>("annotation"),
        KeyAxis<ScopeConstantKind>("constantKind"),
        KeyAxis<AdmissionTable>("admission"),
        KeyAxis<ScopeAliasTarget>("aliasTarget"),
        KeyAxis<ScopeAliasUse>("aliasUse"),
        KeyAxis<ScopeOverloadCaller>("overloadCaller"),
        KeyAxis<ScopeInitFit>("initFit"),
        new RuleAxis("route", c => One(c.Family == Family.Seed ? null : c.Key?.Route), null, new Dictionary<string, IReadOnlySet<string>>()),
        new RuleAxis("mark", c => c.Family == Family.Seed ? Array.Empty<string>() : c.Key?.Marks ?? Array.Empty<string>(), null, new Dictionary<string, IReadOnlySet<string>>(), SetValued: true),
        new RuleAxis("shape", c => One(c.Shape), null, new Dictionary<string, IReadOnlySet<string>>()),
        new RuleAxis("deviation", c => One(c.Deviation), null, new Dictionary<string, IReadOnlySet<string>>()),
    });

    /// <summary>The table's rows: <c>&lt;Tag|*&gt; &lt;rule&gt; axis=value,@Class axis!=value …</c>. Constraints on one row conjoin; the
    /// values of one constraint are alternatives; <c>-</c> is an axis the cell does not carry; <c>#</c> opens a comment line.</summary>
    internal static List<RuleRow> ParseRuleTable(IEnumerable<string> lines, List<string> problems)
    {
        var axes = RuleTableAxes.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var rows = new List<RuleRow>();
        int number = 0;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            SharpyTag? tag = null;
            if (fields.Length < 2 || (fields[0] != "*" && !Enum.TryParse<SharpyTag>(fields[0], out var parsed)))
            {
                problems.Add($"RuleTable L{number}: '{line}' must be '<Tag|*> <rule> axis=value …'");
                continue;
            }
            if (fields[0] != "*")
                tag = Enum.Parse<SharpyTag>(fields[0]);

            var constraints = new List<RuleConstraint>();
            bool bad = false;
            foreach (var field in fields.Skip(2))
            {
                bool negated = field.Contains("!=", StringComparison.Ordinal);
                var split = field.Split(negated ? "!=" : "=", 2);
                if (split.Length != 2 || !axes.TryGetValue(split[0], out var axis))
                {
                    problems.Add($"RuleTable L{number}: '{field}' names no axis");
                    bad = true;
                    continue;
                }
                var values = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in split[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (item.StartsWith('@'))
                    {
                        if (axis.Classes.TryGetValue(item[1..], out var members))
                            values.UnionWith(members);
                        else
                        {
                            problems.Add($"RuleTable L{number}: {axis.Name} has no class {item}");
                            bad = true;
                        }
                    }
                    else if (item != Unset && axis.Universe != null && !axis.Universe.Contains(item))
                    {
                        problems.Add($"RuleTable L{number}: {item} is no value of {axis.Name}");
                        bad = true;
                    }
                    else
                        values.Add(item);
                }
                if (values.Count == 0)
                {
                    problems.Add($"RuleTable L{number}: '{field}' lists no value");
                    bad = true;
                }
                constraints.Add(new RuleConstraint(axis, negated, values, field));
            }
            if (!bad)
                rows.Add(new RuleRow(number, tag, fields[1], constraints, line));
        }
        return rows;
    }

    private static readonly Lazy<(List<RuleRow> Rows, List<string> Problems)> LoadedRuleTable = new(() =>
    {
        var problems = new List<string>();
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", RuleTableFileName);
        if (path == null)
            return (new List<RuleRow>(), new List<string> { $"RuleTable: Conformance/{RuleTableFileName} is missing. It is the table every pool is judged by." });
        return (ParseRuleTable(File.ReadAllLines(path), problems), problems);
    });

    internal static IReadOnlyList<RuleRow> RuleTable() => LoadedRuleTable.Value.Rows;

    /// <summary>The pools that read the table. A row's cell may be in any of them; a cell's row is checked in its own sweep.</summary>
    internal static readonly IReadOnlyList<(string Name, Func<IReadOnlyList<NameCell>> Cells)> RuleTablePools = new (string, Func<IReadOnlyList<NameCell>>)[]
    {
        (nameof(NameResolutionDifferentialTests), GeneratedCells),
        (nameof(ScopeMeaningDifferentialTests), ScopeMeaningDifferentialTests.DraftCells),
    };

    /// <summary>The rows that claim a cell. One is the verdict; none or several is a totality failure.</summary>
    internal static List<RuleRow> RowsOf(IReadOnlyList<RuleRow> rows, NameCell cell)
    {
        var matched = new List<RuleRow>(1);
        foreach (var row in rows)
        {
            if (row.Matches(cell))
                matched.Add(row);
        }
        return matched;
    }

    /// <summary>The table's totality over one pool: every cell has a key, matches exactly one row, and carries that
    /// row's tag and rule. Capped per kind, with the count.</summary>
    internal static IEnumerable<string> RuleTableCellFailures(IReadOnlyList<RuleRow> rows, IReadOnlyList<NameCell> pool)
    {
        var none = new List<string>();
        var several = new List<string>();
        var differs = new List<string>();
        foreach (var cell in pool)
        {
            if (cell.Key == null)
            {
                yield return $"RuleTable: {cell.Id} carries no key, so no key axis reads it";
                continue;
            }
            var matched = RowsOf(rows, cell);
            if (matched.Count == 0)
                none.Add($"RuleTable: {cell.Id} ({cell.Tag}, {cell.Rule}) matches no row");
            else if (matched.Count > 1)
                several.Add($"RuleTable: {cell.Id} ({cell.Tag}, {cell.Rule}) matches rows L{string.Join(", L", matched.Select(r => r.Line))}");
            else if (matched[0].Rule != cell.Rule || (matched[0].Tag ?? cell.Tag) != cell.Tag)
                differs.Add($"RuleTable: {cell.Id} is generated {cell.Tag} under {cell.Rule}, and its row L{matched[0].Line} says {matched[0].Tag?.ToString() ?? "*"} under {matched[0].Rule}");
        }
        foreach (var group in new[] { none, several, differs })
        {
            foreach (var failure in group.Take(25))
                yield return failure;
            if (group.Count > 25)
                yield return $"RuleTable: … {group.Count} such cells in total";
        }
    }

    /// <summary>The table's totality over its rows, counted over every pool that reads it: a row has a cell, only the seed
    /// row leaves the tag to the cell, and a value named on an axis the pools close is carried by a cell.</summary>
    internal static IEnumerable<string> RuleTableRowFailures(IReadOnlyList<RuleRow> rows, IReadOnlyList<NameCell> cells)
    {
        foreach (var problem in LoadedRuleTable.Value.Problems)
            yield return problem;
        foreach (var row in rows)
        {
            if (row.Tag == null && row.Rule != "seed")
                yield return $"RuleTable L{row.Line}: only the seed row may leave the tag to its cell";
            if (!cells.Any(row.Matches))
                yield return $"RuleTable L{row.Line}: no cell of any pool matches '{row.Text}'";
        }
        foreach (var axis in RuleTableAxes.Where(a => a.Universe == null))
        {
            var carried = cells.SelectMany(axis.Read).ToHashSet(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                foreach (var constraint in row.Constraints.Where(c => c.Axis == axis))
                {
                    foreach (var value in constraint.Values.Where(v => v != Unset && !carried.Contains(v)))
                        yield return $"RuleTable L{row.Line}: no cell carries {axis.Name} {value}";
                }
            }
        }
    }

    /// <summary>Both directions for this pool, as the sweep and the fact read them.</summary>
    internal static IEnumerable<string> RuleTableFailures(IReadOnlyList<NameCell> pool)
    {
        var rows = RuleTable();
        return RuleTableRowFailures(rows, RuleTablePools.SelectMany(p => p.Cells()).ToList()).Concat(RuleTableCellFailures(rows, pool));
    }

    private static void DumpRuleTableAxes(string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            axes = RuleTableAxes.Select(a => new
            {
                name = a.Name,
                universe = a.Universe,
                setValued = a.SetValued,
                classes = a.Classes.ToDictionary(c => c.Key, c => c.Value.OrderBy(v => v, StringComparer.Ordinal).ToList()),
            }),
        }));
        foreach (var (pool, cells) in RuleTablePools)
        {
            foreach (var c in cells())
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    pool,
                    id = c.Id,
                    tag = c.Tag.ToString(),
                    rule = c.Rule,
                    model = c.Model,
                    python = c.Python.ToString(),
                    source = c.Source,
                    axes = RuleTableAxes.ToDictionary(a => a.Name, a => a.Read(c).ToList()),
                }));
            }
        }
    }
}

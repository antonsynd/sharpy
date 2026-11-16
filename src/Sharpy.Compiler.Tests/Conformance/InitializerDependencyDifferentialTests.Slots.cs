using System.Collections;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The slot family (plan-775c50, second stage-0 commit; the owner's ruling of 2026-11-16, #2422 comment 6923063788,
/// Q7): which reads of an initializer make a cycle a compile error. A slot is a place a child expression sits in a
/// concrete <see cref="Expression"/> subclass, found by reflection (<see cref="SlotKeys"/>). Each slot is classified
/// (<see cref="Evaluated"/>): its child is evaluated whenever its parent is, or it may not be.
///
/// Each entry is a program: <c>B.q</c> reads <c>A.x</c>, and <c>A.x</c>'s initializer holds <c>B.q</c> in the slot. In an
/// <see cref="Evaluated.Always"/> slot that is a cycle on every run, and the program is refused (SPY0278). In an
/// <see cref="Evaluated.Conditionally"/> slot the entry's text never evaluates the read, the program runs and prints
/// what python prints, and a compile-time cycle would refuse a working program. Every entry's text is parsed, and the
/// read must sit in the slot the entry names.
/// </summary>
public sealed partial class InitializerDependencyDifferentialTests
{
    internal enum Evaluated
    {
        Always,
        Conditionally,
    }

    /// <summary>One program per slot, or per case of a slot whose answer depends on which child or operator it is.
    /// <see cref="Value"/> is what the whole text is worth when the read is not evaluated.</summary>
    internal sealed record SlotEntry(Evaluated When, string Sharpy, int Value = 0, string? Twin = null, string? Case = null);

    private const string SlotCycle = "SPY0278";

    /// <summary>The read every entry carries: a <c>@static</c> value of the type declared beside the reader's.</summary>
    private const string SlotRead = "B.q";

    private const string SlotMarker = "B";

    private static SlotEntry Always(string sharpy, string? twin = null, string? @case = null) => new(Evaluated.Always, sharpy, 0, twin, @case);

    private static SlotEntry Conditionally(string sharpy, int value, string? twin = null, string? @case = null) => new(Evaluated.Conditionally, sharpy, value, twin, @case);

    private static SlotEntry[] ComprehensionIterator(string open, string close) => new[]
    {
        Always($"len({open}v0 for v0 in range({{d}}){close})", @case: "First"),
        Conditionally($"len({open}v1 for v0 in range(0) for v1 in range({{d}}){close})", 0, @case: "Later"),
    };

    internal static readonly IReadOnlyDictionary<string, SlotEntry[]> Slots = new Dictionary<string, SlotEntry[]>
    {
        ["FStringLiteral.Parts.Expression"] = new[] { Always("int(f\"{{d}}5\")") },
        ["FStringLiteral.Parts.Spec"] = new[] { Always("len(f\"{5:{{d}}}\")") },
        ["TStringLiteral.Parts.Expression"] = new[] { Always("len(t\"{{d}}\".interpolations)", twin: "len([{d}])") },
        ["ListLiteral.Elements"] = new[] { Always("[{d}][0]") },
        ["DictLiteral.Entries.Key"] = new[] { Always("len({{d}: 1})") },
        ["DictLiteral.Entries.Value"] = new[] { Always("{1: {d}}[1]") },
        ["SetLiteral.Elements"] = new[] { Always("max({{d}})") },
        ["TupleLiteral.Elements"] = new[] { Always("({d}, 1)[0]") },
        ["ListComprehension.Element"] = new[] { Conditionally("len([{d} for v0 in range(0)])", 0) },
        ["ListComprehension.Clauses:ForClause.Iterator"] = ComprehensionIterator("[", "]"),
        ["ListComprehension.Clauses:IfClause.Condition"] = new[] { Conditionally("len([v0 for v0 in range(0) if {d} > 0])", 0) },
        ["GeneratorExpression.Element"] = new[] { Conditionally("sum({d} for v0 in range(0))", 0) },
        ["GeneratorExpression.Clauses:ForClause.Iterator"] = new[]
        {
            Always("sum(v0 for v0 in range({d}))", @case: "First"),
            Conditionally("sum(v1 for v0 in range(0) for v1 in range({d}))", 0, @case: "Later"),
        },
        ["GeneratorExpression.Clauses:IfClause.Condition"] = new[] { Conditionally("sum(v0 for v0 in range(0) if {d} > 0)", 0) },
        ["SetComprehension.Element"] = new[] { Conditionally("len({{d} for v0 in range(0)})", 0) },
        ["SetComprehension.Clauses:ForClause.Iterator"] = ComprehensionIterator("{", "}"),
        ["SetComprehension.Clauses:IfClause.Condition"] = new[] { Conditionally("len({v0 for v0 in range(0) if {d} > 0})", 0) },
        ["DictComprehension.Key"] = new[] { Conditionally("len({{d}: v0 for v0 in range(0)})", 0) },
        ["DictComprehension.Value"] = new[] { Conditionally("len({v0: {d} for v0 in range(0)})", 0) },
        ["DictComprehension.Clauses:ForClause.Iterator"] = new[]
        {
            Always("len({v0: 1 for v0 in range({d})})", @case: "First"),
            Conditionally("len({v1: 1 for v0 in range(0) for v1 in range({d})})", 0, @case: "Later"),
        },
        ["DictComprehension.Clauses:IfClause.Condition"] = new[] { Conditionally("len({v0: 1 for v0 in range(0) if {d} > 0})", 0) },
        ["DictSpreadComprehension.Spread"] = new[] { Conditionally("len({**{v0: {d}} for v0 in range(0)})", 0, twin: "len({v0: {d} for v0 in range(0)})") },
        ["DictSpreadComprehension.Clauses:ForClause.Iterator"] = new[]
        {
            Always("len({**{v0: 1} for v0 in range({d})})", twin: "len({v0: 1 for v0 in range({d})})", @case: "First"),
            Conditionally("len({**{v1: 1} for v0 in range(0) for v1 in range({d})})", 0, twin: "len({v1: 1 for v0 in range(0) for v1 in range({d})})", @case: "Later"),
        },
        ["DictSpreadComprehension.Clauses:IfClause.Condition"] = new[]
        {
            Conditionally("len({**{v0: 1} for v0 in range(0) if {d} > 0})", 0, twin: "len({v0: 1 for v0 in range(0) if {d} > 0})"),
        },
        ["MemberAccess.Object"] = new[] { Always("Box({d}).v") },
        ["IndexAccess.Object"] = new[] { Always("[{d}, 0][0]") },
        ["IndexAccess.Index"] = new[] { Always("[7, 8, 9, 10][{d}]") },
        ["SliceAccess.Object"] = new[] { Always("[{d}, 0][0:][0]") },
        ["SliceAccess.Start"] = new[] { Always("len([7, 8, 9, 10][{d}:])") },
        ["SliceAccess.Stop"] = new[] { Always("len([7, 8, 9, 10][:{d}])") },
        ["SliceAccess.Step"] = new[] { Always("len([7, 8, 9, 10][::{d}])") },
        ["FunctionCall.Function"] = new[] { Always("mk({d})()") },
        ["FunctionCall.Arguments"] = new[] { Always("inc({d})") },
        ["FunctionCall.KeywordArguments.Value"] = new[] { Always("inc(v={d})") },
        ["UnaryOp.Operand"] = new[] { Always("-{d}") },
        ["BinaryOp.Left"] = new[] { Always("{d} + 1") },
        ["ComparisonChain.Operands"] = new[]
        {
            Always("int({d} < 5 < 9)", @case: "First"),
            Always("int(0 < {d} < 9)", @case: "Second"),
            Conditionally("int(1 < 0 < {d})", 0, @case: "Later"),
        },
        ["ConditionalExpression.Test"] = new[] { Always("5 if {d} > 0 else 6") },
        ["ConditionalExpression.ThenValue"] = new[] { Conditionally("{d} if 1 < 0 else 5", 5) },
        ["ConditionalExpression.ElseValue"] = new[] { Conditionally("5 if 1 > 0 else {d}", 5) },
        ["LambdaExpression.Body"] = new[] { Conditionally("ap0(lambda: {d})", 5) },
        ["ModifiedArgument.Argument"] = new[] { Always("bump(ref {d})", twin: "inc({d})") },
        ["TypeCoercion.Value"] = new[] { Always("({d} as! int)", twin: "({d})") },
        ["Parenthesized.Expression"] = new[] { Always("({d})") },
        ["WalrusExpression.Value"] = new[] { Always("(w0 := {d})") },
        ["TryExpression.Operand"] = new[] { Always("(try inc({d})).unwrap()", twin: "inc({d})") },
        ["MaybeExpression.Operand"] = new[] { Always("(maybe opt({d})).unwrap()", twin: "opt({d})") },
        ["SpreadElement.Value"] = new[] { Always("[*[{d}]][0]") },
        ["MatchExpression.Scrutinee"] = new[] { Always("match {d}:\n    case 3: 30\n    case _: 0", twin: "30 if {d} == 3 else 0") },
        ["MatchExpression.Arms.Pattern"] = new[]
        {
            Conditionally("match 1:\n    case 1: 5\n    case {d}: 6\n    case _: 0", 5, twin: "5 if 1 == 1 else (6 if 1 == {d} else 0)"),
        },
        ["MatchExpression.Arms.Guard"] = new[] { Conditionally("match 1:\n    case 2 if {d} > 0: 3\n    case _: 5", 5, twin: "3 if 1 == 2 and {d} > 0 else 5") },
        ["MatchExpression.Arms.Result"] = new[] { Conditionally("match 1:\n    case 2: {d}\n    case _: 5", 5, twin: "{d} if 1 == 2 else 5") },
    };

    internal static readonly IReadOnlyDictionary<string, Exclusion> ExcludedSlots = new Dictionary<string, Exclusion>
    {
        ["TStringLiteral.Parts.Spec"] = new("a template keeps its format spec as text for its consumer: the f-string's slot is the cell"),
        ["ListComprehension.Clauses:ForClause.Target"] = new("a binder: it reads nothing"),
        ["GeneratorExpression.Clauses:ForClause.Target"] = new("a binder: it reads nothing"),
        ["SetComprehension.Clauses:ForClause.Target"] = new("a binder: it reads nothing"),
        ["DictComprehension.Clauses:ForClause.Target"] = new("a binder: it reads nothing"),
        ["DictSpreadComprehension.Clauses:ForClause.Target"] = new("a binder: it reads nothing"),
        ["LambdaExpression.Parameters.DefaultValue"] = new(
            "a lambda parameter's default is a compile-time constant: a `@static` value is refused there",
            "class B:\n    @static\n    q: int = 3\ndef ap1(f: (int) -> int) -> int:\n    return 5\nt: int = ap1(lambda v: int = B.q: v)\n" + MainPrintingT,
            "SPY0401"),
    };

    /// <summary>The right operand of a <see cref="BinaryOp"/> is its operator's to classify: the slot's cases are the image of <see cref="BinaryOperator"/>.</summary>
    private const string BinaryRight = "BinaryOp.Right";

    internal static readonly IReadOnlyDictionary<BinaryOperator, SlotEntry> BinaryRights = new Dictionary<BinaryOperator, SlotEntry>
    {
        [BinaryOperator.Add] = Always("1 + {d}"),
        [BinaryOperator.Subtract] = Always("9 - {d}"),
        [BinaryOperator.Multiply] = Always("2 * {d}"),
        [BinaryOperator.Divide] = Always("int(6 / {d})"),
        [BinaryOperator.FloorDivide] = Always("7 // {d}"),
        [BinaryOperator.Modulo] = Always("7 % {d}"),
        [BinaryOperator.Power] = Always("2 ** {d}"),
        [BinaryOperator.Equal] = Always("int(3 == {d})"),
        [BinaryOperator.NotEqual] = Always("int(4 != {d})"),
        [BinaryOperator.LessThan] = Always("int(0 < {d})"),
        [BinaryOperator.LessThanOrEqual] = Always("int(0 <= {d})"),
        [BinaryOperator.GreaterThan] = Always("int(9 > {d})"),
        [BinaryOperator.GreaterThanOrEqual] = Always("int(9 >= {d})"),
        [BinaryOperator.And] = Conditionally("int(1 < 0 and {d} > 0)", 0),
        [BinaryOperator.Or] = Conditionally("int(1 > 0 or {d} > 0)", 1),
        [BinaryOperator.BitwiseAnd] = Always("7 & {d}"),
        [BinaryOperator.BitwiseOr] = Always("8 | {d}"),
        [BinaryOperator.BitwiseXor] = Always("8 ^ {d}"),
        [BinaryOperator.LeftShift] = Always("1 << {d}"),
        [BinaryOperator.RightShift] = Always("64 >> {d}"),
        [BinaryOperator.In] = Always("int(3 in [{d}])"),
        [BinaryOperator.NotIn] = Always("int(4 not in [{d}])"),
        [BinaryOperator.IsNot] = Always("int(3 is not {d})"),
        [BinaryOperator.NullCoalesce] = Conditionally("opt(5) ?? {d}", 5, twin: "opt(5) or {d}"),
    };

    private const string OperatorProbeHead = "class B:\n    @static\n    q: int = 3\n";

    internal static readonly IReadOnlyDictionary<BinaryOperator, Exclusion> ExcludedBinaryOperators = new Dictionary<BinaryOperator, Exclusion>
    {
        [BinaryOperator.Is] = new(
            "refused in every program: `x is y` compares references, and the parser reads it as a retired type test",
            OperatorProbeHead + "t: int = int(3 is B.q)\n" + MainPrintingT,
            "SPY0349"),
        [BinaryOperator.PipeForward] = new(
            "its right side is a call the left operand joins, and a type body's pipe into a module function is an internal error with no read in it: no cell isolates the slot",
            "def add2(a: int, b: int) -> int:\n    return a + b\nclass A:\n    @static\n    x: int = 3 |> add2(4)\ndef main() -> None:\n    print(A.x)\n",
            "SPY0908 CS0103"),
        [BinaryOperator.MatMul] = new(
            "behind an experimental feature: the harness deploys none",
            OperatorProbeHead + "t: int = B.q @ 2\n" + MainPrintingT,
            "SPY0331"),
    };

    // ------------------------------------------------------------------------------------------- //
    // The slots of a subclass, by reflection
    // ------------------------------------------------------------------------------------------- //

    private static readonly string AstNamespace = typeof(Expression).Namespace!;

    private static IEnumerable<PropertyInfo> AstProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.DeclaringType != typeof(Node))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>The element type of a property: through a nullable, an array or a list.</summary>
    private static Type ElementType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type != typeof(string) && type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            return ElementType(type.GetGenericArguments()[0]);
        return type;
    }

    private static bool IsAstClass(Type type) => type.IsClass && type.Namespace == AstNamespace && type != typeof(string);

    /// <summary>A type annotation is a type position. A pattern is one slot: what it names is its arm's to reach.</summary>
    private static bool IsSlot(Type type) => typeof(Expression).IsAssignableFrom(type) || typeof(Pattern).IsAssignableFrom(type);

    private static IEnumerable<string> SlotPaths(Type declared, string path, Type owner)
    {
        var type = ElementType(declared);
        if (typeof(TypeAnnotation).IsAssignableFrom(type))
            yield break;
        if (IsSlot(type) || type == owner)
        {
            // An expression, a pattern, or the holder again (a format spec's own parts): the slot ends here.
            yield return path;
            yield break;
        }
        if (!IsAstClass(type))
            yield break;
        var concrete = type.IsAbstract
            ? type.Assembly.GetTypes().Where(t => !t.IsAbstract && type.IsAssignableFrom(t)).OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => (Type: t, Path: $"{path}:{t.Name}"))
            : new[] { (Type: type, Path: path) };
        foreach (var (inner, innerPath) in concrete)
            foreach (var property in AstProperties(inner))
                foreach (var found in SlotPaths(property.PropertyType, $"{innerPath}.{property.Name}", inner))
                    yield return found;
    }

    /// <summary>Every slot of a concrete <see cref="Expression"/> subclass: <c>Subclass.Property</c>, through the holders between.</summary>
    internal static IEnumerable<string> SlotKeys(Type node) =>
        AstProperties(node).SelectMany(p => SlotPaths(p.PropertyType, $"{node.Name}.{p.Name}", node)).Distinct(StringComparer.Ordinal);

    /// <summary>The same walk over a parsed node: each slot with what sits in it.</summary>
    private static IEnumerable<(string Key, object Child)> SlotChildren(object? value, Type declared, string path, Type owner)
    {
        if (value == null)
            yield break;
        if (value is not string && value is IEnumerable items && !IsAstClass(value.GetType()))
        {
            foreach (var item in items)
                foreach (var found in SlotChildren(item, ElementType(declared), path, owner))
                    yield return found;
            yield break;
        }
        var type = value.GetType();
        if (typeof(TypeAnnotation).IsAssignableFrom(type))
            yield break;
        if (IsSlot(type) || type == owner)
        {
            yield return (path, value);
            yield break;
        }
        if (!IsAstClass(type))
            yield break;
        var innerPath = ElementType(declared).IsAbstract ? $"{path}:{type.Name}" : path;
        foreach (var property in AstProperties(type))
            foreach (var found in SlotChildren(property.GetValue(value), property.PropertyType, $"{innerPath}.{property.Name}", type))
                yield return found;
    }

    private static IEnumerable<object> Reachable(object? value)
    {
        if (value == null || value is string)
            yield break;
        if (value is IEnumerable items && !IsAstClass(value.GetType()))
        {
            foreach (var item in items)
                foreach (var inner in Reachable(item))
                    yield return inner;
            yield break;
        }
        if (!IsAstClass(value.GetType()))
            yield break;
        yield return value;
        foreach (var property in AstProperties(value.GetType()))
            foreach (var inner in Reachable(property.GetValue(value)))
                yield return inner;
    }

    /// <summary>The read is a name wherever it sits: an identifier in an expression, a path in a pattern.</summary>
    private static bool HoldsTheRead(object value) =>
        Reachable(value).Any(n => AstProperties(n.GetType()).Any(p =>
            p.PropertyType == typeof(string) ? (string?)p.GetValue(n) == SlotMarker
            : p.GetValue(n) is IEnumerable<string> names && names.Contains(SlotMarker)));

    private static Sharpy.Compiler.Parser.Ast.Module ParseInitializer(string text)
    {
        var tokens = new SharpyLexer($"t0: int = {text}\n").TokenizeAll();
        return new SharpyParser(tokens).ParseModule();
    }

    /// <summary>The slots of a text the read sits in, and the operators of the binary operations whose right operand holds it.</summary>
    private static (HashSet<string> Slots, HashSet<BinaryOperator> Rights) ReadSlots(string text)
    {
        var slots = new HashSet<string>(StringComparer.Ordinal);
        var rights = new HashSet<BinaryOperator>();
        foreach (var node in Reachable(ParseInitializer(text)).OfType<Expression>())
        {
            foreach (var property in AstProperties(node.GetType()))
                foreach (var (key, child) in SlotChildren(property.GetValue(node), property.PropertyType, $"{node.GetType().Name}.{property.Name}", node.GetType()))
                {
                    if (HoldsTheRead(child))
                        slots.Add(key);
                }
            if (node is BinaryOp binary && HoldsTheRead(binary.Right))
                rights.Add(binary.Operator);
        }
        return (slots, rights);
    }

    // ------------------------------------------------------------------------------------------- //
    // The cells
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<(string Id, string Key, SlotEntry Entry)> SlotProduct() =>
        Slots.SelectMany(s => s.Value.Select(e => ($"Slot/{s.Key}{(e.Case == null ? "" : "/" + e.Case)}", s.Key, e)))
            .Concat(BinaryRights.Select(r => ($"Slot/{BinaryRight}/{r.Key}", BinaryRight, r.Value)));

    /// <summary>The two types, in either order. Sharpy's reader sits second, so its read of <c>B.q</c> is typed by a declared type
    /// and the one forward read is the plain <c>A.x + 1</c>. Python's reader sits first: a class body runs where it is written, so
    /// only then is the read's evaluation the difference between running and a NameError.</summary>
    private static string SlotProgram(string shape, bool readerFirst)
    {
        var text = shape.Replace(Operand, SlotRead, StringComparison.Ordinal).Split('\n');
        var reader = new List<string> { "class A:", "    @static", $"    x: int = {text[0]}" };
        reader.AddRange(text.Skip(1).Select(l => "    " + l));
        var other = new[] { "class B:", "    @static", "    q: int = A.x + 1" };
        var body = readerFirst ? reader.Concat(other) : other.Concat(reader);
        return WithHelpers(Harness.Join(body) + Harness.MainBlock("print(A.x, B.q)"));
    }

    /// <summary>A cycle that should be a compile error prints a value made from a storage default today, or throws on one.</summary>
    private static Route SlotRoute(Evaluated when) => (verdict, _, _) =>
        when != Evaluated.Always ? null
        : verdict == VerdictClass.SilentWrong ? $"{ReadinessIssue} silent-wrong"
        : verdict == VerdictClass.RuntimeThrow ? $"{ReadinessIssue} runtime-throw"
        : null;

    internal static IEnumerable<Cell> SlotCells()
    {
        foreach (var (id, _, entry) in SlotProduct())
        {
            var always = entry.When == Evaluated.Always;
            yield return new Cell(id, Family.Slot, SlotProgram(entry.Sharpy, readerFirst: false), SlotProgram(entry.Twin ?? entry.Sharpy, readerFirst: true), $"{entry.Value} {entry.Value + 1}")
            {
                RefusalCode = always ? SlotCycle : null,
                // A read that is not evaluated makes no cycle: the program runs today and must keep running.
                IsControl = !always,
                Python = always ? PythonExpectation.NameError : PythonExpectation.Prints,
                Route = SlotRoute(entry.When),
            };
        }
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> SlotExclusions() =>
        ExcludedSlots.Select(e => ($"slot {e.Key}", e.Value))
            .Concat(ExcludedBinaryOperators.Select(e => ($"BinaryOperator.{e.Key}", e.Value)));

    internal static List<string> SlotTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        var nodes = typeof(Expression).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(Expression).IsAssignableFrom(t)).ToList();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            foreach (var key in SlotKeys(node))
            {
                found.Add(key);
                // A subclass the shape axis excludes has no program to hold a read: its slots ride that exclusion.
                var listed = (Slots.ContainsKey(key) ? 1 : 0) + (ExcludedSlots.ContainsKey(key) ? 1 : 0) + (key == BinaryRight ? 1 : 0) + (ExcludedShapes.ContainsKey(node) ? 1 : 0);
                if (listed != 1)
                    failures.Add($"slot {key} is {(listed == 0 ? "neither classified nor excluded" : "listed more than once")}");
            }
        }
        if (found.Count < 50)
            failures.Add($"the scan found {found.Count} slots: it reads nothing");
        foreach (var key in Slots.Keys.Concat(ExcludedSlots.Keys).Append(BinaryRight).Where(k => !found.Contains(k)))
            failures.Add($"{key} is not a slot of a concrete Expression subclass");

        foreach (var op in Enum.GetValues<BinaryOperator>())
        {
            bool generated = BinaryRights.ContainsKey(op), excluded = ExcludedBinaryOperators.ContainsKey(op);
            if (generated == excluded)
                failures.Add($"BinaryOperator.{op} is {Both(generated)}");
        }

        // Each entry's read sits in the slot it names, in both texts, and each slot with cases has one of each kind it claims.
        foreach (var (id, key, entry) in SlotProduct())
        {
            foreach (var text in new[] { entry.Sharpy, entry.Twin ?? entry.Sharpy })
            {
                if (!text.Contains(Operand, StringComparison.Ordinal))
                    failures.Add($"{id}: '{text}' holds no read");
            }
            var (slots, rights) = ReadSlots(entry.Sharpy.Replace(Operand, SlotRead, StringComparison.Ordinal));
            if (key == BinaryRight ? !rights.Contains(BinaryRights.First(r => ReferenceEquals(r.Value, entry)).Key) : !slots.Contains(key))
                failures.Add($"{id}: the read of '{entry.Sharpy}' sits in [{string.Join(", ", slots.OrderBy(s => s, StringComparer.Ordinal))}], not in {key}");
        }
        foreach (var (key, entries) in Slots)
        {
            if (entries.Length == 0)
                failures.Add($"slot {key} has no entry");
            if (entries.Length > 1 && entries.Any(e => e.Case == null))
                failures.Add($"slot {key} has several entries and one names no case");
        }
        foreach (var when in Enum.GetValues<Evaluated>().Where(w => SlotProduct().All(p => p.Entry.When != w)))
            failures.Add($"Evaluated.{when} has no entry");

        var ids = pool.Where(c => c.Family == Family.Slot).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = SlotProduct().Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)).Take(20))
            failures.Add($"the slot map owes {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)).Take(20))
            failures.Add($"the pool holds {id} and the slot map does not owe it");
        return failures;
    }
}

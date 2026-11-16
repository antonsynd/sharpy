using System.Collections;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Harness = Sharpy.Compiler.Tests.Conformance.NameResolutionDifferentialTests;
using ParameterModifier = Sharpy.Compiler.Parser.Ast.ParameterModifier;
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
///
/// The third stage-0 commit adds the kind of the values on the cycle (<see cref="NodeKinds"/>, the image of
/// <see cref="BindingKind"/>): the ring cells, whose one rule is <see cref="RingIsRefused"/>. A cycle is a compile error
/// when every read on it is evaluated whenever its initializer is, or when every value on it is a <c>const</c> (the set
/// refused today). A cycle that needs a read in a conditional slot or a lambda body to close, and passes through a value
/// that is no <c>const</c>, runs today and must keep running.
///
/// The fifth stage-0 commit closes two things the third verification found open. The slot of a
/// <see cref="ModifiedArgument"/> is its modifier's to classify (<see cref="ModifiedArguments"/>, the image of
/// <see cref="ParameterModifier"/>): a <c>ref</c> or an <c>in</c> operand is read, and an <c>out</c> operand is stored, which
/// is no edge to another value. And a value some plain store names closes no cycle of two or more
/// (<see cref="StoredCells"/>): the store may run before the value's initializer, which then replaces it, so the reads
/// around the cycle never meet a value that is being initialized.
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
    internal sealed record SlotEntry(Evaluated When, string Sharpy, int Value = 0, string? Twin = null, string? Case = null)
    {
        /// <summary>The slot's child is stored to, not read: evaluated whenever its parent is, and no edge to another value.</summary>
        public bool Stores { get; init; }
    }

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

    /// <summary>The operand of a <see cref="ModifiedArgument"/> is its modifier's to classify: the slot's cases are the image of
    /// <see cref="ParameterModifier"/>. A <c>ref</c> and an <c>in</c> operand are read. An <c>out</c> operand is stored: the
    /// callee writes it, nothing reads it, and a store is no edge of the cycle graph, whichever value it names (the
    /// owner's Q21: a store to a value from inside its own initializer is allowed).</summary>
    private const string ModifiedArgumentSlot = "ModifiedArgument.Argument";

    internal static readonly IReadOnlyDictionary<ParameterModifier, SlotEntry> ModifiedArguments = new Dictionary<ParameterModifier, SlotEntry>
    {
        [ParameterModifier.Ref] = Always("bump(ref {d})", twin: "inc({d})"),
        [ParameterModifier.In] = Always("peek(in {d})", twin: "peek0({d})"),
        [ParameterModifier.Out] = new(Evaluated.Always, "fill(out {d})", 1) { Stores = true },
    };

    internal static readonly IReadOnlyDictionary<ParameterModifier, Exclusion> ExcludedModifiedArguments = new Dictionary<ParameterModifier, Exclusion>
    {
        [ParameterModifier.None] = new("the parser builds no ModifiedArgument without a modifier: a plain argument sits in FunctionCall.Arguments, which has its entry"),
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
    private static (HashSet<string> Slots, HashSet<BinaryOperator> Rights, HashSet<ParameterModifier> Modifiers) ReadSlots(string text)
    {
        var slots = new HashSet<string>(StringComparer.Ordinal);
        var rights = new HashSet<BinaryOperator>();
        var modifiers = new HashSet<ParameterModifier>();
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
            if (node is ModifiedArgument modified && HoldsTheRead(modified.Argument))
                modifiers.Add(modified.Modifier);
        }
        return (slots, rights, modifiers);
    }

    // ------------------------------------------------------------------------------------------- //
    // The cells
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<(string Id, string Key, SlotEntry Entry)> SlotProduct() =>
        Slots.SelectMany(s => s.Value.Select(e => ($"Slot/{s.Key}{(e.Case == null ? "" : "/" + e.Case)}", s.Key, e)))
            .Concat(BinaryRights.Select(r => ($"Slot/{BinaryRight}/{r.Key}", BinaryRight, r.Value)))
            .Concat(ModifiedArguments.Select(m => ($"Slot/{ModifiedArgumentSlot}/{m.Key}", ModifiedArgumentSlot, m.Value)));

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

    private const string KeptSuffix = "/Kept";

    /// <summary>The control of a stored slot. The storing value <c>A.x</c> is first read by <c>B.p</c>, so its store to <c>B.q</c>
    /// runs inside B's sweep before <c>q</c>'s place: it is kept, <c>q</c>'s initializer replaces it, and nothing is on a cycle.
    /// A graph that takes the stored operand for a read sees <c>x</c> and <c>q</c> read each other and refuses a program
    /// that runs today.</summary>
    private static string KeptProgram(string shape)
    {
        var body = new[]
        {
            "class B:", "    @static", "    p: int = A.x", "    @static", "    q: int = A.x + 1",
            "class A:", "    @static", $"    x: int = {shape.Replace(Operand, SlotRead, StringComparison.Ordinal)}",
        };
        return WithHelpers(Harness.Join(body) + Harness.MainBlock("print(B.p, B.q, A.x)"));
    }

    /// <summary>A stored slot's two cells. In the family's own program <c>B.q</c>'s initializer reads <c>A.x</c>, whose
    /// initializer stores <c>B.q</c> while that initializer is still running: the store is allowed, and <c>q</c>'s
    /// initializer then replaces it with its result (the owner's Q21). Nothing reads a value that has none, so the program
    /// runs, and a graph that takes the stored operand for a read refuses it. Today it prints the stored value: C# starts
    /// with the class <c>main</c> names first, and <c>q</c>'s initializer reads a default. Python has no modifier: neither
    /// cell has a twin.</summary>
    private static IEnumerable<Cell> StoredSlotCells(string id, SlotEntry entry)
    {
        yield return new Cell(id, Family.Slot, SlotProgram(entry.Sharpy, readerFirst: false), null, $"{entry.Value} {entry.Value + 1}")
        {
            Python = PythonExpectation.NoTwin,
            Route = static (verdict, _, _) => ValueRoute(verdict),
        };
        yield return new Cell(id + KeptSuffix, Family.Slot, KeptProgram(entry.Sharpy), null, $"{entry.Value} {entry.Value + 1} {entry.Value}")
        {
            IsControl = true,
            Python = PythonExpectation.NoTwin,
        };
    }

    internal static IEnumerable<Cell> SlotCells()
    {
        foreach (var (id, _, entry) in SlotProduct())
        {
            if (entry.Stores)
            {
                foreach (var cell in StoredSlotCells(id, entry))
                    yield return cell;
                continue;
            }
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

    // ------------------------------------------------------------------------------------------- //
    // The stored node: a plain store that may run before a value's initializer
    // ------------------------------------------------------------------------------------------- //

    private const string StoredPrefix = "Slot/Stored/";

    /// <summary>The cycle rule's last clause (D8). <c>A.x</c> and <c>B.q</c> read each other in slots evaluated whenever
    /// their initializers are. An earlier value of B calls a function that uses <c>B.q</c>, and a value between reads
    /// <c>A.x</c>. A plain store is kept until <c>q</c>'s initializer replaces it (the owner's Q14), so <c>x</c> reads the
    /// stored value and no read meets a value that is being initialized: the program runs, as it does today, and a graph
    /// that refuses it turns a working program into an error. Any other use reads <c>q</c> first, which starts its
    /// initializer: the cycle is on every run, and it is a compile error. The same holds of a value that reads itself:
    /// after a store it reads what was stored (the owner's Q21).</summary>
    internal static bool StoredNodeRuns(LocationUse use) => IsPlainStore(use);

    /// <summary>The uses that are statements on an int <c>@static</c> value of a plain class: the location family's own.</summary>
    private static IEnumerable<LocationUse> StoredUses() =>
        Enum.GetValues<LocationUse>().Where(u =>
            UseExclusion(u, ValueKind.Int, BindingKind.StaticField) == null && !ExcludedLocationCells.ContainsKey((u, BindingKind.StaticField, HostForm.Plain)));

    private static string StoredProgram(LocationUse use)
    {
        var (statement, _, _) = UseText(use, ValueKind.Int, SlotRead, "1");
        var body = new List<string>
        {
            "class B:", "    @static", "    z: int = pre()", "    @static", "    w: int = A.x", "    @static", "    q: int = A.x + 1",
            "class A:", "    @static", $"    x: int = {SlotRead} + 1",
            "def pre() -> int:",
        };
        body.AddRange(statement.Split('\n').Select(l => "    " + l));
        body.Add("    return 0");
        return WithHelpers(Harness.Join(body) + Harness.MainBlock($"print(B.z, B.w, {SlotRead}, A.x)"));
    }

    /// <summary>One cell per use. Python's class bodies run where they are written, and B's names a class that does not
    /// exist yet: no twin.</summary>
    internal static IEnumerable<Cell> StoredCells()
    {
        foreach (var use in StoredUses())
        {
            var runs = StoredNodeRuns(use);
            var (_, stored, _) = UseText(use, ValueKind.Int, SlotRead, "1");
            yield return new Cell($"{StoredPrefix}{use}", Family.Slot, StoredProgram(use), null, $"0 {stored + 1} {stored + 2} {stored + 1}")
            {
                RefusalCode = runs ? null : SlotCycle,
                // What runs today and must not move.
                IsControl = runs,
                Python = PythonExpectation.NoTwin,
                Route = SlotRoute(runs ? Evaluated.Conditionally : Evaluated.Always),
            };
        }
    }

    private static List<string> StoredTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        var uses = StoredUses().ToList();
        // The use axis is the location family's: every use with an int cell on a plain class's `@static` value.
        foreach (var use in LocationProduct().Where(c => c.Holder == BindingKind.StaticField && c.Form == HostForm.Plain && c.Kind == ValueKind.Int).Select(c => c.Use).Distinct().Where(u => !uses.Contains(u)))
            failures.Add($"LocationUse.{use} has a location cell on a plain class and no stored-node cell");
        foreach (var answer in new[] { true, false }.Where(a => uses.All(u => StoredNodeRuns(u) != a)))
            failures.Add($"no stored-node cell {(answer ? "runs" : "is refused")}");
        if (SlotRead != "B.q" || !Slots.TryGetValue("BinaryOp.Left", out var left) || left.Any(e => e.When != Evaluated.Always))
            failures.Add("the stored-node program's two reads sit in BinaryOp.Left, which is no longer evaluated whenever its parent is");

        var ids = pool.Where(c => c.Id.StartsWith(StoredPrefix, StringComparison.Ordinal)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = uses.Select(u => $"{StoredPrefix}{u}").ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)))
            failures.Add($"the stored-node axis owes {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)))
            failures.Add($"the pool holds {id} and the stored-node axis does not owe it");
        return failures;
    }

    // ------------------------------------------------------------------------------------------- //
    // The ring cells: the kind of the values on a cycle
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A kind of value the cycle graph holds: how a value named after <paramref name="Declare"/>'s first argument
    /// is declared with an initializer, and how it is read.</summary>
    internal sealed record NodeKind(BindingKind Kind, bool IsConst, Func<string, string, string[]> Declare, Func<string, string> Read);

    private static NodeKind ModuleNode(BindingKind kind, string keyword, bool isConst = false) =>
        new(kind, isConst, (n, init) => new[] { $"{keyword}{n}0: int = {init}" }, n => $"{n}0");

    private static NodeKind TypeNode(BindingKind kind, string keyword, bool isStatic, bool isConst = false) =>
        new(
            kind,
            isConst,
            (n, init) => new[] { $"class {n.ToUpperInvariant()}0:" }.Concat(isStatic ? new[] { "    @static" } : Array.Empty<string>()).Append($"    {keyword}v: int = {init}").ToArray(),
            n => $"{n.ToUpperInvariant()}0.v");

    /// <summary>The kinds the plan's graph makes nodes (D8): every value with an initializer that is no literal.</summary>
    internal static readonly IReadOnlyList<NodeKind> NodeKinds = new[]
    {
        ModuleNode(BindingKind.ModuleVar, ""),
        ModuleNode(BindingKind.ModuleComputedConst, "const ", isConst: true),
        ModuleNode(BindingKind.ModuleProperty, "property "),
        TypeNode(BindingKind.StaticField, "", isStatic: true),
        TypeNode(BindingKind.StaticProperty, "property ", isStatic: true),
        TypeNode(BindingKind.ClassComputedConst, "const ", isStatic: false, isConst: true),
    };

    private const string LiteralOnNoCycle = "a literal const reads no value, so it is on no cycle: a const that reads its neighbour is the computed const's cell";

    private const string PerObject = "its initializer runs once per object, in a constructor: the graph of static initializers does not hold it";

    private const string NoStaticInitializer = "names a function or a type: it has no initializer the graph walks";

    private const string InitializedInItsBody = "position-local: it is initialized where its statement runs, and no rule of N3 names it";

    /// <summary>Lazy: it starts from the operand family's exclusions, which another file of this type declares. The kinds that
    /// family leaves out for what they name are left out here for the same fact, said of an initializer.</summary>
    private static readonly Lazy<IReadOnlyDictionary<BindingKind, Exclusion>> LazyExcludedNodeKinds = new(() => new Dictionary<BindingKind, Exclusion>(
        ExcludedOperandKinds.Where(e => NodeKinds.All(n => n.Kind != e.Key)).Select(e => KeyValuePair.Create(
            e.Key,
            e.Value.Reason == NotAnIntValue ? new Exclusion(NoStaticInitializer) : e.Value.Reason == PositionLocal ? new Exclusion(InitializedInItsBody) : e.Value)))
    {
        [BindingKind.ModuleLiteralConst] = new(LiteralOnNoCycle),
        [BindingKind.ClassLiteralConst] = new(LiteralOnNoCycle),
        [BindingKind.InstanceField] = new(PerObject),
        [BindingKind.InstanceProperty] = new(PerObject),
        [BindingKind.None] = new("no binding: a ring's values are named"),
    });

    internal static IReadOnlyDictionary<BindingKind, Exclusion> ExcludedNodeKinds => LazyExcludedNodeKinds.Value;

    /// <summary>The kinds the slot's read cannot name yet. The reader is declared first, as python needs, so its read is a forward one.</summary>
    internal static readonly IReadOnlyDictionary<BindingKind, Exclusion> ExcludedRingTargets = new Dictionary<BindingKind, Exclusion>
    {
        [BindingKind.ModuleVar] = new(
            "a module variable declared later is invisible until rule N1 lands (#2423), which is not this plan's: the kind is a ring's reader and its closing value, never the slot's read",
            "const x0: int = (y0)\ny0: int = x0 + 1\ndef main() -> None:\n    print(x0, y0)\n",
            "SPY0200"),
    };

    /// <summary>The slots the ring cells ride: one evaluated whenever its parent is, one conditional, and the body of a
    /// nested function. Each is an entry of <see cref="Slots"/>, so its class is the map's.</summary>
    internal static readonly IReadOnlyList<string> RingSlots = new[] { "Parenthesized.Expression", "ConditionalExpression.ElseValue", "LambdaExpression.Body" };

    /// <summary>The cycle rule (D8). A ring is refused when its slot's read is evaluated whenever the initializer is, since
    /// every other read on a ring is; or when every value on it is a const, the set refused today.</summary>
    internal static bool RingIsRefused(Evaluated slot, bool everyNodeIsConst) => slot == Evaluated.Always || everyNodeIsConst;

    /// <summary>The rings: every reader kind × every kind the slot's read may name, and each pair whose slot edge joins two
    /// consts again through every kind. Only there does a third value change the rule's answer: any other slot edge is
    /// certain or absent whatever follows it.</summary>
    private static IEnumerable<(NodeKind X, NodeKind Y, NodeKind? Through, string Slot)> RingProduct()
    {
        foreach (var x in NodeKinds)
            foreach (var y in NodeKinds.Where(k => !ExcludedRingTargets.ContainsKey(k.Kind)))
                foreach (var slot in RingSlots)
                {
                    yield return (x, y, null, slot);
                    if (!x.IsConst || !y.IsConst)
                        continue;
                    foreach (var through in NodeKinds)
                        yield return (x, y, through, slot);
                }
    }

    private static string RingId(NodeKind x, NodeKind y, NodeKind? through, string slot) =>
        $"Slot/Ring/{x.Kind}>{y.Kind}{(through == null ? "" : ">" + through.Kind)}/{slot}";

    /// <summary>The reader <c>x</c> holds its read of <c>y</c> in the slot; <c>y</c> reads <c>x</c> again, through <c>z</c> where
    /// there is one. Declared in the order python needs: the reader, the value between, the value read.</summary>
    private static string RingProgram(NodeKind x, NodeKind y, NodeKind? through, string shape)
    {
        var body = new List<string>(x.Declare("x", shape.Replace(Operand, y.Read("y"), StringComparison.Ordinal)));
        if (through != null)
            body.AddRange(through.Declare("z", $"{x.Read("x")} + 1"));
        body.AddRange(y.Declare("y", $"{(through ?? x).Read(through == null ? "x" : "z")} + 1"));
        var printed = through == null ? $"{x.Read("x")}, {y.Read("y")}" : $"{x.Read("x")}, {through.Read("z")}, {y.Read("y")}";
        return WithHelpers(Harness.Join(body) + Harness.MainBlock($"print({printed})"));
    }

    internal static IEnumerable<Cell> RingCells()
    {
        foreach (var (x, y, through, slot) in RingProduct())
        {
            var entry = Slots[slot][0];
            var everyNodeIsConst = x.IsConst && y.IsConst && (through?.IsConst ?? true);
            var refused = RingIsRefused(entry.When, everyNodeIsConst);
            var always = entry.When == Evaluated.Always;
            var model = through == null ? $"{entry.Value} {entry.Value + 1}" : $"{entry.Value} {entry.Value + 1} {entry.Value + 2}";
            yield return new Cell(RingId(x, y, through, slot), Family.Slot, RingProgram(x, y, through, entry.Sharpy), RingProgram(x, y, through, entry.Twin ?? entry.Sharpy), model)
            {
                RefusalCode = refused ? SlotCycle : null,
                // What must not move: a ring that runs today, and a ring of consts, which is refused today.
                IsControl = !always || everyNodeIsConst,
                Python = always ? PythonExpectation.NameError : PythonExpectation.Prints,
                Route = SlotRoute(always && !everyNodeIsConst ? Evaluated.Always : Evaluated.Conditionally),
            };
        }
    }

    private static List<string> RingTotalityFailures(IReadOnlyList<Cell> pool)
    {
        var failures = new List<string>();
        foreach (var kind in Enum.GetValues<BindingKind>())
        {
            bool generated = NodeKinds.Any(n => n.Kind == kind), excluded = ExcludedNodeKinds.ContainsKey(kind);
            if (generated == excluded)
                failures.Add($"node BindingKind.{kind} is {Both(generated)}");
        }
        foreach (var dup in NodeKinds.GroupBy(n => n.Kind).Where(g => g.Count() > 1))
            failures.Add($"node BindingKind.{dup.Key} is declared twice");
        foreach (var kind in ExcludedRingTargets.Keys.Where(k => NodeKinds.All(n => n.Kind != k)))
            failures.Add($"ring target BindingKind.{kind} is no node kind");
        if (NodeKinds.All(n => n.IsConst) || NodeKinds.All(n => !n.IsConst))
            failures.Add("the node kinds hold no const, or nothing else: no ring is mixed");

        foreach (var slot in RingSlots.Where(s => !Slots.TryGetValue(s, out var entries) || entries.Length != 1))
            failures.Add($"ring slot {slot} is no slot with one entry");
        var ridden = RingSlots.Where(Slots.ContainsKey).Select(s => Slots[s][0].When).ToHashSet();
        foreach (var when in Enum.GetValues<Evaluated>().Where(w => !ridden.Contains(w)))
            failures.Add($"Evaluated.{when} rides no ring");
        if (!RingSlots.Contains("LambdaExpression.Body"))
            failures.Add("no ring rides the body of a nested function");

        var rings = RingProduct().ToList();
        foreach (var kind in NodeKinds)
        {
            if (rings.All(r => r.X != kind))
                failures.Add($"node BindingKind.{kind.Kind} is no ring's reader");
            if (rings.All(r => r.Y != kind) && !ExcludedRingTargets.ContainsKey(kind.Kind))
                failures.Add($"node BindingKind.{kind.Kind} is no slot's read");
            if (rings.All(r => r.Through != kind))
                failures.Add($"node BindingKind.{kind.Kind} is on no ring of three");
        }
        // The rule's own falsifiers: a const pair closed through a value that is no const, and through one that is.
        foreach (var (allConst, what) in new[] { (false, "a value that is no const"), (true, "a third const") })
        {
            if (!rings.Any(r => r.Through != null && r.Through.IsConst == allConst && Slots[r.Slot][0].When == Evaluated.Conditionally))
                failures.Add($"no ring closes a conditional read between two consts through {what}");
        }

        var ids = pool.Where(c => c.Family == Family.Slot && c.Id.StartsWith("Slot/Ring/", StringComparison.Ordinal)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = rings.Select(r => RingId(r.X, r.Y, r.Through, r.Slot)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)).Take(20))
            failures.Add($"the ring axes owe {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)).Take(20))
            failures.Add($"the pool holds {id} and the ring axes do not owe it");
        return failures;
    }

    private static IEnumerable<(string What, Exclusion Exclusion)> SlotExclusions() =>
        ExcludedSlots.Select(e => ($"slot {e.Key}", e.Value))
            .Concat(ExcludedBinaryOperators.Select(e => ($"BinaryOperator.{e.Key}", e.Value)))
            .Concat(ExcludedModifiedArguments.Select(e => ($"slot case ParameterModifier.{e.Key}", e.Value)))
            .Concat(ExcludedNodeKinds.Select(e => ($"node BindingKind.{e.Key}", e.Value)))
            .Concat(ExcludedRingTargets.Select(e => ($"ring target BindingKind.{e.Key}", e.Value)));

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
                var listed = (Slots.ContainsKey(key) ? 1 : 0) + (ExcludedSlots.ContainsKey(key) ? 1 : 0) + (key == BinaryRight ? 1 : 0) + (key == ModifiedArgumentSlot ? 1 : 0)
                    + (ExcludedShapes.ContainsKey(node) ? 1 : 0);
                if (listed != 1)
                    failures.Add($"slot {key} is {(listed == 0 ? "neither classified nor excluded" : "listed more than once")}");
            }
        }
        if (found.Count < 50)
            failures.Add($"the scan found {found.Count} slots: it reads nothing");
        foreach (var key in Slots.Keys.Concat(ExcludedSlots.Keys).Append(BinaryRight).Append(ModifiedArgumentSlot).Where(k => !found.Contains(k)))
            failures.Add($"{key} is not a slot of a concrete Expression subclass");

        foreach (var op in Enum.GetValues<BinaryOperator>())
        {
            bool generated = BinaryRights.ContainsKey(op), excluded = ExcludedBinaryOperators.ContainsKey(op);
            if (generated == excluded)
                failures.Add($"BinaryOperator.{op} is {Both(generated)}");
        }

        // The cases of a modified argument's slot are the image of its modifier: a new modifier is red until it is classified.
        foreach (var modifier in Enum.GetValues<ParameterModifier>())
        {
            bool generated = ModifiedArguments.ContainsKey(modifier), excluded = ExcludedModifiedArguments.ContainsKey(modifier);
            if (generated == excluded)
                failures.Add($"slot case ParameterModifier.{modifier} of {ModifiedArgumentSlot} is {Both(generated)}");
        }
        // An operand is stored by the one modifier whose callee must write it, and by no other.
        foreach (var (modifier, entry) in ModifiedArguments.Where(m => m.Value.Stores != (m.Key == ParameterModifier.Out)))
            failures.Add($"slot case ParameterModifier.{modifier} is {(entry.Stores ? "classified as a store and is read" : "classified as a read and is stored")}");
        foreach (var (key, entries) in Slots)
        {
            foreach (var entry in entries.Where(e => e.Stores))
                failures.Add($"slot {key} is classified as a store: only a modified argument's operand is one ('{entry.Sharpy}')");
        }

        // Each entry's read sits in the slot it names, in both texts, and each slot with cases has one of each kind it claims.
        foreach (var (id, key, entry) in SlotProduct())
        {
            foreach (var text in new[] { entry.Sharpy, entry.Twin ?? entry.Sharpy })
            {
                if (!text.Contains(Operand, StringComparison.Ordinal))
                    failures.Add($"{id}: '{text}' holds no read");
            }
            var (slots, rights, modifiers) = ReadSlots(entry.Sharpy.Replace(Operand, SlotRead, StringComparison.Ordinal));
            if (key == BinaryRight ? !rights.Contains(BinaryRights.First(r => ReferenceEquals(r.Value, entry)).Key) : !slots.Contains(key))
                failures.Add($"{id}: the read of '{entry.Sharpy}' sits in [{string.Join(", ", slots.OrderBy(s => s, StringComparer.Ordinal))}], not in {key}");
            if (key == ModifiedArgumentSlot && !modifiers.SetEquals(new[] { ModifiedArguments.First(m => ReferenceEquals(m.Value, entry)).Key }))
                failures.Add($"{id}: the operand of '{entry.Sharpy}' sits under [{string.Join(", ", modifiers.OrderBy(m => m))}]");
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

        failures.AddRange(RingTotalityFailures(pool));
        failures.AddRange(StoredTotalityFailures(pool));

        var ids = pool.Where(c => c.Family == Family.Slot && !c.Id.StartsWith("Slot/Ring/", StringComparison.Ordinal) && !c.Id.StartsWith(StoredPrefix, StringComparison.Ordinal))
            .Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var owed = SlotProduct().SelectMany(p => p.Entry.Stores ? new[] { p.Id, p.Id + KeptSuffix } : new[] { p.Id }).ToHashSet(StringComparer.Ordinal);
        foreach (var id in owed.Where(id => !ids.Contains(id)).Take(20))
            failures.Add($"the slot map owes {id} and the pool does not hold it");
        foreach (var id in ids.Where(id => !owed.Contains(id)).Take(20))
            failures.Add($"the pool holds {id} and the slot map does not owe it");
        return failures;
    }
}

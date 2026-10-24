using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>The AST positions the typed generators fill (#2324, P63b Phase 1 Task 2).</summary>
internal enum Position
{
    /// <summary>An operand: binary / unary / logical operand, a truth-tested test, a guard, a conditional
    /// branch, an f-string hole, an index.</summary>
    Operand,

    /// <summary>A call argument, by callee kind.</summary>
    Argument,

    /// <summary>A whole call, for arity breaks, by callee kind.</summary>
    Call,

    /// <summary>A name read at a use.</summary>
    Name,

    /// <summary>A member name: field, method, f-string method, an operator a user type implements.</summary>
    MemberName,

    /// <summary>A type annotation: declaration, parameter, return, base, type argument, pattern head, handler.</summary>
    Annotation,

    /// <summary>A stored value: an annotated declaration's initializer, a reassignment, a module constant.</summary>
    Store,

    /// <summary>A <c>return</c> / <c>yield</c> / <c>yield from</c> value, or a def with an implicit <c>None</c>.</summary>
    ReturnValue,

    /// <summary>A match pattern or a match statement's arm order.</summary>
    Pattern,
}

/// <summary>
/// One place a one-fact break can land: the <paramref name="Target"/> node (replaced by reference), its
/// nearest node <paramref name="Parent"/>, the <paramref name="SlotType"/> the generator's own facts give
/// the slot (null when they cannot name it), a <paramref name="Family"/> lanes filter on, and a
/// human-readable <paramref name="Describe"/>.
/// </summary>
internal sealed record Site(Position Position, string Family, Node Target, Node Parent, string? SlotType, string Describe);

/// <summary>
/// Enumerates every <see cref="Site"/> of a NORMALIZED module (<see cref="AstRewriter.Normalize"/>): one
/// pass per position over <see cref="AstRewriter.Descendants"/> (annotations included), families named by the slot's role so
/// a lane reads "the arithmetic operands" or "the constructor arguments" without a hand walk. A new
/// construct the generators start emitting is a new site the next day; a site whose slot type the facts
/// cannot name is still listed (SlotType null) so the census can count the under-reach.
/// </summary>
internal static class MutationSites
{
    public static IReadOnlyList<Site> Enumerate(Module module) => Enumerate(new ProgramFacts(module));

    public static IReadOnlyList<Site> Enumerate(ProgramFacts facts)
    {
        var sites = new List<Site>();
        foreach (var node in AstRewriter.Descendants(facts.Module))
        {
            switch (node)
            {
                case BinaryOp b:
                    Operands(facts, b, sites);
                    break;
                case UnaryOp u:
                    Add(sites, facts, Position.Operand, u.Operator == UnaryOperator.Not ? "Not" : "Unary", u.Operand, u,
                        facts.TypeOf(u.Operand), $"operand of unary {u.Operator}");
                    if (u.Operator != UnaryOperator.Not && facts.TypeOf(u.Operand) is { } ut && facts.Types.ContainsKey(ut))
                        Add(sites, facts, Position.MemberName, "OperatorDunder", u, u, ut, $"unary {u.Operator} on {ut}");
                    break;
                case IfStatement i:
                    Add(sites, facts, Position.Operand, "TruthTest", i.Test, i, facts.TypeOf(i.Test), "if test");
                    foreach (var elif in i.ElifClauses)
                        Add(sites, facts, Position.Operand, "TruthTest", elif.Test, i, facts.TypeOf(elif.Test), "elif test");
                    break;
                case WhileStatement w:
                    Add(sites, facts, Position.Operand, "TruthTest", w.Test, w, facts.TypeOf(w.Test), "while test");
                    break;
                case ConditionalExpression c:
                    Add(sites, facts, Position.Operand, "TruthTest", c.Test, c, facts.TypeOf(c.Test), "conditional test");
                    if (TypedSlotOf(facts, c) is { } slot)
                    {
                        Add(sites, facts, Position.Operand, "ConditionalBranch", c.ThenValue, c, slot, "conditional then-branch in a typed slot");
                        Add(sites, facts, Position.Operand, "ConditionalBranch", c.ElseValue, c, slot, "conditional else-branch in a typed slot");
                    }
                    break;
                case MatchStatement m:
                    foreach (var arm in m.Cases)
                    {
                        if (arm.Guard is { } guard)
                            Add(sites, facts, Position.Operand, "Guard", guard, m, facts.TypeOf(guard), "case guard");
                    }
                    Patterns(facts, m, sites);
                    break;
                case GuardPattern gp:
                    Add(sites, facts, Position.Operand, "Guard", gp.Guard, gp, facts.TypeOf(gp.Guard), "guard pattern");
                    break;
                case FStringLiteral f:
                    foreach (var part in f.Parts)
                    {
                        if (part.Expression is not { } hole)
                            continue;
                        var spec = part.Spec is { } specParts ? string.Concat(specParts.Select(p => p.Text ?? "")) : null;
                        Add(sites, facts, Position.Operand, spec is null ? "FStringHole" : NumericSpec(spec) ? "FStringNumericSpec" : "FStringOtherSpec",
                            hole, f, facts.TypeOf(hole), spec is null ? "f-string hole" : $"f-string hole with spec '{spec}'");
                    }
                    break;
                case IndexAccess ia:
                    if (facts.TypeOf(ia.Object) is { } receiver && receiver.StartsWith("list[", StringComparison.Ordinal))
                        Add(sites, facts, Position.Operand, "Index", ia.Index, ia, "int", $"index into {receiver}");
                    break;
                case FunctionCall call:
                    Calls(facts, call, sites);
                    break;
                case Identifier id:
                    Names(facts, id, sites);
                    break;
                case MemberAccess ma:
                    Members(facts, ma, sites);
                    break;
                case TypeAnnotation ta:
                    Annotation(facts, ta, sites);
                    break;
                case VariableDeclaration v:
                    Store(facts, v, sites);
                    break;
                case Assignment a:
                    if (a.Operator == AssignmentOperator.Assign && a.Target is Identifier { Name: var target }
                        && facts.NameType(target, a) is { } declared && IsReassignment(facts, a, target))
                        Add(sites, facts, Position.Store, "Reassignment", a.Value, a, declared, $"reassignment of {target}: {declared}");
                    break;
                case ReturnStatement r:
                    Returns(facts, r, sites);
                    break;
                case YieldStatement y:
                    if (facts.EnclosingFunction(y) is { } gen)
                    {
                        Add(sites, facts, Position.ReturnValue, y.IsFrom ? "YieldFrom" : "Yield", y, gen,
                            gen.ReturnType is { } yt ? ProgramFacts.Spell(yt) : null, y.IsFrom ? "yield from" : "yield value");
                    }
                    break;
                case FunctionDef def:
                    if (def.ReturnType is null && !AstRewriter.Descendants(def).Any(n => n is YieldStatement)
                        && facts.Owner(def) is null && !def.IsAsync)
                        Add(sites, facts, Position.ReturnValue, "ImplicitNoneDef", def, facts.Module, "None", $"def {def.Name} with an implicit None return");
                    break;
            }
        }
        return sites;
    }

    /// <summary>The sites of one <paramref name="position"/> whose family is one of <paramref name="families"/>
    /// (all families when none are named).</summary>
    public static IReadOnlyList<Site> Of(ProgramFacts facts, Position position, params string[] families) =>
        Enumerate(facts)
            .Where(s => s.Position == position && (families.Length == 0 || families.Contains(s.Family)))
            .ToList();

    private static void Add(List<Site> sites, ProgramFacts facts, Position position, string family, Node target, Node parent,
        string? slotType, string describe) =>
        sites.Add(new Site(position, family, target, parent, slotType,
            $"{describe} @ {target.LineStart}:{target.ColumnStart}"));

    private static bool NumericSpec(string spec) => spec.Length > 0 && spec[^1] is 'd' or 'f' or 'e' or 'x' or 'b' or 'o' or '%';

    private static void Operands(ProgramFacts facts, BinaryOp b, List<Site> sites)
    {
        string? family = b.Operator switch
        {
            BinaryOperator.And or BinaryOperator.Or => "Logical",
            BinaryOperator.Is or BinaryOperator.IsNot => "Identity",
            var op when ProgramFacts.IsArithmetic(op) => "Arithmetic",
            var op when ProgramFacts.IsOrdering(op) => "Comparison",
            var op when ProgramFacts.IsEquality(op) => "Equality",
            _ => null,
        };
        if (family is null)
            return;
        Add(sites, facts, Position.Operand, family, b.Left, b, facts.TypeOf(b.Left), $"left operand of {b.Operator}");
        Add(sites, facts, Position.Operand, family, b.Right, b, facts.TypeOf(b.Right), $"right operand of {b.Operator}");

        // A binary operator a user type implements is a member-name site: the dunder reached by its operator.
        if (ProgramFacts.Dunder(b.Operator) is { } dunder && facts.TypeOf(b.Left) is { } left && facts.Types.TryGetValue(left, out var type)
            && facts.Method(type, dunder) is not null)
        {
            Add(sites, facts, Position.MemberName, "OperatorDunder", b, b, left, $"{b.Operator} on {left} ({dunder})");
        }
    }

    /// <summary>The declared type of the slot a conditional expression fills, when it fills a typed one.</summary>
    private static string? TypedSlotOf(ProgramFacts facts, ConditionalExpression c)
    {
        if (!facts.Parents.TryGetValue(c, out var parent))
            return null;
        return parent switch
        {
            VariableDeclaration { Type: { } t } v when ReferenceEquals(v.InitialValue, c) => ProgramFacts.Spell(t),
            Assignment { Target: Identifier { Name: var n } } a when ReferenceEquals(a.Value, c) => facts.NameType(n, a),
            ReturnStatement r when facts.EnclosingFunction(r)?.ReturnType is { } rt => ProgramFacts.Spell(rt),
            _ => null,
        };
    }

    private static void Calls(ProgramFacts facts, FunctionCall call, List<Site> sites)
    {
        var (family, parameterTypes) = Callee(facts, call);
        if (family is null)
            return;
        Add(sites, facts, Position.Call, family, call, call, null, $"call ({family}) with {call.Arguments.Length} argument(s)");
        for (int i = 0; i < call.Arguments.Length; i++)
        {
            var slot = parameterTypes is not null && i < parameterTypes.Count ? parameterTypes[i] : null;
            if (slot is not null && facts.IsTypeParameter(slot, call))
                slot = null;
            Add(sites, facts, Position.Argument, family, call.Arguments[i], call, slot, $"argument {i} of a {family} call");
        }
    }

    /// <summary>The callee family of <paramref name="call"/> and the declared parameter types its arguments fill.</summary>
    private static (string? Family, IReadOnlyList<string?>? Parameters) Callee(ProgramFacts facts, FunctionCall call)
    {
        switch (call.Function)
        {
            case Identifier { Name: "print" or "isinstance" or "None" or "super" }:
                return (null, null);
            case Identifier { Name: "range" or "abs" }:
                return ("OverloadedBuiltin", call.Arguments.Select(_ => (string?)"int").ToList());
            case Identifier { Name: "int" or "float" }:
                return ("OverloadedBuiltin", new string?[] { "int-convertible" });
            case Identifier { Name: "len" }:
                return ("ProtocolBuiltin", new string?[] { "sized" });
            case Identifier { Name: "str" }:
                return ("StrBuiltin", new string?[] { null });
            case Identifier { Name: var name } when facts.Types.TryGetValue(name, out var type):
                if (type.IsDataclass)
                    return ("Dataclass", type.Fields.Values.ToList());
                return ("Constructor", facts.Method(type, "__init__") is { } init ? init.Parameters.Skip(1).Select(p => p.Type).ToList() : Array.Empty<string?>());
            case Identifier { Name: var name } when facts.Functions.TryGetValue(name, out var function):
                return ("UserFunction", function.Parameters.Select(p => p.Type).ToList());
            case IndexAccess { Object: Identifier { Name: var name }, Index: Identifier { Name: var typeArgument } }
                when facts.Functions.TryGetValue(name, out var generic) && generic.TypeParameters.Count == 1:
                return ("ExplicitGeneric", generic.Parameters
                    .Select(p => p.Type == generic.TypeParameters[0] ? typeArgument : p.Type).ToList());
            case MemberAccess { Object: SuperExpression or FunctionCall { Function: Identifier { Name: "super" } }, Member: "__init__" }:
                var owner = facts.EnclosingFunction(call) is { } def ? facts.Owner(def) : null;
                var baseInit = owner?.Bases.Select(b => facts.Types.GetValueOrDefault(ProgramFacts.BaseName(b)))
                    .Where(t => t is not null).Select(t => facts.Method(t!, "__init__")).FirstOrDefault(m => m is not null);
                return ("SuperInit", baseInit?.Parameters.Skip(1).Select(p => p.Type).ToList());
            case MemberAccess { Object: var receiver, Member: var member }:
                var receiverType = facts.TypeOf(receiver);
                if (receiverType == "str")
                    return ("StrMethod", Array.Empty<string?>());
                if (receiverType is not null && facts.Types.TryGetValue(receiverType, out var owning)
                    && facts.Method(owning, member) is { } method)
                    return ("Method", method.Parameters.Skip(1).Select(p => p.Type).ToList());
                return (null, null);
            default:
                return (null, null);
        }
    }

    private static void Names(ProgramFacts facts, Identifier id, List<Site> sites)
    {
        if (!facts.Parents.TryGetValue(id, out var parent))
            return;
        // A read, not a definition, a callee or a member name.
        if (parent is FunctionCall call && ReferenceEquals(call.Function, id))
            return;
        if (parent is Assignment a && ReferenceEquals(a.Target, id))
            return;
        if (parent is ForStatement f && ReferenceEquals(f.Target, id))
            return;
        if (parent is WithStatement w && w.Items.Any(i => ReferenceEquals(i.Target, id)))
            return;
        if (parent is BindingPattern or AsPattern)
            return;
        if (facts.NameType(id.Name, id) is not { } type)
            return;
        var family = Ancestors(facts, id).Any(n => n is FStringLiteral) ? "FStringHole"
            : Ancestors(facts, id).OfType<WithStatement>().Any(ws => ws.Items.Any(i => i.Target is Identifier { Name: var t } && t == id.Name))
                ? "WithTarget"
                : "Plain";
        Add(sites, facts, Position.Name, family, id, parent, type, $"read of {id.Name}: {type}");
    }

    private static void Members(ProgramFacts facts, MemberAccess ma, List<Site> sites)
    {
        if (ma.Object is SuperExpression or FunctionCall { Function: Identifier { Name: "super" } })
            return;
        if (!facts.Parents.TryGetValue(ma, out var parent))
            return;
        var receiver = facts.TypeOf(ma.Object);
        if (receiver is null)
            return;
        var isCall = parent is FunctionCall c && ReferenceEquals(c.Function, ma);
        var inFString = Ancestors(facts, ma).Any(n => n is FStringLiteral);
        string family;
        if (receiver == "str" && isCall)
            family = "StrMethod";
        else if (facts.Types.TryGetValue(receiver, out var type))
        {
            if (isCall && facts.Method(type, ma.Member) is not null)
                family = inFString ? "FStringMethod" : "Method";
            else if (!isCall && (type.Fields.ContainsKey(ma.Member) || facts.TypeOf(ma) is not null))
                family = parent is Assignment asg && ReferenceEquals(asg.Target, ma) ? "FieldStore" : "Field";
            else
                return;
        }
        else
            return;
        Add(sites, facts, Position.MemberName, family, ma, parent, receiver, $"member {ma.Member} of {receiver}");
    }

    private static void Annotation(ProgramFacts facts, TypeAnnotation ta, List<Site> sites)
    {
        if (!facts.Parents.TryGetValue(ta, out var parent))
            return;
        string? family = parent switch
        {
            TypeAnnotation => "TypeArgument",
            VariableDeclaration v when ReferenceEquals(v.Type, ta) => facts.EnclosingFunction(v) is not null
                ? "Declaration"
                : facts.Module.Body.Contains(v) ? "ModuleConstant" : "Field",
            FunctionDef f when ReferenceEquals(f.ReturnType, ta) => "Return",
            FunctionDef f when f.Parameters.Any(p => ReferenceEquals(p.Type, ta)) => "Parameter",
            ClassDef cd when cd.BaseClasses.Any(b => ReferenceEquals(b, ta)) => "BaseClass",
            InterfaceDef id when id.BaseInterfaces.Any(b => ReferenceEquals(b, ta)) => "BaseInterface",
            PositionalPattern or TypePattern => "PatternHead",
            TryStatement => "Handler",
            _ => null,
        };
        if (family is null)
            return;
        Add(sites, facts, Position.Annotation, family, ta, parent, ProgramFacts.Spell(ta), $"{family} annotation {ProgramFacts.Spell(ta)}");
    }

    private static void Store(ProgramFacts facts, VariableDeclaration v, List<Site> sites)
    {
        if (v.Type is not { } type || v.InitialValue is not { } value)
            return;
        var family = facts.EnclosingFunction(v) is null ? "ModuleConstant"
            : value is ListLiteral ? "ListInitializer"
            : "AnnotatedDeclaration";
        Add(sites, facts, Position.Store, family, value, v, ProgramFacts.Spell(type), $"initializer of {v.Name}: {ProgramFacts.Spell(type)}");
    }

    private static bool IsReassignment(ProgramFacts facts, Assignment a, string name)
    {
        // The name was declared before this statement (an annotated declaration or an earlier binding).
        var def = facts.EnclosingFunction(a);
        return def is not null && AstRewriter.Descendants(def)
            .TakeWhile(n => !ReferenceEquals(n, a))
            .Any(n => n is VariableDeclaration { Type: not null } d && d.Name == name);
    }

    private static void Returns(ProgramFacts facts, ReturnStatement r, List<Site> sites)
    {
        if (facts.EnclosingFunction(r) is not { } def)
            return;
        var slot = def.ReturnType is { } rt ? ProgramFacts.Spell(rt) : null;
        if (slot is not null && facts.IsTypeParameter(slot, r))
            slot = null;
        var owner = facts.Owner(def);
        var family = def.Name.StartsWith("__", StringComparison.Ordinal) ? "DunderReturn"
            : owner is not null ? "MethodReturn"
            : "Return";
        Add(sites, facts, Position.ReturnValue, r.Value is null ? "BareReturn" : family, r, def, slot,
            $"return in {def.Name}{(slot is null ? "" : $" -> {slot}")}");
    }

    private static void Patterns(ProgramFacts facts, MatchStatement m, List<Site> sites)
    {
        var subject = facts.TypeOf(m.Scrutinee);
        if (m.Cases.Length >= 2 && m.Cases[^1].Pattern is WildcardPattern)
            Add(sites, facts, Position.Pattern, "WildcardLast", m, m, subject, "match with a trailing wildcard arm");
        foreach (var arm in m.Cases)
        {
            foreach (var node in AstRewriter.Descendants(arm.Pattern))
            {
                string? family = node switch
                {
                    LiteralPattern => "Literal",
                    OrPattern => "Or",
                    AndPattern => "And",
                    PositionalPattern or TypePattern => "Class",
                    TuplePattern => "Tuple",
                    ListPattern => "List",
                    _ => null,
                };
                if (family is null)
                    continue;
                // A pattern nested in another reads the element type; only top-level patterns read the subject.
                var slot = ReferenceEquals(node, arm.Pattern) || family == "Literal" && arm.Pattern is OrPattern ? subject : null;
                Add(sites, facts, Position.Pattern, family, node, m, slot,
                    $"{family} pattern{(ArmReadsCapture(arm) ? " (arm reads a capture)" : "")}");
            }
        }
    }

    /// <summary>Whether the arm's guard or body reads a name its pattern binds.</summary>
    public static bool ArmReadsCapture(MatchCase arm)
    {
        var captures = AstRewriter.Descendants(arm.Pattern)
            .SelectMany(n => n switch
            {
                BindingPattern b => new[] { b.Name.Name },
                AsPattern a => new[] { a.Name.Name },
                _ => Array.Empty<string>(),
            })
            .ToHashSet(StringComparer.Ordinal);
        if (captures.Count == 0)
            return false;
        var reads = arm.Body.Cast<Node>().Concat(arm.Guard is { } g ? new Node[] { g } : Array.Empty<Node>())
            .SelectMany(AstRewriter.Descendants)
            .OfType<Identifier>()
            .Any(i => captures.Contains(i.Name));
        return reads;
    }

    private static IEnumerable<Node> Ancestors(ProgramFacts facts, Node node)
    {
        for (var current = node; facts.Parents.TryGetValue(current, out var parent); current = parent)
            yield return parent;
    }
}

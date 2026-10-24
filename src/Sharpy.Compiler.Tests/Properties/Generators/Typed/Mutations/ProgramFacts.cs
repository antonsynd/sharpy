using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// What a generated program says about its own types, read from its syntax alone (#2324, P63b Phase 1):
/// annotations, literal kinds, constructor calls of its own classes, the return annotations of its own
/// functions and methods, the names its <see cref="TypeEnv"/> declares. The generator is the oracle of its
/// own programs — nothing here asks the compiler, so a site's slot type cannot drift with the checker it
/// tests. A type the facts cannot name is <c>null</c>: the site is still enumerated (and counted as an
/// unknown slot by the census) but no type-directed break applies to it.
/// Type strings: <c>int</c>, <c>str</c>, <c>bool</c>, <c>float</c>, <c>None</c>, <c>list[int]</c>,
/// <c>tuple[int, int]</c>, <c>int?</c>, or a class name declared by the program.
/// </summary>
internal sealed class ProgramFacts
{
    public sealed record FunctionFacts(
        string Name,
        IReadOnlyList<(string Name, string? Type)> Parameters,
        string? ReturnType,
        IReadOnlyList<string> TypeParameters,
        FunctionDef Def);

    public sealed record TypeFacts(
        string Name,
        bool IsInterface,
        IReadOnlyList<string> Bases,
        IReadOnlyDictionary<string, string?> Fields,
        IReadOnlyDictionary<string, FunctionFacts> Methods,
        IReadOnlyList<string> TypeParameters,
        bool IsDataclass,
        Statement Def);

    private readonly Dictionary<string, TypeFacts> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionFacts> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _moduleScope = new(StringComparer.Ordinal);
    private readonly Dictionary<FunctionDef, Dictionary<string, string>> _scopes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FunctionDef, TypeFacts> _owners = new(ReferenceEqualityComparer.Instance);

    public Module Module { get; }

    /// <summary>Every node's nearest node ancestor.</summary>
    public IReadOnlyDictionary<Node, Node> Parents { get; }

    public IReadOnlyDictionary<string, TypeFacts> Types => _types;

    public IReadOnlyDictionary<string, FunctionFacts> Functions => _functions;

    public static readonly IReadOnlySet<string> BuiltinTypes =
        new HashSet<string>(StringComparer.Ordinal) { "int", "str", "bool", "float" };

    public ProgramFacts(Module module)
    {
        Module = module;
        Parents = AstRewriter.Parents(module);

        foreach (var statement in module.Body)
        {
            switch (statement)
            {
                case ClassDef c:
                    _types[c.Name] = TypeOf(c.Name, false, c.BaseClasses, c.Body, c.TypeParameters,
                        c.Decorators.Any(d => d.Name == "dataclass"), c);
                    break;
                case InterfaceDef i:
                    _types[i.Name] = TypeOf(i.Name, true, i.BaseInterfaces, i.Body, i.TypeParameters, false, i);
                    break;
                case FunctionDef f:
                    _functions[f.Name] = Facts(f);
                    break;
                case VariableDeclaration { Type: { } type } v:
                    _moduleScope.TryAdd(v.Name, Spell(type));
                    break;
            }
        }

        foreach (var type in _types.Values)
        {
            foreach (var method in type.Methods.Values)
                _owners[method.Def] = type;
        }

        foreach (var def in AstRewriter.Descendants(module).OfType<FunctionDef>())
            _scopes[def] = BuildScope(def);
    }

    private TypeFacts TypeOf(string name, bool isInterface, IEnumerable<TypeAnnotation> bases,
        IEnumerable<Statement> body, IEnumerable<TypeParameterDef> typeParameters, bool isDataclass, Statement def)
    {
        var statements = body.ToList();
        return new TypeFacts(
            name,
            isInterface,
            bases.Select(Spell).ToList(),
            statements.OfType<VariableDeclaration>()
                .GroupBy(v => v.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Type is { } t ? Spell(t) : null, StringComparer.Ordinal),
            statements.OfType<FunctionDef>()
                .GroupBy(f => f.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => Facts(g.First()), StringComparer.Ordinal),
            typeParameters.Select(t => t.Name).ToList(),
            isDataclass,
            def);
    }

    private static FunctionFacts Facts(FunctionDef def) => new(
        def.Name,
        def.Parameters.Select(p => (p.Name, p.Type is { } t ? (string?)Spell(t) : null)).ToList(),
        def.ReturnType is { } r ? Spell(r) : null,
        def.TypeParameters.Select(t => t.Name).ToList(),
        def);

    /// <summary>A type annotation spelled as a type string.</summary>
    public static string Spell(TypeAnnotation annotation)
    {
        var name = annotation.NameParts.Length > 1 ? string.Join(".", annotation.NameParts) : annotation.Name;
        var spelled = annotation.TypeArguments.Length == 0
            ? name
            : $"{name}[{string.Join(", ", annotation.TypeArguments.Select(Spell))}]";
        return annotation.IsOptional ? spelled + "?" : spelled;
    }

    /// <summary>The innermost function definition enclosing <paramref name="node"/>, or null at module level.</summary>
    public FunctionDef? EnclosingFunction(Node node)
    {
        for (var current = node; Parents.TryGetValue(current, out var parent); current = parent)
        {
            if (parent is FunctionDef f)
                return f;
        }
        return null;
    }

    /// <summary>The class or interface whose method <paramref name="def"/> is, or null.</summary>
    public TypeFacts? Owner(FunctionDef def) => _owners.GetValueOrDefault(def);

    /// <summary>The declared type of the name <paramref name="name"/> where <paramref name="at"/> reads it.</summary>
    public string? NameType(string name, Node at)
    {
        for (var function = EnclosingFunction(at); function is not null; function = EnclosingFunction(function))
        {
            if (_scopes.TryGetValue(function, out var scope) && scope.TryGetValue(name, out var type))
                return type;
        }
        return _moduleScope.GetValueOrDefault(name);
    }

    /// <summary>Whether <paramref name="name"/> is a type parameter in scope at <paramref name="at"/>.</summary>
    public bool IsTypeParameter(string name, Node at)
    {
        for (var function = EnclosingFunction(at); function is not null; function = EnclosingFunction(function))
        {
            if (function.TypeParameters.Any(t => t.Name == name))
                return true;
            if (Owner(function) is { } owner && owner.TypeParameters.Contains(name))
                return true;
        }
        return false;
    }

    // ---- scopes -----------------------------------------------------------------------------

    private Dictionary<string, string> BuildScope(FunctionDef def)
    {
        var scope = new Dictionary<string, string>(StringComparer.Ordinal);
        var owner = _owners.GetValueOrDefault(def);
        foreach (var parameter in def.Parameters)
        {
            if (parameter.Type is { } t)
                scope[parameter.Name] = Spell(t);
            else if (parameter.Name == "self" && owner is not null)
                scope[parameter.Name] = owner.Name;
        }
        _scopes[def] = scope; // visible to TypeOf while the body is read in order
        foreach (var statement in def.Body)
            Bind(statement, scope);
        return scope;
    }

    private void Bind(Statement statement, Dictionary<string, string> scope)
    {
        switch (statement)
        {
            case VariableDeclaration { Type: { } type } v:
                scope.TryAdd(v.Name, Spell(type));
                break;
            case VariableDeclaration { InitialValue: { } value } v:
                if (TypeOf(value) is { } inferred)
                    scope.TryAdd(v.Name, inferred);
                break;
            case Assignment { Target: Identifier { Name: var name }, Value: var value, Operator: AssignmentOperator.Assign }:
                if (TypeOf(value) is { } assigned)
                    scope.TryAdd(name, assigned);
                break;
            case ForStatement f:
                if (f.Target is Identifier { Name: var target } && ElementType(f.Iterator) is { } element)
                    scope.TryAdd(target, element);
                foreach (var s in f.Body)
                    Bind(s, scope);
                break;
            case WhileStatement w:
                foreach (var s in w.Body)
                    Bind(s, scope);
                break;
            case IfStatement i:
                foreach (var s in i.ThenBody.Concat(i.ElifClauses.SelectMany(e => e.Body)).Concat(i.ElseBody))
                    Bind(s, scope);
                break;
            case WithStatement w:
                foreach (var item in w.Items)
                {
                    if (item.Target is Identifier { Name: var withTarget } && EnteredType(item.ContextExpression, w.IsAsync) is { } entered)
                        scope.TryAdd(withTarget, entered);
                }
                foreach (var s in w.Body)
                    Bind(s, scope);
                break;
            case TryStatement t:
                foreach (var s in t.Body.Concat(t.Handlers.SelectMany(h => h.Body)).Concat(t.ElseBody).Concat(t.FinallyBody))
                    Bind(s, scope);
                foreach (var handler in t.Handlers)
                {
                    if (handler.Name is { } n && handler.ExceptionType is { } et)
                        scope.TryAdd(n, Spell(et));
                }
                break;
            case MatchStatement m:
                foreach (var s in m.Cases.SelectMany(c => c.Body))
                    Bind(s, scope);
                break;
        }
    }

    private string? ElementType(Expression iterator) => iterator switch
    {
        Parenthesized p => ElementType(p.Expression),
        FunctionCall { Function: Identifier { Name: "range" } } => "int",
        FunctionCall { Function: Identifier { Name: var f } } when _functions.TryGetValue(f, out var facts)
            && facts.Def.Body.Any(s => s is YieldStatement) => facts.ReturnType,
        _ => TypeOf(iterator) is { } t && t.StartsWith("list[", StringComparison.Ordinal) ? t[5..^1] : null,
    };

    private string? EnteredType(Expression context, bool isAsync)
    {
        if (TypeOf(context) is not { } type || !_types.TryGetValue(type, out var facts))
            return null;
        var enter = facts.Methods.GetValueOrDefault(isAsync ? "__aenter__" : "__enter__");
        return enter?.ReturnType is "Self" ? type : enter?.ReturnType;
    }

    // ---- expressions ------------------------------------------------------------------------

    private static readonly HashSet<BinaryOperator> Arithmetic = new()
    {
        BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply, BinaryOperator.Divide,
        BinaryOperator.FloorDivide, BinaryOperator.Modulo, BinaryOperator.Power,
    };

    public static bool IsArithmetic(BinaryOperator op) => Arithmetic.Contains(op);

    public static bool IsOrdering(BinaryOperator op) => op is BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
        or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual;

    public static bool IsEquality(BinaryOperator op) => op is BinaryOperator.Equal or BinaryOperator.NotEqual;

    /// <summary>The dunder a user type defines for <paramref name="op"/>, or null.</summary>
    public static string? Dunder(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "__add__",
        BinaryOperator.Subtract => "__sub__",
        BinaryOperator.Multiply => "__mul__",
        BinaryOperator.Modulo => "__mod__",
        BinaryOperator.Divide => "__truediv__",
        BinaryOperator.FloorDivide => "__floordiv__",
        BinaryOperator.LessThan => "__lt__",
        BinaryOperator.LessThanOrEqual => "__le__",
        BinaryOperator.GreaterThan => "__gt__",
        BinaryOperator.GreaterThanOrEqual => "__ge__",
        BinaryOperator.Equal => "__eq__",
        BinaryOperator.NotEqual => "__ne__",
        _ => null,
    };

    public static string? Dunder(UnaryOperator op) => op switch
    {
        UnaryOperator.Minus => "__neg__",
        UnaryOperator.Plus => "__pos__",
        UnaryOperator.BitwiseNot => "__invert__",
        _ => null,
    };

    /// <summary>The static type of <paramref name="expression"/> by the program's own facts, or null.</summary>
    public string? TypeOf(Expression expression) => expression switch
    {
        IntegerLiteral => "int",
        FloatLiteral => "float",
        StringLiteral or FStringLiteral => "str",
        BooleanLiteral => "bool",
        NoneLiteral => "None",
        Parenthesized p => TypeOf(p.Expression),
        Identifier id => NameType(id.Name, id),
        ListLiteral { Elements.Length: > 0 } list => Same(list.Elements.Select(TypeOf)) is { } e ? $"list[{e}]" : null,
        TupleLiteral { Elements.Length: > 0 } tuple => tuple.Elements.Select(TypeOf).ToList() is var parts && parts.All(p => p is not null)
            ? $"tuple[{string.Join(", ", parts)}]"
            : null,
        BinaryOp b => BinaryType(b),
        UnaryOp { Operator: UnaryOperator.Not } => "bool",
        UnaryOp u => TypeOf(u.Operand) is { } operand
            ? operand is "int" or "float" ? operand : DunderReturn(operand, Dunder(u.Operator))
            : null,
        ConditionalExpression c => TypeOf(c.ThenValue) is { } then && then == TypeOf(c.ElseValue) ? then : null,
        FunctionCall call => CallType(call),
        MemberAccess m => TypeOf(m.Object) is { } receiver && _types.TryGetValue(receiver, out var facts)
            ? facts.Fields.GetValueOrDefault(m.Member) ?? FieldOfBase(facts, m.Member)
            : null,
        IndexAccess { Object: var o } => TypeOf(o) is { } list && list.StartsWith("list[", StringComparison.Ordinal) ? list[5..^1] : null,
        _ => null,
    };

    private string? FieldOfBase(TypeFacts facts, string member)
    {
        foreach (var b in facts.Bases)
        {
            if (_types.TryGetValue(b, out var baseFacts) && (baseFacts.Fields.GetValueOrDefault(member) ?? FieldOfBase(baseFacts, member)) is { } t)
                return t;
        }
        return null;
    }

    private static string? Same(IEnumerable<string?> types)
    {
        var distinct = types.Distinct().ToList();
        return distinct is [{ } only] ? only : null;
    }

    private string? BinaryType(BinaryOp b)
    {
        if (IsOrdering(b.Operator) || IsEquality(b.Operator)
            || b.Operator is BinaryOperator.And or BinaryOperator.Or or BinaryOperator.Is or BinaryOperator.IsNot
                or BinaryOperator.In or BinaryOperator.NotIn)
        {
            return "bool";
        }
        if (!IsArithmetic(b.Operator))
            return null;
        var (left, right) = (TypeOf(b.Left), TypeOf(b.Right));
        return (left, right) switch
        {
            ("int", "int") => "int",
            ("float", "float") or ("int", "float") or ("float", "int") => "float",
            ("str", "str") when b.Operator == BinaryOperator.Add => "str",
            ({ } user, _) when _types.ContainsKey(user) => DunderReturn(user, Dunder(b.Operator)),
            _ => null,
        };
    }

    private string? DunderReturn(string type, string? dunder) =>
        dunder is not null && _types.TryGetValue(type, out var facts) && facts.Methods.TryGetValue(dunder, out var m)
            ? m.ReturnType
            : null;

    private string? CallType(FunctionCall call) => call.Function switch
    {
        Identifier { Name: "len" or "abs" or "int" } => "int",
        Identifier { Name: "str" } => "str",
        Identifier { Name: "float" } => "float",
        Identifier { Name: "bool" or "isinstance" } => "bool",
        Identifier { Name: var name } when _types.ContainsKey(name) => name,
        Identifier { Name: var name } when _functions.TryGetValue(name, out var f) =>
            f.ReturnType is { } r && !f.TypeParameters.Contains(r) ? r : null,
        IndexAccess { Object: Identifier { Name: var name }, Index: var arg } when _functions.TryGetValue(name, out var g) =>
            g.ReturnType is { } r && g.TypeParameters is [var only] && r == only && arg is Identifier { Name: var concrete }
                ? concrete
                : null,
        IndexAccess { Object: Identifier { Name: var name } } when _types.ContainsKey(name) => null,
        MemberAccess { Object: var receiver, Member: var member } => MethodReturn(receiver, member),
        _ => null,
    };

    private string? MethodReturn(Expression receiver, string member)
    {
        var type = TypeOf(receiver);
        if (type == "str" && member is "upper" or "lower" or "strip")
            return "str";
        if (type is null || !_types.TryGetValue(type, out var facts))
            return null;
        var method = Method(facts, member);
        return method?.ReturnType is "Self" ? type : method?.ReturnType;
    }

    /// <summary>The method <paramref name="member"/> of <paramref name="facts"/> or of a base it names.</summary>
    public FunctionFacts? Method(TypeFacts facts, string member)
    {
        if (facts.Methods.TryGetValue(member, out var m))
            return m;
        foreach (var b in facts.Bases)
        {
            if (_types.TryGetValue(BaseName(b), out var baseFacts) && Method(baseFacts, member) is { } inherited)
                return inherited;
        }
        return null;
    }

    /// <summary>A base spelled <c>I[int]</c> named by its head.</summary>
    public static string BaseName(string spelled) => spelled.IndexOf('[') is var i and >= 0 ? spelled[..i] : spelled;
}

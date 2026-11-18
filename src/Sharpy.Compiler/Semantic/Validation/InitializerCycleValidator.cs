using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// The cycle graph of rule N3 (plan-775c50 D8; the owner's Q7, Q21, Q23): a value whose initializer
/// can never be given a value is a compile error, SPY0278.
///
/// <para><b>Nodes.</b> Every module or <c>@static</c> value of this module with an initializer: a module
/// variable, a module <c>const</c>, a module property with a default, a <c>@static</c> field, a
/// <c>@static</c> property with a default, and a type body's <c>const</c> (<see cref="NodeKinds"/>). All
/// but the consts are Guarded (<see cref="StaticStorageFacts"/>); every <c>const</c> with an initializer
/// is a node, and a literal one reads nothing, so it is on no cycle. An instance member is initialized
/// per object, in a constructor, and is no node.</para>
///
/// <para><b>Reads.</b> A read is resolved through what the checker bound — the identifier's symbol, the
/// member access's resolution, the receiver's denoted type — never through its spelling, and it joins
/// VALUES, never hosts: a read of one value of a type is no edge to the type's other values (D5's
/// read-back is a run-time error, not this graph's).</para>
///
/// <para><b>Two edge sets, never mixed in one cycle.</b> A CERTAIN edge is a read whose slot, and every
/// slot enclosing it, is evaluated whenever its initializer is (<see cref="SlotMap"/>, equal to the
/// stage-0 generator's classification, Q7): nothing under a conditional slot, inside a pattern or in a
/// nested function's body is certain. A store is no edge: the target of a store site and an
/// <c>out</c> operand are stored, not read (Q21). A LEXICAL edge is any read, and joins two
/// <c>const</c>s only. A node on a cycle of certain edges, or on a cycle of lexical edges (all
/// <c>const</c>s), is SPY0278 — unless a plain store in the module names a node of the certain cycle
/// (Q23): the store may run before the value's initializer, which then replaces it, so the reads around
/// the cycle need never meet a value that is being initialized.</para>
///
/// <para><b>Predicate.</b> A refused cycle lies wholly among <c>const</c>s, or its every edge is evaluated
/// whenever its initializer is and no store in its module names a node of it, so initializing any node
/// raises. A store from another module is the residue (A11).</para>
/// </summary>
internal sealed class InitializerCycleValidator : ISemanticValidator
{
    public string Name => "InitializerCycleValidator";

    // After ConstantPositionValidator (250); the slot of the retired forward-read validator (SPY0618, D9).
    public int Order => 251;

    // ------------------------------------------------------------------------------------------- //
    // The node kinds
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A kind of value the graph holds (D8).</summary>
    internal enum NodeKind
    {
        ModuleVariable,
        ModuleConst,
        ModuleProperty,
        StaticField,
        StaticProperty,
        TypeConst,
    }

    /// <summary>The node kinds and whether each is a <c>const</c> (whose lexical edges count). A kind not
    /// listed here is no node: its reads and the reads of it are no edges.</summary>
    internal static readonly IReadOnlyDictionary<NodeKind, bool> NodeKinds = new Dictionary<NodeKind, bool>
    {
        [NodeKind.ModuleVariable] = false,
        [NodeKind.ModuleConst] = true,
        [NodeKind.ModuleProperty] = false,
        [NodeKind.StaticField] = false,
        [NodeKind.StaticProperty] = false,
        [NodeKind.TypeConst] = true,
    };

    // ------------------------------------------------------------------------------------------- //
    // The slot map (Q7)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>Whether a slot's child is evaluated whenever its parent is, may not be, or is stored to.</summary>
    internal enum SlotEvaluation
    {
        Always,
        Conditionally,
        Stored,
    }

    /// <summary>One case of a slot: <see cref="FirstCase"/>, <see cref="SecondCase"/>, <see cref="LaterCase"/> by
    /// the child's position (the clause, operand or arm), or null for every child of the slot.</summary>
    internal sealed record SlotCase(string? Case, SlotEvaluation Evaluation);

    internal const string FirstCase = "First";
    internal const string SecondCase = "Second";
    internal const string LaterCase = "Later";

    /// <summary>The right operand of a <see cref="BinaryOp"/> is its operator's (<see cref="BinaryRights"/>).</summary>
    internal const string BinaryRightSlot = "BinaryOp.Right";

    /// <summary>The operand of a <see cref="ModifiedArgument"/> is its modifier's (<see cref="ModifiedArguments"/>).</summary>
    internal const string ModifiedArgumentSlot = "ModifiedArgument.Argument";

    private static SlotCase[] Always() => new[] { new SlotCase(null, SlotEvaluation.Always) };

    private static SlotCase[] Conditionally() => new[] { new SlotCase(null, SlotEvaluation.Conditionally) };

    private static SlotCase[] Stored() => new[] { new SlotCase(null, SlotEvaluation.Stored) };

    /// <summary>The first child is evaluated whenever its parent is; a later one only when those before it allow.</summary>
    private static SlotCase[] FirstOnly() => new[]
    {
        new SlotCase(FirstCase, SlotEvaluation.Always), new SlotCase(LaterCase, SlotEvaluation.Conditionally),
    };

    /// <summary>Every slot of every concrete <see cref="Expression"/> subclass, keyed as the stage-0 generator
    /// keys it (<c>Subclass.Property[.Holder property]</c>, <c>:Concrete</c> after an abstract holder), but
    /// <see cref="BinaryRightSlot"/> and <see cref="ModifiedArgumentSlot"/>, which their operator and modifier
    /// classify.</summary>
    internal static readonly IReadOnlyDictionary<string, SlotCase[]> SlotMap = BuildSlotMap();

    private static Dictionary<string, SlotCase[]> BuildSlotMap()
    {
        var map = new Dictionary<string, SlotCase[]>(StringComparer.Ordinal)
        {
            ["FStringLiteral.Parts.Expression"] = Always(),
            ["FStringLiteral.Parts.Spec"] = Always(),
            ["TStringLiteral.Parts.Expression"] = Always(),
            // A template evaluates the nested fields of its format spec, as an f-string does.
            ["TStringLiteral.Parts.Spec"] = Always(),
            ["ListLiteral.Elements"] = Always(),
            ["DictLiteral.Entries.Key"] = Always(),
            ["DictLiteral.Entries.Value"] = Always(),
            ["SetLiteral.Elements"] = Always(),
            ["TupleLiteral.Elements"] = Always(),
            ["ListComprehension.Element"] = Conditionally(),
            ["GeneratorExpression.Element"] = Conditionally(),
            ["SetComprehension.Element"] = Conditionally(),
            ["DictComprehension.Key"] = Conditionally(),
            ["DictComprehension.Value"] = Conditionally(),
            ["DictSpreadComprehension.Spread"] = Conditionally(),
            ["MemberAccess.Object"] = Always(),
            ["IndexAccess.Object"] = Always(),
            ["IndexAccess.Index"] = Always(),
            ["SliceAccess.Object"] = Always(),
            ["SliceAccess.Start"] = Always(),
            ["SliceAccess.Stop"] = Always(),
            ["SliceAccess.Step"] = Always(),
            ["MultiAxisAccess.Object"] = Always(),
            ["MultiAxisAccess.Dimensions.Index"] = Always(),
            ["MultiAxisAccess.Dimensions.Start"] = Always(),
            ["MultiAxisAccess.Dimensions.Stop"] = Always(),
            ["MultiAxisAccess.Dimensions.Step"] = Always(),
            ["FunctionCall.Function"] = Always(),
            ["FunctionCall.Arguments"] = Always(),
            ["FunctionCall.KeywordArguments.Value"] = Always(),
            ["UnaryOp.Operand"] = Always(),
            ["QuestionMarkExpression.Operand"] = Always(),
            ["AwaitExpression.Operand"] = Always(),
            ["BinaryOp.Left"] = Always(),
            // A chain stops at the first false comparison: its first two operands are always compared.
            ["ComparisonChain.Operands"] = new[]
            {
                new SlotCase(FirstCase, SlotEvaluation.Always), new SlotCase(SecondCase, SlotEvaluation.Always),
                new SlotCase(LaterCase, SlotEvaluation.Conditionally),
            },
            ["ConditionalExpression.Test"] = Always(),
            ["ConditionalExpression.ThenValue"] = Conditionally(),
            ["ConditionalExpression.ElseValue"] = Conditionally(),
            // A nested function's body runs when it is called, not where it is written.
            ["LambdaExpression.Body"] = Conditionally(),
            // A lambda's default is evaluated where the lambda is (and must be a constant, SPY0401).
            ["LambdaExpression.Parameters.DefaultValue"] = Always(),
            ["TypeCoercion.Value"] = Always(),
            ["TypeCheck.Value"] = Always(),
            ["TypeCheck.IdentityOperand"] = Always(),
            ["Parenthesized.Expression"] = Always(),
            ["WalrusExpression.Value"] = Always(),
            ["TryExpression.Operand"] = Always(),
            ["MaybeExpression.Operand"] = Always(),
            ["StarExpression.Operand"] = Always(),
            ["SpreadElement.Value"] = Always(),
            ["MatchExpression.Scrutinee"] = Always(),
            // The first arm's pattern is tried on every run of the match; a later arm's only when the arms before it fail.
            ["MatchExpression.Arms.Pattern"] = FirstOnly(),
            ["MatchExpression.Arms.Guard"] = Conditionally(),
            ["MatchExpression.Arms.Result"] = Conditionally(),
        };

        // A comprehension's first iterable is evaluated where the comprehension is; a later one, its
        // conditions and its element only per item. Its target is a store site.
        foreach (var comprehension in new[] { "ListComprehension", "GeneratorExpression", "SetComprehension", "DictComprehension", "DictSpreadComprehension" })
        {
            map[$"{comprehension}.Clauses:ForClause.Iterator"] = FirstOnly();
            map[$"{comprehension}.Clauses:ForClause.Target"] = Stored();
            map[$"{comprehension}.Clauses:IfClause.Condition"] = Conditionally();
        }

        return map;
    }

    /// <summary>The right operand of each operator: a short-circuit operator's may not be evaluated.</summary>
    internal static readonly IReadOnlyDictionary<BinaryOperator, SlotEvaluation> BinaryRights =
        Enum.GetValues<BinaryOperator>().ToDictionary(
            op => op,
            op => op is BinaryOperator.And or BinaryOperator.Or or BinaryOperator.NullCoalesce
                ? SlotEvaluation.Conditionally
                : SlotEvaluation.Always);

    /// <summary>The operand of each modifier: a <c>ref</c> or <c>in</c> operand is read, an <c>out</c> operand
    /// is stored (Q21), and a plain argument (which the parser builds as no <see cref="ModifiedArgument"/>) is read.</summary>
    internal static readonly IReadOnlyDictionary<ParameterModifier, SlotEvaluation> ModifiedArguments = new Dictionary<ParameterModifier, SlotEvaluation>
    {
        [ParameterModifier.None] = SlotEvaluation.Always,
        [ParameterModifier.Ref] = SlotEvaluation.Always,
        [ParameterModifier.In] = SlotEvaluation.Always,
        [ParameterModifier.Out] = SlotEvaluation.Stored,
    };

    /// <summary>The evaluation of the <paramref name="index"/>th child of slot <paramref name="key"/>. A slot
    /// the map does not hold is taken as conditional: it adds no certain edge.</summary>
    internal static SlotEvaluation Classify(string key, int index)
    {
        if (!SlotMap.TryGetValue(key, out var cases))
            return SlotEvaluation.Conditionally;
        if (cases.Length == 1 && cases[0].Case == null)
            return cases[0].Evaluation;
        var wanted = index == 0 ? FirstCase : index == 1 && cases.Any(c => c.Case == SecondCase) ? SecondCase : LaterCase;
        return cases.FirstOrDefault(c => c.Case == wanted)?.Evaluation ?? SlotEvaluation.Conditionally;
    }

    // ------------------------------------------------------------------------------------------- //
    // The slots of a node
    // ------------------------------------------------------------------------------------------- //

    /// <summary>One child of an expression: its slot, its position in the slot, the expression or pattern there,
    /// and its class.</summary>
    internal readonly record struct SlotChild(string Key, int Index, Node Child, SlotEvaluation Evaluation);

    /// <summary>The children of <paramref name="node"/>, each in its slot, classified. Total over the concrete
    /// <see cref="Expression"/> subclasses (deliberately: a subclass it does not name adds no certain edge).</summary>
    internal static IEnumerable<SlotChild> SlotsOf(Expression node)
    {
        switch (node)
        {
            case FStringLiteral f:
                return Parts(f.Parts, "FStringLiteral");
            case TStringLiteral t:
                return Parts(t.Parts, "TStringLiteral");
            case ListLiteral list:
                return Each("ListLiteral.Elements", list.Elements);
            case SetLiteral set:
                return Each("SetLiteral.Elements", set.Elements);
            case TupleLiteral tuple:
                return Each("TupleLiteral.Elements", tuple.Elements);
            case DictLiteral dict:
                return dict.Entries.SelectMany(e => Opt("DictLiteral.Entries.Key", e.Key).Concat(One("DictLiteral.Entries.Value", e.Value)));
            case ListComprehension lc:
                return One("ListComprehension.Element", lc.Element).Concat(Clauses("ListComprehension", lc.Clauses));
            case GeneratorExpression ge:
                return One("GeneratorExpression.Element", ge.Element).Concat(Clauses("GeneratorExpression", ge.Clauses));
            case SetComprehension sc:
                return One("SetComprehension.Element", sc.Element).Concat(Clauses("SetComprehension", sc.Clauses));
            case DictComprehension dc:
                return One("DictComprehension.Key", dc.Key).Concat(One("DictComprehension.Value", dc.Value)).Concat(Clauses("DictComprehension", dc.Clauses));
            case DictSpreadComprehension ds:
                return One("DictSpreadComprehension.Spread", ds.Spread).Concat(Clauses("DictSpreadComprehension", ds.Clauses));
            case MemberAccess ma:
                return One("MemberAccess.Object", ma.Object);
            case IndexAccess ia:
                return One("IndexAccess.Object", ia.Object).Concat(One("IndexAccess.Index", ia.Index));
            case SliceAccess sa:
                return One("SliceAccess.Object", sa.Object).Concat(Opt("SliceAccess.Start", sa.Start))
                    .Concat(Opt("SliceAccess.Stop", sa.Stop)).Concat(Opt("SliceAccess.Step", sa.Step));
            case MultiAxisAccess mx:
                return One("MultiAxisAccess.Object", mx.Object).Concat((mx.Dimensions.IsDefault ? ImmutableArray<SubscriptDimension>.Empty : mx.Dimensions).SelectMany(d =>
                    Opt("MultiAxisAccess.Dimensions.Index", d.Index).Concat(Opt("MultiAxisAccess.Dimensions.Start", d.Start))
                        .Concat(Opt("MultiAxisAccess.Dimensions.Stop", d.Stop)).Concat(Opt("MultiAxisAccess.Dimensions.Step", d.Step))));
            case FunctionCall call:
                return One("FunctionCall.Function", call.Function).Concat(Each("FunctionCall.Arguments", call.Arguments))
                    .Concat(call.KeywordArguments.SelectMany(k => One("FunctionCall.KeywordArguments.Value", k.Value)));
            case UnaryOp unary:
                return One("UnaryOp.Operand", unary.Operand);
            case QuestionMarkExpression question:
                return One("QuestionMarkExpression.Operand", question.Operand);
            case AwaitExpression awaited:
                return One("AwaitExpression.Operand", awaited.Operand);
            case BinaryOp binary:
                return One("BinaryOp.Left", binary.Left).Append(new SlotChild(BinaryRightSlot, 0, binary.Right, BinaryRights[binary.Operator]));
            case ComparisonChain chain:
                return Each("ComparisonChain.Operands", chain.Operands);
            case ConditionalExpression conditional:
                return One("ConditionalExpression.Test", conditional.Test).Concat(One("ConditionalExpression.ThenValue", conditional.ThenValue))
                    .Concat(One("ConditionalExpression.ElseValue", conditional.ElseValue));
            case LambdaExpression lambda:
                return lambda.Parameters.SelectMany(p => Opt("LambdaExpression.Parameters.DefaultValue", p.DefaultValue))
                    .Concat(One("LambdaExpression.Body", lambda.Body));
            case ModifiedArgument modified:
                return new[] { new SlotChild(ModifiedArgumentSlot, 0, modified.Argument, ModifiedArguments[modified.Modifier]) };
            case TypeCoercion coercion:
                return One("TypeCoercion.Value", coercion.Value);
            case TypeCheck check:
                return One("TypeCheck.Value", check.Value).Concat(Opt("TypeCheck.IdentityOperand", check.IdentityOperand));
            case Parenthesized parenthesized:
                return One("Parenthesized.Expression", parenthesized.Expression);
            case WalrusExpression walrus:
                return One("WalrusExpression.Value", walrus.Value);
            case TryExpression attempt:
                return One("TryExpression.Operand", attempt.Operand);
            case MaybeExpression maybe:
                return One("MaybeExpression.Operand", maybe.Operand);
            case StarExpression star:
                return One("StarExpression.Operand", star.Operand);
            case SpreadElement spread:
                return One("SpreadElement.Value", spread.Value);
            case MatchExpression match:
                return One("MatchExpression.Scrutinee", match.Scrutinee).Concat(match.Arms.SelectMany((arm, i) =>
                    new[] { new SlotChild("MatchExpression.Arms.Pattern", i, arm.Pattern, Classify("MatchExpression.Arms.Pattern", i)) }
                        .Concat(Opt("MatchExpression.Arms.Guard", arm.Guard)).Concat(One("MatchExpression.Arms.Result", arm.Result))));
            // The leaves: a literal, a name, `super`.
            case IntegerLiteral or FloatLiteral or StringLiteral or BytesLiteralExpression or BooleanLiteral or NoneLiteral
                or EllipsisLiteral or Identifier or SuperExpression:
                return Array.Empty<SlotChild>();
            default:
                // A subclass this switch does not name: its children add no certain edge.
                return node.GetChildNodes().Where(c => c != null).Select(c => new SlotChild("", 0, c, SlotEvaluation.Conditionally));
        }
    }

    private static IEnumerable<SlotChild> One(string key, Expression child) => new[] { new SlotChild(key, 0, child, Classify(key, 0)) };

    private static IEnumerable<SlotChild> Opt(string key, Expression? child) => child == null ? Array.Empty<SlotChild>() : One(key, child);

    private static IEnumerable<SlotChild> Each(string key, ImmutableArray<Expression> children) =>
        children.Select((child, i) => new SlotChild(key, i, child, Classify(key, i)));

    /// <summary>A comprehension's clauses: the position of a <c>for</c> clause among them decides its iterable's case.</summary>
    private static IEnumerable<SlotChild> Clauses(string comprehension, ImmutableArray<ComprehensionClause> clauses)
    {
        for (var i = 0; i < clauses.Length; i++)
        {
            switch (clauses[i])
            {
                case ForClause clause:
                    var iterator = $"{comprehension}.Clauses:ForClause.Iterator";
                    var target = $"{comprehension}.Clauses:ForClause.Target";
                    yield return new SlotChild(iterator, i, clause.Iterator, Classify(iterator, i));
                    yield return new SlotChild(target, i, clause.Target, Classify(target, i));
                    break;
                case IfClause condition:
                    var key = $"{comprehension}.Clauses:IfClause.Condition";
                    yield return new SlotChild(key, i, condition.Condition, Classify(key, i));
                    break;
            }
        }
    }

    /// <summary>An f-string's or template's parts: each part's expression, and its format spec, whose nested
    /// fields are one slot.</summary>
    private static IEnumerable<SlotChild> Parts(ImmutableArray<FStringPart> parts, string holder)
    {
        foreach (var part in parts)
        {
            if (part.Expression != null)
            {
                foreach (var child in One($"{holder}.Parts.Expression", part.Expression))
                    yield return child;
            }

            if (part.Spec is { } spec)
            {
                var key = $"{holder}.Parts.Spec";
                foreach (var nested in SpecExpressions(spec))
                    yield return new SlotChild(key, 0, nested, Classify(key, 0));
            }
        }
    }

    private static IEnumerable<Expression> SpecExpressions(ImmutableArray<FStringPart> spec)
    {
        foreach (var part in spec)
        {
            if (part.Expression != null)
                yield return part.Expression;
            if (part.Spec is { } inner)
            {
                foreach (var nested in SpecExpressions(inner))
                    yield return nested;
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The graph
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A node: its kind, the name its diagnostic carries, where it is declared, its initializer, and
    /// the type declaring it (null at module level).</summary>
    private sealed record ValueNode(NodeKind Kind, string DisplayName, Node Anchor, Expression Initializer, TypeSymbol? Host)
    {
        public bool IsConst => NodeKinds[Kind];
    }

    private SemanticContext _context = null!;

    /// <summary>A module value, by the symbol it declares.</summary>
    private readonly Dictionary<Symbol, ValueNode> _moduleValues = new(ReferenceEqualityComparer.Instance);

    /// <summary>A type's value, by the type and its member name: the checker re-records a field's symbol as it
    /// types the field's declaration, so a resolution made before that names the same member by an earlier
    /// symbol, and the member of the type it resolved is the one identity both share.</summary>
    private readonly Dictionary<(TypeSymbol Owner, string Member), ValueNode> _typeValues = new();
    private readonly List<ValueNode> _ordered = new();

    public void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _moduleValues.Clear();
        _typeValues.Clear();
        _ordered.Clear();

        CollectModule(module);
        if (_ordered.Count == 0)
            return;

        var certain = new Dictionary<ValueNode, HashSet<ValueNode>>(ReferenceEqualityComparer.Instance);
        var lexical = new Dictionary<ValueNode, HashSet<ValueNode>>(ReferenceEqualityComparer.Instance);
        foreach (var node in _ordered)
        {
            certain[node] = new HashSet<ValueNode>(ReferenceEqualityComparer.Instance);
            lexical[node] = new HashSet<ValueNode>(ReferenceEqualityComparer.Instance);
            WalkInitializer(node, node.Initializer, isCertain: true, certain[node], lexical[node]);
        }

        var refused = new Dictionary<ValueNode, bool>(ReferenceEqualityComparer.Instance);
        // A plain store that names a node may run before its initializer: no certain cycle closes through it
        // (Q23). The stores only remove nodes, so a certain cycle among the kept nodes is one among all of
        // them, and the module is walked for its stores only when there is one.
        if (OnCycle(certain, _ => true).Count > 0)
        {
            var stored = new HashSet<ValueNode>(ReferenceEqualityComparer.Instance);
            CollectStores(module.Body, stored);
            foreach (var node in OnCycle(certain, n => !stored.Contains(n)))
                refused[node] = false;
        }
        foreach (var node in OnCycle(lexical, n => n.IsConst))
            refused[node] = true;

        foreach (var node in _ordered.Where(refused.ContainsKey))
        {
            var message = refused[node]
                ? $"Circular constant reference: '{node.DisplayName}' depends on a constant that references it back"
                : $"Circular value reference: '{node.DisplayName}' reads a value whose initializer reads it back on every run, "
                    + "so no value on the cycle can ever be given one. Break the cycle, or read one of the values in a "
                    + "function called after both are initialized";
            context.Diagnostics.AddError(message, node.Anchor.Span, node.Anchor.LineStart, node.Anchor.ColumnStart,
                context.CurrentFilePath, code: DiagnosticCodes.Semantic.CircularConstantReference, phase: CompilerPhase.Validation);
        }
    }

    /// <summary>The nodes on a cycle of <paramref name="edges"/> among the nodes <paramref name="keep"/> admits:
    /// a strongly connected component of two or more, or one node that reads itself.</summary>
    private List<ValueNode> OnCycle(Dictionary<ValueNode, HashSet<ValueNode>> edges, Func<ValueNode, bool> keep)
    {
        var index = new Dictionary<ValueNode, int>(ReferenceEqualityComparer.Instance);
        var low = new Dictionary<ValueNode, int>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<ValueNode>();
        var onStack = new HashSet<ValueNode>(ReferenceEqualityComparer.Instance);
        var result = new List<ValueNode>();
        var counter = 0;

        void Connect(ValueNode v)
        {
            index[v] = low[v] = counter++;
            stack.Push(v);
            onStack.Add(v);
            foreach (var w in edges[v].Where(keep))
            {
                if (!index.ContainsKey(w))
                {
                    Connect(w);
                    low[v] = Math.Min(low[v], low[w]);
                }
                else if (onStack.Contains(w))
                {
                    low[v] = Math.Min(low[v], index[w]);
                }
            }

            if (low[v] != index[v])
                return;
            var component = new List<ValueNode>();
            ValueNode popped;
            do
            {
                popped = stack.Pop();
                onStack.Remove(popped);
                component.Add(popped);
            } while (!ReferenceEquals(popped, v));
            if (component.Count > 1 || edges[v].Contains(v))
                result.AddRange(component);
        }

        foreach (var node in _ordered.Where(keep))
        {
            if (!index.ContainsKey(node))
                Connect(node);
        }

        return result;
    }

    // ------------------------------------------------------------------------------------------- //
    // The nodes
    // ------------------------------------------------------------------------------------------- //

    private void CollectModule(Module module)
    {
        var info = _context.SemanticInfo;
        var properties = new List<PropertyDef>();
        foreach (var statement in module.Body.Select(s => s.UnwrapDecorated()))
        {
            switch (statement)
            {
                case VariableDeclaration { InitialValue: { } initializer } declaration:
                    if ((info.GetDeclarationSymbol(declaration) ?? _context.SymbolTable.Lookup(declaration.Name)) is VariableSymbol symbol)
                        AddModuleValue(symbol, declaration.IsConst ? NodeKind.ModuleConst : NodeKind.ModuleVariable, declaration.Name, declaration, initializer);
                    break;
                case PropertyDef property when property.ExplicitInterface == null:
                    properties.Add(property);
                    break;
                default:
                    if (statement.TryGetNestedDeclaration(out var nested))
                        CollectType(statement, nested, nested.Name);
                    break;
            }
        }

        foreach (var halves in properties.GroupBy(p => p.MemberKey()).Select(g => g.ToList()))
        {
            var name = halves[0].Name;
            if (PropertyInitializer(halves) is { } initializer && _context.SymbolTable.Lookup(name) is VariableSymbol { IsModuleProperty: true } symbol)
                AddModuleValue(symbol, NodeKind.ModuleProperty, name, halves[0], initializer);
        }
    }

    private void CollectType(Statement declaration, NestedDeclaration nested, string path)
    {
        var owner = _context.DeclaredTypes?.SymbolFor(declaration, nested.Name, _context.SymbolTable);
        // The owner's first field of each name, indexed once (the field a by-name scan finds).
        var fields = new Dictionary<string, VariableSymbol>(StringComparer.Ordinal);
        foreach (var f in owner?.Fields ?? Enumerable.Empty<VariableSymbol>())
            fields.TryAdd(f.Name, f);
        var properties = new List<PropertyDef>();
        foreach (var member in nested.Body.Select(m => m.UnwrapDecorated()))
        {
            switch (member)
            {
                case VariableDeclaration { InitialValue: { } initializer } field:
                    if (owner != null && fields.TryGetValue(field.Name, out var symbol) && (field.IsConst || symbol.IsStatic))
                        AddTypeValue(owner, field.Name, field.IsConst ? NodeKind.TypeConst : NodeKind.StaticField, $"{path}.{field.Name}", field, initializer);
                    break;
                case PropertyDef property when property.ExplicitInterface == null:
                    properties.Add(property);
                    break;
                default:
                    if (member.TryGetNestedDeclaration(out var inner))
                        CollectType(member, inner, $"{path}.{inner.Name}");
                    break;
            }
        }

        if (owner == null)
            return;
        // Split halves are one property (its member key), anchored at the first.
        foreach (var halves in properties.GroupBy(p => p.MemberKey()).Select(g => g.ToList()))
        {
            var name = halves[0].Name;
            if (PropertyInitializer(halves) is { } initializer && owner.FindOwnProperty(name) is { IsStatic: true })
                AddTypeValue(owner, name, NodeKind.StaticProperty, $"{path}.{name}", halves[0], initializer);
        }
    }

    /// <summary>The default of a property's auto half, when its storage is a value with an initializer
    /// (<see cref="StaticStorageFacts.ForProperty"/>): split halves are one property, anchored at the first.</summary>
    private static Expression? PropertyInitializer(List<PropertyDef> halves)
        => StaticStorageFacts.ForProperty(halves) == StaticStorage.Guarded
            ? halves.First(h => !h.IsFunctionStyle && h.DefaultValue != null).DefaultValue
            : null;

    private void AddModuleValue(Symbol symbol, NodeKind kind, string displayName, Node anchor, Expression initializer)
    {
        var key = symbol.DeclarationRoot();
        if (!NodeKinds.ContainsKey(kind) || _moduleValues.ContainsKey(key))
            return;
        var node = new ValueNode(kind, displayName, anchor, initializer, null);
        _moduleValues[key] = node;
        _ordered.Add(node);
    }

    private void AddTypeValue(TypeSymbol owner, string member, NodeKind kind, string displayName, Node anchor, Expression initializer)
    {
        if (!NodeKinds.ContainsKey(kind) || _typeValues.ContainsKey((owner, member)))
            return;
        var node = new ValueNode(kind, displayName, anchor, initializer, owner);
        _typeValues[(owner, member)] = node;
        _ordered.Add(node);
    }

    // ------------------------------------------------------------------------------------------- //
    // The reads
    // ------------------------------------------------------------------------------------------- //

    /// <summary>Walks one initializer: every read of a node is a lexical edge, and a certain one when its slot
    /// and every slot enclosing it are evaluated whenever the initializer is.</summary>
    private void WalkInitializer(ValueNode reader, Expression expression, bool isCertain,
        HashSet<ValueNode> certain, HashSet<ValueNode> lexical)
    {
        if (ValueRead(expression, reader) is { } read)
        {
            lexical.Add(read);
            if (isCertain)
                certain.Add(read);
        }

        foreach (var slot in SlotsOf(expression))
        {
            if (slot.Evaluation == SlotEvaluation.Stored)
            {
                WalkStoreTarget(reader, slot.Child, certain, lexical);
                continue;
            }

            var childCertain = isCertain && slot.Evaluation == SlotEvaluation.Always;
            switch (slot.Child)
            {
                case Expression child:
                    WalkInitializer(reader, child, childCertain, certain, lexical);
                    break;
                case Pattern pattern:
                    WalkPattern(reader, pattern, childCertain, certain, lexical);
                    break;
            }
        }
    }

    /// <summary>A store site's target: the value it names is stored, not read. What a target's receiver or
    /// index reads is read, and is never certain.</summary>
    private void WalkStoreTarget(ValueNode reader, Node target, HashSet<ValueNode> certain, HashSet<ValueNode> lexical)
    {
        switch (target)
        {
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    WalkStoreTarget(reader, element, certain, lexical);
                break;
            case ListLiteral list:
                foreach (var element in list.Elements)
                    WalkStoreTarget(reader, element, certain, lexical);
                break;
            case StarExpression star:
                WalkStoreTarget(reader, star.Operand, certain, lexical);
                break;
            case Parenthesized parenthesized:
                WalkStoreTarget(reader, parenthesized.Expression, certain, lexical);
                break;
            case Identifier:
                break;
            case Expression expression:
                foreach (var slot in SlotsOf(expression))
                {
                    if (slot.Child is Expression child)
                        WalkInitializer(reader, child, isCertain: false, certain, lexical);
                }
                break;
        }
    }

    /// <summary>A pattern: the value a top-level value pattern names is read where the pattern is tried; a read
    /// inside a pattern is never certain (D8).</summary>
    private void WalkPattern(ValueNode reader, Pattern pattern, bool isCertain, HashSet<ValueNode> certain, HashSet<ValueNode> lexical)
    {
        if (PatternRead(pattern) is { } read)
        {
            lexical.Add(read);
            if (isCertain)
                certain.Add(read);
        }

        foreach (var child in pattern.GetChildNodes())
        {
            switch (child)
            {
                case Pattern inner:
                    WalkPattern(reader, inner, isCertain: false, certain, lexical);
                    break;
                case Expression expression:
                    WalkInitializer(reader, expression, isCertain: false, certain, lexical);
                    break;
            }
        }
    }

    /// <summary>The node an expression reads, by what the checker bound: an identifier's symbol (or, for an
    /// admitted class-body sibling read, the sibling of the reader's type), a member access's resolution, a
    /// module member's, or a member of the type its receiver denotes.</summary>
    private ValueNode? ValueRead(Expression expression, ValueNode reader)
    {
        var info = _context.SemanticInfo;
        switch (expression)
        {
            case Identifier id:
                // An admitted class-body sibling read names the member of the reader's own type.
                if (info.GetClassBodySiblingRead(id) != null && reader.Host is { } host)
                    return MemberOf(host, id.Name);
                return info.GetIdentifierSymbol(id) is { } bound ? ModuleValueOf(bound) : null;
            case MemberAccess { IsNullConditional: false } access:
                return MemberRead(access);
            default:
                return null;
        }
    }

    private ValueNode? MemberRead(MemberAccess access)
    {
        var info = _context.SemanticInfo;
        if (info.GetMemberAccessResolution(access) is { } resolution)
            return MemberOf(resolution.Owner, resolution.Member.Name);
        if (info.GetModuleMemberResolution(access) is { } moduleMember)
            return ModuleValueOf(moduleMember.Member);
        return DenotedType(access.Object) is { } owner ? MemberOf(owner, access.Member) : null;
    }

    /// <summary>The node a value pattern names: a constant the checker bound, or a member of the type the
    /// pattern's dotted path resolved to.</summary>
    private ValueNode? PatternRead(Pattern pattern)
    {
        var info = _context.SemanticInfo;
        if (info.GetPatternConstantSymbol(pattern) is { } constant)
            return ModuleValueOf(constant);
        if (pattern is MemberAccessPattern access && info.GetPatternMemberAccessResolution(access) is { } resolution
            && resolution.TypeIndex + 2 == access.Parts.Length)
        {
            return MemberOf(resolution.TypeSymbol, access.Parts[^1]);
        }

        return null;
    }

    private ValueNode? ModuleValueOf(Symbol symbol) => _moduleValues.TryGetValue(symbol.DeclarationRoot(), out var node) ? node : null;

    /// <summary>The value <paramref name="member"/> of <paramref name="owner"/>, the type the checker resolved.</summary>
    private ValueNode? MemberOf(TypeSymbol owner, string member) => _typeValues.TryGetValue((owner, member), out var node) ? node : null;

    /// <summary>The type a receiver denotes: a type name the checker bound, a receiver it marked a type reference
    /// or recorded a denoted type for (an alias, a module-qualified name, a constructed generic), or a nested type
    /// reached through its declaring type. Null for a value receiver.</summary>
    private TypeSymbol? DenotedType(Expression receiver)
    {
        var info = _context.SemanticInfo;
        if (receiver is Identifier id && info.GetIdentifierSymbol(id) is TypeSymbol bound)
            return bound;
        if ((info.GetDenotedType(receiver) ?? (info.IsTypeReference(receiver) ? info.GetExpressionType(receiver) : null)) switch
        {
            UserDefinedType { Symbol: { } symbol } => symbol,
            GenericType { GenericDefinition: { } definition } => definition,
            _ => null,
        } is { } denoted)
        {
            return denoted;
        }

        return receiver is MemberAccess { IsNullConditional: false } nested && DenotedType(nested.Object) is { } outer
            ? TypeHierarchyService.FindNestedType(outer, nested.Member)
            : null;
    }

    // ------------------------------------------------------------------------------------------- //
    // The plain stores (Q23)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>Every node a plain store names (D4's plain stores): an assignment's target or a leaf of it, a
    /// <c>for</c>, <c>with</c> or comprehension target, a walrus, and an <c>out</c> operand — on a bare or
    /// qualified name. An augmented assignment reads first, so it is no plain store.</summary>
    private void CollectStores(IEnumerable<Node?> nodes, HashSet<ValueNode> stored)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case null:
                    continue;
                case Assignment { Operator: AssignmentOperator.Assign } assignment:
                    StoreLeaves(assignment.Target, stored);
                    break;
                case ForStatement loop:
                    StoreLeaves(loop.Target, stored);
                    break;
                case ForClause clause:
                    StoreLeaves(clause.Target, stored);
                    break;
                case WithStatement with:
                    foreach (var item in with.Items)
                    {
                        if (item.Target != null)
                            StoreLeaves(item.Target, stored);
                    }
                    break;
                case WalrusExpression walrus when _context.SemanticInfo.GetWalrusSymbol(walrus) is { } bound && ModuleValueOf(bound) is { } walrusNode:
                    stored.Add(walrusNode);
                    break;
                case ModifiedArgument { Modifier: ParameterModifier.Out } operand:
                    StoreLeaves(operand.Argument, stored);
                    break;
            }

            CollectStores(node.GetChildNodes(), stored);
        }
    }

    private void StoreLeaves(Expression target, HashSet<ValueNode> stored)
    {
        switch (target)
        {
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    StoreLeaves(element, stored);
                break;
            case ListLiteral list:
                foreach (var element in list.Elements)
                    StoreLeaves(element, stored);
                break;
            case StarExpression star:
                StoreLeaves(star.Operand, stored);
                break;
            case Parenthesized parenthesized:
                StoreLeaves(parenthesized.Expression, stored);
                break;
            case Identifier id when _context.SemanticInfo.GetIdentifierSymbol(id) is { } bound && ModuleValueOf(bound) is { } node:
                stored.Add(node);
                break;
            case MemberAccess { IsNullConditional: false } access when MemberRead(access) is { } member:
                stored.Add(member);
                break;
        }
    }
}

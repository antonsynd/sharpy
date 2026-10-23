using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Where a statement stands, as far as declarations are concerned: the module body, a type body, or a
/// function body — each either directly or inside a block (<c>if</c>/<c>elif</c>/<c>else</c>,
/// <c>for</c>/<c>while</c> and their <c>else</c>, <c>try</c>/<c>except</c>/<c>else</c>/<c>finally</c>,
/// <c>with</c>, <c>defer</c>, a <c>match</c> case).
/// </summary>
internal enum DeclarationSlot
{
    ModuleBody,
    ModuleBlock,
    TypeBody,
    TypeBodyBlock,
    FunctionBody,
    FunctionBlock,
}

/// <summary>
/// The one owner of the declaration-position verdict (P21e; #2063, rulings R-CS and R-HK): every
/// statement of every statement list in the module is classified, with its
/// <see cref="DeclarationSlot"/>, by ONE switch, <see cref="ClassifyDeclaration"/>.
///
/// <para><b>Arms.</b> A type definition (<c>class</c>, <c>@dataclass class</c>, <c>struct</c>,
/// <c>interface</c>, <c>union</c>, <c>delegate</c>, <c>enum</c>) anywhere but the module body or a type
/// body is SPY0708 — C# has no local types, so the definition would own no symbol. An <c>import</c> /
/// <c>from … import</c> anywhere but the module body is SPY0708 — an import is a module-level fact. A
/// <c>type</c> alias whose bare name is a builtin type name is SPY0212 at every non-declaring position
/// (#2319) — the same <c>BuiltinNameShadowing</c> classifier and text NameResolver's gate applies at the
/// declaring positions, so each position has exactly one owner (plan D2); a fresh-named or
/// backtick-escaped alias falls through (block-scoped, D7). A nested <c>def</c> (in a function body or a
/// block of one) whose name a def of the SAME function already declared in the same block, an enclosing
/// block, or an enclosed block is SPY0204 at the later def (R-HJ, #2314): the emitter spells every local
/// function by its name with no versioning, so C# would see CS0128 (same block) or CS0136 (nested blocks).
/// Sibling blocks — <c>if</c>/<c>elif</c>/<c>else</c> bodies, <c>try</c>/handlers/<c>else</c>/<c>finally</c>,
/// <c>match</c> cases, a loop body and its <c>else</c> — are fresh bindings, and an inner def's body is a
/// function of its own. Methods in a type body and module-level overload sets are NameResolver's. The
/// declaring positions themselves are NameResolver's (it declares the module body and type bodies). Every
/// other statement falls through untouched; in particular a type body's executable statements are
/// <see cref="TypeBodyStatementValidator"/>'s and #2148's, not this validator's (D8).</para>
///
/// <para><b>Why a validator, and why it is safe after the TypeChecker.</b> The pipeline runs after the
/// checker's walk. The checker DECLINES a type definition that owns no symbol without reporting
/// (<c>TypeChecker.SkipUndeclaredTypeDefinition</c>), and an import outside the module body binds
/// nothing, so the refusal here is the only diagnostic on the statement; a use of the unbound import
/// keeps the checker's own SPY0200 (plan D6). Codegen is skipped on any semantic error, so a refused
/// program never reaches the emitter's SPY0510 arms.</para>
///
/// <para><b>Traversal shape (one, stated so a double visit cannot creep in).</b> An explicit walk of
/// statement lists: <see cref="WalkBody"/> classifies each statement of a list at the list's position and
/// then visits the statement with <see cref="_enclosing"/> set to that position. Every override below
/// that owns a statement list walks its lists itself through <see cref="WalkBody"/> and NEVER calls
/// <c>base</c> — so each statement is classified exactly once, by the list that holds it. A
/// <see cref="DecoratedStatement"/> is classified through <c>UnwrapDecorated()</c> by its list and then
/// visited through to its inner statement without being classified again. Expressions, patterns and type
/// annotations cannot hold statements and are not walked (<see cref="DefaultVisit"/> walks child
/// STATEMENTS only).</para>
///
/// <para><b>Statement lists covered</b> (every <c>ImmutableArray&lt;Statement&gt;</c> member of the AST,
/// enumerated @ P21e): <c>Module.Body</c> → ModuleBody; <c>ClassDef</c>/<c>StructDef</c>/
/// <c>InterfaceDef</c>/<c>UnionDef</c><c>.Body</c> → TypeBody; FUNCTION bodies → FunctionBody:
/// <c>FunctionDef.Body</c> (a module function, a method, a <c>@static</c> method, an interface default
/// method, a union method, an <c>async def</c>, a nested def — each is its own FunctionBody),
/// <c>PropertyDef.Body</c> (a <c>property get</c>/<c>set</c>/<c>init</c> accessor), every
/// <c>PropertyObserver.Body</c> (<c>before_set</c>/<c>after_set</c>), and <c>EventDef.Body</c> (a
/// function-style event); BLOCKS → the enclosing scope's block position:
/// <c>IfStatement.ThenBody</c>/<c>ElifClause.Body</c>/<c>ElseBody</c>,
/// <c>WhileStatement</c>/<c>ForStatement</c><c>.Body</c>/<c>ElseBody</c>,
/// <c>TryStatement.Body</c>/<c>ExceptHandler.Body</c>/<c>ElseBody</c>/<c>FinallyBody</c>,
/// <c>WithStatement.Body</c>, <c>DeferStatement.Body</c> and <c>MatchCase.Body</c>. <c>EnumDef</c> and
/// <c>DelegateDef</c> hold no statements. A statement kind not named here (a future body holder) reaches
/// <see cref="DefaultVisit"/>, which classifies the statements among its children as a block of the
/// enclosing position — the walk is total by construction, never silent for a new holder.</para>
/// </summary>
internal sealed class DeclarationPositionValidator : ValidatingAstWalker
{
    public override string Name => "DeclarationPositionValidator";
    public override int Order => 53; // After CircularImportUsageValidator (52), before NamingConventionValidator (55)

    /// <summary>The position of the list holding the statement being visited.</summary>
    private DeclarationSlot _enclosing = DeclarationSlot.ModuleBody;

    /// <summary>
    /// The block tree of the function being walked, innermost list on top: a <see cref="DefBlock"/> for a
    /// function body (a root — no parent) or a block of one (parent = the enclosing list), and null for
    /// any other list (module, type body, and their blocks), so a function's blocks never see another
    /// function's defs.
    /// </summary>
    private readonly Stack<DefBlock?> _defBlocks = new();

    /// <summary>
    /// One statement list of a function, for the nested-def arm. <see cref="Declared"/> holds the defs
    /// written directly in it; <see cref="DeclaredBelow"/> the defs of its already-walked descendant
    /// blocks (merged in when each descendant ends), which is what makes the inner-first order visible.
    /// A sibling's defs reach only the common parent's <see cref="DeclaredBelow"/>, never the sibling.
    /// </summary>
    private sealed class DefBlock
    {
        public DefBlock(DefBlock? parent) => Parent = parent;

        public DefBlock? Parent { get; }
        public HashSet<string> Declared { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DeclaredBelow { get; } = new(StringComparer.Ordinal);

        /// <summary>True when a def of <paramref name="name"/> already stands in this block, an enclosing
        /// block, or an already-walked enclosed block of the same function.</summary>
        public bool Conflicts(string name)
        {
            if (Declared.Contains(name) || DeclaredBelow.Contains(name))
                return true;
            for (var ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor.Declared.Contains(name))
                    return true;
            }

            return false;
        }
    }

    public override void VisitModule(Module node) => WalkBody(node.Body, DeclarationSlot.ModuleBody);

    // ── Type bodies ──────────────────────────────────────────────────────────────────────────────────

    public override void VisitClassDef(ClassDef node) => WalkBody(node.Body, DeclarationSlot.TypeBody);

    public override void VisitStructDef(StructDef node) => WalkBody(node.Body, DeclarationSlot.TypeBody);

    public override void VisitInterfaceDef(InterfaceDef node) => WalkBody(node.Body, DeclarationSlot.TypeBody);

    public override void VisitUnionDef(UnionDef node) => WalkBody(node.Body, DeclarationSlot.TypeBody);

    // ── Function bodies ──────────────────────────────────────────────────────────────────────────────

    public override void VisitFunctionDef(FunctionDef node) => WalkBody(node.Body, DeclarationSlot.FunctionBody);

    public override void VisitPropertyDef(PropertyDef node)
    {
        WalkBody(node.Body, DeclarationSlot.FunctionBody);
        foreach (var observer in node.Observers)
            WalkBody(observer.Body, DeclarationSlot.FunctionBody);
    }

    public override void VisitEventDef(EventDef node) => WalkBody(node.Body, DeclarationSlot.FunctionBody);

    // ── Blocks ───────────────────────────────────────────────────────────────────────────────────────

    public override void VisitIfStatement(IfStatement node)
    {
        var block = BlockOf(_enclosing);
        WalkBody(node.ThenBody, block);
        foreach (var elif in node.ElifClauses)
            WalkBody(elif.Body, block);
        WalkBody(node.ElseBody, block);
    }

    public override void VisitWhileStatement(WhileStatement node)
    {
        var block = BlockOf(_enclosing);
        WalkBody(node.Body, block);
        WalkBody(node.ElseBody, block);
    }

    public override void VisitForStatement(ForStatement node)
    {
        var block = BlockOf(_enclosing);
        WalkBody(node.Body, block);
        WalkBody(node.ElseBody, block);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        var block = BlockOf(_enclosing);
        WalkBody(node.Body, block);
        foreach (var handler in node.Handlers)
            WalkBody(handler.Body, block);
        WalkBody(node.ElseBody, block);
        WalkBody(node.FinallyBody, block);
    }

    public override void VisitWithStatement(WithStatement node) => WalkBody(node.Body, BlockOf(_enclosing));

    public override void VisitDeferStatement(DeferStatement node) => WalkBody(node.Body, BlockOf(_enclosing));

    public override void VisitMatchStatement(MatchStatement node)
    {
        var block = BlockOf(_enclosing);
        foreach (var matchCase in node.Cases)
            WalkBody(matchCase.Body, block);
    }

    // ── Everything else ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The wrapper was classified (unwrapped) by its list; visit through to the inner statement.</summary>
    public override void VisitDecoratedStatement(DecoratedStatement node) => Visit(node.Statement);

    /// <summary>
    /// Every node with no override above — a leaf statement, and (through the base category methods) any
    /// expression, pattern or type annotation. Only the statements among its CHILDREN are walked, never
    /// its expressions: a lambda's body is an expression, so no expression holds a statement. Today no
    /// statement reaching here holds one either; should a new kind ever hold one (a future body holder, or
    /// a kind missing from <see cref="AstVisitor.Visit"/>'s switch), its statements are classified as a
    /// block of the enclosing position rather than skipped.
    /// </summary>
    public override void DefaultVisit(Node node)
    {
        var children = node.GetChildNodes().OfType<Statement>().ToImmutableArray();
        if (children.Length > 0)
            WalkBody(children, BlockOf(_enclosing));
    }

    private void WalkBody(ImmutableArray<Statement> body, DeclarationSlot position)
    {
        var saved = _enclosing;
        var block = position switch
        {
            DeclarationSlot.FunctionBody => new DefBlock(parent: null),
            DeclarationSlot.FunctionBlock => new DefBlock(_defBlocks.Count > 0 ? _defBlocks.Peek() : null),
            _ => null,
        };
        _defBlocks.Push(block);
        foreach (var statement in body)
        {
            ClassifyDeclaration(statement, position);
            _enclosing = position;
            Visit(statement);
        }

        _defBlocks.Pop();
        if (block?.Parent is { } parent)
        {
            parent.DeclaredBelow.UnionWith(block.Declared);
            parent.DeclaredBelow.UnionWith(block.DeclaredBelow);
        }

        _enclosing = saved;
    }

    /// <summary>The position of a block opened at <paramref name="position"/>.</summary>
    private static DeclarationSlot BlockOf(DeclarationSlot position) => position switch
    {
        DeclarationSlot.ModuleBody or DeclarationSlot.ModuleBlock => DeclarationSlot.ModuleBlock,
        DeclarationSlot.TypeBody or DeclarationSlot.TypeBodyBlock => DeclarationSlot.TypeBodyBlock,
        _ => DeclarationSlot.FunctionBlock,
    };

    /// <summary>
    /// The one decision for a declaration statement at <paramref name="position"/>. A statement-scoped
    /// <c>@suppress</c> wrapper is transparent (the inner statement is classified); a type decorator such
    /// as <c>@dataclass</c> is part of the definition, so <c>@dataclass class</c> is the class arm.
    /// </summary>
    private void ClassifyDeclaration(Statement statement, DeclarationSlot position)
    {
        var stmt = statement.UnwrapDecorated();
        switch (stmt)
        {
            case ClassDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "class");
                break;
            case StructDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "struct");
                break;
            case InterfaceDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "interface");
                break;
            case UnionDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "union");
                break;
            case DelegateDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "delegate");
                break;
            case EnumDef when !IsTypeDeclaring(position):
                RefuseTypeDefinition(stmt, "enum");
                break;

            case ImportStatement or FromImportStatement when position != DeclarationSlot.ModuleBody:
                AddError(
                    ImportMessage(position),
                    line: stmt.LineStart,
                    column: stmt.ColumnStart,
                    code: DiagnosticCodes.ValidationOverflow.DeclarationOutsideDeclaringPosition,
                    span: stmt.Span);
                break;

            case TypeAlias alias when !IsTypeDeclaring(position)
                && BuiltinNameShadowing.Classify(
                    Context.Builtins, alias.Name, alias.IsNameBacktickEscaped, isTypeDeclaration: true)
                == BuiltinShadowVerdict.Refused:
                // No span on purpose, as NameResolver's gate: the caret lands on the name token.
                AddError(
                    BuiltinNameShadowing.RefusalMessage(alias.Name),
                    line: alias.NameLineStart,
                    column: alias.NameColumnStart,
                    code: BuiltinNameShadowing.RefusalCode);
                break;

            case FunctionDef def when position is DeclarationSlot.FunctionBody or DeclarationSlot.FunctionBlock:
                DeclareNestedDef(def);
                break;

            default:
                // A declaration at a declaring position (NameResolver's), and every non-declaration
                // statement, falls through untouched.
                break;
        }
    }

    /// <summary>
    /// Records a nested def in its block, refusing it with SPY0204 when the same function already declared
    /// its name in this block, an enclosing block or an enclosed one (R-HJ). The caret is on the name
    /// token (no span: a span wins over line/column at every consumer, and the def's span starts at
    /// <c>def</c>). The refused def is not recorded, so a third def of the name is refused once, too.
    /// </summary>
    private void DeclareNestedDef(FunctionDef def)
    {
        if (_defBlocks.Count == 0 || _defBlocks.Peek() is not { } block)
            return;

        if (block.Conflicts(def.Name))
        {
            AddError(
                $"function '{def.Name}' is already defined in this scope",
                line: def.NameLineStart,
                column: def.NameColumnStart,
                code: DiagnosticCodes.Semantic.DuplicateDefinition);
            return;
        }

        block.Declared.Add(def.Name);
    }

    /// <summary>The module body and a type body declare types; nothing else does (C# has no local types).</summary>
    private static bool IsTypeDeclaring(DeclarationSlot position)
        => position is DeclarationSlot.ModuleBody or DeclarationSlot.TypeBody;

    private void RefuseTypeDefinition(Statement definition, string kind)
        => AddError(
            TypeDefinitionMessage(kind),
            line: definition.LineStart,
            column: definition.ColumnStart,
            code: DiagnosticCodes.ValidationOverflow.DeclarationOutsideDeclaringPosition,
            span: definition.Span);

    /// <summary>The SPY0708 text for a type definition outside a declaring position (R-CS).</summary>
    internal static string TypeDefinitionMessage(string kind)
        => $"{(kind is "interface" or "enum" ? "an" : "a")} {kind} cannot be declared inside a function or a block; "
           + "declare it at module or class level";

    /// <summary>The SPY0708 text for an import outside the module body (R-CS).</summary>
    internal static string ImportMessage(DeclarationSlot position)
    {
        var where = position switch
        {
            DeclarationSlot.FunctionBody => "function",
            DeclarationSlot.TypeBody => "class body",
            _ => "block",
        };
        return $"an import is not allowed inside a {where}; import at module level";
    }
}

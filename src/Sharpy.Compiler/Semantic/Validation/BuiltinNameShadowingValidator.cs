using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns when a binding in VALUE position spells the name of a builtin type or function
/// (SPY0483).
/// </summary>
/// <remarks>
/// <para>The refusal half of this rule (SPY0212, a type declaration taking a builtin TYPE name)
/// lives in <c>NameResolver</c>, because refusing has to happen while the symbol is being bound.
/// The warning half lives here for two reasons. It is a self-contained AST analysis needing no
/// in-progress inference, which is the pipeline's whole remit; and <c>ProjectCompiler</c> lifts only
/// ERRORS out of the name-resolution pass, so a warning raised there is discarded without a trace —
/// measured, not assumed.</para>
///
/// <para><strong>What counts as value position.</strong> Anything that binds a name usable bare as
/// a value: module and local variables, constants, parameters, for-targets, and function
/// declarations. A CLASS MEMBER does not — a field or method is reached through <c>self.</c>, so it
/// never competes with a builtin for a bare spelling, and warning on <c>class Order: def id(self)</c>
/// would be noise about a collision that cannot happen. A method's PARAMETERS are value position,
/// which is why <c>def __init__(self, id: int)</c> is warned: inside that body, <c>id</c> is the
/// parameter.</para>
///
/// <para>A TYPE declaration spelling a builtin FUNCTION name (<c>class len:</c>) is warned rather
/// than refused: it makes no annotation ambiguous, since <c>len</c> was never a type, but the class
/// is unconstructible by its bare spelling.</para>
/// </remarks>
internal sealed class BuiltinNameShadowingValidator : ValidatingAstWalker
{
    public override string Name => "BuiltinNameShadowing";

    // After NamingConventionValidator (55) and TransitionWarningValidator (56): all three are
    // advisory naming diagnostics and this is the most specific of them, so it reads last.
    public override int Order => 57;

    /// <summary>
    /// True while visiting the members of a type declaration directly — reset for the duration of a
    /// function body, whose declarations are locals again.
    /// </summary>
    private bool _inTypeBody;

    /// <summary>Assignment targets already reported in the body currently being visited.</summary>
    private HashSet<string> _assignedNamesWarned = new(StringComparer.Ordinal);

    public override void VisitClassDef(ClassDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitClassDef(node));

    public override void VisitStructDef(StructDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitStructDef(node));

    public override void VisitInterfaceDef(InterfaceDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitInterfaceDef(node));

    public override void VisitEnumDef(EnumDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitEnumDef(node));

    public override void VisitUnionDef(UnionDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitUnionDef(node));

    public override void VisitDelegateDef(DelegateDef node) => VisitTypeDeclaration(node, node.Name,
        node.IsNameBacktickEscaped, () => base.VisitDelegateDef(node));

    public override void VisitFunctionDef(FunctionDef node)
    {
        // A method name is reached through `self.`, so it shadows nothing. A free function's name
        // does, and so do the parameters of both.
        if (!_inTypeBody)
        {
            Warn(node.Name, node.IsNameBacktickEscaped, node.NameLineStart, node.NameColumnStart,
                node.Span, isTypeDeclaration: false);
        }

        foreach (var parameter in node.Parameters)
        {
            Warn(parameter.Name, parameter.IsNameBacktickEscaped,
                parameter.LineStart, parameter.ColumnStart, parameter.Span, isTypeDeclaration: false);
        }

        var wasInTypeBody = _inTypeBody;
        var outerAssignedNames = _assignedNamesWarned;
        _inTypeBody = false;
        _assignedNamesWarned = new HashSet<string>(StringComparer.Ordinal);
        base.VisitFunctionDef(node);
        _assignedNamesWarned = outerAssignedNames;
        _inTypeBody = wasInTypeBody;
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        // A field is `self.id`; only a module-level or local variable binds the bare spelling.
        if (!_inTypeBody)
        {
            Warn(node.Name, node.IsNameBacktickEscaped, node.NameLineStart, node.NameColumnStart,
                node.Span, isTypeDeclaration: false);
        }

        base.VisitVariableDeclaration(node);
    }

    /// <summary>
    /// A bare <c>len = 5</c> binds a local, and every later <c>len = 6</c> rebinds the same one.
    /// Reported once per name per enclosing body, so a loop that reassigns a shadowing name does not
    /// produce a wall of identical warnings about a single binding.
    /// </summary>
    public override void VisitAssignment(Assignment node)
    {
        if (node.Target is Identifier target && _assignedNamesWarned.Add(target.Name))
        {
            Warn(target.Name, target.IsNameBacktickEscaped, target.LineStart, target.ColumnStart,
                target.Span, isTypeDeclaration: false);
        }

        base.VisitAssignment(node);
    }

    public override void VisitForStatement(ForStatement node)
    {
        if (node.Target is Identifier target)
        {
            Warn(target.Name, target.IsNameBacktickEscaped, target.LineStart, target.ColumnStart,
                target.Span, isTypeDeclaration: false);
        }

        base.VisitForStatement(node);
    }

    public override void VisitWalrusExpression(WalrusExpression node)
    {
        Warn(node.Target, node.IsNameBacktickEscaped, node.LineStart, node.ColumnStart,
            node.Span, isTypeDeclaration: false);

        base.VisitWalrusExpression(node);
    }

    public override void VisitForClause(ForClause node)
    {
        if (node.Target is Identifier target)
        {
            Warn(target.Name, target.IsNameBacktickEscaped, target.LineStart, target.ColumnStart,
                target.Span, isTypeDeclaration: false);
        }

        base.VisitForClause(node);
    }

    public override void VisitWithStatement(WithStatement node)
    {
        foreach (var item in node.Items)
        {
            if (item.Target is Identifier id)
            {
                Warn(id.Name, id.IsNameBacktickEscaped, id.LineStart, id.ColumnStart,
                    item.Span, isTypeDeclaration: false);
            }
        }

        base.VisitWithStatement(node);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        foreach (var handler in node.Handlers)
        {
            if (handler.Name != null)
            {
                Warn(handler.Name, handler.IsNameBacktickEscaped, handler.NameLineStart,
                    handler.NameColumnStart, handler.Span, isTypeDeclaration: false);
            }
        }

        base.VisitTryStatement(node);
    }

    private void VisitTypeDeclaration(Statement node, string name, bool isNameBacktickEscaped,
        Action visitBase)
    {
        // Only the builtin-FUNCTION case reaches a warning here; a builtin TYPE name was already
        // refused by SPY0212 during name resolution, and Classify returns Refused for it, which
        // Warn ignores. One classifier, so the two can never both fire.
        Warn(name, isNameBacktickEscaped, node.LineStart, node.ColumnStart, node.Span,
            isTypeDeclaration: true);

        var wasInTypeBody = _inTypeBody;
        _inTypeBody = true;
        visitBase();
        _inTypeBody = wasInTypeBody;
    }

    private void Warn(string name, bool isNameBacktickEscaped, int line, int column,
        Text.TextSpan? span, bool isTypeDeclaration)
    {
        if (BuiltinNameShadowing.Classify(Context.Builtins, name, isNameBacktickEscaped,
                isTypeDeclaration) != BuiltinShadowVerdict.Warned)
            return;

        AddWarning(BuiltinNameShadowing.WarningMessage(name), line, column,
            code: BuiltinNameShadowing.WarningCode, span: span);
    }
}

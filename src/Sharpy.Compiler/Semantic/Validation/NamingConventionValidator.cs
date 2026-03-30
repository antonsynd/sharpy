using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about identifiers with consecutive underscores (e.g., <c>foo__bar</c>),
/// which may cause name mangling collisions or be passed through as unrecognized forms.
/// Exempts dunder names (<c>__init__</c>) and backtick-escaped literals (<c>`foo__bar`</c>).
/// </summary>
internal sealed class NamingConventionValidator : ValidatingAstWalker
{
    public override string Name => "NamingConvention";
    public override int Order => 55; // After ModuleLevelValidator (50), before DecoratorValidator (60)

    public override void VisitFunctionDef(FunctionDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        ValidateParameters(node.Parameters);
        base.VisitFunctionDef(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitInterfaceDef(node);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);

        foreach (var member in node.Members)
        {
            CheckName(member.Name, member.LineStart, member.ColumnStart, member.Span);
        }

        base.VisitEnumDef(node);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);

        foreach (var caseDef in node.Cases)
        {
            CheckName(caseDef.Name, caseDef.LineStart, caseDef.ColumnStart, caseDef.Span);

            foreach (var field in caseDef.Fields)
            {
                if (!string.IsNullOrEmpty(field.Name))
                {
                    CheckName(field.Name, field.LineStart, field.ColumnStart, field.Span);
                }
            }
        }

        base.VisitUnionDef(node);
    }

    public override void VisitDelegateDef(DelegateDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        ValidateParameters(node.Parameters);
        base.VisitDelegateDef(node);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitVariableDeclaration(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitEventDef(node);
    }

    public override void VisitForStatement(ForStatement node)
    {
        CheckForTarget(node.Target);
        base.VisitForStatement(node);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        foreach (var handler in node.Handlers)
        {
            if (handler.Name != null)
                CheckName(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span);
        }

        base.VisitTryStatement(node);
    }

    public override void VisitWithStatement(WithStatement node)
    {
        foreach (var item in node.Items)
        {
            if (item.Name != null)
                CheckName(item.Name, item.LineStart, item.ColumnStart, item.Span);
        }

        base.VisitWithStatement(node);
    }

    private void ValidateParameters(System.Collections.Immutable.ImmutableArray<Parameter> parameters)
    {
        foreach (var param in parameters)
        {
            CheckName(param.Name, param.LineStart, param.ColumnStart, param.Span, isBacktickEscaped: param.IsNameBacktickEscaped);
        }
    }

    private void CheckForTarget(Expression target)
    {
        switch (target)
        {
            case Identifier id:
                CheckName(id.Name, id.LineStart, id.ColumnStart, id.Span, isBacktickEscaped: id.IsNameBacktickEscaped);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CheckForTarget(element);
                break;
        }
    }

    /// <summary>
    /// Collapses consecutive underscores in a name to single underscores.
    /// Preserves leading/trailing underscore patterns (e.g., _foo__bar -> _foo_bar).
    /// </summary>
    private static string CollapseConsecutiveUnderscores(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        var lastWasUnderscore = false;
        foreach (var ch in name)
        {
            if (ch == '_')
            {
                if (!lastWasUnderscore)
                    sb.Append(ch);
                lastWasUnderscore = true;
            }
            else
            {
                sb.Append(ch);
                lastWasUnderscore = false;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Checks a single name for consecutive underscores and emits SPY0453 if found.
    /// Skips dunder names and backtick-escaped literals.
    /// </summary>
    private void CheckName(string name, int line, int column, TextSpan? span, bool isBacktickEscaped = false)
    {
        if (string.IsNullOrEmpty(name))
            return;

        // Skip backtick-escaped literals — user explicitly opted into the name
        if (isBacktickEscaped)
            return;

        // Skip dunder names (__init__, __str__, etc.)
        if (name.StartsWith("__") && name.EndsWith("__") && name.Length > 4)
            return;

        // Strip _ or __ prefix for the body check
        var body = name;
        if (body.StartsWith("__"))
            body = body[2..];
        else if (body.StartsWith("_"))
            body = body[1..];

        // Strip trailing underscores
        body = body.TrimEnd('_');

        if (string.IsNullOrEmpty(body))
            return;

        if (NameFormDetector.HasConsecutiveUnderscores(body))
        {
            var suggestedName = CollapseConsecutiveUnderscores(name);
            var data = new Dictionary<string, string> { { "suggestedName", suggestedName } };
            AddWarning(
                $"Identifier '{name}' contains consecutive underscores, which may cause name mangling collisions. Use backtick escaping or rename.",
                line, column,
                code: DiagnosticCodes.Validation.NamingConventionWarning,
                span: span,
                data: data);
        }
    }
}

using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about identifiers that violate Sharpy naming conventions (SPY0453).
/// Two independent checks run per identifier:
/// <list type="number">
/// <item>Consecutive underscores (e.g., <c>foo__bar</c>), which may cause name
/// mangling collisions or be passed through as unrecognized forms.</item>
/// <item>Per-identifier-kind casing convention from the language spec
/// (e.g., methods should be <c>snake_case</c>, classes <c>PascalCase</c>,
/// enum values <c>SCREAMING_SNAKE_CASE</c>).</item>
/// </list>
/// A single identifier may receive both warnings (e.g., <c>foo__Bar</c>).
/// Exempts dunder names (<c>__init__</c>), backtick-escaped literals
/// (<c>`foo__bar`</c>), and the <c>self</c>/<c>cls</c> parameters.
/// </summary>
internal sealed class NamingConventionValidator : ValidatingAstWalker
{
    public override string Name => "NamingConvention";
    public override int Order => 55; // After ModuleLevelValidator (50), before DecoratorValidator (60)

    /// <summary>
    /// Classifies an identifier so the validator can pick the expected casing convention.
    /// </summary>
    private enum IdentifierCategory
    {
        Method,
        Parameter,
        Variable,
        Constant,
        Type,
        EnumValue,
        Property,
        Event,
        UnionCase,
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Method, isBacktickEscaped: node.IsNameBacktickEscaped);
        ValidateParameters(node.Parameters);
        base.VisitFunctionDef(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitInterfaceDef(node);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);

        foreach (var member in node.Members)
        {
            CheckName(member.Name, member.LineStart, member.ColumnStart, member.Span);
            CheckConvention(member.Name, member.LineStart, member.ColumnStart, member.Span, IdentifierCategory.EnumValue);
        }

        base.VisitEnumDef(node);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);

        foreach (var caseDef in node.Cases)
        {
            CheckName(caseDef.Name, caseDef.LineStart, caseDef.ColumnStart, caseDef.Span);
            CheckConvention(caseDef.Name, caseDef.LineStart, caseDef.ColumnStart, caseDef.Span, IdentifierCategory.UnionCase);

            foreach (var field in caseDef.Fields)
            {
                if (!string.IsNullOrEmpty(field.Name))
                {
                    CheckName(field.Name, field.LineStart, field.ColumnStart, field.Span);
                    CheckConvention(field.Name, field.LineStart, field.ColumnStart, field.Span, IdentifierCategory.Variable);
                }
            }
        }

        base.VisitUnionDef(node);
    }

    public override void VisitDelegateDef(DelegateDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Type, isBacktickEscaped: node.IsNameBacktickEscaped);
        ValidateParameters(node.Parameters);
        base.VisitDelegateDef(node);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        // `const` declarations are constants (SCREAMING_SNAKE_CASE); everything else
        // (locals, module-level variables, class/struct fields) is snake_case.
        var category = node.IsConst ? IdentifierCategory.Constant : IdentifierCategory.Variable;
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, category, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitVariableDeclaration(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Property, isBacktickEscaped: node.IsNameBacktickEscaped);
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        CheckName(node.Name, node.LineStart, node.ColumnStart, node.Span, isBacktickEscaped: node.IsNameBacktickEscaped);
        CheckConvention(node.Name, node.LineStart, node.ColumnStart, node.Span, IdentifierCategory.Event, isBacktickEscaped: node.IsNameBacktickEscaped);
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
            {
                CheckName(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span);
                CheckConvention(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span, IdentifierCategory.Variable);
            }
        }

        base.VisitTryStatement(node);
    }

    public override void VisitWithStatement(WithStatement node)
    {
        foreach (var item in node.Items)
        {
            if (item.Name != null)
            {
                CheckName(item.Name, item.LineStart, item.ColumnStart, item.Span);
                CheckConvention(item.Name, item.LineStart, item.ColumnStart, item.Span, IdentifierCategory.Variable);
            }
        }

        base.VisitWithStatement(node);
    }

    private void ValidateParameters(System.Collections.Immutable.ImmutableArray<Parameter> parameters)
    {
        foreach (var param in parameters)
        {
            CheckName(param.Name, param.LineStart, param.ColumnStart, param.Span, isBacktickEscaped: param.IsNameBacktickEscaped);
            CheckConvention(param.Name, param.LineStart, param.ColumnStart, param.Span, IdentifierCategory.Parameter, isBacktickEscaped: param.IsNameBacktickEscaped);
        }
    }

    private void CheckForTarget(Expression target)
    {
        switch (target)
        {
            case Identifier id:
                CheckName(id.Name, id.LineStart, id.ColumnStart, id.Span, isBacktickEscaped: id.IsNameBacktickEscaped);
                CheckConvention(id.Name, id.LineStart, id.ColumnStart, id.Span, IdentifierCategory.Variable, isBacktickEscaped: id.IsNameBacktickEscaped);
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

    /// <summary>
    /// Checks that a name follows the expected casing convention for its identifier kind
    /// and emits SPY0453 with a suggested rename if it does not. This is independent of the
    /// consecutive-underscore check — a name may trigger both.
    /// Skips dunder names, backtick-escaped literals, and the <c>self</c>/<c>cls</c> parameters.
    /// </summary>
    private void CheckConvention(string name, int line, int column, TextSpan? span,
        IdentifierCategory category, bool isBacktickEscaped = false)
    {
        if (string.IsNullOrEmpty(name))
            return;

        // Skip backtick-escaped literals — user explicitly opted into the name
        if (isBacktickEscaped)
            return;

        // Skip dunder names (__init__, __str__, etc.)
        if (name.StartsWith("__") && name.EndsWith("__") && name.Length > 4)
            return;

        // self/cls are conventional Python receiver names — always exempt
        if (category == IdentifierCategory.Parameter && (name == "self" || name == "cls"))
            return;

        // Strip leading _ or __ prefix (privacy markers) before detecting the form,
        // since `_my_var` is valid snake_case once the marker is removed.
        var body = name;
        if (body.StartsWith("__"))
            body = body[2..];
        else if (body.StartsWith("_"))
            body = body[1..];

        // Strip trailing underscores
        body = body.TrimEnd('_');

        if (string.IsNullOrEmpty(body))
            return;

        var rule = GetConventionRule(category);
        var form = NameFormDetector.Detect(body);

        if (Array.IndexOf(rule.AllowedForms, form) >= 0)
            return;

        var suggestedName = rule.Suggest(name);
        var data = new Dictionary<string, string> { { "suggestedName", suggestedName } };
        AddWarning(
            $"Identifier '{name}' should use {rule.Convention} for {rule.KindLabel}. Consider renaming to '{suggestedName}'.",
            line, column,
            code: DiagnosticCodes.Validation.NamingConventionWarning,
            span: span,
            data: data);
    }

    private readonly record struct ConventionRule(
        NameForm[] AllowedForms,
        string Convention,
        string KindLabel,
        Func<string, string> Suggest);

    private static readonly NameForm[] _snakeForms = { NameForm.SnakeCase, NameForm.SingleWordLower };
    private static readonly NameForm[] _pascalForms = { NameForm.PascalCase, NameForm.SingleWordUpper };
    private static readonly NameForm[] _screamingForms = { NameForm.ScreamingSnakeCase, NameForm.SingleWordUpper };

    private static ConventionRule GetConventionRule(IdentifierCategory category) => category switch
    {
        IdentifierCategory.Method =>
            new ConventionRule(_snakeForms, "snake_case", "methods and functions", ReverseNameMangler.ToSnakeCase),
        IdentifierCategory.Parameter =>
            new ConventionRule(_snakeForms, "snake_case", "parameters", ReverseNameMangler.ToSnakeCase),
        IdentifierCategory.Variable =>
            new ConventionRule(_snakeForms, "snake_case", "variables", ReverseNameMangler.ToSnakeCase),
        IdentifierCategory.Property =>
            new ConventionRule(_snakeForms, "snake_case", "properties", ReverseNameMangler.ToSnakeCase),
        IdentifierCategory.Event =>
            new ConventionRule(_snakeForms, "snake_case", "events", ReverseNameMangler.ToSnakeCase),
        IdentifierCategory.Constant =>
            new ConventionRule(_screamingForms, "SCREAMING_SNAKE_CASE", "constants", ReverseNameMangler.ToScreamingSnakeCase),
        IdentifierCategory.EnumValue =>
            new ConventionRule(_screamingForms, "SCREAMING_SNAKE_CASE", "enum values", ReverseNameMangler.ToScreamingSnakeCase),
        IdentifierCategory.Type =>
            new ConventionRule(_pascalForms, "PascalCase", "types", NameMangler.ToPascalCase),
        IdentifierCategory.UnionCase =>
            new ConventionRule(_pascalForms, "PascalCase", "tagged union cases", NameMangler.ToPascalCase),
        _ => new ConventionRule(_snakeForms, "snake_case", "identifiers", ReverseNameMangler.ToSnakeCase),
    };
}

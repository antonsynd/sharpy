using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates default parameter values in function definitions:
/// - Early-bound defaults must be compile-time constant expressions
/// - Mutable defaults ([], {}, set()) are not allowed in early-bound position
/// - None is only allowed for nullable parameter types
/// - Late-bound defaults (=>) must not reference their own parameter (self-reference)
/// - Late-bound defaults (=>) must not reference parameters declared after them (forward-reference)
///
/// This is the pipeline-compatible version of DefaultParameterValidator.
/// </summary>
internal class DefaultParameterValidator : ValidatingAstWalker
{
    public override string Name => "DefaultParameterValidator";
    public override int Order => 250; // Before type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting default parameter validation");
        base.Validate(module, context);
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunctionDefaults(node);
        base.VisitFunctionDef(node);
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        ValidateLambdaDefaults(node);
        base.VisitLambdaExpression(node);
    }

    /// <summary>
    /// Validates all default parameter values in a function definition.
    /// </summary>
    private void ValidateFunctionDefaults(FunctionDef functionDef)
    {
        // Build set of all parameter names for forward-reference detection
        var allParamNames = new HashSet<string>(
            functionDef.Parameters.Select(p => p.Name),
            StringComparer.Ordinal);

        foreach (var param in functionDef.Parameters)
        {
            if (param.DefaultValue == null)
                continue;

            if (param.IsLateBound)
            {
                ValidateLateBoundDefault(param, functionDef);
            }
            else
            {
                ValidateDefaultValue(param, functionDef.Name, AdmissionTable.ParameterDefault);
            }
        }
    }

    private void ValidateLambdaDefaults(LambdaExpression lambda)
    {
        foreach (var param in lambda.Parameters)
        {
            if (param.DefaultValue == null || param.IsLateBound)
                continue;

            ValidateDefaultValue(param, "lambda", AdmissionTable.LambdaParameterDefault);
        }
    }

    /// <summary>
    /// Validates a late-bound default expression for self-reference and forward-reference.
    /// </summary>
    private void ValidateLateBoundDefault(Parameter param, FunctionDef functionDef)
    {
        var referencedNames = CollectIdentifierNames(param.DefaultValue!);

        // Self-reference: the default expression references the parameter itself
        if (referencedNames.Contains(param.Name))
        {
            AddError(
                $"Late-bound default for parameter '{param.Name}' in function '{functionDef.Name}' cannot reference itself.",
                param.LineStart,
                param.ColumnStart,
                code: DiagnosticCodes.Validation.LateBoundSelfReference,
                span: param.Span);
            return;
        }

        // Forward-reference: the default expression references a parameter declared after this one
        // Collect names of parameters that come AFTER this parameter
        bool foundSelf = false;
        foreach (var other in functionDef.Parameters)
        {
            if (!foundSelf)
            {
                if (other.Name == param.Name)
                    foundSelf = true;
                continue;
            }
            // other comes after param
            if (referencedNames.Contains(other.Name))
            {
                AddError(
                    $"Late-bound default for parameter '{param.Name}' in function '{functionDef.Name}' cannot reference later parameter '{other.Name}'.",
                    param.LineStart,
                    param.ColumnStart,
                    code: DiagnosticCodes.Validation.LateBoundForwardReference,
                    span: param.Span);
                return;
            }
        }
    }

    /// <summary>
    /// Collects all identifier names referenced anywhere in an expression (recursive).
    /// </summary>
    private static HashSet<string> CollectIdentifierNames(Expression expr)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        CollectIdentifierNamesInto(expr, names);
        return names;
    }

    private static void CollectIdentifierNamesInto(Expression expr, HashSet<string> names)
    {
        switch (expr)
        {
            case Identifier id:
                names.Add(id.Name);
                break;
            case BinaryOp bin:
                CollectIdentifierNamesInto(bin.Left, names);
                CollectIdentifierNamesInto(bin.Right, names);
                break;
            case UnaryOp unary:
                CollectIdentifierNamesInto(unary.Operand, names);
                break;
            case Parenthesized paren:
                CollectIdentifierNamesInto(paren.Expression, names);
                break;
            case ConditionalExpression cond:
                CollectIdentifierNamesInto(cond.Test, names);
                CollectIdentifierNamesInto(cond.ThenValue, names);
                CollectIdentifierNamesInto(cond.ElseValue, names);
                break;
            case FunctionCall call:
                CollectIdentifierNamesInto(call.Function, names);
                foreach (var arg in call.Arguments)
                    CollectIdentifierNamesInto(arg, names);
                foreach (var kwarg in call.KeywordArguments)
                    CollectIdentifierNamesInto(kwarg.Value, names);
                break;
            case MemberAccess memberAccess:
                CollectIdentifierNamesInto(memberAccess.Object, names);
                break;
            case IndexAccess indexAccess:
                CollectIdentifierNamesInto(indexAccess.Object, names);
                CollectIdentifierNamesInto(indexAccess.Index, names);
                break;
            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                    CollectIdentifierNamesInto(elem, names);
                break;
            case ListLiteral list:
                foreach (var elem in list.Elements)
                    CollectIdentifierNamesInto(elem, names);
                break;
            default:
                // walker-default-contract: literals and other leaf nodes contribute no
                // identifiers — any kind not listed above is deliberately ignored by this walker
                // (rostered in DispatchSiteInventoryTests).
                break;
        }
    }

    /// <summary>
    /// Validates a single parameter's default value.
    /// </summary>
    private void ValidateDefaultValue(Parameter param, string functionName, AdmissionTable table)
    {
        var defaultValue = param.DefaultValue!;

        // Check for mutable defaults first (these are never allowed)
        if (IsMutableDefault(defaultValue))
        {
            AddError(
                $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
                "Use None as default and initialize in the function body instead.",
                param.LineStart,
                param.ColumnStart, code: DiagnosticCodes.Validation.MutableDefault,
                span: param.Span);
            return;
        }

        // A reference resolves iff it names a const with the same backtick-escape spelling — the
        // rule TypeChecker.TryFoldConstantValue applies when it folds the const's own value.
        var kind = ConstantDefaultClassifier.Classify(defaultValue, id =>
            Context.SymbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true } constSymbol
            && constSymbol.IsNameBacktickEscaped == id.IsNameBacktickEscaped);

        if (!ConstantDefaultClassifier.IsAdmitted(kind, table))
        {
            var steer = kind switch
            {
                EmittableConstantKind.CaseConstructor => FormatCaseConstructorSteer(param, functionName),
                EmittableConstantKind.TupleLiteral =>
                    $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression. " +
                    "Tuple literals are not emittable as parameter defaults; initialize in the function body instead.",
                _ =>
                    $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
            };

            AddError(
                steer,
                param.LineStart,
                param.ColumnStart, code: DiagnosticCodes.Validation.NonConstDefault,
                span: param.Span);
            return;
        }

        // Check None assignment to non-nullable types
        if (defaultValue is NoneLiteral)
        {
            var paramType = Context.TypeResolver.ResolveTypeAnnotation(param.Type);

            // None is only valid for nullable/optional types
            if (paramType is not NullableType and not OptionalType && paramType is not UnknownType)
            {
                AddError(
                    $"Cannot use 'None' as default value for non-nullable parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
                    $"Use '{paramType.GetDisplayName()}?' to make the parameter nullable.",
                    param.LineStart,
                    param.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue,
                    span: param.Span);
            }
        }

        // Check None() assignment to non-optional types
        if (defaultValue is FunctionCall { Function: NoneLiteral } noneCall
            && noneCall.Arguments.Length == 0 && noneCall.KeywordArguments.Length == 0)
        {
            var paramType = Context.TypeResolver.ResolveTypeAnnotation(param.Type);

            if (paramType is not OptionalType && paramType is not UnknownType)
            {
                AddError(
                    $"Cannot use 'None()' as default value for non-optional parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
                    $"Use '{paramType.GetDisplayName()}?' to make the parameter optional.",
                    param.LineStart,
                    param.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue,
                    span: param.Span);
            }
        }
    }

    private static string FormatCaseConstructorSteer(Parameter param, string functionName)
    {
        return $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression. " +
            $"Use 'def {functionName}({param.Name}: {param.Type?.ToString() ?? "T?"} = None()) -> ...: {param.Name} ??= Some(...)' instead.";
    }

    /// <summary>
    /// Checks if an expression is a mutable default value.
    /// Mutable defaults include: [], {}, set()
    /// </summary>
    private static bool IsMutableDefault(Expression expr)
    {
        return expr switch
        {
            // Empty list literal [] or list with elements [1, 2, 3]
            ListLiteral => true,

            // Empty dict literal {} (not to be confused with empty set)
            // DictLiteral is always mutable regardless of contents
            DictLiteral => true,

            // Set literal {1, 2, 3}
            SetLiteral => true,

            // Function call to set()/list()/dict() - collection constructors. Matched against the
            // canonical (paren-stripped) callee so `(list)()` is as mutable as `list()` (#1170).
            FunctionCall call when AstHelper.UnwrapParenthesized(call.Function)
                is Identifier { Name: BuiltinNames.Set or BuiltinNames.List or BuiltinNames.Dict } => true,

            // Parenthesized expression - check inner expression
            Parenthesized paren => IsMutableDefault(paren.Expression),

            _ => false
        };
    }

}

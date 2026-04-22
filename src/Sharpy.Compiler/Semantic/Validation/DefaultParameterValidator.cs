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
                ValidateDefaultValue(param, functionDef.Name);
            }
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
            // Literals and other leaf nodes contribute no identifiers
        }
    }

    /// <summary>
    /// Validates a single parameter's default value.
    /// </summary>
    private void ValidateDefaultValue(Parameter param, string functionName)
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

        // Check that the default value is a compile-time constant
        if (!IsCompileTimeConstant(defaultValue))
        {
            AddError(
                $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
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

            // Function call to set() - set constructor
            FunctionCall call when call.Function is Identifier id && id.Name == BuiltinNames.Set => true,

            // Function call to list() - list constructor
            FunctionCall call when call.Function is Identifier id && id.Name == BuiltinNames.List => true,

            // Function call to dict() - dict constructor
            FunctionCall call when call.Function is Identifier id && id.Name == BuiltinNames.Dict => true,

            // Parenthesized expression - check inner expression
            Parenthesized paren => IsMutableDefault(paren.Expression),

            _ => false
        };
    }

    /// <summary>
    /// Checks if an expression is a compile-time constant.
    /// Compile-time constants include:
    /// - Literal values (int, float, string, bool, None)
    /// - Tuples of compile-time constants
    /// - Unary operations on constants (-1, +1, not True)
    /// - Binary operations on constants (1 + 2) - though typically not recommended
    /// - Enum member access (Color.RED, HttpMethod.GET)
    /// - References to const declarations (MAX_SIZE, DEFAULT_NAME)
    /// </summary>
    private bool IsCompileTimeConstant(Expression expr)
    {
        return expr switch
        {
            // Primitive literals are always compile-time constants
            IntegerLiteral => true,
            FloatLiteral => true,
            StringLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,

            // Tuple of constants is a constant (immutable)
            TupleLiteral tuple => tuple.Elements.All(IsCompileTimeConstant),

            // Unary operations on constants
            UnaryOp unary => IsCompileTimeConstant(unary.Operand),

            // Binary operations on constants (e.g., 1 + 2)
            BinaryOp binary => IsCompileTimeConstant(binary.Left) && IsCompileTimeConstant(binary.Right),

            // Parenthesized expression
            Parenthesized paren => IsCompileTimeConstant(paren.Expression),

            // Conditional expression with all constant parts
            ConditionalExpression cond =>
                IsCompileTimeConstant(cond.Test) &&
                IsCompileTimeConstant(cond.ThenValue) &&
                IsCompileTimeConstant(cond.ElseValue),

            // Identifiers referencing const declarations are compile-time constants
            Identifier id => IsConstReference(id),

            // None() is a compile-time constant (empty Optional)
            FunctionCall call when call.Function is NoneLiteral
                && call.Arguments.Length == 0
                => true,

            // Some(const), Ok(const), Err(const) are compile-time constants
            FunctionCall call when call.Function is Identifier fid
                && fid.Name is "Some" or "Ok" or "Err"
                && call.Arguments.Length == 1
                => IsCompileTimeConstant(call.Arguments[0]),

            // Other function calls are generally NOT compile-time constants
            FunctionCall => false,

            // Member access to enum members is a compile-time constant
            MemberAccess memberAccess => IsEnumMemberAccess(memberAccess),

            // Index access is NOT a compile-time constant
            IndexAccess => false,

            // Mutable collections are NOT compile-time constants
            ListLiteral => false,
            DictLiteral => false,
            SetLiteral => false,

            // Comprehensions are NOT compile-time constants
            ListComprehension => false,
            SetComprehension => false,
            DictComprehension => false,
            DictSpreadComprehension => false,

            // Lambda expressions are NOT compile-time constants
            LambdaExpression => false,

            // Default: not a compile-time constant
            _ => false
        };
    }

    /// <summary>
    /// Checks if an identifier references a const declaration.
    /// </summary>
    private bool IsConstReference(Identifier id)
    {
        var symbol = Context.SymbolTable.Lookup(id.Name);
        return symbol is VariableSymbol { IsConstant: true };
    }

    /// <summary>
    /// Checks if a member access expression refers to an enum member.
    /// E.g., Color.RED, HttpMethod.GET
    /// </summary>
    private bool IsEnumMemberAccess(MemberAccess memberAccess)
    {
        // The object must be an identifier (the enum type name)
        if (memberAccess.Object is not Identifier typeId)
        {
            return false;
        }

        // Look up the type in the symbol table
        var symbol = Context.SymbolTable.Lookup(typeId.Name);

        // Check if it's an enum type
        return symbol is TypeSymbol { TypeKind: TypeKind.Enum };
    }
}

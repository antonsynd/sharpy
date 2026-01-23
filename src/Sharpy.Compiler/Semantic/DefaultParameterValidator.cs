using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates default parameter values in function definitions:
/// - Default values must be compile-time constant expressions
/// - Mutable defaults ([], {}, set()) are not allowed
/// - None is only allowed for nullable parameter types
/// </summary>
/// <remarks>
/// DEPRECATED: This validator has been replaced by DefaultParameterValidatorV2 which
/// implements the ISemanticValidator interface for the new validation pipeline.
/// New code should use ValidationPipelineFactory.CreateDefault() instead of
/// instantiating this class directly.
/// </remarks>
[Obsolete("Use DefaultParameterValidatorV2 via ValidationPipelineFactory.CreateDefault() instead")]
public class DefaultParameterValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly TypeResolver _typeResolver;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public DefaultParameterValidator(
        SymbolTable symbolTable,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _typeResolver = typeResolver;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Validates all default parameter values in a function definition.
    /// </summary>
    public void ValidateFunctionDefaults(FunctionDef functionDef)
    {
        foreach (var param in functionDef.Parameters)
        {
            if (param.DefaultValue != null)
            {
                ValidateDefaultValue(param, functionDef.Name);
            }
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
                param.ColumnStart);
            return;
        }

        // Check that the default value is a compile-time constant
        if (!IsCompileTimeConstant(defaultValue))
        {
            AddError(
                $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
                param.LineStart,
                param.ColumnStart);
            return;
        }

        // Check None assignment to non-nullable types
        if (defaultValue is NoneLiteral)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

            // None is only valid for nullable types
            if (paramType is not NullableType && paramType is not UnknownType)
            {
                AddError(
                    $"Cannot use 'None' as default value for non-nullable parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
                    $"Use '{paramType.GetDisplayName()}?' to make the parameter nullable.",
                    param.LineStart,
                    param.ColumnStart);
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
            FunctionCall call when call.Function is Identifier id && id.Name == "set" => true,

            // Function call to list() - list constructor
            FunctionCall call when call.Function is Identifier id && id.Name == "list" => true,

            // Function call to dict() - dict constructor
            FunctionCall call when call.Function is Identifier id && id.Name == "dict" => true,

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

            // Function calls are generally NOT compile-time constants
            // (unless they are special constant constructors, but we'll be strict here)
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
        var symbol = _symbolTable.Lookup(id.Name);
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
        var symbol = _symbolTable.Lookup(typeId.Name);

        // Check if it's an enum type
        return symbol is TypeSymbol { TypeKind: TypeKind.Enum };
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.LogError(error.Message, line ?? 0, column ?? 0);
    }
}

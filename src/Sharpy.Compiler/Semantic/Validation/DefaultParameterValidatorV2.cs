using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates default parameter values in function definitions:
/// - Default values must be compile-time constant expressions
/// - Mutable defaults ([], {}, set()) are not allowed
/// - None is only allowed for nullable parameter types
///
/// This is the pipeline-compatible version of DefaultParameterValidator.
/// </summary>
public class DefaultParameterValidatorV2 : SemanticValidatorBase
{
    public override string Name => "DefaultParameterValidator";
    public override int Order => 250; // Before type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting default parameter validation");

        foreach (var stmt in module.Body)
        {
            ValidateStatement(stmt);
        }
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunctionDefaults(funcDef);
                // Also validate nested functions
                foreach (var bodyStmt in funcDef.Body)
                    ValidateStatement(bodyStmt);
                break;
            case ClassDef classDef:
                foreach (var member in classDef.Body)
                    ValidateStatement(member);
                break;
            case StructDef structDef:
                foreach (var member in structDef.Body)
                    ValidateStatement(member);
                break;
        }
    }

    /// <summary>
    /// Validates all default parameter values in a function definition.
    /// </summary>
    private void ValidateFunctionDefaults(FunctionDef functionDef)
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
            AddError(_context,
                $"Mutable default value is not allowed for parameter '{param.Name}' in function '{functionName}'. " +
                "Use None as default and initialize in the function body instead.",
                param.LineStart,
                param.ColumnStart, code: DiagnosticCodes.Validation.MutableDefault);
            return;
        }

        // Check that the default value is a compile-time constant
        if (!IsCompileTimeConstant(defaultValue))
        {
            AddError(_context,
                $"Default value for parameter '{param.Name}' in function '{functionName}' must be a compile-time constant expression",
                param.LineStart,
                param.ColumnStart, code: DiagnosticCodes.Validation.NonConstDefault);
            return;
        }

        // Check None assignment to non-nullable types
        if (defaultValue is NoneLiteral)
        {
            var paramType = _context.TypeResolver.ResolveTypeAnnotation(param.Type);

            // None is only valid for nullable/optional types
            if (paramType is not NullableType and not OptionalType && paramType is not UnknownType)
            {
                AddError(_context,
                    $"Cannot use 'None' as default value for non-nullable parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
                    $"Use '{paramType.GetDisplayName()}?' to make the parameter nullable.",
                    param.LineStart,
                    param.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue);
            }
        }

        // Check None() assignment to non-optional types
        if (defaultValue is FunctionCall { Function: NoneLiteral } noneCall
            && noneCall.Arguments.Length == 0 && noneCall.KeywordArguments.Length == 0)
        {
            var paramType = _context.TypeResolver.ResolveTypeAnnotation(param.Type);

            if (paramType is not OptionalType && paramType is not UnknownType)
            {
                AddError(_context,
                    $"Cannot use 'None()' as default value for non-optional parameter '{param.Name}' of type '{paramType.GetDisplayName()}' in function '{functionName}'. " +
                    $"Use '{paramType.GetDisplayName()}?' to make the parameter optional.",
                    param.LineStart,
                    param.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue);
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
        var symbol = _context.SymbolTable.Lookup(id.Name);
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
        var symbol = _context.SymbolTable.Lookup(typeId.Name);

        // Check if it's an enum type
        return symbol is TypeSymbol { TypeKind: TypeKind.Enum };
    }
}

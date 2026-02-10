using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates operator usage in Sharpy code:
/// - Binary operators (+, -, *, /, ==, etc.)
/// - Unary operators (-, +, not, ~)
/// - Augmented assignment operators (+=, -=, etc.)
///
/// Post-pass validation of operator usage. TypeInferenceService handles
/// type inference during type-checking; this validator catches additional
/// constraint violations after types are resolved.
/// </summary>
internal class OperatorValidator : SemanticValidatorBase
{
    public override string Name => "OperatorValidator";
    public override int Order => 500; // Same as ProtocolValidator (after access validation)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting operator validation");

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
            case ForStatement forStmt:
                ValidateExpression(forStmt.Iterator);
                foreach (var bodyStmt in forStmt.Body)
                    ValidateStatement(bodyStmt);
                break;
            case WhileStatement whileStmt:
                ValidateExpression(whileStmt.Test);
                foreach (var bodyStmt in whileStmt.Body)
                    ValidateStatement(bodyStmt);
                break;
            case IfStatement ifStmt:
                ValidateExpression(ifStmt.Test);
                foreach (var bodyStmt in ifStmt.ThenBody)
                    ValidateStatement(bodyStmt);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    ValidateExpression(elif.Test);
                    foreach (var bodyStmt in elif.Body)
                        ValidateStatement(bodyStmt);
                }
                foreach (var bodyStmt in ifStmt.ElseBody)
                    ValidateStatement(bodyStmt);
                break;
            case TryStatement tryStmt:
                foreach (var bodyStmt in tryStmt.Body)
                    ValidateStatement(bodyStmt);
                foreach (var handler in tryStmt.Handlers)
                {
                    foreach (var bodyStmt in handler.Body)
                        ValidateStatement(bodyStmt);
                }
                foreach (var bodyStmt in tryStmt.FinallyBody)
                    ValidateStatement(bodyStmt);
                break;
            case ExpressionStatement exprStmt:
                ValidateExpression(exprStmt.Expression);
                break;
            case Assignment assignment:
                // Check if this is an augmented assignment (+=, -=, etc.)
                if (assignment.Operator != AssignmentOperator.Assign)
                {
                    ValidateAugmentedAssignment(assignment);
                }
                ValidateExpression(assignment.Target);
                ValidateExpression(assignment.Value);
                break;
            case VariableDeclaration varDecl:
                if (varDecl.InitialValue != null)
                    ValidateExpression(varDecl.InitialValue);
                break;
            case ReturnStatement returnStmt:
                if (returnStmt.Value != null)
                    ValidateExpression(returnStmt.Value);
                break;
        }
    }

    private void ValidateExpression(Expression expr)
    {
        switch (expr)
        {
            case BinaryOp binOp:
                ValidateBinaryOp(binOp);
                ValidateExpression(binOp.Left);
                ValidateExpression(binOp.Right);
                break;
            case UnaryOp unaryOp:
                ValidateUnaryOp(unaryOp);
                ValidateExpression(unaryOp.Operand);
                break;
            case FunctionCall call:
                ValidateExpression(call.Function);
                foreach (var arg in call.Arguments)
                    ValidateExpression(arg);
                foreach (var kwArg in call.KeywordArguments)
                    ValidateExpression(kwArg.Value);
                break;
            case MemberAccess memberAccess:
                ValidateExpression(memberAccess.Object);
                break;
            case IndexAccess indexAccess:
                ValidateExpression(indexAccess.Object);
                ValidateExpression(indexAccess.Index);
                break;
            case ListLiteral listLit:
                foreach (var elem in listLit.Elements)
                    ValidateExpression(elem);
                break;
            case DictLiteral dictLit:
                foreach (var entry in dictLit.Entries)
                {
                    ValidateExpression(entry.Key);
                    ValidateExpression(entry.Value);
                }
                break;
            case SetLiteral setLit:
                foreach (var elem in setLit.Elements)
                    ValidateExpression(elem);
                break;
            case TupleLiteral tupleLit:
                foreach (var elem in tupleLit.Elements)
                    ValidateExpression(elem);
                break;
            case ListComprehension listComp:
                ValidateExpression(listComp.Element);
                foreach (var clause in listComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case SetComprehension setComp:
                ValidateExpression(setComp.Element);
                foreach (var clause in setComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case DictComprehension dictComp:
                ValidateExpression(dictComp.Key);
                ValidateExpression(dictComp.Value);
                foreach (var clause in dictComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        ValidateExpression(forClause.Iterator);
                    else if (clause is IfClause ifClause)
                        ValidateExpression(ifClause.Condition);
                }
                break;
            case ConditionalExpression cond:
                ValidateExpression(cond.Test);
                ValidateExpression(cond.ThenValue);
                ValidateExpression(cond.ElseValue);
                break;
            case Parenthesized paren:
                ValidateExpression(paren.Expression);
                break;
        }
    }

    private void ValidateBinaryOp(BinaryOp binOp)
    {
        var leftType = _context.SemanticInfo.GetExpressionType(binOp.Left);
        var rightType = _context.SemanticInfo.GetExpressionType(binOp.Right);

        if (leftType == null || rightType == null)
            return;
        if (leftType is UnknownType || rightType is UnknownType)
            return;

        // Validate specific operators
        switch (binOp.Operator)
        {
            case BinaryOperator.NullCoalesce:
                ValidateNullCoalesce(binOp, leftType, rightType);
                break;

            case BinaryOperator.And:
            case BinaryOperator.Or:
            case BinaryOperator.Is:
            case BinaryOperator.IsNot:
            case BinaryOperator.In:
            case BinaryOperator.NotIn:
                // These are always valid
                break;

            default:
                // For arithmetic, comparison, and bitwise operators,
                // validate that the types support the operation
                ValidateArithmeticOrComparisonOp(binOp, leftType, rightType);
                break;
        }
    }

    /// <summary>
    /// Checks whether an error has already been reported at the given position.
    /// Used to avoid duplicate diagnostics when the TypeChecker has already reported
    /// an operator error (SPY0222) during type inference — this validator should not
    /// re-report it as SPY0402.
    /// </summary>
    private bool HasErrorAtPosition(int? line, int? column)
    {
        if (line == null && column == null)
            return false;
        var errors = _context.Diagnostics.GetErrors();
        for (int i = 0; i < errors.Count; i++)
        {
            if (errors[i].Line == line && errors[i].Column == column)
                return true;
        }
        return false;
    }

    private void ValidateNullCoalesce(BinaryOp binOp, SemanticType leftType, SemanticType rightType)
    {
        // No HasErrorAtPosition guard here: this validator provides a more specific
        // diagnostic (SPY0403: "must be nullable") than the TypeChecker's generic
        // SPY0222 ("does not support operator ??"). Both may fire but the specific
        // one is more actionable for the user.
        if (leftType is not NullableType and not OptionalType)
        {
            AddError(_context,
                $"Left operand of null coalescing operator must be nullable, but got '{leftType.GetDisplayName()}'",
                binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Validation.InvalidNullCoalesce,
                span: binOp.Span);
        }
    }

    private void ValidateArithmeticOrComparisonOp(BinaryOp binOp, SemanticType leftType, SemanticType rightType)
    {
        var dunderName = BinaryOperatorToDunder(binOp.Operator);
        if (dunderName == null)
            return;

        // Check if operator is supported by the left type
        if (!SupportsOperator(leftType, dunderName))
        {
            // Check if it's a comparison operator - primitives and constrained type parameters support these
            if (!IsComparisonOperator(binOp.Operator) || !IsPrimitiveType(leftType))
            {
                // Type parameters with IComparable constraint support comparison operators,
                // and all type parameters support equality (== / !=)
                if (IsTypeParameterOperatorAllowed(binOp.Operator, leftType, rightType))
                    return;

                // Check right-hand side for reflected operator
                var reflectedDunder = GetReflectedDunder(dunderName);
                if (reflectedDunder == null || !SupportsOperator(rightType, reflectedDunder))
                {
                    if (!HasErrorAtPosition(binOp.LineStart, binOp.ColumnStart))
                    {
                        AddError(_context,
                            $"Type '{leftType.GetDisplayName()}' does not support operator '{OperatorToString(binOp.Operator)}' with right operand of type '{rightType.GetDisplayName()}'",
                            binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Validation.UnsupportedOperator,
                            span: binOp.Span);
                    }
                }
            }
        }
    }

    private static bool IsTypeParameterOperatorAllowed(BinaryOperator op, SemanticType leftType, SemanticType rightType)
    {
        var typeParam = leftType as TypeParameterType ?? rightType as TypeParameterType;
        if (typeParam == null)
            return false;

        // Equality operators are always allowed for type parameters
        if (op is BinaryOperator.Equal or BinaryOperator.NotEqual)
            return true;

        // Comparison operators require IComparable constraint
        if (op is BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual)
        {
            foreach (var constraint in typeParam.Constraints)
            {
                if (constraint is TypeConstraint tc && tc.Type.Name.Contains("Comparable"))
                    return true;
            }
        }

        return false;
    }

    private void ValidateUnaryOp(UnaryOp unaryOp)
    {
        var operandType = _context.SemanticInfo.GetExpressionType(unaryOp.Operand);
        if (operandType == null || operandType is UnknownType)
            return;

        // 'not' is always valid
        if (unaryOp.Operator == UnaryOperator.Not)
            return;

        var dunderName = UnaryOperatorToDunder(unaryOp.Operator);
        if (dunderName == null)
            return;

        if (!SupportsOperator(operandType, dunderName))
        {
            if (!HasErrorAtPosition(unaryOp.LineStart, unaryOp.ColumnStart))
            {
                AddError(_context,
                    $"Type '{operandType.GetDisplayName()}' does not support unary operator '{OperatorToString(unaryOp.Operator)}'",
                    unaryOp.LineStart, unaryOp.ColumnStart, code: DiagnosticCodes.Validation.UnsupportedOperator,
                    span: unaryOp.Span);
            }
        }
    }

    private void ValidateAugmentedAssignment(Assignment assignment)
    {
        var targetType = _context.SemanticInfo.GetExpressionType(assignment.Target);
        var valueType = _context.SemanticInfo.GetExpressionType(assignment.Value);

        if (targetType == null || valueType == null)
            return;
        if (targetType is UnknownType || valueType is UnknownType)
            return;

        var dunderName = AugmentedOperatorToDunder(assignment.Operator);
        if (dunderName == null)
            return;

        // Check for in-place operator first, fall back to regular binary operator
        if (!SupportsOperator(targetType, dunderName))
        {
            var regularDunder = dunderName.Replace("__i", "__");
            if (!SupportsOperator(targetType, regularDunder))
            {
                if (!HasErrorAtPosition(assignment.LineStart, assignment.ColumnStart))
                {
                    AddError(_context,
                        $"Unsupported operand types for {OperatorToString(assignment.Operator)}: '{targetType.GetDisplayName()}' and '{valueType.GetDisplayName()}'",
                        assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Validation.UnsupportedOperator,
                        span: assignment.Span);
                }
            }
        }
    }

    private bool SupportsOperator(SemanticType type, string dunderName)
    {
        // String supports concatenation and comparison (check first, before BuiltinType)
        if (type == SemanticType.Str)
        {
            return dunderName is DunderNames.Add or DunderNames.Mul or DunderNames.Eq or DunderNames.Ne or DunderNames.Lt or DunderNames.Le or DunderNames.Gt or DunderNames.Ge;
        }

        // Builtin numeric types support arithmetic operations
        if (type is BuiltinType)
        {
            return dunderName switch
            {
                // Arithmetic - supported by int, float, etc.
                DunderNames.Add or DunderNames.Sub or DunderNames.Mul or DunderNames.Div or DunderNames.FloorDiv or DunderNames.Mod or DunderNames.Pow
                    => IsNumericType(type),
                // Bitwise - supported by int
                DunderNames.And or DunderNames.Or or DunderNames.Xor or DunderNames.LShift or DunderNames.RShift
                    => type == SemanticType.Int || type == SemanticType.Long,
                // Unary
                DunderNames.Neg or DunderNames.Pos => IsNumericType(type),
                DunderNames.Invert => type == SemanticType.Int || type == SemanticType.Long,
                // Comparison - supported by all primitives
                DunderNames.Eq or DunderNames.Ne or DunderNames.Lt or DunderNames.Le or DunderNames.Gt or DunderNames.Ge => true,
                _ => false
            };
        }

        // Generic types (list, dict, set, tuple)
        if (type is GenericType generic)
        {
            return generic.Name switch
            {
                "list" => dunderName is DunderNames.Add or DunderNames.IAdd or DunderNames.Mul or DunderNames.IMul or DunderNames.Eq or DunderNames.Ne,
                "tuple" => dunderName is DunderNames.Add or DunderNames.Mul or DunderNames.Eq or DunderNames.Ne,
                "set" => dunderName is DunderNames.Or or DunderNames.And or DunderNames.Sub or DunderNames.Xor or DunderNames.IOr or DunderNames.IAnd or DunderNames.ISub or DunderNames.IXor or DunderNames.Eq or DunderNames.Ne,
                "dict" => dunderName is DunderNames.Or or DunderNames.IOr or DunderNames.Eq or DunderNames.Ne,
                _ => false
            };
        }

        // User-defined types
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            // Check operator methods (e.g., __eq__, __ne__, __lt__, __add__, etc.)
            if (udt.Symbol.OperatorMethods.ContainsKey(dunderName))
                return true;

            // __ne__ is auto-synthesized from __eq__ (and vice versa) in codegen
            if (dunderName == DunderNames.Ne && udt.Symbol.OperatorMethods.ContainsKey(DunderNames.Eq))
                return true;
            if (dunderName == DunderNames.Eq && udt.Symbol.OperatorMethods.ContainsKey(DunderNames.Ne))
                return true;

            // Check protocol methods
            if (udt.Symbol.ProtocolMethods.ContainsKey(dunderName))
                return true;

            // Check regular methods
            if (udt.Symbol.Methods.Any(m => m.Name == dunderName))
                return true;
        }

        return false;
    }

    private bool IsNumericType(SemanticType type)
    {
        return type == SemanticType.Int
            || type == SemanticType.Long
            || type == SemanticType.Float
            || type == SemanticType.Float32
            || type == SemanticType.Double;
    }

    private bool IsPrimitiveType(SemanticType type)
    {
        return type is BuiltinType || type == SemanticType.Str;
    }

    private bool IsComparisonOperator(BinaryOperator op)
    {
        return op is BinaryOperator.Equal or BinaryOperator.NotEqual
            or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual;
    }

    private string? BinaryOperatorToDunder(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => DunderNames.Add,
            BinaryOperator.Subtract => DunderNames.Sub,
            BinaryOperator.Multiply => DunderNames.Mul,
            BinaryOperator.Divide => DunderNames.Div,
            BinaryOperator.FloorDivide => DunderNames.FloorDiv,
            BinaryOperator.Modulo => DunderNames.Mod,
            BinaryOperator.Power => DunderNames.Pow,
            BinaryOperator.BitwiseAnd => DunderNames.And,
            BinaryOperator.BitwiseOr => DunderNames.Or,
            BinaryOperator.BitwiseXor => DunderNames.Xor,
            BinaryOperator.LeftShift => DunderNames.LShift,
            BinaryOperator.RightShift => DunderNames.RShift,
            BinaryOperator.Equal => DunderNames.Eq,
            BinaryOperator.NotEqual => DunderNames.Ne,
            BinaryOperator.LessThan => DunderNames.Lt,
            BinaryOperator.LessThanOrEqual => DunderNames.Le,
            BinaryOperator.GreaterThan => DunderNames.Gt,
            BinaryOperator.GreaterThanOrEqual => DunderNames.Ge,
            _ => null
        };
    }

    private string? GetReflectedDunder(string dunderName)
    {
        return dunderName switch
        {
            DunderNames.Add => DunderNames.RAdd,
            DunderNames.Sub => DunderNames.RSub,
            DunderNames.Mul => DunderNames.RMul,
            DunderNames.Div => DunderNames.RDiv,
            DunderNames.FloorDiv => DunderNames.RFloorDiv,
            DunderNames.Mod => DunderNames.RMod,
            DunderNames.Pow => DunderNames.RPow,
            DunderNames.And => DunderNames.RAnd,
            DunderNames.Or => DunderNames.ROr,
            DunderNames.Xor => DunderNames.RXor,
            DunderNames.LShift => DunderNames.RLShift,
            DunderNames.RShift => DunderNames.RRShift,
            _ => null
        };
    }

    private string? UnaryOperatorToDunder(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Minus => DunderNames.Neg,
            UnaryOperator.Plus => DunderNames.Pos,
            UnaryOperator.BitwiseNot => DunderNames.Invert,
            _ => null
        };
    }

    private string? AugmentedOperatorToDunder(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => DunderNames.IAdd,
            AssignmentOperator.MinusAssign => DunderNames.ISub,
            AssignmentOperator.StarAssign => DunderNames.IMul,
            AssignmentOperator.SlashAssign => DunderNames.IDiv,
            AssignmentOperator.DoubleSlashAssign => DunderNames.IFloorDiv,
            AssignmentOperator.PercentAssign => DunderNames.IMod,
            AssignmentOperator.PowerAssign => DunderNames.IPow,
            AssignmentOperator.AndAssign => DunderNames.IAnd,
            AssignmentOperator.OrAssign => DunderNames.IOr,
            AssignmentOperator.XorAssign => DunderNames.IXor,
            AssignmentOperator.LeftShiftAssign => DunderNames.ILShift,
            AssignmentOperator.RightShiftAssign => DunderNames.IRShift,
            _ => null
        };
    }

    private string OperatorToString(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.FloorDivide => "//",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Power => "**",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            _ => op.ToString()
        };
    }

    private string OperatorToString(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Minus => "-",
            UnaryOperator.Plus => "+",
            UnaryOperator.Not => "not",
            UnaryOperator.BitwiseNot => "~",
            _ => op.ToString()
        };
    }

    private string OperatorToString(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => "+=",
            AssignmentOperator.MinusAssign => "-=",
            AssignmentOperator.StarAssign => "*=",
            AssignmentOperator.SlashAssign => "/=",
            AssignmentOperator.DoubleSlashAssign => "//=",
            AssignmentOperator.PercentAssign => "%=",
            AssignmentOperator.PowerAssign => "**=",
            AssignmentOperator.AndAssign => "&=",
            AssignmentOperator.OrAssign => "|=",
            AssignmentOperator.XorAssign => "^=",
            AssignmentOperator.LeftShiftAssign => "<<=",
            AssignmentOperator.RightShiftAssign => ">>=",
            _ => op.ToString()
        };
    }
}

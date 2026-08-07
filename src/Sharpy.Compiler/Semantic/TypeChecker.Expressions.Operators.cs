using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Binary/unary operators, comparisons, type casts, type checks, try/maybe expressions
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
        }

        if (binOp.Operator == BinaryOperator.And)
        {
            return CheckBooleanAndOp(binOp);
        }

        if (binOp.Operator == BinaryOperator.Or)
        {
            return CheckBooleanOrOp(binOp);
        }

        // The operand of an `is (not) None` test reads the honest, un-narrowed value: mark it so
        // the read sites skip narrowing for that node (see _typeTestOperand). The scope ends right
        // after the operand checks — the suppression must not leak into sibling expressions.
        // When this is NOT a type test the scope pushes the field's CURRENT value, so an enclosing
        // operand survives rather than being cleared for the duration of these operands.
        var typeTestOperand = _typeTestOperand;
        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            if (binOp.Right is NoneLiteral)
                typeTestOperand = UnwrapParenthesized(binOp.Left);
            else if (binOp.Left is NoneLiteral)
                typeTestOperand = UnwrapParenthesized(binOp.Right);
        }

        SemanticType leftType, rightType;
        using (ScopedValue.Push(ref _typeTestOperand, typeTestOperand))
        {
            leftType = CheckExpression(binOp.Left);
            rightType = CheckExpression(binOp.Right);
        }

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Reject void-returning call operands in equality comparisons (#911). A `None`-typed
        // (VoidType) operand that is NOT the literal `None` is a void-returning call used as a
        // comparand (e.g. `s == f()` where `f() -> None`). Python would evaluate the call and
        // compare against None, but that is almost certainly a bug, and Sharpy already rejects
        // void-call *assignment* (SPY0229). Gating here — before InferBinaryOpType — also
        // guarantees the literal-shape invariant the equality lowering relies on: any VoidType
        // operand that reaches inference/lowering is the `None` literal. Scope is ==/!= only;
        // other operators already fail inference with SPY0222. (Axiom 3 > Axiom 2.)
        if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            var voidOperandFound = false;
            if (leftType is VoidType && binOp.Left is not NoneLiteral)
            {
                AddError(
                    "Expression of type 'None' has no value and cannot be used as a comparison operand; call it as a statement and compare separately",
                    binOp.Left.LineStart,
                    binOp.Left.ColumnStart,
                    code: DiagnosticCodes.Semantic.VoidComparisonOperand,
                    span: binOp.Left.Span);
                voidOperandFound = true;
            }
            if (rightType is VoidType && binOp.Right is not NoneLiteral)
            {
                AddError(
                    "Expression of type 'None' has no value and cannot be used as a comparison operand; call it as a statement and compare separately",
                    binOp.Right.LineStart,
                    binOp.Right.ColumnStart,
                    code: DiagnosticCodes.Semantic.VoidComparisonOperand,
                    span: binOp.Right.Span);
                voidOperandFound = true;
            }
            if (voidOperandFound)
            {
                return SemanticType.Unknown;
            }
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);

        // If type inference fails, report the error directly
        // (validators may not catch all type incompatibilities)
        if (resultType == null)
        {
            var message =
                $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binOp.Operator)}' with operand of type '{rightType.GetDisplayName()}'";

            // When comparing against the `None` literal with ==/!=, point the user at the
            // supported spelling: Sharpy rejects `x == None` (SPY0222) but accepts `x is None`
            // (#1079). Both operand orders (`x == None` and `None == x`) get the hint. The
            // suggested operator rides the diagnostic data payload for a future LSP quick-fix.
            IReadOnlyDictionary<string, string>? data = null;
            if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual
                && (binOp.Left is NoneLiteral || binOp.Right is NoneLiteral))
            {
                var suggestedOperator = binOp.Operator == BinaryOperator.Equal ? "is None" : "is not None";
                message += $". Did you mean '{suggestedOperator}'?";
                data = new Dictionary<string, string> { ["suggestedOperator"] = suggestedOperator };
            }

            AddError(
                message,
                binOp.LineStart,
                binOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                span: binOp.Span,
                data: data);
            return SemanticType.Unknown;
        }

        // Record how equality (==/!=) should be lowered by codegen. Tuples and CLR types
        // that resolve via Equals (no op_Equality) must emit an Equals call rather than a
        // native C# operator. The emitter reads this annotation from SemanticInfo (#886).
        //
        // Invariant (#911): any VoidType operand reaching this point is guaranteed to be the
        // `None` literal — void-returning call operands were rejected above with SPY0329. This
        // is what makes the NoneCheck lowering's AST-shape operand selection well-defined. Three
        // consumers rely on it: InferBinaryOpType/GetBinaryOpLowering (TypeInferenceService),
        // the emitter's NoneCheck branches (RoslynEmitter.Expressions.Operators / .Statements.
        // ControlFlow), and OperatorValidator (suppress-only, never selects operands).
        if (binOp.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual)
        {
            var lowering = _typeInference.GetBinaryOpLowering(binOp.Operator, leftType, rightType);
            if (lowering != BinaryOpLowering.NativeOperator)
            {
                _semanticInfo.SetBinaryOpLowering(binOp, lowering);
            }
        }

        // Constant-fold integer exponentiation so a result that fits a wider integer type
        // widens (e.g. `10 ** 18` → long) and an out-of-range result is diagnosed (SPY0328)
        // instead of being silently truncated by the runtime double cast. Only constant
        // non-negative integer powers are folded; negative exponents keep the existing path
        // (Python: `2 ** -1 == 0.5`). The folded value is recorded so codegen emits a literal (#905).
        if (binOp.Operator == BinaryOperator.Power
            && TypeUtils.IsInteger(leftType) && TypeUtils.IsInteger(rightType))
        {
            var folded = TryFoldIntegerPower(binOp);
            if (folded != null)
                return folded;
        }

        // Constant integer +, -, * whose exact result does not fit the expression's own result
        // type is refused here (SPY0348) rather than left for Roslyn (#1234). Roslyn evaluates
        // CONSTANT expressions in a checked context regardless of the unchecked runtime default,
        // so `3794 * 1973 * 948` reached the C# compiler as CS0220 ("the operation overflows at
        // compile time in checked mode") and surfaced as an SPY0908 internal error — a compiler
        // bug report for what is really a user-program fact.
        if (binOp.Operator is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
            && TypeUtils.IsInteger(leftType) && TypeUtils.IsInteger(rightType))
        {
            CheckConstantIntegerOverflow(binOp, resultType);
        }

        // Warn when is/is not is used with value types — identity comparison is
        // meaningless because value types are boxed, so the result is always False.
        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            var effectiveLeft = leftType is NullableType nl ? nl.UnderlyingType : leftType;
            var effectiveRight = rightType is NullableType nr ? nr.UnderlyingType : rightType;

            if (effectiveLeft.IsValueType && effectiveRight.IsValueType)
            {
                var opSymbol = binOp.Operator == BinaryOperator.Is ? "is" : "is not";
                _diagnostics.AddWarning(
                    $"'{opSymbol}' used with value types '{leftType.GetDisplayName()}' and " +
                    $"'{rightType.GetDisplayName()}' — identity comparison is always False " +
                    "due to boxing; use '==' or '!=' instead",
                    binOp.LineStart,
                    binOp.ColumnStart,
                    _currentFilePath,
                    code: DiagnosticCodes.Validation.IsWithValueTypes,
                    phase: CompilerPhase.TypeChecking);
            }
        }

        return resultType;
    }

    /// <summary>
    /// Constant-folds a <c>base ** exponent</c> expression when both operands are constant
    /// integers and the exponent is non-negative. Returns <see cref="SemanticType.Int"/> or
    /// <see cref="SemanticType.Long"/> (whichever fits) and records the folded value in
    /// <see cref="SemanticInfo"/>; emits SPY0328 and returns <see cref="SemanticType.Unknown"/>
    /// when the result exceeds <c>long</c>. Returns <c>null</c> when the expression is not a
    /// constant non-negative integer power (caller keeps the regular inference result). (#905)
    /// </summary>
    private SemanticType? TryFoldIntegerPower(BinaryOp binOp)
    {
        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp.Left, out var baseValue)
            || !IntegerConstantEvaluator.TryGetConstantInteger(binOp.Right, out var exponent))
            return null;

        // Negative exponents are not folded — they keep the existing (double/runtime) path.
        if (exponent.Sign < 0)
            return null;

        // A constant exponent larger than int.MaxValue can never produce a fixed-width result
        // (and BigInteger.Pow takes an int exponent), so treat it as overflow.
        if (exponent > int.MaxValue)
        {
            ReportIntegerPowerOverflow(binOp);
            return SemanticType.Unknown;
        }

        var result = System.Numerics.BigInteger.Pow(baseValue, (int)exponent);

        // The folded VALUE is re-derived into an IrConstant by the lowering pass (E2 #1056); the
        // type checker keeps only the result-type + overflow decision here. Both sides read the same
        // pure IntegerConstantEvaluator, so they cannot diverge.
        if (result >= int.MinValue && result <= int.MaxValue)
            return SemanticType.Int;

        if (result >= long.MinValue && result <= long.MaxValue)
            return SemanticType.Long;

        ReportIntegerPowerOverflow(binOp);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Refuses a constant integer <c>+</c>/<c>-</c>/<c>*</c> whose exact value does not fit
    /// <paramref name="resultType"/> (SPY0348, #1234). Does nothing when the expression is not a
    /// constant integer, or when its result type is neither <c>int</c> nor <c>long</c> — every
    /// other integer width keeps whatever behavior it has today.
    /// <para>
    /// <b>No widening</b>, deliberately, and unlike constant <c>**</c> (which widens int→long
    /// before reporting SPY0328). A constant must type exactly as the same expression would with
    /// variables in place of its literals: runtime <c>int * int</c> is <c>int</c>, so constant
    /// <c>int * int</c> is <c>int</c> too. The user-visible consequence is that
    /// <c>x: long = 3794 * 1973 * 948</c> is refused even though the value fits <c>long</c> —
    /// annotating one operand (<c>3794L * 1973 * 948</c>) types the whole expression <c>long</c>
    /// and compiles. Documented in arithmetic_operators.md.
    /// </para>
    /// <para>
    /// Constant-vs-runtime asymmetry is intentional too: the same overflow with variable operands
    /// wraps silently under .NET's unchecked default. That is C#'s own documented behavior
    /// (CS0220 is an error while the runtime wraps), so Axiom 1 settles it.
    /// </para>
    /// </summary>
    private void CheckConstantIntegerOverflow(BinaryOp binOp, SemanticType resultType)
    {
        // A suffixed integer literal (3794L) is honoured by CODEGEN but IGNORED by type inference
        // today (#1314): `a: int = 3794L` still fails with CS0266 as an SPY0908, because the checker types
        // the literal 'int' while the emitter writes a C# 'long'. So when a suffix appears anywhere
        // in the constant subtree, the Sharpy result type is not the width Roslyn will compute in,
        // and this check — whose entire job is to predict CS0220 — must use the emitted width
        // instead. Without it, `3794L * 1973 * 948` (which compiles and prints 7096312776 today)
        // would be refused by a diagnostic whose own remedy sentence recommends exactly that
        // spelling. An unsigned suffix declines outright rather than guessing at its width.
        var suffix = GetConstantSuffixWidth(binOp);
        if (suffix == ConstantSuffixWidth.Unsigned)
            return;

        System.Numerics.BigInteger min;
        System.Numerics.BigInteger max;
        if (suffix == ConstantSuffixWidth.Long || resultType == SemanticType.Long)
        {
            min = long.MinValue;
            max = long.MaxValue;
        }
        else if (resultType == SemanticType.Int)
        {
            min = int.MinValue;
            max = int.MaxValue;
        }
        else
        {
            return;
        }

        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp, out var value))
            return;

        if (value >= min && value <= max)
            return;

        // Report only at the FIRST level that overflows. `a * b * c * d` is checked bottom-up, so
        // once `a * b * c` has its own diagnostic every enclosing node would repeat it. An operand
        // that is not itself a foldable operation (a bare out-of-range literal) never had a chance
        // to report, so it must not suppress this one.
        if (OverflowedOperandAlreadyReported(binOp.Left, min, max)
            || OverflowedOperandAlreadyReported(binOp.Right, min, max))
        {
            return;
        }

        AddError(
            $"Constant expression evaluates to {value}, which does not fit " +
            $"'{resultType.GetDisplayName()}'; Sharpy integers are fixed-width. Annotate an " +
            "operand as 'long' (e.g. '3794L * 1973 * 948') so the whole expression is computed " +
            "as 'long', or restructure the computation.",
            binOp.LineStart,
            binOp.ColumnStart,
            code: DiagnosticCodes.Semantic.ConstantIntegerOverflow,
            span: binOp.Span);
    }

    /// <summary>
    /// The integer width the EMITTED C# will compute a constant subtree in, as far as literal
    /// suffixes reveal it. See <see cref="CheckConstantIntegerOverflow"/> for why the Sharpy result
    /// type is not enough. This whole scan is a workaround for #1314 (inference ignores the
    /// suffix); delete it, and the call site's widening, when that is fixed.
    /// </summary>
    private enum ConstantSuffixWidth
    {
        /// <summary>No suffixed literal in the subtree — the Sharpy result type's width applies.</summary>
        None,
        /// <summary>A 64-bit suffix (L). C# propagates long upward through +, -, *.</summary>
        Long,
        /// <summary>An unsigned suffix (U/UL) — width not modelled here; the check declines.</summary>
        Unsigned
    }

    private static ConstantSuffixWidth GetConstantSuffixWidth(Expression expr)
    {
        switch (expr)
        {
            case IntegerLiteral { Suffix: { Length: > 0 } suffix }:
                if (suffix.Contains("u", System.StringComparison.OrdinalIgnoreCase))
                    return ConstantSuffixWidth.Unsigned;
                return suffix.Contains("l", System.StringComparison.OrdinalIgnoreCase)
                    ? ConstantSuffixWidth.Long
                    : ConstantSuffixWidth.None;

            case Parenthesized paren:
                return GetConstantSuffixWidth(paren.Expression);

            case UnaryOp unary:
                return GetConstantSuffixWidth(unary.Operand);

            case BinaryOp binary:
                var left = GetConstantSuffixWidth(binary.Left);
                var right = GetConstantSuffixWidth(binary.Right);
                if (left == ConstantSuffixWidth.Unsigned || right == ConstantSuffixWidth.Unsigned)
                    return ConstantSuffixWidth.Unsigned;
                return left == ConstantSuffixWidth.Long || right == ConstantSuffixWidth.Long
                    ? ConstantSuffixWidth.Long
                    : ConstantSuffixWidth.None;

            default:
                return ConstantSuffixWidth.None;
        }
    }

    /// <summary>
    /// True when <paramref name="operand"/> is itself a foldable constant arithmetic operation
    /// whose value is already out of range — meaning it drew its own SPY0348 and the enclosing
    /// node must stay quiet. See <see cref="CheckConstantIntegerOverflow"/>.
    /// </summary>
    private static bool OverflowedOperandAlreadyReported(
        Expression operand, System.Numerics.BigInteger min, System.Numerics.BigInteger max)
    {
        while (operand is Parenthesized paren)
            operand = paren.Expression;

        return operand is BinaryOp
        {
            Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
        }
            && IntegerConstantEvaluator.TryGetConstantInteger(operand, out var value)
            && (value < min || value > max);
    }

    private void ReportIntegerPowerOverflow(BinaryOp binOp)
    {
        AddError(
            "Result of integer exponentiation does not fit a 64-bit integer; Sharpy integers " +
            "are fixed-width. Use a floating-point base (e.g. '10.0 ** 50') or restructure the computation.",
            binOp.LineStart,
            binOp.ColumnStart,
            code: DiagnosticCodes.Semantic.IntegerPowerOverflow,
            span: binOp.Span);
    }

    // Constant integer literal evaluation moved to the shared, pure IntegerConstantEvaluator so the
    // lowering pass can re-derive the folded value into an IrConstant without duplicating parsing
    // logic (E2 #1056).

    private SemanticType CheckBooleanAndOp(BinaryOp andOp)
    {
        var leftType = CheckExpression(andOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(andOp.Right);
            return SemanticType.Unknown;
        }

        var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);

        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            // Apply the lowering-bearing entries so RHS reads of the narrowed variables record the
            // accessor codegen must emit (e.g. `x.Unwrap()`, `(Dog)a`) — #1081.
            _narrowingContext.ApplyNarrowings(leftNarrowed);
            rightType = CheckExpression(andOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        var resultType = _typeInference.InferBinaryOpType(BinaryOperator.And, leftType, rightType);
        if (resultType == null)
        {
            AddError(
                $"Type '{leftType.GetDisplayName()}' does not support operator 'and' with operand of type '{rightType.GetDisplayName()}'",
                andOp.LineStart,
                andOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                span: andOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    private SemanticType CheckBooleanOrOp(BinaryOp orOp)
    {
        var leftType = CheckExpression(orOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(orOp.Right);
            return SemanticType.Unknown;
        }

        // Expression-level narrowing (#1080): the right operand is evaluated only when the left is
        // falsy, so the left's NEGATIVE narrowings hold inside it (e.g. `x is None or use(x + 1)` —
        // the RHS sees x non-None). The narrowings do not leak past the operand.
        var leftNegativeNarrowed = ExtractNarrowedTypes(orOp.Left, false);

        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNegativeNarrowed);
            rightType = CheckExpression(orOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        var resultType = _typeInference.InferBinaryOpType(BinaryOperator.Or, leftType, rightType);
        if (resultType == null)
        {
            AddError(
                $"Type '{leftType.GetDisplayName()}' does not support operator 'or' with operand of type '{rightType.GetDisplayName()}'",
                orOp.LineStart,
                orOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                span: orOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    /// <summary>
    /// Type-checks the pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend x to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity)
    /// </summary>
    private SemanticType CheckPipeForward(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);

        if (leftType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // The pipe target is a callee, so it normalizes like one: `x |> (f)` and `x |> (f)(y)`
        // resolve exactly as their unparenthesized forms (#1170). Without this a parenthesized
        // target missed the symbol-resolution arms below and fell through to "whatever the
        // expression evaluates to" — which silently reinterpreted `x |> (double)` as a conversion
        // to the `double` primitive instead of a call to a user function named `double`.
        var target = UnwrapParenthesized(binOp.Right);

        // Case 1: x |> f(y, z) - right side is already a function call
        // We need to re-validate with x prepended to arguments
        if (target is FunctionCall funcCall)
        {
            return CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
        }

        // Case 2: x |> f - right side is an identifier or expression that should be callable
        // Pre-check: if right side is an identifier that resolves to a TypeSymbol, emit the
        // constructor-pipe error immediately. CheckExpression returns UnknownType for non-primitive
        // TypeSymbols, which would hide this case behind a silent early return.
        if (target is Identifier preId)
        {
            var preSymbol = _symbolTable.Lookup(preId.Name);
            if (preSymbol is TypeSymbol)
            {
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }
        }

        var rightType = CheckExpression(binOp.Right);

        if (rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Check if right side is a function type
        if (rightType is FunctionType ft)
        {
            // Validate that the function accepts leftType as first argument
            if (ft.ParameterTypes.Count < 1)
            {
                AddError($"Pipe target function takes no arguments, cannot pipe a value to it",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (!leftType.IsAssignableTo(ft.ParameterTypes[0]))
            {
                AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: binOp.Span);
                return SemanticType.Unknown;
            }

            return ft.ReturnType;
        }

        // If right side is an identifier, look up the function symbol
        if (target is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);

                if (requiredParamCount < 1)
                {
                    AddError($"Pipe target function '{id.Name}' takes no required arguments, cannot pipe a value to it",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                // Validate the piped value type matches first parameter
                var firstParam = funcSymbol.Parameters[0];
                if (!leftType.IsAssignableTo(firstParam.Type))
                {
                    AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: binOp.Span);
                    return SemanticType.Unknown;
                }

                // Check if remaining required args are satisfied (they must all have defaults)
                if (requiredParamCount > 1)
                {
                    AddError($"Function '{id.Name}' requires {requiredParamCount} arguments but only 1 is provided via pipe",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass → SomeClass(x)
                // This is allowed, handled similarly to function call
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            AddError($"'{id.Name}' is not callable",
                binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: binOp.Right.Span);
            return SemanticType.Unknown;
        }

        // Right side is some other expression that's not callable
        AddError($"Pipe target must be callable, got '{rightType.GetDisplayName()}'",
            binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a pipe forward where the right side is a function call.
    /// x |> f(y, z) → f(x, y, z) - prepend piped value to argument list.
    /// </summary>
    private SemanticType CheckPipeForwardWithFunctionCall(SemanticType pipedType, FunctionCall call, BinaryOp binOp)
    {
        // Get the function being called, dispatching on the canonical (paren-stripped) callee (#1170)
        var callee = UnwrapParenthesized(call.Function);
        var calleeType = CheckExpression(call.Function);

        // Collect existing argument types
        var existingArgTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            existingArgTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Build the full argument list: piped value + existing args
        var allArgTypes = new List<SemanticType> { pipedType };
        allArgTypes.AddRange(existingArgTypes);

        // Total argument count includes piped value, positional args, and keyword args
        var totalArgCount = allArgTypes.Count + kwargTypes.Count;

        // Try to resolve the function symbol for better validation
        if (callee is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Record the resolved call target for codegen
                _semanticInfo.SetCallTarget(call, funcSymbol);

                // Validate argument count considering variadic and keyword-only params
                var hasVariadicParam = funcSymbol.Parameters.Any(p => p.IsVariadic);
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                var totalParamCount = funcSymbol.Parameters.Count;
                var positionalParamCount = funcSymbol.Parameters.Count(p => !p.IsKeywordOnly);

                var tooFew = totalArgCount < requiredParamCount;
                var tooManyPositional = !hasVariadicParam && allArgTypes.Count > positionalParamCount;
                var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;

                if (tooFew || tooMany || tooManyPositional)
                {
                    if (hasVariadicParam)
                    {
                        AddError($"Function '{id.Name}' expects at least {requiredParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else if (requiredParamCount == totalParamCount)
                    {
                        AddError($"Function '{id.Name}' expects {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else
                    {
                        AddError($"Function '{id.Name}' expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    return SemanticType.Unknown;
                }

                // Validate positional argument types (piped value + call.Arguments)
                var variadicParamIndex = funcSymbol.Parameters.ToList().FindIndex(p => p.IsVariadic);
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    ParameterSymbol param;
                    if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                    {
                        param = funcSymbol.Parameters[variadicParamIndex];
                    }
                    else if (i < funcSymbol.Parameters.Count)
                    {
                        param = funcSymbol.Parameters[i];
                    }
                    else
                    {
                        break; // Shouldn't happen due to tooMany check
                    }

                    if (!argType.IsAssignableTo(param.Type))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{param.Name}' of type '{param.Type.GetDisplayName()}'",
                            argNode.LineStart,
                            argNode.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: argNode.Span);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                            span: kwarg.Span ?? kwarg.Value.Span);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally (including piped value)
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                                span: kwarg.Span ?? kwarg.Value.Span);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                                span: kwarg.Span ?? kwarg.Value.Span);
                        }
                    }
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol typeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass(y) → SomeClass(x, y)
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (symbol != null)
            {
                AddError($"'{id.Name}' is not callable",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                    span: call.Function.Span);
                return SemanticType.Unknown;
            }
        }

        // Fallback: check if callee is a FunctionType
        if (calleeType is FunctionType ft)
        {
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount} (including piped value)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                if (!allArgTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                    AddError($"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{ft.ParameterTypes[i].GetDisplayName()}' is expected",
                        argNode.LineStart,
                        argNode.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: argNode.Span);
                }
            }

            return ft.ReturnType;
        }

        AddError($"Pipe target must be callable, got '{calleeType.GetDisplayName()}'",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferUnaryOpType(unOp.Operator, operandType);

        // If type inference fails, report the error directly
        if (resultType == null)
        {
            AddError(
                $"Type '{operandType.GetDisplayName()}' does not support unary operator '{GetOperatorSymbol(unOp.Operator)}'",
                unOp.LineStart,
                unOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidUnaryOperation,
                span: unOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // A comparison chain like "a < b < c" has:
        // - Operands: [a, b, c]
        // - Operators: [LessThan, LessThan]
        // We need to validate each adjacent pair: (a < b) and (b < c)

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // Check all operands and build their types
        var operandTypes = new List<SemanticType>();
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            operandTypes.Add(CheckExpression(chain.Operands[i]));
        }

        // Validate each comparison pair
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var leftType = operandTypes[i];
            var rightType = operandTypes[i + 1];

            // Skip validation if either operand is Unknown to avoid cascading errors
            if (leftType is UnknownType || rightType is UnknownType)
            {
                continue;
            }

            // Map ComparisonOperator to BinaryOperator and validate
            var binaryOp = TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[i]);
            var resultType = _typeInference.InferBinaryOpType(binaryOp, leftType, rightType);

            // If type inference fails, report the error directly
            if (resultType == null)
            {
                AddError(
                    $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binaryOp)}' with operand of type '{rightType.GetDisplayName()}'",
                    chain.Operands[i].LineStart,
                    chain.Operands[i].ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    span: chain.Span);
            }
        }

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        CheckExpression(cond.Test);

        // Expression-level narrowing (#1080): the true arm is evaluated only when the condition holds,
        // so it sees the condition's positive narrowings; the false arm sees the negative narrowings.
        // Reads inside each arm record their accessor lowering via the narrowing context, exactly as
        // `and`-RHS does — codegen needs no special handling. The narrowings do not leak past the arm.
        var thenEntries = ExtractNarrowedTypes(cond.Test, true);
        var elseEntries = ExtractNarrowedTypes(cond.Test, false);

        SemanticType thenType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(thenEntries);
            thenType = CheckExpression(cond.ThenValue);
        }

        SemanticType elseType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(elseEntries);
            elseType = CheckExpression(cond.ElseValue);
        }

        // Return common type
        if (thenType.IsAssignableTo(elseType))
            return elseType;
        if (elseType.IsAssignableTo(thenType))
            return thenType;

        // Intentional Unknown without error: when then/else branch types are incompatible
        // (e.g., `1 if cond else "str"`), we return Unknown rather than emitting an error
        // because the LCA (least common ancestor) logic is limited. Mark as error recovery
        // to suppress SPY0907 — a proper fix would compute LCA or emit a type mismatch error.
        MarkExpressionAsErrorRecovery(cond);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a type coercion expression (value to Type).
    /// Validates that the coercion is valid per the language specification.
    /// </summary>
    private SemanticType CheckTypeCoercion(TypeCoercion coercion)
    {
        var sourceType = CheckExpression(coercion.Value);

        // The `as?`/`as!` operators own their failure mode, so the target must be written
        // non-nullable — the operator supplies the optionality. Reject `x as? T?` / `x as! T?`.
        if (coercion.TargetType.IsOptional)
        {
            var op = coercion.Mode == CastFailureMode.Null ? "as?" : "as!";
            AddError(
                $"Redundant '?' on the target of '{op}': the operator already determines the " +
                $"failure mode. Drop the '?' — write '{op} {coercion.TargetType.Name}'.",
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.Semantic.RedundantNullableCastTarget,
                span: coercion.Span);
        }

        // Classify the target the same way a type test is classified: `as?`/`as!` name a runtime type
        // just as `is` does, and a bare generic name denotes none (#1235). Erasure is DISALLOWED here
        // because this site binds a VALUE — lowering `as? list` to the non-generic Sharpy.IList would
        // hand back something other than the type this expression is given, so a bare collection name
        // is filled from the source type or refused like any other open generic.
        //
        // The decided type also becomes the semantic result type below. Without that, filling
        // `b as? Box` to `Box[int]` in codegen while the checker still said `Box` would put the two
        // back into the disagreement this batch exists to remove.
        var decidedTarget = ClassifyTypeTestAnnotation(
            coercion.TargetType,
            lodgeOn: coercion.TargetType,
            subjectType: sourceType,
            siteNoun: "cast",
            erasure: CollectionErasure.Disallowed);

        // Resolve the target type. For the Null failure mode (`as?`) the operator supplies the
        // optionality, so the non-nullable target is promoted to T? here to form the result type.
        var targetAnnotation = coercion.TargetType;
        if (coercion.Mode == CastFailureMode.Null && !targetAnnotation.IsOptional)
        {
            targetAnnotation = targetAnnotation with { IsOptional = true };
        }
        var targetType = decidedTarget != null
            ? (coercion.Mode == CastFailureMode.Null
                ? new OptionalType { UnderlyingType = decidedTarget }
                : decidedTarget)
            : _typeResolver.ResolveTypeAnnotation(targetAnnotation);

        // If either type is unknown, skip validation to avoid cascading errors
        if (sourceType is UnknownType || targetType is UnknownType)
        {
            return targetType;
        }

        // Get the underlying target type (strip nullable/optional wrapper if present)
        var underlyingTargetType = targetType switch
        {
            NullableType nullable => nullable.UnderlyingType,
            OptionalType optional => optional.UnderlyingType,
            _ => targetType
        };

        // For the failable form (`as? T`), decide the emission shape here so the emitter never
        // inspects operand types. When the source and stripped target are both plain numeric primitives,
        // record a numeric lowering (widening/identity ⇒ AlwaysFits; narrowing ⇒ a range-checked helper).
        // Any other source keeps the emitter's default type-pattern lowering, which is the only correct
        // shape for object/reference/optional sources but is uncompilable (CS8121) for concrete numerics
        // — the gap #1110 closes.
        if (coercion.Mode == CastFailureMode.Null)
        {
            var lowering = ClassifyNumericSafeCast(sourceType, underlyingTargetType);
            if (lowering != null)
            {
                _semanticInfo.SetTypeCoercionLowering(coercion, lowering);
            }
        }

        // Validate the coercion
        ValidateTypeCoercion(coercion, sourceType, underlyingTargetType);

        return targetType;
    }

    /// <summary>
    /// Classifies a failable numeric cast into its emission shape, or returns <c>null</c> when the
    /// numeric lowering does not apply (leaving the default type-pattern lowering in place). Applies only
    /// when BOTH the source and the stripped target are plain numeric <c>int</c>/<c>long</c>/
    /// <c>float32</c>/<c>double</c> — object, reference, optional/nullable, type-parameter, and other
    /// numeric-family (byte/short/uint/decimal/…) sources are intentionally excluded so their generated
    /// C# stays byte-for-byte unchanged (#1110).
    /// </summary>
    private static TypeCoercionLowering? ClassifyNumericSafeCast(SemanticType source, SemanticType target)
    {
        var srcClr = PrimitiveCatalog.GetPrimitiveInfo(source)?.ClrType;
        var tgtClr = PrimitiveCatalog.GetPrimitiveInfo(target)?.ClrType;

        if (!IsSafeCastNumeric(srcClr) || !IsSafeCastNumeric(tgtClr))
        {
            return null;
        }

        // Narrowing to int: long/float32/double → int is range-checked. int → int is identity.
        if (tgtClr == typeof(int))
        {
            return srcClr == typeof(int)
                ? new TypeCoercionLowering(TypeCoercionLoweringKind.NumericAlwaysFits)
                : new TypeCoercionLowering(TypeCoercionLoweringKind.NumericRangeChecked, "ToIntOrNone");
        }

        // Narrowing to long: float32/double → long is range-checked. int/long → long widens/identity.
        if (tgtClr == typeof(long))
        {
            return srcClr == typeof(float) || srcClr == typeof(double)
                ? new TypeCoercionLowering(TypeCoercionLoweringKind.NumericRangeChecked, "ToLongOrNone")
                : new TypeCoercionLowering(TypeCoercionLoweringKind.NumericAlwaysFits);
        }

        // Target is float32 or double: every int/long/float32/double source fits (widening/identity, or
        // double → float32 which maps overflow to ±∞ and preserves NaN, both representable in float32).
        return new TypeCoercionLowering(TypeCoercionLoweringKind.NumericAlwaysFits);
    }

    /// <summary>
    /// True for exactly the four plain numeric CLR types the safe-cast numeric lowering handles:
    /// <c>int</c>, <c>long</c>, <c>float</c> (Sharpy <c>float32</c>), <c>double</c> (Sharpy <c>float</c>).
    /// </summary>
    private static bool IsSafeCastNumeric(System.Type? clr)
        => clr == typeof(int) || clr == typeof(long) || clr == typeof(float) || clr == typeof(double);

    /// <summary>
    /// Validates that a type coercion is valid per the language specification.
    /// Reports errors for invalid casts.
    /// </summary>
    private void ValidateTypeCoercion(TypeCoercion coercion, SemanticType sourceType, SemanticType targetType)
    {
        // Unboxing: object to any type is valid (runtime check) - check this first
        if (IsObjectType(sourceType))
        {
            return; // Valid
        }

        // Numeric to numeric conversions are always valid (may throw at runtime for narrowing)
        if (PrimitiveCatalog.IsNumeric(sourceType) && PrimitiveCatalog.IsNumeric(targetType))
        {
            return; // Valid
        }

        // Check for invalid numeric/bool to string conversion
        // This is a common mistake - users should use str(x) instead
        if (IsStringType(targetType))
        {
            var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(sourceType);
            if (sourceInfo != null && sourceInfo.ClrType != typeof(string))
            {
                // Source is a primitive but not string - reject
                AddError(
                    $"Cannot cast '{sourceType.GetDisplayName()}' to 'str'. Use str(...) instead.",
                    coercion.LineStart, coercion.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidCast,
                    span: coercion.Span);
                return;
            }
        }

        // Check for user-defined __explicit__ conversion operators
        if (HasUserDefinedConversion(sourceType, targetType, DunderNames.Explicit)
            || HasUserDefinedConversion(targetType, sourceType, DunderNames.Explicit))
        {
            return; // Valid — C# will invoke the user-defined explicit operator
        }

        // Check for valid reference type casts (inheritance relationship or interface implementation)
        if (!CanPotentiallyCast(sourceType, targetType))
        {
            AddError(
                $"Cannot cast '{sourceType.GetDisplayName()}' to '{targetType.GetDisplayName()}' (no inheritance relationship).",
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidCast,
                span: coercion.Span);
        }
    }

    /// <summary>
    /// Returns true if the type is the str/string type.
    /// </summary>
    private static bool IsStringType(SemanticType type)
    {
        return type is BuiltinType builtin && (builtin.Name == BuiltinNames.Str || builtin.Name == "string");
    }

    /// <summary>
    /// Returns true if the type is the object type.
    /// </summary>
    private static bool IsObjectType(SemanticType type)
    {
        return type is BuiltinType { Name: "object" } or UserDefinedType { Name: "object" };
    }

    /// <summary>
    /// Determines if a cast between two types COULD potentially succeed at runtime.
    /// Returns true if there's an inheritance relationship, interface implementation, or unboxing potential.
    /// Returns false if the cast is statically impossible.
    /// </summary>
    private bool CanPotentiallyCast(SemanticType source, SemanticType target)
    {
        // Same type is always castable
        if (source.Equals(target))
            return true;

        // Both must be user-defined types for inheritance checks
        if (source is UserDefinedType sourceUdt && target is UserDefinedType targetUdt)
        {
            // Check if source inherits from target (downcast - always safe)
            if (InheritsFrom(sourceUdt.Symbol, targetUdt.Symbol))
                return true;

            // Check if target inherits from source (upcast - runtime check)
            if (InheritsFrom(targetUdt.Symbol, sourceUdt.Symbol))
                return true;

            // Check if target is an interface that could be implemented
            if (targetUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // Check if source is an interface that the target could implement
            if (sourceUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // No relationship found
            return false;
        }

        // Interface casting is always potentially valid at runtime
        if (source is UserDefinedType && target is UserDefinedType targetType && targetType.Symbol?.TypeKind == TypeKind.Interface)
            return true;

        // Unboxing from object is always valid
        if (IsObjectType(source))
            return true;

        // Boxing to object is always valid
        if (IsObjectType(target))
            return true;

        // For generic types, check the base definition
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            // Same generic definition with potentially different type args
            if (sourceGeneric.GenericDefinition?.Name == targetGeneric.GenericDefinition?.Name)
                return true;
        }

        // Default: allow if types don't fit the checked categories (to be conservative)
        // This handles edge cases and allows the C# compiler to do final validation
        return true;
    }

    private bool HasUserDefinedConversion(SemanticType sourceType, SemanticType targetType, string dunderName)
    {
        var typeSymbol = sourceType switch
        {
            UserDefinedType udt => udt.Symbol,
            _ => null
        };

        if (typeSymbol == null)
            return false;

        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name != dunderName || !method.IsStatic)
                continue;

            if (method.Parameters.Count == 1 && method.ReturnType != null)
            {
                if (method.ReturnType.Equals(targetType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type symbol inherits from another type symbol (directly or indirectly).
    /// </summary>
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)
        => TypeHierarchyService.InheritsFrom(derived, baseType, SemanticBinding);

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        var valueType = CheckExpression(typeCheck.Value);
        _typeResolver.ResolveTypeAnnotation(typeCheck.CheckType);

        // `x is T` is a runtime type test, exactly like isinstance(x, T), so it answers to the same
        // rule: the operand is classified once here and the emitter applies the decision, instead of
        // mapping the written annotation and emitting an open generic (CS0305 behind SPY0908, #1235).
        // Erasure is allowed because this site yields a boolean — `x is list` must mean what
        // `isinstance(x, list)` means, and letting them differ is the disagreement
        // TypeTestNarrowingAgreementTests exists to forbid.
        ClassifyTypeTestAnnotation(
            typeCheck.CheckType,
            lodgeOn: typeCheck.CheckType,
            subjectType: valueType,
            siteNoun: "type test",
            erasure: CollectionErasure.Allowed);

        return SemanticType.Bool;
    }

    /// <summary>
    /// Type-checks a maybe expression: maybe expr.
    /// The operand must be a NullableType (T | None). The result is OptionalType wrapping the underlying type.
    /// </summary>
    private SemanticType CheckMaybeExpression(MaybeExpression maybeExpr)
    {
        var operandType = CheckExpression(maybeExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        if (operandType is not NullableType nullable)
        {
            AddError(
                $"'maybe' expression requires a nullable type (T | None), but got '{operandType.GetDisplayName()}'",
                maybeExpr.LineStart, maybeExpr.ColumnStart, code: DiagnosticCodes.Semantic.InvalidMaybeExpression,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        if (nullable.UnderlyingType is TypeParameterType typeParam
            && !typeParam.Constraints.Any(c => c is ClassConstraint or StructConstraint))
        {
            AddError(
                $"'maybe' cannot be used with unconstrained generic type parameter '{typeParam.Name}'. The type parameter must be constrained to either a reference type or value type.",
                maybeExpr.LineStart, maybeExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.MaybeOnUnconstrainedTypeParameter,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        return new OptionalType { UnderlyingType = nullable.UnderlyingType };
    }

    /// <summary>
    /// Type-checks a try expression: try expr or try[ExceptionType] expr.
    /// Wraps the operand in Result[T, E] where T is the operand type and E is the exception type.
    /// Default E is Exception, except for 'to' expressions where it's InvalidCastException.
    /// </summary>
    private SemanticType CheckTryExpression(TryExpression tryExpr)
    {
        var operandType = CheckExpression(tryExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Determine the error type
        SemanticType errorType;
        if (tryExpr.ExceptionTypes.Length >= 1)
        {
            // Explicit exception type(s): try[E] expr or try[A | B | C] expr.
            // Resolve each, validate it inherits from Exception, then compute the
            // common base type (most specific shared ancestor) for the Result error.
            var resolved = new List<SemanticType>(tryExpr.ExceptionTypes.Length);
            var exceptionSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
            foreach (var typeAnnotation in tryExpr.ExceptionTypes)
            {
                var resolvedType = _typeResolver.ResolveTypeAnnotation(typeAnnotation);
                if (resolvedType is UnknownType)
                {
                    resolved.Add(resolvedType);
                    continue;
                }

                // Validate it is an Exception subclass (skip when we can't find Exception
                // symbol — fail open so unrelated environments still compile).
                if (exceptionSymbol != null && !IsExceptionSubtype(resolvedType, exceptionSymbol))
                {
                    AddError(
                        $"Type '{typeAnnotation.Name}' in 'try' expression must be a subclass of 'Exception'",
                        typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                        code: DiagnosticCodes.Semantic.TryExceptionTypeNotException,
                        span: typeAnnotation.Span);
                }

                resolved.Add(resolvedType);
            }

            errorType = resolved.Count == 1
                ? resolved[0]
                : FindCommonExceptionBase(resolved, exceptionSymbol);
        }
        else if (tryExpr.Operand is TypeCoercion)
        {
            // Special case: try x to Cat → Result[Cat, InvalidCastException]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("InvalidCastException");
            errorType = new UserDefinedType { Name = "InvalidCastException", Symbol = clrSymbol };
        }
        else if (_expectedType is ResultType expectedResult)
        {
            // RFC 3721: infer error type from expected type context (return statement
            // or variable annotation with Result type)
            errorType = expectedResult.ErrorType;
        }
        else if (_currentFunctionReturnType is ResultType enclosingResult
            && _expectedType == null)
        {
            // RFC 3721: infer error type from enclosing function's Result return type
            // Only when there's no explicit type context (e.g., variable annotation)
            errorType = enclosingResult.ErrorType;
        }
        else
        {
            // Default: try expr → Result[T, Exception]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
            errorType = new UserDefinedType { Name = "Exception", Symbol = clrSymbol };
        }

        return new ResultType { OkType = operandType, ErrorType = errorType };
    }

    /// <summary>
    /// Returns true when <paramref name="type"/> is the Exception type itself or a
    /// subclass of it. Walks the base chain via <see cref="TypeHierarchyService"/>.
    /// <para>
    /// A closed generic exception — <c>MyError[int]</c> for <c>class MyError[T](Exception)</c> — is a
    /// <see cref="GenericType"/>, not a <see cref="UserDefinedType"/>, and its derivation is a property
    /// of the definition, so the check reads <see cref="GenericType.GenericDefinition"/>. Without this
    /// the closed spelling that <c>except</c>'s open-generic refusal (SPY0345) tells the user to write
    /// was itself rejected as not-an-Exception — found by running that message's own example.
    /// </para>
    /// </summary>
    private static bool IsExceptionSubtype(SemanticType type, TypeSymbol exceptionSymbol)
    {
        var symbol = type switch
        {
            UserDefinedType { Symbol: { } udtSymbol } => udtSymbol,
            GenericType { GenericDefinition: { } definition } => definition,
            _ => null
        };

        if (symbol == null)
        {
            return false;
        }

        if (TypeHierarchyService.IsSameType(symbol, exceptionSymbol))
        {
            return true;
        }

        return TypeHierarchyService.InheritsFrom(symbol, exceptionSymbol);
    }

    /// <summary>
    /// Computes the most specific common ancestor for a set of exception types used
    /// in a multi-exception try expression (try[A | B | C]). Falls back to the
    /// <c>Exception</c> base type when there is no shared ancestor lower in the chain.
    /// Uses the CLR type chain when available because exception types discovered from
    /// CLR metadata typically have <c>Symbol.BaseType</c> unset.
    /// </summary>
    private SemanticType FindCommonExceptionBase(IReadOnlyList<SemanticType> exceptionTypes, TypeSymbol? exceptionSymbol)
    {
        SemanticType fallback = exceptionSymbol != null
            ? new UserDefinedType { Name = "Exception", Symbol = exceptionSymbol }
            : (exceptionTypes.FirstOrDefault(t => t is not UnknownType) ?? SemanticType.Unknown);

        // Start with the ancestor chain of the first type and intersect with each subsequent type's chain.
        IReadOnlyList<SemanticType>? commonChain = null;
        foreach (var t in exceptionTypes)
        {
            if (t is UnknownType)
            {
                continue;
            }

            var chain = GetExceptionAncestorChain(t);
            if (commonChain == null)
            {
                commonChain = chain;
                continue;
            }

            // Keep only ancestors that appear in 'chain' (preserving most-specific order from commonChain).
            var filtered = new List<SemanticType>();
            foreach (var ancestor in commonChain)
            {
                if (chain.Any(c => SemanticTypesAreSame(c, ancestor)))
                {
                    filtered.Add(ancestor);
                }
            }
            commonChain = filtered;
        }

        if (commonChain == null || commonChain.Count == 0)
        {
            return fallback;
        }

        // First element is the most specific common ancestor
        return commonChain[0];
    }

    /// <summary>
    /// Walks the ancestor chain for an exception SemanticType, preferring symbol-based
    /// inheritance but falling back to the CLR <see cref="System.Type"/> chain for types
    /// discovered from CLR metadata (where <c>Symbol.BaseType</c> is not set). The
    /// returned chain is ordered from most specific (the type itself) to least specific.
    /// </summary>
    private IReadOnlyList<SemanticType> GetExceptionAncestorChain(SemanticType type)
    {
        var chain = new List<SemanticType> { type };

        if (type is UserDefinedType { Symbol: { } symbol })
        {
            // Prefer the symbol-based chain when BaseType is populated (user-defined types).
            if (symbol.BaseType != null)
            {
                return TypeHierarchyService.GetAncestorChain(type);
            }

            // Otherwise walk the CLR base chain (handles CLR-discovered exception types
            // like Sharpy.ValueError where Symbol.BaseType is not set).
            var clrType = symbol.ClrType;
            while (clrType?.BaseType != null)
            {
                clrType = clrType.BaseType;
                var ancestorSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType(clrType.Name);
                chain.Add(new UserDefinedType
                {
                    Name = clrType.Name,
                    Symbol = ancestorSymbol
                });
            }
        }

        return chain;
    }

    /// <summary>
    /// Compares two semantic types for "same type" purposes during ancestor intersection.
    /// Uses <see cref="TypeHierarchyService.IsSameType"/> for user-defined types and
    /// falls back to record equality for others (BuiltinType, Object, etc).
    /// </summary>
    private static bool SemanticTypesAreSame(SemanticType a, SemanticType b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is UserDefinedType ua && b is UserDefinedType ub)
        {
            if (ua.Symbol != null && ub.Symbol != null)
            {
                return TypeHierarchyService.IsSameType(ua.Symbol, ub.Symbol);
            }
            return ua.Name == ub.Name;
        }
        return a.Equals(b);
    }

    /// <summary>
    /// Type-checks a postfix ? operator (expr?). Unwraps Result[T, E] to T (propagating E on error)
    /// or Optional[T] to T (propagating None on absence). The enclosing function must return a
    /// compatible Result or Optional type.
    /// </summary>
    private SemanticType CheckQuestionMarkExpression(QuestionMarkExpression qm)
    {
        // 1. Must be inside a function
        if (_currentFunctionReturnType == null)
        {
            AddError(
                "'?' operator can only be used inside a function",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Validation.QuestionMarkOutsideFunction,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 2. Disallow in finally blocks
        if (_inFinally)
        {
            AddError(
                "'?' operator cannot be used inside a 'finally' block",
                qm.LineStart, qm.ColumnStart,
                code: DiagnosticCodes.Semantic.QuestionMarkInFinally,
                span: qm.Span);
            return SemanticType.Unknown;
        }

        // 3. Type-check the operand
        var operandType = CheckExpression(qm.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // 4. Handle Result<T, E>
        if (operandType is ResultType result)
        {
            if (_currentFunctionReturnType is ResultType returnResult)
            {
                if (!IsAssignable(result.ErrorType, returnResult.ErrorType))
                {
                    AddError(
                        $"'?' error type '{result.ErrorType.GetDisplayName()}' is not assignable to function return error type '{returnResult.ErrorType.GetDisplayName()}'",
                        qm.LineStart, qm.ColumnStart,
                        code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                        span: qm.Span);
                    return SemanticType.Unknown;
                }
                return result.OkType;
            }
            else
            {
                AddError(
                    $"'?' on Result requires function to return Result, but return type is '{_currentFunctionReturnType.GetDisplayName()}'",
                    qm.LineStart, qm.ColumnStart,
                    code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                    span: qm.Span);
                return SemanticType.Unknown;
            }
        }

        // 5. Handle Optional<T>
        if (operandType is OptionalType optional)
        {
            if (_currentFunctionReturnType is OptionalType)
            {
                return optional.UnderlyingType;
            }
            else
            {
                AddError(
                    $"'?' on Optional requires function to return Optional, but return type is '{_currentFunctionReturnType.GetDisplayName()}'",
                    qm.LineStart, qm.ColumnStart,
                    code: DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
                    span: qm.Span);
                return SemanticType.Unknown;
            }
        }

        // 6. Not Result or Optional
        AddError(
            $"'?' operator requires Result or Optional type, got '{operandType.GetDisplayName()}'",
            qm.LineStart, qm.ColumnStart,
            code: DiagnosticCodes.Validation.QuestionMarkNotResultOrOptional,
            span: qm.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Extract narrowed types from a conditional expression
    /// </summary>

    /// <summary>
    /// Gets the human-readable symbol for a binary operator.
    /// </summary>
    private static string GetOperatorSymbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.FloorDivide => "//",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "**",
        BinaryOperator.MatMul => "@",
        BinaryOperator.BitwiseAnd => "&",
        BinaryOperator.BitwiseOr => "|",
        BinaryOperator.BitwiseXor => "^",
        BinaryOperator.LeftShift => "<<",
        BinaryOperator.RightShift => ">>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.And => "and",
        BinaryOperator.Or => "or",
        BinaryOperator.Is => "is",
        BinaryOperator.IsNot => "is not",
        BinaryOperator.In => "in",
        BinaryOperator.NotIn => "not in",
        BinaryOperator.NullCoalesce => "??",
        BinaryOperator.PipeForward => "|>",
        _ => op.ToString()
    };

    /// <summary>
    /// Gets the human-readable symbol for a unary operator.
    /// </summary>
    private static string GetOperatorSymbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Minus => "-",
        UnaryOperator.Plus => "+",
        UnaryOperator.Not => "not",
        UnaryOperator.BitwiseNot => "~",
        _ => op.ToString()
    };
}

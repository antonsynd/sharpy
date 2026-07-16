using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering.Passes;

/// <summary>
/// Constant-folding IR pass (E3, #640) behind <c>opt_const_fold</c>. Reduces compile-time-constant
/// arithmetic / comparison / boolean / bitwise / unary expression subtrees to an
/// <see cref="IrConstant"/>, using <b>exactly the emitted C#'s semantics</b> (Axiom 1): unchecked wrap
/// at the mapped integer width (<c>int</c> = int32, <c>long</c> = int64), IEEE doubles, and logical
/// short-circuit over already-constant operands.
/// </summary>
/// <remarks>
/// <para>
/// Only a fold whose root AST node is a <see cref="BinaryOp"/> or <see cref="UnaryOp"/> — i.e. an
/// operation that genuinely <em>reduces</em> — records a <see cref="IrConstant.SourceAst"/>, so the
/// pass manager exposes only reductions to the emitter (a lone literal folds internally to enable its
/// enclosing operation but is never re-emitted, keeping literal round-trips exact).
/// </para>
/// <para>
/// Deliberately <b>not</b> folded in v1 (recorded on #640): division / floor-division / modulo (Python
/// raises on a zero divisor, so these stay runtime code — Design Decision 6's trap rule), <c>**</c>
/// (already folded by the initial lowering), <c>==</c>/<c>!=</c> (a typed <see cref="IrEqualityComparison"/>,
/// not an opaque node), shifts, strings, and any expression nested inside a typed IR node.
/// </para>
/// </remarks>
internal sealed class ConstFoldPass : IIrPass
{
    public string FlagName => "opt_const_fold";

    public IrModule Rewrite(IrModule module, IrRewriteContext context)
    {
        var body = ImmutableArray.CreateBuilder<IrStatement>(module.Body.Length);
        var changed = false;
        foreach (var statement in module.Body)
        {
            var folded = (IrStatement)FoldNode(statement);
            body.Add(folded);
            changed |= !ReferenceEquals(folded, statement);
        }

        return changed ? module with { Body = body.ToImmutable() } : module;
    }

    /// <summary>
    /// Folds an IR node bottom-up. Opaque expressions whose folded operands are all constant collapse
    /// to an <see cref="IrConstant"/>; every other node is rebuilt with its folded children (so nested
    /// constants inside otherwise-dynamic expressions/statements still reduce). Typed IR nodes are
    /// returned as-is (v1 does not fold inside them).
    /// </summary>
    private IrNode FoldNode(IrNode node)
    {
        switch (node)
        {
            case IrConstant:
                return node;

            case IrOpaqueExpression opaque:
            {
                var children = FoldChildren(opaque.Children, out var childrenChanged);
                if (TryFoldExpression(opaque.Ast, children, opaque.Type, opaque.Span) is { } constant)
                    return constant;
                return childrenChanged
                    ? new IrOpaqueExpression(opaque.Ast, opaque.Type, opaque.Span, children)
                    : opaque;
            }

            case IrOpaqueStatement opaqueStatement:
            {
                var children = FoldChildren(opaqueStatement.Children, out var childrenChanged);
                return childrenChanged
                    ? new IrOpaqueStatement(opaqueStatement.Ast, opaqueStatement.Span, children)
                    : opaqueStatement;
            }

            default:
                // Typed nodes (equality, index, member, lowered loop, scope guard, with-item): v1 does
                // not fold inside them, so leave them untouched to stay conservative and pure.
                return node;
        }
    }

    private ImmutableArray<IrNode> FoldChildren(ImmutableArray<IrNode> children, out bool changed)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            var folded = FoldNode(children[i]);
            if (!ReferenceEquals(folded, children[i]))
                (builder ??= children.ToBuilder())[i] = folded;
        }

        changed = builder is not null;
        return builder is null ? children : builder.ToImmutable();
    }

    /// <summary>
    /// Produces the folded <see cref="IrConstant"/> for <paramref name="ast"/> given its folded
    /// <paramref name="children"/>, or <c>null</c> when it cannot be folded. Literals fold with
    /// <c>SourceAst == null</c> (internal, not re-emitted); operations fold with
    /// <c>SourceAst == ast</c> (recorded for the emitter).
    /// </summary>
    private static IrConstant? TryFoldExpression(Expression ast, ImmutableArray<IrNode> children, SemanticType? type, TextSpan span)
    {
        switch (ast)
        {
            case IntegerLiteral when IntegerConstantEvaluator.TryGetConstantInteger(ast, out var big)
                && big >= long.MinValue && big <= long.MaxValue:
                return new IrConstant((long)big, type, span);

            case FloatLiteral floatLit when (type == SemanticType.Float || type == SemanticType.Double)
                && double.TryParse(floatLit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d):
                return new IrConstant(d, type, span);

            case BooleanLiteral boolLit:
                return new IrConstant(boolLit.Value, type, span);

            case UnaryOp unary when children.Length == 1 && children[0] is IrConstant operand:
                return TryFoldUnary(unary.Operator, operand, type, span, ast);

            case BinaryOp binary when children.Length == 2
                && children[0] is IrConstant left && children[1] is IrConstant right:
                return TryFoldBinary(binary.Operator, left, right, type, span, ast);

            default:
                return null;
        }
    }

    private static IrConstant? TryFoldUnary(UnaryOperator op, IrConstant operand, SemanticType? type, TextSpan span, Expression ast)
    {
        switch (op)
        {
            case UnaryOperator.Minus when IsInt(type) && operand.Value is long l:
                return new IrConstant(WrapInt(-l, type), type, span, ast);
            case UnaryOperator.Minus when IsDouble(type) && TryDouble(operand, out var d):
                return new IrConstant(-d, type, span, ast);
            case UnaryOperator.Not when operand.Value is bool b:
                return new IrConstant(!b, type, span, ast);
            case UnaryOperator.BitwiseNot when IsInt(type) && operand.Value is long bl:
                return new IrConstant(WrapInt(~bl, type), type, span, ast);
            default:
                return null;
        }
    }

    private static IrConstant? TryFoldBinary(BinaryOperator op, IrConstant left, IrConstant right, SemanticType? type, TextSpan span, Expression ast)
    {
        // Integer arithmetic / bitwise: unchecked wrap at the mapped width.
        if (IsInt(type) && left.Value is long a && right.Value is long b)
        {
            long? r = op switch
            {
                BinaryOperator.Add => unchecked(a + b),
                BinaryOperator.Subtract => unchecked(a - b),
                BinaryOperator.Multiply => unchecked(a * b),
                BinaryOperator.BitwiseAnd => a & b,
                BinaryOperator.BitwiseOr => a | b,
                BinaryOperator.BitwiseXor => a ^ b,
                _ => null,
            };
            return r is { } value ? new IrConstant(WrapInt(value, type), type, span, ast) : null;
        }

        // Double arithmetic (IEEE). Division stays runtime code (Python raises on /0). Only fold to a
        // finite result — inf/NaN have no C# literal form, so they stay runtime code.
        if (IsDouble(type) && TryDouble(left, out var da) && TryDouble(right, out var db))
        {
            double? r = op switch
            {
                BinaryOperator.Add => da + db,
                BinaryOperator.Subtract => da - db,
                BinaryOperator.Multiply => da * db,
                _ => null,
            };
            return r is { } value && double.IsFinite(value) ? new IrConstant(value, type, span, ast) : null;
        }

        // Numeric comparisons → bool. Compare as double when either side is a double, else as long.
        if (type == SemanticType.Bool && IsNumeric(left) && IsNumeric(right)
            && op is BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual)
        {
            if (left.Value is long la && right.Value is long lb)
                return new IrConstant(CompareLong(op, la, lb), type, span, ast);
            if (TryDouble(left, out var cda) && TryDouble(right, out var cdb))
                return new IrConstant(CompareDouble(op, cda, cdb), type, span, ast);
        }

        // Logical short-circuit over already-constant operands (both folded, so plain &&/||).
        if (type == SemanticType.Bool && left.Value is bool lbool && right.Value is bool rbool)
        {
            return op switch
            {
                BinaryOperator.And => new IrConstant(lbool && rbool, type, span, ast),
                BinaryOperator.Or => new IrConstant(lbool || rbool, type, span, ast),
                _ => null,
            };
        }

        return null;
    }

    private static bool IsInt(SemanticType? type) => type == SemanticType.Int || type == SemanticType.Long;

    private static bool IsDouble(SemanticType? type) => type == SemanticType.Float || type == SemanticType.Double;

    private static bool IsNumeric(IrConstant c) => c.Value is long or double;

    /// <summary>Wraps a folded integer to its mapped C# width (int32 for <c>int</c>, int64 for <c>long</c>).</summary>
    private static long WrapInt(long value, SemanticType? type) => type == SemanticType.Long ? value : unchecked((int)value);

    private static bool TryDouble(IrConstant c, out double value)
    {
        switch (c.Value)
        {
            case double d: value = d; return true;
            case long l: value = l; return true;
            default: value = 0; return false;
        }
    }

    private static bool CompareLong(BinaryOperator op, long a, long b) => op switch
    {
        BinaryOperator.LessThan => a < b,
        BinaryOperator.LessThanOrEqual => a <= b,
        BinaryOperator.GreaterThan => a > b,
        BinaryOperator.GreaterThanOrEqual => a >= b,
        _ => false,
    };

    private static bool CompareDouble(BinaryOperator op, double a, double b) => op switch
    {
        BinaryOperator.LessThan => a < b,
        BinaryOperator.LessThanOrEqual => a <= b,
        BinaryOperator.GreaterThan => a > b,
        BinaryOperator.GreaterThanOrEqual => a >= b,
        _ => false,
    };
}

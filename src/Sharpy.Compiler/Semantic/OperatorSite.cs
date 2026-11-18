using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// One operator application — a <see cref="BinaryOp"/>, or one link of a <see cref="ComparisonChain"/>
/// (P93 D3, #2648): the operator, its two operand nodes, and the position a diagnostic about it is reported
/// at. A chain link <c>Operands[i] op Operands[i+1]</c> is its binary twin, so the type checker's link routine
/// and the validators decide it from this one shape whichever node it was written as; the position is the
/// twin's (the left operand's start, spanning both operands).
/// </summary>
internal readonly record struct OperatorSite(
    BinaryOperator Operator, Expression Left, Expression Right, int Line, int Column, TextSpan? Span)
{
    /// <summary>The site of a binary operator, reported at the node itself.</summary>
    public static OperatorSite Of(BinaryOp binOp)
        => new(binOp.Operator, binOp.Left, binOp.Right, binOp.LineStart, binOp.ColumnStart, binOp.Span);

    /// <summary>
    /// The site of link <paramref name="index"/> of <paramref name="chain"/>: <c>Operands[index]</c>
    /// <c>Operators[index]</c> <c>Operands[index + 1]</c>, reported where the binary form would be.
    /// </summary>
    public static OperatorSite OfLink(ComparisonChain chain, int index)
    {
        var left = chain.Operands[index];
        var right = chain.Operands[index + 1];
        TextSpan? span = left.Span is { } leftSpan && right.Span is { } rightSpan && rightSpan.End >= leftSpan.Start
            ? TextSpan.FromBounds(leftSpan.Start, rightSpan.End)
            : chain.Span;
        return new OperatorSite(
            TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[index]),
            left, right, left.LineStart, left.ColumnStart, span);
    }

    /// <summary>
    /// Every site of <paramref name="chain"/>, one per link, in source order — what a validator visits
    /// for a chain so each link draws its binary twin's verdict.
    /// </summary>
    public static IEnumerable<OperatorSite> LinksOf(ComparisonChain chain)
    {
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
            yield break;
        for (var i = 0; i < chain.Operators.Length; i++)
            yield return OfLink(chain, i);
    }
}

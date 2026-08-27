using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Emitter-side mutation tests for the operator lowering facts (plan-c6ae1b Phase 7, #1623, #1642):
/// the SAME AST emitted under different recorded tags must produce different C#, and a shape that
/// always needs a fact must throw when none is recorded — the emitter switches on the tag and never
/// re-derives the decision from operand types. The semantic side is <c>OperatorLoweringRecordingTests</c>.
/// </summary>
public class RoslynEmitterOperatorLoweringTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;
    private readonly MethodInfo _generateExpression;

    public RoslynEmitterOperatorLoweringTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
        _generateExpression = typeof(RoslynEmitter).GetMethod(
            "GenerateExpression", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateExpression method not found");
    }

    private string Emit(Expression expr)
    {
        try
        {
            var result = (ExpressionSyntax)_generateExpression.Invoke(_emitter, new object[] { expr })!;
            return result.NormalizeWhitespace().ToFullString();
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    private static Identifier Id(string name) => new() { Name = name };

    private static ComparisonChain Chain(ComparisonOperator op, params Expression[] operands) => new()
    {
        Operands = operands.ToImmutableArray(),
        Operators = Enumerable.Repeat(op, operands.Length - 1).ToImmutableArray(),
    };

    private static ComparisonChainLowering Links(params ComparisonLinkLowering[] links)
        => new(links.ToImmutableArray());

    private static ComparisonLinkLowering Ordering(OperatorLoweringKind kind) => new(kind, null);

    private static ComparisonLinkLowering Equality(BinaryOpLowering strategy)
        => new(OperatorLoweringKind.Native, strategy);

    #region Comparison chains (#1642)

    [Fact]
    public void ComparisonChain_NativeLinks_EmitNativeOperators()
    {
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Ordering(OperatorLoweringKind.Native), Ordering(OperatorLoweringKind.Native)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be("a < b && b < c");
    }

    [Fact]
    public void ComparisonChain_StringOrdinalLinks_EmitStringCompare_SameAstAsNative()
    {
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Ordering(OperatorLoweringKind.StringOrdinalCompare), Ordering(OperatorLoweringKind.StringOrdinalCompare)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be(
            "string.Compare(a, b, System.StringComparison.Ordinal) < 0 && "
            + "string.Compare(b, c, System.StringComparison.Ordinal) < 0");
    }

    [Fact]
    public void ComparisonChain_TypeParameterLinks_EmitCompareTo_SameAstAsNative()
    {
        var chain = Chain(ComparisonOperator.GreaterThanOrEqual, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Ordering(OperatorLoweringKind.TypeParameterCompareTo), Ordering(OperatorLoweringKind.TypeParameterCompareTo)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be("a.CompareTo(b) >= 0 && b.CompareTo(c) >= 0");
    }

    [Fact]
    public void ComparisonChain_MixedLinks_EachLinkFollowsItsOwnTag()
    {
        // `a < b == c` with an ordinal ordering link and a native equality link.
        var chain = new ComparisonChain
        {
            Operands = ImmutableArray.Create<Expression>(Id("a"), Id("b"), Id("c")),
            Operators = ImmutableArray.Create(ComparisonOperator.LessThan, ComparisonOperator.Equal),
        };
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Ordering(OperatorLoweringKind.StringOrdinalCompare), Equality(BinaryOpLowering.NativeOperator)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be("string.Compare(a, b, System.StringComparison.Ordinal) < 0 && b == c");
    }

    [Theory]
    [InlineData(BinaryOpLowering.NativeOperator, "a == b && b == c")]
    [InlineData(BinaryOpLowering.EqualsCallInstance, "a.Equals(b) && b.Equals(c)")]
    [InlineData(BinaryOpLowering.EqualsCallStatic, "object.Equals(a, b) && object.Equals(b, c)")]
    public void ComparisonChain_EqualityLinks_FollowRecordedStrategy_SameAst(BinaryOpLowering strategy, string expected)
    {
        var chain = Chain(ComparisonOperator.Equal, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(Equality(strategy), Equality(strategy)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be(expected);
    }

    [Fact]
    public void ComparisonChain_NotEqualEqualsCallLink_WrapsInNegation()
    {
        var chain = Chain(ComparisonOperator.NotEqual, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Equality(BinaryOpLowering.EqualsCallInstance), Equality(BinaryOpLowering.EqualsCallInstance)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be("!(a.Equals(b)) && !(b.Equals(c))");
    }

    [Fact]
    public void ComparisonChain_NoneCheckLinks_EmitNullPatterns_OnEitherSide()
    {
        // `a == None != c`: None on the right of link 0, on the left of link 1.
        var chain = new ComparisonChain
        {
            Operands = ImmutableArray.Create<Expression>(Id("a"), new NoneLiteral(), Id("c")),
            Operators = ImmutableArray.Create(ComparisonOperator.Equal, ComparisonOperator.NotEqual),
        };
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Equality(BinaryOpLowering.NoneCheck), Equality(BinaryOpLowering.NoneCheck)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be("a is null && c is not null");
    }

    [Fact]
    public void ComparisonChain_EqualityComparerDefaultLink_NamesTheRecordedComparandType()
    {
        var chain = Chain(ComparisonOperator.Equal, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetExpressionType(chain.Operands[0], new TypeParameterType { Name = "T" });
        info.SetExpressionType(chain.Operands[1], new TypeParameterType { Name = "T" });
        info.SetComparisonChainLowering(chain, Links(
            Equality(BinaryOpLowering.EqualityComparerDefault), Equality(BinaryOpLowering.EqualityComparerDefault)));
        _context.SemanticInfo = info;

        Emit(chain).Should().Be(
            "EqualityComparer<T>.Default.Equals(a, b) && EqualityComparer<T>.Default.Equals(b, c)");
    }

    [Fact]
    public void ComparisonChain_WithoutRecordedLowering_Throws()
    {
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), Id("b"), Id("c"));
        _context.SemanticInfo = new SemanticInfo();

        var act = () => Emit(chain);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No ComparisonChainLowering recorded*");
    }

    [Fact]
    public void ComparisonChain_LinkCountMismatch_Throws()
    {
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(Ordering(OperatorLoweringKind.Native)));
        _context.SemanticInfo = info;

        var act = () => Emit(chain);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*1 link(s) for a chain with 2 operator(s)*");
    }

    [Fact]
    public void ComparisonChain_NonTrivialIntermediate_IsCapturedOnce_AndLoweredOnBothSides()
    {
        // "a" < f() < "z" — the call is captured into a temp; both links read the ordinal tag.
        var call = new FunctionCall { Function = Id("f"), Arguments = ImmutableArray<Expression>.Empty };
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), call, Id("z"));
        var info = new SemanticInfo();
        info.SetComparisonChainLowering(chain, Links(
            Ordering(OperatorLoweringKind.StringOrdinalCompare), Ordering(OperatorLoweringKind.StringOrdinalCompare)));
        _context.SemanticInfo = info;

        var code = Emit(chain);

        code.Should().Contain("is var __cmp_");
        code.Should().Contain("string.Compare(a, (f()");
        code.Should().Contain("string.Compare(__cmp_");
        code.Should().Contain(", z, System.StringComparison.Ordinal) < 0");
    }

    /// <summary>
    /// Binary-vs-chain parity at the emitter: the binary <c>a &lt; b</c> under a tag and chain link 0
    /// under the same tag spell the same C# (they share <c>GenerateLoweredComparison</c>).
    /// </summary>
    [Theory]
    [InlineData(OperatorLoweringKind.Native)]
    [InlineData(OperatorLoweringKind.StringOrdinalCompare)]
    [InlineData(OperatorLoweringKind.TypeParameterCompareTo)]
    public void BinaryOrderingAndChainLink_SameTag_EmitIdenticalComparison(OperatorLoweringKind kind)
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.LessThan, Right = Id("b") };
        var chain = Chain(ComparisonOperator.LessThan, Id("a"), Id("b"), Id("c"));
        var info = new SemanticInfo();
        if (kind != OperatorLoweringKind.Native)
            info.SetOperatorLowering(binOp, new OperatorLowering(kind));
        info.SetComparisonChainLowering(chain, Links(Ordering(kind), Ordering(kind)));
        _context.SemanticInfo = info;

        var binary = Emit(binOp);
        var chained = Emit(chain);

        chained.Should().StartWith(binary + " && ");
    }

    #endregion

    #region String repeat (#1623)

    [Fact]
    public void Multiply_StringRepeatStrLeft_EmitsRepeatStringFirst()
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.Multiply, Right = Id("b") };
        var info = new SemanticInfo();
        info.SetOperatorLowering(binOp, new OperatorLowering(OperatorLoweringKind.StringRepeatStrLeft));
        _context.SemanticInfo = info;

        Emit(binOp).Should().Be("global::Sharpy.StringHelpers.Repeat(a, b)");
    }

    [Fact]
    public void Multiply_StringRepeatStrRight_SameAst_SwapsTheArguments()
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.Multiply, Right = Id("b") };
        var info = new SemanticInfo();
        info.SetOperatorLowering(binOp, new OperatorLowering(OperatorLoweringKind.StringRepeatStrRight));
        _context.SemanticInfo = info;

        Emit(binOp).Should().Be("global::Sharpy.StringHelpers.Repeat(b, a)");
    }

    [Fact]
    public void Multiply_WithoutTag_EmitsNativeMultiply()
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.Multiply, Right = Id("b") };
        _context.SemanticInfo = new SemanticInfo();

        Emit(binOp).Should().Be("a * b");
    }

    #endregion

    #region Power (#1623)

    [Theory]
    [InlineData(OperatorLoweringKind.IntegerPowInt, "global::Sharpy.Builtins.CheckedIntPow((int)(a), (int)(b))")]
    [InlineData(OperatorLoweringKind.IntegerPowLong, "global::Sharpy.Builtins.CheckedIntPow((long)(a), (long)(b))")]
    [InlineData(OperatorLoweringKind.FloatPow, "global::System.Math.Pow(a, b)")]
    [InlineData(OperatorLoweringKind.DecimalPow, "global::System.Math.Pow((double)(a), (double)(b))")]
    public void Power_SameAst_FollowsTheRecordedFamily(OperatorLoweringKind kind, string expected)
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.Power, Right = Id("b") };
        var info = new SemanticInfo();
        info.SetOperatorLowering(binOp, new OperatorLowering(kind));
        _context.SemanticInfo = info;

        Emit(binOp).Should().Be(expected);
    }

    [Fact]
    public void Power_WithoutTag_Throws()
    {
        var binOp = new BinaryOp { Left = Id("a"), Operator = BinaryOperator.Power, Right = Id("b") };
        _context.SemanticInfo = new SemanticInfo();

        var act = () => Emit(binOp);

        act.Should().Throw<InvalidOperationException>().WithMessage("*No power lowering recorded*");
    }

    #endregion

    #region Negated integer literal (#1304, #1623)

    private static UnaryOp Negated(string literal)
        => new() { Operator = UnaryOperator.Minus, Operand = new IntegerLiteral { Value = literal } };

    [Fact]
    public void NegatedLiteral_IntTag_EmitsIntMinValueLiteral()
    {
        var unary = Negated("2147483648");
        var info = new SemanticInfo();
        info.SetOperatorLowering(unary, new OperatorLowering(OperatorLoweringKind.NegateLiteralInt));
        _context.SemanticInfo = info;

        Emit(unary).Should().Be("-2147483648");
    }

    [Fact]
    public void NegatedLiteral_LongTag_SameAst_EmitsLongLiteral()
    {
        var unary = Negated("2147483648");
        var info = new SemanticInfo();
        info.SetOperatorLowering(unary, new OperatorLowering(OperatorLoweringKind.NegateLiteralLong));
        _context.SemanticInfo = info;

        Emit(unary).Should().Be("-2147483648L");
    }

    [Fact]
    public void NegatedLiteral_LongMinValue_EmitsLongMinValueLiteral()
    {
        var unary = Negated("9223372036854775808");
        var info = new SemanticInfo();
        info.SetOperatorLowering(unary, new OperatorLowering(OperatorLoweringKind.NegateLiteralLong));
        _context.SemanticInfo = info;

        Emit(unary).Should().Be("-9223372036854775808L");
    }

    [Fact]
    public void NegatedLiteral_WithoutTag_TakesTheOrdinaryUnaryMinusPath()
    {
        var unary = Negated("5");
        _context.SemanticInfo = new SemanticInfo();

        Emit(unary).Should().Be("-5");
    }

    #endregion

    #region Iteration source (#1623)

    private string EmitFor(Expression iterator, SemanticInfo info)
    {
        _context.SemanticInfo = info;
        var forStmt = new ForStatement
        {
            Target = Id("x"),
            Iterator = iterator,
            Body = ImmutableArray.Create<Statement>(new PassStatement()),
        };
        var method = typeof(RoslynEmitter).GetMethod("GenerateBodyStatement", BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            return ((StatementSyntax)method.Invoke(_emitter, new object[] { forStmt })!).NormalizeWhitespace().ToFullString();
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    private string EmitComprehensionIterator(Expression iterator, SemanticInfo info)
    {
        _context.SemanticInfo = info;
        var method = typeof(RoslynEmitter).GetMethod("GenerateComprehensionIterator", BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            return ((ExpressionSyntax)method.Invoke(_emitter, new object[] { iterator })!).NormalizeWhitespace().ToFullString();
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            throw tie.InnerException;
        }
    }

    private static SemanticInfo EnumIteratorInfo(Expression iterator, IterationLoweringKind kind)
    {
        var info = new SemanticInfo();
        info.SetExpressionType(iterator, new UserDefinedType { Name = "Color" });
        info.SetIterationLowering(iterator, new IterationLowering(kind));
        return info;
    }

    [Fact]
    public void For_EnumValuesTag_EmitsEnumGetValues()
    {
        var iterator = Id("Color");
        EmitFor(iterator, EnumIteratorInfo(iterator, IterationLoweringKind.EnumValues))
            .Should().Contain("Enum.GetValues<Color>()");
    }

    [Fact]
    public void For_StringEnumValuesTag_SameAst_EmitsValuesMember()
    {
        var iterator = Id("Color");
        var code = EmitFor(iterator, EnumIteratorInfo(iterator, IterationLoweringKind.StringEnumValues));
        code.Should().Contain("Color.Values");
        code.Should().NotContain("GetValues");
    }

    [Fact]
    public void For_StringCharsTag_EmitsIterateHelper()
    {
        // A str iterator is an ordinary local (spelled as such), not a type name.
        var iterator = Id("s");
        var info = new SemanticInfo();
        info.SetIterationLowering(iterator, new IterationLowering(IterationLoweringKind.StringChars));
        EmitFor(iterator, info).Should().Contain("global::Sharpy.StringHelpers.Iterate(s)");
    }

    [Fact]
    public void For_WithoutTag_IteratesTheExpressionAsIs()
    {
        var iterator = Id("items");
        var code = EmitFor(iterator, new SemanticInfo());
        code.Should().Contain("in items");
        code.Should().NotContain("GetValues");
        code.Should().NotContain("Iterate");
    }

    [Theory]
    [InlineData(IterationLoweringKind.EnumValues, "Enum.GetValues<Color>()")]
    [InlineData(IterationLoweringKind.StringEnumValues, "Color.Values")]
    public void ComprehensionIterator_SameAst_FollowsTheEnumTag(IterationLoweringKind kind, string expected)
    {
        var iterator = Id("Color");
        EmitComprehensionIterator(iterator, EnumIteratorInfo(iterator, kind)).Should().Be(expected);
    }

    [Fact]
    public void ComprehensionIterator_StringCharsTag_EmitsIterateHelper()
    {
        var iterator = Id("s");
        var info = new SemanticInfo();
        info.SetIterationLowering(iterator, new IterationLowering(IterationLoweringKind.StringChars));
        EmitComprehensionIterator(iterator, info).Should().Be("global::Sharpy.StringHelpers.Iterate(s)");
    }

    [Fact]
    public void EnumIteration_WithoutRecordedExpressionType_Throws()
    {
        var iterator = Id("Color");
        var info = new SemanticInfo();
        info.SetIterationLowering(iterator, new IterationLowering(IterationLoweringKind.EnumValues));

        var act = () => EmitComprehensionIterator(iterator, info);

        act.Should().Throw<InvalidOperationException>().WithMessage("*No expression type recorded*");
    }

    #endregion
}

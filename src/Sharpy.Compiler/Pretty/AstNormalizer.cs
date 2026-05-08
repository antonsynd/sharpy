using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

public sealed class AstNormalizer : AstVisitor<Node>
{
    public static readonly AstNormalizer Instance = new();

    public Module NormalizeModule(Module module) =>
        (Module)Visit(module);

    private static T Zero<T>(T node) where T : Node =>
        (T)(node with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            LeadingTrivia = null,
            TrailingTrivia = null
        });

    public override Node VisitModule(Module node) =>
        Zero(node) with { Body = VisitStatements(node.Body) };

    #region Expressions - Literals

    public override Node VisitIntegerLiteral(IntegerLiteral node) => Zero(node);
    public override Node VisitFloatLiteral(FloatLiteral node) => Zero(node);
    public override Node VisitStringLiteral(StringLiteral node) => Zero(node);
    public override Node VisitBytesLiteral(BytesLiteralExpression node) => Zero(node);

    public override Node VisitFStringLiteral(FStringLiteral node)
    {
        var parts = node.Parts.Select(p => p.Expression != null
            ? new FStringPart { Expression = (Expression)Visit(p.Expression), FormatSpec = p.FormatSpec }
            : p).ToImmutableArray();
        return Zero(node) with { Parts = parts };
    }

    public override Node VisitTStringLiteral(TStringLiteral node)
    {
        var parts = node.Parts.Select(p => p.Expression != null
            ? new FStringPart { Expression = (Expression)Visit(p.Expression), FormatSpec = p.FormatSpec }
            : p).ToImmutableArray();
        return Zero(node) with { Parts = parts };
    }

    public override Node VisitBooleanLiteral(BooleanLiteral node) => Zero(node);
    public override Node VisitNoneLiteral(NoneLiteral node) => Zero(node);
    public override Node VisitEllipsisLiteral(EllipsisLiteral node) => Zero(node);

    #endregion

    #region Expressions - Collections

    public override Node VisitListLiteral(ListLiteral node) =>
        Zero(node) with { Elements = VisitExpressions(node.Elements) };

    public override Node VisitDictLiteral(DictLiteral node) =>
        Zero(node) with
        {
            Entries = node.Entries.Select(e => new DictEntry
            {
                Key = e.Key != null ? (Expression)Visit(e.Key) : null,
                Value = (Expression)Visit(e.Value)
            }).ToImmutableArray()
        };

    public override Node VisitSetLiteral(SetLiteral node) =>
        Zero(node) with { Elements = VisitExpressions(node.Elements) };

    public override Node VisitTupleLiteral(TupleLiteral node) =>
        Zero(node) with { Elements = VisitExpressions(node.Elements) };

    #endregion

    #region Expressions - Comprehensions

    public override Node VisitListComprehension(ListComprehension node) =>
        Zero(node) with { Element = (Expression)Visit(node.Element), Clauses = VisitClauses(node.Clauses) };

    public override Node VisitSetComprehension(SetComprehension node) =>
        Zero(node) with { Element = (Expression)Visit(node.Element), Clauses = VisitClauses(node.Clauses) };

    public override Node VisitDictComprehension(DictComprehension node) =>
        Zero(node) with { Key = (Expression)Visit(node.Key), Value = (Expression)Visit(node.Value), Clauses = VisitClauses(node.Clauses) };

    public override Node VisitDictSpreadComprehension(DictSpreadComprehension node) =>
        Zero(node) with { Spread = (Expression)Visit(node.Spread), Clauses = VisitClauses(node.Clauses) };

    public override Node VisitForClause(ForClause node) =>
        Zero(node) with { Target = (Expression)Visit(node.Target), Iterator = (Expression)Visit(node.Iterator) };

    public override Node VisitIfClause(IfClause node) =>
        Zero(node) with { Condition = (Expression)Visit(node.Condition) };

    #endregion

    #region Expressions - Primaries

    public override Node VisitIdentifier(Identifier node) => Zero(node);

    public override Node VisitMemberAccess(MemberAccess node) =>
        Zero(node) with { Object = (Expression)Visit(node.Object) };

    public override Node VisitIndexAccess(IndexAccess node) =>
        Zero(node) with { Object = (Expression)Visit(node.Object), Index = (Expression)Visit(node.Index) };

    public override Node VisitSliceAccess(SliceAccess node) =>
        Zero(node) with
        {
            Object = (Expression)Visit(node.Object),
            Start = node.Start != null ? (Expression)Visit(node.Start) : null,
            Stop = node.Stop != null ? (Expression)Visit(node.Stop) : null,
            Step = node.Step != null ? (Expression)Visit(node.Step) : null
        };

    public override Node VisitFunctionCall(FunctionCall node) =>
        Zero(node) with
        {
            Function = (Expression)Visit(node.Function),
            Arguments = VisitExpressions(node.Arguments),
            KeywordArguments = NormalizeKeywordArgs(node.KeywordArguments)
        };

    #endregion

    #region Expressions - Operators

    public override Node VisitUnaryOp(UnaryOp node) =>
        Zero(node) with { Operand = (Expression)Visit(node.Operand) };

    public override Node VisitBinaryOp(BinaryOp node) =>
        Zero(node) with { Left = (Expression)Visit(node.Left), Right = (Expression)Visit(node.Right), OperatorLine = 0, OperatorColumn = 0 };

    public override Node VisitComparisonChain(ComparisonChain node) =>
        Zero(node) with
        {
            Operands = VisitExpressions(node.Operands),
            OperatorPositions = node.OperatorPositions.Select(_ => (0, 0)).ToImmutableArray()
        };

    #endregion

    #region Expressions - Advanced

    public override Node VisitConditionalExpression(ConditionalExpression node) =>
        Zero(node) with { Test = (Expression)Visit(node.Test), ThenValue = (Expression)Visit(node.ThenValue), ElseValue = (Expression)Visit(node.ElseValue) };

    public override Node VisitLambdaExpression(LambdaExpression node) =>
        Zero(node) with
        {
            Parameters = NormalizeParameters(node.Parameters),
            Body = (Expression)Visit(node.Body),
            ReturnType = NormalizeType(node.ReturnType)
        };

    public override Node VisitTypeCoercion(TypeCoercion node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value), TargetType = NormalizeType(node.TargetType)! };

    public override Node VisitTypeCheck(TypeCheck node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value), CheckType = NormalizeType(node.CheckType)! };

    public override Node VisitParenthesized(Parenthesized node) =>
        Zero(node) with { Expression = (Expression)Visit(node.Expression) };

    public override Node VisitSuperExpression(SuperExpression node) => Zero(node);

    public override Node VisitWalrusExpression(WalrusExpression node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value) };

    public override Node VisitTryExpression(TryExpression node) =>
        Zero(node) with { Operand = (Expression)Visit(node.Operand), ExceptionType = NormalizeType(node.ExceptionType) };

    public override Node VisitMaybeExpression(MaybeExpression node) =>
        Zero(node) with { Operand = (Expression)Visit(node.Operand) };

    public override Node VisitStarExpression(StarExpression node) =>
        Zero(node) with { Operand = (Expression)Visit(node.Operand) };

    public override Node VisitSpreadElement(SpreadElement node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value) };

    public override Node VisitModifiedArgument(ModifiedArgument node) =>
        Zero(node) with { Argument = (Expression)Visit(node.Argument), InlineType = NormalizeType(node.InlineType) };

    public override Node VisitAwaitExpression(AwaitExpression node) =>
        Zero(node) with { Operand = (Expression)Visit(node.Operand) };

    public override Node VisitMatchExpression(MatchExpression node) =>
        Zero(node) with
        {
            Scrutinee = (Expression)Visit(node.Scrutinee),
            Arms = node.Arms.Select(a => new MatchArm
            {
                Pattern = (Pattern)Visit(a.Pattern),
                Guard = a.Guard != null ? (Expression)Visit(a.Guard) : null,
                Result = (Expression)Visit(a.Result)
            }).ToImmutableArray()
        };

    #endregion

    #region Statements - Simple

    public override Node VisitExpressionStatement(ExpressionStatement node) =>
        Zero(node) with { Expression = (Expression)Visit(node.Expression) };

    public override Node VisitAssignment(Assignment node) =>
        Zero(node) with { Target = (Expression)Visit(node.Target), Value = (Expression)Visit(node.Value) };

    public override Node VisitVariableDeclaration(VariableDeclaration node) =>
        Zero(node) with
        {
            Type = NormalizeType(node.Type),
            InitialValue = node.InitialValue != null ? (Expression)Visit(node.InitialValue) : null,
            Decorators = NormalizeDecorators(node.Decorators),
            NameLineStart = 0,
            NameColumnStart = 0
        };

    public override Node VisitAssertStatement(AssertStatement node) =>
        Zero(node) with { Test = (Expression)Visit(node.Test), Message = node.Message != null ? (Expression)Visit(node.Message) : null };

    public override Node VisitPassStatement(PassStatement node) => Zero(node);
    public override Node VisitBreakStatement(BreakStatement node) => Zero(node);
    public override Node VisitBreakWithFlagStatement(BreakWithFlagStatement node) => Zero(node);
    public override Node VisitContinueStatement(ContinueStatement node) => Zero(node);

    public override Node VisitReturnStatement(ReturnStatement node) =>
        Zero(node) with { Value = node.Value != null ? (Expression)Visit(node.Value) : null };

    public override Node VisitYieldStatement(YieldStatement node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value) };

    public override Node VisitRaiseStatement(RaiseStatement node) =>
        Zero(node) with
        {
            Exception = node.Exception != null ? (Expression)Visit(node.Exception) : null,
            Cause = node.Cause != null ? (Expression)Visit(node.Cause) : null
        };

    #endregion

    #region Statements - Compound

    public override Node VisitIfStatement(IfStatement node) =>
        Zero(node) with
        {
            Test = (Expression)Visit(node.Test),
            ThenBody = VisitStatements(node.ThenBody),
            ElifClauses = node.ElifClauses.Select(e => new ElifClause
            {
                Test = (Expression)Visit(e.Test),
                Body = VisitStatements(e.Body)
            }).ToImmutableArray(),
            ElseBody = VisitStatements(node.ElseBody)
        };

    public override Node VisitWhileStatement(WhileStatement node) =>
        Zero(node) with { Test = (Expression)Visit(node.Test), Body = VisitStatements(node.Body), ElseBody = VisitStatements(node.ElseBody) };

    public override Node VisitForStatement(ForStatement node) =>
        Zero(node) with
        {
            Target = (Expression)Visit(node.Target),
            Iterator = (Expression)Visit(node.Iterator),
            Body = VisitStatements(node.Body),
            ElseBody = VisitStatements(node.ElseBody)
        };

    public override Node VisitTryStatement(TryStatement node) =>
        Zero(node) with
        {
            Body = VisitStatements(node.Body),
            Handlers = node.Handlers.Select(h => new ExceptHandler
            {
                ExceptionType = NormalizeType(h.ExceptionType),
                Name = h.Name,
                IsExceptStar = h.IsExceptStar,
                Filter = h.Filter != null ? (Expression)Visit(h.Filter) : null,
                Body = VisitStatements(h.Body)
            }).ToImmutableArray(),
            ElseBody = VisitStatements(node.ElseBody),
            FinallyBody = VisitStatements(node.FinallyBody)
        };

    public override Node VisitWithStatement(WithStatement node) =>
        Zero(node) with
        {
            Items = node.Items.Select(item => new WithItem
            {
                ContextExpression = (Expression)Visit(item.ContextExpression),
                Name = item.Name
            }).ToImmutableArray(),
            Body = VisitStatements(node.Body)
        };

    #endregion

    #region Statements - Definitions

    public override Node VisitFunctionDef(FunctionDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            Parameters = NormalizeParameters(node.Parameters),
            ReturnType = NormalizeType(node.ReturnType),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    public override Node VisitClassDef(ClassDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            BaseClasses = NormalizeTypes(node.BaseClasses),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    public override Node VisitStructDef(StructDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            BaseClasses = NormalizeTypes(node.BaseClasses),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    public override Node VisitInterfaceDef(InterfaceDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            BaseInterfaces = NormalizeTypes(node.BaseInterfaces),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    public override Node VisitEnumDef(EnumDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            Decorators = NormalizeDecorators(node.Decorators),
            Members = node.Members.Select(m => new EnumMember
            {
                Name = m.Name,
                Value = m.Value != null ? (Expression)Visit(m.Value) : null
            }).ToImmutableArray()
        };

    public override Node VisitTypeAlias(TypeAlias node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            Type = NormalizeType(node.Type),
            FunctionType = node.FunctionType != null ? NormalizeFuncType(node.FunctionType) : null
        };

    public override Node VisitPropertyDef(PropertyDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            Type = NormalizeType(node.Type),
            ReturnType = NormalizeType(node.ReturnType),
            DefaultValue = node.DefaultValue != null ? (Expression)Visit(node.DefaultValue) : null,
            Parameters = NormalizeParameters(node.Parameters),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    #endregion

    #region Statements - Imports

    public override Node VisitImportStatement(ImportStatement node) =>
        Zero(node) with { Names = NormalizeImportAliases(node.Names) };

    public override Node VisitFromImportStatement(FromImportStatement node) =>
        Zero(node) with { Names = NormalizeImportAliases(node.Names) };

    #endregion

    #region Statements - Future

    public override Node VisitMatchStatement(MatchStatement node) =>
        Zero(node) with
        {
            Scrutinee = (Expression)Visit(node.Scrutinee),
            Cases = node.Cases.Select(c => new MatchCase
            {
                Pattern = (Pattern)Visit(c.Pattern),
                Guard = c.Guard != null ? (Expression)Visit(c.Guard) : null,
                Body = VisitStatements(c.Body)
            }).ToImmutableArray()
        };

    public override Node VisitUnionDef(UnionDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            Decorators = NormalizeDecorators(node.Decorators),
            Cases = node.Cases.Select(c => new UnionCaseDef
            {
                Name = c.Name,
                Fields = c.Fields.Select(f => new UnionCaseField
                {
                    Name = f.Name,
                    Type = NormalizeType(f.Type)!
                }).ToImmutableArray()
            }).ToImmutableArray()
        };

    public override Node VisitDelegateDef(DelegateDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            TypeParameters = NormalizeTypeParameters(node.TypeParameters),
            Parameters = NormalizeParameters(node.Parameters),
            ReturnType = NormalizeType(node.ReturnType)
        };

    public override Node VisitEventDef(EventDef node) =>
        Zero(node) with
        {
            NameLineStart = 0,
            NameColumnStart = 0,
            Type = NormalizeType(node.Type),
            Parameters = NormalizeParameters(node.Parameters),
            Body = VisitStatements(node.Body),
            Decorators = NormalizeDecorators(node.Decorators)
        };

    #endregion

    #region Patterns

    public override Node VisitWildcardPattern(WildcardPattern node) => Zero(node);

    public override Node VisitBindingPattern(BindingPattern node) =>
        Zero(node) with { Name = (Identifier)Visit(node.Name), Type = NormalizeType(node.Type) };

    public override Node VisitLiteralPattern(LiteralPattern node) =>
        Zero(node) with { Literal = (Expression)Visit(node.Literal) };

    public override Node VisitTypePattern(TypePattern node) =>
        Zero(node) with { Type = NormalizeType(node.Type)!, BindingName = node.BindingName != null ? (Identifier)Visit(node.BindingName) : null };

    public override Node VisitUnionCasePattern(UnionCasePattern node) =>
        Zero(node) with { UnionType = NormalizeType(node.UnionType), FieldPatterns = VisitPatterns(node.FieldPatterns) };

    public override Node VisitTuplePattern(TuplePattern node) =>
        Zero(node) with { Elements = VisitPatterns(node.Elements) };

    public override Node VisitListPattern(ListPattern node) =>
        Zero(node) with { Elements = VisitPatterns(node.Elements), RestPattern = node.RestPattern != null ? (Pattern)Visit(node.RestPattern) : null };

    public override Node VisitOrPattern(OrPattern node) =>
        Zero(node) with { Alternatives = VisitPatterns(node.Alternatives) };

    public override Node VisitAndPattern(AndPattern node) =>
        Zero(node) with { Left = (Pattern)Visit(node.Left), Right = (Pattern)Visit(node.Right) };

    public override Node VisitGuardPattern(GuardPattern node) =>
        Zero(node) with { Inner = (Pattern)Visit(node.Inner), Guard = (Expression)Visit(node.Guard) };

    public override Node VisitMemberAccessPattern(MemberAccessPattern node) => Zero(node);

    public override Node VisitRelationalPattern(RelationalPattern node) =>
        Zero(node) with { Value = (Expression)Visit(node.Value) };

    public override Node VisitPropertyPatternField(PropertyPatternField node) =>
        Zero(node) with { Pattern = (Pattern)Visit(node.Pattern) };

    public override Node VisitPropertyPattern(PropertyPattern node) =>
        Zero(node) with { Type = NormalizeType(node.Type), Fields = node.Fields.Select(f => (PropertyPatternField)Visit(f)).ToImmutableArray() };

    public override Node VisitPositionalPattern(PositionalPattern node) =>
        Zero(node) with { Type = NormalizeType(node.Type), Elements = VisitPatterns(node.Elements) };

    #endregion

    #region Helpers

    private ImmutableArray<Statement> VisitStatements(ImmutableArray<Statement> stmts) =>
        stmts.Select(s => (Statement)Visit(s)).ToImmutableArray();

    private ImmutableArray<Expression> VisitExpressions(ImmutableArray<Expression> exprs) =>
        exprs.Select(e => (Expression)Visit(e)).ToImmutableArray();

    private ImmutableArray<Pattern> VisitPatterns(ImmutableArray<Pattern> patterns) =>
        patterns.Select(p => (Pattern)Visit(p)).ToImmutableArray();

    private ImmutableArray<ComprehensionClause> VisitClauses(ImmutableArray<ComprehensionClause> clauses) =>
        clauses.Select(c => (ComprehensionClause)Visit(c)).ToImmutableArray();

    private static TypeAnnotation? NormalizeType(TypeAnnotation? type)
    {
        if (type == null)
            return null;
        return type with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            TypeArguments = type.TypeArguments.Select(t => NormalizeType(t)!).ToImmutableArray(),
            ErrorType = NormalizeType(type.ErrorType)
        };
    }

    private static ImmutableArray<TypeAnnotation> NormalizeTypes(ImmutableArray<TypeAnnotation> types) =>
        types.Select(t => NormalizeType(t)!).ToImmutableArray();

    private static FunctionType NormalizeFuncType(FunctionType ft) =>
        ft with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            ParameterTypes = ft.ParameterTypes.Select(t => NormalizeType(t)!).ToImmutableArray(),
            ReturnType = NormalizeType(ft.ReturnType)!
        };

    private ImmutableArray<Parameter> NormalizeParameters(ImmutableArray<Parameter> parameters) =>
        parameters.Select(p => p with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            Type = NormalizeType(p.Type),
            DefaultValue = p.DefaultValue != null ? (Expression)Visit(p.DefaultValue) : null
        }).ToImmutableArray();

    private static ImmutableArray<TypeParameterDef> NormalizeTypeParameters(ImmutableArray<TypeParameterDef> tps) =>
        tps.Select(tp => tp with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            DefaultType = NormalizeType(tp.DefaultType),
            Constraints = tp.Constraints.Select(NormalizeConstraint).ToImmutableArray()
        }).ToImmutableArray();

    private static ConstraintClause NormalizeConstraint(ConstraintClause c) =>
        c is TypeConstraint tc ? tc with { Type = NormalizeType(tc.Type)! } : c;

    private ImmutableArray<Decorator> NormalizeDecorators(ImmutableArray<Decorator> decorators) =>
        decorators.Select(d => d with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Span = null,
            Arguments = VisitExpressions(d.Arguments),
            KeywordArguments = NormalizeKeywordArgs(d.KeywordArguments)
        }).ToImmutableArray();

    private ImmutableArray<KeywordArgument> NormalizeKeywordArgs(ImmutableArray<KeywordArgument> kwargs) =>
        kwargs.Select(k => k with
        {
            LineStart = 0,
            ColumnStart = 0,
            LineEnd = 0,
            ColumnEnd = 0,
            Value = (Expression)Visit(k.Value)
        }).ToImmutableArray();

    private static ImmutableArray<ImportAlias> NormalizeImportAliases(ImmutableArray<ImportAlias> aliases) =>
        aliases.Select(a => a with { LineStart = 0, ColumnStart = 0, LineEnd = 0, ColumnEnd = 0, Span = null }).ToImmutableArray();

    #endregion
}
